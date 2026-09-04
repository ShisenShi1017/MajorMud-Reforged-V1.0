using System.Text;
using System.Text.RegularExpressions;

namespace RealmGate.App.Brains;

public static class BorinLocalPromptModes
{
    public const string ExternalConversation = "external conversation";
    public const string PrivateConversation = "private conversation";
    public const string NaturalInitiative = "natural initiative";
    public const string RepositoryResearch = "repository research";
    public const string EngineeringDraft = "engineering draft";
    public const string General = "general reasoning";
}

public sealed record BorinLocalPromptPlan(
    string Mode,
    int ContextSize,
    int EffectiveMaxTokens,
    int InputCharacterBudget,
    int SystemCharacterBudget,
    int HistoryCharacterBudget,
    int TurnCharacterBudget,
    int MaximumHistoryTurns,
    int MaximumMessageCharacters,
    int StatusUpdateMilliseconds,
    bool SuppressGlobalHistory,
    bool PreferRelevantHistory)
{
    public string Summary =>
        $"{Mode} • context {ContextSize} • input budget {InputCharacterBudget} chars • " +
        $"system {SystemCharacterBudget} • history {HistoryCharacterBudget}/{MaximumHistoryTurns} turns • " +
        $"turn {TurnCharacterBudget} • output {EffectiveMaxTokens} tokens";
}

public sealed record BorinLocalSamplingPlan(
    float Temperature,
    float TopP,
    float RepeatPenalty,
    string Reason);

/// <summary>
/// Model-independent planning for Borin's direct LLamaSharp runtime. It keeps
/// each prompt comfortably below the configured context window, reserves output
/// space before inference begins, and chooses purpose-specific history/output
/// budgets so ordinary Forge conversation is not slowed by engineering-sized
/// prompts. It never changes the model, grants permissions, or rewrites memory.
/// </summary>
public static class BorinLocalRuntimeOptimization
{
    private const double ConservativeCharactersPerToken = 3.0d;
    private const int MinimumInputCharacters = 2400;
    private const int MaximumInputCharacters = 60000;

    private static readonly Regex SearchTokenPattern = new(
        "[A-Za-z0-9_./'-]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "have", "will", "your", "you", "are", "was", "were",
        "for", "but", "not", "can", "could", "would", "should", "into", "about", "what", "when", "where", "why",
        "how", "him", "his", "her", "our", "out", "now", "then", "than", "just", "like", "want", "need", "please",
        "borin", "speaker", "message", "current", "conversation", "majormud", "contract", "really", "okay", "yeah",
        "purpose", "request", "reply", "user", "assistant", "system",
    };

    public static BorinLocalPromptPlan CreatePlan(
        BrainContext context,
        LocalGgufBrainSettings settings,
        bool compactRetry = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var mode = ResolveMode(context);
        var outputTokens = ResolveOutputTokens(mode, settings, compactRetry);
        var safetyMargin = Math.Clamp(settings.PromptSafetyMarginTokens, 128, 1024);
        var inputTokens = Math.Max(768, settings.ContextSize - outputTokens - safetyMargin);
        var inputCharacters = Math.Clamp(
            (int)Math.Floor(inputTokens * ConservativeCharactersPerToken),
            MinimumInputCharacters,
            MaximumInputCharacters);

        double systemShare;
        double historyShare;
        int maximumTurns;
        bool suppressGlobalHistory;
        bool preferRelevantHistory;

        switch (mode)
        {
            case BorinLocalPromptModes.ExternalConversation:
                systemShare = 0.76;
                historyShare = 0.00;
                maximumTurns = 0;
                suppressGlobalHistory = true;
                preferRelevantHistory = false;
                break;

            case BorinLocalPromptModes.NaturalInitiative:
                systemShare = 0.78;
                historyShare = 0.00;
                maximumTurns = 0;
                suppressGlobalHistory = true;
                preferRelevantHistory = false;
                break;

            case BorinLocalPromptModes.PrivateConversation:
                systemShare = 0.74;
                historyShare = 0.14;
                maximumTurns = Math.Clamp(
                    Math.Min(settings.HistoryMessageLimit / 2, context.Conversation.ConversationDirection.HistoryTurnLimit),
                    1,
                    6);
                suppressGlobalHistory = false;
                preferRelevantHistory = true;
                break;

            case BorinLocalPromptModes.RepositoryResearch:
                systemShare = 0.75;
                historyShare = 0.04;
                maximumTurns = 1;
                suppressGlobalHistory = false;
                preferRelevantHistory = true;
                break;

            case BorinLocalPromptModes.EngineeringDraft:
                systemShare = 0.72;
                historyShare = 0.06;
                maximumTurns = 2;
                suppressGlobalHistory = false;
                preferRelevantHistory = true;
                break;

            default:
                systemShare = 0.70;
                historyShare = 0.12;
                maximumTurns = Math.Clamp(settings.HistoryMessageLimit / 2, 1, 4);
                suppressGlobalHistory = false;
                preferRelevantHistory = true;
                break;
        }

        var minimumTurnBudget = Math.Min(600, Math.Max(320, inputCharacters / 4));
        var historyBudget = suppressGlobalHistory
            ? 0
            : Math.Max(0, (int)Math.Floor(inputCharacters * historyShare));
        var desiredSystemBudget = Math.Max(1000, (int)Math.Floor(inputCharacters * systemShare));
        var maximumSystemBudget = Math.Max(1000, inputCharacters - historyBudget - minimumTurnBudget);
        var systemBudget = Math.Min(desiredSystemBudget, maximumSystemBudget);

        if (systemBudget + historyBudget + minimumTurnBudget > inputCharacters)
            historyBudget = Math.Max(0, inputCharacters - systemBudget - minimumTurnBudget);

        var turnBudget = Math.Max(0, inputCharacters - systemBudget - historyBudget);
        var maximumMessageCharacters = historyBudget <= 0
            ? 0
            : Math.Clamp(
                maximumTurns <= 1 ? historyBudget : historyBudget / Math.Max(1, maximumTurns * 2),
                280,
                1800);

        if (compactRetry)
        {
            historyBudget = 0;
            systemBudget = Math.Min(
                Math.Max(1000, inputCharacters - minimumTurnBudget),
                Math.Max(1000, inputCharacters * 3 / 4));
            turnBudget = Math.Max(0, inputCharacters - systemBudget);
            maximumTurns = 0;
            maximumMessageCharacters = 0;
            suppressGlobalHistory = true;
            preferRelevantHistory = false;
        }

        return new BorinLocalPromptPlan(
            mode,
            settings.ContextSize,
            outputTokens,
            inputCharacters,
            systemBudget,
            historyBudget,
            turnBudget,
            maximumTurns,
            maximumMessageCharacters,
            Math.Clamp(settings.StatusUpdateMilliseconds, 250, 2500),
            suppressGlobalHistory,
            preferRelevantHistory);
    }

