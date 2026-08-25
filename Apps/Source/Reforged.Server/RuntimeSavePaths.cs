namespace Reforged.Server;

public static class RuntimeSavePaths
{
    public const string ClassicWorldId = "classic";

    public static string HostRoot(string projectRoot) =>
        Path.Combine(projectRoot, "Saves", "Host");

    public static string RealmRoot(string projectRoot, string worldId)
    {
        var normalized = RuntimeIdentity.Normalize(worldId, "world");
        return normalized.Equals(ClassicWorldId, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(projectRoot, "Saves", "Classic")
            : Path.Combine(projectRoot, "Saves", "Custom", normalized);
    }

    public static string WorldIdFromCharacterId(string characterId)
    {
        var raw = (characterId ?? string.Empty).Trim();
        var separator = raw.IndexOf(':');
        return separator < 0
            ? ClassicWorldId
            : RuntimeIdentity.Normalize(raw[..separator], "world");
    }
}
