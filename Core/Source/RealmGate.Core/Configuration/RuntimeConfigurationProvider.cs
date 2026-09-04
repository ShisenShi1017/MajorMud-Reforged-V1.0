using System.Text.Json;

namespace RealmGate.Core.Configuration;

/// <summary>
/// Runtime-safe configuration provider for JSON files, environment overlays, and defaults.
/// Normal users do not need the .NET SDK to consume configuration from compiled applications.
/// </summary>
public sealed class RuntimeConfigurationProvider
{
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values => values;

    public RuntimeConfigurationProvider AddDefault(string key, string value)
    {
        values.TryAdd(key, value);
        return this;
    }

    public RuntimeConfigurationProvider Set(string key, string value)
    {
        values[key] = value;
        return this;
    }

    public RuntimeConfigurationProvider LoadJsonFile(string path, bool optional = true)
    {
        if (!File.Exists(path))
        {
            if (optional)
            {
                return this;
            }

            throw new FileNotFoundException("Configuration file was not found.", path);
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        FlattenElement(document.RootElement, null);
        return this;
    }

    public RuntimeConfigurationProvider LoadEnvironment(string prefix)
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            var value = entry.Value?.ToString();
            if (string.IsNullOrWhiteSpace(key) || value is null || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            values[key[prefix.Length..].Replace("__", ":", StringComparison.Ordinal)] = value;
        }

        return this;
    }

    public string GetString(string key, string fallback = "")
        => values.TryGetValue(key, out var value) ? value : fallback;

    public int GetInt32(string key, int fallback = 0)
        => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    public bool GetBoolean(string key, bool fallback = false)
        => values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private void FlattenElement(JsonElement element, string? prefix)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}:{property.Name}";
                FlattenElement(property.Value, key);
            }
            return;
        }

        if (prefix is not null)
        {
            values[prefix] = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString();
        }
    }
}
