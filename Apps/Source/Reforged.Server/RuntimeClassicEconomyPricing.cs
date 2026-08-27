namespace Reforged.Server;

/// <summary>
/// Stock MajorMUD merchandise/training arithmetic recovered from the current
/// MMUD Explorer v1.11p formulas.  These routines intentionally preserve the
/// original stage order: currency -> markup -> Charm -> legacy overflow/final
/// rounding for buys; currency -> Charm for sells; and the trainer-specific
/// (markup + 100) integer formula for level training.
/// </summary>
public static class RuntimeClassicEconomyPricing
{
    // MMUD Explorer reproduces the legacy 32-bit overflow bug by repeatedly
    // subtracting 0xFFFFFFFF when the post-Charm value exceeds that boundary.
    private const decimal LegacyOverflowBoundary = 4_294_967_295m;

    public const string MerchandiseFormulaId = "economy.classic.merchandise.mmexplorer_v111p_0282";
    public const string TrainingFormulaId = "economy.classic.training.mmexplorer_v111p_0282";

    public static long QuoteBuyCopper(long baseCopper, int markupPercent, int charm)
    {
        RequireInputs(baseCopper, markupPercent);
        decimal value = baseCopper;

        // GetItemValue: nCopperBuy = nCopperBuy + Fix(nCopperBuy * (nMarkup / 100))
        if (markupPercent > 0)
            value += decimal.Truncate(value * markupPercent / 100m);

        // GetItemValue stock branch:
        // nCharmModBuy = 1 - ((Fix(nCharm / 5) - 10) / 100)
        if (charm > 0)
        {
            var charmModifier = 1m - (((charm / 5) - 10) / 100m);
            value *= charmModifier;
            value = ApplyLegacyOverflow(value);
        }

        return FinalVbRound(value);
    }

    public static long QuoteSellCopper(long baseCopper, int charm)
    {
        if (baseCopper < 0) throw new InvalidOperationException("classic-economy-base-negative");
        decimal value = baseCopper;

        // Stock selling is based on item value, not shop markup.
        // nCharmModSell = Fix(nCharm / 2) + 25
        // nCopperSell = Fix((nCharmModSell * nCopperSell) / 100)
        if (charm > 0)
        {
            var charmPercent = (charm / 2) + 25;
            value = charmPercent * value;
            value = ApplyLegacyOverflow(value);
            value = decimal.Truncate(value / 100m);
        }

        return FinalVbRound(value);
    }

    public static long TrainingCostCopper(int trainingLevelIndex, int markupPercent)
    {
        if (trainingLevelIndex < 0) throw new InvalidOperationException("classic-training-level-negative");
        if (markupPercent < 0) throw new InvalidOperationException("classic-training-markup-negative");

        // Recovered Pascal/MMUD Explorer formula:
        // Result := (longword((Level * 5) * (Markup + 100)) div 100) * 10;
        var value = decimal.Truncate((decimal)trainingLevelIndex * 5m * (markupPercent + 100m) / 100m) * 10m;
        if (value > long.MaxValue) throw new OverflowException("classic-training-cost-overflow");
        return (long)value;
    }

    public static int BuyCharmMultiplierPercent(int charm)
        => charm > 0 ? 100 - ((charm / 5) - 10) : 100;

    public static int SellCharmPercent(int charm)
        => charm > 0 ? (charm / 2) + 25 : 100;

    private static void RequireInputs(long baseCopper, int markupPercent)
    {
        if (baseCopper < 0) throw new InvalidOperationException("classic-economy-base-negative");
        if (markupPercent < 0) throw new InvalidOperationException("classic-economy-markup-negative");
    }

    private static decimal ApplyLegacyOverflow(decimal value)
    {
        if (value <= LegacyOverflowBoundary) return value;
        // Equivalent to the retained VB Do While loop without an unbounded loop.
        var wraps = decimal.Floor((value - 1m) / LegacyOverflowBoundary);
        return value - (wraps * LegacyOverflowBoundary);
    }

    private static long FinalVbRound(decimal value)
    {
        if (value <= 0m) return 0;
        var rounded = decimal.Round(value, 0, MidpointRounding.ToEven);
        if (rounded > long.MaxValue) throw new OverflowException("classic-economy-price-overflow");
        return (long)rounded;
    }
}
