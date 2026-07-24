using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.Server;

public enum RuntimeExecutionBindingStatus { CanonicalIdentityOnly, PromotedTestExecution, PromotedDefinitionBinding, NativeBlocked }
public sealed record RuntimeExecutionBinding(string DefinitionId, string Kind, string Name, RuntimeExecutionBindingStatus Status, bool RuntimeExecutable, string Reason, IReadOnlyList<string> BindingReferences, IReadOnlyList<string> EvidenceReferences, string SafeFallback);
public sealed record RuntimeNonNativeExhaustionCatalog(IReadOnlyDictionary<string,RuntimeExecutionBinding> Records);

public static class RuntimeNonNativeExhaustionCatalogLoader
{
    private static readonly Regex CanonicalId = new(@"^(spell|item|ability)\.\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public static RuntimeNonNativeExhaustionCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("NON_NATIVE_MATRIX_PATH_REQUIRED");
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        var records=new Dictionary<string,RuntimeExecutionBinding>(StringComparer.OrdinalIgnoreCase);
        foreach(var e in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id=S(e,"definition_id"); var status=ParseStatus(S(e,"status")); var executable=e.GetProperty("runtime_executable").GetBoolean();
            var r=new RuntimeExecutionBinding(id,S(e,"kind"),S(e,"name"),status,executable,S(e,"reason"),Strings(e,"binding_refs"),Strings(e,"evidence_refs"),S(e,"safe_fallback"));
            if(!CanonicalId.IsMatch(id)||r.EvidenceReferences.Count==0) throw new InvalidOperationException("NON_NATIVE_BINDING_INVALID");
            if(executable != (status==RuntimeExecutionBindingStatus.PromotedTestExecution)) throw new InvalidOperationException("NON_NATIVE_EXECUTABLE_STATUS_MISMATCH");
            if(!records.TryAdd(id,r)) throw new InvalidOperationException("NON_NATIVE_BINDING_DUPLICATE");
        }
        if(records.Count==0) throw new InvalidOperationException("NON_NATIVE_MATRIX_EMPTY");
        return new(records);
    }
    public static RuntimeExecutionBinding RequireKnown(RuntimeNonNativeExhaustionCatalog catalog,string definitionId)=>catalog.Records.TryGetValue(definitionId,out var r)?r:throw new InvalidOperationException("NON_NATIVE_DEFINITION_UNBOUND");
    public static RuntimeExecutionBinding RequireExecutable(RuntimeNonNativeExhaustionCatalog catalog,string definitionId)
    {
        var r=RequireKnown(catalog,definitionId);
        if(!r.RuntimeExecutable) throw new InvalidOperationException(r.Status switch { RuntimeExecutionBindingStatus.NativeBlocked=>"NON_NATIVE_EXECUTION_NATIVE_BLOCKED", RuntimeExecutionBindingStatus.PromotedDefinitionBinding=>"NON_NATIVE_EXECUTION_DEFINITION_ONLY", _=>"NON_NATIVE_EXECUTION_IDENTITY_ONLY" });
        return r;
    }
    private static RuntimeExecutionBindingStatus ParseStatus(string x)=>x switch {"canonical_identity_only"=>RuntimeExecutionBindingStatus.CanonicalIdentityOnly,"promoted_test_execution"=>RuntimeExecutionBindingStatus.PromotedTestExecution,"promoted_definition_binding"=>RuntimeExecutionBindingStatus.PromotedDefinitionBinding,"native_blocked"=>RuntimeExecutionBindingStatus.NativeBlocked,_=>throw new InvalidOperationException("NON_NATIVE_STATUS_UNKNOWN")};
    private static string S(JsonElement e,string n)=>e.GetProperty(n).GetString()??string.Empty;
    private static IReadOnlyList<string> Strings(JsonElement e,string n)=>e.GetProperty(n).EnumerateArray().Select(x=>x.GetString()??string.Empty).Where(x=>x.Length>0).ToArray();
}
