namespace MajorMud.Reforged.Compiler;

public sealed record LibrariumCompilerOptions(
    string RepositoryRoot,
    string WorldId = "classic",
    bool Clean = false,
    bool ClientProjection = true)
{
    public string LibrariumRoot => Path.Combine(RepositoryRoot, "Librarium");
    public string DataRoot => Path.Combine(RepositoryRoot, "Data");
    public string OutputRoot => Path.Combine(DataRoot, NormalizeWorld(WorldId));

    public static string NormalizeWorld(string worldId)
    {
        if (string.IsNullOrWhiteSpace(worldId)) return "Classic";
        return worldId.Equals("classic", StringComparison.OrdinalIgnoreCase) ? "Classic" : worldId.Trim();
    }
}
