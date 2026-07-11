using System.Text.Json;

namespace Reforged.Server;

/// <summary>
/// Resolves generated world data exclusively from server configuration.
/// The server does not embed world IDs, gameplay file names, or machine-specific data paths.
/// </summary>
public static class ServerDataRootResolver
{
    public static (string WorldId, string DataRoot) ResolveDefault(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var root = Path.GetFullPath(projectRoot);
        var configurationPath = Path.Combine(root, "Config", "server.worlds.json");
        if (!File.Exists(configurationPath))
        {
            throw new InvalidOperationException($"Server world configuration was not found: {configurationPath}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configurationPath));
        var configuration = document.RootElement;
        var defaultWorldId = configuration.TryGetProperty("defaultWorldId", out var defaultWorldValue)
            ? defaultWorldValue.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(defaultWorldId))
        {
            throw new InvalidOperationException("Server world configuration does not declare defaultWorldId.");
        }

        if (!configuration.TryGetProperty("worlds", out var worlds) || worlds.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Server world configuration does not declare a worlds array.");
        }

        foreach (var world in worlds.EnumerateArray())
        {
            var worldId = world.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
            if (!string.Equals(worldId, defaultWorldId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var configuredDataRoot = world.TryGetProperty("dataRoot", out var dataRootValue)
                ? dataRootValue.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(configuredDataRoot))
            {
                throw new InvalidOperationException($"Default world '{defaultWorldId}' does not declare dataRoot.");
            }

            var resolvedDataRoot = Path.GetFullPath(Path.Combine(root, configuredDataRoot));
            var relative = Path.GetRelativePath(root, resolvedDataRoot);
            if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Configured dataRoot escapes the repository root: {configuredDataRoot}");
            }

            return (defaultWorldId, resolvedDataRoot);
        }

        throw new InvalidOperationException($"Default world '{defaultWorldId}' is not present in the worlds array.");
    }
}
