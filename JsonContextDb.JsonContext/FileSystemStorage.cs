namespace JsonContextDb.JsonContext;

// Defines an interface for file storage operations, abstracting file I/O for testability.
public interface IFileStorage
{
	string ReadText(string path); // Synchronous read
	Task<string> ReadTextAsync(string path); // Asynchronous read
	Task WriteTextAsync(string path, string content);
	bool Exists(string path);
}

// Concrete implementation of IFileStorage using the System.IO.File API.
public class FileSystemStorage : IFileStorage
{
	// Reads text from a file synchronously.
	public string ReadText(string path) => File.ReadAllText(path);

	// Reads text from a file asynchronously.
	public async Task<string> ReadTextAsync(string path) => await File.ReadAllTextAsync(path);

	// Writes text to a file asynchronously.
	public async Task WriteTextAsync(string path, string content) => await File.WriteAllTextAsync(path, content);

	// Checks if a file exists.
	public bool Exists(string path) => File.Exists(path);
}
