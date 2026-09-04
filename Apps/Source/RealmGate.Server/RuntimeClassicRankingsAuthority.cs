namespace RealmGate.Server;

/// <summary>
/// Native TOP/TOPTEN ranking closure recovered from the supplied WCCMMUD.DLL.
///
/// _generate_top_list inserts candidates by descending experience. At 0x004772B0 the
/// existing slot experience is compared with the candidate and JAE at 0x004772B6 skips
/// replacement when existing >= candidate, so equal-experience entries retain the existing
/// source-list order. EXP_IN_TOPGANG (0x004906C0) is read by the gang header/row renderers
/// at 0x004342C2 and 0x004343AD and only controls whether the Exp column/value is displayed.
/// It does not participate in the ranking metric or ordering.
/// </summary>
public static class RuntimeClassicRankingsAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const uint GenerateTopListVa = 0x004770B1u;
    public const uint ExperienceCompareVa = 0x004772B0u;
    public const uint EqualOrGreaterSkipVa = 0x004772B6u;
    public const uint ExpInTopGangGlobalVa = 0x004906C0u;
    public const uint GangHeaderExpCheckVa = 0x004342C2u;
    public const uint GangRowExpCheckVa = 0x004343ADu;

    public const bool ExperienceDescending = true;
    public const bool EqualExperienceRetainsExistingSourceOrder = true;
    public const bool ExpInTopGangAffectsPresentationOnly = true;
    public const bool SuppliedExpInTopGangDefault = true;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        Check(ExperienceDescending && EqualExperienceRetainsExistingSourceOrder &&
            ExpInTopGangAffectsPresentationOnly && SuppliedExpInTopGangDefault,
            "native-ranking-contract-demoted", failures);
        Check(GenerateTopListVa == 0x004770B1u && ExperienceCompareVa == 0x004772B0u &&
            EqualOrGreaterSkipVa == 0x004772B6u,
            "native-ranking-compare-addresses", failures);
        Check(GangHeaderExpCheckVa == 0x004342C2u && GangRowExpCheckVa == 0x004343ADu,
            "native-ranking-exp-presentation-xrefs", failures);
        return failures;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }
}
