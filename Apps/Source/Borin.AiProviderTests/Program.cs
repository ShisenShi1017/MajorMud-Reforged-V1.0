using System.Net;
using System.Text;
using System.Text.Json;
using Reforged.App.Brains;
using Reforged.App.Forge;
using Reforged.App.BorinCapabilities;

var tests = new (string Name, Func<Task> Run)[]
{
    ("NVIDIA request uses configured endpoint, model, and bearer credential", ValidCompletionAsync),
    ("Academy answers retain structured speech and evidence rationale", AcademyDecisionTextAsync),
    ("NVIDIA engineering requests return a full structured patch artifact", EngineeringArtifactAsync),
    ("Natural Borin engineering requests start jobs without slash commands", NaturalEngineeringRoutingAsync),
    ("Pronoun repair follow-ups retain the previously described defect", ContextualEngineeringRoutingAsync),
    ("Developer evidence prioritizes the real Sys Goto implementation", SysGotoEvidenceAsync),
    ("Developer patch validation rejects placeholders and invented paths", PatchCompletenessAsync),
    ("NVIDIA provider rejects a missing credential before network use", MissingCredentialAsync),
    ("NVIDIA provider classifies authentication failure without exposing the key", AuthenticationFailureAsync),
    ("NVIDIA provider preserves 429 retry metadata for the shared scheduler", RateLimitAsync),
    ("NVIDIA provider rejects malformed completions", MalformedResponseAsync),
    ("NVIDIA connection probe survives truncated optional greeting JSON only", ConnectionProbeTruncationAsync),
    ("AI context redacts credential-shaped content", RedactionAsync),
    ("Context builder injects authoritative connected and logged-in state", VerifiedStateAsync),
    ("Context builder sends only selected bounded memory", MemoryMinimizationAsync),
    ("Context builder omits absolute repository paths", RepositoryPrivacyAsync),
    ("Developer context includes bounded exact repository evidence", DeveloperEvidenceAsync),
    ("NVIDIA request honors caller cancellation", CancellationAsync),
    ("Structured decision parser rejects unknown fields and invalid types", StructuredParserAsync),
    ("Action validator rejects stale state fingerprints", StaleDecisionAsync),
    ("Action validator rejects actions when live state changes during reasoning", StateChangedAsync),
    ("Action validator admits only currently verified movement exits", MovementValidationAsync),
    ("Action validator enforces developer permissions", DeveloperPermissionAsync),
    ("Action validator rejects duplicate actions for the same state", DuplicateActionAsync),
    ("Shared rate budget enforces normal pacing and bounded burst capacity", RateBudgetAsync),
    ("Shared rate budget wait is cancellable", RateBudgetCancellationAsync),
    ("Hybrid router keeps routine gameplay local and sends complex work to NVIDIA", HybridRoutingAsync),
    ("Hybrid router falls back locally on classified NVIDIA failure", HybridFallbackAsync),
    ("Capability catalog covers all Borin roles while filtering permissions", CapabilityCatalogAsync),
    ("Controlled repository reader blocks path escape and binary files", RepositoryContainmentAsync),
    ("Controlled executor translates verified movement through the player gateway", PlayerDispatchAsync),
    ("Controlled executor validates before dispatch", ValidationBeforeDispatchAsync),
    ("Controlled executor dispatches developer reads without shell access", DeveloperDispatchAsync),
    ("Unregistered Sysop tools fail closed", UnboundSysopAsync),
    ("Worldsmith Sysop gateway remains fully withheld", SysopTestRealmAsync),
    ("Worldsmith remains withheld regardless of target realm", SysopLiveRealmBlockedAsync),
    ("Autonomous player gateway accepts ordinary full-game commands", FullPlayerCommandAsync),
    ("Autonomous player gateway blocks protected control commands", ProtectedPlayerCommandAsync),
    ("Developer may apply patches to the isolated workspace without main incorporation", WorkspacePatchAuthorityAsync),
    ("Main repository incorporation remains human-only", MainIncorporationHeldAsync)
    ,("Verified engineering job can incorporate only through the explicit local orchestrator action", VerifiedIncorporationAsync),
    ("All NVIDIA tiers use the shared secure credential target", SharedCredentialSlotAsync),
    ("Tier router selects Local, Lightning, and Ultra from task metadata", TierSelectionAsync),
    ("Low-confidence Lightning result escalates to Ultra only once", LowConfidenceEscalationAsync),
    ("Unavailable Ultra falls back with the same task context", UltraFallbackAsync),
    ("Unavailable Lightning does not consume Ultra for an ordinary task", LightningFailureIsolationAsync),
    ("Lightning and Ultra rate budgets account independently", IndependentRateAccountingAsync),
    ("Critical Ultra work receives one bounded Super verification", CriticalSuperVerificationAsync),
    ("Super disagreement lowers confidence without replacing Ultra", SuperDisagreementAsync),
    ("Automatic escalation configuration persists", EscalationConfigurationPersistenceAsync),
    ("Borin routes explicit capabilities without hijacking ordinary chat", GuardianCapabilityRoutingAsync),
    ("Retired Hall state migrates into Borin capability storage", BorinCapabilityMigrationAsync)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"Result: {tests.Length - failures}/{tests.Length} passed");
return failures == 0 ? 0 : 1;

static BrainContext Context(string request = "Inspect the verified state")
{
    var state = new ForgeConversationState();
    state.Configuration.CharacterName = "Borin Ironbeard";
    return BrainContext.ForBorin(request, state, null);
}

static async Task ValidCompletionAsync()
{
    var handler = new StubHandler(request =>
    {
        Equal("https://integrate.api.nvidia.com/v1/chat/completions", request.RequestUri?.ToString());
        Equal("Bearer", request.Headers.Authorization?.Scheme);
        Equal("nvapi-test-secret-value", request.Headers.Authorization?.Parameter);
        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var json = JsonDocument.Parse(body);
        Equal("nvidia/nemotron-3.5-lightning-30b-a3b", json.RootElement.GetProperty("model").GetString());
        Equal("json_schema", json.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        False(json.RootElement.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean(),
            "Structured NVIDIA requests must disable thinking so reasoning cannot truncate the JSON decision.");
        var schemaProperties = json.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema").GetProperty("properties");
        Equal(1000, schemaProperties.GetProperty("speech").GetProperty("maxLength").GetInt32());
        Equal(1200, schemaProperties.GetProperty("reason").GetProperty("maxLength").GetInt32());
        var system = json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
        True(system.Contains("This exact turn has reached NVIDIA NIM", StringComparison.Ordinal), "Current provider connectivity fact was omitted.");
        True(system.Contains("does not prove that a MajorMUD terminal session is connected", StringComparison.Ordinal), "Provider/game connection separation was omitted.");
        var fingerprint = json.RootElement.GetProperty("metadata").GetProperty("borin_state").GetString()!;
        var completion = Completion(DecisionJson("Speak", fingerprint, "Aye, inspect first."), "req-1", 12, 5);
        completion.Headers.TryAddWithoutValidation("x-ratelimit-limit-requests", "40");
        completion.Headers.TryAddWithoutValidation("x-ratelimit-remaining-requests", "39");
        return completion;
    });
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore("nvapi-test-secret-value"));
    var response = await provider.ThinkAsync(Context());
    Equal(BorinAiIntent.Speak, response.Decision?.Intent);
    Equal("Aye, inspect first.", response.Decision?.Speech);
    Equal(12, response.PromptTokens);
    Equal(5, response.CompletionTokens);
    Equal(40, provider.EndpointStatus.ReportedRequestLimit);
    Equal(39, provider.EndpointStatus.ReportedRemaining);
}

