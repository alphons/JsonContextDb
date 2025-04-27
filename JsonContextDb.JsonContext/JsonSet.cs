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
}
