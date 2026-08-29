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
        if (evilPoints >= FiendAttackLockoutEvilPoints)
            consequence |= RuntimeClassicLawConsequence.AttackInitiationLocked;
        return consequence;
    }

    public static int ApplyLawfulAttackMultiplier(int basePenalty, bool targetIsLawful)
    {
        if (basePenalty < 0) throw new ArgumentOutOfRangeException(nameof(basePenalty));
        return targetIsLawful ? checked(basePenalty * LawfulAttackPenaltyMultiplier) : basePenalty;
    }

    public static bool IsLevelRangeAllowed(int attackerLevel, int defenderLevel, int maximumLevelDifference)
    {
        if (attackerLevel < 1 || defenderLevel < 1) throw new ArgumentOutOfRangeException(nameof(attackerLevel), "PvP levels must be positive.");
        // Setup examples use -1 to mean unrestricted; zero is also treated as no configured range.
        return maximumLevelDifference <= 0 || Math.Abs(attackerLevel - defenderLevel) <= maximumLevelDifference;
    }

    public static int RequirePracticeArenaEntryHitPoints(RuntimeCharacterRecord character)
    {
        if (!character.Conditions.TryGetValue(PracticeArenaEntryHpCondition, out var raw) ||
            !int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var hp) || hp < 1)
            throw new InvalidOperationException("Practice-arena entry HP was not captured; arena death fails closed rather than consuming a life or inventing restoration HP.");
        return Math.Min(character.MaximumHitPoints, hp);
    }
}
