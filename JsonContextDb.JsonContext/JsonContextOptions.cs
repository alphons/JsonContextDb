using System.Text.Json;

namespace JsonContextDb.JsonContext;

/// <summary>
/// Configuration options for <see cref="JsonContext"/>.
/// </summary>
public class JsonContextOptions
{
	// Configures JSON serialization options, enabling indented output by default.
	public JsonSerializerOptions SerializerOptions { get; set; } = new() { WriteIndented = true };

	// Factory function to generate file names based on entity type, defaulting to "{TypeName}.json".
	public Func<Type, string> FileNameFactory { get; set; } = type => $"{type.Name}.json";
}
