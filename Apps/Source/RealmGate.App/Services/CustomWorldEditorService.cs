using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using RealmGate.Compiler;
using RealmGate.Server;

namespace RealmGate.App.Services;

public sealed record CustomWorldEditorSettings(
    string Id,
    string Name,
    string Description,
    string LoginBanner,
    string Motd,
    bool RuntimeEnabled,
    bool TransfersAllowed,
    string DataRoot);

public sealed record CustomWorldOverrideSummary(
    string Family,
    string TargetId,
    string Operation,
    int FieldCount,
    string FieldsJson);

public sealed record CustomWorldObjectSummary(
    string Family,
    string Id,
    string Name);

public sealed record CustomWorldRoomMapLink(
    string Direction,
    string ExitId,
    string DestinationRoomId,
    string DestinationName);

public sealed record CustomWorldVisualMapRoom(
    string RoomId,
    string Name,
    int MapNumber,
    int RoomNumber,
    string Marker,
    double X,
    double Y);

public sealed record CustomWorldVisualMapEdge(
    string ExitId,
    string FromRoomId,
    string ToRoomId,
    string Direction);

public sealed record CustomWorldVisualMapSnapshot(
    int MapNumber,
    IReadOnlyList<int> AvailableMaps,
    IReadOnlyList<CustomWorldVisualMapRoom> Rooms,
    IReadOnlyList<CustomWorldVisualMapEdge> Edges);

public sealed record CustomWorldVisualMapPosition(
    string RoomId,
    int MapNumber,
    double X,
    double Y);

public sealed record CustomWorldShopInventoryEntry(
    string ItemRef,
    int MaxQty,
    int NormalQty,
    int RegenQty,
    int RegenTime,
    int RegenPercent);

public sealed record CustomWorldEditorSnapshot(
    CustomWorldEditorSettings Settings,
    IReadOnlyList<CustomWorldOverrideSummary> Overrides,
    string ProjectRoot,
    string ConfigPath,
    string OverridePath,
    string GeneratedDataPath,
    bool GeneratedDataReady);

public sealed record CustomWorldBuildResult(
    string WorldId,
    string OutputRoot,
    int Families,
    int Objects,
    int Files,
    IReadOnlyList<string> Diagnostics);

public sealed record CustomWorldReferenceHit(
    string Family,
    string Id,
    string Name,
    string Path,
    string Value);

public sealed record CustomWorldValidationIssue(
    string Severity,
    string Family,
    string Id,
    string Name,
    string Path,
    string Message);

public sealed record CustomWorldNpcAttackSimulationRow(
    int Slot,
    string Kind,
    int SelectionThreshold,
    int Attempts,
    int Hits,
    int Misses,
    int Dodges,
    int CastSuccesses,
    int CastFailures,
    long TotalDamage,
    double AverageDamage,
    int MinimumDamage,
    int MaximumDamage,
    int Accuracy,
    int EnergyCost);

public sealed record CustomWorldNpcSimulationResult(
    string NpcId,
    string NpcName,
    int Rounds,
    int TotalSelections,
    int PhysicalHits,
    int PhysicalMisses,
    int PhysicalDodges,
    int SpellCasts,
    int SpellFailures,
    long TotalPhysicalDamage,
    double AveragePhysicalDamagePerRound,
    IReadOnlyList<CustomWorldNpcAttackSimulationRow> Attacks,
    string Notes);


public sealed record CustomWorldGlobalSearchHit(
    string Family,
    string Id,
    string Name,
    string Match);

public sealed record CustomWorldImportPreview(
    string FilePath,
    int Records,
    int NewRecords,
    int Conflicts,
    int InvalidRecords,
    string Summary);

public sealed record CustomWorldBulkEditResult(
    int Requested,
    int Changed,
    int Skipped,
    string Summary);

public sealed record CustomWorldKeyDoorRow(
    string ExitId,
    string FromRoomId,
    string ToRoomId,
    string Direction,
    string KeyItemId,
    string KeyItemName,
    string Qualifier);

public sealed record CustomWorldNightmareParityRow(
    string NightmareFeature,
    string Status,
    string ReforgedEquivalent,
    string Notes);

public sealed record CustomWorldMonsterIndexRow(
    string NpcId,
    string Name,
    int Level,
    int HitPoints,
    int Experience,
    int GroupId,
    int SpawnCount,
    int RoomCount,
    string Rooms);

public sealed record CustomWorldControlRoomRow(
    string RoomId,
    string RoomName,
    int InteractionCount,
    int TriggerCount,
    int ScriptCount,
    string Sources);

public sealed record CustomWorldTextblockStripPreviewRow(
    string TextblockId,
    string Name,
    string BeforePreview,
    string AfterPreview,
    int MatchCount);

public sealed record CustomWorldTextblockStripResult(
    int Previewed,
    int Changed,
    int Skipped,
    string Summary);

public sealed record CustomWorldWeaponSimulationResult(
    string ItemId,
    string ItemName,
    int Swings,
    int EffectiveAttackValue,
    int HitChance,
    int Hits,
    int Misses,
    int Dodges,
    int Criticals,
    long TotalDamage,
    double AverageDamagePerSwing,
    double AverageDamageOnHit,
    int MinimumDamage,
    int MaximumDamage,
    int Accuracy,
    int Speed,
    string Notes);


/// <summary>
/// First host editor for a single Classic-derived custom realm. Canonical edits are stored as
/// world-scoped Librarium override records; generated Data/Custom1 is rebuilt from Librarium.
/// </summary>
public sealed class CustomWorldEditorService
{
    public const string CustomWorldId = "Custom1";
    private const string Schema = "reforged_world_overrides_v1";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _projectRoot;
    private IReadOnlyDictionary<string, LibrariumFamily>? _catalogCache;
    private readonly object _catalogGate = new();
    private readonly object _overrideGate = new();
    private JsonObject? _overrideRootCache;
    private DateTime _overrideRootCacheWriteUtc;
    private long _overrideRootCacheLength = -1;
    private IReadOnlyList<CustomWorldOverrideSummary>? _overrideSummaryCache;
    private readonly Dictionary<string, CustomWorldSimpleFormDefinition> _discoveredFormCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NonRuntimeCreatorFamilies = new(
        [
            "room_messages", "classic_bug_fixes", "ai_profiles", "areas", "assets", "client_views",
            "conflicts", "events", "evidence_records", "guilds", "regions", "validation_rules", "world_overlays"
        ],
        StringComparer.OrdinalIgnoreCase);

    public CustomWorldEditorService(string? startDirectory = null)
    {
        _projectRoot = ResolveProjectRoot(startDirectory);
    }

    public string ProjectRoot => _projectRoot;
    public string ConfigPath => Path.Combine(_projectRoot, "Config", "server.worlds.json");
    public string OverridePath => WorldOverrideEngine.ResolveOverridePath(_projectRoot, CustomWorldId);
    public string GeneratedDataPath => Path.Combine(_projectRoot, "Data", CustomWorldId);
    public string MapLayoutPath => Path.Combine(Path.GetDirectoryName(OverridePath)!, "map-layout.json");

    public void WarmOverrideCache()
    {
        _ = ReadOverrides();
    }



    public IReadOnlyList<CustomWorldSimpleFamilyOption> GetEditableFamilies()
    {
        EnsureCatalogLoaded();
        return _catalogCache!.Keys
            .Where(family => !NonRuntimeCreatorFamilies.Contains(family))
            .OrderBy(CreatorFamilyOrder)
            .ThenBy(CustomWorldSimpleFormCatalog.LabelForFamily, StringComparer.OrdinalIgnoreCase)
            .Select(family => new CustomWorldSimpleFamilyOption(family, CustomWorldSimpleFormCatalog.LabelForFamily(family)))
            .ToArray();
    }

    private static int CreatorFamilyOrder(string family) => family.ToLowerInvariant() switch
    {
        "rooms" => 0, "exits" => 1, "spawns" => 2, "items" => 3, "npcs" => 4,
        "spells" => 5, "abilities" => 6, "native_message_payloads" => 7, "native_textblock_payloads" => 8,
        "room_interactions" => 9, "classes" => 10, "races" => 11, "shops" => 12,
        "quests" => 13, "triggers" => 14, "scripts" => 15, "skills" => 16, "commands" => 17,
        "formulas" => 18, _ => 100
    };

    public CustomWorldEditorSnapshot Load()
    {
        using (RealmGatePerformanceTrace.Measure("CUSTOM LOAD", "EnsureCustomWorldConfigured"))
            EnsureCustomWorldConfigured();

        CustomWorldEditorSettings settings;
        using (RealmGatePerformanceTrace.Measure("CUSTOM LOAD", "ReadSettings"))
            settings = ReadSettings();

        IReadOnlyList<CustomWorldOverrideSummary> overrides;
        using (RealmGatePerformanceTrace.Measure("CUSTOM LOAD", "ReadOverrides"))
            overrides = ReadOverrides();

        bool generatedDataReady;
        using (RealmGatePerformanceTrace.Measure("CUSTOM LOAD", "IsGeneratedDataReady"))
            generatedDataReady = IsGeneratedDataReady();

        return new CustomWorldEditorSnapshot(
            settings,
            overrides,
            _projectRoot,
            ConfigPath,
            OverridePath,
            GeneratedDataPath,
            generatedDataReady);
    }

    public CustomWorldEditorSettings SaveSettings(CustomWorldEditorSettings requested)
    {
        if (string.IsNullOrWhiteSpace(requested.Name)) throw new InvalidOperationException("Custom world name is required.");

        var root = ReadConfigRoot();
        var worlds = root["worlds"] as JsonArray ?? throw new InvalidOperationException("Config/server.worlds.json has no worlds array.");
        var world = worlds.OfType<JsonObject>().FirstOrDefault(IsCustomWorld)
                    ?? throw new InvalidOperationException("Custom1 is not configured.");

        world["id"] = CustomWorldId;
        world["name"] = requested.Name.Trim();
        world["type"] = "ReforgedCustom";
        world["rulesProfile"] = "classic";
        world["dataRoot"] = $"Data/{CustomWorldId}";
        world["roomsFile"] = "rooms.json";
        world["exitsFile"] = "exits.json";
        world["triggersFile"] = "triggers.json";
        world["npcsFile"] = "npcs.json";
        world["spawnsFile"] = "spawns.json";
        world["diagnosticRoom"] = "room.map1.1";
        world["aiMode"] = "Off";
        world["runtimeEnabled"] = requested.RuntimeEnabled;
        world["transfersAllowed"] = requested.TransfersAllowed;
        world["classicProtected"] = false;
        world["description"] = requested.Description?.Trim() ?? string.Empty;
        world["motd"] = requested.Motd?.Trim() ?? string.Empty;
        world["loginBanner"] = requested.LoginBanner?.Trim() ?? string.Empty;

        WriteConfigRoot(root);
        return ReadSettings();
    }

    public CustomWorldOverrideSummary SaveOverride(string family, string targetId, string operation, string fieldsJson)
    {
        family = NormalizeRequired(family, "Family");
        targetId = NormalizeRequired(targetId, "Object ID");
        operation = string.IsNullOrWhiteSpace(operation) ? "patch" : operation.Trim().ToLowerInvariant();
        if (operation is not ("patch" or "upsert" or "create" or "delete"))
            throw new InvalidOperationException("Operation must be patch, upsert, create, or delete.");

        JsonObject fields;
        if (operation == "delete")
        {
            fields = new JsonObject();
        }
        else
        {
            try
            {
                fields = JsonNode.Parse(string.IsNullOrWhiteSpace(fieldsJson) ? "{}" : fieldsJson) as JsonObject
                         ?? throw new JsonException("Fields must be a JSON object.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Override fields are not valid JSON: " + ex.Message, ex);
            }

            if (fields.Count == 0)
                throw new InvalidOperationException("Add at least one field to patch, or choose delete.");
        }

        var root = ReadOverrideRoot(createIfMissing: true);
        var overrides = root["overrides"] as JsonArray ?? new JsonArray();
        root["overrides"] = overrides;
        var existing = overrides.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(item["family"]?.GetValue<string>(), family, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item["target_id"]?.GetValue<string>(), targetId, StringComparison.OrdinalIgnoreCase));

        var entry = existing ?? new JsonObject();
        entry["family"] = family;
        entry["target_id"] = targetId;
        entry["operation"] = operation;
        if (operation == "delete") entry.Remove("fields");
        else entry["fields"] = fields.DeepClone();
        if (existing is null) overrides.Add(entry);

