namespace RealmGate.Server;

public enum RuntimePassiveModifierKind { Attack, Defense, Initiative, Speed, Resistance, ResourceCost, Cooldown }
public enum RuntimeControlBreakReason { ControllerDeath, TargetDeath, RoomSeparation, Dispel, DurationExpired, Logout, Disconnect, WorldReset, Removal }
public enum RuntimeControlPersistenceDisposition { TemporaryOnly, PersistUntilValidatedRemoval }

public sealed record RuntimePassiveCombatAbilityPolicy(
    string PolicyId, string AbilityId, RuntimePassiveModifierKind Kind,
    int FlatModifier, int BasisPointModifier, int Priority,
    string? RequiredAttackPath, string? RequiredItemId,
    RuntimeResolutionConfidence Confidence, IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimePassiveCombatAbilityRequest(
    string ActorId, string? AttackPath, IReadOnlyList<string> ActiveAbilityIds,
    IReadOnlyList<string> EquippedItemIds, int BaseValue);

public sealed record RuntimePassiveCombatAbilityContribution(
    string PolicyId, string AbilityId, RuntimePassiveModifierKind Kind,
    int FlatModifier, int BasisPointModifier, int IntermediateValue,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimePassiveCombatAbilityDecision(
    string ActorId, RuntimePassiveModifierKind Kind, int BaseValue, int FinalValue,
    IReadOnlyList<RuntimePassiveCombatAbilityContribution> Contributions,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeControlLifecyclePolicy(
    string PolicyId, string SpellId, RuntimeControlEffectKind Kind,
    bool BreaksOnControllerDeath, bool BreaksOnTargetDeath, bool BreaksOnRoomSeparation,
    bool BreaksOnDispel, bool BreaksOnDurationExpiration, bool BreaksOnLogout,
    bool BreaksOnDisconnect, bool BreaksOnWorldReset, bool BreaksOnRemoval,
    RuntimeControlPersistenceDisposition PersistenceDisposition,
    RuntimeResolutionConfidence Confidence, IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeControlLifecycleRequest(
    string LifecycleEventId, string SpellId, string ControllerId, string TargetId,
    RuntimeControlBreakReason Reason, bool SameRoom, bool ControllerLiving,
    bool TargetLiving, bool PersistenceAvailable);

public sealed record RuntimeControlLifecycleDecision(
    string LifecycleEventId, string SpellId, RuntimeControlEffectKind Kind,
    bool BreakControl, bool RemoveEffect, bool ClearControllerOwnership,
    bool PersistControl, RuntimeControlBreakReason Reason,
    IReadOnlyList<string> EvidenceReferences);

public interface IRuntimeControlLifecycleSink
{
    void Prepare(RuntimeControlLifecycleDecision decision);
    void Commit(RuntimeControlLifecycleDecision decision);
    void Rollback(RuntimeControlLifecycleDecision decision);
}

public sealed class RuntimePassiveAbilityControlLifecycleCoordinator
{
    private readonly IReadOnlyDictionary<string, RuntimePassiveCombatAbilityPolicy[]> _passiveByAbility;
    private readonly IReadOnlyDictionary<string, RuntimeControlLifecyclePolicy> _controlBySpell;
    private readonly HashSet<string> _lifecycleEvents = new(StringComparer.OrdinalIgnoreCase);

    public RuntimePassiveAbilityControlLifecycleCoordinator(
        IEnumerable<RuntimePassiveCombatAbilityPolicy> passivePolicies,
        IEnumerable<RuntimeControlLifecyclePolicy> controlPolicies)
    {
        var passive = (passivePolicies ?? throw new ArgumentNullException(nameof(passivePolicies))).ToArray();
        var controls = (controlPolicies ?? throw new ArgumentNullException(nameof(controlPolicies))).ToArray();
        foreach (var policy in passive) ValidatePassivePolicy(policy);
        foreach (var policy in controls) ValidateControlPolicy(policy);
        if (passive.Select(x => x.PolicyId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != passive.Length)
            throw new InvalidOperationException("PASSIVE_ABILITY_DUPLICATE_POLICY");
        _passiveByAbility = passive.GroupBy(x => x.AbilityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(p => p.Priority).ThenBy(p => p.PolicyId, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        _controlBySpell = controls.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_controlBySpell.Count != controls.Length) throw new InvalidOperationException("CONTROL_LIFECYCLE_DUPLICATE_POLICY");
    }

    public RuntimePassiveCombatAbilityDecision ResolvePassive(
        RuntimePassiveCombatAbilityRequest request, RuntimePassiveModifierKind kind)
    {
        ArgumentNullException.ThrowIfNull(request); RequireActor(request.ActorId);
        if (request.ActiveAbilityIds is null) throw new InvalidOperationException("PASSIVE_ACTIVE_ABILITIES_REQUIRED");
        if (request.EquippedItemIds is null) throw new InvalidOperationException("PASSIVE_EQUIPPED_ITEMS_REQUIRED");
        if (request.ActiveAbilityIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.ActiveAbilityIds.Count)
            throw new InvalidOperationException("PASSIVE_ACTIVE_ABILITY_DUPLICATE");
        foreach (var abilityId in request.ActiveAbilityIds) RequireCanonical(abilityId, @"^ability\.\d+$", "PASSIVE_ABILITY_ID_INVALID");
        foreach (var itemId in request.EquippedItemIds) RequireCanonical(itemId, @"^item\.\d+$", "PASSIVE_ITEM_ID_INVALID");

        var current = request.BaseValue;
        var contributions = new List<RuntimePassiveCombatAbilityContribution>();
        foreach (var abilityId in request.ActiveAbilityIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (!_passiveByAbility.TryGetValue(abilityId, out var policies))
                throw new InvalidOperationException("PASSIVE_ABILITY_POLICY_REQUIRED");
            foreach (var policy in policies.Where(x => x.Kind == kind))
            {
                if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
                    throw new InvalidOperationException("PASSIVE_ABILITY_POLICY_NOT_PROMOTED");
                if (policy.RequiredAttackPath is not null && !string.Equals(policy.RequiredAttackPath, request.AttackPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (policy.RequiredItemId is not null && !request.EquippedItemIds.Contains(policy.RequiredItemId, StringComparer.OrdinalIgnoreCase))
                    continue;
                var afterFlat = checked(current + policy.FlatModifier);
                var scaled = checked((long)afterFlat * checked(10000 + policy.BasisPointModifier));
                current = checked((int)(scaled / 10000));
                contributions.Add(new RuntimePassiveCombatAbilityContribution(policy.PolicyId, policy.AbilityId,
                    policy.Kind, policy.FlatModifier, policy.BasisPointModifier, current, policy.EvidenceReferences));
            }
        }
        return new RuntimePassiveCombatAbilityDecision(request.ActorId, kind, request.BaseValue, current,
            contributions, contributions.SelectMany(x => x.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public RuntimeControlLifecycleDecision ResolveControlLifecycle(
        RuntimeControlLifecycleRequest request, IReadOnlyList<IRuntimeControlLifecycleSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(sinks);
        RequireCanonical(request.LifecycleEventId, @"^controllifecycle\.\d+$", "CONTROL_LIFECYCLE_EVENT_ID_INVALID");
        RequireCanonical(request.SpellId, @"^spell\.\d+$", "CONTROL_LIFECYCLE_SPELL_ID_INVALID");
        RequireActor(request.ControllerId); RequireActor(request.TargetId);
        if (!_controlBySpell.TryGetValue(request.SpellId, out var policy)) throw new InvalidOperationException("CONTROL_LIFECYCLE_POLICY_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("CONTROL_LIFECYCLE_POLICY_NOT_PROMOTED");
        if (!_lifecycleEvents.Add(request.LifecycleEventId)) throw new InvalidOperationException("CONTROL_LIFECYCLE_EVENT_DUPLICATE");
        try
        {
            var shouldBreak = request.Reason switch
            {
                RuntimeControlBreakReason.ControllerDeath => policy.BreaksOnControllerDeath && !request.ControllerLiving,
                RuntimeControlBreakReason.TargetDeath => policy.BreaksOnTargetDeath && !request.TargetLiving,
                RuntimeControlBreakReason.RoomSeparation => policy.BreaksOnRoomSeparation && !request.SameRoom,
                RuntimeControlBreakReason.Dispel => policy.BreaksOnDispel,
                RuntimeControlBreakReason.DurationExpired => policy.BreaksOnDurationExpiration,
                RuntimeControlBreakReason.Logout => policy.BreaksOnLogout,
                RuntimeControlBreakReason.Disconnect => policy.BreaksOnDisconnect,
                RuntimeControlBreakReason.WorldReset => policy.BreaksOnWorldReset,
                RuntimeControlBreakReason.Removal => policy.BreaksOnRemoval,
                _ => throw new InvalidOperationException("CONTROL_LIFECYCLE_REASON_UNKNOWN")
            };
            var persist = !shouldBreak && policy.PersistenceDisposition == RuntimeControlPersistenceDisposition.PersistUntilValidatedRemoval;
            if (persist && !request.PersistenceAvailable) throw new InvalidOperationException("CONTROL_LIFECYCLE_PERSISTENCE_UNAVAILABLE");
            var decision = new RuntimeControlLifecycleDecision(request.LifecycleEventId, request.SpellId, policy.Kind,
                shouldBreak, shouldBreak, shouldBreak, persist, request.Reason, policy.EvidenceReferences);
            foreach (var sink in sinks) sink.Prepare(decision);
            var committed = new List<IRuntimeControlLifecycleSink>();
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
        catch { _lifecycleEvents.Remove(request.LifecycleEventId); throw; }
    }

    private static void ValidatePassivePolicy(RuntimePassiveCombatAbilityPolicy p)
    {
        RequireCanonical(p.PolicyId, @"^passiveabilitypolicy\.\d+$", "PASSIVE_POLICY_ID_INVALID");
        RequireCanonical(p.AbilityId, @"^ability\.\d+$", "PASSIVE_ABILITY_ID_INVALID");
        if (p.RequiredItemId is not null) RequireCanonical(p.RequiredItemId, @"^item\.\d+$", "PASSIVE_REQUIRED_ITEM_ID_INVALID");
        if (p.BasisPointModifier < -10000 || p.EvidenceReferences.Count == 0 || !p.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PASSIVE_POLICY_INVALID");
    }
    private static void ValidateControlPolicy(RuntimeControlLifecyclePolicy p)
    {
        RequireCanonical(p.PolicyId, @"^controllifecyclepolicy\.\d+$", "CONTROL_LIFECYCLE_POLICY_ID_INVALID");
        RequireCanonical(p.SpellId, @"^spell\.\d+$", "CONTROL_LIFECYCLE_SPELL_ID_INVALID");
        if (p.EvidenceReferences.Count == 0 || !p.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CONTROL_LIFECYCLE_POLICY_INVALID");
    }
    private static void RequireActor(string id) => RequireCanonical(id, @"^(character|npc)\.\d+$", "PASSIVE_CONTROL_ACTOR_ID_INVALID");
    private static void RequireCanonical(string value, string pattern, string error)
    { if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, pattern)) throw new InvalidOperationException(error); }
}
