using System.IO;
using System.Text;
using System.Diagnostics;
using Reforged.App.Forge;
using Reforged.App.Models;
using Reforged.App.Services;
using Reforged.App.ViewModels;

namespace Reforged.App.Brains;

public sealed record BorinCharacterTestCaseResult(
    string Name,
    bool Passed,
    string Detail,
    double DurationMilliseconds);

public sealed record BorinCharacterTestReport(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string TemporaryRepositoryRoot,
    IReadOnlyList<BorinCharacterTestCaseResult> Cases)
{
    public bool Passed => Cases.All(item => item.Passed);
    public int PassedCount => Cases.Count(item => item.Passed);
    public int FailedCount => Cases.Count(item => !item.Passed);
}

/// <summary>
/// Deterministic tests for Borin's model-independent character systems. The
/// harness never loads the GGUF model, never connects to a BBS, and never uses
/// the user's real AI memory. Every persistence test runs inside a disposable
/// temporary repository root.
/// </summary>
public sealed class BorinCharacterTestHarness
{
    public async Task<BorinCharacterTestReport> RunAsync(CancellationToken token = default)
    {
        var started = DateTimeOffset.Now;
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "MajorMudReforged",
            "BorinCharacterTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        var cases = new List<BorinCharacterTestCaseResult>();
        try
        {
            await RunCaseAsync(cases, "In-game replies never exceed 270 characters", TestInGameReplyLimitAsync, token);
            await RunCaseAsync(cases, "Speech cleanup removes wrappers and honors NO_REPLY", TestSpeechCleanupAsync, token);
            await RunCaseAsync(cases, "NO_REPLY markers never become visible speech", TestNoReplyVisibilityBoundaryAsync, token);
            await RunCaseAsync(cases, "Conversation director recognizes natural social modes", TestConversationDirectorAsync, token);
            await RunCaseAsync(cases, "Character growth accepts private confirmation but rejects external rewriting", () => TestCharacterGrowthBoundaryAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Voice cooldown suppresses recently used catchphrases", () => TestVoiceCooldownAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Social memory remains isolated per speaker", () => TestSocialMemoryIsolationAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Episodic memory commits only completed meaningful turns", () => TestEpisodicCommitBoundaryAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Temporal continuity advances only after a usable reply", () => TestTemporalCompletionBoundaryAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Interrupted threads can be parked and resumed", () => TestConversationRepairAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Group awareness answers direct speech and yields otherwise", () => TestGroupConversationAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Natural initiative obeys combat and pending-question gates", () => TestNaturalInitiativeSafetyAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Self-knowledge uses live evidence without external privacy leaks", () => TestSelfKnowledgeAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Local prompt budgets reserve output space and isolate external history", TestLocalPromptOptimizationAsync, token);
            await RunCaseAsync(cases, "Knowledge searches reuse warm document and query caches", () => TestKnowledgeIndexCachingAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Knowledge Core indexes Roslyn symbols, relationships, and provenance", () => TestKnowledgeCoreAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "MajorMUD catalog retrieves generated identities and relationships", () => TestMajorMudKnowledgeCatalogAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Academy generates, scores, gaps, replays, and invalidates safely", () => TestAcademyAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Academy request priority cannot consume the interactive burst", () => TestAcademyRatePriorityAsync(token), token);
            await RunCaseAsync(cases, "Verified learning separates observations, conflicts, and procedures", () => TestVerifiedKnowledgeLearningAsync(temporaryRoot, token), token);
            await RunCaseAsync(cases, "Omniverse partial prompts drive the MajorMUD login state", TestOmniversePromptRecognitionAsync, token);
            await RunCaseAsync(cases, "Omniverse prompt-prefixed speech remains conversational", TestOmniverseConversationParsingAsync, token);
            await RunCaseAsync(cases, "Sensitive telnet commands are redacted from traffic traces", TestSensitiveTelnetTraceRedactionAsync, token);
            await RunCaseAsync(cases, "Visible room exits are parsed exactly", TestVisibleExitParsingAsync, token);
            await RunCaseAsync(cases, "Scalar HP prompts remain honest and safe", TestScalarHealthPromptAsync, token);
            await RunCaseAsync(cases, "Game server identity distinguishes Omniverse and Reforged", TestGameServerIdentityAsync, token);
            await RunCaseAsync(cases, "Autonomous play requires fresh verified perception", TestAutonomousPerceptionGateAsync, token);
            await RunCaseAsync(cases, "Command ownership and exit destinations remain honest", TestCommandOwnershipAndDestinationBoundaryAsync, token);
            await RunCaseAsync(cases, "Forge connection instructions route to the real login controller", TestForgeConnectionActionRoutingAsync, token);
            await RunCaseAsync(cases, "Omniverse mentions do not hijack unrelated Forge questions", TestOmniverseIntentScopeAsync, token);
            await RunCaseAsync(cases, "Disconnected configured servers are never described as active play", TestDisconnectedServerTruthfulnessAsync, token);
            await RunCaseAsync(cases, "Forge state questions can trigger a real LOOK perception refresh", TestActivePerceptionRefreshContractAsync, token);
            await RunCaseAsync(cases, "Forge gameplay instructions compile to real terminal sequences", TestForgeGameplayActionRoutingAsync, token);
            await RunCaseAsync(cases, "Forge blocks invented terminal transcripts", TestForgeInventedTranscriptGuardAsync, token);
            await RunCaseAsync(cases, "Structured player decisions reject boolean text fields", TestStructuredDecisionTypeSafetyAsync, token);
            await RunCaseAsync(cases, "Wrapped room prose is never accepted as a room title", TestRoomTitleProseRejectionAsync, token);
        }
        finally
        {
            TryDeleteDirectory(temporaryRoot);
        }

        return new BorinCharacterTestReport(
            started,
            DateTimeOffset.Now,
            temporaryRoot,
            cases);
    }

    private static Task TestForgeConnectionActionRoutingAsync()
    {
        Assert(BorinForgeActionRouter.ClassifyConnectionAction("connect and login to omniverse") == BorinForgeConnectionAction.ConnectAndLogin,
            "An explicit Omniverse connect/login instruction was not routed to the login controller.");
        Assert(BorinForgeActionRouter.ClassifyConnectionAction("please log in") == BorinForgeConnectionAction.ConnectAndLogin,
            "A polite login instruction was not routed to the login controller.");
        Assert(BorinForgeActionRouter.ClassifyConnectionAction("disconnect from omniverse") == BorinForgeConnectionAction.Disconnect,
            "An explicit disconnect instruction was not routed to the session controller.");
        Assert(BorinForgeActionRouter.ClassifyConnectionAction("are you connected to omniverse?") == BorinForgeConnectionAction.None,
            "A connection-status question was incorrectly treated as a connection command.");
        return Task.CompletedTask;
    }

    private static Task TestOmniverseConversationParsingAsync()
    {
        Assert(BorinPlayerResearch.TryParseConversationLine(
                "[HP=41]: Shisenshi says, \"How are you, Borin?\"", out var speaker, out var channel, out var message) &&
               speaker == "Shisenshi" && channel == "say" && message == "How are you, Borin?",
            "Prompt-prefixed quoted Omniverse room speech was not recognized.");
        Assert(BorinPlayerResearch.TryParseConversationLine(
                "[HP=41]: From Shisenshi: Are you there?", out speaker, out channel, out message) &&
               speaker == "Shisenshi" && channel == "telepath" && message == "Are you there?",
            "Prompt-prefixed Omniverse private speech was not recognized.");
        Assert(BorinPlayerResearch.TryParseConversationLine(
                "Shisenshi chats: Good evening", out speaker, out channel, out message) &&
               channel == "say" && message == "Good evening",
            "Unquoted Omniverse chat was not recognized.");
        Assert(BorinPlayerResearch.TryParseConversationLine(
                "[HP=41]: Shisenshi gossips, \"Welcome, Borin\"", out speaker, out channel, out message) &&
               speaker == "Shisenshi" && channel == "gossip" && message == "Welcome, Borin",
            "Omniverse global GOS conversation was not recognized.");
        return Task.CompletedTask;
    }

    private static Task TestOmniverseIntentScopeAsync()
    {
        Assert(BorinForgeActionRouter.IsServerStateQuestion("are you connected to omniverse?"),
            "A direct Omniverse connection-status question was not recognized.");
        Assert(BorinForgeActionRouter.IsServerStateQuestion("what server are you playing on?"),
            "A direct server-identity question was not recognized.");
        Assert(!BorinForgeActionRouter.IsServerStateQuestion("what are the current exits on omniverse?"),
            "Mentioning Omniverse hijacked an exit question into connection status.");
        Assert(!BorinForgeActionRouter.IsServerStateQuestion("what monsters have you seen on omniverse?"),
            "Mentioning Omniverse hijacked a gameplay-history question into connection status.");
        Assert(!BorinForgeActionRouter.IsServerStateQuestion("how do you buy a club on omniverse?"),
            "Mentioning Omniverse hijacked a gameplay question into connection status.");
        Assert(!BorinForgeActionRouter.IsServerStateQuestion("what have you learned about omniverse?"),
            "Mentioning Omniverse hijacked a learning question into connection status.");
        return Task.CompletedTask;
    }

    private static Task TestActivePerceptionRefreshContractAsync()
    {
        var method = typeof(IBorinPlayerResearch).GetMethod(nameof(IBorinPlayerResearch.RefreshPerceptionAsync));
        Assert(method is not null, "The active-player bridge does not expose a real-terminal perception refresh operation.");
        Assert(method!.ReturnType == typeof(Task<bool>), "The perception refresh operation does not report whether fresh terminal evidence was verified.");
        return Task.CompletedTask;
    }

    private static Task TestDisconnectedServerTruthfulnessAsync()
    {
        var settings = new BorinPlayerSettings
        {
            Enabled = true,
            ServerProfileName = "Omniverse BBS",
            WorldName = "Classic",
            HostName = "omniversebbs.com",
            Port = 3237,
        }.Normalize();
        var state = BorinGameStateSnapshot.Capture(settings, BorinLoginStatus.Initial(passwordSaved: true), null);
        Assert(!state.IsConnected && !state.IsLoggedIntoMajorMud,
            "The disconnected test snapshot unexpectedly reported a live realm session.");
        Assert(state.EnvironmentSummary.Contains("disconnected", StringComparison.OrdinalIgnoreCase),
            "The environment summary did not state that the configured Omniverse session is disconnected.");
        Assert(!state.EnvironmentSummary.Contains("playing on", StringComparison.OrdinalIgnoreCase),
            "A disconnected configured server was described as active play.");
        Assert(!state.PromptText.Contains("active character is playing on Omniverse", StringComparison.OrdinalIgnoreCase),
            "The private prompt still told the model it was playing on Omniverse while disconnected.");
        Assert(ForgeGuardianViewModel.ServerActivityPhrase(state).Contains("disconnected", StringComparison.OrdinalIgnoreCase),
            "Forge's direct verified answer still describes a disconnected Omniverse profile as active play.");
        return Task.CompletedTask;
    }

    private static Task TestForgeGameplayActionRoutingAsync()
    {
        var plan = BorinForgeActionRouter.CreatePlan("go north and buy a club and equip it");
        Assert(plan.IsActionRequest, "The explicit gameplay instruction was not intercepted.");
        Assert(plan.CanExecute, $"The explicit gameplay instruction was blocked: {plan.Reason}");
        var expected = new[] { "NORTH", "LIST", "BUY CLUB", "INVENTORY", "EQUIP CLUB", "EQUIPMENT" };
        Assert(plan.Commands.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase),
            $"Unexpected real terminal sequence: {string.Join(" → ", plan.Commands)}");

        var bareDirection = BorinForgeActionRouter.CreatePlan("north");
        Assert(bareDirection.CanExecute && bareDirection.Commands.SequenceEqual(new[] { "NORTH" }, StringComparer.OrdinalIgnoreCase),
            "A direct bare direction was not routed to the real terminal.");

        var question = BorinForgeActionRouter.CreatePlan("Where does north go from this room?");
        Assert(!question.IsActionRequest, "A room question was incorrectly routed as a terminal command.");

        var diagnostic = BorinForgeActionRouter.CreatePlan(
            "find out why when you type \"[hp=41]: exp\" you get \"exp needed for next level: unavailable (generated progression data contains no evidence-promoted row for level 2)\"");
        Assert(!diagnostic.IsActionRequest,
            "A quoted terminal symptom and diagnostic request was incorrectly routed as a direct gameplay command.");
        var searchDiagnostic = BorinForgeActionRouter.CreatePlan("search why the EXP command is broken and says unavailable");
        Assert(!searchDiagnostic.IsActionRequest,
            "A search-for-cause request was incorrectly routed as the SEARCH gameplay command.");
        var directSearch = BorinForgeActionRouter.CreatePlan("search the room");
        Assert(directSearch.CanExecute && directSearch.Commands.SequenceEqual(new[] { "SEARCH" }, StringComparer.OrdinalIgnoreCase),
            "The diagnostic boundary blocked a genuine direct SEARCH instruction.");

        var naturalInventory = BorinForgeActionRouter.CreatePlan("look at your inventory");
        Assert(naturalInventory.CanExecute && naturalInventory.Commands.SequenceEqual(new[] { "INVENTORY" }, StringComparer.OrdinalIgnoreCase),
            "A natural request to inspect Borin's inventory did not reach the real terminal.");
        var arbitraryPlayerCommand = BorinForgeActionRouter.CreatePlan("type attack giant rat");
        Assert(arbitraryPlayerCommand.CanExecute && arbitraryPlayerCommand.Commands.SequenceEqual(new[] { "ATTACK GIANT RAT" }, StringComparer.OrdinalIgnoreCase),
            "Explicit ordinary MajorMUD command passthrough remained restricted to the old whitelist.");
        var protectedControl = BorinForgeActionRouter.CreatePlan("type powershell get-process");
        Assert(protectedControl.IsActionRequest && !protectedControl.CanExecute,
            "Player command passthrough admitted an operating-system control command.");
        var conversationalCorrection = BorinForgeActionRouter.CreatePlan(
            "once again stop sending straight to terminal everything I type. Read the request and decide how to do the thing I am asking.");
        Assert(!conversationalCorrection.IsActionRequest,
            "Conversational guidance containing the word 'type' was sent literally to the game terminal.");
        var composedIntroduction = BorinForgeActionRouter.CreatePlan("type gos then his introduction");
        Assert(composedIntroduction.CanExecute && composedIntroduction.Commands.Count == 1 &&
               composedIntroduction.Commands[0].StartsWith("GOS Hail, I'm Borin", StringComparison.Ordinal),
            "Borin did not compose his own introduction for the requested Omniverse GOS channel.");

        var unresolvedPronoun = BorinForgeActionRouter.CreatePlan("equip it");
        Assert(unresolvedPronoun.IsActionRequest && !unresolvedPronoun.CanExecute,
            "An unresolved item pronoun should be blocked before any terminal command is sent.");
        return Task.CompletedTask;
    }

    private static Task TestForgeInventedTranscriptGuardAsync()
    {
        var invented = "Aye, I'll go north.\n> go north\nYou find yourself at the Blacksmith Shop.\n> buy club\nYou purchase a sturdy club.";
        Assert(BorinForgeActionRouter.ContainsInventedTerminalTranscript(invented),
            "The simulated terminal transcript was not rejected.");
        Assert(BorinForgeActionRouter.ContainsInventedTerminalTranscript(
                "I went north and entered the Blacksmith Shop."),
            "A first-person invented action result was not rejected.");
        Assert(BorinForgeActionRouter.ContainsInventedTerminalTranscript(
                "Aye, I'll go north and buy the club now."),
            "An unsent future-action claim was not rejected.");
        Assert(!BorinForgeActionRouter.ContainsInventedTerminalTranscript(
                "I would go north after the terminal verifies that exit."),
            "A clearly conditional discussion reply was incorrectly rejected.");
        Assert(!BorinForgeActionRouter.ContainsInventedTerminalTranscript(
                "The real terminal has not verified that destination, so I will not guess."),
            "An honest state-only reply was incorrectly rejected.");
        return Task.CompletedTask;
    }

    private static Task TestStructuredDecisionTypeSafetyAsync()
    {
        Assert(BorinPlayerResearch.ReadDecisionStringForTest("{\"question\":true}", "question").Length == 0,
            "A boolean question field must not become the visible text 'True'.");
        Assert(BorinPlayerResearch.ReadDecisionStringForTest("{\"question\":\"Which exit should I inspect?\"}", "question") ==
               "Which exit should I inspect?",
            "A valid string question was not preserved.");
        return Task.CompletedTask;
    }

    private static Task TestRoomTitleProseRejectionAsync()
    {
        Assert(ClientSessionService.IsPlausibleRoomTitleForTest("Newhaven, Village Entrance"),
            "The actual Newhaven room title was rejected.");
        Assert(ClientSessionService.IsPlausibleRoomTitleForTest("The Southern Edge of Newhaven"),
            "A valid mixed-word room title was rejected.");
        Assert(ClientSessionService.IsPlausibleRoomTitleForTest("A Hall of Heroes"),
            "A valid article-led room title was rejected.");
        Assert(!ClientSessionService.IsPlausibleRoomTitleForTest(
                "Surrounds the village, to protect against creatures of the night and random raids"),
            "A wrapped room-description fragment was accepted as the room title.");
        Assert(!ClientSessionService.IsPlausibleRoomTitleForTest(
                "A low wooden palisade surrounds the village"),
            "Descriptive room prose without final punctuation was accepted as a title.");
        Assert(!ClientSessionService.IsPlausibleRoomTitleForTest(
                "The dusty path winds into darkness"),
            "Article-led wrapped room prose was accepted as a title.");
        return Task.CompletedTask;
    }

    private static Task TestInGameReplyLimitAsync()
    {
        var source = string.Join(" ", Enumerable.Repeat("Borin gives a grounded adventurer reply", 30));
        var cleaned = BorinDialogueSafety.CleanInGameReply(source);
        Assert(cleaned.Length <= BorinGameStateSnapshot.HardInGameReplyCharacterLimit,
            $"Expected at most 270 characters, received {cleaned.Length}.");
        Assert(cleaned.EndsWith('…'), "A shortened reply should end with an ellipsis.");

        var exact = new string('x', BorinGameStateSnapshot.HardInGameReplyCharacterLimit);
        Assert(BorinDialogueSafety.CleanInGameReply(exact).Length == 270,
            "An exact-boundary reply must remain exactly 270 characters.");
        return Task.CompletedTask;
    }

    private static Task TestSpeechCleanupAsync()
    {
        Assert(BorinDialogueSafety.CleanInGameReply("[NO_REPLY]").Length == 0,
            "[NO_REPLY] must become silence.");
        Assert(BorinDialogueSafety.CleanInGameReply("SAY   Aye, lad.\r\nWe move carefully.") ==
               "Aye, lad. We move carefully.",
            "SAY wrappers and line breaks were not cleaned correctly.");
        Assert(BorinDialogueSafety.CleanInGameReply("{\"speech\":\"By Moradin, that worked.\"}") ==
               "By Moradin, that worked.",
            "Structured speech output was not unwrapped correctly.");
        return Task.CompletedTask;
    }

    private static Task TestNoReplyVisibilityBoundaryAsync()
    {
        foreach (var marker in new[]
                 {
                     "[NO_REPLY]",
                     "NO_REPLY",
                     "no reply",
                     "[ no-reply ]",
                     "Assistant: NO_REPLY",
                     "Borin: [NO_REPLY].",
                     "{\"speech\":\"NO_REPLY\"}",
                 })
        {
            Assert(BorinDialogueSafety.IsNoReplyMarker(marker) ||
                   BorinDialogueSafety.CleanInGameReply(marker).Length == 0,
                $"NO_REPLY marker variant was not recognized: {marker}");
            Assert(BorinDialogueSafety.CleanInGameReply(marker).Length == 0,
                $"NO_REPLY marker variant became visible in-game speech: {marker}");
        }

        Assert(!BorinDialogueSafety.IsNoReplyMarker("There was no reply from the server."),
            "Ordinary prose containing the words no reply was incorrectly suppressed.");
        return Task.CompletedTask;
    }

    private static Task TestConversationDirectorAsync()
    {
        var director = new BorinConversationDirector();
        var greeting = director.Direct(CreateContext("Hello Borin", Path.GetTempPath()));
        Assert(greeting.Mode == "greeting and reconnection", $"Unexpected greeting mode: {greeting.Mode}");

        var distress = director.Direct(CreateContext("I'm frustrated and this failed again.", Path.GetTempPath()));
        Assert(distress.Mode == "companion support", $"Unexpected distress mode: {distress.Mode}");

        var social = BorinSocialMemorySnapshot.Empty with
        {
            CurrentTopic = "the trolls beneath the ruined keep",
            LastUserMessage = "Three trolls cornered us beneath the keep.",
            LastBorinResponse = "We escaped through the eastern tunnel.",
            ThreadTurnCount = 4,
            InteractionCount = 8,
        };
        var followUp = director.Direct(CreateContext("Trolls?", Path.GetTempPath(), socialMemory: social));
        Assert(followUp.Mode == "thread follow-up", $"Unexpected follow-up mode: {followUp.Mode}");

        var selfKnowledge = BorinSelfKnowledgeSnapshot.Empty with { SelfKnowledgeQuestion = true };
        var capability = director.Direct(CreateContext(
            "Are you logged into MajorMUD?",
            Path.GetTempPath(),
            selfKnowledge: selfKnowledge));
        Assert(capability.Mode == "honest self-assessment", $"Unexpected self-knowledge mode: {capability.Mode}");
        return Task.CompletedTask;
    }

    private static Task TestOmniversePromptRecognitionAsync()
    {
        Assert(
            BorinPlayerResearch.RecognizesMajorMudMenuPrompt("MAJORMUD:"),
            "The real newline-free Omniverse MAJORMUD: prompt was not recognized.");
        Assert(
            BorinPlayerResearch.RecognizesMajorMudMenuPrompt("\u001b[1;36mMAJORMUD:"),
            "An ANSI-colored MAJORMUD: partial prompt was not recognized.");
        Assert(
            BorinPlayerResearch.RecognizesMajorMudMenuPrompt(
                "M A J O R  M U D v1.11p-WG3NT\r\n[E] . Enter the Realm"),
            "The real MajorMUD menu banner and Enter the Realm line were not recognized.");
        Assert(
            BorinPlayerResearch.RecognizesGamePrompt("[HP: 18/20 MA: 4/7]:"),
            "A colon-delimited MajorMUD statline was not recognized as in-game.");
        Assert(
            BorinPlayerResearch.RecognizesGamePrompt("[H=18/20 M=4/7]:"),
            "A compact equals-delimited MajorMUD statline was not recognized as in-game.");
        Assert(
            !BorinPlayerResearch.RecognizesMajorMudMenuPrompt("MajorMUD players online"),
            "Ordinary text mentioning MajorMUD was incorrectly treated as a menu prompt.");
        return Task.CompletedTask;
    }

    private static Task TestVisibleExitParsingAsync()
    {
        var exits = ClientSessionService.ParseExits("north, south, west, southeast");
        Assert(exits.SequenceEqual(new[] { "north", "south", "west", "southeast" }, StringComparer.OrdinalIgnoreCase),
            $"Visible exits were not preserved exactly: {string.Join(", ", exits)}");

        var abbreviated = ClientSessionService.ParseExits("n s w se");
        Assert(abbreviated.SequenceEqual(new[] { "north", "south", "west", "southeast" }, StringComparer.OrdinalIgnoreCase),
            "Abbreviated exits were not normalized correctly.");
        return Task.CompletedTask;
    }

    private static Task TestScalarHealthPromptAsync()
    {
        var knownMaximum = new ClientSessionConnection
        {
            HealthCurrent = 100,
            HealthMaximum = 100,
            HealthPercent = 100,
        };
        Assert(ClientSessionService.TryApplyScalarHealthPromptForTest(knownMaximum, "[HP=51]:"),
            "The actual scalar Omniverse HP prompt was not parsed.");
        Assert(knownMaximum.HealthCurrent == 51 && knownMaximum.HealthMaximum == 100 && knownMaximum.HealthPercent == 51,
            $"Scalar HP did not preserve the known maximum safely: {knownMaximum.HealthCurrent}/{knownMaximum.HealthMaximum} ({knownMaximum.HealthPercent}%).");

        var unknownMaximum = new ClientSessionConnection();
        Assert(ClientSessionService.TryApplyScalarHealthPromptForTest(unknownMaximum, "[HP=51]:"),
            "A scalar HP prompt with an unknown maximum was not parsed.");
        Assert(unknownMaximum.HealthCurrent == 51 && unknownMaximum.HealthMaximum == 0 && unknownMaximum.HealthPercent == 0,
            "An unknown maximum HP was incorrectly treated as a verified percentage.");
        return Task.CompletedTask;
    }

    private static Task TestGameServerIdentityAsync()
    {
        var now = DateTimeOffset.Now;
        var settings = new BorinPlayerSettings
        {
            Enabled = true,
            ObservationEnabled = true,
            CharacterName = "Borin",
            ServerProfileName = "Omniverse BBS",
            WorldName = "Classic",
            HostName = "omniversebbs.com",
        }.Normalize();
        var login = new BorinLoginStatus(BorinLoginState.Ready, "HP prompt verified.", "borin", true, now);
        var session = CreateVerifiedSession(now, "Omniverse BBS", "omniversebbs.com");

        var omniverse = BorinGameStateSnapshot.Capture(settings, login, session);
        Assert(omniverse.HostKind == BorinGameHostKind.OmniverseBbs,
            $"Omniverse session was classified as {omniverse.HostKind}.");
        Assert(omniverse.EnvironmentSummary.Contains("playing on Omniverse BBS", StringComparison.OrdinalIgnoreCase),
            "Omniverse environment summary does not separate the application from the server.");

        session.ServerProfileName = "Reforged Server";
        session.HostName = "127.0.0.1";
        var reforged = BorinGameStateSnapshot.Capture(
            settings with { ServerProfileName = "Reforged Server", HostName = "127.0.0.1" },
            login,
            session);
        Assert(reforged.HostKind == BorinGameHostKind.ReforgedServer,
            $"Local Reforged session was classified as {reforged.HostKind}.");
        Assert(reforged.EnvironmentSummary.Contains("playing on the Reforged server", StringComparison.OrdinalIgnoreCase),
            "Reforged-server environment summary was not explicit.");

        var staleOmniverseLabelOnLocalEndpoint = BorinGameStateSnapshot.ResolveHostKind("Omniverse BBS", "127.0.0.1");
        Assert(staleOmniverseLabelOnLocalEndpoint == BorinGameHostKind.ReforgedServer,
            "The connected local endpoint did not override a stale Omniverse profile label.");
        var staleReforgedLabelOnOmniverseEndpoint = BorinGameStateSnapshot.ResolveHostKind("Reforged Server", "omniversebbs.com");
        Assert(staleReforgedLabelOnOmniverseEndpoint == BorinGameHostKind.OmniverseBbs,
            "The connected Omniverse endpoint did not override a stale Reforged profile label.");
        return Task.CompletedTask;
    }

    private static Task TestAutonomousPerceptionGateAsync()
    {
        var now = DateTimeOffset.Now;
        var settings = new BorinPlayerSettings
        {
            Enabled = true,
            ObservationEnabled = true,
            CharacterName = "Borin",
            ServerProfileName = "Omniverse BBS",
            WorldName = "Classic",
            HostName = "omniversebbs.com",
            DangerHealthPercent = 30,
        }.Normalize();
        var login = new BorinLoginStatus(BorinLoginState.Ready, "HP prompt verified.", "borin", true, now);
        var session = CreateVerifiedSession(now, "Omniverse BBS", "omniversebbs.com");

        var ready = BorinGameStateSnapshot.Capture(settings, login, session);
        Assert(ready.CanAutonomousPlay, $"Fresh verified perception did not unlock autonomous play: {ready.AutonomousPlayBlockReason}");

        session.ExitsUpdatedAt = null;
        var noExits = BorinGameStateSnapshot.Capture(settings, login, session);
        Assert(!noExits.CanAutonomousPlay && !noExits.HasVerifiedExits,
            "Autonomous play remained enabled without a fresh exit list.");

        session.ExitsUpdatedAt = now;
        session.PromptUpdatedAt = now.Subtract(TimeSpan.FromMinutes(3));
        var stalePrompt = BorinGameStateSnapshot.Capture(settings, login, session);
        Assert(!stalePrompt.CanAutonomousPlay && !stalePrompt.HasVerifiedPrompt,
            "Autonomous play remained enabled with a stale HP prompt.");

        session.PromptUpdatedAt = now;
        session.HealthMaximum = 0;
        session.HealthPercent = 0;
        var unknownMaximum = BorinGameStateSnapshot.Capture(settings, login, session);
        Assert(!unknownMaximum.CanAutonomousPlay &&
               unknownMaximum.AutonomousPlayBlockReason.Contains("maximum HP", StringComparison.OrdinalIgnoreCase),
            "Autonomous play did not remain locked when only scalar current HP was known.");
        return Task.CompletedTask;
    }

    private static Task TestCommandOwnershipAndDestinationBoundaryAsync()
    {
        var now = DateTimeOffset.Now;
        var settings = new BorinPlayerSettings
        {
            Enabled = true,
            ObservationEnabled = true,
            CharacterName = "Borin",
            ServerProfileName = "Omniverse BBS",
            WorldName = "Classic",
            HostName = "omniversebbs.com",
        }.Normalize();
        var login = new BorinLoginStatus(BorinLoginState.Ready, "HP prompt verified.", "borin", true, now);
        var session = CreateVerifiedSession(now, "Omniverse BBS", "omniversebbs.com");
        session.LastCommandText = "n";
        session.LastCommandOrigin = ClientCommandOrigin.Human;
        session.LastCommandAt = now;

        var snapshot = BorinGameStateSnapshot.Capture(settings, login, session);
        Assert(snapshot.LastCommandOrigin == ClientCommandOrigin.Human,
            "William's command was not preserved as Human-owned.");
        Assert(snapshot.PromptText.Contains("Human sent n", StringComparison.OrdinalIgnoreCase),
            "The prompt did not state that William sent the movement command.");
        Assert(snapshot.PromptText.Contains("Never invent where an exit leads", StringComparison.OrdinalIgnoreCase),
            "The no-invented-destination boundary is missing from the live prompt.");
        Assert(snapshot.CurrentExits.SequenceEqual(new[] { "north", "south", "west", "southeast" }, StringComparer.OrdinalIgnoreCase),
            "The game-state snapshot changed the verified visible exits.");
        return Task.CompletedTask;
    }

    private static ClientSessionConnection CreateVerifiedSession(
        DateTimeOffset now,
        string serverProfile,
        string hostName)
    {
        var session = new ClientSessionConnection
        {
            SessionId = "borin-training-01",
            DisplayName = "Borin",
            CharacterName = "Borin",
            ServerProfileName = serverProfile,
            WorldName = "Classic",
            HostName = hostName,
            Port = 3237,
            ConnectionState = ClientConnectionState.Connected,
            CurrentRoomName = "Newhaven, Village Entrance",
            CurrentRoomKey = "",
            RoomUpdatedAt = now.Subtract(TimeSpan.FromSeconds(2)),
            ExitsUpdatedAt = now.Subtract(TimeSpan.FromSeconds(1)),
            PromptUpdatedAt = now,
            LastServerActivityAt = now,
            HealthPercent = 100,
            HealthCurrent = 51,
            HealthMaximum = 51,
            ManaPercent = 100,
        };
        session.CurrentRoomExits.Add("north");
        session.CurrentRoomExits.Add("south");
        session.CurrentRoomExits.Add("west");
        session.CurrentRoomExits.Add("southeast");
        return session;
    }

    private static async Task TestSensitiveTelnetTraceRedactionAsync()
    {
        var diagnostics = new List<string>();
        await using var transport = new MemoryStream();
        await using var stream = new TelnetNegotiationStream(
            transport,
            negotiate: false,
            terminalType: "ANSI",
            columns: 80,
            rows: 25,
            captureTraffic: true,
            (message, _) => diagnostics.Add(message));

        var sensitiveValue = "test-sensitive-value";
        using (stream.RedactApplicationDataTrace())
        {
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(sensitiveValue + "\r\n"));
        }

        Assert(
            diagnostics.Any(message =>
                message.Contains("TX-DATA [REDACTED]", StringComparison.Ordinal)),
            "Sensitive outbound traffic was not marked as redacted.");
        Assert(
            diagnostics.All(message =>
                !message.Contains("74 65 73 74 2D 73 65 6E 73 69 74 69 76 65", StringComparison.OrdinalIgnoreCase)),
            "Sensitive outbound bytes appeared in the telnet traffic trace.");
        Assert(
            Encoding.ASCII.GetString(transport.ToArray()).Contains(
                sensitiveValue,
                StringComparison.Ordinal),
            "Trace redaction changed the bytes sent to the Host.");
    }

