using Reforged.App.Forge;

namespace Reforged.App.Brains;

/// <summary>
/// Provider-neutral entry point used by Borin's user-facing conversation surface.
/// A future direct GGUF/Qwen implementation can replace the current adapter without
/// changing Borin's capability coordinator or the Forge view model.
/// </summary>
public interface IBrain
{
    Task<BrainResponse> ThinkAsync(
        BrainContext context,
        CancellationToken token = default);
}

/// <summary>
/// Everything Borin needs for one turn. The existing Forge state is retained as
/// the compatibility context until its persisted data is migrated to dedicated
/// conversation and memory stores in later stages.
/// </summary>
public sealed record BrainContext(
    string GuardianId,
    string GuardianName,
    string Purpose,
    string Request,
    ConversationContext Conversation)
{
    public static BrainContext ForBorin(
        string request,
        ForgeConversationState state,
        ForgeRepositorySnapshot? repositorySnapshot,
        string purpose = "Conversation",
        BorinMemorySnapshot? memory = null,
        BorinReadOnlyEvidenceSnapshot? evidence = null,
        BorinClassicTraceSnapshot? classicTrace = null,
        BorinKnowledgeIndexSnapshot? knowledge = null,
        BorinReforgedBootstrapSnapshot? bootstrap = null,
        BorinLivingCharacterSnapshot? livingCharacter = null,
        BorinConversationDirection? conversationDirection = null,
        BorinSocialMemorySnapshot? socialMemory = null,
        BorinEpisodicMemorySnapshot? episodicMemory = null,
        BorinMemoryConsolidationSnapshot? memoryConsolidation = null,
        BorinTemporalContinuitySnapshot? temporalContinuity = null,
        BorinCharacterGrowthSnapshot? characterGrowth = null,
        BorinInitiativeSnapshot? initiative = null,
        BorinVoiceSnapshot? voice = null,
        BorinConversationRepairSnapshot? conversationRepair = null,
        BorinSituationalAwarenessSnapshot? situationalAwareness = null,
        BorinGroupConversationSnapshot? groupConversation = null,
        BorinGameStateSnapshot? gameState = null,
        BorinGameplayLearningSnapshot? gameplayLearning = null,
        BorinSelfKnowledgeSnapshot? selfKnowledge = null) =>
        new(
            GuardianId: "borin",
            GuardianName: state.Configuration.CharacterName,
            Purpose: purpose,
            Request: request,
            Conversation: new ConversationContext(
                state,
                repositorySnapshot,
                memory ?? BorinMemorySnapshot.Empty,
                evidence ?? BorinReadOnlyEvidenceSnapshot.Empty(),
                classicTrace ?? BorinClassicTraceSnapshot.Empty(),
                knowledge ?? BorinKnowledgeIndexSnapshot.Empty(),
                bootstrap ?? BorinReforgedBootstrapSnapshot.Empty(),
                livingCharacter ?? BorinLivingCharacterSnapshot.Empty,
                conversationDirection ?? BorinConversationDirection.Empty,
                socialMemory ?? BorinSocialMemorySnapshot.Empty,
                episodicMemory ?? BorinEpisodicMemorySnapshot.Empty,
                memoryConsolidation ?? BorinMemoryConsolidationSnapshot.Empty,
                temporalContinuity ?? BorinTemporalContinuitySnapshot.Empty,
                characterGrowth ?? BorinCharacterGrowthSnapshot.Empty,
                initiative ?? BorinInitiativeSnapshot.Empty,
                voice ?? BorinVoiceSnapshot.Empty,
                conversationRepair ?? BorinConversationRepairSnapshot.Empty,
                situationalAwareness ?? BorinSituationalAwarenessSnapshot.Empty,
                groupConversation ?? BorinGroupConversationSnapshot.Empty,
                gameState ?? BorinGameStateSnapshot.Empty,
                gameplayLearning ?? BorinGameplayLearningSnapshot.Empty,
                selfKnowledge ?? BorinSelfKnowledgeSnapshot.Empty));
}

/// <summary>
/// Persisted conversation, memories, attachments, project rules, and the current
/// repository snapshot supplied to a brain implementation for one request.
/// </summary>
public sealed record ConversationContext(
    ForgeConversationState State,
    ForgeRepositorySnapshot? RepositorySnapshot,
    BorinMemorySnapshot Memory,
    BorinReadOnlyEvidenceSnapshot Evidence,
    BorinClassicTraceSnapshot ClassicTrace,
    BorinKnowledgeIndexSnapshot Knowledge,
    BorinReforgedBootstrapSnapshot Bootstrap,
    BorinLivingCharacterSnapshot LivingCharacter,
    BorinConversationDirection ConversationDirection,
    BorinSocialMemorySnapshot SocialMemory,
    BorinEpisodicMemorySnapshot EpisodicMemory,
    BorinMemoryConsolidationSnapshot MemoryConsolidation,
    BorinTemporalContinuitySnapshot TemporalContinuity,
    BorinCharacterGrowthSnapshot CharacterGrowth,
    BorinInitiativeSnapshot Initiative,
    BorinVoiceSnapshot Voice,
    BorinConversationRepairSnapshot ConversationRepair,
    BorinSituationalAwarenessSnapshot SituationalAwareness,
    BorinGroupConversationSnapshot GroupConversation,
    BorinGameStateSnapshot GameState,
    BorinGameplayLearningSnapshot GameplayLearning,
    BorinSelfKnowledgeSnapshot SelfKnowledge);

/// <summary>
/// A normalized response from any Borin brain provider.
/// </summary>
public sealed record BrainPerformanceMetrics(
    int OutputTokens,
    double FirstTokenSeconds,
    double TotalSeconds,
    double TokensPerSecond,
    double ModelLoadSeconds,
    string PerformancePreset)
{
    public string Display =>
        $"{TokensPerSecond:0.0} tok/s • first token {FirstTokenSeconds:0.0}s • {OutputTokens} tokens • {PerformancePreset}";
}

public sealed record BrainResponse(
    string Text,
    string Provider,
    bool UsedFallback = false,
    bool WasCanceled = false,
    BrainPerformanceMetrics? Performance = null,
    bool ReachedTokenLimit = false,
    string? FallbackReason = null,
    BorinAiDecision? Decision = null);
