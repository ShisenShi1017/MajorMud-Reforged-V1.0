namespace Reforged.Server;

public static class RuntimeEconomyNativeMarkupSmokeReport
{
    public static IReadOnlyList<string> Expectations => new[]
    {
        "raw markup 100 converts to 10000 basis points and leaves value unchanged",
        "raw markup 275 converts to 27500 basis points and multiplies value by 2.75 under the selected rounding policy",
        "service markup is multiplicative rather than additive",
        "negative markup and checked-arithmetic overflow fail deterministically",
        "markup field semantics do not activate an unproven position in the native service formula"
    };
}
