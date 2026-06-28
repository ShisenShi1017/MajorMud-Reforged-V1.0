using System.Text.Json;
using MajorMud.Core.Models;

namespace MajorMud.Core.Loading;

/// <summary>
/// Flexible Core data record used by repository-backed loaders before category-specific models are finalized.
/// It preserves every source JSON field in RawFields while projecting common identity fields into DataRecord.
/// </summary>
public sealed class CanonicalDataRecord : DataRecord
{
    public string Category { get; init; } = string.Empty;
    public string SourceFile { get; init; } = string.Empty;
    public int Index { get; init; }
    public Dictionary<string, JsonElement> RawFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public JsonElement? GetField(string name)
        => RawFields.TryGetValue(name, out var value) ? value : null;
}
