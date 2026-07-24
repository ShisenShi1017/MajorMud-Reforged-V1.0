using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcProtectionAdmissionResult(
    bool Allowed,
    string TargetNpcInstanceId,
    string? BlockingNpcInstanceId,
    string? BlockingNpcName,
    string Reason);

/// <summary>
/// Authoritative Classic protected-NPC attack boundary. Classic has no creature factions,
/// reputation thresholds, or NPC-vs-NPC hostility. A living NPC may explicitly protect one
/// specific NPC in the same room. When present, the protector moves between the player and the
/// protected target and the requested attack is blocked.
/// </summary>
public static class RuntimeNpcProtectionAdmission
{
    private static readonly string[] ProtectionKeys = ["protected_npc_instance_id", "guard_target_instance_id"];

    public static RuntimeNpcProtectionAdmissionResult Admit(
        string targetNpcInstanceId,
        string roomId,
        IReadOnlyList<RuntimeLivingNpcState> roomNpcs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNpcInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(roomNpcs);

        var blocker = roomNpcs
            .Where(npc => npc.Alive
                && npc.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase)
                && !npc.InstanceId.Equals(targetNpcInstanceId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(npc => npc.InstanceId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(npc => ProtectionKeys.Any(key =>
                npc.Metadata.TryGetValue(key, out var protectedId)
                && protectedId.Equals(targetNpcInstanceId, StringComparison.OrdinalIgnoreCase)));

        return blocker is null
            ? new(true, targetNpcInstanceId, null, null, "no-explicit-protector")
            : new(false, targetNpcInstanceId, blocker.InstanceId, blocker.Name, "explicit-protector-blocked-attack");
    }
}

public sealed class RuntimeNpcProtectionAdmissionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcProtectionAdmissionSmokeReport Run()
    {
        var report = new RuntimeNpcProtectionAdmissionSmokeReport();
        try
        {
            var protectedNpc = Npc("npc.target", "Protected", "room.1", new Dictionary<string,string>());
            var protector = Npc("npc.blocker", "Protector", "room.1", new Dictionary<string,string>
            {
                ["protected_npc_instance_id"] = protectedNpc.InstanceId
            });
            var unrelatedHostileNpc = Npc("npc.hostile", "Hostile", "room.1", new Dictionary<string,string>
            {
                ["disposition"] = "hostile"
            });

            var blocked = RuntimeNpcProtectionAdmission.Admit(protectedNpc.InstanceId, "room.1", [protectedNpc, protector, unrelatedHostileNpc]);
            Require(!blocked.Allowed && blocked.BlockingNpcInstanceId == protector.InstanceId, "explicit-protector-blocks-attack");
            report.Checks.Add("explicit-protector-blocks-attack");

            var allowed = RuntimeNpcProtectionAdmission.Admit(unrelatedHostileNpc.InstanceId, "room.1", [protectedNpc, protector, unrelatedHostileNpc]);
            Require(allowed.Allowed, "unprotected-npc-attack-admitted");
            report.Checks.Add("unprotected-npc-attack-admitted");

            var differentRoom = protector with { RoomId = "room.2" };
            Require(RuntimeNpcProtectionAdmission.Admit(protectedNpc.InstanceId, "room.1", [protectedNpc, differentRoom]).Allowed,
                "protector-must-be-in-same-room");
            report.Checks.Add("protector-must-be-in-same-room");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static RuntimeLivingNpcState Npc(string id, string name, string room, IReadOnlyDictionary<string,string> metadata)
        => new(id, "spawn.1", id, name, room, room, 10, 10, true, null, "none", 1, metadata);
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
