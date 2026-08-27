using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using MajorMud.Reforged.Compiler;

namespace Reforged.App.Services;

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

    public CustomWorldEditorService(string? startDirectory = null)
    {
        _projectRoot = ResolveProjectRoot(startDirectory);
    }

    public string ProjectRoot => _projectRoot;
    public string ConfigPath => Path.Combine(_projectRoot, "Config", "server.worlds.json");
    public string OverridePath => WorldOverrideEngine.ResolveOverridePath(_projectRoot, CustomWorldId);
    public string GeneratedDataPath => Path.Combine(_projectRoot, "Data", CustomWorldId);

    public CustomWorldEditorSnapshot Load()
    {
        EnsureCustomWorldConfigured();
        var settings = ReadSettings();
        var overrides = ReadOverrides();
        return new CustomWorldEditorSnapshot(
            settings,
            overrides,
            _projectRoot,
            ConfigPath,
            OverridePath,
            GeneratedDataPath,
            IsGeneratedDataReady());
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

    public IReadOnlyList<CustomWorldObjectSummary> BrowseObjects(string family, string? search, int limit = 500)
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
                if (entry["fields"] is JsonObject fields && fields["name"] is not null)
                {
                    try { name = fields["name"]!.GetValue<string>() ?? name; } catch { }
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
            .Take(Math.Clamp(limit, 1, 2000))
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

    public CustomWorldSimpleFormDefinition GetSimpleForm(string family) => CustomWorldSimpleFormCatalog.Get(family);

    public (bool ExistsInBase, IReadOnlyDictionary<string, string> TextValues, IReadOnlyDictionary<string, bool> BoolValues) LoadSimpleRecord(
        string family,
        string targetId)
    {
        family = NormalizeRequired(family, "Family");
        targetId = NormalizeRequired(targetId, "Object ID");
        var definition = CustomWorldSimpleFormCatalog.Get(family);
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
        var definition = CustomWorldSimpleFormCatalog.Get(family);
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
                CustomWorldSimpleFieldKind.Lines => new JsonArray(SplitLines(raw).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
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
        var root = ReadOverrideRoot(createIfMissing: true);
        return (root["overrides"] as JsonArray)?.OfType<JsonObject>()
            .Select(ToSummary)
            .OrderBy(entry => entry.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.TargetId, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
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
        if (!File.Exists(OverridePath))
        {
            var empty = CreateOverrideRoot();
            if (createIfMissing) WriteOverrideRoot(empty);
            return empty;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(OverridePath)) as JsonObject
                   ?? throw new JsonException("Override root must be a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Custom1 override file is invalid: " + ex.Message, ex);
        }
    }

    private void WriteOverrideRoot(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OverridePath)!);
        File.WriteAllText(OverridePath, root.ToJsonString(JsonOptions) + Environment.NewLine);
    }

    private LibrariumFamily GetFamily(string family)
    {
        if (_catalogCache is null)
        {
            var diagnostics = new List<CompilerDiagnostic>();
            _catalogCache = new LibrariumCatalogReader()
                .ReadFamilies(Path.Combine(_projectRoot, "Librarium"), diagnostics)
                .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        return _catalogCache.TryGetValue(family, out var result)
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
        return string.Empty;
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
        if (kind == CustomWorldSimpleFieldKind.Lines && node is JsonArray array)
            return string.Join(Environment.NewLine, array.Select(value => value?.ToString() ?? string.Empty).Where(value => value.Length > 0));
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
        throw new DirectoryNotFoundException("Could not locate the MajorMUD Reforged repository root.");
    }
}
