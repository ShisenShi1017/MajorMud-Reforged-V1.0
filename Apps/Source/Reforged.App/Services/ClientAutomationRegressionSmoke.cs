using System.IO;
using System.Text.Json;
using Reforged.App.Models;

namespace Reforged.App.Services;

public static class ClientAutomationRegressionSmoke
{
    public static async Task<int> RunAsync(IApplicationSettingsService settings)
    {
        var failures = new List<string>();
        void Require(bool condition, string name) { if (!condition) failures.Add(name); }

        var profilePath = Path.Combine(settings.RootPath, "Client", "Profiles", "automation-default.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        var legacy = new ClientAutomationProfile
        {
            SchemaVersion = 0,
            Capabilities = new ClientAutomationCapabilities { EmergencyStop = true, AwayFromKeyboardEnabled = true },
            Options = new ClientAutomationOptions
            {
                Combat = new CombatAutomationOptions { AutoAttack = true },
                Health = new HealthAutomationOptions { AutoHeal = true },
                Spells = new SpellAutomationOptions { MaintainBless = true },
                Party = new PartyAutomationOptions { AutoFollowLeader = true },
                LootAndCash = new LootCashAutomationOptions { GetItems = true },
                Roaming = new RoamingAutomationOptions { AutoSearch = true },
                Utility = new UtilityAutomationOptions(),
                PvP = new PvpAutomationOptions { Enabled = true }
            }
        };
        await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var service = new ClientAutomationService(settings);
        await service.LoadAsync();
        var capabilities = service.Profile.Capabilities;
        Require(service.Profile.SchemaVersion == ClientAutomationService.CurrentProfileSchemaVersion, "legacy-schema-migrated");
        Require(capabilities.CombatEnabled && capabilities.HealthRecoveryEnabled && capabilities.SpellMaintenanceEnabled, "legacy-core-options-migrated");
        Require(capabilities.PartyAutomationEnabled && capabilities.LootAutomationEnabled && capabilities.RoamingAutomationEnabled, "legacy-support-options-migrated");
        Require(capabilities.PvpAutomationEnabled && capabilities.CommunicationsAutomationEnabled, "legacy-pvp-communications-migrated");
        Require(capabilities.EmergencyStop, "migration-preserves-emergency-stop");

        var isolated = new ClientAutomationCapabilities();
        foreach (var area in Enum.GetValues<AutomationArea>())
            Require(!ClientAutomationCapabilityPolicy.Allows(isolated, area), $"{area}-disabled-in-isolation");
        isolated.HealthRecoveryEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.Health), "health-switch-admitted"); isolated.HealthRecoveryEnabled = false;
        isolated.SpellMaintenanceEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.Spells), "spell-switch-admitted"); isolated.SpellMaintenanceEnabled = false;
        isolated.CombatEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.Combat), "combat-switch-admitted"); isolated.CombatEnabled = false;
        isolated.PvpAutomationEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.PvP), "pvp-switch-admitted"); isolated.PvpAutomationEnabled = false;
        isolated.PartyAutomationEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.Party), "party-switch-admitted"); isolated.PartyAutomationEnabled = false;
        isolated.RoamingAutomationEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.Roaming) && ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.Utility), "roaming-switch-admitted"); isolated.RoamingAutomationEnabled = false;
        isolated.LootAutomationEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.LootAndCash), "loot-switch-admitted"); isolated.LootAutomationEnabled = false;
        isolated.TimedEventsEnabled = true; Require(ClientAutomationCapabilityPolicy.Allows(isolated, AutomationArea.Events), "events-switch-admitted");

        service.SetMasterEnabled(true);
        capabilities.TextTriggersEnabled = true;
        capabilities.TextTriggers.Clear();
        capabilities.TextTriggers.Add(new ClientTextTrigger { MatchText = "a unique warning", Command = "look", CooldownSeconds = 30 });
        var issued = new List<ClientAutomationCommandRequest>();
        service.CommandRequested += (_, request) => issued.Add(request);
        var now = DateTimeOffset.UtcNow;
        var context = new ClientAutomationContext(100, 100, false, false, false, "a unique warning", now, "smoke-session");
        service.Evaluate(context);
        service.Evaluate(context);
        Require(issued.Count == 1, "trigger-single-command-and-cooldown");

        var combatParser = new ClassicCombatStreamParser();
        var engaged = combatParser.Observe("*Combat Engaged*");
        Require(engaged.Kind == ClassicCombatEventKind.Engaged && combatParser.InCombat && combatParser.AutocombatActive, "break-state-engaged-autocombat-on");
        var broken = combatParser.Observe("*Combat Off*");
        Require(broken.Kind == ClassicCombatEventKind.Disengaged && !combatParser.InCombat && !combatParser.AutocombatActive, "break-state-local-autocombat-off");
        var incomingAfterBreak = combatParser.Observe("An opponent hits you for 7 damage.");
        Require(incomingAfterBreak.Kind == ClassicCombatEventKind.IncomingHit && combatParser.InCombat && !combatParser.AutocombatActive, "break-state-incoming-danger-with-autocombat-off");

        var deduplicator = new ClientAutomationCommandDeduplicator();
        Require(deduplicator.ShouldSend(" Attack   Rat ", ClientCommandOrigin.ClientAutomation, now), "first-automatic-command-admitted");
        Require(!deduplicator.ShouldSend("attack rat", ClientCommandOrigin.RouteAutomation, now.AddMilliseconds(100)), "cross-family-duplicate-blocked");
        Require(deduplicator.ShouldSend("attack rat", ClientCommandOrigin.ClientAutomation, now.AddSeconds(1)), "later-repeat-admitted");
        Require(deduplicator.ShouldSend("attack rat", ClientCommandOrigin.Human, now.AddMilliseconds(110)), "human-command-never-deduplicated");

        capabilities.TextTriggersEnabled = false;
        service.Evaluate(context with { Timestamp = now.AddMinutes(1) });
        Require(issued.Count == 1, "disabled-trigger-sends-nothing");
        capabilities.TextTriggersEnabled = true;
        capabilities.EmergencyStop = true;
        service.Evaluate(context with { Timestamp = now.AddMinutes(2) });
        Require(issued.Count == 1, "emergency-stop-blocks-trigger");

        capabilities.EmergencyStop = false;
        capabilities.AliasesEnabled = true;
        capabilities.Aliases.Add(new ClientCommandAlias { Name = "zz", Command = "look" });
        Require(service.TryResolveHumanInput("zz north", out var aliasCommands) && aliasCommands.SequenceEqual(["look north"]), "alias-expansion");
        capabilities.Aliases.Add(new ClientCommandAlias { Name = "kt", Command = "kill {1};look {0}" });
        Require(service.TryResolveHumanInput("kt goblin king", out var parameterAlias) &&
                parameterAlias.SequenceEqual(["kill goblin", "look goblin king"]), "alias-positional-multistep-expansion");
        capabilities.Aliases.Add(new ClientCommandAlias { Name = "offalias", Command = "score", Enabled = false });
        Require(!service.TryResolveHumanInput("offalias", out _), "disabled-individual-alias-blocked");
        Require(ClientAutomationExpansionEngine.AliasNameConflictReason("gos") is not null, "alias-chat-command-conflict-blocked");
        capabilities.AliasesEnabled = false;
        Require(!service.TryResolveHumanInput("zz north", out _), "disabled-alias-blocked");
        capabilities.MacrosEnabled = true;
        capabilities.Macros.Add(new ClientCommandMacro { Name = "safe", Commands = ["score", "inventory"] });
        Require(service.TryResolveHumanInput("/macro safe", out var macroCommands) && macroCommands.Count == 2, "macro-expansion");
        capabilities.Macros.Add(new ClientCommandMacro { Name = "hunt", Commands = ["attack {1}", "look {0}"] });
        Require(service.TryResolveHumanInput("/macro hunt giant rat", out var parameterMacro) &&
                parameterMacro.SequenceEqual(["attack giant", "look giant rat"]), "macro-positional-expansion");

        capabilities.TextTriggers.Clear();
        capabilities.TextTriggersEnabled = true;
        var captureTrigger = new ClientTextTrigger
        {
            Name = "Capture attacker",
            MatchMode = ClientTextTriggerMatchMode.Wildcard,
            MatchText = "A {target} attacks you*",
            Command = "attack {target};look {target}",
            CooldownSeconds = 0,
            Priority = 1
        };
        capabilities.TextTriggers.Add(captureTrigger);

        const string captureLine = "A giant rat attacks you!";
        var wildcardMatched = ClientAutomationExpansionEngine.TryMatchTrigger(
            captureTrigger,
            captureLine,
            out var wildcardCaptures,
            out var wildcardError);
        Require(
            wildcardMatched &&
            wildcardError is null &&
            wildcardCaptures.TryGetValue("target", out var wildcardTarget) &&
            wildcardTarget == "giant rat",
            "wildcard-trigger-capture");

        Require(
            ClientAutomationExpansionEngine.TryExpandTriggerResponse(
                captureTrigger.Command,
                wildcardCaptures,
                out var wildcardCommands) &&
            wildcardCommands.SequenceEqual(["attack giant rat", "look giant rat"]),
            "wildcard-trigger-batch-expansion");

        var captureContext = context with
        {
            SessionId = "capture-session",
            LastLine = captureLine,
            Timestamp = now.AddMinutes(5)
        };

        // The legacy migration fixture intentionally enables PvP Alert, which also
        // matches "attacks you" at priority 1. Isolate the text-trigger dispatch
        // assertion so unrelated automation cannot win the deterministic tie-break.
        var previousPvpCapability = capabilities.PvpAutomationEnabled;
        var previousSpellCapability = capabilities.SpellMaintenanceEnabled;
        capabilities.PvpAutomationEnabled = false;
        capabilities.SpellMaintenanceEnabled = false;
        try
        {
            var captured = service.Evaluate(captureContext);
            Require(
                captured.Count == 1 &&
                string.Equals(captured[0].RuleId, captureTrigger.Id, StringComparison.OrdinalIgnoreCase) &&
                captured[0].Commands.SequenceEqual(["attack giant rat", "look giant rat"]),
                "wildcard-trigger-capture-and-batch");
        }
        finally
        {
            capabilities.PvpAutomationEnabled = previousPvpCapability;
            capabilities.SpellMaintenanceEnabled = previousSpellCapability;
        }
        capabilities.TextTriggers.Clear();
        var badRegexTrigger = new ClientTextTrigger
        {
            Name = "Bad Regex",
            MatchMode = ClientTextTriggerMatchMode.Regex,
            MatchText = "([",
            Command = "look"
        };
        capabilities.TextTriggers.Add(badRegexTrigger);

        var badRegexMatched = ClientAutomationExpansionEngine.TryMatchTrigger(
            badRegexTrigger,
            captureContext.LastLine,
            out _,
            out var badRegexError);
        Require(!badRegexMatched && !string.IsNullOrWhiteSpace(badRegexError), "invalid-regex-pattern-rejected");

        var badRegexEvaluation = service.Evaluate(
            captureContext with { SessionId = "bad-regex", Timestamp = now.AddMinutes(6) });
        Require(
            badRegexEvaluation.All(request => !string.Equals(
                request.RuleId,
                badRegexTrigger.Id,
                StringComparison.OrdinalIgnoreCase)),
            "invalid-regex-trigger-fails-closed");

        capabilities.TimedEventsEnabled = true;
        service.Profile.Options.Events.ScheduledEvents.Clear();
        service.Profile.Options.Events.ScheduledEvents.Add(new ClientScheduledEvent { Name = "smoke", Command = "score;inventory", Enabled = true, IntervalSeconds = 60 });
        var timedContext = context with { SessionId = "timed-session", LastLine = string.Empty, Timestamp = now };
        Require(service.EvaluateTimedEvents(timedContext, true).Count == 0, "timed-event-primes-without-send");
        var timedRequest = service.EvaluateTimedEvents(timedContext with { Timestamp = now.AddMinutes(2) }, true);
        Require(timedRequest.Count == 1 && timedRequest[0].Commands.Count == 2, "timed-event-sends-multistep-once");
        capabilities.EmergencyStop = true;
        Require(service.EvaluateTimedEvents(timedContext with { Timestamp = now.AddMinutes(4) }, true).Count == 0, "emergency-stop-blocks-timed-event");

        await service.SaveAsync();
        var reloaded = new ClientAutomationService(settings);
        await reloaded.LoadAsync();
        Require(reloaded.Profile.SchemaVersion == ClientAutomationService.CurrentProfileSchemaVersion, "schema-persists-after-restart");
        Require(reloaded.Profile.Capabilities.EmergencyStop && reloaded.Profile.Capabilities.TimedEventsEnabled, "switches-persist-after-restart");
        var reportDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Reports", "Client");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "client-automation-smoke.txt");

        if (failures.Count > 0)
        {
            var lines = new List<string>
            {
                $"Client automation smoke: FAIL ({failures.Count})",
                $"UTC: {DateTimeOffset.UtcNow:O}",
                $"SchemaVersion: {reloaded.Profile.SchemaVersion}",
                $"EmergencyStop: {reloaded.Profile.Capabilities.EmergencyStop}",
                $"TimedEventsEnabled: {reloaded.Profile.Capabilities.TimedEventsEnabled}",
                "",
                "Failures:"
            };

            lines.AddRange(failures.Select(failure => $"FAIL: {failure}"));
            await File.WriteAllLinesAsync(reportPath, lines);
            return 41;
        }

        await File.WriteAllLinesAsync(
            reportPath,
            [
                "Client automation smoke: PASS",
                $"UTC: {DateTimeOffset.UtcNow:O}"
            ]);
        return 0;
    }
}
