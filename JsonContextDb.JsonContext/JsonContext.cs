using System.Collections;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.Json;

namespace JsonContextDb.JsonContext;

/// <summary>
/// <license>Copyright (c) 2025 Alphons van der Heijden. All rights reserved.</license>
/// Date: April 2025
/// A lightweight, JSON-based data context that emulates Entity Framework Core behavior.
/// Provides methods to query and modify entities stored in JSON files, with thread-safe operations
/// suitable for small datasets. Entities are managed in-memory and persisted to JSON files on disk.
/// </summary>
/// <remarks>
/// This class is designed to be used as a singleton to minimize file I/O, loading JSON files once at initialization.
/// All in-memory operations are thread-safe using a lock. File reads are synchronous for simplicity, while file writes
/// are asynchronous to avoid blocking during persistence. The context mimics EF Core's API, including <see cref="Set{T}"/>
/// for querying and <see cref="SaveChangesAsync"/> for persisting changes. File operations are abstracted via
/// <see cref="IFileStorage"/> for testability. Entities must have an integer <c>Id</c> property for identification.
/// </remarks>
public class JsonContext(string? dataDirectory, IFileStorage? fileStorage = null, JsonContextOptions? options = null)
{
	// Directory where JSON files are stored, required and validated.
	private readonly string dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));

	// File storage implementation, defaults to FileSystemStorage if null.
	private readonly IFileStorage fileStorage = fileStorage ?? new FileSystemStorage();

	// Configuration options for serialization and file naming.
	private readonly JsonContextOptions jsonContextOptions = options ?? new JsonContextOptions();

	// In-memory storage of entity lists, keyed by entity type.
	private readonly Dictionary<Type, IList> entityLists = [];

	// Tracks pending changes (add, update, remove) for SaveChangesAsync.
	private readonly List<(Type Type, object Entity, ActionType Action)> changes = [];

	// Stores serialized snapshots of entities for change tracking.
	private readonly Dictionary<object, byte[]> snapshots = [];

	// Synchronization object for thread-safe operations.
	private readonly object lockObject = new();

	// Cache of compiled ID accessors for entities, improving performance.
	private static readonly Dictionary<Type, Func<object, int>> IdAccessors = [];

	public enum ActionType
	{
		Add,
		Create,
		Update,
		Delete,
		Remove
	}

	private byte[] ComputeSHA256Hash(object entity, JsonSerializerOptions options)
	{
		byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(entity, options);
		return SHA256.HashData(jsonBytes); // 32 bytes output
	}

	// Retrieves a queryable set of entities of type T, loading from file if not cached.
	public JsonSet<T> Set<T>() where T : class
	{
		lock (lockObject)
		{
			// Return cached entity list if already loaded.
			if (entityLists.TryGetValue(typeof(T), out IList? list) && list is List<T> convertedList)
				return new JsonSet<T>(this, convertedList);
		}

		// Load entities from JSON file.
		List<T> entities = LoadEntities<T>();

		lock (lockObject)
		{
			entityLists[typeof(T)] = entities;
			return new JsonSet<T>(this, entities);
		}
	}

	// Asynchronous version of Set<T>, loading entities from file asynchronously.
	public async Task<JsonSet<T>> SetAsync<T>() where T : class
	{
		lock (lockObject)
		{
			// Return cached entity list if already loaded.
			if (entityLists.TryGetValue(typeof(T), out IList? list) && list is List<T> convertedList)
				return new JsonSet<T>(this, convertedList);
		}

		// Load entities from JSON file asynchronously.
		List<T> entities = await LoadEntitiesAsync<T>();

		lock (lockObject)
		{
			entityLists[typeof(T)] = entities;
			return new JsonSet<T>(this, entities);
		}
	}

	private static int GetIdSafe(object entity)
	{
		var type = entity.GetType();
		var idProperty = type.GetProperty("Id") ?? throw new InvalidOperationException("Entity must have an Id property.");
		return (int?)idProperty.GetValue(entity) ?? 0;
	}

	public class MetaData
	{
		public int Id { get; set; } // Verplicht voor JsonContext
		public string EntityType { get; set; } = string.Empty; // Naam van het entiteitstype (bijv. "Customer")
		public int NextId { get; set; } // Volgende beschikbare ID voor dit type
	}

	/// <summary>
	/// Haalt of maakt de MetaData voor een entiteitstype en retourneert de volgende beschikbare ID.
	/// Werkt de MetaData direct bij in de in-memory lijst.
	/// </summary>
	/// <returns>De volgende beschikbare ID voor het entiteitstype.</returns>
	private int GetNextIdForEntityType<T>() where T : class
	{
		lock (lockObject)
		{
			var typeKey = typeof(T).Name;

			List<MetaData>? metaDataList = null;

			if(!entityLists.ContainsKey(typeof(MetaData)))
				Set<MetaData>();

			metaDataList = entityLists[typeof(MetaData)] as List<MetaData>;

			if (metaDataList == null)
				throw new ArgumentNullException(nameof(metaDataList));

			var metaData = metaDataList.FirstOrDefault(m => m.EntityType == typeKey);

			if (metaData == null)
			{
				metaData = new MetaData
				{
					Id = metaDataList.Count>0 ? metaDataList.Max(m => m.Id) + 1 : 1,
					EntityType = typeKey,
					NextId = 1
				};
				metaDataList.Add(metaData);
			}

			int nextId = metaData.NextId;
			metaData.NextId++;

			return nextId;
		}
	}

	private static void SetId(object entity, int id)
	{
		var type = entity.GetType();
		var idProperty = type.GetProperty("Id") ?? throw new InvalidOperationException("Entity must have an Id property.");
		idProperty.SetValue(entity, id);
	}

	/// <summary>
	/// Marks an entity for addition to the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
	internal void Add<T>(T entity) where T : class
	{
		ArgumentNullException.ThrowIfNull(entity);
		lock (lockObject)
		{
			var currentId = GetIdSafe(entity);
			if (currentId == 0) // Alleen toewijzen als ID niet is ingesteld
			{
				SetId(entity, GetNextIdForEntityType<T>());
			}

			changes.Add((typeof(T), entity, ActionType.Add));
			snapshots[entity] = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
		}
	}

	/// <summary>
	/// Marks multiple entities for addition to the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
	internal void AddRange<T>(IEnumerable<T> entities) where T : class
	{
		ArgumentNullException.ThrowIfNull(entities);
		lock (lockObject)
		{
			foreach (var entity in entities)
			{
				ArgumentNullException.ThrowIfNull(entity);
				var currentId = GetIdSafe(entity);
				if (currentId == 0)
				{
					SetId(entity, GetNextIdForEntityType<T>());
				}

				changes.Add((typeof(T), entity, ActionType.Add));
				snapshots[entity] = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
			}
		}
	}

	/// <summary>
	/// Marks an entity for update in the data context. Only accessible via <see cref="JsonSet{T}"/>.
	/// </summary>
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
	/// <returns>A task that represents the asynchronous operation, with an integer indicating the number of entities
	/// successfully added, updated, or removed.</returns>
	/// <remarks>
	/// Applies pending changes to in-memory lists within a lock, then persists to JSON files asynchronously.
	/// If a file write fails, in-memory changes are rolled back to ensure consistency, and pending changes are preserved for retry.
	/// </remarks>
	public async Task<int> SaveChangesAsync()
	{
		Dictionary<Type, (string FilePath, string Json)> filesToWrite;
		int affectedEntities;
		Dictionary<Type, IList> entityListsBackup;
		HashSet<Type> modifiedTypes;
		List<(Type Type, object Entity, ActionType Action)> changesBackup;

		lock (lockObject)
		{
			changes.Add((typeof(MetaData), new MetaData(), ActionType.Update));

			affectedEntities = 0;
			filesToWrite = [];
			entityListsBackup = entityLists.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			modifiedTypes = [.. changes.Select(c => c.Type)];
			changesBackup = [.. changes];

			// Detecteer wijzigingen in alle entiteiten
			foreach (var kvp in entityLists)
			{
				var type = kvp.Key;
				var entities = kvp.Value.Cast<object>().ToList();
				foreach (var entity in entities)
				{
					if (snapshots.TryGetValue(entity, out var snapshot))
					{
						var current = ComputeSHA256Hash(entity, jsonContextOptions.SerializerOptions);
						if (!snapshot.SequenceEqual(current) && !changes.Any(c => c.Entity == entity))
						{
							// Entiteit is gewijzigd, markeer als Update
							changes.Add((type, entity, ActionType.Update));
							snapshots[entity] = current;
							modifiedTypes.Add(type);
						}
					}
				}
			}

			// Verwerk expliciete veranderingen (Add, Update, Remove)
			foreach (var group in changes.GroupBy(c => c.Type))
			{
				var type = group.Key;
				if (!entityLists.TryGetValue(type, out var list))
				{
					list = new List<object>();
					entityLists[type] = list;
				}
				var entities = list.Cast<object>().ToList();

				foreach (var change in group)
				{
					if (change.Action == ActionType.Add)
					{
						entities.Add(change.Entity);
						affectedEntities++;
					}
					else if (change.Action == ActionType.Update)
					{
						var id = GetId(change.Entity);
						var existing = entities.FirstOrDefault(e => GetId(e) == id);
						if (existing != null)
						{
							entities.Remove(existing);
							entities.Add(change.Entity);
							affectedEntities++;
						}
					}
					else if (change.Action == ActionType.Remove)
					{
						if (entities.Remove(change.Entity))
						{
							affectedEntities++;
						}
					}
				}
				entityLists[type] = entities;

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
				await fileStorage.WriteTextAsync(tempFilePath, json);
				File.Move(tempFilePath, filePath, overwrite: true);
			}
			changes.Clear();
			changes.TrimExcess();
			snapshots.Clear();
			snapshots.TrimExcess();
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

		return affectedEntities;
	}

	/// <summary>
	/// Loads the list of entities of type <typeparamref name="T"/> from the corresponding JSON file.
	/// </summary>
	/// <typeparam name="T">The type of entity, which must be a class with an integer <c>Id</c> property.</typeparam>
	/// <returns>A <see cref="List{T}"/> containing the deserialized entities, or an empty list if the file does not exist.</returns>
	/// <remarks>
	/// The JSON file is named using the <see cref="JsonContextOptions.FileNameFactory"/> and located in the directory
	/// specified during construction. If the file does not exist or is corrupted, an empty list is returned or an exception
	/// is thrown. This method uses synchronous file I/O for simplicity and is called by <see cref="Set{T}"/> to initialize
	/// the in-memory entity list.
	/// </remarks>
	private List<T> LoadEntities<T>() where T : class
	{
		var jsonFilePath = Path.Combine(dataDirectory, jsonContextOptions.FileNameFactory(typeof(T)));
		if (!fileStorage.Exists(jsonFilePath))
		{
			return [];
		}

		try
		{
			var json = fileStorage.ReadText(jsonFilePath);
			var entities = JsonSerializer.Deserialize<List<T>>(json, jsonContextOptions.SerializerOptions) ?? [];

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
	/// Loads the list of entities of type <typeparamref name="T"/> from the corresponding JSON file asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of entity, which must be a class with an integer <c>Id</c> property.</typeparam>
	/// <returns>A task that represents the asynchronous operation, returning a <see cref="List{T}"/> containing the deserialized entities, or an empty list if the file does not exist.</ returns>
	/// <remarks>
	/// The JSON file is named using the <see cref="JsonContextOptions.FileNameFactory"/> and located in the directory
	/// specified during construction. If the file does not exist or is corrupted, an empty list is returned or an exception
	/// is thrown. This method uses asynchronous file I/O and is called by <see cref="SetAsync{T}"/> to initialize
	/// the in-memory entity list.
	/// </remarks>
	private async Task<List<T>> LoadEntitiesAsync<T>() where T : class
	{
		// Construct the file path using the configured factory.
		var jsonFilePath = Path.Combine(dataDirectory, jsonContextOptions.FileNameFactory(typeof(T)));
		if (!fileStorage.Exists(jsonFilePath))
		{
			return [];
		}

		try
		{
			// Read and deserialize the JSON file asynchronously.
			var json = await fileStorage.ReadTextAsync(jsonFilePath);
			var entities = JsonSerializer.Deserialize<List<T>>(json, jsonContextOptions.SerializerOptions) ?? [];

			lock (lockObject)
			{
				// Store snapshots for change tracking.
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
}

/// <summary>
/// A queryable collection of entities that supports querying, adding, updating, and removing entities
/// in a JSON-based data context.
/// </summary>
/// <typeparam name="T">The type of entity, which must be a class with an integer <c>Id</c> property.</typeparam>
public class JsonSet<T>(JsonContext context, List<T> entities) : IQueryable<T>, IQueryable, IEnumerable<T>, IEnumerable where T : class
{
	// Reference to the parent JsonContext for operations.
	private readonly JsonContext context = context ?? throw new ArgumentNullException(nameof(context));

	// Queryable entity collection for LINQ operations.
	private readonly IQueryable<T> queryable = entities?.AsQueryable() ?? throw new ArgumentNullException(nameof(entities));

	// LINQ queryable properties.
	public Type ElementType => queryable.ElementType;
	public Expression Expression => queryable.Expression;
	public IQueryProvider Provider => queryable.Provider;

	// Delegates add operations to the JsonContext.
	public void Add(T entity) => context.Add(entity);
	public void AddRange(IEnumerable<T> entities) => context.AddRange(entities);

	// Delegates update operations to the JsonContext.
	public void Update(T entity) => context.Update(entity);
	public void UpdateRange(IEnumerable<T> entities) => context.UpdateRange(entities);

	// Delegates remove operations to the JsonContext.
	public void Remove(T entity) => context.Remove(entity);
	public void RemoveRange(IEnumerable<T> entities) => context.RemoveRange(entities);

	// Provides enumeration over the queryable entities.
	public IEnumerator<T> GetEnumerator() => queryable.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
