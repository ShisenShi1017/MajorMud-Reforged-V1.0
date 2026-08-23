using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Reforged.App.Brains;

public sealed record NvidiaNimEndpointStatus(string Model, int? ReportedRequestLimit, int? ReportedRemaining, DateTimeOffset? ObservedAt);
public interface INvidiaNimEndpointTelemetry { NvidiaNimEndpointStatus EndpointStatus { get; } }

public sealed class NvidiaNimOptions
{
    public Uri BaseUri { get; init; } = new("https://integrate.api.nvidia.com/v1/");
    public string Model { get; init; } = "nvidia/nemotron-3.5-lightning-30b-a3b";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
    public int MaximumOutputTokens { get; init; } = 2048;
    public double Temperature { get; init; } = 0.2;
}

/// <summary>
/// OpenAI-compatible NVIDIA NIM reasoning provider. It has no command, file, session,
/// memory, or world mutation capability; it only returns a proposed response.
/// </summary>
public sealed class NvidiaNimProvider : IAiProvider, INvidiaNimEndpointTelemetry
{
    private readonly HttpClient _client;
    private readonly INvidiaNimCredentialStore _credentials;
    private readonly IBorinContextBuilder _contextBuilder;
    private readonly NvidiaNimOptions _options;
    private NvidiaNimEndpointStatus _endpointStatus;

