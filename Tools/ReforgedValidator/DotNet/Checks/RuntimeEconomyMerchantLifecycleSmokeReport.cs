namespace Reforged.Server;

public static class RuntimeEconomyMerchantLifecycleSmokeReport
{
    public static IReadOnlyList<string> CoveredBehaviors => new[]
    {
        "merchant-ownership-binding",
        "shop-hours-and-overnight-windows",
        "visibility-and-customer-restrictions",
        "fixed-quantity-restocking",
        "fill-to-target-restocking",
        "deterministic-weighted-generated-inventory",
        "maximum-stock-enforcement",
        "elapsed-cycle-catch-up",
        "atomic-stock-and-lifecycle-rollback",
        "restock-replay-protection",
        "versioned-merchant-lifecycle-persistence",
        "native-policy-input-boundary"
    };
}
