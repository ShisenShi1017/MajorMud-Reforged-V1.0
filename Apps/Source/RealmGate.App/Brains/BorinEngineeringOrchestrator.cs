using System.IO;
using System.Text;
using System.Text.Json;

namespace RealmGate.App.Brains;

public static class BorinEngineeringRequestRouter
{
    public static bool IsContextualRepairFollowUp(string? request)
    {
        var value = StripAddress(request?.Trim() ?? string.Empty);
        return System.Text.RegularExpressions.Regex.IsMatch(value,
                   @"\b(?:find|investigate|diagnose|propose|create|make|fix|repair)\b.*\b(?:fix|patch|repair|solution|it|that|this|the issue|the problem)\b",
                   System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant) &&
               System.Text.RegularExpressions.Regex.IsMatch(value, @"\b(?:it|that|this|the issue|the problem)\b",
                   System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static readonly string[] StartPrefixes =
    [
        "fix ", "repair ", "resolve ", "correct ", "implement ",
        "investigate and fix ", "investigate and repair ", "investigate why ",
        "investigate the ", "diagnose and fix ", "diagnose why ",
        "find and fix ", "create a patch for ", "propose a patch for "
    ];

    public static bool TryGetCommission(string? request, out string commission)
    {
        commission = StripAddress(request?.Trim() ?? string.Empty);
        if (commission.Length == 0)
            return false;
        var normalized = commission.ToLowerInvariant();
        return StartPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
    }

    public static bool TryGetContextualCommission(string? request, string? previousUserRequest, out string commission)
    {
        commission = string.Empty;
        var followUp = StripAddress(request?.Trim() ?? string.Empty);
        var prior = previousUserRequest?.Trim() ?? string.Empty;
        if (followUp.Length == 0 || prior.Length == 0) return false;
        var asksRepair = IsContextualRepairFollowUp(followUp);
        var refersBack = System.Text.RegularExpressions.Regex.IsMatch(followUp, @"\b(?:it|that|this|the issue|the problem)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var priorIsDiagnostic = System.Text.RegularExpressions.Regex.IsMatch(prior,
            @"\b(?:why|broken|bug|error|exception|unavailable|incorrect|wrong|does not|doesn't|failed|failure)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!asksRepair || !refersBack || !priorIsDiagnostic) return false;
        commission = $"{followUp}\n\nPREVIOUSLY DESCRIBED ISSUE (authoritative conversation context):\n{prior}";
        return true;
    }

    public static bool TryGetContextualCommission(string? request, IEnumerable<string> recentConversation, out string commission)
    {
        commission = string.Empty;
        foreach (var candidate in recentConversation.Where(value => !string.IsNullOrWhiteSpace(value)).Take(16))
        {
            if (TryGetContextualCommission(request, candidate, out commission)) return true;
            var diagnosticFinding = System.Text.RegularExpressions.Regex.IsMatch(candidate,
                @"\b(?:root cause|missing|malformed|evidence-promoted|progression data|no evidence|failed lookup|cannot calculate|can't calculate)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!diagnosticFinding) continue;
            var syntheticPrior = "Diagnostic finding: " + candidate;
            if (TryGetContextualCommission(request, syntheticPrior + " error", out commission)) return true;
        }
        return false;
    }

    private static string StripAddress(string value)
    {
        foreach (var prefix in new[] { "borin,", "borin:", "borin " })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value[prefix.Length..].TrimStart();
        }
        return value;
    }
}

public static class BorinEngineeringJobStages
{
    public const string None = "None";
    public const string Investigating = "Investigating";
    public const string Drafted = "Drafted";
    public const string AwaitingApproval = "AwaitingApproval";
    public const string AppliedToWorkspace = "AppliedToWorkspace";
    public const string Built = "Built";
    public const string Validated = "Validated";
    public const string Packaged = "Packaged";
    public const string Incorporated = "Incorporated";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";
}

public sealed record BorinEngineeringJobEvent(string Stage, string Summary, DateTimeOffset At);

public sealed record BorinEngineeringJobSnapshot(
    string Id,
    string Request,
    string Stage,
    string Summary,
    string DraftText,
    IReadOnlyList<string> ProposedFiles,
    string BuildSummary,
    string ValidationSummary,
    string ArtifactPath,
    IReadOnlyList<BorinEngineeringJobEvent> History,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool Exists => Id.Length > 0;
    public bool AwaitingApproval => Stage == BorinEngineeringJobStages.AwaitingApproval;
    public bool CanBuildAndTest => Stage is BorinEngineeringJobStages.AppliedToWorkspace or BorinEngineeringJobStages.Built or BorinEngineeringJobStages.Validated or BorinEngineeringJobStages.Failed;
    public bool CanPackage => Stage == BorinEngineeringJobStages.Validated;
    public bool CanIncorporate => Stage is BorinEngineeringJobStages.Validated or BorinEngineeringJobStages.Packaged;

    public static BorinEngineeringJobSnapshot None(string repositoryRoot) => new(
        string.Empty,
        string.Empty,
        BorinEngineeringJobStages.None,
        "No autonomous engineering job is active. Say `Fix ...` or use `/job start <request>`.",
        string.Empty,
        Array.Empty<string>(),
        string.Empty,
        string.Empty,
        string.Empty,
        Array.Empty<BorinEngineeringJobEvent>(),
        DateTimeOffset.MinValue,
        DateTimeOffset.MinValue);
}

public sealed record BorinEngineeringJobActionResult(
    bool Succeeded,
    BorinEngineeringJobSnapshot Job,
    string Summary,
    string Output = "",
    string ArtifactPath = "");

public interface IBorinEngineeringOrchestrator
{
    Task<BorinEngineeringJobSnapshot> GetCurrentAsync(string repositoryRoot, CancellationToken token = default);
    Task<IReadOnlyList<BorinEngineeringJobSnapshot>> ListAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringJobSnapshot> BeginAsync(string repositoryRoot, string request, CancellationToken token = default);
    Task<BorinEngineeringJobSnapshot> RecordDraftAsync(string repositoryRoot, string draftText, CancellationToken token = default);
    Task<BorinEngineeringJobActionResult> ShowChangesAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringJobActionResult> ApproveAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringJobActionResult> RejectAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringJobActionResult> BuildAndTestAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringJobActionResult> PackageAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringJobActionResult> IncorporateAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringJobActionResult> FailAsync(string repositoryRoot, string reason, CancellationToken token = default);
}

/// <summary>
/// Tracks one approval-gated engineering commission at a time and delegates all
/// source-changing operations to BorinEngineeringWorkspace. The orchestrator may
/// write job metadata only under AI/Workspaces/Borin/Jobs. It has no operation that
/// writes a draft into the main repository, runs an installer, commits, or pushes.
/// </summary>
public sealed class BorinEngineeringOrchestrator : IBorinEngineeringOrchestrator
{
    private sealed class JobLedger
    {
        public string CurrentJobId { get; set; } = string.Empty;
        public List<JobRecord> Jobs { get; set; } = [];
    }

    private sealed class JobEventRecord
    {
        public string Stage { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTimeOffset At { get; set; }
    }

    private sealed class JobRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Request { get; set; } = string.Empty;
        public string Stage { get; set; } = BorinEngineeringJobStages.None;
        public string Summary { get; set; } = string.Empty;
        public string DraftText { get; set; } = string.Empty;
        public List<string> ProposedFiles { get; set; } = [];
        public string BuildSummary { get; set; } = string.Empty;
        public string ValidationSummary { get; set; } = string.Empty;
        public string ArtifactPath { get; set; } = string.Empty;
        public List<JobEventRecord> History { get; set; } = [];
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IBorinEngineeringWorkspace _workspace;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BorinEngineeringOrchestrator(IBorinEngineeringWorkspace workspace)
    {
        _workspace = workspace;
    }

    public async Task<BorinEngineeringJobSnapshot> GetCurrentAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var current = ledger.Jobs.LastOrDefault(job => job.Id == ledger.CurrentJobId)
                ?? ledger.Jobs.OrderByDescending(job => job.UpdatedAt).FirstOrDefault();
            return current is null ? BorinEngineeringJobSnapshot.None(repositoryRoot) : ToSnapshot(current);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<BorinEngineeringJobSnapshot>> ListAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            return ledger.Jobs
                .OrderByDescending(job => job.UpdatedAt)
                .Take(25)
                .Select(ToSnapshot)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinEngineeringJobSnapshot> BeginAsync(
        string repositoryRoot,
        string request,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        request = request.Trim();
        if (request.Length == 0)
            throw new ArgumentException("An engineering request is required.", nameof(request));
        if (request.Length > 8000)
            throw new InvalidOperationException("The engineering request exceeds the 8,000 character safety limit.");

        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var now = DateTimeOffset.Now;
            var record = new JobRecord
            {
                Id = $"JOB-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..31],
                Request = request,
                Stage = BorinEngineeringJobStages.Investigating,
                Summary = "Borin is investigating the request with read-only evidence. No source files have changed.",
                CreatedAt = now,
                UpdatedAt = now,
                History =
                [
                    new JobEventRecord
                    {
                        Stage = BorinEngineeringJobStages.Investigating,
                        Summary = "Read-only investigation started.",
                        At = now
                    }
                ]
            };
            ledger.Jobs.Add(record);
            while (ledger.Jobs.Count > 100)
                ledger.Jobs.RemoveAt(0);
            ledger.CurrentJobId = record.Id;
            await SaveLedgerAsync(repositoryRoot, ledger, token);
            return ToSnapshot(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinEngineeringJobSnapshot> RecordDraftAsync(
        string repositoryRoot,
        string draftText,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        draftText = draftText.Trim();
        if (!draftText.Contains("```diff", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The proposed answer did not contain a fenced unified diff.");

        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = RequireCurrent(ledger);
            var now = DateTimeOffset.Now;
            record.Stage = BorinEngineeringJobStages.Drafted;
            record.DraftText = draftText.Length > 250000 ? draftText[..250000] : draftText;
            record.ProposedFiles = ExtractProposedFiles(record.DraftText).Take(50).ToList();
            record.History.Add(new JobEventRecord
            {
                Stage = BorinEngineeringJobStages.Drafted,
                Summary = $"Unified draft recorded across {record.ProposedFiles.Count} proposed file(s).",
                At = now
            });
            record.Stage = BorinEngineeringJobStages.AwaitingApproval;
            record.Summary = $"Draft ready across {record.ProposedFiles.Count} proposed file(s). It may be applied automatically only to the isolated workspace; main-repository incorporation remains human-held.";
            record.History.Add(new JobEventRecord
            {
                Stage = BorinEngineeringJobStages.AwaitingApproval,
                Summary = "Draft queued at the legacy workspace-apply gate. This gate authorizes only the isolated workspace, never the main repository.",
                At = now
            });
            record.UpdatedAt = now;
            await SaveLedgerAsync(repositoryRoot, ledger, token);
            return ToSnapshot(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinEngineeringJobActionResult> ShowChangesAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var current = await GetCurrentAsync(repositoryRoot, token);
        if (!current.Exists)
            return new(false, current, current.Summary);

        if (current.Stage is BorinEngineeringJobStages.Investigating or BorinEngineeringJobStages.Drafted or BorinEngineeringJobStages.AwaitingApproval)
        {
            var preview = current.DraftText.Length > 60000 ? current.DraftText[..60000] + "\n\n[Draft preview truncated.]" : current.DraftText;
            return new(
                current.DraftText.Length > 0,
                current,
                current.DraftText.Length > 0 ? "Showing the proposed draft. It has not been applied anywhere." : "No draft is ready yet.",
                preview);
        }

        var operation = await _workspace.ShowDiffAsync(repositoryRoot, token);
        return new(operation.Succeeded, current, operation.Summary, operation.Output, operation.ArtifactPath);
    }

    public async Task<BorinEngineeringJobActionResult> ApproveAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var current = await GetCurrentAsync(repositoryRoot, token);
        if (!current.Exists)
            return new(false, current, current.Summary);
        if (!current.AwaitingApproval)
            return new(false, current, $"Job {current.Id} is at {current.Stage}; approval is only accepted while AwaitingApproval.");

        var operation = await _workspace.ApplyDraftAsync(repositoryRoot, current.DraftText, token);
        var updated = await UpdateCurrentAsync(repositoryRoot, record =>
        {
            record.Stage = operation.Succeeded ? BorinEngineeringJobStages.AppliedToWorkspace : BorinEngineeringJobStages.Failed;
            record.Summary = operation.Succeeded
                ? "Draft applied only inside the isolated workspace. The main repository remains locked."
                : "Draft application failed safely inside the isolated workspace. The main repository was untouched.";
        }, token);
        return new(operation.Succeeded, updated, operation.Summary, operation.Output, operation.ArtifactPath);
    }

    public async Task<BorinEngineeringJobActionResult> RejectAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var current = await GetCurrentAsync(repositoryRoot, token);
        if (!current.Exists)
            return new(false, current, current.Summary);
        if (current.Stage is BorinEngineeringJobStages.Packaged or BorinEngineeringJobStages.Rejected)
            return new(false, current, $"Job {current.Id} is already {current.Stage}.");

        var updated = await UpdateCurrentAsync(repositoryRoot, record =>
        {
            record.Stage = BorinEngineeringJobStages.Rejected;
            record.Summary = "Draft rejected. No source changes were applied to the main repository.";
        }, token);
        return new(true, updated, updated.Summary);
    }

    public async Task<BorinEngineeringJobActionResult> BuildAndTestAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var current = await GetCurrentAsync(repositoryRoot, token);
        if (!current.Exists)
            return new(false, current, current.Summary);
        if (!current.CanBuildAndTest)
            return new(false, current, $"Job {current.Id} is at {current.Stage}. Approve and apply the draft to the isolated workspace first, or use the correction loop after a recorded build failure.");

        var build = await _workspace.BuildAsync(repositoryRoot, token);
        if (!build.Succeeded)
        {
            var failed = await UpdateCurrentAsync(repositoryRoot, record =>
            {
                record.Stage = BorinEngineeringJobStages.Failed;
                record.BuildSummary = build.Summary;
                record.Summary = "The isolated workspace build failed. Main repository remains untouched.";
            }, token);
            return new(false, failed, build.Summary, build.Output);
        }

        var built = await UpdateCurrentAsync(repositoryRoot, record =>
        {
            record.Stage = BorinEngineeringJobStages.Built;
            record.BuildSummary = build.Summary;
            record.Summary = "The isolated workspace built successfully. Validation is running next.";
        }, token);

        var tests = await _workspace.TestAsync(repositoryRoot, token);
        var validated = await UpdateCurrentAsync(repositoryRoot, record =>
        {
            record.Stage = tests.Succeeded ? BorinEngineeringJobStages.Validated : BorinEngineeringJobStages.Failed;
            record.ValidationSummary = tests.Summary;
            record.Summary = tests.Succeeded
                ? "The isolated workspace built and validated successfully. It is eligible for review-only packaging."
                : "The isolated workspace validation failed. Main repository remains untouched.";
        }, token);

        var output = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(build.Output))
            output.AppendLine("BUILD").AppendLine(build.Output.Trim());
        if (!string.IsNullOrWhiteSpace(tests.Output))
            output.AppendLine().AppendLine("VALIDATION").AppendLine(tests.Output.Trim());
        return new(tests.Succeeded, validated, tests.Succeeded ? "Build and validation passed in the isolated workspace." : tests.Summary, Trim(output.ToString()));
    }

    public async Task<BorinEngineeringJobActionResult> PackageAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var current = await GetCurrentAsync(repositoryRoot, token);
        if (!current.Exists)
            return new(false, current, current.Summary);
        if (!current.CanPackage)
            return new(false, current, $"Job {current.Id} is at {current.Stage}. A package requires a successful isolated build and validation first.");

        var operation = await _workspace.CreateUpdateZipAsync(repositoryRoot, token);
        var updated = await UpdateCurrentAsync(repositoryRoot, record =>
        {
            record.Stage = operation.Succeeded ? BorinEngineeringJobStages.Packaged : BorinEngineeringJobStages.Failed;
            record.ArtifactPath = operation.ArtifactPath;
            record.Summary = operation.Succeeded
                ? "A review-only update ZIP was created. It was not installed or applied to the main repository."
                : "Update ZIP packaging failed safely. Main repository remains untouched.";
        }, token);
        return new(operation.Succeeded, updated, operation.Summary, operation.Output, operation.ArtifactPath);
    }

    public async Task<BorinEngineeringJobActionResult> IncorporateAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var current = await GetCurrentAsync(repositoryRoot, token);
        if (!current.Exists)
            return new(false, current, current.Summary);
        if (!current.CanIncorporate)
            return new(false, current, $"Job {current.Id} is at {current.Stage}. Main incorporation requires a verified isolated build and tests.");

        var operation = await _workspace.IncorporateVerifiedAsync(repositoryRoot, current.ProposedFiles, token);
        var updated = await UpdateCurrentAsync(repositoryRoot, record =>
        {
            if (operation.Succeeded)
            {
                record.Stage = BorinEngineeringJobStages.Incorporated;
                record.ArtifactPath = operation.ArtifactPath;
            }
            record.Summary = operation.Succeeded
                ? "William explicitly incorporated the verified isolated patch into the main repository. It was not committed, pushed, published, installed, or deployed."
                : "Main-repository incorporation was blocked safely; no target file was overwritten.";
        }, token);
        return new(operation.Succeeded, updated, operation.Summary, operation.Output, operation.ArtifactPath);
    }

    public async Task<BorinEngineeringJobActionResult> FailAsync(
        string repositoryRoot,
        string reason,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var current = await GetCurrentAsync(repositoryRoot, token);
        if (!current.Exists)
            return new(false, current, reason);
        var updated = await UpdateCurrentAsync(repositoryRoot, record =>
        {
            record.Stage = BorinEngineeringJobStages.Failed;
            record.Summary = reason;
        }, token);
        return new(false, updated, reason);
    }

    private async Task<BorinEngineeringJobSnapshot> UpdateCurrentAsync(
        string repositoryRoot,
        Action<JobRecord> update,
        CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var ledger = await LoadLedgerAsync(repositoryRoot, token);
            var record = RequireCurrent(ledger);
            var previousStage = record.Stage;
            var previousSummary = record.Summary;
            update(record);
            record.UpdatedAt = DateTimeOffset.Now;
            record.History ??= [];
            if (!string.Equals(previousStage, record.Stage, StringComparison.Ordinal) ||
                !string.Equals(previousSummary, record.Summary, StringComparison.Ordinal))
            {
                record.History.Add(new JobEventRecord
                {
                    Stage = record.Stage,
                    Summary = record.Summary,
                    At = record.UpdatedAt
                });
            }
            await SaveLedgerAsync(repositoryRoot, ledger, token);
            return ToSnapshot(record);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static JobRecord RequireCurrent(JobLedger ledger)
    {
        return ledger.Jobs.LastOrDefault(job => job.Id == ledger.CurrentJobId)
            ?? throw new InvalidOperationException("No autonomous engineering job is active.");
    }

    private static BorinEngineeringJobSnapshot ToSnapshot(JobRecord record) => new(
        record.Id,
        record.Request,
        record.Stage,
        record.Summary,
        record.DraftText,
        record.ProposedFiles.ToArray(),
        record.BuildSummary,
        record.ValidationSummary,
        record.ArtifactPath,
        (record.History ?? []).Select(item => new BorinEngineeringJobEvent(item.Stage, item.Summary, item.At)).ToArray(),
        record.CreatedAt,
        record.UpdatedAt);

    private static IReadOnlyList<string> ExtractProposedFiles(string draftText)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in draftText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!rawLine.StartsWith("+++ b/", StringComparison.Ordinal) && !rawLine.StartsWith("--- a/", StringComparison.Ordinal))
                continue;
            var value = rawLine[6..].Trim().Replace('\\', '/');
            if (value.Length > 0 && value != "/dev/null")
                paths.Add(value);
        }
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ValidateRepositoryRoot(string repositoryRoot)
    {
        var full = Path.GetFullPath(repositoryRoot.Trim().Trim('"'));
        var appProject = Path.Combine(full, "Apps", "Source", "RealmGate.App", "RealmGate.App.csproj");
        var coreProject = Path.Combine(full, "Core", "Source", "RealmGate.Core", "RealmGate.Core.csproj");
        if (!File.Exists(appProject) || !File.Exists(coreProject))
            throw new DirectoryNotFoundException("The configured repository root does not contain the RealmGate app and core projects.");
        return full;
    }

    private static string ResolveLedgerPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Workspaces", "Borin", "Jobs", "engineering-jobs.json");

    private static async Task<JobLedger> LoadLedgerAsync(string repositoryRoot, CancellationToken token)
    {
        var path = ResolveLedgerPath(repositoryRoot);
        if (!File.Exists(path))
            return new JobLedger();
        try
        {
            await using var stream = File.OpenRead(path);
            var ledger = await JsonSerializer.DeserializeAsync<JobLedger>(stream, JsonOptions, token) ?? new JobLedger();
            ledger.Jobs ??= [];
            foreach (var job in ledger.Jobs)
                job.History ??= [];
            return ledger;
        }
        catch (JsonException)
        {
            var corrupt = path + ".corrupt-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(path, corrupt, overwrite: true);
            return new JobLedger();
        }
    }

    private static async Task SaveLedgerAsync(string repositoryRoot, JobLedger ledger, CancellationToken token)
    {
        var path = ResolveLedgerPath(repositoryRoot);
        var directory = Path.GetDirectoryName(path) ?? repositoryRoot;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, ledger, JsonOptions, token);
        File.Move(temp, path, overwrite: true);
    }

    private static string Trim(string value) => value.Length > 60000 ? value[^60000..] : value;
}
