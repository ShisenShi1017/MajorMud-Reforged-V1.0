using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeDoorAccessSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeDoorAccessSmokeReport Run()
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }
        var empty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RuntimeDoorDefinition Define(string id, string linked, bool open, bool locked) => new(
            id, "exit:" + id, id == "east" ? "room:1" : "room:2", id == "east" ? "room:2" : "room:1", "iron door", open, locked, false,
            "key:iron", true, 5, new HashSet<string>(new[] { "human" }, StringComparer.OrdinalIgnoreCase), empty, empty, empty,
            new HashSet<string>(new[] { "quest:gate" }, StringComparer.OrdinalIgnoreCase), empty, empty, null, null, linked, TimeSpan.FromMinutes(10), RuntimeDoorPersistence.WorldState, true, true);
        var service = new RuntimeDoorAccessService(new[] { Define("east", "west", false, true), Define("west", "east", false, true) });
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var actor = new RuntimeDoorActorContext("actor", 10, "human", null, null, empty, new HashSet<string>(new[] { "quest:gate" }, StringComparer.OrdinalIgnoreCase), new HashSet<string>(new[] { "lockpick", "bash-door" }, StringComparer.OrdinalIgnoreCase), empty, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["key:iron"] = 1 }, now);
        Check(service.EvaluateTraversal("east", actor).Code == "door-locked", "locked-blocks-traversal");
        var unlock = service.Act("east", RuntimeDoorAction.Unlock, actor, "key:iron");
        Check(unlock.Allowed && unlock.ConsumedKeyId == "key:iron", "consumable-key-request");
        Check(service.CaptureStates().All(x => !x.Locked), "linked-unlock-synchronized");
        Check(service.Act("east", RuntimeDoorAction.Open, actor).Allowed, "open-after-unlock");
        var traversal = service.EvaluateTraversal("east", actor);
        Check(traversal.Allowed && traversal.TrapCheckRequired && traversal.MovementMayProceed, "ordering-trap-before-movement");
        var restricted = actor with { RaceId = "elf" };
        Check(service.EvaluateTraversal("east", restricted).Code == "access-race-restricted", "race-restriction-fail-closed");
        Check(service.Tick(now.AddMinutes(11)).Count > 0, "door-reset-timer");
        Check(service.CaptureStates().All(x => x.Locked && !x.Open), "door-reset-state");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
    }
}