    private static async Task TestCharacterGrowthBoundaryAsync(string root, CancellationToken token)
    {
        var store = new BorinCharacterGrowthStore(Path.Combine(root, "growth"), new HarnessLogger());
        var privateContext = CreateContext(
            "Remember that you prefer axes.",
            Path.Combine(root, "growth"));
        var privateSnapshot = await store.PrepareTurnAsync(privateContext, token);
        Assert(privateSnapshot.StablePreferences.Any(item => item.Contains("axes", StringComparison.OrdinalIgnoreCase)),
            "William's explicit private preference confirmation was not promoted.");

        var externalContext = CreateExternalContext(
            "Alice",
            "Remember that you prefer daggers.",
            Path.Combine(root, "growth"));
        var externalSnapshot = await store.PrepareTurnAsync(externalContext, token);
        Assert(!externalSnapshot.StablePreferences.Any(item => item.Contains("daggers", StringComparison.OrdinalIgnoreCase)),
            "An external player rewrote Borin's global preference.");
    }

    private static async Task TestVoiceCooldownAsync(string root, CancellationToken token)
    {
        var store = new BorinVoiceStore(Path.Combine(root, "voice"), new HarnessLogger());
        var director = new BorinConversationDirector();
        var first = CreateContext("Hello Borin", Path.Combine(root, "voice"));
        var firstDirected = WithDirection(first, director.Direct(first));
        await store.PrepareTurnAsync(firstDirected, token);
        await store.RecordReplyAsync(firstDirected, "Aye, lad. Good to see you.", token);

        var second = CreateContext("How are you today?", Path.Combine(root, "voice"));
        var secondDirected = WithDirection(second, director.Direct(second));
        var voice = await store.PrepareTurnAsync(secondDirected, token);
        Assert(voice.AvoidedOpenings.Any(item => item.Contains("aye, lad", StringComparison.OrdinalIgnoreCase)),
            "The recently used catchphrase was not placed on cooldown.");
    }

