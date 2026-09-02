namespace Reforged.Server;
public static class RuntimeEconomyValuationServicesSmokeReport
{
    public static IReadOnlyList<string> CoveredBehaviors => new[] {
        "deterministic-item-valuation", "durability-and-charge-adjustment", "policy-driven-native-unknowns",
        "repair-valuation-integration", "service-provider-ownership", "eligibility-validation",
        "atomic-charge-effect-persist-order", "full-state-rollback", "service-replay-protection",
        "training-spell-ability-boundaries", "resurrection-inn-teleport-guild-callbacks",
        "economic-event-wallet-transfer-integration", "payment-replay-protection", "persistence-roundtrip"
    };
}
