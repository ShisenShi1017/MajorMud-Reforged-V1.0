using System.Text.Json;

namespace MajorMud.Reforged.Core.Serialization;

/// <summary>
/// Default dependency-free JSON serializer. It centralizes formatting choices so applications do not drift.
/// </summary>
public sealed class JsonReforgedSerializer : IReforgedSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonReforgedSerializer(bool writeIndented = true)
    {
        _options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true
        };
    }

    public string FormatName => "json";

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);

    public T? Deserialize<T>(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }
        return JsonSerializer.Deserialize<T>(content, _options);
    }
}
