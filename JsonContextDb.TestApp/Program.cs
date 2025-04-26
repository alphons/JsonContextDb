
using JsonContextDb.JsonContext;
using System.Diagnostics;

var context = new  JsonContext(Path.Combine(AppContext.BaseDirectory, "Data"));

var users = context.Set<User>();
var vsers = context.Set<Vser>();

if (!users.Any())
{

	for (int i = 1; i <= 100000; i++)
	{
		users.Add(new User
		{
			Name = $"annet {i}"
		});
		vsers.Add(new Vser
		{
			Name = $"alphons {i}"
		});
	}
	await context.SaveChangesAsync();
}

var sw = Stopwatch.StartNew();

var user = users.FirstOrDefault(x => x.Name.Contains("99998"));

if (user != null)
	Console.WriteLine($"{user.Name} - {user.Id} {sw.ElapsedMilliseconds} mS");
else
	Console.WriteLine("not found");

sw = Stopwatch.StartNew();

user = users.FirstOrDefault(x => x.Name.Contains("Tester"));

if (user != null)
	Console.WriteLine($"{user.Name} - {user.Id} {sw.ElapsedMilliseconds} mS");
else
	Console.WriteLine("not found");

if(user != null)
	user.Name = "Tester 1";

sw = Stopwatch.StartNew();

var count = await context.SaveChangesAsync();

Console.WriteLine($"Count:{count} {sw.ElapsedMilliseconds} mS");

Console.ReadLine();

class User
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

class Vser
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
}