using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcGoldSnapshot(string NpcInstanceId, long Gold, string Authority, DateTimeOffset? UpdatedAtUtc);
public sealed record RuntimeNpcGoldMutationResult(bool Applied, string NpcInstanceId, long PreviousGold, long CurrentGold, long Delta, string Reason);

/// <summary>
/// Exact world-owned NPC gold balance boundary. The caller owns all generation, pricing, reward,
/// merchant-funding, and loot-allocation policy. This boundary only validates a materialized NPC,
/// explicit authority, nonnegative balances, and checked exact mutation before persisting the result.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcGoldSnapshot InspectNpcGold(string instanceId)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var npc))
                throw new InvalidOperationException("instance-not-found");
            var gold = ReadNpcGold(npc.Metadata);
            var authority = npc.Metadata.TryGetValue("npc_gold_authority", out var value) ? value : string.Empty;
            DateTimeOffset? updated = null;
            if (npc.Metadata.TryGetValue("npc_gold_updated_at_utc", out var raw) && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) updated = parsed;
            return new(npc.InstanceId, gold, authority, updated);
        }
    }

    public RuntimeNpcGoldMutationResult SetNpcGoldExact(string instanceId, long exactGold, string authority, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var npc)) return Reject(instanceId, "instance-not-found");
            if (string.IsNullOrWhiteSpace(authority)) return Reject(instanceId, "authority-required", ReadNpcGold(npc.Metadata));
            if (exactGold < 0) return Reject(instanceId, "gold-must-be-nonnegative", ReadNpcGold(npc.Metadata));
            var previous = ReadNpcGold(npc.Metadata);
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_gold"] = exactGold.ToString(CultureInfo.InvariantCulture),
                ["npc_gold_authority"] = authority.Trim(),
                ["npc_gold_updated_at_utc"] = nowUtc.ToString("O"),
                ["npc_gold_last_operation"] = "set-exact"
            };
            _npcs[instanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, instanceId, previous, exactGold, exactGold - previous, "gold-set-exactly");
        }
    }

    public RuntimeNpcGoldMutationResult AdjustNpcGoldExact(string instanceId, long delta, string authority, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var npc)) return Reject(instanceId, "instance-not-found");
            var previous = ReadNpcGold(npc.Metadata);
            if (string.IsNullOrWhiteSpace(authority)) return Reject(instanceId, "authority-required", previous);
            long current;
            try { current = checked(previous + delta); }
            catch (OverflowException) { return Reject(instanceId, "gold-overflow-rejected", previous); }
            if (current < 0) return Reject(instanceId, "insufficient-npc-gold", previous);
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_gold"] = current.ToString(CultureInfo.InvariantCulture),
                ["npc_gold_authority"] = authority.Trim(),
                ["npc_gold_updated_at_utc"] = nowUtc.ToString("O"),
                ["npc_gold_last_operation"] = delta >= 0 ? "credit-exact" : "debit-exact",
                ["npc_gold_last_delta"] = delta.ToString(CultureInfo.InvariantCulture)
            };
            _npcs[instanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, instanceId, previous, current, delta, "gold-adjusted-exactly");
        }
    }

    private static long ReadNpcGold(IReadOnlyDictionary<string,string> metadata)
    {
        if (!metadata.TryGetValue("npc_gold", out var raw) || string.IsNullOrWhiteSpace(raw)) return 0;
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new InvalidOperationException("persisted-npc-gold-invalid");
        return value;
    }

    private static RuntimeNpcGoldMutationResult Reject(string instanceId, string reason, long current = 0) =>
        new(false, instanceId ?? string.Empty, current, current, 0, reason);
}

public sealed class RuntimeNpcGoldOwnershipSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcGoldOwnershipSmokeReport Run()
    {
        var report = new RuntimeNpcGoldOwnershipSmokeReport();
        try
        {
            var type = typeof(RuntimeLivingWorldService);
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.InspectNpcGold)) is not null, "npc-gold-inspection-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.SetNpcGoldExact)) is not null, "npc-gold-set-exact-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.AdjustNpcGoldExact)) is not null, "npc-gold-adjust-exact-exposed");
            report.Checks.AddRange(["npc-gold-inspection-exposed","npc-gold-set-exact-exposed","npc-gold-adjust-exact-exposed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
