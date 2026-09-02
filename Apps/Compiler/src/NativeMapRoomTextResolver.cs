using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

public sealed class NativeMapRoomTextResolver
{
    private static readonly Regex NativeMapFilePattern = new(@"^WCCMP(?<map>\d{3})\.DAT$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const int RoomBlockSize = 1536;

    public RoomTextAuditResult ApplyDescriptions(string repositoryRoot, LibrariumFamily family)
    {
        var librariumDescriptions = LoadLibrariumDescriptions(repositoryRoot);
        if (librariumDescriptions.Count == 0)
            throw new InvalidDataException("Canonical Librarium room descriptions are required for generation.");
        return ApplyLibrariumDescriptions(repositoryRoot, family, librariumDescriptions);
    }

    private RoomTextAuditResult ApplyNativeDescriptions(string repositoryRoot, LibrariumFamily family)
    {
        var nativeBlocks = LoadNativeDescriptionBlocks(repositoryRoot);
        var queuesByMapAndTitle = nativeBlocks
            .GroupBy(block => (block.MapId, Title: NormalizeTitle(block.Title)))
            .ToDictionary(
                group => group.Key,
                group => new Queue<NativeRoomTextBlock>(group.OrderBy(block => block.BlockIndex)),
                new MapTitleComparer());

        var records = family.Records
            .Select((record, index) => new RoomRecordSlot(record, index))
            .OrderBy(slot => ReadInt(slot.Record, "map_number", "map_id", "mapId"))
            .ThenBy(slot => ReadInt(slot.Record, "room_number", "roomNumber"))
            .ThenBy(slot => slot.Index)
            .ToList();

        var unresolved = new List<RoomTextUnresolved>();
        var emitted = 0;
        var inlineDescriptions = 0;

        foreach (var slot in records)
        {
            var record = slot.Record;
            if (HasText(record, "description", "long_description", "desc1"))
            {
                inlineDescriptions++;
                continue;
            }

            var mapId = ReadInt(record, "map_number", "map_id", "mapId");
            var title = record["name"]?.GetValue<string>() ?? string.Empty;
            if (mapId <= 0 || string.IsNullOrWhiteSpace(title))
            {
                unresolved.Add(RoomTextUnresolved.FromRecord(record, "missing map/title"));
                continue;
            }

            var key = (mapId, Title: NormalizeTitle(title));
            if (!queuesByMapAndTitle.TryGetValue(key, out var queue) || queue.Count == 0)
            {
                unresolved.Add(RoomTextUnresolved.FromRecord(record, "no native map text block matched title"));
                continue;
            }

            var block = queue.Dequeue();
            if (string.IsNullOrWhiteSpace(block.Description))
            {
                unresolved.Add(RoomTextUnresolved.FromRecord(record, "matched native map block has empty description"));
                continue;
            }

            record["description"] = block.Description;
            record["description_evidence"] = new JsonObject
            {
                ["source"] = "native_map_dat",
                ["source_file"] = block.SourceFile.Replace('\\', '/'),
                ["map_id"] = block.MapId,
                ["block_index"] = block.BlockIndex,
                ["title_offset"] = block.TitleOffset,
                ["description_line_count"] = block.DescriptionLineCount,
                ["resolver"] = "title_order_within_map",
                ["classification"] = "binary_derived_room_description"
            };
            emitted++;
        }

        return new RoomTextAuditResult(
            family.Records.Count,
            inlineDescriptions,
            DescReferenceCount: 0,
            LongDescReferenceCount: 0,
            nativeBlocks.Count,
            ResolvedReferences: emitted,
            UnresolvedReferences: unresolved.Count,
            EmittedNonEmptyDescriptions: emitted + inlineDescriptions,
            EmittedEmptyDescriptions: family.Records.Count - emitted - inlineDescriptions,
            DuplicateNativeTitles: CountDuplicateTitles(nativeBlocks),
            MissingNativeMapFiles: FindMissingNativeMapFiles(repositoryRoot, records),
            unresolved.Take(25).ToList());
    }

    public int ExportPromotedDescriptions(string repositoryRoot, LibrariumFamily family, string outputPath)
    {
        var clonedRecords = family.Records.Select(record => (JsonObject)record.DeepClone()).ToList();
        var clonedFamily = new LibrariumFamily(family.Name, family.SourceFile, clonedRecords);
        ApplyNativeDescriptions(repositoryRoot, clonedFamily);

        var records = clonedRecords
            .Where(record => !string.IsNullOrWhiteSpace(record["description"]?.GetValue<string>()))
            .Select(record => new JsonObject
            {
                ["id"] = $"room_description.{record["id"]?.GetValue<string>()}",
                ["room_id"] = record["id"]?.GetValue<string>() ?? string.Empty,
                ["legacy_id"] = record["legacy_id"]?.DeepClone(),
                ["map_number"] = record["map_number"]?.DeepClone(),
                ["room_number"] = record["room_number"]?.DeepClone(),
                ["name"] = record["name"]?.DeepClone(),
                ["object_type"] = "room_description",
                ["world_scope"] = new JsonArray("classic"),
                ["description"] = record["description"]?.GetValue<string>() ?? string.Empty,
                ["evidence_refs"] = new JsonArray(record["description_evidence"]?.DeepClone()),
                ["classic_lock"] = "evidence_backed_immutable",
                ["runtime_mapping"] = new JsonObject
                {
                    ["source"] = "Librarium/Objects/room_descriptions",
                    ["generated_only"] = true,
                    ["runtime_reads_generated_data"] = true,
                    ["status"] = "promoted_binary_derived_room_description",
                    ["exports_to"] = "/Data/<world>/rooms.json"
                }
            })
            .OrderBy(record => ReadInt(record, "map_number"))
            .ThenBy(record => ReadInt(record, "room_number"))
            .ToList();

        var root = new JsonObject
        {
            ["schema"] = "librarium_room_description_records_v1",
            ["code_update"] = "0139",
            ["source_of_truth"] = "binary_derived_native_map_dat",
            ["record_count"] = records.Count,
            ["records"] = new JsonArray(records.Select(record => record.DeepClone()).ToArray())
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return records.Count;
    }

    public RoomTextAuditResult Audit(string repositoryRoot, LibrariumFamily family)
    {
        var cloned = new LibrariumFamily(
            family.Name,
            family.SourceFile,
            family.Records.Select(record => (JsonObject)record.DeepClone()).ToList());
        return ApplyDescriptions(repositoryRoot, cloned);
    }

    private static RoomTextAuditResult ApplyLibrariumDescriptions(
        string repositoryRoot,
        LibrariumFamily family,
        IReadOnlyDictionary<string, LibrariumRoomDescriptionRecord> descriptionsByRoomId)
    {
        var unresolved = new List<RoomTextUnresolved>();
        var emitted = 0;
        var inlineDescriptions = 0;

        foreach (var record in family.Records)
        {
            if (HasText(record, "description", "long_description", "desc1"))
            {
                inlineDescriptions++;
                continue;
            }

            var id = record["id"]?.GetValue<string>() ?? string.Empty;
            if (!descriptionsByRoomId.TryGetValue(id, out var descriptionRecord))
            {
                unresolved.Add(RoomTextUnresolved.FromRecord(record, "no Librarium room_description record matched room id"));
                continue;
            }

            var description = descriptionRecord.Record["description"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(description))
            {
                unresolved.Add(RoomTextUnresolved.FromRecord(record, "matched Librarium room_description record has empty description"));
                continue;
            }

            record["description"] = description;
            var evidence = new JsonObject
            {
                ["source"] = "librarium_room_description",
                ["source_file"] = descriptionRecord.SourceFile,
                ["record_id"] = descriptionRecord.Record["id"]?.GetValue<string>() ?? string.Empty,
                ["classification"] = ReadClassification(descriptionRecord.Record)
            };
            if (descriptionRecord.Record["classic_lock"] is not null)
                evidence["classic_lock"] = descriptionRecord.Record["classic_lock"]!.DeepClone();
            if (descriptionRecord.Record["evidence_refs"] is not null)
                evidence["librarium_evidence_refs"] = descriptionRecord.Record["evidence_refs"]!.DeepClone();
            record["description_evidence"] = evidence;
            emitted++;
        }

        return new RoomTextAuditResult(
            family.Records.Count,
            inlineDescriptions,
            DescReferenceCount: descriptionsByRoomId.Count,
            LongDescReferenceCount: 0,
            descriptionsByRoomId.Count,
            ResolvedReferences: emitted,
            UnresolvedReferences: unresolved.Count,
            EmittedNonEmptyDescriptions: emitted + inlineDescriptions,
            EmittedEmptyDescriptions: family.Records.Count - emitted - inlineDescriptions,
            DuplicateNativeTitles: 0,
            MissingNativeMapFiles: unresolved.Count == 0
                ? Array.Empty<string>()
                : FindMissingNativeMapFiles(
                    repositoryRoot,
                    family.Records.Select((record, index) => new RoomRecordSlot(record, index)).ToList()),
            unresolved.Take(25).ToList());
    }

    private static IReadOnlyDictionary<string, LibrariumRoomDescriptionRecord> LoadLibrariumDescriptions(string repositoryRoot)
    {
        var root = Path.Combine(repositoryRoot, "Librarium", "Objects", "room_descriptions");
        if (!Directory.Exists(root)) return new Dictionary<string, LibrariumRoomDescriptionRecord>(StringComparer.OrdinalIgnoreCase);

        var merged = new Dictionary<string, LibrariumRoomDescriptionRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var selected in Directory.GetFiles(root, "*.json")
                     .Where(IsRoomDescriptionRecordFile)
                     .OrderBy(RoomDescriptionSourcePriority)
                     .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(repositoryRoot, selected).Replace('\\', '/');
            var node = JsonNode.Parse(File.ReadAllText(selected));
            var records = node is JsonObject obj && obj["records"] is JsonArray array
                ? array.OfType<JsonObject>()
                : node is JsonArray direct
                    ? direct.OfType<JsonObject>()
                    : [];

            foreach (var record in records)
            {
                var roomId = record["room_id"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(roomId)) continue;
                merged.TryAdd(roomId, new LibrariumRoomDescriptionRecord(record, relative));
            }
        }

        return merged;
    }

    private static bool IsRoomDescriptionRecordFile(string path)
    {
        var filename = Path.GetFileName(path);
        return filename.EndsWith("_records.json", StringComparison.OrdinalIgnoreCase) ||
               filename.Contains("_records_", StringComparison.OrdinalIgnoreCase);
    }

    private static int RoomDescriptionSourcePriority(string path)
    {
        var filename = Path.GetFileName(path);
        if (filename.Contains("native", StringComparison.OrdinalIgnoreCase)) return 0;
        if (filename.Contains("wccmp", StringComparison.OrdinalIgnoreCase)) return 1;
        if (filename.Contains("binary", StringComparison.OrdinalIgnoreCase)) return 1;
        return 10;
    }

    private static string ReadClassification(JsonObject record)
    {
        if (record["classic_lock"]?.GetValue<string>() == "evidence_backed_immutable")
            return "binary_derived_room_description_promoted_to_librarium";
        return "reference_room_description_promoted_to_librarium";
    }

    private static IReadOnlyList<NativeRoomTextBlock> LoadNativeDescriptionBlocks(string repositoryRoot)
    {
        var candidates = new[]
        {
            Path.Combine(repositoryRoot, "ImportedDatabases", "NativeRuntime")
        };

        var blocks = new List<NativeRoomTextBlock>();
        foreach (var directory in candidates.Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "WCCMP*.DAT", SearchOption.TopDirectoryOnly)
                         .Concat(Directory.EnumerateFiles(directory, "wccmp*.dat", SearchOption.TopDirectoryOnly))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var match = NativeMapFilePattern.Match(Path.GetFileName(path));
                if (!match.Success) continue;
                if (!int.TryParse(match.Groups["map"].Value, out var mapId)) continue;

                var relative = Path.GetRelativePath(repositoryRoot, path);
                var data = File.ReadAllBytes(path);
                foreach (var block in ReadBlocks(data, mapId, relative))
                    blocks.Add(block);
            }
        }