static async Task AcademyDecisionTextAsync()
{
    var provider = new StubAiProvider("lightning", _ => new AiResponse(
        "unused", "lightning", "lightning", Decision: new BorinAiDecision(
            "The item is an authoritative generated record.", BorinAiIntent.Speak, null, .95,
            "Data/Classic/items.json:1 establishes item.965. Confidence: PROVEN.", new string('a', 64))));
    var brain = new AiProviderBrainAdapter(provider);
    var context = Context("Answer from evidence") with { Purpose = "Borin Academy evidence-based training" };
    var response = await brain.ThinkAsync(context);
    True(response.Text.Contains("authoritative generated record", StringComparison.Ordinal),
        "Academy adapter discarded the model speech.");
    True(response.Text.Contains("Data/Classic/items.json:1", StringComparison.Ordinal) &&
         response.Text.Contains("Confidence: PROVEN", StringComparison.Ordinal),
        "Academy adapter discarded the model's structured evidence rationale.");
    Equal(BorinAiIntent.Speak, response.Decision?.Intent);
}

static async Task EngineeringArtifactAsync()
{
    var handler = new StubHandler(request =>
    {
        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var json = JsonDocument.Parse(body);
        var responseFormat = json.RootElement.GetProperty("response_format").GetProperty("json_schema");
        Equal("borin_engineering_proposal", responseFormat.GetProperty("name").GetString());
        var properties = responseFormat.GetProperty("schema").GetProperty("properties");
        Equal(100000, properties.GetProperty("artifact").GetProperty("maxLength").GetInt32());
        var fingerprint = json.RootElement.GetProperty("metadata").GetProperty("borin_state").GetString()!;
        var proposal = JsonSerializer.Serialize(new
        {
            diagnosis = "The verified source needs one focused correction.",
            artifact = "```diff\n--- a/example.cs\n+++ b/example.cs\n@@ -1 +1 @@\n-old\n+new\n```",
            confidence = 0.91,
            reason = "The proposed line directly addresses the supplied evidence.",
            stateFingerprint = fingerprint
        });
        return Completion(proposal, "req-engineering", 20, 40);
    });
    var state = new ForgeConversationState();
    state.Configuration.CharacterName = "Borin Ironbeard";
    var context = BrainContext.ForBorin("Draft the correction", state, null, "Controlled engineering draft");
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore("nvapi-test-secret-value"));
    var response = await provider.ThinkAsync(context);
    True(response.Text.Contains("```diff", StringComparison.Ordinal), "The full patch artifact was not rendered for the engineering workflow.");
    True(response.Text.Contains("Confidence: 0.91", StringComparison.Ordinal), "Engineering proposal confidence was not retained.");
    Equal(null, response.Decision);
}

static Task NaturalEngineeringRoutingAsync()
{
    True(BorinEngineeringRequestRouter.TryGetCommission(
        "Borin, investigate why Sys Goto does not stay checked. Create a patch in the isolated workspace.",
        out var commission), "A naturally addressed investigation did not start an engineering job.");
    True(commission.StartsWith("investigate why Sys Goto", StringComparison.OrdinalIgnoreCase),
        "The Borin address was not removed from the engineering commission.");
    True(BorinEngineeringRequestRouter.TryGetCommission("propose a patch for the checkbox persistence bug", out _),
        "A natural patch proposal did not start an engineering job.");
    False(BorinEngineeringRequestRouter.TryGetCommission("How is your hybrid brain doing?", out _),
        "Ordinary conversation was incorrectly classified as an engineering job.");
    return Task.CompletedTask;
}

static Task ContextualEngineeringRoutingAsync()
{
    const string issue = "find out why when you type [hp=41]: exp you get exp needed for next level: unavailable because generated progression data contains no evidence-promoted row for level 2";
    True(BorinEngineeringRequestRouter.TryGetContextualCommission("find and propose a fix for it", issue, out var commission),
        "The repair follow-up was not bound to the immediately preceding diagnostic issue.");
    True(commission.Contains("generated progression data", StringComparison.OrdinalIgnoreCase) &&
         commission.Contains("propose a fix", StringComparison.OrdinalIgnoreCase),
        "The resolved engineering commission lost either the requested action or original EXP symptom.");
    False(BorinEngineeringRequestRouter.TryGetContextualCommission("fix it", "How are you doing today?", out _),
        "An ambiguous repair pronoun was attached to unrelated conversation.");
    var interruptedThread = new[]
    {
        "Borin: progression generation evidence promotion level race class",
        "Borin: The generated progression data is missing the evidence-promoted level 2 row.",
        "You: " + issue,
    };
    True(BorinEngineeringRequestRouter.TryGetContextualCommission("fix it", interruptedThread, out var recovered) &&
         recovered.Contains("level 2", StringComparison.OrdinalIgnoreCase),
        "The repair follow-up did not recover the recent diagnostic thread after an intervening investigation reply.");
    return Task.CompletedTask;
}

static async Task SysGotoEvidenceAsync()
{
    var evidence = await new BorinReadOnlyAccess().GatherAsync(
        Directory.GetCurrentDirectory(),
        "investigate why Sys Goto does not stay checked",
        16);
    True(evidence.Items.Any(item => item.RelativePath.Equals("Apps/Source/Reforged.App/Views/EditorView.xaml", StringComparison.OrdinalIgnoreCase)),
        "The real Sys Goto view was not retrieved within the bounded evidence budget.");
    True(evidence.Items.Any(item => item.RelativePath.Equals("Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs", StringComparison.OrdinalIgnoreCase)),
        "The real Sys Goto view model was not retrieved within the bounded evidence budget. Selected: " + string.Join(", ", evidence.Items.Select(item => item.RelativePath).Distinct()));
    True(evidence.Items.Any(item => item.RelativePath.Equals("Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs", StringComparison.OrdinalIgnoreCase) &&
        item.Snippet.Contains("public sealed class EditorViewModel", StringComparison.Ordinal)),
        "The Sys Goto evidence did not include exact surrounding source needed for an applicable patch hunk.");
    True(evidence.Items.Any(item => item.RelativePath.Equals("Apps/Source/Reforged.App/App.xaml.cs", StringComparison.OrdinalIgnoreCase) && item.Snippet.Contains("new EditorView", StringComparison.Ordinal)),
        "The construction reference for EditorView was not retrieved, so Borin could not trace the missing view-model attachment. Selected: " + string.Join(" | ", evidence.Items.Select(item => item.RelativePath + ": " + item.Snippet)));
}

static Task PatchCompletenessAsync()
{
    False(BorinPatchProposalValidator.TryValidateCompleteness(
        "--- a/Apps/Yöntem/Fake.cs\n+++ b/Apps/Source/Fake.cs\n+value = /* logic to retrieve and persist checked state */;",
        out _), "An invented non-ASCII path and placeholder implementation were accepted.");
    True(BorinPatchProposalValidator.TryValidateCompleteness(
        "--- a/Apps/Source/Example.cs\n+++ b/Apps/Source/Example.cs\n-old\n+new",
        out _), "A complete minimal patch was rejected.");
    return Task.CompletedTask;
}

