namespace RealmGate.App.Brains;

/// <summary>
/// Provider boundary for external or local reasoning engines. Borin's BrainContext remains
/// locally constructed and authoritative; providers only return a proposed response.
/// </summary>
public interface IAiProvider
{
    string Name { get; }

    Task<AiResponse> ThinkAsync(
        BrainContext context,
        CancellationToken cancellationToken = default);
}

public sealed record AiResponse(
    string Text,
    string Provider,
    string Model,
    string? RequestId = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    BorinAiDecision? Decision = null,
    bool UsedFallback = false,
    string? FallbackReason = null);

public sealed class LocalAiProvider(IBrain brain) : IAiProvider
{
    private readonly IBrain _brain = brain ?? throw new ArgumentNullException(nameof(brain));

    public string Name => "Local AI";

    public async Task<AiResponse> ThinkAsync(BrainContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var response = await _brain.ThinkAsync(context, cancellationToken).ConfigureAwait(false);
        return new AiResponse(response.Text, response.Provider, "local");
    }
}

/// <summary>Adapts the provider router back to Borin's existing conversation brain boundary.</summary>
public sealed class AiProviderBrainAdapter(IAiProvider provider) : IBrain
{
    private readonly IAiProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public async Task<BrainResponse> ThinkAsync(BrainContext context, CancellationToken token = default)
    {
        var response = await _provider.ThinkAsync(context, token).ConfigureAwait(false);
        var text = SelectText(context, response);
        return new BrainResponse(text, response.Provider, response.UsedFallback, FallbackReason: response.FallbackReason, Decision: response.Decision);
    }

    private static string SelectText(BrainContext context, AiResponse response)
    {
        if (response.Decision is null)
            return response.Text;
        if ((context.Purpose.Contains("developer", StringComparison.OrdinalIgnoreCase) ||
             context.Purpose.Contains("engineering", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(response.Decision.Target))
            return response.Decision.Target;
        if (context.Purpose.Contains("Borin Academy", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(response.Decision.Speech) &&
            !string.IsNullOrWhiteSpace(response.Decision.Reason) &&
            !response.Decision.Speech.Contains(response.Decision.Reason, StringComparison.OrdinalIgnoreCase))
            return response.Decision.Speech.Trim() + "\n\nEvidence rationale: " + response.Decision.Reason.Trim();
        return !string.IsNullOrWhiteSpace(response.Decision.Speech)
            ? response.Decision.Speech
            : response.Decision.Reason;
    }
}

public enum AiProviderFailureKind
{
    Authentication,
    RateLimited,
    Timeout,
    Network,
    ServiceUnavailable,
    ModelUnavailable,
    MalformedResponse,
    Configuration,
    Unknown
}

public sealed class AiProviderException : Exception
{
    public AiProviderException(
        AiProviderFailureKind kind,
        string message,
        int? statusCode = null,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public AiProviderFailureKind Kind { get; }
    public int? StatusCode { get; }
    public TimeSpan? RetryAfter { get; }
}
