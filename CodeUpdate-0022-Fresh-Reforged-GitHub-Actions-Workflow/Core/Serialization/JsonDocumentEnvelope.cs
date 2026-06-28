namespace Reforged.Core.Serialization;

/// <summary>
/// Standard envelope for generated Reforged JSON files.
/// Keeps provenance and schema version next to the data.
/// </summary>
public sealed class JsonDocumentEnvelope<T>
{
    public string Schema { get; set; } = "major-mud-reforged.v1";
    public string EntityType { get; set; } = string.Empty;
    public string GeneratedBy { get; set; } = string.Empty;
    public DateTimeOffset GeneratedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Sources { get; set; } = new();
    public T? Data { get; set; }
}
