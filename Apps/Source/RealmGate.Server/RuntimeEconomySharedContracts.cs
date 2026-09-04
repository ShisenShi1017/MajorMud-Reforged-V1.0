namespace RealmGate.Server;

public enum RuntimeEconomyRoundingPolicy
{
    Floor,
    Ceiling,
    Nearest
}

public sealed record RuntimeBankDefinition(
    string BankId,
    string CurrencyId,
    long MaximumBalance,
    bool DepositsEnabled,
    bool WithdrawalsEnabled,
    bool BalanceInquiryEnabled,
    string AuthorityNote);

public static class RuntimeEconomyCheckedMath
{
    public static long ApplyBasisPoints(long value, int basisPoints, RuntimeEconomyRoundingPolicy rounding)
    {
        if (value < 0) throw new InvalidOperationException("economy-value-negative");
        if (basisPoints < 0) throw new InvalidOperationException("economy-basis-points-negative");
        var numerator = checked(value * basisPoints);
        const long denominator = 10_000L;
        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        if (remainder == 0) return quotient;
        return rounding switch
        {
            RuntimeEconomyRoundingPolicy.Floor => quotient,
            RuntimeEconomyRoundingPolicy.Ceiling => checked(quotient + 1),
            RuntimeEconomyRoundingPolicy.Nearest => remainder * 2 >= denominator ? checked(quotient + 1) : quotient,
            _ => throw new InvalidOperationException("economy-rounding-policy-unsupported")
        };
    }
}
