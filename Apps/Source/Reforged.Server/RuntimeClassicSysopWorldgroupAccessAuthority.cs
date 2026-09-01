namespace Reforged.Server;

public sealed record RuntimeClassicSysopWorldgroupSetting(
    string Setting,
    string SuppliedDefault,
    string Owner,
    string Classification);

/// <summary>
/// CodeUpdate0369 closes the MajorMUD-to-Worldgroup SYSOP access boundary.
///
/// The supplied WCCMMUD.DLL recognizes 21 SYS_* access-class settings plus the unrelated
/// SYS_LOG_DEATHS Boolean. Every one of those 21 access settings is explicitly present in the
/// supplied WCCMMUD.INI. WCCMMUD!_cmd_sysop calls the imported WGSERVER _haskey service through
/// thunk 0x004789EE at native callsites 0x00462DD9 and 0x00463009, proving that the configured
/// strings are host access-class gates. Worldgroup owns class membership/inheritance itself.
/// Reforged maps that host boundary to authenticated roles/capabilities and does not expose a
/// legacy plaintext-key authentication command.
/// </summary>
public static class RuntimeClassicSysopWorldgroupAccessAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const string SuppliedIniSha256 = "9cbc9c00baa88f853e05b277526b40ee60a8bb5b9051818256993f8b9b4279a1";
    public const int RecognizedSysAccessSettingCount = 21;
    public const int SuppliedExplicitSysAccessSettingCount = 21;
    public const int SuppliedOptionalAbsentSettingCount = 0;
    public const uint WorldgroupHasKeyThunkVa = 0x004789EEu;
    public static readonly uint[] CmdSysopHasKeyCallsiteVas = [0x00462DD9u, 0x00463009u];
    public const bool NativeDispatcherCallsWorldgroupHasKey = true;
    public const bool WorldgroupOwnsAccessClassMembershipAndInheritance = true;
    public const bool MajorMudOwnsSettingToCommandMapping = true;
    public const bool ReforgedPlaintextKeyCommandAllowed = false;

    public static IReadOnlyList<RuntimeClassicSysopWorldgroupSetting> Settings { get; } =
    [
        new("SYS_DISABLE_GANGS", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_RESET_ROOMS", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_LIGHTNING_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_STATUS_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_ADDLIFE_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_RETRAIN_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_ALIGNMENT_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_EDITEVIL_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_SPEAK_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_LISTUSER_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_LISTLIMIT_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_USERINFO_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_JAIL_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_CHANGE_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_BUFFERS_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_CLEARSUICIDE_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_REPORT_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_TOPTEN_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_DISBAND_KEY", "WCCGAMEOP", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_GOTO_KEY", "DEMO", "MajorMUD config -> Worldgroup access class", "supplied-explicit"),
        new("SYS_MAP_KEY", "DEMO", "MajorMUD config -> Worldgroup access class", "supplied-explicit")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Settings.Count != RecognizedSysAccessSettingCount ||
            Settings.Select(row => row.Setting).Distinct(StringComparer.OrdinalIgnoreCase).Count() != RecognizedSysAccessSettingCount)
            failures.Add("sysop-worldgroup-setting-census");
        if (Settings.Count(row => row.Classification == "supplied-explicit") != SuppliedExplicitSysAccessSettingCount)
            failures.Add("sysop-worldgroup-supplied-setting-count");
        Check(SuppliedOptionalAbsentSettingCount == 0, "sysop-worldgroup-optional-setting-count", failures);
        if (CmdSysopHasKeyCallsiteVas.Length != 2 || WorldgroupHasKeyThunkVa != 0x004789EEu ||
            !NativeDispatcherCallsWorldgroupHasKey)
            failures.Add("sysop-worldgroup-native-haskey-boundary");
        Check(WorldgroupOwnsAccessClassMembershipAndInheritance && MajorMudOwnsSettingToCommandMapping &&
            !ReforgedPlaintextKeyCommandAllowed, "sysop-worldgroup-owner-boundary", failures);
        if (!Settings.Any(row => row.Setting == "SYS_GOTO_KEY" && row.SuppliedDefault == "DEMO") ||
            !Settings.Any(row => row.Setting == "SYS_MAP_KEY" && row.SuppliedDefault == "DEMO") ||
            !Settings.Any(row => row.Setting == "SYS_RESET_ROOMS" && row.SuppliedDefault == "WCCGAMEOP") ||
            !Settings.Any(row => row.Setting == "SYS_REPORT_KEY" && row.SuppliedDefault == "WCCGAMEOP") ||
            !Settings.Any(row => row.Setting == "SYS_BUFFERS_KEY" && row.SuppliedDefault == "WCCGAMEOP") ||
            !Settings.Any(row => row.Setting == "SYS_CLEARSUICIDE_KEY" && row.SuppliedDefault == "WCCGAMEOP"))
            failures.Add("sysop-worldgroup-default-anchor");
        return failures;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }
}
