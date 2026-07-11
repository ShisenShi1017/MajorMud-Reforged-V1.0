namespace Reforged.Server;

public sealed record RuntimeCombatThreatEntry(string SourceId, string TargetId, long Threat);

public interface IRuntimeCombatThreatPolicy
{
    IReadOnlyList<RuntimeCombatThreatEntry> Project(
        RuntimeCombatSnapshot before,
        IReadOnlyList<RuntimeCombatEvent> events,
        IReadOnlyCollection<RuntimeCombatThreatEntry> current);
}

public interface IRuntimeCombatAiPolicy
{
    RuntimeCombatAction? SelectAction(
        RuntimeCombatSnapshot snapshot,
        RuntimeCombatParticipant participant,
        IReadOnlyCollection<RuntimeCombatThreatEntry> threat,
        DateTimeOffset nowUtc);
}

public interface IRuntimeCombatRoundEffectSource
{
    IReadOnlyList<RuntimeCombatEvent> ResolveRoundStart(RuntimeCombatSnapshot snapshot, DateTimeOffset nowUtc);
}

public interface IRuntimeCombatRewardHandler
{
    IReadOnlyList<RuntimeCombatEvent> ResolveRewards(RuntimeCombatSnapshot snapshot, DateTimeOffset nowUtc);
}

public sealed record RuntimeCombatAdvancedSnapshot(
    RuntimeCombatSnapshot Combat,
    IReadOnlyList<RuntimeCombatThreatEntry> Threat,
    bool RewardsApplied);

public sealed class RuntimeCombatCoordinator
{
    private readonly object _gate = new();
    private readonly RuntimeCombatSession _session;
    private readonly IRuntimeCombatThreatPolicy _threatPolicy;
    private readonly IRuntimeCombatAiPolicy _aiPolicy;
    private readonly IRuntimeCombatRoundEffectSource _roundEffects;
    private readonly IRuntimeCombatRewardHandler _rewards;
    private readonly IRuntimeCombatEventSink _sink;
    private readonly List<RuntimeCombatThreatEntry> _threat = new();
    private bool _rewardsApplied;

    public RuntimeCombatCoordinator(
        RuntimeCombatSession session,
        IRuntimeCombatThreatPolicy threatPolicy,
        IRuntimeCombatAiPolicy aiPolicy,
        IRuntimeCombatRoundEffectSource roundEffects,
        IRuntimeCombatRewardHandler rewards,
        IRuntimeCombatEventSink sink)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _threatPolicy = threatPolicy ?? throw new ArgumentNullException(nameof(threatPolicy));
        _aiPolicy = aiPolicy ?? throw new ArgumentNullException(nameof(aiPolicy));
        _roundEffects = roundEffects ?? throw new ArgumentNullException(nameof(roundEffects));
        _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public RuntimeCombatAdvancedSnapshot Capture()
    {
        lock (_gate)
            return new RuntimeCombatAdvancedSnapshot(_session.Capture(), _threat.ToArray(), _rewardsApplied);
    }

    public void Restore(RuntimeCombatAdvancedSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            _session.Restore(snapshot.Combat);
            _threat.Clear();
            _threat.AddRange(snapshot.Threat);
            _rewardsApplied = snapshot.RewardsApplied;
        }
    }

    public IReadOnlyList<RuntimeCombatEvent> BeginRound(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var before = Capture();
            var applied = new List<RuntimeCombatEvent>();
            try
            {
                _session.BeginRound();
                foreach (var combatEvent in _roundEffects.ResolveRoundStart(_session.Capture(), nowUtc))
                {
                    _sink.Apply(combatEvent);
                    applied.Add(combatEvent);
                }
                return applied;
            }
            catch
            {
                for (var i = applied.Count - 1; i >= 0; i--) { try { _sink.Revert(applied[i]); } catch { } }
                Restore(before);
                throw;
            }
        }
    }

    public RuntimeCombatActionResult Execute(RuntimeCombatAction action, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var before = Capture();
            RuntimeCombatActionResult? result = null;
            try
            {
                result = _session.Execute(action, nowUtc);
                if (!result.Succeeded) return result;
                ReplaceThreat(_threatPolicy.Project(before.Combat, result.Events, _threat));
                ApplyRewardsIfComplete(nowUtc);
                return result;
            }
            catch
            {
                if (result is not null)
                {
                    for (var i = result.Events.Count - 1; i >= 0; i--)
                    {
                        try { _sink.Revert(result.Events[i]); } catch { }
                    }
                }
                Restore(before);
                throw;
            }
        }
    }

    public IReadOnlyList<RuntimeCombatActionResult> ExecuteNpcTurns(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var results = new List<RuntimeCombatActionResult>();
            var snapshot = _session.Capture();
            foreach (var participantId in snapshot.InitiativeOrder)
            {
                var current = _session.Capture();
                if (current.State != RuntimeCombatState.Active) break;
                var participant = current.Participants.FirstOrDefault(x =>
                    x.ParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase));
                if (participant is null || !participant.IsAlive || participant.Kind != RuntimeCombatParticipantKind.Npc)
                    continue;
                var action = _aiPolicy.SelectAction(current, participant, _threat, nowUtc);
                if (action is null) continue;
                results.Add(Execute(action, nowUtc));
            }
            return results;
        }
    }

    public IReadOnlyList<string> Validate()
    {
        lock (_gate)
        {
            var errors = _session.Validate().ToList();
            var participantIds = _session.Capture().Participants.Select(x => x.ParticipantId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (_threat.Any(x => x.Threat < 0)) errors.Add("negative-threat");
            if (_threat.Any(x => !participantIds.Contains(x.SourceId) || !participantIds.Contains(x.TargetId))) errors.Add("invalid-threat-reference");
            if (_threat.GroupBy(x => (x.SourceId.ToUpperInvariant(), x.TargetId.ToUpperInvariant())).Any(x => x.Count() > 1)) errors.Add("duplicate-threat-entry");
            if (_rewardsApplied && _session.Capture().State == RuntimeCombatState.Active) errors.Add("rewards-applied-before-completion");
            return errors;
        }
    }

    private void ApplyRewardsIfComplete(DateTimeOffset nowUtc)
    {
        var snapshot = _session.Capture();
        if (_rewardsApplied || snapshot.State != RuntimeCombatState.Completed) return;
        var applied = new List<RuntimeCombatEvent>();
        try
        {
            foreach (var combatEvent in _rewards.ResolveRewards(snapshot, nowUtc))
            {
                _sink.Apply(combatEvent);
                applied.Add(combatEvent);
            }
            _rewardsApplied = true;
        }
        catch
        {
            for (var i = applied.Count - 1; i >= 0; i--) { try { _sink.Revert(applied[i]); } catch { } }
            throw;
        }
    }

    private void ReplaceThreat(IReadOnlyList<RuntimeCombatThreatEntry> projected)
    {
        _threat.Clear();
        _threat.AddRange(projected);
    }
}
