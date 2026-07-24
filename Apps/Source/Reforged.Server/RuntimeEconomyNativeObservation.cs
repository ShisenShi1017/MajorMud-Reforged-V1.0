using System.Security.Cryptography;
using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeEconomyNativeObservationKind { StaticRecord, ControlledTransaction, SaveTransition, TimerTransition, CallTrace }
public enum RuntimeEconomyNativeAssertionState { Observed, Correlated, Contradicted, Incomplete }

public sealed record RuntimeEconomyNativeSourceFingerprint(string SourceId,long Length,string Sha256);
public sealed record RuntimeEconomyNativeAssertion(
    string AssertionId,string DependencyId,RuntimeEconomyNativeObservationKind Kind,
    RuntimeEconomyNativeAssertionState State,string InputDigest,string OutputDigest,
    IReadOnlyList<string> SourceIds,IReadOnlyList<string> Notes);
public sealed record RuntimeEconomyNativeObservationBundle(
    int SchemaVersion,string WorldId,IReadOnlyList<RuntimeEconomyNativeSourceFingerprint> Sources,
    IReadOnlyList<RuntimeEconomyNativeAssertion> Assertions);
public sealed record RuntimeEconomyNativePromotionResult(
    bool Valid,IReadOnlyList<string> Errors,IReadOnlyList<string> PromotableDependencies,
    IReadOnlyList<string> RemainingDependencies,string Digest);

public static class RuntimeEconomyNativeObservationEngine
{
    public const int CurrentSchemaVersion=1;
    private static readonly string[] RequiredDependencies={
        "native.timer-unit","native.price-order","native.repair-formula","native.bank-constants",
        "native.service-cost-order","native.limited-release-order","native.transaction-order"};

    public static RuntimeEconomyNativePromotionResult ValidateAndClassify(
        RuntimeEconomyNativeObservationBundle bundle,
        RuntimeEconomyNativeCompletionCatalog completion)
    {
        var errors=new List<string>();
        if(bundle.SchemaVersion!=CurrentSchemaVersion) errors.Add("native-observation-schema-unsupported");
        if(string.IsNullOrWhiteSpace(bundle.WorldId)||!bundle.WorldId.Equals(completion.WorldId,StringComparison.OrdinalIgnoreCase))
            errors.Add("native-observation-world-mismatch");
        if(bundle.Sources.GroupBy(x=>x.SourceId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-observation-source-duplicate");
        if(bundle.Assertions.GroupBy(x=>x.AssertionId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-observation-assertion-duplicate");
        foreach(var source in bundle.Sources)
        {
            if(source.Length<=0||source.Sha256.Length!=64||source.Sha256.Any(c=>!Uri.IsHexDigit(c))) errors.Add($"native-observation-source-invalid:{source.SourceId}");
        }
        var sourceIds=bundle.Sources.Select(x=>x.SourceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var completionIds=completion.Dependencies.Select(x=>x.DependencyId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var assertion in bundle.Assertions)
        {
            if(!completionIds.Contains(assertion.DependencyId)) errors.Add($"native-observation-dependency-unknown:{assertion.AssertionId}");
            if(string.IsNullOrWhiteSpace(assertion.InputDigest)||string.IsNullOrWhiteSpace(assertion.OutputDigest)) errors.Add($"native-observation-digest-required:{assertion.AssertionId}");
            if(assertion.SourceIds.Count==0||assertion.SourceIds.Any(x=>!sourceIds.Contains(x))) errors.Add($"native-observation-source-unresolved:{assertion.AssertionId}");
        }
        var promotable=new List<string>(); var remaining=new List<string>();
        foreach(var dependencyId in RequiredDependencies)
        {
            var observations=bundle.Assertions.Where(x=>x.DependencyId.Equals(dependencyId,StringComparison.OrdinalIgnoreCase)).ToArray();
            var contradicted=observations.Any(x=>x.State==RuntimeEconomyNativeAssertionState.Contradicted);
            var observed=observations.Where(x=>x.State==RuntimeEconomyNativeAssertionState.Observed).ToArray();
            var independent=observed.SelectMany(x=>x.SourceIds).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var stable=observed.Select(x=>x.OutputDigest).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1;
            if(!contradicted&&observed.Length>=2&&independent>=2&&stable) promotable.Add(dependencyId); else remaining.Add(dependencyId);
        }
        return new(errors.Count==0,errors,promotable,remaining,ComputeDigest(bundle));
    }

    public static RuntimeEconomyNativeCompletionCatalog Promote(
        RuntimeEconomyNativeCompletionCatalog catalog,RuntimeEconomyNativePromotionResult result)
    {
        if(!result.Valid) throw new InvalidOperationException("native-observation-invalid");
        var promotable=result.PromotableDependencies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies=catalog.Dependencies.Select(x=>promotable.Contains(x.DependencyId)
            ? x with { State=RuntimeEconomyNativeDependencyState.Resolved,BlocksSemanticExhaustion=false,
                SafeFallback="Resolved by reproducible native observation bundle; source fingerprints and assertion digests retained." }
            : x).ToArray();
        return catalog with { Dependencies=dependencies };
    }

    public static void VerifySource(RuntimeEconomyNativeSourceFingerprint expected,ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length!=expected.Length) throw new InvalidOperationException($"native-source-length-mismatch:{expected.SourceId}");
        var digest=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if(!digest.Equals(expected.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"native-source-sha256-mismatch:{expected.SourceId}");
    }

    public static string ComputeDigest(RuntimeEconomyNativeObservationBundle bundle)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(bundle,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