static async Task MissingCredentialAsync()
{
    var handler = new StubHandler(_ => throw new InvalidOperationException("Network must not be used."));
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore(null));
    var exception = await ThrowsAsync<AiProviderException>(() => provider.ThinkAsync(Context()));
    Equal(AiProviderFailureKind.Configuration, exception.Kind);
    Equal(0, handler.RequestCount);
}

static async Task AuthenticationFailureAsync()
{
    const string key = "nvapi-super-secret-token";
    var handler = new StubHandler(_ => Json(HttpStatusCode.Unauthorized, $"credential {key} invalid"));
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore(key));
    var exception = await ThrowsAsync<AiProviderException>(() => provider.ThinkAsync(Context()));
    Equal(AiProviderFailureKind.Authentication, exception.Kind);
    False(exception.Message.Contains(key, StringComparison.Ordinal), "Exception leaked the API key.");
    True(exception.Message.Contains(AiSecretRedactor.Redacted, StringComparison.Ordinal), "Exception did not redact the API key.");
}

static async Task RateLimitAsync()
{
    var handler = new StubHandler(_ =>
    {
        var response = Json(HttpStatusCode.TooManyRequests, "rate limited");
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(4));
        return response;
    });
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore("nvapi-test-secret-value"));
    var exception = await ThrowsAsync<AiProviderException>(() => provider.ThinkAsync(Context()));
    Equal(AiProviderFailureKind.RateLimited, exception.Kind);
    Equal(TimeSpan.FromSeconds(4), exception.RetryAfter);
}

static async Task MalformedResponseAsync()
{
    var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{not-json"));
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore("nvapi-test-secret-value"));
    var exception = await ThrowsAsync<AiProviderException>(() => provider.ThinkAsync(Context()));
    Equal(AiProviderFailureKind.MalformedResponse, exception.Kind);
}

static async Task ConnectionProbeTruncationAsync()
{
    var truncated = "{\"speech\":\"Aye\",\"intent\":\"Speak\",\"target\":null,\"confidence\":0.9,\"reason\":\"answering";
    var handler = new StubHandler(_ => Completion(truncated, "req-truncated", 10, 2048));
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore("nvapi-test-secret-value"));
    var state = new ForgeConversationState();
    var connectionContext = BrainContext.ForBorin("Return a short greeting", state, null, "NVIDIA Lightning connection test");
    var response = await provider.ThinkAsync(connectionContext);
    Equal(BorinAiIntent.Speak, response.Decision?.Intent);
    True(response.UsedFallback && response.FallbackReason?.Contains("greeting JSON", StringComparison.Ordinal) == true,
        "Connection probe did not report its safe local greeting fallback.");

    var strictException = await ThrowsAsync<AiProviderException>(() => provider.ThinkAsync(Context("ordinary operational request")));
    Equal(AiProviderFailureKind.MalformedResponse, strictException.Kind);
}

static async Task RedactionAsync()
{
    const string key = "nvapi-abcdefghijklmnop";
    string? captured = null;
    var handler = new StubHandler(request =>
    {
        captured = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(captured);
        var fingerprint = document.RootElement.GetProperty("metadata").GetProperty("borin_state").GetString()!;
        return Completion(DecisionJson("Speak", fingerprint, "safe"));
    });
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore("transport-key"));
    await provider.ThinkAsync(Context($"diagnose this accidental token {key}"));
    False(captured!.Contains(key, StringComparison.Ordinal), "Request context leaked credential-shaped text.");
    True(captured.Contains(AiSecretRedactor.Redacted, StringComparison.Ordinal), "Request context did not contain the redaction marker.");
}

static Task VerifiedStateAsync()
{
    var game = BorinGameStateSnapshot.Empty with
    {
        Phase = BorinGamePhase.InGame,
        ConnectionState = "Connected",
        LoginState = BorinLoginState.Ready,
        IsConnected = true,
        IsLoggedIntoMajorMud = true,
        HasVerifiedLiveState = true,
        HasVerifiedPrompt = true,
        PromptFreshness = "just now",
        CharacterName = "Borin",
        World = "Classic",
        CurrentRoom = "The Rusty Axe Tavern",
        CurrentRoomKey = "room.map1.1847",
        HasVerifiedRoom = true,
        CurrentExits = ["north", "west"],
        HasVerifiedExits = true,
        CapturedAt = DateTimeOffset.Parse("2026-08-20T12:00:00Z")
    };
    var state = new ForgeConversationState();
    var context = BrainContext.ForBorin("Where am I?", state, null, "MajorMUD player reasoning", gameState: game);
    var package = new BorinContextBuilder().Build(context);
    True(package.Role.HasFlag(BorinRole.Player), "Player role was not selected.");
    True(package.UserMessage.Contains("connected=True", StringComparison.Ordinal), "Connected state was not injected.");
    True(package.UserMessage.Contains("loggedIntoMajorMud=True", StringComparison.Ordinal), "Authenticated state was not injected.");
    True(package.UserMessage.Contains("The Rusty Axe Tavern", StringComparison.Ordinal), "Verified room was not injected.");
    True(package.StateFingerprint.Length == 64, "State fingerprint was not generated.");
    return Task.CompletedTask;
}

static Task MemoryMinimizationAsync()
{
    var entries = Enumerable.Range(1, 6).Select(index => new BorinMemoryEntry
    {
        Id = $"memory-{index}",
        Text = $"selected-memory-{index}",
        Category = BorinMemoryCategory.ProjectDecision,
        Evidence = BorinMemoryEvidence.Decided
    }).ToArray();
    var memory = new BorinMemorySnapshot(entries, 400, 200, 200, 0, "private-memory-path");
    var context = BrainContext.ForBorin("Review memory", new ForgeConversationState(), null, memory: memory);
    var package = new BorinContextBuilder(new BorinContextBuilderOptions { MaximumDurableMemories = 2 }).Build(context);
    Equal(2, package.IncludedDurableMemories);
    True(package.UserMessage.Contains("selected-memory-1", StringComparison.Ordinal), "First relevant memory missing.");
    False(package.UserMessage.Contains("selected-memory-3", StringComparison.Ordinal), "Unselected memory leaked into provider context.");
    False(package.UserMessage.Contains("private-memory-path", StringComparison.Ordinal), "Memory storage path leaked into provider context.");
    return Task.CompletedTask;
}

static Task RepositoryPrivacyAsync()
{
    var repository = new ForgeRepositorySnapshot(
        @"C:\Users\Private\SecretRepository",
        10,
        100,
        3,
        4,
        ["MajorMud.Reforged.sln"],
        [@"Logs\latest.log"]);
    var context = BrainContext.ForBorin("Inspect architecture", new ForgeConversationState(), repository, "Developer repository analysis");
    var package = new BorinContextBuilder().Build(context);
    False(package.UserMessage.Contains(repository.RootPath, StringComparison.OrdinalIgnoreCase), "Absolute repository path leaked.");
    True(package.UserMessage.Contains("projects=10", StringComparison.Ordinal), "Repository summary was not retained.");
    return Task.CompletedTask;
}

