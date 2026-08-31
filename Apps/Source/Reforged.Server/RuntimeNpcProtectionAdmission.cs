using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcProtectionAdmissionResult(
    bool Allowed,
    string TargetNpcInstanceId,
    string? BlockingNpcInstanceId,
    string? BlockingNpcName,
    string Reason);

public sealed record RuntimeClassicMonsGuardsTargetResult(
    string RequestedTargetNpcInstanceId,
    string EffectiveTargetNpcInstanceId,
    string EffectiveTargetNpcName,
    bool Redirected,
    int? GuardLegacyId,
    string Reason);

/// <summary>
/// NPC protection boundaries.
///
/// Classic ability 146 (MonsGuards) is not a generic ATTACK admission blocker. Native
/// _cast_monster_target scans the requested monster's ordered ability slots, then the
/// current room's 15 monster slots, and substitutes the first matching monster legacy id
/// as the effective spell target. The generic instance-id protector admission below is a
/// Reforged/custom-world extension and receives no Classic parity credit.
/// </summary>
public static class RuntimeNpcProtectionAdmission
{
    private static readonly string[] ProtectionKeys = ["protected_npc_instance_id", "guard_target_instance_id"];

    public static RuntimeClassicMonsGuardsTargetResult ResolveClassicSpellTarget(
        RuntimeLivingNpcState requestedTarget,
        IReadOnlyList<RuntimeLivingNpcState> roomNpcs)
    {
        ArgumentNullException.ThrowIfNull(requestedTarget);
        ArgumentNullException.ThrowIfNull(roomNpcs);

        var guardedLegacyIds = ReadLegacyIds(requestedTarget.Metadata, "guarded_npc_legacy_ids");
        if (guardedLegacyIds.Count == 0)
            return new(requestedTarget.InstanceId, requestedTarget.InstanceId, requestedTarget.Name, false, null, "no-monsguards-ability");

        // Native rooms hold 15 active monster references. NpcsInRoom already projects the
        // stable Classic active-slot order; retain only those first 15 candidates here.
        var nativeRoomSlots = roomNpcs
            .Where(npc => npc.Alive && npc.RoomId.Equals(requestedTarget.RoomId, StringComparison.OrdinalIgnoreCase))
            .Take(15)
            .ToArray();

        foreach (var guardedLegacyId in guardedLegacyIds)
        {
            foreach (var candidate in nativeRoomSlots)
            {
                if (candidate.InstanceId.Equals(requestedTarget.InstanceId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!TryReadLegacyId(candidate.Metadata, out var candidateLegacyId) || candidateLegacyId != guardedLegacyId) continue;
                return new(requestedTarget.InstanceId, candidate.InstanceId, candidate.Name, true, guardedLegacyId,
                    "classic-cast-monster-target-monsguards-substitution");
            }
        }

        return new(requestedTarget.InstanceId, requestedTarget.InstanceId, requestedTarget.Name, false, null,
            "monsguards-target-not-present-in-native-room-slots");
    }

    /// <summary>Reforged/custom-world explicit protector extension. Not a Classic MonsGuards rule.</summary>
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
            ? new(true, targetNpcInstanceId, null, null, "no-reforged-explicit-protector")
            : new(false, targetNpcInstanceId, blocker.InstanceId, blocker.Name, "reforged-explicit-protector-blocked-attack");
    }

    private static IReadOnlyList<int> ReadLegacyIds(IReadOnlyDictionary<string,string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return Array.Empty<int>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
            .Where(value => value > 0)
            .ToArray();
    }

    private static bool TryReadLegacyId(IReadOnlyDictionary<string,string> metadata, out int legacyId)
    {
        legacyId = 0;
        return metadata.TryGetValue("npc_legacy_id", out var raw) && int.TryParse(raw, out legacyId) && legacyId > 0;
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
            var protectedNpc = Npc("npc.target", "Protected", "room.1", 900, new Dictionary<string,string>
            {
                ["guarded_npc_legacy_ids"] = "653,652"
            });
            var wrongGuard = Npc("npc.slot.0", "Wrong", "room.1", 652, new Dictionary<string,string>());
            var firstGuard = Npc("npc.slot.1", "First Guard", "room.1", 653, new Dictionary<string,string>());
            var secondGuard = Npc("npc.slot.2", "Second Guard", "room.1", 653, new Dictionary<string,string>());

            var redirected = RuntimeNpcProtectionAdmission.ResolveClassicSpellTarget(protectedNpc, [wrongGuard, firstGuard, secondGuard, protectedNpc]);
            Require(redirected.Redirected && redirected.EffectiveTargetNpcInstanceId == firstGuard.InstanceId && redirected.GuardLegacyId == 653,
                "monsguards-ability-order-before-room-slot-order");
            report.Checks.Add("monsguards-ability-order-before-room-slot-order");

            var absent = RuntimeNpcProtectionAdmission.ResolveClassicSpellTarget(protectedNpc, [protectedNpc]);
            Require(!absent.Redirected && absent.EffectiveTargetNpcInstanceId == protectedNpc.InstanceId,
                "monsguards-no-present-guard-keeps-original-target");
            report.Checks.Add("monsguards-no-present-guard-keeps-original-target");

            var genericProtected = Npc("npc.generic", "Generic", "room.1", 901, new Dictionary<string,string>());
            var genericProtector = Npc("npc.blocker", "Protector", "room.1", 902, new Dictionary<string,string>
            {
                ["protected_npc_instance_id"] = genericProtected.InstanceId
            });
            var blocked = RuntimeNpcProtectionAdmission.Admit(genericProtected.InstanceId, "room.1", [genericProtected, genericProtector]);
            Require(!blocked.Allowed && blocked.BlockingNpcInstanceId == genericProtector.InstanceId, "reforged-protector-extension-still-available");
            report.Checks.Add("reforged-protector-extension-still-available");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static RuntimeLivingNpcState Npc(string id, string name, string room, int legacyId, IReadOnlyDictionary<string,string> metadata)
    {
        var combined = new Dictionary<string,string>(metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["npc_legacy_id"] = legacyId.ToString()
        };
        return new(id, "spawn.1", $"npc.{legacyId}", name, room, room, 10, 10, true, null, "none", 1, combined);
    }
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
