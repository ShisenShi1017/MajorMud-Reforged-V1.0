namespace Reforged.Server;

public sealed record WorldgroupVectorHookEvidence(string Name, uint Rva, uint DescriptorGlobal);

public static class WorldgroupVectorHookRegistry
{
    public const string Module = "WGSERVER.EXE";
    public const uint NewArrayVa = 0x0043814Cu;
    public const uint AddToArrayVa = 0x004381F2u;
    public const uint InvokeVectorsVa = 0x0041FB83u;
    public const int InitialCapacity = 25;
    public const int GrowthIncrement = 25;
    public const int ElementSize = 4;

    public static readonly IReadOnlyList<WorldgroupVectorHookEvidence> Hooks = new[]
    {
        new WorldgroupVectorHookEvidence("_hook_connect", 0x0001FA86u, 0x0045D3C0u),
        new WorldgroupVectorHookEvidence("_hook_disconnect", 0x0001FA9Du, 0x0045D3C4u),
        new WorldgroupVectorHookEvidence("_hook_cleanup", 0x0001FAB4u, 0x0045D3C8u),
        new WorldgroupVectorHookEvidence("_hook_createacct", 0x0001FACBu, 0x0045D3CCu),
        new WorldgroupVectorHookEvidence("_hook_delacct", 0x0001FAE2u, 0x0045D3D0u),
        new WorldgroupVectorHookEvidence("_hook_startup", 0x0001FAF9u, 0x0045D3D4u),
        new WorldgroupVectorHookEvidence("_hook_shutdown", 0x0001FB10u, 0x0045D3D8u),
        new WorldgroupVectorHookEvidence("_hook_finalshutdown", 0x0001FB27u, 0x0045D3DCu),
        new WorldgroupVectorHookEvidence("_hook_swt2aa", 0x0001FB3Eu, 0x0045D3E0u),
        new WorldgroupVectorHookEvidence("_hook_swt2cs", 0x0001FB55u, 0x0045D3E4u),
        new WorldgroupVectorHookEvidence("_hook_announce", 0x0001FB6Cu, 0x0045D3E8u),
    };

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Hooks.Count != 11) errors.Add("hook-count");
        if (Hooks.Select(x => x.DescriptorGlobal).Distinct().Count() != Hooks.Count) errors.Add("duplicate-descriptor-global");
        return errors;
    }
}
