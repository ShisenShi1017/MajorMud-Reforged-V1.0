namespace Reforged.Server;

/// <summary>
/// Native MajorMUD 1.11p configuration evidence retained from WCCMMUD.mcv / WCCMMUD.INI.
/// Reforged does not ship, read, or write a WCCMMUD.INI file. These values are evidence-backed
/// Classic defaults that are projected into Reforged's own typed/runtime configuration owners.
/// Unknown native ranges and live-reload semantics remain outside this authority.
/// </summary>
public static class RuntimeClassicConfigurationAuthority
{
    public const string NativeConfigurationFileName = "WCCMMUD.INI";
    public static readonly bool ReforgedUsesNativeConfigurationFile = false;
    public const int NativeConfigurationPromptCount = 44;

    // Exact retained WCCMMUD.mcv defaults used by active Reforged Classic owners.
    public const int PlayerDataSaveIntervalSeconds = 60;
    public const int BufferDataSaveIntervalSeconds = 300;
    public const int PvpDisabledLevelDifference = -1;
    public const int MinimumPvpLevelDifference = -1;
    public const int MaximumPvpLevelDifference = 100;
    public const int MaximumPvpLevelDifferenceAllowed = 100;
    public const int InactivitySeconds = 5000;
    public const int MaximumTopTenEntries = 30;
    public const int EvilPointsRetainedAfterFinalDeathPercent = 50;
    public const int GenderChangeGoldCost = 5000;
    public const int MaximumEvilPointsForgivenPerDay = 18;
    public const int EvilPointCycleMinutes = 60;
    public const int EvilPointsForgivenPerCycle = 1;
    public const bool LawfulChoiceEnabled = true;
    public const bool KeepExperienceAfterFinalDeath = true;
    public const bool AllowSysopLightningOnAllPlayers = true;
    public const bool ProfanityCheckingEnabled = false;
    public const bool SweepCoinsFromGangHouses = false;
    public const string SweepCoinThreshold = "GOLD";

    /// <summary>
    /// Exact printable setup prompts retained in WCCMMUD.mcv. This is an evidence catalog,
    /// not a parser for a legacy INI file and not permission to invent undocumented ranges.
    /// </summary>
    public static IReadOnlyList<(int Offset, string PromptWithDefault)> NativePrompts { get; } = new (int, string)[]
    {
        (38, "Credits per minute consumed while in the game 60"),
        (87, "Credits consumed per minute by ALL users (even Exempt) 0"),
        (264, "Seconds between saving Player data? 60"),
        (303, "Seconds between saving Buffer data? 300"),
        (343, "Number of buffers to save/set? 1"),
        (376, "Number of rooms to buffer in memory? 1500"),
        (418, "Number of items to buffer in memory? 750"),
        (459, "Number of active monsters to buffer? 2000"),
        (501, "Number of known monsters to buffer? 300"),
        (541, "Number of spells to buffer in memory? 150"),
        (583, "Number of races to buffer in memory? 12"),
        (623, "Number of classes to buffer in memory? 16"),
        (665, "Number of bankbooks to buffer in memory? 32"),
        (709, "Number of messages to buffer in memory? 250"),
        (753, "Number of messages to buffer in memory? 50"),
        (796, "Number of messages to buffer in memory? 10"),
        (839, "Character to start lines for talking (slow): ."),
        (886, "Character to start lines for yelling: \""),
        (941, "Number of hit points at which the user dies? -100"),
        (991, "Extra Levels of Experience before Training? 10"),
        (1038, "Level of punishment for hanging up? NONE"),
        (1097, "Maximum number of TOPTEN users/gangs lists? 30"),
        (1144, "Percentage of evil points to keep? 50"),
        (1188, "Profanity checking? NO"),
        (1211, "Rate to perform initialization? 80"),
        (1246, "Relative polling counter? 750"),
        (1276, "Maximum level attainable without NODMOKEY? 2"),
        (1321, "Include experience in topten command? YES"),
        (1363, "Maximum level difference for PVP? 100"),
        (1401, "Minimum percentage to lose on hangup? 0"),
        (1441, "Maximum percentage to lose on hangup? 0"),
        (1481, "Maximum items to lose on hangup? 0"),
        (1516, "Number of seconds of inactivity allowed? 5000"),
        (1563, "Keep some experience after final death? YES"),
        (1607, "Allow SYSOP LIGHTNING on all players? YES"),
        (1649, "Gold to charge to change gender? 5000"),
        (1687, "Experience (x10000) required? 1000"),
        (1722, "Delete MajorMUD user with BBS User? NO"),
        (1761, "Max EPs to forgive per day? 18"),
        (1792, "Minutes per Evil Point Cycle? 60"),
        (1825, "EPs to forgive per cycle? 1"),
        (1853, "Enable the choice of Lawful? YES"),
        (1886, "Sweep coins of this and lower? GOLD"),
        (1922, "Sweep coins from gang houses? NO")
    };

    public static IReadOnlyList<string> ValidateEvidenceCatalog()
    {
        var failures = new List<string>();
        if (ReforgedUsesNativeConfigurationFile) failures.Add("reforged-must-not-use-wccmmud-ini");
        if (NativePrompts.Count != NativeConfigurationPromptCount) failures.Add("native-configuration-prompt-count");
        if (NativePrompts.Select(row => row.Offset).Distinct().Count() != NativePrompts.Count) failures.Add("native-configuration-prompt-offset-duplicate");
        if (NativePrompts.Any(row => string.IsNullOrWhiteSpace(row.PromptWithDefault))) failures.Add("native-configuration-prompt-empty");
        return failures;
    }
}
