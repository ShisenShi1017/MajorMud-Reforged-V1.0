namespace MajorMud.Reforged.Compiler;

public sealed record LibrariumCompilerOptions(
    string RepositoryRoot,
    string WorldId = "classic",
    bool Clean = false,
    bool ClientProjection = true,
    string? OutputRootOverride = null)
{
    public string LibrariumRoot => Path.Combine(RepositoryRoot, "Librarium");
    public string DataRoot => Path.Combine(RepositoryRoot, "Data");
    public string OutputRoot => string.IsNullOrWhiteSpace(OutputRootOverride)
        ? Path.Combine(DataRoot, NormalizeWorld(WorldId))
        : Path.GetFullPath(OutputRootOverride);

    public static string NormalizeWorld(string worldId)
    {
        if (string.IsNullOrWhiteSpace(worldId)) return "Classic";
        return worldId.Equals("classic", StringComparison.OrdinalIgnoreCase) ? "Classic" : worldId.Trim();
    }
}
