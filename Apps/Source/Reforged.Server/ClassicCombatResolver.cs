namespace Reforged.Server;

public enum ClassicAttackType
{
    Punch = 1,
    Kick = 2,
    JumpKick = 3,
    Backstab = 4,
    NormalWeapon = 5,
    Bash = 6,
    Smash = 7,
    Special125 = 8
}

public enum ClassicHitOutcome
{
    Miss = 0,
    Glance = 1,
    Hit = 2,
    Dodged = 3,
    Critical = 4
}

public sealed record ClassicCombatantStats(
    int AttackValue,
    int DefenseValue,
    int ArmorClass,
    int DamageResistance,
    int DodgeBonus,
    int ImposedAccuracyModifier = 0,
    int CriticalChance = 0);

public sealed record ClassicAttackRequest(
    ClassicAttackType AttackType,
    ClassicCombatantStats Attacker,
    ClassicCombatantStats Defender,
    int MinimumDamage,
    int MaximumDamage);

public sealed record ClassicAttackResult(
    ClassicHitOutcome Outcome,
    int HitChance,
    int DodgeChance,
    int RolledDamage,
    int DamageAfterDr,
    int FinalDamage,
    bool CriticalRolled);

public interface IClassicRandom
{
    int NextInclusive(int minimum, int maximum);
}

public sealed class SystemClassicRandom : IClassicRandom
{
    private readonly Random _random;

