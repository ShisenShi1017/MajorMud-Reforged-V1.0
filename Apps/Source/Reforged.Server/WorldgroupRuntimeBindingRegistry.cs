namespace Reforged.Server;

public sealed record WorldgroupRuntimeBinding(string FileName, string Sha256, long Size, uint PeTimestamp, string Architecture, int NamedExportCount, int MatchedMajorMudHostSymbolCount, int TotalMajorMudHostSymbolCount);

public static class WorldgroupRuntimeBindingRegistry
{
    public static readonly WorldgroupRuntimeBinding SuppliedRuntime = new(
        "wgserver.exe",
        "4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f",
        432128L,
        0x4DB5267Fu,
        "x86",
        1381,
        156,
        156);

    public static bool Matches(string sha256, long size, uint peTimestamp) =>
        string.Equals(sha256, SuppliedRuntime.Sha256, System.StringComparison.OrdinalIgnoreCase) &&
        size == SuppliedRuntime.Size && peTimestamp == SuppliedRuntime.PeTimestamp;
}
