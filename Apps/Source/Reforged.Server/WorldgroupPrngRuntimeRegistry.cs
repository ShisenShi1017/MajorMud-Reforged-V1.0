namespace Reforged.Server;

public sealed record WorldgroupPrngRuntimeEvidence(string Component, uint Rva, string EvidenceClass, string Semantics);

public static class WorldgroupPrngRuntimeRegistry
{
    public const string RuntimeModule = "CW3220MT.DLL";
    public const string RuntimeSha256 = "4a2f6889f9dc5cfdf4ac44156dde8db0d20c6a0f63db06c576ac0bbba2011a15";
    public static readonly IReadOnlyList<WorldgroupPrngRuntimeEvidence> Evidence = new[]
    {
        new WorldgroupPrngRuntimeEvidence("WGSERVER _rand import thunk", 0x0004AE08u, "proven", "Jumps through IAT slot 0x0047B648 to CW3220MT.DLL!_rand."),
        new WorldgroupPrngRuntimeEvidence("CW3220MT _srand", 0x00016100u, "proven", "Stores the supplied 32-bit seed in thread-local runtime state at offset 0x44 and clears the companion high word/state at offset 0x48."),
        new WorldgroupPrngRuntimeEvidence("CW3220MT _rand", 0x0001611Cu, "proven", "Updates thread-local seed as seed = seed * 0x015A4E35 + 1 modulo 2^32 and returns (seed >> 16) & 0x7FFF."),
        new WorldgroupPrngRuntimeEvidence("WGSERVER startup seeding", 0x0001D5A4u, "proven", "Calls exported _now, zero-extends its packed 16-bit time value, and passes that value to CW3220MT.DLL!_srand exactly once in the statically reachable WGSERVER image."),
        new WorldgroupPrngRuntimeEvidence("WGSERVER _now", 0x000360FAu, "proven", "Builds a packed time seed as (hour << 11) + (minute << 5) + (second >> 1), yielding two-second resolution."),
    };

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Evidence.Count != 5) errors.Add("prng-evidence-count");
        if (Evidence.Any(x => x.Rva == 0 || x.EvidenceClass != "proven")) errors.Add("invalid-prng-evidence");
        return errors;
    }
}
