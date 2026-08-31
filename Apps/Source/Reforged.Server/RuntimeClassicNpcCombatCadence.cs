namespace Reforged.Server;

/// <summary>
/// Recovered Classic NPC combat cadence. CodeUpdate0335 ties the attack loop, energy refresh,
/// direct-spell roll/cost fields and physical early-end branch to the original WCCMMUD.DLL.
/// CodeUpdate0364 completes the broader native NPC-AI ownership boundary and keeps generic
/// Reforged perception/threat/flee/patrol policies isolated from Classic behavior.
/// </summary>
public static class RuntimeClassicNpcCombatCadence
{
    public const string AttackRowCountFormulaId = "combat.npc_attack_row_count";
    public const string AttemptsPerRoundFormulaId = "combat.npc_attack_attempts_per_round";
    public const string EnergyCarryFormulaId = "combat.npc_attack_energy_round_carry";
    public const string SpellCastSuccessFormulaId = "combat.npc_spell_cast_success";
    public const string SpellFailureEnergyFormulaId = "combat.npc_spell_failure_half_energy";
    public const string PhysicalEarlyEndFormulaId = "combat.npc_physical_early_round_end";
    public const string StockHpRegenIntervalFormulaId = "combat.npc_hp_regen_stock_interval";
    public const int AttemptsPerRound = 6;
    public const int StockHpRegenSlowFastTicks = RuntimeClassicTimingAuthority.SlowSchedulerFastTicks;

    public static int ResolveAttemptSlots(RuntimeNpcDefinition npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (npc.AttackCount < 0)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_COUNT_NEGATIVE:{npc.Id}:{npc.AttackCount}");
        // Classic stores five physical attack rows. The generated attack_count field is a
        // Reforged projection-integrity count of executable decoded rows, not a sixth native
        // combat control field. Validate it, but never use it to change native storage width
        // or the six-attempt dispatcher.
        if (npc.Attacks.Count > 5)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_ROWS_EXCEED_NATIVE_FIVE:{npc.Id}:{npc.Attacks.Count}");
        if (npc.Attacks.Count != npc.AttackCount)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_ROW_COUNT_MISMATCH:{npc.Id}:{npc.AttackCount}:{npc.Attacks.Count}");
        return npc.Attacks.Count == 0 ? 0 : AttemptsPerRound;
    }

    public static int RefreshRoundEnergy(int currentEnergy, int authoredEnergy)
    {
        if (currentEnergy < 0) throw new ArgumentOutOfRangeException(nameof(currentEnergy));
        if (authoredEnergy < 0) throw new InvalidDataException("NPC_COMBAT_NEGATIVE_ROUND_ENERGY");
        // Native _energy_update_monster compares active +0x16 with the authored copy at +0x114.
        // Below baseline it adds one authored-energy quantum; above baseline it clamps to the
        // baseline; equality is left unchanged. Active monsters are spawned with both values set
        // to the authored Energy field, so idle energy does not accumulate without bound.
        if (authoredEnergy > currentEnergy) return checked(currentEnergy + authoredEnergy);
        if (authoredEnergy < currentEnergy) return authoredEnergy;
        return currentEnergy;
    }

    public static bool CanAfford(RuntimeNpcAttackDefinition attack, int currentEnergy)
    {
        ArgumentNullException.ThrowIfNull(attack);
        if (currentEnergy < 0) throw new ArgumentOutOfRangeException(nameof(currentEnergy));
        if (attack.EnergyCost < 0) throw new InvalidDataException("NPC_COMBAT_NEGATIVE_ATTACK_ENERGY_COST");
        return attack.EnergyCost <= currentEnergy;
    }

    public static bool SpellCastSucceeds(RuntimeNpcAttackDefinition attack, int roll)
    {
        ArgumentNullException.ThrowIfNull(attack);
        if (attack.Type != 2) throw new InvalidOperationException("NPC_COMBAT_NOT_DIRECT_SPELL_ROW");
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        // Native _monster_cast requests [0,100) and succeeds when roll < AttMin.
        // For Type 2 rows AttMin is cast-success percentage; AttAcc is spell id and AttMax cast level.
        return roll < attack.MinimumDamage;
    }

    public static int DeductFullAttackCost(int currentEnergy, RuntimeNpcAttackDefinition attack)
    {
        if (!CanAfford(attack, currentEnergy)) throw new InvalidOperationException("NPC_COMBAT_INSUFFICIENT_ENERGY");
        return checked(currentEnergy - attack.EnergyCost);
    }

    public static int DeductFailedSpellCost(int currentEnergyBeforeAttack, RuntimeNpcAttackDefinition attack)
    {
        if (attack.Type != 2) throw new InvalidOperationException("NPC_COMBAT_HALF_COST_ONLY_FOR_DIRECT_SPELL");
        if (!CanAfford(attack, currentEnergyBeforeAttack)) throw new InvalidOperationException("NPC_COMBAT_INSUFFICIENT_ENERGY");
        // Native positive Int16 AttEnergy failure path skips deduction when the authored cost is
        // zero; otherwise arithmetic /2 floors odd values and a cost of 1 still charges 1.
        if (attack.EnergyCost == 0) return currentEnergyBeforeAttack;
        var halfCost = Math.Max(1, attack.EnergyCost / 2);
        return checked(currentEnergyBeforeAttack - halfCost);
    }

