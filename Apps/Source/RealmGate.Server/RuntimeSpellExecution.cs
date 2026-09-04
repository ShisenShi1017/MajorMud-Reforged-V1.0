namespace RealmGate.Server;

// PASS59 parity boundary: Reforged atomic rollback and multi-target ordering are compatibility behavior, not proof of stock MajorMUD native execution order. See RuntimeClassicCombatSpellNativeBoundaryAuthority.

public sealed record RuntimeSpellExecutionEvent(
    string EventId,
    string SpellId,
    string CasterId,
    RuntimeSpellTarget Target,
    string Operation,
    int Magnitude,
    DateTimeOffset OccurredAtUtc,
    string? ActiveEffectId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record RuntimeSpellExecutionResult(
    bool Succeeded,
    string? FailureCode,
    string? FailureMessage,
    RuntimeSpellCastResult Cast,
    IReadOnlyList<RuntimeSpellExecutionEvent> Events,
    IReadOnlyList<RuntimeActiveSpellEffect>? AppliedEffects = null);

public interface IRuntimeSpellTargetPolicy
{
    RuntimeSpellValidationIssue? ValidateTarget(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request);
}

public interface IRuntimeSpellOperationPolicy
{
    IReadOnlyList<RuntimeSpellExecutionEvent> ResolveOperations(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request, RuntimeActiveSpellEffect effect);
}

public interface IRuntimeSpellEventSink
{
    void Apply(RuntimeSpellExecutionEvent spellEvent);
    void Revert(RuntimeSpellExecutionEvent spellEvent);
}

public sealed class RuntimeSpellExecutionEngine
{
    private readonly RuntimeSpellCatalog _catalog;
    private readonly RuntimeSpellEngine _spells;
    private readonly IReadOnlyList<IRuntimeSpellTargetPolicy> _targetPolicies;
    private readonly IRuntimeSpellOperationPolicy _operations;
    private readonly IRuntimeSpellEventSink _sink;
    private readonly object _gate = new();

    public RuntimeSpellExecutionEngine(
        RuntimeSpellCatalog catalog,
        RuntimeSpellEngine spells,
        IRuntimeSpellOperationPolicy operations,
        IRuntimeSpellEventSink sink,
        IEnumerable<IRuntimeSpellTargetPolicy>? targetPolicies = null)
    {
        _catalog = catalog;
        _spells = spells;
        _operations = operations;
        _sink = sink;
        _targetPolicies = (targetPolicies ?? Array.Empty<IRuntimeSpellTargetPolicy>()).ToArray();
    }

    public RuntimeSpellExecutionResult Cast(RuntimeSpellCastRequest request)
    {
        lock (_gate)
        {
            var spell = _catalog.Require(request.SpellId);
            foreach (var policy in _targetPolicies)
            {
                var issue = policy.ValidateTarget(spell, request);
                if (issue is not null)
                {
                    var resources = _spells.Snapshot().Resources.GetValueOrDefault(request.CasterId, new RuntimeSpellResources(0, 0));
                    return new RuntimeSpellExecutionResult(false, issue.Code, issue.Message,
                        new RuntimeSpellCastResult(false, issue.Code, issue.Message, resources, null), Array.Empty<RuntimeSpellExecutionEvent>());
                }
            }

            var before = _spells.Snapshot();
            var cast = _spells.Cast(request);
            if (!cast.Succeeded || cast.Effect is null)
                return new RuntimeSpellExecutionResult(false, cast.FailureCode, cast.FailureMessage, cast, Array.Empty<RuntimeSpellExecutionEvent>());

            var events = _operations.ResolveOperations(spell, request, cast.Effect).ToArray();
            var applied = new List<RuntimeSpellExecutionEvent>();
            try
            {
                foreach (var spellEvent in events)
                {
                    _sink.Apply(spellEvent);
                    applied.Add(spellEvent);
                }
                return new RuntimeSpellExecutionResult(true, null, null, cast, events);
            }
            catch
            {
                for (var index = applied.Count - 1; index >= 0; index--)
                {
                    try { _sink.Revert(applied[index]); } catch { }
                }
                _spells.RestoreSnapshot(before);
                throw;
            }
        }
    }

    public RuntimeSpellExecutionResult CastMany(string casterId, string spellId, IReadOnlyList<RuntimeSpellTarget> targets, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (targets.Count == 0) throw new InvalidOperationException("SPELL_TARGET_SET_EMPTY");
            var spell = _catalog.Require(spellId);
            var requests = targets.DistinctBy(target => (target.Kind, target.Id)).Select(target => new RuntimeSpellCastRequest(casterId, spellId, target, nowUtc)).ToArray();
            foreach (var request in requests)
                foreach (var policy in _targetPolicies)
                    if (policy.ValidateTarget(spell, request) is { } issue)
                        return new RuntimeSpellExecutionResult(false, issue.Code, issue.Message,
                            new RuntimeSpellCastResult(false, issue.Code, issue.Message, _spells.Snapshot().Resources.GetValueOrDefault(casterId, new RuntimeSpellResources(0, 0)), null), [], []);

            var before = _spells.Snapshot();
            var primary = _spells.Cast(requests[0]);
            if (!primary.Succeeded || primary.Effect is null) return new(false, primary.FailureCode, primary.FailureMessage, primary, [], []);
            var effects = new List<RuntimeActiveSpellEffect> { primary.Effect };
            var events = new List<RuntimeSpellExecutionEvent>();
            var applied = new List<RuntimeSpellExecutionEvent>();
            try
            {
                for (var index = 0; index < requests.Length; index++)
                {
                    var effect = index == 0 ? primary.Effect : _spells.AddAdditionalTarget(requests[index]);
                    if (index > 0) effects.Add(effect);
                    foreach (var spellEvent in _operations.ResolveOperations(spell, requests[index], effect))
                    {
                        _sink.Apply(spellEvent); applied.Add(spellEvent); events.Add(spellEvent);
                    }
                }
                return new(true, null, null, primary, events, effects);
            }
            catch
            {
                for (var index = applied.Count - 1; index >= 0; index--) try { _sink.Revert(applied[index]); } catch { }
                _spells.RestoreSnapshot(before);
                throw;
            }
        }
    }

    public IReadOnlyList<RuntimeSpellExecutionEvent> Tick(DateTimeOffset nowUtc, Func<RuntimeActiveSpellEffect, IReadOnlyList<RuntimeSpellExecutionEvent>> periodicResolver)
    {
        lock (_gate)
        {
            var events = new List<RuntimeSpellExecutionEvent>();
            foreach (var effect in _spells.Snapshot().ActiveEffects.Where(x => x.ExpiresAtUtc is null || x.ExpiresAtUtc > nowUtc))
            {
                foreach (var spellEvent in periodicResolver(effect))
                {
                    _sink.Apply(spellEvent);
                    events.Add(spellEvent);
                }
            }
            _spells.Expire(nowUtc);
            return events;
        }
    }

    public RuntimeActiveSpellEffect Dispel(string effectId) => _spells.RemoveEffect(effectId);
}
