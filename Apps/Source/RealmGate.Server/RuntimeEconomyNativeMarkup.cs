using System.Security.Cryptography;
using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeEconomyNativeMarkupConfidence { ProvenFieldEncoding, StronglyCorrelatedAdditivePercentSemantics, UnknownExecutionOrder }
public enum RuntimeEconomyNativeMarkupDomain { Merchandise, Trainer, SpellService, OtherService }

public sealed record RuntimeEconomyNativeMarkupBinding(
    string ProviderId, int LegacyShopNumber, int ServiceType, int RawMarkupPercent,
    int MultiplierBasisPoints, RuntimeEconomyNativeMarkupDomain Domain,
    RuntimeEconomyNativeMarkupConfidence Confidence, IReadOnlyList<string> EvidenceRefs);

public sealed record RuntimeEconomyNativeMarkupCatalog(
    int SchemaVersion, string WorldId, IReadOnlyList<RuntimeEconomyNativeMarkupBinding> Bindings,
    string NeutralRule, string ArithmeticRule, string OrderingState);

public static class RuntimeEconomyNativeMarkup
{
    public const int CurrentSchemaVersion = 1;
    public const int NeutralMarkupPercent = 0;

    /// <summary>Converts MajorMUD's additive markup percentage into a normal multiplier.</summary>
    public static int ToMultiplierBasisPoints(int rawMarkupPercent)
    {
        if (rawMarkupPercent < 0) throw new InvalidOperationException("native-markup-negative");
        return checked((100 + rawMarkupPercent) * 100);
    }

    public static long Apply(long value, int rawMarkupPercent, RuntimeEconomyRoundingPolicy rounding)
    {
        if (value < 0) throw new InvalidOperationException("native-markup-value-negative");
        return RuntimeEconomyCheckedMath.ApplyBasisPoints(value, ToMultiplierBasisPoints(rawMarkupPercent), rounding);
    }

    public static RuntimeEconomyNativeMarkupDomain ResolveDomain(int serviceType) => serviceType switch
    {
        0 or 1 or 2 or 3 or 4 or 5 or 6 => RuntimeEconomyNativeMarkupDomain.Merchandise,
        8 => RuntimeEconomyNativeMarkupDomain.Trainer,
        10 => RuntimeEconomyNativeMarkupDomain.SpellService,
        _ => RuntimeEconomyNativeMarkupDomain.OtherService
    };

    public static IReadOnlyList<string> Validate(RuntimeEconomyNativeMarkupCatalog catalog)
    {
        var errors = new List<string>();
        if (catalog.SchemaVersion != CurrentSchemaVersion) errors.Add("native-markup-schema-unsupported");
        if (string.IsNullOrWhiteSpace(catalog.WorldId)) errors.Add("native-markup-world-required");
        if (catalog.Bindings.Count == 0) errors.Add("native-markup-bindings-required");
        if (catalog.Bindings.GroupBy(x => x.ProviderId, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) errors.Add("native-markup-provider-duplicate");
        foreach (var binding in catalog.Bindings)
        {
            if (binding.LegacyShopNumber <= 0 || string.IsNullOrWhiteSpace(binding.ProviderId)) errors.Add($"native-markup-binding-invalid:{binding.ProviderId}");
            if (binding.RawMarkupPercent < 0 || binding.MultiplierBasisPoints != ToMultiplierBasisPoints(binding.RawMarkupPercent)) errors.Add($"native-markup-conversion-invalid:{binding.ProviderId}");
            if (binding.EvidenceRefs.Count == 0) errors.Add($"native-markup-evidence-required:{binding.ProviderId}");
        }
        return errors;
    }

    public static string ComputeDigest(RuntimeEconomyNativeMarkupCatalog catalog)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
