namespace Reforged.Server;

public static class RuntimeEconomyServiceCostMultiplierCompositionSmokeReport
{
    public static IReadOnlyList<string> Run()
    {
        var checks=new List<string>();
        if(!RuntimeEconomyServiceCostMultiplierComposition.IsOrderInvariant(101,12500,27500,9000,RuntimeEconomyRoundingPolicy.Nearest)) throw new InvalidOperationException("composition-order");
        checks.Add("composition-order-invariant");
        var value=RuntimeEconomyServiceCostMultiplierComposition.Apply(100,12500,20000,10000,RuntimeEconomyRoundingPolicy.Floor);
        if(value!=250) throw new InvalidOperationException("composition-value");
        checks.Add("composition-single-round");
        try { RuntimeEconomyServiceCostMultiplierComposition.Apply(long.MaxValue,100000,100000,100000,RuntimeEconomyRoundingPolicy.Ceiling); throw new InvalidOperationException("overflow-not-detected"); }
        catch(OverflowException) { checks.Add("composition-overflow-checked"); }
        return checks;
    }
}
