using System.IO;
using System.Text;
using Reforged.App.Forge;

namespace Reforged.App.Brains;

public sealed record BorinSelfKnowledgeSnapshot(
    string Surface,
    string BrainRuntimeState,
    bool BrainPathAvailable,
    bool BrainModelReady,
    bool RepositoryConfigured,
    bool RepositoryExists,
    bool RepositorySnapshotAvailable,
    int CurrentEvidenceItems,
    bool LiveGameStateVerified,
    BorinGamePhase GamePhase,
    bool Connected,
    bool LoggedIntoMajorMud,
    bool CanDraftThisTurn,
    bool ControlledWorkspaceEditsConfigured,
    bool ControlledBuildsConfigured,
    bool ControlledTestsConfigured,
    bool ControlledGitConfigured,
    bool ControlledProgramLaunchConfigured,
    bool InternetResearchConfigured,
    bool SelfKnowledgeQuestion,
    string PromptText,
    string ExternalPromptText)
{
    public static BorinSelfKnowledgeSnapshot Empty { get; } = new(
        "unknown surface",
        "unknown",
        false,
        false,
        false,
        false,
        false,
        0,
        false,
        BorinGamePhase.Unknown,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        "HONEST SELF-KNOWLEDGE CONTRACT\n- Current capabilities were not captured for this turn. State uncertainty plainly and do not claim access or completed actions.",
        "HONEST SELF-KNOWLEDGE CONTRACT\n- Current capabilities were not captured for this turn. State uncertainty plainly and do not claim access or completed actions.");

    public string Summary =>
        $"{Surface} • brain {BrainRuntimeState} • repository {(RepositoryExists ? "available" : "not verified")} • " +
        $"game {(LiveGameStateVerified ? GamePhase.ToString() : "not verified")}";

    public string ExternalSummary =>
        $"external MajorMUD self-knowledge • game {(LiveGameStateVerified ? GamePhase.ToString() : "not verified")}";
}

public interface IBorinSelfKnowledgeStore
{
    Task<BorinSelfKnowledgeSnapshot> PrepareTurnAsync(
        BrainContext context,
        LocalGgufBrainRuntimeStatus runtimeStatus,
        CancellationToken token = default);
}

/// <summary>
/// Builds a fresh, non-persistent capability and evidence map for every turn.
/// It never grants permissions. It only explains what the conversation brain can
/// currently observe, what separate controlled services are configured to do,
/// and which claims remain reports or memories rather than verified facts.
/// This pass creates no memory file; every snapshot is rebuilt from current state.
/// </summary>
public sealed class BorinSelfKnowledgeStore : IBorinSelfKnowledgeStore
{
    private static readonly string[] SelfKnowledgeTerms =
    [
        "can you", "are you able", "do you have access", "what can you", "what do you see",
        "are you connected", "are you logged", "where are you", "can you see", "can you read",
        "can you edit", "can you change", "can you build", "can you test", "can you run",
        "can you install", "can you deploy", "can you commit", "can you push", "can you browse",
        "did you edit", "did you change", "did you build", "did you test", "did you install",
        "did you deploy", "did you run", "did you send", "did you move", "did you fight",
        "have you edited", "have you built", "have you tested", "have you installed",
        "what did you do", "what have you done", "what are your limits", "your capabilities",
    ];

    public Task<BorinSelfKnowledgeSnapshot> PrepareTurnAsync(
        BrainContext context,
        LocalGgufBrainRuntimeStatus runtimeStatus,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runtimeStatus);
        token.ThrowIfCancellationRequested();

        var state = context.Conversation.State;
        var configuration = state.Configuration;
        var game = context.Conversation.GameState;
        var evidence = context.Conversation.Evidence;
        var external = IsExternalConversation(context);
        var surface = ResolveSurface(context);
        var repositoryPath = ResolveRepositoryPath(context);
        var repositoryConfigured = repositoryPath.Length > 0;
        var repositoryExists = repositoryConfigured && Directory.Exists(repositoryPath);
        var repositorySnapshotAvailable = context.Conversation.RepositorySnapshot is not null;
        var evidenceItems = evidence.Items?.Count ?? 0;
        var engineeringDraft = IsEngineeringDraft(context.Purpose);
        var selfKnowledgeQuestion = LooksLikeSelfKnowledgeQuestion(context.Request);
        var brainReady = runtimeStatus.IsReady ||
                         runtimeStatus.State.Equals("Thinking", StringComparison.OrdinalIgnoreCase) ||
                         runtimeStatus.State.Equals("Retrying", StringComparison.OrdinalIgnoreCase) ||
                         runtimeStatus.State.Equals("Loading", StringComparison.OrdinalIgnoreCase);
        var brainPathAvailable = !runtimeStatus.State.Equals("Faulted", StringComparison.OrdinalIgnoreCase) &&
                                 !runtimeStatus.State.Equals("Disposed", StringComparison.OrdinalIgnoreCase);

