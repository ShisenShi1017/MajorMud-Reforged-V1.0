namespace RealmGate.Server;

public static class RuntimeEconomyNativeBindingSmokeReport
{
    public static IReadOnlyList<string> Scenarios=>new[]{
        "native-source-sha256-and-length-validation",
        "178-classic-shop-bindings-resolve",
        "1950-item-value-bindings-resolve",
        "1204-shop-inventory-relationships-resolve",
        "service-type-ownership-boundaries-preserved",
        "merchant-lifecycle-policy-binding",
        "item-valuation-policy-binding",
        "unknown-native-time-unit-fails-closed",
        "unknown-price-modifier-order-remains-policy-bound",
        "wcc-wbb-source-provenance-retained"
    };
}
