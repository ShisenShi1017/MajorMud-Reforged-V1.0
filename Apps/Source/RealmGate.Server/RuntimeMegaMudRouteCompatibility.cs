using System.Collections.Concurrent;
using System.Text.Json;

namespace RealmGate.Server;

internal sealed record RuntimeMegaMudRouteTransition(
    string FromSignature,
    string Command,
    string NormalizedCommand,
    string ToSignature,
    int ObservationCount,
    int SourceRouteCount);

/// <summary>
/// Conservative fallback for legacy MegaMUD .mp special-route commands.
/// Authoritative generated runtime actions always win. This catalog is used
/// only when no decoded action already owns the entered command, and only a
/// unique room-signature destination is allowed to resolve.
/// </summary>
internal static class RuntimeMegaMudRouteCompatibility
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, RuntimeMegaMudRouteTransition>> Catalogs =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool TryResolve(
        string projectRoot,
        RuntimeWorld world,
        RuntimeRoom currentRoom,
        string enteredCommand,
        out RuntimeRoom targetRoom,
        out RuntimeMegaMudRouteTransition transition)
    {
        targetRoom = null!;
        transition = null!;

        var normalizedCommand = NormalizeCommand(enteredCommand);
        if (normalizedCommand.Length == 0) return false;

        var currentSignature = RuntimeMajorMudCompatibility.ComputeMegaMudRoomSignature(currentRoom.Name, currentRoom.Exits);
        var key = Key(currentSignature, normalizedCommand);
        var dataRoot = ServerDataRootResolver.Resolve(projectRoot, world.Definition.Id).DataRoot;
        var catalog = Catalogs.GetOrAdd(Path.GetFullPath(dataRoot), LoadCatalog);
        if (!catalog.TryGetValue(key, out transition!)) return false;
        var resolvedTransition = transition;

        // MegaMUD signatures are compact fingerprints and can theoretically
        // collide. Never guess: compatibility movement is enabled only when
        // the recovered destination fingerprint resolves to exactly one room.
        var candidates = world.AllRooms()
            .Where(room => RuntimeMajorMudCompatibility.ComputeMegaMudRoomSignature(room.Name, room.Exits)
                .Equals(resolvedTransition.ToSignature, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (candidates.Length != 1)
        {
            transition = null!;
            return false;
        }

        targetRoom = candidates[0];
        return true;
    }

    private static IReadOnlyDictionary<string, RuntimeMegaMudRouteTransition> LoadCatalog(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "megamud_routes.json");
        if (!File.Exists(path))
            return new Dictionary<string, RuntimeMegaMudRouteTransition>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var result = new Dictionary<string, RuntimeMegaMudRouteTransition>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in RuntimeChunkedClassicCatalog.LoadRecords(path))
            {
                var item = new RuntimeMegaMudRouteTransition(
                    JsonValueReader.ReadString(row, "fromSignature", string.Empty),
                    JsonValueReader.ReadString(row, "command", string.Empty),
                    JsonValueReader.ReadString(row, "normalizedCommand", string.Empty),
                    JsonValueReader.ReadString(row, "toSignature", string.Empty),
                    JsonValueReader.ReadInt(row, "observationCount", 0),
                    JsonValueReader.ReadInt(row, "sourceRouteCount", 0));
                if (string.IsNullOrWhiteSpace(item.FromSignature)
                    || string.IsNullOrWhiteSpace(item.ToSignature)
                    || string.IsNullOrWhiteSpace(item.NormalizedCommand))
                    continue;

                var key = Key(item.FromSignature, item.NormalizedCommand);
                // Generation excludes ambiguous pairs. Keep the runtime strict
                // as well if a hand-edited catalog ever introduces a duplicate.
                if (result.ContainsKey(key))
                    result.Remove(key);
                else
                    result[key] = item;
            }
            return result;
        }
        catch
        {
            // Compatibility data must never prevent the game server from booting.
            return new Dictionary<string, RuntimeMegaMudRouteTransition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Key(string signature, string command) =>
        $"{signature.Trim().ToUpperInvariant()}|{NormalizeCommand(command)}";

    private static string NormalizeCommand(string command) =>
        string.Join(' ', (command ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
