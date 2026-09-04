namespace RealmGate.Server;

public enum RuntimeClassicExtensionBoundaryKind
{
    ClassicGameplayOwner,
    ReforgedOnly,
    CompatibilitySurface
}

public sealed record RuntimeClassicExtensionBoundaryRow(
    string Id,
    RuntimeClassicExtensionBoundaryKind Kind,
    string Boundary,
    string Evidence);

/// <summary>
/// Keeps proven Reforged-only surfaces out of Classic without treating the generic
/// infrastructure itself as evidence for or against native MajorMUD behavior.
/// </summary>
public static class RuntimeClassicExtensionBoundaryAuthority
{
    // CodeUpdate0351 direct WCCMMUD.DLL export census plus decoded Classic service corpus.
    public static readonly int NativeCommandExportCount = 78;
    public static readonly int DecodedClassicServiceCount = 204;
    public static readonly bool NativeGiveCommandPresent = true;
    public static readonly bool NativeAuctionCommandPresent = true;
    public static readonly bool NativeTradeCommandPresent = false;
    public static readonly bool NativeRepairCommandPresent = false;
    public static readonly int DecodedRepairServiceCount = 0;

    private static readonly HashSet<string> ReforgedOnlyCommands =
        new(StringComparer.OrdinalIgnoreCase) { "trade", "repair" };

    public static IReadOnlyList<RuntimeClassicExtensionBoundaryRow> Rows { get; } =
    [
        new("command.trade-escrow", RuntimeClassicExtensionBoundaryKind.ReforgedOnly,
            "The direct TRADE convenience transaction is unavailable in Classic; stock GIVE remains the player-transfer owner.",
            "Original WCCMMUD.DLL exports _cmd_give/_cmd_auction but no _cmd_trade; retained parity evidence and official command help confirm the boundary."),
        new("service.repair", RuntimeClassicExtensionBoundaryKind.ReforgedOnly,
            "Generated durability REPAIR is unavailable in Classic because no stock repair command or service is projected.",
            "Original WCCMMUD.DLL has no _cmd_repair; official shop command help and 204 decoded Classic services contain no repair owner."),
        new("random.generic-streams", RuntimeClassicExtensionBoundaryKind.ReforgedOnly,
            "RuntimeGlobalRandomService is generic Reforged infrastructure and cannot satisfy Classic PRNG parity.",
            "Librarium/Objects/parity/parity_records.json."),
        new("random.worldgroup", RuntimeClassicExtensionBoundaryKind.ClassicGameplayOwner,
            "RuntimeClassicWorldgroupPrng owns the proven native Borland rand/srand core.",
            "CW3220MT disassembly and Librarium/Objects/parity/parity_records.json."),
        new("world.generic-hazards", RuntimeClassicExtensionBoundaryKind.ReforgedOnly,
            "A generic environmental-hazard subsystem is not a stock Classic record family.",
            "RoomType 0-24 schema closure; native effects remain with decoded traps, casts, text and actions."),
        new("world.generic-containers", RuntimeClassicExtensionBoundaryKind.ReforgedOnly,
            "A generic world-container subsystem is not a standalone Classic owner.",
            "Librarium/Objects/parity/parity_records.json."),
        new("world.generic-traps", RuntimeClassicExtensionBoundaryKind.ReforgedOnly,
            "RuntimeTrapService is not the Classic type-09/type-24 trap owner.",
            "Librarium/Objects/parity/parity_records.json."),
        new("world.native-traps", RuntimeClassicExtensionBoundaryKind.ClassicGameplayOwner,
            "Generated Classic exit traps and native RoomRec state retain Classic ownership.",
            "RuntimeClassicExitTrapCatalog and Librarium/Objects/parity/parity_records.json."),
        new("compatibility.native-save-catalog", RuntimeClassicExtensionBoundaryKind.CompatibilitySurface,
            "Native save/catalog byte compatibility is an import/export compatibility surface, not an unclassified gameplay extension.",
            "Compatibility ownership is separately governed by the import/export/recovery assertions; it does not admit Reforged gameplay behavior into Classic.")
    ];

    public static bool IsClassicWorld(string worldType) =>
        worldType.Equals("Classic", StringComparison.OrdinalIgnoreCase);

    public static bool IsReforgedOnlyCommand(string command) =>
        ReforgedOnlyCommands.Contains(command);

    public static bool IsCommandAvailable(string worldType, string command) =>
        !IsClassicWorld(worldType) || !IsReforgedOnlyCommand(command);

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 9) failures.Add("extension-boundary-row-count");
        if (Rows.Count(row => row.Kind == RuntimeClassicExtensionBoundaryKind.ReforgedOnly) != 6)
            failures.Add("extension-boundary-reforged-only-count");
        if (Rows.Count(row => row.Kind == RuntimeClassicExtensionBoundaryKind.ClassicGameplayOwner) != 2)
            failures.Add("extension-boundary-classic-owner-count");
        if (Rows.Count(row => row.Kind == RuntimeClassicExtensionBoundaryKind.CompatibilitySurface) != 1)
            failures.Add("extension-boundary-compatibility-surface-count");
        if (Rows.Any(row => string.IsNullOrWhiteSpace(row.Boundary) || string.IsNullOrWhiteSpace(row.Evidence)))
            failures.Add("extension-boundary-unclassified-row");
        if (NativeCommandExportCount != 78 || DecodedClassicServiceCount != 204 ||
            !NativeGiveCommandPresent || !NativeAuctionCommandPresent || NativeTradeCommandPresent ||
            NativeRepairCommandPresent || DecodedRepairServiceCount != 0)
            failures.Add("native-extension-absence-evidence");
        if (IsCommandAvailable("Classic", "trade") || IsCommandAvailable("Classic", "repair"))
            failures.Add("classic-extension-command-admission");
        if (!IsCommandAvailable("Classic", "give") || !IsCommandAvailable("Custom", "trade") || !IsCommandAvailable("AI", "repair"))
            failures.Add("stock-or-overlay-command-admission");
        return failures;
    }
}