static Task DeveloperEvidenceAsync()
{
    var evidence = new BorinReadOnlyEvidenceSnapshot(
        [new BorinReadOnlyEvidenceItem("Apps/Source/Reforged.App/Example.cs", 42, "source", "verified-root-cause-marker", 1)],
        1, 1, @"C:\private\repository", "evidence ready");
    var context = BrainContext.ForBorin(
        "Diagnose the verified defect", new ForgeConversationState(), null,
        "Autonomous engineering job draft", evidence: evidence);
    var package = new BorinContextBuilder().Build(context);
    True(package.UserMessage.Contains("verified-root-cause-marker", StringComparison.Ordinal), "Exact developer evidence was omitted.");
    False(package.UserMessage.Contains(@"C:\private\repository", StringComparison.OrdinalIgnoreCase), "Evidence repository root leaked.");
    True(package.SystemMessage.Contains("structured speech field is visible dialogue only", StringComparison.Ordinal), "Visible speech contract was omitted.");
    return Task.CompletedTask;
}

static async Task CancellationAsync()
{
    var handler = new CancelingHandler();
    var provider = new NvidiaNimProvider(new HttpClient(handler), new StubCredentialStore("nvapi-test-secret-value"));
    using var cancellation = new CancellationTokenSource();
    var pending = provider.ThinkAsync(Context(), cancellation.Token);
    cancellation.Cancel();
    await ThrowsAsync<OperationCanceledException>(async () => await pending);
}

static Task StructuredParserAsync()
{
    var valid = DecisionJson("Move", new string('a', 64), null, "north");
    True(BorinAiDecisionParser.TryParse(valid, out var decision, out _), "Valid decision was rejected.");
    Equal(BorinAiIntent.Move, decision?.Intent);
    False(BorinAiDecisionParser.TryParse(valid.TrimEnd('}') + ",\"command\":\"format c:\"}", out _, out var unknownError), "Unknown decision field was accepted.");
    True(unknownError.Contains("Unknown", StringComparison.Ordinal), "Unknown field rejection was not explained.");
    False(BorinAiDecisionParser.TryParse("""{"speech":null,"intent":"Move","target":"north","confidence":"high","reason":"test","stateFingerprint":"abc"}""", out _, out _), "String confidence was accepted.");
    return Task.CompletedTask;
}

static Task StaleDecisionAsync()
{
    var context = VerifiedPlayerContext(["north"]);
    var package = new BorinContextBuilder().Build(context);
    var decision = new BorinAiDecision(null, BorinAiIntent.Move, "north", .9, "Explore", "stale");
    var result = new BorinActionValidator().Validate(decision, package, context);
    False(result.Allowed, "Stale decision was accepted.");
    Equal("stale-state", result.Code);
    return Task.CompletedTask;
}

static Task StateChangedAsync()
{
    var requested = VerifiedPlayerContext(["north"]);
    var package = new BorinContextBuilder().Build(requested);
    var changedGame = requested.Conversation.GameState with
    {
        CurrentRoom = "Different Room",
        CurrentRoomKey = "room.test.2",
        CapturedAt = requested.Conversation.GameState.CapturedAt.AddSeconds(1)
    };
    var current = BrainContext.ForBorin("Explore", requested.Conversation.State, null, "MajorMUD player reasoning", gameState: changedGame);
    var result = new BorinActionValidator().Validate(
        new BorinAiDecision(null, BorinAiIntent.Move, "north", .9, "Explore", package.StateFingerprint), package, current);
    False(result.Allowed, "Action was accepted after the authoritative room changed.");
    Equal("stale-state", result.Code);
    return Task.CompletedTask;
}

static Task MovementValidationAsync()
{
    var context = VerifiedPlayerContext(["north", "west"]);
    var package = new BorinContextBuilder().Build(context);
    var validator = new BorinActionValidator();
    var permitted = validator.Validate(new BorinAiDecision(null, BorinAiIntent.Move, "n", .9, "Explore", package.StateFingerprint), package, context);
    True(permitted.Allowed, "Verified north exit was rejected.");
    var blocked = validator.Validate(new BorinAiDecision(null, BorinAiIntent.Move, "south", .9, "Explore", package.StateFingerprint), package, context);
    False(blocked.Allowed, "Unverified south exit was accepted.");
    Equal("target-not-present", blocked.Code);
    return Task.CompletedTask;
}

static Task DeveloperPermissionAsync()
{
    var state = new ForgeConversationState();
    state.Configuration.PermissionMode = ForgePermissionMode.Observe;
    var context = BrainContext.ForBorin("Apply this", state, null, "Developer code repair");
    var package = new BorinContextBuilder().Build(context);
    var result = new BorinActionValidator().Validate(
        new BorinAiDecision(null, BorinAiIntent.ApplyPatch, "fix.patch", .95, "Repair", package.StateFingerprint), package, context);
    False(result.Allowed, "ApplyPatch was accepted in Observe mode.");
    Equal("permission-denied", result.Code);
    return Task.CompletedTask;
}

static Task DuplicateActionAsync()
{
    var context = VerifiedPlayerContext(["north"]);
    var package = new BorinContextBuilder().Build(context);
    var validator = new BorinActionValidator();
    var decision = new BorinAiDecision(null, BorinAiIntent.Move, "north", .9, "Explore", package.StateFingerprint);
    True(validator.Validate(decision, package, context).Allowed, "First action was rejected.");
    var duplicate = validator.Validate(decision, package, context);
    False(duplicate.Allowed, "Duplicate action was accepted.");
    Equal("duplicate-action", duplicate.Code);
    return Task.CompletedTask;
}

static async Task RateBudgetAsync()
{
    var budget = new SlidingWindowAiRateBudget(new AiRateBudgetOptions
    {
        NormalRequestsPerWindow = 2,
        HardRequestsPerWindow = 3,
        Window = TimeSpan.FromMilliseconds(120),
        MaximumQueueWait = TimeSpan.FromSeconds(2)
    });
    await budget.AcquireAsync();
    await budget.AcquireAsync();
    await budget.AcquireAsync(AiRatePriority.InteractiveBurst);
    Equal(3, budget.Snapshot.RequestsInWindow);
    Equal(0, budget.Snapshot.RemainingHardRequests);
    var started = System.Diagnostics.Stopwatch.StartNew();
    await budget.AcquireAsync();
    True(started.Elapsed >= TimeSpan.FromMilliseconds(70), "Normal request was not paced by the shared window.");
}

static async Task RateBudgetCancellationAsync()
{
    var budget = new SlidingWindowAiRateBudget(new AiRateBudgetOptions
    {
        NormalRequestsPerWindow = 1,
        HardRequestsPerWindow = 1,
        Window = TimeSpan.FromMinutes(1),
        MaximumQueueWait = TimeSpan.FromMinutes(2)
    });
    await budget.AcquireAsync();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
    await ThrowsAsync<OperationCanceledException>(() => budget.AcquireAsync(cancellationToken: cancellation.Token));
}

