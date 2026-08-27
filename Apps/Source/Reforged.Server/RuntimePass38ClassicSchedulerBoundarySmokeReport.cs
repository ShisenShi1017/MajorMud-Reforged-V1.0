using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass38ClassicSchedulerBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass38ClassicSchedulerBoundarySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicSchedulerEvidenceRegistry.Validate().Count == 0, "scheduler-evidence-registry-valid");
        Check(RuntimeClassicTimingAuthority.CombatRoundSeconds == 5, "classic-combat-round-5s");
        Check(RuntimeClassicTimingAuthority.SpellSystemRoundSeconds == 3, "classic-spell-system-round-3s");
        Check(RuntimeClassicTimingAuthority.PlayerDataSaveIntervalSeconds == 60, "classic-player-save-60s");
        Check(RuntimeClassicTimingAuthority.BufferDataSaveIntervalSeconds == 300, "classic-buffer-save-300s");
        Check(RuntimeClassicTimingAuthority.GracefulExitDelaySeconds == 5, "classic-exit-delay-5s");
        Check(RuntimeClassicTimingAuthority.FocusedRegenerationActivationSeconds == 20, "classic-focus-activation-20s");
        Check(RuntimeClassicTimingAuthority.PassiveRegenerationSeconds == 30, "classic-passive-regen-30s");
        Check(RuntimeClassicTimingAuthority.RestRegenerationSeconds == 20, "classic-rest-regen-20s");
        Check(RuntimeClassicTimingAuthority.MeditateRegenerationSeconds == 10, "classic-meditate-regen-10s");
        Check(RuntimeClassicLiveStatusCatalog.CombatRoundSeconds == RuntimeClassicTimingAuthority.CombatRoundSeconds, "live-status-combat-clock-centralized");
        Check(RuntimeClassicLiveStatusCatalog.SpellRoundSeconds == RuntimeClassicTimingAuthority.SpellSystemRoundSeconds, "live-status-spell-clock-centralized");
        Check(RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds == RuntimeClassicTimingAuthority.CombatRoundSeconds * 1000, "combat-scheduler-centralized");
        Check(RuntimeCharacterRegenerationAuthority.PassiveCadence == TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.PassiveRegenerationSeconds), "passive-regen-clock-centralized");
        Check(RuntimeCharacterRegenerationAuthority.RestCadence == TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.RestRegenerationSeconds), "rest-regen-clock-centralized");
        Check(RuntimeCharacterRegenerationAuthority.MeditateCadence == TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.MeditateRegenerationSeconds), "meditate-regen-clock-centralized");
        Check(!RuntimeClassicSchedulerEvidenceRegistry.UniversalTenSecondGameplayPhasePromoted, "no-universal-ten-second-clock");
        Check(!RuntimeClassicSchedulerEvidenceRegistry.ExactSameCycleOrderingResolved, "same-cycle-order-remains-gated");
        Check(!RuntimeClassicSchedulerEvidenceRegistry.ExactHostPhaseResolved, "host-phase-remains-gated");
        Check(!RuntimeClassicSchedulerEvidenceRegistry.ExactBackgroundCadenceResolved, "background-cadence-remains-gated");
        Check(!RuntimeClassicSchedulerEvidenceRegistry.ExactNpcPlayerFirstOrderingResolved, "npc-player-first-remains-gated");
        Check(!RuntimeClassicSchedulerEvidenceRegistry.ExactSpawnNextTickActionResolved, "spawn-next-tick-action-remains-gated");

        return new(failures.Count == 0, checks, failures);
    }
}
