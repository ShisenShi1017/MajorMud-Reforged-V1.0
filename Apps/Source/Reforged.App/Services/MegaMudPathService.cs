using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reforged.App.Models;

namespace Reforged.App.Services;

public interface IMegaMudPathService
{
    ObservableCollection<MegaMudPathRecord> AvailablePaths { get; }
    ImportedPathProfile ImportedProfile { get; }
    string SourceDescription { get; }
    RouteRecordingState Recording { get; }
    Task LoadAsync();
    Task ImportAsync(MegaMudPathRecord route);
    Task RemoveImportAsync(MegaMudPathRecord route);
    Task SaveImportedProfileAsync();
    void BeginRecording(string sessionId, string routeName, string startRoom);
    void RecordCommand(string sessionId, string command);
    Task<ImportedPathEntry?> FinishRecordingAsync(string endRoom);
    void CancelRecording();
    Task DuplicateImportedAsync(ImportedPathEntry route);
    Task DeleteImportedAsync(ImportedPathEntry route);
    MegaMudPathRecord? CalculateReplacementRoute(string currentRoomKey, string destinationRoomKey);
}

public sealed partial class MegaMudPathService(IApplicationSettingsService settings) : IMegaMudPathService
{
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    public ObservableCollection<MegaMudPathRecord> AvailablePaths { get; } = [];
    public ImportedPathProfile ImportedProfile { get; private set; } = new();
    public RouteRecordingState Recording { get; } = new();
    public string SourceDescription { get; private set; } = "MegaMUD path source not loaded";

    private string ProfilePath => Path.Combine(settings.RootPath, "Client", "Paths", "local-host-classic.json");

