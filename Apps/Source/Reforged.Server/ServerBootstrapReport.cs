using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Captures the non-gameplay Server bootstrap result.
/// </summary>
public sealed class ServerBootstrapReport
{
    private readonly List<string> checks = new();

    public IReadOnlyList<string> Checks => checks;

    public string? ProjectRoot { get; internal set; }

    public string? ProjectName { get; internal set; }

    public string? CodeUpdate { get; internal set; }

    public string? DatabaseVersion { get; internal set; }

    public string? Milestone { get; internal set; }

    public ValidationResult Validation { get; } = new();

    public ServerStartupDiagnosticReport? StartupDiagnostics { get; private set; }

    public bool Passed => !Validation.HasErrors && (StartupDiagnostics?.Passed ?? true);

    public int ExitCode => Passed ? 0 : 1;

    public int IssueCount => Validation.Issues.Count;

    public int InfoCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Info);

    public int WarningCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);

    public int ErrorCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Error);

    public int FatalCount => Validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Fatal);

    public void AddCheck(string name) => checks.Add(name);

    public void AttachStartupDiagnostics(ServerStartupDiagnosticReport diagnostics)
    {
        StartupDiagnostics = diagnostics;
        Validation.Merge(diagnostics.Validation);
    }
}