    public static int RefundRejectedSpellCost(int currentEnergyBeforeAttack, RuntimeNpcAttackDefinition attack)
    {
        if (attack.Type != 2) throw new InvalidOperationException("NPC_COMBAT_REFUND_ONLY_FOR_DIRECT_SPELL");
        if (!CanAfford(attack, currentEnergyBeforeAttack)) throw new InvalidOperationException("NPC_COMBAT_INSUFFICIENT_ENERGY");
        // Native _monster_cast first debits the full AttEnergy after the pre-cast success roll.
        // Later effect-admission/no-effect exits add that exact full signed-word cost back.
        // Net energy for those branches is therefore unchanged; this is distinct from the
        // pre-cast failure branch above, which retains the half-cost penalty.
        return currentEnergyBeforeAttack;
    }

    public static bool ShouldEndRoundAfterPhysicalSelection(RuntimeNpcAttackDefinition attack, int remainingEnergy)
    {
        ArgumentNullException.ThrowIfNull(attack);
        if (remainingEnergy < 0) throw new ArgumentOutOfRangeException(nameof(remainingEnergy));
        return attack.Type == 1 && remainingEnergy < attack.EnergyCost;
    }
}

public sealed record RuntimeClassicNpcCombatCadenceAuditResult(string Status, IReadOnlyList<string> Checks);

public static class RuntimeClassicNpcCombatCadenceAudit
{
    public static RuntimeClassicNpcCombatCadenceAuditResult Run()
    {
        var checks = new List<string>();
        var physical = new RuntimeNpcAttackDefinition(1, 1, 5, 10, 25, 50, 0, 125, 0, 0, 0);
        var spell = new RuntimeNpcAttackDefinition(2, 2, 75, 15, 75, 376, 0, 127, 0, 0, 0);
        var catchAll = new RuntimeNpcAttackDefinition(3, 1, 8, 12, 232, 60, 0, 50, 0, 0, 0);

        Require(RuntimeClassicNpcCombatCadence.AttemptsPerRound == 6, "six-attempt-slots"); checks.Add("six-attempt-slots");
        Require(RuntimeClassicNpcCombatCadence.StockHpRegenSlowFastTicks == 30, "stock-hp-regen-slow-generation-30-fast-ticks"); checks.Add("stock-hp-regen-slow-generation-30-fast-ticks");
        Require(RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(1000, 1000) == 1000, "energy-idle-equality-holds"); checks.Add("energy-idle-equality-holds");
        Require(RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(250, 1000) == 1250, "energy-below-baseline-adds-one-quantum"); checks.Add("energy-below-baseline-adds-one-quantum");
        Require(RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(1250, 1000) == 1000, "energy-above-baseline-clamps"); checks.Add("energy-above-baseline-clamps");
        Require(RuntimeNpcAttackSelector.Select(new[] { physical, spell, catchAll }, 0, 24).Attack?.Slot == 1, "threshold-first-native-zero-based"); checks.Add("threshold-first-native-zero-based");
        Require(RuntimeNpcAttackSelector.Select(new[] { physical, spell, catchAll }, 0, 25).Attack?.Slot == 2, "threshold-second-native-zero-based"); checks.Add("threshold-second-native-zero-based");
        var unaffordable = RuntimeNpcAttackSelector.Select(new[] { physical, spell, catchAll }, 0, 25);
        Require(unaffordable.Attack?.Slot == 2 && !unaffordable.Affordable, "unaffordable-row-not-renormalized"); checks.Add("unaffordable-row-not-renormalized");
        Require(RuntimeNpcAttackSelector.Select(new[] { catchAll }, 1000, 99).Attack?.Slot == 3, "threshold-over-100-catchall"); checks.Add("threshold-over-100-catchall");
        Require(RuntimeClassicNpcCombatCadence.SpellCastSucceeds(spell, 74), "spell-cast-success-native-less-than"); checks.Add("spell-cast-success-native-less-than");
        Require(!RuntimeClassicNpcCombatCadence.SpellCastSucceeds(spell, 75), "spell-cast-failure-native-boundary"); checks.Add("spell-cast-failure-native-boundary");
        Require(RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(200, spell) == 137, "spell-half-cost-floor-127"); checks.Add("spell-half-cost-floor-127");
        Require(RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(200, spell with { EnergyCost = 125 }) == 138, "spell-half-cost-floor-125"); checks.Add("spell-half-cost-floor-125");
        Require(RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(2, spell with { EnergyCost = 1 }) == 1, "spell-half-cost-minimum-one"); checks.Add("spell-half-cost-minimum-one");
        Require(RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(2, spell with { EnergyCost = 0 }) == 2, "spell-zero-cost-remains-free"); checks.Add("spell-zero-cost-remains-free");
        Require(RuntimeClassicNpcCombatCadence.RefundRejectedSpellCost(200, spell) == 200, "spell-effect-rejection-full-refund"); checks.Add("spell-effect-rejection-full-refund");
        Require(RuntimeClassicNpcCombatCadence.ShouldEndRoundAfterPhysicalSelection(physical, 124), "physical-early-end"); checks.Add("physical-early-end");
        Require(!RuntimeClassicNpcCombatCadence.ShouldEndRoundAfterPhysicalSelection(spell, 0), "spell-does-not-use-physical-early-end"); checks.Add("spell-does-not-use-physical-early-end");
        return new RuntimeClassicNpcCombatCadenceAuditResult("PASS", checks);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
