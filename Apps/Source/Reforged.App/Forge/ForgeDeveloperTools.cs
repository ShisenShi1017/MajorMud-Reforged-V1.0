using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Reforged.App.Forge;

public interface IForgeDeveloperTools
{
    Task<string> InspectToolsAsync(string rootPath, CancellationToken token = default);
    Task<ForgeWorkspaceStatus> CreateWorkspaceAsync(ForgeConversationState state, CancellationToken token = default);
    Task<ForgeWorkspaceStatus> GetWorkspaceStatusAsync(ForgeConversationState state, CancellationToken token = default);
    Task<string> ApplyLatestDiffToWorkspaceAsync(ForgeConversationState state, CancellationToken token = default);
    Task<ForgeCommandResult> BuildWorkspaceAsync(ForgeConversationState state, CancellationToken token = default);
    Task<ForgeCommandResult> TestWorkspaceAsync(ForgeConversationState state, CancellationToken token = default);
    Task<string> ShowDiffAsync(ForgeConversationState state, CancellationToken token = default);
    Task<string> ApplyWorkspaceToMainAsync(ForgeConversationState state, CancellationToken token = default);
    Task<string> RollbackLastApplyAsync(ForgeConversationState state, CancellationToken token = default);
    Task<string> OpenSolutionAsync(ForgeConversationState state, CancellationToken token = default);
}

