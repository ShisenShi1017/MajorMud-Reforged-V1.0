using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Reforged.App.Views;

public partial class LibrariumView : UserControl
{
    private readonly LibrariumBrowserModel _model = new();

    public LibrariumView()
    {
        InitializeComponent();
        DataContext = _model;
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        try
        {
            var root = FindRepositoryRoot();
            _model.Load(root);
        }
        catch (Exception exception)
        {
            _model.Summary = "Librarium could not be loaded: " + exception.Message;
            _model.ListHeader = "No records loaded.";
            _model.FilteredRecords.Clear();
            _model.SelectedRecord = LibrariumRecordRow.FromError(exception);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Librarium")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Data")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Librarium")) &&
                Directory.Exists(Path.Combine(current.FullName, "Data")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find a repository root containing Librarium and Data.");
    }
}

public sealed class LibrariumBrowserModel : INotifyPropertyChanged
{
    private readonly Dictionary<string, List<LibrariumRecordRow>> _recordsByCategory = new(StringComparer.OrdinalIgnoreCase);

    private string _summary = "Loading Librarium...";
    private string _listHeader = "";
    private string _searchText = "";
    private LibrariumCategory? _selectedCategory;
    private LibrariumRecordRow? _selectedRecord;

    public LibrariumBrowserModel()
    {
        Categories =
        [
            new LibrariumCategory("weapons", "Weapons"),
            new LibrariumCategory("armor", "Armor"),
            new LibrariumCategory("spells", "Spells"),
            new LibrariumCategory("abilities", "Abilities"),
            new LibrariumCategory("classes", "Classes"),
            new LibrariumCategory("races", "Races"),
            new LibrariumCategory("items", "Items"),
            new LibrariumCategory("monsters", "Monsters"),
            new LibrariumCategory("quests", "Quests"),
            new LibrariumCategory("rooms", "Rooms"),
            new LibrariumCategory("exits", "Exits / Paths")
        ];

        _selectedCategory = Categories[0];
    }

    public ObservableCollection<LibrariumCategory> Categories { get; }
    public ObservableCollection<LibrariumRecordRow> FilteredRecords { get; } = [];

    public string Summary
    {
        get => _summary;
        set { _summary = value; OnPropertyChanged(nameof(Summary)); }
    }

    public string ListHeader
    {
        get => _listHeader;
        set { _listHeader = value; OnPropertyChanged(nameof(ListHeader)); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            RefreshFilter();
        }
    }

