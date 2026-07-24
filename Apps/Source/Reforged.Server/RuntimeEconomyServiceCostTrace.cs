using System.Security.Cryptography;
using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeEconomyServiceCostStage { ResolveService, ValidateProvider, ValidateEligibility, ResolveBaseCost, ApplyLevelOrRankPolicy, ApplyProviderMarkup, ApplyDiscountOrSurcharge, ApplyAdditiveAdjustment, ApplyMinimumCost, Round, ValidateFunds, Debit, ApplyEffect, Persist, CommitReplay }
public enum RuntimeEconomyServiceCostTraceState { ProvenRuntimeOrder, StronglyCorrelatedNativeField, NativeBinaryRequired }

public sealed record RuntimeEconomyServiceCostTraceStep(int Sequence, RuntimeEconomyServiceCostStage Stage, RuntimeEconomyServiceCostTraceState State, string EvidenceRef, string FailureBehavior);
public sealed record RuntimeEconomyTrainerBinding(string ServiceId,int LegacyShopNumber,string Name,int ServiceType,int MinimumLevel,int MaximumLevel,int ClassLimit,int MarkupPercent,string? RoomId,IReadOnlyList<string> EvidenceRefs);
public sealed record RuntimeEconomyServiceCostTraceCatalog(int SchemaVersion,string WorldId,IReadOnlyList<RuntimeEconomyTrainerBinding> Trainers,IReadOnlyList<RuntimeEconomyTrainerBinding> SpellServices,IReadOnlyList<RuntimeEconomyServiceCostTraceStep> ExecutionOrder);
public sealed record RuntimeEconomyServiceCostPolicy(long BaseCost,int LevelOrRankBasisPoints,int ProviderMarkupPercent,int DiscountOrSurchargeBasisPoints,RuntimeEconomyRoundingPolicy RoundingPolicy,bool NativeExactOrderConfirmed,bool CompositeMultiplierOrderConfirmed=false);
public sealed record RuntimeEconomyServiceCostResult(bool Allowed,string? FailureCode,long Cost,IReadOnlyList<RuntimeEconomyServiceCostStage> AppliedStages);

public static class RuntimeEconomyServiceCostTraceEngine
{
    public const int CurrentSchemaVersion=1;

    public static RuntimeEconomyServiceCostResult Calculate(RuntimeEconomyServiceCostPolicy policy,long quantity)
    {
        if(quantity<=0) return new(false,"service-cost-quantity-invalid",0,Array.Empty<RuntimeEconomyServiceCostStage>());
        if(policy.BaseCost<0) return new(false,"service-cost-base-invalid",0,Array.Empty<RuntimeEconomyServiceCostStage>());
        if(!policy.NativeExactOrderConfirmed && !policy.CompositeMultiplierOrderConfirmed) return new(false,"native-service-cost-order-unresolved",0,Array.Empty<RuntimeEconomyServiceCostStage>());
        var stages=new List<RuntimeEconomyServiceCostStage>{RuntimeEconomyServiceCostStage.ResolveBaseCost};
        long value=checked(policy.BaseCost*quantity);
        if(policy.CompositeMultiplierOrderConfirmed)
        {
            value=RuntimeEconomyServiceCostMultiplierComposition.Apply(
                value,
                policy.LevelOrRankBasisPoints,
                RuntimeEconomyNativeMarkup.ToMultiplierBasisPoints(policy.ProviderMarkupPercent),
                checked(10000+policy.DiscountOrSurchargeBasisPoints),
                policy.RoundingPolicy);
            stages.Add(RuntimeEconomyServiceCostStage.ApplyLevelOrRankPolicy);
            stages.Add(RuntimeEconomyServiceCostStage.ApplyProviderMarkup);
            stages.Add(RuntimeEconomyServiceCostStage.ApplyDiscountOrSurcharge);
        }
        else
        {
            value=RuntimeEconomyCheckedMath.ApplyBasisPoints(value,policy.LevelOrRankBasisPoints,policy.RoundingPolicy);
            stages.Add(RuntimeEconomyServiceCostStage.ApplyLevelOrRankPolicy);
            value=RuntimeEconomyNativeMarkup.Apply(value,policy.ProviderMarkupPercent,policy.RoundingPolicy);
            stages.Add(RuntimeEconomyServiceCostStage.ApplyProviderMarkup);
            value=RuntimeEconomyCheckedMath.ApplyBasisPoints(value,checked(10000+policy.DiscountOrSurchargeBasisPoints),policy.RoundingPolicy);
            stages.Add(RuntimeEconomyServiceCostStage.ApplyDiscountOrSurcharge);
        }
        stages.Add(RuntimeEconomyServiceCostStage.Round);
        return new(true,null,value,stages);
    }

    public static IReadOnlyList<string> Validate(RuntimeEconomyServiceCostTraceCatalog catalog)
    {
        var errors=new List<string>();
        if(catalog.SchemaVersion!=CurrentSchemaVersion) errors.Add("service-cost-trace-schema-unsupported");
        if(string.IsNullOrWhiteSpace(catalog.WorldId)) errors.Add("service-cost-trace-world-required");
        var ids=catalog.Trainers.Concat(catalog.SpellServices).Select(x=>x.ServiceId).ToArray();
        if(ids.GroupBy(x=>x,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("service-cost-trace-service-duplicate");
        foreach(var binding in catalog.Trainers.Concat(catalog.SpellServices))
        {
            if(binding.LegacyShopNumber<=0||string.IsNullOrWhiteSpace(binding.Name)||binding.MinimumLevel<0||binding.MaximumLevel<binding.MinimumLevel) errors.Add($"service-cost-trace-binding-invalid:{binding.ServiceId}");
            if(binding.EvidenceRefs.Count==0) errors.Add($"service-cost-trace-evidence-required:{binding.ServiceId}");
        }
        var sequence=catalog.ExecutionOrder.OrderBy(x=>x.Sequence).Select(x=>x.Sequence).ToArray();
        if(!sequence.SequenceEqual(Enumerable.Range(1,sequence.Length))) errors.Add("service-cost-trace-order-gap");
        return errors;
    }

    public static string ComputeDigest(RuntimeEconomyServiceCostTraceCatalog catalog)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(catalog,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
