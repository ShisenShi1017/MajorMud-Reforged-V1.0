using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App.Guardians;

public interface IGuardianBrainRouter { GuardianBrainDecision Route(GuardianBrainRequest request); }
public sealed class GuardianBrainRouter : IGuardianBrainRouter
{
    public GuardianBrainDecision Route(GuardianBrainRequest request) => request.RequiresMasterBrain
        ? new(true, "Requires model reasoning or content generation through the shared LM Studio adapter.")
        : new(false, "Handled by the local Guardian framework.");
}

public interface IGuardianAssignmentStore
{
    Task SaveAsync(GuardianHallState state, CancellationToken token = default);
    Task<GuardianHallState?> LoadAsync(CancellationToken token = default);
    string RootDirectory { get; }
}

public sealed class GuardianAssignmentStore : IGuardianAssignmentStore
{
    private readonly string _path;
    private readonly string _legacyPath;
    private readonly string _migrationReceiptPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string RootDirectory { get; }
    public GuardianAssignmentStore(string rootPath)
    {
        RootDirectory = Path.Combine(rootPath, "GuardianHall");
        Directory.CreateDirectory(RootDirectory);
        _path = Path.Combine(RootDirectory, "guardian-hall-state.json");
        _legacyPath = Path.Combine(RootDirectory, "assignments.json");
        _migrationReceiptPath = Path.Combine(RootDirectory, "migration-phase1-to-role-workspaces.json");
    }
    public async Task SaveAsync(GuardianHallState state, CancellationToken token = default)
    {
        state.SchemaVersion = 7;
        var tempPath = _path + ".tmp";
        var backupPath = _path + ".backup";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, token);
            await stream.FlushAsync(token);
        }
        if (File.Exists(_path)) File.Copy(_path, backupPath, true);
        File.Move(tempPath, _path, true);
    }
    public async Task<GuardianHallState?> LoadAsync(CancellationToken token = default)
    {
        if (File.Exists(_path))
        {
            try
            {
                await using var stream = File.OpenRead(_path);
                var state = await JsonSerializer.DeserializeAsync<GuardianHallState>(stream, JsonOptions, token);
                if (state is not null) state.SchemaVersion = Math.Max(7, state.SchemaVersion);
                return state;
            }
            catch (JsonException) when (File.Exists(_path + ".backup"))
            {
                await using var backup = File.OpenRead(_path + ".backup");
                var recovered = await JsonSerializer.DeserializeAsync<GuardianHallState>(backup, JsonOptions, token);
                if (recovered is not null)
                {
                    recovered.SchemaVersion = Math.Max(7, recovered.SchemaVersion);
                    recovered.MigrationStatus = "Recovered Guardian Hall state from atomic backup";
                }
                return recovered;
            }
        }

        if (!File.Exists(_legacyPath)) return null;
        return await MigrateLegacyAssignmentsAsync(token);
    }

    private async Task<GuardianHallState> MigrateLegacyAssignmentsAsync(CancellationToken token)
    {
        await using var stream = File.OpenRead(_legacyPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var state = new GuardianHallState
        {
            SchemaVersion = 2,
            MigrationStatus = "Migrated Phase 1 generic assignments into role-specific workspaces",
            MigratedFrom = Path.GetFileName(_legacyPath),
            MigratedUtc = DateTime.UtcNow.ToString("O"),
            Guardians = GuardianCatalog.CreateDefaults()
        };

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var legacy in document.RootElement.EnumerateArray()) ApplyLegacyAssignment(state, legacy);
        }

        var backupPath = _legacyPath + ".phase1-backup";
        if (!File.Exists(backupPath)) File.Copy(_legacyPath, backupPath);
        await SaveAsync(state, token);
        await File.WriteAllTextAsync(_migrationReceiptPath, JsonSerializer.Serialize(new
        {
            schemaVersion = state.SchemaVersion,
            migratedUtc = state.MigratedUtc,
            source = _legacyPath,
            backup = backupPath,
            destination = _path,
            rule = "Role-safe one-time migration; character assignment is retained only by Wanderer."
        }, JsonOptions), token);
        return state;
    }

    private static void ApplyLegacyAssignment(GuardianHallState state, JsonElement legacy)
    {
        var roleText = ReadText(legacy, "Role");
        if (!Enum.TryParse<GuardianRole>(roleText, true, out var role)) return;
        var profile = state.Guardians.FirstOrDefault(x => x.Role == role);
        if (profile is null) return;
        profile.Status = ReadEnum(legacy, "Status", GuardianStatus.Sleeping);
        profile.CurrentMission = ReadText(legacy, "CurrentMission", profile.CurrentMission);
        profile.LastActivity = ReadText(legacy, "LastActivity", profile.LastActivity);

        var server = ReadText(legacy, "ServerProfile", "Local Reforged Host");
        var world = ReadText(legacy, "World", "Classic");
        var realm = ReadText(legacy, "RealmInstance", "Test Realm");
        var character = ReadText(legacy, "Character", "None");
        var permission = ReadEnum(legacy, "Permission", GuardianPermission.Observe);
        var schedule = ReadEnum(legacy, "Schedule", GuardianSchedule.Manual);

        switch (role)
        {
            case GuardianRole.RealmKeeper:
                state.RealmKeeper.LocalHost = server.Contains("Local", StringComparison.OrdinalIgnoreCase);
                state.RealmKeeper.LanHost = server.Contains("LAN", StringComparison.OrdinalIgnoreCase);
                state.RealmKeeper.InternetHost = server.Contains("Internet", StringComparison.OrdinalIgnoreCase);
                state.RealmKeeper.Classic = world.Contains("Classic", StringComparison.OrdinalIgnoreCase);
                state.RealmKeeper.Custom = world.Contains("Custom", StringComparison.OrdinalIgnoreCase);
                state.RealmKeeper.AiWorlds = world.Contains("AI", StringComparison.OrdinalIgnoreCase);
                state.RealmKeeper.TestRealms = realm.Contains("Test", StringComparison.OrdinalIgnoreCase) || realm.Contains("Development", StringComparison.OrdinalIgnoreCase);
                state.RealmKeeper.RealmTimeline.Insert(0, $"Migrated Phase 1 scope: {server} / {world} / {realm}");
                break;
            case GuardianRole.Wanderer:
                state.Wanderer.Server = server; state.Wanderer.World = world; state.Wanderer.Realm = realm;
                if (!string.IsNullOrWhiteSpace(character) && !character.Equals("None", StringComparison.OrdinalIgnoreCase)) state.Wanderer.Character = character;
                state.Wanderer.Permission = permission; state.Wanderer.Schedule = schedule;
                state.Wanderer.Mission = profile.CurrentMission;
                break;
            case GuardianRole.Analyst:
                state.Analyst.DataSources = $"Migrated scope reference: {server} / {world} / {realm}; runtime telemetry, Librarium, imported records";
                state.Analyst.ReportFrequency = schedule.ToString();
                break;
            case GuardianRole.Worldsmith:
                state.Worldsmith.TargetTestRealm = realm.Contains("Live", StringComparison.OrdinalIgnoreCase) ? "Development Realm" : realm;
                state.Worldsmith.BuildProject = profile.CurrentMission == "Awaiting assignment" ? state.Worldsmith.BuildProject : profile.CurrentMission;
                state.Worldsmith.ApprovalRequired = permission != GuardianPermission.Administrator;
                break;
            case GuardianRole.Archivist:
                state.Archivist.SearchText = profile.CurrentMission == "Awaiting assignment" ? state.Archivist.SearchText : profile.CurrentMission;
                break;
        }
    }

    private static string ReadText(JsonElement element, string name, string fallback = "")
    {
        if (!element.TryGetProperty(name, out var value)) return fallback;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }

    private static T ReadEnum<T>(JsonElement element, string name, T fallback) where T : struct, Enum
    {
        var text = ReadText(element, name);
        if (Enum.TryParse<T>(text, true, out var parsed)) return parsed;
        if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) && Enum.IsDefined(typeof(T), number)) return (T)Enum.ToObject(typeof(T), number);
        return fallback;
    }
}

public interface IRealmKeeperTelemetryService
{
    Task<RealmKeeperTelemetrySnapshot> CaptureAsync(RealmKeeperWorkspace workspace, CancellationToken token = default);
}

public sealed class RealmKeeperTelemetryService : IRealmKeeperTelemetryService
{
    private static readonly string[] CountedFiles = ["rooms.json", "exits.json", "npcs.json", "items.json", "quests.json", "spells.json", "triggers.json", "scripts.json"];
    private readonly string _repositoryRoot;

    public RealmKeeperTelemetryService(string repositoryRoot)
    {
        _repositoryRoot = repositoryRoot;
    }

    public async Task<RealmKeeperTelemetrySnapshot> CaptureAsync(RealmKeeperWorkspace workspace, CancellationToken token = default)
    {
        var local = await Task.Run(() => Capture(token), token);
        var remotes = await CaptureRemoteAsync(workspace, token);
        if (remotes.Count == 0) return local;
        return local with
        {
            ConnectedHosts = local.ConnectedHosts + remotes.Count(x => x.Connected),
            CoveredWorlds = local.CoveredWorlds + remotes.Sum(x => x.Worlds),
            ActiveSaveFiles = local.ActiveSaveFiles + remotes.Sum(x => x.Population),
            HealthSummary = local.HealthSummary + " " + string.Join(" ", remotes.Select(x => $"{x.Name}: {x.Health}.")),
            ActivitySummary = local.ActivitySummary + " " + string.Join(" ", remotes.Where(x => x.Connected).Select(x => $"{x.Name} population {x.Population}, events {x.ActiveEvents}.")),
            SourceSummary = local.SourceSummary + $"; authenticated HTTP telemetry from {remotes.Count(x => x.Connected)} configured remote host(s)",
            TimelineEntries = local.TimelineEntries.Concat(remotes.Select(x => $"{DateTime.Now:g} — {x.Name}: {(x.Connected ? x.Health : x.Error)}")).ToList()
        };
    }

