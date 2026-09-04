using RealmGate.App.Models;

namespace RealmGate.App.Services;

public static class ClientBatch3RegressionSmoke
{
    public static async Task<int> RunAsync(IApplicationSettingsService settings)
    {
        var failures = new List<string>();
        void Require(bool condition, string name)
        {
            if (!condition) failures.Add(name);
        }

        var automation = new ClientAutomationService(settings);
        await automation.LoadAsync();
        automation.SetMasterEnabled(true);
        var capabilities = automation.Profile.Capabilities;
        capabilities.HealthRecoveryEnabled = true;
        capabilities.SpellMaintenanceEnabled = true;
        capabilities.CombatEnabled = true;
        capabilities.PvpAutomationEnabled = true;
        capabilities.PartyAutomationEnabled = true;
        capabilities.LootAutomationEnabled = true;
        capabilities.RoamingAutomationEnabled = true;
        capabilities.NavigationEnabled = true;
        capabilities.CommunicationsAutomationEnabled = true;
        capabilities.TimedEventsEnabled = true;
        foreach (var area in Enum.GetValues<AutomationArea>())
            Require(ClientAutomationCapabilityPolicy.Allows(capabilities, area), $"combined-capability-{area}");

        automation.Profile.Options.Events.ScheduledEvents.Clear();
        automation.Profile.Options.Events.ScheduledEvents.Add(new ClientScheduledEvent
        {
            Name = "reliability",
            Command = "score",
            Enabled = true,
            IntervalMinutes = 1,
            OnlyOutOfCombat = true
        });
        var now = DateTimeOffset.UtcNow;
        var clock = new ClientAutomationContext(100, 100, false, false, false, string.Empty, now, "batch3-clock");
        Require(automation.EvaluateTimedEvents(clock, false).Count == 0, "events-never-run-disconnected");
        Require(automation.EvaluateTimedEvents(clock, true).Count == 0, "events-prime-after-connect");
        Require(automation.EvaluateTimedEvents(clock with { InCombat = true, Timestamp = now.AddMinutes(2) }, true).Count == 0, "events-pause-in-combat");
        Require(automation.EvaluateTimedEvents(clock with { Timestamp = now.AddMinutes(3) }, true).Count == 1, "events-resume-out-of-combat");

        var coordination = new ClientCoordinationService(settings);
        coordination.Profile.Enabled = true;
        coordination.Profile.MinimumDispatchDelayMs = 0;
        coordination.Profile.Sessions.Clear();
        coordination.Profile.Sessions.Add(new CoordinatedSession { SessionId = "leader", CharacterName = "Leader", Role = CoordinationRole.Leader });
        coordination.Profile.Sessions.Add(new CoordinatedSession { SessionId = "tank", CharacterName = "Tank", Role = CoordinationRole.Tank });
        coordination.Profile.Sessions.Add(new CoordinatedSession { SessionId = "healer", CharacterName = "Healer", Role = CoordinationRole.Healer });
        var followers = coordination.DispatchLocal("followers", "follow Leader");
        Require(followers.Count == 2 && followers.All(item => item.SessionId is "tank" or "healer"), "multi-character-target-isolation");
        coordination.MarkCompleted("tank", "ok");
        Require(coordination.Profile.Sessions.Single(item => item.SessionId == "tank").PendingCommands == 0, "per-character-acknowledgement");
        Require(coordination.Profile.Sessions.Single(item => item.SessionId == "healer").PendingCommands == 1, "other-character-queue-preserved");
        coordination.ResyncAll();
        Require(coordination.Profile.Sessions.All(item => item.PendingCommands == 0 && !item.NeedsResync), "party-reconnect-resync");

        var protocol = new ReforgedClientProtocolFilter();
        var privatePayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"hp\":10}"));
        var privateFrame = $"@@REFORGED-PROTOCOL|STATS|{privatePayload}\r\n";
        var split = privateFrame.Length / 2;
        var first = protocol.Process("ordinary combat text\r\n" + privateFrame[..split]);
        var second = protocol.Process(privateFrame[split..] + "more game text\r\n");
        Require(first.VisibleText.Contains("ordinary combat text") && !first.VisibleText.Contains("REFORGED-PROTOCOL"), "private-frame-prefix-hidden");
        Require(second.Messages.Count == 1 && !second.VisibleText.Contains("\"hp\"") && second.VisibleText.Contains("more game text"), "chunked-private-frame-hidden");

        var deduplicator = new ClientAutomationCommandDeduplicator();
        Require(deduplicator.ShouldSend("attack rat", ClientCommandOrigin.ClientAutomation, now), "combined-first-action");
        Require(!deduplicator.ShouldSend("attack rat", ClientCommandOrigin.RouteAutomation, now.AddMilliseconds(50)), "combined-race-deduplicated");
        Require(deduplicator.ShouldSend("attack rat", ClientCommandOrigin.Human, now.AddMilliseconds(60)), "manual-play-unhindered");

        var paths = new MegaMudPathService(settings);
        await paths.LoadAsync();

        // Clean installs must carry their own Classic destination/route graph. The
        // recovered ImportedDatabases/MegaMudMP archive is optional evidence only.
        Require(paths.AvailablePaths.Count > 0, "megamud-bundled-destination-catalog-loaded");
        Require(paths.AvailablePaths.Any(path => path.Steps.Count == 0 && !string.IsNullOrWhiteSpace(path.NativeEndKey)),
            "megamud-bundled-destinations-present");
        Require(!paths.SourceDescription.Contains("source folder was not found", StringComparison.OrdinalIgnoreCase),
            "megamud-no-imported-database-runtime-dependency");
        Require(paths.CalculateReplacementRoute("1/1", "1/3") is { Steps.Count: > 0 },
            "megamud-bundled-route-graph-executable");

        var recoveredExecutableRoutes = paths.AvailablePaths.Where(path => path.Steps.Count > 0).ToArray();
        Require(recoveredExecutableRoutes.SelectMany(path => path.Steps).All(step =>
            step.Number > 0 && !string.IsNullOrWhiteSpace(step.Command)), "megamud-recovered-path-steps-structurally-valid");

        var reportDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Reports", "Client");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "client-batch3-smoke.txt");

        if (failures.Count == 0)
        {
            await File.WriteAllLinesAsync(
                reportPath,
                [
                    "Batch 3 client regression smoke: PASS",
                    $"UTC: {DateTimeOffset.UtcNow:O}"
                ]);
            return 0;
        }

        var reportLines = new List<string>
        {
            $"Batch 3 client regression smoke: FAIL ({failures.Count})",
            $"UTC: {DateTimeOffset.UtcNow:O}",
            "",
            "Failures:"
        };
        reportLines.AddRange(failures.Select(failure => $"FAIL: {failure}"));
        await File.WriteAllLinesAsync(reportPath, reportLines);
        return 43;
    }
}
