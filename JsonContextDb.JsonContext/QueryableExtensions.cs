namespace JsonContextDb.JsonContext;

public static class QueryableExtensions
{
	public static Task<List<T>> ToListAsync<T>(this IQueryable<T> queryable)
	{
		return queryable == null ? throw new ArgumentNullException(nameof(queryable)) : Task.FromResult(queryable.ToList());
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IQueryable<T> queryable)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
	{
		ArgumentNullException.ThrowIfNull(queryable);

		foreach (var item in queryable.ToList())
		{
			yield return item;
		}
	}

}
