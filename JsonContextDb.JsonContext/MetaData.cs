namespace JsonContextDb.JsonContext;

internal class MetaData
{
	public int Id { get; set; } // Required for JsonContext
	public string EntityType { get; set; } = string.Empty; // Name of the entity type (e.g., "Customer")
	public int NextId { get; set; } // Next available ID for this type
}