    public SystemClassicRandom(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public int NextInclusive(int minimum, int maximum)
    {
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        return _random.Next(minimum, checked(maximum + 1));
    }
}

public sealed class ClassicCombatResolver
{
    public const string HitFormulaId = "combat.hit_ac.native";
    public const string ClassCombatAccuracyFormulaId = "combat.class_combat_level_accuracy";
    public const string PhysicalDamageFormulaId = "combat.physical_damage.native";
    public const string StrengthDamageFormulaId = RuntimeClassicPhysicalFormulaCalculator.StrengthMaximumDamageFormulaId;
    public const string PlayerAttackEnergyFormulaId = RuntimeClassicPhysicalFormulaCalculator.AttackEnergyFormulaId;
    public const string SpecialAttackTimingFormulaId = RuntimeClassicPhysicalFormulaCalculator.SpecialAttackTimingFormulaId;
    public const string QuickAndDeadlyFormulaId = RuntimeClassicPhysicalFormulaCalculator.QuickAndDeadlyFormulaId;
    public const string QuestCombatBonusFormulaId = "combat.quest_ability_bonuses";
    public const string ItemCriticalBonusFormulaId = "combat.item_critical_ability_58";
    public const string CriticalFormulaId = "combat.critical.native";
    public const string CriticalRollBoundaryFormulaId = RuntimeClassicPhysicalFormulaCalculator.CriticalRollBoundaryFormulaId;
    public const string CriticalDamageRangeFormulaId = RuntimeClassicPhysicalFormulaCalculator.CriticalDamageRangeFormulaId;
    public const string BackstabAccuracyFormulaId = RuntimeClassicPhysicalFormulaCalculator.BackstabAccuracyFormulaId;
    public const string BackstabDamageFormulaId = RuntimeClassicPhysicalFormulaCalculator.BackstabDamageFormulaId;
    public const string BackstabDefenseFormulaId = "combat.backstab_defense";
    public const string DodgeFormulaId = "combat.dodge.native";
    public const string DodgeBaseFormulaId = "combat.dodge_base";
    public const string DodgeAbilityFormulaId = "combat.dodge_ability34";
    public const string DodgeFinalFormulaId = "combat.dodge_final";
    public const string DodgeRollBoundaryFormulaId = RuntimeClassicPhysicalFormulaCalculator.DodgeRollBoundaryFormulaId;
    public const string NpcAttackCountFormulaId = "combat.npc_attack_count";
    public const string NpcAttackEnergyFormulaId = "combat.npc_attack_energy";
    public const string NpcAttackSelectionWeightFormulaId = RuntimeNpcAttackSelector.FormulaId;
    public const string NpcResistedSpellEnergyRefundFormulaId = "combat.npc_resisted_spell_half_energy_refund";
    public const string NpcEncounterEnergyResetFormulaId = "combat.npc_encounter_energy_reset";
    public const string NpcDecodedCurrencyAmountsFormulaId = "combat.npc_decoded_currency_amounts";
    public const string NpcHitSpellRelationshipFormulaId = "combat.npc_hit_spell_relationship";
    public const string NpcHitSpellMetadataFormulaId = "combat.npc_hit_spell_metadata";
    public const string NpcHitSpellClassificationFormulaId = "combat.npc_hit_spell_classification";
    public const string NpcHitSpellAttackElementFormulaId = "combat.npc_hit_spell_attack_element";
    public const string NpcHitSpellEffectRangeFormulaId = "combat.npc_hit_spell_effect_range";
    public const string NpcHitSpellTargetScopeFormulaId = "combat.npc_hit_spell_target_scope";
    public const string NpcHitSpellResistanceScopeFormulaId = "combat.npc_hit_spell_resistance_scope";
    public const string NpcHitSpellResistanceRollFormulaId = "combat.npc_hit_spell_resistance_roll";
    public const string NpcHitSpellTypeSemanticsFormulaId = "combat.npc_hit_spell_type_semantics";
    public const string NpcHitSpellImmediateDamageFormulaId = "combat.npc_hit_spell_immediate_damage";
    public const string NpcHitSpellElementalResistanceFormulaId = "combat.npc_hit_spell_elemental_resistance";
    public const string NpcHitSpellPoisonImmunityFormulaId = "combat.npc_hit_spell_poison_immunity";
    public const string NpcHitSpellStatusDeliveryFormulaId = "combat.npc_hit_spell_status_delivery";
    public const string NpcHitSpellExpirationFormulaId = "combat.npc_hit_spell_expiration_cleanup";
    public const string NpcHitSpellCurePoisonFormulaId = "combat.npc_hit_spell_cure_poison";
    public const string StatusLifecycleFormulaId = "combat.status_effect_lifecycle";
    public const string StatusPeriodicFormulaId = "combat.status_effect_periodic_round";
    public const string StatusModifierFormulaId = "combat.status_effect_modifiers";
    public const string StatusCureDispelFormulaId = "combat.status_effect_cure_dispel";
    public const string StatusDeathCleanupFormulaId = "combat.status_effect_death_cleanup";
    public const string BackstabPreparationFormulaId = "combat.backstab_preparation";
    public const string PhysicalAttackConcealmentBreakFormulaId = "combat.physical_attack_concealment_break";
    public const string IncapacitatedDodgeSuppressionFormulaId = "combat.incapacitated_dodge_suppression";
    public const string IncapacitatedAttackSuppressionFormulaId = "combat.incapacitated_attack_suppression";
    public const string PhysicalSpecialCommandRoutingFormulaId = "combat.physical_special_command_routing";
    public const string PerfectStealthCommandFormulaId = "combat.perfect_stealth_commands";
    public const string StealthVsPerceptionFormulaId = RuntimeClassicStealthResolver.FormulaId;

    private readonly IClassicRandom _random;

