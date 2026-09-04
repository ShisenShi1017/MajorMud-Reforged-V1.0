using System.Numerics;

namespace RealmGate.Server;

public enum RuntimeEconomyServiceCostFinalizationOrder
{
    AdditiveBeforeMultipliers,
    AdditiveAfterMultipliers,
    MinimumBeforeFinalRounding,
    MinimumAfterFinalRounding,
    NativeExact
}

public sealed record RuntimeEconomyServiceCostFinalizationPolicy(
    long AdditiveAdjustment,
    long MinimumCost,
    RuntimeEconomyServiceCostFinalizationOrder AdditiveOrder,
    RuntimeEconomyServiceCostFinalizationOrder MinimumOrder,
    RuntimeEconomyRoundingPolicy RoundingPolicy,
    bool NativeOrderConfirmed);

public sealed record RuntimeEconomyServiceCostFinalizationResult(
    bool Allowed,
    string? FailureCode,
    long Cost,
    IReadOnlyList<string> AppliedStages);

public static class RuntimeEconomyServiceCostFinalization
{
    public static RuntimeEconomyServiceCostFinalizationResult Calculate(
        long baseValue,
        int levelOrRankBasisPoints,
        int providerMarkupBasisPoints,
        int discountOrSurchargeBasisPoints,
        RuntimeEconomyServiceCostFinalizationPolicy policy)
    {
        if(baseValue<0||policy.MinimumCost<0)
            return new(false,"service-cost-finalization-negative",0,Array.Empty<string>());
        if(policy.AdditiveOrder==RuntimeEconomyServiceCostFinalizationOrder.NativeExact||
           policy.MinimumOrder==RuntimeEconomyServiceCostFinalizationOrder.NativeExact)
        {
            if(!policy.NativeOrderConfirmed)
                return new(false,"native-service-cost-finalization-order-unresolved",0,Array.Empty<string>());
        }

        var stages=new List<string>{"base"};
        BigInteger working=baseValue;
        if(policy.AdditiveOrder==RuntimeEconomyServiceCostFinalizationOrder.AdditiveBeforeMultipliers)
        {
            working+=policy.AdditiveAdjustment;
            if(working<0) return new(false,"service-cost-finalization-underflow",0,stages);
            stages.Add("additive-before-multipliers");
        }

        var multiplied=RuntimeEconomyServiceCostMultiplierComposition.Apply(
            CheckedLong(working),levelOrRankBasisPoints,providerMarkupBasisPoints,
            discountOrSurchargeBasisPoints,policy.RoundingPolicy);
        working=multiplied;
        stages.Add("composite-multipliers");

        if(policy.AdditiveOrder==RuntimeEconomyServiceCostFinalizationOrder.AdditiveAfterMultipliers)
        {
            working+=policy.AdditiveAdjustment;
            if(working<0) return new(false,"service-cost-finalization-underflow",0,stages);
            stages.Add("additive-after-multipliers");
        }

        if(policy.MinimumOrder==RuntimeEconomyServiceCostFinalizationOrder.MinimumBeforeFinalRounding)
        {
            working=BigInteger.Max(working,policy.MinimumCost);
            stages.Add("minimum-before-final-rounding");
        }

        // Multiplier composition performs the sole configured integer rounding.
        stages.Add("final-rounding");

        if(policy.MinimumOrder==RuntimeEconomyServiceCostFinalizationOrder.MinimumAfterFinalRounding)
        {
            working=BigInteger.Max(working,policy.MinimumCost);
            stages.Add("minimum-after-final-rounding");
        }

        return new(true,null,CheckedLong(working),stages);
    }

    private static long CheckedLong(BigInteger value)
    {
        if(value<0||value>long.MaxValue) throw new OverflowException("service-cost-finalization-overflow");
        return (long)value;
    }
}
