namespace RealmGate.Server;

public sealed record WorldgroupExportImplementation(string Symbol, int Ordinal, uint Rva, string CallingConvention, int ArgumentCount, string ReturnDomain, string EvidenceClass, string Semantics);

public static class WorldgroupExportImplementationRegistry
{
    public static readonly IReadOnlyList<WorldgroupExportImplementation> PriorityExports = new[]
    {
        new WorldgroupExportImplementation("_sameas", 1134, 0x0003DAE4u, "cdecl", 2, "int16/bool", "proven", "Returns 1 only when both NUL-terminated strings have identical length and each byte compares equal after the host character-fold helper; otherwise returns 0."),
        new WorldgroupExportImplementation("_genrdn", 1452, 0x0000739Cu, "cdecl", 2, "int32", "proven", "If exclusiveMaximum is 0 returns 0. If exclusiveMaximum <= minimum returns minimum. Otherwise repeatedly draws from the thread-local CW3220MT PRNG and accumulates bounded remainders until the result is at least minimum; result remains below exclusiveMaximum."),
        new WorldgroupExportImplementation("_lngrnd", 1453, 0x000073EEu, "cdecl", 2, "int32", "proven", "Instruction-equivalent range routine to _genrdn with the same edge cases and thread-local CW3220MT PRNG dependency."),
        new WorldgroupExportImplementation("_tfsopn", 1275, 0x0003E7DCu, "cdecl", 1, "int16 count", "proven", "Normalizes and bounds a file pattern into the TFS global buffer, derives its path component, enumerates matching files with _fnd1st/_fndnxt, stores the first search result, sets TFS state to 1 when a match exists or 0 otherwise, and returns the number of matches."),
        new WorldgroupExportImplementation("_tfspfx", 1276, 0x0003E9D8u, "cdecl", 1, "int16/bool", "proven", "Checks whether the supplied prefix matches the beginning of the active TFS text buffer using _sameto. On match it advances by strlen(prefix), skips whitespace, stores the suffix pointer globally, and returns 1 only when the remaining suffix is non-empty."),
        new WorldgroupExportImplementation("_tfsabt", 1271, 0x0003EA3Eu, "cdecl", 0, "void/int32 zeroed side effect", "proven", "If TFS state equals 3, closes/releases the global TFS handle through the host close helper, then unconditionally resets TFS state to 0."),
    };

    public static WorldgroupExportImplementation? Find(string symbol) => PriorityExports.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.Ordinal));
    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PriorityExports.Count != 6) errors.Add("priority-export-count");
        if (PriorityExports.Select(x => x.Symbol).Distinct(StringComparer.Ordinal).Count() != PriorityExports.Count) errors.Add("duplicate-symbol");
        if (PriorityExports.Any(x => x.Rva == 0 || x.EvidenceClass != "proven")) errors.Add("invalid-evidence");
        return errors;
    }
}
