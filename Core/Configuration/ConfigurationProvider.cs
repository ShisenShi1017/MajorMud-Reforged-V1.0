namespace MajorMud.Reforged.Core.Configuration;

/// <summary>
/// Small dependency-free configuration provider used by all MajorMud Reforged applications.
/// Applications may layer file, environment, or command-line values over these settings without duplicating models.
/// </summary>
public sealed class ConfigurationProvider
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public ConfigurationProvider Set(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Configuration key cannot be blank.", nameof(key));
        }
        _values[key.Trim()] = value;
        return this;
    }

    public string Get(string key, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }
        return _values.TryGetValue(key.Trim(), out var value) ? value : fallback;
    }

    public bool GetBoolean(string key, bool fallback = false)
    {
        var value = Get(key, string.Empty);
        return bool.TryParse(value, out var result) ? result : fallback;
    }

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
}
