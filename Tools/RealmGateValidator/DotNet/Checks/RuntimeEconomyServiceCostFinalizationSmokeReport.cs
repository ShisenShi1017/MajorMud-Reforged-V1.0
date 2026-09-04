namespace RealmGate.Server;

public static class RuntimeEconomyServiceCostFinalizationSmokeReport
{
    public static IReadOnlyList<string> Run()
    {
        var results=new List<string>();
        var before=RuntimeEconomyServiceCostFinalization.Calculate(100,10000,15000,10000,
            new(10,1,RuntimeEconomyServiceCostFinalizationOrder.AdditiveBeforeMultipliers,
                RuntimeEconomyServiceCostFinalizationOrder.MinimumAfterFinalRounding,
                RuntimeEconomyRoundingPolicy.Floor,false));
        if(!before.Allowed||before.Cost!=165) throw new InvalidOperationException("finalization-before-smoke");
        results.Add("additive-before:pass");
        var floor=RuntimeEconomyServiceCostFinalization.Calculate(1,10000,10000,10000,
            new(0,5,RuntimeEconomyServiceCostFinalizationOrder.AdditiveAfterMultipliers,
                RuntimeEconomyServiceCostFinalizationOrder.MinimumAfterFinalRounding,
                RuntimeEconomyRoundingPolicy.Floor,false));
        if(!floor.Allowed||floor.Cost!=5) throw new InvalidOperationException("finalization-minimum-smoke");
        results.Add("minimum-after:pass");
        var unresolved=RuntimeEconomyServiceCostFinalization.Calculate(100,10000,10000,10000,
            new(0,0,RuntimeEconomyServiceCostFinalizationOrder.NativeExact,
                RuntimeEconomyServiceCostFinalizationOrder.NativeExact,
                RuntimeEconomyRoundingPolicy.Floor,false));
        if(unresolved.Allowed||unresolved.FailureCode!="native-service-cost-finalization-order-unresolved") throw new InvalidOperationException("finalization-fail-closed-smoke");
        results.Add("native-unresolved:pass");
        return results;
    }
}