    public ClassicCombatResolver(IClassicRandom? random = null, RuntimeClassicFormulaAuthority? authority = null)
    {
        _random = random ?? new SystemClassicRandom();
        if (authority is not null)
        {
            authority.RequireExecutable(HitFormulaId);
            authority.RequireExecutable(ClassCombatAccuracyFormulaId);
            authority.RequireExecutable(PhysicalDamageFormulaId);
            authority.RequireExecutable(StrengthDamageFormulaId);
            authority.RequireExecutable(PlayerAttackEnergyFormulaId);
            authority.RequireExecutable(SpecialAttackTimingFormulaId);
            authority.RequireExecutable(QuickAndDeadlyFormulaId);
            authority.RequireExecutable(QuestCombatBonusFormulaId);
            authority.RequireExecutable(ItemCriticalBonusFormulaId);
            authority.RequireExecutable(CriticalFormulaId);
            authority.RequireExecutable(CriticalRollBoundaryFormulaId);
            authority.RequireExecutable(CriticalDamageRangeFormulaId);
            authority.RequireExecutable(BackstabAccuracyFormulaId);
            authority.RequireExecutable(BackstabDamageFormulaId);
            authority.RequireExecutable(BackstabDefenseFormulaId);
            authority.RequireExecutable(DodgeFormulaId);
            authority.RequireExecutable(DodgeBaseFormulaId);
            authority.RequireExecutable(DodgeAbilityFormulaId);
            authority.RequireExecutable(DodgeFinalFormulaId);
            authority.RequireExecutable(DodgeRollBoundaryFormulaId);
            authority.RequireExecutable(NpcAttackCountFormulaId);
            authority.RequireExecutable(NpcAttackEnergyFormulaId);
            authority.RequireExecutable(NpcHitSpellRelationshipFormulaId);
            authority.RequireExecutable(NpcHitSpellMetadataFormulaId);
            authority.RequireExecutable(NpcHitSpellClassificationFormulaId);
            authority.RequireExecutable(NpcHitSpellAttackElementFormulaId);
            authority.RequireExecutable(NpcHitSpellEffectRangeFormulaId);
            authority.RequireExecutable(NpcHitSpellTargetScopeFormulaId);
            authority.RequireExecutable(NpcHitSpellResistanceScopeFormulaId);
            authority.RequireExecutable(NpcHitSpellResistanceRollFormulaId);
            authority.RequireExecutable(NpcHitSpellTypeSemanticsFormulaId);
            authority.RequireExecutable(NpcHitSpellImmediateDamageFormulaId);
            authority.RequireExecutable(NpcHitSpellElementalResistanceFormulaId);
            authority.RequireExecutable(NpcHitSpellPoisonImmunityFormulaId);
            authority.RequireExecutable(NpcHitSpellStatusDeliveryFormulaId);
            authority.RequireExecutable(NpcHitSpellExpirationFormulaId);
            authority.RequireExecutable(NpcHitSpellCurePoisonFormulaId);
            authority.RequireExecutable(StatusLifecycleFormulaId);
            authority.RequireExecutable(StatusPeriodicFormulaId);
            authority.RequireExecutable(StatusModifierFormulaId);
            authority.RequireExecutable(StatusCureDispelFormulaId);
            authority.RequireExecutable(StatusDeathCleanupFormulaId);
            authority.RequireExecutable(BackstabPreparationFormulaId);
            authority.RequireExecutable(PhysicalAttackConcealmentBreakFormulaId);
            authority.RequireExecutable(IncapacitatedDodgeSuppressionFormulaId);
            authority.RequireExecutable(IncapacitatedAttackSuppressionFormulaId);
        }
    }

    public ClassicAttackResult Resolve(ClassicAttackRequest request)
    {
        if (request.MinimumDamage < 0 || request.MaximumDamage < request.MinimumDamage)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Damage range must be non-negative and ordered.");
        }

        var modifiers = GetModifiers(request.AttackType);
        var scaledMinimum = Scale(request.MinimumDamage, modifiers.PreDrDamagePercent);
        var scaledMaximum = Scale(request.MaximumDamage, modifiers.PreDrDamagePercent);
        var hitChance = CalculateHitChance(request.AttackType, request.Attacker, request.Defender, modifiers.AccuracyModifier);
        var hitRoll = _random.NextInclusive(1, 100);
        if (hitRoll >= hitChance)
        {
            return new ClassicAttackResult(ClassicHitOutcome.Miss, hitChance, 0, 0, 0, 0, false);
        }