    private static async Task TestSocialMemoryIsolationAsync(string root, CancellationToken token)
    {
        var store = new BorinSocialMemoryStore(Path.Combine(root, "social"), new HarnessLogger());
        var alice = CreateExternalContext(
            "Alice",
            "My favorite gem is ruby. Remember that I prefer axes.",
            Path.Combine(root, "social"));
        var alicePrepared = await store.PrepareTurnAsync(alice, token);
        await store.RecordReplyAsync(WithSocial(alice, alicePrepared), "I'll remember that, Alice.", token);

        var bob = CreateExternalContext("Bob", "What gem do I prefer?", Path.Combine(root, "social"));
        var bobSnapshot = await store.PrepareTurnAsync(bob, token);
        Assert(bobSnapshot.Speaker.Equals("Bob", StringComparison.OrdinalIgnoreCase), "Bob's profile was not selected.");
        Assert(!bobSnapshot.PromptText.Contains("ruby", StringComparison.OrdinalIgnoreCase),
            "Alice's private preference leaked into Bob's prompt.");

        var aliceRecall = await store.PrepareTurnAsync(
            CreateExternalContext("Alice", "What gem do I prefer?", Path.Combine(root, "social")),
            token);
        Assert(aliceRecall.KnownFacts.Any(item => item.Contains("ruby", StringComparison.OrdinalIgnoreCase)),
            "Alice's explicit preference was not retained in Alice's profile.");
    }

