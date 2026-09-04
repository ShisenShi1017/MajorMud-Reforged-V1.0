namespace RealmGate.Server;

public sealed record WorldgroupHookEvidence(string Name, uint Rva, uint HeadAddress);
public sealed record WorldgroupCallbackRegistryEvidence(string Name, uint Rva, int EntrySize, int CallbackOffset, string Semantics);

public static class WorldgroupCallbackDispatchRegistry
{
    public const string Module = "WGSERVER.EXE";
    public const string ModuleSha256 = "4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f";
    public const uint AddToArrayVa = 0x004381F2u;

    public static readonly IReadOnlyList<WorldgroupHookEvidence> Hooks = new[]
    {
        new WorldgroupHookEvidence("connect", 0x0001FA86u, 0x0045D3C0u),
        new WorldgroupHookEvidence("disconnect", 0x0001FA9Du, 0x0045D3C4u),
        new WorldgroupHookEvidence("cleanup", 0x0001FAB4u, 0x0045D3C8u),
        new WorldgroupHookEvidence("create-account", 0x0001FACBu, 0x0045D3CCu),
        new WorldgroupHookEvidence("delete-account", 0x0001FAE2u, 0x0045D3D0u),
        new WorldgroupHookEvidence("startup", 0x0001FAF9u, 0x0045D3D4u),
        new WorldgroupHookEvidence("shutdown", 0x0001FB10u, 0x0045D3D8u),
        new WorldgroupHookEvidence("final-shutdown", 0x0001FB27u, 0x0045D3DCu),
    };

    public static readonly IReadOnlyList<WorldgroupCallbackRegistryEvidence> Registries = new[]
    {
        new WorldgroupCallbackRegistryEvidence("_register_pseudok", 0x0001D0CEu, 20, 16, "16-byte key plus callback; capacity grows by 20 entries."),
        new WorldgroupCallbackRegistryEvidence("_register_pseudok2", 0x0001D174u, 20, 16, "Second independent 16-byte key plus callback registry."),
        new WorldgroupCallbackRegistryEvidence("_register_textvar", 0x0001FBB3u, 20, 16, "16-byte text-variable identifier plus callback."),
        new WorldgroupCallbackRegistryEvidence("_register_stascn", 0x0002D684u, 4, 0, "Growable raw function-pointer array."),
        new WorldgroupCallbackRegistryEvidence("_register_agent", 0x00045482u, 28, 12, "28-byte copied descriptor with default callbacks installed for null slots."),
        new WorldgroupCallbackRegistryEvidence("_register_module", 0x0001FC6Au, 36, 36, "3..24 character module name validation and a default callback at offset 0x24."),
        new WorldgroupCallbackRegistryEvidence("_registerDSModule", 0x0004AB10u, 12, 0, "12-byte descriptor; capacity grows in blocks of ten."),
    };

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Hooks.Count != 8) errors.Add("hook-count");
        if (Hooks.Select(x => x.HeadAddress).Distinct().Count() != Hooks.Count) errors.Add("duplicate-hook-head");
        if (Registries.Any(x => x.Rva == 0 || x.EntrySize <= 0)) errors.Add("invalid-callback-registry");
        return errors;
    }
}
