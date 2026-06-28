namespace MajorMud.Reforged.Core.Health;

/// <summary>
/// Performs lightweight repository checks shared by the Launcher, update scripts, editor, importer, and server startup validation.
/// </summary>
public sealed class RepositoryHealthChecker
{
    public RepositoryHealthReport Check(string projectRoot)
    {
        var report = new RepositoryHealthReport
        {
            GeneratedUtc = DateTimeOffset.UtcNow,
            RepositoryVersion = ReadVersion(projectRoot)
        };

        RequireDirectory(report, projectRoot, "Core");
        RequireDirectory(report, projectRoot, "Data");
        RequireDirectory(report, projectRoot, "Docs");
        RequireDirectory(report, projectRoot, "Bin");
        RequireFile(report, projectRoot, "README.md");
        RequireFile(report, projectRoot, "CHANGELOG.md");
        RequireFile(report, projectRoot, "VERSION.md");
        RequireFile(report, projectRoot, "ProjectManifest.json");
        return report;
    }

    private static string ReadVersion(string root)
    {
        var versionFile = Path.Combine(root, "VERSION.md");
        return File.Exists(versionFile) ? File.ReadAllText(versionFile) : string.Empty;
    }

    private static void RequireDirectory(RepositoryHealthReport report, string root, string relative)
    {
        if (!Directory.Exists(Path.Combine(root, relative)))
        {
            report.Issues.Add(new RepositoryHealthIssue { Severity = "Error", Area = "Repository", Path = relative, Message = $"Missing required directory: {relative}" });
        }
    }

    private static void RequireFile(RepositoryHealthReport report, string root, string relative)
    {
        if (!File.Exists(Path.Combine(root, relative)))
        {
            report.Issues.Add(new RepositoryHealthIssue { Severity = "Error", Area = "Repository", Path = relative, Message = $"Missing required file: {relative}" });
        }
    }
}
