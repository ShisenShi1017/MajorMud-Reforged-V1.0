namespace Reforged.Server;

public static class RuntimeResolvedStatusApplicationSmokeReport
{
    public static IReadOnlyList<string> CoveredBehaviors => new[]
    {
        "save-adjusted status duration",
        "minimum-duration clamping",
        "promoted duration rounding",
        "refresh uses adjusted duration",
        "replacement uses adjusted duration",
        "independent stack uses adjusted duration",
        "magnitude and secondary-effect suppression",
        "fail-closed unresolved duration policy"
    };
}
