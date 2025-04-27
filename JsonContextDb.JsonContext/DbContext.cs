using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace JsonContextDb.JsonContext;

/// <summary>
/// A lightweight, JSON-based data context that emulates Entity Framework Core behavior.
/// Provides thread-safe methods to query and modify entities stored in JSON files, suitable for small datasets.
/// Entities are managed in-memory and persisted to JSON files on disk.
/// </summary>
/// <remarks>
/// Designed to be used as a singleton to minimize file I/O, loading JSON files once at initialization.
/// All in-memory operations are thread-safe using a lock. File reads are synchronous for simplicity, while file writes
/// are asynchronous to avoid blocking. The context mimics EF Core's API, including <see cref="Set{T}"/> for querying
/// and <see cref="SaveChangesAsync"/> for persisting changes. Entities must have an integer <c>Id</c> property.
/// Copyright (c) 2025 Alphons van der Heijden. All rights reserved.
/// </remarks>
public class DbContext(string? dataDirectory, DbContextOptions? options = null)
{
	// Directory where JSON files are stored, required and validated.
	private readonly string dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));

	// Configuration options for serialization and file naming.
	private readonly DbContextOptions jsonContextOptions = options ?? new DbContextOptions();

	// In-memory storage of entity lists, keyed by entity type.
	private readonly Dictionary<Type, IList> entityLists = [];

	// Tracks pending changes (add, update, remove) for SaveChangesAsync.
	private readonly List<(Type Type, object Entity, ActionType Action)> changes = [];

	// Stores serialized snapshots of entities for change tracking, using SHA-256 hashes to detect modifications.
	private readonly Dictionary<object, byte[]> snapshots = [];

	// Synchronization object for thread-safe operations.
	private readonly object lockObject = new();

	// Cache of compiled ID accessors for entities, improving performance.
	private static readonly Dictionary<Type, Func<object, int>> IdAccessors = [];

	public enum ActionType
	{
		Unknown,
		Add,
		Update,
		Remove
	}

	/// <summary>
	/// Loads the list of entities of type <typeparamref name="T"/> from the corresponding JSON file.
	/// </summary>
	/// <typeparam name="T">The type of entity, which must be a class with an integer <c>Id</c> property.</typeparam>
	/// <returns>A <see cref="List{T}"/> containing the deserialized entities, or an empty list if the file does not exist.</returns>
	/// <remarks>
	/// The JSON file is named using the <see cref="DbContextOptions.FileNameFactory"/> and located in the directory
	/// specified during construction. If the file does not exist or is corrupted, an empty list is returned or an exception
	/// is thrown. This method uses synchronous file I/O for simplicity and is called by <see cref="Set{T}"/> to initialize
	/// the in-memory entity list.
	/// </remarks>
	private List<T> LoadEntities<T>() where T : class
	{
		var jsonFilePath = Path.Combine(dataDirectory, jsonContextOptions.FileNameFactory(typeof(T)));

		var fi = new FileInfo(jsonFilePath);

		if (!fi.Exists)
			return [];

		try
		{
			using var stream = fi.OpenRead();

			var entities = JsonSerializer.Deserialize<List<T>>(stream, jsonContextOptions.SerializerOptions) ?? [];

			lock (lockObject)
			{
				foreach (var entity in entities)
				{
					snapshots[entity] = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
				}
			}

			return entities;
		}
		catch (JsonException ex)
		{
			throw new InvalidOperationException($"Failed to deserialize JSON file '{jsonFilePath}'.", ex);
		}
	}


	/// <summary>
	/// Computes a SHA-256 hash of the serialized entity for change tracking.
	/// </summary>
	/// <param name="entity">The entity to hash.</param>
	/// <param name="options">The JSON serialization options.</param>
	/// <returns>A 32-byte hash of the serialized entity.</returns>
	private static byte[] ComputeSHA256Hash(object entity, JsonSerializerOptions options)
		=> SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entity, options)); // 32 bytes output

	// Retrieves a queryable set of entities of type T
	public DbSet<T> Set<T>() where T : class => new(this);

	internal List<T> GetList<T>() where T : class
	{
		if (!entityLists.ContainsKey(typeof(T)))
			entityLists[typeof(T)] = LoadEntities<T>();
		return entityLists[typeof(T)] as List<T> ?? throw new Exception($"LoadEntities returned null on {nameof(T)}");
	}

	private IList GetList(Type type)
	{
		if (!entityLists.TryGetValue(type, out IList? list))
			throw new Exception($"Collection {type} disapeared");
		return list;
	}

	/// <summary>
	/// Retrieves the <c>Id</c> property value from an entity.
	/// </summary>
	/// <param name="entity">The entity from which to retrieve the <c>Id</c>.</param>
	/// <returns>The integer value of the <c>Id</c> property.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the entity lacks an <c>Id</c> property or the <c>Id</c> is null.</exception>
	/// <remarks>
	/// This method uses a cached accessor to avoid repeated reflection. It assumes all entities have an integer
	/// <c>Id</c> property, which is required for matching entities in the in-memory list.
	/// </remarks>
	private static int GetId(object entity)
	{
		var type = entity.GetType();
		// Retrieve or create a cached accessor for the Id property.
		if (!IdAccessors.TryGetValue(type, out var accessor))
		{
			var idProperty = type.GetProperty("Id") ?? throw new InvalidOperationException("Entity must have an Id property.");
			accessor = (obj) => (int)(idProperty.GetValue(obj) ?? throw new InvalidOperationException("Id cannot be null."));
			IdAccessors[type] = accessor;
		}
		return accessor(entity);
	}

	/// <summary>
	/// Retrieves or creates the <see cref="MetaData"/> for an entity type and returns the next available ID.
	/// Updates the <see cref="MetaData"/> directly in the in-memory list.
	/// </summary>
	/// <returns>The next available ID for the entity type.</returns>
	/// <remarks>
	/// This method is thread-safe, ensuring that ID generation is atomic and consistent across concurrent calls.
	/// If no <see cref="MetaData"/> exists for the entity type, a new entry is created with an initial ID of 1.
	/// </remarks>
	private int GetNextIdForEntityType(Type t)
	{
		var typeKey = t.Name;
		var metaDataList = GetList<MetaData>();

		var metaData = metaDataList.FirstOrDefault(m => m.EntityType == typeKey);

		if (metaData == null)
		{
			metaData = new MetaData
			{
				Id = metaDataList.Count > 0 ? metaDataList.Max(m => m.Id) + 1 : 1,
				EntityType = typeKey,
				NextId = 1
			};
			metaDataList.Add(metaData);
		}

		int nextId = metaData.NextId;
		metaData.NextId++;

		return nextId;

	}

	private static void SetId(object entity, int id)
	{
		var type = entity.GetType();
		var idProperty = type.GetProperty("Id") ?? throw new InvalidOperationException("Entity must have an Id property.");
		idProperty.SetValue(entity, id);
	}

	/// <summary>
	/// Marks an entity for addition to the data context, assigning a new ID if none is set.
	/// </summary>
	/// <param name="entity">The entity to add.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="entity"/> is null.</exception>
	/// <remarks>
	/// This method is thread-safe, using a lock to ensure atomic operations. If the entity's <c>Id</c> is 0,
	/// a new ID is generated using <see cref="GetNextIdForEntityType{T}"/>. A snapshot is created for change tracking.
	/// Only accessible via <see cref="JsonSet{T}"/>.
	/// </remarks>
	internal void Add<T>(T entity) where T : class
	{
		ArgumentNullException.ThrowIfNull(entity);
		lock (lockObject)
		{
			//var list = GetList<T>();
			//list.Add(entity);

			changes.Add((typeof(T), entity, ActionType.Add));
			snapshots[entity] = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
		}
	}

	/// <summary>
	/// Marks multiple entities for addition to the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
	/// <param name="entities">The entities to add.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="entities"/> or any entity is null.</exception>
	internal void AddRange<T>(IEnumerable<T> entities) where T : class
	{
		ArgumentNullException.ThrowIfNull(entities);
		lock (lockObject)
		{
			//var list = GetList<T>();

			foreach (var entity in entities)
			{
				ArgumentNullException.ThrowIfNull(entity);
				//list.Add(entity);
				changes.Add((typeof(T), entity, ActionType.Add));
				snapshots[entity] = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
			}
		}
	}

	/// <summary>
	/// Marks an entity for update in the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
	/// <param name="entity">The entity to update.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="entity"/> is null.</exception>
	internal void Update<T>(T entity) where T : class
	{
		ArgumentNullException.ThrowIfNull(entity);
		lock (lockObject)
		{
			// Check if entity has changed by comparing serialized snapshots.
			if (snapshots.TryGetValue(entity, out var snapshot))
			{
				var current = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
				if (!snapshot.SequenceEqual(current)) // Only update if changed
				{
					changes.Add((typeof(T), entity, ActionType.Update));
					snapshots[entity] = current;
				}
			}
			else
			{
				// New entity, track for update and store snapshot.
				changes.Add((typeof(T), entity, ActionType.Update));
				snapshots[entity] = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
			}
		}
	}

	/// <summary>
	/// Marks multiple entities for update in the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
	/// <param name="entities">The entities to update.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="entities"/> or any entity is null.</exception>
	internal void UpdateRange<T>(IEnumerable<T> entities) where T : class
	{
		ArgumentNullException.ThrowIfNull(entities);
		lock (lockObject)
		{
			// Update each entity individually.
			foreach (var entity in entities)
			{
				ArgumentNullException.ThrowIfNull(entity);
				Update(entity);
			}
		}
	}

	/// <summary>
	/// Marks an entity for removal from the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
	/// <param name="entity">The entity to remove.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="entity"/> is null.</exception>
	internal void Remove<T>(T entity) where T : class
	{
		ArgumentNullException.ThrowIfNull(entity);
		lock (lockObject)
		{
			// Track entity for removal and remove its snapshot.
			changes.Add((typeof(T), entity, ActionType.Remove));
			snapshots.Remove(entity);
		}
	}

	/// <summary>
	/// Marks multiple entities for removal from the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
	/// <param name="entities">The entities to remove.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="entities"/> or any entity is null.</exception>
	internal void RemoveRange<T>(IEnumerable<T> entities) where T : class
	{
		ArgumentNullException.ThrowIfNull(entities);
		lock (lockObject)
		{
			// Track each entity for removal and remove its snapshot.
			foreach (var entity in entities)
			{
				ArgumentNullException.ThrowIfNull(entity);
				changes.Add((typeof(T), entity, ActionType.Remove));
				snapshots.Remove(entity);
			}
		}
	}

	/// <summary>
	/// Asynchronously saves all pending changes to the JSON files and returns the number of affected entities.
	/// </summary>
	/// <returns>A task representing the asynchronous operation, returning the number of entities successfully added, updated, or removed.</returns>
	/// <remarks>
	/// Applies pending changes to in-memory lists within a lock for thread-safety, then persists to JSON files asynchronously.
	/// If a file write fails (e.g., due to access conflicts or I/O errors), in-memory changes are rolled back to maintain consistency,
	/// and pending changes are preserved for retry. Concurrent modifications to the same entity type may lead to overwrites;
	/// use with caution in high-concurrency scenarios.
	/// </remarks>
	public async Task<int> SaveChangesAsync()
	{
		Dictionary<Type, (string FilePath, string Json)> filesToWrite = [];
		int affectedEntities = 0;
		Dictionary<Type, IList> entityListsBackup;
		HashSet<Type> modifiedTypes;
		List<(Type Type, object Entity, ActionType Action)> changesBackup;

		lock (lockObject)
		{
			changes.Add((typeof(MetaData), new MetaData(), ActionType.Unknown));

			entityListsBackup = entityLists.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			modifiedTypes = [.. changes.Select(c => c.Type)];
			changesBackup = [.. changes];

			foreach (var kvp in entityLists)
			{
				var type = kvp.Key;
				var entities = kvp.Value;
				foreach (var entity in entities)
				{
					if (snapshots.TryGetValue(entity, out var snapshot))
					{
						var current = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
						if (!snapshot.SequenceEqual(current) && !changes.Any(c => c.Entity == entity))
						{
							changes.Add((type, entity, ActionType.Update));
							snapshots[entity] = current;
							modifiedTypes.Add(type);
						}
					}
				}
			}

			foreach (var group in changes.GroupBy(c => c.Type))
			{
				var type = group.Key;
				var entities = GetList(type);

				foreach (var change in group)
				{
					if (change.Action == ActionType.Add)
					{
						SetId(change.Entity, GetNextIdForEntityType(type));

						snapshots[change.Entity] = ComputeSHA256Hash(change.Entity, jsonContextOptions.SerializerOptions);

						entities.Add(change.Entity);

						if (typeof(MetaData) != change.Entity.GetType())
							affectedEntities++;
					}
					else if (change.Action == ActionType.Update)
					{
						var id = GetId(change.Entity);
						var existing = entities.Cast<object>().FirstOrDefault(e => GetId(e) == id);
						if (existing != null)
						{
							entities.Remove(existing);
							entities.Add(change.Entity);
							if (typeof(MetaData) != change.Entity.GetType())
								affectedEntities++;
						}
					}
					else if (change.Action == ActionType.Remove)
					{
						entities.Remove(change.Entity);
						affectedEntities++;
					}
				}

				if (modifiedTypes.Contains(type))
				{
					var jsonFilePath = Path.Combine(dataDirectory, jsonContextOptions.FileNameFactory(type));
					var json = JsonSerializer.Serialize(entities, jsonContextOptions.SerializerOptions);
					filesToWrite[type] = (jsonFilePath, json);
				}
			}
		}

		try
		{
			foreach (var (filePath, json) in filesToWrite.Values)
			{
				var tempFilePath = filePath + ".tmp";
				await File.WriteAllTextAsync(tempFilePath, json);
				File.Move(tempFilePath, filePath, overwrite: true);
			}
			filesToWrite.Clear();
			filesToWrite.TrimExcess();
			changes.Clear();
			changes.TrimExcess();
		}
		catch (Exception ex)
		{
			lock (lockObject)
			{
				entityLists.Clear();
				foreach (var kvp in entityListsBackup)
				{
					entityLists[kvp.Key] = kvp.Value;
				}
				changes.AddRange(changesBackup);
			}
			throw new InvalidOperationException("Failed to save changes. Changes rolled back, pending changes preserved.", ex);
		}

		Debug.WriteLine($"AffectedEntities:{affectedEntities}");
		return affectedEntities;
	}
}


