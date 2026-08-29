namespace Reforged.Server;

/// <summary>
/// Stock MajorMUD merchandise/training arithmetic recovered directly from the
/// original WCCMMUD.DLL. The native implementation uses 32-bit integer stages:
/// currency -> optional >100,000 BUY scale -> additive shop markup -> Charm ->
/// optional scale restore. SELL uses currency -> Charm only. There is no
/// floating-point/VB final rounding step in the stock DLL callsites.
/// </summary>
public static class RuntimeClassicEconomyPricing
{
    public const string MerchandiseFormulaId = "economy.classic.merchandise.native_wccmmud_0334";
    public const string TrainingFormulaId = "economy.classic.training.native_wccmmud_0334";
    public const uint NativeBuyScaleThresholdCopper = 100_000u;
    public const uint NativeBuyScaleDivisor = 100u;

    public static long QuoteBuyCopper(long baseCopper, int markupPercent, int charm)
    {
        RequireInputs(baseCopper, markupPercent, charm);
        var value = RequireNativeUInt32(baseCopper, "classic-economy-base-over-native-u32");
        var scaled = value > NativeBuyScaleThresholdCopper;
        if (scaled) value /= NativeBuyScaleDivisor;

        // WCCMMUD.DLL 0x0041c153..0x0041c169:
        // floor(value * (shop.Markup + 100) / 100), using the low native DWORD.
        value = NativeMultiplyDivide100(value, checked((uint)(markupPercent + 100)));

        // WCCMMUD.DLL 0x0041c13c..0x0041c177:
        // Charm factor = 110 - floor(Charm / 5). This runs even at Charm == 0.
        var charmPercent = BuyCharmMultiplierPercent(charm);
        value = NativeMultiplyDivide100(value, checked((uint)charmPercent));

        // WCCMMUD.DLL 0x0041c17a..0x0041c184 restores the x100 scale after
        // both integer pricing stages. The native IMUL keeps the low DWORD.
        if (scaled) value = unchecked(value * NativeBuyScaleDivisor);
        return value;
    }

    public static long QuoteSellCopper(long baseCopper, int charm)
    {
        if (charm < 0) throw new InvalidOperationException("classic-economy-charm-negative");
        var value = RequireNativeUInt32(baseCopper, "classic-economy-base-over-native-u32");

        // WCCMMUD.DLL 0x0041c65b..0x0041c673:
        // floor(baseCopper * (floor(Charm / 2) + 25) / 100).
        // Shop markup is intentionally absent and Charm == 0 yields 25 percent.
        return NativeMultiplyDivide100(value, checked((uint)SellCharmPercent(charm)));
    }

    public static long TrainingCostCopper(int trainingLevelIndex, int markupPercent)
    {
        if (trainingLevelIndex < 0) throw new InvalidOperationException("classic-training-level-negative");
        if (markupPercent < 0) throw new InvalidOperationException("classic-training-markup-negative");

        // WCCMMUD.DLL 0x0041e95b..0x0041e981 computes the amount in silver:
        // floor((Level * 5) * (Markup + 100) / 100). 0x0041e984..0x0041e990
        // passes that value in the silver denomination slot, therefore x10 copper.
        var levelTimesFive = checked((uint)trainingLevelIndex * 5u);
        var silver = NativeMultiplyDivide100(levelTimesFive, checked((uint)(markupPercent + 100)));
        return checked((long)silver * 10L);
    }

    public static int BuyCharmMultiplierPercent(int charm)
    {
        if (charm < 0) throw new InvalidOperationException("classic-economy-charm-negative");
        var value = 110 - (charm / 5);
        if (value < 0) throw new InvalidOperationException("classic-economy-buy-charm-factor-negative");
        return value;
    }

    public static int SellCharmPercent(int charm)
    {
        if (charm < 0) throw new InvalidOperationException("classic-economy-charm-negative");
        return checked((charm / 2) + 25);
    }

    private static void RequireInputs(long baseCopper, int markupPercent, int charm)
    {
        if (baseCopper < 0) throw new InvalidOperationException("classic-economy-base-negative");
        if (markupPercent < 0) throw new InvalidOperationException("classic-economy-markup-negative");
        if (charm < 0) throw new InvalidOperationException("classic-economy-charm-negative");
    }

    private static uint RequireNativeUInt32(long value, string error)
    {
        if (value < 0 || value > uint.MaxValue) throw new InvalidOperationException(error);
        return (uint)value;
    }

    private static uint NativeMultiplyDivide100(uint left, uint right)
    {
        // The stock code uses 32-bit IMUL then clears EDX before unsigned DIV 100,
        // so only the low DWORD of the product survives into the division.
        var product = unchecked(left * right);
        return product / 100u;
    }
}