static async Task HybridRoutingAsync()
{
    var local = new StubAiProvider("local", _ => new AiResponse("local", "local", "local"));
    var cloud = new StubAiProvider("cloud", _ => new AiResponse("cloud", "NVIDIA NIM", "nemotron"));
    var settings = new BorinAiRoutingSettings { Mode = BorinBrainMode.Hybrid };
    var router = new HybridAiProviderRouter(local, cloud, settings);
    Equal("local", (await router.ThinkAsync(BrainContext.ForBorin("look", new ForgeConversationState(), null, "Routine gameplay"))).Text);
    Equal("cloud", (await router.ThinkAsync(BrainContext.ForBorin("Diagnose the root cause", new ForgeConversationState(), null, "Developer engineering"))).Text);
    Equal(1, local.CallCount);
    Equal(1, cloud.CallCount);
}

static async Task HybridFallbackAsync()
{
    var local = new StubAiProvider("local", _ => new AiResponse("safe local", "local", "local"));
    var cloud = new StubAiProvider("cloud", _ => throw new AiProviderException(AiProviderFailureKind.RateLimited, "429"));
    var router = new HybridAiProviderRouter(local, cloud, new BorinAiRoutingSettings { Mode = BorinBrainMode.NvidiaOnly, FallBackToLocal = true });
    var response = await router.ThinkAsync(Context("analyze this"));
    Equal("safe local", response.Text);
    True(response.UsedFallback, "Fallback was not marked.");
    Equal("NVIDIA RateLimited", response.FallbackReason);
}

static Task CapabilityCatalogAsync()
{
    var configuration = new ForgeConfiguration { PermissionMode = ForgePermissionMode.Observe };
    var catalog = new BorinCapabilityCatalog();
    var capabilities = catalog.Resolve(BorinRole.Player | BorinRole.Sysop | BorinRole.Developer, configuration);
    True(capabilities.Any(item => item.Capability.ToolId == "game.move" && item.Available), "Player movement capability missing.");
    True(capabilities.Any(item => item.Capability.ToolId == "world.get_room" && item.Available), "Sysop room inspection capability missing.");
    True(capabilities.Any(item => item.Capability.ToolId == "dev.search_code" && item.Available), "Developer search capability missing.");
    True(capabilities.Any(item => item.Capability.ToolId == "dev.apply_patch" && !item.Available), "Patch application ignored Observe permission.");
    True(capabilities.Any(item => item.Capability.ToolId == "editor.apply_test_change" && !item.Available), "Test Realm apply ignored approval permission.");
    False(capabilities.Any(item => item.Capability.ProductionMutation && item.Available), "Production mutation was exposed.");
    return Task.CompletedTask;
}

static async Task RepositoryContainmentAsync()
{
    var root = FindRepositoryRootForTests();
    var reader = new BorinRepositoryReadTools();
    var text = await reader.ReadFileAsync(root, "Apps/Source/Reforged.App/Brains/AiProviderContracts.cs");
    True(text.Contains("interface IAiProvider", StringComparison.Ordinal), "Approved source file was not read.");
    await ThrowsAsync<InvalidOperationException>(() => reader.ReadFileAsync(root, "../outside.txt"));
    await ThrowsAsync<InvalidOperationException>(() => reader.ReadFileAsync(root, Path.Combine(root, "AGENTS.md")));
    await ThrowsAsync<InvalidOperationException>(() => reader.ReadFileAsync(root, "Apps/Source/Reforged.App/Assets/Borin/BorinIronbeard.png"));
}

static async Task PlayerDispatchAsync()
{
    var context = VerifiedPlayerContext(["north"]);
    var package = new BorinContextBuilder().Build(context);
    var gateway = new StubPlayerGateway();
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), gateway);
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.Move, "n", .9, "Explore", package.StateFingerprint), package, context);
    True(result.Succeeded, "Verified movement was not dispatched.");
    Equal("NORTH", gateway.LastCommand);
    Equal(1, gateway.SendCount);
}

static async Task ValidationBeforeDispatchAsync()
{
    var context = VerifiedPlayerContext(["north"]);
    var package = new BorinContextBuilder().Build(context);
    var gateway = new StubPlayerGateway();
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), gateway);
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.Move, "north", .9, "Explore", "wrong-state"), package, context);
    False(result.Succeeded, "Stale movement was dispatched.");
    Equal("stale-state", result.Code);
    Equal(0, gateway.SendCount);
}

static async Task DeveloperDispatchAsync()
{
    var state = new ForgeConversationState();
    state.Configuration.RepositoryPath = FindRepositoryRootForTests();
    var context = BrainContext.ForBorin("Read the provider", state, null, "Developer repository analysis");
    var package = new BorinContextBuilder().Build(context);
    var gateway = new StubDeveloperGateway();
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), developer: gateway);
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.ReadFile, "safe.cs", .9, "Inspect", package.StateFingerprint), package, context);
    True(result.Succeeded, "Developer read was not dispatched.");
    Equal("safe.cs", gateway.LastTarget);
    Equal(1, gateway.ReadCount);
}

static async Task UnboundSysopAsync()
{
    var context = BrainContext.ForBorin("Inspect room", new ForgeConversationState(), null, "Sysop world inspection");
    var package = new BorinContextBuilder().Build(context);
    var executor = new BorinControlledToolExecutor(new BorinActionValidator());
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.InspectRoom, "room.1", .9, "Inspect", package.StateFingerprint), package, context);
    False(result.Succeeded, "Missing Sysop gateway did not fail closed.");
    Equal("tool-not-bound", result.Code);
}

static async Task SysopTestRealmAsync()
{
    var context = BrainContext.ForBorin("Monitor Test Realm", new ForgeConversationState(), null, "Sysop world monitoring");
    var package = new BorinContextBuilder().Build(context);
    var service = new StubWorldsmithService();
    var workspace = new WorldsmithWorkspace { TargetTestRealm = "Test Realm", ApprovalRequired = true };
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), sysop: new BorinWorldsmithSysopToolGateway(service, workspace));
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.MonitorWorld, null, .9, "Inspect", package.StateFingerprint), package, context);
    False(result.Succeeded, "Withheld Worldsmith inspection was dispatched.");
    Equal("worldsmith-withheld", result.Code);
    Equal(0, service.InspectCount);
}

static async Task SysopLiveRealmBlockedAsync()
{
    var context = BrainContext.ForBorin("Monitor live realm", new ForgeConversationState(), null, "Sysop world monitoring");
    var package = new BorinContextBuilder().Build(context);
    var service = new StubWorldsmithService();
    var workspace = new WorldsmithWorkspace { TargetTestRealm = "Live Realm", ApprovalRequired = true };
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), sysop: new BorinWorldsmithSysopToolGateway(service, workspace));
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.MonitorWorld, null, .9, "Inspect", package.StateFingerprint), package, context);
    False(result.Succeeded, "Live Realm Sysop target was accepted.");
    Equal("worldsmith-withheld", result.Code);
    Equal(0, service.InspectCount);
}

static async Task FullPlayerCommandAsync()
{
    var context = VerifiedPlayerContext(["north"]);
    var package = new BorinContextBuilder().Build(context);
    var gateway = new StubPlayerGateway();
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), gateway);
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.GameCommand, "GIVE sword Bob", .9, "Help another player", package.StateFingerprint), package, context);
    True(result.Succeeded, "Ordinary MajorMUD command was not executed autonomously.");
    Equal("GIVE sword Bob", gateway.LastCommand);
}

