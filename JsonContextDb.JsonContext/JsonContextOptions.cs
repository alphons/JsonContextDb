using System.Text.Json;

namespace JsonContextDb.JsonContext;

/// <summary>
/// Configuration options for <see cref="JsonContext"/>, controlling JSON serialization and file naming.
/// </summary>
/// <remarks>
/// This class allows customization of how <see cref="JsonContext"/> serializes entities to JSON and names the output files.
/// Instances of this class are typically provided during <see cref="JsonContext"/> initialization and should not be modified
/// afterward to ensure consistent behavior. The default settings enable indented JSON output and generate file names based on
/// the entity type name (e.g., "Customer.json" for a <c>Customer</c> type).
/// </remarks>
public class JsonContextOptions
{
	/// <summary>
	/// Gets or sets the JSON serialization options used for reading and writing entities.
	/// </summary>
	/// <remarks>
	/// The default value is a <see cref="JsonSerializerOptions"/> instance with <c>WriteIndented</c> set to <c>true</c> for readable JSON output.
	/// Customizing this property allows control over serialization behavior, such as case sensitivity or property naming.
	/// This property must not be null when used by <see cref="JsonContext"/>.
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown if set to null.</exception>
	public JsonSerializerOptions SerializerOptions { get; set; } = new() { WriteIndented = true };

	/// <summary>
	/// Gets or sets a factory function that generates file names based on entity types.
	/// </summary>
	/// <remarks>
	/// The default factory generates file names in the format "{TypeName}.json" (e.g., "Customer.json" for a <c>Customer</c> type).
	/// Customizing this function allows for alternative naming conventions, such as including subdirectories or different extensions.
	/// The function must return a valid file name and should not return null or empty strings.
	/// This property must not be null when used by <see cref="JsonContext"/>.
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown if set to null.</exception>
	public Func<Type, string> FileNameFactory { get; set; } = type => $"{type.Name}.json";
}
