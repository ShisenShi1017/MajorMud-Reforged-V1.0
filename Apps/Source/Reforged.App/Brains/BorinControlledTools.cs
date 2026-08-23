using System.IO;
using System.Text.Json;
using Reforged.App.Forge;
using Reforged.App.Guardians;

namespace Reforged.App.Brains;

public sealed record BorinToolObservation(
    bool Succeeded,
    string Code,
    BorinAiIntent Intent,
    string Target,
    string Authority,
    string Output,
    bool ChangedState,
    DateTimeOffset CompletedAt);

public interface IBorinControlledToolExecutor
{
    Task<BorinToolObservation> ExecuteAsync(
        BorinAiDecision decision,
        BorinProviderContext requestedContext,
        BrainContext currentContext,
        CancellationToken cancellationToken = default);
}

public interface IBorinPlayerToolGateway
{
    Task ConnectAndLoginAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<BorinCommandGatewayResult> SendAutonomousCommandAsync(string command, string reason, CancellationToken cancellationToken = default);
}

public sealed class BorinPlayerToolGateway(IBorinPlayerResearch player) : IBorinPlayerToolGateway
{
    private readonly IBorinPlayerResearch _player = player ?? throw new ArgumentNullException(nameof(player));
    public Task ConnectAndLoginAsync(CancellationToken cancellationToken = default) => _player.ConnectAsync(cancellationToken);
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => _player.DisconnectAsync(cancellationToken);
    public Task<BorinCommandGatewayResult> SendAutonomousCommandAsync(string command, string reason, CancellationToken cancellationToken = default) =>
        _player.SendAutonomousCommandAsync(command, reason, cancellationToken);
}

public interface IBorinDeveloperToolGateway
{
    Task<string> SearchCodeAsync(string query, ForgeConversationState state, CancellationToken cancellationToken = default);
    Task<string> ReadFileAsync(string relativePath, ForgeConversationState state, CancellationToken cancellationToken = default);
    Task<string> ReadDirectoryAsync(string relativePath, ForgeConversationState state, CancellationToken cancellationToken = default);
    Task<string> ReadLatestLogsAsync(ForgeConversationState state, CancellationToken cancellationToken = default);
    Task<ForgeCommandResult> BuildAsync(ForgeConversationState state, CancellationToken cancellationToken = default);
    Task<ForgeCommandResult> TestAsync(ForgeConversationState state, CancellationToken cancellationToken = default);
    Task<string> PreviewPatchAsync(ForgeConversationState state, CancellationToken cancellationToken = default);
    Task<string> ApplyPatchToWorkspaceAsync(ForgeConversationState state, CancellationToken cancellationToken = default);
}

