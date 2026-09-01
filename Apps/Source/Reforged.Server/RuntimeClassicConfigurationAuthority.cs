namespace Reforged.Server;

/// <summary>
/// Native MajorMUD 1.11p configuration evidence retained from WCCMMUD.mcv / WCCMMUD.INI.
/// Reforged does not ship, read, or write a WCCMMUD.INI file. These values are evidence-backed
/// Classic defaults that are projected into Reforged's own typed/runtime configuration owners.
/// CodeUpdate0368 recovers the exact 44-prompt visible setup map, native option IDs,
/// value types/ranges, and reload-vs-startup ownership from _reload__wccmmud/_init__wccmmud.
/// The two retained coin-sweep prompts have no direct setup-read call in this 1.11p DLL and
/// are cataloged explicitly as retained-but-unconsumed rather than assigned guessed behavior.
/// </summary>
public enum RuntimeClassicConfigurationLoadOwner
{
    Reloadable,
    StartupOnly,
    CatalogOnlyNoNativeLoadCall
}

public sealed record RuntimeClassicConfigurationOptionAuthority(
    int OptionId,
    string Symbol,
    string ValueType,
    string DefaultValue,
    int? Minimum,
    int? Maximum,
    RuntimeClassicConfigurationLoadOwner LoadOwner,
    string NativeReader);

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

    /// <summary>
    /// Exact visible WCCMMUD.mcv setup map. Reloadable rows are consumed inside
    /// _reload__wccmmud (0x4011FF..0x401A61). StartupOnly rows are consumed by
    /// _init__wccmmud after reload. COINSWP/GCOINSW are retained in the visible MCV
    /// catalog but have no direct option-read call in either routine in this DLL.
    /// </summary>
    public static IReadOnlyList<RuntimeClassicConfigurationOptionAuthority> VisibleOptionAuthorities { get; } =
    [
        new(4, "GAMCRD", "integer", "60", 0, 32767, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(5, "REALCRD", "integer", "0", 0, 32767, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(19, "SECOTH", "integer", "60", 1, 20000, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(20, "SECBUFF", "integer", "300", 1, 20000, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(21, "BUFTOSAV", "integer", "1", 1, 32767, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(22, "ROOMBUF", "integer", "1500", 10, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(23, "ITEMBUF", "integer", "750", 10, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(24, "MONSBUF", "integer", "2000", 10, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(25, "KMONSBUF", "integer", "300", 10, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(26, "SPELBUF", "integer", "150", 10, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(27, "RACEBUF", "integer", "12", 5, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(28, "CLASSBUF", "integer", "16", 5, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(29, "BANKBUF", "integer", "32", 5, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(30, "MESSBUF", "integer", "250", 5, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(31, "SHOPBUF", "integer", "50", 5, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(32, "GANGBUF", "integer", "10", 3, 32767, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(33, "TALKCHAR", "character", ".", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "chropt"),
        new(34, "YELLCHAR", "character", "\"", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "chropt"),
        new(36, "DEATHHP", "integer", "-100", -20000, 0, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(37, "XTRALVL", "integer", "10", 0, 16, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(38, "PUNHUP", "choice", "NONE", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "add_multiple_option"),
        new(49, "MAXTOP", "integer", "30", 10, 3000, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(50, "EVILKEEP", "integer", "50", 0, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(52, "PROFCHEK", "boolean", "NO", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "ynopt"),
        new(53, "INITRATE", "integer", "80", 10, 20000, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(54, "POLLRATE", "integer", "750", 10, 2000, RuntimeClassicConfigurationLoadOwner.StartupOnly, "numopt"),
        new(55, "DEMOLVL", "integer", "2", 1, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(56, "EXPINTEN", "boolean", "YES", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "ynopt"),
        new(57, "LVLDIFF", "integer", "100", -1, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(58, "MINHUPHP", "integer", "0", 0, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(59, "MAXHUPHP", "integer", "0", 0, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(60, "MAXHUPIT", "integer", "0", 0, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(61, "INACTIVE", "integer", "5000", 60, 32000, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(63, "KEEPEXP", "boolean", "YES", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "ynopt"),
        new(64, "LIGHTEVY", "boolean", "YES", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "ynopt"),
        new(65, "GENDERGO", "integer", "5000", -1, 32000, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(66, "GANGEXP", "integer", "1000", 0, 32767, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(67, "DELUSER", "boolean", "NO", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "ynopt"),
        new(68, "MAXEPDAY", "integer", "18", 4, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(69, "EPCYCLE", "integer", "60", 15, 360, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(70, "EPAMOUNT", "integer", "1", 1, 100, RuntimeClassicConfigurationLoadOwner.Reloadable, "numopt"),
        new(71, "ENABLAWF", "boolean", "YES", null, null, RuntimeClassicConfigurationLoadOwner.Reloadable, "ynopt"),
        new(72, "COINSWP", "choice", "GOLD", null, null, RuntimeClassicConfigurationLoadOwner.CatalogOnlyNoNativeLoadCall, "none"),
        new(73, "GCOINSW", "boolean", "NO", null, null, RuntimeClassicConfigurationLoadOwner.CatalogOnlyNoNativeLoadCall, "none")
    ];

    public const int VisibleReloadableOptionCount = 29;
    public const int VisibleStartupOnlyOptionCount = 13;
    public const int VisibleCatalogOnlyOptionCount = 2;

    public static IReadOnlyList<string> ValidateEvidenceCatalog()
    {
        var failures = new List<string>();
        if (ReforgedUsesNativeConfigurationFile) failures.Add("reforged-must-not-use-wccmmud-ini");
        if (NativePrompts.Count != NativeConfigurationPromptCount) failures.Add("native-configuration-prompt-count");
        if (NativePrompts.Select(row => row.Offset).Distinct().Count() != NativePrompts.Count) failures.Add("native-configuration-prompt-offset-duplicate");
        if (NativePrompts.Any(row => string.IsNullOrWhiteSpace(row.PromptWithDefault))) failures.Add("native-configuration-prompt-empty");
        if (VisibleOptionAuthorities.Count != NativeConfigurationPromptCount) failures.Add("visible-option-authority-count");
        if (VisibleOptionAuthorities.Select(row => row.OptionId).Distinct().Count() != NativeConfigurationPromptCount) failures.Add("visible-option-id-duplicate");
        if (VisibleOptionAuthorities.Count(row => row.LoadOwner == RuntimeClassicConfigurationLoadOwner.Reloadable) != VisibleReloadableOptionCount) failures.Add("visible-reloadable-count");
        if (VisibleOptionAuthorities.Count(row => row.LoadOwner == RuntimeClassicConfigurationLoadOwner.StartupOnly) != VisibleStartupOnlyOptionCount) failures.Add("visible-startup-count");
        if (VisibleOptionAuthorities.Count(row => row.LoadOwner == RuntimeClassicConfigurationLoadOwner.CatalogOnlyNoNativeLoadCall) != VisibleCatalogOnlyOptionCount) failures.Add("visible-catalog-only-count");
        if (VisibleOptionAuthorities.Where(row => row.ValueType == "integer").Any(row => row.Minimum is null || row.Maximum is null)) failures.Add("numeric-range-missing");
        return failures;
    }
}