    public NvidiaNimProvider(
        HttpClient client,
        INvidiaNimCredentialStore credentials,
        NvidiaNimOptions? options = null,
        IBorinContextBuilder? contextBuilder = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _options = options ?? new NvidiaNimOptions();
        _contextBuilder = contextBuilder ?? new BorinContextBuilder();
        if (!_options.BaseUri.IsAbsoluteUri || _options.BaseUri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("NVIDIA NIM requires an absolute HTTPS base URI.", nameof(options));
        if (string.IsNullOrWhiteSpace(_options.Model))
            throw new ArgumentException("An NVIDIA NIM model is required.", nameof(options));
        _endpointStatus = new(_options.Model, null, null, null);
    }

    public string Name => "NVIDIA NIM";
    public NvidiaNimEndpointStatus EndpointStatus => Volatile.Read(ref _endpointStatus);

    public async Task<AiResponse> ThinkAsync(BrainContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = await _credentials.ReadApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
            throw new AiProviderException(AiProviderFailureKind.Configuration, "The NVIDIA NIM API key is not configured.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseUri, "chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(BuildPayload(_contextBuilder.Build(context))),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException(AiProviderFailureKind.Timeout, "The NVIDIA NIM request timed out.", innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiProviderException(AiProviderFailureKind.Network, "The NVIDIA NIM endpoint could not be reached.", innerException: exception);
        }

        using (response)
        {
            ObserveRateHeaders(response);
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw CreateHttpFailure(response, body);

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (!root.TryGetProperty("choices", out var choices) ||
                    choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0 ||
                    !choices[0].TryGetProperty("message", out var message) ||
                    !message.TryGetProperty("content", out var content) ||
                    content.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(content.GetString()))
                {
                    throw new AiProviderException(AiProviderFailureKind.MalformedResponse, "NVIDIA NIM returned no usable assistant content.");
                }

                int? promptTokens = null;
                int? completionTokens = null;
                if (root.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var prompt) && prompt.TryGetInt32(out var promptValue)) promptTokens = promptValue;
                    if (usage.TryGetProperty("completion_tokens", out var completion) && completion.TryGetInt32(out var completionValue)) completionTokens = completionValue;
                }

                var requestId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
                var responseText = content.GetString()!;
                if (IsEngineeringArtifact(context.Purpose))
                {
                    if (!BorinEngineeringProposalParser.TryParse(responseText, out var proposal, out var proposalError))
                        throw new AiProviderException(AiProviderFailureKind.MalformedResponse, $"NVIDIA NIM returned an invalid engineering proposal. {proposalError}");
                    if (!string.Equals(proposal!.StateFingerprint, _contextBuilder.Build(context).StateFingerprint, StringComparison.Ordinal))
                        throw new AiProviderException(AiProviderFailureKind.MalformedResponse, "NVIDIA NIM returned an engineering proposal for stale state.");
                    return new AiResponse(proposal.Render(), Name, _options.Model, requestId, promptTokens, completionTokens);
                }
                if (!BorinAiDecisionParser.TryParse(responseText, out var decision, out var decisionError))
                {
                    // A successful authenticated completion proves endpoint connectivity even when
                    // a model emits a truncated optional greeting. This exception is deliberately
                    // limited to the non-action connection probe; operational turns remain strict.
                    if (IsConnectionTest(context.Purpose))
                    {
                        var fingerprint = _contextBuilder.Build(context).StateFingerprint;
                        var safeDecision = new BorinAiDecision(
                            "Aye, the NVIDIA reasoning forge answered the connection test.",
                            BorinAiIntent.Speak,
                            null,
                            1,
                            "Authenticated endpoint returned usable assistant content; the optional greeting structure was malformed.",
                            fingerprint);
                        return new AiResponse(safeDecision.Speech!, Name, _options.Model, requestId, promptTokens, completionTokens,
                            safeDecision, UsedFallback: true, FallbackReason: "Connection-test greeting JSON was malformed");
                    }
                    throw new AiProviderException(AiProviderFailureKind.MalformedResponse, $"NVIDIA NIM returned an invalid structured decision. {decisionError}");
                }
                return new AiResponse(responseText, Name, _options.Model, requestId, promptTokens, completionTokens, decision);
            }
            catch (JsonException exception)
            {
                throw new AiProviderException(AiProviderFailureKind.MalformedResponse, "NVIDIA NIM returned malformed JSON.", innerException: exception);
            }
        }
    }

    private void ObserveRateHeaders(HttpResponseMessage response)
    {
        static int? Read(HttpResponseMessage message, params string[] names)
        {
            foreach (var name in names)
            {
                if (!message.Headers.TryGetValues(name, out var values)) continue;
                var value = values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(value)) continue;
                var digits = new string(value.TakeWhile(character => char.IsDigit(character)).ToArray());
                if (int.TryParse(digits, out var parsed) && parsed > 0) return parsed;
            }
            return null;
        }
        var limit = Read(response, "x-ratelimit-limit-requests", "ratelimit-limit");
        var remaining = Read(response, "x-ratelimit-remaining-requests", "ratelimit-remaining");
        if (limit.HasValue || remaining.HasValue)
            Volatile.Write(ref _endpointStatus, new(_options.Model, limit, remaining, DateTimeOffset.Now));
    }

    private object BuildPayload(BorinProviderContext context)
    {
        var providerVerifiedSystemMessage = context.SystemMessage + $"""

CURRENT PROVIDER FACTS
This exact turn has reached NVIDIA NIM and is being processed by model {_options.Model}. Therefore that NVIDIA tier is connected for this request.
This proves cloud reasoning availability for the current turn only. It does not prove that a MajorMUD terminal session is connected or logged in.
Never describe the hybrid brain, NVIDIA, or the Forge as disconnected merely because VERIFIED LIVE STATE reports a disconnected game/telnet session.
Historical memories, logs, and earlier errors are not current faults unless current verified evidence repeats them.
""";
        var engineeringArtifact = IsEngineeringArtifact(context.Purpose);
        return new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = providerVerifiedSystemMessage },
                new { role = "user", content = context.UserMessage }
            },
            temperature = Math.Clamp(_options.Temperature, 0, 2),
            max_tokens = Math.Clamp(_options.MaximumOutputTokens, 64, 8192),
            // Nemotron reasoning tokens share max_tokens with visible output. NVIDIA
            // recommends disabling thinking for structured JSON so the decision is
            // not truncated after a long hidden reasoning trace.
            chat_template_kwargs = new { enable_thinking = false },
            stream = false,
            metadata = new { borin_state = context.StateFingerprint, borin_role = context.Role.ToString() },
            response_format = engineeringArtifact
                ? BuildEngineeringResponseFormat(context.StateFingerprint)
                : BuildDecisionResponseFormat(context.StateFingerprint)
        };
    }

    private static bool IsEngineeringArtifact(string purpose) =>
        purpose.Contains("engineering draft", StringComparison.OrdinalIgnoreCase) ||
        purpose.Contains("engineering job draft", StringComparison.OrdinalIgnoreCase) ||
        purpose.Contains("build correction", StringComparison.OrdinalIgnoreCase);

    private static bool IsConnectionTest(string purpose) =>
        purpose.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
        purpose.Contains("connection test", StringComparison.OrdinalIgnoreCase);

    private static object BuildDecisionResponseFormat(string stateFingerprint) => new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "borin_decision",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    speech = new { type = new[] { "string", "null" }, maxLength = 1000 },
                    intent = new { type = "string", @enum = Enum.GetNames<BorinAiIntent>() },
                    target = new { type = new[] { "string", "null" }, maxLength = 500 },
                    confidence = new { type = "number", minimum = 0, maximum = 1 },
                    reason = new { type = "string", minLength = 1, maxLength = 1200 },
                    stateFingerprint = new { type = "string", @const = stateFingerprint }
                },
                required = new[] { "speech", "intent", "target", "confidence", "reason", "stateFingerprint" },
                additionalProperties = false
            }
        }
    };

    private static object BuildEngineeringResponseFormat(string stateFingerprint) => new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "borin_engineering_proposal",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    diagnosis = new { type = "string", minLength = 1, maxLength = 4000 },
                    artifact = new { type = "string", minLength = 1, maxLength = 100000 },
                    confidence = new { type = "number", minimum = 0, maximum = 1 },
                    reason = new { type = "string", minLength = 1, maxLength = 1200 },
                    stateFingerprint = new { type = "string", @const = stateFingerprint }
                },
                required = new[] { "diagnosis", "artifact", "confidence", "reason", "stateFingerprint" },
                additionalProperties = false
            }
        }
    };

    private static AiProviderException CreateHttpFailure(HttpResponseMessage response, string body)
    {
        var status = (int)response.StatusCode;
        var kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiProviderFailureKind.Authentication,
            HttpStatusCode.TooManyRequests => AiProviderFailureKind.RateLimited,
            HttpStatusCode.NotFound => AiProviderFailureKind.ModelUnavailable,
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => AiProviderFailureKind.ServiceUnavailable,
            _ => AiProviderFailureKind.Unknown
        };
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var safeDetail = AiSecretRedactor.Redact(body);
        if (safeDetail.Length > 500) safeDetail = safeDetail[..500];
        return new AiProviderException(kind, $"NVIDIA NIM returned HTTP {status}. {safeDetail}".Trim(), status, retryAfter);
    }
}
