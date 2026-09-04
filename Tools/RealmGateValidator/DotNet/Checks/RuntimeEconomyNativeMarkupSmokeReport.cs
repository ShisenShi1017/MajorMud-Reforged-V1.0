namespace RealmGate.Server;

public static class RuntimeEconomyNativeMarkupSmokeReport
{
    public static IReadOnlyList<string> Expectations => new[]
    {
        "raw markup 0 converts to 10000 basis points and leaves value unchanged",
        "raw markup 100 converts to 20000 basis points and adds 100 percent to base value",
        "raw markup 275 converts to 37500 basis points and produces 3.75x base value before Charm",
        "Classic merchandise markup is additive and precedes the stock Charm buy modifier",
        "Classic selling ignores shop markup and uses the independent Charm sell formula",
        "negative markup and checked-arithmetic overflow fail deterministically",
        "generic service ordering remains fail-closed where no service-specific stock formula is recovered"
    };
}
