namespace Reforged.Server;

public static class RuntimeCombatStatusIntegrationSmokeReport
{
    public static IReadOnlyList<string> CoveredBehaviors => new[]
    {
        "attacker status modifies physical attack input",
        "defender status modifies physical defense input",
        "physical attack restriction fails before turn consumption",
        "spell cast restriction fails before resource reservation",
        "hidden-break signal survives attack and cast integration",
        "status-adjusted initiative and speed reorder scheduled turns deterministically",
        "movement restriction fails before invalidation, threat cleanup, or room handoff",
        "missing actor effect snapshots fail closed",
        "integration traces preserve contributing evidence"
    };
}