    public async Task LoadAsync()
    {
        await LoadProfileAsync();
        var root = FindRepositoryRoot();
        var source = root is null ? null : Path.Combine(root, "ImportedDatabases", "MegaMudMP");
        var rooms = root is null ? new Dictionary<string, LibrariumRoom>(StringComparer.OrdinalIgnoreCase) : LoadRooms(root);
        AvailablePaths.Clear();
        if (source is null || !Directory.Exists(source))
        {
            SourceDescription = "MegaMUD .mp source folder was not found. Imported Client paths remain available.";
            return;
        }

        foreach (var file in Directory.EnumerateFiles(source, "*.mp", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var route = Parse(file, rooms);
            route.IsImported = ImportedProfile.Paths.Any(p => string.Equals(p.SourceFileName, route.FileName, StringComparison.OrdinalIgnoreCase));
            AvailablePaths.Add(route);
        }
        SourceDescription = $"{AvailablePaths.Count:N0} MegaMUD .mp paths • read-only source • local imports stored per server/world profile";
    }

    public async Task ImportAsync(MegaMudPathRecord route)
    {
        var existing = ImportedProfile.Paths.FirstOrDefault(p => string.Equals(p.SourceFileName, route.FileName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            ImportedProfile.Paths.Add(new ImportedPathEntry
            {
                SourceFileName = route.FileName,
                DisplayName = route.DisplayName,
                StartLabel = route.StartLabel,
                EndLabel = route.EndLabel,
                Commands = new ObservableCollection<string>(route.Steps.Select(s => s.Command).Where(c => !string.IsNullOrWhiteSpace(c))),
            });
        }
        route.IsImported = true;
        await SaveProfileAsync();
    }

    public async Task RemoveImportAsync(MegaMudPathRecord route)
    {
        var existing = ImportedProfile.Paths.FirstOrDefault(p => string.Equals(p.SourceFileName, route.FileName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) ImportedProfile.Paths.Remove(existing);
        route.IsImported = false;
        await SaveProfileAsync();
    }


    public Task SaveImportedProfileAsync() => SaveProfileAsync();

    public void BeginRecording(string sessionId, string routeName, string startRoom)
    {
        Recording.IsRecording = true;
        Recording.SessionId = sessionId;
        Recording.RouteName = string.IsNullOrWhiteSpace(routeName) ? "Recorded Route" : routeName.Trim();
        Recording.StartRoom = startRoom?.Trim() ?? string.Empty;
        Recording.Commands.Clear();
    }

    public void RecordCommand(string sessionId, string command)
    {
        if (!Recording.IsRecording || !string.Equals(Recording.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)) return;
        var text = command.Trim();
        if (text.Length == 0 || text.StartsWith('@')) return;
        Recording.Commands.Add(text);
    }

    public async Task<ImportedPathEntry?> FinishRecordingAsync(string endRoom)
    {
        if (!Recording.IsRecording || Recording.Commands.Count == 0) { CancelRecording(); return null; }
        var entry = new ImportedPathEntry
        {
            SourceFileName = $"recorded-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.client",
            DisplayName = Recording.RouteName,
            StartLabel = Recording.StartRoom,
            EndLabel = endRoom?.Trim() ?? string.Empty,
            Commands = new ObservableCollection<string>(Recording.Commands),
            Notes = "Recorded from live Client commands.",
            ImportedAt = DateTimeOffset.UtcNow,
        };
        ImportedProfile.Paths.Add(entry);
        CancelRecording();
        await SaveProfileAsync();
        return entry;
    }

    public void CancelRecording()
    {
        Recording.IsRecording = false;
        Recording.SessionId = string.Empty;
        Recording.Commands.Clear();
    }

    public async Task DuplicateImportedAsync(ImportedPathEntry route)
    {
        ImportedProfile.Paths.Add(new ImportedPathEntry
        {
            SourceFileName = $"copy-{Guid.NewGuid():N}.client",
            DisplayName = route.DisplayName + " Copy",
            StartLabel = route.StartLabel,
            EndLabel = route.EndLabel,
            Commands = new ObservableCollection<string>(route.Commands),
            Notes = route.Notes,
            Favorite = route.Favorite,
            ImportedAt = DateTimeOffset.UtcNow,
        });
        await SaveProfileAsync();
    }

    public async Task DeleteImportedAsync(ImportedPathEntry route)
    {
        ImportedProfile.Paths.Remove(route);
        await SaveProfileAsync();
    }


    public MegaMudPathRecord? CalculateReplacementRoute(string currentRoomKey, string destinationRoomKey)
    {
        var start = NormalizeKey(currentRoomKey);
        var destination = NormalizeKey(destinationRoomKey);
        if (start.Length == 0 || destination.Length == 0) return null;
        if (string.Equals(start, destination, StringComparison.OrdinalIgnoreCase))
            return new MegaMudPathRecord { FileName = "client-recovery.mp", StartLabel = start, EndLabel = destination, NativeStartKey = start, NativeEndKey = destination };

        var edges = new Dictionary<string, List<(string To, string Command)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in AvailablePaths)
        {
            foreach (var step in route.Steps.Where(step => step.IsMovement && !string.IsNullOrWhiteSpace(step.NativeRoomKey) && !string.IsNullOrWhiteSpace(step.ExpectedNativeRoomKey)))
            {
                var from = NormalizeKey(step.NativeRoomKey);
                var to = NormalizeKey(step.ExpectedNativeRoomKey);
                if (!edges.TryGetValue(from, out var list)) edges[from] = list = [];
                if (!list.Any(edge => string.Equals(edge.To, to, StringComparison.OrdinalIgnoreCase) && string.Equals(edge.Command, step.Command, StringComparison.OrdinalIgnoreCase)))
                    list.Add((to, step.Command));
            }
        }

        var queue = new Queue<string>();
        var previous = new Dictionary<string, (string From, string Command)>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            if (!edges.TryGetValue(room, out var choices)) continue;
            foreach (var edge in choices)
            {
                if (!visited.Add(edge.To)) continue;
                previous[edge.To] = (room, edge.Command);
                if (string.Equals(edge.To, destination, StringComparison.OrdinalIgnoreCase))
                {
                    queue.Clear();
                    break;
                }
                queue.Enqueue(edge.To);
            }
        }
        if (!previous.ContainsKey(destination)) return null;

        var chain = new List<(string From, string To, string Command)>();
        var cursor = destination;
        while (!string.Equals(cursor, start, StringComparison.OrdinalIgnoreCase))
        {
            var link = previous[cursor];
            chain.Add((link.From, cursor, link.Command));
            cursor = link.From;
        }
        chain.Reverse();
        var steps = new ObservableCollection<MegaMudPathStep>();
        for (var index = 0; index < chain.Count; index++)
        {
            var edge = chain[index];
            steps.Add(new MegaMudPathStep
            {
                Number = index + 1, NativeRoomKey = edge.From, Command = edge.Command, IsMovement = true,
                FriendlyCommand = Friendly(edge.Command), ExpectedNativeRoomKey = edge.To,
            });
        }
        return new MegaMudPathRecord
        {
            FileName = $"client-recovery-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.mp", Author = "Reforged Client",
            StartLabel = start, EndLabel = destination, NativeStartKey = start, NativeEndKey = destination,
            DeclaredStepCount = steps.Count, Steps = steps,
        };
    }

    private static string NormalizeKey(string value) => value.Trim().Trim('[', ']', '(', ')').ToUpperInvariant();

    private async Task LoadProfileAsync()
    {
        if (!File.Exists(ProfilePath)) return;
        try
        {
            ImportedProfile = JsonSerializer.Deserialize<ImportedPathProfile>(await File.ReadAllTextAsync(ProfilePath), _json) ?? new ImportedPathProfile();
        }
        catch (JsonException) { ImportedProfile = new ImportedPathProfile(); }
        catch (IOException) { ImportedProfile = new ImportedPathProfile(); }
    }

    private async Task SaveProfileAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        await File.WriteAllTextAsync(ProfilePath, JsonSerializer.Serialize(ImportedProfile, _json));
    }

