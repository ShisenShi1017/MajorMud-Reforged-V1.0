using System.Text;

namespace Reforged.Server;

public enum RuntimeHelpTopicKind { General, Command, Ability, Spell, Item, Quest, Administrative }
public enum RuntimeHelpAccess { Player, Builder, Administrator, Host }
public enum RuntimeHelpContentOwner { Module, Host, Generated, Librarium }

public sealed record RuntimeHelpTopicDefinition(
    string TopicId,
    string Title,
    string Body,
    RuntimeHelpTopicKind Kind,
    RuntimeHelpAccess MinimumAccess,
    RuntimeHelpContentOwner ContentOwner,
    IReadOnlyCollection<string> Aliases,
    IReadOnlyCollection<string> RelatedTopicIds,
    string? SourceTextblockId = null,
    bool Enabled = true);

public sealed record RuntimeHelpActor(string ActorId, RuntimeHelpAccess Access, bool Authenticated = true);
public sealed record RuntimeHelpTopicView(string TopicId, string Title, string Body, RuntimeHelpTopicKind Kind, RuntimeHelpContentOwner ContentOwner, IReadOnlyList<string> RelatedTopicIds, string? SourceTextblockId);
public sealed record RuntimeHelpSearchHit(string TopicId, string Title, RuntimeHelpTopicKind Kind, int Score);
public sealed record RuntimeHelpResult(bool Success, string Code, RuntimeHelpTopicView? Topic, IReadOnlyList<RuntimeHelpSearchHit> Hits);

public sealed class RuntimeHelpReferenceService
{
    private const int MaximumQueryLength = 128;
    private const int MaximumRenderedBodyLength = 32_768;
    private readonly Dictionary<string, RuntimeHelpTopicDefinition> _topics;
    private readonly Dictionary<string, string> _aliases;

