namespace RealmGate.Server;

// PASS59 parity boundary: recovered formulas in this resolver keep their individual evidence grades; unresolved native cross-formula/branch/RNG ordering is gated by RuntimeClassicCombatSpellNativeBoundaryAuthority.

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
    public const string NpcAttackCountFormulaId = RuntimeClassicNpcCombatCadence.AttackRowCountFormulaId;
    public const string NpcAttackAttemptsFormulaId = RuntimeClassicNpcCombatCadence.AttemptsPerRoundFormulaId;
    public const string NpcAttackEnergyFormulaId = RuntimeClassicNpcCombatCadence.EnergyCarryFormulaId;
    public const string NpcAttackSelectionWeightFormulaId = RuntimeNpcAttackSelector.FormulaId;
    public const string NpcSpellCastSuccessFormulaId = RuntimeClassicNpcCombatCadence.SpellCastSuccessFormulaId;
    public const string NpcResistedSpellEnergyRefundFormulaId = RuntimeClassicNpcCombatCadence.SpellFailureEnergyFormulaId;
    public const string NpcPhysicalEarlyEndFormulaId = RuntimeClassicNpcCombatCadence.PhysicalEarlyEndFormulaId;
    public const string NpcEncounterEnergyResetFormulaId = "combat.npc_encounter_energy_start";
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
            authority.RequireExecutable(NpcAttackAttemptsFormulaId);
            authority.RequireExecutable(NpcAttackEnergyFormulaId);
            authority.RequireExecutable(NpcAttackSelectionWeightFormulaId);
            authority.RequireExecutable(NpcSpellCastSuccessFormulaId);
            authority.RequireExecutable(NpcResistedSpellEnergyRefundFormulaId);
            authority.RequireExecutable(NpcPhysicalEarlyEndFormulaId);
            authority.RequireExecutable(NpcEncounterEnergyResetFormulaId);
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
        if (!RuntimeClassicPhysicalFormulaCalculator.IsHitRollSuccessful(hitChance, hitRoll))
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
        // Native _calculate_attack applies Bash/Smash's x3/x5 multiplier after DR but
        // before the defender Dodge roll. Keep that order even though a successful
        // dodge ultimately zeros damage either way.
        var postSpecialDamage = checked(postDrDamage * modifiers.PostDrMultiplier);
        var dodgeChance = CalculateDodgeChance(request.AttackType, request.Attacker.AttackValue, request.Defender.DodgeBonus);
        if (dodgeChance > 0 && RuntimeClassicPhysicalFormulaCalculator.IsDodgeRollSuccessful(
                dodgeChance, _random.NextInclusive(1, 100)))
        {
            return new ClassicAttackResult(ClassicHitOutcome.Dodged, hitChance, dodgeChance, rolledDamage, postSpecialDamage, 0, critical);
        }

        var finalDamage = Math.Max(0, postSpecialDamage);
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
        // Stock MajorMUD clamps physical hit chance to 8..99. Backstab receives
        // a pre-resolved stock defense value from the caller (AC/4 + BS defense
        // for ordinary NPCs, or the appropriate see-hidden variant).
        if (attackType == ClassicAttackType.Backstab)
        {
            if (defender.DefenseValue <= 0) return 99;
            return Math.Clamp(attacker.AttackValue - defender.DefenseValue, 8, 99);
        }

        var adjustedAttackValue = Math.Clamp(
            attacker.AttackValue + attackAccuracyModifier + defender.ImposedAccuracyModifier,
            1, 9999);
        var defense = Math.Clamp(checked(defender.DefenseValue + defender.ArmorClass), 0, 9999);
        if (defense <= 0) return 99;

        // MMUD Explorer CalculateAttackDefense: accTemp=(ACCY*ACCY)\140, min 1;
        // hit=100-((Defense*Defense)\accTemp), then stock clamp 8..99.
        var hitRating = Math.Max(1L, ((long)adjustedAttackValue * adjustedAttackValue) / 140L);
        var chance = 100 - (int)(((long)defense * defense) / hitRating);
        return Math.Clamp(chance, 8, 99);
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
    public const string StockAccuracyFormulaId = "combat.stock_player_accuracy.native_0357";

    // MMUD Explorer frmMain.frm 27694: Fix((STR - 50) / 10).
    // C# integer division truncates toward zero, matching VB6 Fix for integer inputs.
    public static int CalculateStrengthMaximumDamageBonus(int strength)
        => (strength - 50) / 10;


    // Native WCCMMUD.DLL _move_player_to_fighter (0x42AD90-0x42AF98).
    // The original DLL folds signed equipped Accuracy and the <33% living encumbrance
    // bonus together, rounds that bucket down/toward-zero to an even contribution,
    // then applies class combat, floor-sqrt(level), Level/2, STR/3 and AGI/6.
    public static int CalculateStockPlayerAccuracyBase(
        int classCombatCode, int level, int strength, int agility,
        int equippedAccuracy, int encumbrancePercent, bool living = true)
    {
        if (classCombatCode <= 0) return 0;
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (encumbrancePercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(encumbrancePercent));

        var bucket = equippedAccuracy == 0 ? 1 : equippedAccuracy;
        if (living && encumbrancePercent < 33)
            bucket = checked(bucket + 15 - (encumbrancePercent / 10));

        var root = (int)Math.Sqrt(level);
        while ((long)(root + 1) * (root + 1) <= level) root++;
        while ((long)root * root > level) root--;

        var inner = checked(
            checked(root * (classCombatCode - 1))
            + checked(classCombatCode * 2)
            + (level / 2)
            + (bucket / 2)
            - 2);
        return checked(
            checked(inner * 2)
            + ((strength - 50) / 3)
            + ((agility - 50) / 6));
    }

    // MMUD Explorer modMMudFunc.bas 2111-2132, stock MajorMUD branch.
    // Stock uses Fix((Stealth + Agility) / 2) + Fix(BSAccu / 2), then
    // +5 for class stealth or -15 for race-only stealth, followed by normal accuracy bonuses.
    public static int CalculateBackstabAccuracy(int stealth, int agility, int backstabAccuracyBonus, bool classStealth, int normalAccuracyBonus)
    {
        var accuracy = checked(((stealth + agility) / 2) + (backstabAccuracyBonus / 2));
        accuracy = checked(accuracy + (classStealth ? 5 : -15));
        return checked(accuracy + normalAccuracyBonus);
    }

    // Native WCCMMUD.DLL _move_player_to_fighter (0x42B064-0x42B119) directly closes
    // both Backstab endpoints: 2*normal + 2*Level + floor(Stealth/10) + ability 117/118,
    // optional race-only 75%, then (Level+100)%. MMUD Explorer independently agrees.
    public static int CalculateBackstabDamage(int level, int stealth, int normalDamage, int backstabDamageBonus, bool classStealth)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (normalDamage < 0) throw new ArgumentOutOfRangeException(nameof(normalDamage));
        var damage = checked((level * 2) + (stealth / 10) + (normalDamage * 2) + backstabDamageBonus);
        if (!classStealth) damage = checked((damage * 75) / 100);
        return checked(((level + 100) * damage) / 100);
    }

    // MMUD Explorer modMMudFunc.bas 4678 and Nightmare Redux frmSwingCalc.frm 2006.
    // The speedAdjustmentPercent parameter is retained for tooling/compatibility calculators.
    // CodeUpdate0369 proves WCCMMUD.INI UPDATE_SPEED belongs to Worldgroup update/poll pacing
    // and is not read by WCCMMUD!_compute_energy_used; live Classic gameplay therefore enters
    // this formula through CalculateClassicLiveAttackEnergyCost at the native 100% branch.
    public static int CalculateAttackEnergyCost(
        int combatCode,
        int level,
        int weaponSpeed,
        int agility,
        int strength = 0,
        int encumbrancePercent = -1,
        int itemStrengthRequirement = 0,
        int speedAdjustmentPercent = 0,
        bool isBackstab = false)
    {
        if (combatCode < 0) throw new ArgumentOutOfRangeException(nameof(combatCode));
        if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (weaponSpeed < 0) throw new ArgumentOutOfRangeException(nameof(weaponSpeed));
        if (encumbrancePercent < -1) throw new ArgumentOutOfRangeException(nameof(encumbrancePercent));
        if (itemStrengthRequirement < 0) throw new ArgumentOutOfRangeException(nameof(itemStrengthRequirement));
        if (speedAdjustmentPercent < 0) throw new ArgumentOutOfRangeException(nameof(speedAdjustmentPercent));
        if (weaponSpeed == 0) return 0;

        var denominator = checked((((level * (combatCode + 2)) + 45) * (agility + 150)) / 6);
        if (denominator <= 0) throw new InvalidOperationException("CLASSIC_ATTACK_ENERGY_DENOMINATOR_INVALID");
        var energy = checked((weaponSpeed * 1000) / denominator);

        // MMUD Explorer CalcEnergyUsed: failing the wielded weapon's STR requirement
        // raises energy by 1.5% for each missing STR point.
        if (strength > 0 && strength < itemStrengthRequirement)
            energy = checked(((((itemStrengthRequirement - strength) * 3) + 200) * energy) / 200);

        // Analysis/compatibility branch only. It is not sourced from WCCMMUD.INI UPDATE_SPEED.
        if (speedAdjustmentPercent > 0 && speedAdjustmentPercent != 100 && !isBackstab)
            energy = checked((energy * speedAdjustmentPercent) / 100);

        // Stock encumbrance is part of attack energy itself. At 0% encumbrance this
        // is 75% of base energy; at 50% it is 100%; at 100% it is 125%.
        if (encumbrancePercent >= 0)
            energy = checked((energy * ((encumbrancePercent / 2) + 75)) / 100);

        return energy;
    }

    public static int CalculateClassicLiveAttackEnergyCost(
        int combatCode,
        int level,
        int weaponSpeed,
        int agility,
        int strength = 0,
        int encumbrancePercent = -1,
        int itemStrengthRequirement = 0,
        bool isBackstab = false) =>
        CalculateAttackEnergyCost(
            combatCode, level, weaponSpeed, agility, strength, encumbrancePercent,
            itemStrengthRequirement, RuntimeClassicAttackSpeedBoundaryAuthority.ClassicLiveAttackSpeedPercent, isBackstab);

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

    // Stock MajorMUD physical hit checks use an inclusive 1..100 roll.
    // The stock hit floor/cap are 8/99, and a roll equal to the final chance succeeds.
    public static bool IsHitRollSuccessful(int hitChance, int roll)
    {
        if (roll < 1 || roll > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll <= Math.Clamp(hitChance, 0, 100);
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
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(6, 1, 50, 50, 0, 0) == 46, "stock-accuracy-level-one-warrior-vector"); report.Checks.Add("stock-accuracy-level-one-warrior-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(6, 20, 80, 60, 3, 0) == 109, "stock-accuracy-class-stat-equipment-vector"); report.Checks.Add("stock-accuracy-class-stat-equipment-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(4, 20, 49, 49, 2, 32) == 70, "stock-accuracy-even-fold-vector"); report.Checks.Add("stock-accuracy-even-fold-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(4, 20, 49, 49, 1, 32) == 68, "stock-accuracy-odd-fold-vector"); report.Checks.Add("stock-accuracy-odd-fold-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(3, 50, 40, 40, -3, 33) == 80, "stock-accuracy-signed-truncation-vector"); report.Checks.Add("stock-accuracy-signed-truncation-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabAccuracy(120, 80, 10, true, 5) == 115, "backstab-accuracy-stock-known-vector"); report.Checks.Add("backstab-accuracy-stock-known-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(20, 120, 8, 3, true) == 85, "backstab-damage-known-vector"); report.Checks.Add("backstab-damage-known-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(1, 0, 1, 0, true) == 4, "backstab-damage-classic-level-scale"); report.Checks.Add("backstab-damage-classic-level-scale");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(20, 120, 8, 3, false) == 63, "backstab-damage-race-only-75-percent"); report.Checks.Add("backstab-damage-race-only-75-percent");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(50) == 0, "strength-50-zero"); report.Checks.Add("strength-50-zero");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(100) == 5, "strength-100-plus-five"); report.Checks.Add("strength-100-plus-five");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(49) == 0, "strength-fix-truncation"); report.Checks.Add("strength-fix-truncation");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateStrengthMaximumDamageBonus(40) == -1, "strength-negative-bonus"); report.Checks.Add("strength-negative-bonus");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(2, 20, 100, 50) == 24, "attack-energy-known-vector"); report.Checks.Add("attack-energy-known-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(2, 20, 100, 50, 50, 0, 50) == 18, "attack-energy-zero-encumbrance-75-percent"); report.Checks.Add("attack-energy-zero-encumbrance-75-percent");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(2, 20, 100, 50, 40, 50, 60) == 31, "attack-energy-strength-requirement-and-medium-encumbrance"); report.Checks.Add("attack-energy-strength-requirement-and-medium-encumbrance");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(2, 20, 100, 50, 50, 100, 50) == 30, "attack-energy-heavy-encumbrance-125-percent"); report.Checks.Add("attack-energy-heavy-encumbrance-125-percent");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(2, 20, 100, 50, 50, 50, 50, 80) == 19, "attack-energy-analysis-speed-adjustment"); report.Checks.Add("attack-energy-analysis-speed-adjustment");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateAttackEnergyCost(2, 20, 100, 50, 50, 50, 50, 80, true) == 24, "backstab-analysis-ignores-speed-adjustment"); report.Checks.Add("backstab-analysis-ignores-speed-adjustment");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateClassicLiveAttackEnergyCost(2, 20, 100, 50, 50, 50, 50) == 24, "attack-energy-live-classic-native-default"); report.Checks.Add("attack-energy-live-classic-native-default");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateNaturalCriticalChance(1, 50, 50, 50) == 1, "natural-critical-minimum-one"); report.Checks.Add("natural-critical-minimum-one");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateNaturalCriticalChance(50, 100, 90, 80) == 13, "natural-critical-stat-vector"); report.Checks.Add("natural-critical-stat-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateNaturalCriticalChance(500, 500, 500, 500) == 75, "natural-critical-cap-75"); report.Checks.Add("natural-critical-cap-75");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(50, 190, 0) == 10, "qnd-base-vector"); report.Checks.Add("qnd-base-vector");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(100, 100, 0) == 20, "qnd-cap-20"); report.Checks.Add("qnd-cap-20");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateQuickAndDeadlyCriticalBonus(50, 190, 33) == 5, "qnd-encumbrance-halves"); report.Checks.Add("qnd-encumbrance-halves");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(40) == 40, "final-critical-no-diminishing-through-forty"); report.Checks.Add("final-critical-no-diminishing-through-forty");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(43) == 41, "final-critical-one-third-excess"); report.Checks.Add("final-critical-one-third-excess");
            Require(RuntimeClassicPhysicalFormulaCalculator.CalculateFinalCriticalChance(1000) == 99, "final-critical-cap-99"); report.Checks.Add("final-critical-cap-99");
            Require(RuntimeClassicPhysicalFormulaCalculator.IsHitRollSuccessful(8, 8), "hit-inclusive-floor-boundary"); report.Checks.Add("hit-inclusive-floor-boundary");
            Require(!RuntimeClassicPhysicalFormulaCalculator.IsHitRollSuccessful(8, 9), "hit-floor-next-roll-fails"); report.Checks.Add("hit-floor-next-roll-fails");
            Require(RuntimeClassicPhysicalFormulaCalculator.IsHitRollSuccessful(99, 99), "hit-inclusive-cap-boundary"); report.Checks.Add("hit-inclusive-cap-boundary");
            Require(!RuntimeClassicPhysicalFormulaCalculator.IsHitRollSuccessful(99, 100), "hit-cap-roll-100-fails"); report.Checks.Add("hit-cap-roll-100-fails");
            var hitFloor = ClassicCombatResolver.CalculateHitChance(ClassicAttackType.NormalWeapon, new ClassicCombatantStats(1, 0, 0, 0, 0), new ClassicCombatantStats(0, 0, 9999, 0, 0));
            Require(hitFloor == 8, "stock-hit-floor-eight"); report.Checks.Add("stock-hit-floor-eight");
            var hitCap = ClassicCombatResolver.CalculateHitChance(ClassicAttackType.NormalWeapon, new ClassicCombatantStats(100, 0, 0, 0, 0), new ClassicCombatantStats(0, 0, 0, 0, 0));
            Require(hitCap == 99, "stock-hit-cap-ninety-nine"); report.Checks.Add("stock-hit-cap-ninety-nine");
            var zeroBackstabDefense = ClassicCombatResolver.CalculateHitChance(ClassicAttackType.Backstab, new ClassicCombatantStats(25, 0, 0, 0, 0), new ClassicCombatantStats(0, 0, 0, 0, 0));
            Require(zeroBackstabDefense == 99, "stock-backstab-zero-defense-cap"); report.Checks.Add("stock-backstab-zero-defense-cap");
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
