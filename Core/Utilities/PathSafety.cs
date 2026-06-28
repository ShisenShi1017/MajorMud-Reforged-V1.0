namespace Reforged.Core.Utilities;

/// <summary>
/// Shared path-safety helpers used by update, import, editor, and server code.
/// </summary>
public static class PathSafety
{
    public static bool IsProtectedProjectPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        return normalized.Equals("Saves", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Saves/", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ImportedDatabases", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("ImportedDatabases/", StringComparison.OrdinalIgnoreCase);
    }
}
