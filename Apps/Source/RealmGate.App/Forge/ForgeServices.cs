using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealmGate.App.Forge;

public interface IForgeStateStore
{
    Task<ForgeConversationState> LoadAsync(CancellationToken token = default);
    Task SaveAsync(ForgeConversationState state, CancellationToken token = default);
}

public interface IForgeRepositoryService
{
    string DiscoverRepositoryRoot();
    Task<ForgeRepositorySnapshot> ScanAsync(string rootPath, CancellationToken token = default);
    Task<string> SearchAsync(string rootPath, string query, CancellationToken token = default);
    Task<string> ReadLatestLogsAsync(string rootPath, CancellationToken token = default);
    Task<string> BuildContextAsync(string rootPath, string request, CancellationToken token = default);
    Task<ForgeStudyRecord> StudyAsync(string rootPath, string area, string relativePath, CancellationToken token = default);
}

public interface IForgeCommandRunner
{
    Task<ForgeCommandResult> RunBuildAsync(string rootPath, CancellationToken token = default);
    Task<ForgeCommandResult> RunTestsAsync(string rootPath, CancellationToken token = default);
}

public interface IForgeBrain
{
    Task<string> RespondAsync(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot, CancellationToken token = default);
}

public interface IForgeBrainManager
{
    Task<string> DetectLmStudioAsync(CancellationToken token = default);
    Task<string> TestConnectionAsync(ForgeConfiguration configuration, CancellationToken token = default);
    Task<IReadOnlyList<string>> ListModelsAsync(ForgeConfiguration configuration, CancellationToken token = default);
    Task<ForgeBrainHealth> GetHealthAsync(ForgeConfiguration configuration, CancellationToken token = default);
}

public sealed class ForgeStateStore : IForgeStateStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public ForgeStateStore(string applicationRoot)
    {
        var folder = Path.Combine(applicationRoot, "ForgeGuardian");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "forge-state.json");
    }

    public async Task<ForgeConversationState> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(_path)) return CreateDefault();
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<ForgeConversationState>(stream, JsonOptions, token) ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public async Task SaveAsync(ForgeConversationState state, CancellationToken token = default)
    {
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, token);
    }

    private static ForgeConversationState CreateDefault() => new()
    {
        ApprovedProjectRules =
        [
            "The Host owns authoritative runtime state.",
            "Generated Classic data is read-only; repair its source or generator instead.",
            "The Librarium is authoritative for verified world knowledge.",
            "Client-only features must not modify Host or MajorMUD game data.",
            "Source changes require a visible plan, validation, and human approval.",
            "Live-server changes use separate permissions from source-code work."
        ],
        Messages =
        [
            new ForgeChatMessage
            {
                Role = "Borin",
                Text = "Ah, welcome home, lad. Pull up a chair an' set yer ale upon the bar. Once LM Studio has Qwen2.5-Coder loaded and its local server be running, I can study this repository, draft C# and XAML repairs, work in a safe Git workshop, build, test, and bring every change back for yer approval. What shall we mend first?"
            }
        ]
    };
}

public sealed class ForgeRepositoryService : IForgeRepositoryService
{
    private static readonly string[] Ignored = ["bin", "obj", ".git", "Data\\Classic", "Data/Classic"];