static async Task ProtectedPlayerCommandAsync()
{
    var context = VerifiedPlayerContext(["north"]);
    var package = new BorinContextBuilder().Build(context);
    var gateway = new StubPlayerGateway();
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), gateway);
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.GameCommand, "SYSOP LIST USERS", .9, "Escalate", package.StateFingerprint), package, context);
    False(result.Succeeded, "SYSOP command crossed the Player authority boundary.");
    Equal("protected-command", result.Code);
    Equal(0, gateway.SendCount);
}

static async Task WorkspacePatchAuthorityAsync()
{
    var state = new ForgeConversationState();
    state.Configuration.RepositoryPath = FindRepositoryRootForTests();
    state.Configuration.PermissionMode = ForgePermissionMode.Draft;
    state.Configuration.AllowWorkspaceEdits = true;
    var context = BrainContext.ForBorin("Apply patch in workspace", state, null, "Developer engineering patch");
    var package = new BorinContextBuilder().Build(context);
    var gateway = new StubDeveloperGateway();
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), developer: gateway);
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.ApplyPatch, "proposal", .9, "Test repair", package.StateFingerprint), package, context);
    True(result.Succeeded, "Isolated workspace patch application required main-repository approval.");
    Equal(1, gateway.ApplyCount);
}

static async Task MainIncorporationHeldAsync()
{
    var state = new ForgeConversationState();
    state.Configuration.RepositoryPath = FindRepositoryRootForTests();
    state.Configuration.PermissionMode = ForgePermissionMode.ApplyWithApproval;
    var context = BrainContext.ForBorin("Incorporate patch", state, null, "Developer engineering patch");
    var package = new BorinContextBuilder().Build(context);
    var gateway = new StubDeveloperGateway();
    var executor = new BorinControlledToolExecutor(new BorinActionValidator(), developer: gateway);
    var result = await executor.ExecuteAsync(
        new BorinAiDecision(null, BorinAiIntent.IncorporatePatch, "proposal", .99, "Promote repair", package.StateFingerprint), package, context);
    False(result.Succeeded, "Model incorporated a patch into the main repository.");
    Equal("human-approval-required", result.Code);
    Equal(0, gateway.ApplyCount);
}

static async Task VerifiedIncorporationAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "borin-incorporation-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(Path.Combine(root, "Apps", "Source", "Reforged.App"));
    Directory.CreateDirectory(Path.Combine(root, "Core", "Source", "Reforged.Core"));
    File.WriteAllText(Path.Combine(root, "Apps", "Source", "Reforged.App", "Reforged.App.csproj"), "<Project />");
    File.WriteAllText(Path.Combine(root, "Core", "Source", "Reforged.Core", "Reforged.Core.csproj"), "<Project />");
    try
    {
        var workspace = new StubEngineeringWorkspace();
        var orchestrator = new BorinEngineeringOrchestrator(workspace);
        await orchestrator.BeginAsync(root, "Repair verified example");
        await orchestrator.RecordDraftAsync(root, """
```diff
diff --git a/Example.cs b/Example.cs
--- a/Example.cs
+++ b/Example.cs
@@ -1 +1 @@
-old
+new
```
""");
        var premature = await orchestrator.IncorporateAsync(root);
        False(premature.Succeeded, "Unverified draft was incorporated.");
        await orchestrator.ApproveAsync(root);
        var verified = await orchestrator.BuildAndTestAsync(root);
        True(verified.Succeeded, "Fake isolated verification did not pass.");
        var incorporated = await orchestrator.IncorporateAsync(root);
        True(incorporated.Succeeded, "Verified patch was not incorporated through the explicit local action.");
        Equal(BorinEngineeringJobStages.Incorporated, incorporated.Job.Stage);
        Equal(1, workspace.IncorporateCount);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static Task SharedCredentialSlotAsync()
{
    var shared = new WindowsNvidiaNimCredentialStore(NvidiaNimKeySlot.Shared);
    var lightning = new WindowsNvidiaNimCredentialStore(NvidiaNimKeySlot.Lightning);
    var ultra = new WindowsNvidiaNimCredentialStore(NvidiaNimKeySlot.Ultra);
    Equal(NvidiaNimKeySlot.Lightning, lightning.Slot);
    Equal(NvidiaNimKeySlot.Ultra, ultra.Slot);
    Equal(shared.CredentialTargetName, lightning.CredentialTargetName);
    False(shared.CredentialTargetName.Equals(ultra.CredentialTargetName, StringComparison.OrdinalIgnoreCase), "Legacy Ultra target was not retained for safe migration.");
    return Task.CompletedTask;
}

static async Task TierSelectionAsync()
{
    var local = new StubAiProvider("local", _ => new("local", "local", "local"));
    var lightning = new StubAiProvider("lightning", _ => new("lightning", "lightning", "lightning"));
    var ultra = new StubAiProvider("ultra", _ => new("ultra", "ultra", "ultra"));
    var router = new TieredBorinAiProviderRouter(local, lightning, ultra, new BorinAiRoutingSettings());
    Equal("local", (await router.ThinkAsync(BrainContext.ForBorin("look", new ForgeConversationState(), null, "Routine gameplay"))).Text);
    Equal("lightning", (await router.ThinkAsync(BrainContext.ForBorin("How do ye like the forge?", new ForgeConversationState(), null, "Conversation"))).Text);
    Equal("lightning", (await router.ThinkAsync(BrainContext.ForBorin("Answer only from the supplied command evidence.", new ForgeConversationState(), null, "Borin Academy evidence-based training"))).Text);
    Equal("ultra", (await router.ThinkAsync(BrainContext.ForBorin("Trace the supplied relationships.", new ForgeConversationState(), null, "Borin Academy deep reasoning with conflicting or multi-hop evidence"))).Text);
    Equal("ultra", (await router.ThinkAsync(DeepDeveloperContext())).Text);
}

static async Task LowConfidenceEscalationAsync()
{
    BrainContext? lightningContext = null;
    BrainContext? ultraContext = null;
    var local = new StubAiProvider("local", _ => new("local", "local", "local"));
    var lightning = new StubAiProvider("lightning", context => { lightningContext = context; return new("usable", "lightning", "lightning", Decision: new BorinAiDecision("hmm", BorinAiIntent.Speak, null, .2, "uncertain", new string('a', 64))); });
    var ultra = new StubAiProvider("ultra", context => { ultraContext = context; return new("deep", "ultra", "ultra"); });
    var router = new TieredBorinAiProviderRouter(local, lightning, ultra, new BorinAiRoutingSettings());
    var context = BrainContext.ForBorin("Discuss an ordinary issue", new ForgeConversationState(), null, "Conversation");
    Equal("deep", (await router.ThinkAsync(context)).Text);
    True(ReferenceEquals(context, lightningContext) && ReferenceEquals(context, ultraContext), "Escalation did not preserve the exact task context.");
    Equal(1, ultra.CallCount);
}

static async Task UltraFallbackAsync()
{
    BrainContext? fallbackContext = null;
    var local = new StubAiProvider("local", _ => new("local", "local", "local"));
    var lightning = new StubAiProvider("lightning", context => { fallbackContext = context; return new("lightning fallback", "lightning", "lightning"); });
    var ultra = new StubAiProvider("ultra", _ => throw new AiProviderException(AiProviderFailureKind.Authentication, "ultra unavailable"));
    var router = new TieredBorinAiProviderRouter(local, lightning, ultra, new BorinAiRoutingSettings());
    var context = DeepDeveloperContext();
    var response = await router.ThinkAsync(context);
    Equal("lightning fallback", response.Text);
    True(response.UsedFallback, "Ultra fallback was not marked.");
    True(ReferenceEquals(context, fallbackContext), "Ultra fallback lost the task context.");
}

static async Task LightningFailureIsolationAsync()
{
    var local = new StubAiProvider("local", _ => new("safe local", "local", "local"));
    var lightning = new StubAiProvider("lightning", _ => throw new AiProviderException(AiProviderFailureKind.RateLimited, "lightning limited"));
    var ultra = new StubAiProvider("ultra", _ => new("ultra", "ultra", "ultra"));
    var router = new TieredBorinAiProviderRouter(local, lightning, ultra, new BorinAiRoutingSettings());
    Equal("safe local", (await router.ThinkAsync(BrainContext.ForBorin("Hello", new ForgeConversationState(), null, "Conversation"))).Text);
    Equal(0, ultra.CallCount);
}

static async Task IndependentRateAccountingAsync()
{
    var lightning = new SlidingWindowAiRateBudget(new AiRateBudgetOptions { NormalRequestsPerWindow = 2, HardRequestsPerWindow = 2 });
    var ultra = new SlidingWindowAiRateBudget(new AiRateBudgetOptions { NormalRequestsPerWindow = 1, HardRequestsPerWindow = 1 });
    await lightning.AcquireAsync(); await lightning.AcquireAsync(); await ultra.AcquireAsync();
    Equal(2, lightning.Snapshot.RequestsInWindow);
    Equal(1, ultra.Snapshot.RequestsInWindow);
}

static async Task CriticalSuperVerificationAsync()
{
    var decision = new BorinAiDecision("verified", BorinAiIntent.ProposePatch, "fix.patch", .9, "evidence", new string('a', 64));
    var local = new StubAiProvider("local", _ => new("local", "local", "local"));
    var lightning = new StubAiProvider("lightning", _ => new("lightning", "lightning", "lightning"));
    var ultra = new StubAiProvider("ultra", _ => new("primary", "ultra", "ultra", Decision: decision));
    var super = new StubAiProvider("super", _ => new("verification", "super", "super", Decision: decision));
    var router = new TieredBorinAiProviderRouter(local, lightning, ultra, new BorinAiRoutingSettings(), super: super);
    var result = await router.ThinkAsync(DeepDeveloperContext());
    Equal("primary", result.Text);
    Equal(1, super.CallCount);
    True(router.LastRoute.Reason.Contains("verified by Super", StringComparison.OrdinalIgnoreCase), "Verification was not recorded.");
}

static async Task SuperDisagreementAsync()
{
    var primary = new BorinAiDecision("patch", BorinAiIntent.ProposePatch, "fix.patch", .95, "evidence", new string('a', 64));
    var dissent = primary with { Intent = BorinAiIntent.SearchCode, Target = "different" };
    var local = new StubAiProvider("local", _ => new("local", "local", "local"));
    var lightning = new StubAiProvider("lightning", _ => new("lightning", "lightning", "lightning"));
    var ultra = new StubAiProvider("ultra", _ => new("primary", "ultra", "ultra", Decision: primary));
    var super = new StubAiProvider("super", _ => new("dissent", "super", "super", Decision: dissent));
    var router = new TieredBorinAiProviderRouter(local, lightning, ultra, new BorinAiRoutingSettings(), super: super);
    var result = await router.ThinkAsync(DeepDeveloperContext());
    True(result.UsedFallback, "Verifier disagreement was not surfaced.");
    True(result.Decision?.Confidence <= .5, "Disagreement did not lower action confidence.");
}

static Task EscalationConfigurationPersistenceAsync()
{
    var json = JsonSerializer.Serialize(new ForgeConfiguration { AutomaticallyEscalateDifficultTasks = false });
    var restored = JsonSerializer.Deserialize<ForgeConfiguration>(json)!;
    False(restored.AutomaticallyEscalateDifficultTasks, "Escalation setting was not persisted.");
    return Task.CompletedTask;
}

static BrainContext DeepDeveloperContext()
{
    var items = Enumerable.Range(1, 4).Select(index => new BorinReadOnlyEvidenceItem($"Apps/Subsystem{index}/File{index}.cs", index, "source", $"evidence {index}", 1)).ToArray();
    var evidence = new BorinReadOnlyEvidenceSnapshot(items, 4, 4, "private", "cross-file evidence");
    return BrainContext.ForBorin("Choose a repair across the verified files", new ForgeConversationState(), null, "Developer engineering", evidence: evidence);
}

static string FindRepositoryRootForTests()
{
    var current = new DirectoryInfo(Environment.CurrentDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "MajorMud.Reforged.sln"))) return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Test repository root was not found.");
}

