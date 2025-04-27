using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace JsonContextDb.JsonContext;

/// <summary>
/// A queryable collection of entities that supports querying, adding, updating, and removing entities
/// in a JSON-based data context.
/// </summary>
/// <typeparam name="T">The type of entity, which must be a class with an integer <c>Id</c> property.</typeparam>
public class DbSet<T>(DbContext context) : IQueryable<T>, IQueryable, IEnumerable<T>, IEnumerable where T : class
{
	// Reference to the parent JsonContext for operations.
	private readonly DbContext context = context ?? throw new ArgumentNullException(nameof(context));

	// Queryable entity collection for LINQ operations.
	private IQueryable<T> GetQueryable() => context.GetList<T>().AsQueryable();

	/// <summary>
	/// Gets the type of the elements in the collection.
	/// </summary>
	public Type ElementType => GetQueryable().ElementType;

	/// <summary>
	/// Gets the expression tree that represents the query.
	/// </summary>
	public Expression Expression => GetQueryable().Expression;

	/// <summary>
	/// Gets the query provider that executes the query.
	/// </summary>
	public IQueryProvider Provider => GetQueryable().Provider;

	/// <summary>
	/// Adds a single entity to the data context.
	/// </summary>
	/// <param name="entity">The entity to add.</param>
	public void Add(T entity) => context.Add(entity);

	/// <summary>
	/// Adds a collection of entities to the data context.
	/// </summary>
	/// <param name="entities">The entities to add.</param>
	public void AddRange(IEnumerable<T> entities) => context.AddRange(entities);

	/// <summary>
	/// Adds a collection of entities to the data context.
	/// </summary>
	/// <param name="entities">The entities to add.</param>
	public void AddRange(params T[] entities) => context.AddRange(entities);

	/// <summary>
	/// Updates a single entity in the data context.
	/// </summary>
	/// <param name="entity">The entity to update.</param>
	public void Update(T entity) => context.Update(entity);

	/// <summary>
	/// Updates a collection of entities in the data context.
	/// </summary>
	/// <param name="entities">The entities to update.</param>
	public void UpdateRange(IEnumerable<T> entities) => context.UpdateRange(entities);

	/// <summary>
	/// Removes a single entity from the data context.
	/// </summary>
	/// <param name="entity">The entity to remove.</param>
	public void Remove(T entity) => context.Remove(entity);

	/// <summary>
	/// Removes a collection of entities from the data context.
	/// </summary>
	/// <param name="entities">The entities to remove.</param>
	public void RemoveRange(IEnumerable<T> entities) => context.RemoveRange(entities);

	/// <summary>
	/// Gets an enumerator for the collection of entities.
	/// </summary>
	/// <returns>An enumerator that can be used to iterate through the collection.</returns>
	public IEnumerator<T> GetEnumerator() => GetQueryable().GetEnumerator();

	/// <summary>
	/// Gets a non-generic enumerator for the collection of entities.
	/// </summary>
	/// <returns>An enumerator that can be used to iterate through the collection.</returns>
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>
	/// Asynchronously retrieves the first entity that matches the specified predicate, or the first entity if no predicate is provided.
	/// Returns null if no entity is found.
	/// </summary>
	/// <param name="predicate">An optional expression to filter entities.</param>
	/// <returns>A task representing the asynchronous operation, returning the first matching entity or null.</returns>
	public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>>? predicate = null)
	{
		var queryable = GetQueryable();
		var result = predicate != null
			? queryable.FirstOrDefault(predicate)
			: queryable.FirstOrDefault();
		return Task.FromResult(result);
	}

	/// <summary>
	/// Asynchronously retrieves all entities as a list.
	/// </summary>
	/// <returns>A task representing the asynchronous operation, returning a list of all entities.</returns>
	public Task<List<T>> ToListAsync()
	{
		var result = GetQueryable().ToList();
		return Task.FromResult(result);
	}

	/// <summary>
	/// Asynchronously streams entities that match the specified predicate, or all entities if no predicate is provided.
	/// </summary>
	/// <param name="predicate">An optional expression to filter entities.</param>
	/// <returns>An asynchronous enumerable of entities that match the predicate.</returns>
	public async IAsyncEnumerable<T> GetAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default, Expression<Func<T, bool>>? predicate = null)
	{
		var queryable = GetQueryable();
		var enumerable = predicate != null ? queryable.Where(predicate) : queryable;
		foreach (var entity in enumerable)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Yield(); // Ensures asynchronous context
			yield return entity;
		}
	}
}