    public string DiscoverRepositoryRoot()
    {
        var candidates = new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }
            .Concat(GetParents(Environment.CurrentDirectory))
            .Concat(GetParents(AppContext.BaseDirectory));
        return candidates.FirstOrDefault(IsRepositoryRoot) ?? Environment.CurrentDirectory;
    }

    public Task<ForgeRepositorySnapshot> ScanAsync(string rootPath, CancellationToken token = default) => Task.Run(() =>
    {
        EnsureRoot(rootPath);
        var files = EnumerateSafeFiles(rootPath).ToArray();
        var projects = files.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = files.Count(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));
        var tests = projects.Count(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase));
        var logs = files.Where(IsLog).OrderByDescending(File.GetLastWriteTimeUtc).Take(12).ToArray();
        var solutions = files.Where(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).Select(f => Path.GetRelativePath(rootPath, f)).ToArray();
        return new ForgeRepositorySnapshot(rootPath, projects.Length, source, tests, logs.Length, solutions, logs.Select(f => Path.GetRelativePath(rootPath, f)).ToArray());
    }, token);

    public Task<string> SearchAsync(string rootPath, string query, CancellationToken token = default) => Task.Run(() =>
    {
        EnsureRoot(rootPath);
        if (string.IsNullOrWhiteSpace(query)) return "Tell me what symbol, error, feature, or phrase to search for.";
        var matches = new List<string>();
        foreach (var file in EnumerateSafeFiles(rootPath).Where(IsTextSource).Take(10000))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++;
                    if (!line.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                    matches.Add($"{Path.GetRelativePath(rootPath, file)}:{lineNumber}: {line.Trim()}");
                    if (matches.Count >= 50) break;
                }
            }
            catch { }
            if (matches.Count >= 50) break;
        }
        return matches.Count == 0
            ? $"I found no exact matches for '{query}'. Try a class name, error code, or shorter phrase."
            : string.Join(Environment.NewLine, matches);
    }, token);

    public Task<string> ReadLatestLogsAsync(string rootPath, CancellationToken token = default) => Task.Run(() =>
    {
        EnsureRoot(rootPath);
        var logs = EnumerateSafeFiles(rootPath).Where(IsLog).OrderByDescending(File.GetLastWriteTimeUtc).Take(5).ToArray();
        if (logs.Length == 0) return "No readable log files were found in the repository.";
        var output = new StringBuilder();
        foreach (var log in logs)
        {
            token.ThrowIfCancellationRequested();
            output.AppendLine($"--- {Path.GetRelativePath(rootPath, log)} ---");
            try
            {
                var lines = File.ReadLines(log).TakeLast(120);
                foreach (var line in lines) output.AppendLine(line);
            }
            catch (Exception ex) { output.AppendLine($"Could not read log: {ex.Message}"); }
        }
        return output.ToString();
    }, token);


    public Task<string> BuildContextAsync(string rootPath, string request, CancellationToken token = default) => Task.Run(() =>
    {
        EnsureRoot(rootPath);
        var output = new StringBuilder();
        output.AppendLine("REPOSITORY CONTEXT (read-only)");
        foreach (var name in new[] { "AGENTS.md", "PROJECT_STATUS.md", "README.md", "VERSION.md", "CHANGELOG.md" })
        {
            var path = Path.Combine(rootPath, name);
            if (!File.Exists(path)) continue;
            output.AppendLine($"\n--- {name} ---");
            try { output.AppendLine(string.Join(Environment.NewLine, File.ReadLines(path).Take(220))); } catch { }
            if (output.Length > 18000) break;
        }

        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "the", "and", "that", "this", "with", "from", "into", "have", "will", "what", "when", "where", "build", "fix", "app", "application", "please", "borin" };
        var terms = request.Split(new[] { ' ', '\r', '\n', '\t', ':', ',', ';', '(', ')', '[', ']', '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim('`', '"', '\''))
            .Where(t => t.Length >= 4 && !stop.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

        if (terms.Length > 0)
        {
            output.AppendLine("\n--- Relevant source excerpts ---");
            var matches = 0;
            foreach (var file in EnumerateSafeFiles(rootPath).Where(IsTextSource).Take(12000))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (!terms.Any(t => lines[i].Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                        output.AppendLine($"\n# {Path.GetRelativePath(rootPath, file)}:{i + 1}");
                        for (var j = Math.Max(0, i - 3); j <= Math.Min(lines.Length - 1, i + 7); j++)
                            output.AppendLine($"{j + 1}: {lines[j]}");
                        matches++;
                        if (matches >= 18 || output.Length >= 42000) break;
                    }
                }
                catch { }
                if (matches >= 18 || output.Length >= 42000) break;
            }
        }
        return output.ToString();
    }, token);

    private static IEnumerable<string> GetParents(string path)
    {
        var current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;
        while (current is not null) { yield return current.FullName; current = current.Parent; }
    }

    private static bool IsRepositoryRoot(string path) => Directory.Exists(path) &&
        (File.Exists(Path.Combine(path, "AGENTS.md")) || Directory.Exists(Path.Combine(path, ".git"))) && Directory.Exists(Path.Combine(path, "Apps"));

    public Task<ForgeStudyRecord> StudyAsync(string rootPath, string area, string relativePath, CancellationToken token = default) => Task.Run(() =>
    {
        EnsureRoot(rootPath);
        var target = string.IsNullOrWhiteSpace(relativePath) ? rootPath : Path.Combine(rootPath, relativePath);
        if (!Directory.Exists(target)) throw new DirectoryNotFoundException(target);

        var files = EnumerateSafeFiles(target).ToArray();
        var sourceFiles = files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                           f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)).ToArray();
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projects = files.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(rootPath, f)).OrderBy(x => x).ToArray();

        foreach (var file in sourceFiles.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var line in File.ReadLines(file).Take(5000))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("namespace ", StringComparison.Ordinal))
                    {
                        var value = trimmed[10..].Trim().TrimEnd(';', '{');
                        if (!string.IsNullOrWhiteSpace(value)) namespaces.Add(value);
                    }
                    foreach (var keyword in new[] { " class ", " record ", " interface ", " enum " })
                    {
                        var index = (" " + trimmed).IndexOf(keyword, StringComparison.Ordinal);
                        if (index < 0) continue;
                        var remainder = (" " + trimmed)[(index + keyword.Length)..];
                        var name = new string(remainder.TakeWhile(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                        if (!string.IsNullOrWhiteSpace(name)) classes.Add(name);
                    }
                }
            }
            catch { }
        }

        var brainFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate", "ForgeGuardian", "ProjectBrain");
        Directory.CreateDirectory(brainFolder);
        var safeArea = string.Concat(area.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safeArea)) safeArea = "Repository";
        var summaryPath = Path.Combine(brainFolder, $"{safeArea}.md");
        var summary = new StringBuilder();
        summary.AppendLine($"# {area} Study");
        summary.AppendLine();
        summary.AppendLine($"Studied: {DateTimeOffset.Now:O}");
        summary.AppendLine($"Repository: {rootPath}");
        summary.AppendLine($"Area path: {relativePath}");
        summary.AppendLine($"Files: {files.Length}");
        summary.AppendLine($"Source/UI files: {sourceFiles.Length}");
        summary.AppendLine($"Namespaces: {namespaces.Count}");
        summary.AppendLine($"Classes/records/interfaces/enums: {classes.Count}");
        summary.AppendLine();
        summary.AppendLine("## Projects");
        foreach (var project in projects.Take(100)) summary.AppendLine($"- {project}");
        summary.AppendLine();
        summary.AppendLine("## Namespaces");
        foreach (var value in namespaces.OrderBy(x => x).Take(250)) summary.AppendLine($"- {value}");
        summary.AppendLine();
        summary.AppendLine("## Types");
        foreach (var value in classes.OrderBy(x => x).Take(500)) summary.AppendLine($"- {value}");
        File.WriteAllText(summaryPath, summary.ToString());

        return new ForgeStudyRecord
        {
            Area = area,
            RelativePath = relativePath,
            SummaryPath = summaryPath,
            FileCount = files.Length,
            SourceFileCount = sourceFiles.Length,
            ClassCount = classes.Count,
            NamespaceCount = namespaces.Count,
            StudiedAt = DateTimeOffset.Now
        };
    }, token);

    private static void EnsureRoot(string rootPath)
    {
        if (!Directory.Exists(rootPath)) throw new DirectoryNotFoundException($"Forge repository path does not exist: {rootPath}");
    }

    private static IEnumerable<string> EnumerateSafeFiles(string rootPath)
    {
        var pending = new Stack<string>(); pending.Push(rootPath);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            IEnumerable<string> directories;
            IEnumerable<string> files;
            try { directories = Directory.EnumerateDirectories(folder); files = Directory.EnumerateFiles(folder); }
            catch { continue; }
            foreach (var file in files) yield return file;
            foreach (var directory in directories)
            {
                var relative = Path.GetRelativePath(rootPath, directory);
                if (Ignored.Any(i => relative.Equals(i, StringComparison.OrdinalIgnoreCase) || relative.StartsWith(i + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
                pending.Push(directory);
            }
        }
    }

    private static bool IsTextSource(string f) => new[] { ".cs", ".xaml", ".json", ".md", ".xml", ".props", ".targets", ".bat", ".ps1", ".sh", ".txt", ".csproj", ".sln" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase);
    private static bool IsLog(string f) => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && (f.Contains("log", StringComparison.OrdinalIgnoreCase) || f.Contains("verify", StringComparison.OrdinalIgnoreCase));
}

public sealed class ForgeCommandRunner : IForgeCommandRunner
{
    public Task<ForgeCommandResult> RunBuildAsync(string rootPath, CancellationToken token = default) => RunAsync(rootPath, "dotnet", "build -c Release", token);
    public Task<ForgeCommandResult> RunTestsAsync(string rootPath, CancellationToken token = default) => RunAsync(rootPath, "dotnet", "test -c Release --no-restore", token);

    private static async Task<ForgeCommandResult> RunAsync(string rootPath, string fileName, string arguments, CancellationToken token)
    {
        if (!Directory.Exists(rootPath)) throw new DirectoryNotFoundException(rootPath);
        var output = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = rootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        try
        {
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            await process.WaitForExitAsync(token);
            return new ForgeCommandResult($"{fileName} {arguments}", process.ExitCode, output.ToString(), DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            return new ForgeCommandResult($"{fileName} {arguments}", -1, $"Command could not start: {ex.Message}", DateTimeOffset.Now);
        }
    }
}


public sealed class ForgeBrainManager : IForgeBrainManager
{
    public Task<string> DetectLmStudioAsync(CancellationToken token = default) => Task.Run(() =>
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LM Studio", "LM Studio.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Studio", "LM Studio.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LM Studio", "LM Studio.exe")
        };
        var found = candidates.FirstOrDefault(File.Exists);
        return found is null
            ? "LM Studio was not found in the common install locations. It may still be installed elsewhere. Open LM Studio manually, load a coding model, then start its Local Server on port 1234."
            : $"Aye, LM Studio be installed at `{found}`. Open it, load a coding model, and start the Local Server. Borin expects `http://127.0.0.1:1234/v1`.";
    }, token);

    public async Task<string> TestConnectionAsync(ForgeConfiguration configuration, CancellationToken token = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            AddAuthorizationIfRequired(client, configuration);
            var modelsUri = string.IsNullOrWhiteSpace(configuration.ModelsEndpoint)
                ? DeriveModelsEndpoint(configuration.ApiEndpoint)
                : configuration.ModelsEndpoint;

            using var modelsResponse = await client.GetAsync(modelsUri, token);
            var modelsBody = await modelsResponse.Content.ReadAsStringAsync(token);
            if (!modelsResponse.IsSuccessStatusCode)
                return $"Nay, LM Studio answered the models check with HTTP {(int)modelsResponse.StatusCode}. Details: {modelsBody}";

            var models = ParseModels(modelsBody);
            if (models.Count == 0)
                return "The local server answered, but no loaded model was reported. Load a model in LM Studio, then test again.";

            if (string.IsNullOrWhiteSpace(configuration.ModelName))
                configuration.ModelName = models[0];

            var payload = new
            {
                model = configuration.ModelName,
                messages = new object[]
                {
                    new { role = "system", content = "Reply with exactly: FORGE_BRAIN_READY" },
                    new { role = "user", content = "Connection test." }
                },
                temperature = 0.0,
                max_tokens = 16,
                stream = false
            };

            using var chatResponse = await client.PostAsync(
                configuration.ApiEndpoint,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                token);
            var chatBody = await chatResponse.Content.ReadAsStringAsync(token);
            if (!chatResponse.IsSuccessStatusCode)
                return $"The model list works, but chat failed with HTTP {(int)chatResponse.StatusCode}. LM Studio says: {(chatBody.Length > 1200 ? chatBody[..1200] : chatBody)}";

            using var doc = JsonDocument.Parse(chatBody);
            var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return $"Aye! The Master Brain answers true through `{configuration.ModelName}`. Reply: {reply}";
        }
        catch (Exception ex)
        {
            return $"The forge-fire has not caught yet: {ex.Message}. Keep LM Studio running, load a model, and start the Local Server on port 1234.";
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(ForgeConfiguration configuration, CancellationToken token = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        AddAuthorizationIfRequired(client, configuration);
        var modelsUri = string.IsNullOrWhiteSpace(configuration.ModelsEndpoint)
            ? DeriveModelsEndpoint(configuration.ApiEndpoint)
            : configuration.ModelsEndpoint;
        using var response = await client.GetAsync(modelsUri, token);
        var body = await response.Content.ReadAsStringAsync(token);
        response.EnsureSuccessStatusCode();
        return ParseModels(body);
    }

    public async Task<ForgeBrainHealth> GetHealthAsync(ForgeConfiguration configuration, CancellationToken token = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            AddAuthorizationIfRequired(client, configuration);
            var modelsUri = string.IsNullOrWhiteSpace(configuration.ModelsEndpoint)
                ? DeriveModelsEndpoint(configuration.ApiEndpoint)
                : configuration.ModelsEndpoint;
            using var response = await client.GetAsync(modelsUri, token);
            var body = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode)
                return new ForgeBrainHealth(false, false, string.Empty, $"HTTP {(int)response.StatusCode}");
            var models = ParseModels(body);
            var selected = !string.IsNullOrWhiteSpace(configuration.ModelName)
                ? configuration.ModelName
                : models.FirstOrDefault() ?? string.Empty;
            return new ForgeBrainHealth(true, models.Count > 0, selected,
                models.Count > 0 ? "LM Studio reachable and a model is loaded." : "LM Studio reachable; no model loaded.");
        }
        catch (Exception ex)
        {
            return new ForgeBrainHealth(false, false, string.Empty, ex.Message);
        }
    }

    private static string DeriveModelsEndpoint(string chatEndpoint)
    {
        if (string.IsNullOrWhiteSpace(chatEndpoint)) return "http://127.0.0.1:1234/v1/models";
        return chatEndpoint.Replace("/chat/completions", "/models", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ParseModels(string body)
    {
        var result = new List<string>();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in data.EnumerateArray())
            if (item.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.GetString())) result.Add(id.GetString()!);
        return result;
    }

    private static void AddAuthorizationIfRequired(HttpClient client, ForgeConfiguration configuration)
    {
        if (!configuration.RequireApiKey) return;
        var key = Environment.GetEnvironmentVariable(configuration.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }
}

