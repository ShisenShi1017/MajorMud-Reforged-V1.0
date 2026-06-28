using System.Text.RegularExpressions;

namespace Reforged.Core.Health;

/// <summary>
/// Enforces the Milestone 1 rule that shared models live in Core and are not duplicated in applications.
/// This checker is intentionally dependency-free so the Launcher, update scripts, and future server startup
/// validation can use the same policy without requiring external packages.
/// </summary>
public sealed class DuplicateModelEnforcer
{
    private static readonly Regex TypeDeclaration = new(@"\b(public|internal|private|protected)?\s*(sealed\s+|abstract\s+|partial\s+)?(class|record|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedApplicationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClientApplication",
        "EditorApplication",
        "ImporterApplication",
        "LauncherApplication",
        "ServerApplication"
    };

    public DuplicateModelEnforcementReport Check(string projectRoot)
    {
        var report = new DuplicateModelEnforcementReport { GeneratedUtc = DateTimeOffset.UtcNow };
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
        {
            report.Issues.Add(new DuplicateModelEnforcementIssue
            {
                Severity = "Error",
                Message = "Project root does not exist; duplicate model enforcement could not run."
            });
            return report;
        }

        var coreTypes = FindDeclaredTypes(Path.Combine(projectRoot, "Core"));
        var appTypes = FindDeclaredTypes(Path.Combine(projectRoot, "Apps"));

        foreach (var appType in appTypes)
        {
            var appTypeName = appType.Key;
            var appTypeLocation = appType.Value;

            if (AllowedApplicationTypes.Contains(appTypeName))
            {
                continue;
            }

            if (!IsModelLike(appTypeName))
            {
                continue;
            }

            if (coreTypes.TryGetValue(appTypeName, out var coreLocation))
            {
                report.Issues.Add(new DuplicateModelEnforcementIssue
                {
                    ModelName = appTypeName,
                    CoreLocation = coreLocation,
                    ApplicationLocation = appTypeLocation,
                    Severity = "Error",
                    Message = $"Application model '{appTypeName}' duplicates a Core type. Move shared model changes into Core and reference Core from the application."
                });
            }
        }

        return report;
    }

    private static Dictionary<string, string> FindDeclaredTypes(string root)
    {
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
        {
            return types;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedOrBuildPath(file))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match match in TypeDeclaration.Matches(text))
            {
                var name = match.Groups["name"].Value;
                if (!types.ContainsKey(name))
                {
                    types[name] = file;
                }
            }
        }

        return types;
    }

    private static bool IsGeneratedOrBuildPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
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
}