    public static BorinLocalSamplingPlan ResolveSampling(
        BrainContext context,
        LocalGgufBrainSettings settings,
        BorinLocalPromptPlan plan,
        bool compactRetry)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(plan);

        if (compactRetry)
        {
            return new BorinLocalSamplingPlan(
                Math.Clamp(settings.Temperature, 0.48f, 0.62f),
                Math.Clamp(settings.TopP, 0.72f, 0.88f),
                Math.Clamp(settings.RepeatPenalty, 1.08f, 1.18f),
                "compact deterministic retry");
        }

        var voice = context.Conversation.Voice;
        return plan.Mode switch
        {
            BorinLocalPromptModes.RepositoryResearch or BorinLocalPromptModes.EngineeringDraft =>
                new BorinLocalSamplingPlan(
                    Math.Clamp(settings.TechnicalTemperature, 0.20f, 0.60f),
                    Math.Clamp(Math.Min(settings.TopP, 0.86f), 0.65f, 0.90f),
                    Math.Clamp(Math.Max(settings.RepeatPenalty, 1.10f), 1.05f, 1.22f),
                    "precise technical reasoning"),

            BorinLocalPromptModes.ExternalConversation =>
                new BorinLocalSamplingPlan(
                    Math.Clamp(settings.Temperature + voice.TemperatureOffset, 0.54f, 0.76f),
                    Math.Clamp(Math.Max(voice.TopPMinimum, settings.TopP), 0.76f, 0.94f),
                    Math.Clamp(Math.Max(voice.RepeatPenaltyMinimum, settings.RepeatPenalty), 1.08f, 1.20f),
                    "brief grounded in-game conversation"),

            BorinLocalPromptModes.PrivateConversation or BorinLocalPromptModes.NaturalInitiative =>
                new BorinLocalSamplingPlan(
                    Math.Clamp(settings.Temperature + voice.TemperatureOffset, 0.56f, 0.84f),
                    Math.Clamp(Math.Max(voice.TopPMinimum, settings.TopP), 0.78f, 0.95f),
                    Math.Clamp(Math.Max(voice.RepeatPenaltyMinimum, settings.RepeatPenalty), 1.08f, 1.20f),
                    "natural character conversation"),

            _ => new BorinLocalSamplingPlan(
                Math.Clamp(settings.Temperature, 0.45f, 0.72f),
                Math.Clamp(settings.TopP, 0.72f, 0.92f),
                Math.Clamp(settings.RepeatPenalty, 1.05f, 1.20f),
                "balanced general reasoning"),
        };
    }

    public static IReadOnlySet<string> ExtractRelevantTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return SearchTokenPattern.Matches(value)
            .Select(match => match.Value.Trim('.', '/', '-', '\'', '"').ToLowerInvariant())
            .Where(term => term.Length >= 3 && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static int CountRelevantTerms(string? value, IReadOnlySet<string> terms)
    {
        if (string.IsNullOrWhiteSpace(value) || terms.Count == 0)
            return 0;

        var score = 0;
        foreach (var term in terms)
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                score++;
        }
        return score;
    }

    public static string LimitSection(string? text, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(text) || maximumCharacters <= 0)
            return string.Empty;

        var value = text.Trim();
        if (value.Length <= maximumCharacters)
            return value;
        if (maximumCharacters < 96)
            return value[..maximumCharacters];

        const string marker = "\n...[lower-priority context compacted]...\n";
        var available = maximumCharacters - marker.Length;
        var headLength = Math.Max(1, available * 3 / 4);
        var tailLength = Math.Max(1, available - headLength);
        return value[..headLength] + marker + value[^tailLength..];
    }

    private static string ResolveMode(BrainContext context)
    {
        var purpose = context.Purpose ?? string.Empty;
        if (purpose.Contains("External MajorMUD", StringComparison.OrdinalIgnoreCase))
            return purpose.Contains("natural initiative", StringComparison.OrdinalIgnoreCase)
                ? BorinLocalPromptModes.NaturalInitiative
                : BorinLocalPromptModes.ExternalConversation;
        if (purpose.Contains("natural initiative", StringComparison.OrdinalIgnoreCase))
            return BorinLocalPromptModes.NaturalInitiative;
        if (purpose.Equals("Conversation", StringComparison.OrdinalIgnoreCase))
            return BorinLocalPromptModes.PrivateConversation;
        if (purpose.StartsWith("Local repository research", StringComparison.OrdinalIgnoreCase))
            return BorinLocalPromptModes.RepositoryResearch;
        if (purpose.Equals("Controlled engineering draft", StringComparison.OrdinalIgnoreCase) ||
            purpose.Equals("Autonomous engineering job draft", StringComparison.OrdinalIgnoreCase))
        {
            return BorinLocalPromptModes.EngineeringDraft;
        }
        return BorinLocalPromptModes.General;
    }

    private static int ResolveOutputTokens(
        string mode,
        LocalGgufBrainSettings settings,
        bool compactRetry)
    {
        if (compactRetry)
            return Math.Clamp(Math.Min(settings.MaxTokens, 192), 96, 192);

        return mode switch
        {
            BorinLocalPromptModes.ExternalConversation =>
                Math.Clamp(Math.Min(settings.MaxTokens, settings.ExternalConversationMaxTokens), 64, 160),
            BorinLocalPromptModes.NaturalInitiative =>
                Math.Clamp(Math.Min(settings.MaxTokens, settings.NaturalInitiativeMaxTokens), 64, 192),
            BorinLocalPromptModes.PrivateConversation =>
                Math.Clamp(Math.Min(settings.MaxTokens, settings.PrivateConversationMaxTokens), 128, 384),
            BorinLocalPromptModes.RepositoryResearch =>
                Math.Clamp(Math.Max(settings.MaxTokens, settings.RepositoryResearchMaxTokens), 512, 1000),
            BorinLocalPromptModes.EngineeringDraft =>
                Math.Clamp(Math.Max(settings.MaxTokens, settings.EngineeringDraftMaxTokens), 512, 1000),
            _ => settings.MaxTokens,
        };
    }
}

