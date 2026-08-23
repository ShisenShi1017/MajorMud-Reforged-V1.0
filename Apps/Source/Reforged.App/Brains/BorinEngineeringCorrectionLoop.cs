using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

public static class BorinEngineeringCorrectionOutcomes
{
    public const string Applied = "Applied";
    public const string BuildFailed = "BuildFailed";
    public const string ValidationFailed = "ValidationFailed";
    public const string Passed = "Passed";
    public const string Rejected = "Rejected";
    public const string Stopped = "Stopped";
}

public sealed record BorinEngineeringCorrectionAttempt(
    int Number,
    string JobId,
    string DiagnosticSummary,
    IReadOnlyList<string> Files,
    string Outcome,
    string ResultSummary,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt);

public sealed record BorinEngineeringCorrectionStatus(
    string JobId,
    int AttemptCount,
    int MaximumAttempts,
    bool IsStopped,
    string Summary,
    IReadOnlyList<BorinEngineeringCorrectionAttempt> Attempts)
{
    public bool CanAttempt => !IsStopped && AttemptCount < MaximumAttempts;
    public BorinEngineeringCorrectionAttempt? Latest => Attempts.LastOrDefault();

    public static BorinEngineeringCorrectionStatus Empty(string jobId, int maximumAttempts = 3) =>
        new(jobId, 0, maximumAttempts, false,
            "No build-correction attempts have been recorded for this engineering job.",
            Array.Empty<BorinEngineeringCorrectionAttempt>());
}

public sealed record BorinEngineeringCorrectionApplyResult(
    bool Succeeded,
    string Summary,
    string Output,
    BorinEngineeringCorrectionStatus Status);

public interface IBorinEngineeringCorrectionLoop
{
    Task<BorinEngineeringCorrectionStatus> GetStatusAsync(
        string repositoryRoot,
        string jobId,
        CancellationToken token = default);

    Task<BorinEngineeringCorrectionApplyResult> ApplyCorrectionAsync(
        string repositoryRoot,
        BorinEngineeringJobSnapshot job,
        string diagnostics,
        string correctionReply,
        CancellationToken token = default);

    Task<BorinEngineeringCorrectionStatus> CompleteLatestAsync(
        string repositoryRoot,
        string jobId,
        bool buildPassed,
        bool validationPassed,
        string summary,
        CancellationToken token = default);

    Task<BorinEngineeringCorrectionStatus> StopAsync(
        string repositoryRoot,
        string jobId,
        string reason,
        CancellationToken token = default);
}

/// <summary>
/// Persists bounded correction attempts and applies model-generated correction patches
/// only to Borin's isolated workspace. Patch paths must remain within the exact file
/// set approved on the engineering job. This service cannot write to the main source
/// tree, install packages, commit, or push.
/// </summary>
public sealed class BorinEngineeringCorrectionLoop : IBorinEngineeringCorrectionLoop
{
    private sealed class CorrectionLedger
    {
        public List<JobCorrectionRecord> Jobs { get; set; } = [];
    }

    private sealed class JobCorrectionRecord
    {
        public string JobId { get; set; } = string.Empty;
        public int MaximumAttempts { get; set; } = 3;
        public bool IsStopped { get; set; }
        public string StopReason { get; set; } = string.Empty;
        public List<CorrectionAttemptRecord> Attempts { get; set; } = [];
    }

