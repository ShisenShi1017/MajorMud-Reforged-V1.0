using System.Security.Cryptography;
using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeEconomyNativeDependencyState { Resolved, PolicyBound, NativeBinaryRequired }

public sealed record RuntimeEconomyNativeBankBinding(
    string BankId,int LegacyServiceNumber,string Name,string? RoomId,string ServiceKind,
    RuntimeEconomyNativeConfidence Confidence,IReadOnlyList<string> EvidenceRefs);

public sealed record RuntimeEconomyNativeRuleBinding(
    string RuleId,string RuleScope,bool GlobalRule,IReadOnlyList<string> ShopIds,IReadOnlyList<string> ItemIds,
    RuntimeEconomyNativeConfidence Confidence,IReadOnlyList<string> EvidenceRefs);

public sealed record RuntimeEconomyNativeDependency(
    string DependencyId,string BehaviorOwner,RuntimeEconomyNativeDependencyState State,
    string RequiredSource,string SafeFallback,bool BlocksSemanticExhaustion);

public sealed record RuntimeEconomyNativeCompletionCatalog(
    int SchemaVersion,string WorldId,IReadOnlyList<RuntimeEconomyNativeBankBinding> Banks,
    IReadOnlyList<RuntimeEconomyNativeRuleBinding> Rules,IReadOnlyList<RuntimeEconomyNativeDependency> Dependencies);

public sealed record RuntimeEconomyNativeCompletionValidation(
    bool Valid,bool SemanticallyExhausted,IReadOnlyList<string> Errors,IReadOnlyList<string> BlockingDependencies,string Digest);

public static class RuntimeEconomyNativeCompletionEngine
{
    public const int CurrentSchemaVersion=1;

    public static RuntimeEconomyNativeCompletionValidation Validate(RuntimeEconomyNativeCompletionCatalog catalog)
    {
        var errors=new List<string>();
        if(catalog.SchemaVersion!=CurrentSchemaVersion) errors.Add("native-completion-schema-unsupported");
        if(string.IsNullOrWhiteSpace(catalog.WorldId)) errors.Add("native-completion-world-required");
        if(catalog.Banks.GroupBy(x=>x.BankId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-bank-id-duplicate");
        if(catalog.Rules.GroupBy(x=>x.RuleId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-rule-id-duplicate");
        if(catalog.Dependencies.GroupBy(x=>x.DependencyId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-dependency-id-duplicate");
        foreach(var bank in catalog.Banks)
        {
            if(bank.LegacyServiceNumber<0||string.IsNullOrWhiteSpace(bank.Name)||string.IsNullOrWhiteSpace(bank.ServiceKind))
                errors.Add($"native-bank-invalid:{bank.BankId}");
            if(bank.Confidence is RuntimeEconomyNativeConfidence.Weak or RuntimeEconomyNativeConfidence.Unknown)
                errors.Add($"native-bank-not-runtime-promotable:{bank.BankId}");
        }
        foreach(var rule in catalog.Rules)
        {
            if(string.IsNullOrWhiteSpace(rule.RuleScope)) errors.Add($"native-rule-scope-required:{rule.RuleId}");
            if(!rule.GlobalRule&&rule.ShopIds.Count==0&&rule.ItemIds.Count==0) errors.Add($"native-rule-unowned:{rule.RuleId}");
        }
        foreach(var dependency in catalog.Dependencies)
        {
            if(string.IsNullOrWhiteSpace(dependency.BehaviorOwner)||string.IsNullOrWhiteSpace(dependency.RequiredSource)||string.IsNullOrWhiteSpace(dependency.SafeFallback))
                errors.Add($"native-dependency-incomplete:{dependency.DependencyId}");
            if(dependency.State==RuntimeEconomyNativeDependencyState.Resolved&&dependency.BlocksSemanticExhaustion)
                errors.Add($"native-dependency-resolved-but-blocking:{dependency.DependencyId}");
        }
        var blocking=catalog.Dependencies.Where(x=>x.BlocksSemanticExhaustion&&x.State!=RuntimeEconomyNativeDependencyState.Resolved).Select(x=>x.DependencyId).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        return new(errors.Count==0,errors.Count==0&&blocking.Length==0,errors,blocking,ComputeDigest(catalog));
    }

    public static RuntimeBankDefinition BindBankDefinition(RuntimeEconomyNativeBankBinding bank,string currencyId,long maximumBalance)
    {
        if(string.IsNullOrWhiteSpace(currencyId)) throw new InvalidOperationException("native-bank-currency-required");
        if(maximumBalance<0) throw new InvalidOperationException("native-bank-limit-invalid");
        return new RuntimeBankDefinition(bank.BankId,currencyId,maximumBalance,true,true,true,
            "Native bank identity and provider location are strongly correlated; fees, limits and sharing policy remain explicit configuration inputs.");
    }

    public static void RequireResolved(RuntimeEconomyNativeCompletionCatalog catalog,string dependencyId)
    {
        var dependency=catalog.Dependencies.SingleOrDefault(x=>x.DependencyId.Equals(dependencyId,StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"native-dependency-missing:{dependencyId}");
        if(dependency.State!=RuntimeEconomyNativeDependencyState.Resolved)
            throw new InvalidOperationException($"native-dependency-unresolved:{dependency.DependencyId}:{dependency.RequiredSource}");
    }

    public static string ComputeDigest(RuntimeEconomyNativeCompletionCatalog catalog)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(catalog,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
