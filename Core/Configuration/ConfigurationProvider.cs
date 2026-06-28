using System.Text.Json;

namespace MajorMud.Reforged.Core.Configuration;

/// <summary>
/// Dependency-free configuration provider used by all MajorMud Reforged applications.
/// Applications may layer file, environment, or command-line values over these settings without duplicating models.
/// </summary>
public sealed class ConfigurationProvider : IConfigurationProvider
{
    private readonly Dictionary<string, ConfigurationEntry> _values = new(StringComparer.OrdinalIgnoreCase);

    public ConfigurationProvider Set(string key, string value, string source = "memory")
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Configuration key cannot be blank.", nameof(key));
        }

        var normalizedKey = NormalizeKey(key);
        _values[normalizedKey] = new ConfigurationEntry
        {
            Key = normalizedKey,
            Value = value,
            Source = string.IsNullOrWhiteSpace(source) ? "memory" : source.Trim()
        };
        return this;
    }

    public ConfigurationProvider LoadDictionary(IDictionary<string, string> values, string source)
    {
        foreach (var pair in values)
        {
            Set(pair.Key, pair.Value, source);
        }

        return this;
    }

    public ConfigurationProvider LoadJsonFile(string path, bool optional = true)
    {
        if (!File.Exists(path))
        {
            if (optional)
            {
                return this;
            }

            throw new FileNotFoundException("Configuration file was not found.", path);
        }

        var json = File.ReadAllText(path);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new Dictionary<string, string>();

        return LoadDictionary(values, path);
    }

    public ConfigurationProvider LoadEnvironment(string prefix)
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Set(key[prefix.Length..], entry.Value?.ToString() ?? string.Empty, "environment");
        }

        return this;
    }

    public string Get(string key, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        return _values.TryGetValue(NormalizeKey(key), out var entry) ? entry.Value : fallback;
    }

    public bool GetBoolean(string key, bool fallback = false)
    {
        var value = Get(key, string.Empty);
        return bool.TryParse(value, out var result) ? result : fallback;
    }

    public int GetInteger(string key, int fallback = 0)
    {
        var value = Get(key, string.Empty);
        return int.TryParse(value, out var result) ? result : fallback;
    }

    public IReadOnlyDictionary<string, string> Snapshot()
    {
        return _values.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ConfigurationEntry> Entries => _values.Values.OrderBy(v => v.Key).ToArray();

    private static string NormalizeKey(string key) => key.Trim().Replace("__", ":", StringComparison.Ordinal);
}
