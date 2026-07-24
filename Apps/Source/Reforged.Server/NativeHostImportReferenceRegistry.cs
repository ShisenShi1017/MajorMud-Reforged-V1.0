namespace Reforged.Server;

public sealed record NativeHostImportReferenceClass(string Classification, int SymbolCount);

public static class NativeHostImportReferenceRegistry
{
    public const string EvidenceId = "FinalNativeClosure0005";
    public const string Scope = "Proven static reference form for WGSERVER imports; host implementation remains fail-closed.";
    public static readonly IReadOnlyList<NativeHostImportReferenceClass> Proven = new NativeHostImportReferenceClass[]
    {
        new("direct_callable_import", 121),
        new("iat_data_or_indirect_reference", 34),
        new("imported_no_static_code_reference", 1),
    };
}
