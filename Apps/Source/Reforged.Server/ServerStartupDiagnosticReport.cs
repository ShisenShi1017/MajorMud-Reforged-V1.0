using Reforged.Core.Diagnostics;
using Reforged.Core.Logging;
using Reforged.Core.Validation;

namespace Reforged.Server;

/// <summary>
/// Captures the startup-only Server diagnostics result without starting gameplay systems.
/// </summary>
public sealed class ServerStartupDiagnosticReport
{
    private readonly List<string> checks = new();

    public IReadOnlyList<string> Checks => checks;

    public ValidationResult Validation { get; } = new();

    public StartupDiagnosticReport? CoreReport { get; private set; }

    public IReadOnlyList<LogEntry> LogEntries { get; private set; } = Array.Empty<LogEntry>();

    public bool Passed => !Validation.HasErrors && (CoreReport?.Passed ?? true);

    public void AddCheck(string name) => checks.Add(name);

    public void AttachCoreReport(StartupDiagnosticReport report)
    {
        CoreReport = report;
        Validation.Merge(report.Validation);
    }

    public void AttachLogEntries(IReadOnlyList<LogEntry> entries) => LogEntries = entries;
}