public sealed class ForgeBrain(IForgeRepositoryService repository, IForgeCommandRunner commands) : IForgeBrain
{
    private static readonly string[] MasterBrainKeywords =
    [
        "implement", "write code", "create code", "generate code", "draft patch", "unified diff",
        "fix the code", "repair the code", "refactor", "design a feature", "build a feature",
        "add a feature", "create a class", "create a service", "create a tab", "analyze architecture",
        "debug this", "find the root cause", "review this file", "implement this file"
    ];

    public async Task<string> RespondAsync(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot, CancellationToken token = default)
    {
        var text = request.Trim();
        var lower = text.ToLowerInvariant();
        var root = state.Configuration.RepositoryPath;
        var explicitMasterBrain = lower.StartsWith("/brain ") || lower.StartsWith("master brain ");

        if (IsGreeting(lower))
            return Greeting(state.Configuration);

        if (lower is "help" or "what can you do")
            return Help(state);

        if (lower.Contains("what files") && lower.Contains("attach"))
            return DescribeAttachments(state);

        if (!explicitMasterBrain && (lower.Contains("scan") || lower.Contains("inspect repository")))
        {
            var scan = await repository.ScanAsync(root, token);
            return $"Aye, I've walked the whole workshop. I found {scan.ProjectCount} projects, {scan.SourceFileCount} source/UI files, {scan.TestProjectCount} test projects, and {scan.LogFileCount} recent logs. Solutions: {(scan.Solutions.Count == 0 ? "none found" : string.Join(", ", scan.Solutions))}. No files were changed.";
        }

        if (!explicitMasterBrain && (lower.StartsWith("search ") || lower.StartsWith("find ")))
        {
            var query = text[(text.IndexOf(' ') + 1)..].Trim(' ', '"');
            var found = await repository.SearchAsync(root, query, token);
            return $"Aye, here's what the stonework says about '{query}':\n\n{found}\n\nRead-only search; nothing was changed.";
        }

        if (!explicitMasterBrain && (lower.Contains("read log") || lower.Contains("latest log") || lower.Contains("server log")))
        {
            var logs = await repository.ReadLatestLogsAsync(root, token);
            return $"I've pulled the freshest ledgers from the cellar. Here they are:\n\n{logs}";
        }

        if (!explicitMasterBrain && (lower is "build" || lower.Contains("build the app") || lower.Contains("build solution")))
        {
            if (!state.Configuration.AllowBuildCommands)
                return "The build hammer is locked in Settings. Enable approved build commands first.";
            var result = await commands.RunBuildAsync(root, token);
            return FormatCommand(result);
        }

        if (!explicitMasterBrain && (lower is "test" || lower.Contains("run tests")))
        {
            if (!state.Configuration.AllowTestCommands)
                return "The testing casks are locked in Settings. Enable approved test commands first.";
            var result = await commands.RunTestsAsync(root, token);
            return FormatCommand(result);
        }

        var useMasterBrain = ShouldUseMasterBrain(text, state);
        if (useMasterBrain)
        {
            if (string.IsNullOrWhiteSpace(state.Configuration.ApiEndpoint) ||
                string.IsNullOrWhiteSpace(state.Configuration.ModelName))
            {
                return $"Aye, {state.Configuration.AddressUserAs}. This task needs the Master Brain, but no loaded model be selected yet. Start LM Studio, load the coding model, press Find Models, then try again.\n\n{Plan(text, state, snapshot)}";
            }

            var projectContext = await repository.BuildContextAsync(root, text, token);
            return await TryOnlineAsync(text, state, snapshot, projectContext, token);
        }

        return OfflineConversation(text, state, snapshot);
    }

