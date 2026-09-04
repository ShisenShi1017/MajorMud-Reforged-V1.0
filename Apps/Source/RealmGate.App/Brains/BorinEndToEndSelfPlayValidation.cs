using RealmGate.App.Models;

namespace RealmGate.App.Brains;

internal sealed record BorinEndToEndSelfPlayValidationReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    string Detail);

/// <summary>
/// A model-free, network-free lifecycle validation that composes the autonomous
/// subsystems added across Code Updates 0271-0280. This complements the focused
/// character tests by proving the components agree on one continuous player story.
/// </summary>
internal static class BorinEndToEndSelfPlayValidation
{
    public static async Task<BorinEndToEndSelfPlayValidationReport> RunAsync(
        string temporaryRoot,
        CancellationToken token = default)
    {
        var checks = new List<string>();
        var failures = new List<string>();

        void Check(bool condition, string description)
        {
            if (condition)
                checks.Add(description);
            else
                failures.Add(description);
        }

        var state = HealthyState();
        var personalExperience = new BorinGameplayLearningSnapshot(
            "Omniverse BBS",
            "Classic",
            "Verified personal experience is available.",
            new[]
            {
                "LEVELING • At level 10, Harbor Road produced steady experience with survivable fights.",
                "DANGER • The eastern crypt repeatedly caused near-defeat at this level.",
            },
            18,
            DateTimeOffset.Now,
            Path.Combine(temporaryRoot, "AI", "Memory", "borin-gameplay-learning.json"));
        var librarium = new BorinLibrariumPlayerGuidance(
            new[]
            {
                "LEVELING/SCOUTING CANDIDATE • Harbor Road • moderate XP/HP evidence.",
                "NPC SPAWN REFERENCE FOR CURRENT ROOM • Quartermaster.",
                "QUEST/NPC FLOW CANDIDATE • Quartermaster -> SUPPLIES keyword.",
                "ITEMS/SHOPS • Newhaven weapon shop has equipment references; live LIST decides stock and price.",
            },
            "Structured Librarium reference candidates; live server state remains authoritative.");

        var progression = BorinProgressionPlanner.Build(state, personalExperience, librarium, string.Empty);
        Check(
            progression.Evidence.Any(value => value.Contains("PERSONAL", StringComparison.OrdinalIgnoreCase)) &&
            progression.Priorities.Any(value => value.Contains("verified", StringComparison.OrdinalIgnoreCase) || value.Contains("level", StringComparison.OrdinalIgnoreCase)),
            "Progression planning combines personal leveling experience with Librarium guidance.");

        var equipment = BorinEquipmentInventoryPlanner.Build(state, personalExperience, librarium);
        Check(
            equipment.Priorities.Any(value => value.Contains("LIST", StringComparison.OrdinalIgnoreCase)) &&
            equipment.Priorities.Any(value => value.Contains("SELL", StringComparison.OrdinalIgnoreCase) || value.Contains("DROP", StringComparison.OrdinalIgnoreCase)),
            "Equipment/shop planning requires live verification before transactions and protects irreversible item actions.");

        var quest = BorinQuestNpcPlanner.Build(state, personalExperience, librarium);
        Check(
            quest.Priorities.Any(value => value.Contains("one", StringComparison.OrdinalIgnoreCase) && value.Contains("step", StringComparison.OrdinalIgnoreCase)) &&
            quest.Evidence.Any(value => value.Contains("FLOW", StringComparison.OrdinalIgnoreCase)),
            "Quest planning sees decoded flow evidence but advances only one live-verified step at a time.");

        Check(
            BorinAutonomousSafetyPolicy.Validate("ATTACK giant rat", state).Allowed &&
            BorinAutonomousSafetyPolicy.Validate("EQUIP iron sword", state).Allowed,
            "Ordinary combat and fresh-evidence equipment actions remain autonomous.");
        Check(
            !BorinAutonomousSafetyPolicy.Validate("QUIT", state).Allowed &&
            !BorinAutonomousSafetyPolicy.Validate("NORTH;SELL sword", state).Allowed,
            "Account/session control and command batching remain blocked deterministically.");

        var staleInventory = state with { InventoryFreshness = "not captured" };
        Check(
            !BorinAutonomousSafetyPolicy.Validate("SELL iron sword", staleInventory).Allowed,
            "Irreversible possession changes remain blocked when live inventory evidence is stale/missing.");

        var recovery = new BorinSelfPlayRecoveryTracker();
        var noProgress = state with { LastCommand = "NORTH", CapturedAt = state.CapturedAt.AddSeconds(1) };
        recovery.RecordOutcome("NORTH", state, noProgress, Array.Empty<string>(), freshServerEvidence: true);
        recovery.RecordOutcome("NORTH", state, noProgress, new[] { "MovementBlocked" }, freshServerEvidence: true);
        Check(
            recovery.ShouldSuppressAction("NORTH", state, out _),
            "Two same-context no-progress/failure outcomes suppress a third blind repeat.");

        var progressed = state with
        {
            CurrentRoom = "Harbor Road",
            CurrentRoomKey = "harbor-road",
            ExperienceCurrent = state.ExperienceCurrent + 125,
            CapturedAt = state.CapturedAt.AddSeconds(2),
        };
        recovery.RecordOutcome("WEST", state, progressed, new[] { "ExperienceGain" }, freshServerEvidence: true);
        Check(
            recovery.BuildGuidance(progressed).EscalationLevel < 3,
            "Verified room/XP progress clears the immediate abandon-goal escalation instead of creating a permanent action ban.");

        var lifecycleRoot = Path.Combine(temporaryRoot, "lifecycle");
        Directory.CreateDirectory(lifecycleRoot);
        var lifecycle = new BorinPlayerSessionLifecycleStore();
        await lifecycle.ConfigureAsync(lifecycleRoot, "borin-e2e", token);
        var warning = await lifecycle.BeginCleanupAsync(
            BorinCleanupSignalKind.Warning,
            "Server cleanup warning",
            "Continue leveling safely",
            resumeAutonomousPlay: true,
            progressed,
            token);
        Check(warning.Exists && warning.ResumeAutonomousPlay, "Cleanup warning checkpoints the saved autonomous assignment.");

        var consolidated = await lifecycle.CompleteConsolidationAsync(personalExperience, "Verified session reflection.", token);
        Check(consolidated.ConsolidatedAt is not null, "Cleanup checkpoint receives durable memory consolidation before resume.");

        var disconnected = await lifecycle.MarkDisconnectedAsync("Server disconnected for cleanup.", token);
        Check(
            BorinPlayerSessionLifecycleStore.HasConfirmedCleanupMemoryBoundary(disconnected),
            "Actual cleanup disconnect establishes the durable memory-push boundary.");

        var resumed = await lifecycle.MarkResumedAsync(progressed, token);
        Check(resumed.ResumedAt is not null, "Post-cleanup verified state marks the lifecycle resumed.");

        var diagnosticsRoot = Path.Combine(temporaryRoot, "diagnostics");
        Directory.CreateDirectory(diagnosticsRoot);
        var diagnostics = new BorinSelfPlayRuntimeDiagnostics();
        diagnostics.Configure(diagnosticsRoot, "borin-e2e", enabled: true, maximumFileKilobytes: 128, retentionFiles: 2);
        diagnostics.Record("CommandSent", "password=hunter2 token=abc123 ordinary validation event", state, "LOOK");
        var diagnosticSummary = diagnostics.Snapshot();
        var diagnosticText = File.Exists(diagnosticSummary.StoragePath)
            ? await File.ReadAllTextAsync(diagnosticSummary.StoragePath, token)
            : string.Empty;
        Check(
            diagnosticSummary.BufferedEvents == 1 &&
            diagnosticText.Contains("[REDACTED]", StringComparison.Ordinal) &&
            !diagnosticText.Contains("hunter2", StringComparison.Ordinal) &&
            !diagnosticText.Contains("abc123", StringComparison.Ordinal),
            "Runtime self-play diagnostics are bounded/high-level and redact credential-like values.");

        return new BorinEndToEndSelfPlayValidationReport(
            failures.Count == 0,
            checks,
            failures,
            failures.Count == 0
                ? $"PASS {checks.Count}/{checks.Count} integrated autonomous-player lifecycle checks."
                : $"FAIL {failures.Count} integrated autonomous-player lifecycle checks: {string.Join(" | ", failures)}");
    }

