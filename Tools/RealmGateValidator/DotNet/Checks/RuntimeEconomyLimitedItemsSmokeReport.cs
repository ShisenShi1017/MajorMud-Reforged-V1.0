namespace RealmGate.Server;
public static class RuntimeEconomyLimitedItemsSmokeReport
{
    public static readonly string[] Checks={
        "world-limit-reservation-and-overflow-protection",
        "unique-owner-enforcement",
        "merchant-and-loot-eligibility-integration",
        "activation-release-destruction-recycling",
        "deterministic-respawn-processing",
        "atomic-ledger-rollback-on-persistence-failure",
        "replay-protection",
        "versioned-limited-item-persistence"
    };
}
