using System.IO;
using System.Text.Json;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

public interface IClientDataService
{
    IReadOnlyList<ClientDataCategory> Categories { get; }
    string WorldName { get; }
    string SourceDescription { get; }
    Task LoadAsync(CancellationToken token = default);
    Task SaveOverrideAsync(ClientDataRecord record, CancellationToken token = default);
    Task ResetOverrideAsync(ClientDataRecord record, CancellationToken token = default);
}

public sealed class ClientDataService(IApplicationSettingsService settings) : IClientDataService
{
    private static readonly (string Name, string File)[] Definitions =
    [
        ("Players", "players.json"),
        ("Monsters", "npcs.json"),
        ("Items", "items.json"),
        ("Spells", "spells.json"),
        ("Rooms", "rooms.json"),
        ("Paths", "exits.json"),
        ("Macros", "commands.json"),
        ("Races", "races.json"),
        ("Classes", "classes.json")
    ];

    private readonly List<ClientDataCategory> _categories = [];
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private Dictionary<string, ClientDataOverride> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private string _dataRoot = string.Empty;

    public IReadOnlyList<ClientDataCategory> Categories => _categories;
    public string WorldName { get; private set; } = "Classic";
    public string SourceDescription { get; private set; } = "Host data unavailable";

    public async Task LoadAsync(CancellationToken token = default)
    {
        _categories.Clear();
        _dataRoot = FindGeneratedWorldDataRoot();
        await LoadOverridesAsync(token);

        foreach (var definition in Definitions)
        {
            token.ThrowIfCancellationRequested();
            var category = new ClientDataCategory { Name = definition.Name, SourceFile = definition.File };
            var path = Path.Combine(_dataRoot, definition.File);
            if (File.Exists(path))
            {
                foreach (var recordElement in EnumerateRecords(path))
                {
                    var record = CreateRecord(definition.Name, recordElement);
                    if (_overrides.TryGetValue(record.Id, out var local))
                    {
                        record.Local = local;
                    }
                    category.Records.Add(record);
                }
            }
            _categories.Add(category);
        }

        SourceDescription = Directory.Exists(_dataRoot)
            ? $"Host-published {WorldName} client data"
            : "Host data unavailable";
    }

    public async Task SaveOverrideAsync(ClientDataRecord record, CancellationToken token = default)
    {
        _overrides[record.Id] = record.Local;
        await PersistOverridesAsync(token);
    }

    public async Task ResetOverrideAsync(ClientDataRecord record, CancellationToken token = default)
    {
        record.Local = new ClientDataOverride();
        _overrides.Remove(record.Id);
        await PersistOverridesAsync(token);
    }

    private static IEnumerable<JsonElement> EnumerateRecords(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, relative.Replace('/', Path.DirectorySeparatorChar));
                foreach (var item in EnumerateRecords(partPath)) yield return item;
            }
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray()) yield return item.Clone();
            yield break;
        }
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in records.EnumerateArray()) yield return item.Clone();
        }
    }

    private static ClientDataRecord CreateRecord(string category, JsonElement element)
    {
        var id = ReadString(element, "id") ?? $"{category}:{Guid.NewGuid():N}";
        var name = ReadString(element, "name")
                   ?? ReadString(element, "title")
                   ?? ReadString(element, "text")
                   ?? id;
        var summary = category switch
        {
            "Monsters" => JoinFields(("Level", ReadNested(element, "combat", "level")), ("Alignment", ReadNested(element, "behavior", "alignment"))),
            "Items" => JoinFields(("Type", ReadNested(element, "classification", "type")), ("Weight", ReadNested(element, "economy", "weight"))),
            "Spells" => JoinFields(("Level", ReadString(element, "level")), ("Type", ReadString(element, "spell_type"))),
            "Rooms" => JoinFields(("Map", ReadString(element, "map_number")), ("Room", ReadString(element, "room_number"))),
            _ => ReadString(element, "description") ?? string.Empty
        };
        return new ClientDataRecord
        {
            Id = id,
            Category = category,
            Name = name,
            Summary = summary,
            HostDetails = BuildFriendlyDetails(element)
        };
    }

    private static string BuildFriendlyDetails(JsonElement element)
    {
        var lines = new List<string>();
        Add(lines, "Name", ReadString(element, "name"));
        Add(lines, "Description", ReadString(element, "description"));
        Add(lines, "Level", ReadNested(element, "combat", "level") ?? ReadString(element, "level"));
        Add(lines, "Alignment", ReadNested(element, "behavior", "alignment"));
        Add(lines, "Type", ReadNested(element, "classification", "type") ?? ReadString(element, "type"));
        Add(lines, "Weight", ReadNested(element, "economy", "weight"));
        Add(lines, "Cost", ReadNested(element, "economy", "cost"));
        Add(lines, "Map", ReadString(element, "map_number"));
        Add(lines, "Room", ReadString(element, "room_number"));
        Add(lines, "Message", ReadString(element, "text"));
        return lines.Count == 0 ? "No additional Host-published details are available." : string.Join(Environment.NewLine, lines);
    }

    private static void Add(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value}");
    }

    private static string JoinFields(params (string Label, string? Value)[] fields)
    {
        return string.Join(" • ", fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => $"{field.Label}: {field.Value}"));
    }

    private static string? ReadNested(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current)) return null;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            _ => null
        };
    }

    private static string? ReadString(JsonElement element, string property)
        => ReadNested(element, property);

    private string FindGeneratedWorldDataRoot()
    {
        var candidates = new List<string>();
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && cursor is not null; i++, cursor = cursor.Parent)
        {
            candidates.Add(Path.Combine(cursor.FullName, "Data", WorldName));
        }
        candidates.Add(Path.Combine(Environment.CurrentDirectory, "Data", WorldName));
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private string OverridePath => Path.Combine(settings.RootPath, "Client", "Profiles", "Local", WorldName, "client-data-overrides.json");

    private async Task LoadOverridesAsync(CancellationToken token)
    {
        if (!File.Exists(OverridePath)) return;
        await using var stream = File.OpenRead(OverridePath);
        _overrides = await JsonSerializer.DeserializeAsync<Dictionary<string, ClientDataOverride>>(stream, _json, token)
                     ?? new Dictionary<string, ClientDataOverride>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task PersistOverridesAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OverridePath)!);
        await using var stream = File.Create(OverridePath);
        await JsonSerializer.SerializeAsync(stream, _overrides, _json, token);
    }
}