        var controlledEdits = configuration.PermissionMode == ForgePermissionMode.ApplyWithApproval &&
                              configuration.AllowWorkspaceEdits;
        var controlledBuilds = configuration.AllowBuildCommands;
        var controlledTests = configuration.AllowTestCommands;
        var controlledGit = configuration.AllowGitCommands;
        var controlledLaunch = configuration.AllowLocalProgramLaunch;
        var internetResearch = configuration.InternetMode != ForgeInternetMode.Offline;

        var privatePrompt = BuildPrivatePrompt(
            surface,
            runtimeStatus,
            brainPathAvailable,
            brainReady,
            repositoryConfigured,
            repositoryExists,
            repositorySnapshotAvailable,
            evidenceItems,
            game,
            engineeringDraft,
            controlledEdits,
            controlledBuilds,
            controlledTests,
            controlledGit,
            controlledLaunch,
            internetResearch,
            selfKnowledgeQuestion);
        var externalPrompt = BuildExternalPrompt(
            game,
            selfKnowledgeQuestion);

        return Task.FromResult(new BorinSelfKnowledgeSnapshot(
            surface,
            runtimeStatus.State,
            brainPathAvailable,
            brainReady,
            repositoryConfigured,
            repositoryExists,
            repositorySnapshotAvailable,
            evidenceItems,
            game.HasVerifiedLiveState,
            game.Phase,
            game.IsConnected,
            game.IsLoggedIntoMajorMud,
            engineeringDraft,
            controlledEdits,
            controlledBuilds,
            controlledTests,
            controlledGit,
            controlledLaunch,
            internetResearch,
            selfKnowledgeQuestion,
            privatePrompt,
            externalPrompt));
    }

    private static string BuildPrivatePrompt(
        string surface,
        LocalGgufBrainRuntimeStatus runtimeStatus,
        bool brainPathAvailable,
        bool brainReady,
        bool repositoryConfigured,
        bool repositoryExists,
        bool repositorySnapshotAvailable,
        int evidenceItems,
        BorinGameStateSnapshot game,
        bool engineeringDraft,
        bool controlledEdits,
        bool controlledBuilds,
        bool controlledTests,
        bool controlledGit,
        bool controlledLaunch,
        bool internetResearch,
        bool selfKnowledgeQuestion)
    {
        var builder = new StringBuilder();
        builder.AppendLine("HONEST SELF-KNOWLEDGE AND CAPABILITY CONTRACT");
        builder.AppendLine($"- Current surface: {surface}.");
        builder.AppendLine($"- Local brain runtime: {runtimeStatus.State}; direct local path available: {brainPathAvailable}; model already loaded/active: {brainReady}. Detail: {Normalize(runtimeStatus.Detail, "no runtime detail", 180)}");
        builder.AppendLine("What is verified now:");
        builder.AppendLine($"- Repository configured / exists: {repositoryConfigured} / {repositoryExists}.");
        builder.AppendLine($"- Current repository snapshot supplied: {repositorySnapshotAvailable}; current read-only evidence items supplied: {evidenceItems}.");
        builder.AppendLine($"- Client connected / logged into MajorMUD / live realm state verified: {game.IsConnected} / {game.IsLoggedIntoMajorMud} / {game.HasVerifiedLiveState}.");
        builder.AppendLine($"- Current verified game phase: {game.Phase}. Current room may be stated only from the verified game-state section.");
        builder.AppendLine("What this conversation brain can do now:");
        builder.AppendLine("- Form one reply from the supplied live snapshot, current-turn evidence, verified learned observations, and clearly labeled memories or reports.");
        builder.AppendLine($"- Read and cite project text only when current-turn repository evidence is actually supplied. Evidence available this turn: {evidenceItems > 0}.");
        builder.AppendLine($"- Draft source text or a proposed diff only when the purpose explicitly authorizes a controlled engineering draft. Authorized for this turn: {engineeringDraft}.");
        builder.AppendLine("What this conversation brain cannot do by itself:");
        builder.AppendLine("- It cannot edit or delete files, run shell commands, build, test, install, deploy, restart, launch programs, commit, push, reveal credentials, or execute arbitrary game commands merely by saying it did.");
        builder.AppendLine("Separate controlled RealmGate services:");
        builder.AppendLine($"- Workspace edits with approval configured: {controlledEdits}; builds: {controlledBuilds}; tests: {controlledTests}; git: {controlledGit}; local program launch: {controlledLaunch}; internet research: {internetResearch}.");
        builder.AppendLine("- A configured service is not an action already performed. Claim completion only when the current turn contains the corresponding successful tool/orchestrator evidence.");
        builder.AppendLine("Knowledge authority ladder:");
        builder.AppendLine("1. Current verified Client game state and current-turn tool/orchestrator results.");
        builder.AppendLine("2. Current-turn read-only repository evidence with exact paths and lines.");
        builder.AppendLine("3. Persisted application records explicitly labeled as reports or prior results.");
        builder.AppendLine("4. Memories, learned observations, and player statements, which are context—not present-tense proof.");
        builder.AppendLine("5. Unknown: anything not supported above. Say that it is unknown instead of guessing.");
        builder.AppendLine("Action-claim rules:");
        builder.AppendLine("- Distinguish 'I can discuss or draft this' from 'a controlled service can perform this after approval' and from 'this was completed successfully.'");
        builder.AppendLine("- Never use a planned goal, generated reply, remembered discussion, or configured checkbox as evidence that an action happened.");
        builder.AppendLine("- When asked about connection, login, room, combat, inventory, equipment, or vitals, answer from the verified game-state snapshot already supplied; do not ask William for logs the Client already has.");
        builder.AppendLine("- When asked whether an operation completed, look for explicit current evidence. If none exists, say it is not verified.");
        if (selfKnowledgeQuestion)
            builder.AppendLine("- The current request is a direct self-knowledge/capability question. Answer it plainly first, then add only the minimum useful distinction or limitation.");
        return builder.ToString().TrimEnd();
    }

    private static string BuildExternalPrompt(
        BorinGameStateSnapshot game,
        bool selfKnowledgeQuestion)
    {
        var builder = new StringBuilder();
        builder.AppendLine("HONEST SELF-KNOWLEDGE CONTRACT FOR EXTERNAL MAJORMUD SPEECH");
        builder.AppendLine("- Borin may answer through the controlled conversation path. Do not expose the local model, runtime state, host process, or internal diagnostics.");
        builder.AppendLine($"- Connected / logged into MajorMUD / live realm state verified: {game.IsConnected} / {game.IsLoggedIntoMajorMud} / {game.HasVerifiedLiveState}; phase: {game.Phase}.");
        builder.AppendLine("- Current game facts come only from the verified game-state section. Old memory and another player's claim cannot override it.");
        builder.AppendLine("- Borin may converse, but this reply does not authorize movement, combat, purchases, equipment changes, account commands, file access, builds, tools, or any other action.");
        builder.AppendLine("- Do not reveal private Forge capabilities, repository details, host paths, tool settings, credentials, internal status text, or William's private project state.");
        builder.AppendLine("- Never claim an action happened because Borin suggested it, wanted it, remembered discussing it, or generated words about it.");
        builder.AppendLine("- If a fact is not verified in the live snapshot or verified gameplay learning, admit uncertainty briefly.");
        if (selfKnowledgeQuestion)
            builder.AppendLine("- The player asked about Borin's current state or ability. Answer directly and briefly, staying within the in-game character limit.");
        return builder.ToString().TrimEnd();
    }

    private static string ResolveSurface(BrainContext context)
    {
        if (IsExternalConversation(context))
            return "external MajorMUD conversation";
        if (IsEngineeringDraft(context.Purpose))
            return "controlled engineering draft";
        if (context.Purpose.StartsWith("Local repository research", StringComparison.OrdinalIgnoreCase))
            return "local repository research";
        if (context.Purpose.Contains("initiative", StringComparison.OrdinalIgnoreCase))
            return "private Forge initiative";
        if (context.Purpose.Equals("Conversation", StringComparison.OrdinalIgnoreCase))
            return "private Forge conversation";
        return Normalize(context.Purpose, "private RealmGate operation", 80);
    }

    private static string ResolveRepositoryPath(BrainContext context)
    {
        var candidates = new[]
        {
            context.Conversation.RepositorySnapshot?.RootPath,
            context.Conversation.Evidence.RepositoryRoot,
            context.Conversation.State.Configuration.RepositoryPath,
        };
        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim() ?? string.Empty;
    }

    private static bool IsExternalConversation(BrainContext context) =>
        context.Purpose.StartsWith("External MajorMUD", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Contains("EXTERNAL MAJORMUD CONVERSATION CONTRACT", StringComparison.OrdinalIgnoreCase);

    private static bool IsEngineeringDraft(string purpose) =>
        purpose.Equals("Controlled engineering draft", StringComparison.OrdinalIgnoreCase) ||
        purpose.Equals("Autonomous engineering job draft", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSelfKnowledgeQuestion(string request)
    {
        if (string.IsNullOrWhiteSpace(request))
            return false;
        var normalized = request.Trim().ToLowerInvariant();
        return SelfKnowledgeTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static string Normalize(string? value, string fallback, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized[..Math.Min(maximumLength, normalized.Length)];
    }
}
