using System.Text.Json;
using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Starts the Server application far enough to verify repository identity, required folders,
/// manifest readability, canonical data loading, module discovery, startup diagnostics, and read-only Classic parity inventory without starting gameplay systems.
/// </summary>
public sealed class ServerBootstrap
{
    private static readonly string[] RequiredDirectories =
    {
        "Apps",
        "Core",
        "Data",
        "Docs",
        "Reports",
        "Bin"
    };

    public ServerBootstrapReport Run(string? startDirectory = null)
    {
        var report = new ServerBootstrapReport();
        var root = LocateProjectRoot(startDirectory ?? Directory.GetCurrentDirectory(), report);
        if (root is null)
        {
            return report;
        }

        report.ProjectRoot = root;
        VerifyRequiredDirectories(root, report);
        VerifyProjectManifest(root, report);

        report.AddCheck("server-data-catalog");
        var serverDataCatalog = new ServerDataCatalogAudit().Run(root, report.Validation);
        report.AttachServerDataCatalog(serverDataCatalog);

        report.AddCheck("game-module-catalog");
        var gameModuleCatalog = new ServerGameModuleCatalog().Load();
        report.AttachGameModuleCatalog(gameModuleCatalog);

        report.AddCheck("startup-diagnostics");
        var diagnostics = new ServerStartupDiagnostics().Run(root);
        report.AttachStartupDiagnostics(diagnostics);

        report.AddCheck("classic-parity-audit");
        var classicParityAudit = new ClassicParityAudit().Run(root);
        report.AttachClassicParityAudit(classicParityAudit);

        return report;
    }

    private static string? LocateProjectRoot(string startDirectory, ServerBootstrapReport report)
    {
        report.AddCheck("project-root");
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ProjectManifest.json")) && Directory.Exists(Path.Combine(directory.FullName, "Data")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_PROJECT_ROOT", "Could not locate repository root containing ProjectManifest.json and Data/.");
        return null;
    }

    private static void VerifyRequiredDirectories(string projectRoot, ServerBootstrapReport report)
    {
        report.AddCheck("required-directories");
        foreach (var directory in RequiredDirectories)
        {
            var path = Path.Combine(projectRoot, directory);
            var exists = Directory.Exists(path);
            report.AddRequiredDirectory(directory, path, exists);

            if (!exists)
            {
                report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_DIRECTORY", $"Required directory is missing: {directory}");
            }
        }
    }

    private static void VerifyProjectManifest(string projectRoot, ServerBootstrapReport report)
    {
        report.AddCheck("project-manifest");
        var manifestPath = Path.Combine(projectRoot, "ProjectManifest.json");
        if (!File.Exists(manifestPath))
        {
            report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_MANIFEST", "ProjectManifest.json was not found.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var project = root.TryGetProperty("project", out var projectProperty) ? projectProperty.GetString() : null;
            var codeUpdate = root.TryGetProperty("codeUpdate", out var codeUpdateProperty) ? codeUpdateProperty.GetString() : null;
            var databaseVersion = root.TryGetProperty("databaseVersion", out var databaseVersionProperty) ? databaseVersionProperty.GetString() : null;
            var milestone = root.TryGetProperty("milestone", out var milestoneProperty) ? milestoneProperty.GetString() : null;

            report.ProjectName = project;
            report.CodeUpdate = codeUpdate;
            report.DatabaseVersion = databaseVersion;
            report.Milestone = milestone;

            if (!string.Equals(project, "Reforged", StringComparison.Ordinal))
            {
                report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_PROJECT", "ProjectManifest.json does not identify project as Reforged.");
            }

            if (string.IsNullOrWhiteSpace(codeUpdate))
            {
                report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_CODE_UPDATE", "ProjectManifest.json does not contain a codeUpdate value.");
            }

            if (string.IsNullOrWhiteSpace(databaseVersion))
            {
                report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_DATABASE_VERSION", "ProjectManifest.json does not contain a databaseVersion value.");
            }
        }
        catch (JsonException ex)
        {
            report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_MANIFEST_JSON", $"ProjectManifest.json could not be parsed: {ex.Message}");
        }
        catch (IOException ex)
        {
            report.Validation.Add(ValidationSeverity.Error, "SERVER_BOOTSTRAP_MANIFEST_IO", $"ProjectManifest.json could not be read: {ex.Message}");
        }
    }
}