        var critical = modifiers.CriticalAllowed &&
            RuntimeClassicPhysicalFormulaCalculator.IsCriticalRollSuccessful(
                Math.Clamp(request.Attacker.CriticalChance, 0, 100),
                _random.NextInclusive(1, 100));
        if (critical)
        {
            var criticalRange = RuntimeClassicPhysicalFormulaCalculator.CalculateCriticalDamageRange(scaledMaximum);
            scaledMinimum = criticalRange.MinimumDamage;
            scaledMaximum = criticalRange.MaximumDamage;
        }

        var rolledDamage = _random.NextInclusive(scaledMinimum, scaledMaximum);
        var postDrDamage = rolledDamage - Math.Max(0, request.Defender.DamageResistance);
        var dodgeChance = CalculateDodgeChance(request.AttackType, request.Attacker.AttackValue, request.Defender.DodgeBonus);
        if (dodgeChance > 0 && RuntimeClassicPhysicalFormulaCalculator.IsDodgeRollSuccessful(
                dodgeChance, _random.NextInclusive(1, 100)))
        {
            return new ClassicAttackResult(ClassicHitOutcome.Dodged, hitChance, dodgeChance, rolledDamage, postDrDamage, 0, critical);
        }

        var finalDamage = Math.Max(0, postDrDamage);
        finalDamage = checked(finalDamage * modifiers.PostDrMultiplier);
        var outcome = finalDamage <= 0
            ? ClassicHitOutcome.Glance
            : critical ? ClassicHitOutcome.Critical : ClassicHitOutcome.Hit;

        return new ClassicAttackResult(outcome, hitChance, dodgeChance, rolledDamage, postDrDamage, finalDamage, critical);
    }

    public static int CalculateHitChance(
        ClassicAttackType attackType,
        ClassicCombatantStats attacker,
        ClassicCombatantStats defender,
        int attackAccuracyModifier = 0)
    {
        if (attackType == ClassicAttackType.Backstab)
        {
            return Math.Clamp(attacker.AttackValue - defender.DefenseValue, 10, 99);
        }

        var adjustedAttackValue = attacker.AttackValue + attackAccuracyModifier + defender.ImposedAccuracyModifier;
        if (adjustedAttackValue == 0)
        {
            return 10;
        }

        var hitRating = ((long)adjustedAttackValue * adjustedAttackValue) / 140;
        if (hitRating <= 0)
        {
            return 10;
        }

        var defense = (long)defender.DefenseValue + defender.ArmorClass;
        var chance = 100 - (int)((defense * defense) / hitRating);
        return Math.Clamp(chance, 10, 99);
    }

    public static int CalculateDodgeChance(ClassicAttackType attackType, int attackerAttackValue, int defenderDodgeBonus)
    {
        var chance = CalculateBaseDodgeChance(attackerAttackValue, defenderDodgeBonus);
        if (attackType == ClassicAttackType.Backstab)
        {
            chance /= 5;
        }

        return Math.Clamp(chance, 0, 95);
    }

    public static int CalculateBaseDodgeChance(int attackerAttackValue, int defenderDodgeBonus)
    {
        if (attackerAttackValue < 9 || defenderDodgeBonus <= 0)
        {
            return 0;
        }

        var divisor = attackerAttackValue >> 3;
        if (divisor <= 0)
        {
            return 0;
        }

        return Math.Min(95, (defenderDodgeBonus * 10) / divisor);
    }

    private static int Scale(int damage, int percent)
        => checked(((percent + 100) * damage) / 100);

    private static AttackModifiers GetModifiers(ClassicAttackType attackType)
        => attackType switch
        {
            ClassicAttackType.Punch => new(0, 0, true, 1),
            ClassicAttackType.Kick => new(0, 33, true, 1),
            ClassicAttackType.JumpKick => new(0, 66, true, 1),
            ClassicAttackType.Backstab => new(0, 0, false, 1),
            ClassicAttackType.NormalWeapon => new(0, 0, true, 1),
            ClassicAttackType.Bash => new(-15, 10, false, 3),
            ClassicAttackType.Smash => new(-25, 20, false, 5),
            ClassicAttackType.Special125 => new(-75, 125, false, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(attackType))
        };