    private static MegaMudPathRecord Parse(string path, IReadOnlyDictionary<string, LibrariumRoom> rooms)
    {
        var lines = File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var warnings = new ObservableCollection<string>();
        var headerValues = lines.Count > 0 ? BracketValues().Matches(lines[0]).Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToArray() : [];
        var author = headerValues.Length > 1 ? headerValues[1] : string.Empty;
        var start = lines.Count > 1 ? ParseEndpoint(lines[1], rooms) : null;
        MegaMudPathEndpoint? end = null;
        var dataIndex = 2;
        if (lines.Count > 2 && lines[2].StartsWith('[')) { end = ParseEndpoint(lines[2], rooms); dataIndex = 3; }
        if (start is null) warnings.Add("The starting room header could not be decoded.");

        string nativeStart = string.Empty, nativeEnd = string.Empty;
        var declared = 0;
        if (lines.Count > dataIndex)
        {
            var routeHeader = lines[dataIndex].Split(':');
            if (routeHeader.Length >= 3)
            {
                nativeStart = routeHeader[0]; nativeEnd = routeHeader[1];
                _ = int.TryParse(routeHeader[2], out declared);
            }
            else warnings.Add("The native route header is incomplete.");
            dataIndex++;
        }

        var steps = new ObservableCollection<MegaMudPathStep>();
        for (var i = dataIndex; i < lines.Count; i++)
        {
            var parts = lines[i].Split(':', 3);
            if (parts.Length < 3) { warnings.Add($"Line {i + 1} was skipped because it is incomplete."); continue; }
            var command = parts[2].Trim();
            steps.Add(new MegaMudPathStep
            {
                Number = steps.Count + 1,
                NativeRoomKey = parts[0],
                NativeFlags = parts[1],
                Command = command,
                IsMovement = IsMovement(command),
                FriendlyCommand = Friendly(command),
            });
        }
        for (var index = 0; index < steps.Count; index++)
        {
            var current = steps[index];
            var expected = index + 1 < steps.Count ? steps[index + 1].NativeRoomKey : nativeEnd;
            steps[index] = new MegaMudPathStep
            {
                Number = current.Number, NativeRoomKey = current.NativeRoomKey, NativeFlags = current.NativeFlags,
                Command = current.Command, IsMovement = current.IsMovement, FriendlyCommand = current.FriendlyCommand,
                ExpectedNativeRoomKey = current.IsMovement ? expected : string.Empty,
            };
        }
        if (declared > 0 && declared != steps.Count) warnings.Add($"File declares {declared} steps but contains {steps.Count} readable steps.");

        return new MegaMudPathRecord
        {
            FileName = Path.GetFileName(path), SourcePath = path, Author = author,
            StartLabel = start?.Name ?? HeaderLabel(headerValues, 0), EndLabel = end?.Name ?? string.Empty,
            Start = start, End = end, NativeStartKey = nativeStart, NativeEndKey = nativeEnd,
            DeclaredStepCount = declared, Steps = steps, Warnings = warnings,
        };
    }