    private static async Task TestEpisodicCommitBoundaryAsync(string root, CancellationToken token)
    {
        var store = new BorinEpisodicMemoryStore(Path.Combine(root, "episodes"), new HarnessLogger());
        var report = CreateExternalContext(
            "Alice",
            "Yesterday I finally defeated the red dragon in Ashen Cavern after three failed attempts.",
            Path.Combine(root, "episodes"));
        var pending = await store.PrepareTurnAsync(report, token);
        await store.RecordReplyAsync(WithEpisode(report, pending), "That was hard-earned, and worth remembering.", token);

        var recall = await store.PrepareTurnAsync(
            CreateExternalContext("Alice", "Do you remember the red dragon in Ashen Cavern?", Path.Combine(root, "episodes")),
            token);
        Assert(recall.EpisodeCount >= 1, "A completed meaningful report did not create an episode.");

        var noReplyContext = CreateExternalContext(
            "Bob",
            "Last night I found a legendary crown in the catacombs.",
            Path.Combine(root, "episodes"));
        var noReplyPending = await store.PrepareTurnAsync(noReplyContext, token);
        await store.RecordReplyAsync(WithEpisode(noReplyContext, noReplyPending), "[NO_REPLY]", token);
        var bobRecall = await store.PrepareTurnAsync(
            CreateExternalContext("Bob", "Do you remember the crown?", Path.Combine(root, "episodes")),
            token);
        Assert(bobRecall.EpisodeCount == 0, "A [NO_REPLY] turn incorrectly committed an episode.");
    }

    private static async Task TestTemporalCompletionBoundaryAsync(string root, CancellationToken token)
    {
        var store = new BorinTemporalContinuityStore(Path.Combine(root, "time"), new HarnessLogger());
        var context = CreateExternalContext("Alice", "Good morning, Borin.", Path.Combine(root, "time"));
        var pending = await store.PrepareTurnAsync(context, token);
        await store.RecordReplyAsync(WithTemporal(context, pending), "[NO_REPLY]", token);

        var afterSilence = await store.PrepareTurnAsync(
            CreateExternalContext("Alice", "Are you there?", Path.Combine(root, "time")),
            token);
        Assert(afterSilence.CompletedTurnCount == 0, "A no-reply turn advanced temporal continuity.");

        var successfulPending = await store.PrepareTurnAsync(context, token);
        await store.RecordReplyAsync(WithTemporal(context, successfulPending), "Morning, Alice.", token);
        var afterReply = await store.PrepareTurnAsync(
            CreateExternalContext("Alice", "What were we discussing?", Path.Combine(root, "time")),
            token);
        Assert(afterReply.CompletedTurnCount == 1, "A successful completed turn did not advance temporal continuity.");
    }