    private static BorinGameStateSnapshot HealthyState() => BorinGameStateSnapshot.Empty with
    {
        Phase = BorinGamePhase.InGame,
        PhaseDetail = "Synthetic verified MajorMUD prompt for deterministic validation.",
        ConnectionState = ClientConnectionState.Connected.ToString(),
        LoginState = BorinLoginState.Ready,
        IsConnected = true,
        IsLoggedIntoMajorMud = true,
        HasVerifiedLiveState = true,
        CharacterName = "Borin",
        Server = "Omniverse BBS",
        World = "Classic",
        HostKind = BorinGameHostKind.OmniverseBbs,
        HostName = "omniversebbs.com",
        CurrentRoom = "Newhaven Square",
        CurrentRoomKey = "newhaven-square",
        CurrentExits = new[] { "north", "west" },
        HasVerifiedPrompt = true,
        HasVerifiedRoom = true,
        HasVerifiedExits = true,
        CanAutonomousPlay = true,
        AutonomousPlayBlockReason = string.Empty,
        RoomFreshness = "captured within 1 second",
        ExitFreshness = "captured within 1 second",
        PromptFreshness = "captured within 1 second",
        HealthPercent = 78,
        ManaPercent = 65,
        HealthCurrent = 78,
        HealthMaximum = 100,
        ManaCurrent = 65,
        ManaMaximum = 100,
        InCombat = false,
        IsResting = false,
        IsMoving = false,
        CharacterLevel = 10,
        ExperienceCurrent = 24500,
        ExperienceNeeded = 4100,
        CarriedCurrency = 350,
        InventoryItems = new[] { "iron sword", "healing draught", "brass key" },
        EquippedItems = new[] { "iron sword" },
        InventoryFreshness = "captured within 1 second",
        EquipmentFreshness = "captured within 1 second",
        LastCommand = "LOOK",
        LastCommandOrigin = ClientCommandOrigin.AutonomousAgent,
        LastCommandFreshness = "captured within 1 second",
        CapturedAt = DateTimeOffset.Now,
    };
}
