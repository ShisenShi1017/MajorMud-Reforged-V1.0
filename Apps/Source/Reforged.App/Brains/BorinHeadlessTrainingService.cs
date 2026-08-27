using System.Diagnostics;
using System.Text.Json;

namespace Reforged.App.Brains;

/// <summary>
/// Chat-driven launcher for Borin's unattended MajorMUD training curriculum.
/// There is intentionally no UI button: Forge phrases start/status/stop the trainer.
/// </summary>
public sealed class BorinHeadlessTrainingService
{
    public bool IsTrainingRequest(string request)
    {
        if (string.IsNullOrWhiteSpace(request)) return false;
        var text = request.Trim().ToLowerInvariant();
        return text.Contains("headless training", StringComparison.Ordinal)
            || text.Contains("headless trainer", StringComparison.Ordinal)
            || text.Contains("train headless", StringComparison.Ordinal);
    }

    public Task<string> HandleAsync(string repositoryPath, string request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = request.Trim().ToLowerInvariant();
        if (text.Contains("stop", StringComparison.Ordinal) || text.Contains("cancel", StringComparison.Ordinal))
            return Task.FromResult(Stop(repositoryPath));
        if (text.Contains("status", StringComparison.Ordinal) || text.Contains("progress", StringComparison.Ordinal) || text.Contains("how is", StringComparison.Ordinal))
            return Task.FromResult(Status(repositoryPath));
        return Task.FromResult(Start(repositoryPath));
    }

    private static string Start(string repositoryPath)
    {
        var root = NormalizeRepositoryRoot(repositoryPath);
        if (root.Length == 0 || !Directory.Exists(root))
            return "I cannot start headless training because the Reforged repository path is not valid.";

        var trainingRoot = Path.Combine(root, "AI", "Training");
        var script = Path.Combine(trainingRoot, "borin_headless_trainer.py");
        if (!File.Exists(script))
            return $"I cannot start headless training because the trainer script is missing: {script}";

        var pidPath = Path.Combine(trainingRoot, "runtime", "trainer.pid");
        if (TryReadLiveProcess(pidPath, out var existing))
            return $"Headless training is already running (PID {existing!.Id}). Say 'Borin, headless training status' for progress.";

        Directory.CreateDirectory(Path.Combine(trainingRoot, "runtime"));
        var python = ResolvePython();
        if (python is null)
            return "I cannot start headless training because Python was not found on PATH. Install Python 3, then tell me to start headless training again.";

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
        start.ArgumentList.Add("--hours");
        start.ArgumentList.Add("22");
        start.ArgumentList.Add("--player");
        start.ArgumentList.Add("Borin");

        try
        {
            var process = Process.Start(start);
            if (process is null) return "The headless trainer process could not be started.";
            File.WriteAllText(pidPath, process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return "Headless training started. I will run the 22-hour curriculum without opening the game UI. In the economy phase I will learn buying, selling, and equipment choices; I will bank carried money above 10 gold, and if I fall below 10 gold I must earn/find more instead of withdrawing it from the bank.";
        }
        catch (Exception ex)
        {
            return "Headless training could not start: " + ex.Message;
        }
    }

    private static string Stop(string repositoryPath)
    {
        var root = NormalizeRepositoryRoot(repositoryPath);
        var pidPath = Path.Combine(root, "AI", "Training", "runtime", "trainer.pid");
        if (!TryReadLiveProcess(pidPath, out var process))
            return "Headless training is not currently running.";

        try
        {
            process!.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            TryDelete(pidPath);
            return "Headless training stopped. The latest checkpoint and learned economy/equipment data were left on disk so the next run can continue.";
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
                ? $"Headless training is running (PID {process!.Id}), but it has not written its first progress snapshot yet."
                : "Headless training has not produced a status snapshot yet.";

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(statusPath));
            var rootElement = document.RootElement;
            var phase = Read(rootElement, "phase", "unknown");
            var elapsed = Read(rootElement, "elapsed", "unknown");
            var remaining = Read(rootElement, "remaining", "unknown");
            var steps = Read(rootElement, "steps", "0");
            var reward = Read(rootElement, "totalReward", "0");
            var carried = Read(rootElement, "carriedGold", "unknown");
            var banked = Read(rootElement, "bankedGold", "unknown");
            var state = running ? $"running (PID {process!.Id})" : "not running";
            return $"Headless training is {state}. Phase: {phase}. Elapsed: {elapsed}; remaining: {remaining}; steps: {steps}; total reward: {reward}. Economy snapshot: carried {carried} gold, banked {banked} gold.";
        }
        catch (Exception ex)
        {
            return $"Headless training status file exists, but I could not read it: {ex.Message}";
        }
    }

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
        foreach (var candidate in new[]
        {
            (FileName: "python.exe", PrefixArguments: Array.Empty<string>()),
            (FileName: "python", PrefixArguments: Array.Empty<string>()),
            (FileName: "py.exe", PrefixArguments: new[] { "-3" }),
            (FileName: "py", PrefixArguments: new[] { "-3" })
        })
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
                if (process is null) continue;
                if (process.WaitForExit(3000) && process.ExitCode == 0) return candidate;
            }
            catch { }
        }
        return null;
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
