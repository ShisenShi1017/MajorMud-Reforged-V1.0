namespace MajorMud.Reforged.Core.Health;

/// <summary>
/// Validates the required Code Update / Database Version package layout.
/// Every update ZIP must contain one labeled top-level folder with the apply script inside it.
/// </summary>
public sealed class UpdatePackageHealthCheck
{
    public RepositoryHealthReport CheckExtractedUpdateFolder(string updateFolder)
    {
        var report = new RepositoryHealthReport
        {
            GeneratedUtc = DateTimeOffset.UtcNow,
            RepositoryVersion = "Update Package"
        };

        RequireFile(report, updateFolder, "UpdateManifest.json");
        var applyScripts = Directory.Exists(updateFolder)
            ? Directory.GetFiles(updateFolder, "Apply-*.bat", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        if (applyScripts.Length == 0)
        {
            report.Issues.Add(new RepositoryHealthIssue
            {
                Severity = "Error",
                Area = "UpdatePackage",
                Path = updateFolder,
                Message = "Missing top-level Apply-*.bat script. The script must live inside the labeled update folder."
            });
        }

        RequireDirectory(report, updateFolder, "Payload");
        RequireDirectory(report, updateFolder, "Reports");

        return report;
    }

    private static void RequireDirectory(RepositoryHealthReport report, string root, string relative)
    {
        if (!Directory.Exists(Path.Combine(root, relative)))
        {
            report.Issues.Add(new RepositoryHealthIssue { Severity = "Error", Area = "UpdatePackage", Path = relative, Message = $"Missing required update directory: {relative}" });
        }
    }

    private static void RequireFile(RepositoryHealthReport report, string root, string relative)
    {
        if (!File.Exists(Path.Combine(root, relative)))
        {
            report.Issues.Add(new RepositoryHealthIssue { Severity = "Error", Area = "UpdatePackage", Path = relative, Message = $"Missing required update file: {relative}" });
        }
    }
}
