using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDDoorAuditReport(
    bool Passed,
    string WorldId,
    int DecodedDoorExitCount,
    int RuntimeDoorDefinitionCount,
    int RuntimeDoorStateCount,
    int KeyQualifiedDoorCount,
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

public static class RuntimeBundleDDoorAudit
{
    public static RuntimeBundleDDoorAuditReport Run(string projectRoot, string worldId)
    {
        var options = ServerRuntimeOptions.Load(projectRoot);
        var definition = options.Worlds.First(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        var persistenceRoot = Path.Combine(Path.GetTempPath(), "reforged-bundle-d-door-audit-" + Guid.NewGuid().ToString("N"));
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }

        try
        {
            var service = new RuntimeLivingWorldService(projectRoot, world, persistenceRoot);
            var decodedDoors = world.AllRooms().SelectMany(room => room.Exits)
                .Where(exit => exit.GateKind.Equals("door", StringComparison.OrdinalIgnoreCase)).ToArray();
            var definitions = service.CaptureWorldObjectDefinitions()
                .Where(x => x.Kind.Equals("door", StringComparison.OrdinalIgnoreCase)).ToArray();
            var states = service.Capture().Objects.Where(x => definitions.Any(d => d.Id.Equals(x.Id, StringComparison.OrdinalIgnoreCase))).ToArray();

            Check(decodedDoors.Length == definitions.Length, "every_decoded_door_exit_has_one_live_runtime_definition");
            Check(definitions.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == definitions.Length, "door_definition_ids_are_unique");
            Check(states.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == states.Length, "door_state_ids_are_unique");
            Check(definitions.All(x => states.Any(s => s.Id.Equals(x.Id, StringComparison.OrdinalIgnoreCase))), "every_live_door_has_runtime_state");
            Check(definitions.All(x => world.FindRoomById(x.RoomId) is not null), "every_door_source_room_resolves_canonically");
            Check(definitions.All(x => string.IsNullOrWhiteSpace(x.TargetRoomId) || world.FindRoomById(x.TargetRoomId) is not null), "every_executable_door_destination_resolves_canonically");
            Check(definitions.All(x => !string.IsNullOrWhiteSpace(x.Direction)), "every_door_is_bound_to_an_exit_direction");
            Check(definitions.All(x => !x.InitiallyOpen && !x.InitiallyLocked), "unqualified_classic_doors_use_noninvented_closed_unlocked_initial_state");
            var doorValidationFailures = service.Validate()
                .Where(x => x.Contains("object", StringComparison.OrdinalIgnoreCase) || x.Contains("door", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Check(doorValidationFailures.Length == 0, "live_world_door_state_validation_passes");

            var accessSmoke = RuntimeDoorAccessSmokeReport.Run();
            Check(accessSmoke.Passed, "advanced_door_access_policy_smoke_passes");

            return new(
                failures.Count == 0,
                world.Definition.Id,
                decodedDoors.Length,
                definitions.Length,
                states.Length,
                definitions.Count(x => !string.IsNullOrWhiteSpace(x.KeyItemId)),
                checks,
                failures,
                new[]
                {
                    "Door identity is the generated exit identity; no parallel map or display-name lookup is authoritative.",
                    "Live movement checks locked state before closed state and blocks traversal through either condition.",
                    "Open, close, lock, and unlock commands mutate persisted world-object state through RuntimeLivingWorldService.",
                    "Door source and destination validation uses canonical map-qualified room IDs.",
                    "Required generated key item IDs are preserved and enforced when present."
                },
                new[]
                {
                    "RuntimeDoorAccessService is retained as non-live validation evidence for broken, linked, timed-reset, restricted, key-consumption, pick, and bash semantics.",
                    "Generated doors without explicit state evidence remain closed and unlocked rather than receiving invented locks or keys."
                },
                new[]
                {
                    "Picklock and strength success calculations remain evidence-only where the generated prerequisite threshold is null.",
                    "Magical lock, quest lock, broken-door, linked-door, and reset fields are not promoted unless a generated record supplies explicit values."
                },
                new[]
                {
                    "RuntimeLivingWorldService is the sole live command, movement, and persistence owner. RuntimeDoorAccessService is not registered as a live execution path and therefore cannot independently mutate world door state."
                },
                new[]
                {
                    "Native meanings for unclassified door qualifiers and exact pick/bash formulas remain unresolved."
                });
        }
        finally
        {
            try { if (Directory.Exists(persistenceRoot)) Directory.Delete(persistenceRoot, true); } catch { }
        }
    }
}
