namespace JsonContextDb.JsonContext;

/// <summary>
/// Defines an interface for file storage operations, abstracting file I/O for testability and flexibility.
/// </summary>
/// <remarks>
/// This interface is used by <see cref="JsonContext"/> to read and write JSON files, enabling mocking in unit tests
/// and supporting alternative storage implementations. All methods are expected to handle file paths relative to
/// the configured data directory.
/// </remarks>
public interface IFileStorage
{
	/// <summary>
	/// Reads the contents of a file synchronously as a string.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <returns>The contents of the file as a string.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
	/// <exception cref="IOException">Thrown if an I/O error occurs while reading the file.</exception>
	/// <exception cref="UnauthorizedAccessException">Thrown if access to the file is denied.</exception>
	string ReadText(string path);

	/// <summary>
	/// Reads the contents of a file asynchronously as a string.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <returns>A task representing the asynchronous operation, returning the contents of the file as a string.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
	/// <exception cref="IOException">Thrown if an I/O error occurs while reading the file.</exception>
	/// <exception cref="UnauthorizedAccessException">Thrown if access to the file is denied.</exception>
	Task<string> ReadTextAsync(string path);

	/// <summary>
	/// Writes the specified content to a file asynchronously.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <param name="content">The content to write to the file.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> or <paramref name="content"/> is null.</exception>
	/// <exception cref="IOException">Thrown if an I/O error occurs while writing the file.</exception>
	/// <exception cref="UnauthorizedAccessException">Thrown if access to the file is denied.</exception>
	Task WriteTextAsync(string path, string content);

	/// <summary>
	/// Checks if a file exists at the specified path.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
	bool Exists(string path);
}

/// <summary>
/// A concrete implementation of <see cref="IFileStorage"/> using the <see cref="System.IO.File"/> API.
/// </summary>
/// <remarks>
/// This class provides file I/O operations for reading and writing JSON files in <see cref="JsonContext"/>.
/// It uses the standard .NET file system APIs, performing synchronous reads for simplicity and asynchronous
/// writes to avoid blocking. All operations are performed relative to the data directory configured in
/// <see cref="JsonContext"/>.
/// </remarks>
public class FileSystemStorage : IFileStorage
{
	/// <summary>
	/// Reads the contents of a file synchronously as a string.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <returns>The contents of the file as a string.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
	/// <exception cref="IOException">Thrown if an I/O error occurs while reading the file.</exception>
	/// <exception cref="UnauthorizedAccessException">Thrown if access to the file is denied.</exception>
	/// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
	public string ReadText(string path) => File.ReadAllText(path);

	/// <summary>
	/// Reads the contents of a file asynchronously as a string.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <returns>A task representing the asynchronous operation, returning the contents of the file as a string.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
	/// <exception cref="IOException">Thrown if an I/O error occurs while reading the file.</exception>
	/// <exception cref="UnauthorizedAccessException">Thrown if access to the file is denied.</exception>
	/// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
	public async Task<string> ReadTextAsync(string path) => await File.ReadAllTextAsync(path);

	/// <summary>
	/// Writes the specified content to a file asynchronously.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <param name="content">The content to write to the file.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> or <paramref name="content"/> is null.</exception>
	/// <exception cref="IOException">Thrown if an I/O error occurs while writing the file.</exception>
	/// <exception cref="UnauthorizedAccessException">Thrown if access to the file is denied.</exception>
	public async Task WriteTextAsync(string path, string content) => await File.WriteAllTextAsync(path, content);

	/// <summary>
	/// Checks if a file exists at the specified path.
	/// </summary>
	/// <param name="path">The path to the file.</param>
	/// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
	public bool Exists(string path) => File.Exists(path);
}