public sealed class ForgeDeveloperTools : IForgeDeveloperTools
{
    public async Task<string> InspectToolsAsync(string rootPath, CancellationToken token = default)
    {
        var names = new[] { "dotnet", "git", "pwsh", "powershell", "devenv", "code" };
        var lines = new List<string>();
        foreach (var name in names)
        {
            var result = await RunAsync(rootPath, OperatingSystem.IsWindows() ? "where" : "which", name, token);
            lines.Add($"{name}: {(result.Succeeded ? result.Output.Trim() : "not found")}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    public async Task<ForgeWorkspaceStatus> CreateWorkspaceAsync(ForgeConversationState state, CancellationToken token = default)
    {
        var root = state.Configuration.RepositoryPath;
        EnsureRoot(root);
        if (!state.Configuration.AllowGitCommands || !state.Configuration.AllowWorkspaceEdits)
            return new ForgeWorkspaceStatus("", false, false, 0, "Git/workspace permissions are disabled.");

        var repositoryParent = Directory.GetParent(root)?.FullName ?? root;
        var parent = Path.Combine(repositoryParent, ".ReforgedForgeWorkspaces", new DirectoryInfo(root).Name);
        Directory.CreateDirectory(parent);
        var path = Path.Combine(parent, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var result = await RunAsync(root, "git", $"worktree add --detach \"{path}\" HEAD", token);
        if (!result.Succeeded)
            return new ForgeWorkspaceStatus(path, false, false, 0, "Could not create Git worktree. " + result.Output);
        state.ActiveWorkspacePath = path;
        return await GetWorkspaceStatusAsync(state, token);
    }

    public async Task<ForgeWorkspaceStatus> GetWorkspaceStatusAsync(ForgeConversationState state, CancellationToken token = default)
    {
        var path = state.ActiveWorkspacePath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return new ForgeWorkspaceStatus(path, false, false, 0, "No active Forge workspace.");
        var status = await RunAsync(path, "git", "status --short", token);
        var count = status.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return new ForgeWorkspaceStatus(path, true, status.Succeeded, count, status.Succeeded ? status.Output : "Workspace exists, but Git status failed: " + status.Output);
    }

    public async Task<string> ApplyLatestDiffToWorkspaceAsync(ForgeConversationState state, CancellationToken token = default)
    {
        var workspace = state.ActiveWorkspacePath;
        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace)) return "No active workspace. Create one first.";
        if (!state.Configuration.AllowWorkspaceEdits) return "Workspace editing permission is disabled.";
        var text = state.Messages.LastOrDefault(m => m.Role != "You" && m.Text.Contains("```diff", StringComparison.OrdinalIgnoreCase))?.Text;
        if (string.IsNullOrWhiteSpace(text)) return "I found no fenced ```diff patch in the recent Borin response.";
        var match = Regex.Match(text, "```diff\\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!match.Success) return "The proposed diff could not be parsed.";
        var patchDir = Path.Combine(workspace, ".forge");
        Directory.CreateDirectory(patchDir);
        var patch = Path.Combine(patchDir, "latest-proposal.patch");
        await File.WriteAllTextAsync(patch, match.Groups[1].Value.Trim() + Environment.NewLine, token);
        var check = await RunAsync(workspace, "git", $"apply --check --whitespace=nowarn \"{patch}\"", token);
        if (!check.Succeeded) return "Patch validation failed; nothing changed.\n" + check.Output;
        var apply = await RunAsync(workspace, "git", $"apply --whitespace=nowarn \"{patch}\"", token);
        return apply.Succeeded ? "Aye, the proposed diff was applied only inside the Forge workspace. Review and test it before main-repository approval." : "Patch apply failed.\n" + apply.Output;
    }

    public Task<ForgeCommandResult> BuildWorkspaceAsync(ForgeConversationState state, CancellationToken token = default) =>
        RunAsync(Workspace(state), "dotnet", "build -c Release", token);

    public Task<ForgeCommandResult> TestWorkspaceAsync(ForgeConversationState state, CancellationToken token = default) =>
        RunAsync(Workspace(state), "dotnet", "test -c Release --no-restore", token);

    public async Task<string> ShowDiffAsync(ForgeConversationState state, CancellationToken token = default)
    {
        var workspace = Workspace(state);
        await RunAsync(workspace, "git", "add -N .", token);
        var stat = await RunAsync(workspace, "git", "diff --stat", token);
        var full = await RunAsync(workspace, "git", "diff --no-ext-diff", token);
        var output = stat.Output + Environment.NewLine + full.Output;
        return output.Length > 30000 ? output[^30000..] : output;
    }

    public async Task<string> ApplyWorkspaceToMainAsync(ForgeConversationState state, CancellationToken token = default)
    {
        if (state.Configuration.PermissionMode != ForgePermissionMode.ApplyWithApproval) return "Apply permission is not enabled.";
        var root = state.Configuration.RepositoryPath;
        var workspace = Workspace(state);
        var patchDir = Path.Combine(root, ".forge", "approved-patches");
        Directory.CreateDirectory(patchDir);
        var patch = Path.Combine(patchDir, $"forge-{DateTime.Now:yyyyMMdd-HHmmss}.patch");
        await RunAsync(workspace, "git", "add -N .", token);
        var diff = await RunAsync(workspace, "git", "diff --binary", token);
        if (!diff.Succeeded || string.IsNullOrWhiteSpace(diff.Output)) return "There are no workspace changes to apply.";
        await File.WriteAllTextAsync(patch, diff.Output, token);
        var check = await RunAsync(root, "git", $"apply --check \"{patch}\"", token);
        if (!check.Succeeded) return "Main-repository validation failed; nothing was applied.\n" + check.Output;
        var apply = await RunAsync(root, "git", $"apply \"{patch}\"", token);
        if (!apply.Succeeded) return "Main-repository apply failed.\n" + apply.Output;
        state.LastAppliedPatchPath = patch;
        return $"Approved workspace patch applied to the main repository. Rollback remains available from {patch}. No commit or push was performed.";
    }

    public async Task<string> RollbackLastApplyAsync(ForgeConversationState state, CancellationToken token = default)
    {
        var patch = state.LastAppliedPatchPath;
        if (string.IsNullOrWhiteSpace(patch) || !File.Exists(patch)) return "No applied Forge patch is available to roll back.";
        var root = state.Configuration.RepositoryPath;
        var check = await RunAsync(root, "git", $"apply --reverse --check \"{patch}\"", token);
        if (!check.Succeeded) return "Rollback validation failed; nothing changed.\n" + check.Output;
        var result = await RunAsync(root, "git", $"apply --reverse \"{patch}\"", token);
        if (result.Succeeded) state.LastAppliedPatchPath = string.Empty;
        return result.Succeeded ? "Aye, the last Forge patch was rolled back from the main repository." : result.Output;
    }

    public Task<string> OpenSolutionAsync(ForgeConversationState state, CancellationToken token = default)
    {
        if (!state.Configuration.AllowLocalProgramLaunch)
            return Task.FromResult("Local program launching is disabled.");

        var root = state.Configuration.RepositoryPath;
        var sln = Directory.EnumerateFiles(root, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln is null)
            return Task.FromResult("No solution file was found at the repository root.");

        try
        {
            Process.Start(new ProcessStartInfo { FileName = sln, UseShellExecute = true, WorkingDirectory = root });
            return Task.FromResult("The solution was opened with its registered Windows development program.");
        }
        catch (Exception ex)
        {
            return Task.FromResult("Could not open the solution: " + ex.Message);
        }
    }

    private static string Workspace(ForgeConversationState state)
    {
        if (string.IsNullOrWhiteSpace(state.ActiveWorkspacePath) || !Directory.Exists(state.ActiveWorkspacePath))
            throw new InvalidOperationException("No active Forge workspace. Create one first.");
        return state.ActiveWorkspacePath;
    }

    private static void EnsureRoot(string root) { if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root); }

    private static async Task<ForgeCommandResult> RunAsync(string root, string file, string args, CancellationToken token)
    {
        var output = new StringBuilder();
        using var process = new Process { StartInfo = new ProcessStartInfo
        {
            FileName = file, Arguments = args, WorkingDirectory = root,
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        }};
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        try
        {
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            await process.WaitForExitAsync(token);
            return new ForgeCommandResult($"{file} {args}", process.ExitCode, output.ToString(), DateTimeOffset.Now);
        }
        catch (Exception ex) { return new ForgeCommandResult($"{file} {args}", -1, ex.Message, DateTimeOffset.Now); }
    }
}
