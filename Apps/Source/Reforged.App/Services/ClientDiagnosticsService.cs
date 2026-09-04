using System.IO;
using System.Text;
using Reforged.App.Models;

namespace Reforged.App.Services;

public interface IClientDiagnosticsService
{
    ClientDiagnosticsSnapshot Snapshot { get; }
    Task RunAsync(CancellationToken cancellationToken = default);
    Task<string> ExportAsync(string? destinationPath = null, CancellationToken cancellationToken = default);
}

public sealed class ClientDiagnosticsService : IClientDiagnosticsService
{
    private readonly string _rootPath;
    private readonly IClientSessionService _sessions;
    private readonly IClientParsingProfileService _parsing;

    public ClientDiagnosticsService(string rootPath, IClientSessionService sessions, IClientParsingProfileService parsing)
    {
        _rootPath = rootPath;
        _sessions = sessions;
        _parsing = parsing;
    }

    public ClientDiagnosticsSnapshot Snapshot { get; } = new();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Snapshot.Checks.Clear();
        cancellationToken.ThrowIfCancellationRequested();

        Add("Client data directory", "Storage", CheckWritableDirectory(_rootPath));
        Add("Session limit", "Sessions", _sessions.Sessions.Count <= 6
            ? Pass($"{_sessions.Sessions.Count} of 6 session slots are currently defined.")
            : Error($"{_sessions.Sessions.Count} sessions are defined; the Client limit is six."));

        var duplicateIds = _sessions.Sessions
            .GroupBy(session => session.SessionId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Add("Unique session identifiers", "Sessions", duplicateIds.Length == 0
            ? Pass("Every session has a unique local identifier.")
            : Error($"Duplicate session identifiers: {string.Join(", ", duplicateIds)}"));

        var invalidPorts = _sessions.Sessions.Where(session => session.Port is < 1 or > 65535).ToArray();
        Add("Connection ports", "Sessions", invalidPorts.Length == 0
            ? Pass("All configured ports are within the valid TCP range.")
            : Error($"Invalid ports: {string.Join(", ", invalidPorts.Select(s => s.DisplayName))}"));

        var unnamedHosts = _sessions.Sessions.Where(session => string.IsNullOrWhiteSpace(session.HostName)).ToArray();
        Add("Connection hosts", "Sessions", unnamedHosts.Length == 0
            ? Pass("Every configured session has a Host name or address.")
            : Warning($"Missing Host name: {string.Join(", ", unnamedHosts.Select(s => s.DisplayName))}"));

        var faulted = _sessions.Sessions.Where(session => session.ConnectionState == ClientConnectionState.Faulted).ToArray();
        Add("Connection health", "Sessions", faulted.Length == 0
            ? Pass("No session is currently faulted.")
            : Warning($"Faulted sessions: {string.Join(", ", faulted.Select(s => s.DisplayName))}"));

        Add("Parsing profile", "Recognition", _parsing.Profile.Rules.Count > 0
            ? Pass($"{_parsing.Profile.Rules.Count} recognition rules are available for {_parsing.Profile.ServerName} / {_parsing.Profile.WorldName}.")
            : Error("The selected parsing profile has no recognition rules."));

        var invalidRules = _parsing.Profile.Rules.Where(rule => rule.Enabled && string.IsNullOrWhiteSpace(rule.Pattern)).ToArray();
        Add("Enabled parsing rules", "Recognition", invalidRules.Length == 0
            ? Pass("Every enabled recognition rule has a match pattern.")
            : Error($"{invalidRules.Length} enabled recognition rules have empty patterns."));

        Add("Terminal state", "Terminal", _sessions.Sessions.All(session => session.TerminalPreferences.MaximumScrollbackLines >= 100)
            ? Pass("Every session retains at least 100 terminal entries.")
            : Warning("One or more sessions have an unusually small scrollback limit."));

        Add("Route safety", "Navigation", _sessions.Sessions.All(session => session.RouteState != RouteExecutionState.Faulted)
            ? Pass("No session currently has a faulted route.")
            : Warning("One or more sessions have a faulted route that should be reviewed before resuming."));

        await Task.Yield();
        Snapshot.LastRun = DateTimeOffset.Now;
        var errors = Snapshot.Checks.Count(check => check.Severity == ClientDiagnosticSeverity.Error);
        var warnings = Snapshot.Checks.Count(check => check.Severity == ClientDiagnosticSeverity.Warning);
        Snapshot.Summary = errors == 0
            ? warnings == 0
                ? $"All {Snapshot.Checks.Count} Client readiness checks passed."
                : $"Client diagnostics completed with {warnings} warning(s) and no errors."
            : $"Client diagnostics found {errors} error(s) and {warnings} warning(s).";
    }

    public async Task<string> ExportAsync(string? destinationPath = null, CancellationToken cancellationToken = default)
    {
        if (Snapshot.LastRun is null) await RunAsync(cancellationToken);
        var diagnosticsDirectory = Path.Combine(_rootPath, "Diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);
        destinationPath ??= Path.Combine(diagnosticsDirectory, $"client-diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");

        var text = new StringBuilder()
            .AppendLine("RealmGate Client Diagnostics")
            .AppendLine($"Generated: {DateTimeOffset.Now:O}")
            .AppendLine($"Summary: {Snapshot.Summary}")
            .AppendLine();
        foreach (var check in Snapshot.Checks)
            text.AppendLine($"[{check.Severity}] {check.Area} / {check.Name}: {check.Status} — {check.Details}");

        await File.WriteAllTextAsync(destinationPath, text.ToString(), cancellationToken);
        return destinationPath;
    }

    private void Add(string name, string area, (string Status, string Details, ClientDiagnosticSeverity Severity) result) =>
        Snapshot.Checks.Add(new ClientDiagnosticCheck { Name = name, Area = area, Status = result.Status, Details = result.Details, Severity = result.Severity });

    private static (string, string, ClientDiagnosticSeverity) CheckWritableDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var testPath = Path.Combine(path, $".client-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testPath, "ok");
            File.Delete(testPath);
            return Pass(path);
        }
        catch (Exception exception)
        {
            return Error(exception.Message);
        }
    }

    private static (string, string, ClientDiagnosticSeverity) Pass(string details) => ("Passed", details, ClientDiagnosticSeverity.Information);
    private static (string, string, ClientDiagnosticSeverity) Warning(string details) => ("Warning", details, ClientDiagnosticSeverity.Warning);
    private static (string, string, ClientDiagnosticSeverity) Error(string details) => ("Failed", details, ClientDiagnosticSeverity.Error);
}
