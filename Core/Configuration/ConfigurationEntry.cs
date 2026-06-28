namespace Reforged.Core.Configuration;

/// <summary>
/// Represents a single normalized configuration value and its origin.
/// </summary>
public sealed class ConfigurationEntry
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Source { get; init; } = "memory";
}
