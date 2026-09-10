using System.Text.Json;

namespace RealmGate.Server;

/// <summary>
/// Resolves generated world data exclusively from server configuration.
/// Live gameplay must resolve the selected world, never silently fall back to the configured default world.
/// </summary>
public static class ServerDataRootResolver
{
    public static (string WorldId, string DataRoot) ResolveDefault(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var root = Path.GetFullPath(projectRoot);
        var configurationPath = Path.Combine(root, "Config", "server.worlds.json");
        if (!File.Exists(configurationPath))
            throw new InvalidOperationException($"Server world configuration was not found: {configurationPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(configurationPath));
        var configuration = document.RootElement;
        var defaultWorldId = configuration.TryGetProperty("defaultWorldId", out var defaultWorldValue)
            ? defaultWorldValue.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(defaultWorldId))
            throw new InvalidOperationException("Server world configuration does not declare defaultWorldId.");

        return Resolve(projectRoot, defaultWorldId);
    }

    public static (string WorldId, string DataRoot) Resolve(string projectRoot, string worldId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);

        var root = Path.GetFullPath(projectRoot);
        var configurationPath = Path.Combine(root, "Config", "server.worlds.json");
        if (!File.Exists(configurationPath))
            throw new InvalidOperationException($"Server world configuration was not found: {configurationPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(configurationPath));
        var configuration = document.RootElement;
        if (!configuration.TryGetProperty("worlds", out var worlds) || worlds.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Server world configuration does not declare a worlds array.");

        foreach (var world in worlds.EnumerateArray())
        {
            var configuredWorldId = world.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
            if (!string.Equals(configuredWorldId, worldId, StringComparison.OrdinalIgnoreCase))
                continue;

            var runtimeEnabled = !world.TryGetProperty("runtimeEnabled", out var runtimeEnabledValue)
                || runtimeEnabledValue.ValueKind != JsonValueKind.False;
            if (!runtimeEnabled)
                throw new InvalidOperationException($"Requested world '{worldId}' is not runtime-enabled.");

            var configuredDataRoot = world.TryGetProperty("dataRoot", out var dataRootValue)
                ? dataRootValue.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(configuredDataRoot))
                throw new InvalidOperationException($"Requested world '{worldId}' does not declare dataRoot.");

            var resolvedDataRoot = Path.GetFullPath(Path.Combine(root, configuredDataRoot));
            var relative = Path.GetRelativePath(root, resolvedDataRoot);
            if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                throw new InvalidOperationException($"Configured dataRoot escapes the repository root: {configuredDataRoot}");

            if (!Directory.Exists(resolvedDataRoot))
                throw new InvalidOperationException($"Generated data root for world '{configuredWorldId}' was not found: {resolvedDataRoot}");

            return (configuredWorldId ?? worldId, resolvedDataRoot);
        }

        throw new InvalidOperationException($"Requested world '{worldId}' is not present in the worlds array.");
    }
}