    private sealed record RemoteResult(string Name, bool Connected, int Worlds, int Population, int ActiveEvents, string Health, string Error);

    private static async Task<List<RemoteResult>> CaptureRemoteAsync(RealmKeeperWorkspace workspace, CancellationToken token)
    {
        var results = new List<RemoteResult>();
        foreach (var endpoint in workspace.RemoteHosts.Where(x => x.Enabled))
        {
            try
            {
                if (!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
                    throw new InvalidOperationException("Base URL must be an absolute HTTP or HTTPS URL");
                using var client = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(Math.Clamp(endpoint.TimeoutSeconds, 1, 60)) };
                if (!string.IsNullOrWhiteSpace(endpoint.ApiTokenEnvironmentVariable))
                {
                    var tokenValue = Environment.GetEnvironmentVariable(endpoint.ApiTokenEnvironmentVariable);
                    if (string.IsNullOrWhiteSpace(tokenValue)) throw new InvalidOperationException($"Environment variable {endpoint.ApiTokenEnvironmentVariable} is not set");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
                }
                using var response = await client.GetAsync(endpoint.TelemetryPath, token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
                var root = doc.RootElement;
                var worlds = ReadInt(root, "worlds"); var population = ReadInt(root, "population"); var events = ReadInt(root, "activeEvents");
                var health = root.TryGetProperty("health", out var h) ? h.GetString() ?? "Connected" : "Connected";
                endpoint.Status = health; endpoint.LastContactUtc = DateTime.UtcNow.ToString("O"); endpoint.LastError = "";
                results.Add(new(endpoint.Name, true, worlds, population, events, health, ""));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                endpoint.Status = "Unavailable"; endpoint.LastContactUtc = DateTime.UtcNow.ToString("O"); endpoint.LastError = ex.Message;
                results.Add(new(endpoint.Name, false, 0, 0, 0, "Unavailable", ex.Message));
            }
        }
        return results;
    }

    private static int ReadInt(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private RealmKeeperTelemetrySnapshot Capture(CancellationToken token)
    {
        if (!Directory.Exists(_repositoryRoot))
        {
            return new(DateTime.UtcNow, false, 0, 0, 0, 0,
                "Repository root unavailable; local realm health cannot be measured.",
                "No local activity source is available.", _repositoryRoot, []);
        }

        token.ThrowIfCancellationRequested();
        var dataRoot = Path.Combine(_repositoryRoot, "Data");
        var worldDirectories = Directory.Exists(dataRoot)
            ? Directory.EnumerateDirectories(dataRoot)
                .Where(path => !Path.GetFileName(path).Equals("CompilerSmokeTest", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];
        var coveredWorlds = worldDirectories.Count;
        var generatedRecords = worldDirectories.Sum(CountGeneratedRecords);

        var savesRoot = Path.Combine(_repositoryRoot, "Saves");
        var saveFiles = Directory.Exists(savesRoot)
            ? Directory.EnumerateFiles(savesRoot, "*", SearchOption.AllDirectories).ToList()
            : [];

        var validator = Path.Combine(_repositoryRoot, "Reports", "RuntimeVerification", "validator_latest.md");
        var buildReports = Directory.Exists(Path.Combine(_repositoryRoot, "Release", "MajorMudReforged"))
            ? Directory.EnumerateFiles(Path.Combine(_repositoryRoot, "Release", "MajorMudReforged"), "BUILD_REPORT*.txt").ToList()
            : [];
        var validatorSummary = ReadHealthMarker(validator);
        var latestBuild = buildReports.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        var buildSummary = latestBuild is null ? "No build report" : ReadHealthMarker(latestBuild);

        var observedFiles = new List<string>();
        observedFiles.AddRange(saveFiles);
        if (File.Exists(validator)) observedFiles.Add(validator);
        observedFiles.AddRange(buildReports);
        observedFiles.AddRange(FindRuntimeLogs());
        var latestActivity = observedFiles.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        var activitySummary = latestActivity is null
            ? "No save, validation, build, or runtime-log activity was found."
            : $"Latest local activity: {Path.GetRelativePath(_repositoryRoot, latestActivity)} at {File.GetLastWriteTime(latestActivity):g}.";

        var crashFiles = FindCrashEvidence().ToList();
        var health = $"Local host available. Worlds: {coveredWorlds}; generated records sampled: {generatedRecords:N0}; save files: {saveFiles.Count}. Validator: {validatorSummary}. Build: {buildSummary}. Crash evidence: {crashFiles.Count}.";
        var timeline = new List<string>
        {
            $"{DateTime.Now:g} — local telemetry refreshed",
            $"Generated data: {coveredWorlds} world folder(s), {generatedRecords:N0} sampled records",
            $"Persistence: {saveFiles.Count} save/session file(s)",
            $"Health gates: validator {validatorSummary}; build {buildSummary}"
        };
        if (crashFiles.Count > 0)
            timeline.Add($"Attention: {crashFiles.Count} crash/error evidence file(s) detected");

        return new(DateTime.UtcNow, true, 1, coveredWorlds, generatedRecords, saveFiles.Count,
            health, activitySummary,
            "Local repository, generated Data, Saves, RuntimeVerification reports, Release build reports, and runtime logs",
            timeline);
    }

    private int CountGeneratedRecords(string worldDirectory)
    {
        var total = 0;
        foreach (var name in CountedFiles)
        {
            var path = Path.Combine(worldDirectory, name);
            if (!File.Exists(path)) continue;
            try
            {
                total += ChunkedGeneratedData.CountRecords(path);
            }
            catch (JsonException)
            {
                // A malformed generated file is surfaced by the project validator; telemetry skips it safely.
            }
        }
        return total;
    }

    private IEnumerable<string> FindRuntimeLogs()
    {
        foreach (var directory in new[] { "Logs", "Diagnostics", "Reports/RuntimeVerification", "Release/MajorMudReforged" })
        {
            var path = Path.Combine(_repositoryRoot, directory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(path)) continue;
            foreach (var file in Directory.EnumerateFiles(path, "*.log", SearchOption.AllDirectories)) yield return file;
            foreach (var file in Directory.EnumerateFiles(path, "*.txt", SearchOption.TopDirectoryOnly)) yield return file;
        }
    }

    private IEnumerable<string> FindCrashEvidence()
    {
        return FindRuntimeLogs().Where(path =>
        {
            var name = Path.GetFileName(path);
            return name.Contains("crash", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("error", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ReadHealthMarker(string path)
    {
        if (!File.Exists(path)) return "unavailable";
        try
        {
            var text = File.ReadAllText(path);
            if (text.Contains("PASS", StringComparison.OrdinalIgnoreCase)) return "PASS";
            if (text.Contains("FAIL", StringComparison.OrdinalIgnoreCase)) return "FAIL";
            if (text.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase)) return "BLOCKED";
            return "present";
        }
        catch (IOException)
        {
            return "locked";
        }
    }
}

public interface IAnalystTelemetryService
{
    Task<AnalystTelemetrySnapshot> CaptureAsync(AnalystWorkspace workspace, CancellationToken token = default);
}

public sealed class AnalystTelemetryService : IAnalystTelemetryService
{
    private readonly string _repositoryRoot;
    private readonly string _historyPath;
    private static readonly JsonSerializerOptions HistoryJson = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, string[]> TargetFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Combat"] = ["npcs.json", "monsters.json", "spells.json", "abilities.json", "items.json"],
        ["Economy"] = ["items.json", "shops.json", "economy.json"],
        ["Drops"] = ["items.json", "npcs.json", "monsters.json", "loot.json"],
        ["NPC Balance"] = ["npcs.json", "monsters.json", "spawns.json"],
        ["Quest Balance"] = ["quests.json", "scripts.json", "triggers.json", "items.json"],
        ["Experience Curve"] = ["classes.json", "races.json", "experience.json", "quests.json"],
        ["Item Balance"] = ["items.json", "weapons.json", "armor.json", "shops.json"],
        ["Progression"] = ["classes.json", "races.json", "experience.json", "quests.json", "items.json"]
    };

    public AnalystTelemetryService(string repositoryRoot)
    {
        _repositoryRoot = repositoryRoot;
        _historyPath = Path.Combine(repositoryRoot, "GuardianHall", "Analyst", "analysis-history.json");
    }

    public Task<AnalystTelemetrySnapshot> CaptureAsync(AnalystWorkspace workspace, CancellationToken token = default)
        => Task.Run(() => Capture(workspace, token), token);

    private AnalystTelemetrySnapshot Capture(AnalystWorkspace workspace, CancellationToken token)
    {
        var wanted = TargetFiles.TryGetValue(workspace.AnalysisTarget, out var names) ? names : TargetFiles["Combat"];
        var dataRoot = Path.Combine(_repositoryRoot, "Data");
        var files = Directory.Exists(dataRoot)
            ? Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.AllDirectories)
                .Where(path => wanted.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}CompilerSmokeTest{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];
        token.ThrowIfCancellationRequested();

        var records = 0;
        var familyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            var count = CountRecords(file);
            records += count;
            var family = Path.GetFileNameWithoutExtension(file);
            familyCounts[family] = familyCounts.GetValueOrDefault(family) + count;
        }

        var reportRoots = new[] { Path.Combine(_repositoryRoot, "Reports"), Path.Combine(_repositoryRoot, "GuardianHall", "Analyst", "Reports") };
        var reports = reportRoots.Where(Directory.Exists).SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Count(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        var now = DateTime.UtcNow;
        var history = LoadHistory();
        var previous = history.Where(x => x.Target.Equals(workspace.AnalysisTarget, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CapturedUtc).FirstOrDefault();
        history.Add(new AnalystHistoryRecord(now, workspace.AnalysisTarget, files.Count, records, familyCounts));
        history = history.OrderByDescending(x => x.CapturedUtc).Take(365).OrderBy(x => x.CapturedUtc).ToList();
        SaveHistory(history);

        var targetHistory = history.Where(x => x.Target.Equals(workspace.AnalysisTarget, StringComparison.OrdinalIgnoreCase)).ToList();
        var comparison = previous is null ? "Baseline captured; no earlier snapshot exists." : BuildComparison(previous, records, files.Count);
        var familyText = familyCounts.Count == 0 ? "No matching generated families" : string.Join(" · ", familyCounts.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value:N0}"));
        var chart = familyCounts.OrderByDescending(x => x.Value).Select(x => new AnalystChartSeries { Label = x.Key, Count = x.Value, Percent = records == 0 ? 0 : x.Value * 100d / records }).ToList();
        var graph = records == 0 ? "No graph series available for the selected target." : $"{chart.Count} rendered family series across {records:N0} generated records.";
        var trend = targetHistory.Count < 2 ? $"Historical baseline captured at {DateTime.Now:g}." : $"{targetHistory.Count} persisted snapshots. {comparison}";
        return new(now, files.Count > 0, files.Count, records, reports,
            $"Generated Data ({familyText}); Librarium remains evidence authority and is not modified by analysis.", graph, trend,
            $"Read-only scan of generated Data and persisted Analyst history for {workspace.AnalysisTarget}; no character control or runtime mutation.",
            targetHistory.Count, comparison, chart);
    }

    private List<AnalystHistoryRecord> LoadHistory()
    {
        try { return File.Exists(_historyPath) ? JsonSerializer.Deserialize<List<AnalystHistoryRecord>>(File.ReadAllText(_historyPath), HistoryJson) ?? [] : []; }
        catch (JsonException) { return []; }
        catch (IOException) { return []; }
    }

    private void SaveHistory(List<AnalystHistoryRecord> history)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
        var temp = _historyPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(history, HistoryJson));
        File.Move(temp, _historyPath, true);
    }

    private static string BuildComparison(AnalystHistoryRecord previous, int records, int files)
    {
        var recordDelta = records - previous.SourceRecords;
        var fileDelta = files - previous.SourceFiles;
        return $"Since {previous.CapturedUtc.ToLocalTime():g}: records {recordDelta:+#;-#;0}, files {fileDelta:+#;-#;0}.";
    }

    private static int CountRecords(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind == JsonValueKind.Array) return doc.RootElement.GetArrayLength();
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array) return records.GetArrayLength();
        }
        catch (JsonException) { }
        catch (IOException) { }
        return 0;
    }

    private sealed record AnalystHistoryRecord(DateTime CapturedUtc, string Target, int SourceFiles, int SourceRecords, Dictionary<string, int> FamilyCounts);
}


public interface IWorldsmithRuntimeService
{
    Task<WorldsmithRuntimeSnapshot> InspectAsync(WorldsmithWorkspace workspace, CancellationToken token = default);
    Task<string> GenerateProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default);
    Task<string> SimulateAsync(WorldsmithWorkspace workspace, CancellationToken token = default);
    Task<string> ApproveProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default);
    Task<WorldsmithApplyResult> ApplyApprovedProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default);
}