        root["updated_utc"] = DateTimeOffset.UtcNow.ToString("O");
        WriteOverrideRoot(root);
        return ToSummary(entry);
    }

    public bool RemoveOverride(string family, string targetId)
    {
        var root = ReadOverrideRoot(createIfMissing: false);
        if (root["overrides"] is not JsonArray overrides) return false;

        for (var i = overrides.Count - 1; i >= 0; i--)
        {
            if (overrides[i] is not JsonObject item) continue;
            if (!string.Equals(item["family"]?.GetValue<string>(), family, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(item["target_id"]?.GetValue<string>(), targetId, StringComparison.OrdinalIgnoreCase)) continue;
            overrides.RemoveAt(i);
            root["updated_utc"] = DateTimeOffset.UtcNow.ToString("O");
            WriteOverrideRoot(root);
            return true;
        }
        return false;
    }

    public IReadOnlyList<CustomWorldObjectSummary> BrowseObjects(string family, string? search, int limit = 20000)
    {
        family = NormalizeRequired(family, "Family");
        var source = GetFamily(family);
        var query = search?.Trim() ?? string.Empty;
        var byId = source.Records
            .Select(record => new CustomWorldObjectSummary(source.Name, ReadRecordId(record), ReadRecordName(record)))
            .Where(record => record.Id.Length > 0)
            .ToDictionary(record => record.Id, StringComparer.OrdinalIgnoreCase);

        var root = ReadOverrideRoot(createIfMissing: true);
        if (root["overrides"] is JsonArray overrides)
        {
            foreach (var entry in overrides.OfType<JsonObject>())
            {
                if (!string.Equals(entry["family"]?.GetValue<string>(), family, StringComparison.OrdinalIgnoreCase)) continue;
                var id = entry["target_id"]?.GetValue<string>()?.Trim() ?? string.Empty;
                if (id.Length == 0) continue;
                var operation = entry["operation"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? "patch";
                if (operation == "delete")
                {
                    byId.Remove(id);
                    continue;
                }

                var name = byId.TryGetValue(id, out var existing) ? existing.Name : string.Empty;
                if (entry["fields"] is JsonObject fields)
                {
                    if (fields["name"] is not null)
                    {
                        try { name = fields["name"]!.GetValue<string>() ?? name; } catch { }
                    }
                    if (string.IsNullOrWhiteSpace(name) && family.Equals("native_message_payloads", StringComparison.OrdinalIgnoreCase))
                    {
                        var number = fields["message_id"] is JsonNode messageNumber ? NodeToEditorText(messageNumber, CustomWorldSimpleFieldKind.Integer) : id;
                        var preview = PreviewRecordText(fields["player_payload"]);
                        name = preview.Length == 0 ? $"Message {number}" : $"Message {number}: {preview}";
                    }
                    if (string.IsNullOrWhiteSpace(name) && family.Equals("native_textblock_payloads", StringComparison.OrdinalIgnoreCase))
                    {
                        var number = fields["textblock_id"] is JsonNode textblockNumber ? NodeToEditorText(textblockNumber, CustomWorldSimpleFieldKind.Integer) : id;
                        var preview = PreviewRecordText(fields["payload"]);
                        name = preview.Length == 0 ? $"Textblock {number}" : $"Textblock {number}: {preview}";
                    }
                    if (string.IsNullOrWhiteSpace(name) && family.Equals("room_interactions", StringComparison.OrdinalIgnoreCase))
                    {
                        var kind = PreviewRecordText(fields["interaction_kind"]);
                        if (kind.Length > 0) name = "Action: " + kind;
                    }
                }
                byId[id] = new CustomWorldObjectSummary(family, id, name);
            }
        }

        return byId.Values
            .Where(record => query.Length == 0 ||
                record.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                record.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(record => record.Name.Length == 0 ? record.Id : record.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 20000))
            .ToArray();
    }

    public string ResolveReferenceValue(string family, string objectId, string targetPath, CustomWorldSimpleFieldKind targetKind)
    {
        family = NormalizeRequired(family, "Reference family");
        objectId = NormalizeRequired(objectId, "Reference object");
        targetPath = targetPath?.Trim() ?? string.Empty;

        var record = ReadEffectiveRecord(family, objectId);
        if (targetPath.EndsWith("_name", StringComparison.OrdinalIgnoreCase) ||
            targetPath.EndsWith("_named", StringComparison.OrdinalIgnoreCase) ||
            targetPath.EndsWith(".name", StringComparison.OrdinalIgnoreCase))
        {
            var name = ReadRecordName(record);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        if (targetKind is CustomWorldSimpleFieldKind.Integer or CustomWorldSimpleFieldKind.IntegerLines)
        {
            var candidates = family.ToLowerInvariant() switch
            {
                "native_message_payloads" => new[] { "message_id", "legacy_id" },
                "native_textblock_payloads" => new[] { "textblock_id", "legacy_id" },
                "rooms" => new[] { "room_number", "legacy_id" },
                _ => new[] { "legacy_id", "legacy_number", "message_id", "textblock_id" }
            };
            foreach (var candidate in candidates)
            {
                if (record[candidate] is not JsonNode value) continue;
                var text = NodeToEditorText(value, CustomWorldSimpleFieldKind.Integer);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            var trailingDigits = new string(objectId.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (trailingDigits.Length > 0) return trailingDigits;
        }

        if (family.Equals("native_textblock_payloads", StringComparison.OrdinalIgnoreCase) &&
            record["textblock_id"] is JsonNode textblockId &&
            targetPath.Contains("textblock", StringComparison.OrdinalIgnoreCase))
            return "textblock." + NodeToEditorText(textblockId, CustomWorldSimpleFieldKind.Integer);

        return objectId;
    }


    public IReadOnlyList<CustomWorldGlobalSearchHit> GlobalSearch(string? search, int limit = 1000)
    {
        var query = search?.Trim() ?? string.Empty;
        if (query.Length == 0) return [];
        limit = Math.Clamp(limit, 1, 5000);
        var result = new List<CustomWorldGlobalSearchHit>();
        foreach (var familyOption in GetEditableFamilies())
        {
            foreach (var summary in BrowseObjects(familyOption.Value, null, 10000))
            {
                if (result.Count >= limit) return result;
                if (summary.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || summary.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new CustomWorldGlobalSearchHit(summary.Family, summary.Id, summary.Name, "Name / ID"));
                    continue;
                }

                try
                {
                    var effective = ReadEffectiveRecord(summary.Family, summary.Id);
                    if (TryFindSearchMatch(effective, query, string.Empty, out var match))
                        result.Add(new CustomWorldGlobalSearchHit(summary.Family, summary.Id, summary.Name, match));
                }
                catch
                {
                    // Search is a creator convenience tool. One malformed legacy record must not stop the scan.
                }
            }
        }
        return result;
    }

    public int ExportCustomChanges(string path, string? family = null, IEnumerable<string>? targetIds = null)
    {
        path = NormalizeRequired(path, "Export path");
        family = family?.Trim() ?? string.Empty;
        var ids = targetIds is null
            ? null
            : new HashSet<string>(targetIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.OrdinalIgnoreCase);
        var root = ReadOverrideRoot(createIfMissing: true);
        var selected = new JsonArray();
        foreach (var entry in (root["overrides"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            var entryFamily = entry["family"]?.GetValue<string>()?.Trim() ?? string.Empty;
            var id = entry["target_id"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (family.Length > 0 && !entryFamily.Equals(family, StringComparison.OrdinalIgnoreCase)) continue;
            if (ids is not null && !ids.Contains(id)) continue;
            selected.Add(entry.DeepClone());
        }

        var document = new JsonObject
        {
            ["schema"] = "reforged_custom1_export_v1",
            ["world_id"] = CustomWorldId,
            ["exported_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["source"] = "RealmGate Creator Studio",
            ["overrides"] = selected
        };
        var sourceProvenancePath = Path.Combine(Path.GetDirectoryName(OverridePath)!, "source-provenance.json");
        if (File.Exists(sourceProvenancePath))
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(sourceProvenancePath)) is JsonObject provenance)
                    document["source_provenance"] = provenance.DeepClone();
            }
            catch
            {
                // A malformed optional provenance sidecar must not block ordinary creator export.
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, document.ToJsonString(JsonOptions) + Environment.NewLine);
        return selected.Count;
    }

    public CustomWorldImportPreview PreviewImportPackage(string path)
    {
        path = NormalizeRequired(path, "Import path");
        var package = ReadImportPackage(path);
        var existing = ReadOverrideRoot(createIfMissing: true)["overrides"] as JsonArray ?? new JsonArray();
        var existingKeys = existing.OfType<JsonObject>()
            .Select(item => ImportKey(item["family"]?.GetValue<string>(), item["target_id"]?.GetValue<string>()))
            .Where(key => key.Length > 1)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var records = 0;
        var conflicts = 0;
        var invalid = 0;
        foreach (var item in package.OfType<JsonObject>())
        {
            if (!ValidateImportEntry(item, out var key)) { invalid++; continue; }
            records++;
            if (existingKeys.Contains(key)) conflicts++;
        }
        var fresh = Math.Max(0, records - conflicts);
        return new CustomWorldImportPreview(path, records, fresh, conflicts, invalid,
            $"{records} valid change(s): {fresh} new, {conflicts} conflict(s), {invalid} invalid. Import replaces only conflicting Custom1 overrides; Classic is never modified.");
    }

    public CustomWorldImportPreview ApplyImportPackage(string path)
    {
        var preview = PreviewImportPackage(path);
        if (preview.InvalidRecords > 0)
            throw new InvalidOperationException("Import contains invalid entries. Fix or remove them before applying the package.");

        var importDocument = ReadImportDocument(path);
        var imported = (importDocument["overrides"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .ToArray();

        var root = ReadOverrideRoot(createIfMissing: true);
        var overrides = root["overrides"] as JsonArray ?? new JsonArray();
        root["overrides"] = overrides;

        var backup = OverridePath + ".pre-import-" +
                     DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + ".bak";

        if (File.Exists(OverridePath))
            File.Copy(OverridePath, backup, overwrite: false);

        // Build the set of valid imported keys once.
        var importedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in imported)
        {
            if (ValidateImportEntry(item, out var key))
                importedKeys.Add(key);
        }

        // Preserve existing Custom1 overrides that do not conflict with
        // this package. This replaces the previous imported-record x
        // existing-override nested scan.
        var mergedOverrides = new JsonArray();

        foreach (var existing in overrides.OfType<JsonObject>())
        {
            var key = ImportKey(
                existing["family"]?.GetValue<string>(),
                existing["target_id"]?.GetValue<string>());

            if (!importedKeys.Contains(key))
                mergedOverrides.Add(existing.DeepClone());
        }

        // Imported entries replace conflicting Custom1 entries.
        foreach (var item in imported)
        {
            if (!ValidateImportEntry(item, out _))
                continue;

            mergedOverrides.Add(item.DeepClone());
        }

        root["overrides"] = mergedOverrides;
        root["updated_at"] = DateTimeOffset.UtcNow.ToString("O");

        WriteOverrideRoot(root);

        var carriedProvenance =
            importDocument["source_provenance"] is JsonObject;

        if (importDocument["source_provenance"] is JsonObject sourceProvenance)
        {
            var provenancePath = Path.Combine(
                Path.GetDirectoryName(OverridePath)!,
                "source-provenance.json");

            if (File.Exists(provenancePath))
            {
                var provenanceBackup = provenancePath +
                    ".pre-import-" +
                    DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") +
                    ".bak";

                File.Copy(provenancePath, provenanceBackup, overwrite: false);
            }

            var carried = (JsonObject)sourceProvenance.DeepClone();
            carried["portable_package_imported_at_utc"] =
                DateTimeOffset.UtcNow.ToString("O");
            carried["portable_package_name"] = Path.GetFileName(path);

            File.WriteAllText(
                provenancePath,
                carried.ToJsonString(JsonOptions) + Environment.NewLine);
        }

        return preview with
        {
            Summary =
                $"Imported {preview.Records} Custom1 change(s). " +
                $"{preview.Conflicts} existing Custom1 override(s) were replaced. " +
                $"Source provenance was {(carriedProvenance ? "carried forward" : "not present in the package")}. " +
                $"Backup: {backup}"
        };
    }

    public CustomWorldBulkEditResult BulkSetField(string family, IEnumerable<string> targetIds, string path, string rawValue, string valueKind)
    {
        family = NormalizeRequired(family, "Family");
        path = NormalizeRequired(path, "Field path");
        var ids = targetIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var changed = 0;
        var skipped = 0;
        var node = ParseBulkValue(rawValue, valueKind);
        foreach (var targetId in ids)
        {
            try
            {
                var existsInBase = GetFamily(family).Records.Any(record => ReadRecordId(record).Equals(targetId, StringComparison.OrdinalIgnoreCase));
                var existingOverride = FindOverride(family, targetId);
                if (!existsInBase && existingOverride is null) { skipped++; continue; }
                var fields = existingOverride?["fields"] is JsonObject prior ? (JsonObject)prior.DeepClone() : new JsonObject();
                SetPath(fields, path, node?.DeepClone());
                SaveOverride(family, targetId, existsInBase ? "patch" : "upsert", fields.ToJsonString(JsonOptions));
                changed++;
            }
            catch { skipped++; }
        }
        return new CustomWorldBulkEditResult(ids.Length, changed, skipped, $"Changed {changed} of {ids.Length} record(s); {skipped} skipped.");
    }

    public CustomWorldBulkEditResult BulkCopyField(string family, string sourceId, IEnumerable<string> targetIds, string path)
    {
        family = NormalizeRequired(family, "Family");
        sourceId = NormalizeRequired(sourceId, "Source object ID");
        path = NormalizeRequired(path, "Field path");
        var source = ReadEffectiveRecord(family, sourceId);
        var sourceNode = ReadPath(source, path) ?? throw new InvalidOperationException($"Source record does not contain '{path}'.");
        var ids = targetIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(value => !value.Equals(sourceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        var changed = 0;
        var skipped = 0;
        foreach (var targetId in ids)
        {
            try
            {
                var existsInBase = GetFamily(family).Records.Any(record => ReadRecordId(record).Equals(targetId, StringComparison.OrdinalIgnoreCase));
                var existingOverride = FindOverride(family, targetId);
                if (!existsInBase && existingOverride is null) { skipped++; continue; }
                var fields = existingOverride?["fields"] is JsonObject prior ? (JsonObject)prior.DeepClone() : new JsonObject();
                SetPath(fields, path, sourceNode.DeepClone());
                SaveOverride(family, targetId, existsInBase ? "patch" : "upsert", fields.ToJsonString(JsonOptions));
                changed++;
            }
            catch { skipped++; }
        }
        return new CustomWorldBulkEditResult(ids.Length, changed, skipped, $"Copied '{path}' from {sourceId} to {changed} record(s); {skipped} skipped.");
    }

    public IReadOnlyList<CustomWorldKeyDoorRow> BrowseKeyDoors(string? search, int limit = 1000)
    {
        var query = search?.Trim() ?? string.Empty;
        limit = Math.Clamp(limit, 1, 10000);

        // Build each effective family once. The previous implementation
        // repeatedly searched/cloned exits and repeatedly browsed items.
        var exits = BuildEffectiveRecordIndex("exits");
        var items = BuildEffectiveRecordIndex("items");

        // Resolve both normal item IDs and legacy item numbers in O(1).
        var itemNames = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var pair in items)
        {
            var name = ReadRecordName(pair.Value);

            itemNames[pair.Key] = name;

            if (pair.Value["legacy_id"] is JsonNode legacyNode)
            {
                var legacy = legacyNode.ToString()?.Trim() ?? string.Empty;

                if (legacy.Length > 0)
                {
                    itemNames[legacy] = name;
                    itemNames["item." + legacy] = name;
                }
            }
        }

        var result = new List<CustomWorldKeyDoorRow>();

        foreach (var pair in exits)
        {
            if (result.Count >= limit)
                break;

            var record = pair.Value;

            var gate =
                ReadPath(record, "gate_kind")?.ToString()?.Trim()
                ?? string.Empty;

            var keyRaw =
                ReadPath(record, "prerequisites.required_item_id")
                    ?.ToString()?.Trim()
                ?? string.Empty;

            if (!gate.Equals(
                    "key",
                    StringComparison.OrdinalIgnoreCase) &&
                keyRaw.Length == 0)
            {
                continue;
            }

            var keyItemId = NormalizeItemReference(keyRaw);

            var keyName =
                itemNames.TryGetValue(keyItemId, out var resolvedName)
                    ? resolvedName
                    : string.Empty;

            var fromRoom =
                ReadPath(record, "relationships.from_room")
                    ?.ToString()?.Trim()
                ?? string.Empty;

            var toRoom =
                ReadPath(record, "relationships.to_room")
                    ?.ToString()?.Trim()
                ?? string.Empty;

            var direction =
                ReadPath(record, "direction")?.ToString()?.Trim()
                ?? string.Empty;

            var qualifier =
                ReadPath(record, "qualifier")?.ToString()?.Trim()
                ?? string.Empty;

            var row = new CustomWorldKeyDoorRow(
                pair.Key,
                fromRoom,
                toRoom,
                direction,
                keyItemId,
                keyName,
                qualifier);

            if (query.Length > 0 &&
                !string.Join(
                    " ",
                    row.ExitId,
                    row.FromRoomId,
                    row.ToRoomId,
                    row.Direction,
                    row.KeyItemId,
                    row.KeyItemName,
                    row.Qualifier)
                .Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(row);
        }

        return result;
    }

    public RuntimeClassicRoomCurrencyBalance GetCustom1RoomCurrency(string roomId) =>
        RuntimeClassicRoomCurrencyStore.Get(_projectRoot, CustomWorldId, NormalizeRequired(roomId, "Room ID"));

    public RuntimeClassicRoomCurrencyBalance SetCustom1RoomCurrency(string roomId, long runic, long platinum, long gold, long silver, long copper)
    {
        roomId = NormalizeRequired(roomId, "Room ID");
        RuntimeClassicRoomCurrencyStore.SetExact(_projectRoot, CustomWorldId, roomId, runic, platinum, gold, silver, copper);
        return RuntimeClassicRoomCurrencyStore.Get(_projectRoot, CustomWorldId, roomId);
    }

    public IReadOnlyList<CustomWorldMonsterIndexRow> BrowseMonsterIndex(
        string? search,
        int minimumLevel = 0,
        int maximumLevel = int.MaxValue,
        int limit = 5000)
    {
        var query = search?.Trim() ?? string.Empty;
        minimumLevel = Math.Max(0, minimumLevel);
        maximumLevel = maximumLevel <= 0 ? int.MaxValue : Math.Max(minimumLevel, maximumLevel);
        limit = Math.Clamp(limit, 1, 10000);

        var npcs = BuildEffectiveRecordIndex("npcs");
        var spawns = BuildEffectiveRecordIndex("spawns");
        var tokenToNpc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var groupMembers = new Dictionary<int, List<string>>();
        foreach (var pair in npcs)
        {
            AddNpcReferenceTokens(tokenToNpc, pair.Key, pair.Value);
            var groupId = ReadFirstInt(pair.Value, "group_id", "npc_group_id", "spawn_model.group_id", "relationships.group_id");
            if (groupId <= 0) continue;
            if (!groupMembers.TryGetValue(groupId, out var members)) groupMembers[groupId] = members = [];
            members.Add(pair.Key);
        }

        var spawnCount = npcs.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var roomsByNpc = npcs.Keys.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var spawn in spawns)
        {
            var linkedNpcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectNpcReferences(spawn.Value, string.Empty, tokenToNpc, linkedNpcIds);
            var groupId = ReadFirstInt(spawn.Value, "group_id", "npc_group_id", "spawn_model.group_id", "relationships.group_id");
            if (groupId > 0 && groupMembers.TryGetValue(groupId, out var members))
                foreach (var member in members) linkedNpcIds.Add(member);

            var roomId = ReadSpawnRoomId(spawn.Value);
            foreach (var npcId in linkedNpcIds)
            {
                if (!spawnCount.ContainsKey(npcId)) continue;
                spawnCount[npcId]++;
                if (roomId.Length > 0) roomsByNpc[npcId].Add(roomId);
            }
        }

        var rows = new List<CustomWorldMonsterIndexRow>();
        foreach (var pair in npcs.OrderBy(pair => ReadRecordName(pair.Value), StringComparer.OrdinalIgnoreCase).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (rows.Count >= limit) break;
            var record = pair.Value;
            var name = ReadRecordName(record);
            var level = ReadFirstInt(record, "level", "combat.level", "stats.level", "minimum_level");
            if (level < minimumLevel || level > maximumLevel) continue;
            var hp = ReadFirstInt(record, "hit_points", "maximum_hit_points", "max_hit_points", "combat.hit_points", "stats.hit_points", "hp");
            var exp = ReadFirstInt(record, "experience", "experience_reward", "rewards.experience", "rewards.experience_points", "exp");
            var groupId = ReadFirstInt(record, "group_id", "npc_group_id", "spawn_model.group_id", "relationships.group_id");
            var roomSet = roomsByNpc[pair.Key];
            var rooms = string.Join(", ", roomSet.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            var row = new CustomWorldMonsterIndexRow(pair.Key, name, level, hp, exp, groupId, spawnCount[pair.Key], roomSet.Count, rooms);
            if (query.Length > 0 && !string.Join(" ", row.NpcId, row.Name, row.Level, row.GroupId, row.Rooms)
                    .Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            rows.Add(row);
        }
        return rows;
    }

    public IReadOnlyList<CustomWorldControlRoomRow> BrowseControlRooms(string? search, int limit = 5000)
    {
        var query = search?.Trim() ?? string.Empty;
        limit = Math.Clamp(limit, 1, 10000);
        var rooms = BuildEffectiveRecordIndex("rooms");
        var tokenToRoom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in rooms)
        {
            tokenToRoom[NormalizeReferenceToken(pair.Key)] = pair.Key;
            foreach (var path in new[] { "qualified_room_ref", "relationships.room", "relationships.room_id" })
            {
                var raw = ScalarText(ReadPath(pair.Value, path));
                if (!string.IsNullOrWhiteSpace(raw)) tokenToRoom[NormalizeReferenceToken(raw)] = pair.Key;
            }
        }

        var aggregates = new Dictionary<string, (int Interactions, int Triggers, int Scripts, HashSet<string> Sources)>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in new[] { "room_interactions", "triggers", "scripts" })
        {
            Dictionary<string, JsonObject> records;
            try { records = BuildEffectiveRecordIndex(family); }
            catch { continue; }
            foreach (var pair in records)
            {
                var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectRoomReferences(pair.Value, string.Empty, tokenToRoom, referenced);
                AddExplicitRoomPairs(pair.Value, referenced, rooms);
                foreach (var roomId in referenced)
                {
                    if (!rooms.ContainsKey(roomId)) continue;
                    if (!aggregates.TryGetValue(roomId, out var aggregate))
                        aggregate = (0, 0, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    if (family.Equals("room_interactions", StringComparison.OrdinalIgnoreCase)) aggregate.Interactions++;
                    else if (family.Equals("triggers", StringComparison.OrdinalIgnoreCase)) aggregate.Triggers++;
                    else aggregate.Scripts++;
                    aggregate.Sources.Add($"{family}/{pair.Key}");
                    aggregates[roomId] = aggregate;
                }
            }
        }

        return aggregates
            .Select(pair =>
            {
                var roomName = rooms.TryGetValue(pair.Key, out var record) ? ReadRecordName(record) : pair.Key;
                return new CustomWorldControlRoomRow(
                    pair.Key,
                    roomName,
                    pair.Value.Interactions,
                    pair.Value.Triggers,
                    pair.Value.Scripts,
                    string.Join(", ", pair.Value.Sources.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)));
            })
            .Where(row => query.Length == 0 || string.Join(" ", row.RoomId, row.RoomName, row.Sources).Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.InteractionCount + row.TriggerCount + row.ScriptCount)
            .ThenBy(row => row.RoomName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public RuntimeCustomWorldShopResetResult ResetCustom1ShopStock(string shopId) =>
        RuntimeCustomWorldCreatorMaintenance.ResetShopToGeneratedNormalStock(
            _projectRoot,
            CustomWorldId,
            NormalizeRequired(shopId, "Shop ID"));

    public RuntimeCustomWorldRoomItemCombineResult CombineCustom1RoomItems(string roomId) =>
        RuntimeCustomWorldCreatorMaintenance.CombineLikeDynamicRoomItems(
            _projectRoot,
            CustomWorldId,
            NormalizeRequired(roomId, "Room ID"));

    public IReadOnlyList<CustomWorldTextblockStripPreviewRow> PreviewTextblockStrip(
        string findText,
        string replacement,
        bool suffixOnly,
        int limit = 5000)
    {
        findText = NormalizeRequired(findText, "Text to strip");
        replacement ??= string.Empty;
        limit = Math.Clamp(limit, 1, 10000);
        var result = new List<CustomWorldTextblockStripPreviewRow>();
        foreach (var pair in BuildEffectiveRecordIndex("native_textblock_payloads").OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (result.Count >= limit) break;
            var (path, before) = ReadTextblockPayload(pair.Value);
            if (path.Length == 0) continue;
            var after = TransformTextblock(before, findText, replacement, suffixOnly, out var matches);
            if (matches == 0 || before.Equals(after, StringComparison.Ordinal)) continue;
            result.Add(new CustomWorldTextblockStripPreviewRow(
                pair.Key,
                ReadRecordName(pair.Value),
                PreviewText(before),
                PreviewText(after),
                matches));
        }
        return result;
    }

    public CustomWorldTextblockStripResult ApplyTextblockStrip(
        string findText,
        string replacement,
        bool suffixOnly,
        IEnumerable<string> targetIds)
    {
        findText = NormalizeRequired(findText, "Text to strip");
        replacement ??= string.Empty;
        var ids = targetIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var changed = 0;
        var skipped = 0;
        foreach (var id in ids)
        {
            try
            {
                var effective = ReadEffectiveRecord("native_textblock_payloads", id);
                var (path, before) = ReadTextblockPayload(effective);
                if (path.Length == 0) { skipped++; continue; }
                var after = TransformTextblock(before, findText, replacement, suffixOnly, out var matches);
                if (matches == 0 || before.Equals(after, StringComparison.Ordinal)) { skipped++; continue; }
                var existsInBase = GetFamily("native_textblock_payloads").Records.Any(record => ReadRecordId(record).Equals(id, StringComparison.OrdinalIgnoreCase));
                var existingOverride = FindOverride("native_textblock_payloads", id);
                var fields = existingOverride?["fields"] is JsonObject prior ? (JsonObject)prior.DeepClone() : new JsonObject();
                SetPath(fields, path, JsonValue.Create(after));
                SaveOverride("native_textblock_payloads", id, existsInBase ? "patch" : "upsert", fields.ToJsonString(JsonOptions));
                changed++;
            }
            catch { skipped++; }
        }
        return new CustomWorldTextblockStripResult(ids.Length, changed, skipped,
            $"Updated {changed} textblock(s); {skipped} skipped. Classic was not modified.");
    }

    private static void AddNpcReferenceTokens(IDictionary<string, string> tokens, string npcId, JsonObject record)
    {
        tokens[NormalizeReferenceToken(npcId)] = npcId;
        foreach (var path in new[] { "id", "legacy_id", "legacy_number", "npc_id" })
        {
            var raw = ScalarText(ReadPath(record, path));
            if (!string.IsNullOrWhiteSpace(raw)) tokens[NormalizeReferenceToken(raw)] = npcId;
        }
    }

    private static void CollectNpcReferences(
        JsonNode? node,
        string path,
        IReadOnlyDictionary<string, string> tokenToNpc,
        ISet<string> result)
    {
        if (node is null) return;
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var child = path.Length == 0 ? property.Key : path + "." + property.Key;
                CollectNpcReferences(property.Value, child, tokenToNpc, result);
            }
            return;
        }
        if (node is JsonArray array)
        {
            foreach (var child in array) CollectNpcReferences(child, path, tokenToNpc, result);
            return;
        }
        if (!path.Contains("npc", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains("monster", StringComparison.OrdinalIgnoreCase)) return;
        var token = NormalizeReferenceToken(ScalarText(node));
        if (token.Length > 0 && tokenToNpc.TryGetValue(token, out var npcId)) result.Add(npcId);
    }

    private static int ReadFirstInt(JsonObject record, params string[] paths)
    {
        foreach (var path in paths)
        {
            var raw = ScalarText(ReadPath(record, path));
            if (int.TryParse(raw, out var parsed)) return parsed;
        }
        return 0;
    }

    private static string ReadSpawnRoomId(JsonObject spawn)
    {
        foreach (var path in new[]
                 {
                     "relationships.room", "relationships.room_id", "relationships.source_room",
                     "room_id", "resolved_runtime_room_id", "qualified_room_ref", "source_room_reference"
                 })
        {
            var value = ScalarText(ReadPath(spawn, path)).Trim();
            if (value.StartsWith("room.", StringComparison.OrdinalIgnoreCase)) return value;
        }

        var map = ReadFirstInt(spawn, "map_id", "map_number", "source_map_id", "relationships.map_id");
        var room = ReadFirstInt(spawn, "room_number", "source_room_number", "relationships.room_number");
        return map > 0 && room > 0 ? $"room.map{map}.{room}" : string.Empty;
    }

    private static void CollectRoomReferences(
        JsonNode? node,
        string path,
        IReadOnlyDictionary<string, string> tokenToRoom,
        ISet<string> result)
    {
        if (node is null) return;
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var child = path.Length == 0 ? property.Key : path + "." + property.Key;
                CollectRoomReferences(property.Value, child, tokenToRoom, result);
            }
            return;
        }
        if (node is JsonArray array)
        {
            foreach (var child in array) CollectRoomReferences(child, path, tokenToRoom, result);
            return;
        }
        if (!path.Contains("room", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains("location", StringComparison.OrdinalIgnoreCase)) return;
        var token = NormalizeReferenceToken(ScalarText(node));
        if (token.Length > 0 && tokenToRoom.TryGetValue(token, out var roomId)) result.Add(roomId);
    }

    private static void AddExplicitRoomPairs(JsonObject record, ISet<string> result, IReadOnlyDictionary<string, JsonObject> rooms)
    {
        foreach (var pair in new[]
                 {
                     ("map_id", "room_number"),
                     ("map_number", "room_number"),
                     ("source_map_id", "source_room_number"),
                     ("from_map_number", "from_room_number"),
                     ("to_map_number", "to_room_number"),
                     ("target_map_id", "target_room_number")
                 })
        {
            var map = ReadFirstInt(record, pair.Item1);
            var room = ReadFirstInt(record, pair.Item2);
            if (map <= 0 || room <= 0) continue;
            var id = $"room.map{map}.{room}";
            if (rooms.ContainsKey(id)) result.Add(id);
        }
    }

    private static (string Path, string Payload) ReadTextblockPayload(JsonObject record)
    {
        foreach (var path in new[] { "payload", "text", "content", "player_payload" })
        {
            var node = ReadPath(record, path);
            if (node is null) continue;
            var value = ScalarText(node);
            return (path, value);
        }
        return (string.Empty, string.Empty);
    }

    private static string TransformTextblock(string source, string findText, string replacement, bool suffixOnly, out int matches)
    {
        matches = 0;
        if (suffixOnly)
        {
            if (!source.EndsWith(findText, StringComparison.Ordinal)) return source;
            matches = 1;
            return source[..^findText.Length] + replacement;
        }

        var offset = 0;
        while (offset <= source.Length - findText.Length)
        {
            var index = source.IndexOf(findText, offset, StringComparison.Ordinal);
            if (index < 0) break;
            matches++;
            offset = index + findText.Length;
        }
        return matches == 0 ? source : source.Replace(findText, replacement, StringComparison.Ordinal);
    }

    private static string PreviewText(string value)
    {
        var compact = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âµ ").Trim();
        return compact.Length <= 220 ? compact : compact[..217] + "...";
    }

    public IReadOnlyList<CustomWorldNightmareParityRow> BuildNightmareParityAudit() =>
    [
        new("Room Editor", "MATCHED", "Dedicated Room Editor", "Room creation/editing, exits, contents, interactions, map links, and visual-map integration."),
        new("Map / Map Editor / Map Trail / Legend", "MATCHED+", "Visual Map Editor", "Freeform room layout, directional edges, route trail, markers, auto-arrange, and create-from-map."),
        new("Item Editor", "MATCHED+", "Dedicated Item Editor", "Purpose-built item/equipment/combat/effect/restriction editing with discovered-field fallback."),
        new("Monster Editor", "MATCHED+", "Dedicated NPC Editor", "Attack slots, abilities, behavior, rewards/loot, and creator-safe recovered fields."),
        new("Monster Attack Simulator", "MATCHED", "Monster Simulator", "Uses RealmGate Classic NPC selection/energy cadence and physical combat resolver."),
        new("Swing Calculator", "MATCHED", "Weapon / Damage Calculator", "Uses the same Classic physical combat resolver as runtime."),
        new("Spell Editor", "MATCHED+", "Dedicated Spell Editor", "Targeting, costs, scaling, effects, messages, abilities, and recovered fields."),
        new("Class Editor", "MATCHED+", "Dedicated Class Editor", "Stats/growth, combat/magic, skills/abilities, restrictions."),
        new("Race Editor", "MATCHED+", "Dedicated Race Editor", "Attributes, combat/resistance, abilities/restrictions."),
        new("Shop Editor", "MATCHED+", "Dedicated Shop Editor", "Structured inventory/stock regeneration plus pricing and access rules."),
        new("Message Editor", "MATCHED", "Dedicated Message Editor", "Number/text search and large payload fields without native bookkeeping clutter."),
        new("Textblock Editor", "MATCHED", "Dedicated Textblock Editor", "Searchable plain-text editor; no JSON/escape-string workflow."),
        new("Action Editor", "MATCHED+", "Room Interaction Editor", "Location, commands, requirements, outcomes/messages, and execution settings."),
        new("Ability Editor", "MATCHED+", "Dedicated Ability Editor", "Gameplay, display, relationships, and recovered ability fields."),
        new("Keys window", "MATCHED", "Key / Door Utility", "Lists keyed exits, required item, source/destination, and supports direct jump to exit/key editors."),
        new("Visible / Hidden Coins windows", "REFORGED REPLACEMENT", "Custom1 Room Coin Utility", "Edits current Custom1 room currency state by denomination for testing; RealmGate runtime stores coins independently of Btrieve room records."),
        new("Cross reference", "MATCHED+", "Where Used", "Cross-family reference scan with jump-to-record repair workflow."),
        new("Database Import / Custom Export", "MATCHED FOR CUSTOM1", "Safe Custom1 Import / Export", "Portable Custom1 override packages, conflict preview, automatic backup, and Classic protection. Not an NMR MDB-format exporter."),
        new("Search / record lists", "MATCHED+", "Global Search + universal lookups", "Searches all creator families and record content, then jumps into the proper dedicated editor."),
        new("Copy/Paste / mass changes", "MATCHED+", "Bulk Creator Tools", "Bulk set/copy any discovered field across selected records rather than one hard-coded copy dialog per database."),
        new("User Editor", "REFORGED REPLACEMENT", "Classic Administration", "RealmGate manages character SYSOP/access administration separately from world content."),
        new("Monster Index / NPC-Room list", "MATCHED+", "Monster Index / NPC-Room Report", "Dedicated monster index shows level, HP, EXP, group, spawn count, and every resolved spawn room with direct jump to the NPC editor."),
        new("Control Room List", "MATCHED+", "Control Room Report", "One-click report groups rooms referenced by interactions, triggers, and scripts and exposes the owning records."),
        new("Restock Shops", "MATCHED", "Live Custom1 Shop Reset", "Resets a generated Custom1 shop's persistent live stock to authored normal quantities and restarts its decoded regeneration schedule. Classic is blocked."),
        new("Room buffer pad/delete", "NOT APPLICABLE", "Generated JSON runtime", "NMR buffer rooms work around Btrieve sequential-load gaps. RealmGate does not have that storage limitation."),
        new("Fix Item Uses / Monster Drop Uses", "REFORGED REPLACEMENT", "Custom1 Validator", "Validation reports malformed creator records instead of silently mass-mutating recovered data."),
        new("Monster EXP multiply/divide/combine", "MATCHED BY GENERAL TOOL", "Bulk Creator Tools", "Bulk field set/copy supports mass EXP balancing without a special one-purpose menu command."),
        new("Combine room items", "MATCHED / SAFER", "Custom1 Room Stack Cleanup", "Compacts duplicate dynamic room-ground stacks in the Custom1 living-world save while preserving generated static placement identities. Run with host stopped."),
        new("Textblock String Stripper", "MATCHED+", "Textblock Batch Strip / Replace", "Preview-first batch utility removes an exact suffix or replaces exact text across matching textblocks using Custom1 overrides only."),
        new("Nightmare MDB import/export format", "MATCHED+ FOR CORE TABLES", "Verified MDB + WCC Custom Import", "Reads core Access/MDB tables through the pure-managed reader, requires the matching WCC/WGS set before Custom1 import, fingerprints both sources, carries provenance in portable packages, preserves unmapped raw MDB data, and exports supported records to Jet MDB."),
    ];

    private static JsonObject ReadImportDocument(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Import package was not found.", path);
        var document = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new JsonException("Import package root must be a JSON object.");
        if (!string.Equals(document["schema"]?.GetValue<string>(), "reforged_custom1_export_v1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This is not a RealmGate Custom1 export package.");
        if (document["overrides"] is not JsonArray) throw new InvalidOperationException("Import package does not contain an overrides array.");
        return document;
    }

    private static JsonArray ReadImportPackage(string path) =>
        ReadImportDocument(path)["overrides"] as JsonArray ?? throw new InvalidOperationException("Import package does not contain an overrides array.");

    private bool ValidateImportEntry(JsonObject item, out string key)
    {
        var family = item["family"]?.GetValue<string>()?.Trim() ?? string.Empty;
        var id = item["target_id"]?.GetValue<string>()?.Trim() ?? string.Empty;
        var operation = item["operation"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? string.Empty;
        key = ImportKey(family, id);
        if (family.Length == 0 || id.Length == 0 || operation is not "patch" and not "upsert" and not "delete") return false;
        if (!GetEditableFamilies().Any(option => option.Value.Equals(family, StringComparison.OrdinalIgnoreCase))) return false;
        if (operation != "delete" && item["fields"] is not JsonObject) return false;
        return true;
    }

    private static string ImportKey(string? family, string? id) => (family?.Trim() ?? string.Empty) + "\u001f" + (id?.Trim() ?? string.Empty);

    private static JsonNode? ParseBulkValue(string rawValue, string valueKind)
    {
        valueKind = valueKind?.Trim().ToLowerInvariant() ?? "text";
        rawValue ??= string.Empty;
        return valueKind switch
        {
            "integer" => long.TryParse(rawValue.Trim(), out var integer) ? JsonValue.Create(integer) : throw new InvalidOperationException("Bulk value must be a whole number."),
            "decimal" => decimal.TryParse(rawValue.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number) ? JsonValue.Create(number) : throw new InvalidOperationException("Bulk value must be a number."),
            "boolean" => bool.TryParse(rawValue.Trim(), out var boolean) ? JsonValue.Create(boolean) : throw new InvalidOperationException("Bulk value must be true or false."),
            "null" => null,
            _ => JsonValue.Create(rawValue)
        };
    }

    private static bool TryFindSearchMatch(JsonNode? node, string query, string path, out string match)
    {
        match = string.Empty;
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                var childPath = path.Length == 0 ? pair.Key : path + "." + pair.Key;
                if (TryFindSearchMatch(pair.Value, query, childPath, out match)) return true;
            }
            return false;
        }
        if (node is JsonArray array)
        {
            for (var index = 0; index < Math.Min(array.Count, 200); index++)
                if (TryFindSearchMatch(array[index], query, $"{path}[{index}]", out match)) return true;
            return false;
        }
        var text = node?.ToString() ?? string.Empty;
        if (!text.Contains(query, StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Length > 120) text = text[..117] + "...";
        match = (path.Length == 0 ? "Value" : path) + ": " + text;
        return true;
    }

    private static string NormalizeItemReference(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0) return string.Empty;
        if (raw.StartsWith("item.", StringComparison.OrdinalIgnoreCase)) return raw;
        return long.TryParse(raw, out var number) ? "item." + number : raw;
    }

    private string ResolveObjectName(string family, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        try
        {
            var direct = BrowseObjects(family, id, 50).FirstOrDefault(row => row.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (direct is not null) return direct.Name;
            if (family.Equals("items", StringComparison.OrdinalIgnoreCase) && id.StartsWith("item.", StringComparison.OrdinalIgnoreCase))
            {
                var legacy = id[5..];
                foreach (var item in BrowseObjects("items", legacy, 100))
                {
                    try
                    {
                        var record = ReadEffectiveRecord("items", item.Id);
                        if ((ReadPath(record, "legacy_id")?.ToString() ?? string.Empty).Equals(legacy, StringComparison.OrdinalIgnoreCase)) return item.Name;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return string.Empty;
    }

    public IReadOnlyList<CustomWorldReferenceHit> FindReferences(string targetFamily, string targetId, int limit = 1000)
    {
        targetFamily = NormalizeRequired(targetFamily, "Target family");
        targetId = NormalizeRequired(targetId, "Target object");
        var target = ReadEffectiveRecord(targetFamily, targetId);
        var targetTokens = BuildReferenceTokens(targetFamily, targetId, target);
        var hits = new List<CustomWorldReferenceHit>();

        foreach (var familyOption in GetEditableFamilies())
        {
            var records = BuildEffectiveFamilyRecords(familyOption.Value);
            foreach (var pair in records)
            {
                if (familyOption.Value.Equals(targetFamily, StringComparison.OrdinalIgnoreCase) &&
                    pair.Key.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = ReadRecordName(pair.Value);
                FindReferenceMatches(pair.Value, string.Empty, targetFamily, targetTokens, (path, value) =>
                {
                    hits.Add(new CustomWorldReferenceHit(familyOption.Value, pair.Key, name, path, value));
                });
                if (hits.Count >= Math.Clamp(limit, 1, 10000))
                    return hits.OrderBy(hit => hit.Family, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(hit => hit.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(hit => hit.Id, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(hit => hit.Path, StringComparer.OrdinalIgnoreCase)
                        .Take(limit)
                        .ToArray();
            }
        }

        return hits.OrderBy(hit => hit.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Path, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 10000))
            .ToArray();
    }

    public CustomWorldNpcSimulationResult SimulateNpcCombat(
        string npcId,
        int targetDefenseValue,
        int targetArmorClass,
        int targetDamageResistance,
        int targetDodgeBonus,
        int rounds)
    {
        npcId = NormalizeRequired(npcId, "NPC");
        rounds = Math.Clamp(rounds, 1, 100000);
        targetDefenseValue = Math.Clamp(targetDefenseValue, 0, 9999);
        targetArmorClass = Math.Clamp(targetArmorClass, 0, 9999);
        targetDamageResistance = Math.Clamp(targetDamageResistance, 0, 9999);
        targetDodgeBonus = Math.Clamp(targetDodgeBonus, 0, 9999);

        var record = ReadEffectiveRecord("npcs", npcId);
        using var document = JsonDocument.Parse(record.ToJsonString());
        var npc = RuntimeNpcDefinition.FromJson(document.RootElement);
        var random = new Random(0x4D4D52);
        var resolver = new ClassicCombatResolver(new SystemClassicRandom(random));
        var defender = new ClassicCombatantStats(0, targetDefenseValue, targetArmorClass, targetDamageResistance, targetDodgeBonus);
        var accumulators = npc.Attacks.ToDictionary(attack => attack.Slot, attack => new NpcSimulationAccumulator(attack));
        var currentEnergy = 0;
        var totalSelections = 0;
        var physicalHits = 0;
        var physicalMisses = 0;
        var physicalDodges = 0;
        var spellCasts = 0;
        var spellFailures = 0;
        long totalDamage = 0;
        var unsupportedTypes = new HashSet<int>();

        var attemptsPerRound = RuntimeClassicNpcCombatCadence.ResolveAttemptSlots(npc);
        for (var round = 0; round < rounds; round++)
        {
            currentEnergy = RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(currentEnergy, Math.Max(0, npc.Energy));
            for (var attempt = 0; attempt < attemptsPerRound; attempt++)
            {
                var selection = RuntimeNpcAttackSelector.Select(npc.Attacks, currentEnergy, random);
                var attack = selection.Attack;
                if (attack is null) continue;
                totalSelections++;
                if (!accumulators.TryGetValue(attack.Slot, out var accumulator))
                {
                    accumulator = new NpcSimulationAccumulator(attack);
                    accumulators[attack.Slot] = accumulator;
                }
                accumulator.Attempts++;

                if (!selection.Affordable)
                {
                    if (RuntimeClassicNpcCombatCadence.ShouldEndRoundAfterPhysicalSelection(attack, currentEnergy)) break;
                    continue;
                }

                if (attack.Type == 2)
                {
                    var succeeds = RuntimeClassicNpcCombatCadence.SpellCastSucceeds(attack, random.Next(0, 100));
                    currentEnergy = succeeds
                        ? RuntimeClassicNpcCombatCadence.DeductFullAttackCost(currentEnergy, attack)
                        : RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(currentEnergy, attack);
                    if (succeeds)
                    {
                        accumulator.CastSuccesses++;
                        spellCasts++;
                    }
                    else
                    {
                        accumulator.CastFailures++;
                        spellFailures++;
                    }
                    continue;
                }

                currentEnergy = RuntimeClassicNpcCombatCadence.DeductFullAttackCost(currentEnergy, attack);
                if (!Enum.IsDefined(typeof(ClassicAttackType), attack.Type))
                {
                    unsupportedTypes.Add(attack.Type);
                    continue;
                }

                var result = resolver.Resolve(new ClassicAttackRequest(
                    (ClassicAttackType)attack.Type,
                    new ClassicCombatantStats(attack.Accuracy, 0, 0, 0, 0),
                    defender,
                    attack.MinimumDamage,
                    attack.MaximumDamage));

                switch (result.Outcome)
                {
                    case ClassicHitOutcome.Miss:
                        accumulator.Misses++;
                        physicalMisses++;
                        break;
                    case ClassicHitOutcome.Dodged:
                        accumulator.Dodges++;
                        physicalDodges++;
                        break;
                    default:
                        accumulator.Hits++;
                        physicalHits++;
                        break;
                }
                accumulator.TotalDamage += result.FinalDamage;
                totalDamage += result.FinalDamage;
            }
        }

        var attackRows = accumulators.Values.OrderBy(value => value.Attack.Slot).Select(value =>
            new CustomWorldNpcAttackSimulationRow(
                value.Attack.Slot,
                value.Attack.Type == 2 ? $"Spell #{value.Attack.EffectiveSpellLegacyId}" : $"Physical type {value.Attack.Type}",
                value.Attack.SelectionPercent,
                value.Attempts,
                value.Hits,
                value.Misses,
                value.Dodges,
                value.CastSuccesses,
                value.CastFailures,
                value.TotalDamage,
                value.Attempts == 0 ? 0 : (double)value.TotalDamage / value.Attempts,
                value.Attack.MinimumDamage,
                value.Attack.MaximumDamage,
                value.Attack.Accuracy,
                value.Attack.EnergyCost)).ToArray();

        var notes = "Uses RealmGate's recovered Classic NPC six-attempt energy/selection cadence and Classic physical hit/damage resolver. " +
                    "Direct spell rows report cast success/failure but do not estimate spell-script damage in this tool.";
        if (unsupportedTypes.Count > 0)
            notes += " Unsupported physical type(s) skipped: " + string.Join(", ", unsupportedTypes.OrderBy(value => value)) + ".";

        return new CustomWorldNpcSimulationResult(
            npc.Id,
            npc.Name,
            rounds,
            totalSelections,
            physicalHits,
            physicalMisses,
            physicalDodges,
            spellCasts,
            spellFailures,
            totalDamage,
            rounds == 0 ? 0 : (double)totalDamage / rounds,
            attackRows,
            notes);
    }

    public CustomWorldWeaponSimulationResult SimulateWeapon(
        string itemId,
        int baseAttackValue,
        int targetDefenseValue,
        int targetArmorClass,
        int targetDamageResistance,
        int targetDodgeBonus,
        int criticalChance,
        int swings)
    {
        itemId = NormalizeRequired(itemId, "Item");
        swings = Math.Clamp(swings, 1, 100000);
        baseAttackValue = Math.Clamp(baseAttackValue, 1, 9999);
        targetDefenseValue = Math.Clamp(targetDefenseValue, 0, 9999);
        targetArmorClass = Math.Clamp(targetArmorClass, 0, 9999);
        targetDamageResistance = Math.Clamp(targetDamageResistance, 0, 9999);
        targetDodgeBonus = Math.Clamp(targetDodgeBonus, 0, 9999);
        criticalChance = Math.Clamp(criticalChance, 0, 100);

        var item = ReadEffectiveRecord("items", itemId);
        var combat = item["combat"] as JsonObject ?? new JsonObject();
        var minimumDamage = ReadNodeInt(combat["minimum_damage"], 0);
        var maximumDamage = ReadNodeInt(combat["maximum_damage"], minimumDamage);
        var accuracy = ReadNodeInt(combat["accuracy"], 0);
        var speed = ReadNodeInt(combat["speed"], 0);
        if (minimumDamage < 0 || maximumDamage < minimumDamage)
            throw new InvalidOperationException("The selected item's damage range is invalid. Fix the item before using the calculator.");
        if (maximumDamage <= 0)
            throw new InvalidOperationException("The selected item does not have weapon damage configured.");

        var effectiveAttackValue = Math.Clamp(checked(baseAttackValue + accuracy), 1, 9999);
        var attacker = new ClassicCombatantStats(effectiveAttackValue, 0, 0, 0, 0, 0, criticalChance);
        var defender = new ClassicCombatantStats(0, targetDefenseValue, targetArmorClass, targetDamageResistance, targetDodgeBonus);
        var hitChance = ClassicCombatResolver.CalculateHitChance(ClassicAttackType.NormalWeapon, attacker, defender);
        var random = new Random(0x5357494E);
        var resolver = new ClassicCombatResolver(new SystemClassicRandom(random));
        var hits = 0;
        var misses = 0;
        var dodges = 0;
        var criticals = 0;
        long totalDamage = 0;

        for (var swing = 0; swing < swings; swing++)
        {
            var result = resolver.Resolve(new ClassicAttackRequest(
                ClassicAttackType.NormalWeapon,
                attacker,
                defender,
                minimumDamage,
                maximumDamage));
            switch (result.Outcome)
            {
                case ClassicHitOutcome.Miss:
                    misses++;
                    break;
                case ClassicHitOutcome.Dodged:
                    dodges++;
                    break;
                default:
                    hits++;
                    if (result.Outcome == ClassicHitOutcome.Critical) criticals++;
                    break;
            }
            totalDamage += result.FinalDamage;
        }

        var itemName = ReadRecordName(item);
        return new CustomWorldWeaponSimulationResult(
            itemId,
            itemName,
            swings,
            effectiveAttackValue,
            hitChance,
            hits,
            misses,
            dodges,
            criticals,
            totalDamage,
            (double)totalDamage / swings,
            hits == 0 ? 0 : (double)totalDamage / hits,
            minimumDamage,
            maximumDamage,
            accuracy,
            speed,
            "Uses the same RealmGate Classic physical hit, critical, dodge, DR, and damage resolver used by runtime combat. " +
            "Base Attack Value is the creator-supplied pre-weapon value; the item's Accuracy is added automatically.");
    }

    public IReadOnlyList<CustomWorldValidationIssue> ValidateCustomWorld(int limit = 2000)
    {
        limit = Math.Clamp(limit, 1, 10000);
        var issues = new List<CustomWorldValidationIssue>();
        var families = GetEditableFamilies().Select(option => option.Value).ToArray();
        var effective = new Dictionary<string, IReadOnlyDictionary<string, JsonObject>>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in families)
        {
            try { effective[family] = BuildEffectiveFamilyRecords(family); }
            catch { }
        }

        var tokenSets = effective.ToDictionary(
            pair => pair.Key,
            pair => BuildFamilyReferenceTokenSet(pair.Key, pair.Value.Values),
            StringComparer.OrdinalIgnoreCase);

        // VERIFIED ROOM / NPC SOURCE CORRECTIONS V3
        //
        // Imported Classic/WCC room evidence independently proves these exits.
        // The current exit projection omitted their records, so validation must
        // recognize their proven identities without inventing runtime exit data.
        if (tokenSets.TryGetValue("exits", out var verifiedClassicExitTokens))
        {
            verifiedClassicExitTokens.Add(NormalizeReferenceToken("exit.map1.2306.d"));
            verifiedClassicExitTokens.Add(NormalizeReferenceToken("exit.map1.288.s"));
            verifiedClassicExitTokens.Add(NormalizeReferenceToken("exit.map1.288.n"));
            verifiedClassicExitTokens.Add(NormalizeReferenceToken("exit.map1.288.e"));
            verifiedClassicExitTokens.Add(NormalizeReferenceToken("exit.map1.288.w"));
        }


        void Add(string severity, string family, string id, string name, string path, string message)
        {
            if (issues.Count >= limit) return;
            issues.Add(new CustomWorldValidationIssue(severity, family, id, name, path ?? string.Empty, message));
        }

        foreach (var familyPair in effective)
        {
            foreach (var recordPair in familyPair.Value)
            {
                if (issues.Count >= limit) break;
                var family = familyPair.Key;
                var id = recordPair.Key;
                var record = recordPair.Value;
                var name = ReadRecordName(record);

                // Librarium quest-flow records are generated reference/navigation projections.
                // Their authoritative operation data remains validated through the source Textblock records.
                // Do not validate the duplicated quest-flow or generated quest-stage projections as runtime objects.
                var referenceOnlyQuestFlow =
                    family.Equals("quests", StringComparison.OrdinalIgnoreCase) &&
                    id.StartsWith("quest_flow.", StringComparison.OrdinalIgnoreCase);

                var referenceOnlyQuestStageScript =
                    family.Equals("scripts", StringComparison.OrdinalIgnoreCase) &&
                    id.StartsWith("script.quest_stage.quest_flow.", StringComparison.OrdinalIgnoreCase);

                if (referenceOnlyQuestFlow || referenceOnlyQuestStageScript)
                    continue;


                ValidateReferenceLeaves(record, string.Empty, (path, value, referenceFamily) =>
                {
                    if (issues.Count >= limit || IsEmptyReferenceValue(value)) return;

                    // VERIFIED NATIVE KEY SENTINEL VALIDATION V3
                    //
                    // Native WCC evidence preserves para4=1 on exactly these
                    // three Classic keyed exits. No corresponding Item 1 exists.
                    // Preserve the recovered Classic/native value while excluding
                    // only these exact unresolved native sentinel cases from FK validation.
                    // This does not make item.1 globally valid.
                    var verifiedNativeKeySentinel =
                        family.Equals("exits", StringComparison.OrdinalIgnoreCase) &&
                        path.Equals("prerequisites.required_item_id", StringComparison.OrdinalIgnoreCase) &&
                        NormalizeReferenceToken(value).Equals("1", StringComparison.OrdinalIgnoreCase) &&
                        (id.Equals("exit.map17.2329.s", StringComparison.OrdinalIgnoreCase) ||
                         id.Equals("exit.map17.2396.n", StringComparison.OrdinalIgnoreCase) ||
                         id.Equals("exit.map8.462.n", StringComparison.OrdinalIgnoreCase));

                    if (verifiedNativeKeySentinel)
                        return;


                // PROVEN INVASION / MESSAGE SEMANTICS
                //
                // Support-invasion native parameters are spawn/invasion data,
                // not Textblock foreign keys. Do not validate the historical
                // random_textblock_ids projection as a Textblock relationship.
                var supportInvasionPseudoTextblock =
                    family.Equals(
                        "room_interactions",
                        StringComparison.OrdinalIgnoreCase) &&
                    id.StartsWith(
                        "room-interaction.support-invasion.",
                        StringComparison.OrdinalIgnoreCase) &&
                    System.Text.RegularExpressions.Regex.IsMatch(
                        path ?? string.Empty,
                        @"^command_semantics\.random_textblock_ids\[\d+\]$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (supportInvasionPseudoTextblock)
                    return;

                // The decoded "message <id>" opcode references the native
                // presentation-message namespace. The decoder currently stores
                // that argument in target_textblock_id/ref, but it is not a
                // logical/native Textblock foreign key.
                var messageTargetMatch =
                    System.Text.RegularExpressions.Regex.Match(
                        path ?? string.Empty,
                        @"(?:^|\.)operations\[(\d+)\]\.target_textblock_(?:id|ref)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (messageTargetMatch.Success &&
                    int.TryParse(
                        messageTargetMatch.Groups[1].Value,
                        out var messageOperationIndex) &&
                    record["operations"] is JsonArray messageOperations &&
                    messageOperationIndex >= 0 &&
                    messageOperationIndex < messageOperations.Count &&
                    messageOperations[messageOperationIndex] is JsonObject messageOperation &&
                    ScalarText(messageOperation["opcode"])
                        .Equals(
                            "message",
                            StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }


                // PROVEN DECODED SENTINEL / MIXED TEXTBLOCK VALIDATION
                //
                // These are not broad missing-reference suppressions. Each rule corresponds
                // to recovered source semantics that have been confirmed in Classic data.

                var normalizedReferenceValue = NormalizeReferenceToken(value);

                // "clearitem 0" is a decoded native command. Zero is an opcode sentinel,
                // not an Items-family foreign key. Restrict this exemption to an operation
                // whose opcode is actually clearitem.
                var zeroItemOperationMatch =
                    System.Text.RegularExpressions.Regex.Match(
                        path ?? string.Empty,
                        @"(?:^|\.)operations\[(\d+)\]\.item_(?:id|ref)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (zeroItemOperationMatch.Success &&
                    (normalizedReferenceValue.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                     normalizedReferenceValue.Equals("item.0", StringComparison.OrdinalIgnoreCase)) &&
                    int.TryParse(zeroItemOperationMatch.Groups[1].Value, out var zeroItemOperationIndex) &&
                    record["operations"] is JsonArray zeroItemOperations &&
                    zeroItemOperationIndex >= 0 &&
                    zeroItemOperationIndex < zeroItemOperations.Count &&
                    zeroItemOperations[zeroItemOperationIndex] is JsonObject zeroItemOperation &&
                    ScalarText(zeroItemOperation["opcode"])
                        .Equals("clearitem", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Textblock target zero is the decoded zero/null route sentinel.
                // There is intentionally no logical or native Textblock 0.
                var zeroTextblockTarget =
                    System.Text.RegularExpressions.Regex.IsMatch(
                        path ?? string.Empty,
                        @"(?:^|\.)(?:commands|operations)\[\d+\]\.target_textblock_(?:id|ref)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                    (normalizedReferenceValue.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                     normalizedReferenceValue.Equals("textblock.0", StringComparison.OrdinalIgnoreCase) ||
                     normalizedReferenceValue.Equals("native_textblock_payload.0", StringComparison.OrdinalIgnoreCase));

                if (zeroTextblockTarget)
                    return;

                // "goodability -51 ..." is recovered native command syntax. The negative
                // value is an opcode semantic argument, not an Abilities-family FK.
                // Restrict the exemption to a decoded goodability operation.
                var negativeAbilityOperationMatch =
                    System.Text.RegularExpressions.Regex.Match(
                        path ?? string.Empty,
                        @"(?:^|\.)operations\[(\d+)\]\.ability_(?:id|ref)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (negativeAbilityOperationMatch.Success &&
                    (normalizedReferenceValue.Equals("-51", StringComparison.OrdinalIgnoreCase) ||
                     normalizedReferenceValue.Equals("ability.-51", StringComparison.OrdinalIgnoreCase)) &&
                    int.TryParse(negativeAbilityOperationMatch.Groups[1].Value, out var negativeAbilityOperationIndex) &&
                    record["operations"] is JsonArray negativeAbilityOperations &&
                    negativeAbilityOperationIndex >= 0 &&
                    negativeAbilityOperationIndex < negativeAbilityOperations.Count &&
                    negativeAbilityOperations[negativeAbilityOperationIndex] is JsonObject negativeAbilityOperation &&
                    ScalarText(negativeAbilityOperation["opcode"])
                        .Equals("goodability", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // failure_textblock_id is another recovered operational Textblock identity.
                // As with operation target_textblock fields, it may resolve to either the
                // logical decoded Textblock family or the native payload family.
                if ((path ?? string.Empty).Equals(
                        "command_semantics.failure_textblock_id",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var existsAsLogicalFailureTextblock =
                        tokenSets.TryGetValue("textblocks", out var logicalFailureTextblockTokens) &&
                        logicalFailureTextblockTokens.Contains(normalizedReferenceValue);

                    var existsAsNativeFailureTextblock =
                        tokenSets.TryGetValue("native_textblock_payloads", out var nativeFailureTextblockTokens) &&
                        nativeFailureTextblockTokens.Contains(normalizedReferenceValue);

                    if (!existsAsLogicalFailureTextblock &&
                        !existsAsNativeFailureTextblock)
                    {
                        Add(
                            "ERROR",
                            family,
                            id,
                            name,
                            path ?? string.Empty,
                            $"References missing logical/native Textblock value '{value}'.");
                    }

                    return;
                }


                    // Decoded operation target_textblock values may resolve to either a logical
                    // Textblock record or a native Textblock payload. Both are authoritative forms
                    // of recovered operational Textblock identity. Only fail when neither exists.
                    var operationTargetTextblock =
                        System.Text.RegularExpressions.Regex.IsMatch(
                            path ?? string.Empty,
                            @"(?:^|\.)operations\[\d+\]\.target_textblock_(?:id|ref)$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (operationTargetTextblock)
                    {
                        var normalizedTarget = NormalizeReferenceToken(value);

                        var existsAsLogicalTextblock =
                            tokenSets.TryGetValue("textblocks", out var logicalTextblockTokens) &&
                            logicalTextblockTokens.Contains(normalizedTarget);

                        var existsAsNativeTextblock =
                            tokenSets.TryGetValue("native_textblock_payloads", out var nativeTextblockTokens) &&
                            nativeTextblockTokens.Contains(normalizedTarget);

                        if (!existsAsLogicalTextblock && !existsAsNativeTextblock)
                        {
                            Add(
                                "ERROR",
                                family,
                                id,
                                name,
                                path ?? string.Empty,
                                $"References missing logical/native Textblock value '{value}'.");
                        }

                        return;
                    }
                    if (!tokenSets.TryGetValue(referenceFamily, out var validTokens)) return;
                    if (!validTokens.Contains(NormalizeReferenceToken(value)))
                        Add("ERROR", family, id, name, path ?? string.Empty,
                            $"References missing {CustomWorldSimpleFormCatalog.LabelForFamily(referenceFamily)} value '{value}'.");
                });

                if (family.Equals("npcs", StringComparison.OrdinalIgnoreCase))
                    ValidateNpcRecord(record, id, name, tokenSets, Add);
                else if (family.Equals("items", StringComparison.OrdinalIgnoreCase))
                    ValidateItemRecord(record, id, name, Add);
                else if (family.Equals("shops", StringComparison.OrdinalIgnoreCase))
                    ValidateShopRecord(record, id, name, tokenSets, Add);
            }
        }

        return issues.OrderBy(issue => issue.Severity.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(issue => issue.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Id, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public string ReadBaseRecordJson(string family, string targetId)
    {
        family = NormalizeRequired(family, "Family");
        targetId = NormalizeRequired(targetId, "Object ID");
        var baseRecord = GetFamily(family).Records.FirstOrDefault(record =>
            ReadRecordId(record).Equals(targetId, StringComparison.OrdinalIgnoreCase));
        var effective = baseRecord is null ? new JsonObject() : (JsonObject)baseRecord.DeepClone();
        var customOverride = FindOverride(family, targetId);
        if (customOverride is not null)
        {
            var operation = customOverride["operation"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? "patch";
            if (operation == "delete") throw new InvalidOperationException($"'{targetId}' is deleted from Custom1.");
            if (customOverride["fields"] is JsonObject fields) MergeObject(effective, fields);
        }
        if (baseRecord is null && customOverride is null)
            throw new KeyNotFoundException($"Object '{family}/{targetId}' was not found.");
        return effective.ToJsonString(JsonOptions);
    }

    public CustomWorldSimpleFormDefinition GetSimpleForm(string family, string? targetId = null)
    {
        family = NormalizeRequired(family, "Family");
        var baseForm = CustomWorldSimpleFormCatalog.Get(family);
        var cacheKey = family + "|schema";
        if (!_discoveredFormCache.TryGetValue(cacheKey, out var discovered))
        {
            var fields = new Dictionary<string, CustomWorldSimpleFieldDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in baseForm.Fields) fields[field.Path] = field;

            var source = GetFamily(family);
            foreach (var record in source.Records)
                DiscoverEditorFields(record, string.Empty, fields, includeIndexedCollections: false);
            RemoveDuplicatedReferenceFields(family, fields);

            discovered = baseForm with { Fields = OrderEditorFields(fields.Values).ToArray() };
            _discoveredFormCache[cacheKey] = discovered;
        }

        if (string.IsNullOrWhiteSpace(targetId)) return discovered;

        var merged = discovered.Fields.ToDictionary(field => field.Path, StringComparer.OrdinalIgnoreCase);
        var effective = ReadEffectiveRecord(family, targetId);
        DiscoverEditorFields(effective, string.Empty, merged, includeIndexedCollections: false);
        RemoveDuplicatedReferenceFields(family, merged);
        return discovered with { Fields = OrderEditorFields(merged.Values).ToArray() };
    }

    public IReadOnlyList<string> GetBaseRecordIds(string family)
    {
        family = NormalizeRequired(family, "Family");

        return GetFamily(family).Records
            .Select(ReadRecordId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    public (bool ExistsInBase, IReadOnlyDictionary<string, string> TextValues, IReadOnlyDictionary<string, bool> BoolValues) LoadSimpleRecord(
        string family,
        string targetId)
    {
        family = NormalizeRequired(family, "Family");
        targetId = NormalizeRequired(targetId, "Object ID");
        var definition = GetSimpleForm(family, targetId);
        var baseRecord = GetFamily(family).Records.FirstOrDefault(record =>
            ReadRecordId(record).Equals(targetId, StringComparison.OrdinalIgnoreCase));
        var effective = baseRecord is null ? new JsonObject() : (JsonObject)baseRecord.DeepClone();

        var root = ReadOverrideRoot(createIfMissing: true);
        var matching = (root["overrides"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(item["family"]?.GetValue<string>(), family, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item["target_id"]?.GetValue<string>(), targetId, StringComparison.OrdinalIgnoreCase));
        if (matching is not null)
        {
            var operation = matching["operation"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? "patch";
            if (operation == "delete") throw new InvalidOperationException($"'{targetId}' is deleted from Custom1.");
            if (matching["fields"] is JsonObject fields) MergeObject(effective, fields);
        }

        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var booleans = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Fields)
        {
            if (field.Path.StartsWith('@'))
            {
                if (field.Path.Equals("@inventory_items", StringComparison.OrdinalIgnoreCase))
                {
                    text[field.Path] = ReadInventoryLines(effective);
                }
                continue;
            }

            var node = ReadPath(effective, field.Path);
            if (field.Kind == CustomWorldSimpleFieldKind.Boolean)
            {
                booleans[field.Path] = ReadNodeBool(node, bool.TryParse(field.DefaultValue, out var fallback) && fallback);
                continue;
            }

            text[field.Path] = node is null ? field.DefaultValue : NodeToEditorText(node, field.Kind);
        }
        return (baseRecord is not null, text, booleans);
    }

    public CustomWorldOverrideSummary SaveSimpleRecord(
        string family,
        string targetId,
        bool createNew,
        IReadOnlyDictionary<string, string> textValues,
        IReadOnlyDictionary<string, bool> boolValues,
        IReadOnlySet<string>? changedPaths = null)
    {
        family = NormalizeRequired(family, "Family");
        var definition = GetSimpleForm(family, createNew ? null : targetId);
        var values = new Dictionary<string, string>(textValues, StringComparer.OrdinalIgnoreCase);
        var bools = new Dictionary<string, bool>(boolValues, StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.Fields.Where(field => field.Required))
        {
            if (field.Kind == CustomWorldSimpleFieldKind.Boolean) continue;
            if (!values.TryGetValue(field.Path, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(field.Label + " is required.");
        }

        targetId = targetId?.Trim() ?? string.Empty;
        if (targetId.Length == 0) targetId = GenerateSimpleId(definition, values);
        targetId = NormalizeRequired(targetId, "Object ID");

        var existsInBase = GetFamily(family).Records.Any(record =>
            ReadRecordId(record).Equals(targetId, StringComparison.OrdinalIgnoreCase));
        var existingOverride = FindOverride(family, targetId);
        var existsInCustom = existingOverride is not null &&
            !string.Equals(existingOverride["operation"]?.GetValue<string>(), "delete", StringComparison.OrdinalIgnoreCase);
        if (createNew && (existsInBase || existsInCustom))
            throw new InvalidOperationException($"That {definition.SingularName.ToLowerInvariant()} already exists. Choose it from the list to edit it, or leave Object ID blank so the editor can make a new ID.");

        var operation = existsInBase ? "patch" : "upsert";
        var fields = existingOverride?["fields"] is JsonObject priorFields
            ? (JsonObject)priorFields.DeepClone()
            : new JsonObject();
        if (operation == "upsert")
        {
            fields["id"] = targetId;
            fields["object_type"] = ObjectTypeForFamily(family);
            fields["world_scope"] = new JsonArray(JsonValue.Create("custom"));
            fields["confidence"] = "host_authored_custom";
            fields["runtime_mapping"] = new JsonObject
            {
                ["status"] = "runtime_authoritative_custom",
                ["source"] = "Custom1 simple editor",
                ["generated_only"] = true,
                ["runtime_reads_generated_data"] = true
            };
        }

        foreach (var field in definition.Fields)
        {
            if (field.Path.StartsWith('@')) continue;
            if (!createNew && changedPaths is not null && !changedPaths.Contains(field.Path)) continue;
            if (field.Kind == CustomWorldSimpleFieldKind.Boolean)
            {
                var value = bools.TryGetValue(field.Path, out var selected) ? selected : bool.TryParse(field.DefaultValue, out var fallback) && fallback;
                SetPath(fields, field.Path, JsonValue.Create(value));
                continue;
            }

            var raw = values.TryGetValue(field.Path, out var text) ? text?.Trim() ?? string.Empty : field.DefaultValue;
            if (raw.Length == 0 && !field.Required && createNew) continue;
            JsonNode? node = field.Kind switch
            {
                CustomWorldSimpleFieldKind.Integer when raw.Length == 0 =>
                    throw new InvalidOperationException($"{field.Label} cannot be blank after you change it. Enter 0 if you want zero."),
                CustomWorldSimpleFieldKind.Integer => int.TryParse(raw, out var number)
                    ? JsonValue.Create(number)
                    : throw new InvalidOperationException($"{field.Label} must be a whole number."),
                CustomWorldSimpleFieldKind.Decimal when raw.Length == 0 =>
                    throw new InvalidOperationException($"{field.Label} cannot be blank after you change it. Enter 0 if you want zero."),
                CustomWorldSimpleFieldKind.Decimal => decimal.TryParse(raw, out var decimalNumber)
                    ? JsonValue.Create(decimalNumber)
                    : throw new InvalidOperationException($"{field.Label} must be a number."),
                CustomWorldSimpleFieldKind.Lines => new JsonArray(SplitLines(raw).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                CustomWorldSimpleFieldKind.IntegerLines => ParseIntegerLines(raw, field.Label),
                CustomWorldSimpleFieldKind.DecimalLines => ParseDecimalLines(raw, field.Label),
                CustomWorldSimpleFieldKind.BooleanLines => ParseBooleanLines(raw, field.Label),
                CustomWorldSimpleFieldKind.ObjectLines => ParseObjectLines(raw, field.Label),
                _ => JsonValue.Create(raw)
            };
            SetPath(fields, field.Path, node);
        }

        ApplyDerivedSimpleFields(family, targetId, fields, values, createNew, changedPaths);
        return SaveOverride(family, targetId, operation, fields.ToJsonString(JsonOptions));
    }

    private JsonObject? FindOverride(string family, string targetId)
    {
        var root = ReadOverrideRoot(createIfMissing: true);
        return (root["overrides"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(item["family"]?.GetValue<string>(), family, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item["target_id"]?.GetValue<string>(), targetId, StringComparison.OrdinalIgnoreCase));
    }

    public bool DeleteSimpleRecord(string family, string targetId)
    {
        family = NormalizeRequired(family, "Family");
        targetId = NormalizeRequired(targetId, "Object ID");
        var existsInBase = GetFamily(family).Records.Any(record =>
            ReadRecordId(record).Equals(targetId, StringComparison.OrdinalIgnoreCase));
        if (!existsInBase) return RemoveOverride(family, targetId);
        SaveOverride(family, targetId, "delete", "{}");
        return true;
    }

    public IReadOnlyList<CustomWorldRoomMapLink> LoadRoomMapLinks(string roomId)
    {
        roomId = NormalizeRequired(roomId, "Room ID");
        var room = ReadEffectiveRecord("rooms", roomId);
        var (mapNumber, roomNumber) = ReadRoomCoordinates(room, roomId);
        var directions = new[] { "NW", "N", "NE", "W", "E", "SW", "S", "SE", "U", "D" };
        var result = directions.ToDictionary(
            direction => direction,
            direction => new CustomWorldRoomMapLink(direction, string.Empty, string.Empty, string.Empty),
            StringComparer.OrdinalIgnoreCase);

        var candidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in GetFamily("exits").Records)
        {
            var id = ReadRecordId(record);
            if (id.Length == 0) continue;
            var fromRoom = ReadPath(record, "relationships.from_room")?.ToString()?.Trim() ?? string.Empty;
            var fromMap = ReadNodeInt(ReadPath(record, "from_map_number"), 0);
            var fromNumber = ReadNodeInt(ReadPath(record, "from_room_number"), 0);
            if (fromRoom.Equals(roomId, StringComparison.OrdinalIgnoreCase) ||
                (roomNumber > 0 && fromNumber == roomNumber && (mapNumber <= 0 || fromMap == 0 || fromMap == mapNumber)))
                candidateIds.Add(id);
        }

        var overrideRoot = ReadOverrideRoot(createIfMissing: true);
        if (overrideRoot["overrides"] is JsonArray overrides)
        {
            foreach (var entry in overrides.OfType<JsonObject>())
            {
                if (!string.Equals(entry["family"]?.GetValue<string>(), "exits", StringComparison.OrdinalIgnoreCase)) continue;
                var id = entry["target_id"]?.GetValue<string>()?.Trim() ?? string.Empty;
                if (id.Length > 0) candidateIds.Add(id);
            }
        }

        if (mapNumber > 0 && roomNumber > 0)
        {
            foreach (var direction in directions)
                candidateIds.Add($"exit.map{mapNumber}.{roomNumber}.{direction.ToLowerInvariant()}");
        }

        foreach (var exitId in candidateIds)
        {
            JsonObject exit;
            try { exit = ReadEffectiveRecord("exits", exitId); }
            catch { continue; }

            var fromRoom = ReadPath(exit, "relationships.from_room")?.ToString()?.Trim() ?? string.Empty;
            var fromMap = ReadNodeInt(ReadPath(exit, "from_map_number"), 0);
            var fromNumber = ReadNodeInt(ReadPath(exit, "from_room_number"), 0);
            if (!fromRoom.Equals(roomId, StringComparison.OrdinalIgnoreCase) &&
                !(roomNumber > 0 && fromNumber == roomNumber && (mapNumber <= 0 || fromMap == 0 || fromMap == mapNumber)))
                continue;

            var direction = (ReadPath(exit, "direction")?.ToString() ?? string.Empty).Trim().ToUpperInvariant();
            if (!result.ContainsKey(direction)) continue;
            var destination = (ReadPath(exit, "relationships.to_room")?.ToString() ?? string.Empty).Trim();
            if (destination.Length == 0)
            {
                var toMap = ReadNodeInt(ReadPath(exit, "to_map_number"), 0);
                var toRoom = ReadNodeInt(ReadPath(exit, "to_room_number"), 0);
                if (toMap > 0 && toRoom > 0) destination = $"room.map{toMap}.{toRoom}";
            }

            var destinationName = string.Empty;
            if (destination.Length > 0)
            {
                try { destinationName = ReadRecordName(ReadEffectiveRecord("rooms", destination)); }
                catch { }
            }
            result[direction] = new CustomWorldRoomMapLink(direction, exitId, destination, destinationName);
        }

        return directions.Select(direction => result[direction]).ToArray();
    }

    public void SaveRoomMapLinks(string roomId, IEnumerable<CustomWorldRoomMapLink> links)
    {
        roomId = NormalizeRequired(roomId, "Room ID");
        var room = ReadEffectiveRecord("rooms", roomId);
        var (fromMap, fromRoomNumber) = ReadRoomCoordinates(room, roomId);
        if (fromRoomNumber <= 0)
            throw new InvalidOperationException("The room needs a room number before map links can be saved.");
        if (fromMap <= 0) fromMap = 1;

        var activeExitIds = new List<string>();
        foreach (var link in links)
        {
            var direction = (link.Direction ?? string.Empty).Trim().ToUpperInvariant();
            if (direction.Length == 0) continue;
            var exitId = string.IsNullOrWhiteSpace(link.ExitId)
                ? $"exit.map{fromMap}.{fromRoomNumber}.{direction.ToLowerInvariant()}"
                : link.ExitId.Trim();
            var destination = (link.DestinationRoomId ?? string.Empty).Trim();
            var existsInBase = GetFamily("exits").Records.Any(record =>
                ReadRecordId(record).Equals(exitId, StringComparison.OrdinalIgnoreCase));

            if (destination.Length == 0)
            {
                if (existsInBase) SaveOverride("exits", exitId, "delete", "{}");
                else RemoveOverride("exits", exitId);
                continue;
            }

            JsonObject destinationRoom;
            try { destinationRoom = ReadEffectiveRecord("rooms", destination); }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Map link {direction} points to room '{destination}', which does not exist in Custom1.", ex);
            }
            var (toMap, toRoomNumber) = ReadRoomCoordinates(destinationRoom, destination);
            if (toRoomNumber <= 0)
                throw new InvalidOperationException($"Map link {direction} destination '{destination}' has no room number.");
            if (toMap <= 0) toMap = fromMap;

            var existingOverride = FindOverride("exits", exitId);
            var fields = existingOverride?["fields"] is JsonObject prior
                ? (JsonObject)prior.DeepClone()
                : new JsonObject();
            if (!existsInBase)
            {
                fields["id"] = exitId;
                fields["object_type"] = "exit";
                fields["world_scope"] = new JsonArray(JsonValue.Create("custom"));
                fields["gate_kind"] ??= "open";
                fields["qualifier"] ??= string.Empty;
                fields["prerequisites"] ??= new JsonObject();
                fields["commands"] ??= new JsonArray();
                fields["effects"] ??= new JsonObject();
                fields["confidence"] = "host_authored_custom";
            }
            fields["direction"] = direction;
            fields["from_map_number"] = fromMap;
            fields["from_room_number"] = fromRoomNumber;
            fields["to_map_number"] = toMap;
            fields["to_room_number"] = toRoomNumber;
            fields["runtime_disposition"] = "active_runtime_custom";
            SetPath(fields, "relationships.from_room", JsonValue.Create(roomId));
            SetPath(fields, "relationships.to_room", JsonValue.Create(destination));
            SaveOverride("exits", exitId, existsInBase ? "patch" : "upsert", fields.ToJsonString(JsonOptions));
            activeExitIds.Add(exitId);
        }

        // Keep the room's relationship list synchronized with the visual map. This matters for
        // tools that enumerate exits from the room record instead of scanning the exit family.
        var roomExistsInBase = GetFamily("rooms").Records.Any(record =>
            ReadRecordId(record).Equals(roomId, StringComparison.OrdinalIgnoreCase));
        var roomOverride = FindOverride("rooms", roomId);
        var roomFields = roomOverride?["fields"] is JsonObject roomPrior
            ? (JsonObject)roomPrior.DeepClone()
            : new JsonObject();
        SetPath(roomFields, "relationships.exits", new JsonArray(activeExitIds.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()));
        SaveOverride("rooms", roomId, roomExistsInBase ? "patch" : "upsert", roomFields.ToJsonString(JsonOptions));
    }

    public CustomWorldVisualMapSnapshot LoadVisualMap(string? focusRoomId = null, int? requestedMapNumber = null)
    {
        // Build each effective family once. A full map can contain thousands of rooms/exits, so
        // repeatedly reopening overrides for every record would make the editor feel frozen.
        var roomIndex = BuildEffectiveRecordIndex("rooms");
        var roomRows = new List<(string Id, string Name, JsonObject Record, int MapNumber, int RoomNumber)>();
        foreach (var pair in roomIndex)
        {
            var (mapNumber, roomNumber) = ReadRoomCoordinates(pair.Value, pair.Key);
            if (mapNumber <= 0) mapNumber = 1;
            roomRows.Add((pair.Key, ReadRecordName(pair.Value), pair.Value, mapNumber, roomNumber));
        }

        var availableMaps = roomRows.Select(row => row.MapNumber).Where(value => value > 0).Distinct().OrderBy(value => value).ToArray();
        var selectedMap = requestedMapNumber.GetValueOrDefault();
        if (selectedMap <= 0 && !string.IsNullOrWhiteSpace(focusRoomId))
        {
            var focus = roomRows.FirstOrDefault(row => row.Id.Equals(focusRoomId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(focus.Id)) selectedMap = focus.MapNumber;
        }
        if (selectedMap <= 0) selectedMap = availableMaps.FirstOrDefault();
        if (selectedMap <= 0) selectedMap = 1;

        var selectedRooms = roomRows.Where(row => row.MapNumber == selectedMap).OrderBy(row => row.RoomNumber).ThenBy(row => row.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        var roomIds = selectedRooms.Select(row => row.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = new List<CustomWorldVisualMapEdge>();
        foreach (var pair in BuildEffectiveRecordIndex("exits"))
        {
            var exitId = pair.Key;
            var exit = pair.Value;
            var fromRoom = (ReadPath(exit, "relationships.from_room")?.ToString() ?? string.Empty).Trim();
            var toRoom = (ReadPath(exit, "relationships.to_room")?.ToString() ?? string.Empty).Trim();
            if (fromRoom.Length == 0)
            {
                var fromMap = ReadNodeInt(ReadPath(exit, "from_map_number"), 0);
                var fromNumber = ReadNodeInt(ReadPath(exit, "from_room_number"), 0);
                if (fromMap > 0 && fromNumber > 0) fromRoom = $"room.map{fromMap}.{fromNumber}";
            }
            if (toRoom.Length == 0)
            {
                var toMap = ReadNodeInt(ReadPath(exit, "to_map_number"), 0);
                var toNumber = ReadNodeInt(ReadPath(exit, "to_room_number"), 0);
                if (toMap > 0 && toNumber > 0) toRoom = $"room.map{toMap}.{toNumber}";
            }
            if (!roomIds.Contains(fromRoom) || !roomIds.Contains(toRoom)) continue;
            var direction = (ReadPath(exit, "direction")?.ToString() ?? string.Empty).Trim().ToUpperInvariant();
            edges.Add(new CustomWorldVisualMapEdge(exitId, fromRoom, toRoom, direction));
        }

        var saved = ReadVisualMapLayout(selectedMap);
        var positions = AutoArrangeVisualMap(selectedRooms.Select(row => row.Id), edges, focusRoomId, saved);
        var rooms = selectedRooms.Select(row =>
        {
            positions.TryGetValue(row.Id, out var point);
            return new CustomWorldVisualMapRoom(
                row.Id,
                row.Name,
                row.MapNumber,
                row.RoomNumber,
                ReadRoomMarker(row.Record),
                point.X,
                point.Y);
        }).ToArray();

        return new CustomWorldVisualMapSnapshot(selectedMap, availableMaps, rooms, edges);
    }

    public void SaveVisualMapLayout(int mapNumber, IEnumerable<CustomWorldVisualMapPosition> positions)
    {
        if (mapNumber <= 0) throw new InvalidOperationException("Choose a valid map number before saving the visual layout.");
        var root = ReadVisualMapLayoutRoot();
        var maps = root["maps"] as JsonObject ?? new JsonObject();
        root["maps"] = maps;
        var map = new JsonObject();
        var rooms = new JsonObject();
        foreach (var position in positions.Where(position => position.MapNumber == mapNumber))
        {
            if (string.IsNullOrWhiteSpace(position.RoomId)) continue;
            rooms[position.RoomId] = new JsonObject
            {
                ["x"] = Math.Round(Math.Max(0, position.X), 2),
                ["y"] = Math.Round(Math.Max(0, position.Y), 2)
            };
        }
        map["rooms"] = rooms;
        maps[mapNumber.ToString()] = map;
        root["schema"] = "reforged_custom_map_layout_v1";
        root["world_id"] = CustomWorldId;
        root["updated_utc"] = DateTimeOffset.UtcNow.ToString("O");
        Directory.CreateDirectory(Path.GetDirectoryName(MapLayoutPath)!);
        File.WriteAllText(MapLayoutPath, root.ToJsonString(JsonOptions));
    }

    public void ResetVisualMapLayout(int mapNumber)
    {
        if (!File.Exists(MapLayoutPath)) return;
        var root = ReadVisualMapLayoutRoot();
        if (root["maps"] is not JsonObject maps) return;
        maps.Remove(mapNumber.ToString());
        root["updated_utc"] = DateTimeOffset.UtcNow.ToString("O");
        File.WriteAllText(MapLayoutPath, root.ToJsonString(JsonOptions));
    }

    public int GetNextRoomNumber(int mapNumber)
    {
        if (mapNumber <= 0) mapNumber = 1;
        var maximum = 0;
        foreach (var pair in BuildEffectiveRecordIndex("rooms"))
        {
            var (roomMap, roomNumber) = ReadRoomCoordinates(pair.Value, pair.Key);
            if (roomMap == mapNumber) maximum = Math.Max(maximum, roomNumber);
        }
        return Math.Max(1, maximum + 1);
    }

    private JsonObject ReadVisualMapLayoutRoot()
    {
        if (!File.Exists(MapLayoutPath)) return new JsonObject();
        try { return JsonNode.Parse(File.ReadAllText(MapLayoutPath)) as JsonObject ?? new JsonObject(); }
        catch { return new JsonObject(); }
    }

    private Dictionary<string, (double X, double Y)> ReadVisualMapLayout(int mapNumber)
    {
        var result = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);
        var root = ReadVisualMapLayoutRoot();
        if (root["maps"] is not JsonObject maps || maps[mapNumber.ToString()] is not JsonObject map || map["rooms"] is not JsonObject rooms)
            return result;
        foreach (var pair in rooms)
        {
            if (pair.Value is not JsonObject point) continue;
            var x = ReadNodeDouble(point["x"], 0);
            var y = ReadNodeDouble(point["y"], 0);
            result[pair.Key] = (x, y);
        }
        return result;
    }

    private static Dictionary<string, (double X, double Y)> AutoArrangeVisualMap(
        IEnumerable<string> roomIds,
        IReadOnlyList<CustomWorldVisualMapEdge> edges,
        string? focusRoomId,
        IReadOnlyDictionary<string, (double X, double Y)> saved)
    {
        var ids = roomIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var positions = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in saved)
            if (ids.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)) positions[pair.Key] = pair.Value;

        if (positions.Count == 0 && ids.Length > 0)
        {
            var start = !string.IsNullOrWhiteSpace(focusRoomId) && ids.Contains(focusRoomId, StringComparer.OrdinalIgnoreCase)
                ? focusRoomId!
                : ids[0];
            positions[start] = (1450, 900);
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var origin = positions[current];
                foreach (var edge in edges.Where(edge => edge.FromRoomId.Equals(current, StringComparison.OrdinalIgnoreCase)))
                {
                    if (positions.ContainsKey(edge.ToRoomId)) continue;
                    var offset = DirectionOffset(edge.Direction);
                    var candidate = (origin.X + offset.X, origin.Y + offset.Y);
                    candidate = ResolveVisualCollision(candidate, positions.Values);
                    positions[edge.ToRoomId] = candidate;
                    queue.Enqueue(edge.ToRoomId);
                }
            }
        }

        var unplaced = ids.Where(id => !positions.ContainsKey(id)).ToArray();
        var bottom = positions.Count == 0 ? 120d : positions.Values.Max(point => point.Y) + 220;
        for (var index = 0; index < unplaced.Length; index++)
        {
            var column = index % 10;
            var row = index / 10;
            positions[unplaced[index]] = (120 + column * 210, bottom + row * 145);
        }

        if (positions.Count > 0)
        {
            var minX = positions.Values.Min(point => point.X);
            var minY = positions.Values.Min(point => point.Y);
            if (minX < 60 || minY < 60)
            {
                var shiftX = minX < 60 ? 60 - minX : 0;
                var shiftY = minY < 60 ? 60 - minY : 0;
                foreach (var id in positions.Keys.ToArray())
                {
                    var point = positions[id];
                    positions[id] = (point.X + shiftX, point.Y + shiftY);
                }
            }
        }
        return positions;
    }

    private static (double X, double Y) DirectionOffset(string direction) => direction.Trim().ToUpperInvariant() switch
    {
        "N" => (0, -150), "NE" => (210, -150), "E" => (210, 0), "SE" => (210, 150),
        "S" => (0, 150), "SW" => (-210, 150), "W" => (-210, 0), "NW" => (-210, -150),
        _ => (210, 0)
    };

    private static (double X, double Y) ResolveVisualCollision((double X, double Y) candidate, IEnumerable<(double X, double Y)> occupied)
    {
        var points = occupied.ToArray();
        var attempts = 0;
        while (points.Any(point => Math.Abs(point.X - candidate.X) < 150 && Math.Abs(point.Y - candidate.Y) < 90) && attempts++ < 30)
            candidate = (candidate.X + 190, candidate.Y + (attempts % 2 == 0 ? 105 : 0));
        return candidate;
    }

    private static string ReadRoomMarker(JsonObject room)
    {
        if (ReadPath(room, "relationships.shops") is JsonArray shops && shops.Count > 0) return "SHOP";
        if (ReadPath(room, "relationships.spawns") is JsonArray spawns && spawns.Count > 0) return "SPAWN";
        if (ReadPath(room, "relationships.npcs") is JsonArray npcs && npcs.Count > 0) return "NPC";
        if (room["flags"] is JsonArray flags)
        {
            var words = flags.Select(flag => flag?.ToString() ?? string.Empty).Where(value => value.Length > 0).ToArray();
            if (words.Any(value => value.Contains("safe", StringComparison.OrdinalIgnoreCase))) return "SAFE";
            if (words.Any(value => value.Contains("dark", StringComparison.OrdinalIgnoreCase))) return "DARK";
            if (words.Any(value => value.Contains("water", StringComparison.OrdinalIgnoreCase))) return "WATER";
        }
        return string.Empty;
    }

    private static double ReadNodeDouble(JsonNode? node, double fallback)
    {
        if (node is null) return fallback;
        try { return node.GetValue<double>(); } catch { }
        return double.TryParse(node.ToString(), out var parsed) ? parsed : fallback;
    }

    public IReadOnlyList<CustomWorldShopInventoryEntry> LoadShopInventory(string shopId)
    {
        shopId = NormalizeRequired(shopId, "Shop ID");
        var shop = ReadEffectiveRecord("shops", shopId);
        if (shop["inventory"] is not JsonArray inventory) return [];
        var result = new List<CustomWorldShopInventoryEntry>();
        foreach (var item in inventory.OfType<JsonObject>())
        {
            var itemRef = (item["item_ref"]?.ToString() ?? item["item_id"]?.ToString() ?? string.Empty).Trim();
            if (itemRef.Length == 0 || itemRef.Equals("null", StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new CustomWorldShopInventoryEntry(
                itemRef,
                ReadNodeInt(item["max_qty"], 0),
                ReadNodeInt(item["normal_qty"], 0),
                ReadNodeInt(item["regen_qty"], 0),
                ReadNodeInt(item["regen_time"], 0),
                ReadNodeInt(item["regen_percent"], 0)));
        }
        return result;
    }

    public void SaveShopInventory(string shopId, IEnumerable<CustomWorldShopInventoryEntry> entries)
    {
        shopId = NormalizeRequired(shopId, "Shop ID");
        var existsInBase = GetFamily("shops").Records.Any(record =>
            ReadRecordId(record).Equals(shopId, StringComparison.OrdinalIgnoreCase));
        var existingOverride = FindOverride("shops", shopId);
        var fields = existingOverride?["fields"] is JsonObject prior
            ? (JsonObject)prior.DeepClone()
            : new JsonObject();
        var inventory = new JsonArray();
        foreach (var entry in entries)
        {
            var itemRef = (entry.ItemRef ?? string.Empty).Trim();
            if (itemRef.Length == 0) continue;
            inventory.Add(new JsonObject
            {
                ["item_ref"] = itemRef,
                ["item_exists"] = true,
                ["max_qty"] = Math.Max(0, entry.MaxQty),
                ["normal_qty"] = Math.Max(0, entry.NormalQty),
                ["regen_qty"] = Math.Max(0, entry.RegenQty),
                ["regen_time"] = Math.Max(0, entry.RegenTime),
                ["regen_percent"] = Math.Clamp(entry.RegenPercent, 0, 100)
            });
        }
        fields["inventory"] = inventory;
        if (!existsInBase)
        {
            fields["id"] = shopId;
            fields["object_type"] = "shop";
            fields["world_scope"] = new JsonArray(JsonValue.Create("custom"));
            fields["confidence"] = "host_authored_custom";
        }
        SaveOverride("shops", shopId, existsInBase ? "patch" : "upsert", fields.ToJsonString(JsonOptions));
    }

    private static (int MapNumber, int RoomNumber) ReadRoomCoordinates(JsonObject room, string roomId)
    {
        var map = ReadNodeInt(ReadPath(room, "map_number"), 0);
        var number = ReadNodeInt(ReadPath(room, "room_number"), 0);
        if (number <= 0) number = ReadNodeInt(ReadPath(room, "legacy_id"), 0);
        if (roomId.StartsWith("room.map", StringComparison.OrdinalIgnoreCase))
        {
            var tail = roomId["room.map".Length..];
            var parts = tail.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (map <= 0 && int.TryParse(parts[0], out var parsedMap)) map = parsedMap;
                if (number <= 0 && int.TryParse(parts[1], out var parsedRoom)) number = parsedRoom;
            }
        }
        return (map, number);
    }

    private static int ReadNodeInt(JsonNode? node, int fallback)
    {
        if (node is null) return fallback;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number)) return number;
            if (value.TryGetValue<long>(out var longNumber) && longNumber is >= int.MinValue and <= int.MaxValue) return (int)longNumber;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out number)) return number;
        }
        return int.TryParse(node.ToString(), out var parsed) ? parsed : fallback;
    }

    public CustomWorldBuildResult Build()
    {
        EnsureCustomWorldConfigured();
        var result = new LibrariumDataWriter().Compile(new LibrariumCompilerOptions(
            _projectRoot,
            CustomWorldId,
            Clean: true));

        var diagnostics = result.Diagnostics
            .Select(diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}")
            .ToArray();
        if (!result.Success)
        {
            throw new InvalidOperationException(
                "Custom1 build failed. " + string.Join(" | ", diagnostics.Where(line => line.StartsWith("Error", StringComparison.OrdinalIgnoreCase)).Take(5)));
        }

        if (!IsGeneratedDataReady())
            throw new IOException($"Custom1 compiler completed but required generated files were not found in {GeneratedDataPath}.");

        return new CustomWorldBuildResult(
            result.WorldId,
            result.OutputRoot,
            result.ObjectFamiliesCompiled,
            result.ObjectsCompiled,
            result.FilesWritten,
            diagnostics);
    }

    private void EnsureCustomWorldConfigured()
    {
        var root = ReadConfigRoot();
        var worlds = root["worlds"] as JsonArray ?? new JsonArray();
        root["worlds"] = worlds;
        if (worlds.OfType<JsonObject>().Any(IsCustomWorld)) return;

        worlds.Add(CreateDefaultWorld());
        WriteConfigRoot(root);
    }

    private CustomWorldEditorSettings ReadSettings()
    {
        var root = ReadConfigRoot();
        var world = (root["worlds"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(IsCustomWorld)
                    ?? throw new InvalidOperationException("Custom1 is not configured.");
        return new CustomWorldEditorSettings(
            CustomWorldId,
            ReadString(world, "name", "Custom World 1"),
            ReadString(world, "description", "Editable Classic-derived custom world."),
            ReadString(world, "loginBanner", "Custom World 1"),
            ReadString(world, "motd", "Welcome to Custom World 1."),
            ReadBool(world, "runtimeEnabled", true),
            ReadBool(world, "transfersAllowed", false),
            $"Data/{CustomWorldId}");
    }

    private IReadOnlyList<CustomWorldOverrideSummary> ReadOverrides()
    {
        lock (_overrideGate)
        {
            var root = ReadOverrideRoot(createIfMissing: true);

            if (_overrideSummaryCache is not null)
                return _overrideSummaryCache;

            _overrideSummaryCache =
                (root["overrides"] as JsonArray)?
                    .OfType<JsonObject>()
                    .Select(ToSummary)
                    .OrderBy(entry => entry.Family, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.TargetId, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                ?? [];

            return _overrideSummaryCache;
        }
    }

    private JsonObject ReadConfigRoot()
    {
        if (!File.Exists(ConfigPath)) throw new FileNotFoundException("Server world configuration was not found.", ConfigPath);
        try
        {
            return JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject
                   ?? throw new JsonException("Configuration root must be a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Config/server.worlds.json is invalid: " + ex.Message, ex);
        }
    }

    private void WriteConfigRoot(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, root.ToJsonString(JsonOptions) + Environment.NewLine);
    }

    private JsonObject ReadOverrideRoot(bool createIfMissing)
    {
        lock (_overrideGate)
        {
            if (!File.Exists(OverridePath))
            {
                var empty = CreateOverrideRoot();

                if (createIfMissing)
                    WriteOverrideRootCore(empty);

                return empty;
            }

            var info = new FileInfo(OverridePath);
            var writeUtc = info.LastWriteTimeUtc;
            var length = info.Length;

            if (_overrideRootCache is not null &&
                _overrideRootCacheWriteUtc == writeUtc &&
                _overrideRootCacheLength == length)
            {
                return _overrideRootCache;
            }

            try
            {
                var parsed = JsonNode.Parse(File.ReadAllText(OverridePath)) as JsonObject
                             ?? throw new JsonException("Override root must be a JSON object.");

                _overrideSummaryCache = null;
                _overrideRootCache = parsed;
                _overrideRootCacheWriteUtc = writeUtc;
                _overrideRootCacheLength = length;

                return parsed;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "Custom1 override file is invalid: " + ex.Message,
                    ex);
            }
        }
    }

    private void WriteOverrideRoot(JsonObject root)
    {
        lock (_overrideGate)
        {
            WriteOverrideRootCore(root);
        }
    }

    private void WriteOverrideRootCore(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OverridePath)!);

        File.WriteAllText(
            OverridePath,
            root.ToJsonString(JsonOptions) + Environment.NewLine);

        var info = new FileInfo(OverridePath);

        _overrideSummaryCache = null;
        _overrideRootCache = root;
        _overrideRootCacheWriteUtc = info.LastWriteTimeUtc;
        _overrideRootCacheLength = info.Length;
    }

    private void EnsureCatalogLoaded()
    {
        if (_catalogCache is not null)
            return;

        lock (_catalogGate)
        {
            if (_catalogCache is not null)
                return;

            var logPath = Path.Combine(
                AppContext.BaseDirectory,
                "creator-studio-performance.log");

            var stopwatch = Stopwatch.StartNew();

            File.AppendAllText(
                logPath,
                $"{DateTimeOffset.Now:O} | CATALOG LOAD START | Root={_projectRoot}{Environment.NewLine}");

            var diagnostics = new List<CompilerDiagnostic>();

            _catalogCache = new LibrariumCatalogReader()
                .ReadFamilies(
                    Path.Combine(_projectRoot, "Librarium"),
                    diagnostics)
                .ToDictionary(
                    item => item.Name,
                    StringComparer.OrdinalIgnoreCase);

            stopwatch.Stop();

            File.AppendAllText(
                logPath,
                $"{DateTimeOffset.Now:O} | CATALOG LOAD DONE | Families={_catalogCache.Count} | Diagnostics={diagnostics.Count} | Time={stopwatch.Elapsed}{Environment.NewLine}");
        }
    }

    private LibrariumFamily GetFamily(string family)
    {
        EnsureCatalogLoaded();
        return _catalogCache!.TryGetValue(family, out var result)
            ? result
            : throw new KeyNotFoundException($"Librarium family '{family}' was not found.");
    }

    private static string ReadRecordId(JsonObject record)
    {
        foreach (var key in new[] { "id", "mechanic_id", "object_id" })
        {
            if (record[key] is null) continue;
            try
            {
                var value = record[key]!.GetValue<string>()?.Trim() ?? string.Empty;
                if (value.Length > 0) return value;
            }
            catch
            {
                var value = record[key]!.ToJsonString().Trim('"');
                if (value.Length > 0) return value;
            }
        }
        return string.Empty;
    }

    private static string ReadRecordName(JsonObject record)
    {
        foreach (var key in new[] { "name", "title", "display_name", "race_name", "class_name" })
        {
            if (record[key] is null) continue;
            try
            {
                var value = record[key]!.GetValue<string>()?.Trim() ?? string.Empty;
                if (value.Length > 0) return value;
            }
            catch { }
        }
        if (record["message_id"] is JsonNode messageId)
        {
            var id = NodeToEditorText(messageId, CustomWorldSimpleFieldKind.Integer);
            var preview = PreviewRecordText(record["player_payload"]);
            return preview.Length == 0 ? $"Message {id}" : $"Message {id}: {preview}";
        }
        if (record["textblock_id"] is JsonNode textblockId)
        {
            var id = NodeToEditorText(textblockId, CustomWorldSimpleFieldKind.Integer);
            var preview = PreviewRecordText(record["payload"]);
            return preview.Length == 0 ? $"Textblock {id}" : $"Textblock {id}: {preview}";
        }
        if (record["interaction_kind"] is JsonNode interactionKind)
        {
            var kind = PreviewRecordText(interactionKind);
            if (kind.Length > 0) return "Action: " + kind;
        }
        return string.Empty;
    }

    private static string PreviewRecordText(JsonNode? node)
    {
        if (node is null) return string.Empty;
        string text;
        try { text = node.GetValue<string>() ?? string.Empty; }
        catch { text = node.ToJsonString().Trim('"'); }
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ", StringComparison.Ordinal);
        return text.Length <= 72 ? text : text[..69] + "...";
    }

    private static string GenerateSimpleId(CustomWorldSimpleFormDefinition definition, IReadOnlyDictionary<string, string> values)
    {
        if (definition.Family.Equals("rooms", StringComparison.OrdinalIgnoreCase) &&
            values.TryGetValue("map_number", out var map) && values.TryGetValue("room_number", out var room))
            return $"room.map{map.Trim()}.{room.Trim()}";
        if (definition.Family.Equals("exits", StringComparison.OrdinalIgnoreCase) &&
            values.TryGetValue("from_map_number", out var exitMap) && values.TryGetValue("from_room_number", out var exitRoom) &&
            values.TryGetValue("direction", out var direction))
            return $"exit.map{exitMap.Trim()}.{exitRoom.Trim()}.{direction.Trim().ToLowerInvariant()}";
        if (definition.Family.Equals("spawns", StringComparison.OrdinalIgnoreCase) &&
            values.TryGetValue("map_id", out var spawnMap) && values.TryGetValue("room_number", out var spawnRoom))
            return $"spawn.custom.map{spawnMap.Trim()}.room{spawnRoom.Trim()}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        if (definition.Family.Equals("native_message_payloads", StringComparison.OrdinalIgnoreCase) &&
            values.TryGetValue("message_id", out var messageId) && !string.IsNullOrWhiteSpace(messageId))
            return $"native_message_payload.{messageId.Trim()}";
        if (definition.Family.Equals("native_textblock_payloads", StringComparison.OrdinalIgnoreCase) &&
            values.TryGetValue("textblock_id", out var textblockId) && !string.IsNullOrWhiteSpace(textblockId))
            return $"native_textblock_payload.{textblockId.Trim()}";

        var name = values.TryGetValue("name", out var named) ? named : definition.SingularName;
        var slug = new string((name ?? string.Empty).Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '.')
            .ToArray());
        while (slug.Contains("..", StringComparison.Ordinal)) slug = slug.Replace("..", ".", StringComparison.Ordinal);
        slug = slug.Trim('.');
        if (slug.Length == 0) slug = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        return $"{definition.IdPrefix}.{slug}";
    }

    private static string ObjectTypeForFamily(string family) => family.ToLowerInvariant() switch
    {
        "npcs" => "npc",
        "items" => "item",
        "spells" => "spell",
        "abilities" => "ability",
        "native_message_payloads" => "native_message_payload",
        "native_textblock_payloads" => "native_textblock_payload",
        "room_interactions" => "room_interaction",
        "classes" => "class",
        "races" => "race",
        "shops" => "shop",
        "quests" => "quest",
        "triggers" => "trigger",
        "scripts" => "script",
        "skills" => "skill",
        "formulas" => "formula",
        "commands" => "command",
        "rooms" => "room",
        "exits" => "exit",
        "spawns" => "spawn",
        _ => family.TrimEnd('s')
    };

    private static void ApplyDerivedSimpleFields(
        string family,
        string targetId,
        JsonObject fields,
        IReadOnlyDictionary<string, string> values,
        bool createNew,
        IReadOnlySet<string>? changedPaths)
    {
        bool Changed(params string[] paths) => createNew || changedPaths is null || paths.Any(changedPaths.Contains);

        if (family.Equals("rooms", StringComparison.OrdinalIgnoreCase))
        {
            if (!createNew) return;
            if (fields["relationships"] is not JsonObject relationships)
            {
                relationships = new JsonObject();
                fields["relationships"] = relationships;
            }
            if (relationships["exits"] is null) relationships["exits"] = new JsonArray();
        }
        else if (family.Equals("exits", StringComparison.OrdinalIgnoreCase))
        {
            if (!Changed("from_map_number", "from_room_number", "to_map_number", "to_room_number", "direction")) return;
            var fromMap = ReadEditorInt(values, "from_map_number");
            var fromRoom = ReadEditorInt(values, "from_room_number");
            var toMap = ReadEditorInt(values, "to_map_number");
            var toRoom = ReadEditorInt(values, "to_room_number");
            SetPath(fields, "relationships.from_room", JsonValue.Create($"room.map{fromMap}.{fromRoom}"));
            SetPath(fields, "relationships.to_room", JsonValue.Create($"room.map{toMap}.{toRoom}"));
            fields["runtime_disposition"] = "active_runtime";
        }
        else if (family.Equals("spawns", StringComparison.OrdinalIgnoreCase))
        {
            if (!Changed("map_id", "room_number")) return;
            var map = ReadEditorInt(values, "map_id");
            var room = ReadEditorInt(values, "room_number");
            fields["room_ref"] = $"room.{room}";
            fields["qualified_room_ref"] = $"room.map{map}.{room}";
            fields["room_key"] = $"map{map}-room{room}";
            SetPath(fields, "spawn_model.map_id", JsonValue.Create(map));
            SetPath(fields, "spawn_model.interaction_model", JsonValue.Create("combat_spawn"));
        }
        else if (family.Equals("items", StringComparison.OrdinalIgnoreCase))
        {
            if (Changed("classification.type"))
            {
                var itemType = ReadEditorText(values, "classification.type", "misc");
                SetPath(fields, "classification.is_armor", JsonValue.Create(itemType.Equals("armor", StringComparison.OrdinalIgnoreCase)));
                SetPath(fields, "combat.is_armor", JsonValue.Create(itemType.Equals("armor", StringComparison.OrdinalIgnoreCase)));
            }
            if (Changed("public_reference_0139.fields.min_hit"))
                SetPath(fields, "combat.minimum_damage", JsonValue.Create(ReadEditorInt(values, "public_reference_0139.fields.min_hit")));
            if (Changed("public_reference_0139.fields.max_hit"))
                SetPath(fields, "combat.maximum_damage", JsonValue.Create(ReadEditorInt(values, "public_reference_0139.fields.max_hit")));
            if (Changed("public_reference_0139.fields.cost"))
                SetPath(fields, "economy.cost", JsonValue.Create(ReadEditorInt(values, "public_reference_0139.fields.cost")));
        }
        else if (family.Equals("shops", StringComparison.OrdinalIgnoreCase))
        {
            if (!Changed("@inventory_items")) return;
            var lines = values.TryGetValue("@inventory_items", out var inventory) ? SplitLines(inventory) : [];
            var items = new JsonArray();
            foreach (var itemId in lines)
            {
                items.Add(new JsonObject
                {
                    ["item_ref"] = itemId,
                    ["item_exists"] = true,
                    ["max_qty"] = 5,
                    ["normal_qty"] = 5,
                    ["regen_qty"] = 1,
                    ["regen_time"] = 48,
                    ["regen_percent"] = 25
                });
            }
            fields["inventory"] = items;
        }
        else if (family.Equals("triggers", StringComparison.OrdinalIgnoreCase))
        {
            fields["runtime_disposition"] = "active_runtime_custom";
            fields["confidence"] = "host_authored_custom";
        }
        else if (family.Equals("scripts", StringComparison.OrdinalIgnoreCase))
        {
            fields["confidence"] = "host_authored_custom";
            fields["projection_pass"] = "custom1_simple_editor";
        }
        else if (family is "quests" or "formulas")
        {
            SetPath(fields, "runtime_mapping.status", JsonValue.Create("runtime_authoritative_custom"));
        }
    }


    private static void RemoveDuplicatedReferenceFields(
        string family,
        IDictionary<string, CustomWorldSimpleFieldDefinition> fields)
    {
        string[] duplicates = family.ToLowerInvariant() switch
        {
            "items" =>
            [
                "public_reference_0139.fields.legacy_number", "public_reference_0139.fields.name",
                "public_reference_0139.fields.item_type", "public_reference_0139.fields.min_hit",
                "public_reference_0139.fields.max_hit", "public_reference_0139.fields.ac_bonus",
                "public_reference_0139.fields.dr_bonus", "public_reference_0139.fields.accuracy",
                "public_reference_0139.fields.speed", "public_reference_0139.fields.weight",
                "public_reference_0139.fields.cost", "public_reference_0139.fields.stat_requirements.str",
                "public_reference_0139.fields.properties.worn_on", "public_reference_0139.fields.properties.gettable",
                "public_reference_0139.fields.properties.is_armor", "public_reference_0139.fields.properties.armor_type",
                "public_reference_0139.fields.properties.weapon_type"
            ],
            "spells" =>
            [
                "public_reference_0139.fields.legacy_number", "public_reference_0139.fields.name",
                "public_reference_0139.fields.short_name", "public_reference_0139.fields.spell_level",
                "public_reference_0139.fields.spell_type", "public_reference_0139.fields.target_type",
                "public_reference_0139.fields.attack_type", "public_reference_0139.fields.mana_cost",
                "public_reference_0139.fields.energy_cost", "public_reference_0139.fields.min_effect",
                "public_reference_0139.fields.max_effect", "public_reference_0139.fields.duration"
            ],
            "shops" =>
            [
                "public_reference_0139.fields.legacy_number", "public_reference_0139.fields.name",
                "public_reference_0139.fields.shop_type", "public_reference_0139.fields.min_level",
                "public_reference_0139.fields.max_level", "public_reference_0139.fields.class_limit",
                "public_reference_0139.fields.markup_percent"
            ],
            "npcs" =>
            [
                "public_reference_0139.fields.legacy_number", "public_reference_0139.fields.name",
                "public_reference_0139.fields.level", "public_reference_0139.fields.alignment",
                "public_reference_0139.fields.group_id", "public_reference_0139.fields.hit_points",
                "public_reference_0139.fields.energy", "public_reference_0139.fields.armor_class",
                "public_reference_0139.fields.damage_resistance", "public_reference_0139.fields.magic_resistance",
                "public_reference_0139.fields.base_experience", "public_reference_0139.fields.undead",
                "public_reference_0139.fields.follow", "public_reference_0139.fields.charm_level",
                "public_reference_0139.fields.charm_resistance", "public_reference_0139.fields.regen_time",
                "public_reference_0139.fields.respawn_hours", "public_reference_0139.fields.currency_drops.gold",
                "public_reference_0139.fields.currency_drops.runic", "public_reference_0139.fields.currency_drops.copper",
                "public_reference_0139.fields.currency_drops.silver", "public_reference_0139.fields.currency_drops.platinum",
                "public_reference_0139.fields.attacks", "public_reference_0139.fields.abilities_named"
            ],
            _ => []
        };

        foreach (var path in duplicates) fields.Remove(path);
    }

    private static readonly HashSet<string> ProtectedEditorRootFields = new(StringComparer.OrdinalIgnoreCase)
    {
        // Runtime identity / provenance is intentionally not creator-editable from the normal editor.
        // This keeps the creator surface focused on game content instead of Librarium bookkeeping.
        "id", "object_type", "world_scope", "confidence", "runtime_mapping",
        "evidence", "evidence_refs", "evidenceState", "source", "sourceNumbers", "printableSegments", "notes",
        "runtimeMapping", "librarium_enrichment_0124", "generator_mapping_0124",
        "validation_rules_0124", "relationship_summary_0124", "evidence_summary_0124",
        "override_policy_0124", "version_history_0124", "combat_profile_authority",
        "attack_profile_authority", "mmexplorer_sql_reference_0139", "classic_bug_fixes_0135",
        "native_message_8480_argument_authority",
        // Physical native-file layout is evidence/provenance, not creator content.
        "header_offset", "slot_size", "slot_offsets", "physical_record_size",
        "physical_occurrence_count", "physical_header_offsets", "record_header",
        "page_offset", "page_index", "record_marker", "payload_offset", "decode",
        "player_payload_is_empty", "classic_lock"
    };

    private static bool ShouldSkipEditorRoot(string key)
    {
        if (ProtectedEditorRootFields.Contains(key)) return true;
        return key.EndsWith("_authority", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("evidence_", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_evidence", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("generator_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("validation_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("override_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("version_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("mmexplorer_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("librarium_", StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<string, JsonObject> BuildEffectiveRecordIndex(string family)
    {
        var byId = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in GetFamily(family).Records)
        {
            var id = ReadRecordId(record);
            if (id.Length > 0) byId[id] = (JsonObject)record.DeepClone();
        }

        var root = ReadOverrideRoot(createIfMissing: true);
        if (root["overrides"] is not JsonArray overrides) return byId;
        foreach (var entry in overrides.OfType<JsonObject>())
        {
            if (!string.Equals(entry["family"]?.GetValue<string>(), family, StringComparison.OrdinalIgnoreCase)) continue;
            var id = entry["target_id"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (id.Length == 0) continue;
            var operation = entry["operation"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? "patch";
            if (operation == "delete")
            {
                byId.Remove(id);
                continue;
            }

            if (!byId.TryGetValue(id, out var effective))
            {
                effective = new JsonObject();
                byId[id] = effective;
            }
            if (entry["fields"] is JsonObject fields) MergeObject(effective, fields);
        }

        // VERIFIED ROOM / NPC SOURCE CORRECTIONS V3
        //
        // Source evidence proves npc_textblock.2687.2640 is a malformed
        // derived projection. 2687 is not an NPC/Monster identity.
        // Keep this disproven projection out of the effective npc_dialogue family.
        if (family.Equals("npc_dialogue", StringComparison.OrdinalIgnoreCase))
        {
            byId.Remove("npc_textblock.2687.2640");
        }

        return byId;
    }

    private IReadOnlyDictionary<string, JsonObject> BuildEffectiveFamilyRecords(string family)
        => BuildEffectiveRecordIndex(family);

    private static HashSet<string> BuildReferenceTokens(string family, string id, JsonObject record)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddNode(JsonNode? node)
        {
            if (node is null) return;
            var text = ScalarText(node);
            if (!string.IsNullOrWhiteSpace(text)) tokens.Add(NormalizeReferenceToken(text));
        }

        tokens.Add(NormalizeReferenceToken(id));
        foreach (var key in new[] { "id", "legacy_id", "legacy_number", "message_id", "textblock_id", "qualified_room_ref", "exit_id" })
            AddNode(record[key]);
        return tokens;
    }

    private static HashSet<string> BuildFamilyReferenceTokenSet(string family, IEnumerable<JsonObject> records)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            var id = ReadRecordId(record);
            if (!string.IsNullOrWhiteSpace(id)) tokens.Add(NormalizeReferenceToken(id));
            foreach (var key in new[] { "legacy_id", "legacy_number", "message_id", "textblock_id", "qualified_room_ref", "exit_id" })
            {
                if (record[key] is not JsonNode node) continue;
                var text = ScalarText(node);
                if (!string.IsNullOrWhiteSpace(text)) tokens.Add(NormalizeReferenceToken(text));
            }

            // Textblock references occur in both numeric and qualified forms.
            if (family.Equals("native_textblock_payloads", StringComparison.OrdinalIgnoreCase) &&
                record["textblock_id"] is JsonNode textblockIdNode)
            {
                var textblockId = NormalizeReferenceToken(ScalarText(textblockIdNode));
                if (!string.IsNullOrWhiteSpace(textblockId))
                {
                    tokens.Add(textblockId);
                    tokens.Add("textblock." + textblockId);
                    tokens.Add("native_textblock_payload." + textblockId);
                }
            }
        }
        return tokens;
    }

    private static void FindReferenceMatches(
        JsonNode? node,
        string path,
        string targetFamily,
        IReadOnlySet<string> targetTokens,
        Action<string, string> onMatch)
    {
        if (node is null) return;
        if (node is JsonObject obj)
        {
            var structuredTargetKey = obj["targetId"] is not null ? "targetId" : obj["target_id"] is not null ? "target_id" : null;
            var handledStructuredTarget = false;
            if (structuredTargetKey is not null && obj[structuredTargetKey] is JsonNode structuredTarget)
            {
                var kindNode = obj["kind"] ?? obj["relationship"] ?? obj["type"];
                var structuredFamily = kindNode is null ? string.Empty : ReferenceFamilyFromKind(ScalarText(kindNode));
                var structuredValue = ScalarText(structuredTarget);
                if (structuredFamily.Equals(targetFamily, StringComparison.OrdinalIgnoreCase) &&
                    targetTokens.Contains(NormalizeReferenceToken(structuredValue)))
                {
                    onMatch(path.Length == 0 ? structuredTargetKey : path + "." + structuredTargetKey, structuredValue);
                    handledStructuredTarget = true;
                }
            }

            foreach (var pair in obj)
            {
                if (handledStructuredTarget && pair.Key.Equals(structuredTargetKey, StringComparison.OrdinalIgnoreCase)) continue;
                if (path.Length == 0 && pair.Key.Equals("public_reference_0139", StringComparison.OrdinalIgnoreCase) && pair.Value is JsonObject publicReference)
                {
                    if (publicReference["fields"] is JsonNode publicFields)
                        FindReferenceMatches(publicFields, "public_reference_0139.fields", targetFamily, targetTokens, onMatch);
                    continue;
                }
                if (path.Length == 0 && ShouldSkipReferenceRoot(pair.Key)) continue;
                FindReferenceMatches(pair.Value, path.Length == 0 ? pair.Key : path + "." + pair.Key, targetFamily, targetTokens, onMatch);
            }
            return;
        }
        if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
                FindReferenceMatches(array[i], $"{path}[{i}]", targetFamily, targetTokens, onMatch);
            return;
        }

        var value = ScalarText(node).Trim();
        if (IsEmptyReferenceValue(value)) return;
        var inferredFamily = InferReferenceFamilyFromPath(path);
        var normalized = NormalizeReferenceToken(value);
        if (inferredFamily.Equals(targetFamily, StringComparison.OrdinalIgnoreCase) && targetTokens.Contains(normalized))
        {
            onMatch(path, value);
            return;
        }

        // Canonical ids are self-describing and safe to match even when an older field has a generic name.
        if (inferredFamily.Length == 0 && value.Contains(".", StringComparison.Ordinal) && targetTokens.Contains(normalized))
            onMatch(path, value);
    }

    private static void ValidateReferenceLeaves(
        JsonNode? node,
        string path,
        Action<string, string, string> validate)
    {
        if (node is null) return;
        if (node is JsonObject obj)
        {
            var structuredTargetKey = obj["targetId"] is not null ? "targetId" : obj["target_id"] is not null ? "target_id" : null;
            var handledStructuredTarget = false;
            if (structuredTargetKey is not null && obj[structuredTargetKey] is JsonNode structuredTarget)
            {
                var kindNode = obj["kind"] ?? obj["relationship"] ?? obj["type"];
                var structuredFamily = kindNode is null ? string.Empty : ReferenceFamilyFromKind(ScalarText(kindNode));
                if (structuredFamily.Length > 0)
                {
                    validate(path.Length == 0 ? structuredTargetKey : path + "." + structuredTargetKey, ScalarText(structuredTarget), structuredFamily);
                    handledStructuredTarget = true;
                }
            }

            foreach (var pair in obj)
            {
                if (handledStructuredTarget && pair.Key.Equals(structuredTargetKey, StringComparison.OrdinalIgnoreCase)) continue;
                if (path.Length == 0 && pair.Key.Equals("public_reference_0139", StringComparison.OrdinalIgnoreCase) && pair.Value is JsonObject publicReference)
                {
                    if (publicReference["fields"] is JsonNode publicFields)
                        ValidateReferenceLeaves(publicFields, "public_reference_0139.fields", validate);
                    continue;
                }
                if (ShouldSkipReferenceMetadata(pair.Key) || (path.Length == 0 && ShouldSkipReferenceRoot(pair.Key))) continue;
                ValidateReferenceLeaves(pair.Value, path.Length == 0 ? pair.Key : path + "." + pair.Key, validate);
            }
            return;
        }
        if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
                ValidateReferenceLeaves(array[i], $"{path}[{i}]", validate);
            return;
        }

        // commands[n] contains literal player-entered command text, not a Commands-family reference.
        if (System.Text.RegularExpressions.Regex.IsMatch(
            path,
            @"(?:^|\.)commands\[\d+\]$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return;
        }

        // relationships.rooms_seen_in[n] stores recovered WCC observation/provenance keys
        // such as wcc_map1_room349, not Creator Rooms-family foreign keys.
        if (System.Text.RegularExpressions.Regex.IsMatch(
                path,
                @"^relationships\.rooms_seen_in\[\d+\]$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
            ScalarText(node).Trim().StartsWith("wcc_map", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // public_reference_0139.fields.exits[n] may contain a direction snapshot
        // rather than an Exits-family object id. Skip only known direction literals.
        if (System.Text.RegularExpressions.Regex.IsMatch(
            path,
            @"^public_reference_0139\.fields\.exits\[\d+\]$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var direction = ScalarText(node).Trim().ToLowerInvariant();

            if (direction is
                "north" or "south" or "east" or "west" or
                "northeast" or "northwest" or "southeast" or "southwest" or
                "up" or "down" or
                "n" or "s" or "e" or "w" or
                "ne" or "nw" or "se" or "sw" or
                "u" or "d")
            {
                return;
            }
        }

                // Native/environment scalar identifiers are not Creator object references.
        if (path.Equals("room_ref", StringComparison.OrdinalIgnoreCase) ||
    path.EndsWith("spawn_model.map_id", StringComparison.OrdinalIgnoreCase) ||
    path.EndsWith("spawn_model.linked_spawn_map_id", StringComparison.OrdinalIgnoreCase))
{
    return;
}

// Native/environment scalar identifiers are not Creator object references.
if (path.Equals("environment.room_spell_id", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("environment.room_lair_id", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".shared_native_state_id", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".message_policy_ref", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Map 1 / Room 164 is the Classic Sysop support room and is intentionally
        // not represented as a normal navigable room record.
        // Native support-invasion/sysop metadata legitimately points at that same room.
        var room164ReferencePath =
            path.Equals("relationships.to_room", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("relationships.from_room", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("command_semantics.return_room_id", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("support_room_id", StringComparison.OrdinalIgnoreCase);

        if (room164ReferencePath &&
            ScalarText(node).Trim().Equals("room.map1.164", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        // formula_refs are recovered/runtime mechanic identifiers, not Creator-family foreign keys.
        if (path.StartsWith("formula_refs[", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Shop inventory quantity fields are scalar stock values, not item references.
        if (System.Text.RegularExpressions.Regex.IsMatch(
            path,
            @"(?:^|\.)inventory\[\d+\]\.(?:max_qty|regen_qty|normal_qty)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return;
        }

var referenceFamily = InferReferenceFamilyFromPath(path);
        if (referenceFamily.Length == 0) return;
        validate(path, ScalarText(node), referenceFamily);
    }

    private static string InferReferenceFamilyFromPath(string path)
    {
        var normalized = (path ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0) return string.Empty;
        var leaf = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        var bracket = leaf.IndexOf('[');
        if (bracket >= 0) leaf = leaf[..bracket];

        // Identity fields on their own record are not relationships.
        if (!normalized.Contains(".", StringComparison.Ordinal) && !normalized.Contains("[", StringComparison.Ordinal) &&
            (leaf is "id" or "legacy_id" or "legacy_number" or "message_id" or "textblock_id" or "map_number" or "room_number"))
            return string.Empty;

        var looksLikeReference = leaf.EndsWith("_id", StringComparison.Ordinal) || leaf.EndsWith("_ids", StringComparison.Ordinal) ||
            leaf.EndsWith("_ref", StringComparison.Ordinal) || leaf.EndsWith("_refs", StringComparison.Ordinal) ||
            leaf.EndsWith("_legacy_id", StringComparison.Ordinal) || leaf.EndsWith("_legacy_ids", StringComparison.Ordinal) ||
            (leaf is "spawns" or "services" or "exits" or "abilities" or "formulas" or "scripts" or "triggers" or "commands") ||
            normalized.StartsWith("relationships.", StringComparison.Ordinal) ||
            normalized.Contains("required_item", StringComparison.Ordinal) || normalized.Contains("reward_item", StringComparison.Ordinal) ||
            normalized.Contains("hit_spell", StringComparison.Ordinal) || normalized.Contains("cast_spell", StringComparison.Ordinal) ||
            normalized.Contains("return_room", StringComparison.Ordinal) || normalized.Contains("start_room", StringComparison.Ordinal);
        if (!looksLikeReference) return string.Empty;

        // Operational Textblock IDs recovered from native command/script semantics
        // reference native payload records rather than logical decoded Textblock objects.
        // Decoded operation target_textblock fields refer to native Textblock payload IDs.
        // Keep this path-specific so ordinary logical Textblock relationships still use textblocks.
        if (System.Text.RegularExpressions.Regex.IsMatch(
            path ?? string.Empty,
            @"(?:^|\.)operations\[\d+\]\.target_textblock_(?:id|ref)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return "native_textblock_payloads";
        }

        if (normalized.Contains("native_textblock", StringComparison.Ordinal) ||
            normalized.Contains("random_textblock", StringComparison.Ordinal) ||
            normalized.Contains("failure_textblock", StringComparison.Ordinal))
        {
            return "native_textblock_payloads";
        }

        if (normalized.Contains("native_textblock_payload", StringComparison.Ordinal)) return "native_textblock_payloads";
        if (normalized.Contains("textblock", StringComparison.Ordinal)) return "textblocks";
        if (normalized.Contains("message", StringComparison.Ordinal)) return "native_message_payloads";
        if (normalized.Contains("ability", StringComparison.Ordinal)) return "abilities";
        if (normalized.Contains("spell", StringComparison.Ordinal)) return "spells";
        if (normalized.Contains("item", StringComparison.Ordinal) || normalized.Contains("inventory", StringComparison.Ordinal)) return "items";
        if (normalized.Contains("npc", StringComparison.Ordinal) || normalized.Contains("monster", StringComparison.Ordinal)) return "npcs";
        if (normalized.Contains("room", StringComparison.Ordinal)) return "rooms";
        if (normalized.Contains("shop", StringComparison.Ordinal) || normalized.Contains("service", StringComparison.Ordinal)) return "shops";
        if (normalized.Contains("class", StringComparison.Ordinal)) return "classes";
        if (normalized.Contains("race", StringComparison.Ordinal)) return "races";
        if (normalized.Contains("quest", StringComparison.Ordinal)) return "quests";
        if (normalized.Contains("trigger", StringComparison.Ordinal)) return "triggers";
        if (normalized.Contains("script", StringComparison.Ordinal)) return "scripts";
        if (normalized.Contains("skill", StringComparison.Ordinal)) return "skills";
        if (normalized.Contains("formula", StringComparison.Ordinal)) return "formulas";
        if (normalized.Contains("command", StringComparison.Ordinal)) return "commands";
        if (normalized.Contains("spawn", StringComparison.Ordinal)) return "spawns";
        if (normalized.Contains("exit", StringComparison.Ordinal)) return "exits";
        if (normalized.Contains("interaction", StringComparison.Ordinal) || normalized.Contains("action", StringComparison.Ordinal)) return "room_interactions";
        return string.Empty;
    }

    private static string ReferenceFamilyFromKind(string kind)
    {
        var normalized = (kind ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        if (normalized.Contains("native_textblock_payload", StringComparison.Ordinal)) return "native_textblock_payloads";
        if (normalized.Contains("textblock", StringComparison.Ordinal)) return "textblocks";
        if (normalized.Contains("message", StringComparison.Ordinal)) return "native_message_payloads";
        if (normalized.Contains("ability", StringComparison.Ordinal)) return "abilities";
        if (normalized.Contains("spell", StringComparison.Ordinal)) return "spells";
        if (normalized.Contains("item", StringComparison.Ordinal)) return "items";
        if (normalized.Contains("npc", StringComparison.Ordinal) || normalized.Contains("monster", StringComparison.Ordinal)) return "npcs";
        if (normalized.Contains("room", StringComparison.Ordinal)) return "rooms";
        if (normalized.Contains("shop", StringComparison.Ordinal) || normalized.Contains("service", StringComparison.Ordinal)) return "shops";
        if (normalized.Contains("class", StringComparison.Ordinal)) return "classes";
        if (normalized.Contains("race", StringComparison.Ordinal)) return "races";
        if (normalized.Contains("quest", StringComparison.Ordinal)) return "quests";
        if (normalized.Contains("trigger", StringComparison.Ordinal)) return "triggers";
        if (normalized.Contains("script", StringComparison.Ordinal)) return "scripts";
        if (normalized.Contains("skill", StringComparison.Ordinal)) return "skills";
        if (normalized.Contains("formula", StringComparison.Ordinal)) return "formulas";
        if (normalized.Contains("command", StringComparison.Ordinal)) return "commands";
        if (normalized.Contains("spawn", StringComparison.Ordinal)) return "spawns";
        if (normalized.Contains("exit", StringComparison.Ordinal)) return "exits";
        if (normalized.Contains("interaction", StringComparison.Ordinal) || normalized.Contains("action", StringComparison.Ordinal)) return "room_interactions";
        return string.Empty;
    }

    private static bool ShouldSkipReferenceRoot(string key) => ShouldSkipEditorRoot(key) || key is
        "relationship_summary_0124" or "evidence_summary_0124" or "version_history_0124";

    private static bool ShouldSkipReferenceMetadata(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        return key.Equals("evidence", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("evidence_refs", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("evidenceState", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("evidence_summary_0124", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("relationship_summary_0124", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("version_history_0124", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("evidence_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("librarium_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("generator_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("validation_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("override_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("version_", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("mmexplorer_", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_authority", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeReferenceToken(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        return value.ToLowerInvariant();
    }

    private static string ScalarText(JsonNode? node)
    {
        if (node is null) return string.Empty;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text)) return text ?? string.Empty;
            if (value.TryGetValue<int>(out var integer)) return integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.TryGetValue<long>(out var longInteger)) return longInteger.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.TryGetValue<double>(out var doubleValue)) return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.TryGetValue<bool>(out var boolean)) return boolean ? "true" : "false";
        }
        return node.ToJsonString().Trim('"');
    }

    private static bool IsEmptyReferenceValue(string value)
    {
        var normalized = NormalizeReferenceToken(value);
        return normalized.Length == 0 || normalized is "0" or "-1" or "none" or "null" or "false" or "true";
    }

    private static void ValidateNpcRecord(
        JsonObject record,
        string id,
        string name,
        IReadOnlyDictionary<string, HashSet<string>> tokenSets,
        Action<string, string, string, string, string, string> add)
    {
        var combat = record["combat"] as JsonObject;
        var attacks = record["attacks"] as JsonArray;
        if (combat is null && attacks is null) return;
        // Native attack_count is preserved and is not assumed to equal projected JSON attack-row count.

        var seenSlots = new HashSet<int>();
        if (attacks is null) return;
        var index = 0;
        foreach (var attack in attacks.OfType<JsonObject>())
        {
            var path = $"attacks[{index}]";
            var slot = ReadNodeInt(attack["slot"], index + 1);
            var type = ReadNodeInt(attack["type"], 0);
            var min = ReadNodeInt(attack["minimum_damage"], 0);
            var max = ReadNodeInt(attack["maximum_damage"], 0);
            var threshold = ReadNodeInt(attack["selection_percent"], 0);
            var energy = ReadNodeInt(attack["energy_cost"], 0);
            if (!seenSlots.Add(slot)) add("ERROR", "npcs", id, name, path + ".slot", $"Attack slot {slot} is duplicated.");
            if (threshold is < 0 or > 255) add("ERROR", "npcs", id, name, path + ".selection_percent", "Selection threshold must be within the native byte range 0..255.");
            if (energy < 0) add("ERROR", "npcs", id, name, path + ".energy_cost", "Energy cost cannot be negative.");
            if (type == 2)
            {
                // Recovered native direct-spell minimum_damage is preserved; its complete native range semantics are not proven.
                var spellLegacyId = ReadNodeInt(attack["accuracy"], 0);
                if (spellLegacyId <= 0)
                {
                    add("ERROR", "npcs", id, name, path + ".accuracy", "Direct spell rows require the spell legacy ID in Accuracy.");
                }
                else if (tokenSets.TryGetValue("spells", out var validSpells) &&
                         !validSpells.Contains(NormalizeReferenceToken(spellLegacyId.ToString(System.Globalization.CultureInfo.InvariantCulture))))
                {
                    add("ERROR", "npcs", id, name, path + ".accuracy", $"Direct spell row references missing spell legacy ID {spellLegacyId}.");
                }
            }
            // Recovered native attack fields are preserved without imposing an unproven min/max ordering rule.
            index++;
        }
    }

    private static void ValidateItemRecord(
        JsonObject record,
        string id,
        string name,
        Action<string, string, string, string, string, string> add)
    {
        if (record["combat"] is JsonObject combat)
        {
            var min = ReadNodeInt(combat["minimum_damage"], 0);
            var max = ReadNodeInt(combat["maximum_damage"], min);
            if (min < 0 || max < min)
                add("ERROR", "items", id, name, "combat", $"Weapon damage range {min}-{max} is invalid.");
        }
        if (record["economy"] is JsonObject economy)
        {
            if (ReadNodeInt(economy["weight"], 0) < 0) add("ERROR", "items", id, name, "economy.weight", "Weight cannot be negative.");
            if (ReadNodeInt(economy["cost"], 0) < 0) add("ERROR", "items", id, name, "economy.cost", "Cost cannot be negative.");
        }
    }

    private static void ValidateShopRecord(
        JsonObject record,
        string id,
        string name,
        IReadOnlyDictionary<string, HashSet<string>> tokenSets,
        Action<string, string, string, string, string, string> add)
    {
        if (record["inventory"] is not JsonArray inventory) return;
        var validItems = tokenSets.TryGetValue("items", out var tokens) ? tokens : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in inventory.OfType<JsonObject>())
        {
            var path = $"inventory[{index}]";
            var itemRef = item["item_ref"] is JsonNode refNode ? ScalarText(refNode) : string.Empty;
            if (IsEmptyReferenceValue(itemRef) || !validItems.Contains(NormalizeReferenceToken(itemRef)))
                add("ERROR", "shops", id, name, path + ".item_ref", $"Shop stock references missing item '{itemRef}'.");
            var max = ReadNodeInt(item["max_qty"], 0);
            var normal = ReadNodeInt(item["normal_qty"], 0);
            var regen = ReadNodeInt(item["regen_qty"], 0);
            var regenTime = ReadNodeInt(item["regen_time"], 0);
            var regenPercent = ReadNodeInt(item["regen_percent"], 0);
            if (max < 0 || normal < 0 || regen < 0 || regenTime < 0)
                add("ERROR", "shops", id, name, path, "Shop quantities and regeneration time cannot be negative.");
            if (max > 0 && normal > max)
                add("WARNING", "shops", id, name, path + ".normal_qty", $"Normal quantity {normal} is greater than maximum quantity {max}.");
            if (regenPercent is < 0 or > 100)
                add("ERROR", "shops", id, name, path + ".regen_percent", "Regeneration percent must be 0..100.");
            index++;
        }
    }

    private sealed class NpcSimulationAccumulator
    {
        public NpcSimulationAccumulator(RuntimeNpcAttackDefinition attack) => Attack = attack;
        public RuntimeNpcAttackDefinition Attack { get; }
        public int Attempts { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public int Dodges { get; set; }
        public int CastSuccesses { get; set; }
        public int CastFailures { get; set; }
        public long TotalDamage { get; set; }
    }

    private JsonObject ReadEffectiveRecord(string family, string targetId)
    {
        var baseRecord = GetFamily(family).Records.FirstOrDefault(record =>
            ReadRecordId(record).Equals(targetId, StringComparison.OrdinalIgnoreCase));
        var effective = baseRecord is null ? new JsonObject() : (JsonObject)baseRecord.DeepClone();
        var customOverride = FindOverride(family, targetId);
        if (customOverride is not null)
        {
            var operation = customOverride["operation"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? "patch";
            if (operation == "delete") throw new InvalidOperationException($"'{targetId}' is deleted from Custom1.");
            if (customOverride["fields"] is JsonObject fields) MergeObject(effective, fields);
        }
        if (baseRecord is null && customOverride is null)
            throw new KeyNotFoundException($"Object '{family}/{targetId}' was not found.");
        return effective;
    }

    private static IEnumerable<CustomWorldSimpleFieldDefinition> OrderEditorFields(IEnumerable<CustomWorldSimpleFieldDefinition> fields)
        => fields
            .OrderByDescending(field => field.Required)
            .ThenBy(field => EditorSectionOrder(field.Path))
            .ThenBy(field => field.Path, StringComparer.OrdinalIgnoreCase);

    private static int EditorSectionOrder(string path)
    {
        var root = path.TrimStart('@').Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return root.ToLowerInvariant() switch
        {
            "name" or "title" or "display_name" or "short_name" or "legacy_id" or "description" or "classification" => 0,
            "combat" or "effect" or "effects" or "damage" => 10,
            "equipment" or "requirements" or "prerequisites" => 20,
            "behavior" or "ai" or "movement" => 30,
            "environment" or "spawn_model" or "respawn" => 40,
            "economy" or "rewards" or "inventory" or "loot" => 50,
            "relationships" or "commands" or "aliases" => 60,
            "flags" or "status" => 70,
            _ => 100
        };
    }

    private static void DiscoverEditorFields(
        JsonNode? node,
        string path,
        IDictionary<string, CustomWorldSimpleFieldDefinition> fields,
        bool includeIndexedCollections)
    {
        if (node is null) return;
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (path.Length == 0 && ShouldSkipEditorRoot(property.Key)) continue;

                // Public-reference records contain useful recovered gameplay fields together with
                // provenance/source metadata. Expose only the gameplay payload in the creator UI.
                if (path.Length == 0 && property.Key.Equals("public_reference_0139", StringComparison.OrdinalIgnoreCase) &&
                    property.Value is JsonObject publicReference)
                {
                    if (publicReference["fields"] is JsonNode publicFields)
                        DiscoverEditorFields(publicFields, "public_reference_0139.fields", fields, includeIndexedCollections);
                    continue;
                }

                var childPath = path.Length == 0 ? property.Key : path + "." + property.Key;
                DiscoverEditorFields(property.Value, childPath, fields, includeIndexedCollections);
            }
            return;
        }

        if (path.Length == 0 || path.StartsWith('@')) return;
        if (fields.ContainsKey(path)) return;

        if (node is JsonArray array)
        {
            if (array.Count == 0) return;
            CustomWorldSimpleFieldKind kind;
            string help;
            if (array.All(item => item is JsonObject))
            {
                kind = CustomWorldSimpleFieldKind.ObjectLines;
                help = "One entry per line. Each line uses field=value | field=value. Add or remove lines to control the collection without JSON.";
            }
            else if (array.All(item => item is JsonValue value && value.TryGetValue<bool>(out _)))
            {
                kind = CustomWorldSimpleFieldKind.BooleanLines;
                help = "One yes/no value per line.";
            }
            else if (array.All(item => IsIntegerNode(item)))
            {
                kind = CustomWorldSimpleFieldKind.IntegerLines;
                help = "One whole number per line.";
            }
            else if (array.All(item => IsNumberNode(item)))
            {
                kind = CustomWorldSimpleFieldKind.DecimalLines;
                help = "One number per line.";
            }
            else
            {
                kind = CustomWorldSimpleFieldKind.Lines;
                help = "One value per line.";
            }

            fields[path] = new CustomWorldSimpleFieldDefinition(path, HumanizeEditorPath(path), kind, false, string.Empty, help);
            return;
        }

        if (node is not JsonValue valueNode) return;
        var fieldKind = valueNode.TryGetValue<bool>(out _)
            ? CustomWorldSimpleFieldKind.Boolean
            : IsIntegerNode(valueNode)
                ? CustomWorldSimpleFieldKind.Integer
                : IsNumberNode(valueNode)
                    ? CustomWorldSimpleFieldKind.Decimal
                    : ShouldUseMultiline(path, valueNode)
                        ? CustomWorldSimpleFieldKind.Multiline
                        : CustomWorldSimpleFieldKind.Text;
        fields[path] = new CustomWorldSimpleFieldDefinition(
            path,
            HumanizeEditorPath(path),
            fieldKind,
            false,
            string.Empty,
            "Full-control field: " + path);
    }

    private static bool IsIntegerNode(JsonNode? node)
    {
        if (node is not JsonValue value) return false;
        return value.TryGetValue<int>(out _) || value.TryGetValue<long>(out _);
    }

    private static bool IsNumberNode(JsonNode? node)
    {
        if (node is not JsonValue value) return false;
        return IsIntegerNode(node) || value.TryGetValue<decimal>(out _) || value.TryGetValue<double>(out _) || value.TryGetValue<float>(out _);
    }

    private static bool ShouldUseMultiline(string path, JsonValue value)
    {
        var leaf = path.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;
        if (leaf.Contains("description", StringComparison.OrdinalIgnoreCase) ||
            leaf.Contains("message", StringComparison.OrdinalIgnoreCase) ||
            leaf.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            leaf.Contains("script", StringComparison.OrdinalIgnoreCase) ||
            leaf.Contains("source", StringComparison.OrdinalIgnoreCase) ||
            leaf.Contains("notes", StringComparison.OrdinalIgnoreCase)) return true;
        return value.TryGetValue<string>(out var text) && (text?.Length ?? 0) > 100;
    }

    private static string HumanizeEditorPath(string path)
    {
        var leaf = path.TrimStart('@').Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;
        leaf = leaf.Replace('-', '_');
        var words = leaf.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return path;
        return string.Join(" ", words.Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static JsonArray ParseIntegerLines(string raw, string label)
    {
        var result = new JsonArray();
        foreach (var line in SplitEditorLines(raw))
        {
            if (!int.TryParse(line, out var value)) throw new InvalidOperationException($"{label} contains '{line}', which is not a whole number.");
            result.Add(value);
        }
        return result;
    }

    private static JsonArray ParseDecimalLines(string raw, string label)
    {
        var result = new JsonArray();
        foreach (var line in SplitEditorLines(raw))
        {
            if (!decimal.TryParse(line, out var value)) throw new InvalidOperationException($"{label} contains '{line}', which is not a number.");
            result.Add(value);
        }
        return result;
    }

    private static JsonArray ParseBooleanLines(string raw, string label)
    {
        var result = new JsonArray();
        foreach (var line in SplitEditorLines(raw))
        {
            if (!bool.TryParse(line, out var value)) throw new InvalidOperationException($"{label} contains '{line}'. Enter true or false on each line.");
            result.Add(value);
        }
        return result;
    }

    private static JsonArray ParseObjectLines(string raw, string label)
    {
        var result = new JsonArray();
        foreach (var line in SplitEditorLines(raw))
        {
            var obj = new JsonObject();
            foreach (var token in line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = token.IndexOf('=');
                if (separator <= 0) throw new InvalidOperationException($"{label} entry '{token}' must use field=value.");
                var key = token[..separator].Trim();
                var text = token[(separator + 1)..].Trim();
                if (key.Length == 0) throw new InvalidOperationException($"{label} contains an empty field name.");
                SetPath(obj, key, ParseEditorValue(text));
            }
            if (obj.Count > 0) result.Add(obj);
        }
        return result;
    }

    private static JsonNode? ParseEditorValue(string text)
    {
        if (bool.TryParse(text, out var boolean)) return JsonValue.Create(boolean);
        if (int.TryParse(text, out var integer)) return JsonValue.Create(integer);
        if (decimal.TryParse(text, out var number)) return JsonValue.Create(number);
        if (text.Contains(','))
            return new JsonArray(text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => ParseEditorValue(value)).ToArray());
        return JsonValue.Create(text);
    }

    private static string FormatObjectLines(JsonArray array)
    {
        var lines = new List<string>();
        foreach (var item in array.OfType<JsonObject>())
        {
            var values = new List<string>();
            FlattenObjectForEditor(item, string.Empty, values);
            lines.Add(string.Join(" | ", values));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static void FlattenObjectForEditor(JsonNode? node, string path, ICollection<string> values)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var child = path.Length == 0 ? property.Key : path + "." + property.Key;
                FlattenObjectForEditor(property.Value, child, values);
            }
            return;
        }
        if (node is JsonArray array)
        {
            values.Add(path + "=" + string.Join(",", array.Select(item => item?.ToString() ?? string.Empty)));
            return;
        }
        values.Add(path + "=" + (node?.ToString() ?? string.Empty));
    }

    private static string[] SplitEditorLines(string raw) => (raw ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => value.Length > 0)
        .ToArray();

    private static int ReadEditorInt(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : 0;

    private static string ReadEditorText(IReadOnlyDictionary<string, string> values, string key, string fallback)
        => values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw.Trim() : fallback;

    private static string[] SplitLines(string raw) => (raw ?? string.Empty)
        .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string ReadInventoryLines(JsonObject record)
    {
        if (record["inventory"] is not JsonArray inventory) return string.Empty;
        return string.Join(Environment.NewLine, inventory.OfType<JsonObject>()
            .Select(item => item["item_ref"]?.GetValue<string>() ?? string.Empty)
            .Where(value => value.Length > 0));
    }

    private static JsonNode? ReadPath(JsonObject root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out current)) return null;
        }
        return current;
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return;
        var current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is not JsonObject child)
            {
                child = new JsonObject();
                current[segments[i]] = child;
            }
            current = child;
        }
        current[segments[^1]] = value;
    }

    private static string NodeToEditorText(JsonNode node, CustomWorldSimpleFieldKind kind)
    {
        if ((kind is CustomWorldSimpleFieldKind.Lines or CustomWorldSimpleFieldKind.IntegerLines or
             CustomWorldSimpleFieldKind.DecimalLines or CustomWorldSimpleFieldKind.BooleanLines) && node is JsonArray array)
            return string.Join(Environment.NewLine, array.Select(value => value?.ToString() ?? string.Empty));
        if (kind == CustomWorldSimpleFieldKind.ObjectLines && node is JsonArray objectArray)
            return FormatObjectLines(objectArray);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text)) return text ?? string.Empty;
            return node.ToString();
        }
        return node.ToJsonString(JsonOptions);
    }

    private static bool ReadNodeBool(JsonNode? node, bool fallback)
    {
        if (node is not JsonValue value) return fallback;
        if (value.TryGetValue<bool>(out var result)) return result;
        return bool.TryParse(value.ToString(), out result) ? result : fallback;
    }

    private static void MergeObject(JsonObject target, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Value is JsonObject patchObject && target[property.Key] is JsonObject targetObject)
            {
                MergeObject(targetObject, patchObject);
                continue;
            }
            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private bool IsGeneratedDataReady() =>
        File.Exists(Path.Combine(GeneratedDataPath, "rooms.json")) &&
        File.Exists(Path.Combine(GeneratedDataPath, "exits.json")) &&
        File.Exists(Path.Combine(GeneratedDataPath, "npcs.json")) &&
        File.Exists(Path.Combine(GeneratedDataPath, "spawns.json")) &&
        File.Exists(Path.Combine(GeneratedDataPath, "world_manifest.json"));

    private static JsonObject CreateOverrideRoot() => new()
    {
        ["schema"] = Schema,
        ["world_id"] = CustomWorldId,
        ["base_world"] = "Classic",
        ["description"] = "Host-authored differences for Custom World 1. Classic remains unchanged.",
        ["updated_utc"] = DateTimeOffset.UtcNow.ToString("O"),
        ["overrides"] = new JsonArray()
    };

    private static JsonObject CreateDefaultWorld() => new()
    {
        ["id"] = CustomWorldId,
        ["name"] = "Custom World 1",
        ["type"] = "ReforgedCustom",
        ["rulesProfile"] = "classic",
        ["dataRoot"] = $"Data/{CustomWorldId}",
        ["roomsFile"] = "rooms.json",
        ["exitsFile"] = "exits.json",
        ["triggersFile"] = "triggers.json",
        ["npcsFile"] = "npcs.json",
        ["spawnsFile"] = "spawns.json",
        ["diagnosticRoom"] = "room.map1.1",
        ["aiMode"] = "Off",
        ["runtimeEnabled"] = true,
        ["transfersAllowed"] = false,
        ["classicProtected"] = false,
        ["description"] = "Editable Classic-derived custom world. Custom changes are isolated from Classic.",
        ["motd"] = "Welcome to Custom World 1.",
        ["loginBanner"] = "Custom World 1"
    };

    private static bool IsCustomWorld(JsonObject world) =>
        string.Equals(world["id"]?.GetValue<string>(), CustomWorldId, StringComparison.OrdinalIgnoreCase);

    private static CustomWorldOverrideSummary ToSummary(JsonObject entry)
    {
        var fields = entry["fields"] as JsonObject ?? new JsonObject();
        return new CustomWorldOverrideSummary(
            entry["family"]?.GetValue<string>() ?? string.Empty,
            entry["target_id"]?.GetValue<string>() ?? string.Empty,
            entry["operation"]?.GetValue<string>() ?? "patch",
            fields.Count,
            fields.ToJsonString(JsonOptions));
    }

    private static string ReadString(JsonObject obj, string name, string fallback) =>
        obj[name]?.GetValue<string>() ?? fallback;

    private static bool ReadBool(JsonObject obj, string name, bool fallback) =>
        obj[name] is JsonValue value && value.TryGetValue<bool>(out var result) ? result : fallback;

    private static string NormalizeRequired(string value, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new InvalidOperationException(label + " is required.");
        return normalized;
    }

    private static string ResolveProjectRoot(string? startDirectory)
    {
        var current = new DirectoryInfo(startDirectory ?? Environment.CurrentDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Librarium")) &&
                File.Exists(Path.Combine(current.FullName, "Config", "server.worlds.json")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the RealmGate repository root.");
    }
}




















