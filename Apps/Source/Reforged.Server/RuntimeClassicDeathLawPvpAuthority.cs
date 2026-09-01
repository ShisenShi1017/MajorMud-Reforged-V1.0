namespace Reforged.Server;

[Flags]
public enum RuntimeClassicLawConsequence
{
    None = 0,
    GuardsAttackUntilDowned = 1 << 0,
    GuardsAttackUntilDeath = 1 << 1,
    // Bit 2 intentionally remains unused in executable Classic enforcement. The archived
    // MajorMUD legal-system documentation assigns bounty-hunter escalation to Villain while
    // native player help describes possible divine intervention at Villain. Until that source
    // conflict is reconciled against the target 1.11p native runtime, do not guess the actor.
    DivineInterventionEligible = 1 << 3,
    AttackInitiationLocked = 1 << 4
}

/// <summary>
/// Evidence-backed Classic MajorMUD death/law/PvP constants recovered from retained native help,
/// WCC configuration prompts, and the archived MajorMUD legal-system documentation.  This class
/// intentionally describes only proven/strong contracts.  It does not invent guard spawn cadence,
/// bounty arithmetic, normal-death XP loss, or disconnect punishment formulas.
/// </summary>
public static class RuntimeClassicDeathLawPvpAuthority
{
    public const int GracefulExitDelaySeconds = RuntimeClassicTimingAuthority.GracefulExitDelaySeconds;
    public const int FiendAttackLockoutEvilPoints = 300;
    public const int LawfulAttackPenaltyMultiplier = 3;
    public const int MinimumPvpLevel = 4;
    public const int PlayerAttackBaseEvilPoints = 10;
    public const int EvilRelationSlowUpdateRounds = 11;
    public const int NativePracticeArenaRoomType = 5;
    public const int DefaultGoodRespawnMap = 1;
    public const int DefaultGoodRespawnRoom = 2189;
    public const int DefaultEvilRespawnMap = 1;
    public const int DefaultEvilRespawnRoom = 142;
    public const int EvilRespawnThreshold = 40;
    public const string PracticeArenaEntryHpCondition = "classic.practice_arena.entry_hp";

    public static bool IsPracticeArena(RuntimeRoom room, bool arenaEnabled) =>
        arenaEnabled && room.EnvironmentRoomType == NativePracticeArenaRoomType;

    public static bool IsArenaEnabled(string projectRoot, string worldId)
    {
        var path = Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "sysop", $"{RuntimeIdentity.Normalize(worldId, "world")}.arena");
        // Reforged's SYSOP ARENA COMBAT maps to the native global byte = 1; NORMAL maps to 0.
        return File.Exists(path) && File.ReadAllText(path).Trim().Equals("combat", StringComparison.OrdinalIgnoreCase);
    }

    public static RuntimeRoom ResolveDeathDestination(RuntimeWorld world, RuntimeRoom deathRoom, int evilPoints)
    {
        if (deathRoom.EnvironmentDeathRoomId > 0)
            return world.FindRoom(deathRoom.MapId, deathRoom.EnvironmentDeathRoomId)
                ?? throw new InvalidOperationException($"Native death-room destination {deathRoom.MapId}/{deathRoom.EnvironmentDeathRoomId} is unavailable.");
        var roomNumber = evilPoints >= EvilRespawnThreshold ? DefaultEvilRespawnRoom : DefaultGoodRespawnRoom;
        return world.FindRoom(1, roomNumber)
            ?? throw new InvalidOperationException($"Native default death-room destination 1/{roomNumber} is unavailable.");
    }

    public static RuntimeClassicLawConsequence ResolveLawConsequence(string reputation, int evilPoints)
    {
        var consequence = reputation.Trim().ToUpperInvariant() switch
        {
            "OUTLAW" => RuntimeClassicLawConsequence.GuardsAttackUntilDowned,
            "CRIMINAL" => RuntimeClassicLawConsequence.GuardsAttackUntilDeath,
            // Native HL1B11 says Villain is Criminal plus possible intervention by the "Gods".
            // The archived legal-system operator documentation instead places bounty hunters here
            // and divine lightning at Fiend. Preserve the directly rendered help behavior while the
            // conflicting enforcement actor is still evidence-gated.
            "VILLAIN" or "VILLIAN" => RuntimeClassicLawConsequence.GuardsAttackUntilDeath | RuntimeClassicLawConsequence.DivineInterventionEligible,
            // Fiend escalation is source-conflicted; preserve the independently proven 300-EP
            // attack lockout below and fail closed on additional active enforcement.
            "FIEND" => RuntimeClassicLawConsequence.None,
            _ => RuntimeClassicLawConsequence.None
        };
        if (evilPoints > FiendAttackLockoutEvilPoints)
            consequence |= RuntimeClassicLawConsequence.AttackInitiationLocked;
        return consequence;
    }

    public static int ApplyLawfulAttackMultiplier(int basePenalty, bool targetIsLawful)
    {
        if (basePenalty < 0) throw new ArgumentOutOfRangeException(nameof(basePenalty));
        return targetIsLawful ? checked(basePenalty * LawfulAttackPenaltyMultiplier) : basePenalty;
    }

    public static bool IsPvpEnabled(int maximumLevelDifference)
    {
        if (maximumLevelDifference < RuntimeClassicConfigurationAuthority.MinimumPvpLevelDifference ||
            maximumLevelDifference > RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifferenceAllowed)
            throw new ArgumentOutOfRangeException(nameof(maximumLevelDifference), "Classic LVLDIFF must be in the native -1..100 range.");
        return maximumLevelDifference != RuntimeClassicConfigurationAuthority.PvpDisabledLevelDifference;
    }

    public static bool IsLevelRangeAllowed(int attackerLevel, int defenderLevel, int maximumLevelDifference, bool defenderIsFiend = false)
    {
        if (attackerLevel < 1 || defenderLevel < 1) throw new ArgumentOutOfRangeException(nameof(attackerLevel), "PvP levels must be positive.");
        if (!IsPvpEnabled(maximumLevelDifference)) return false;
        if (attackerLevel < MinimumPvpLevel || defenderLevel < MinimumPvpLevel) return false;

        // Native WCCMMUD setup: lower-level FIENDS do not receive level-range protection.
        if (defenderIsFiend && defenderLevel < attackerLevel) return true;

        // Zero is a valid native setting and therefore permits same-level PvP only.
        return Math.Abs(attackerLevel - defenderLevel) <= maximumLevelDifference;
    }


    public static int CalculatePlayerAttackEvilPoints(int defenderEvilPoints, bool defenderIsLawful)
    {
        // _attack_user_user passes base 10 into _add_evil_points. _add_evil_points suppresses
        // the award for Seedy-or-worse targets (EP >= 30), triples it for Lawful/Saint,
        // doubles it for Good, and otherwise preserves the ten-point base.
        if (defenderEvilPoints >= 30) return 0;
        if (defenderIsLawful || defenderEvilPoints < -200) return PlayerAttackBaseEvilPoints * 3;
        if (defenderEvilPoints < -50) return PlayerAttackBaseEvilPoints * 2;
        return PlayerAttackBaseEvilPoints;
    }

    public static int RequirePracticeArenaEntryHitPoints(RuntimeCharacterRecord character)
    {
        if (!character.Conditions.TryGetValue(PracticeArenaEntryHpCondition, out var raw) ||
            !int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var hp) || hp < 1)
            throw new InvalidOperationException("Practice-arena entry HP was not captured; arena death fails closed rather than consuming a life or inventing restoration HP.");
        return Math.Min(character.MaximumHitPoints, hp);
    }
}