    private sealed record AttackModifiers(int AccuracyModifier, int PreDrDamagePercent, bool CriticalAllowed, int PostDrMultiplier);
}

// Consolidated from RuntimeClassicPhysicalFormulaCalculator.cs during Bundle B runtime closure.
public static class RuntimeClassicPhysicalFormulaCalculator
{
    public const string StrengthMaximumDamageFormulaId = "combat.physical_damage.strength_contribution";
    public const string AttackEnergyFormulaId = "combat.player_attack_energy";
    public const string QuickAndDeadlyFormulaId = "combat.quick_and_deadly_critical_bonus";
    public const string NaturalCriticalChanceFormulaId = "combat.natural_critical_chance";
    public const string PlayerAttackSchedulingFormulaId = "combat.player_attack_energy_scheduling";
    public const string SpecialAttackTimingFormulaId = "combat.special_attack_timing";
    public const string CriticalRollBoundaryFormulaId = "combat.critical_roll_boundary";
    public const string CriticalDamageRangeFormulaId = "combat.critical_damage_range";
    public const string FinalCriticalChanceFormulaId = "combat.final_critical_chance";
    public const string CriticalResolutionOrderFormulaId = "combat.critical_resolution_order";
    public const string DodgeRollBoundaryFormulaId = "combat.dodge_roll_boundary";
    public const string BackstabAccuracyFormulaId = "combat.backstab_accuracy";
    public const string BackstabDamageFormulaId = "combat.backstab_damage";

    // MMUD Explorer frmMain.frm 27694: Fix((STR - 50) / 10).
    // C# integer division truncates toward zero, matching VB6 Fix for integer inputs.
    public static int CalculateStrengthMaximumDamageBonus(int strength)
        => (strength - 50) / 10;


    // MMUD Explorer modMMudFunc.bas 2111-2132, stock MajorMUD branch.
    // Stock uses Fix((Stealth + Agility) / 2) + Fix(BSAccu / 2), then
    // +5 for class stealth or -15 for race-only stealth, followed by normal accuracy bonuses.
    public static int CalculateBackstabAccuracy(int stealth, int agility, int backstabAccuracyBonus, bool classStealth, int normalAccuracyBonus)
    {
        var accuracy = checked(((stealth + agility) / 2) + (backstabAccuracyBonus / 2));
        accuracy = checked(accuracy + (classStealth ? 5 : -15));
        return checked(accuracy + normalAccuracyBonus);
    }

