using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDExitAuditReport(
    bool Passed,
    string WorldId,
    int ExitCount,
    int CardinalExitCount,
    int VerticalExitCount,
    int SpecialExitCount,
    int OneWayExitCount,
    int BlockedDestinationCount,
    int ResolvedDestinationCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDExitAudit
{
    private static readonly HashSet<string> Cardinal = new(new[] { "N", "S", "E", "W", "NE", "NW", "SE", "SW" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Vertical = new(new[] { "U", "D" }, StringComparer.OrdinalIgnoreCase);

    public static RuntimeBundleDExitAuditReport Run(string projectRoot, string worldId)
    {
        var options = ServerRuntimeOptions.Load(projectRoot);
        var definition = options.Worlds.First(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        var exits = world.AllRooms().SelectMany(room => room.Exits.Select(exit => (Room: room, Exit: exit))).ToArray();
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }

        Check(exits.Length == world.ExitCount, "runtime_exit_count_matches_room_graph");
        Check(exits.All(x => x.Exit.SourceKey == new RuntimeRoomKey(x.Room.MapId, x.Room.RoomNumber)), "every_exit_is_owned_by_its_canonical_source_room");
        Check(exits.All(x => !string.IsNullOrWhiteSpace(x.Exit.Id)), "every_exit_has_generated_identity");
        Check(exits.All(x => !string.IsNullOrWhiteSpace(x.Exit.Direction)), "every_exit_has_direction");
        Check(exits.GroupBy(x => $"{x.Room.Id}:{x.Exit.Direction}", StringComparer.OrdinalIgnoreCase).All(g => g.Count() == 1), "one_exit_per_direction_per_room");
        Check(exits.Where(x => !x.Exit.HasBlockedDestination).All(x => world.ResolveExitDestination(x.Exit) is not null), "all_executable_exit_destinations_resolve");
        Check(exits.Where(x => x.Exit.HasBlockedDestination).All(x => world.ResolveExitDestination(x.Exit) is null), "all_dangling_destinations_are_runtime_blocked");
        Check(exits.All(x => ReferenceEquals(world.FindExit(x.Room, x.Exit.Direction), x.Exit)), "authoritative_exit_lookup_returns_room_owned_instance");

        var reverse = new HashSet<string>(exits.Select(x => $"{x.Room.Id}>{x.Exit.TargetRoomId}"), StringComparer.OrdinalIgnoreCase);
        var oneWay = exits.Count(x => !x.Exit.HasBlockedDestination && !reverse.Contains($"{x.Exit.TargetRoomId}>{x.Room.Id}"));
        var special = exits.Count(x => !Cardinal.Contains(x.Exit.Direction) && !Vertical.Contains(x.Exit.Direction) ||
                                     !string.IsNullOrWhiteSpace(x.Exit.GateKind) && !x.Exit.GateKind.Equals("open", StringComparison.OrdinalIgnoreCase));

        return new RuntimeBundleDExitAuditReport(
            failures.Count == 0,
            world.Definition.Id,
            exits.Length,
            exits.Count(x => Cardinal.Contains(x.Exit.Direction)),
            exits.Count(x => Vertical.Contains(x.Exit.Direction)),
            special,
            oneWay,
            exits.Count(x => x.Exit.HasBlockedDestination),
            exits.Count(x => world.ResolveExitDestination(x.Exit) is not null),
            checks,
            failures,
            new[]
            {
                "RuntimeWorld owns the map-qualified exit graph and is the single destination resolver.",
                "RuntimeSession movement, directional look, living-world traversal, and gate evaluation consume RuntimeWorld exit resolution.",
                "One-way exits are preserved as decoded graph relationships and are not auto-reciprocated.",
                "Dangling generated destinations remain visible to validation but are blocked from live movement."
            },
            new[]
            {
                "Exit type, gate kind, qualifier, disposition, destination status, and source verification remain attached to RuntimeExit for later specialized consumers.",
                "Non-cardinal and qualified exits are preserved without inventing reciprocal or gate behavior."
            },
            new[] { "Door, hidden, conditional, teleporter, toll, cast-transition, and trap effects remain assigned to their dedicated complete Bundle D passes." },
            new[] { "Movement triggers remain compatibility evidence; they may block invalid destinations but do not replace RuntimeExit graph ownership." },
            new[] { "Unclassified native exit qualifiers remain evidence-only until a source proves their gameplay effect." });
    }
}