    public LibrariumCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value) return;
            _selectedCategory = value;
            OnPropertyChanged(nameof(SelectedCategory));
            RefreshFilter();
        }
    }

    public LibrariumRecordRow? SelectedRecord
    {
        get => _selectedRecord;
        set { _selectedRecord = value; OnPropertyChanged(nameof(SelectedRecord)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Load(string repositoryRoot)
    {
        _recordsByCategory.Clear();

        var dataRoot = Path.Combine(repositoryRoot, "Data", "Classic");
        if (!Directory.Exists(dataRoot))
        {
            throw new DirectoryNotFoundException("Generated Classic data folder was not found: " + dataRoot);
        }

        var items = LoadRecords(dataRoot, "items.json", "items");
        var spells = LoadRecords(dataRoot, "spells.json", "spells");
        var abilities = LoadRecords(dataRoot, "abilities.json", "abilities");
        LibrariumRecordRow.AbilityNames = BuildAbilityNameLookup(abilities);
        LibrariumRecordRow.ItemNames = BuildNameLookup(items, "item.");
        var classes = LoadNestedRecords(dataRoot, "character_foundation.json", "classes", "classes");
        var races = LoadNestedRecords(dataRoot, "character_foundation.json", "races", "races");
        if (classes.Count == 0) classes = LoadRecords(dataRoot, "classes.json", "classes");
        if (races.Count == 0) races = LoadRecords(dataRoot, "races.json", "races");
        var monsters = LoadRecords(dataRoot, "npcs.json", "monsters");
        LibrariumRecordRow.NpcNames = BuildNameLookup(monsters, "npc.");
        var questFlows = LoadRecords(dataRoot, "quests.json", "quests")
            .Where(IsActualQuestFlow)
            .ToList();
        var questGuides = QuestWalkthroughCatalog.Load(Path.Combine(repositoryRoot, "Librarium", "Objects", "quests", "quest_walkthrough_guides.txt"));
        LibrariumRecordRow.QuestGuides = questGuides;
        var quests = BuildQuestSummaries(questFlows, questGuides);
        var rooms = LoadRecords(dataRoot, "rooms.json", "rooms");
        var exits = LoadRecords(dataRoot, "exits.json", "exits");

        _recordsByCategory["items"] = items;
        _recordsByCategory["weapons"] = items.Where(IsWeapon).ToList();
        _recordsByCategory["armor"] = items.Where(IsArmor).ToList();
        _recordsByCategory["spells"] = spells;
        _recordsByCategory["abilities"] = abilities;
        _recordsByCategory["classes"] = classes;
        _recordsByCategory["races"] = races;
        _recordsByCategory["monsters"] = monsters;
        _recordsByCategory["quests"] = quests;
        _recordsByCategory["rooms"] = rooms;
        _recordsByCategory["exits"] = exits;

        foreach (var category in Categories)
        {
            category.Count = _recordsByCategory.TryGetValue(category.Key, out var records) ? records.Count : 0;
        }

        var total = _recordsByCategory.Values.Sum(records => records.Count);
        Summary = $"{total:N0} browsable Classic records loaded from generated /Data. Search applies to the selected tab.";
        RefreshFilter();
    }

    private List<LibrariumRecordRow> LoadRecords(string dataRoot, string fileName, string category)
    {
        var path = Path.Combine(dataRoot, fileName);
        if (!File.Exists(path)) return [];

        return EnumerateGeneratedRecords(path)
            .Select(record => LibrariumRecordRow.FromJson(category, fileName, record))
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<JsonElement> EnumerateGeneratedRecords(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var expectedCount = root.TryGetProperty("record_count", out var recordCountElement) && recordCountElement.TryGetInt32(out var declaredCount)
                ? declaredCount
                : (int?)null;
            var loadedCount = 0;
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(partPath))
                {
                    throw new FileNotFoundException($"Generated chunk listed by '{Path.GetFileName(path)}' was not found.", partPath);
                }
                foreach (var record in EnumerateGeneratedRecords(partPath))
                {
                    loadedCount++;
                    yield return record;
                }
            }
            if (expectedCount.HasValue && loadedCount != expectedCount.Value)
            {
                throw new InvalidDataException($"Generated chunk manifest '{Path.GetFileName(path)}' declares {expectedCount.Value:N0} records but loaded {loadedCount:N0}.");
            }
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray()) yield return item.Clone();
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("records", out var recordsElement) &&
            recordsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in recordsElement.EnumerateArray()) yield return item.Clone();
        }
    }

    private static List<LibrariumRecordRow> BuildQuestSummaries(IReadOnlyList<LibrariumRecordRow> questFlows, IReadOnlyList<QuestWalkthroughGuide> guides)
    {
        var decoded = questFlows
            .GroupBy(ReadQuestGroupName, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group => LibrariumRecordRow.FromQuestSummary(group.Key, group.OrderBy(ReadQuestNpcName).ThenBy(row => row.Id).ToList()))
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (guides.Count == 0)
        {
            return decoded;
        }

        var guided = guides
            .Select(guide => LibrariumRecordRow.FromQuestGuide(guide, FindRelatedQuestFlows(guide, questFlows)))
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return guided;
    }

    private static IReadOnlyList<LibrariumRecordRow> FindRelatedQuestFlows(QuestWalkthroughGuide guide, IReadOnlyList<LibrariumRecordRow> questFlows)
    {
        var flag = guide.PrimaryFlag;
        if (flag == 0)
        {
            return [];
        }

        return questFlows.Where(flow =>
        {
            using var document = JsonDocument.Parse(flow.RawJson);
            return ReadInt(document.RootElement, "quest_ability_id") == flag;
        }).ToList();
    }

    private static string ReadQuestGroupName(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        var questName = FirstPathValue(root, ["quest_flag_name"]);
        if (IsAlignmentQuestFlag(questName))
        {
            var rootTextblock = FirstPathValue(root, ["relationships", "root_textblock"], ["root_textblock_ref"]);
            if (!string.IsNullOrWhiteSpace(rootTextblock))
            {
                return $"alignment.{rootTextblock}";
            }
        }

        return questName;
    }

    private static string ReadQuestNpcName(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        return FirstPathValue(document.RootElement, ["npc_name"], ["relationships", "npc"]);
    }

    private static string ReadQuestNpcNameFromRaw(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        var npcRef = FirstPathValue(root, ["npc_ref"], ["relationships", "npc"]);
        var npcName = FirstPathValue(root, ["npc_name"]);
        if (string.IsNullOrWhiteSpace(npcName)) npcName = LibrariumRecordRow.ResolveNpcName(npcRef);
        return npcName;
    }

    private static bool IsAlignmentQuestFlag(string questName) =>
        questName.Equals("GoodQuest", StringComparison.OrdinalIgnoreCase) ||
        questName.Equals("EvilQuest", StringComparison.OrdinalIgnoreCase) ||
        questName.Equals("NeutralQuest", StringComparison.OrdinalIgnoreCase);

    private static List<LibrariumRecordRow> LoadNestedRecords(string dataRoot, string fileName, string propertyName, string category)
    {
        var path = Path.Combine(dataRoot, fileName);
        if (!File.Exists(path)) return [];

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty(propertyName, out var recordsElement) ||
            recordsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return recordsElement
            .EnumerateArray()
            .Select(record => LibrariumRecordRow.FromJson(category, fileName, record))
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsActualQuestFlow(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        var classification = FirstPathValue(root, ["classification"]);
        if (classification.Equals("npc_textblock_quest_flow", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryReadPath(root, ["script_flow", "stages"], out var stages) &&
               stages.ValueKind == JsonValueKind.Array &&
               stages.GetArrayLength() > 0;
    }

    private static Dictionary<int, string> BuildAbilityNameLookup(IEnumerable<LibrariumRecordRow> abilities)
    {
        var lookup = new Dictionary<int, string>();
        foreach (var ability in abilities)
        {
            using var document = JsonDocument.Parse(ability.RawJson);
            var root = document.RootElement;
            var id = ReadInt(root, "legacy_id");
            if (id == 0)
            {
                var text = FirstPathValue(root, ["id"]).Replace("ability.", "", StringComparison.OrdinalIgnoreCase);
                _ = int.TryParse(text, out id);
            }

            if (id == 0) continue;
            var name = FirstPathValue(root, ["name"], ["public_reference_0139", "fields", "name"]);
            if (!string.IsNullOrWhiteSpace(name)) lookup[id] = name;
        }

        return lookup;
    }

    private static Dictionary<int, string> BuildNameLookup(IEnumerable<LibrariumRecordRow> records, string idPrefix)
    {
        var lookup = new Dictionary<int, string>();
        foreach (var record in records)
        {
            using var document = JsonDocument.Parse(record.RawJson);
            var root = document.RootElement;
            var id = ReadInt(root, "legacy_id");
            if (id == 0)
            {
                var text = FirstPathValue(root, ["id"]).Replace(idPrefix, "", StringComparison.OrdinalIgnoreCase);
                _ = int.TryParse(text, out id);
            }

            if (id == 0) continue;
            var name = FirstPathValue(root, ["name"], ["public_reference_0139", "fields", "name"]);
            if (!string.IsNullOrWhiteSpace(name)) lookup[id] = name;
        }

        return lookup;
    }

    private void RefreshFilter()
    {
        FilteredRecords.Clear();

        if (SelectedCategory is null ||
            !_recordsByCategory.TryGetValue(SelectedCategory.Key, out var records))
        {
            ListHeader = "No records loaded.";
            SelectedRecord = null;
            return;
        }

        var query = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? records
            : records.Where(row => row.Matches(query));

        foreach (var row in filtered.Take(2_000))
        {
            FilteredRecords.Add(row);
        }

        var displayed = FilteredRecords.Count;
        ListHeader = $"{SelectedCategory.Header}: {displayed:N0} shown of {records.Count:N0}";
        SelectedRecord = FilteredRecords.FirstOrDefault();
    }

    private static bool IsWeapon(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        if (ReadBool(root, "classification", "is_armor") ||
            ReadBool(root, "combat", "is_armor") ||
            ReadBool(root, "public_reference_0139", "fields", "properties", "is_armor"))
        {
            return false;
        }

        var itemType = FirstPathValue(root,
            ["item_type"],
            ["classification", "type"],
            ["public_reference_0139", "fields", "item_type"]);

        return itemType.Contains("weapon", StringComparison.OrdinalIgnoreCase) ||
               ReadInt(root, "classification", "weapon_type") > 0 ||
               ReadInt(root, "combat", "weapon_type") > 0 ||
               ReadInt(root, "public_reference_0139", "fields", "properties", "weapon_type") > 0 ||
               ReadInt(root, "combat", "maximum_damage") > 0 ||
               ReadInt(root, "public_reference_0139", "fields", "max_hit") > 0;
    }

    private static bool IsArmor(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        var itemType = FirstPathValue(root,
            ["item_type"],
            ["classification", "type"],
            ["public_reference_0139", "fields", "item_type"]);

        return ReadBool(root, "classification", "is_armor") ||
               ReadBool(root, "combat", "is_armor") ||
               ReadBool(root, "public_reference_0139", "fields", "properties", "is_armor") ||
               ReadInt(root, "combat", "armor_type") > 0 ||
               ReadInt(root, "public_reference_0139", "fields", "properties", "armor_type") > 0 ||
               itemType.Contains("armor", StringComparison.OrdinalIgnoreCase) ||
               itemType.Contains("shield", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstPathValue(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (TryReadPath(root, path, out var value))
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
            }
        }

        return "";
    }

    private static bool ReadBool(JsonElement root, params string[] path)
    {
        if (!TryReadPath(root, path, out var value)) return false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static int ReadInt(JsonElement root, params string[] path)
    {
        if (!TryReadPath(root, path, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric)) return numeric;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static bool TryReadPath(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class LibrariumCategory : INotifyPropertyChanged
{
    private int _count;

    public LibrariumCategory(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public string Key { get; }
    public string Title { get; }

    public int Count
    {
        get => _count;
        set
        {
            _count = value;
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(Header));
        }
    }

    public string Header => Count > 0 ? $"{Title} ({Count:N0})" : Title;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record QuestWalkthroughGuide(string Title, string SourceTitle, int PrimaryFlag, IReadOnlyList<string> Lines);

public static partial class QuestWalkthroughCatalog
{
    public static IReadOnlyList<QuestWalkthroughGuide> Load(string path)
    {
        if (!File.Exists(path)) return [];

        var lines = File.ReadAllLines(path)
            .Select(line => line.TrimEnd())
            .ToArray();
        var marker = Array.FindIndex(lines, line => line.Trim().Equals("quest steps", StringComparison.OrdinalIgnoreCase));
        var source = marker >= 0 ? lines.Skip(marker + 1).ToArray() : lines;
        var blocks = SplitBlocks(source);
        var guides = new List<QuestWalkthroughGuide>();
        foreach (var block in blocks)
        {
            guides.AddRange(ParseBlock(block));
        }

        return guides
            .Where(guide => guide.Lines.Count > 0)
            .GroupBy(guide => guide.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(guide => guide.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string[]> SplitBlocks(IReadOnlyList<string> lines)
    {
        var current = new List<string>();
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("====", StringComparison.Ordinal))
            {
                if (current.Any(text => !string.IsNullOrWhiteSpace(text)))
                {
                    yield return current.ToArray();
                }

                current.Clear();
                continue;
            }

            current.Add(line);
        }

        if (current.Any(text => !string.IsNullOrWhiteSpace(text)))
        {
            yield return current.ToArray();
        }
    }

    private static IEnumerable<QuestWalkthroughGuide> ParseBlock(IReadOnlyList<string> block)
    {
        var meaningful = block.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (meaningful.Count == 0) yield break;

        var title = meaningful[0].Trim('.');
        if (IsAlignmentTitle(title))
        {
            foreach (var guide in ParseAlignmentBlock(title, meaningful.Skip(1).ToArray()))
            {
                yield return guide;
            }

            yield break;
        }

        yield return new QuestWalkthroughGuide(NormalizeTitle(title), title, InferPrimaryFlag(title), meaningful.Skip(1).ToArray());
    }

    private static IEnumerable<QuestWalkthroughGuide> ParseAlignmentBlock(string sourceTitle, IReadOnlyList<string> lines)
    {
        var currentTitle = "";
        var current = new List<string>();

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(currentTitle) || current.Count == 0) return;
            var title = NormalizeAlignmentSubquestTitle(currentTitle);
            _pending.Add(new QuestWalkthroughGuide(title, sourceTitle, InferPrimaryFlag(sourceTitle), current.ToArray()));
            current.Clear();
        }

        _pending.Clear();
        foreach (var line in lines)
        {
            if (IsAlignmentSubquest(line))
            {
                Flush();
                currentTitle = line;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentTitle))
            {
                current.Add(line);
            }
        }

        Flush();
        foreach (var guide in _pending)
        {
            yield return guide;
        }
    }

    private static readonly List<QuestWalkthroughGuide> _pending = [];

    private static bool IsAlignmentTitle(string title) =>
        title.Contains("Good Alignment Quest", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("Evil Alignment Quest", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("Neutral Alignment Quest", StringComparison.OrdinalIgnoreCase);

    private static bool IsAlignmentSubquest(string line) =>
        AlignmentSubquestRegex().IsMatch(line);

    private static string NormalizeAlignmentSubquestTitle(string title)
    {
        var cleaned = title.Trim();
        if (cleaned.Equals("1st Good", StringComparison.OrdinalIgnoreCase)) return "Good 1st Quest";
        if (cleaned.Equals("2nd Good", StringComparison.OrdinalIgnoreCase)) return "Good 2nd Quest";
        if (cleaned.Equals("3rd Good", StringComparison.OrdinalIgnoreCase)) return "Good 3rd Quest";
        if (cleaned.Equals("4th Good", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Good 4", StringComparison.OrdinalIgnoreCase)) return "Good 4th Quest";
        if (cleaned.Equals("5th Good", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Good 5", StringComparison.OrdinalIgnoreCase)) return "Good 5th Quest";
        if (cleaned.Equals("1st Evil", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Evil 1", StringComparison.OrdinalIgnoreCase)) return "Evil 1st Quest";
        if (cleaned.Equals("2nd Evil", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Evil 2", StringComparison.OrdinalIgnoreCase)) return "Evil 2nd Quest";
        if (cleaned.Equals("3rd Evil", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Evil 3", StringComparison.OrdinalIgnoreCase)) return "Evil 3rd Quest";
        if (cleaned.Equals("4th Evil", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Evil 4", StringComparison.OrdinalIgnoreCase)) return "Evil 4th Quest";
        if (cleaned.Equals("5th Evil", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Evil 5", StringComparison.OrdinalIgnoreCase)) return "Evil 5th Quest";
        if (cleaned.Equals("1st Neutral", StringComparison.OrdinalIgnoreCase)) return "Neutral 1st Quest";
        if (cleaned.Equals("2nd Neutral", StringComparison.OrdinalIgnoreCase)) return "Neutral 2nd Quest";
        if (cleaned.Equals("3rd Neutral", StringComparison.OrdinalIgnoreCase)) return "Neutral 3rd Quest";
        if (cleaned.Equals("4th Neutral", StringComparison.OrdinalIgnoreCase)) return "Neutral 4th Quest";
        if (cleaned.Equals("5th Neutral", StringComparison.OrdinalIgnoreCase)) return "Neutral 5th Quest";
        return NormalizeTitle(cleaned);
    }

    private static string NormalizeTitle(string title)
    {
        var cleaned = title.Trim().Trim('.');
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }

    private static int InferPrimaryFlag(string title)
    {
        var text = title.ToLowerInvariant();
        if (text.Contains("ice sorceress")) return 125;
        if (text.Contains("good")) return 126;
        if (text.Contains("neutral")) return 127;
        if (text.Contains("evil")) return 128;
        if (text.Contains("high druid")) return 129;
        if (text.Contains("blood") || text.Contains("champion") || text.Contains("bishop")) return 130;
        if (text.Contains("adult red") || text.Contains("dragon")) return 131;
        if (text.Contains("apparatus") || text.Contains("wererat")) return 132;
        if (text.Contains("phoenix")) return 133;
        if (text.Contains("dao")) return 134;
        if (text.Contains("witchunter") || text.Contains("draka") || text.Contains("mage bane")) return 50;
        return 0;
    }

    [GeneratedRegex(@"^((1st|2nd|3rd|4th|5th)\s+(Good|Evil|Neutral)|Good\s+[1-5]|Evil\s+[1-5])$", RegexOptions.IgnoreCase)]
    private static partial Regex AlignmentSubquestRegex();
}

public sealed class LibrariumRecordRow
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };
    private readonly string _rawJson;
    private ObservableCollection<LibrariumFieldRow>? _fields;
    private string? _jsonText;

    private LibrariumRecordRow(
        string category,
        string sourceFile,
        string id,
        string displayName,
        string kind,
        string preview,
        string rawJson,
        string searchBlob,
        ObservableCollection<LibrariumFieldRow>? fields = null)
    {
        Category = category;
        SourceFile = sourceFile;
        Id = id;
        DisplayName = displayName;
        Kind = kind;
        Preview = preview;
        _rawJson = rawJson;
        SearchBlob = searchBlob;
        _fields = fields;
    }

    public string Category { get; }
    public string SourceFile { get; }
    public string Id { get; }
    public string DisplayName { get; }
    public string Kind { get; }
    public string Preview { get; }
    public string JsonText => _jsonText ??= PrettyPrint(_rawJson);
    public string RawJson => _rawJson;
    public string SearchBlob { get; }
    public ObservableCollection<LibrariumFieldRow> Fields => _fields ??= BuildFields(Category, _rawJson);
    public string Subheading => string.IsNullOrWhiteSpace(Kind) ? Id : $"{Kind} | {Id}";
    public static IReadOnlyDictionary<int, string> AbilityNames { get; set; } = new Dictionary<int, string>();
    public static IReadOnlyDictionary<int, string> ItemNames { get; set; } = new Dictionary<int, string>();
    public static IReadOnlyDictionary<int, string> NpcNames { get; set; } = new Dictionary<int, string>();
    public static IReadOnlyList<QuestWalkthroughGuide> QuestGuides { get; set; } = [];

    public bool Matches(string query)
    {
        foreach (var token in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!SearchBlob.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static LibrariumRecordRow FromJson(string category, string sourceFile, JsonElement record)
    {
        var clone = record.Clone();
        var rawJson = clone.GetRawText();
        var id = FirstValue(clone, "id", "record_id", "runtime_id", "key", "room_id", "npc_id", "item_id", "class_id", "race_id");
        var name = FirstValue(clone, "name", "title", "display_name", "item_name", "npc_name", "room_name", "class_name", "race_name");
        var kind = FirstValue(clone, "type", "kind", "category", "classification", "item_type", "npc_type", "slot", "map_id");
        var preview = FirstValue(clone, "description", "summary", "notes", "text", "short_description", "long_description");

        if (string.IsNullOrWhiteSpace(id)) id = ReadQualifiedId(clone);
        if (string.IsNullOrWhiteSpace(id)) id = "(no id)";
        if (string.IsNullOrWhiteSpace(name)) name = id;
        if (category.Equals("quests", StringComparison.OrdinalIgnoreCase))
        {
            name = BuildQuestDisplayName(clone, name);
        }
        else if (category.Equals("exits", StringComparison.OrdinalIgnoreCase))
        {
            var direction = FirstValue(clone, "direction");
            var fromRoom = FirstPathValue(clone, ["relationships", "from_room"]);
            var toRoom = FirstPathValue(clone, ["relationships", "to_room"]);
            name = !string.IsNullOrWhiteSpace(direction) && !string.IsNullOrWhiteSpace(fromRoom) && !string.IsNullOrWhiteSpace(toRoom)
                ? $"{fromRoom} {direction} → {toRoom}"
                : name;
            preview = FirstValue(clone, "door_name", "action", "command", "description");
        }

        if (string.IsNullOrWhiteSpace(kind)) kind = category;
        if (string.IsNullOrWhiteSpace(preview)) preview = BuildPreview(clone);

        var searchBlob = string.Join(' ', category, sourceFile, id, name, kind, preview, rawJson);
        return new LibrariumRecordRow(category, sourceFile, id, name, kind, Trim(preview, 180), rawJson, searchBlob);
    }

    public static LibrariumRecordRow FromQuestGuide(QuestWalkthroughGuide guide, IReadOnlyList<LibrariumRecordRow> relatedFlows)
    {
        var fields = new ObservableCollection<LibrariumFieldRow>
        {
            new("Quest", guide.Title)
        };

        if (guide.PrimaryFlag > 0)
        {
            fields.Add(new LibrariumFieldRow("Primary Flag", guide.PrimaryFlag.ToString()));
        }

        var decodedNpcIds = DescribeRelatedNpcIds(relatedFlows);
        if (!string.IsNullOrWhiteSpace(decodedNpcIds))
        {
            fields.Add(new LibrariumFieldRow("Decoded NPC IDs", decodedNpcIds));
        }

        var step = 1;
        foreach (var line in guide.Lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var label = BuildGuideStepLabel(line, step);
            fields.Add(new LibrariumFieldRow(label, line));
            step++;
        }

        var rawJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = $"quest.guide.{Slug(guide.Title)}",
            ["name"] = guide.Title,
            ["classification"] = "quest_walkthrough_guide",
            ["source_title"] = guide.SourceTitle,
            ["primary_flag"] = guide.PrimaryFlag == 0 ? null : guide.PrimaryFlag,
            ["decoded_npc_ids"] = decodedNpcIds,
            ["steps"] = guide.Lines
        }, PrettyJsonOptions);

        var searchBlob = string.Join(' ', "quests", guide.Title, guide.SourceTitle, decodedNpcIds, string.Join(' ', guide.Lines), rawJson);
        return new LibrariumRecordRow("quests", "quest_walkthrough_guides.txt", $"quest.guide.{Slug(guide.Title)}", guide.Title, "Quest Guide", $"{guide.Lines.Count:N0} guide lines", rawJson, searchBlob, fields);
    }

    private static string DescribeRelatedNpcIds(IReadOnlyList<LibrariumRecordRow> relatedFlows)
    {
        var values = new List<string>();
        foreach (var flow in relatedFlows)
        {
            using var document = JsonDocument.Parse(flow.RawJson);
            var root = document.RootElement;
            var npcRef = FirstPathValue(root, ["npc_ref"], ["relationships", "npc"]);
            var npcName = FirstPathValue(root, ["npc_name"]);
            if (string.IsNullOrWhiteSpace(npcName)) npcName = ResolveNpcName(npcRef);
            if (string.IsNullOrWhiteSpace(npcRef)) continue;
            values.Add(string.IsNullOrWhiteSpace(npcName) ? npcRef : $"{npcName} ({npcRef})");
        }

        return string.Join(", ", values.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildGuideStepLabel(string line, int fallbackStep)
    {
        var match = Regex.Match(line, @"^(?<flag>\d+)\((?<step>[^)]+)\)");
        if (match.Success)
        {
            return $"Flag {match.Groups["flag"].Value}({match.Groups["step"].Value})";
        }

        return line.StartsWith("Empty -", StringComparison.OrdinalIgnoreCase)
            ? "Start"
            : $"Step {fallbackStep}";
    }

    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        return Regex.Replace(new string(chars), "-+", "-").Trim('-');
    }

    public static LibrariumRecordRow FromQuestSummary(string questName, IReadOnlyList<LibrariumRecordRow> flows)
    {
        if (TryBuildKnownQuestGuide(questName, flows, out var knownGuide))
        {
            return knownGuide;
        }

        var displayName = BuildQuestSummaryDisplayName(questName, flows);
        var stateNames = ReadQuestStateNames(flows);
        var fields = new ObservableCollection<LibrariumFieldRow>
        {
            new("Quest", displayName),
            new("Quest States", stateNames),
            new("Branches", $"{flows.Count:N0} NPC dialogue branch{(flows.Count == 1 ? "" : "es")}")
        };

        var summaryBranches = new List<Dictionary<string, object?>>();
        var branchNumber = 1;
        foreach (var flow in flows)
        {
            using var document = JsonDocument.Parse(flow.RawJson);
            var root = document.RootElement;
            var npcRef = FirstPathValue(root, ["npc_ref"], ["relationships", "npc"]);
            var npcName = FirstPathValue(root, ["npc_name"]);
            if (string.IsNullOrWhiteSpace(npcName)) npcName = ResolveNpcName(npcRef);
            if (string.IsNullOrWhiteSpace(npcName)) npcName = npcRef;

            var keywords = ReadStringArray(root, ["command_keywords"], ["relationships", "command_keywords"]);
            var branchLabel = $"Talk to {npcName}";
            fields.Add(new LibrariumFieldRow($"Branch {branchNumber}", string.IsNullOrWhiteSpace(keywords)
                ? branchLabel
                : $"{branchLabel}. Say/ask: {keywords}."));

            var branchSummary = new Dictionary<string, object?>
            {
                ["npc"] = npcName,
                ["npc_ref"] = npcRef,
                ["say_or_ask"] = keywords
            };
            var stagesSummary = new List<Dictionary<string, object?>>();

            if (TryReadPath(root, ["script_flow", "stages"], out var stages) &&
                stages.ValueKind == JsonValueKind.Array)
            {
                var requiredItems = stages.EnumerateArray()
                    .SelectMany(ReadRequiredItems)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (requiredItems.Length > 0)
                {
                    fields.Add(new LibrariumFieldRow($"Branch {branchNumber} Required Items", string.Join(", ", requiredItems)));
                    branchSummary["required_items"] = requiredItems;
                }

                var stageNumber = 1;
                foreach (var stage in stages.EnumerateArray())
                {
                    var stageText = DescribeQuestStage(stage, questName);
                    if (!string.IsNullOrWhiteSpace(stageText))
                    {
                        fields.Add(new LibrariumFieldRow($"Branch {branchNumber} Step {stageNumber}", stageText));
                    }

                    stagesSummary.Add(new Dictionary<string, object?>
                    {
                        ["stage"] = stageNumber,
                        ["textblock"] = FirstPathValue(stage, ["textblock_ref"]),
                        ["summary"] = stageText
                    });
                    stageNumber++;
                }
            }

            branchSummary["steps"] = stagesSummary;
            summaryBranches.Add(branchSummary);
            branchNumber++;
        }

        var rawJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = $"quest.{questName}",
            ["name"] = displayName,
            ["classification"] = "quest_summary",
            ["quest_group"] = questName,
            ["quest_flags"] = stateNames,
            ["branches"] = summaryBranches
        }, PrettyJsonOptions);

        var searchBlob = string.Join(' ', "quests", questName, displayName, rawJson);
        return new LibrariumRecordRow("quests", "quests.json", $"quest.{questName}", displayName, "Quest", $"{flows.Count:N0} NPC dialogue branches", rawJson, searchBlob, fields);
    }

    private static string BuildQuestSummaryDisplayName(string questGroup, IReadOnlyList<LibrariumRecordRow> flows)
    {
        if (!questGroup.StartsWith("alignment.", StringComparison.OrdinalIgnoreCase))
        {
            return FriendlyQuestName(questGroup);
        }

        var keywords = flows
            .SelectMany(ReadQuestKeywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var keywordText = string.Join(" ", keywords).ToLowerInvariant();
        if (keywordText.Contains("spirit dagger of goijar") || keywordText.Contains("goijar")) return "Spirit Dagger of Goijar Quest";
        if (keywordText.Contains("mithril") || keywordText.Contains("forge")) return "Mithril Forge Quest";
        if (keywordText.Contains("kobold") || keywordText.Contains("crown")) return "Kobold Crown Quest";
        if (keywordText.Contains("balthazar") || keywordText.Contains("ingredient")) return "Balthazar Alliance Quest";
        if (keywordText.Contains("grey lord") || keywordText.Contains("webbing")) return "Grey Lord Quest";
        if (keywordText.Contains("annora") || keywordText.Contains("gem")) return "Annora Gem Quest";
        if (keywordText.Contains("deed") || keywordText.Contains("retrieve")) return "Balthazar Deed Quest";
        if (keywordText.Contains("prophecy")) return "Prophecy Quest";
        if (keywordText.Contains("demon")) return "Demon Quest";
        if (keywordText.Contains("messenger")) return "Wounded Messenger Quest";
        if (keywordText.Contains("chancellor") || keywordText.Contains("package")) return "Chancellor Package Quest";
        if (keywordText.Contains("adventure") || keywordText.Contains("gold")) return "Gold Delivery Quest";
        if (keywordText.Contains("festus")) return "Festus Quest";
        if (keywordText.Contains("mission")) return "Alignment Mission Quest";

        var firstNpc = flows.Select(ReadQuestNpcNameFromRaw).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        return string.IsNullOrWhiteSpace(firstNpc) ? "Alignment Quest" : $"{firstNpc} Quest";
    }

    private static string ReadQuestNpcNameFromRaw(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        var npcRef = FirstPathValue(root, ["npc_ref"], ["relationships", "npc"]);
        var npcName = FirstPathValue(root, ["npc_name"]);
        if (string.IsNullOrWhiteSpace(npcName)) npcName = ResolveNpcName(npcRef);
        return npcName;
    }

    private static IEnumerable<string> ReadQuestKeywords(LibrariumRecordRow row)
    {
        using var document = JsonDocument.Parse(row.RawJson);
        var root = document.RootElement;
        if (!TryReadPath(root, ["relationships", "command_keywords"], out var keywords) ||
            keywords.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var keyword in keywords.EnumerateArray())
        {
            var text = ValueToText(keyword);
            if (!string.IsNullOrWhiteSpace(text)) yield return text;
        }
    }

    private static string ReadQuestStateNames(IReadOnlyList<LibrariumRecordRow> flows)
    {
        var names = flows
            .Select(flow =>
            {
                using var document = JsonDocument.Parse(flow.RawJson);
                return FirstPathValue(document.RootElement, ["quest_flag_name"]);
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(FriendlyQuestName)
            .ToArray();

        return names.Length == 0 ? "Unknown" : string.Join(", ", names);
    }

    private static bool TryBuildKnownQuestGuide(string questName, IReadOnlyList<LibrariumRecordRow> flows, out LibrariumRecordRow row)
    {
        if (!questName.Equals("PhoenixQuest", StringComparison.OrdinalIgnoreCase))
        {
            row = null!;
            return false;
        }

        var guide = new[]
        {
            "Locate the Old Man in the Asylum. Greet and speak with the old man.",
            "Exit the game, come back into the game. Go south, then west. You will be outside of the Asylum.",
            "Go to the Thieves' Guild. Purchase a Skeleton Key.",
            "Kill the Dread Mystic. You will get a Yellow Note.",
            "Kill the Flesh Golem. You will get a key to open the door there.",
            "Go into the Library and read the scroll or parchment.",
            "Kill the Smuggler Boss. You will get a Smudged Paper.",
            "Go to Orfeo and ask him about Morukai.",
            "Greet Morukai and ask him about Orfeo.",
            "Ask Morukai about the highlighted topics.",
            "Find the four components: eggs, quartz, roots, and acid glands.",
            "Eggs: go below the Tilled Fields in the Gnome Village.",
            "Quartz: go to the Bandit Keep near Aldreth's Tree. At the door, search north for a secret passage. Go tree and you will be inside the keep.",
            "Roots: go past the Jagged Hills in the Dragonteeth Hills. The Jagged Hills are on the northern side of the Dragonteeth Hills between Silvermere and Passage to the Silver River.",
            "Acid Glands: go below the Mad Wizard. Kill the White Jelly.",
            "Go to the Gnome Alchemist and ask about Potion. Take 1 platinum for the purchase.",
            "Go to the Gnome Inventor and ask about FORKS.",
            "Return to Morukai and ask about components. Ask about Return.",
            "Locate the Haunting Spirit.",
            "Type DESTROY BARRIER to use the Magical Rod of Quartz and open the passage.",
            "Find the Necromancer beyond the Shimmering Barrier and kill him for the Phoenix Egg.",
            "Return to Morukai and ask about INFORMATION.",
            "Go to the Lava Fields and locate the door to the north. Use the Basalt Key to open the door. Once inside, type HURL EGG."
        };

        var fields = new ObservableCollection<LibrariumFieldRow>
        {
            new("Quest", "Phoenix Feather Quest"),
            new("Quest State", questName),
            new("Required Items", "Skeleton Key, Yellow Note, Flesh Golem key, Smudged Paper, eggs, quartz, roots, acid glands, potion, forks, Magical Rod of Quartz, Phoenix Egg, Basalt Key"),
            new("Branches", $"{flows.Count:N0} decoded NPC dialogue branches are available behind this guide.")
        };

        for (var index = 0; index < guide.Length; index++)
        {
            fields.Add(new LibrariumFieldRow($"Step {index + 1}", guide[index]));
        }

        var rawJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = $"quest.{questName}",
            ["name"] = "Phoenix Feather Quest",
            ["classification"] = "quest_guide",
            ["quest_flag_name"] = questName,
            ["guide_source"] = "user_verified_host_walkthrough",
            ["steps"] = guide,
            ["decoded_branch_count"] = flows.Count,
            ["decoded_branches"] = flows.Select(flow => flow.Id).ToArray()
        }, PrettyJsonOptions);

        var searchBlob = string.Join(' ', "quests", questName, "Phoenix Feather Quest", string.Join(' ', guide), rawJson);
        row = new LibrariumRecordRow("quests", "quests.json", $"quest.{questName}", "Phoenix Feather Quest", "Quest Guide", $"{guide.Length:N0} walkthrough steps", rawJson, searchBlob, fields);
        return true;
    }

    private static string FriendlyQuestName(string questName)
    {
        if (string.IsNullOrWhiteSpace(questName)) return "Quest";
        var name = questName.EndsWith("Quest", StringComparison.OrdinalIgnoreCase)
            ? questName[..^5]
            : questName;
        return FriendlyLabel(name) + " Quest";
    }

    private static string ReadStringArray(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (!TryReadPath(root, path, out var value) || IsEmpty(value)) continue;
            if (value.ValueKind == JsonValueKind.Array)
            {
                return string.Join(", ", value.EnumerateArray()
                    .Select(ValueToText)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            }

            return ValueToText(value);
        }

        return "";
    }

    private static string DescribeQuestStage(JsonElement stage, string questName)
    {
        if (!TryReadPath(stage, ["operations"], out var operations) ||
            operations.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var lines = operations.EnumerateArray()
            .GroupBy(operation => FirstPathValue(operation, ["line"]))
            .Select(group => DescribeQuestLine(group.ToArray(), questName))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> ReadRequiredItems(JsonElement stage)
    {
        if (!TryReadPath(stage, ["operations"], out var operations) ||
            operations.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var operation in operations.EnumerateArray())
        {
            if (!FirstPathValue(operation, ["opcode"]).Equals("takeitem", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var item = ResolveItemName(FirstPathValue(operation, ["item_ref"], ["item_id"]));
            if (!string.IsNullOrWhiteSpace(item))
            {
                yield return item;
            }
        }
    }

    private static string DescribeQuestLine(IReadOnlyList<JsonElement> operations, string questName)
    {
        var actions = new List<string>();
        foreach (var operation in operations)
        {
            var opcode = FirstPathValue(operation, ["opcode"]).ToLowerInvariant();
            var item = ResolveItemName(FirstPathValue(operation, ["item_ref"], ["item_id"]));
            var textblock = FirstPathValue(operation, ["target_textblock_ref"], ["target_textblock_id"]);

            switch (opcode)
            {
                case "checkability":
                case "testability":
                case "failability":
                case "goodaligned":
                case "evilaligned":
                case "neutralaligned":
                    break;
                case "takeitem":
                    actions.Add(string.IsNullOrWhiteSpace(item) ? "bring or turn in the required item" : $"bring or turn in {item}");
                    break;
                case "giveitem":
                    actions.Add(string.IsNullOrWhiteSpace(item) ? "give the reward item" : $"give {item}");
                    break;
                case "giveability":
                    break;
                case "text":
                case "message":
                    actions.Add(string.IsNullOrWhiteSpace(textblock) ? "show dialogue" : $"show dialogue {textblock}");
                    break;
                case "teleport":
                    actions.Add("move the player to the quest destination");
                    break;
                case "summon":
                    actions.Add("summon the quest creature or NPC");
                    break;
                case "cast":
                    actions.Add("apply the quest spell effect");
                    break;
                case "remoteaction":
                    actions.Add("run the linked world action");
                    break;
                default:
                    var raw = FirstPathValue(operation, ["raw"]);
                    if (!string.IsNullOrWhiteSpace(raw)) actions.Add(FriendlyLabel(raw));
                    break;
            }
        }

        return actions.Count == 0 ? "" : string.Join("; ", actions.Distinct(StringComparer.OrdinalIgnoreCase)) + ".";
    }

    public static string ResolveNpcName(string npcRef)
    {
        var id = ReadReferenceNumber(npcRef);
        return id != 0 && NpcNames.TryGetValue(id, out var name) ? name : npcRef;
    }

    private static string ResolveItemName(string itemRef)
    {
        var id = ReadReferenceNumber(itemRef);
        return id != 0 && ItemNames.TryGetValue(id, out var name) ? name : itemRef;
    }

    private static int ReadReferenceNumber(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return 0;
        var digits = new string(reference.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var id) ? id : 0;
    }

    private static string BuildQuestDisplayName(JsonElement root, string fallback)
    {
        var questName = FirstPathValue(root, ["quest_flag_name"]);
        var npc = FirstPathValue(root, ["relationships", "npc"]);
        if (!string.IsNullOrWhiteSpace(questName) && !string.IsNullOrWhiteSpace(npc))
        {
            return $"{questName} ({npc})";
        }

        if (!string.IsNullOrWhiteSpace(questName))
        {
            return questName;
        }

        const string prefix = "Textblock quest flow: ";
        return fallback.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? fallback[prefix.Length..]
            : fallback;
    }

    public static LibrariumRecordRow FromError(Exception exception)
    {
        var fields = new ObservableCollection<LibrariumFieldRow>
        {
            new("error", exception.Message),
            new("details", exception.ToString())
        };
        return new LibrariumRecordRow("error", "", "error", "Librarium load error", "error", exception.Message, exception.ToString(), exception.ToString(), fields);
    }

    private static ObservableCollection<LibrariumFieldRow> BuildFields(string category, string rawJson)
    {
        var fields = new ObservableCollection<LibrariumFieldRow>();
        using var document = JsonDocument.Parse(rawJson);
        AddCuratedFields(category, document.RootElement, fields);
        if (fields.Count == 0)
        {
            FlattenImportant(document.RootElement, "", fields, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return fields;
    }

    private static void AddCuratedFields(string category, JsonElement root, ObservableCollection<LibrariumFieldRow> fields)
    {
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFirst(fields, added, "ID", root, ["id"], ["legacy_id"], ["record_id"], ["runtime_id"]);
        AddFirst(fields, added, "Name", root, ["name"], ["title"], ["display_name"], ["room_name"], ["npc_name"], ["item_name"], ["class_name"], ["race_name"]);

        switch (category)
        {
            case "weapons":
            case "armor":
            case "items":
                AddFirst(fields, added, "Type", root, ["item_type"], ["classification", "type"]);
                AddFirst(fields, added, "Gettable", root, ["gettable"], ["flags", "gettable"]);
                AddFirst(fields, added, "Weight", root, ["economy", "weight"], ["weight"]);
                AddFirst(fields, added, "Cost", root, ["economy", "cost"], ["cost"]);
                AddFirst(fields, added, "Worn On", root, ["equipment", "worn_on"], ["worn_on"]);
                AddFirst(fields, added, "Class Restricted", root, ["equipment", "class_restricted"]);
                AddFirst(fields, added, "Race Restricted", root, ["equipment", "race_restricted"]);
                AddFirst(fields, added, "Strength Required", root, ["equipment", "str_requirement"]);
                AddFirst(fields, added, "Min Damage", root, ["combat", "minimum_damage"], ["minimum_damage"]);
                AddFirst(fields, added, "Max Damage", root, ["combat", "maximum_damage"], ["maximum_damage"]);
                AddFirst(fields, added, "Armor Class", root, ["combat", "armor_class_bonus"], ["armor_class_bonus"]);
                AddFirst(fields, added, "Damage Resist", root, ["combat", "damage_resistance_bonus"], ["damage_resistance_bonus"]);
                AddFirst(fields, added, "Accuracy", root, ["combat", "accuracy"], ["accuracy"]);
                AddFirst(fields, added, "Speed", root, ["combat", "speed"], ["speed"]);
                AddFirst(fields, added, "Weapon Type", root, ["combat", "weapon_type"], ["classification", "weapon_type"]);
                AddFirst(fields, added, "Armor Type", root, ["combat", "armor_type"]);
                AddFirst(fields, added, "Is Armor", root, ["combat", "is_armor"], ["classification", "is_armor"]);
                AddFirst(fields, added, "Container", root, ["container"]);
                AddFirst(fields, added, "Consumable", root, ["consumable"]);
                AddFirst(fields, added, "Stackable", root, ["stackable"]);
                AddFirst(fields, added, "Charges", root, ["default_charges"], ["charges"]);
                break;
            case "rooms":
                AddFirst(fields, added, "Map", root, ["map_id"], ["map"], ["mapId"]);
                AddFirst(fields, added, "Room Number", root, ["room_number"], ["room"], ["roomNumber"]);
                AddFirst(fields, added, "Title", root, ["title"], ["name"], ["room_name"]);
                AddFirst(fields, added, "Description", root, ["description"], ["long_description"]);
                AddFirst(fields, added, "Area", root, ["area"], ["area_id"], ["region"], ["region_id"]);
                AddFirst(fields, added, "Terrain", root, ["terrain"], ["terrain_type"]);
                AddFirst(fields, added, "Flags", root, ["flags"]);
                AddFirst(fields, added, "Exits", root, ["exits"]);
                AddFirst(fields, added, "Doors", root, ["doors"]);
                AddFirst(fields, added, "Services", root, ["services"]);
                break;
            case "exits":
                AddFirst(fields, added, "Direction", root, ["direction"]);
                AddFirst(fields, added, "From Room", root, ["relationships", "from_room"]);
                AddFirst(fields, added, "To Room", root, ["relationships", "to_room"]);
                AddFirst(fields, added, "From Map", root, ["from_map_number"]);
                AddFirst(fields, added, "From Room Number", root, ["from_room_number"]);
                AddFirst(fields, added, "To Map", root, ["to_map_number"]);
                AddFirst(fields, added, "To Room Number", root, ["to_room_number"]);
                AddFirst(fields, added, "Door", root, ["door"]);
                AddFirst(fields, added, "Door Name", root, ["door_name"]);
                AddFirst(fields, added, "Flags", root, ["flags"]);
                AddFirst(fields, added, "Requirements", root, ["requirements"]);
                break;
            case "spells":
                AddFirst(fields, added, "Description", root, ["description"], ["public_reference_0139", "fields", "descriptions"], ["notes"]);
                AddFirst(fields, added, "Spell Level", root, ["classification", "spell_level"], ["spell_level"], ["public_reference_0139", "fields", "spell_level"]);
                AddFirst(fields, added, "Spell Type", root, ["classification", "spell_type"], ["spell_type"], ["public_reference_0139", "fields", "spell_type"]);
                AddFirst(fields, added, "Target Type", root, ["classification", "target_type"], ["target_type"], ["public_reference_0139", "fields", "target_type"]);
                AddFirst(fields, added, "Attack Type", root, ["classification", "attack_type"], ["attack_type"], ["public_reference_0139", "fields", "attack_type"]);
                AddFirst(fields, added, "Mana Cost", root, ["costs", "mana_cost"], ["mana_cost"], ["public_reference_0139", "fields", "mana_cost"]);
                AddFirst(fields, added, "Energy Cost", root, ["costs", "energy_cost"], ["energy_cost"], ["public_reference_0139", "fields", "energy_cost"]);
                AddFirst(fields, added, "Min Effect", root, ["effect", "min_effect"], ["min_effect"], ["public_reference_0139", "fields", "min_effect"]);
                AddFirst(fields, added, "Max Effect", root, ["effect", "max_effect"], ["max_effect"], ["public_reference_0139", "fields", "max_effect"]);
                AddFirst(fields, added, "Duration", root, ["effect", "duration"], ["duration"], ["public_reference_0139", "fields", "duration"]);
                AddFirst(fields, added, "Resist Type", root, ["resist_type"], ["public_reference_0139", "fields", "resist_type"]);
                AddFirst(fields, added, "Difficulty", root, ["difficulty"], ["public_reference_0139", "fields", "difficulty"]);
                AddFirst(fields, added, "Message Style", root, ["message_style"], ["public_reference_0139", "fields", "message_style"]);
                AddFirst(fields, added, "Abilities", root, ["abilities_named"], ["public_reference_0139", "fields", "abilities_named"]);
                AddFirst(fields, added, "Formulas", root, ["formula_refs"]);
                break;
            case "abilities":
                AddFirst(fields, added, "Description", root, ["description"], ["public_reference_0139", "fields", "description"], ["notes"]);
                AddFirst(fields, added, "Implementation Status", root, ["impl_status"], ["public_reference_0139", "fields", "impl_status"]);
                AddFirst(fields, added, "Color Red", root, ["color_red"], ["public_reference_0139", "fields", "color_red"]);
                AddFirst(fields, added, "Color Green", root, ["color_green"], ["public_reference_0139", "fields", "color_green"]);
                AddFirst(fields, added, "Color Blue", root, ["color_blue"], ["public_reference_0139", "fields", "color_blue"]);
                AddFirst(fields, added, "DLL Notes", root, ["dll_notes"], ["public_reference_0139", "fields", "dll_notes"]);
                break;
            case "monsters":
                AddFirst(fields, added, "Type", root, ["npc_type"], ["type"], ["classification", "type"]);
                AddFirst(fields, added, "Level", root, ["level"], ["stats", "level"]);
                AddFirst(fields, added, "HP", root, ["hp"], ["health"], ["stats", "hp"], ["stats", "health"]);
                AddFirst(fields, added, "Mana", root, ["mana"], ["stats", "mana"]);
                AddFirst(fields, added, "Alignment", root, ["alignment"], ["stats", "alignment"]);
                AddFirst(fields, added, "Experience", root, ["experience"], ["xp"], ["stats", "experience"]);
                AddFirst(fields, added, "Gold", root, ["gold"], ["currency", "gold"]);
                AddFirst(fields, added, "Armor Class", root, ["armor_class"], ["combat", "armor_class"]);
                AddFirst(fields, added, "Damage", root, ["damage"], ["combat", "damage"]);
                AddFirst(fields, added, "Attack", root, ["attack"], ["combat", "attack"]);
                AddFirst(fields, added, "Defense", root, ["defense"], ["combat", "defense"]);
                AddFirst(fields, added, "Disposition", root, ["disposition"], ["hostility"], ["combat", "hostility"]);
                AddFirst(fields, added, "Spawn", root, ["spawn"], ["spawn_id"], ["spawn_group"]);
                break;
            case "classes":
                AddFirst(fields, added, "Description", root, ["description"], ["notes"]);
                AddFirst(fields, added, "Class Number", root, ["number"], ["public_reference_0139", "fields", "legacy_number"]);
                AddHitPointRange(fields, added, root);
                AddFirst(fields, added, "Experience Modifier", root, ["experience_add_percent"], ["experience_percent"], ["public_reference_0139", "fields", "exp_modifier"]);
                AddFirst(fields, added, "Magic Type", root, ["magery_name"], ["magic_name"], ["magery_type"], ["public_reference_0139", "fields", "magic_type"], ["magic_type"]);
                AddFirst(fields, added, "Magic Level", root, ["magic_level"], ["public_reference_0139", "fields", "magic_level"]);
                AddFirst(fields, added, "Weapon Type", root, ["weapon_type_name"], ["weapon_name"], ["weapon_type"], ["public_reference_0139", "fields", "weapon_bonus"], ["weapon_bonus"]);
                AddFirst(fields, added, "Armor Type", root, ["armor_type_name"], ["armor_name"], ["armour_type"], ["armor_type"], ["public_reference_0139", "fields", "armor_bonus"], ["armor_bonus"]);
                AddFirst(fields, added, "Combat Rating", root, ["combat_rating_name"], ["combat_name"], ["combat_level"], ["public_reference_0139", "fields", "combat_bonus"], ["combat_bonus"]);
                AddAbilitySlots(fields, added, "Abilities", root);
                break;
            case "races":
                AddFirst(fields, added, "Description", root, ["description"], ["notes"]);
                AddFirst(fields, added, "Race Number", root, ["number"], ["public_reference_0139", "fields", "legacy_number"]);
                AddFirst(fields, added, "Character Point Cost", root, ["base_cp"], ["public_reference_0139", "fields", "cp_cost"]);
                AddFirst(fields, added, "Experience Modifier", root, ["experience_add_percent"], ["public_reference_0139", "fields", "exp_modifier"]);
                AddFirst(fields, added, "HP Per Level Adjustment", root, ["hp_per_level_adjustment"], ["public_reference_0139", "fields", "hp_bonus"]);
                AddStatRange(fields, added, "Strength", root, "str");
                AddStatRange(fields, added, "Intellect", root, "int");
                AddStatRange(fields, added, "Willpower", root, "wil");
                AddStatRange(fields, added, "Health", root, "hea");
                AddStatRange(fields, added, "Agility", root, "agl");
                AddStatRange(fields, added, "Charm", root, "cha");
                AddAbilitySlots(fields, added, "Abilities", root);
                break;
            case "quests":
                AddFirst(fields, added, "Description", root, ["description"], ["notes"]);
                AddFirst(fields, added, "Quest Type", root, ["classification"]);
                AddFirst(fields, added, "Quest State", root, ["quest_flag_name"]);
                AddFirst(fields, added, "Talk To", root, ["relationships", "npc"], ["npc"], ["npc_ref"]);
                AddFirst(fields, added, "Say", root, ["relationships", "command_keywords"], ["command_keywords"]);
                AddFirst(fields, added, "Needed Item", root, ["relationships", "item"], ["item"]);
                AddQuestSteps(fields, added, root);
                break;
        }
    }

    private static void AddFirst(ObservableCollection<LibrariumFieldRow> fields, HashSet<string> added, string label, JsonElement root, params string[][] paths)
    {
        if (added.Contains(label)) return;
        foreach (var path in paths)
        {
            if (!TryReadPath(root, path, out var value) || IsEmpty(value)) continue;
            fields.Add(new LibrariumFieldRow(label, ValueToText(value)));
            added.Add(label);
            return;
        }
    }

    private static void AddHitPointRange(ObservableCollection<LibrariumFieldRow> fields, HashSet<string> added, JsonElement root)
    {
        var min = FirstPathValue(root, ["min_hp"], ["public_reference_0139", "fields", "min_hp"]);
        var max = FirstPathValue(root, ["max_hp"], ["hp_roll_cap"], ["public_reference_0139", "fields", "max_hp"]);
        if (!string.IsNullOrWhiteSpace(min) && !string.IsNullOrWhiteSpace(max))
        {
            AddUnique(fields, added, "HP Per Level", $"{min}-{max}");
            return;
        }

        AddFirst(fields, added, "HP Per Level", root, ["hp_per_level"], ["hp_per_level_adjustment"]);
    }

    private static void AddStatRange(ObservableCollection<LibrariumFieldRow> fields, HashSet<string> added, string label, JsonElement root, string statKey)
    {
        var min = FirstPathValue(root, ["minimum_stats", statKey], ["public_reference_0139", "fields", "min_stats", statKey]);
        var max = FirstPathValue(root, ["maximum_stats", statKey], ["public_reference_0139", "fields", "max_stats", statKey]);
        if (!string.IsNullOrWhiteSpace(min) || !string.IsNullOrWhiteSpace(max))
        {
            AddUnique(fields, added, label, $"{Fallback(min, "?")} / {Fallback(max, "?")} min/max");
        }
    }

    private static void AddAbilitySlots(ObservableCollection<LibrariumFieldRow> fields, HashSet<string> added, string label, JsonElement root)
    {
        JsonElement slots;
        Func<JsonElement, string> formatter;
        if (TryReadPath(root, ["public_reference_0139", "fields", "abilities"], out slots) &&
            slots.ValueKind == JsonValueKind.Array &&
            slots.GetArrayLength() > 0)
        {
            formatter = DescribePublicAbilitySlot;
        }
        else if (TryReadPath(root, ["ability_slots"], out slots) &&
                 slots.ValueKind == JsonValueKind.Array &&
                 slots.GetArrayLength() > 0)
        {
            formatter = DescribeNativeAbilitySlot;
        }
        else if (TryReadPath(root, ["abilities"], out slots) &&
                 slots.ValueKind == JsonValueKind.Array &&
                 slots.GetArrayLength() > 0)
        {
            formatter = DescribePublicAbilitySlot;
        }
        else
        {
            return;
        }

        var values = slots.EnumerateArray()
            .Select(formatter)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        if (values.Length > 0)
        {
            AddUnique(fields, added, label, string.Join(", ", values));
        }
    }

    private static string DescribeNativeAbilitySlot(JsonElement slot)
    {
        if (slot.ValueKind != JsonValueKind.Object) return ValueToText(slot);

        var idText = FirstPathValue(slot, ["ability_id"]);
        _ = int.TryParse(idText, out var abilityId);
        var value = FirstPathValue(slot, ["value"], ["bonus"]);
        return DescribeAbilityValue(abilityId, value);
    }

    private static string DescribePublicAbilitySlot(JsonElement slot)
    {
        if (slot.ValueKind != JsonValueKind.Object) return ValueToText(slot);

        var idText = FirstPathValue(slot, ["value"], ["ability_id"]);
        _ = int.TryParse(idText, out var abilityId);
        var bonus = FirstPathValue(slot, ["bonus"]);
        return DescribeAbilityValue(abilityId, bonus);
    }

    private static string DescribeAbilityValue(int abilityId, string value)
    {
        var name = abilityId != 0 && AbilityNames.TryGetValue(abilityId, out var abilityName)
            ? abilityName
            : abilityId == 0 ? "" : $"Ability {abilityId}";
        if (string.IsNullOrWhiteSpace(name)) return "";
        return string.IsNullOrWhiteSpace(value) || value == "0" ? name : $"{name} ({value})";
    }

    private static void AddQuestSteps(ObservableCollection<LibrariumFieldRow> fields, HashSet<string> added, JsonElement root)
    {
        var npc = FirstPathValue(root, ["relationships", "npc"], ["npc"], ["npc_ref"]);
        var words = FirstPathValue(root, ["relationships", "command_keywords"], ["command_keywords"]);
        if (!string.IsNullOrWhiteSpace(npc) || !string.IsNullOrWhiteSpace(words))
        {
            var start = string.IsNullOrWhiteSpace(npc) ? "Start from the quest source" : $"Talk to {npc}";
            if (!string.IsNullOrWhiteSpace(words)) start += $"; try saying: {words}";
            AddUnique(fields, added, "How To Start", start);
        }

        if (TryReadPath(root, ["script_flow", "stages"], out var stages) &&
            stages.ValueKind == JsonValueKind.Array)
        {
            var step = 1;
            foreach (var stage in stages.EnumerateArray())
            {
                var operations = TryReadPath(stage, ["operations"], out var operationsElement) &&
                                 operationsElement.ValueKind == JsonValueKind.Array
                    ? operationsElement.EnumerateArray().Select(DescribeQuestOperation).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray()
                    : [];

                var value = operations.Length == 0
                    ? "No player action is required for this step."
                    : string.Join(" Then ", operations);
                AddUnique(fields, added, $"Step {step}", value);
                step++;
            }

            if (step > 1) return;
        }

        if (TryReadPath(root, ["relationships"], out var relationships) &&
            relationships.ValueKind == JsonValueKind.Object)
        {
            var parts = relationships.EnumerateObject()
                .Where(property => !IsEmpty(property.Value))
                .Select(property => $"{FriendlyLabel(property.Name)}: {ValueToText(property.Value)}")
                .ToArray();
            if (parts.Length > 0)
            {
                AddUnique(fields, added, "Step 1", string.Join("; ", parts));
                return;
            }
        }

        AddUnique(fields, added, "Steps", "No decoded step sequence is available in generated Classic data for this quest record.");
    }

    private static string DescribeQuestOperation(JsonElement operation)
    {
        var opcode = FirstPathValue(operation, ["opcode"]);
        if (string.IsNullOrWhiteSpace(opcode)) return "";

        var raw = FirstPathValue(operation, ["raw"]);
        var kind = FirstPathValue(operation, ["operation_kind"]);
        var ability = FirstPathValue(operation, ["quest_flag_name"], ["ability_ref"], ["ability_id"]);
        var abilityValue = FirstPathValue(operation, ["ability_value"]);
        var item = FirstPathValue(operation, ["item_ref"], ["item_id"]);
        var targetText = FirstPathValue(operation, ["target_textblock_ref"], ["target_textblock_id"]);

        return opcode.ToLowerInvariant() switch
        {
            "checkability" or "testability" => string.IsNullOrWhiteSpace(abilityValue)
                ? $"player must already be on {ability}."
                : $"player must be at {ability} step {abilityValue}.",
            "failability" => $"if the player has not started {ability}.",
            "giveability" => string.IsNullOrWhiteSpace(abilityValue)
                ? "advance the quest."
                : $"advance the quest to step {abilityValue}.",
            "takeitem" => string.IsNullOrWhiteSpace(item) ? "take the required item from the player." : $"take {item} from the player.",
            "giveitem" => string.IsNullOrWhiteSpace(item) ? "give the player an item." : $"give the player {item}.",
            "text" => string.IsNullOrWhiteSpace(targetText) ? "show the next dialogue." : "show the next dialogue.",
            "message" => string.IsNullOrWhiteSpace(targetText) ? "show a message." : "show a message.",
            "teleport" => "move the player to the quest destination.",
            "summon" => "summon the quest NPC or creature.",
            "cast" => "apply the quest spell effect.",
            "remoteaction" => "run a linked world action.",
            _ => string.IsNullOrWhiteSpace(kind) ? FriendlyLabel(Fallback(raw, opcode)) : $"{FriendlyLabel(kind)}."
        };
    }

    private static string FirstPathValue(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (TryReadPath(root, path, out var value) && !IsEmpty(value))
            {
                return ValueToText(value);
            }
        }

        return "";
    }

    private static string Fallback(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

    private static void AddUnique(ObservableCollection<LibrariumFieldRow> fields, HashSet<string> added, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var uniqueLabel = label;
        var index = 2;
        while (!added.Add(uniqueLabel))
        {
            uniqueLabel = $"{label} #{index++}";
        }

        fields.Add(new LibrariumFieldRow(uniqueLabel, value));
    }

    private static string PrettyPrint(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
        }
        catch
        {
            return rawJson;
        }
    }

    private static string FirstValue(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryFindProperty(element, name, out var value))
            {
                var text = ValueToText(value);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }

        return "";
    }

    private static bool TryFindProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object &&
                    TryFindProperty(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string ReadQualifiedId(JsonElement element)
    {
        var map = FirstValue(element, "map_id", "map", "mapId");
        var room = FirstValue(element, "room_number", "room", "roomNumber");
        if (!string.IsNullOrWhiteSpace(map) && !string.IsNullOrWhiteSpace(room))
        {
            return $"{map}:{room}";
        }

        return "";
    }

    private static string BuildPreview(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Trim(ValueToText(element), 180);
        }

        var values = new List<string>();
        foreach (var property in element.EnumerateObject().Take(8))
        {
            var text = ValueToText(property.Value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add($"{property.Name}: {Trim(text, 60)}");
            }
        }

        return string.Join("; ", values);
    }

    private static bool TryReadPath(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.Object => !value.EnumerateObject().Any(),
            _ => false
        };

    private static void FlattenImportant(JsonElement element, string prefix, ObservableCollection<LibrariumFieldRow> fields, int depth, HashSet<string> added)
    {
        if (fields.Count >= 120)
        {
            if (fields[^1].Path != "More")
            {
                fields.Add(new LibrariumFieldRow("More", "Additional raw data exists but is hidden from the game-facing view."));
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(prefix) && !IsImportantPath(prefix))
        {
            return;
        }

        if (depth > 8)
        {
            AddFallbackField(fields, added, prefix, element);
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var path = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenImportant(property.Value, path, fields, depth + 1, added);
                }
                break;
            case JsonValueKind.Array:
                var array = element.EnumerateArray().ToArray();
                if (array.Length == 0)
                {
                    return;
                }

                if (array.All(value => value.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array))
                {
                    AddFallbackField(fields, added, prefix, element);
                    return;
                }

                AddFallbackField(fields, added, prefix, element);
                for (var index = 0; index < Math.Min(array.Length, 20); index++)
                {
                    FlattenImportant(array[index], $"{prefix}[{index}]", fields, depth + 1, added);
                }
                break;
            default:
                AddFallbackField(fields, added, prefix, element);
                break;
        }
    }

    private static void AddFallbackField(ObservableCollection<LibrariumFieldRow> fields, HashSet<string> added, string path, JsonElement value)
    {
        if (IsEmpty(value)) return;

        var label = FriendlyLabel(path);
        if (!added.Add(label)) return;

        fields.Add(new LibrariumFieldRow(label, ValueToText(value)));
    }

    private static bool IsImportantPath(string path)
    {
        var normalized = path.ToLowerInvariant();
        var blocked = new[]
        {
            "evidence", "reference", "source", "confidence", "authority", "runtime_mapping",
            "generator_mapping", "validation", "history", "manifest", "provenance", "trace",
            "promotion", "override_policy", "librarium_enrichment", "compile", "checksum",
            "raw", "import", "relationship_summary", "version_history"
        };

        if (blocked.Any(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var allowed = new[]
        {
            "id", "legacy", "name", "title", "description", "type", "classification", "stats",
            "attributes", "strength", "intelligence", "willpower", "agility", "health", "charm",
            "hp", "mana", "level", "alignment", "experience", "xp", "gold", "cost", "weight",
            "economy", "equipment", "worn", "class", "race", "requirement", "flags", "gettable",
            "combat", "damage", "armor", "resistance", "accuracy", "speed", "weapon", "container",
            "consumable", "stackable", "charges", "map", "room", "area", "region", "terrain",
            "exit", "door", "lock", "key", "trap", "service", "shop", "trainer", "bank",
            "spawn", "disposition", "hostility", "abilities", "skills", "spells"
        };

        return allowed.Any(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string FriendlyLabel(string path)
    {
        var segment = path.Split('.', '[', ']').LastOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? path;
        var words = segment.Replace("_", " ", StringComparison.Ordinal).Replace("-", " ", StringComparison.Ordinal);
        return string.Join(' ', words.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Capitalize));
    }

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string ValueToText(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            JsonValueKind.Undefined => "",
            JsonValueKind.Array => SummarizeArray(value),
            JsonValueKind.Object => SummarizeObject(value),
            _ => Trim(value.ToString(), 600)
        };

    private static string SummarizeArray(JsonElement value)
    {
        var items = value.EnumerateArray().ToArray();
        if (items.Length == 0) return "";
        if (items.All(item => item.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array))
        {
            return Trim(string.Join(", ", items.Select(ValueToText)), 600);
        }

        return $"{items.Length:N0} entries";
    }

    private static string SummarizeObject(JsonElement value)
    {
        var parts = value.EnumerateObject()
            .Where(property => IsImportantPath(property.Name) && !IsEmpty(property.Value))
            .Take(8)
            .Select(property => $"{FriendlyLabel(property.Name)}: {ValueToText(property.Value)}");

        return Trim(string.Join("; ", parts), 600);
    }

    private static string Trim(string value, int maxLength)
    {
        value = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}

public sealed record LibrariumFieldRow(string Path, string Value);