    private static bool ShouldUseMasterBrain(string request, ForgeConversationState state)
    {
        if (state.Configuration.UseMasterBrainForGeneralConversation)
            return true;

        var lower = request.ToLowerInvariant();
        if (lower.StartsWith("/brain ") || lower.StartsWith("master brain "))
            return true;

        if (state.Attachments.Count > 0 &&
            (lower.Contains("implement") || lower.Contains("analyze") || lower.Contains("review") ||
             lower.Contains("merge") || lower.Contains("apply") || lower.Contains("read this")))
            return true;

        return MasterBrainKeywords.Any(lower.Contains);
    }

    private static bool IsGreeting(string lower) =>
        lower is "hello" or "hi" or "hey" or "good morning" or "good afternoon" or "good evening" ||
        lower.StartsWith("hello borin") || lower.StartsWith("hi borin") ||
        lower.StartsWith("good morning borin") || lower.StartsWith("good evening borin");

    private static string Greeting(ForgeConfiguration profile) =>
        $"Aye, good to see ye, {profile.AddressUserAs}. Pull up a chair an' set yer {profile.FavoriteDrink.ToLowerInvariant()} upon the bar. The local forge be ready; I only wake the Master Brain when ye ask me to design, repair, or write code. What troubles the workshop this day?";