static BrainContext VerifiedPlayerContext(IReadOnlyList<string> exits)
{
    var game = BorinGameStateSnapshot.Empty with
    {
        Phase = BorinGamePhase.InGame,
        ConnectionState = "Connected",
        LoginState = BorinLoginState.Ready,
        IsConnected = true,
        IsLoggedIntoMajorMud = true,
        HasVerifiedLiveState = true,
        HasVerifiedPrompt = true,
        HasVerifiedRoom = true,
        HasVerifiedExits = true,
        CurrentRoom = "Test Room",
        CurrentRoomKey = "room.test.1",
        CurrentExits = exits,
        CapturedAt = DateTimeOffset.Parse("2026-08-20T12:00:00Z")
    };
    return BrainContext.ForBorin("Explore", new ForgeConversationState(), null, "MajorMUD player reasoning", gameState: game);
}

static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

static HttpResponseMessage Completion(string decisionJson, string id = "req-test", int promptTokens = 1, int completionTokens = 1)
{
    var body = JsonSerializer.Serialize(new
    {
        id,
        choices = new[] { new { message = new { content = decisionJson } } },
        usage = new { prompt_tokens = promptTokens, completion_tokens = completionTokens }
    });
    return Json(HttpStatusCode.OK, body);
}

static string DecisionJson(string intent, string fingerprint, string? speech = null, string? target = null) =>
    JsonSerializer.Serialize(new
    {
        speech,
        intent,
        target,
        confidence = 0.9,
        reason = "Test decision",
        stateFingerprint = fingerprint
    });