    private static MegaMudPathEndpoint? ParseEndpoint(string line, IReadOnlyDictionary<string, LibrariumRoom> rooms)
    {
        var value = BracketValues().Match(line).Groups[1].Value.Trim();
        if (value.Length == 0) return null;
        var sections = value.Split(':', 3);
        var code = sections.Length > 0 ? sections[0].Trim() : string.Empty;
        var region = sections.Length > 1 ? sections[1].Trim() : string.Empty;
        var location = sections.Length > 2 ? sections[2].Trim() : value;
        var match = RoomSuffix().Match(location);
        int? map = null, room = null;
        var name = location;
        if (match.Success)
        {
            name = match.Groups[1].Value.Trim();
            if (int.TryParse(match.Groups[2].Value, out var mapValue)) map = mapValue;
            if (int.TryParse(match.Groups[3].Value, out var roomValue)) room = roomValue;
        }
        var endpoint = new MegaMudPathEndpoint { Code = code, Region = region, Name = name, MapNumber = map, RoomNumber = room };
        if (map is not null && room is not null && rooms.TryGetValue($"{map}:{room}", out var known))
        {
            endpoint.LibrariumRoomId = known.Id; endpoint.LibrariumName = known.Name; endpoint.LibrariumDescription = known.Description;
        }
        return endpoint;
    }

    private static Dictionary<string, LibrariumRoom> LoadRooms(string root)
    {
        var result = new Dictionary<string, LibrariumRoom>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(root, "Data", "Classic", "rooms.json");
        if (!File.Exists(path)) return result;
        foreach (var row in EnumerateGeneratedRecords(path))
        {
            if (!row.TryGetProperty("map_number", out var mapNode) || !row.TryGetProperty("room_number", out var roomNode)) continue;
            var map = mapNode.GetInt32(); var room = roomNode.GetInt32();
            result[$"{map}:{room}"] = new LibrariumRoom(
                row.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                row.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                row.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty);
        }
        return result;
    }

    private static IEnumerable<JsonElement> EnumerateGeneratedRecords(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("record_parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, relative.Replace('/', Path.DirectorySeparatorChar));
                foreach (var row in EnumerateGeneratedRecords(partPath)) yield return row;
            }
            yield break;
        }
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in root.EnumerateArray()) yield return row.Clone();
            yield break;
        }
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            foreach (var row in records.EnumerateArray()) yield return row.Clone();
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "ImportedDatabases", "MegaMudMP")) && Directory.Exists(Path.Combine(current.FullName, "Data", "Classic"))) return current.FullName;
                current = current.Parent;
            }
        }
        return null;
    }

    private static string HeaderLabel(string[] values, int index) => values.Length > index ? values[index] : string.Empty;
    private static bool IsMovement(string command) => new[] { "n", "ne", "e", "se", "s", "sw", "w", "nw", "u", "d" }.Contains(command, StringComparer.OrdinalIgnoreCase);
    private static string Friendly(string command) => command.ToLowerInvariant() switch
    {
        "n" => "North", "ne" => "Northeast", "e" => "East", "se" => "Southeast", "s" => "South",
        "sw" => "Southwest", "w" => "West", "nw" => "Northwest", "u" => "Up", "d" => "Down", _ => command,
    };

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex BracketValues();
    [GeneratedRegex(@"^(.*?)-(-?\d+)\s+(-?\d+)\s*$")]
    private static partial Regex RoomSuffix();
    private sealed record LibrariumRoom(string Id, string Name, string Description);
}