    private sealed class CorrectionAttemptRecord
    {
        public int Number { get; set; }
        public string DiagnosticSummary { get; set; } = string.Empty;
        public List<string> Files { get; set; } = [];
        public string Outcome { get; set; } = BorinEngineeringCorrectionOutcomes.Applied;
        public string ResultSummary { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const int DefaultMaximumAttempts = 3;
    private const int MaximumCorrectionBytes = 1024 * 1024;
    private const int MaximumDiagnosticCharacters = 30000;
    private readonly IBorinEngineeringWorkspace _workspace;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BorinEngineeringCorrectionLoop(IBorinEngineeringWorkspace workspace)
    {
        _workspace = workspace;
    }

    public async Task<BorinEngineeringCorrectionStatus> GetStatusAsync(
        string repositoryRoot,
        string jobId,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        jobId = ValidateJobId(jobId);
        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = ledger.Jobs.LastOrDefault(item => item.JobId.Equals(jobId, StringComparison.OrdinalIgnoreCase));
            return record is null ? BorinEngineeringCorrectionStatus.Empty(jobId) : ToStatus(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinEngineeringCorrectionApplyResult> ApplyCorrectionAsync(
        string repositoryRoot,
        BorinEngineeringJobSnapshot job,
        string diagnostics,
        string correctionReply,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        if (!job.Exists)
            return new(false, "No engineering job is active.", string.Empty, BorinEngineeringCorrectionStatus.Empty(string.Empty));
        if (job.ProposedFiles.Count == 0)
            return new(false, "The engineering job has no approved file scope.", string.Empty, BorinEngineeringCorrectionStatus.Empty(job.Id));

        var patch = ExtractUnifiedDiff(correctionReply);
        if (string.IsNullOrWhiteSpace(patch))
            return new(false, "The correction response did not contain one fenced unified diff.", string.Empty, await GetStatusAsync(repositoryRoot, job.Id, token));
        if (Encoding.UTF8.GetByteCount(patch) > MaximumCorrectionBytes)
            return new(false, "The correction patch exceeds the 1 MB safety limit.", string.Empty, await GetStatusAsync(repositoryRoot, job.Id, token));

        var correctionFiles = ParsePatchPaths(patch);
        if (correctionFiles.Count == 0)
            return new(false, "The correction patch did not contain a valid repository-relative file path.", string.Empty, await GetStatusAsync(repositoryRoot, job.Id, token));

        var approved = new HashSet<string>(job.ProposedFiles.Select(NormalizeRelativePath), StringComparer.OrdinalIgnoreCase);
        var outsideScope = correctionFiles.Where(path => !approved.Contains(path)).ToArray();
        if (outsideScope.Length > 0)
        {
            var status = await RecordRejectedAsync(
                repositoryRoot,
                job.Id,
                diagnostics,
                correctionFiles,
                "Correction exceeded the approved file scope: " + string.Join(", ", outsideScope),
                token);
            return new(false, status.Summary, string.Join(Environment.NewLine, outsideScope), status);
        }

        await _gate.WaitAsync(token);
        BorinEngineeringCorrectionStatus before;
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = GetOrCreate(ledger, job.Id);
            before = ToStatus(record);
            if (record.IsStopped)
                return new(false, "The correction loop is stopped for this job. Start a new engineering job to continue.", string.Empty, before);
            if (record.Attempts.Count >= record.MaximumAttempts)
                return new(false, $"The correction retry limit of {record.MaximumAttempts} has been reached.", string.Empty, before);
        }
        finally
        {
            _gate.Release();
        }

        var operation = await _workspace.ApplyDraftAsync(repositoryRoot, correctionReply, token);

        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = GetOrCreate(ledger, job.Id);
            var now = DateTimeOffset.Now;
            var attempt = new CorrectionAttemptRecord
            {
                Number = record.Attempts.Count + 1,
                DiagnosticSummary = TrimDiagnostic(diagnostics),
                Files = correctionFiles.ToList(),
                Outcome = operation.Succeeded
                    ? BorinEngineeringCorrectionOutcomes.Applied
                    : BorinEngineeringCorrectionOutcomes.Rejected,
                ResultSummary = operation.Summary,
                StartedAt = now,
                UpdatedAt = now
            };
            record.Attempts.Add(attempt);
            await SaveLedgerAsync(repositoryRoot, ledger, token);
            var status = ToStatus(record);
            return new(operation.Succeeded, operation.Summary, operation.Output, status);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinEngineeringCorrectionStatus> CompleteLatestAsync(
        string repositoryRoot,
        string jobId,
        bool buildPassed,
        bool validationPassed,
        string summary,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        jobId = ValidateJobId(jobId);
        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = GetOrCreate(ledger, jobId);
            var latest = record.Attempts.LastOrDefault();
            if (latest is null)
                return ToStatus(record);

            latest.Outcome = buildPassed
                ? validationPassed
                    ? BorinEngineeringCorrectionOutcomes.Passed
                    : BorinEngineeringCorrectionOutcomes.ValidationFailed
                : BorinEngineeringCorrectionOutcomes.BuildFailed;
            latest.ResultSummary = TrimDiagnostic(summary);
            latest.UpdatedAt = DateTimeOffset.Now;
            await SaveLedgerAsync(repositoryRoot, ledger, token);
            return ToStatus(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinEngineeringCorrectionStatus> StopAsync(
        string repositoryRoot,
        string jobId,
        string reason,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        jobId = ValidateJobId(jobId);
        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = GetOrCreate(ledger, jobId);
            record.IsStopped = true;
            record.StopReason = string.IsNullOrWhiteSpace(reason) ? "Correction loop stopped by the user." : reason.Trim();
            var latest = record.Attempts.LastOrDefault();
            if (latest is not null && latest.Outcome == BorinEngineeringCorrectionOutcomes.Applied)
            {
                latest.Outcome = BorinEngineeringCorrectionOutcomes.Stopped;
                latest.ResultSummary = record.StopReason;
                latest.UpdatedAt = DateTimeOffset.Now;
            }
            await SaveLedgerAsync(repositoryRoot, ledger, token);
            return ToStatus(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BorinEngineeringCorrectionStatus> RecordRejectedAsync(
        string repositoryRoot,
        string jobId,
        string diagnostics,
        IReadOnlyList<string> files,
        string reason,
        CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = GetOrCreate(ledger, jobId);
            var now = DateTimeOffset.Now;
            record.Attempts.Add(new CorrectionAttemptRecord
            {
                Number = record.Attempts.Count + 1,
                DiagnosticSummary = TrimDiagnostic(diagnostics),
                Files = files.ToList(),
                Outcome = BorinEngineeringCorrectionOutcomes.Rejected,
                ResultSummary = reason,
                StartedAt = now,
                UpdatedAt = now
            });
            await SaveLedgerAsync(repositoryRoot, ledger, token);
            return ToStatus(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static JobCorrectionRecord GetOrCreate(CorrectionLedger ledger, string jobId)
    {
        var record = ledger.Jobs.LastOrDefault(item => item.JobId.Equals(jobId, StringComparison.OrdinalIgnoreCase));
        if (record is not null)
            return record;
        record = new JobCorrectionRecord { JobId = jobId, MaximumAttempts = DefaultMaximumAttempts };
        ledger.Jobs.Add(record);
        while (ledger.Jobs.Count > 100)
            ledger.Jobs.RemoveAt(0);
        return record;
    }

    private static BorinEngineeringCorrectionStatus ToStatus(JobCorrectionRecord record)
    {
        var attempts = record.Attempts.Select(item => new BorinEngineeringCorrectionAttempt(
            item.Number,
            record.JobId,
            item.DiagnosticSummary,
            item.Files.ToArray(),
            item.Outcome,
            item.ResultSummary,
            item.StartedAt,
            item.UpdatedAt)).ToArray();
        string summary;
        if (record.IsStopped)
            summary = $"Correction loop stopped after {attempts.Length}/{record.MaximumAttempts} attempt(s): {record.StopReason}";
        else if (attempts.Length == 0)
            summary = "No build-correction attempts have been recorded for this engineering job.";
        else
            summary = $"Correction attempts: {attempts.Length}/{record.MaximumAttempts} • latest {attempts[^1].Outcome} • {attempts[^1].ResultSummary}";
        return new(record.JobId, attempts.Length, record.MaximumAttempts, record.IsStopped, summary, attempts);
    }

    private static string ExtractUnifiedDiff(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var match = Regex.Match(text, "```(?:diff|patch)\\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static IReadOnlyList<string> ParsePatchPaths(string patch)
    {
        if (patch.Contains("GIT binary patch", StringComparison.OrdinalIgnoreCase) ||
            patch.Contains("Binary files", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(patch, "^\\+\\+\\+\\s+/dev/null\\s*$", RegexOptions.Multiline))
        {
            throw new InvalidOperationException("Binary changes and file deletions are outside the correction-loop safety boundary.");
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(patch, "^diff --git a/(.+?) b/(.+?)\\s*$", RegexOptions.Multiline))
            paths.Add(ValidateRelativePath(match.Groups[2].Value.Trim().Trim('"')));
        if (paths.Count == 0)
        {
            foreach (Match match in Regex.Matches(patch, "^\\+\\+\\+\\s+(?:b/)?(.+?)\\s*$", RegexOptions.Multiline))
            {
                var value = match.Groups[1].Value.Trim().Trim('"');
                if (!value.Equals("/dev/null", StringComparison.OrdinalIgnoreCase))
                    paths.Add(ValidateRelativePath(value));
            }
        }
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ValidateRelativePath(string value)
    {
        var normalized = NormalizeRelativePath(value);
        if (normalized.Length == 0 || Path.IsPathRooted(normalized) ||
            normalized.Equals("..", StringComparison.Ordinal) ||
            normalized.StartsWith("../", StringComparison.Ordinal) ||
            normalized.Contains("/../", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsafe correction path: " + value);
        }
        return normalized;
    }

    private static string NormalizeRelativePath(string value) =>
        value.Replace('\\', '/').TrimStart('/').Trim();

    private static string TrimDiagnostic(string value)
    {
        value = value.Trim();
        return value.Length <= MaximumDiagnosticCharacters ? value : value[^MaximumDiagnosticCharacters..];
    }

    private static string ValidateJobId(string jobId)
    {
        jobId = jobId.Trim();
        if (jobId.Length == 0)
            throw new InvalidOperationException("No engineering job ID was provided.");
        if (jobId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("The engineering job ID is invalid.");
        return jobId;
    }

    private static string ValidateRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new InvalidOperationException("The Reforged repository path is empty.");
        var full = Path.GetFullPath(repositoryRoot.Trim());
        var appProject = Path.Combine(full, "Apps", "Source", "Reforged.App", "Reforged.App.csproj");
        var coreProject = Path.Combine(full, "Core", "Source", "Reforged.Core", "Reforged.Core.csproj");
        if (!File.Exists(appProject) || !File.Exists(coreProject))
            throw new DirectoryNotFoundException("The selected folder is not the MajorMUD Reforged repository: " + full);
        return full;
    }

    private static string ResolveLedgerPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Workspaces", "Borin", "Corrections", "correction-ledger.json");

    private static async Task<CorrectionLedger> LoadLedgerAsync(string repositoryRoot, CancellationToken token)
    {
        var path = ResolveLedgerPath(repositoryRoot);
        if (!File.Exists(path))
            return new CorrectionLedger();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<CorrectionLedger>(stream, JsonOptions, token) ?? new CorrectionLedger();
        }
        catch (JsonException)
        {
            return new CorrectionLedger();
        }
    }

    private static async Task SaveLedgerAsync(string repositoryRoot, CorrectionLedger ledger, CancellationToken token)
    {
        var path = ResolveLedgerPath(repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, ledger, JsonOptions, token);
        File.Move(temporary, path, true);
    }
}
