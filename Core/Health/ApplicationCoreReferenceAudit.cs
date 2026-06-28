namespace Reforged.Core.Health;

/// <summary>
/// Lightweight source-layout audit for Milestone 1: every application project must reference Core.
/// This is text/file inspection only and does not require the .NET SDK.
/// </summary>
public sealed class ApplicationCoreReferenceAudit
{
    private static readonly string[] RequiredApplications =
    {
        "Reforged.Server",
        "Reforged.Importer",
        "Reforged.Editor",
        "Reforged.Launcher",
        "Reforged.Client"
    };

    public IReadOnlyList<string> Inspect(string projectRoot)
    {
        var issues = new List<string>();
        var sourceRoot = Path.Combine(projectRoot, "Apps", "Source");

        foreach (var app in RequiredApplications)
        {
            var projectPath = Path.Combine(sourceRoot, app, $"{app}.csproj");
            if (!File.Exists(projectPath))
            {
                issues.Add($"Missing application project: {projectPath}");
                continue;
            }

            var text = File.ReadAllText(projectPath);
            if (!text.Contains("Reforged.Core.csproj", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Application project does not reference Core: {projectPath}");
            }
        }

        return issues;
    }
}