    public RuntimeHelpReferenceService(IEnumerable<RuntimeHelpTopicDefinition> definitions)
    {
        _topics = new(StringComparer.OrdinalIgnoreCase);
        _aliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_topics.TryAdd(definition.TopicId, definition)) throw new ArgumentException($"duplicate-topic:{definition.TopicId}", nameof(definitions));
            RegisterAlias(definition.TopicId, definition.TopicId);
            RegisterAlias(definition.Title, definition.TopicId);
            foreach (var alias in definition.Aliases) RegisterAlias(alias, definition.TopicId);
        }
        foreach (var definition in _topics.Values)
            foreach (var related in definition.RelatedTopicIds)
                if (!_topics.ContainsKey(related)) throw new ArgumentException($"missing-related-topic:{definition.TopicId}:{related}", nameof(definitions));
    }

    public RuntimeHelpResult Resolve(RuntimeHelpActor actor, string query)
    {
        var gate = ValidateRequest(actor, query);
        if (gate is not null) return gate;
        var canonical = Canonicalize(query);
        if (_aliases.TryGetValue(canonical, out var topicId) && _topics.TryGetValue(topicId, out var exact))
            return CanView(actor, exact) ? Success(exact) : Fail("help-topic-not-available");
        var hits = SearchVisible(actor, canonical, 10);
        if (hits.Count == 1) return Success(_topics[hits[0].TopicId]);
        return new(hits.Count > 0, hits.Count > 0 ? "help-search-results" : "help-topic-not-found", null, hits);
    }

    public RuntimeHelpResult Search(RuntimeHelpActor actor, string query, int maximumResults = 20)
    {
        var gate = ValidateRequest(actor, query);
        if (gate is not null) return gate;
        if (maximumResults is < 1 or > 100) return Fail("help-result-limit-invalid");
        var hits = SearchVisible(actor, Canonicalize(query), maximumResults);
        return new(true, hits.Count > 0 ? "help-search-results" : "help-search-empty", null, hits);
    }

    public IReadOnlyList<string> ValidateCatalog()
    {
        var issues = new List<string>();
        foreach (var topic in _topics.Values)
        {
            foreach (var related in topic.RelatedTopicIds)
                if (!_topics.ContainsKey(related)) issues.Add($"missing-related-topic:{topic.TopicId}:{related}");
            if (topic.Body.Length > MaximumRenderedBodyLength) issues.Add($"body-too-long:{topic.TopicId}");
        }
        return issues;
    }

    private IReadOnlyList<RuntimeHelpSearchHit> SearchVisible(RuntimeHelpActor actor, string query, int maximumResults)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _topics.Values.Where(t => CanView(actor, t)).Select(t =>
        {
            var title = Canonicalize(t.Title);
            var id = Canonicalize(t.TopicId);
            var aliases = t.Aliases.Select(Canonicalize).ToArray();
            var score = 0;
            if (id == query || title == query || aliases.Contains(query, StringComparer.OrdinalIgnoreCase)) score += 1000;
            if (id.StartsWith(query, StringComparison.OrdinalIgnoreCase) || title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 250;
            foreach (var token in tokens)
            {
                if (id.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 80;
                if (title.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 100;
                if (aliases.Any(a => a.Contains(token, StringComparison.OrdinalIgnoreCase))) score += 60;
            }
            return new RuntimeHelpSearchHit(t.TopicId, t.Title, t.Kind, score);
        }).Where(h => h.Score > 0)
          .OrderByDescending(h => h.Score)
          .ThenBy(h => h.TopicId, StringComparer.OrdinalIgnoreCase)
          .Take(maximumResults).ToArray();
    }

    private static RuntimeHelpResult? ValidateRequest(RuntimeHelpActor actor, string query)
    {
        if (!actor.Authenticated) return Fail("help-authentication-required");
        if (string.IsNullOrWhiteSpace(actor.ActorId)) return Fail("help-actor-id-required");
        if (string.IsNullOrWhiteSpace(query)) return Fail("help-query-required");
        if (query.Length > MaximumQueryLength) return Fail("help-query-too-long");
        if (query.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) return Fail("help-query-invalid");
        return null;
    }

    private static bool CanView(RuntimeHelpActor actor, RuntimeHelpTopicDefinition topic) => topic.Enabled && actor.Access >= topic.MinimumAccess;
    private static RuntimeHelpResult Success(RuntimeHelpTopicDefinition topic) => new(true, "help-topic-resolved", new(topic.TopicId, topic.Title, Render(topic.Body), topic.Kind, topic.ContentOwner, topic.RelatedTopicIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), topic.SourceTextblockId), Array.Empty<RuntimeHelpSearchHit>());
    private static RuntimeHelpResult Fail(string code) => new(false, code, null, Array.Empty<RuntimeHelpSearchHit>());
    private static string Render(string body)
    {
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
        return normalized.Length <= MaximumRenderedBodyLength ? normalized : normalized[..MaximumRenderedBodyLength];
    }
    private static string Canonicalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '_') { pendingSpace = builder.Length > 0; continue; }
            if (pendingSpace) { builder.Append(' '); pendingSpace = false; }
            if (char.IsLetterOrDigit(ch) || ch is '?' or '.') builder.Append(ch);
        }
        return builder.ToString();
    }
    private void RegisterAlias(string alias, string topicId)
    {
        var canonical = Canonicalize(alias);
        if (string.IsNullOrWhiteSpace(canonical)) throw new ArgumentException($"empty-alias:{topicId}");
        if (_aliases.TryGetValue(canonical, out var existing) && !string.Equals(existing, topicId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"duplicate-alias:{canonical}:{existing}:{topicId}");
        _aliases[canonical] = topicId;
    }
    private static void ValidateDefinition(RuntimeHelpTopicDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.TopicId)) throw new ArgumentException("topic-id-required");
        if (string.IsNullOrWhiteSpace(definition.Title)) throw new ArgumentException($"topic-title-required:{definition.TopicId}");
        if (string.IsNullOrWhiteSpace(definition.Body)) throw new ArgumentException($"topic-body-required:{definition.TopicId}");
        if (definition.Body.Length > MaximumRenderedBodyLength) throw new ArgumentException($"topic-body-too-long:{definition.TopicId}");
    }
}