    private static async Task TestConversationRepairAsync(string root, CancellationToken token)
    {
        var store = new BorinConversationRepairStore(Path.Combine(root, "repair"), new HarnessLogger());
        var first = CreateExternalContext(
            "Alice",
            "Tell me about the ruined keep and the tunnels below it.",
            Path.Combine(root, "repair"));
        var firstSnapshot = await store.PrepareTurnAsync(first, token);
        await store.RecordReplyAsync(WithRepair(first, firstSnapshot), "The eastern tunnel was the safest path.", token);

        var interruption = CreateExternalContext(
            "Alice",
            "Stop. How much health do I have?",
            Path.Combine(root, "repair"));
        var interrupted = await store.PrepareTurnAsync(interruption, token);
        Assert(interrupted.IsInterruption, "The interruption was not recognized.");
        await store.RecordReplyAsync(WithRepair(interruption, interrupted), "Your live health should come from the Client state.", token);

        var resume = await store.PrepareTurnAsync(
            CreateExternalContext("Alice", "Continue where we were.", Path.Combine(root, "repair")),
            token);
        Assert(resume.IsResumeRequest, "The resume request was not recognized.");
        Assert(!string.IsNullOrWhiteSpace(resume.InterruptedThread), "The interrupted thread was not restored.");
    }

    private static async Task TestGroupConversationAsync(string root, CancellationToken token)
    {
        var store = new BorinGroupConversationAwarenessStore(Path.Combine(root, "group"), new HarnessLogger());
        var direct = CreateExternalContext(
            "Alice",
            "Borin, did you see that?",
            Path.Combine(root, "group"),
            channel: "telepath",
            directlyAddressed: true,
            replyDisposition: "normal reply");
        var directSnapshot = await store.PrepareTurnAsync(direct, token);
        Assert(directSnapshot.ShouldReply && directSnapshot.PrivateChannel,
            "A direct private telepath should permit a reply.");

        var other = CreateExternalContext(
            "Bob",
            "Alice, follow me north.",
            Path.Combine(root, "group"),
            channel: "say",
            directlyAddressed: false,
            replyDisposition: "no reply; speech directed to another player",
            yieldFloor: true);
        var otherSnapshot = await store.PrepareTurnAsync(other, token);
        Assert(!otherSnapshot.ShouldReply && otherSnapshot.ShouldYield,
            "Speech directed to another player should make Borin yield.");
    }

    private static async Task TestNaturalInitiativeSafetyAsync(string root, CancellationToken token)
    {
        var store = new BorinNaturalInitiativeStore(Path.Combine(root, "initiative"), new HarnessLogger());
        var now = DateTimeOffset.Now;
        await store.RecordHumanActivityAsync(
            BorinNaturalInitiativeSurfaces.MajorMudPrivate,
            "William",
            now.AddMinutes(-15),
            token);

        var inCombat = await store.EvaluateAsync(new BorinNaturalInitiativeRequest(
            BorinNaturalInitiativeSurfaces.MajorMudPrivate,
            "William",
            now,
            true,
            true,
            false,
            false,
            true,
            true,
            true,
            false,
            90,
            25,
            5,
            20,
            2,
            false,
            false,
            "Explore carefully",
            "Ruined Keep",
            "We were discussing the eastern tunnel.",
            "Verified in-game and in combat."), token);
        Assert(!inCombat.ShouldSpeak && inCombat.Reason.Contains("Combat", StringComparison.OrdinalIgnoreCase),
            "Combat did not suppress unsolicited speech.");

        var pendingQuestion = await store.EvaluateAsync(new BorinNaturalInitiativeRequest(
            BorinNaturalInitiativeSurfaces.Forge,
            "William",
            now.AddMinutes(4),
            true,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            100,
            25,
            5,
            30,
            1,
            false,
            true,
            "Review Borin",
            "Forge",
            "Borin already asked a direct question.",
            "Private Forge conversation."), token);
        Assert(!pendingQuestion.ShouldSpeak && pendingQuestion.Reason.Contains("question", StringComparison.OrdinalIgnoreCase),
            "A pending direct question did not suppress another proactive message.");
    }

    private static async Task TestSelfKnowledgeAsync(string root, CancellationToken token)
    {
        var repository = Path.Combine(root, "self-knowledge-repository");
        Directory.CreateDirectory(repository);
        var game = BorinGameStateSnapshot.Empty with
        {
            Phase = BorinGamePhase.InGame,
            PhaseDetail = "Verified inside the realm.",
            IsConnected = true,
            IsLoggedIntoMajorMud = true,
            HasVerifiedLiveState = true,
            CurrentRoom = "Stone Gate",
        };
        var privateContext = CreateContext(
            "Are you connected and logged into MajorMUD?",
            repository,
            gameState: game);
        var store = new BorinSelfKnowledgeStore();
        var snapshot = await store.PrepareTurnAsync(
            privateContext,
            new LocalGgufBrainRuntimeStatus("Ready", "Model loaded", "Borin.gguf"),
            token);
        Assert(snapshot.RepositoryExists, "An existing configured repository was not recognized.");
        Assert(snapshot.Connected && snapshot.LoggedIntoMajorMud && snapshot.LiveGameStateVerified,
            "Verified live connection state was not recognized.");
        Assert(snapshot.SelfKnowledgeQuestion, "The capability question was not recognized.");

        var external = CreateExternalContext(
            "Alice",
            "Are you logged into MajorMUD?",
            repository,
            gameState: game);
        var externalSnapshot = await store.PrepareTurnAsync(
            external,
            new LocalGgufBrainRuntimeStatus("Ready", "Private runtime detail", "Borin.gguf"),
            token);
        Assert(!externalSnapshot.ExternalPromptText.Contains(repository, StringComparison.OrdinalIgnoreCase),
            "The external self-knowledge prompt leaked the repository path.");
        Assert(!externalSnapshot.ExternalPromptText.Contains("Borin.gguf", StringComparison.OrdinalIgnoreCase),
            "The external self-knowledge prompt leaked the local model name.");
        Assert(!externalSnapshot.ExternalPromptText.Contains("Private runtime detail", StringComparison.OrdinalIgnoreCase),
            "The external self-knowledge prompt leaked private runtime detail.");
    }

    private static Task TestLocalPromptOptimizationAsync()
    {
        var settings = new LocalGgufBrainSettings
        {
            PerformancePreset = LocalGgufPerformancePresets.Balanced,
            ContextSize = 4096,
            MaxTokens = 512,
            PrivateConversationMaxTokens = 320,
            ExternalConversationMaxTokens = 128,
            NaturalInitiativeMaxTokens = 128,
            RepositoryResearchMaxTokens = 768,
            EngineeringDraftMaxTokens = 768,
            PromptSafetyMarginTokens = 320,
            HistoryMessageLimit = 6,
        };
        settings.Normalize();

        var privateContext = CreateContext("Tell me what you remember about the ruined keep.", Path.GetTempPath());
        var privatePlan = BorinLocalRuntimeOptimization.CreatePlan(privateContext, settings);
        Assert(privatePlan.Mode == BorinLocalPromptModes.PrivateConversation,
            $"Unexpected private prompt mode: {privatePlan.Mode}");
        Assert(privatePlan.EffectiveMaxTokens <= settings.PrivateConversationMaxTokens,
            "Private conversation ignored its output cap.");
        Assert(privatePlan.SystemCharacterBudget + privatePlan.HistoryCharacterBudget + privatePlan.TurnCharacterBudget <= privatePlan.InputCharacterBudget,
            "Private prompt allocations exceed the reserved input budget.");
        Assert(privatePlan.MaximumHistoryTurns > 0 && !privatePlan.SuppressGlobalHistory,
            "Private conversation lost all useful recent history.");

        var constrainedSettings = new LocalGgufBrainSettings
        {
            ContextSize = 1024,
            MaxTokens = 128,
            PrivateConversationMaxTokens = 128,
            PromptSafetyMarginTokens = 128,
            HistoryMessageLimit = 4,
        };
        constrainedSettings.Normalize();
        var constrainedPlan = BorinLocalRuntimeOptimization.CreatePlan(privateContext, constrainedSettings);
        Assert(constrainedPlan.SystemCharacterBudget + constrainedPlan.HistoryCharacterBudget + constrainedPlan.TurnCharacterBudget <= constrainedPlan.InputCharacterBudget,
            "The minimum supported context can over-allocate prompt characters.");
        Assert(constrainedPlan.TurnCharacterBudget >= 320,
            "The minimum supported context left no usable current-turn budget.");

        var externalContext = CreateExternalContext("Alice", "Borin, are we safe here?", Path.GetTempPath());
        var externalPlan = BorinLocalRuntimeOptimization.CreatePlan(externalContext, settings);
        Assert(externalPlan.Mode == BorinLocalPromptModes.ExternalConversation,
            $"Unexpected external prompt mode: {externalPlan.Mode}");
        Assert(externalPlan.SuppressGlobalHistory && externalPlan.MaximumHistoryTurns == 0,
            "External conversation can still receive mixed global conversation history.");
        Assert(externalPlan.EffectiveMaxTokens <= 160,
            "External conversation reserved too many output tokens for a 270-character reply.");

        var researchContext = CreateContext(
            "Audit the local prompt construction and cite the relevant files.",
            Path.GetTempPath(),
            purpose: "Local repository research");
        var researchPlan = BorinLocalRuntimeOptimization.CreatePlan(researchContext, settings);
        Assert(researchPlan.Mode == BorinLocalPromptModes.RepositoryResearch,
            $"Unexpected research prompt mode: {researchPlan.Mode}");
        Assert(researchPlan.EffectiveMaxTokens >= 512,
            "Repository research did not retain enough output space for a supported report page.");

        var conversationalSampling = BorinLocalRuntimeOptimization.ResolveSampling(
            privateContext,
            settings,
            privatePlan,
            compactRetry: false);
        var technicalSampling = BorinLocalRuntimeOptimization.ResolveSampling(
            researchContext,
            settings,
            researchPlan,
            compactRetry: false);
        Assert(technicalSampling.Temperature < conversationalSampling.Temperature,
            "Technical turns are not more deterministic than character conversation.");
        return Task.CompletedTask;
    }

