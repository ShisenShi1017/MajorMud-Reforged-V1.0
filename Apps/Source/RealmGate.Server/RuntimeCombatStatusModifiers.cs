namespace RealmGate.Server;

public enum RuntimeCombatStatusAction
{
    None,
    PhysicalAttack,
    SpellCast,
    Movement
}

public sealed record RuntimeCombatStatusModifierPolicy(
    string PolicyId,
    string SpellId,
    int AttackModifierPerMagnitude,
    int DefenseModifierPerMagnitude,
    int InitiativeModifierPerMagnitude,
    int SpeedModifierPerMagnitude,
    int ResistanceModifierPerMagnitude,
    int VisibilityModifierPerMagnitude,
    bool BlocksPhysicalAttack,
    bool BlocksSpellCast,
    bool BlocksMovement,
    bool BreaksHiddenOnPhysicalAttack,
    bool BreaksHiddenOnSpellCast,
    RuntimeResolutionConfidence Confidence,
    IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeCombatStatusModifierInput(
    string ActorId,
    RuntimeCombatStatusAction RequestedAction,
    int BaseAttack,
    int BaseDefense,
    int BaseInitiative,
    int BaseSpeed,
    int BaseResistance,
    int BaseVisibility,
    IReadOnlyList<RuntimeSpellEffectInstance> ActiveEffects);

public sealed record RuntimeCombatStatusModifierContribution(
    string EffectId,
    string SpellId,
    int EffectiveMagnitude,
    int AttackDelta,
    int DefenseDelta,
    int InitiativeDelta,
    int SpeedDelta,
    int ResistanceDelta,
    int VisibilityDelta,
    bool BlocksRequestedAction,
    bool BreaksHidden,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatStatusModifierTrace(
    string ActorId,
    RuntimeCombatStatusAction RequestedAction,
    int FinalAttack,
    int FinalDefense,
    int FinalInitiative,
    int FinalSpeed,
    int FinalResistance,
    int FinalVisibility,
    bool ActionBlocked,
    bool HiddenBroken,
    IReadOnlyList<RuntimeCombatStatusModifierContribution> Contributions,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeCombatStatusModifierEvaluator
{
    private readonly IReadOnlyDictionary<string, RuntimeCombatStatusModifierPolicy> _policies;

    public RuntimeCombatStatusModifierEvaluator(IEnumerable<RuntimeCombatStatusModifierPolicy> policies)
    {
        var all = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        _policies = all.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_policies.Count != all.Length) throw new InvalidOperationException("COMBAT_STATUS_DUPLICATE_POLICY");
        foreach (var policy in all) ValidatePolicy(policy);
    }

    public RuntimeCombatStatusModifierTrace Evaluate(RuntimeCombatStatusModifierInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireActor(input.ActorId);
        if (input.ActiveEffects is null) throw new InvalidOperationException("COMBAT_STATUS_EFFECTS_REQUIRED");

        var attack = input.BaseAttack;
        var defense = input.BaseDefense;
        var initiative = input.BaseInitiative;
        var speed = input.BaseSpeed;
        var resistance = input.BaseResistance;
        var visibility = input.BaseVisibility;
        var blocked = false;
        var hiddenBroken = false;
        var contributions = new List<RuntimeCombatStatusModifierContribution>();

        foreach (var effect in input.ActiveEffects
                     .Where(x => x.State == RuntimeSpellEffectState.Active &&
                                 x.TargetId.Equals(input.ActorId, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.AppliedTick)
                     .ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase))
        {
            if (!_policies.TryGetValue(effect.SpellId, out var policy))
                throw new InvalidOperationException("COMBAT_STATUS_POLICY_REQUIRED");
            if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
                throw new InvalidOperationException("COMBAT_STATUS_POLICY_NOT_PROMOTED");
            if (effect.StackCount <= 0 || effect.Magnitude < 0)
                throw new InvalidOperationException("COMBAT_STATUS_EFFECT_MAGNITUDE_INVALID");

            var magnitude = checked(effect.Magnitude * effect.StackCount);
            var attackDelta = checked(policy.AttackModifierPerMagnitude * magnitude);
            var defenseDelta = checked(policy.DefenseModifierPerMagnitude * magnitude);
            var initiativeDelta = checked(policy.InitiativeModifierPerMagnitude * magnitude);
            var speedDelta = checked(policy.SpeedModifierPerMagnitude * magnitude);
            var resistanceDelta = checked(policy.ResistanceModifierPerMagnitude * magnitude);
            var visibilityDelta = checked(policy.VisibilityModifierPerMagnitude * magnitude);
            var blocks = input.RequestedAction switch
            {
                RuntimeCombatStatusAction.PhysicalAttack => policy.BlocksPhysicalAttack,
                RuntimeCombatStatusAction.SpellCast => policy.BlocksSpellCast,
                RuntimeCombatStatusAction.Movement => policy.BlocksMovement,
                _ => false
            };
            var breaksHidden = input.RequestedAction switch
            {
                RuntimeCombatStatusAction.PhysicalAttack => policy.BreaksHiddenOnPhysicalAttack,
                RuntimeCombatStatusAction.SpellCast => policy.BreaksHiddenOnSpellCast,
                _ => false
            };

            attack = checked(attack + attackDelta);
            defense = checked(defense + defenseDelta);
            initiative = checked(initiative + initiativeDelta);
            speed = checked(speed + speedDelta);
            resistance = checked(resistance + resistanceDelta);
            visibility = checked(visibility + visibilityDelta);
            blocked |= blocks;
            hiddenBroken |= breaksHidden;
            contributions.Add(new RuntimeCombatStatusModifierContribution(
                effect.EffectId, effect.SpellId, magnitude, attackDelta, defenseDelta,
                initiativeDelta, speedDelta, resistanceDelta, visibilityDelta,
                blocks, breaksHidden, policy.EvidenceReferences));
        }

        return new RuntimeCombatStatusModifierTrace(
            input.ActorId, input.RequestedAction, attack, defense, initiative, speed,
            resistance, visibility, blocked, hiddenBroken, contributions,
            contributions.SelectMany(x => x.EvidenceReferences)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void ValidatePolicy(RuntimeCombatStatusModifierPolicy policy)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(policy.PolicyId, @"^statusmodifier\.\d+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(policy.SpellId, @"^spell\.\d+$"))
            throw new InvalidOperationException("COMBAT_STATUS_CANONICAL_ID_INVALID");
        if (policy.EvidenceReferences.Count == 0)
            throw new InvalidOperationException("COMBAT_STATUS_EVIDENCE_REQUIRED");
        if (!policy.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("COMBAT_STATUS_SAFE_FALLBACK_REQUIRED");
    }

    private static void RequireActor(string actorId)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(actorId, @"^(character|npc)\.\d+$"))
            throw new InvalidOperationException("COMBAT_STATUS_CANONICAL_ACTOR_ID_REQUIRED");
    }
}
