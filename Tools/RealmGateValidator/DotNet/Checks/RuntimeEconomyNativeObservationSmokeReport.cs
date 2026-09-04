namespace RealmGate.Server;

public static class RuntimeEconomyNativeObservationSmokeReport
{
    public static IReadOnlyList<string> ExpectedChecks=>new[]{
        "reject source fingerprint mismatch",
        "reject duplicate assertion IDs",
        "reject unknown dependency IDs",
        "reject unresolved assertion sources",
        "require two observed assertions from two independent sources",
        "reject contradictory observations",
        "require stable output digests before promotion",
        "promote only individually proven dependencies",
        "retain fail-closed state for all remaining dependencies"
    };
}