    private static string Help(ForgeConversationState state) =>
        $"Aye. I can talk with ye locally, scan the repository, search code, read logs, build, test, manage a safe Git workshop, and read attached files. I wake the Master Brain only for complex coding work unless ye enable general AI conversation. Attached files: {state.Attachments.Count}. Main-repository edits still require approval.";

    private static string DescribeAttachments(ForgeConversationState state)
    {
        if (state.Attachments.Count == 0)
            return "No scrolls be pinned to the workbench yet. Use Attach File, then tell me whether to read, compare, or implement it.";
        return "These scrolls be attached:\n\n" + string.Join("\n", state.Attachments.Select(a => $"- {a.DisplayLabel}"));
    }

    private static string OfflineConversation(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot)
    {
        var lower = request.ToLowerInvariant();
        if (lower.Contains("thank"))
            return $"Ye're welcome, {state.Configuration.AddressUserAs}. Good craft be lighter work with two sets o' hands.";

        if (lower.Contains("who are you") || lower.Contains("your name"))
            return $"I be {state.Configuration.CharacterName}, {state.Configuration.CharacterTitle}. {state.Configuration.Mission}";

        return Plan(request, state, snapshot);
    }

    private static string FormatCommand(ForgeCommandResult result)
    {
        var verdict = result.Succeeded ? "The forge rings true" : "There be a crack in the stone";
        var tail = result.Output.Length > 12000 ? result.Output[^12000..] : result.Output;
        return $"{verdict}. `{result.Command}` finished with exit code {result.ExitCode}.\n\n{tail}\n\nNo source files were changed by this command.";
    }