    // MMUD Explorer modMMudFunc.bas 4196-4198 and frmBSCalc.frm 1123-1124.
    // Classic applies the level-scaling branch because Not bGreaterMUD is true.
    public static int CalculateBackstabDamage(int level, int stealth, int normalDamage, int backstabDamageBonus, bool classStealth)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (normalDamage < 0) throw new ArgumentOutOfRangeException(nameof(normalDamage));
        var damage = checked((level * 2) + (stealth / 10) + (normalDamage * 2) + backstabDamageBonus);
        if (!classStealth) damage = checked((damage * 75) / 100);
        return checked(((level + 100) * damage) / 100);
    }

    // MMUD Explorer modMMudFunc.bas 4678 and Nightmare Redux frmSwingCalc.frm 2006.
    public static int CalculateAttackEnergyCost(int combatCode, int level, int weaponSpeed, int agility)
    {
        if (combatCode < 0) throw new ArgumentOutOfRangeException(nameof(combatCode));
        if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (weaponSpeed < 0) throw new ArgumentOutOfRangeException(nameof(weaponSpeed));
        if (weaponSpeed == 0) return 0;
        var denominator = checked((((level * (combatCode + 2)) + 45) * (agility + 150)) / 6);
        if (denominator <= 0) throw new InvalidOperationException("CLASSIC_ATTACK_ENERGY_DENOMINATOR_INVALID");
        return checked((weaponSpeed * 1000) / denominator);
    }
    // MMUD Explorer frmMain.frm 27917-27951 and MudInfo formula publication.
    // Classic natural critical chance is capped before Quick-and-Deadly, quest, and item modifiers.
    public static int CalculateNaturalCriticalChance(int level, int intellect, int agility, int charm)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        var chance = checked(
            (level / 10) +
            ((intellect - 50) / 10) +
            ((agility - 50) / 20) +
            ((charm - 50) / 30));
        return Math.Clamp(chance, 1, 75);
    }

    // MMUD Explorer modMMudFunc.bas 4564-4566 and Nightmare Redux frmSwingCalc.frm 2064-2067.
    // Classic formula: (200 - weapon energy) + Fix((AGL - 50) / 10), capped at 20;
    // encumbrance of 33% or more halves the result using VB6 Fix semantics.
    public static int CalculateQuickAndDeadlyCriticalBonus(int agility, int attackEnergyCost, int encumbrancePercent)
    {
        if (attackEnergyCost < 0) throw new ArgumentOutOfRangeException(nameof(attackEnergyCost));
        if (encumbrancePercent < 0) throw new ArgumentOutOfRangeException(nameof(encumbrancePercent));
        var bonus = checked((200 - attackEnergyCost) + ((agility - 50) / 10));
        if (bonus > 20) bonus = 20;
        if (bonus < 0) bonus = 0;
        if (encumbrancePercent >= 33) bonus /= 2;
        return bonus;
    }

    // Stock Classic applies diminishing returns only after natural, condition, quest,
    // equipped-item, and Quick-and-Deadly contributions have been combined.
    // Values through 40 are unchanged; excess points count at one-third, capped at 99.
    public static int CalculateFinalCriticalChance(int combinedCriticalChance)
    {
        if (combinedCriticalChance <= 40) return Math.Clamp(combinedCriticalChance, 0, 99);
        return Math.Clamp(40 + ((combinedCriticalChance - 40) / 3), 0, 99);
    }

    // Classic percentage checks use an inclusive 1..100 roll. A chance of 1 succeeds
    // only on roll 1; 100 succeeds on every roll. This avoids the old 0..100 off-by-one path.
    public static bool IsCriticalRollSuccessful(int criticalChance, int roll)
    {
        if (roll < 1 || roll > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll <= Math.Clamp(criticalChance, 0, 100);
    }


    // Classic percentage checks use an inclusive 1..100 roll. Dodge succeeds when
    // the roll is less than or equal to the final clamped dodge chance.
    public static bool IsDodgeRollSuccessful(int dodgeChance, int roll)
    {
        if (roll < 1 || roll > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll <= Math.Clamp(dodgeChance, 0, 100);
    }

    // Retained Classic help states that a critical hit deals up to four times normal damage.
    // MMUD Explorer's combat range uses twice through four times the normal maximum.
    public static RuntimeCriticalDamageRange CalculateCriticalDamageRange(int normalMaximumDamage)
    {
        if (normalMaximumDamage < 0) throw new ArgumentOutOfRangeException(nameof(normalMaximumDamage));
        return new RuntimeCriticalDamageRange(
            checked(normalMaximumDamage * 2),
            checked(normalMaximumDamage * 4));
    }

    // MMUD Explorer modMMudFunc.bas 1587-1593 and frmSwingCalc.frm 2006.
    // Each combat round supplies 1000 action-energy units. Whole swings are executed
    // when enough carried energy is available, with a hard Classic cap of five swings.
    public static RuntimePlayerAttackSchedule ResolveNormalWeaponAttackSchedule(
        int carriedEnergy, int attackEnergyCost, int roundEnergy = 1000)
    {
        if (carriedEnergy < 0) throw new ArgumentOutOfRangeException(nameof(carriedEnergy));
        if (attackEnergyCost < 0) throw new ArgumentOutOfRangeException(nameof(attackEnergyCost));
        if (roundEnergy < 0) throw new ArgumentOutOfRangeException(nameof(roundEnergy));
        if (attackEnergyCost == 0) return new RuntimePlayerAttackSchedule(1, 0, carriedEnergy);

        var available = checked(carriedEnergy + roundEnergy);
        var swings = Math.Min(5, available / attackEnergyCost);
        var remaining = checked(available - checked(swings * attackEnergyCost));
        return new RuntimePlayerAttackSchedule(swings, available, remaining);
    }

    // MMUD Explorer modMMudFunc.bas 1564-1595 and 1648-1674.
    // Backstab and Smash consume the whole 1000-energy round and execute exactly once.
    // Bash uses normal weapon energy doubled, may execute multiple affordable bashes, and
    // remains capped by the Classic five-swing limit. The encounter returns to a normal
    // weapon attack after this opening special round.
    public static RuntimePlayerAttackSchedule ResolvePhysicalAttackSchedule(
        ClassicAttackType attackType, int carriedEnergy, int normalAttackEnergyCost, int roundEnergy = 1000)
    {
        if (carriedEnergy < 0) throw new ArgumentOutOfRangeException(nameof(carriedEnergy));
        if (normalAttackEnergyCost < 0) throw new ArgumentOutOfRangeException(nameof(normalAttackEnergyCost));
        if (roundEnergy < 0) throw new ArgumentOutOfRangeException(nameof(roundEnergy));

        return attackType switch
        {
            ClassicAttackType.Backstab or ClassicAttackType.Smash
                => new RuntimePlayerAttackSchedule(1, roundEnergy, 0),
            ClassicAttackType.Bash
                => ResolveNormalWeaponAttackSchedule(carriedEnergy, checked(normalAttackEnergyCost * 2), roundEnergy),
            _ => ResolveNormalWeaponAttackSchedule(carriedEnergy, normalAttackEnergyCost, roundEnergy)
        };
    }

}

public sealed record RuntimeCriticalDamageRange(int MinimumDamage, int MaximumDamage);

public sealed record RuntimePlayerAttackSchedule(int Swings, int AvailableEnergy, int RemainingEnergy);

public sealed class RuntimeClassicPhysicalFormulaSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeClassicPhysicalFormulaSmokeReport Run()
    {
        var report = new RuntimeClassicPhysicalFormulaSmokeReport();
        try
        {
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabAccuracy(120, 80, 10, true, 5) == 115, "backstab-accuracy-stock-known-vector"); report.Checks.Add("backstab-accuracy-stock-known-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(20, 120, 8, 3, true) == 85, "backstab-damage-known-vector"); report.Checks.Add("backstab-damage-known-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(1, 0, 1, 0, true) == 4, "backstab-damage-classic-level-scale"); report.Checks.Add("backstab-damage-classic-level-scale");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(20, 120, 8, 3, false) == 63, "backstab-damage-race-only-75-percent"); report.Checks.Add("backstab-damage-race-only-75-percent");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(50) == 0, "strength-50-zero"); report.Checks.Add("strength-50-zero");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(100) == 5, "strength-100-plus-five"); report.Checks.Add("strength-100-plus-five");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(49) == 0, "strength-fix-truncation"); report.Checks.Add("strength-fix-truncation");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(40) == -1, "strength-negative-bonus"); report.Checks.Add("strength-negative-bonus");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(2, 20, 100, 50) == 24, "attack-energy-known-vector"); report.Checks.Add("attack-energy-known-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateNaturalCriticalChance(1, 50, 50, 50) == 1, "natural-critical-minimum-one"); report.Checks.Add("natural-critical-minimum-one");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateNaturalCriticalChance(50, 100, 90, 80) == 13, "natural-critical-stat-vector"); report.Checks.Add("natural-critical-stat-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateNaturalCriticalChance(500, 500, 500, 500) == 75, "natural-critical-cap-75"); report.Checks.Add("natural-critical-cap-75");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(50, 190, 0) == 10, "qnd-base-vector"); report.Checks.Add("qnd-base-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(100, 100, 0) == 20, "qnd-cap-20"); report.Checks.Add("qnd-cap-20");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(50, 190, 33) == 5, "qnd-encumbrance-halves"); report.Checks.Add("qnd-encumbrance-halves");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(40) == 40, "final-critical-no-diminishing-through-forty"); report.Checks.Add("final-critical-no-diminishing-through-forty");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(43) == 41, "final-critical-one-third-excess"); report.Checks.Add("final-critical-one-third-excess");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(1000) == 99, "final-critical-cap-99"); report.Checks.Add("final-critical-cap-99");
            Require(RuntimeClassicPhysicalFormulaCalculator.IsCriticalRollSuccessful(1, 1), "critical-one-percent-lower-bound"); report.Checks.Add("critical-one-percent-lower-bound");
            Require(!RuntimeClassicPhysicalFormulaCalculator.IsCriticalRollSuccessful(1, 2), "critical-one-percent-upper-fail"); report.Checks.Add("critical-one-percent-upper-fail");
            Require(RuntimeClassicPhysicalFormulaCalculator.IsCriticalRollSuccessful(100, 100), "critical-hundred-percent"); report.Checks.Add("critical-hundred-percent");
            Require(RuntimeClassicPhysicalFormulaCalculator.IsDodgeRollSuccessful(1, 1), "dodge-one-percent-lower-bound"); report.Checks.Add("dodge-one-percent-lower-bound");
            Require(!RuntimeClassicPhysicalFormulaCalculator.IsDodgeRollSuccessful(1, 2), "dodge-one-percent-upper-fail"); report.Checks.Add("dodge-one-percent-upper-fail");
            Require(RuntimeClassicPhysicalFormulaCalculator.IsDodgeRollSuccessful(100, 100), "dodge-hundred-percent"); report.Checks.Add("dodge-hundred-percent");
            var criticalRange = RuntimeClassicPhysicalFormulaCalculator.CalculateCriticalDamageRange(12);
            Require(criticalRange.MinimumDamage == 24 && criticalRange.MaximumDamage == 48, "critical-two-to-four-times-maximum"); report.Checks.Add("critical-two-to-four-times-maximum");
            var schedule1 = RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(0, 300);
            Require(schedule1.Swings == 3 && schedule1.RemainingEnergy == 100, "attack-schedule-first-round"); report.Checks.Add("attack-schedule-first-round");
            var schedule2 = RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(schedule1.RemainingEnergy, 300);
            Require(schedule2.Swings == 3 && schedule2.RemainingEnergy == 200, "attack-schedule-carry-forward"); report.Checks.Add("attack-schedule-carry-forward");
            var schedule3 = RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(schedule2.RemainingEnergy, 300);
            Require(schedule3.Swings == 4 && schedule3.RemainingEnergy == 0, "attack-schedule-fractional-recovery"); report.Checks.Add("attack-schedule-fractional-recovery");
            Require(RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(0, 100).Swings == 5, "attack-schedule-five-swing-cap"); report.Checks.Add("attack-schedule-five-swing-cap");
            var bashSchedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(ClassicAttackType.Bash, 0, 300);
            Require(bashSchedule.Swings == 1 && bashSchedule.RemainingEnergy == 400, "bash-doubled-energy-schedule"); report.Checks.Add("bash-doubled-energy-schedule");
            var fastBashSchedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(ClassicAttackType.Bash, 0, 100);
            Require(fastBashSchedule.Swings == 5 && fastBashSchedule.RemainingEnergy == 0, "bash-five-swing-cap"); report.Checks.Add("bash-five-swing-cap");
            var smashSchedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(ClassicAttackType.Smash, 275, 100);
            Require(smashSchedule.Swings == 1 && smashSchedule.RemainingEnergy == 0, "smash-full-round-single-action"); report.Checks.Add("smash-full-round-single-action");
            var backstabSchedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(ClassicAttackType.Backstab, 125, 100);
            Require(backstabSchedule.Swings == 1 && backstabSchedule.RemainingEnergy == 0, "backstab-full-round-single-action"); report.Checks.Add("backstab-full-round-single-action");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