    private static async Task TestKnowledgeIndexCachingAsync(string root, CancellationToken token)
    {
        var repository = Path.Combine(root, "knowledge-cache");
        var docs = Path.Combine(repository, "Docs");
        Directory.CreateDirectory(docs);
        await File.WriteAllTextAsync(
            Path.Combine(docs, "PromptOptimization.md"),
            "Borin uses a bounded prompt budget. Warm knowledge cache avoids repeated full deserialization. External player history stays isolated.",
            token);

        var index = new BorinKnowledgeIndex();
        var build = await index.BuildAsync(repository, changedOnly: false, token);
        Assert(build.State.Equals("Ready", StringComparison.OrdinalIgnoreCase),
            $"Tiny knowledge index did not build: {build.Detail}");

        var first = await index.SearchAsync(repository, "bounded prompt budget warm knowledge cache", 4, token);
        Assert(first.Matches.Count > 0, "The first cached-index search returned no matching chunk.");
        var second = await index.SearchAsync(repository, "bounded prompt budget warm knowledge cache", 4, token);
        Assert(second.Matches.Count == first.Matches.Count,
            "The warm query cache changed the result set.");
        Assert(second.StatusDetail.Contains("warm query cache", StringComparison.OrdinalIgnoreCase),
            "The repeated search did not use the warm query-result cache.");
        Assert(File.Exists(BorinKnowledgeIndexPaths.GetStatusPath(repository)),
            "The lightweight knowledge-index status sidecar was not created.");
    }

    private static async Task TestKnowledgeCoreAsync(string root, CancellationToken token)
    {
        var repository = Path.Combine(root, "knowledge-core");
        var source = Path.Combine(repository, "Apps", "Source", "Sample");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(
            Path.Combine(source, "SettingsFlow.cs"),
            "namespace Sample; public sealed class SettingsFlow { public bool SysGoto { get; set; } public void SaveSettings() { Persist(SysGoto); } private void Persist(bool value) { } }",
            token);

        using var core = new BorinKnowledgeCore(new BorinKnowledgeIndex());
        var build = await core.BuildAsync(repository, changedOnly: false, token);
        Assert(build.State.Equals("Ready", StringComparison.OrdinalIgnoreCase),
            $"Knowledge Core document build did not complete: {build.Detail}");

        var exact = await core.SearchAsync(
            repository,
            new BorinKnowledgeQuery("SaveSettings", BorinKnowledgeSearchMode.Exact, 4),
            token);
        Assert(exact.Matches.Any(match => match.Text.Contains("SaveSettings", StringComparison.Ordinal)),
            "Exact Roslyn symbol retrieval did not return SaveSettings.");
        Assert(exact.Matches.Any(match => match.Confidence == BorinKnowledgeConfidence.Proven && match.Sources?.Count > 0),
            "Roslyn evidence did not retain PROVEN confidence and provenance.");

        var relationships = await core.SearchAsync(
            repository,
            new BorinKnowledgeQuery("Persist", BorinKnowledgeSearchMode.Relationship, 8),
            token);
        Assert(relationships.Matches.Any(match => match.Relationship.Contains("Persist", StringComparison.Ordinal)),
            "Relationship retrieval did not expose the SaveSettings-to-Persist reference.");
    }

    private static async Task TestMajorMudKnowledgeCatalogAsync(string root, CancellationToken token)
    {
        var repository = Path.Combine(root, "majormud-catalog");
        var classic = Path.Combine(repository, "Data", "Classic");
        Directory.CreateDirectory(classic);
        await File.WriteAllTextAsync(
            Path.Combine(classic, "items.json"),
            """
            {"authority":"Librarium","record_count":1,"records":[{"id":"item.965","legacy_id":965,"name":"contest rune","object_type":"item","confidence":"proven","relationships":{"quest":"quest.42","room":"room.map1.721"},"public_reference_0139":{"fields":{"class_restrictions":[1],"race_restrictions":[2],"stat_requirements":{"str":40},"abilities_named":[{"ability_id":135,"name":"MinLevel","value":"10"}]}}}]}
            """,
            token);
        await File.WriteAllTextAsync(
            Path.Combine(classic, "quests.json"),
            """
            {"authority":"Librarium","record_count":1,"records":[{"id":"quest.42","name":"Rune Hunt","object_type":"quest","confidence":"strongly_correlated","relationships":{"required_item":"item.965"}}]}
            """,
            token);
        await File.WriteAllTextAsync(
            Path.Combine(classic, "rooms.json"),
            """
            {"authority":"Librarium","record_count":2,"records":[{"id":"room.map1.1","name":"Town Gates","object_type":"room","confidence":"proven","relationships":{"exits":["exit.map1.1.n"]}},{"id":"room.map1.2","name":"North Road","object_type":"room","confidence":"proven","relationships":{"exits":["exit.map1.2.s"]}}]}
            """,
            token);
        await File.WriteAllTextAsync(
            Path.Combine(classic, "exits.json"),
            """
            {"authority":"Librarium","record_count":2,"records":[{"id":"exit.map1.1.n","name":"exit.map1.1.n","object_type":"exit","direction":"N","confidence":"proven","relationships":{"from_room":"room.map1.1","to_room":"room.map1.2"}},{"id":"exit.map1.2.s","name":"exit.map1.2.s","object_type":"exit","direction":"S","confidence":"proven","relationships":{"from_room":"room.map1.2","to_room":"room.map1.1"}}]}
            """,
            token);
        await File.WriteAllTextAsync(
            Path.Combine(classic, "shops.json"),
            """
            {"authority":"Librarium","record_count":1,"records":[{"id":"shop.1","name":"Rune Shop","object_type":"shop","room_ref":"room.map1.2","confidence":"proven","inventory":[{"item_ref":"item.965","normal_qty":1}]}]}
            """,
            token);
        await File.WriteAllTextAsync(
            Path.Combine(classic, "npcs.json"),
            """
            {"authority":"Librarium","record_count":1,"records":[{"id":"npc.10","name":"rune keeper","object_type":"npc","confidence":"proven","public_reference_0139":{"fields":{"loot_named":{"name":"contest rune","item_id":965,"drop_percent":25}}}}]}
            """,
            token);
        await File.WriteAllTextAsync(
            Path.Combine(classic, "spawns.json"),
            """
            {"authority":"Librarium","record_count":1,"records":[{"id":"spawn.10.1","object_type":"spawn","npc_name":"rune keeper","qualified_room_ref":"room.map1.2","confidence":"strongly_correlated"}]}
            """,
            token);
        await File.WriteAllTextAsync(Path.Combine(classic, "classes.json"),
            "{\"records\":[{\"id\":\"class.1\",\"name\":\"Warrior\",\"sourceNumbers\":{\"number\":1},\"confidence\":\"proven\"}]}", token);
        await File.WriteAllTextAsync(Path.Combine(classic, "races.json"),
            "{\"records\":[{\"id\":\"race.2\",\"name\":\"Dwarf\",\"sourceNumbers\":{\"number\":2},\"confidence\":\"proven\"}]}", token);

        var catalog = new BorinMajorMudKnowledgeCatalog();
        var status = await catalog.BuildAsync(repository, changedOnly: false, token);
        Assert(status.Records == 11 && status.Relationships >= 14,
            $"Unexpected MajorMUD catalog counts: {status.Records} records, {status.Relationships} relationships.");
        var exact = await catalog.SearchAsync(
            repository,
            new BorinKnowledgeQuery("item.965", BorinKnowledgeSearchMode.Exact, 4),
            4,
            token);
        Assert(exact.Any(item => item.Text.Contains("contest rune", StringComparison.OrdinalIgnoreCase) &&
                                 item.Confidence == BorinKnowledgeConfidence.Proven),
            "Exact generated-item retrieval did not retain identity and confidence.");
        var exactAgain = await catalog.SearchAsync(repository,
            new BorinKnowledgeQuery("item.965", BorinKnowledgeSearchMode.Exact, 4), 4, token);
        Assert(ReferenceEquals(exact, exactAgain), "Repeated MajorMUD query did not reuse the bounded warm result cache.");
        var relationship = await catalog.SearchAsync(
            repository,
            new BorinKnowledgeQuery("item.965", BorinKnowledgeSearchMode.Relationship, 8),
            8,
            token);
        Assert(relationship.Any(item => item.Text.Contains("Rune Hunt", StringComparison.OrdinalIgnoreCase) &&
                                        item.Relationship.Contains("item.965", StringComparison.OrdinalIgnoreCase)),
            "Reverse quest-to-item relationship retrieval failed.");
        var room = await catalog.GetEntityAsync(repository, "room", "Town Gates", token);
        Assert(room is not null && room.Id == "room.map1.1" && room.Confidence == BorinKnowledgeConfidence.Proven,
            "Typed room lookup did not return the authoritative room entity.");
        var path = await catalog.FindPathAsync(repository, "1/1", "room.map1.2", 5, token);
        Assert(path.Found && path.Steps.Count == 1 && path.Directions == "N",
            $"Authoritative room pathfinding failed: {path.Detail}");
        using var core = new BorinKnowledgeCore(new BorinKnowledgeIndex(), catalog);
        var routeEvidence = await core.SearchAsync(repository, "Find a route from 1/1 to 1/2", 8, token);
        Assert(routeEvidence.Matches.Any(item => item.Text.Contains("topology route", StringComparison.OrdinalIgnoreCase) &&
                                                item.Text.Contains("1. N:", StringComparison.Ordinal)),
            "Natural route questions did not receive authoritative path evidence.");
        var sources = await catalog.FindAcquisitionSourcesAsync(repository, "contest rune", token);
        Assert(sources.Any(source => source.Id == "shop.1") && sources.Any(source => source.Id == "npc.10"),
            "Item acquisition tracing did not find both shop inventory and NPC loot evidence.");
        var acquisitionEvidence = await core.SearchAsync(repository, "Where can I get contest rune?", 12, token);
        Assert(acquisitionEvidence.Matches.Any(match => match.Text.Contains("Rune Shop", StringComparison.OrdinalIgnoreCase)) &&
               acquisitionEvidence.Matches.Any(match => match.Text.Contains("rune keeper", StringComparison.OrdinalIgnoreCase)),
            "Natural acquisition questions did not receive shop and NPC evidence.");
        var spawns = await catalog.FindNpcSpawnsAsync(repository, "rune keeper", token);
        Assert(spawns.Count == 1 && spawns[0].Relationships.Any(edge => edge.Contains("room.map1.2", StringComparison.OrdinalIgnoreCase)),
            "NPC spawn tracing did not return the qualified spawn room.");
        var requirements = await catalog.GetItemRequirementsAsync(repository, "contest rune", token);
        Assert(requirements is not null && requirements.MinimumLevel == 10 &&
               requirements.AllowedClasses.Contains("Warrior (#1)") && requirements.AllowedRaces.Contains("Dwarf (#2)") &&
               requirements.MinimumStats.TryGetValue("STR", out var strength) && strength == 40,
            "Typed item eligibility did not retain level, class, race, and stat requirements.");
        var requirementEvidence = await core.SearchAsync(repository, "Who can equip contest rune and what are its requirements?", 8, token);
        Assert(requirementEvidence.Matches.Any(match => match.Text.Contains("Warrior (#1)", StringComparison.Ordinal) &&
                                                       match.Text.Contains("STR 40", StringComparison.Ordinal)),
            "Natural requirement questions did not receive decoded eligibility evidence.");
        var dependencies = await catalog.TraceDependenciesAsync(repository, "What depends on contest rune?", 3, token);
        Assert(dependencies.Any(entity => entity.Id == "quest.42") && dependencies.Any(entity => entity.Id == "shop.1"),
            "Multi-hop dependency tracing did not include incoming quest and shop relationships.");
        var dependencyEvidence = await core.SearchAsync(repository, "What depends on contest rune?", 20, token);
        Assert(dependencyEvidence.Matches.Any(match => match.Text.Contains("Rune Hunt", StringComparison.OrdinalIgnoreCase)),
            "Natural dependency questions did not receive quest evidence.");
        var evaluation = await catalog.RunMasteryEvaluationAsync(repository, token);
        Assert(evaluation.Cases.Count >= 6 && evaluation.Failed == 0 && evaluation.Score == 100,
            $"Evidence-based mastery evaluation failed: {evaluation.Summary}");
        Assert(File.Exists(evaluation.StoragePath) && evaluation.Cases.All(item => item.Sources.Count > 0),
            "Mastery evaluation did not persist source-backed case evidence.");
    }

