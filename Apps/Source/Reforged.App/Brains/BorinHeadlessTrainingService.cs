using System.Diagnostics;
using System.Text.Json;

namespace Reforged.App.Brains;

/// <summary>
/// Chat-driven launcher/status/test-report surface for Borin's unattended MajorMUD training.
/// There is intentionally no UI button: Forge phrases start/status/report/stop the trainer.
/// </summary>
public sealed class BorinHeadlessTrainingService
{
    public bool IsTrainingRequest(string request)
    {
        if (string.IsNullOrWhiteSpace(request)) return false;
        var text = request.Trim().ToLowerInvariant();
        return text.Contains("headless training", StringComparison.Ordinal)
            || text.Contains("headless trainer", StringComparison.Ordinal)
            || text.Contains("train headless", StringComparison.Ordinal)
            || text.Contains("continuous training", StringComparison.Ordinal)
            || text.Contains("training tester", StringComparison.Ordinal)
            || text.Contains("tester report", StringComparison.Ordinal)
            || text.Contains("training defects", StringComparison.Ordinal)
            || text.Contains("defects found", StringComparison.Ordinal)
            || text.Contains("bugs found", StringComparison.Ordinal)
            || text.Contains("found broken", StringComparison.Ordinal)
            || text.Contains("have you found broken", StringComparison.Ordinal)
            || text.Contains("whats broken", StringComparison.Ordinal)
            || text.Contains("what's broken", StringComparison.Ordinal)
            || text.Contains("what is broken", StringComparison.Ordinal);
    }

    public async Task<string> HandleAsync(string repositoryPath, string request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = request.Trim().ToLowerInvariant();
        if (IsTesterReportRequest(text))
            return DefectReport(repositoryPath);
        if (text.Contains("stop", StringComparison.Ordinal) || text.Contains("cancel", StringComparison.Ordinal))
            return await StopAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        if (text.Contains("status", StringComparison.Ordinal) || text.Contains("progress", StringComparison.Ordinal) || text.Contains("how is", StringComparison.Ordinal))
            return Status(repositoryPath);
        return Start(repositoryPath);
    }

    private static bool IsTesterReportRequest(string text) =>
        text.Contains("tester report", StringComparison.Ordinal)
        || text.Contains("training defects", StringComparison.Ordinal)
        || text.Contains("defects found", StringComparison.Ordinal)
        || text.Contains("bugs found", StringComparison.Ordinal)
        || text.Contains("found broken", StringComparison.Ordinal)
        || text.Contains("have you found broken", StringComparison.Ordinal)
        || text.Contains("whats broken", StringComparison.Ordinal)
        || text.Contains("what's broken", StringComparison.Ordinal)
        || text.Contains("what is broken", StringComparison.Ordinal);

