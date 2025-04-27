using JsonContextDb.JsonContext;
using System.Diagnostics;

namespace JsonContextDb.TestApp;

public class JCTester
{
	public static async Task Main()
	{
		File.Delete(Path.Combine(AppContext.BaseDirectory, "Data", "User.json"));
		File.Delete(Path.Combine(AppContext.BaseDirectory, "Data", "MetaData.json"));

		var context = new JCUsersContext(Path.Combine(AppContext.BaseDirectory, "Data"));

		context.Users.AddRange(
			new User { Name = "Alphons" },
			new User { Name = "Annet" }
		);

		Debug.Assert(context.Users.Count() == 0);

		var cnt = await context.SaveChangesAsync();

		var tttt = context.Users;

		Debug.Assert(cnt == 2);

		Debug.Assert(context.Users.Count() == 2);

		var users = await context.Users
			.Where(p => p.Name.Contains('A'))
			.OrderBy(p => p.Name)
			.ToListAsync();

		foreach (var user in users)
		{
			Console.WriteLine($"Id:{user.Id} User: {user.Name}");
		}

		var alphons = await context.Users.FirstOrDefaultAsync(p => p.Name == "Alphons");

		if (alphons != null)
		{
			alphons.Name = "Alphonsje";

			var name = context.Users.ToList()[0].Name;

			Debug.Assert(name == "Alphonsje");

			var cnt2 = await context.SaveChangesAsync();

			Debug.Assert(cnt2 == 1);

			Debug.Assert(context.Users.Where(x => x.Id == 1).FirstOrDefault()?.Name == "Alphonsje");

			Console.WriteLine($"\nBijgewerkte Name voor {alphons.Name}");
		}

		var alphonsDelete = await context.Users
			.FirstOrDefaultAsync(p => p.Name == "Alphonsje");

		if (alphonsDelete != null)
		{
			context.Users.Remove(alphonsDelete);

			Debug.Assert(context.Users.Where(x => x.Id == 1).FirstOrDefault()?.Name == "Alphonsje");

			var cnt3 = await context.SaveChangesAsync();

			Debug.Assert(cnt3 == 1);

			Console.WriteLine($"Alphonsje verwijderd {cnt3}");
		}

		Console.WriteLine("\nResterende users:");
		await foreach (var user in context.Users.AsAsyncEnumerable())
		{
			Console.WriteLine($"Id:{user.Id} User: {user.Name}");
		}
	}
}


public class JCUsersContext(string dataDirectory) : DbContext(dataDirectory)
{
	public DbSet<User> Users => Set<User>();
}
