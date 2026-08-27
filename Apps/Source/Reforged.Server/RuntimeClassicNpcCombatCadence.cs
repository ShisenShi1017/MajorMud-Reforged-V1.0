namespace Reforged.Server;

/// <summary>
/// Recovered Classic NPC combat cadence. The retained MMUD Explorer and Nightmare Redux
/// monster attack simulators independently implement the same six-attempt, carried-energy loop.
/// These rules are kept separate from broader NPC perception/aggro/assist/flee semantics, which
/// remain evidence-gated.
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
    public const int StockHpRegenRounds = 18;

    public static int ResolveAttemptSlots(RuntimeNpcDefinition npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (npc.AttackCount < 0)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_COUNT_NEGATIVE:{npc.Id}:{npc.AttackCount}");
        if (npc.AttackCount == 0) return 0;
        if (npc.Attacks.Count == 0)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_COUNT_WITHOUT_ROWS:{npc.Id}:{npc.AttackCount}");
        if (npc.Attacks.Count != npc.AttackCount)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_ROW_COUNT_MISMATCH:{npc.Id}:{npc.AttackCount}:{npc.Attacks.Count}");
        return AttemptsPerRound;
    }

    public static int AddRoundEnergy(int carriedEnergy, int energyPerRound)
    {
        if (carriedEnergy < 0) throw new ArgumentOutOfRangeException(nameof(carriedEnergy));
        if (energyPerRound < 0) throw new InvalidDataException("NPC_COMBAT_NEGATIVE_ROUND_ENERGY");
        return checked(carriedEnergy + energyPerRound);
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
        if (roll is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        // For Type 2 rows AttMin is the native cast-success percentage; AttAcc is the spell id.
        return roll <= attack.MinimumDamage;
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
        var halfCost = checked((int)Math.Round(attack.EnergyCost / 2d, MidpointRounding.ToEven));
        return checked(currentEnergyBeforeAttack - halfCost);
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
        var spell = new RuntimeNpcAttackDefinition(2, 2, 100, 15, 75, 376, 0, 127, 0, 0, 0);
        var catchAll = new RuntimeNpcAttackDefinition(3, 1, 8, 12, 232, 60, 0, 50, 0, 0, 0);

        Require(RuntimeClassicNpcCombatCadence.AttemptsPerRound == 6, "six-attempt-slots"); checks.Add("six-attempt-slots");
        Require(RuntimeClassicNpcCombatCadence.StockHpRegenRounds == 18, "stock-hp-regen-interval-18-rounds"); checks.Add("stock-hp-regen-interval-18-rounds");
        Require(RuntimeClassicNpcCombatCadence.AddRoundEnergy(250, 1000) == 1250, "energy-carries-without-cap"); checks.Add("energy-carries-without-cap");
        Require(RuntimeNpcAttackSelector.Select(new[] { physical, spell, catchAll }, 0, 25).Attack?.Slot == 1, "threshold-first-inclusive"); checks.Add("threshold-first-inclusive");
        Require(RuntimeNpcAttackSelector.Select(new[] { physical, spell, catchAll }, 0, 26).Attack?.Slot == 2, "threshold-second-range"); checks.Add("threshold-second-range");
        var unaffordable = RuntimeNpcAttackSelector.Select(new[] { physical, spell, catchAll }, 0, 26);
        Require(unaffordable.Attack?.Slot == 2 && !unaffordable.Affordable, "unaffordable-row-not-renormalized"); checks.Add("unaffordable-row-not-renormalized");
        Require(RuntimeNpcAttackSelector.Select(new[] { catchAll }, 1000, 100).Attack?.Slot == 3, "threshold-over-100-catchall"); checks.Add("threshold-over-100-catchall");
        Require(RuntimeClassicNpcCombatCadence.SpellCastSucceeds(spell, 100), "spell-cast-success-inclusive"); checks.Add("spell-cast-success-inclusive");
        Require(RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(200, spell) == 136, "spell-half-cost-bankers-127"); checks.Add("spell-half-cost-bankers-127");
        Require(RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(200, spell with { EnergyCost = 125 }) == 138, "spell-half-cost-bankers-125"); checks.Add("spell-half-cost-bankers-125");
        Require(RuntimeClassicNpcCombatCadence.ShouldEndRoundAfterPhysicalSelection(physical, 124), "physical-early-end"); checks.Add("physical-early-end");
        Require(!RuntimeClassicNpcCombatCadence.ShouldEndRoundAfterPhysicalSelection(spell, 0), "spell-does-not-use-physical-early-end"); checks.Add("spell-does-not-use-physical-early-end");
        return new RuntimeClassicNpcCombatCadenceAuditResult("PASS", checks);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
