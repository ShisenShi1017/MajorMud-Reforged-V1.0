using System.Text.Json;

namespace RealmGate.Server;

/// <summary>
/// Resolves generated world data exclusively from server configuration.
/// Live gameplay must resolve the selected world, never silently fall back to the configured default world.
/// </summary>
public static class ServerDataRootResolver
{
    private static readonly object OverrideGate = new();
    private static readonly Dictionary<string, RuntimeDataRootOverride> RuntimeOverrides =
        new(StringComparer.OrdinalIgnoreCase);

    public static IDisposable RegisterRuntimeOverride(
        string projectRoot,
        string runtimeWorldId,
        string dataWorldId,
        string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeWorldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataWorldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        var root = Path.GetFullPath(projectRoot);
        var resolvedDataRoot = Path.GetFullPath(dataRoot);
        var relative = Path.GetRelativePath(root, resolvedDataRoot);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidOperationException($"Runtime data root escapes the repository root: {dataRoot}");
        if (!Directory.Exists(resolvedDataRoot))
            throw new InvalidOperationException($"Runtime data root was not found: {resolvedDataRoot}");

        var key = OverrideKey(root, runtimeWorldId);
        var registration = new RuntimeDataRootOverride(
            runtimeWorldId.Trim(),
            dataWorldId.Trim(),
            resolvedDataRoot,
            Guid.NewGuid());
        lock (OverrideGate)
        {
            if (RuntimeOverrides.ContainsKey(key))
                throw new InvalidOperationException($"Runtime data root override already exists for world '{runtimeWorldId}'.");
            RuntimeOverrides[key] = registration;
        }

        return new OverrideRegistration(key, registration.Token);
    }

    public static string ResolveDataWorldId(string projectRoot, string worldId)
    {
        var root = Path.GetFullPath(projectRoot);
        return TryGetOverride(root, worldId, out var registration)
            ? registration.DataWorldId
            : Resolve(root, worldId).WorldId;
    }

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
        if (TryGetOverride(root, worldId, out var runtimeOverride))
            return (runtimeOverride.RuntimeWorldId, runtimeOverride.DataRoot);

        return ResolveConfigured(root, worldId, requireExistingDataRoot: true);
    }

    /// <summary>
    /// Resolves a configured output location before generated data exists. This is reserved for
    /// compiler/generation workflows; live runtime callers must continue to use <see cref="Resolve"/>.
    /// </summary>
    public static (string WorldId, string DataRoot) ResolveForGeneration(string projectRoot, string worldId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);

        return ResolveConfigured(Path.GetFullPath(projectRoot), worldId, requireExistingDataRoot: false);
    }

    private static (string WorldId, string DataRoot) ResolveConfigured(
        string root,
        string worldId,
        bool requireExistingDataRoot)
    {
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

            if (requireExistingDataRoot && !Directory.Exists(resolvedDataRoot))
                throw new InvalidOperationException($"Generated data root for world '{configuredWorldId}' was not found: {resolvedDataRoot}");

            return (configuredWorldId ?? worldId, resolvedDataRoot);
        }

        throw new InvalidOperationException($"Requested world '{worldId}' is not present in the worlds array.");
    }

    private static bool TryGetOverride(string projectRoot, string worldId, out RuntimeDataRootOverride registration)
    {
        lock (OverrideGate)
            return RuntimeOverrides.TryGetValue(OverrideKey(projectRoot, worldId), out registration!);
    }

    private static string OverrideKey(string projectRoot, string worldId) =>
        $"{Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}\n" +
        RuntimeIdentity.Normalize(worldId, "world");

    private sealed record RuntimeDataRootOverride(
        string RuntimeWorldId,
        string DataWorldId,
        string DataRoot,
        Guid Token);

    private sealed class OverrideRegistration(string key, Guid token) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (OverrideGate)
            {
                if (RuntimeOverrides.TryGetValue(key, out var current) && current.Token == token)
                    RuntimeOverrides.Remove(key);
            }
        }
    }
}
