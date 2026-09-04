namespace RealmGate.Server;

public enum RuntimeSpellTargetKind
{
    Character,
    Npc,
    Item,
    Corpse,
    Room,
    Exit
}

public enum RuntimeSpellTargetMode
{
    Self,
    ExplicitSingle,
    Party,
    RoomCreatures,
    NoExplicitTarget,
    NativeModeUnresolved
}

public sealed record RuntimeSpellTargetPolicy(
    string PolicyId,
    string SpellId,
    RuntimeSpellTargetMode? TargetMode,
    IReadOnlySet<RuntimeSpellTargetKind> AllowedKinds,
    int? MaximumTargets,
    bool IncludeCaster,
    bool RequireSameRoom,
    bool RequireAlive,
    bool RequireVisible,
    bool AllowFriendly,
    bool AllowHostile,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeSpellTargetCandidate(
    string TargetId,
    RuntimeSpellTargetKind Kind,
    string RoomId,
    bool IsAlive,
    bool IsVisible,
    bool IsFriendlyToCaster,
    string? PartyId);

public sealed record RuntimeSpellTargetRequest(
    string CastId,
    string CasterId,
    string SpellId,
    string CasterRoomId,
    string? CasterPartyId,
    string? ExplicitTargetId,
    IReadOnlyList<RuntimeSpellTargetCandidate> Candidates);

public sealed record RuntimeSpellTargetDecision(
    string CastId,
    string SpellId,
    RuntimeSpellTargetMode Mode,
    IReadOnlyList<string> TargetIds,
    IReadOnlyList<string> Rejections,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeSpellTargeting
{
    private readonly IReadOnlyDictionary<string, RuntimeSpellTargetPolicy> _policies;

    public RuntimeSpellTargeting(IEnumerable<RuntimeSpellTargetPolicy> policies)
    {
        var materialized = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        foreach (var policy in materialized) ValidatePolicy(policy);
        _policies = materialized.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_policies.Count != materialized.Length) throw new InvalidOperationException("SPELL_TARGET_DUPLICATE_POLICY");
    }

    public RuntimeSpellTargetDecision Resolve(RuntimeSpellTargetRequest request)
    {
        RequireId(request.CastId, "SPELL_TARGET_CAST_ID_REQUIRED");
        RequireId(request.CasterId, "SPELL_TARGET_CASTER_ID_REQUIRED");
        RequireCanonicalSpell(request.SpellId);
        RequireCanonicalRoom(request.CasterRoomId);
        if (!_policies.TryGetValue(request.SpellId, out var policy))
            throw new InvalidOperationException("SPELL_TARGET_POLICY_UNAVAILABLE");
        if (policy.TargetMode is null || policy.TargetMode == RuntimeSpellTargetMode.NativeModeUnresolved)
            throw new InvalidOperationException("SPELL_TARGET_MODE_UNRESOLVED");

        var candidates = NormalizeCandidates(request.Candidates);
        var rejections = new List<string>();
        var selected = policy.TargetMode.Value switch
        {
            RuntimeSpellTargetMode.Self => ResolveSelf(request, candidates, policy, rejections),
            RuntimeSpellTargetMode.ExplicitSingle => ResolveExplicit(request, candidates, policy, rejections),
            RuntimeSpellTargetMode.Party => ResolveParty(request, candidates, policy, rejections),
            RuntimeSpellTargetMode.RoomCreatures => ResolveRoom(request, candidates, policy, rejections),
            RuntimeSpellTargetMode.NoExplicitTarget => Array.Empty<RuntimeSpellTargetCandidate>(),
            _ => throw new InvalidOperationException("SPELL_TARGET_MODE_UNRESOLVED")
        };

        var limited = ApplyMaximum(selected, policy.MaximumTargets);
        if (policy.TargetMode != RuntimeSpellTargetMode.NoExplicitTarget && limited.Count == 0)
            throw new InvalidOperationException("SPELL_TARGET_SET_EMPTY");

        return new RuntimeSpellTargetDecision(
            request.CastId,
            request.SpellId,
            policy.TargetMode.Value,
            limited.Select(x => x.TargetId).ToArray(),
            rejections.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            policy.EvidenceReferences);
    }

    private static IReadOnlyList<RuntimeSpellTargetCandidate> ResolveSelf(
        RuntimeSpellTargetRequest request,
        IReadOnlyList<RuntimeSpellTargetCandidate> candidates,
        RuntimeSpellTargetPolicy policy,
        List<string> rejections)
    {
        var self = candidates.FirstOrDefault(x => x.TargetId.Equals(request.CasterId, StringComparison.OrdinalIgnoreCase));
        if (self is null) throw new InvalidOperationException("SPELL_TARGET_CASTER_SNAPSHOT_REQUIRED");
        return Accept(self, request, policy, rejections) ? new[] { self } : Array.Empty<RuntimeSpellTargetCandidate>();
    }

    private static IReadOnlyList<RuntimeSpellTargetCandidate> ResolveExplicit(
        RuntimeSpellTargetRequest request,
        IReadOnlyList<RuntimeSpellTargetCandidate> candidates,
        RuntimeSpellTargetPolicy policy,
        List<string> rejections)
    {
        RequireId(request.ExplicitTargetId, "SPELL_TARGET_EXPLICIT_ID_REQUIRED");
        var candidate = candidates.FirstOrDefault(x => x.TargetId.Equals(request.ExplicitTargetId, StringComparison.OrdinalIgnoreCase));
        if (candidate is null) throw new InvalidOperationException("SPELL_TARGET_EXPLICIT_NOT_FOUND");
        return Accept(candidate, request, policy, rejections) ? new[] { candidate } : Array.Empty<RuntimeSpellTargetCandidate>();
    }

    private static IReadOnlyList<RuntimeSpellTargetCandidate> ResolveParty(
        RuntimeSpellTargetRequest request,
        IReadOnlyList<RuntimeSpellTargetCandidate> candidates,
        RuntimeSpellTargetPolicy policy,
        List<string> rejections)
    {
        RequireId(request.CasterPartyId, "SPELL_TARGET_PARTY_ID_REQUIRED");
        return candidates.Where(x => string.Equals(x.PartyId, request.CasterPartyId, StringComparison.OrdinalIgnoreCase))
            .Where(x => policy.IncludeCaster || !x.TargetId.Equals(request.CasterId, StringComparison.OrdinalIgnoreCase))
            .Where(x => Accept(x, request, policy, rejections)).ToArray();
    }

    private static IReadOnlyList<RuntimeSpellTargetCandidate> ResolveRoom(
        RuntimeSpellTargetRequest request,
        IReadOnlyList<RuntimeSpellTargetCandidate> candidates,
        RuntimeSpellTargetPolicy policy,
        List<string> rejections) =>
        candidates.Where(x => x.Kind is RuntimeSpellTargetKind.Character or RuntimeSpellTargetKind.Npc)
            .Where(x => policy.IncludeCaster || !x.TargetId.Equals(request.CasterId, StringComparison.OrdinalIgnoreCase))
            .Where(x => Accept(x, request, policy, rejections)).ToArray();

    private static bool Accept(RuntimeSpellTargetCandidate candidate, RuntimeSpellTargetRequest request,
        RuntimeSpellTargetPolicy policy, List<string> rejections)
    {
        string? reason = null;
        if (!policy.AllowedKinds.Contains(candidate.Kind)) reason = $"{candidate.TargetId}:kind";
        else if (policy.RequireSameRoom && !candidate.RoomId.Equals(request.CasterRoomId, StringComparison.OrdinalIgnoreCase)) reason = $"{candidate.TargetId}:room";
        else if (policy.RequireAlive && !candidate.IsAlive) reason = $"{candidate.TargetId}:dead";
        else if (policy.RequireVisible && !candidate.IsVisible) reason = $"{candidate.TargetId}:hidden";
        else if (candidate.IsFriendlyToCaster && !policy.AllowFriendly) reason = $"{candidate.TargetId}:friendly";
        else if (!candidate.IsFriendlyToCaster && !policy.AllowHostile) reason = $"{candidate.TargetId}:hostile";
        if (reason is null) return true;
        rejections.Add(reason);
        return false;
    }

    private static IReadOnlyList<RuntimeSpellTargetCandidate> NormalizeCandidates(IReadOnlyList<RuntimeSpellTargetCandidate> candidates)
    {
        var result = new List<RuntimeSpellTargetCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.OrderBy(x => x.TargetId, StringComparer.OrdinalIgnoreCase))
        {
            RequireCanonicalTarget(candidate.TargetId, candidate.Kind);
            RequireCanonicalRoom(candidate.RoomId);
            if (!seen.Add(candidate.TargetId)) throw new InvalidOperationException("SPELL_TARGET_DUPLICATE_CANDIDATE");
            result.Add(candidate);
        }
        return result;
    }

    private static IReadOnlyList<RuntimeSpellTargetCandidate> ApplyMaximum(IReadOnlyList<RuntimeSpellTargetCandidate> selected, int? maximum) =>
        maximum is null ? selected : selected.Take(maximum.Value).ToArray();

    private static void ValidatePolicy(RuntimeSpellTargetPolicy policy)
    {
        RequireCanonicalSpell(policy.SpellId);
        RequireId(policy.PolicyId, "SPELL_TARGET_POLICY_ID_REQUIRED");
        if (policy.AllowedKinds.Count == 0 && policy.TargetMode != RuntimeSpellTargetMode.NoExplicitTarget && policy.TargetMode is not null)
            throw new InvalidOperationException("SPELL_TARGET_ALLOWED_KINDS_REQUIRED");
        if (policy.MaximumTargets <= 0) throw new InvalidOperationException("SPELL_TARGET_MAXIMUM_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("SPELL_TARGET_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("SPELL_TARGET_FALLBACK_REQUIRED");
    }

    private static void RequireCanonicalSpell(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("spell.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_TARGET_CANONICAL_SPELL_ID_REQUIRED");
    }

    private static void RequireCanonicalRoom(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("room.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_TARGET_CANONICAL_ROOM_ID_REQUIRED");
    }

    private static void RequireCanonicalTarget(string id, RuntimeSpellTargetKind kind)
    {
        var prefix = kind switch
        {
            RuntimeSpellTargetKind.Character => "character.",
            RuntimeSpellTargetKind.Npc => "npc.",
            RuntimeSpellTargetKind.Item => "item.",
            RuntimeSpellTargetKind.Corpse => "corpse.",
            RuntimeSpellTargetKind.Room => "room.",
            RuntimeSpellTargetKind.Exit => "exit.",
            _ => throw new InvalidOperationException("SPELL_TARGET_KIND_UNRESOLVED")
        };
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_TARGET_CANONICAL_TARGET_ID_REQUIRED");
    }

    private static void RequireId(string? value, string error)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(error);
    }
}
