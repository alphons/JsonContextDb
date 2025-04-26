
using JsonContextDb.JsonContext;
using System.Diagnostics;

var context = new  JsonContext(Path.Combine(AppContext.BaseDirectory, "Data"));

var users = context.Set<User>();
var vsers = context.Set<Vser>();

var sw = Stopwatch.StartNew();

for (int i = 0; i < 1000; i++)
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


await Task.Delay(1000);


await context.SaveChangesAsync();

var us = context.Set<User>();

var u = us.FirstOrDefault(x => x.Name.Contains("123"));


Console.WriteLine(u.Name + " " + u.Id + " " + sw.ElapsedMilliseconds + "mS");
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