        return blocks;
    }

    private static IEnumerable<NativeRoomTextBlock> ReadBlocks(byte[] data, int mapId, string sourceFile)
    {
        var count = data.Length / RoomBlockSize;
        for (var blockIndex = 0; blockIndex < count; blockIndex++)
        {
            var offset = blockIndex * RoomBlockSize;
            var length = Math.Min(RoomBlockSize, data.Length - offset);
            var strings = ExtractPrintableStrings(data, offset, length).ToList();
            if (strings.Count == 0) continue;

            var title = strings
                .FirstOrDefault(entry => IsLikelyTitle(entry.Text));
            if (title is null) continue;

            var descriptionLines = strings
                .Where(entry => entry.Offset > title.Offset)
                .Select(entry => entry.Text.TrimEnd())
                .Where(text => IsDescriptionLine(text))
                .ToList();

            if (descriptionLines.Count == 0) continue;

            yield return new NativeRoomTextBlock(
                mapId,
                blockIndex,
                title.Offset,
                title.Text.Trim(),
                string.Join("\n", descriptionLines),
                descriptionLines.Count,
                sourceFile);
        }
    }

    private static IEnumerable<PrintableString> ExtractPrintableStrings(byte[] data, int offset, int length)
    {
        var start = -1;
        for (var i = offset; i < offset + length; i++)
        {
            var printable = data[i] >= 0x20 && data[i] <= 0x7e;
            if (printable)
            {
                if (start < 0) start = i;
                continue;
            }

            if (start >= 0 && i - start >= 4)
            {
                yield return new PrintableString(start, System.Text.Encoding.Latin1.GetString(data, start, i - start));
            }

            start = -1;
        }

        var end = offset + length;
        if (start >= 0 && end - start >= 4)
            yield return new PrintableString(start, System.Text.Encoding.Latin1.GetString(data, start, end - start));
    }

    private static bool IsLikelyTitle(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length < 3) return false;
        if (trimmed.EndsWith(".ANS", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.Contains("  ", StringComparison.Ordinal)) return false;
        return trimmed.Any(char.IsLetter) && trimmed.Length <= 80;
    }

    private static bool IsDescriptionLine(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length < 4) return false;
        if (trimmed.EndsWith(".ANS", StringComparison.OrdinalIgnoreCase)) return false;
        return trimmed.Any(char.IsLetter);
    }

    private static bool HasText(JsonObject record, params string[] keys)
        => keys.Any(key => !string.IsNullOrWhiteSpace(record[key]?.GetValue<string>()));

    private static int ReadInt(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = obj[key];
            if (value is null) continue;
            if (value is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<int>(out var number)) return number;
                if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out number)) return number;
            }
        }
        return 0;
    }

    private static string NormalizeTitle(string value)
        => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");

    private static int CountDuplicateTitles(IReadOnlyList<NativeRoomTextBlock> blocks)
        => blocks.GroupBy(block => (block.MapId, Title: NormalizeTitle(block.Title)))
            .Count(group => group.Count() > 1);

    private static IReadOnlyList<string> FindMissingNativeMapFiles(string repositoryRoot, IReadOnlyList<RoomRecordSlot> records)
    {
        var available = LoadNativeDescriptionBlocks(repositoryRoot)
            .Select(block => block.MapId)
            .Distinct()
            .ToHashSet();
        return records
            .Select(slot => ReadInt(slot.Record, "map_number", "map_id", "mapId"))
            .Where(map => map > 0 && !available.Contains(map))
            .Distinct()
            .Order()
            .Select(map => $"WCCMP{map:000}.DAT")
            .ToList();
    }

    private sealed class MapTitleComparer : IEqualityComparer<(int MapId, string Title)>
    {
        public bool Equals((int MapId, string Title) x, (int MapId, string Title) y)
            => x.MapId == y.MapId && string.Equals(x.Title, y.Title, StringComparison.Ordinal);

        public int GetHashCode((int MapId, string Title) obj)
            => HashCode.Combine(obj.MapId, obj.Title);
    }

    private sealed record PrintableString(int Offset, string Text);
    private sealed record NativeRoomTextBlock(
        int MapId,
        int BlockIndex,
        int TitleOffset,
        string Title,
        string Description,
        int DescriptionLineCount,
        string SourceFile);
    private sealed record LibrariumRoomDescriptionRecord(JsonObject Record, string SourceFile);
    private sealed record RoomRecordSlot(JsonObject Record, int Index);
}

