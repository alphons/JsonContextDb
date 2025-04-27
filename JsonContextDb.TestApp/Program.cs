
using JsonContextDb.JsonContext;
using System.Diagnostics;

var context = new  JsonContext(Path.Combine(AppContext.BaseDirectory, "Data"));

var users = context.Set<User>();
var vsers = context.Set<Vser>();

if (!users.Any())
{
	var nr = 2;
	for (int i = 1; i <= nr; i++)
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

	Debug.Assert(users.Count() == nr);
	Debug.Assert(vsers.Count() == nr);

	var cnt = await context.SaveChangesAsync();

	Debug.Assert(cnt == 2 * nr);
}

var total = users.Count();

var sw = Stopwatch.StartNew();

var newUser = new User
{
	Name = "Daar gaat ie"
};

users.Add(newUser);

Debug.Assert(users.Count() == total + 1);

Debug.Assert(newUser.Id == 0);

var count1 = await context.SaveChangesAsync();

Debug.Assert(count1 == 1);

Debug.Assert(newUser.Id != 0);

var user = await users.FirstOrDefaultAsync(x => x.Name.Contains("1"));

//Debug.Assert(user != null);

//if (user != null)
//	Console.WriteLine($"{user.Name} - {user.Id} {sw.ElapsedMilliseconds} mS");

//Debug.Assert(users.Count() == total + 1);

newUser.Name = "Tester 1";

var count = await context.SaveChangesAsync();

Debug.Assert(count == 1);

Console.WriteLine($"{sw.ElapsedMilliseconds} mS");

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