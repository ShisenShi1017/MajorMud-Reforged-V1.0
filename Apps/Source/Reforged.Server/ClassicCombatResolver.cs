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
    private readonly IClassicRandom _random;

    public ClassicCombatResolver(IClassicRandom? random = null)
    {
        _random = random ?? new SystemClassicRandom();
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
            _random.NextInclusive(0, 100) < Math.Clamp(request.Attacker.CriticalChance, 0, 100);
        if (critical)
        {
            scaledMinimum = checked(scaledMaximum * 2);
            scaledMaximum = checked(scaledMaximum * 4);
        }

        var rolledDamage = _random.NextInclusive(scaledMinimum, scaledMaximum);
        var postDrDamage = rolledDamage - Math.Max(0, request.Defender.DamageResistance);
        var dodgeChance = CalculateDodgeChance(request.AttackType, request.Attacker.AttackValue, request.Defender.DodgeBonus);
        if (dodgeChance > 0 && _random.NextInclusive(0, 100) < dodgeChance)
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
        if (attackerAttackValue < 9 || defenderDodgeBonus <= 0)
        {
            return 0;
        }

        var divisor = attackerAttackValue >> 3;
        if (divisor <= 0)
        {
            return 0;
        }

        var chance = Math.Min(95, (defenderDodgeBonus * 10) / divisor);
        if (attackType == ClassicAttackType.Backstab)
        {
            chance /= 5;
        }

        return chance;
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
