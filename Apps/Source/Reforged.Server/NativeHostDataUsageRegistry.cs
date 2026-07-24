namespace Reforged.Server;

public sealed record NativeHostDataUsageProfile(
    string Symbol,
    string Classification,
    int StaticReferenceCount,
    int PointerDereferenceCount,
    string ObservedClientOffsets,
    string ObservedAccessWidths);

public static class NativeHostDataUsageRegistry
{
    public const string EvidenceId = "FinalNativeClosure0008";
    public const string Scope = "Proven MajorMUD-side use of imported Worldgroup data slots; exact Worldgroup host types remain fail-closed.";

    public static readonly IReadOnlyList<NativeHostDataUsageProfile> Proven = new NativeHostDataUsageProfile[]
    {
        new("_accbb", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 12, 10, "0", "dword"),
        new("_bgnedt", "import_contents_consumed_without_recoverable_local_dereference", 10, 0, "", ""),
        new("_channel", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 6, 4, "0", "dword"),
        new("_clsbb", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 10, 8, "0", "dword"),
        new("_fsdemg", "import_address_or_contents_passed_without_local_field_recovery", 54, 0, "", ""),
        new("_fsdscb", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 26, 22, "0", "dword"),
        new("_genbb", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 68, 64, "0", "dword"),
        new("_globs", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 6, 4, "0", "dword"),
        new("_inplen", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 4, 2, "0", "dword"),
        new("_input", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 158, 26, "0,60", "byte"),
        new("_margc", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 574, 570, "0", "dword"),
        new("_margv", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 644, 632, "0,4,8,12,16", "dword"),
        new("_msysid", "import_address_or_contents_passed_without_local_field_recovery", 4, 0, "", ""),
        new("_namtmp", "import_address_or_contents_passed_without_local_field_recovery", 4, 0, "", ""),
        new("_nglobs", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 6, 4, "0", "dword"),
        new("_nterms", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 272, 266, "0", "dword,word"),
        new("_numbyts", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 6, 4, "0", "dword"),
        new("_othusn", "module_specific_usage", 12, 6, "0", "dword"),
        new("_prfbuf", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 88, 84, "0", "dword"),
        new("_prfptr", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 44, 42, "0", "dword"),
        new("_register_agent", "import_contents_consumed_without_recoverable_local_dereference", 2, 0, "", ""),
        new("_status", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 8, 4, "0", "dword"),
        new("_syscyc", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 6, 4, "0", "dword"),
        new("_tfsbuf", "import_address_or_contents_passed_without_local_field_recovery", 4, 0, "", ""),
        new("_tfspst", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 10, 8, "0", "dword"),
        new("_tfstate", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 6, 4, "0", "dword"),
        new("_txtvars", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 12, 10, "0", "dword"),
        new("_uidarr", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 4, 2, "0", "dword"),
        new("_usaptr", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 60, 56, "0", "dword"),
        new("_usrnum", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 3314, 3280, "0", "dword"),
        new("_usrptr", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 430, 424, "0", "dword"),
        new("_vdaptr", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 58, 56, "0", "dword"),
        new("_vdasiz", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 12, 10, "0", "dword"),
        new("_vdatmp", "import_slot_resolves_to_pointer_with_observed_client_dereferences", 12, 10, "0", "dword"),
        new("_version", "import_address_or_contents_passed_without_local_field_recovery", 4, 0, "", ""),
    };
}