static async Task<T> ThrowsAsync<T>(Func<Task> operation) where T : Exception
{
    try { await operation(); }
    catch (T exception) { return exception; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static void False(bool value, string message) => True(!value, message);

static Task GuardianCapabilityRoutingAsync()
{
    Equal(BorinCapabilityOperation.RealmMonitoring,
        BorinCapabilityRequestRouter.Route("Borin, check the realm health").Capability);
    var analysis = BorinCapabilityRequestRouter.Route("Analyze the economy and show current trends");
    Equal(BorinCapabilityOperation.Analysis, analysis.Capability);
    Equal("Economy", analysis.Argument);
    var evidence = BorinCapabilityRequestRouter.Route("Search Librarium evidence for Sys Goto persistence");
    Equal(BorinCapabilityOperation.EvidenceSearch, evidence.Capability);
    Equal("Sys Goto persistence", evidence.Argument);
    Equal(BorinCapabilityOperation.WorldsmithWithheld,
        BorinCapabilityRequestRouter.Route("Use Worldsmith to create a room").Capability);
    Equal(BorinCapabilityOperation.None,
        BorinCapabilityRequestRouter.Route("How are you doing today, Borin?").Capability);
    Equal(BorinCapabilityOperation.None,
        BorinCapabilityRequestRouter.Route("Look at your inventory").Capability);
    return Task.CompletedTask;
}

static async Task BorinCapabilityMigrationAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "BorinCapabilityMigrationTests", Guid.NewGuid().ToString("N"));
    var retired = Path.Combine(root, "GuardianHall");
    Directory.CreateDirectory(retired);
    try
    {
        await File.WriteAllTextAsync(Path.Combine(retired, "guardian-hall-state.json"),
            """{"SchemaVersion":7,"Analyst":{"AnalysisTarget":"Economy"}}""");
        var store = new BorinCapabilityStateStore(root);
        var state = await store.LoadAsync();
        True(state is not null, "Legacy capability state was not loaded.");
        Equal("Economy", state!.Analyst.AnalysisTarget);
        True(File.Exists(Path.Combine(root, "Borin", "Capabilities", "borin-capabilities-state.json")),
            "Migrated Borin capability state was not created.");
        True(File.Exists(Path.Combine(retired, "guardian-hall-state.json.retired-backup")),
            "Legacy backup was not preserved.");
        True(BorinCapabilityProfileCatalog.CreateDefaults().All(profile => profile.Name == "Borin Ironbeard"),
            "A retired Guardian identity remains in the capability profiles.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

sealed class StubCredentialStore(string? key) : INvidiaNimCredentialStore
{
    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) => Task.FromResult(!string.IsNullOrWhiteSpace(key));
    public Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string?> ReadApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(key);
    public Task ClearApiKeyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult(respond(request));
    }
}

sealed class CancelingHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Unreachable");
    }
}

sealed class StubAiProvider(string name, Func<BrainContext, AiResponse> respond) : IAiProvider
{
    public string Name { get; } = name;
    public int CallCount { get; private set; }
    public Task<AiResponse> ThinkAsync(BrainContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        return Task.FromResult(respond(context));
    }
}

sealed class StubPlayerGateway : IBorinPlayerToolGateway
{
    public string LastCommand { get; private set; } = string.Empty;
    public int SendCount { get; private set; }
    public Task ConnectAndLoginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<BorinCommandGatewayResult> SendAutonomousCommandAsync(string command, string reason, CancellationToken cancellationToken = default)
    {
        SendCount++;
        LastCommand = command;
        return Task.FromResult(new BorinCommandGatewayResult(true, true, command, reason, DateTimeOffset.Now));
    }
}

sealed class StubDeveloperGateway : IBorinDeveloperToolGateway
{
    public string LastTarget { get; private set; } = string.Empty;
    public int ReadCount { get; private set; }
    public int ApplyCount { get; private set; }
    public Task<string> SearchCodeAsync(string query, ForgeConversationState state, CancellationToken cancellationToken = default) => Task.FromResult("search");
    public Task<string> ReadFileAsync(string relativePath, ForgeConversationState state, CancellationToken cancellationToken = default) { LastTarget = relativePath; ReadCount++; return Task.FromResult("source"); }
    public Task<string> ReadDirectoryAsync(string relativePath, ForgeConversationState state, CancellationToken cancellationToken = default) => Task.FromResult("directory");
    public Task<string> ReadLatestLogsAsync(ForgeConversationState state, CancellationToken cancellationToken = default) => Task.FromResult("logs");
    public Task<ForgeCommandResult> BuildAsync(ForgeConversationState state, CancellationToken cancellationToken = default) => Task.FromResult(new ForgeCommandResult("build", 0, "pass", DateTimeOffset.Now));
    public Task<ForgeCommandResult> TestAsync(ForgeConversationState state, CancellationToken cancellationToken = default) => Task.FromResult(new ForgeCommandResult("test", 0, "pass", DateTimeOffset.Now));
    public Task<string> PreviewPatchAsync(ForgeConversationState state, CancellationToken cancellationToken = default) => Task.FromResult("diff");
    public Task<string> ApplyPatchToWorkspaceAsync(ForgeConversationState state, CancellationToken cancellationToken = default) { ApplyCount++; return Task.FromResult("applied"); }
}

sealed class StubEngineeringWorkspace : IBorinEngineeringWorkspace
{
    public int IncorporateCount { get; private set; }
    public Task<BorinEngineeringWorkspaceStatus> CreateAsync(string repositoryRoot, bool recreate = false, CancellationToken token = default) =>
        Task.FromResult(new BorinEngineeringWorkspaceStatus("Ready", Path.Combine(repositoryRoot, "workspace"), true, 0, "ready"));
    public Task<BorinEngineeringWorkspaceStatus> GetStatusAsync(string repositoryRoot, CancellationToken token = default) =>
        CreateAsync(repositoryRoot, false, token);
    public Task<BorinEngineeringOperationResult> ApplyDraftAsync(string repositoryRoot, string borinReply, CancellationToken token = default) =>
        Task.FromResult(new BorinEngineeringOperationResult(true, "applied"));
    public Task<BorinEngineeringOperationResult> ShowDiffAsync(string repositoryRoot, CancellationToken token = default) =>
        Task.FromResult(new BorinEngineeringOperationResult(true, "diff", "diff --git a/Example.cs b/Example.cs"));
    public Task<BorinEngineeringOperationResult> BuildAsync(string repositoryRoot, CancellationToken token = default) =>
        Task.FromResult(new BorinEngineeringOperationResult(true, "build passed"));
    public Task<BorinEngineeringOperationResult> TestAsync(string repositoryRoot, CancellationToken token = default) =>
        Task.FromResult(new BorinEngineeringOperationResult(true, "tests passed"));
    public Task<BorinEngineeringOperationResult> CreateUpdateZipAsync(string repositoryRoot, CancellationToken token = default) =>
        Task.FromResult(new BorinEngineeringOperationResult(true, "packaged"));
    public Task<BorinEngineeringOperationResult> IncorporateVerifiedAsync(string repositoryRoot, IReadOnlyList<string> verifiedPaths, CancellationToken token = default)
    {
        IncorporateCount++;
        return Task.FromResult(new BorinEngineeringOperationResult(true, "incorporated", ArtifactPath: "approved.patch"));
    }
}

sealed class StubWorldsmithService : IWorldsmithRuntimeService
{
    public int InspectCount { get; private set; }
    public Task<WorldsmithRuntimeSnapshot> InspectAsync(WorldsmithWorkspace workspace, CancellationToken token = default)
    {
        InspectCount++;
        return Task.FromResult(new WorldsmithRuntimeSnapshot(DateTime.UtcNow, true, 2, 10, "test", "safe"));
    }
    public Task<string> GenerateProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default) => Task.FromResult("proposal.json");
    public Task<string> SimulateAsync(WorldsmithWorkspace workspace, CancellationToken token = default) => Task.FromResult("simulation.md");
    public Task<string> ApproveProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default) => throw new InvalidOperationException("AI gateway must never approve.");
    public Task<WorldsmithApplyResult> ApplyApprovedProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default) => Task.FromResult(new WorldsmithApplyResult("applied", "receipt", "backup"));
}
