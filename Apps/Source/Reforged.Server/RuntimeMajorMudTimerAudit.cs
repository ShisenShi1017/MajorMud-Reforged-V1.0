using System.Text;

namespace Reforged.Server;

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
/// Conservative MajorMUD timer/reset audit. It reports proven timer values separately
/// from still-unresolved host-clock, room-entry, reset-order, and restart semantics.
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
        diagnostics.Add("Ordinary lair room Delay is projected in minutes, but exact wall-clock-minute gating remains evidence-gated.");
        diagnostics.Add("Last Killed/regen evidence is now active as room-entry admission: elapsed NPC respawn timers do not materialize from the global heartbeat; a player entering the home room admits due spawns.");
        diagnostics.Add("Zero-delay room semantics remain disputed and receive no parity credit.");
        diagnostics.Add("WCCMMUD.DLL drives a one-second one-shot background-fast callback; internal medium/energy/monster-create/slow buckets are due at 3/5/5/30 logical fast ticks respectively.");
        diagnostics.Add("Delayed background-fast service increments the logical tick once and re-arms from the execution moment; elapsed wall-clock catch-up is not synthesized while running.");
        diagnostics.Add("REST increments its native fast counter and fires when it exceeds 20 (21st fast callback). MEDITATE fires when its counter exceeds 14 (15th fast callback), despite historical HELP saying at least 20 seconds for both.");
        diagnostics.Add("There is no evidence-backed single universal 10-second Classic gameplay phase: combat, spell/status, regeneration, save, exit, and room-entry respawn owners use distinct recovered boundaries.");
        diagnostics.Add("Room reset ordering, hard-reset exceptions, world reset ordering, restart deadline persistence, and full cross-owner simultaneous-event ordering remain unresolved.");
        diagnostics.Add($"Native WCCMMUD player-data save interval is {RuntimeClassicTimingAuthority.PlayerDataSaveIntervalSeconds}s; native buffer-data prompt is {RuntimeClassicTimingAuthority.BufferDataSaveIntervalSeconds}s, but exact buffer ownership/order remains UNKNOWN.");
        diagnostics.Add("Existing in-flight respawn deadlines are preserved across upgrade/restore; the new timer authority is guaranteed for newly scheduled deaths rather than retroactively rewriting uncertain persisted deadlines.");

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
        builder.AppendLine($"- Native minute gate pending: {report.NativeMinuteGatePendingCount}");
        builder.AppendLine($"- Native room-entry admission pending: {report.NativeRoomEntryPendingCount}");
        builder.AppendLine($"- Zero-delay UNKNOWN: {report.ZeroDelayUnknownCount}");
        builder.AppendLine($"- Direct NPCs still using unverified room-delay minutes: {report.UnverifiedDirectRoomDelayCount}");
        builder.AppendLine();
        builder.AppendLine("RECOVERED CADENCES (implementation/evidence; not all receive strict parity credit)");
        builder.AppendLine($"- Passive HP regen cadence: {report.PassiveRegenSeconds}s");
        builder.AppendLine($"- REST native trigger: every {report.RestSeconds} serviced fast callbacks (HELP says at least 20 seconds)");
        builder.AppendLine($"- MEDITATE native trigger: every {report.MeditateSeconds} serviced fast callbacks (DLL overrides conflicting 20-second HELP guidance)");
        builder.AppendLine($"- Runtime combat round: {report.CombatRoundSeconds}s");
        builder.AppendLine($"- Runtime spell/system round: {report.SpellRoundSeconds}s");
        builder.AppendLine($"- Recovered MajorMUD combat round evidence: {report.RecoveredCombatRoundSeconds}s");
        builder.AppendLine($"- Native WCCMMUD player-data save interval: {report.NativePlayerSaveSeconds}s");
        builder.AppendLine($"- Native WCCMMUD buffer-data save interval: {report.NativeBufferSaveSeconds}s (owner/order still evidence-gated)");
        builder.AppendLine();
        builder.AppendLine("KNOWN NATIVE BOUNDARIES");
        foreach (var diagnostic in report.Diagnostics)
            builder.AppendLine($"- {diagnostic}");
        return builder.ToString().TrimEnd();
    }
}
