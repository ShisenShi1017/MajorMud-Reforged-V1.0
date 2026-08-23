using System.Net.Http;

namespace Reforged.App.Brains;

public enum BorinBrainMode
{
    Hybrid,
    LocalOnly,
    NvidiaOnly
}

public enum AiRatePriority
{
    Background,
    Normal,
    InteractiveBurst
}

public sealed record AiRateBudgetSnapshot(
    int NormalLimit,
    int HardLimit,
    int RequestsInWindow,
    int RemainingHardRequests,
    int WaitingRequests,
    DateTimeOffset? ResetsAt);

public sealed class AiRateBudgetOptions
{
    public int NormalRequestsPerWindow { get; init; } = 20;
    public int HardRequestsPerWindow { get; init; } = 40;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan MaximumQueueWait { get; init; } = TimeSpan.FromSeconds(45);
}

public interface IAiRateBudget
{
    Task AcquireAsync(AiRatePriority priority = AiRatePriority.Normal, CancellationToken cancellationToken = default);
    AiRateBudgetSnapshot Snapshot { get; }
}

/// <summary>Shared sliding-window budget intended to be registered once per API credential.</summary>
public sealed class SlidingWindowAiRateBudget : IAiRateBudget
{
    private readonly AiRateBudgetOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<DateTimeOffset> _requests = new();
    private int _waiting;

    public SlidingWindowAiRateBudget(AiRateBudgetOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new AiRateBudgetOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (_options.NormalRequestsPerWindow <= 0 || _options.HardRequestsPerWindow <= 0 ||
            _options.NormalRequestsPerWindow > _options.HardRequestsPerWindow || _options.HardRequestsPerWindow > 40)
            throw new ArgumentOutOfRangeException(nameof(options), "Rate limits must be positive, normal must not exceed hard, and hard must not exceed NVIDIA's 40 RPM allowance.");
        if (_options.Window <= TimeSpan.Zero || _options.MaximumQueueWait <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Rate window and queue wait must be positive.");
    }

    public AiRateBudgetSnapshot Snapshot
    {
        get
        {
            _gate.Wait();
            try
            {
                Prune(_timeProvider.GetUtcNow());
                return SnapshotUnsafe();
            }
            finally { _gate.Release(); }
        }
    }

    public async Task AcquireAsync(AiRatePriority priority = AiRatePriority.Normal, CancellationToken cancellationToken = default)
    {
        var deadline = _timeProvider.GetUtcNow() + _options.MaximumQueueWait;
        Interlocked.Increment(ref _waiting);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TimeSpan delay;
                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var now = _timeProvider.GetUtcNow();
                    Prune(now);
                    var limit = priority switch
                    {
                        AiRatePriority.InteractiveBurst => _options.HardRequestsPerWindow,
                        AiRatePriority.Background => Math.Min(10, Math.Max(1, _options.NormalRequestsPerWindow / 2)),
                        _ => _options.NormalRequestsPerWindow,
                    };
                    if (_requests.Count < limit)
                    {
                        _requests.Enqueue(now);
                        return;
                    }
                    delay = (_requests.Peek() + _options.Window) - now;
                    if (delay < TimeSpan.FromMilliseconds(10)) delay = TimeSpan.FromMilliseconds(10);
                    if (now + delay > deadline)
                        throw new AiProviderException(AiProviderFailureKind.RateLimited, "The shared NVIDIA request queue exceeded its bounded wait.", retryAfter: delay);
                }
                finally { _gate.Release(); }
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        finally { Interlocked.Decrement(ref _waiting); }
    }

    private void Prune(DateTimeOffset now)
    {
        while (_requests.Count > 0 && now - _requests.Peek() >= _options.Window)
            _requests.Dequeue();
    }

    private AiRateBudgetSnapshot SnapshotUnsafe()
    {
        DateTimeOffset? reset = _requests.Count == 0 ? null : _requests.Peek() + _options.Window;
        return new(_options.NormalRequestsPerWindow, _options.HardRequestsPerWindow, _requests.Count,
            Math.Max(0, _options.HardRequestsPerWindow - _requests.Count), Math.Max(0, Volatile.Read(ref _waiting)), reset);
    }
}

