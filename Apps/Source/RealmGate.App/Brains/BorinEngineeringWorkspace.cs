using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealmGate.App.Brains;

public interface IBorinEngineeringWorkspace
{
    Task<BorinEngineeringWorkspaceStatus> CreateAsync(string repositoryRoot, bool recreate = false, CancellationToken token = default);
    Task<BorinEngineeringWorkspaceStatus> GetStatusAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringOperationResult> ApplyDraftAsync(string repositoryRoot, string borinReply, CancellationToken token = default);
    Task<BorinEngineeringOperationResult> ShowDiffAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringOperationResult> BuildAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringOperationResult> TestAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringOperationResult> CreateUpdateZipAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinEngineeringOperationResult> IncorporateVerifiedAsync(string repositoryRoot, IReadOnlyList<string> verifiedPaths, CancellationToken token = default);
}

public sealed record BorinEngineeringWorkspaceStatus(
    string State,
    string WorkspacePath,
    bool Exists,
    int ChangedFileCount,
    string Summary)
{
    public static BorinEngineeringWorkspaceStatus NotCreated(string workspacePath) =>
        new("NotCreated", workspacePath, false, 0,
            "No controlled engineering workspace exists. The main repository remains locked.");
}

public sealed record BorinEngineeringOperationResult(
    bool Succeeded,
    string Summary,
    string Output = "",
    string ArtifactPath = "");