internal sealed class BorinPromptComposer
{
    private readonly StringBuilder _builder = new();
    private readonly int _maximumCharacters;

    public BorinPromptComposer(int maximumCharacters)
    {
        _maximumCharacters = Math.Max(1000, maximumCharacters);
    }

    public int Remaining => Math.Max(0, _maximumCharacters - _builder.Length);

    public void AppendRequired(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Remaining <= 0)
            return;

        var separatorCharacters = _builder.Length > 0 ? Environment.NewLine.Length * 2 : 0;
        var available = Math.Max(0, Remaining - separatorCharacters);
        var limited = BorinLocalRuntimeOptimization.LimitSection(text, available);
        if (_builder.Length > 0 && limited.Length > 0)
            _builder.AppendLine().AppendLine();
        _builder.Append(limited);
    }

    public void AppendSection(
        string title,
        string? summary,
        string? detail,
        int desiredCharacters,
        int minimumCharacters = 120)
    {
        if (Remaining < minimumCharacters || (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(detail)))
            return;

        var heading = string.IsNullOrWhiteSpace(summary)
            ? title.Trim()
            : $"{title.Trim()} ({summary.Trim()}):";
        var available = Math.Min(Remaining, Math.Max(minimumCharacters, desiredCharacters));
        var bodyBudget = Math.Max(0, available - heading.Length - 2);
        var body = BorinLocalRuntimeOptimization.LimitSection(detail, bodyBudget);
        var section = body.Length == 0 ? heading : heading + "\n" + body;
        AppendRequired(section);
    }

    public override string ToString() => _builder.ToString().Trim();
}