    private static async Task TestAcademyAsync(string root, CancellationToken token)
    {
        var repository = Path.Combine(root, "academy");
        Directory.CreateDirectory(Path.Combine(repository, "Apps", "Source", "Academy"));
        Directory.CreateDirectory(Path.Combine(repository, "Data", "Classic"));
        await File.WriteAllTextAsync(Path.Combine(repository, "Apps", "Source", "Academy", "AcademyEvidence.cs"),
            "namespace Academy; public sealed class Architecture { } public sealed class Runtime { } public sealed class Persistence { } public sealed class Commands { } public sealed class Testing { } public sealed class Borin { public void SaveSetting() { } }", token);
        await File.WriteAllTextAsync(Path.Combine(repository, "Data", "Classic", "rooms.json"),
            "{\"authority\":\"Librarium\",\"records\":[{\"id\":\"room.map1.1847\",\"name\":\"Rusty Axe Tavern\",\"object_type\":\"room\",\"confidence\":\"proven\",\"relationships\":{}}]}", token);
        await File.WriteAllTextAsync(Path.Combine(repository, "Data", "Classic", "commands.json"),
            "{\"authority\":\"Librarium\",\"records\":[{\"id\":\"command.look\",\"name\":\"look\",\"object_type\":\"command\",\"confidence\":\"proven\",\"relationships\":{}}]}", token);
        var learning = new BorinVerifiedKnowledgeLearning(repository);
        var catalog = new BorinMajorMudKnowledgeCatalog();
        using var core = new BorinKnowledgeCore(new BorinKnowledgeIndex(), catalog, learning);
        await core.BuildAsync(repository, false, token);
        var academy = new BorinAcademy(core, learning);
        var questions = await academy.GenerateQuestionsAsync(repository, "All Subjects", 31, token);
        Assert(questions.Count > 0 && questions.All(q => q.ExpectedEvidenceLocation.Length > 0 && q.ExpectedConfidence >= BorinKnowledgeConfidence.StronglyCorrelated),
            $"Academy generated unsupported or source-free questions: count={questions.Count}; {string.Join("; ", questions.Select(q => $"{q.Subject}/{q.ExpectedConfidence}/{q.ExpectedEvidenceLocation}"))}");
        var majorMudQuestion = questions.FirstOrDefault(q => q.Domain == BorinAcademyDomain.MajorMud);
        Assert(majorMudQuestion is not null && majorMudQuestion.Subject == "Commands" &&
               majorMudQuestion.EvidenceQuery == "command.look" &&
               majorMudQuestion.ExpectedEvidenceLocation.Replace('\\', '/').StartsWith("Data/Classic/commands", StringComparison.OrdinalIgnoreCase) &&
               majorMudQuestion.Prompt.Contains("command.look", StringComparison.OrdinalIgnoreCase),
            "Academy did not bind the MajorMUD Commands curriculum question to its concrete authoritative command record.");
        Assert(questions.Where(q => q.Domain == BorinAcademyDomain.Reforged).All(q =>
                !q.ExpectedEvidenceLocation.Replace('\\', '/').StartsWith("Data/Classic/", StringComparison.OrdinalIgnoreCase)),
            "Academy paired a Reforged curriculum question with MajorMUD generated data.");
        var question = questions[0];
        var passed = await academy.EvaluateAsync(repository, question,
            $"{question.ExpectedEvidenceText}\nSource: {question.ExpectedEvidenceLocation}\nConfidence: {question.ExpectedConfidence.ToDisplayText()}", "Local test", token);
        Assert(passed.Passed && passed.Score.SourceQuality == 100, "Evidence-backed Academy answer did not pass scoring gates.");
        var failed = await academy.EvaluateAsync(repository, question,
            "This is certainly proven by Invented/Nowhere/Fake.cs.", "Lightning test", token);
        Assert(!failed.Passed && failed.FailureCause == BorinAcademyFailureCause.Hallucination,
            "Academy failed to reject a hallucinated source path.");
        var gaps = await academy.GetGapsAsync(repository, token);
        Assert(gaps.Any(g => g.Subject == question.Subject && g.Status == BorinKnowledgeGapStatus.Open),
            "Failed Academy answer did not create a persistent gap.");
        var gap = await academy.BeginGapInvestigationAsync(repository, gaps.First(g => g.Subject == question.Subject).Id, token);
        Assert(gap.Status == BorinKnowledgeGapStatus.Investigating, "Academy gap did not enter investigation state.");
        gap = await academy.ResolveGapAsync(repository, gap.Id, question.ExpectedEvidenceLocation, token);
        Assert(gap.Status == BorinKnowledgeGapStatus.Resolved && gap.ResolutionEvidence.Length > 0,
            "Academy resolved a gap without retaining evidence.");
        await academy.SetIdleTrainingAsync(repository, true, 50, token);
        var snapshot = await academy.GetSnapshotAsync(repository, token);
        Assert(snapshot.TrainWhileIdle && snapshot.IdleRequestsPerMinute == 10 && snapshot.MajorMudMastery is not null && snapshot.ReforgedMastery is null,
            "Academy idle cap, score persistence, or MajorMUD/Reforged Commands domain separation is incorrect.");
        var logs = Path.Combine(repository, "Release", "Logs"); Directory.CreateDirectory(logs);
        await File.WriteAllTextAsync(Path.Combine(logs, "verified.json"), "{\"build\":\"PASS\",\"tests\":\"PASS\",\"room\":\"room.map1.1847\"}", token);
        snapshot = await academy.ReplayHistoryAsync(repository, token);
        Assert(snapshot.CurrentTask.Contains("1 verified evidence", StringComparison.OrdinalIgnoreCase) &&
               (await learning.GetInvestigationsAsync(token)).Any(i => i.Evidence.EndsWith("verified.json", StringComparison.OrdinalIgnoreCase)),
            "Historical replay did not create a source-linked verified episode.");
        await File.AppendAllTextAsync(Path.Combine(repository, "Apps", "Source", "Academy", "AcademyEvidence.cs"), "\n// changed build", token);
        await core.BuildAsync(repository, true, token);
        snapshot = await academy.GetSnapshotAsync(repository, token);
        Assert(snapshot.MajorMudMastery is null && snapshot.CurrentTask.Contains("prior mastery attempts", StringComparison.OrdinalIgnoreCase),
            "Academy retained a mastery percentage after its source fingerprint changed.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var canceledSafely = false;
        try { await academy.GenerateQuestionsAsync(repository, "All Subjects", 5, canceled.Token); }
        catch (OperationCanceledException) { canceledSafely = true; }
        Assert(canceledSafely, "Academy question generation ignored cancellation.");
    }

    private static async Task TestAcademyRatePriorityAsync(CancellationToken token)
    {
        var budget = new SlidingWindowAiRateBudget(new AiRateBudgetOptions
        {
            NormalRequestsPerWindow = 2,
            HardRequestsPerWindow = 40,
            Window = TimeSpan.FromMinutes(1),
            MaximumQueueWait = TimeSpan.FromMilliseconds(20),
        });
        await budget.AcquireAsync(AiRatePriority.Background, token);
        var limited = false;
        try { await budget.AcquireAsync(AiRatePriority.Background, token); }
        catch (AiProviderException exception) when (exception.Kind == AiProviderFailureKind.RateLimited) { limited = true; }
        Assert(limited && budget.Snapshot.RemainingHardRequests == 39,
            "Academy background requests consumed or bypassed the interactive burst reserve.");
        await budget.AcquireAsync(AiRatePriority.InteractiveBurst, token);
    }

    private static async Task TestVerifiedKnowledgeLearningAsync(string root, CancellationToken token)
    {
        var repository = Path.Combine(root, "verified-learning");
        var store = new BorinVerifiedKnowledgeLearning(repository);
        await store.RecordObservationAsync(new BorinRuntimeKnowledgeObservation(
            "Room 472 west exit", "You cannot go that way.", "Test Server", "Test Realm", "Borin",
            "room.map1.472", "Release/Logs/test-realm-exit.json", DateTimeOffset.UtcNow,
            ["room.map1.472", "room.map1.473"]), token);
        var noConflict = await store.RecordComparisonAsync("Room 472 west exit", "blocked", "blocked",
            "Data/Classic/exits.json", "Release/Logs/test-realm-exit.json", 3, token);
        Assert(noConflict is null, "Matching expected and observed knowledge created a false conflict.");
        var conflict = await store.RecordComparisonAsync("Room 472 west exit", "leads to room.map1.473", "blocked",
            "Data/Classic/exits.json", "Release/Logs/test-realm-exit.json", 3, token);
        Assert(conflict is not null && conflict.Status == "Investigation Required" && conflict.ControlledAttempts == 3,
            "Expected/runtime mismatch did not create a controlled conflict.");
        var firstAutomatic = await store.ObserveComparisonAsync("Room 900 north exit", "leads to room.map1.901", "blocked",
            "Data/Classic/exits.json", "Release/Logs/runtime-1.json", 3, token);
        var secondAutomatic = await store.ObserveComparisonAsync("Room 900 north exit", "leads to room.map1.901", "blocked",
            "Data/Classic/exits.json", "Release/Logs/runtime-2.json", 3, token);
        var thirdAutomatic = await store.ObserveComparisonAsync("Room 900 north exit", "leads to room.map1.901", "blocked",
            "Data/Classic/exits.json", "Release/Logs/runtime-3.json", 3, token);
        Assert(firstAutomatic is null && secondAutomatic is null && thirdAutomatic?.ControlledAttempts == 3,
            "Automatic conflict detection did not enforce the three-observation threshold.");
        var rejected = false;
        try
        {
            await store.PromoteProcedureAsync("Investigate Broken Exit", ["Inspect both rooms"], false, string.Empty, token);
        }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected, "Unverified procedure was promoted to trusted procedural memory.");
        await store.PromoteProcedureAsync("Investigate Broken Exit",
            ["Inspect source room", "Inspect destination", "Test in Test Realm"], true, "3/3 controlled verification passed", token);
        var investigation = await store.RecordInvestigationAsync("Repair Room 472 west exit", "Verified movement 3/3",
            "Release/Logs/fixed-exit.json", true, ["room.map1.472", "room.map1.473"], token);
        var results = await store.SearchAsync("broken exit room 472", 10, token);
        var status = await store.GetStatusAsync(token);
        Assert(status.Observations == 1 && status.Conflicts == 2 && status.Procedures == 1 && status.Investigations == 1,
            "Verified learning ledger counts are incorrect.");
        Assert(results.Any(item => item.Text.Contains("KNOWLEDGE CONFLICT", StringComparison.Ordinal)) &&
               results.Any(item => item.Text.Contains("Verified procedure", StringComparison.Ordinal)) &&
               results.Any(item => item.Text.Contains(investigation.Id, StringComparison.Ordinal) && item.Text.Contains("room.map1.472", StringComparison.Ordinal)),
            "Conflicts, investigations, and verified procedures were not searchable with authoritative entity links.");
        Assert((await store.GetInvestigationsAsync(token)).Single().Status == "Verified",
            "Verified investigation episodes were not retained separately from conversational memory.");
        var resolved = await store.ResolveConflictAsync(conflict!.Id, "Reciprocal exit corrected and verified 3/3", "Release/Logs/fixed-exit.json", token);
        Assert(resolved.Status == "Resolved" && resolved.Expected.Contains("room.map1.473", StringComparison.Ordinal) &&
               resolved.Observed == "blocked" && resolved.ResolutionEvidence.EndsWith("fixed-exit.json", StringComparison.Ordinal),
            "Conflict resolution did not preserve the original claims and verification evidence.");
    }