public sealed class BorinDeveloperToolGateway(
    IForgeRepositoryService repository,
    IForgeDeveloperTools developerTools,
    IBorinRepositoryReadTools readTools) : IBorinDeveloperToolGateway
{
    private readonly IForgeRepositoryService _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IForgeDeveloperTools _developerTools = developerTools ?? throw new ArgumentNullException(nameof(developerTools));
    private readonly IBorinRepositoryReadTools _readTools = readTools ?? throw new ArgumentNullException(nameof(readTools));

    public Task<string> SearchCodeAsync(string query, ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _repository.SearchAsync(RequireRoot(state), query, cancellationToken);
    public Task<string> ReadFileAsync(string relativePath, ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _readTools.ReadFileAsync(RequireRoot(state), relativePath, cancellationToken);
    public Task<string> ReadDirectoryAsync(string relativePath, ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _readTools.ReadDirectoryAsync(RequireRoot(state), relativePath, cancellationToken);
    public Task<string> ReadLatestLogsAsync(ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _repository.ReadLatestLogsAsync(RequireRoot(state), cancellationToken);
    public Task<ForgeCommandResult> BuildAsync(ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _developerTools.BuildWorkspaceAsync(state, cancellationToken);
    public Task<ForgeCommandResult> TestAsync(ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _developerTools.TestWorkspaceAsync(state, cancellationToken);
    public Task<string> PreviewPatchAsync(ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _developerTools.ShowDiffAsync(state, cancellationToken);
    public Task<string> ApplyPatchToWorkspaceAsync(ForgeConversationState state, CancellationToken cancellationToken = default) =>
        _developerTools.ApplyLatestDiffToWorkspaceAsync(state, cancellationToken);

    private static string RequireRoot(ForgeConversationState state)
    {
        var root = Path.GetFullPath(state.Configuration.RepositoryPath ?? string.Empty);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The configured repository root does not exist.");
        return root;
    }
}

public interface IBorinSysopToolGateway
{
    Task<BorinToolObservation> ExecuteAsync(BorinAiIntent intent, string? target, CancellationToken cancellationToken = default);
}

/// <summary>
/// Binds the currently available Sysop Test Realm workflow to Guardian Hall's existing
/// Worldsmith service. It cannot approve a proposal and cannot target a live realm.
/// </summary>
public sealed class BorinWorldsmithSysopToolGateway(
    IWorldsmithRuntimeService worldsmith,
    WorldsmithWorkspace workspace) : IBorinSysopToolGateway
{
    private readonly IWorldsmithRuntimeService _worldsmith = worldsmith ?? throw new ArgumentNullException(nameof(worldsmith));
    private readonly WorldsmithWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    public async Task<BorinToolObservation> ExecuteAsync(BorinAiIntent intent, string? target, CancellationToken cancellationToken = default)
    {
        EnsureSafeTarget();
        switch (intent)
        {
            case BorinAiIntent.MonitorWorld:
            case BorinAiIntent.InspectRuntime:
                var snapshot = await _worldsmith.InspectAsync(_workspace, cancellationToken).ConfigureAwait(false);
                return Result(true, "test-realm-inspected", intent, target, JsonSerializer.Serialize(snapshot), false);
            case BorinAiIntent.ProposeWorldChange:
                _workspace.Constraints = target?.Trim() ?? _workspace.Constraints;
                var proposal = await _worldsmith.GenerateProposalAsync(_workspace, cancellationToken).ConfigureAwait(false);
                _workspace.LastProposalPath = proposal;
                _workspace.ApprovedProposalPath = string.Empty;
                return Result(true, "test-realm-proposal-created", intent, target, $"Proposal created for human approval: {proposal}", true);
            case BorinAiIntent.ApplyTestWorldChange:
                var applied = await _worldsmith.ApplyApprovedProposalAsync(_workspace, cancellationToken).ConfigureAwait(false);
                return Result(true, "test-realm-staging-applied", intent, target, $"Staged: {applied.AppliedPath}\nReceipt: {applied.ReceiptPath}\nBackup: {applied.BackupPath}", true);
            default:
                return Result(false, "tool-not-bound", intent, target, "This Sysop capability has no controlled Worldsmith adapter yet.", false);
        }
    }

    private void EnsureSafeTarget()
    {
        var target = _workspace.TargetTestRealm ?? string.Empty;
        if (!target.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
            !target.Contains("Development", StringComparison.OrdinalIgnoreCase) &&
            !target.Contains("Offline", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Borin Sysop tools are restricted to Test, Development, or Offline Projection realms.");
        if (!_workspace.ApprovalRequired)
            throw new InvalidOperationException("Worldsmith approval must remain required.");
    }

    private static BorinToolObservation Result(bool success, string code, BorinAiIntent intent, string? target, string output, bool changed) =>
        new(success, code, intent, target ?? string.Empty, "Guardian Hall Worldsmith Test Realm service", AiSecretRedactor.Redact(output), changed, DateTimeOffset.Now);
}

public interface IBorinRepositoryReadTools
{
    Task<string> ReadFileAsync(string repositoryRoot, string relativePath, CancellationToken cancellationToken = default);
    Task<string> ReadDirectoryAsync(string repositoryRoot, string relativePath, CancellationToken cancellationToken = default);
}

/// <summary>Bounded, read-only repository access. Absolute paths and reparse-point traversal are rejected.</summary>
public sealed class BorinRepositoryReadTools : IBorinRepositoryReadTools
{
    private const int MaximumFileCharacters = 64_000;
    private const int MaximumDirectoryEntries = 250;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".csproj", ".sln", ".json", ".md", ".txt", ".log", ".xml", ".yaml", ".yml", ".props", ".targets", ".patch", ".diff"
    };

    public async Task<string> ReadFileAsync(string repositoryRoot, string relativePath, CancellationToken cancellationToken = default)
    {
        var path = ResolveContainedPath(repositoryRoot, relativePath, requireExists: true);
        if (!File.Exists(path)) throw new FileNotFoundException("The requested repository file was not found.");
        if (!AllowedExtensions.Contains(Path.GetExtension(path))) throw new InvalidOperationException("The requested file type is not approved for AI text reading.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[MaximumFileCharacters + 1];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        var text = new string(buffer, 0, Math.Min(count, MaximumFileCharacters));
        return count > MaximumFileCharacters ? text + "\n[file truncated]" : text;
    }

    public Task<string> ReadDirectoryAsync(string repositoryRoot, string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveContainedPath(repositoryRoot, relativePath, requireExists: true);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("The requested repository directory was not found.");
        var root = Path.GetFullPath(repositoryRoot);
        var entries = Directory.EnumerateFileSystemEntries(path)
            .Where(entry => !Path.GetFileName(entry).Equals(".git", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumDirectoryEntries + 1)
            .Select(entry => (Directory.Exists(entry) ? "[dir] " : "[file] ") + Path.GetRelativePath(root, entry))
            .ToArray();
        var truncated = entries.Length > MaximumDirectoryEntries;
        var output = string.Join(Environment.NewLine, entries.Take(MaximumDirectoryEntries));
        return Task.FromResult(truncated ? output + Environment.NewLine + "[directory listing truncated]" : output);
    }

    private static string ResolveContainedPath(string repositoryRoot, string relativePath, bool requireExists)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot)) throw new DirectoryNotFoundException("The repository root does not exist.");
        if (string.IsNullOrWhiteSpace(relativePath)) relativePath = ".";
        if (Path.IsPathRooted(relativePath)) throw new InvalidOperationException("Absolute paths are not allowed.");
        var root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!resolved.Equals(root, StringComparison.OrdinalIgnoreCase) && !resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The requested path escapes the repository root.");
        if (requireExists && !File.Exists(resolved) && !Directory.Exists(resolved)) throw new FileNotFoundException("The requested repository path was not found.");
        RejectReparseTraversal(root, resolved);
        return resolved;
    }

    private static void RejectReparseTraversal(string root, string resolved)
    {
        var relative = Path.GetRelativePath(root, resolved);
        var current = root;
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Repository reads cannot traverse symbolic links or reparse points.");
        }
    }
}

public sealed class BorinControlledToolExecutor(
    IBorinActionValidator validator,
    IBorinPlayerToolGateway? player = null,
    IBorinDeveloperToolGateway? developer = null,
    IBorinSysopToolGateway? sysop = null) : IBorinControlledToolExecutor
{
    private const int MaximumObservationCharacters = 30_000;
    private readonly IBorinActionValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    public async Task<BorinToolObservation> ExecuteAsync(BorinAiDecision decision, BorinProviderContext requestedContext, BrainContext currentContext, CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(decision, requestedContext, currentContext);
        if (!validation.Allowed) return Observation(false, validation.Code, decision, "BorinActionValidator", validation.Reason, false);
        try
        {
            if (IsPlayerBound(decision.Intent))
            {
                if (player is null) return Unbound(decision, "Player gateway is not registered.");
                return await ExecutePlayerAsync(player, decision, cancellationToken).ConfigureAwait(false);
            }
            if (IsDeveloperBound(decision.Intent))
            {
                if (developer is null) return Unbound(decision, "Developer gateway is not registered.");
                return await ExecuteDeveloperAsync(developer, decision, currentContext.Conversation.State, cancellationToken).ConfigureAwait(false);
            }
            if (IsSysopIntent(decision.Intent))
            {
                if (sysop is null) return Unbound(decision, "A controlled Sysop/Editor gateway is not registered for this capability.");
                return Normalize(await sysop.ExecuteAsync(decision.Intent, decision.Target, cancellationToken).ConfigureAwait(false));
            }
            return Unbound(decision, "This validated capability does not yet have a controlled local adapter.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return Observation(false, "tool-failed", decision, "controlled local gateway", exception.Message, false);
        }
    }

    private static async Task<BorinToolObservation> ExecutePlayerAsync(IBorinPlayerToolGateway gateway, BorinAiDecision decision, CancellationToken token)
    {
        if (decision.Intent == BorinAiIntent.ConnectAndLogin)
        {
            await gateway.ConnectAndLoginAsync(token).ConfigureAwait(false);
            return Observation(true, "connect-requested", decision, "IBorinPlayerResearch", "The canonical Borin session received the connect/login request.", true);
        }
        if (decision.Intent == BorinAiIntent.Disconnect)
        {
            await gateway.DisconnectAsync(token).ConfigureAwait(false);
            return Observation(true, "disconnect-requested", decision, "IBorinPlayerResearch", "The canonical Borin session received the disconnect request.", true);
        }
        var command = decision.Intent switch
        {
            BorinAiIntent.LookRoom => "LOOK",
            BorinAiIntent.Move => NormalizeDirection(decision.Target),
            BorinAiIntent.Inventory => "INVENTORY",
            BorinAiIntent.Equipment => "EQUIPMENT",
            BorinAiIntent.Attack => "ATTACK " + decision.Target!.Trim(),
            BorinAiIntent.Cast => "CAST " + decision.Target!.Trim(),
            BorinAiIntent.GameCommand => decision.Target!.Trim(),
            _ => string.Empty
        };
        if (command.Length == 0) return Unbound(decision, "The player intent requires a richer typed argument contract before command translation.");
        var result = await gateway.SendAutonomousCommandAsync(command, $"Validated autonomous AI action: {decision.Reason}", token).ConfigureAwait(false);
        return Observation(result.Allowed && result.Sent, result.Allowed ? "player-command-result" : "player-command-blocked", decision,
            "IBorinPlayerResearch autonomous command gateway", result.Summary, result.Sent);
    }

    private static async Task<BorinToolObservation> ExecuteDeveloperAsync(IBorinDeveloperToolGateway gateway, BorinAiDecision decision, ForgeConversationState state, CancellationToken token)
    {
        string output;
        var changed = false;
        switch (decision.Intent)
        {
            case BorinAiIntent.SearchCode:
            case BorinAiIntent.SearchReferences: output = await gateway.SearchCodeAsync(decision.Target!, state, token).ConfigureAwait(false); break;
            case BorinAiIntent.ReadFile: output = await gateway.ReadFileAsync(decision.Target!, state, token).ConfigureAwait(false); break;
            case BorinAiIntent.ReadDirectory: output = await gateway.ReadDirectoryAsync(decision.Target!, state, token).ConfigureAwait(false); break;
            case BorinAiIntent.ReadBuildErrors:
            case BorinAiIntent.ReadRuntimeLogs:
            case BorinAiIntent.ReadCrashLogs:
            case BorinAiIntent.ReadTestResults: output = await gateway.ReadLatestLogsAsync(state, token).ConfigureAwait(false); break;
            case BorinAiIntent.Build:
                var build = await gateway.BuildAsync(state, token).ConfigureAwait(false); output = $"exit={build.ExitCode}\n{build.Output}"; break;
            case BorinAiIntent.Test:
                var test = await gateway.TestAsync(state, token).ConfigureAwait(false); output = $"exit={test.ExitCode}\n{test.Output}"; break;
            case BorinAiIntent.PreviewPatch: output = await gateway.PreviewPatchAsync(state, token).ConfigureAwait(false); break;
            case BorinAiIntent.ApplyPatch: output = await gateway.ApplyPatchToWorkspaceAsync(state, token).ConfigureAwait(false); changed = true; break;
            default: return Unbound(decision, "The developer intent requires a dedicated controlled adapter or richer typed arguments.");
        }
        return Observation(true, "tool-completed", decision, "Borin developer gateway", output, changed);
    }

    private static bool IsPlayerBound(BorinAiIntent intent) => intent is BorinAiIntent.ConnectAndLogin or BorinAiIntent.Disconnect or BorinAiIntent.LookRoom or BorinAiIntent.Move or BorinAiIntent.Inventory or BorinAiIntent.Equipment or BorinAiIntent.Attack or BorinAiIntent.Cast or BorinAiIntent.Interact or BorinAiIntent.Shop or BorinAiIntent.Quest or BorinAiIntent.Pathfind or BorinAiIntent.GameCommand;
    private static bool IsDeveloperBound(BorinAiIntent intent) => intent is BorinAiIntent.SearchCode or BorinAiIntent.SearchReferences or BorinAiIntent.ReadFile or BorinAiIntent.ReadDirectory or BorinAiIntent.ReadBuildErrors or BorinAiIntent.ReadRuntimeLogs or BorinAiIntent.ReadCrashLogs or BorinAiIntent.ReadTestResults or BorinAiIntent.Build or BorinAiIntent.Test or BorinAiIntent.PreviewPatch or BorinAiIntent.ApplyPatch;
    private static bool IsSysopIntent(BorinAiIntent intent) => intent is BorinAiIntent.MonitorWorld or BorinAiIntent.InspectRoom or BorinAiIntent.InspectExit or BorinAiIntent.InspectMonster or BorinAiIntent.InspectItem or BorinAiIntent.InspectScript or BorinAiIntent.InspectTrigger or BorinAiIntent.InspectRuntime or BorinAiIntent.ReportIssue or BorinAiIntent.ProposeWorldChange or BorinAiIntent.ApplyTestWorldChange or BorinAiIntent.ResetTestRealm;

    private static string NormalizeDirection(string? target) => target?.Trim().ToUpperInvariant() switch
    {
        "N" or "NORTH" => "NORTH", "S" or "SOUTH" => "SOUTH", "E" or "EAST" => "EAST", "W" or "WEST" => "WEST",
        "U" or "UP" => "UP", "D" or "DOWN" => "DOWN", "NE" or "NORTHEAST" => "NORTHEAST", "NW" or "NORTHWEST" => "NORTHWEST",
        "SE" or "SOUTHEAST" => "SOUTHEAST", "SW" or "SOUTHWEST" => "SOUTHWEST", _ => string.Empty
    };

    private static BorinToolObservation Unbound(BorinAiDecision decision, string reason) => Observation(false, "tool-not-bound", decision, "controlled tool registry", reason, false);
    private static BorinToolObservation Normalize(BorinToolObservation observation)
    {
        var safe = AiSecretRedactor.Redact(observation.Output);
        if (safe.Length > MaximumObservationCharacters) safe = safe[..MaximumObservationCharacters] + "\n[observation truncated]";
        return observation with { Output = safe };
    }
    private static BorinToolObservation Observation(bool success, string code, BorinAiDecision decision, string authority, string output, bool changed)
    {
        var safe = AiSecretRedactor.Redact(output ?? string.Empty);
        if (safe.Length > MaximumObservationCharacters) safe = safe[..MaximumObservationCharacters] + "\n[observation truncated]";
        return new(success, code, decision.Intent, decision.Target ?? string.Empty, authority, safe, changed, DateTimeOffset.Now);
    }
}