public sealed record RoomTextAuditResult(
    int SourceRooms,
    int InlineDescriptions,
    int DescReferenceCount,
    int LongDescReferenceCount,
    int TextRecordsLoaded,
    int ResolvedReferences,
    int UnresolvedReferences,
    int EmittedNonEmptyDescriptions,
    int EmittedEmptyDescriptions,
    int DuplicateNativeTitles,
    IReadOnlyList<string> MissingNativeMapFiles,
    IReadOnlyList<RoomTextUnresolved> FirstUnresolvedRooms);

public sealed record RoomTextUnresolved(
    string RoomId,
    string Title,
    int MapId,
    int RoomNumber,
    string Reference,
    string Family,
    string Reason)
{
    public static RoomTextUnresolved FromRecord(JsonObject record, string reason)
        => new(
            record["id"]?.GetValue<string>() ?? string.Empty,
            record["name"]?.GetValue<string>() ?? string.Empty,
            ReadInt(record, "map_number", "map_id", "mapId"),
            ReadInt(record, "room_number", "roomNumber"),
            string.Empty,
            "native_map_dat",
            reason);

    private static int ReadInt(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = obj[key];
            if (value is null) continue;
            if (value is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<int>(out var number)) return number;
                if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out number)) return number;
            }
        }
        return 0;
    }
}
