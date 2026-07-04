using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Captures the non-gameplay Server bootstrap result.
/// </summary>
public sealed class ServerBootstrapReport
{
    private readonly List<string> checks = new();
    private readonly List<RequiredDirectoryStatus> requiredDirectories = new();

    public IReadOnlyList<string> Checks => checks;

    public IReadOnlyList<RequiredDirectoryStatus> RequiredDirectories => requiredDirectories;

    public string? ProjectRoot { get; internal set; }

    public string? ProjectName { get; internal set; }

    public string? CodeUpdate { get; internal set; }

    public string? DatabaseVersion { get; internal set; }

    public string? Milestone { get; internal set; }

    public ValidationResult Validation { get; } = new();

    public ServerStartupDiagnosticReport? StartupDiagnostics { get; private set; }

    public ClassicParityAuditReport? ClassicParityAudit { get; private set; }

    public ServerDataCatalogAuditReport? ServerDataCatalog { get; private set; }

    public ServerWorldProjectionAuditReport? WorldProjection { get; private set; }

    public GameModuleCatalog? GameModuleCatalog { get; private set; }

    public ServerModuleReadinessReport? ModuleReadiness { get; private set; }

    public bool Passed => !Validation.HasErrors && (StartupDiagnostics?.Passed ?? true);

    public int ExitCode => Passed ? 0 : 1;

    public int IssueCount => Validation.Issues.Count;

    public int InfoCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Info);

    public int WarningCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);

    public int ErrorCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Error);

    public int FatalCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Fatal);

    public int RequiredDirectoryCount => requiredDirectories.Count;

    public int PresentRequiredDirectoryCount => requiredDirectories.Count(directory => directory.Exists);

    public int MissingRequiredDirectoryCount => requiredDirectories.Count(directory => !directory.Exists);

    public ServerBootstrapReadiness Readiness => new(
        ProjectRoot is not null,
        PresentRequiredDirectoryCount == RequiredDirectoryCount && RequiredDirectoryCount > 0,
        !string.IsNullOrWhiteSpace(ProjectName) &&
            !string.IsNullOrWhiteSpace(CodeUpdate) &&
            !string.IsNullOrWhiteSpace(DatabaseVersion) &&
            !string.IsNullOrWhiteSpace(Milestone),
        StartupDiagnostics?.Passed == true,
        Passed);

    public void AddCheck(string name) => checks.Add(name);

    public void AddRequiredDirectory(string name, string path, bool exists) =>
        requiredDirectories.Add(new RequiredDirectoryStatus(name, path, exists));

    public void AttachStartupDiagnostics(ServerStartupDiagnosticReport diagnostics)
    {
        StartupDiagnostics = diagnostics;
        Validation.Merge(diagnostics.Validation);
    }

    public void AttachClassicParityAudit(ClassicParityAuditReport audit)
    {
        ClassicParityAudit = audit;
    }

    public void AttachServerDataCatalog(ServerDataCatalogAuditReport audit)
    {
        ServerDataCatalog = audit;
    }

    public void AttachWorldProjection(ServerWorldProjectionAuditReport audit)
    {
        WorldProjection = audit;
    }

    public void AttachGameModuleCatalog(GameModuleCatalog catalog)
    {
        GameModuleCatalog = catalog;
    }

    public void AttachModuleReadiness(ServerModuleReadinessReport report)
    {
        ModuleReadiness = report;
    }
}

public sealed record RequiredDirectoryStatus(string Name, string Path, bool Exists);

public sealed record ServerBootstrapReadiness(
    bool ProjectRootFound,
    bool RequiredDirectoriesReady,
    bool ManifestReady,
    bool StartupDiagnosticsReady,
    bool HostToolsReady)
{
    public string Status => HostToolsReady ? "host-tools-ready" : "not-ready";

    public string GameplayStatus => "disabled";
}
