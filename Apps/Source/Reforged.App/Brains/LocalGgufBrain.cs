using System.Diagnostics;
using System.IO;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;
using LLama.Transformers;
using Reforged.App.Forge;
using Reforged.App.Services;

namespace Reforged.App.Brains;

public sealed record LocalGgufBrainRuntimeStatus(
    string State,
    string Detail,
    string ModelName = "",
    int GpuLayerCount = 0,
    string PerformancePreset = "",
    double ModelLoadSeconds = 0,
    int OutputTokens = 0,
    double FirstTokenSeconds = 0,
    double TokensPerSecond = 0)
{
    public bool IsReady => State.Equals("Ready", StringComparison.OrdinalIgnoreCase);

    public string GpuLayerDisplay => GpuLayerCount < 0
        ? "maximum GPU offload"
        : $"{GpuLayerCount} GPU layers";

    public string PerformanceDisplay
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(PerformancePreset))
                parts.Add(PerformancePreset);
            if (TokensPerSecond > 0)
                parts.Add($"{TokensPerSecond:0.0} tok/s");
            if (FirstTokenSeconds > 0)
                parts.Add($"first token {FirstTokenSeconds:0.0}s");
            if (OutputTokens > 0)
                parts.Add($"{OutputTokens} tokens");
            if (ModelLoadSeconds > 0)
                parts.Add($"load {ModelLoadSeconds:0.0}s");
            return string.Join(" • ", parts);
        }
    }

    public string Display
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ModelName))
                return $"{State} — {Detail}";

            var performance = PerformanceDisplay;
            return string.IsNullOrWhiteSpace(performance)
                ? $"{State}: {ModelName} • {GpuLayerDisplay} — {Detail}"
                : $"{State}: {ModelName} • {GpuLayerDisplay} • {performance} — {Detail}";
        }
    }
}

/// <summary>
/// Loads Borin's GGUF model directly through LLamaSharp. The model weights live
/// for the lifetime of the application and requests are serialized so one model
/// is never driven concurrently by multiple Borin capability requests.
/// </summary>
public sealed class LocalGgufBrain : IBrain, IAsyncDisposable
{
    private static readonly string[] StopSequences =
    [
        "<|im_end|>",
        "<|endoftext|>",
        "<|im_start|>user",
        "\r\nUser:",
        "\nUser:",
        "\r\nAssistant:",
        "\nAssistant:",
        " Assistant:",
        " User:",
        "\r\nSystem:",
        "\nSystem:",
    ];

    private readonly IBrain _fallback;
    private readonly IAppLogger _logger;
    private readonly IBorinLivingCharacterStore _livingCharacter;
    private readonly IBorinSocialMemoryStore _socialMemory;
    private readonly IBorinEpisodicMemoryStore _episodicMemory;
    private readonly IBorinMemoryConsolidationStore _memoryConsolidation;
    private readonly IBorinTemporalContinuityStore _temporalContinuity;
    private readonly IBorinCharacterGrowthStore _characterGrowth;
    private readonly IBorinInitiativeStore _initiative;
    private readonly IBorinVoiceStore _voice;
    private readonly IBorinConversationRepairStore _conversationRepair;
    private readonly IBorinSituationalAwarenessStore _situationalAwareness;
    private readonly IBorinGroupConversationAwarenessStore _groupConversation;
    private readonly IBorinGameplayLearningStore _gameplayLearning;
    private readonly IBorinSelfKnowledgeStore _selfKnowledge;
    private readonly IBorinConversationDirector _conversationDirector;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LLamaWeights? _weights;
    private ModelParams? _modelParameters;
    private string? _loadedConfigurationKey;
    private double _lastModelLoadSeconds;
    private int _disposed;

    public LocalGgufBrain(
        IBrain fallback,
        IAppLogger logger,
        IBorinLivingCharacterStore livingCharacter,
        IBorinSocialMemoryStore socialMemory,
        IBorinEpisodicMemoryStore episodicMemory,
        IBorinMemoryConsolidationStore memoryConsolidation,
        IBorinTemporalContinuityStore temporalContinuity,
        IBorinCharacterGrowthStore characterGrowth,
        IBorinInitiativeStore initiative,
        IBorinVoiceStore voice,
        IBorinConversationRepairStore conversationRepair,
        IBorinSituationalAwarenessStore situationalAwareness,
        IBorinGroupConversationAwarenessStore groupConversation,
        IBorinGameplayLearningStore gameplayLearning,
        IBorinSelfKnowledgeStore selfKnowledge,
        IBorinConversationDirector conversationDirector)
    {
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _livingCharacter = livingCharacter ?? throw new ArgumentNullException(nameof(livingCharacter));
        _socialMemory = socialMemory ?? throw new ArgumentNullException(nameof(socialMemory));
        _episodicMemory = episodicMemory ?? throw new ArgumentNullException(nameof(episodicMemory));
        _memoryConsolidation = memoryConsolidation ?? throw new ArgumentNullException(nameof(memoryConsolidation));
        _temporalContinuity = temporalContinuity ?? throw new ArgumentNullException(nameof(temporalContinuity));
        _characterGrowth = characterGrowth ?? throw new ArgumentNullException(nameof(characterGrowth));
        _initiative = initiative ?? throw new ArgumentNullException(nameof(initiative));
        _voice = voice ?? throw new ArgumentNullException(nameof(voice));
        _conversationRepair = conversationRepair ?? throw new ArgumentNullException(nameof(conversationRepair));
        _situationalAwareness = situationalAwareness ?? throw new ArgumentNullException(nameof(situationalAwareness));
        _groupConversation = groupConversation ?? throw new ArgumentNullException(nameof(groupConversation));
        _gameplayLearning = gameplayLearning ?? throw new ArgumentNullException(nameof(gameplayLearning));
        _selfKnowledge = selfKnowledge ?? throw new ArgumentNullException(nameof(selfKnowledge));
        _conversationDirector = conversationDirector ?? throw new ArgumentNullException(nameof(conversationDirector));
    }

    public LocalGgufBrainRuntimeStatus CurrentStatus { get; private set; } =
        new("Unloaded", "Send Borin a message or press Load Local Brain.");

    public event Action<LocalGgufBrainRuntimeStatus>? StatusChanged;

    public IReadOnlyList<string> PerformancePresets => LocalGgufPerformancePresets.All;

