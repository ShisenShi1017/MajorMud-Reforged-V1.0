namespace MajorMud.Reforged.Core.Configuration;

/// <summary>
/// Shared read-only configuration contract used by all applications.
/// This prevents Server, Importer, Editor, Launcher, and Client from inventing
/// separate settings readers.
/// </summary>
public interface IConfigurationProvider
{
    string Get(string key, string fallback = "");
    bool GetBoolean(string key, bool fallback = false);
    int GetInteger(string key, int fallback = 0);
    IReadOnlyDictionary<string, string> Snapshot();
}
