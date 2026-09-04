namespace RealmGate.Server;

public static class RuntimeEconomyNativeCompletionSmokeReport
{
    public static IReadOnlyList<string> ExpectedChecks => new[]
    {
        "27 native bank/service identities validate and bind without inventing fees or limits",
        "23 economy rule groups retain bidirectional shop/item ownership",
        "unresolved timer, formula, banking, limited-item and DLL ordering dependencies fail closed",
        "semantic exhaustion remains false while any blocking native dependency is unresolved",
        "resolved dependencies cannot remain marked as blocking"
    };
}