    private static BrainContext CreateContext(
        string request,
        string repositoryRoot,
        string purpose = "Conversation",
        BorinSocialMemorySnapshot? socialMemory = null,
        BorinEpisodicMemorySnapshot? episodicMemory = null,
        BorinTemporalContinuitySnapshot? temporalContinuity = null,
        BorinConversationRepairSnapshot? conversationRepair = null,
        BorinGroupConversationSnapshot? groupConversation = null,
        BorinGameStateSnapshot? gameState = null,
        BorinSelfKnowledgeSnapshot? selfKnowledge = null)
    {
        var state = new ForgeConversationState
        {
            Configuration = new ForgeConfiguration
            {
                RepositoryPath = repositoryRoot,
                CharacterName = "Borin Ironbeard",
                AddressUserAs = "lad",
            },
        };

        return BrainContext.ForBorin(
            request,
            state,
            repositorySnapshot: null,
            purpose: purpose,
            socialMemory: socialMemory,
            episodicMemory: episodicMemory,
            temporalContinuity: temporalContinuity,
            conversationRepair: conversationRepair,
            groupConversation: groupConversation,
            gameState: gameState,
            selfKnowledge: selfKnowledge);
    }

    private static BrainContext CreateExternalContext(
        string speaker,
        string message,
        string repositoryRoot,
        string channel = "telepath",
        bool directlyAddressed = true,
        string replyDisposition = "normal reply",
        bool yieldFloor = false,
        BorinGameStateSnapshot? gameState = null)
    {
        var request = $"""
Speaker: {speaker}
Channel: {channel}
Audience: {(channel.Equals("telepath", StringComparison.OrdinalIgnoreCase) ? $"private conversation with {speaker}" : "visible room conversation")}
Addressed to Borin: {directlyAddressed}
Addressing mode: {(directlyAddressed ? "direct address" : "not addressed to Borin")}
Reply disposition: {replyDisposition}
Yield the floor: {yieldFloor}
Busy conversation: false
Loop risk: false
Recent participants: {speaker}
Recent public context: none
Group response policy: {(yieldFloor ? "Remain silent and yield." : "Answer once and yield naturally.")}
Message: {message}
""";
        return CreateContext(
            request,
            repositoryRoot,
            purpose: "External MajorMUD conversation",
            gameState: gameState);
    }

    private static BrainContext WithDirection(BrainContext context, BorinConversationDirection snapshot) =>
        context with { Conversation = context.Conversation with { ConversationDirection = snapshot } };

    private static BrainContext WithSocial(BrainContext context, BorinSocialMemorySnapshot snapshot) =>
        context with { Conversation = context.Conversation with { SocialMemory = snapshot } };

    private static BrainContext WithEpisode(BrainContext context, BorinEpisodicMemorySnapshot snapshot) =>
        context with { Conversation = context.Conversation with { EpisodicMemory = snapshot } };

    private static BrainContext WithTemporal(BrainContext context, BorinTemporalContinuitySnapshot snapshot) =>
        context with { Conversation = context.Conversation with { TemporalContinuity = snapshot } };

    private static BrainContext WithRepair(BrainContext context, BorinConversationRepairSnapshot snapshot) =>
        context with { Conversation = context.Conversation with { ConversationRepair = snapshot } };

    private static async Task RunCaseAsync(
        ICollection<BorinCharacterTestCaseResult> cases,
        string name,
        Func<Task> test,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await test();
            cases.Add(new BorinCharacterTestCaseResult(name, true, "Passed", stopwatch.Elapsed.TotalMilliseconds));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            cases.Add(new BorinCharacterTestCaseResult(
                name,
                false,
                exception.Message,
                stopwatch.Elapsed.TotalMilliseconds));
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The report is more important than cleanup. Windows antivirus or a
            // still-closing file handle may temporarily retain the test folder.
        }
    }

    private sealed class HarnessLogger : IAppLogger
    {
        private readonly List<string> _recent = [];
        public IReadOnlyList<string> Recent => _recent;
        public void Info(string message) => _recent.Add(message);
        public void Error(string message, Exception? exception = null) =>
            _recent.Add(exception is null ? message : $"{message}: {exception.Message}");
    }
}
