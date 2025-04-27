using System.Collections;
using System.Linq.Expressions;

namespace JsonContextDb.JsonContext;

/// <summary>
/// A queryable collection of entities that supports querying, adding, updating, and removing entities
/// in a JSON-based data context.
/// </summary>
/// <typeparam name="T">The type of entity, which must be a class with an integer <c>Id</c> property.</typeparam>
public class JsonSet<T>(JsonContext context) : IQueryable<T>, IQueryable, IEnumerable<T>, IEnumerable where T : class
{
	// Reference to the parent JsonContext for operations.
	private readonly JsonContext context = context ?? throw new ArgumentNullException(nameof(context));

	// Queryable entity collection for LINQ operations.
	private IQueryable<T> GetQueryable() => context.GetList<T>().AsQueryable();
	public Type ElementType => GetQueryable().ElementType;
	public Expression Expression => GetQueryable().Expression;
	public IQueryProvider Provider => GetQueryable().Provider;
	public void Add(T entity) => context.Add(entity);
	public void AddRange(IEnumerable<T> entities) => context.AddRange(entities);
	public void Update(T entity) => context.Update(entity);
	public void UpdateRange(IEnumerable<T> entities) => context.UpdateRange(entities);
	public void Remove(T entity) => context.Remove(entity);
	public void RemoveRange(IEnumerable<T> entities) => context.RemoveRange(entities);
	public IEnumerator<T> GetEnumerator() => GetQueryable().GetEnumerator();
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
}