    private static string Plan(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot)
    {
        var context = snapshot is null
            ? "I have not scanned the repository yet."
            : $"The current workshop contains {snapshot.ProjectCount} projects and {snapshot.SourceFileCount} source/UI files.";

        var errors = ExtractErrors(request);
        if (errors.Count > 0)
        {
            return $"Aye, I see {errors.Count} compiler/runtime clue(s): {string.Join(", ", errors)}. {context}\n\nMy safe repair path be: locate each symbol and its owning project; inspect the nearest call path; check references and imports; reproduce with an approved build; draft the smallest correction in the safe workshop; then rerun verification. Say `master brain fix this code` when ye want the coding model to draft the repair.";
        }

        var attachmentNote = state.Attachments.Count == 0
            ? string.Empty
            : $"\n\nThere be {state.Attachments.Count} attached file(s) ready for review.";

        return $"Pull up a chair. I understand the request as: **{request}**\n\n{context}{attachmentNote}\n\nI can handle conversation and routine workshop commands locally. For original code or deep diagnosis, say `master brain` or ask me to implement, repair, refactor, or design it. I will not silently alter the main repository, commit, push, install programs, or touch the live server.";
    }

    private static List<string> ExtractErrors(string request)
    {
        var tokens = request.Split([' ', '\r', '\n', '\t', ':', ',', ';', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries);
        return tokens
            .Where(t => (t.StartsWith("CS", StringComparison.OrdinalIgnoreCase) && t.Length is >= 6 and <= 10) ||
                        t.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static string BuildAttachmentContext(ForgeConversationState state)
    {
        if (state.Attachments.Count == 0)
            return "No attached files.";

        var output = new StringBuilder();
        output.AppendLine("ATTACHED FILES (user supplied; treat executable scripts as text only)");
        foreach (var attachment in state.Attachments.Take(8))
        {
            output.AppendLine($"\n--- {attachment.OriginalName} ---");
            try
            {
                var extension = Path.GetExtension(attachment.StoredPath).ToLowerInvariant();
                if (extension == ".zip")
                {
                    using var archive = ZipFile.OpenRead(attachment.StoredPath);
                    foreach (var entry in archive.Entries.Take(150))
                        output.AppendLine($"{entry.FullName} ({entry.Length} bytes)");
                }
                else if (new[] { ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".cs", ".xaml", ".csproj", ".sln", ".log", ".patch", ".diff", ".ps1", ".bat", ".cmd" }.Contains(extension))
                {
                    var value = File.ReadAllText(attachment.StoredPath);
                    output.AppendLine(value.Length > 16000 ? value[..16000] + "\n[attachment truncated]" : value);
                }
                else
                {
                    output.AppendLine($"Binary or unsupported preview type. Path: {attachment.StoredPath}");
                }
            }
            catch (Exception ex)
            {
                output.AppendLine($"Could not read attachment: {ex.Message}");
            }

            if (output.Length > 32000)
                break;
        }

        return output.ToString();
    }

    private static async Task<string> TryOnlineAsync(
        string request,
        ForgeConversationState state,
        ForgeRepositorySnapshot? snapshot,
        string projectContext,
        CancellationToken token)
    {
        var key = Environment.GetEnvironmentVariable(state.Configuration.ApiKeyEnvironmentVariable);
        if (state.Configuration.RequireApiKey && string.IsNullOrWhiteSpace(key))
            return $"The selected brain requires a key, but `{state.Configuration.ApiKeyEnvironmentVariable}` be not set.\n\n{Plan(request, state, snapshot)}";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(8) };
            if (state.Configuration.RequireApiKey && !string.IsNullOrWhiteSpace(key))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

            var profile = state.Configuration;
            var system = $"""
You are {profile.CharacterName}, {profile.CharacterTitle}, age {profile.CharacterAge}, and senior software engineer for RealmGate.
Mission: {profile.Mission}
Address the user as: {profile.AddressUserAs}
Speech style: {profile.SpeechStyle}
Temperament: {profile.Temperament}
Values: {profile.Values}
Favorite drink: {profile.FavoriteDrink}
Favorite saying: {profile.FavoriteSaying}
Additional character instructions: {profile.CharacterInstructions}

Keep code and technical explanations exact. Inspect supplied context, identify root cause, prefer the smallest architecture-consistent change, preserve existing behavior, and add tests when appropriate. When implementation is requested, return a complete valid unified Git patch inside one fenced diff block. Never claim a build, test, edit, commit, push, or server action occurred unless a Forge tool result confirms it. Never edit generated Classic data.
Project rulebook: {string.Join(" ", state.ApprovedProjectRules)}
Learned project memories: {string.Join(" ", state.LearnedMemories.TakeLast(40))}
Offline subsystem studies: {string.Join("; ", state.SubsystemStudies.Values.Select(x => x.Display))}
""";

            var maxContext = Math.Clamp(profile.LocalModelMaxContextCharacters, 4000, 50000);
            var trimmedProjectContext = projectContext.Length > maxContext
                ? projectContext[..maxContext] + "\n[repository context truncated]"
                : projectContext;
            var attachmentContext = BuildAttachmentContext(state);
            if (attachmentContext.Length > maxContext)
                attachmentContext = attachmentContext[..maxContext] + "\n[attachment context truncated]";

            var cleanedRequest = request.StartsWith("/brain ", StringComparison.OrdinalIgnoreCase)
                ? request[7..]
                : request;

            var payload = new
            {
                model = profile.ModelName,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new
                    {
                        role = "user",
                        content = $"Repository snapshot: {JsonSerializer.Serialize(snapshot)}\n\n{trimmedProjectContext}\n\n{attachmentContext}\n\nUSER REQUEST: {cleanedRequest}\n\nReturn a precise diagnosis/plan, or a complete unified diff when code changes are requested."
                    }
                },
                temperature = 0.2,
                max_tokens = Math.Clamp(profile.MasterBrainMaxOutputTokens, 256, 8192),
                stream = false
            };

            var json = JsonSerializer.Serialize(payload);
            using var response = await client.PostAsync(
                profile.ApiEndpoint,
                new StringContent(json, Encoding.UTF8, "application/json"),
                token);
            var body = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                var details = body.Length > 1200 ? body[..1200] : body;
                return $"LM Studio rejected the Master Brain request with HTTP {(int)response.StatusCode}.\n\n{details}\n\nI kept the request local and changed no files.\n\n{Plan(request, state, snapshot)}";
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.GetArrayLength() == 0 ||
                !choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content))
            {
                return $"LM Studio answered, but the reply format be unfamiliar. Raw reply:\n\n{(body.Length > 2000 ? body[..2000] : body)}";
            }

            return content.GetString() ?? "The Master Brain answered with an empty message.";
        }
        catch (Exception ex)
        {
            return $"Bah, the Master Brain line failed: {ex.Message}. I kept the request local and changed no files.\n\n{Plan(request, state, snapshot)}";
        }
    }
}
