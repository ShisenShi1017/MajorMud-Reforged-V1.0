namespace RealmGate.Compiler;

public sealed record LibrariumCompilerOptions(
    string RepositoryRoot,
    string WorldId = "classic",
    bool Clean = false,
    bool ClientProjection = true,
    string? OutputRootOverride = null)
{
    public string LibrariumRoot => Path.Combine(RepositoryRoot, "Librarium");
    public string DataRoot => Path.Combine(RepositoryRoot, "Data");
    public string OutputRoot => ResolveOutputRoot();

    public static string NormalizeWorld(string worldId)
    {
        if (string.IsNullOrWhiteSpace(worldId)) return "Classic";
        return worldId.Equals("classic", StringComparison.OrdinalIgnoreCase) ? "Classic" : worldId.Trim();
    }

    private string ResolveOutputRoot()
    {
        var outputRoot = string.IsNullOrWhiteSpace(OutputRootOverride)
            ? Path.Combine(DataRoot, NormalizeWorld(WorldId))
            : Path.GetFullPath(OutputRootOverride);
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        var fullLibrariumRoot = Path.GetFullPath(LibrariumRoot);

        if (IsSameOrDescendant(fullOutputRoot, fullLibrariumRoot))
        {
            throw new InvalidOperationException(
                $"Generated runtime Data must never be written inside Librarium. Requested output: '{fullOutputRoot}'.");
        }

        return fullOutputRoot;
    }

    private static bool IsSameOrDescendant(string candidate, string parent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidate, parent, comparison)) return true;

        var parentWithSeparator = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(parentWithSeparator, comparison);
    }
}
