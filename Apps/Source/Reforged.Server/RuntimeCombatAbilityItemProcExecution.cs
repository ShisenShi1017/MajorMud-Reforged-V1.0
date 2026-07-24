namespace Reforged.Server;

public enum RuntimeCombatAbilityKind { PassiveModifier, ActivatedAttack, ActivatedSpell, Defensive }
public enum RuntimeItemProcTrigger { OnHit, OnCast, OnBeingHit, OnKill, Defensive }
public enum RuntimeProcDisposition { Damage, Healing, ApplyStatus, ResourceDelta, ThreatDelta }

public sealed record RuntimeCombatAbilityPolicy(
    string PolicyId, string AbilityId, RuntimeCombatAbilityKind Kind,
    string? RequiredAttackPath, string? RequiredItemId, int ResourceCost,
    int CooldownTicks, int Priority, RuntimeResolutionConfidence Confidence,
    IReadOnlyList<string> EvidenceReferences, string SafeFallback);

public sealed record RuntimeItemProcPolicy(
    string PolicyId, string ItemId, RuntimeItemProcTrigger Trigger,
    RuntimeProcDisposition Disposition, int ChanceBasisPoints, int Priority,
    int Magnitude, string? SpellId, string? EffectId,
    RuntimeResolutionConfidence Confidence, IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeCombatAbilityRequest(
    string ExecutionId, string CombatId, string AbilityId, string ActorId,
    string TargetId, string? AttackPath, string? EquippedItemId,
    int AvailableResource, long CurrentTick, long CooldownReadyTick,
    IReadOnlyList<RuntimeItemProcPolicy> CandidateProcs, int RandomSeed);

public sealed record RuntimeCombatAbilityDecision(
    string ExecutionId, bool Accepted, int ResourceConsumed, long NextCooldownTick,
    IReadOnlyList<RuntimeItemProcDecision> ProcDecisions,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeItemProcDecision(
    string PolicyId, string ItemId, RuntimeItemProcTrigger Trigger,
    bool Triggered, RuntimeProcDisposition Disposition, int Magnitude,
    string? SpellId, string? EffectId, int Roll,
    IReadOnlyList<string> EvidenceReferences);

public interface IRuntimeCombatAbilitySink
{
    void Prepare(RuntimeCombatAbilityDecision decision);
    void Commit(RuntimeCombatAbilityDecision decision);
    void Rollback(RuntimeCombatAbilityDecision decision);
}

public sealed class RuntimeCombatAbilityItemProcCoordinator
{
    private readonly IReadOnlyDictionary<string, RuntimeCombatAbilityPolicy> _abilities;
    private readonly HashSet<string> _executions = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatAbilityItemProcCoordinator(IEnumerable<RuntimeCombatAbilityPolicy> abilities)
    {
        var all = (abilities ?? throw new ArgumentNullException(nameof(abilities))).ToArray();
        _abilities = all.ToDictionary(x => x.AbilityId, StringComparer.OrdinalIgnoreCase);
        if (_abilities.Count != all.Length) throw new InvalidOperationException("ABILITY_DUPLICATE_POLICY");
        foreach (var policy in all) ValidateAbilityPolicy(policy);
    }

    public RuntimeCombatAbilityDecision Execute(RuntimeCombatAbilityRequest request, IReadOnlyList<IRuntimeCombatAbilitySink> sinks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sinks);
        RequireCanonical(request.ExecutionId, @"^abilityexecution\.\d+$", "ABILITY_EXECUTION_ID_INVALID");
        RequireCanonical(request.CombatId, @"^combat\.\d+$", "ABILITY_COMBAT_ID_INVALID");
        RequireCanonical(request.AbilityId, @"^ability\.\d+$", "ABILITY_ID_INVALID");
        RequireActor(request.ActorId); RequireActor(request.TargetId);
        if (!_abilities.TryGetValue(request.AbilityId, out var ability)) throw new InvalidOperationException("ABILITY_POLICY_REQUIRED");
        if (ability.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("ABILITY_POLICY_NOT_PROMOTED");
        if (!_executions.Add(request.ExecutionId)) throw new InvalidOperationException("ABILITY_EXECUTION_DUPLICATE");
        try
        {
            if (request.CurrentTick < request.CooldownReadyTick) throw new InvalidOperationException("ABILITY_COOLDOWN_ACTIVE");
            if (request.AvailableResource < ability.ResourceCost) throw new InvalidOperationException("ABILITY_RESOURCE_INSUFFICIENT");
            if (ability.RequiredAttackPath is not null && !string.Equals(ability.RequiredAttackPath, request.AttackPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ABILITY_ATTACK_PATH_INVALID");
            if (ability.RequiredItemId is not null && !string.Equals(ability.RequiredItemId, request.EquippedItemId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ABILITY_REQUIRED_ITEM_INVALID");

            var procs = ResolveProcs(request.CandidateProcs, request.RandomSeed);
            var evidence = ability.EvidenceReferences.Concat(procs.SelectMany(x => x.EvidenceReferences))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var decision = new RuntimeCombatAbilityDecision(request.ExecutionId, true, ability.ResourceCost,
                checked(request.CurrentTick + ability.CooldownTicks), procs, evidence);

            foreach (var sink in sinks) sink.Prepare(decision);
            var committed = new List<IRuntimeCombatAbilitySink>();
            try
            {
                foreach (var sink in sinks) { sink.Commit(decision); committed.Add(sink); }
            }
            catch
            {
                for (var i = committed.Count - 1; i >= 0; i--) committed[i].Rollback(decision);
                throw;
            }
            return decision;
        }
        catch { _executions.Remove(request.ExecutionId); throw; }
    }

    private static IReadOnlyList<RuntimeItemProcDecision> ResolveProcs(IReadOnlyList<RuntimeItemProcPolicy> candidates, int seed)
    {
        if (candidates is null) throw new InvalidOperationException("PROC_CANDIDATES_REQUIRED");
        var ordered = candidates.OrderBy(x => x.Priority).ThenBy(x => x.PolicyId, StringComparer.OrdinalIgnoreCase).ToArray();
        if (ordered.Select(x => x.PolicyId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ordered.Length)
            throw new InvalidOperationException("PROC_DUPLICATE_POLICY");
        var rng = new Random(seed); var results = new List<RuntimeItemProcDecision>();
        foreach (var policy in ordered)
        {
            ValidateProcPolicy(policy);
            if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("PROC_POLICY_NOT_PROMOTED");
            var roll = rng.Next(1, 10001); var triggered = roll <= policy.ChanceBasisPoints;
            results.Add(new RuntimeItemProcDecision(policy.PolicyId, policy.ItemId, policy.Trigger, triggered,
                policy.Disposition, triggered ? policy.Magnitude : 0, policy.SpellId, policy.EffectId, roll,
                policy.EvidenceReferences));
        }
        return results;
    }

    private static void ValidateAbilityPolicy(RuntimeCombatAbilityPolicy p)
    {
        RequireCanonical(p.PolicyId, @"^abilitypolicy\.\d+$", "ABILITY_POLICY_ID_INVALID");
        RequireCanonical(p.AbilityId, @"^ability\.\d+$", "ABILITY_ID_INVALID");
        if (p.RequiredItemId is not null) RequireCanonical(p.RequiredItemId, @"^item\.\d+$", "ABILITY_ITEM_ID_INVALID");
        if (p.ResourceCost < 0 || p.CooldownTicks < 0 || p.EvidenceReferences.Count == 0 || !p.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ABILITY_POLICY_INVALID");
    }

    private static void ValidateProcPolicy(RuntimeItemProcPolicy p)
    {
        RequireCanonical(p.PolicyId, @"^procpolicy\.\d+$", "PROC_POLICY_ID_INVALID");
        RequireCanonical(p.ItemId, @"^item\.\d+$", "PROC_ITEM_ID_INVALID");
        if (p.SpellId is not null) RequireCanonical(p.SpellId, @"^spell\.\d+$", "PROC_SPELL_ID_INVALID");
        if (p.EffectId is not null) RequireCanonical(p.EffectId, @"^effect\.\d+$", "PROC_EFFECT_ID_INVALID");
        if (p.ChanceBasisPoints is < 0 or > 10000 || p.EvidenceReferences.Count == 0 || !p.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PROC_POLICY_INVALID");
    }

    private static void RequireActor(string id) => RequireCanonical(id, @"^(character|npc)\.\d+$", "ABILITY_ACTOR_ID_INVALID");
    private static void RequireCanonical(string value, string pattern, string error)
    { if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, pattern)) throw new InvalidOperationException(error); }
}
