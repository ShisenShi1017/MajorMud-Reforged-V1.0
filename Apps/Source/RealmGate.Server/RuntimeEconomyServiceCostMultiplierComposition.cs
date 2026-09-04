using System.Numerics;

namespace RealmGate.Server;

public enum RuntimeEconomyMultiplierCompositionConfidence
{
    ProvenIntegerArithmetic,
    StronglyCorrelatedMultiplicativeFields,
    NativeIntermediateRoundingUnknown
}

public sealed record RuntimeEconomyMultiplierCompositionPolicy(
    int LevelOrRankBasisPoints,
    int ProviderMarkupBasisPoints,
    int DiscountOrSurchargeBasisPoints,
    RuntimeEconomyRoundingPolicy RoundingPolicy,
    RuntimeEconomyMultiplierCompositionConfidence Confidence);

public static class RuntimeEconomyServiceCostMultiplierComposition
{
    private static readonly BigInteger BasisPointDenominator = new(10000);

    public static long Apply(long baseValue,int levelOrRankBasisPoints,int providerMarkupBasisPoints,int discountOrSurchargeBasisPoints,RuntimeEconomyRoundingPolicy roundingPolicy)
    {
        if(baseValue<0) throw new InvalidOperationException("service-cost-composition-base-negative");
        if(levelOrRankBasisPoints<0||providerMarkupBasisPoints<0||discountOrSurchargeBasisPoints<0) throw new InvalidOperationException("service-cost-composition-multiplier-negative");
        var numerator=new BigInteger(baseValue)*levelOrRankBasisPoints*providerMarkupBasisPoints*discountOrSurchargeBasisPoints;
        var denominator=BigInteger.Pow(BasisPointDenominator,3);
        var quotient=BigInteger.DivRem(numerator,denominator,out var remainder);
        if(remainder!=0)
        {
            quotient=roundingPolicy switch
            {
                RuntimeEconomyRoundingPolicy.Floor=>quotient,
                RuntimeEconomyRoundingPolicy.Ceiling=>quotient+1,
                RuntimeEconomyRoundingPolicy.Nearest=>remainder*2>=denominator?quotient+1:quotient,
                _=>throw new InvalidOperationException("service-cost-composition-rounding-unsupported")
            };
        }
        if(quotient<0||quotient>long.MaxValue) throw new OverflowException("service-cost-composition-overflow");
        return (long)quotient;
    }

    public static bool IsOrderInvariant(long baseValue,int aBasisPoints,int bBasisPoints,int cBasisPoints,RuntimeEconomyRoundingPolicy roundingPolicy)
    {
        var first=Apply(baseValue,aBasisPoints,bBasisPoints,cBasisPoints,roundingPolicy);
        var second=Apply(baseValue,bBasisPoints,cBasisPoints,aBasisPoints,roundingPolicy);
        var third=Apply(baseValue,cBasisPoints,aBasisPoints,bBasisPoints,roundingPolicy);
        return first==second&&second==third;
    }
}