    public async Task<LocalGgufBrainSettings> GetSettingsAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var repositoryRoot = LocalGgufModelLocator.ResolveRepositoryRoot(context);
        return await LocalGgufBrainSettingsStore.LoadAsync(repositoryRoot, token);
    }

    public async Task<LocalGgufBrainRuntimeStatus> ApplyPerformancePresetAsync(
        BrainContext context,
        string preset,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ThrowIfDisposed();

        await _gate.WaitAsync(token);
        try
        {
            var repositoryRoot = LocalGgufModelLocator.ResolveRepositoryRoot(context);
            var settings = await LocalGgufBrainSettingsStore.LoadAsync(repositoryRoot, token);
            var modelPath = LocalGgufModelLocator.ResolveModelPath(repositoryRoot, settings)
                ?? throw new FileNotFoundException(
                    $"No GGUF model was found. Place Qwen in {Path.Combine(repositoryRoot, "AI", "Models")}.");

            LocalGgufPerformancePresets.Apply(settings, preset, modelPath);
            await LocalGgufBrainSettingsStore.SaveAsync(repositoryRoot, settings, token);
            DisposeModel();
            SetStatus(new LocalGgufBrainRuntimeStatus(
                "Tuning",
                $"Applying the {settings.PerformancePreset} preset and reloading the model.",
                Path.GetFileName(modelPath),
                settings.GpuLayerCount,
                settings.PerformancePreset));
            await EnsureModelLoadedAsync(modelPath, settings, repositoryRoot, token);
            return CurrentStatus;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalGgufBrainRuntimeStatus> LoadAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ThrowIfDisposed();

        await _gate.WaitAsync(token);
        try
        {
            var (settings, modelPath, repositoryRoot) = await ResolveModelAsync(context, token);
            await EnsureModelLoadedAsync(modelPath, settings, repositoryRoot, token);
            return CurrentStatus;
        }
        catch (OperationCanceledException)
        {
            SetStatus(_weights is null
                ? new LocalGgufBrainRuntimeStatus("Canceled", "Model loading was canceled.")
                : CurrentStatus with { State = "Ready", Detail = "The loaded model remains ready." });
            throw;
        }
        catch (Exception exception)
        {
            SetStatus(new LocalGgufBrainRuntimeStatus("Faulted", exception.Message));
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BrainResponse> ThinkAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ThrowIfDisposed();

        await _gate.WaitAsync(token);
        try
        {
            var naturalInitiativeTurn = IsNaturalInitiativeTurn(context);
            var livingCharacterConversation = IsLivingCharacterConversation(context);
            try
            {
                var livingCharacter = livingCharacterConversation
                    ? await _livingCharacter.PrepareTurnAsync(context, token)
                    : naturalInitiativeTurn
                        ? context.Conversation.LivingCharacter
                        : await _livingCharacter.GetSnapshotAsync(token);
                context = context with
                {
                    Conversation = context.Conversation with { LivingCharacter = livingCharacter },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin living-character preparation failed; continuing with safe defaults", exception);
            }

            try
            {
                var socialMemory = livingCharacterConversation
                    ? await _socialMemory.PrepareTurnAsync(context, token)
                    : naturalInitiativeTurn
                        ? context.Conversation.SocialMemory
                        : await _socialMemory.GetSnapshotAsync(token);
                context = context with
                {
                    Conversation = context.Conversation with { SocialMemory = socialMemory },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin social-memory preparation failed; continuing with isolated safe defaults", exception);
            }

            try
            {
                var episodicMemory = livingCharacterConversation
                    ? await _episodicMemory.PrepareTurnAsync(context, token)
                    : naturalInitiativeTurn
                        ? context.Conversation.EpisodicMemory
                        : await _episodicMemory.GetSnapshotAsync(token);
                context = context with
                {
                    Conversation = context.Conversation with { EpisodicMemory = episodicMemory },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin episodic-memory preparation failed; continuing without story recall", exception);
            }

            try
            {
                var memoryConsolidation = livingCharacterConversation
                    ? await _memoryConsolidation.PrepareTurnAsync(context, token)
                    : naturalInitiativeTurn
                        ? context.Conversation.MemoryConsolidation
                        : await _memoryConsolidation.GetSnapshotAsync(token);
                context = context with
                {
                    Conversation = context.Conversation with { MemoryConsolidation = memoryConsolidation },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin memory-consolidation preparation failed; continuing with existing memory snapshots", exception);
            }

            try
            {
                var temporalContinuity = livingCharacterConversation
                    ? await _temporalContinuity.PrepareTurnAsync(context, token)
                    : naturalInitiativeTurn
                        ? context.Conversation.TemporalContinuity
                        : await _temporalContinuity.GetSnapshotAsync(token);
                context = context with
                {
                    Conversation = context.Conversation with { TemporalContinuity = temporalContinuity },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin temporal-continuity preparation failed; continuing without elapsed-time context", exception);
            }

            try
            {
                var characterGrowth = livingCharacterConversation
                    ? await _characterGrowth.PrepareTurnAsync(context, token)
                    : naturalInitiativeTurn
                        ? context.Conversation.CharacterGrowth
                        : await _characterGrowth.GetSnapshotAsync(token);
                context = context with
                {
                    Conversation = context.Conversation with { CharacterGrowth = characterGrowth },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin character-growth preparation failed; continuing with slow-growth safe defaults", exception);
            }

            try
            {
                var initiative = livingCharacterConversation
                    ? await _initiative.PrepareTurnAsync(context, token)
                    : naturalInitiativeTurn
                        ? context.Conversation.Initiative
                        : await _initiative.GetSnapshotAsync(token);
                context = context with
                {
                    Conversation = context.Conversation with { Initiative = initiative },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin initiative preparation failed; continuing with responsive safe defaults", exception);
            }

            try
            {
                var gameplayLearning = await _gameplayLearning.PrepareTurnAsync(context, token);
                context = context with
                {
                    Conversation = context.Conversation with { GameplayLearning = gameplayLearning },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin verified gameplay-learning preparation failed; continuing without learned gameplay lessons", exception);
            }

            try
            {
                var selfKnowledge = await _selfKnowledge.PrepareTurnAsync(context, CurrentStatus, token);
                context = context with
                {
                    Conversation = context.Conversation with { SelfKnowledge = selfKnowledge },
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Borin self-knowledge preparation failed; continuing with explicit uncertainty", exception);
            }

            if (livingCharacterConversation)
            {
                try
                {
                    var groupConversation = await _groupConversation.PrepareTurnAsync(context, token);
                    context = context with
                    {
                        Conversation = context.Conversation with { GroupConversation = groupConversation },
                    };
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.Error("Borin group-conversation awareness failed; continuing with direct-speaker safe defaults", exception);
                }

                try
                {
                    var conversationRepair = await _conversationRepair.PrepareTurnAsync(context, token);
                    context = context with
                    {
                        Conversation = context.Conversation with { ConversationRepair = conversationRepair },
                    };
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.Error("Borin conversational-repair preparation failed; continuing with active-listening defaults", exception);
                }

                try
                {
                    var direction = _conversationDirector.Direct(context);
                    context = context with
                    {
                        Conversation = context.Conversation with { ConversationDirection = direction },
                    };
                }
                catch (Exception exception)
                {
                    _logger.Error("Borin conversation direction failed; continuing with a direct response plan", exception);
                }

                try
                {
                    var voice = await _voice.PrepareTurnAsync(context, token);
                    context = context with
                    {
                        Conversation = context.Conversation with { Voice = voice },
                    };
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.Error("Borin voice planning failed; continuing with a grounded default voice", exception);
                }

                try
                {
                    var situationalAwareness = await _situationalAwareness.PrepareTurnAsync(context, token);
                    context = context with
                    {
                        Conversation = context.Conversation with { SituationalAwareness = situationalAwareness },
                    };
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.Error("Borin situational-awareness integration failed; continuing with strict grounded defaults", exception);
                }
            }

            LocalGgufBrainSettings settings;
            string modelPath;
            string repositoryRoot;
            try
            {
                (settings, modelPath, repositoryRoot) = await ResolveModelAsync(context, token);
                await EnsureModelLoadedAsync(modelPath, settings, repositoryRoot, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.Error("Borin direct GGUF setup failed", exception);
                SetStatus(new LocalGgufBrainRuntimeStatus("Faulted", exception.Message));
                return await UseFallbackAsync(context, exception.Message, token);
            }

            try
            {
                SetStatus(new LocalGgufBrainRuntimeStatus(
                    "Thinking",
                    "Borin is forming one reply.",
                    Path.GetFileName(modelPath),
                    _modelParameters?.GpuLayerCount ?? settings.GpuLayerCount,
                    settings.PerformancePreset,
                    _lastModelLoadSeconds));

                BrainResponse response;
                try
                {
                    response = await InferAsync(context, settings, modelPath, compactRetry: false, token: token);
                }
                catch (InvalidOperationException exception) when (IsConversationTurnOrderingFailure(exception))
                {
                    _logger.Info(
                        $"Borin direct GGUF found an unfinished user turn for '{Path.GetFileName(modelPath)}'; " +
                        "retrying once with a clean compact history.");
                    SetStatus(new LocalGgufBrainRuntimeStatus(
                        "Retrying",
                        "An unfinished prior conversation turn was discarded. Borin is retrying with clean context.",
                        Path.GetFileName(modelPath),
                        _modelParameters?.GpuLayerCount ?? settings.GpuLayerCount,
                        settings.PerformancePreset,
                        _lastModelLoadSeconds));
                    response = await InferAsync(context, settings, modelPath, compactRetry: true, token: token);
                }
                catch (EmptyLocalBrainResponseException)
                {
                    _logger.Info($"Borin direct GGUF returned no usable text for '{Path.GetFileName(modelPath)}'; retrying once with a compact turn.");
                    SetStatus(new LocalGgufBrainRuntimeStatus(
                        "Retrying",
                        "The first turn was empty. Borin is retrying with compact context.",
                        Path.GetFileName(modelPath),
                        _modelParameters?.GpuLayerCount ?? settings.GpuLayerCount,
                        settings.PerformancePreset,
                        _lastModelLoadSeconds));
                    response = await InferAsync(context, settings, modelPath, compactRetry: true, token: token);
                }
                var performance = response.Performance;
                SetStatus(new LocalGgufBrainRuntimeStatus(
                    "Ready",
                    response.WasCanceled ? "The reply was stopped; the model remains loaded." : "Local brain ready.",
                    Path.GetFileName(modelPath),
                    _modelParameters?.GpuLayerCount ?? settings.GpuLayerCount,
                    settings.PerformancePreset,
                    _lastModelLoadSeconds,
                    performance?.OutputTokens ?? 0,
                    performance?.FirstTokenSeconds ?? 0,
                    performance?.TokensPerSecond ?? 0));
                if (livingCharacterConversation && !response.WasCanceled)
                    await RecordCharacterReplySafelyAsync(context, response.Text);
                return response;
            }
            catch (OperationCanceledException)
            {
                SetStatus(new LocalGgufBrainRuntimeStatus(
                    "Ready",
                    "The request was canceled; the model remains loaded.",
                    Path.GetFileName(modelPath),
                    _modelParameters?.GpuLayerCount ?? settings.GpuLayerCount,
                    settings.PerformancePreset,
                    _lastModelLoadSeconds));
                throw;
            }
            catch (EmptyLocalBrainResponseException exception)
            {
                _logger.Error($"Direct GGUF inference returned empty output twice for '{Path.GetFileName(modelPath)}'", exception);
                SetStatus(new LocalGgufBrainRuntimeStatus(
                    "Ready",
                    "The last turn produced no usable text after one compact retry; the model remains loaded.",
                    Path.GetFileName(modelPath),
                    _modelParameters?.GpuLayerCount ?? settings.GpuLayerCount,
                    settings.PerformancePreset,
                    _lastModelLoadSeconds));
                var emptyTurnFallback = IsExternalConversationTurn(context)
                    ? "I heard ye, but me thoughts failed to form cleanly. Ask me again in a moment."
                    : "Aye, me local model is loaded, but that turn produced no usable reply after two attempts. Try the request once more; no files or tools were changed.";
                return new BrainResponse(
                    emptyTurnFallback,
                    $"Direct GGUF / {Path.GetFileName(modelPath)}",
                    UsedFallback: false);
            }
            catch (Exception exception)
            {
                _logger.Error($"Direct GGUF inference failed for '{Path.GetFileName(modelPath)}'", exception);
                SetStatus(new LocalGgufBrainRuntimeStatus(
                    "Ready",
                    "The last conversation turn failed, but the local model remains loaded and ready for the next message.",
                    Path.GetFileName(modelPath),
                    _modelParameters?.GpuLayerCount ?? settings.GpuLayerCount,
                    settings.PerformancePreset,
                    _lastModelLoadSeconds));
                return new BrainResponse(
                    $"Aye, that local thinking turn failed before I could answer: {exception.Message} " +
                    "The GGUF model remains loaded; try the message again. I changed no files and started no tools.",
                    $"Direct GGUF / {Path.GetFileName(modelPath)}",
                    UsedFallback: false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsNaturalInitiativeTurn(BrainContext context) =>
        context.Purpose.Contains("natural initiative", StringComparison.OrdinalIgnoreCase);

    private static bool IsExternalConversationTurn(BrainContext context) =>
        context.Purpose.Contains("External MajorMUD", StringComparison.OrdinalIgnoreCase);

    private static bool IsConversationalTurn(BrainContext context) =>
        context.Purpose.Equals("Conversation", StringComparison.OrdinalIgnoreCase) ||
        IsExternalConversationTurn(context) ||
        IsNaturalInitiativeTurn(context);

    private static bool IsLivingCharacterConversation(BrainContext context) =>
        IsConversationalTurn(context) && !IsNaturalInitiativeTurn(context);

    private async Task RecordCharacterReplySafelyAsync(BrainContext context, string response)
    {
        try
        {
            await _livingCharacter.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin living-character reply persistence failed", exception);
        }

        try
        {
            await _socialMemory.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin social-memory reply persistence failed", exception);
        }

        try
        {
            await _episodicMemory.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin episodic-memory reply persistence failed", exception);
        }

        try
        {
            await _temporalContinuity.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin temporal-continuity reply persistence failed", exception);
        }

        try
        {
            await _characterGrowth.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin character-growth reply persistence failed", exception);
        }

        try
        {
            await _initiative.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin initiative reply persistence failed", exception);
        }

        try
        {
            await _voice.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin voice-pattern persistence failed", exception);
        }

        try
        {
            await _conversationRepair.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin conversational-repair persistence failed", exception);
        }

        try
        {
            await _situationalAwareness.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin situational-awareness persistence failed", exception);
        }

        try
        {
            await _groupConversation.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin group-conversation persistence failed", exception);
        }

        try
        {
            await _memoryConsolidation.RecordReplyAsync(context, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Borin memory-consolidation persistence failed", exception);
        }
    }

    private async Task<(LocalGgufBrainSettings Settings, string ModelPath, string RepositoryRoot)> ResolveModelAsync(
        BrainContext context,
        CancellationToken token)
    {
        var repositoryRoot = LocalGgufModelLocator.ResolveRepositoryRoot(context);
        var settings = await LocalGgufBrainSettingsStore.LoadAsync(repositoryRoot, token);
        if (!settings.Enabled)
            throw new InvalidOperationException("The direct local GGUF brain is disabled in AI/Config/borin-brain.json.");

        var modelPath = LocalGgufModelLocator.ResolveModelPath(repositoryRoot, settings);
        if (modelPath is null)
        {
            throw new FileNotFoundException(
                $"No GGUF model was found. Place Qwen in {Path.Combine(repositoryRoot, "AI", "Models")}.");
        }

        if (_weights is null && settings.AutoTuneOnFirstLoad &&
            settings.PerformancePreset.Equals(LocalGgufPerformancePresets.Auto, StringComparison.OrdinalIgnoreCase))
        {
            LocalGgufPerformancePresets.Apply(settings, LocalGgufPerformancePresets.Auto, modelPath);
            await LocalGgufBrainSettingsStore.SaveAsync(repositoryRoot, settings, token);
        }

        return (settings, modelPath, repositoryRoot);
    }

    private async Task EnsureModelLoadedAsync(
        string modelPath,
        LocalGgufBrainSettings settings,
        string repositoryRoot,
        CancellationToken token)
    {
        var configurationKey = $"{modelPath}|{settings.ContextSize}|{settings.GpuLayerCount}";
        if (_weights is not null &&
            string.Equals(_loadedConfigurationKey, configurationKey, StringComparison.Ordinal) &&
            _modelParameters is not null)
        {
            SetStatus(new LocalGgufBrainRuntimeStatus(
                "Ready",
                "Reusing the already-loaded local brain.",
                Path.GetFileName(modelPath),
                _modelParameters.GpuLayerCount,
                settings.PerformancePreset,
                _lastModelLoadSeconds));
            return;
        }

        DisposeModel();
        SetStatus(new LocalGgufBrainRuntimeStatus(
            "Loading",
            "Loading model weights. The first load can take a few minutes.",
            Path.GetFileName(modelPath),
            settings.GpuLayerCount,
            settings.PerformancePreset));

        var requestedGpuLayers = settings.GpuLayerCount;
        var candidates = BuildGpuLayerCandidates(settings);
        Exception? lastFailure = null;
        LLamaWeights? loadedWeights = null;
        ModelParams? loadedParameters = null;
        var loadTimer = Stopwatch.StartNew();

        foreach (var gpuLayers in candidates)
        {
            token.ThrowIfCancellationRequested();
            var parameters = CreateModelParameters(modelPath, settings.ContextSize, gpuLayers);
            var gpuLabel = gpuLayers < 0 ? "maximum GPU offload" : $"{gpuLayers} GPU layers";
            _logger.Info($"Loading Borin direct GGUF brain: {Path.GetFileName(modelPath)} with {gpuLabel} ({settings.PerformancePreset})");
            SetStatus(new LocalGgufBrainRuntimeStatus(
                "Loading",
                $"Trying {gpuLabel}.",
                Path.GetFileName(modelPath),
                gpuLayers,
                settings.PerformancePreset));

            try
            {
                loadedWeights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters), token);
                loadedParameters = parameters;
                break;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastFailure = exception;
                _logger.Error($"Direct GGUF load failed with {gpuLabel}; trying the next safe option", exception);
            }
        }

        if (loadedWeights is null || loadedParameters is null)
            throw new InvalidOperationException("The GGUF model could not load with any GPU/CPU fallback.", lastFailure);

        loadTimer.Stop();
        _lastModelLoadSeconds = loadTimer.Elapsed.TotalSeconds;
        _weights = loadedWeights;
        _modelParameters = loadedParameters;

        if (loadedParameters.GpuLayerCount != requestedGpuLayers)
            settings.GpuLayerCount = loadedParameters.GpuLayerCount;
        settings.LastSuccessfulGpuLayerCount = loadedParameters.GpuLayerCount;
        _loadedConfigurationKey = $"{modelPath}|{settings.ContextSize}|{settings.GpuLayerCount}";
        await LocalGgufBrainSettingsStore.SaveAsync(repositoryRoot, settings, token);

        SetStatus(new LocalGgufBrainRuntimeStatus(
            "Ready",
            "Local brain loaded and ready.",
            Path.GetFileName(modelPath),
            loadedParameters.GpuLayerCount,
            settings.PerformancePreset,
            _lastModelLoadSeconds));
        _logger.Info(
            $"Borin direct GGUF brain loaded in {_lastModelLoadSeconds:0.0}s: {Path.GetFileName(modelPath)} with " +
            $"{(loadedParameters.GpuLayerCount < 0 ? "maximum GPU offload" : $"{loadedParameters.GpuLayerCount} GPU layers")}");
    }

    private static IReadOnlyList<int> BuildGpuLayerCandidates(LocalGgufBrainSettings settings)
    {
        var candidates = new List<int> { settings.GpuLayerCount };
        if (settings.GpuLayerCount < 0 &&
            settings.LastSuccessfulGpuLayerCount >= 0 &&
            settings.LastSuccessfulGpuLayerCount != settings.GpuLayerCount)
        {
            candidates.Add(settings.LastSuccessfulGpuLayerCount);
        }

        if (!candidates.Contains(0))
            candidates.Add(0);
        return candidates.Distinct().ToArray();
    }

    private static ModelParams CreateModelParameters(string modelPath, int contextSize, int gpuLayerCount) =>
        new(modelPath)
        {
            ContextSize = (uint)contextSize,
            GpuLayerCount = gpuLayerCount,
        };

    private async Task<BrainResponse> InferAsync(
        BrainContext brainContext,
        LocalGgufBrainSettings settings,
        string modelPath,
        bool compactRetry,
        CancellationToken token)
    {
        if (_weights is null || _modelParameters is null)
            throw new InvalidOperationException("The GGUF model was not loaded.");

        var promptPlan = BorinLocalRuntimeOptimization.CreatePlan(brainContext, settings, compactRetry);
        var samplingPlan = BorinLocalRuntimeOptimization.ResolveSampling(brainContext, settings, promptPlan, compactRetry);
        using var context = _weights.CreateContext(_modelParameters);
        var executor = new InteractiveExecutor(context);
        var history = compactRetry
            ? BuildCompactRetryHistory(brainContext, promptPlan)
            : BuildHistory(brainContext, settings, promptPlan);
        var session = new ChatSession(executor, history)
            .WithHistoryTransform(new PromptTemplateTransformer(_weights, withAssistant: true))
            .WithOutputTransform(new LLamaTransforms.KeywordTextOutputStreamTransform(
                ["<|im_end|>", "<|endoftext|>", "�"],
                redundancyLength: 5,
                removeAllMatchedTokens: true));

        using var sampling = new DefaultSamplingPipeline
        {
            Temperature = samplingPlan.Temperature,
            TopP = samplingPlan.TopP,
            RepeatPenalty = samplingPlan.RepeatPenalty,
        };
        var effectiveMaxTokens = promptPlan.EffectiveMaxTokens;
        var inference = new InferenceParams
        {
            MaxTokens = effectiveMaxTokens,
            AntiPrompts = new List<string>(StopSequences),
            SamplingPipeline = sampling,
            OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill,
        };

        var answer = new StringBuilder();
        var canceled = false;
        var inferenceTimer = Stopwatch.StartNew();
        var firstTokenSeconds = 0d;
        var streamedPieces = 0;
        var lastStatusUpdate = TimeSpan.Zero;
        try
        {
            await foreach (var text in session.ChatAsync(
                new ChatHistory.Message(AuthorRole.User, BuildTurnPrompt(brainContext, promptPlan)),
                inference,
                token))
            {
                if (string.IsNullOrEmpty(text))
                    continue;

                if (firstTokenSeconds <= 0)
                    firstTokenSeconds = inferenceTimer.Elapsed.TotalSeconds;
                answer.Append(text);
                streamedPieces++;

                if (streamedPieces == 1 ||
                    inferenceTimer.Elapsed - lastStatusUpdate >= TimeSpan.FromMilliseconds(promptPlan.StatusUpdateMilliseconds))
                {
                    lastStatusUpdate = inferenceTimer.Elapsed;
                    var liveRate = streamedPieces / Math.Max(0.1, inferenceTimer.Elapsed.TotalSeconds);
                    SetStatus(new LocalGgufBrainRuntimeStatus(
                        "Generating",
                        "Borin is speaking.",
                        Path.GetFileName(modelPath),
                        _modelParameters.GpuLayerCount,
                        settings.PerformancePreset,
                        _lastModelLoadSeconds,
                        streamedPieces,
                        firstTokenSeconds,
                        liveRate));
                }
            }
        }
        catch (OperationCanceledException) when (answer.Length > 0)
        {
            canceled = true;
        }

        inferenceTimer.Stop();
        var response = CleanResponse(answer.ToString());
        var noReplyPermitted = IsNaturalInitiativeTurn(brainContext) ||
                               (IsExternalConversationTurn(brainContext) &&
                                (!brainContext.Conversation.GroupConversation.ShouldReply ||
                                 brainContext.Conversation.GroupConversation.ShouldYield));
        if (!noReplyPermitted && BorinDialogueSafety.IsNoReplyMarker(response))
        {
            _logger.Info("Borin returned an internal NO_REPLY marker on a turn that requires an answer; treating it as empty and retrying safely.");
            response = string.Empty;
        }
        if (string.IsNullOrWhiteSpace(response))
        {
            if (canceled)
                throw new OperationCanceledException(token);
            throw new EmptyLocalBrainResponseException();
        }

        var outputTokens = context.Tokenize(response, special: true).Length;
        var totalSeconds = Math.Max(0.001, inferenceTimer.Elapsed.TotalSeconds);
        var tokensPerSecond = outputTokens / totalSeconds;
        var performance = new BrainPerformanceMetrics(
            outputTokens,
            firstTokenSeconds,
            totalSeconds,
            tokensPerSecond,
            _lastModelLoadSeconds,
            settings.PerformancePreset);

        _logger.Info(
            $"Borin direct GGUF inference complete: {performance.Display} • total {totalSeconds:0.0}s • " +
            $"{promptPlan.Summary} • sampling {samplingPlan.Reason} • {Path.GetFileName(modelPath)}");

        var configuration = brainContext.Conversation.State.Configuration;
        configuration.ProviderName = "Reforged Direct GGUF";
        configuration.ModelName = Path.GetFileName(modelPath);
        configuration.ApiEndpoint = string.Empty;
        configuration.ModelsEndpoint = string.Empty;
        configuration.AutoReconnectMasterBrain = false;
        configuration.UseMasterBrainForGeneralConversation = true;

        var reachedTokenLimit = !canceled && outputTokens >= Math.Max(1, effectiveMaxTokens - 8);
        return new BrainResponse(
            response,
            $"Direct GGUF / {Path.GetFileName(modelPath)}",
            UsedFallback: false,
            WasCanceled: canceled,
            Performance: performance,
            ReachedTokenLimit: reachedTokenLimit);
    }

    private static ChatHistory BuildCompactRetryHistory(
        BrainContext context,
        BorinLocalPromptPlan plan)
    {
        var history = new ChatHistory();
        var configuration = context.Conversation.State.Configuration;
        var externalConversation = IsExternalConversationTurn(context);
        var noReplyPermitted = IsNaturalInitiativeTurn(context) ||
                               (externalConversation &&
                                (!context.Conversation.GroupConversation.ShouldReply ||
                                 context.Conversation.GroupConversation.ShouldYield));
        var replyRule = noReplyPermitted
            ? "This turn's explicit policy permits silence. Return exactly [NO_REPLY] only when silence is still required."
            : "This turn requires an answer. Never return [NO_REPLY], NO_REPLY, or another silence marker; give a brief honest answer even if information is missing.";
        var composer = new BorinPromptComposer(plan.SystemCharacterBudget);
        composer.AppendRequired($"""
You are {configuration.CharacterName}, an old dwarf AI character running in the MajorMUD Reforged application.
Reforged is the application, not automatically the connected game server. Follow the verified game-state environment identity exactly.
The prior inference produced no usable text. Answer this turn directly, naturally, and exactly once.
Do not emit ChatML tokens, role labels, or an empty answer. {replyRule}
Verified live state, honest self-knowledge, privacy, evidence, and permission limits override memory or personality.
If information is missing, state the uncertainty instead of remaining silent.
""");
        composer.AppendSection(
            "Verified game state",
            context.Conversation.GameState.Summary,
            context.Conversation.GameState.PromptText,
            Math.Min(900, plan.SystemCharacterBudget / 6));
        composer.AppendSection(
            "Honest self-knowledge",
            IsExternalConversationTurn(context)
                ? context.Conversation.SelfKnowledge.ExternalSummary
                : context.Conversation.SelfKnowledge.Summary,
            IsExternalConversationTurn(context)
                ? context.Conversation.SelfKnowledge.ExternalPromptText
                : context.Conversation.SelfKnowledge.PromptText,
            Math.Min(900, plan.SystemCharacterBudget / 6));
        composer.AppendSection(
            "Current situation",
            context.Conversation.SituationalAwareness.Summary,
            IsExternalConversationTurn(context)
                ? context.Conversation.SituationalAwareness.ExternalPromptText
                : context.Conversation.SituationalAwareness.PromptText,
            Math.Min(1200, plan.SystemCharacterBudget / 5));
        composer.AppendSection(
            "Listening and response plan",
            context.Conversation.ConversationDirection.Summary,
            $"{context.Conversation.ConversationRepair.Summary}\n{context.Conversation.ConversationDirection.PromptText}\n{context.Conversation.Voice.PromptText}",
            Math.Min(1200, plan.SystemCharacterBudget / 5));
        composer.AppendSection(
            "Per-speaker continuity",
            context.Conversation.SocialMemory.Summary,
            context.Conversation.SocialMemory.PromptText,
            Math.Min(900, plan.SystemCharacterBudget / 6));
        composer.AppendSection(
            "Relevant episode",
            context.Conversation.EpisodicMemory.Summary,
            context.Conversation.EpisodicMemory.PromptText,
            Math.Min(700, plan.SystemCharacterBudget / 8));
        history.AddMessage(AuthorRole.System, composer.ToString());
        return history;
    }

    private static ChatHistory BuildHistory(
        BrainContext context,
        LocalGgufBrainSettings settings,
        BorinLocalPromptPlan plan)
    {
        var history = new ChatHistory();
        history.AddMessage(AuthorRole.System, BuildSystemPrompt(context, settings, plan));
        if (plan.SuppressGlobalHistory || plan.MaximumHistoryTurns <= 0 || plan.HistoryCharacterBudget <= 0)
            return history;

        var messages = context.Conversation.State.Messages;
        var scanLimit = Math.Clamp(Math.Max(settings.HistoryMessageLimit * 3, 12), 12, 60);
        var start = Math.Max(0, messages.Count - scanLimit);
        var selected = new List<(AuthorRole Role, string Content)>();

        for (var index = start; index < messages.Count; index++)
        {
            var message = messages[index];
            if (index == messages.Count - 1 &&
                message.Role.Equals("You", StringComparison.OrdinalIgnoreCase) &&
                message.Text.Trim().Equals(context.Request.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            var role = message.Role.Equals("You", StringComparison.OrdinalIgnoreCase)
                ? AuthorRole.User
                : AuthorRole.Assistant;
            var content = role == AuthorRole.Assistant
                ? CleanResponse(message.Text)
                : message.Text.Trim();
            if (!string.IsNullOrWhiteSpace(content))
                selected.Add((role, content));
        }

        var completedTurns = BuildCompletedConversationTurns(selected);
        var optimized = SelectOptimizedConversationHistory(completedTurns, context, plan);
        foreach (var turn in optimized)
        {
            history.AddMessage(AuthorRole.User, turn.User);
            history.AddMessage(AuthorRole.Assistant, turn.Assistant);
        }

        return history;
    }

    private static IReadOnlyList<(string User, string Assistant, int Order)> BuildCompletedConversationTurns(
        IReadOnlyList<(AuthorRole Role, string Content)> selected)
    {
        var completed = new List<(string User, string Assistant, int Order)>();
        string? pendingUser = null;
        var order = 0;

        foreach (var message in selected)
        {
            if (message.Role == AuthorRole.User)
            {
                pendingUser = string.IsNullOrWhiteSpace(pendingUser)
                    ? message.Content
                    : pendingUser + "\n" + message.Content;
                continue;
            }

            if (message.Role != AuthorRole.Assistant || string.IsNullOrWhiteSpace(pendingUser))
                continue;

            completed.Add((pendingUser, message.Content, order++));
            pendingUser = null;
        }

        // A pending user message represents the current, canceled, or failed turn.
        // ChatAsync appends exactly one fresh user message, so it is discarded here.
        return completed;
    }

    private static IReadOnlyList<(string User, string Assistant)> SelectOptimizedConversationHistory(
        IReadOnlyList<(string User, string Assistant, int Order)> completed,
        BrainContext context,
        BorinLocalPromptPlan plan)
    {
        if (completed.Count == 0 || plan.MaximumHistoryTurns <= 0 || plan.HistoryCharacterBudget <= 0)
            return Array.Empty<(string User, string Assistant)>();

        var terms = BorinLocalRuntimeOptimization.ExtractRelevantTerms(
            context.Request + " " +
            context.Conversation.SocialMemory.CurrentTopic + " " +
            context.Conversation.ConversationRepair.InterruptedThread);
        var lastOrder = completed[^1].Order;
        var requiredRecentOrder = Math.Max(0, lastOrder - 1);
        var ranked = completed
            .Select(turn => new
            {
                Turn = turn,
                Required = turn.Order >= requiredRecentOrder,
                Score = BorinLocalRuntimeOptimization.CountRelevantTerms(turn.User + " " + turn.Assistant, terms) * 20 +
                        Math.Max(0, 10 - (lastOrder - turn.Order)),
            })
            .OrderByDescending(item => item.Required)
            .ThenByDescending(item => item.Score)
            .ThenByDescending(item => item.Turn.Order)
            .Take(plan.MaximumHistoryTurns)
            .Select(item => item.Turn)
            .OrderBy(turn => turn.Order)
            .ToArray();

        var acceptedNewestFirst = new List<(string User, string Assistant, int Order)>();
        var remaining = plan.HistoryCharacterBudget;
        foreach (var turn in ranked.Reverse())
        {
            var user = BorinLocalRuntimeOptimization.LimitSection(turn.User, plan.MaximumMessageCharacters);
            var assistant = BorinLocalRuntimeOptimization.LimitSection(turn.Assistant, plan.MaximumMessageCharacters);
            var cost = user.Length + assistant.Length;
            if (cost > remaining && acceptedNewestFirst.Count > 0)
                continue;

            if (cost > remaining)
            {
                var half = Math.Max(140, remaining / 2);
                user = BorinLocalRuntimeOptimization.LimitSection(user, half);
                assistant = BorinLocalRuntimeOptimization.LimitSection(assistant, Math.Max(140, remaining - user.Length));
                cost = user.Length + assistant.Length;
            }

            if (user.Length == 0 || assistant.Length == 0)
                continue;
            acceptedNewestFirst.Add((user, assistant, turn.Order));
            remaining -= cost;
            if (remaining <= 280)
                break;
        }

        return acceptedNewestFirst
            .OrderBy(turn => turn.Order)
            .Select(turn => (turn.User, turn.Assistant))
            .ToArray();
    }

    private static string BuildExternalLivingCharacterPrompt(
        BorinLivingCharacterSnapshot living,
        BorinSocialMemorySnapshot social) =>
        $"""
Borin's external-player character continuity for this turn:
- Current mood: {living.Mood}.
- Current speaker: {living.CurrentSpeaker}.
- Relationship: {living.Relationship}; trust {living.RelationshipTrust}/100; familiarity {living.RelationshipFamiliarity}/100.
- Authoritative per-speaker topic: {social.CurrentTopic}; {social.ThreadTurnCount} turn(s) in this speaker's thread.
- The speaker's last message: {social.LastUserMessage}
- Borin's prior reply to this speaker: {social.LastBorinResponse}

External social behavior:
- Let mood and relationship affect wording subtly; never reveal scores.
- Continue only this speaker's isolated history. Never expose private conversations, project rules, goals, or memories involving anyone else.
- Use only the facts, experiences, commitments, questions, and recent exchanges in the supplied isolated social profile.
- Never invent game state, shared history, completed actions, or verified rules.
""";

    private static string BuildConversationalSystemPrompt(
        BrainContext context,
        LocalGgufBrainSettings settings,
        BorinLocalPromptPlan plan,
        bool externalConversation)
    {
        var configuration = context.Conversation.State.Configuration;
        var direction = context.Conversation.ConversationDirection;
        var voice = context.Conversation.Voice;
        var repair = context.Conversation.ConversationRepair;
        var situation = context.Conversation.SituationalAwareness;
        var group = context.Conversation.GroupConversation;
        var game = context.Conversation.GameState;
        var learning = context.Conversation.GameplayLearning;
        var self = context.Conversation.SelfKnowledge;
        var social = context.Conversation.SocialMemory;
        var living = context.Conversation.LivingCharacter;
        var episodic = context.Conversation.EpisodicMemory;
        var temporal = context.Conversation.TemporalContinuity;
        var growth = context.Conversation.CharacterGrowth;
        var initiative = context.Conversation.Initiative;
        var memoryMaintenance = context.Conversation.MemoryConsolidation;
        var composer = new BorinPromptComposer(plan.SystemCharacterBudget);
        var speechStyle = BorinLocalRuntimeOptimization.LimitSection(configuration.SpeechStyle, 280);
        var temperament = BorinLocalRuntimeOptimization.LimitSection(configuration.Temperament, 280);
        var values = BorinLocalRuntimeOptimization.LimitSection(configuration.Values, 320);
        var mission = BorinLocalRuntimeOptimization.LimitSection(configuration.Mission, 360);
        var characterInstructions = BorinLocalRuntimeOptimization.LimitSection(configuration.CharacterInstructions, 850);
        var externalSilenceRequired = externalConversation && (!group.ShouldReply || group.ShouldYield);
        var modeRule = externalConversation
            ? externalSilenceRequired
                ? "This is an external MajorMUD conversation and the verified group policy requires silence. Return exactly [NO_REPLY]."
                : "This is an external MajorMUD conversation. The verified group gate approved one reply. Return only Borin's spoken words and never output [NO_REPLY], NO_REPLY, or another silence marker on this addressed turn."
            : "This is a private Forge conversation with William. He directly addressed you. Speak as his trusted long-term adventuring companion, not as customer support or a repository report. Never output [NO_REPLY], NO_REPLY, or another silence marker in Forge; answer honestly even when the answer is uncertainty.";

        composer.AppendRequired($"""
You are {configuration.CharacterName}, {configuration.CharacterTitle}, age {configuration.CharacterAge}, an old dwarf AI character in the MajorMUD Reforged application.
The Forge is a private application workspace, not a game room. Your active character may be playing on Omniverse BBS, the Reforged server, or another verified server; use the supplied game-state identity and never merge them.
Speech style: {speechStyle}
Temperament: {temperament}
Values: {values}
Mission: {mission}
Character rules: {characterInstructions}
{modeRule}

Answer naturally and exactly once. React to the speaker's meaning and emotion before adding facts. Continue the current topic unless the listening plan says the speaker corrected, interrupted, abandoned, switched, or resumed it.
Priority order for this turn:
1. Group-yield, privacy, safety, permission, and evidence boundaries.
2. Verified live game state and honest current capability.
3. Listening/repair and the direct response plan.
4. Unified current situation.
5. Only the isolated memories and character details relevant to this speaker.
Live Client evidence overrides memory. Connected is not the same as logged into MajorMUD. Historical gameplay learning never overrides live state or decoded authority.
Never claim a file edit, build, test, install, deployment, login, command, movement, combat action, or other completed act without explicit current evidence. Conversation never grants those permissions.
Vary openings and sentence rhythm. Do not begin every reply with "Aye," repeat the user's message, narrate stage directions, expose internal scores, or append a generic capability offer.
Ask at most one useful follow-up question, and only when both the director and voice plan allow it. Admit uncertainty briefly instead of inventing facts or shared history.
Do not emit role labels, ChatML tokens, JSON wrappers, quotation marks around the whole answer, or a second turn.
{(externalConversation ? $"The final spoken text must be at or below {BorinGameStateSnapshot.HardInGameReplyCharacterLimit} characters. Aim below {BorinGameStateSnapshot.PreferredInGameReplyCharacterTarget}; the sender enforces the hard final limit." : "For ordinary conversation, prefer one to four short paragraphs and stop when the thought is complete.")}
""");

        composer.AppendSection(
            "Verified active-player game state",
            game.Summary,
            externalConversation ? game.ExternalPromptText : game.PromptText,
            externalConversation ? 620 : 500);
        composer.AppendSection(
            "Honest self-knowledge and capability boundary",
            externalConversation ? self.ExternalSummary : self.Summary,
            externalConversation ? self.ExternalPromptText : self.PromptText,
            externalConversation ? 520 : 450);
        composer.AppendSection(
            "Group-conversation floor",
            group.Summary,
            externalConversation ? group.ExternalPromptText : group.PromptText,
            externalConversation ? 720 : 160,
            externalConversation ? 160 : 70);
        composer.AppendSection(
            "Natural listening and repair",
            repair.Summary,
            externalConversation ? repair.ExternalPromptText : repair.PromptText,
            360);
        composer.AppendSection(
            "Turn response direction",
            direction.Summary,
            direction.PromptText,
            520);
        composer.AppendSection(
            "Unified current situation",
            situation.Summary,
            externalConversation ? situation.ExternalPromptText : situation.PromptText,
            externalConversation ? 720 : 650);
        composer.AppendSection(
            "Voice and dialogue rhythm",
            voice.Summary,
            voice.PromptText,
            380);
        composer.AppendSection(
            "Per-speaker social continuity",
            social.Summary,
            social.PromptText,
            externalConversation ? 650 : 560);
        composer.AppendSection(
            "Living-character continuity",
            living.Summary,
            externalConversation ? BuildExternalLivingCharacterPrompt(living, social) : living.PromptText,
            360);
        composer.AppendSection(
            "Relevant episodic recall",
            episodic.Summary,
            episodic.PromptText,
            320);
        composer.AppendSection(
            "Verified gameplay learning",
            learning.Summary,
            externalConversation ? learning.ExternalPromptText : learning.PromptText,
            260);
        composer.AppendSection(
            "Time continuity",
            temporal.Summary,
            externalConversation ? temporal.ExternalPromptText : temporal.PromptText,
            220);
        composer.AppendSection(
            "Slow character growth",
            growth.Summary,
            externalConversation ? growth.ExternalPromptText : growth.PromptText,
            220);
        composer.AppendSection(
            "Safe motivation and initiative",
            initiative.Summary,
            externalConversation ? initiative.ExternalPromptText : initiative.PromptText,
            220);
        composer.AppendSection(
            "Director-selected relevant memories",
            $"{direction.RelevantRecall.Count} selected",
            direction.MemoryPrompt,
            220);
        composer.AppendSection(
            "Memory maintenance boundary",
            externalConversation ? "isolated per-speaker maintenance" : memoryMaintenance.Summary,
            externalConversation ? memoryMaintenance.ExternalPromptText : memoryMaintenance.PromptText,
            120,
            70);

        if (!externalConversation && context.Conversation.State.ApprovedProjectRules.Count > 0)
        {
            composer.AppendSection(
                "Approved standing project rules",
                $"{context.Conversation.State.ApprovedProjectRules.Count} active",
                string.Join("\n", context.Conversation.State.ApprovedProjectRules.Select(rule => $"- {rule}")),
                420);
        }

        return composer.ToString();
    }

    private static string BuildSystemPrompt(
        BrainContext context,
        LocalGgufBrainSettings settings,
        BorinLocalPromptPlan plan)
    {
        var state = context.Conversation.State;
        var configuration = state.Configuration;
        var externalConversation = IsExternalConversationTurn(context);
        if (IsConversationalTurn(context))
            return BuildConversationalSystemPrompt(context, settings, plan, externalConversation);

        var repositoryResearch = plan.Mode == BorinLocalPromptModes.RepositoryResearch;
        var engineeringDraft = plan.Mode == BorinLocalPromptModes.EngineeringDraft;
        var composer = new BorinPromptComposer(plan.SystemCharacterBudget);
        var capabilityRules = engineeringDraft
            ? "You may draft source code and exactly one repository-relative unified diff for review. Drafting is not editing. Stop at AwaitingApproval and never claim apply, build, test, install, validation, packaging, commit, push, or deployment occurred."
            : "You have only the read-only evidence explicitly supplied for this turn. You have no file-edit, delete, build, test, shell, restart, installation, packaging, deployment, commit, push, login, movement, or game-command permission.";

        composer.AppendRequired($"""
You are {configuration.CharacterName}, {configuration.CharacterTitle}, age {configuration.CharacterAge}, an old dwarf working inside the MajorMUD Reforged application.
Reforged is the application and engineering project; do not confuse it with whichever MajorMUD server the active player session uses.
Speech style: {configuration.SpeechStyle}
Temperament: {configuration.Temperament}
Values: {configuration.Values}
Mission: {configuration.Mission}
Character rules: {configuration.CharacterInstructions}
Address the user as {configuration.AddressUserAs}. Keep the dwarf voice readable and never let it obscure exact technical details.
{capabilityRules}

The Librarium and decoded/generated data remain authoritative for gameplay. Never invent decoded gameplay facts.
Treat paths and line numbers as read-only observations, not proof that a proposed fix is correct. Indexed knowledge is retrieval context, not model training.
Keep Classic trace stages separate: decoded evidence does not prove Librarium projection, generated data, runtime loading, live consumption, persistence/reset/cleanup, or parity validation. Imported database material is reference-only.
Treat honest self-knowledge as authoritative for current capability. Configured capability, a plan, a drafted patch, or remembered discussion is not evidence that an action completed.
Memory evidence labels remain literal: discussed, decided, installed, built, and validated are different states and must never be promoted without evidence.
When evidence is absent, say so. Reply exactly once without role labels, ChatML tokens, or a second turn.
{(repositoryResearch ? "For repository research, report only supported current findings, group duplicate evidence by path, cite exact supplied locations, and stop at a clean section boundary if the output limit is reached." : string.Empty)}
""");

        var self = context.Conversation.SelfKnowledge;
        composer.AppendSection(
            "Honest self-knowledge",
            self.Summary,
            self.PromptText,
            repositoryResearch ? 650 : 850);

        if (repositoryResearch || engineeringDraft)
        {
            composer.AppendSection(
                "Read-only repository evidence",
                context.Conversation.Evidence.Summary,
                context.Conversation.Evidence.PromptText,
                repositoryResearch ? 2400 : 1900);
            composer.AppendSection(
                "Structured Classic trace",
                context.Conversation.ClassicTrace.Summary,
                context.Conversation.ClassicTrace.PromptText,
                repositoryResearch ? 2200 : 1500);
            composer.AppendSection(
                "Ranked persistent knowledge",
                context.Conversation.Knowledge.Summary,
                context.Conversation.Knowledge.PromptText,
                repositoryResearch ? 1800 : 1200);
            composer.AppendSection(
                "Compact Reforged orientation",
                context.Conversation.Bootstrap.Summary,
                context.Conversation.Bootstrap.PromptText,
                900);
            composer.AppendSection(
                "Fixed Reforged authority rules",
                BorinProjectAwareness.Version,
                BorinProjectAwareness.PromptText,
                700);
        }
        else
        {
            composer.AppendSection(
                "Verified active-player game state",
                context.Conversation.GameState.Summary,
                context.Conversation.GameState.PromptText,
                850);
            composer.AppendSection(
                "Fixed Reforged awareness",
                BorinProjectAwareness.Version,
                BorinProjectAwareness.PromptText,
                850);
            composer.AppendSection(
                "Compact Reforged orientation",
                context.Conversation.Bootstrap.Summary,
                context.Conversation.Bootstrap.PromptText,
                900);
            composer.AppendSection(
                "Relevant persistent memory",
                context.Conversation.Memory.Summary,
                context.Conversation.Memory.PromptText,
                750);
            composer.AppendSection(
                "Read-only repository evidence",
                context.Conversation.Evidence.Summary,
                context.Conversation.Evidence.PromptText,
                1100);
            composer.AppendSection(
                "Ranked persistent knowledge",
                context.Conversation.Knowledge.Summary,
                context.Conversation.Knowledge.PromptText,
                1000);
        }

        if (state.ApprovedProjectRules.Count > 0)
        {
            composer.AppendSection(
                "Approved project rules",
                $"{state.ApprovedProjectRules.Count} active",
                string.Join("\n", state.ApprovedProjectRules.Select(rule => $"- {rule}")),
                650);
        }

        return composer.ToString();
    }

    private static string BuildTurnPrompt(BrainContext context, BorinLocalPromptPlan plan)
    {
        if (IsLivingCharacterConversation(context))
            return BorinLocalRuntimeOptimization.LimitSection(context.Request, plan.TurnCharacterBudget);

        var snapshot = context.Conversation.RepositorySnapshot;
        var repository = snapshot is null
            ? "Repository snapshot: not currently available."
            : $"Repository snapshot: {snapshot.ProjectCount} projects, {snapshot.SourceFileCount} source/UI files, {snapshot.TestProjectCount} test projects, root {snapshot.RootPath}.";

        var evidence = context.Conversation.Evidence;
        var classicTrace = context.Conversation.ClassicTrace;
        var knowledge = context.Conversation.Knowledge;
        var bootstrap = context.Conversation.Bootstrap;

        var turn = $"""
Purpose: {context.Purpose}
{repository}
Read-only evidence already supplied in the system context: {evidence.Summary}
Classic trace already supplied in the system context: {classicTrace.Summary}
Indexed knowledge already supplied in the system context: {knowledge.Summary}
Compact Reforged bootstrap already supplied in the system context: {bootstrap.Summary}
Request: {context.Request}
""";
        return BorinLocalRuntimeOptimization.LimitSection(turn, plan.TurnCharacterBudget);
    }

    private static bool IsConversationTurnOrderingFailure(InvalidOperationException exception) =>
        exception.Message.Contains(
            "Cannot add a user message after another user message",
            StringComparison.OrdinalIgnoreCase);

    private async Task<BrainResponse> UseFallbackAsync(
        BrainContext context,
        string reason,
        CancellationToken token)
    {
        _logger.Info($"Borin direct GGUF fallback: {reason}");
        var fallback = await _fallback.ThinkAsync(context, token);
        return fallback with
        {
            Text = $"Me local brain could not answer: {reason}\n\n{fallback.Text}",
            Provider = $"{fallback.Provider} (local GGUF unavailable)",
            UsedFallback = true,
        };
    }

    private static string CleanResponse(string text)
    {
        var cleaned = text
            .Replace("<|im_end|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|endoftext|>", string.Empty, StringComparison.Ordinal)
            .Trim();

        while (cleaned.StartsWith("Assistant:", StringComparison.OrdinalIgnoreCase) ||
               cleaned.StartsWith("Borin:", StringComparison.OrdinalIgnoreCase))
        {
            var colon = cleaned.IndexOf(':');
            cleaned = colon >= 0 ? cleaned[(colon + 1)..].TrimStart() : cleaned;
        }

        var cutAt = cleaned.Length;
        foreach (var marker in StopSequences)
        {
            var index = cleaned.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && index < cutAt)
                cutAt = index;
        }

        if (cutAt < cleaned.Length)
            cleaned = cleaned[..cutAt];

        return cleaned.Trim();
    }

    private void SetStatus(LocalGgufBrainRuntimeStatus status)
    {
        CurrentStatus = status;
        StatusChanged?.Invoke(status);
    }

    private sealed class EmptyLocalBrainResponseException : Exception
    {
        public EmptyLocalBrainResponseException()
            : base("The model returned an empty response on this turn.")
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(LocalGgufBrain));
    }

    private void DisposeModel()
    {
        _weights?.Dispose();
        _weights = null;
        _modelParameters = null;
        _loadedConfigurationKey = null;
        _lastModelLoadSeconds = 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _gate.WaitAsync();
        try
        {
            DisposeModel();
            SetStatus(new LocalGgufBrainRuntimeStatus("Unloaded", "Application shutdown complete."));
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