public sealed record WorldsmithApplyResult(string AppliedPath, string ReceiptPath, string BackupPath);

public sealed class WorldsmithRuntimeService : IWorldsmithRuntimeService
{
    private readonly string _repositoryRoot;
    private readonly string _workspaceRoot;
    private static readonly Dictionary<string, string[]> Families = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Rooms"] = ["rooms.json"], ["NPCs"] = ["npcs.json", "monsters.json"],
        ["Items"] = ["items.json", "weapons.json", "armor.json"],
        ["Quests"] = ["quests.json", "scripts.json", "triggers.json"],
        ["Encounters"] = ["spawns.json", "npcs.json", "monsters.json"],
        ["Dungeons"] = ["rooms.json", "spawns.json", "triggers.json"],
        ["World Events"] = ["events.json", "scripts.json", "triggers.json"]
    };

    public WorldsmithRuntimeService(string repositoryRoot, string guardianRoot)
    {
        _repositoryRoot = repositoryRoot;
        _workspaceRoot = Path.Combine(guardianRoot, "Worldsmith");
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "Proposals"));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "Simulations"));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "Approvals"));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "Applied"));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "Backups"));
    }

    public Task<WorldsmithRuntimeSnapshot> InspectAsync(WorldsmithWorkspace workspace, CancellationToken token = default)
        => Task.Run(() => Inspect(workspace, token), token);

    public async Task<string> GenerateProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default)
    {
        var snapshot = await InspectAsync(workspace, token);
        if (!snapshot.TargetIsSafe) throw new InvalidOperationException(snapshot.SafetySummary);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var safeName = string.Concat(workspace.BuildProject.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(_workspaceRoot, "Proposals", $"{stamp}-{safeName}.json");
        var proposal = new
        {
            schemaVersion = 2,
            generatedUtc = DateTime.UtcNow,
            status = "preview-only",
            approvalCode = CreateApprovalCode(workspace, snapshot),
            workspace.TargetTestRealm,
            workspace.BuildProject,
            workspace.ContentType,
            selectedContent = SelectedContent(workspace),
            workspace.Constraints,
            modes = new { workspace.ClassicSafe, workspace.Custom, workspace.AiWorld },
            workspace.ApprovalRequired,
            baseline = new { snapshot.BaselineFiles, snapshot.BaselineRecords, snapshot.SourceSummary },
            safeguards = new[] { "test-realm-only", "isolated-staging-only", "approval-gated", "canonical-id-preservation" }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(proposal, new JsonSerializerOptions { WriteIndented = true }), token);
        return path;
    }

    public async Task<string> SimulateAsync(WorldsmithWorkspace workspace, CancellationToken token = default)
    {
        var snapshot = await InspectAsync(workspace, token);
        if (!snapshot.TargetIsSafe) throw new InvalidOperationException(snapshot.SafetySummary);
        var path = Path.Combine(_workspaceRoot, "Simulations", $"Simulation-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var selected = string.Join(", ", SelectedContent(workspace));
        var text = $"# Worldsmith Simulation\n\nGenerated: {DateTime.Now:g}\n\nProject: {workspace.BuildProject}\n\nTarget: {workspace.TargetTestRealm}\n\nContent: {selected}\n\nBaseline files: {snapshot.BaselineFiles}\n\nBaseline records: {snapshot.BaselineRecords}\n\nConstraints: {workspace.Constraints}\n\nResult: SAFE PREVIEW. No live or generated repository data was modified. Approval required: {workspace.ApprovalRequired}.\n";
        await File.WriteAllTextAsync(path, text, token);
        return path;
    }

    public async Task<string> ApproveProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(workspace.LastProposalPath) || !File.Exists(workspace.LastProposalPath))
            throw new InvalidOperationException("Generate a proposal before approval.");
        var snapshot = await InspectAsync(workspace, token);
        if (!snapshot.TargetIsSafe) throw new InvalidOperationException(snapshot.SafetySummary);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(workspace.LastProposalPath, token));
        var expected = document.RootElement.GetProperty("approvalCode").GetString() ?? "";
        if (!string.Equals(expected, workspace.ApprovalCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval code does not match the generated proposal.");
        var approvalPath = Path.Combine(_workspaceRoot, "Approvals", $"Approved-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var approval = new
        {
            schemaVersion = 1,
            approvedUtc = DateTime.UtcNow,
            proposalPath = Path.GetFullPath(workspace.LastProposalPath),
            proposalSha256 = ComputeSha256(workspace.LastProposalPath),
            targetRealm = workspace.TargetTestRealm,
            approvalCode = expected,
            status = "approved-for-isolated-test-realm-staging",
            safeguards = new[] { "no-live-realm", "hash-bound-approval", "receipt-required", "rollback-copy-required" }
        };
        await File.WriteAllTextAsync(approvalPath, JsonSerializer.Serialize(approval, new JsonSerializerOptions { WriteIndented = true }), token);
        workspace.ApprovedProposalPath = workspace.LastProposalPath;
        return approvalPath;
    }

    public async Task<WorldsmithApplyResult> ApplyApprovedProposalAsync(WorldsmithWorkspace workspace, CancellationToken token = default)
    {
        if (!workspace.ApprovalRequired) throw new InvalidOperationException("Approval Required must remain enabled for apply.");
        var snapshot = await InspectAsync(workspace, token);
        if (!snapshot.TargetIsSafe) throw new InvalidOperationException(snapshot.SafetySummary);
        if (string.IsNullOrWhiteSpace(workspace.ApprovedProposalPath) || !File.Exists(workspace.ApprovedProposalPath))
            throw new InvalidOperationException("No approved proposal is available.");
        if (!string.Equals(Path.GetFullPath(workspace.ApprovedProposalPath), Path.GetFullPath(workspace.LastProposalPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The current proposal differs from the approved proposal.");

        var realmName = Sanitize(workspace.TargetTestRealm);
        var projectName = Sanitize(workspace.BuildProject);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var targetRoot = Path.Combine(_workspaceRoot, "Applied", realmName, projectName);
        var backupRoot = Path.Combine(_workspaceRoot, "Backups", realmName, projectName, stamp);
        Directory.CreateDirectory(targetRoot);
        Directory.CreateDirectory(backupRoot);
        var appliedPath = Path.Combine(targetRoot, "approved-proposal.json");
        if (File.Exists(appliedPath)) File.Copy(appliedPath, Path.Combine(backupRoot, "approved-proposal.json"), true);
        File.Copy(workspace.ApprovedProposalPath, appliedPath, true);
        var receiptPath = Path.Combine(targetRoot, $"apply-receipt-{stamp}.json");
        var receipt = new
        {
            schemaVersion = 1,
            appliedUtc = DateTime.UtcNow,
            targetRealm = workspace.TargetTestRealm,
            project = workspace.BuildProject,
            sourceProposal = Path.GetFullPath(workspace.ApprovedProposalPath),
            sourceSha256 = ComputeSha256(workspace.ApprovedProposalPath),
            appliedPath,
            backupPath = backupRoot,
            status = "staged-to-isolated-test-realm-workspace",
            runtimeMutation = false,
            librariumMutation = false
        };
        await File.WriteAllTextAsync(receiptPath, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }), token);
        return new(appliedPath, receiptPath, backupRoot);
    }

    private static string CreateApprovalCode(WorldsmithWorkspace workspace, WorldsmithRuntimeSnapshot snapshot)
    {
        var material = $"{workspace.TargetTestRealm}|{workspace.BuildProject}|{workspace.ContentType}|{snapshot.BaselineFiles}|{snapshot.BaselineRecords}|{DateTime.UtcNow:yyyyMMddHHmm}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash)[..8];
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sanitize(string value)
    {
        var cleaned = string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(cleaned) ? "Unnamed" : cleaned.Trim();
    }

    private WorldsmithRuntimeSnapshot Inspect(WorldsmithWorkspace workspace, CancellationToken token)
    {
        var safe = workspace.TargetTestRealm.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                   workspace.TargetTestRealm.Contains("Development", StringComparison.OrdinalIgnoreCase) ||
                   workspace.TargetTestRealm.Contains("Offline", StringComparison.OrdinalIgnoreCase);
        var wanted = Families.TryGetValue(workspace.ContentType, out var names) ? names : Families["Rooms"];
        var dataRoot = Path.Combine(_repositoryRoot, "Data");
        var files = Directory.Exists(dataRoot) ? Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.AllDirectories)
            .Where(x => wanted.Contains(Path.GetFileName(x), StringComparer.OrdinalIgnoreCase))
            .Where(x => !x.Contains($"{Path.DirectorySeparatorChar}CompilerSmokeTest{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)).ToList() : [];
        var records = 0;
        foreach (var file in files) { token.ThrowIfCancellationRequested(); records += CountRecords(file); }
        var safety = safe ? "Target is isolated for proposal and simulation output; no apply path is enabled." : "Blocked: Worldsmith requires a Test, Development, or Offline Projection realm.";
        return new(DateTime.UtcNow, safe, files.Count, records, $"Generated Data baseline for {workspace.ContentType}", safety);
    }

    private static IReadOnlyList<string> SelectedContent(WorldsmithWorkspace w)
    {
        var values = new List<string>();
        if (w.Rooms) values.Add("Rooms"); if (w.Npcs) values.Add("NPCs"); if (w.Items) values.Add("Items");
        if (w.Quests) values.Add("Quests"); if (w.Encounters) values.Add("Encounters"); if (w.Dungeons) values.Add("Dungeons"); if (w.WorldEvents) values.Add("World Events");
        if (values.Count == 0) values.Add(w.ContentType);
        return values;
    }

    private static int CountRecords(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind == JsonValueKind.Array) return doc.RootElement.GetArrayLength();
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array) return records.GetArrayLength();
        }
        catch (JsonException) { } catch (IOException) { }
        return 0;
    }
}

public interface IWandererTelemetryService
{
    Task<WandererTelemetrySnapshot> CaptureAsync(WandererWorkspace workspace, CancellationToken token = default);
    Task<string> ExecuteMissionStepAsync(WandererWorkspace workspace, CancellationToken token = default);
    Task<string> ExecuteAutonomousStepAsync(WandererWorkspace workspace, CancellationToken token = default);
}

public sealed class WandererTelemetryService : IWandererTelemetryService
{
    private readonly string _repositoryRoot;
    private readonly IClientSessionService? _sessions;
    public WandererTelemetryService(string repositoryRoot, IClientSessionService? sessions = null)
    {
        _repositoryRoot = repositoryRoot;
        _sessions = sessions;
    }

    public async Task<WandererTelemetrySnapshot> CaptureAsync(WandererWorkspace workspace, CancellationToken token = default)
    {
        var inProcess = CaptureInProcessSession(workspace);
        if (inProcess is not null) return inProcess;

        if (workspace.LiveConnectorEnabled)
        {
            try { return await CaptureRemoteAsync(workspace, token); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var local = await Task.Run(() => CaptureLocal(workspace, token), token);
                return local with { SourceSummary = $"HTTP Guardian endpoint unavailable: {DescribeConnectorFailure(ex)}. Fallback: {local.SourceSummary}" };
            }
        }
        return await Task.Run(() => CaptureLocal(workspace, token), token);
    }

    public async Task<string> ExecuteMissionStepAsync(WandererWorkspace workspace, CancellationToken token = default)
    {
        if (!workspace.AllowMissionDispatch) throw new InvalidOperationException("Mission dispatch is not explicitly enabled");
        if (workspace.Permission < GuardianPermission.Interact) throw new InvalidOperationException("Wanderer permission does not allow interaction");

        var session = FindInProcessSession(workspace, connectedOnly: true);
        if (session is not null && _sessions is not null)
        {
            var command = ExtractUserDirectedLocalCommand(workspace.Mission);
            if (command is null)
                throw new InvalidOperationException("The local Wanderer session is connected, but Mission must contain one single-line game command before dispatch.");
            await _sessions.SendCommandAsync(session.SessionId, command, token);
            workspace.LastMissionDispatch = DateTime.UtcNow.ToString("O");
            return $"Wanderer sent user-directed local mission step '{command}' to {session.DisplayName}";
        }

        if (!workspace.LiveConnectorEnabled)
            throw new InvalidOperationException("No matching connected Play session was found and the optional HTTP Guardian connector is disabled");
        if (!Uri.TryCreate(workspace.SessionBaseUrl, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Session base URL must be an absolute HTTP or HTTPS Guardian API URL");
        try
        {
            using var client = CreateClient(workspace, baseUri);
            var payload = JsonSerializer.Serialize(new
            {
                server = workspace.Server, world = workspace.World, realm = workspace.Realm, character = workspace.Character,
                mission = workspace.Mission, explorationStyle = workspace.ExplorationStyle, workspace.CombatPolicy,
                workspace.RetreatPolicy, workspace.LootPolicy, workspace.Questing, workspace.MapDiscovery,
                requestedUtc = DateTime.UtcNow
            });
            using var response = await client.PostAsync(workspace.MissionStepPath, new StringContent(payload, Encoding.UTF8, "application/json"), token);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync(token);
            workspace.LastMissionDispatch = DateTime.UtcNow.ToString("O");
            return string.IsNullOrWhiteSpace(result) ? "Mission step accepted by live session" : result.Trim();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(DescribeConnectorFailure(ex), ex);
        }
    }


    public async Task<string> ExecuteAutonomousStepAsync(WandererWorkspace workspace, CancellationToken token = default)
    {
        if (!workspace.AutonomousRunning) return "Autonomous play is paused";
        if (!workspace.AllowMissionDispatch) throw new InvalidOperationException("Enable guarded mission-step dispatch before autonomous play");
        if (workspace.Permission < GuardianPermission.Interact) throw new InvalidOperationException("Wanderer permission must be Interact or higher");
        var session = FindInProcessSession(workspace, connectedOnly: true)
            ?? throw new InvalidOperationException("No matching connected Play session is available");
        if (_sessions is null) throw new InvalidOperationException("Client session service is unavailable");

        var retreatPercent = ParsePercent(workspace.RetreatPolicy, 30);
        string? command = null;
        string reason;

        if (workspace.AutonomousMaxActions > 0 && workspace.AutonomousActionCount >= workspace.AutonomousMaxActions)
        {
            workspace.AutonomousRunning = false;
            return $"Autonomous action limit {workspace.AutonomousMaxActions} reached";
        }

        if (session.InCombat)
        {
            if (session.HealthPercent <= retreatPercent)
            {
                command = "flee";
                reason = $"retreat policy triggered at {session.HealthPercent}% HP";
            }
            else if (workspace.AutonomousCombat)
            {
                command = "attack";
                reason = "continue current combat";
            }
            else return "Combat detected; autonomous combat is disabled";
        }
        else if (workspace.AutonomousRecovery && (session.HealthPercent <= retreatPercent || session.ManaPercent <= 25))
        {
            command = session.IsResting ? null : "rest";
            reason = $"recover HP {session.HealthPercent}% / MP {session.ManaPercent}%";
            if (command is null) return reason + "; already resting";
        }
        else if (workspace.AutonomousLoot && RecentTextContains(session, "is dead", "you killed", "corpse"))
        {
            command = "get all";
            reason = "loot policy after a defeated enemy";
        }
        else if (workspace.AutonomousExplore)
        {
            command = ChooseExit(session, workspace);
            reason = command is null ? "request room information before choosing an exit" : "explore a reported room exit";
            command ??= "look";
        }
        else
        {
            command = "look";
            reason = "remain observant while exploration is disabled";
        }

        await _sessions.SendCommandAsync(session.SessionId, command, token);
        workspace.AutonomousActionCount++;
        workspace.LastAutonomousAction = $"{DateTime.Now:g} — {command} ({reason})";
        workspace.AutonomousStatus = $"Running · action {workspace.AutonomousActionCount:N0} · {reason}";
        workspace.AutonomousHistory.Insert(0, workspace.LastAutonomousAction);
        while (workspace.AutonomousHistory.Count > 50) workspace.AutonomousHistory.RemoveAt(workspace.AutonomousHistory.Count - 1);
        return $"Wanderer sent '{command}': {reason}";
    }

    private static int ParsePercent(string text, int fallback)
    {
        var match = Regex.Match(text ?? string.Empty, @"(\d{1,3})\s*%");
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? Math.Clamp(value, 1, 99) : fallback;
    }

    private static bool RecentTextContains(ClientSessionConnection session, params string[] needles)
    {
        return session.TerminalEntries.TakeLast(12).Any(e => !e.IsOutgoing && needles.Any(n => e.Text.Contains(n, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? ChooseExit(ClientSessionConnection session, WandererWorkspace workspace)
    {
        var exitLine = session.TerminalEntries.TakeLast(30).Reverse()
            .FirstOrDefault(e => !e.IsOutgoing && Regex.IsMatch(e.Text, @"\bExits?\s*:", RegexOptions.IgnoreCase))?.Text;
        if (string.IsNullOrWhiteSpace(exitLine)) return null;
        var colon = exitLine.IndexOf(':');
        if (colon < 0) return null;
        var candidates = Regex.Matches(exitLine[(colon + 1)..], @"\b(?:north|south|east|west|northeast|northwest|southeast|southwest|up|down|n|s|e|w|ne|nw|se|sw|u|d)\b", RegexOptions.IgnoreCase)
            .Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();
        if (candidates.Count == 0) return null;
        var recent = workspace.AutonomousHistory.Take(8).ToList();
        var unvisited = candidates.FirstOrDefault(x => !recent.Any(h => h.Contains($"— {x} ", StringComparison.OrdinalIgnoreCase)));
        return unvisited ?? candidates[workspace.AutonomousActionCount % candidates.Count];
    }

    private WandererTelemetrySnapshot? CaptureInProcessSession(WandererWorkspace workspace)
    {
        var session = FindInProcessSession(workspace, connectedOnly: false);
        if (session is null) return null;
        var connected = session.ConnectionState == ClientConnectionState.Connected;
        var route = !string.IsNullOrWhiteSpace(session.ActiveRouteName)
            ? $"{session.ActiveRouteName}: {session.RouteProgress}"
            : !string.IsNullOrWhiteSpace(session.CurrentRoomName) ? session.CurrentRoomName : session.ConnectionMessage;
        var queue = new List<string>
        {
            connected ? "Connected Play session is available for guarded mission steps" : $"Play session state: {session.ConnectionState}"
        };
        return new(DateTime.UtcNow, connected, 0, session.TerminalEntries.Count, session.InventoryItems.Count,
            route, session.InventorySummary, $"{session.VitalSummary} • {session.ExperienceSummary}",
            $"In-process Play session: {session.DisplayName} ({session.HostName}:{session.Port})", queue);
    }

    private ClientSessionConnection? FindInProcessSession(WandererWorkspace workspace, bool connectedOnly)
    {
        if (_sessions is null) return null;
        var candidates = _sessions.Sessions.AsEnumerable();
        if (connectedOnly) candidates = candidates.Where(x => x.ConnectionState == ClientConnectionState.Connected);
        var character = workspace.Character?.Trim() ?? string.Empty;
        var world = workspace.World?.Trim() ?? string.Empty;
        return candidates
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(character) && string.Equals(x.CharacterName, character, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(world) && string.Equals(x.WorldName, world, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.ConnectionState == ClientConnectionState.Connected)
            .FirstOrDefault(x => string.IsNullOrWhiteSpace(character) || string.Equals(x.CharacterName, character, StringComparison.OrdinalIgnoreCase) || string.Equals(x.DisplayName, character, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractUserDirectedLocalCommand(string mission)
    {
        if (string.IsNullOrWhiteSpace(mission)) return null;
        var firstLine = mission.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(firstLine)) return null;
        return firstLine.IndexOfAny(['\0', '\u001b']) >= 0 ? null : firstLine;
    }

    private static string DescribeConnectorFailure(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("invalid status line", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Reforged Realms Host", StringComparison.OrdinalIgnoreCase))
            return "The configured address is the MajorMUD/Telnet game port, not an HTTP Guardian API endpoint. Use the connected Play session for local dispatch, or configure a separate HTTP Guardian endpoint.";
        return message;
    }

    private async Task<WandererTelemetrySnapshot> CaptureRemoteAsync(WandererWorkspace workspace, CancellationToken token)
    {
        if (!Uri.TryCreate(workspace.SessionBaseUrl, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Session base URL must be an absolute HTTP or HTTPS URL");
        using var client = CreateClient(workspace, baseUri);
        var path = workspace.SessionTelemetryPath + (workspace.SessionTelemetryPath.Contains('?') ? "&" : "?") +
            "character=" + Uri.EscapeDataString(workspace.Character) + "&world=" + Uri.EscapeDataString(workspace.World) + "&realm=" + Uri.EscapeDataString(workspace.Realm);
        using var response = await client.GetAsync(path, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var root = doc.RootElement;
        var connected = root.TryGetProperty("connected", out var c) && c.ValueKind == JsonValueKind.True;
        var rooms = ReadInt(root, "discoveredRooms");
        var journals = ReadInt(root, "journalEntries");
        var inventoryCount = ReadInt(root, "inventoryItems");
        var route = ReadString(root, "currentRoute", connected ? "Connected; no route reported" : "Session not connected");
        var inventory = ReadString(root, "inventorySummary", $"{inventoryCount} live inventory item(s)");
        var progress = ReadString(root, "progress", "Live progress unavailable");
        var queue = new List<string>();
        if (root.TryGetProperty("missionQueue", out var missions) && missions.ValueKind == JsonValueKind.Array)
            queue.AddRange(missions.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (queue.Count == 0) queue.Add(connected ? "Continue current live mission" : "Connect the assigned live character session");
        return new(DateTime.UtcNow, connected, rooms, journals, inventoryCount, route, inventory, progress,
            "Authenticated live Wanderer session telemetry", queue);
    }

    private HttpClient CreateClient(WandererWorkspace workspace, Uri baseUri)
    {
        var client = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(Math.Clamp(workspace.SessionTimeoutSeconds, 1, 60)) };
        if (!string.IsNullOrWhiteSpace(workspace.ApiTokenEnvironmentVariable))
        {
            var tokenValue = Environment.GetEnvironmentVariable(workspace.ApiTokenEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(tokenValue)) { client.Dispose(); throw new InvalidOperationException($"Environment variable {workspace.ApiTokenEnvironmentVariable} is not set"); }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
        }
        return client;
    }

    private static int ReadInt(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
    private static string ReadString(JsonElement root, string name, string fallback)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private WandererTelemetrySnapshot CaptureLocal(WandererWorkspace workspace, CancellationToken token)
    {
        var savesRoot = Path.Combine(_repositoryRoot, "Saves");
        var dataRoot = Path.Combine(_repositoryRoot, "Data");
        var sessionFiles = Directory.Exists(savesRoot) ? Directory.EnumerateFiles(savesRoot, "*", SearchOption.AllDirectories).ToList() : [];
        token.ThrowIfCancellationRequested();
        var characterMatches = sessionFiles.Where(path => Path.GetFileNameWithoutExtension(path).Contains(workspace.Character, StringComparison.OrdinalIgnoreCase) || path.Contains(workspace.Character, StringComparison.OrdinalIgnoreCase)).ToList();
        var activeFiles = characterMatches.Count > 0 ? characterMatches : sessionFiles;
        var journals = activeFiles.Count(path => Path.GetFileName(path).Contains("journal", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".log", StringComparison.OrdinalIgnoreCase));
        var inventories = activeFiles.Count(path => Path.GetFileName(path).Contains("inventory", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Contains("character", StringComparison.OrdinalIgnoreCase));
        var roomFiles = Directory.Exists(dataRoot) ? Directory.EnumerateFiles(dataRoot, "rooms.json", SearchOption.AllDirectories).ToList() : [];
        var exitFiles = Directory.Exists(dataRoot) ? Directory.EnumerateFiles(dataRoot, "exits.json", SearchOption.AllDirectories).ToList() : [];
        var discoveredRooms = roomFiles.Sum(path => ChunkedGeneratedData.CountRecords(path));
        var knownExits = exitFiles.Sum(path => ChunkedGeneratedData.CountRecords(path));
        var latest = activeFiles.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        var route = latest is null ? "No active character session detected" : $"Latest session evidence: {Path.GetRelativePath(_repositoryRoot, latest)}";
        var inventory = inventories == 0 ? "No character inventory snapshot detected" : $"{inventories} inventory/character persistence file(s) available";
        var progress = discoveredRooms == 0 ? "No generated room corpus available" : $"{discoveredRooms:N0} rooms and {knownExits:N0} exits available to exploration telemetry";
        var queue = new List<string>();
        if (latest is null) queue.Add("Connect or create the assigned character session");
        if (workspace.MapDiscovery && discoveredRooms > 0) queue.Add($"Compare player discovery against {discoveredRooms:N0} known rooms and {knownExits:N0} exits");
        if (workspace.AutoJournal && journals == 0) queue.Add("Create the first expedition journal entry");
        if (queue.Count == 0) queue.Add("Continue current exploration mission");
        return new(DateTime.UtcNow, latest is not null, discoveredRooms, journals, inventories, route, inventory, progress,
            "Local Saves character/session evidence plus chunk-aware generated room and exit records", queue);
    }
}

internal static class ChunkedGeneratedData
{
    public static int CountRecords(string manifestOrDataPath)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return CountRecords(manifestOrDataPath, visited);
    }

    private static int CountRecords(string path, HashSet<string> visited)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) || !visited.Add(path)) return 0;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array) return root.GetArrayLength();
            if (root.ValueKind != JsonValueKind.Object) return 0;
            if (root.TryGetProperty("record_parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            {
                var baseDirectory = Path.GetDirectoryName(path) ?? string.Empty;
                var total = 0;
                foreach (var part in parts.EnumerateArray())
                {
                    var relative = part.GetString();
                    if (string.IsNullOrWhiteSpace(relative)) continue;
                    total += CountRecords(Path.Combine(baseDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), visited);
                }
                return total;
            }
            return root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array
                ? records.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}

public sealed class ArchivistService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
        { ".wcc", ".wbb", ".dll", ".json", ".xml", ".csv", ".txt", ".mdb", ".bin" };
    private readonly string _root;
    public ArchivistService(string root) { _root = Path.Combine(root, "Archivist"); Directory.CreateDirectory(_root); }

    public IEnumerable<DecodeQueueItem> QueueFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(File.Exists))
        {
            var ext = Path.GetExtension(path);
            yield return new DecodeQueueItem { Path = path, FileName = Path.GetFileName(path), Type = ext.TrimStart('.').ToUpperInvariant(), Status = Supported.Contains(ext) ? "Queued" : "Unsupported", Confidence = EvidenceConfidence.Unknown };
        }
    }

    public async Task DecodeAsync(ArchivistWorkspace workspace, IEnumerable<DecodeQueueItem> selected, CancellationToken token = default)
    {
        var items = selected.Where(x => x.Status != "Unsupported").Distinct().ToList();
        if (items.Count == 0) throw new InvalidOperationException("No supported evidence is selected for decoding.");
        var decodedRoot = Path.Combine(_root, "Decoded"); Directory.CreateDirectory(decodedRoot);
        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                item.Status = "Inspecting";
                var info = new FileInfo(item.Path);
                item.SizeBytes = info.Length;
                await using (var stream = File.OpenRead(item.Path))
                    item.Sha256 = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
                item.Findings = await InspectFileAsync(item.Path, item.Type, token);
                item.Status = "Decoded";
                item.Confidence = EvidenceConfidence.Proven;
                var relationships = FindRelationships(workspace, item);
                workspace.EvidenceTree.Add(new EvidenceNode
                {
                    Name = item.FileName,
                    Source = $"{item.Path} | SHA-256 {item.Sha256}",
                    Confidence = EvidenceConfidence.Proven,
                    Relationships = relationships
                });
                var artifact = Path.Combine(decodedRoot, Sanitize(item.FileName) + ".evidence.json");
                await File.WriteAllTextAsync(artifact, JsonSerializer.Serialize(new
                {
                    source = item.Path, item.FileName, item.Type, item.SizeBytes, item.Sha256,
                    item.Findings, confidence = item.Confidence.ToString(), relationships,
                    decodedUtc = DateTime.UtcNow, librariumAction = "preview-only"
                }, new JsonSerializerOptions { WriteIndented = true }), token);
                workspace.LastEvidencePath = artifact;
            }
            catch (Exception ex)
            {
                item.Status = "Failed";
                item.Findings = ex.Message;
            }
        }
        workspace.DecodedFiles = workspace.DecodeQueue.Count(x => x.Status == "Decoded");
        workspace.FailedFiles = workspace.DecodeQueue.Count(x => x.Status == "Failed");
        workspace.DecodeStatus = $"Decoded {workspace.DecodedFiles} file(s); {workspace.FailedFiles} failed. Evidence remains preview-only.";
        workspace.LibrariumPreview = BuildPreview(workspace);
        workspace.RelationshipViewer = BuildRelationships(workspace);
        workspace.GeneratedKnowledge = workspace.LastEvidencePath.Length == 0 ? "No evidence artifact generated" : $"Latest evidence artifact: {workspace.LastEvidencePath}";
    }

    public IReadOnlyList<EvidenceSearchResult> SearchEvidence(ArchivistWorkspace workspace, string? query)
    {
        var terms = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<EvidenceSearchResult>();

        foreach (var item in workspace.DecodeQueue.Where(x => x.Status == "Decoded"))
        {
            var searchable = $"{item.FileName} {item.Path} {item.Type} {item.Findings} {item.Sha256}";
            if (!Matches(searchable, terms)) continue;
            var node = workspace.EvidenceTree.FirstOrDefault(x => string.Equals(x.Name, item.FileName, StringComparison.OrdinalIgnoreCase));
            results.Add(new EvidenceSearchResult
            {
                Name = item.FileName,
                Source = item.Path,
                Type = item.Type,
                Confidence = item.Confidence,
                MatchSummary = HighlightSummary(item.Findings, terms),
                Relationships = node?.Relationships ?? "No indexed relationships",
                Sha256 = item.Sha256
            });
        }

        foreach (var node in workspace.EvidenceTree)
        {
            if (results.Any(x => string.Equals(x.Name, node.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Source, node.Source, StringComparison.OrdinalIgnoreCase))) continue;
            var searchable = $"{node.Name} {node.Source} {node.Relationships} {node.Confidence}";
            if (!Matches(searchable, terms)) continue;
            results.Add(new EvidenceSearchResult
            {
                Name = node.Name,
                Source = node.Source,
                Type = "Evidence node",
                Confidence = node.Confidence,
                MatchSummary = HighlightSummary(node.Source, terms),
                Relationships = node.Relationships
            });
        }

        return results
            .OrderByDescending(x => x.Confidence == EvidenceConfidence.Proven)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(250)
            .ToList();
    }

    public string BuildRelationshipDetail(ArchivistWorkspace workspace, EvidenceSearchResult result)
    {
        var reverse = workspace.EvidenceTree
            .Where(x => x.Relationships.Contains(result.Name, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var reverseText = reverse.Length == 0 ? "No reverse references" : $"Referenced by: {string.Join(", ", reverse)}";
        return $"{result.Name}\nType: {result.Type}\nConfidence: {result.Confidence}\nSource: {result.Source}\nSHA-256: {(string.IsNullOrWhiteSpace(result.Sha256) ? "not recorded" : result.Sha256)}\nRelationships: {result.Relationships}\n{reverseText}\nMatch: {result.MatchSummary}";
    }

    private static bool Matches(string value, string[] terms) => terms.Length == 0 || terms.All(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string HighlightSummary(string value, string[] terms)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Indexed evidence match";
        var first = terms.Select(t => value.IndexOf(t, StringComparison.OrdinalIgnoreCase)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, first - 60);
        var length = Math.Min(220, value.Length - start);
        return value.Substring(start, length).ReplaceLineEndings(" ");
    }

    public async Task<string> SaveEvidenceAsync(ArchivistWorkspace workspace)
    {
        var evidence = Path.Combine(_root, "SavedEvidence"); Directory.CreateDirectory(evidence);
        var path = Path.Combine(evidence, $"Evidence-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            savedUtc = DateTime.UtcNow,
            previewOnly = true,
            queue = workspace.DecodeQueue,
            evidence = workspace.EvidenceTree,
            relationships = workspace.RelationshipViewer,
            librariumPreview = workspace.LibrariumPreview
        }, new JsonSerializerOptions { WriteIndented = true }));
        workspace.LastEvidencePath = path;
        workspace.SavedEvidenceCount++;
        return path;
    }

    private static async Task<string> InspectFileAsync(string path, string type, CancellationToken token)
    {
        var ext = type.ToUpperInvariant();
        if (ext == "JSON")
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            var root = doc.RootElement;
            var count = root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() : root.ValueKind == JsonValueKind.Object ? root.EnumerateObject().Count() : 1;
            return $"Valid JSON; root={root.ValueKind}; top-level entries={count}.";
        }
        if (ext == "XML")
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.None);
            return $"Valid XML; root={doc.Root?.Name.LocalName ?? "none"}; descendants={doc.Descendants().Count()}.";
        }
        if (ext == "CSV")
        {
            var lines = await ReadLinesCappedAsync(path, 10000, token);
            var columns = lines.Count == 0 ? 0 : lines[0].Split(',').Length;
            return $"CSV/text table; sampled rows={lines.Count}; header columns={columns}.";
        }
        if (ext is "TXT" or "WCC" or "WBB")
        {
            var bytes = await ReadBytesCappedAsync(path, 1024 * 1024, token);
            var printable = bytes.Length == 0 ? 0 : bytes.Count(b => b is 9 or 10 or 13 || b is >= 32 and <= 126) * 100.0 / bytes.Length;
            var strings = ExtractStrings(bytes).Take(20).ToArray();
            return $"Sampled {bytes.Length:N0} bytes; printable={printable:F1}%; strings={strings.Length}: {string.Join(" | ", strings)}";
        }
        if (ext == "DLL")
        {
            var bytes = await ReadBytesCappedAsync(path, 4096, token);
            var mz = bytes.Length >= 2 && bytes[0] == (byte)'M' && bytes[1] == (byte)'Z';
            return $"Portable executable header={(mz ? "MZ present" : "not detected")}; sampled {bytes.Length} bytes. Native analysis requires a registered local tool.";
        }
        if (ext is "MDB" or "BIN")
        {
            var bytes = await ReadBytesCappedAsync(path, 65536, token);
            var strings = ExtractStrings(bytes).Take(20).ToArray();
            return $"Binary evidence; sampled {bytes.Length:N0} bytes; extracted strings={strings.Length}: {string.Join(" | ", strings)}";
        }
        return "File metadata captured; no safe built-in decoder is registered for this type.";
    }

    private static async Task<byte[]> ReadBytesCappedAsync(string path, int cap, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        var length = (int)Math.Min(stream.Length, cap);
        var bytes = new byte[length];
        var read = 0;
        while (read < length) { var n = await stream.ReadAsync(bytes.AsMemory(read, length - read), token); if (n == 0) break; read += n; }
        return read == length ? bytes : bytes[..read];
    }

    private static async Task<List<string>> ReadLinesCappedAsync(string path, int cap, CancellationToken token)
    {
        var result = new List<string>();
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        while (result.Count < cap && await reader.ReadLineAsync(token) is { } line) result.Add(line);
        return result;
    }

    private static IEnumerable<string> ExtractStrings(byte[] bytes)
    {
        var sb = new StringBuilder();
        foreach (var b in bytes)
        {
            if (b is >= 32 and <= 126) sb.Append((char)b);
            else { if (sb.Length >= 5) yield return sb.ToString(); sb.Clear(); }
        }
        if (sb.Length >= 5) yield return sb.ToString();
    }

    private static string FindRelationships(ArchivistWorkspace workspace, DecodeQueueItem item)
    {
        var stem = Path.GetFileNameWithoutExtension(item.FileName);
        var related = workspace.DecodeQueue.Where(x => !ReferenceEquals(x, item) &&
            (Path.GetFileNameWithoutExtension(x.FileName).Equals(stem, StringComparison.OrdinalIgnoreCase) || x.Type == item.Type))
            .Select(x => x.FileName).Take(8).ToArray();
        return related.Length == 0 ? "No queue relationships detected" : string.Join(", ", related);
    }

    private static string BuildPreview(ArchivistWorkspace workspace) =>
        $"Preview only — {workspace.EvidenceTree.Count} evidence node(s), {workspace.DecodedFiles} decoded file(s), {workspace.DecodeQueue.Count(x => x.Confidence == EvidenceConfidence.Unknown)} unknown(s). No Librarium files were overwritten.";

    private static string BuildRelationships(ArchivistWorkspace workspace) =>
        string.Join(Environment.NewLine, workspace.EvidenceTree.TakeLast(20).Select(x => $"{x.Name} -> {x.Relationships}"));

    private static string Sanitize(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    public async Task<string> GenerateReportAsync(ArchivistWorkspace workspace, string format)
    {
        var reports = Path.Combine(_root, "Reports"); Directory.CreateDirectory(reports);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var summary = BuildSummary(workspace);
        string path;
        switch (format.ToUpperInvariant())
        {
            case "JSON":
                path = Path.Combine(reports, $"Archivist-Report-{stamp}.json");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
                {
                    reportVersion = "1.0", generatedUtc = DateTime.UtcNow, previewOnly = true,
                    investigationSummary = BuildInvestigationSummary(workspace),
                    decodedStructures = workspace.DecodeQueue,
                    evidence = workspace.EvidenceTree,
                    confidence = workspace.EvidenceTree.GroupBy(x => x.Confidence.ToString()).ToDictionary(x => x.Key, x => x.Count()),
                    relationships = workspace.EvidenceTree.Select(x => new { x.Name, x.Relationships }),
                    suggestedRuntime = "No runtime behavior is promoted automatically. Review and explicit approval are required.",
                    openQuestions = BuildOpenQuestions(workspace)
                }, new JsonSerializerOptions { WriteIndented = true }));
                break;
            case "HTML":
                path = Path.Combine(reports, $"Archivist-Report-{stamp}.html");
                await File.WriteAllTextAsync(path, BuildHtmlReport(workspace));
                break;
            case "PDF":
                path = Path.Combine(reports, $"Archivist-Report-{stamp}.pdf");
                await File.WriteAllBytesAsync(path, MinimalPdf(summary));
                break;
            default:
                path = Path.Combine(reports, $"Archivist-Report-{stamp}.md");
                await File.WriteAllTextAsync(path, "# Archivist Investigation Report\n\n" + summary);
                break;
        }
        workspace.LastReportPath = path;
        workspace.GeneratedKnowledge = $"{format} report generated: {path}";
        return path;
    }

    public async Task<string> ExportCodexAsync(ArchivistWorkspace workspace)
    {
        var exports = Path.Combine(_root, "CodexExports"); Directory.CreateDirectory(exports);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var staging = Path.Combine(exports, $"Codex-{stamp}"); Directory.CreateDirectory(staging);
        var summary = BuildSummary(workspace);
        await File.WriteAllTextAsync(Path.Combine(staging, "HANDOFF.md"), "# Codex Handoff\n\n" + summary);
        await File.WriteAllTextAsync(Path.Combine(staging, "INVESTIGATION_SUMMARY.md"), "# Investigation Summary\n\n" + BuildInvestigationSummary(workspace));
        await File.WriteAllTextAsync(Path.Combine(staging, "DECODED_STRUCTURES.json"), JsonSerializer.Serialize(workspace.DecodeQueue, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(staging, "EVIDENCE.json"), JsonSerializer.Serialize(workspace.EvidenceTree, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(staging, "RELATIONSHIPS.md"), "# Relationships\n\n" + BuildRelationships(workspace));
        await File.WriteAllTextAsync(Path.Combine(staging, "SUGGESTED_RUNTIME.md"), "# Suggested Runtime\n\nNo runtime behavior is promoted automatically. Review evidence and require explicit approval before implementation.\n");
        await File.WriteAllTextAsync(Path.Combine(staging, "OPEN_QUESTIONS.md"), "# Open Questions\n\n" + string.Join("\n", BuildOpenQuestions(workspace).Select(x => $"- {x}")));
        await File.WriteAllTextAsync(Path.Combine(staging, "manifest.json"), JsonSerializer.Serialize(new
        {
            packageVersion = "1.0", generatedUtc = DateTime.UtcNow, previewOnly = true,
            decodedFiles = workspace.DecodedFiles, failedFiles = workspace.FailedFiles,
            evidenceRecords = workspace.EvidenceTree.Count,
            files = Directory.EnumerateFiles(staging).Select(Path.GetFileName).OrderBy(x => x)
        }, new JsonSerializerOptions { WriteIndented = true }));
        var zip = Path.Combine(exports, $"Archivist-Codex-Package-{stamp}.zip");
        ZipFile.CreateFromDirectory(staging, zip); Directory.Delete(staging, true);
        workspace.LastCodexPath = zip;
        workspace.GeneratedKnowledge = $"Codex package generated: {zip}";
        return zip;
    }

    public ToolValidationResult ValidateTool(LocalToolRegistration tool)
    {
        if (!tool.Enabled) return new(false, "Tool is disabled.", "");
        if (string.IsNullOrWhiteSpace(tool.ExecutablePath)) return new(false, "Executable path is empty.", "");
        var executable = Environment.ExpandEnvironmentVariables(tool.ExecutablePath.Trim().Trim('"'));
        if (!File.Exists(executable)) return new(false, $"Executable was not found: {executable}", executable);
        var working = ResolveWorkingDirectory(tool, executable);
        if (!Directory.Exists(working)) return new(false, $"Working directory was not found: {working}", executable);
        return new(true, $"Ready: {Path.GetFileName(executable)} | Working directory: {working}", executable);
    }

    public string LaunchTool(LocalToolRegistration tool, string? selectedFile)
    {
        var validation = ValidateTool(tool);
        tool.ValidationStatus = validation.Message;
        tool.LastError = validation.IsValid ? "" : validation.Message;
        if (!validation.IsValid) throw new InvalidOperationException(validation.Message);
        var executable = validation.ResolvedExecutable;
        var working = ResolveWorkingDirectory(tool, executable);
        var file = selectedFile is not null && File.Exists(selectedFile) ? selectedFile : "";
        var folder = file.Length == 0 ? "" : Path.GetDirectoryName(file) ?? "";
        var args = (tool.Arguments ?? "")
            .Replace("{file}", Quote(file), StringComparison.OrdinalIgnoreCase)
            .Replace("{folder}", Quote(folder), StringComparison.OrdinalIgnoreCase)
            .Replace("{root}", Quote(_root), StringComparison.OrdinalIgnoreCase);
        var info = new ProcessStartInfo(executable, args)
        {
            UseShellExecute = true,
            WorkingDirectory = working,
            ErrorDialog = false
        };
        var process = Process.Start(info) ?? throw new InvalidOperationException("The operating system did not start the registered tool.");
        tool.LastLaunchUtc = DateTime.UtcNow.ToString("O");
        tool.LastError = "";
        tool.ValidationStatus = $"Launched process {process.Id} at {DateTime.Now:g}";
        return tool.ValidationStatus;
    }

    private static string ResolveWorkingDirectory(LocalToolRegistration tool, string executable) =>
        string.IsNullOrWhiteSpace(tool.WorkingDirectory)
            ? Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory
            : Environment.ExpandEnvironmentVariables(tool.WorkingDirectory.Trim().Trim('"'));

    private static string Quote(string value) => string.IsNullOrWhiteSpace(value) ? "" : $"\"{value.Replace("\"", "\\\"")}\"";

    private static string BuildInvestigationSummary(ArchivistWorkspace w) =>
        $"Queued {w.DecodeQueue.Count} file(s); decoded {w.DecodedFiles}; failed {w.FailedFiles}; evidence nodes {w.EvidenceTree.Count}; saved snapshots {w.SavedEvidenceCount}. All outputs are preview-only.";

    private static string[] BuildOpenQuestions(ArchivistWorkspace w)
    {
        var questions = w.DecodeQueue.Where(x => x.Status != "Decoded" || x.Confidence == EvidenceConfidence.Unknown)
            .Select(x => $"Decode or classify {x.FileName} ({x.Status}, {x.Confidence})").ToList();
        if (questions.Count == 0) questions.Add("No unresolved queue entries. Review decoded findings for runtime promotion decisions.");
        return questions.ToArray();
    }

    private static string BuildHtmlReport(ArchivistWorkspace w)
    {
        static string E(string value) => System.Net.WebUtility.HtmlEncode(value);
        var decoded = string.Join("", w.DecodeQueue.Select(x => $"<tr><td>{E(x.FileName)}</td><td>{E(x.Type)}</td><td>{E(x.Status)}</td><td>{E(x.Confidence.ToString())}</td><td>{E(x.Findings)}</td></tr>"));
        var evidence = string.Join("", w.EvidenceTree.Select(x => $"<tr><td>{E(x.Name)}</td><td>{E(x.Confidence.ToString())}</td><td>{E(x.Source)}</td><td>{E(x.Relationships)}</td></tr>"));
        var questions = string.Join("", BuildOpenQuestions(w).Select(x => $"<li>{E(x)}</li>"));
        return $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Archivist Investigation Report</title><style>body{{font-family:Segoe UI,Arial;margin:40px;max-width:1200px}}table{{border-collapse:collapse;width:100%}}th,td{{border:1px solid #999;padding:6px;text-align:left;vertical-align:top}}h1,h2{{color:#4d351a}}</style></head><body><h1>Archivist Investigation Report</h1><p>{E(BuildInvestigationSummary(w))}</p><h2>Decoded Structures</h2><table><tr><th>File</th><th>Type</th><th>Status</th><th>Confidence</th><th>Findings</th></tr>{decoded}</table><h2>Evidence and Relationships</h2><table><tr><th>Evidence</th><th>Confidence</th><th>Source</th><th>Relationships</th></tr>{evidence}</table><h2>Suggested Runtime</h2><p>No runtime behavior is promoted automatically. Review and explicit approval are required.</p><h2>Open Questions</h2><ul>{questions}</ul></body></html>";
    }

    private static string BuildSummary(ArchivistWorkspace w)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generated: {DateTime.Now:F}").AppendLine(BuildInvestigationSummary(w));
        sb.AppendLine("\n## Decoded Structures"); foreach (var i in w.DecodeQueue) sb.AppendLine($"- {i.FileName} [{i.Type}] — {i.Status} — {i.Confidence} — {i.Findings}");
        sb.AppendLine("\n## Evidence and Confidence"); foreach (var e in w.EvidenceTree) sb.AppendLine($"- {e.Name}: {e.Confidence} ({e.Source}) — {e.Relationships}");
        sb.AppendLine("\n## Suggested Runtime\nNo runtime behavior is promoted automatically. Review and approval are required.");
        sb.AppendLine("\n## Open Questions"); foreach (var q in BuildOpenQuestions(w)) sb.AppendLine($"- {q}");
        return sb.ToString();
    }

    public sealed record ToolValidationResult(bool IsValid, string Message, string ResolvedExecutable);

    private static byte[] MinimalPdf(string text)
    {
        var safe = string.Join(" ", text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        if (safe.Length > 3000) safe = safe[..3000];
        var stream = $"BT /F1 10 Tf 50 760 Td ({safe}) Tj ET";
        var objects = new[] { "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n", "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n", "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources<< /Font<< /F1 5 0 R >> >> /Contents 4 0 R >>endobj\n", $"4 0 obj<< /Length {Encoding.ASCII.GetByteCount(stream)} >>stream\n{stream}\nendstream endobj\n", "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n" };
        var sb = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 }; foreach (var o in objects) { offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString())); sb.Append(o); }
        var xref = Encoding.ASCII.GetByteCount(sb.ToString()); sb.Append("xref\n0 6\n0000000000 65535 f \n"); foreach (var o in offsets.Skip(1)) sb.Append($"{o:0000000000} 00000 n \n"); sb.Append($"trailer<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"); return Encoding.ASCII.GetBytes(sb.ToString());
    }
}

public static class GuardianCatalog
{
    public static List<GuardianProfile> CreateDefaults() =>
    [
        new() { Role=GuardianRole.RealmKeeper, Name="Thane Aldric", Title="Realm Keeper", Icon="♜", Appearance="Elder human steward in silver armor and a deep blue cloak, studying a realm map.", Personality="Calm, protective, deliberate, and operational.", Mission="Protect every connected realm and coordinate global administration." },
        new() { Role=GuardianRole.Wanderer, Name="Lyra Windstep", Title="Wanderer", Icon="⌖", Appearance="Elven explorer in weathered leather with maps, compass, and travel cloak.", Personality="Curious, observant, and honest about what remains undiscovered.", Mission="Play naturally, explore, map, test quests and routes, and record evidence." },
        new() { Role=GuardianRole.Analyst, Name="Magnus Grey", Title="Analyst", Icon="Σ", Appearance="Human scholar surrounded by charts, ledgers, candles, and annotated reports.", Personality="Precise, skeptical, evidence-driven, and quietly humorous.", Mission="Analyze balance, combat, economy, progression, quests, and runtime trends." },
        new() { Role=GuardianRole.Worldsmith, Name="Elara Stonewright", Title="Worldsmith", Icon="⚒", Appearance="Dwarven architect at a blueprint table with models, rulers, and a stoneworker's apron.", Personality="Creative, collaborative, practical, and approval-conscious.", Mission="Draft content and repairs only in approved test workspaces." },
        new() { Role=GuardianRole.Archivist, Name="Brother Caelum", Title="Archivist", Icon="✥", Appearance="Quiet human monk at an ancient oak desk inside a candlelit library.", Personality="Patient, meticulous, cautious, and unwilling to guess.", Mission="Decode evidence and prepare preview-only Librarium knowledge packages." }
    ];
}


public sealed class GuardianHallHardeningService
{
    private readonly IGuardianAssignmentStore _store;
    public GuardianHallHardeningService(IGuardianAssignmentStore store) => _store = store;

    public async Task RunAsync(GuardianHallState state, GuardianHallDiagnosticsWorkspace diagnostics, CancellationToken token = default)
    {
        diagnostics.Results.Clear();
        diagnostics.PassedChecks = 0;
        diagnostics.FailedChecks = 0;
        Check(diagnostics, state.Guardians.Select(x => x.Role).Distinct().Count() == 5, "All five Guardian roles are present");
        Check(diagnostics, !string.IsNullOrWhiteSpace(state.Wanderer.Character), "Wanderer retains a character assignment");
        Check(diagnostics, state.Worldsmith.TargetTestRealm != "Live Realm", "Worldsmith is isolated from Live Realm");
        Check(diagnostics, state.Guardians.First(x => x.Role == GuardianRole.Analyst).Role == GuardianRole.Analyst, "Analyst role is available without character control");
        Check(diagnostics, state.Archivist.LibrariumPreview.Contains("Preview only", StringComparison.OrdinalIgnoreCase), "Archivist Librarium remains preview-only");
        Check(diagnostics, state.Orchestration.Jobs.All(x => x.Role != GuardianRole.Worldsmith || x.Status != GuardianJobStatus.Running), "Worldsmith has no unattended running job");
        Check(diagnostics, state.RealmKeeper.RemoteHosts.All(IsValidRemoteHost), "Realm Keeper remote host configurations are bounded and valid");
        Check(diagnostics, IsValidWandererConnector(state.Wanderer), "Wanderer connector configuration is bounded and valid");

        try
        {
            await _store.SaveAsync(state, token);
            var reloaded = await _store.LoadAsync(token);
            Check(diagnostics, reloaded is not null && reloaded.SchemaVersion >= 7 && reloaded.Guardians.Count == state.Guardians.Count, "Atomic persistence round-trip succeeded");
        }
        catch (Exception ex)
        {
            Check(diagnostics, false, $"Atomic persistence round-trip failed: {ex.Message}");
        }

        diagnostics.LastRunUtc = DateTime.UtcNow.ToString("O");
        diagnostics.OverallStatus = diagnostics.FailedChecks == 0
            ? $"PASS {diagnostics.PassedChecks}/{diagnostics.PassedChecks + diagnostics.FailedChecks} Guardian Hall hardening checks"
            : $"FAIL {diagnostics.FailedChecks} of {diagnostics.PassedChecks + diagnostics.FailedChecks} Guardian Hall hardening checks";
        await _store.SaveAsync(state, token);
    }

    private static bool IsValidRemoteHost(RealmHostEndpoint endpoint)
    {
        if (!endpoint.Enabled) return true;
        return endpoint.TimeoutSeconds is >= 1 and <= 60
            && Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsValidWandererConnector(WandererWorkspace workspace)
    {
        if (!workspace.LiveConnectorEnabled) return true;
        return workspace.SessionTimeoutSeconds is >= 1 and <= 60
            && Uri.TryCreate(workspace.SessionBaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static void Check(GuardianHallDiagnosticsWorkspace diagnostics, bool passed, string message)
    {
        diagnostics.Results.Add($"{(passed ? "PASS" : "FAIL")} — {message}");
        if (passed) diagnostics.PassedChecks++; else diagnostics.FailedChecks++;
    }
}

public sealed class GuardianOrchestrator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task RunDueJobsAsync(GuardianOrchestrationWorkspace orchestration, Func<GuardianRole, Task<string>> executor, CancellationToken token = default)
    {
        if (!orchestration.Enabled || orchestration.Paused) return;
        if (!await _gate.WaitAsync(0, token)) return;
        try
        {
            var now = DateTime.UtcNow;
            foreach (var job in orchestration.Jobs.Where(x => x.Status == GuardianJobStatus.Queued && DateTime.TryParse(x.ScheduledUtc, out var due) && due <= now).ToList())
            {
                token.ThrowIfCancellationRequested();
                job.Status = GuardianJobStatus.Running;
                job.StartedUtc = DateTime.UtcNow.ToString("O");
                job.Attempt++;
                try
                {
                    job.Message = await executor(job.Role);
                    job.Status = GuardianJobStatus.Completed;
                }
                catch (InvalidOperationException ex)
                {
                    job.Message = ex.Message;
                    job.Status = GuardianJobStatus.Blocked;
                }
                catch (OperationCanceledException)
                {
                    job.Message = "Cancelled";
                    job.Status = GuardianJobStatus.Cancelled;
                }
                catch (Exception ex)
                {
                    job.Message = ex.Message;
                    job.Status = GuardianJobStatus.Failed;
                }
                finally
                {
                    job.CompletedUtc = DateTime.UtcNow.ToString("O");
                }
            }
            orchestration.LastRunUtc = DateTime.UtcNow.ToString("O");
            orchestration.NextRunUtc = DateTime.UtcNow.AddSeconds(Math.Max(15, orchestration.TickSeconds)).ToString("O");
            orchestration.RuntimeStatus = $"Scheduler processed {orchestration.Jobs.Count(x => x.Status is GuardianJobStatus.Completed or GuardianJobStatus.Blocked or GuardianJobStatus.Failed)} job(s)";
            while (orchestration.Jobs.Count > 100) orchestration.Jobs.RemoveAt(0);
        }
        finally { _gate.Release(); }
    }

    public GuardianJobRecord Queue(GuardianOrchestrationWorkspace orchestration, GuardianRole role, string jobType, DateTime? dueUtc = null)
    {
        var job = new GuardianJobRecord { Role = role, JobType = jobType, ScheduledUtc = (dueUtc ?? DateTime.UtcNow).ToString("O") };
        orchestration.Jobs.Add(job);
        orchestration.RuntimeStatus = $"Queued {role} {jobType}";
        return job;
    }
}
