using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNpcAiNativeClosureAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    int NpcCount,
    int MonsGuardsOwnerCount,
    int Message8480AttackRows,
    int Message8480BoundRows,
    int Message8480UnboundRows);

public static class RuntimeClassicNpcAiNativeClosureAudit
{
    public static RuntimeClassicNpcAiNativeClosureAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string code) { checks.Add(code); if (!condition) failures.Add(code); }

        var npcPath = Path.Combine(projectRoot, "Data", "Classic", "npcs.json");
        var rows = RuntimeChunkedClassicCatalog.LoadRecords(npcPath);
        var npcs = rows.Select(RuntimeNpcDefinition.FromJson).ToArray();

        Check(npcs.Length == 1100, "classic-npc-count-1100");
        Check(npcs.All(npc => npc.FollowValue is >= 0 and <= 100), "native-int16-follow-percent-corpus-range");
        Check(npcs.SelectMany(npc => npc.Attacks).All(attack => attack.SelectionPercent is >= 0 and <= 255), "native-byte-attack-percent-range");
        Check(npcs.SelectMany(npc => npc.Attacks).Any(attack => attack.SelectionPercent > 100), "native-byte-attack-percent-over-100-preserved");

        var guards = npcs.Where(npc => npc.GuardedNpcLegacyIds.Count > 0).ToArray();
        var ids = npcs.Select(npc => npc.LegacyId).ToHashSet();
        Check(guards.Length == 112, "monsguards-owner-count-112");
        Check(guards.SelectMany(npc => npc.GuardedNpcLegacyIds).All(ids.Contains), "monsguards-targets-resolve-to-classic-npcs");

        var message8480Rows = npcs.SelectMany(npc => npc.Attacks.Select(attack => (Npc: npc, Attack: attack)))
            .Where(row => row.Attack.MissMessageLegacyId == 8480)
            .ToArray();
        var bound8480 = message8480Rows.Where(row => !string.IsNullOrWhiteSpace(row.Attack.MissVerb) && !string.IsNullOrWhiteSpace(row.Attack.WeaponName)).ToArray();
        var unbound8480 = message8480Rows.Except(bound8480).ToArray();
        Check(message8480Rows.Length == 9, "message-8480-nine-physical-rows");
        Check(bound8480.Length == 4 && bound8480.All(row => row.Npc.LegacyId == 59), "message-8480-thrag-four-rows-bound");
        Check(unbound8480.Length == 5 && unbound8480.All(row => row.Npc.LegacyId == 1006), "message-8480-nahr-five-rows-remain-unbound");

        var payloadPath = Path.Combine(projectRoot, "Data", "Classic", "native_message_payloads.json");
        var payload8480 = RuntimeChunkedClassicCatalog.LoadRecords(payloadPath)
            .Single(row => JsonValueReader.ReadInt(row, "message_id", 0) == 8480);
        Check(JsonValueReader.ReadString(payload8480, "player_payload", string.Empty) == "%s %s %s with his %s, but %s dodges!", "message-8480-is-combat-dodge-template-not-assist-broadcast");

        return new(failures.Count == 0, checks, failures, npcs.Length, guards.Length, message8480Rows.Length, bound8480.Length, unbound8480.Length);
    }
}
