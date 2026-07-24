namespace Reforged.Server;

public sealed record RuntimeStatusIntegratedPhysicalAttack(
    RuntimePhysicalAttackRequest Request,
    RuntimeCombatStatusModifierTrace AttackerStatus,
    RuntimeCombatStatusModifierTrace DefenderStatus,
    bool HiddenBreakRequired,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeStatusIntegratedSpellCast(
    RuntimeSpellCastInstance Cast,
    RuntimeCombatStatusModifierTrace Status,
    bool HiddenBreakRequired,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeStatusIntegratedScheduledTurn(
    RuntimeCombatScheduledTurn Turn,
    int AdjustedInitiative,
    int AdjustedSpeed,
    RuntimeCombatStatusModifierTrace Status,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeStatusIntegratedMovement(
    RuntimeCombatMovementResult Result,
    RuntimeCombatStatusModifierTrace Status,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeCombatStatusIntegrationCoordinator
{
    private readonly RuntimeCombatStatusModifierEvaluator _status;

    public RuntimeCombatStatusIntegrationCoordinator(RuntimeCombatStatusModifierEvaluator status)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
    }

    public RuntimeStatusIntegratedPhysicalAttack IntegratePhysicalAttack(
        RuntimePhysicalAttackRequest request,
        IReadOnlyList<RuntimeSpellEffectInstance> attackerEffects,
        IReadOnlyList<RuntimeSpellEffectInstance> defenderEffects)
    {
        ArgumentNullException.ThrowIfNull(request);
        var attacker = _status.Evaluate(new RuntimeCombatStatusModifierInput(
            request.AttackerId, RuntimeCombatStatusAction.PhysicalAttack,
            request.AttackerAttackInput, 0, 0, 0, 0, 0, RequireEffects(attackerEffects)));
        if (attacker.ActionBlocked) throw new InvalidOperationException("COMBAT_STATUS_PHYSICAL_ATTACK_BLOCKED");

        var defender = _status.Evaluate(new RuntimeCombatStatusModifierInput(
            request.DefenderId, RuntimeCombatStatusAction.None,
            0, request.DefenderDefenseInput, 0, 0, 0, 0, RequireEffects(defenderEffects)));

        var adjusted = request with
        {
            AttackerAttackInput = attacker.FinalAttack,
            DefenderDefenseInput = defender.FinalDefense
        };
        return new RuntimeStatusIntegratedPhysicalAttack(adjusted, attacker, defender,
            attacker.HiddenBroken, Merge(attacker, defender));
    }

    public RuntimeStatusIntegratedSpellCast StartSpellCast(
        RuntimeSpellCastLifecycle lifecycle,
        string castId,
        string actorId,
        string spellId,
        long worldTick,
        IReadOnlyList<RuntimeSpellEffectInstance> activeEffects,
        int baseResistance = 0,
        int baseVisibility = 0)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        var trace = _status.Evaluate(new RuntimeCombatStatusModifierInput(
            actorId, RuntimeCombatStatusAction.SpellCast, 0, 0, 0, 0,
            baseResistance, baseVisibility, RequireEffects(activeEffects)));
        if (trace.ActionBlocked) throw new InvalidOperationException("COMBAT_STATUS_SPELL_CAST_BLOCKED");
        var cast = lifecycle.Start(castId, actorId, spellId, worldTick);
        return new RuntimeStatusIntegratedSpellCast(cast, trace, trace.HiddenBroken, trace.EvidenceReferences);
    }

    public IReadOnlyList<RuntimeStatusIntegratedScheduledTurn> IntegrateScheduledTurns(
        IReadOnlyList<RuntimeCombatScheduledTurn> turns,
        IReadOnlyDictionary<string, IReadOnlyList<RuntimeSpellEffectInstance>> effectsByActor,
        IReadOnlyDictionary<string, int>? baseSpeedByActor = null)
    {
        if (turns is null) throw new ArgumentNullException(nameof(turns));
        if (effectsByActor is null) throw new ArgumentNullException(nameof(effectsByActor));
        var integrated = new List<RuntimeStatusIntegratedScheduledTurn>(turns.Count);
        foreach (var turn in turns)
        {
            if (!effectsByActor.TryGetValue(turn.ActorId, out var effects))
                throw new InvalidOperationException("COMBAT_STATUS_SCHEDULE_EFFECTS_REQUIRED");
            var speed = baseSpeedByActor is not null && baseSpeedByActor.TryGetValue(turn.ActorId, out var value) ? value : 0;
            var trace = _status.Evaluate(new RuntimeCombatStatusModifierInput(
                turn.ActorId, RuntimeCombatStatusAction.None, 0, 0, turn.Initiative, speed, 0, 0,
                RequireEffects(effects)));
            integrated.Add(new RuntimeStatusIntegratedScheduledTurn(
                turn, trace.FinalInitiative, trace.FinalSpeed, trace, trace.EvidenceReferences));
        }
        return integrated
            .OrderByDescending(x => x.AdjustedInitiative)
            .ThenByDescending(x => x.AdjustedSpeed)
            .ThenBy(x => x.Turn.Sequence)
            .ThenBy(x => x.Turn.ActorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public RuntimeStatusIntegratedMovement ExecuteMovement(
        RuntimeCombatMovementCoordinator movement,
        RuntimeCombatMovementRequest request,
        IReadOnlyList<RuntimeSpellEffectInstance> activeEffects,
        int baseSpeed = 0,
        int baseVisibility = 0)
    {
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(request);
        var trace = _status.Evaluate(new RuntimeCombatStatusModifierInput(
            request.ActorId, RuntimeCombatStatusAction.Movement, 0, 0, 0,
            baseSpeed, 0, baseVisibility, RequireEffects(activeEffects)));
        if (trace.ActionBlocked) throw new InvalidOperationException("COMBAT_STATUS_MOVEMENT_BLOCKED");
        var result = movement.Execute(request);
        return new RuntimeStatusIntegratedMovement(result, trace, trace.EvidenceReferences);
    }

    private static IReadOnlyList<RuntimeSpellEffectInstance> RequireEffects(IReadOnlyList<RuntimeSpellEffectInstance>? effects) =>
        effects ?? throw new InvalidOperationException("COMBAT_STATUS_ACTIVE_EFFECTS_REQUIRED");

    private static IReadOnlyList<string> Merge(params RuntimeCombatStatusModifierTrace[] traces) =>
        traces.SelectMany(x => x.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