public sealed class RateLimitedAiProvider(
    IAiProvider inner,
    IAiRateBudget budget,
    AiRatePriority priority = AiRatePriority.Normal) : IAiProvider
{
    private readonly IAiProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IAiRateBudget _budget = budget ?? throw new ArgumentNullException(nameof(budget));
    public string Name => _inner.Name;

    public async Task<AiResponse> ThinkAsync(BrainContext context, CancellationToken cancellationToken = default)
    {
        var effectivePriority = context.Purpose.Contains("Borin Academy", StringComparison.OrdinalIgnoreCase)
            ? AiRatePriority.Background
            : priority;
        await _budget.AcquireAsync(effectivePriority, cancellationToken).ConfigureAwait(false);
        return await _inner.ThinkAsync(context, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class BorinAiRoutingSettings
{
    public BorinBrainMode Mode { get; set; } = BorinBrainMode.Hybrid;
    public bool FallBackToLocal { get; set; } = true;
    public bool AutomaticallyEscalateDifficultTasks { get; set; } = true;
}

public interface ICloudReasoningPolicy
{
    bool ShouldUseCloud(BrainContext context);
}

public sealed class BorinLocalFirstReasoningPolicy : ICloudReasoningPolicy
{
    private static readonly string[] ComplexSignals =
    [
        "diagnose", "root cause", "why does", "why is", "analyze", "investigate", "strategy", "plan",
        "repair", "patch", "refactor", "architecture", "compiler error", "build failed", "inconsistent",
        "impossible quest", "pattern", "what should i investigate", "compare", "ambiguous"
    ];

    public bool ShouldUseCloud(BrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var purpose = context.Purpose;
        if (purpose.Contains("routine gameplay", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("terminal parsing", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("deterministic", StringComparison.OrdinalIgnoreCase))
            return false;
        if (purpose.Contains("developer", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("engineering", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("sysop", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("Borin Academy", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("conversation", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("player reasoning", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("MajorMUD", StringComparison.OrdinalIgnoreCase))
            return true;
        return ComplexSignals.Any(signal => context.Request.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }
}

public enum BorinAiTier { Local, Lightning, Ultra }

public sealed record BorinAiRouteSnapshot(
    BorinAiTier Tier,
    string KeySlot,
    string Reason,
    DateTimeOffset At,
    bool Escalated = false,
    string Fallback = "")
{
    public static BorinAiRouteSnapshot Initial { get; } = new(BorinAiTier.Local, "None", "No routed request yet.", DateTimeOffset.MinValue);
}

public interface IBorinAiRouteTelemetry
{
    BorinAiRouteSnapshot LastRoute { get; }
}

public sealed record BorinTaskMetadata(
    string TaskType,
    BorinRole Role,
    int ContextCharacters,
    int EvidenceFiles,
    int PreviousFailures,
    bool IsCorrection,
    bool IsArchitecture,
    bool HasConflictingEvidence,
    bool ExplicitDeepReasoning);

public interface IBorinAiTierSelector
{
    BorinAiTier Select(BrainContext context, BorinTaskMetadata metadata, BorinAiRoutingSettings settings, ICloudReasoningPolicy localPolicy);
}

public sealed class BorinAiTierSelector : IBorinAiTierSelector
{
    public BorinAiTier Select(BrainContext context, BorinTaskMetadata metadata, BorinAiRoutingSettings settings, ICloudReasoningPolicy localPolicy)
    {
        if (settings.Mode == BorinBrainMode.LocalOnly) return BorinAiTier.Local;
        if (settings.Mode == BorinBrainMode.Hybrid && !localPolicy.ShouldUseCloud(context)) return BorinAiTier.Local;

        var score = 0;
        if (metadata.IsArchitecture) score += 4;
        if (metadata.ExplicitDeepReasoning) score += 4;
        if (metadata.EvidenceFiles >= 4) score += 3;
        else if (metadata.EvidenceFiles >= 2) score += 1;
        if (metadata.ContextCharacters >= 16000) score += 2;
        if (metadata.IsCorrection) score += 2;
        if (metadata.PreviousFailures >= 2) score += 3;
        if (metadata.HasConflictingEvidence) score += 2;
        if (metadata.Role.HasFlag(BorinRole.Developer) && metadata.EvidenceFiles >= 3) score++;
        return settings.AutomaticallyEscalateDifficultTasks && score >= 4 ? BorinAiTier.Ultra : BorinAiTier.Lightning;
    }
}

/// <summary>Three-tier local/Lightning/Ultra router. At most one escalation occurs per request.</summary>
public sealed class TieredBorinAiProviderRouter : IAiProvider, IBorinAiRouteTelemetry
{
    private readonly IAiProvider _local;
    private readonly IAiProvider _lightning;
    private readonly IAiProvider _ultra;
    private readonly BorinAiRoutingSettings _settings;
    private readonly ICloudReasoningPolicy _localPolicy;
    private readonly IBorinAiTierSelector _selector;
    private readonly Dictionary<string, int> _lightningFailures = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private BorinAiRouteSnapshot _lastRoute = BorinAiRouteSnapshot.Initial;

    public TieredBorinAiProviderRouter(IAiProvider local, IAiProvider lightning, IAiProvider ultra,
        BorinAiRoutingSettings settings, ICloudReasoningPolicy? localPolicy = null, IBorinAiTierSelector? selector = null)
    {
        _local = local; _lightning = lightning; _ultra = ultra; _settings = settings;
        _localPolicy = localPolicy ?? new BorinLocalFirstReasoningPolicy();
        _selector = selector ?? new BorinAiTierSelector();
    }

    public string Name => "Borin Tiered Hybrid AI";
    public BorinAiRouteSnapshot LastRoute { get { lock (_gate) return _lastRoute; } }

    public async Task<AiResponse> ThinkAsync(BrainContext context, CancellationToken cancellationToken = default)
    {
        var key = ObjectiveKey(context);
        var failures = FailureCount(key);
        var metadata = BuildMetadata(context, failures);
        var tier = _selector.Select(context, metadata, _settings, _localPolicy);
        if (tier == BorinAiTier.Local)
            return await RouteAsync(_local, context, cancellationToken, BorinAiTier.Local, "None", "Deterministic/local-first task.");
        if (tier == BorinAiTier.Ultra)
            return await RouteUltraFirstAsync(context, cancellationToken, metadata);

        try
        {
            var lightning = await RouteAsync(_lightning, context, cancellationToken, BorinAiTier.Lightning, "Lightning", "Standard cloud reasoning.");
            if (!_settings.AutomaticallyEscalateDifficultTasks || lightning.Decision?.Confidence is not < 0.55)
            {
                ClearFailure(key);
                return lightning;
            }
            IncrementFailure(key);
            try
            {
                var ultra = await RouteAsync(_ultra, context, cancellationToken, BorinAiTier.Ultra, "Ultra", "Lightning returned low confidence; one bounded escalation.", true);
                ClearFailure(key);
                return ultra;
            }
            catch (Exception exception) when (IsProviderFailure(exception))
            {
                SetRoute(BorinAiTier.Lightning, "Lightning", "Ultra escalation unavailable; retaining the usable Lightning result.", true, SafeReason(exception));
                return lightning with { UsedFallback = true, FallbackReason = "Ultra unavailable; retained Lightning result" };
            }
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            var effectiveFailure = exception;
            var count = IncrementFailure(key);
            if (_settings.AutomaticallyEscalateDifficultTasks && count >= 2 && MetadataWarrantsUltra(metadata))
            {
                try { return await RouteAsync(_ultra, context, cancellationToken, BorinAiTier.Ultra, "Ultra", "Deep task after repeated Lightning failures.", true); }
                catch (Exception ultraException) when (IsProviderFailure(ultraException)) { effectiveFailure = ultraException; }
            }
            if (!_settings.FallBackToLocal) throw;
            var local = await _local.ThinkAsync(context, cancellationToken).ConfigureAwait(false);
            SetRoute(BorinAiTier.Local, "None", "Lightning unavailable; safe local fallback.", false, SafeReason(effectiveFailure));
            return local with { UsedFallback = true, FallbackReason = SafeReason(effectiveFailure) };
        }
    }

    private async Task<AiResponse> RouteUltraFirstAsync(BrainContext context, CancellationToken token, BorinTaskMetadata metadata)
    {
        try { return await RouteAsync(_ultra, context, token, BorinAiTier.Ultra, "Ultra", DeepReason(metadata)); }
        catch (Exception ultraException) when (IsProviderFailure(ultraException))
        {
            try
            {
                var lightning = await RouteAsync(_lightning, context, token, BorinAiTier.Lightning, "Lightning", "Ultra unavailable; same-context Lightning fallback.", false, SafeReason(ultraException));
                return lightning with { UsedFallback = true, FallbackReason = SafeReason(ultraException) };
            }
            catch (Exception lightningException) when (IsProviderFailure(lightningException) && _settings.FallBackToLocal)
            {
                var local = await _local.ThinkAsync(context, token).ConfigureAwait(false);
                SetRoute(BorinAiTier.Local, "None", "Ultra and Lightning unavailable; safe local fallback.", false, SafeReason(lightningException));
                return local with { UsedFallback = true, FallbackReason = "Ultra and Lightning unavailable" };
            }
        }
    }

    private async Task<AiResponse> RouteAsync(IAiProvider provider, BrainContext context, CancellationToken token,
        BorinAiTier tier, string slot, string reason, bool escalated = false, string fallback = "")
    {
        SetRoute(tier, slot, reason, escalated, fallback);
        return await provider.ThinkAsync(context, token).ConfigureAwait(false);
    }

    private static BorinTaskMetadata BuildMetadata(BrainContext context, int failures)
    {
        var purpose = context.Purpose ?? string.Empty;
        var request = context.Request ?? string.Empty;
        var evidenceFiles = context.Conversation.Evidence.Items.Select(item => item.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var role = purpose.Contains("sysop", StringComparison.OrdinalIgnoreCase) || purpose.Contains("world", StringComparison.OrdinalIgnoreCase) ? BorinRole.Sysop :
            purpose.Contains("player", StringComparison.OrdinalIgnoreCase) || purpose.Contains("MajorMUD", StringComparison.OrdinalIgnoreCase) ? BorinRole.Player : BorinRole.Developer;
        return new(purpose, role, request.Length + context.Conversation.Evidence.PromptText.Length, evidenceFiles, failures,
            purpose.Contains("correction", StringComparison.OrdinalIgnoreCase) || purpose.Contains("compiler", StringComparison.OrdinalIgnoreCase),
            purpose.Contains("architecture", StringComparison.OrdinalIgnoreCase) || request.Contains("architecture-level", StringComparison.OrdinalIgnoreCase),
            request.Contains("conflicting evidence", StringComparison.OrdinalIgnoreCase) || request.Contains("ambiguous evidence", StringComparison.OrdinalIgnoreCase),
            request.Contains("deep reasoning", StringComparison.OrdinalIgnoreCase) || purpose.Contains("deep reasoning", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MetadataWarrantsUltra(BorinTaskMetadata metadata) => metadata.IsCorrection || metadata.IsArchitecture || metadata.EvidenceFiles >= 3 || metadata.HasConflictingEvidence;
    private static string DeepReason(BorinTaskMetadata m) => m.PreviousFailures >= 2 ? "Deep task after repeated Lightning failures." : m.IsArchitecture ? "Architecture-level task metadata." : m.EvidenceFiles >= 4 ? "Cross-file investigation metadata." : "High-complexity task metadata.";
    private static bool IsProviderFailure(Exception exception) => exception is AiProviderException or HttpRequestException or TimeoutException;
    private static string SafeReason(Exception exception) => exception is AiProviderException p ? $"{p.Kind}" : "provider unavailable";
    private static string ObjectiveKey(BrainContext context) => $"{context.Purpose.Trim().ToUpperInvariant()}|{context.Request.Trim().ToUpperInvariant()}";
    private int FailureCount(string key) { lock (_gate) return _lightningFailures.GetValueOrDefault(key); }
    private int IncrementFailure(string key) { lock (_gate) { var value = _lightningFailures.GetValueOrDefault(key) + 1; _lightningFailures[key] = value; if (_lightningFailures.Count > 100) _lightningFailures.Remove(_lightningFailures.Keys.First()); return value; } }
    private void ClearFailure(string key) { lock (_gate) _lightningFailures.Remove(key); }
    private void SetRoute(BorinAiTier tier, string slot, string reason, bool escalated = false, string fallback = "") { lock (_gate) _lastRoute = new(tier, slot, reason, DateTimeOffset.Now, escalated, fallback); }
}

public sealed class HybridAiProviderRouter : IAiProvider
{
    private readonly IAiProvider _local;
    private readonly IAiProvider _nvidia;
    private readonly BorinAiRoutingSettings _settings;
    private readonly ICloudReasoningPolicy _policy;

    public HybridAiProviderRouter(
        IAiProvider local,
        IAiProvider nvidia,
        BorinAiRoutingSettings settings,
        ICloudReasoningPolicy? policy = null)
    {
        _local = local ?? throw new ArgumentNullException(nameof(local));
        _nvidia = nvidia ?? throw new ArgumentNullException(nameof(nvidia));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _policy = policy ?? new BorinLocalFirstReasoningPolicy();
    }

    public string Name => "Borin Hybrid AI";

    public async Task<AiResponse> ThinkAsync(BrainContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_settings.Mode == BorinBrainMode.LocalOnly ||
            (_settings.Mode == BorinBrainMode.Hybrid && !_policy.ShouldUseCloud(context)))
            return await _local.ThinkAsync(context, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _nvidia.ThinkAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (_settings.FallBackToLocal && IsSafeProviderFailure(exception))
        {
            var fallback = await _local.ThinkAsync(context, cancellationToken).ConfigureAwait(false);
            return fallback with { UsedFallback = true, FallbackReason = SafeReason(exception) };
        }
    }

    private static bool IsSafeProviderFailure(Exception exception) =>
        exception is AiProviderException or HttpRequestException or TimeoutException;

    private static string SafeReason(Exception exception) => exception is AiProviderException provider
        ? $"NVIDIA {provider.Kind}"
        : "NVIDIA unavailable";
}
