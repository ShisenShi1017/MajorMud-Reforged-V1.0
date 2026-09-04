using System.Text;

namespace RealmGate.Server;

public sealed record RuntimeMajorMudTimerAuditReport(
    int ObservedRoomBossTimerCount,
    int ObservedNamedNpcTimerCount,
    int LiveNpcCount,
    IReadOnlyDictionary<string, int> LiveTimerModes,
    int NativeMinuteGatePendingCount,
    int NativeRoomEntryPendingCount,
    int ZeroDelayUnknownCount,
    int UnverifiedDirectRoomDelayCount,
    int CombatRoundSeconds,
    int SpellRoundSeconds,
    int RecoveredCombatRoundSeconds,
    int PassiveRegenSeconds,
    int RestSeconds,
    int MeditateSeconds,
    int NativePlayerSaveSeconds,
    int NativeBufferSaveSeconds,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// MajorMUD timer/reset audit after CodeUpdate0368 native scheduler, room-delay,
/// generation-admission, reset-owner, and configuration closure. Legacy pending counters
/// remain visible only to identify snapshots written before the closure.
/// </summary>
public static class RuntimeMajorMudTimerAudit
{
    public const int RecoveredCombatRoundSeconds = RuntimeClassicTimingAuthority.CombatRoundSeconds;

    public static RuntimeMajorMudTimerAuditReport Run(string projectRoot, RuntimeLivingWorldService living)
    {
        var catalog = RuntimeMajorMudSpawnTimingCatalog.Load(projectRoot);
        var npcs = living.Capture().Npcs;
        var modes = npcs
            .GroupBy(npc => npc.Metadata.GetValueOrDefault("npc_respawn_timer_mode") ?? "unclassified", StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        static bool IsTrue(RuntimeLivingNpcState npc, string key) =>
            npc.Metadata.TryGetValue(key, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);

        var diagnostics = new List<string>();
        var runtimeCombatRoundSeconds = RuntimeClassicLiveStatusCatalog.CombatRoundSeconds;
        var runtimeSpellRoundSeconds = RuntimeClassicLiveStatusCatalog.SpellRoundSeconds;
        var runtimeSpellSystemRoundMilliseconds = RuntimeLiveGameplayFacade.ClassicSpellSystemRoundMilliseconds;
        if (runtimeCombatRoundSeconds != RecoveredCombatRoundSeconds)
            diagnostics.Add($"Combat round mismatch: runtime={runtimeCombatRoundSeconds}s, recovered={RecoveredCombatRoundSeconds}s.");
        if (runtimeSpellRoundSeconds != 3)
            diagnostics.Add($"Spell/system round mismatch: runtime={runtimeSpellRoundSeconds}s, recovered=3s.");
        if (runtimeSpellSystemRoundMilliseconds != runtimeSpellRoundSeconds * 1000)
            diagnostics.Add($"Spell/status scheduler mismatch: runtime={runtimeSpellSystemRoundMilliseconds}ms, recovered={runtimeSpellRoundSeconds * 1000}ms.");
        diagnostics.Add("Native _generate_monster proves Room Delay is an integer-minute gate: Delay 0 is immediately eligible; positive Delay is blocked while elapsed whole minutes <= Delay and becomes eligible only when elapsed > Delay.");
        diagnostics.Add("Native generation has two live admission paths: _user_allowed_in_room on room entry and _background_monster_create against active-user rooms every five-second owner generation.");
        diagnostics.Add("WCCMMUD.DLL freshly arms fast/medium/energy/monster-create/slow and save-related one-shot callbacks on module startup; callback deadlines are not restored or caught up across restart.");
        diagnostics.Add("Delayed one-shot owner service executes one generation and re-arms that owner from execution time; independent owner deadlines are not derived from fast-tick modulo phase.");
        diagnostics.Add("REST increments its native fast counter and fires when it exceeds 20 (21st fast callback). MEDITATE fires when its counter exceeds 14 (15th fast callback), despite historical HELP saying at least 20 seconds for both.");
        diagnostics.Add("There is no evidence-backed single universal 10-second Classic gameplay phase: combat, spell/status, regeneration, save, exit, and room-entry respawn owners use distinct recovered boundaries.");
        diagnostics.Add("SYSOP RESET is not a universal reset transaction: ROOM clears only the room generation count; AREA clears only the linked area population count. Cleanup/recharge/restock/initialize/player/combat/corpse owners remain separate.");
        diagnostics.Add($"Native background_other owns Player-data save at {RuntimeClassicTimingAuthority.PlayerDataSaveIntervalSeconds}s; background_save_buffers owns Buffer-data save at {RuntimeClassicTimingAuthority.BufferDataSaveIntervalSeconds}s. Both are freshly armed one-shot owners.");
        diagnostics.Add("Persisted Reforged NPC respawn deadlines survive application restart because they represent native Last-Killed-derived eligibility, while native scheduler callback phase itself is freshly armed on startup.");

        return new RuntimeMajorMudTimerAuditReport(
            catalog.RoomBossTimerCount,
            catalog.NpcNameTimerCount,
            npcs.Count,
            modes,
            npcs.Count(npc => IsTrue(npc, "npc_respawn_native_minute_gate_pending")),
            npcs.Count(npc => IsTrue(npc, "npc_respawn_native_room_entry_pending")),
            npcs.Count(npc => (npc.Metadata.GetValueOrDefault("npc_respawn_timer_mode") ?? string.Empty).Equals("generated-room-delay-zero", StringComparison.OrdinalIgnoreCase)),
            npcs.Count(npc => (npc.Metadata.GetValueOrDefault("npc_respawn_timer_mode") ?? string.Empty).Equals("unverified-direct-room-delay-minutes", StringComparison.OrdinalIgnoreCase)),
            RuntimeClassicLiveStatusCatalog.CombatRoundSeconds,
            RuntimeClassicLiveStatusCatalog.SpellRoundSeconds,
            RecoveredCombatRoundSeconds,
            (int)RuntimeCharacterRegenerationAuthority.PassiveCadence.TotalSeconds,
            (int)RuntimeCharacterRegenerationAuthority.RestCadence.TotalSeconds,
            (int)RuntimeCharacterRegenerationAuthority.MeditateCadence.TotalSeconds,
            RuntimeClassicTimingAuthority.PlayerDataSaveIntervalSeconds,
            RuntimeClassicTimingAuthority.BufferDataSaveIntervalSeconds,
            diagnostics);
    }

    public static string Render(RuntimeMajorMudTimerAuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("MAJORMUD TIMER / RESET AUDIT");
        builder.AppendLine($"Observed fixed boss-room timers: {report.ObservedRoomBossTimerCount}");
        builder.AppendLine($"Observed named-NPC timer fallbacks: {report.ObservedNamedNpcTimerCount}");
        builder.AppendLine($"Live NPC instances: {report.LiveNpcCount}");
        builder.AppendLine();
        builder.AppendLine("LIVE TIMER AUTHORITY");
        foreach (var pair in report.LiveTimerModes)
            builder.AppendLine($"- {pair.Key}: {pair.Value}");
        builder.AppendLine($"- Legacy pre-0368 minute-gate-pending metadata: {report.NativeMinuteGatePendingCount}");
        builder.AppendLine($"- Legacy pre-0368 room-entry-pending metadata: {report.NativeRoomEntryPendingCount}");
        builder.AppendLine($"- Legacy pre-0368 zero-delay UNKNOWN mode: {report.ZeroDelayUnknownCount}");
        builder.AppendLine($"- Legacy pre-0368 unverified direct-delay mode: {report.UnverifiedDirectRoomDelayCount}");
        builder.AppendLine();
        builder.AppendLine("RECOVERED CADENCES (implementation/evidence; not all receive strict parity credit)");
        builder.AppendLine($"- Passive HP regen cadence: {report.PassiveRegenSeconds}s");
        builder.AppendLine($"- REST native trigger: every {report.RestSeconds} serviced fast callbacks (HELP says at least 20 seconds)");
        builder.AppendLine($"- MEDITATE native trigger: every {report.MeditateSeconds} serviced fast callbacks (DLL overrides conflicting 20-second HELP guidance)");
        builder.AppendLine($"- Runtime combat round: {report.CombatRoundSeconds}s");
        builder.AppendLine($"- Runtime spell/system round: {report.SpellRoundSeconds}s");
        builder.AppendLine($"- Recovered MajorMUD combat round evidence: {report.RecoveredCombatRoundSeconds}s");
        builder.AppendLine($"- Native WCCMMUD player-data save interval: {report.NativePlayerSaveSeconds}s");
        builder.AppendLine($"- Native WCCMMUD buffer-data save interval: {report.NativeBufferSaveSeconds}s (background_save_buffers owner)");
        builder.AppendLine();
        builder.AppendLine("KNOWN NATIVE BOUNDARIES");
        foreach (var diagnostic in report.Diagnostics)
            builder.AppendLine($"- {diagnostic}");
        return builder.ToString().TrimEnd();
    }
}