public static class BorinPatchProposalValidator
{
    public static bool TryValidateCompleteness(string? patchText, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(patchText)) { error = "The proposed patch is empty."; return false; }
        if (Regex.IsMatch(patchText, @"/\*\s*(?:logic|todo|implementation)[^*]*\*/|<!--\s*\.\.\.\s*-->|^\s*[+-]?\s*//\s*\.\.\.\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        { error = "The proposed patch contains placeholder pseudo-code instead of a complete implementation."; return false; }
        foreach (Match header in Regex.Matches(patchText, @"^(?:---|\+\+\+)\s+(?:a/|b/)?(.+?)\s*$", RegexOptions.Multiline))
        {
            var path = header.Groups[1].Value.Trim().Trim('"');
            if (path == "/dev/null") continue;
            if (path.Any(character => character > 127))
            { error = "The proposed patch contains a non-ASCII path that is not an exact repository path."; return false; }
        }
        return true;
    }
}

/// <summary>
/// Borin's Stage 6 engineering bridge. It may copy the repository into a private
/// snapshot, apply a reviewed unified diff inside that snapshot, build/test only
/// inside that snapshot, and create a reviewable update ZIP. It has no method that
/// copies source changes back into the main repository or runs an update installer.
/// </summary>
public sealed class BorinEngineeringWorkspace : IBorinEngineeringWorkspace
{
    private static readonly HashSet<string> AllowedDraftExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".csproj", ".sln", ".props", ".targets",
        ".json", ".xml", ".yml", ".yaml", ".md", ".txt",
        ".ps1", ".bat", ".cmd", ".sql", ".resx", ".config"
    };

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "Backups", "Reports", "Release", "Build",
        "Archive", ".ReforgedBorinWorkspaces", ".vs", ".idea", ".agents",
        ".codex", ".codex-remote-attachments"
    };

    private static readonly string[] ForbiddenRelativePrefixes =
    {
        ".git/", "AI/Models/", "AI/Memory/", "AI/Workspaces/", "Backups/",
        "Reports/", "Release/", "Build/", ".github/workflows/"
    };

    private const int MaximumDraftBytes = 2 * 1024 * 1024;
    private const int MaximumDraftFiles = 50;
    private const int MaximumDisplayedOutput = 60000;

    public async Task<BorinEngineeringWorkspaceStatus> CreateAsync(
        string repositoryRoot,
        bool recreate = false,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var workspacePath = ResolveWorkspacePath(repositoryRoot);

        if (recreate && Directory.Exists(workspacePath))
            await Task.Run(() => DeleteWorkspaceSafely(repositoryRoot, workspacePath), token);

        if (Directory.Exists(workspacePath) && Directory.Exists(Path.Combine(workspacePath, ".git")))
        {
            var baseline = await RunProcessAsync(workspacePath, "git", ["rev-parse", "--verify", "HEAD"], token);
            if (baseline.Succeeded)
                return await GetStatusAsync(repositoryRoot, token);
            await Task.Run(() => DeleteWorkspaceSafely(repositoryRoot, workspacePath), token);
        }

        if (Directory.Exists(workspacePath))
            await Task.Run(() => DeleteWorkspaceSafely(repositoryRoot, workspacePath), token);

        Directory.CreateDirectory(workspacePath);
        await Task.Run(() => CopyRepositorySnapshot(repositoryRoot, workspacePath, token), token);

        var init = await RunProcessAsync(workspacePath, "git", ["init"], token);
        if (!init.Succeeded)
            return new BorinEngineeringWorkspaceStatus("Faulted", workspacePath, true, 0,
                "The private snapshot was copied, but its local Git baseline could not be initialized. " + init.Output);

        await RunProcessAsync(workspacePath, "git", ["config", "user.name", "Borin Controlled Workspace"], token);
        await RunProcessAsync(workspacePath, "git", ["config", "user.email", "borin@local.invalid"], token);
        await RunProcessAsync(workspacePath, "git", ["config", "core.autocrlf", "false"], token);
        await RunProcessAsync(workspacePath, "git", ["config", "core.safecrlf", "false"], token);
        await RunProcessAsync(workspacePath, "git", ["config", "core.longpaths", "true"], token);

        var add = await RunProcessAsync(workspacePath, "git", ["add", "-A"], token);
        if (!add.Succeeded)
            return new BorinEngineeringWorkspaceStatus("Faulted", workspacePath, true, 0,
                "The private snapshot exists, but its baseline could not be staged. " + SummarizeGitFailure(add.Output));

        var commit = await RunProcessAsync(
            workspacePath,
            "git",
            ["commit", "--no-gpg-sign", "-m", "Borin controlled workspace baseline"],
            token);
        if (!commit.Succeeded)
            return new BorinEngineeringWorkspaceStatus("Faulted", workspacePath, true, 0,
                "The private snapshot exists, but its baseline commit failed. " + SummarizeGitFailure(commit.Output));

        return new BorinEngineeringWorkspaceStatus(
            "Ready",
            workspacePath,
            true,
            0,
            "Controlled workspace ready. It is a private snapshot of the current local repository; the main repository remains locked.");
    }

    public async Task<BorinEngineeringWorkspaceStatus> GetStatusAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var workspacePath = ResolveWorkspacePath(repositoryRoot);
        if (!Directory.Exists(workspacePath) || !Directory.Exists(Path.Combine(workspacePath, ".git")))
            return BorinEngineeringWorkspaceStatus.NotCreated(workspacePath);

        var baseline = await RunProcessAsync(workspacePath, "git", ["rev-parse", "--verify", "HEAD"], token);
        if (!baseline.Succeeded)
            return new BorinEngineeringWorkspaceStatus(
                "InvalidBaseline", workspacePath, true, 0,
                "The controlled workspace has no valid baseline and will be rebuilt before applying a patch.");

        var status = await RunProcessAsync(
            workspacePath,
            "git",
            ["status", "--short", "--untracked-files=all"],
            token);
        if (!status.Succeeded)
            return new BorinEngineeringWorkspaceStatus(
                "Faulted", workspacePath, true, 0, "Workspace exists, but status failed: " + status.Output);

        var count = status.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Length;
        return new BorinEngineeringWorkspaceStatus(
            count == 0 ? "Ready" : "DraftChanged",
            workspacePath,
            true,
            count,
            count == 0
                ? "Workspace matches its private baseline. Main repository remains locked."
                : $"Workspace contains {count} changed file(s). Review the diff, build, and test before packaging.");
    }

    public async Task<BorinEngineeringOperationResult> ApplyDraftAsync(
        string repositoryRoot,
        string borinReply,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var workspace = await EnsureWorkspaceAsync(repositoryRoot, token);
        if (!workspace.Exists || workspace.State == "Faulted")
            return new BorinEngineeringOperationResult(false, workspace.Summary);

        var patchText = ExtractUnifiedDiff(borinReply);
        if (string.IsNullOrWhiteSpace(patchText))
            return new BorinEngineeringOperationResult(false,
                "No fenced unified diff was found in Borin's latest reply. Use `/draft <request>` first.");
        if (Encoding.UTF8.GetByteCount(patchText) > MaximumDraftBytes)
            return new BorinEngineeringOperationResult(false, "The proposed patch exceeds the 2 MB Stage 6 safety limit.");

        var paths = ParseAndValidatePatchPaths(patchText);
        if (paths.Count == 0)
            return new BorinEngineeringOperationResult(false, "The proposed patch contains no valid repository-relative file paths.");
        if (paths.Count > MaximumDraftFiles)
            return new BorinEngineeringOperationResult(false, $"The proposed patch touches {paths.Count} files; the Stage 6 limit is {MaximumDraftFiles}.");

        var draftDirectory = Path.Combine(workspace.WorkspacePath, ".borin", "drafts");
        Directory.CreateDirectory(draftDirectory);
        var patchPath = Path.Combine(draftDirectory, "latest-proposal.patch");
        await File.WriteAllTextAsync(patchPath, patchText.TrimEnd() + Environment.NewLine, new UTF8Encoding(false), token);

        var check = await RunProcessAsync(
            workspace.WorkspacePath,
            "git",
            ["apply", "--check", "--whitespace=nowarn", patchPath],
            token);
        if (!check.Succeeded)
            return new BorinEngineeringOperationResult(false,
                "Patch validation failed. Nothing changed in the workspace.", TrimOutput(check.Output));

        var apply = await RunProcessAsync(
            workspace.WorkspacePath,
            "git",
            ["apply", "--whitespace=nowarn", patchPath],
            token);
        if (!apply.Succeeded)
            return new BorinEngineeringOperationResult(false,
                "Patch application failed inside the workspace. The main repository was untouched.", TrimOutput(apply.Output));

        return new BorinEngineeringOperationResult(
            true,
            $"Draft applied only inside the controlled workspace across {paths.Count} file(s). The main repository was not changed.",
            string.Join(Environment.NewLine, paths.Select(path => "- " + path)));
    }

    public async Task<BorinEngineeringOperationResult> ShowDiffAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var workspace = await EnsureWorkspaceAsync(repositoryRoot, token);
        if (!workspace.Exists || workspace.State == "Faulted")
            return new BorinEngineeringOperationResult(false, workspace.Summary);

        await RunProcessAsync(workspace.WorkspacePath, "git", ["add", "-N", "."], token);
        var stat = await RunProcessAsync(workspace.WorkspacePath, "git", ["diff", "--stat", "--no-ext-diff"], token);
        var diff = await RunProcessAsync(workspace.WorkspacePath, "git", ["diff", "--no-ext-diff", "--no-color"], token);
        var output = (stat.Output + Environment.NewLine + diff.Output).Trim();
        if (string.IsNullOrWhiteSpace(output))
            return new BorinEngineeringOperationResult(true, "The controlled workspace has no source changes.");

        return new BorinEngineeringOperationResult(true,
            "Controlled workspace diff. No main-repository files were changed.", TrimOutput(output));
    }

    public async Task<BorinEngineeringOperationResult> BuildAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var workspace = await EnsureWorkspaceAsync(repositoryRoot, token);
        if (!workspace.Exists || workspace.State == "Faulted")
            return new BorinEngineeringOperationResult(false, workspace.Summary);

        var buildTarget = FindBuildTarget(workspace.WorkspacePath);
        if (buildTarget is null)
            return new BorinEngineeringOperationResult(false, "No solution or RealmGate.App project was found in the controlled workspace.");

        var result = await RunProcessAsync(
            workspace.WorkspacePath,
            "dotnet",
            ["build", buildTarget, "-c", "Release", "--nologo"],
            token);
        return new BorinEngineeringOperationResult(
            result.Succeeded,
            result.Succeeded
                ? "Workspace build passed. This proves only the private workspace build; nothing was installed or applied."
                : "Workspace build failed. The main repository remains untouched.",
            TrimOutput(result.Output));
    }

    public async Task<BorinEngineeringOperationResult> TestAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var workspace = await EnsureWorkspaceAsync(repositoryRoot, token);
        if (!workspace.Exists || workspace.State == "Faulted")
            return new BorinEngineeringOperationResult(false, workspace.Summary);

        var buildTarget = FindBuildTarget(workspace.WorkspacePath);
        if (buildTarget is null)
            return new BorinEngineeringOperationResult(false, "No solution or project was found in the controlled workspace.");

        var result = await RunProcessAsync(
            workspace.WorkspacePath,
            "dotnet",
            ["test", buildTarget, "-c", "Release", "--nologo", "--no-restore"],
            token);
        return new BorinEngineeringOperationResult(
            result.Succeeded,
            result.Succeeded
                ? "Workspace tests passed. This is not proof that the main repository or live app was validated."
                : "Workspace tests failed. The main repository remains untouched.",
            TrimOutput(result.Output));
    }

    public async Task<BorinEngineeringOperationResult> CreateUpdateZipAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        var workspace = await EnsureWorkspaceAsync(repositoryRoot, token);
        if (!workspace.Exists || workspace.State == "Faulted")
            return new BorinEngineeringOperationResult(false, workspace.Summary);

        var deleted = await GetGitPathsAsync(workspace.WorkspacePath, "D", token);
        if (deleted.Count > 0)
            return new BorinEngineeringOperationResult(false,
                "Stage 6 Pass 0001 will not package file deletions. Review these paths manually:\n" +
                string.Join(Environment.NewLine, deleted.Select(path => "- " + path)));

        var changed = await GetChangedAndUntrackedPathsAsync(workspace.WorkspacePath, token);
        if (changed.Count == 0)
            return new BorinEngineeringOperationResult(false, "There are no changed workspace files to package.");
        if (changed.Count > MaximumDraftFiles)
            return new BorinEngineeringOperationResult(false,
                $"The workspace contains {changed.Count} changed files; the Stage 6 package limit is {MaximumDraftFiles}.");

        foreach (var relative in changed)
            ValidateRelativeDraftPath(relative);

        var packagesRoot = Path.Combine(repositoryRoot, "AI", "Workspaces", "Borin", "Packages");
        Directory.CreateDirectory(packagesRoot);
        var name = $"RealmGate-Borin-Draft-{DateTime.Now:yyyyMMdd-HHmmss}";
        var stagingRoot = Path.Combine(Path.GetTempPath(), name + "-" + Guid.NewGuid().ToString("N"));
        var topFolder = Path.Combine(stagingRoot, name);
        Directory.CreateDirectory(topFolder);

        try
        {
            var payloadHashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relative in changed)
            {
                token.ThrowIfCancellationRequested();
                var source = CombineUnderRoot(workspace.WorkspacePath, relative);
                if (!File.Exists(source))
                    throw new FileNotFoundException("Changed workspace file was not found.", source);
                var destination = CombineUnderRoot(topFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
                payloadHashes[NormalizeRelativePath(relative)] = ComputeSha256(destination);
            }

            var readmePath = Path.Combine(topFolder, "README.txt");
            await File.WriteAllTextAsync(readmePath, BuildDraftReadme(name, changed), new UTF8Encoding(false), token);
            var ps1Path = Path.Combine(topFolder, "Install-And-Validate.ps1");
            await File.WriteAllTextAsync(ps1Path, BuildDraftInstallerPowerShell(name), new UTF8Encoding(false), token);
            var batPath = Path.Combine(topFolder, "Install-And-Validate.bat");
            await File.WriteAllTextAsync(batPath, BuildDraftInstallerBatch(name), new UTF8Encoding(false), token);

            var packageHashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(topFolder, "*", SearchOption.AllDirectories))
            {
                var relative = NormalizeRelativePath(Path.GetRelativePath(topFolder, file));
                packageHashes[relative] = ComputeSha256(file);
            }

            var manifest = new
            {
                updateId = name,
                stage = 6,
                pass = "0001-draft",
                status = "DRAFT_REQUIRES_USER_APPROVAL",
                generatedAt = DateTimeOffset.Now,
                noBackup = true,
                noRollback = true,
                payload = payloadHashes,
                files = packageHashes,
            };
            var manifestPath = Path.Combine(topFolder, "UpdateManifest.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false),
                token);

            var zipPath = Path.Combine(packagesRoot, name + ".zip");
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            ZipFile.CreateFromDirectory(stagingRoot, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            return new BorinEngineeringOperationResult(
                true,
                $"Draft update ZIP created with {changed.Count} changed file(s). It was not installed or applied.",
                string.Join(Environment.NewLine, changed.Select(path => "- " + path)),
                zipPath);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, true);
        }
    }

    public async Task<BorinEngineeringOperationResult> IncorporateVerifiedAsync(
        string repositoryRoot,
        IReadOnlyList<string> verifiedPaths,
        CancellationToken token = default)
    {
        repositoryRoot = ValidateRepositoryRoot(repositoryRoot);
        ArgumentNullException.ThrowIfNull(verifiedPaths);
        var workspace = await EnsureWorkspaceAsync(repositoryRoot, token);
        if (!workspace.Exists || workspace.State == "Faulted")
            return new(false, workspace.Summary);

        var expected = verifiedPaths.Select(ValidateRelativeDraftPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var deleted = await GetGitPathsAsync(workspace.WorkspacePath, "D", token);
        if (deleted.Count > 0)
            return new(false, "Verified incorporation does not permit file deletions.\n" + string.Join('\n', deleted));
        var changed = await GetChangedAndUntrackedPathsAsync(workspace.WorkspacePath, token);
        if (changed.Count == 0)
            return new(false, "The verified workspace contains no changes to incorporate.");
        var outsideScope = changed.Where(path => !expected.Contains(path)).ToArray();
        if (outsideScope.Length > 0)
            return new(false, "The workspace changed outside the verified job scope; incorporation was blocked.\n" + string.Join('\n', outsideScope));

        var mainStatusArguments = new List<string> { "status", "--porcelain", "--untracked-files=all", "--" };
        mainStatusArguments.AddRange(changed);
        var mainStatus = await RunProcessAsync(repositoryRoot, "git", mainStatusArguments, token);
        if (!mainStatus.Succeeded)
            return new(false, "Could not verify the main working tree before incorporation.", TrimOutput(mainStatus.Output));
        if (!string.IsNullOrWhiteSpace(mainStatus.Output))
            return new(false, "One or more verified target files already have main-repository changes. Nothing was overwritten.", TrimOutput(mainStatus.Output));

        await RunProcessAsync(workspace.WorkspacePath, "git", ["add", "-N", "."], token);
        var diff = await RunProcessAsync(workspace.WorkspacePath, "git", ["diff", "--binary", "--no-ext-diff", "HEAD", "--", "./"], token);
        if (!diff.Succeeded || string.IsNullOrWhiteSpace(diff.Output))
            return new(false, "The verified workspace diff could not be generated.", TrimOutput(diff.Output));
        if (Encoding.UTF8.GetByteCount(diff.Output) > MaximumDraftBytes)
            return new(false, "The verified patch exceeds the 2 MB incorporation limit.");

        var approvals = Path.Combine(repositoryRoot, "AI", "Workspaces", "Borin", "ApprovedPatches");
        Directory.CreateDirectory(approvals);
        var patchPath = Path.Combine(approvals, $"borin-approved-{DateTime.Now:yyyyMMdd-HHmmss}.patch");
        await File.WriteAllTextAsync(patchPath, diff.Output, new UTF8Encoding(false), token);

        var check = await RunProcessAsync(repositoryRoot, "git", ["apply", "--check", "--whitespace=nowarn", patchPath], token);
        if (!check.Succeeded)
            return new(false, "Main-repository patch validation failed; nothing was incorporated.", TrimOutput(check.Output), patchPath);
        var apply = await RunProcessAsync(repositoryRoot, "git", ["apply", "--whitespace=nowarn", patchPath], token);
        if (!apply.Succeeded)
            return new(false, "Main-repository patch application failed atomically.", TrimOutput(apply.Output), patchPath);

        return new(true,
            $"William-approved verified patch incorporated into the main repository across {changed.Count} file(s). No commit, push, publish, install, or production change was performed.",
            string.Join(Environment.NewLine, changed.Select(path => "- " + path)), patchPath);
    }

    private async Task<BorinEngineeringWorkspaceStatus> EnsureWorkspaceAsync(
        string repositoryRoot,
        CancellationToken token)
    {
        var status = await GetStatusAsync(repositoryRoot, token);
        return status.State == "InvalidBaseline"
            ? await CreateAsync(repositoryRoot, recreate: true, token)
            : status.Exists ? status : await CreateAsync(repositoryRoot, false, token);
    }

    private static string ValidateRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new InvalidOperationException("The RealmGate repository path is empty.");
        var full = Path.GetFullPath(repositoryRoot.Trim());
        var appProject = Path.Combine(full, "Apps", "Source", "RealmGate.App", "RealmGate.App.csproj");
        var coreProject = Path.Combine(full, "Core", "Source", "RealmGate.Core", "RealmGate.Core.csproj");
        if (!File.Exists(appProject) || !File.Exists(coreProject))
            throw new DirectoryNotFoundException("The selected folder is not the RealmGate repository: " + full);
        return full;
    }

    private static string ResolveWorkspacePath(string repositoryRoot)
    {
        var parent = Directory.GetParent(repositoryRoot)?.FullName ?? repositoryRoot;
        var repositoryName = new DirectoryInfo(repositoryRoot).Name;
        return Path.Combine(parent, ".ReforgedBorinWorkspaces", repositoryName, "active");
    }

    private static void DeleteWorkspaceSafely(string repositoryRoot, string workspacePath)
    {
        var expected = Path.GetFullPath(ResolveWorkspacePath(repositoryRoot));
        var actual = Path.GetFullPath(workspacePath);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to delete an unexpected workspace path.");
        try
        {
            Directory.Delete(actual, true);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // A damaged local Git baseline can contain a protected/read-only object that
            // prevents recursive deletion. Renaming the exact validated workspace root is
            // atomic on the same volume and lets a clean active snapshot be created without
            // traversing or modifying the inaccessible child. The quarantined snapshot is
            // retained for manual recovery/cleanup and is never treated as active.
            if (!Directory.Exists(actual)) return;
            var parent = Directory.GetParent(actual)?.FullName
                ?? throw new InvalidOperationException("The controlled workspace parent could not be resolved.", exception);
            var quarantine = Path.Combine(parent, $"stale-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            Directory.Move(actual, quarantine);
        }
    }

    private static void CopyRepositorySnapshot(string repositoryRoot, string workspacePath, CancellationToken token)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };
        foreach (var file in Directory.EnumerateFiles(root, "*", enumeration))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, file);
            if (ShouldExcludeFromSnapshot(relative))
                continue;
            try
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    continue;
                var destination = CombineUnderRoot(workspacePath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, true);
            }
            catch (UnauthorizedAccessException)
            {
                // Locked/local-only files are deliberately omitted from the private snapshot.
            }
            catch (IOException)
            {
                // Build artifacts and transient files are deliberately omitted when locked.
            }
        }
    }

    private static bool ShouldExcludeFromSnapshot(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => ExcludedDirectoryNames.Contains(segment)))
            return true;
        return normalized.StartsWith("AI/Models/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("AI/Memory/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("AI/Workspaces/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractUnifiedDiff(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var match = Regex.Match(text, "```(?:diff|patch)\\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static IReadOnlyList<string> ParseAndValidatePatchPaths(string patchText)
    {
        if (patchText.Contains("GIT binary patch", StringComparison.OrdinalIgnoreCase) ||
            patchText.Contains("Binary files", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Binary patches are not allowed in the controlled engineering workspace.");
        }
        if (!BorinPatchProposalValidator.TryValidateCompleteness(patchText, out var completenessError))
            throw new InvalidOperationException(completenessError);
        if (Regex.IsMatch(patchText, "^\\+\\+\\+\\s+/dev/null\\s*$", RegexOptions.Multiline))
            throw new InvalidOperationException("File deletion patches are not allowed in Stage 6 Pass 0001.");

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(
                     patchText,
                     "^diff --git a/(.+?) b/(.+?)\\s*$",
                     RegexOptions.Multiline))
        {
            paths.Add(ValidateRelativeDraftPath(match.Groups[2].Value.Trim().Trim('"')));
        }

        if (paths.Count == 0)
        {
            foreach (Match match in Regex.Matches(
                         patchText,
                         "^\\+\\+\\+\\s+(?:b/)?(.+?)\\s*$",
                         RegexOptions.Multiline))
            {
                var value = match.Groups[1].Value.Trim().Trim('"');
                if (!value.Equals("/dev/null", StringComparison.OrdinalIgnoreCase))
                    paths.Add(ValidateRelativeDraftPath(value));
            }
        }
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ValidateRelativeDraftPath(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized) ||
            normalized.Equals("..", StringComparison.Ordinal) ||
            normalized.StartsWith("../", StringComparison.Ordinal) ||
            normalized.Contains("/../", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The draft contains an unsafe path: " + relativePath);
        }
        if (ForbiddenRelativePrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The draft targets a protected path: " + normalized);

        var extension = Path.GetExtension(normalized);
        if (!AllowedDraftExtensions.Contains(extension))
            throw new InvalidOperationException("The draft targets a file type not allowed in Stage 6 Pass 0001: " + normalized);
        return normalized;
    }

    private static string? FindBuildTarget(string workspacePath)
    {
        var solution = Directory.EnumerateFiles(workspacePath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (solution is not null)
            return solution;
        var appProject = Path.Combine(workspacePath, "Apps", "Source", "RealmGate.App", "RealmGate.App.csproj");
        return File.Exists(appProject) ? appProject : null;
    }

    private static async Task<IReadOnlyList<string>> GetChangedAndUntrackedPathsAsync(
        string workspacePath,
        CancellationToken token)
    {
        var changed = await GetGitPathsAsync(workspacePath, "ACMRTUXB", token);
        var untrackedResult = await RunProcessAsync(
            workspacePath,
            "git",
            ["ls-files", "--others", "--exclude-standard"],
            token);
        if (!untrackedResult.Succeeded)
            throw new InvalidOperationException("Could not enumerate untracked workspace files: " + untrackedResult.Output);

        var paths = new HashSet<string>(changed, StringComparer.OrdinalIgnoreCase);
        foreach (var path in SplitLines(untrackedResult.Output))
        {
            if (!path.StartsWith(".borin/", StringComparison.OrdinalIgnoreCase))
                paths.Add(NormalizeRelativePath(path));
        }
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static async Task<IReadOnlyList<string>> GetGitPathsAsync(
        string workspacePath,
        string diffFilter,
        CancellationToken token)
    {
        var result = await RunProcessAsync(
            workspacePath,
            "git",
            ["diff", "--name-only", $"--diff-filter={diffFilter}", "HEAD"],
            token);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not enumerate workspace changes: " + result.Output);
        return SplitLines(result.Output)
            .Where(path => !path.StartsWith(".borin/", StringComparison.OrdinalIgnoreCase))
            .Select(NormalizeRelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> SplitLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CombineUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escaped the controlled root: " + relativePath);
        return full;
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/').Trim();

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string TrimOutput(string output)
    {
        var value = output.Trim();
        return value.Length <= MaximumDisplayedOutput ? value : value[^MaximumDisplayedOutput..];
    }

    private static string SummarizeGitFailure(string output)
    {
        var useful = SplitLines(output)
            .Where(line => !(line.StartsWith("warning: in the working copy of '", StringComparison.OrdinalIgnoreCase) &&
                             line.Contains("will be replaced by", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var value = string.Join(Environment.NewLine, useful).Trim();
        if (value.Length == 0)
            return "Git returned a nonzero exit code without diagnostic output.";
        return value.Length <= 4000 ? value : value[^4000..];
    }

    private static string BuildDraftReadme(string name, IReadOnlyList<string> changed) =>
        $"""
{name}

STATUS: DRAFT REQUIRES USER APPROVAL

This ZIP was created from Borin's isolated Stage 6 engineering workspace.
It was not installed, applied, committed, or pushed automatically.

Payload files:
{string.Join(Environment.NewLine, changed.Select(path => "- " + path))}

Review every changed file before running Install-And-Validate.bat.
The included installer uses no backup and no rollback, matching the current RealmGate update policy.
""";

    private static string BuildDraftInstallerBatch(string name) =>
        $"""
@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-And-Validate.ps1"
set "EXITCODE=%ERRORLEVEL%"
echo.
if not "%EXITCODE%"=="0" (
  echo FAILED: {name} did not complete.
  echo No rollback was performed. Installed source files remain in the repository.
) else (
  echo PASS: {name} completed.
)
echo.
pause
exit /b %EXITCODE%
""";

    private static string BuildDraftInstallerPowerShell(string name) =>
        $$"""
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$UpdateRoot = $PSScriptRoot
$RepositoryRoot = 'E:\MajorMud-Reforged Github'
$ManifestPath = Join-Path $UpdateRoot 'UpdateManifest.json'
$Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$ReportRoot = Join-Path $RepositoryRoot 'Reports\BorinBrain\GeneratedDrafts'
$LogPath = Join-Path $ReportRoot '{{name}}-$Stamp.log'

function Write-Log([string]$Message = '') { $Message | Tee-Object -FilePath $LogPath -Append }
function Assert-File([string]$Path, [string]$Description) {
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Description was not found: $Path" }
}

try {
  New-Item -ItemType Directory -Path $ReportRoot -Force | Out-Null
  Write-Log '============================================================'
  Write-Log '{{name}}'
  Write-Log 'User-approved draft installer - no backup and no rollback'
  Write-Log '============================================================'
  Assert-File $ManifestPath 'Update manifest'
  $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
  if ($manifest.status -ne 'DRAFT_REQUIRES_USER_APPROVAL') { throw 'Unexpected draft status.' }

  foreach ($entry in $manifest.files.PSObject.Properties) {
    $path = Join-Path $UpdateRoot $entry.Name
    Assert-File $path "Package file '$($entry.Name)'"
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne ([string]$entry.Value).ToLowerInvariant()) { throw "Package checksum mismatch: $($entry.Name)" }
  }

  Get-Process -Name 'RealmGate' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  foreach ($entry in $manifest.payload.PSObject.Properties) {
    $source = Join-Path $UpdateRoot $entry.Name
    $target = Join-Path $RepositoryRoot $entry.Name
    Assert-File $source "Payload file '$($entry.Name)'"
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Log "Installed: $($entry.Name)"
  }

  if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet was not found in PATH.' }
  $appProject = Join-Path $RepositoryRoot 'Apps\Source\RealmGate.App\RealmGate.App.csproj'
  $appChanged = @($manifest.payload.PSObject.Properties.Name | Where-Object { $_ -like 'Apps/Source/RealmGate.App/*' -or $_ -like 'Apps\Source\RealmGate.App\*' }).Count -gt 0
  if ($appChanged -and (Test-Path -LiteralPath $appProject)) {
    $publish = Join-Path $RepositoryRoot 'Build\BorinGeneratedDraft\Publish'
    if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
    & dotnet restore $appProject --nologo 2>&1 | Tee-Object -FilePath $LogPath -Append
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
    & dotnet publish $appProject -c Release -r win-x64 --self-contained false --no-restore --nologo -o $publish 2>&1 | Tee-Object -FilePath $LogPath -Append
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
    foreach ($target in @((Join-Path $RepositoryRoot 'Release\RealmGate'))) {
      New-Item -ItemType Directory -Path $target -Force | Out-Null
      & robocopy $publish $target /E /R:2 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
      if ($LASTEXITCODE -ge 8) { throw "Robocopy failed for $target." }
    }
  }
  else {
    $solution = Get-ChildItem -LiteralPath $RepositoryRoot -Filter '*.sln' -File | Select-Object -First 1
    if ($null -ne $solution) {
      & dotnet build $solution.FullName -c Release --nologo 2>&1 | Tee-Object -FilePath $LogPath -Append
      if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    }
  }

  Write-Log 'PASS: User-approved draft installed and validated.'
  exit 0
}
catch {
  Write-Log "FAILED: $($_.Exception.Message)"
  Write-Log 'No rollback was performed. Installed source files remain in the repository.'
  exit 1
}
""";

    private static async Task<ProcessResult> RunProcessAsync(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken token)
    {
        var output = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                output.AppendLine(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                output.AppendLine(eventArgs.Data);
        };

        try
        {
            if (!process.Start())
                return new ProcessResult(false, -1, $"Could not start {fileName}.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token);
            return new ProcessResult(process.ExitCode == 0, process.ExitCode, output.ToString());
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cancellation only.
            }
            throw;
        }
        catch (Exception exception)
        {
            return new ProcessResult(false, -1, exception.Message);
        }
    }

    private sealed record ProcessResult(bool Succeeded, int ExitCode, string Output);
}


