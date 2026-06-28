using System.Text.RegularExpressions;

namespace Reforged.Core.Health;

/// <summary>
/// SDK-free source-level duplicate model check for Milestone 1 completion.
/// Shared model-like types must live in Core, not in application projects.
/// </summary>
public sealed class DuplicateModelEnforcementHealthCheck
{
    private static readonly Regex TypeDeclaration = new(@"\b(class|record|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    public IReadOnlyList<string> Check(string projectRoot)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
        {
            issues.Add("Project root does not exist.");
            return issues;
        }

        var coreNames = FindTypeNames(Path.Combine(projectRoot, "Core"));
        var appNames = FindTypes(Path.Combine(projectRoot, "Apps"));
        foreach (var appType in appNames)
        {
            if (!IsModelLike(appType.Name))
            {
                continue;
            }

            if (coreNames.Contains(appType.Name))
            {
                issues.Add($"Duplicate model-like type '{appType.Name}' found in application source: {appType.Path}");
            }
        }

        return issues;
    }

    private static HashSet<string> FindTypeNames(string root)
    {
        return FindTypes(root).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<DeclaredType> FindTypes(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match match in TypeDeclaration.Matches(text))
            {
                yield return new DeclaredType(match.Groups["name"].Value, file);
            }
        }
    }

    private static bool IsModelLike(string name)
    {
        return name.EndsWith("Record", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Model", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Entity", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Data", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Info", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Reference", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Manifest", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Result", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Issue", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DeclaredType(string Name, string Path);
}