    private static string Start(string repositoryPath)
    {
        var root = NormalizeRepositoryRoot(repositoryPath);
        if (root.Length == 0 || !Directory.Exists(root))
            return "I cannot start headless training because the Reforged repository path is not valid.";

        var trainingRoot = Path.Combine(root, "AI", "Training");
        var script = Path.Combine(trainingRoot, "borin_headless_trainer.py");
        if (!File.Exists(script))
            return $"I cannot start headless training because the trainer script is missing: {script}";

        var runtimeRoot = Path.Combine(trainingRoot, "runtime");
        var pidPath = Path.Combine(runtimeRoot, "trainer.pid");
        if (TryReadLiveProcess(pidPath, out var existing))
            return $"Continuous headless training is already running (PID {existing!.Id}). Say 'Borin, headless training status' for progress or 'Borin, what have you found broken?' for the tester report.";

        Directory.CreateDirectory(runtimeRoot);
        TryDelete(Path.Combine(runtimeRoot, "stop.request"));
        var python = ResolvePython();
        if (python is null)
            return "I cannot start headless training because Python could not be found. Install Python 3 or set BORIN_PYTHON to python.exe, then tell me to start headless training again.";

        var start = new ProcessStartInfo
        {
            FileName = python.Value.FileName,
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var prefix in python.Value.PrefixArguments) start.ArgumentList.Add(prefix);
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--repo");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add("--continuous");
        start.ArgumentList.Add("--player");
        start.ArgumentList.Add("Borin");

        try
        {
            var process = Process.Start(start);
            if (process is null) return "The continuous headless trainer process could not be started.";
            File.WriteAllText(pidPath, process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return "Continuous headless training and testing started against the 100% Classic parity runtime. Before normal learning and again at every full-cycle boundary, I will run the final-strong and cumulative Classic parity smoke gates, then probe HELP/TOP/TOPTEN through the live player runtime. I will keep cycling through survival, exploration and hazards, combat, spells/powers/item use, NPCs/dialogue/quests, inventory/equipment/repairs, progression, and economy until you tell me to stop. Each learning cycle is about 4 hours, plus parity-gate time. I will keep 10 gold carried, bank surplus, never withdraw just to refill that reserve, and I will record any parity regression or reproducible runtime defect while I train.";
        }
        catch (Exception ex)
        {
            return "Continuous headless training could not start: " + ex.Message;
        }
    }

    private static async Task<string> StopAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var root = NormalizeRepositoryRoot(repositoryPath);
        var runtimeRoot = Path.Combine(root, "AI", "Training", "runtime");
        var pidPath = Path.Combine(runtimeRoot, "trainer.pid");
        if (!TryReadLiveProcess(pidPath, out var process))
            return "Continuous headless training is not currently running.";

        try
        {
            Directory.CreateDirectory(runtimeRoot);
            File.WriteAllText(Path.Combine(runtimeRoot, "stop.request"), DateTimeOffset.UtcNow.ToString("O"));
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                process!.Refresh();
                if (process.HasExited)
                {
                    TryDelete(pidPath);
                    return "Continuous headless training stopped cleanly. The latest learning checkpoint and tester defect report were saved.";
                }
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }

            process!.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            TryDelete(pidPath);
            return "Continuous headless training was stopped after the graceful-stop timeout. The most recent periodic checkpoint and tester report remain on disk.";
        }
        catch (Exception ex)
        {
            return "I could not stop the headless trainer cleanly: " + ex.Message;
        }
    }

    private static string Status(string repositoryPath)
    {
        var root = NormalizeRepositoryRoot(repositoryPath);
        var trainingRoot = Path.Combine(root, "AI", "Training");
        var pidPath = Path.Combine(trainingRoot, "runtime", "trainer.pid");
        var statusPath = Path.Combine(trainingRoot, "runtime", "status.json");
        var running = TryReadLiveProcess(pidPath, out var process);

        if (!File.Exists(statusPath))
            return running
                ? $"Continuous headless training is running (PID {process!.Id}), but it has not written its first progress snapshot yet."
                : "Headless training has not produced a status snapshot yet.";

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(statusPath));
            var rootElement = document.RootElement;
            var mode = Read(rootElement, "mode", "unknown");
            var cycle = Read(rootElement, "cycle", "1");
            var phase = Read(rootElement, "phase", "unknown");
            var elapsed = Read(rootElement, "elapsed", "unknown");
            var phaseRemaining = Read(rootElement, "phaseRemaining", "unknown");
            var steps = Read(rootElement, "steps", "0");
            var reward = Read(rootElement, "totalReward", "0");
            var carried = Read(rootElement, "carriedGold", "unknown");
            var banked = Read(rootElement, "bankedGold", "unknown");
            var tester = rootElement.TryGetProperty("tester", out var testerElement) && testerElement.ValueKind == JsonValueKind.Object
                ? testerElement
                : default;
            var defects = tester.ValueKind == JsonValueKind.Object ? Read(tester, "uniqueDefects", "0") : "0";
            var critical = tester.ValueKind == JsonValueKind.Object ? Read(tester, "critical", "0") : "0";
            var high = tester.ValueKind == JsonValueKind.Object ? Read(tester, "high", "0") : "0";
            var parity = rootElement.TryGetProperty("parityRegression", out var parityElement) && parityElement.ValueKind == JsonValueKind.Object
                ? parityElement
                : default;
            var parityPassed = parity.ValueKind == JsonValueKind.Object ? Read(parity, "passed", "pending") : "pending";
            var parityChecked = parity.ValueKind == JsonValueKind.Object ? Read(parity, "lastCheckedUtc", "not checked") : "not checked";
            var state = running ? $"running (PID {process!.Id})" : "not running";
            return $"Headless training is {state} in {mode} mode. Cycle {cycle}; phase: {phase}; total elapsed: {elapsed}; phase remaining: {phaseRemaining}; learning steps: {steps}; total reward: {reward}. Economy: carried {carried} gold; banked {banked}. Classic parity regression gate: {parityPassed}; last checked: {parityChecked}. Tester: {defects} unique findings ({critical} critical, {high} high). Say 'Borin, what have you found broken?' for the defect report.";
        }
        catch (Exception ex)
        {
            return $"Headless training status file exists, but I could not read it: {ex.Message}";
        }
    }

    private static string DefectReport(string repositoryPath)
    {
        var root = NormalizeRepositoryRoot(repositoryPath);
        var reportPath = Path.Combine(root, "AI", "Training", "runtime", "defects.json");
        if (!File.Exists(reportPath))
            return "The training tester has not recorded a defect report yet. Start headless training and ask me again after it has exercised the game.";

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
            var report = document.RootElement;
            var summary = report.TryGetProperty("summary", out var summaryElement) ? summaryElement : default;
            var unique = summary.ValueKind == JsonValueKind.Object ? Read(summary, "uniqueDefects", "0") : "0";
            var occurrences = summary.ValueKind == JsonValueKind.Object ? Read(summary, "totalOccurrences", "0") : "0";
            var critical = summary.ValueKind == JsonValueKind.Object ? Read(summary, "critical", "0") : "0";
            var high = summary.ValueKind == JsonValueKind.Object ? Read(summary, "high", "0") : "0";
            if (!report.TryGetProperty("defects", out var defects) || defects.ValueKind != JsonValueKind.Array || defects.GetArrayLength() == 0)
                return $"The training tester has not found a reproducible defect yet. Unique findings: {unique}; occurrences: {occurrences}.";

            var rows = defects.EnumerateArray()
                .Select(element => new
                {
                    Severity = Read(element, "severity", "info"),
                    Title = Read(element, "title", "Defect"),
                    Count = Read(element, "occurrences", "1"),
                    Command = Read(element, "command", "unknown"),
                    Room = Read(element, "room", "unknown"),
                    Phase = Read(element, "phase", "unknown"),
                    Detail = Read(element, "detail", string.Empty),
                    Rank = SeverityRank(Read(element, "severity", "info"))
                })
                .OrderByDescending(row => row.Rank)
                .ThenByDescending(row => int.TryParse(row.Count, out var count) ? count : 0)
                .Take(8)
                .ToArray();

            var lines = rows.Select(row =>
            {
                var detail = row.Detail.Length > 180 ? row.Detail[..180] + "…" : row.Detail;
                return $"[{row.Severity.ToUpperInvariant()}] {row.Title} — {row.Count} occurrence(s); phase {row.Phase}; room {row.Room}; command '{row.Command}'.{(detail.Length == 0 ? string.Empty : " " + detail)}";
            });
            return $"Training tester report: {unique} unique finding(s), {occurrences} total occurrence(s), {critical} critical, {high} high. Highest-priority findings:\n" + string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            return "The training tester report exists, but I could not read it: " + ex.Message;
        }
    }

    private static int SeverityRank(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0
    };

    private static string NormalizeRepositoryRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFullPath(path.Trim().Trim('"')); }
        catch { return string.Empty; }
    }

    private static bool TryReadLiveProcess(string pidPath, out Process? process)
    {
        process = null;
        try
        {
            if (!File.Exists(pidPath) || !int.TryParse(File.ReadAllText(pidPath).Trim(), out var pid)) return false;
            var candidate = Process.GetProcessById(pid);
            if (candidate.HasExited) { TryDelete(pidPath); return false; }
            process = candidate;
            return true;
        }
        catch
        {
            TryDelete(pidPath);
            return false;
        }
    }

    private static (string FileName, string[] PrefixArguments)? ResolvePython()
    {
        var candidates = new List<(string FileName, string[] PrefixArguments)>();
        var configured = Environment.GetEnvironmentVariable("BORIN_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add((configured.Trim().Trim('"'), Array.Empty<string>()));

        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pythonPrograms = Path.Combine(local, "Programs", "Python");
            if (Directory.Exists(pythonPrograms))
            {
                foreach (var directory in Directory.EnumerateDirectories(pythonPrograms, "Python*")
                             .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    var executable = Path.Combine(directory, "python.exe");
                    if (File.Exists(executable)) candidates.Add((executable, Array.Empty<string>()));
                }
            }
        }
        catch { }

        candidates.Add(("python.exe", Array.Empty<string>()));
        candidates.Add(("python", Array.Empty<string>()));
        candidates.Add(("py.exe", new[] { "-3" }));
        candidates.Add(("py", new[] { "-3" }));

        foreach (var candidate in candidates)
        {
            if (ProbePython(candidate)) return candidate;
        }
        return null;
    }

    private static bool ProbePython((string FileName, string[] PrefixArguments) candidate)
    {
        try
        {
            var probe = new ProcessStartInfo
            {
                FileName = candidate.FileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var prefix in candidate.PrefixArguments) probe.ArgumentList.Add(prefix);
            probe.ArgumentList.Add("--version");
            using var process = Process.Start(probe);
            return process is not null && process.WaitForExit(3000) && process.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string Read(JsonElement element, string property, string fallback)
    {
        if (!element.TryGetProperty(property, out var value)) return fallback;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
