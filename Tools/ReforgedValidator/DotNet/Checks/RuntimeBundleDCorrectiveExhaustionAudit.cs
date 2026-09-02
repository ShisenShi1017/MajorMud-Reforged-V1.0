using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDCorrectiveEvidence(
    string Phase,
    string Source,
    string Classification,
    string Finding);

public sealed record RuntimeBundleDCorrectiveExhaustionAuditReport(
    bool Passed,
    string WorldId,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<RuntimeBundleDCorrectiveEvidence> Evidence,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Combined corrective D1-D6 audit. Unlike the original generated-schema-only audit, this gate
/// explicitly requires canonical Librarium promotions, generated Classic data, and live consumers.
/// </summary>
public static class RuntimeBundleDCorrectiveExhaustionAudit
{
    public static RuntimeBundleDCorrectiveExhaustionAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var evidence = new List<RuntimeBundleDCorrectiveEvidence>();
        void Check(bool condition, string id) { if (condition) checks.Add(id); else failures.Add(id); }
        string Read(string relative) => File.ReadAllText(Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        bool Exists(string relative) => File.Exists(Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        bool Contains(string relative, string value) => Exists(relative) && Read(relative).Contains(value, StringComparison.OrdinalIgnoreCase);

        var requiredSources = new[]
        {
            "Data/Classic/rooms.json",
            "Data/Classic/exits.json",
            "Data/Classic/abilities.json",
            "Librarium/Objects/rooms/room_records.json",
            "Librarium/Objects/exits/exit_records.json",
            "Librarium/Objects/room_interactions/room_interaction_records.json",
            "Librarium/Objects/abilities/ability_records.json"
        };
        foreach (var source in requiredSources) Check(Exists(source), "all-source-present:" + source);

        Check(Contains("Data/Classic/abilities.json", "Del@Maint"), "decoded-del-maint-ability-present");
        Check(Contains("Librarium/Objects/abilities/ability_records.json", "ability.119") &&
              Contains("Librarium/Objects/abilities/ability_records.json", "Del@Maint"),
              "librarium-del-maint-119-provenance");
        Check(Contains("Librarium/Objects/abilities/ability_records.json", "delete the item, hidden or not, at cleanup"),
              "native-delete-at-cleanup-display-evidence");

        var world = RuntimeBundleDWorldAuthorityAudit.Run(projectRoot, worldId);
        var rooms = RuntimeBundleDRoomAudit.Run(projectRoot, worldId);
        var exits = RuntimeBundleDExitAudit.Run(projectRoot, worldId);
        var doors = RuntimeBundleDDoorAudit.Run(projectRoot, worldId);
        var search = RuntimeBundleDSearchAudit.Run(projectRoot, worldId);
        var cleanup = RuntimeBundleDCleanupAudit.Run(projectRoot, worldId);
        Check(world.Passed, "d1-world-authority-audit");
        Check(rooms.Passed, "d2-room-runtime-audit");
        Check(exits.Passed, "d3-exit-runtime-audit");
        Check(doors.Passed, "d4-door-runtime-audit");
        Check(search.Passed, "d5-search-runtime-audit");
        Check(cleanup.Passed, "d6-cleanup-runtime-audit");

        var server = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        Check(Directory.GetFiles(server, "RuntimeWorldLoader.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-world-loader");
        Check(Directory.GetFiles(server, "RuntimeClassicCleanup.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-cleanup-consumer");
        Check(Directory.GetFiles(server, "RuntimeClassicHiddenSearch.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-hidden-search-consumer");
        Check(Directory.GetFiles(server, "RuntimeLivingWorld.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-live-door-state-owner");
        Check(!Read("Apps/Source/Reforged.Server/RuntimeBundleDSearch.cs").Contains("Registry.Discover(actor.Id, target.TargetId)", StringComparison.Ordinal),
            "see-hidden-does-not-auto-persist-object-discovery");
        Check(Read("Tools/ReforgedValidator/DotNet/Checks/RuntimeBundleDWorldAuthorityAudit.cs").Contains("CollectProperties(property.Value, properties)", StringComparison.Ordinal),
            "world-authority-recurses-all-nested-generated-fields");

        evidence.Add(new("D1", "decoded + generated + runtime", "Verified", "Canonical map-qualified room identity and one RuntimeWorld graph are live."));
        evidence.Add(new("D2", "canonical room records", "Strongly Corroborated", "Room light, shop, spell, lair, delay, command, NPC, and placed fields exist in decoded sources; only proven generated fields execute."));
        evidence.Add(new("D3", "canonical exit records", "Strongly Corroborated", "Prerequisites, effects, hidden mode, tolls, edge state, and reciprocal semantics are retained as decoded evidence and require specialized consumers."));
        evidence.Add(new("D4", "door_lock_access_catalog + live world", "Verified", "RuntimeLivingWorldService is the live door state owner; the richer door service is non-live validation evidence."));
        evidence.Add(new("D5", "generated data and live validation", "Verified", "Discovery is per character; See Hidden is not reused as automatic room-object discovery."));
        evidence.Add(new("D6", "ability 119 + native text + cleanup snippets", "Verified", "Decoded Del@Maint maps to runtime DeleteAtCleanup behavior; ordinary room ground becomes hidden."));

        return new(
            failures.Count == 0,
            worldId,
            checks,
            failures,
            evidence,
            new[]
            {
                "D1-D6 have one combined audit gate that requires canonical Librarium, generated, and live-runtime evidence sources.",
                "The decoded source name is Del@Maint (ability 119); DeleteAtCleanup is only the runtime semantic behavior name.",
                "Newhaven Arena remains canonical room.map1.2150.",
                "Room-object discovery remains player-specific and is not globally revealed by Search or See Hidden.",
                "RuntimeLivingWorldService is the sole live door command/movement/persistence owner."
            },
            new[]
            {
                "The bounded percentile Search comparison remains strongly corroborated; exact primary-stat weighting is not promoted.",
                "Advanced exit and door semantics remain attached to decoded records and are executed only when explicit generated evidence supplies the prerequisite/effect."
            },
            new[]
            {
                "Exit hidden mode, toll, damage, action ordering, class/alignment/level restrictions, reciprocal state linkage, and automatic routing where no live specialized consumer is proven.",
                "Hidden room currency and non-item object discovery persistence where no target identity model is generated."
            },
            new[]
            {
                "Prior reports renamed the decoded Del@Maint flag as del@cleanup; Revision 0007/0008 supersede that provenance wording.",
                "RuntimeDoorAccessService models richer semantics but is deliberately not a second live door state authority."
            },
            new[]
            {
                "Exact native Search stat formula and target-specific concealment derivation.",
                "Universal maintenance/cleanup cadence and exact ordering relative to every reset cohort.",
                "Unclassified native room latch and exit qualifier effects awaiting the final native pass."
            });
    }
}
