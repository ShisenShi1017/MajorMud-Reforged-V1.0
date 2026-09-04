using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reforged.App.Models;

namespace Reforged.App.Services;

public enum RealmGateGameDataAuthorityMode
{
    ReforgedWorld,
    ExternalBbs
}

public sealed record RealmGateGameDataContext(
    RealmGateGameDataAuthorityMode Mode,
    string BaseWorld,
    string BaseDataRoot,
    string ServerKey,
    string OverlayPath,
    bool IsEditable,
    string Description);

public sealed record RealmGateGameDataRecord(
    string Family,
    string Id,
    string Name,
    JsonObject EffectiveRecord,
    int OverrideCount)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
    public bool HasOverrides => OverrideCount > 0;
}

public sealed record RealmGateGameDataField(string Path, string Value, bool IsOverridden);

public sealed record RealmGateMdbImportRecordDelta(
    string Family,
    string RecordId,
    JsonObject? AddedRecord,
    IReadOnlyDictionary<string, JsonNode?> Fields);

public sealed record RealmGateMdbImportDelta(
    string FileName,
    string Sha256,
    DateTimeOffset ImportedAtUtc,
    IReadOnlyList<RealmGateMdbImportRecordDelta> Records);

public sealed record RealmGateMdbImportMetadata(
    string FileName,
    string Sha256,
    DateTimeOffset ImportedAtUtc);

/// <summary>
/// CodeUpdate0142 game-data authority boundary.
///
/// Reforged connections consume the generated Data/&lt;active world&gt; projection selected
/// by the server's Reforged catalog identity. Those world projections are read-only in
/// the Play client.
///
/// External BBS connections always use generated Classic as the base. Only field-level
/// differences are persisted under LocalAppData in a BBS-specific overlay. Effective
/// reads are therefore: manual override -> imported MDB delta -> Classic. Re-importing
/// replaces only the MDB-derived portion and preserves manual edits. Records present in
/// the MDB but absent from Classic are stored as overlay additions because the complete
/// new record is itself a difference. No editable copy of the complete MDB/base data is created.
/// </summary>
public sealed class RealmGateClientGameDataOverlayService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, string> FamilyFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Items"] = "items.json",
            ["Equipment"] = "equipment.json",
            ["Spells"] = "spells.json",
            ["Abilities"] = "abilities.json",
            ["Monsters / NPCs"] = "npcs.json",
            ["Shops"] = "shops.json",
            ["Banks"] = "banks.json",
            ["Rooms"] = "rooms.json",
            ["Exits / Paths"] = "exits.json",
            ["Quests"] = "quests.json",
            ["Classes"] = "classes.json",
            ["Races"] = "races.json",
            ["Skills"] = "skills.json",
            ["Spawns"] = "spawns.json",
            ["Status Effects"] = "status_effects.json",
            ["Room Interactions"] = "room_interactions.json"
        };

    public IReadOnlyList<string> Families => FamilyFiles.Keys.ToArray();

    public RealmGateGameDataContext ResolveContext(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (IsReforgedConnection(session))
        {
            var activeWorld = FirstNonBlank(session.ServerCatalogWorld, session.WorldName, "Classic");
            var dataRoot = FindWorldDataRoot(activeWorld, allowClassicFallback: false, out var resolvedWorld);
            return new RealmGateGameDataContext(
                RealmGateGameDataAuthorityMode.ReforgedWorld,
                resolvedWorld,
                dataRoot,
                BuildServerKey(session),
                string.Empty,
                IsEditable: false,
                $"RealmGate server authority: {resolvedWorld}. The client reads that RealmGate world projection directly; local BBS overrides are disabled.");
        }

        var classicRoot = FindWorldDataRoot("Classic", allowClassicFallback: false, out _);
        var serverKey = BuildServerKey(session);
        var overlayPath = BuildOverlayPath(session, serverKey);
        return new RealmGateGameDataContext(
            RealmGateGameDataAuthorityMode.ExternalBbs,
            "Classic",
            classicRoot,
            serverKey,
            overlayPath,
            IsEditable: true,
            $"External BBS authority: Classic base + local overlay for {session.ServerProfileName} ({session.HostName}:{session.Port}).");
    }

    public IReadOnlyList<RealmGateGameDataRecord> LoadFamily(ClientSessionConnection session, string family)
    {
        var context = ResolveContext(session);
        if (!FamilyFiles.TryGetValue(family, out var fileName))
            return [];

        var basePath = Path.Combine(context.BaseDataRoot, fileName);
        if (!File.Exists(basePath))
            return [];

        var overlay = context.Mode == RealmGateGameDataAuthorityMode.ExternalBbs
            ? LoadOverlay(context)
            : new RealmGateBbsOverlayDocument();

        var familyOverrides = overlay.Families.TryGetValue(family, out var records)
            ? records
            : new Dictionary<string, RealmGateRecordOverlay>(StringComparer.OrdinalIgnoreCase);

        var result = new List<RealmGateGameDataRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in EnumerateRecords(basePath))
        {
            if (record is not JsonObject source)
                continue;

            var id = ResolveRecordId(family, source);
            if (id.Length == 0)
                continue;

            seen.Add(id);
            var effective = (JsonObject)source.DeepClone();
            var count = 0;
            if (familyOverrides.TryGetValue(id, out var recordOverlay))
                count = ApplyRecordOverlay(effective, recordOverlay);

            result.Add(new RealmGateGameDataRecord(
                family,
                id,
                ResolveRecordName(effective),
                effective,
                count));
        }

        // A record that exists only in the imported MDB has no Classic row to fall
        // back to. Its canonicalized imported record is therefore the smallest valid
        // overlay representation of that difference. Manual fields still win.
        foreach (var pair in familyOverrides)
        {
            if (seen.Contains(pair.Key) || pair.Value.ImportedRecord is null)
                continue;

            var effective = (JsonObject)pair.Value.ImportedRecord.DeepClone();
            var count = ApplyRecordOverlay(effective, pair.Value, applyImportedRecordFields: false);
            result.Add(new RealmGateGameDataRecord(
                family,
                pair.Key,
                ResolveRecordName(effective),
                effective,
                Math.Max(1, count)));
        }

        return result
            .OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<RealmGateGameDataRecord> LoadClassicFamily(string family)
    {
        if (!FamilyFiles.TryGetValue(family, out var fileName))
            return [];

        var classicRoot = FindWorldDataRoot("Classic", allowClassicFallback: false, out _);
        var basePath = Path.Combine(classicRoot, fileName);
        if (!File.Exists(basePath))
            return [];

        var result = new List<RealmGateGameDataRecord>();
        foreach (var record in EnumerateRecords(basePath))
        {
            if (record is not JsonObject source)
                continue;
            var id = ResolveRecordId(family, source);
            if (id.Length == 0)
                continue;
            var clone = (JsonObject)source.DeepClone();
            result.Add(new RealmGateGameDataRecord(family, id, ResolveRecordName(clone), clone, 0));
        }

        return result
            .OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<RealmGateGameDataRecord> LoadBaseFamily(ClientSessionConnection session, string family)
    {
        var context = ResolveContext(session);
        if (!FamilyFiles.TryGetValue(family, out var fileName))
            return [];

        var basePath = Path.Combine(context.BaseDataRoot, fileName);
        if (!File.Exists(basePath))
            return [];

        var result = new List<RealmGateGameDataRecord>();
        foreach (var record in EnumerateRecords(basePath))
        {
            if (record is not JsonObject source)
                continue;
            var id = ResolveRecordId(family, source);
            if (id.Length == 0)
                continue;
            var clone = (JsonObject)source.DeepClone();
            result.Add(new RealmGateGameDataRecord(family, id, ResolveRecordName(clone), clone, 0));
        }

        return result
            .OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public JsonObject? LoadEffectiveRecord(ClientSessionConnection session, string family, string recordId)
    {
        return LoadFamily(session, family)
            .FirstOrDefault(record => string.Equals(record.Id, recordId, StringComparison.OrdinalIgnoreCase))
            ?.EffectiveRecord;
    }

    public IReadOnlyList<RealmGateGameDataField> FlattenFields(
        ClientSessionConnection session,
        string family,
        RealmGateGameDataRecord record)
    {
        var context = ResolveContext(session);
        var overridden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.Mode == RealmGateGameDataAuthorityMode.ExternalBbs)
        {
            var overlay = LoadOverlay(context);
            if (overlay.Families.TryGetValue(family, out var familyMap) &&
                familyMap.TryGetValue(record.Id, out var recordOverlay))
            {
                overridden.UnionWith(recordOverlay.ImportedFields.Keys);
                overridden.UnionWith(recordOverlay.Fields.Keys);
            }
        }

        var fields = new List<RealmGateGameDataField>();
        FlattenNode(record.EffectiveRecord, string.Empty, fields, overridden);
        return fields.OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void SetOverride(
        ClientSessionConnection session,
        string family,
        string recordId,
        string fieldPath,
        string jsonValue)
    {
        var context = RequireEditableExternalContext(session);
        if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(recordId) || string.IsNullOrWhiteSpace(fieldPath))
            throw new ArgumentException("Family, record id, and field path are required.");

        JsonNode? value;
        try
        {
            value = JsonNode.Parse(jsonValue);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Override value must be valid JSON. Strings need quotes, for example \"new name\".", exception);
        }

        var overlay = LoadOverlay(context);
        var familyMap = GetOrCreateFamily(overlay, family);
        if (!familyMap.TryGetValue(recordId, out var recordOverlay))
        {
            recordOverlay = new RealmGateRecordOverlay();
            familyMap[recordId] = recordOverlay;
        }

        recordOverlay.Fields[fieldPath.Trim()] = value;
        overlay.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveOverlay(context, overlay);
    }

    public void RemoveFieldOverride(ClientSessionConnection session, string family, string recordId, string fieldPath)
    {
        var context = RequireEditableExternalContext(session);
        var overlay = LoadOverlay(context);
        if (!overlay.Families.TryGetValue(family, out var familyMap) ||
            !familyMap.TryGetValue(recordId, out var recordOverlay))
            return;

        // Reset means Classic fallback, not merely "remove the manual layer".
        // Therefore both manual and MDB-derived values for the field are removed.
        recordOverlay.Fields.Remove(fieldPath);
        recordOverlay.ImportedFields.Remove(fieldPath);
        if (recordOverlay.Fields.Count == 0 &&
            recordOverlay.ImportedFields.Count == 0 &&
            recordOverlay.ImportedRecord is null)
        {
            familyMap.Remove(recordId);
        }
        if (familyMap.Count == 0)
            overlay.Families.Remove(family);

        overlay.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveOverlay(context, overlay);
    }

    public void RemoveRecordOverrides(ClientSessionConnection session, string family, string recordId)
    {
        var context = RequireEditableExternalContext(session);
        var overlay = LoadOverlay(context);
        if (overlay.Families.TryGetValue(family, out var familyMap))
        {
            familyMap.Remove(recordId);
            if (familyMap.Count == 0)
                overlay.Families.Remove(family);
        }

        overlay.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveOverlay(context, overlay);
    }

    public void ClearOverlay(ClientSessionConnection session)
    {
        var context = RequireEditableExternalContext(session);
        if (File.Exists(context.OverlayPath))
            File.Delete(context.OverlayPath);
    }

    public int CountOverrides(ClientSessionConnection session)
    {
        var context = ResolveContext(session);
        if (context.Mode != RealmGateGameDataAuthorityMode.ExternalBbs)
            return 0;

        var overlay = LoadOverlay(context);
        return overlay.Families.Values.Sum(family => family.Values.Sum(record =>
            record.Fields.Keys
                .Concat(record.ImportedFields.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() + (record.ImportedRecord is null ? 0 : 1)));
    }

    public string GetOverlayPath(ClientSessionConnection session) => ResolveContext(session).OverlayPath;

    public RealmGateMdbImportMetadata? GetLastImportMetadata(ClientSessionConnection session)
    {
        var context = ResolveContext(session);
        if (context.Mode != RealmGateGameDataAuthorityMode.ExternalBbs)
            return null;
        var overlay = LoadOverlay(context);
        if (string.IsNullOrWhiteSpace(overlay.LastImportedMdbFileName) ||
            string.IsNullOrWhiteSpace(overlay.LastImportedMdbSha256) ||
            overlay.LastImportedMdbAtUtc is null)
        {
            return null;
        }

        return new RealmGateMdbImportMetadata(
            overlay.LastImportedMdbFileName,
            overlay.LastImportedMdbSha256,
            overlay.LastImportedMdbAtUtc.Value);
    }

    public void ReplaceImportedMdbDelta(ClientSessionConnection session, RealmGateMdbImportDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        var context = RequireEditableExternalContext(session);
        var overlay = LoadOverlay(context);

        // Re-import owns only the imported layer. Existing manual overrides are
        // preserved and remain the highest-priority client-side values.
        foreach (var family in overlay.Families.Values)
        {
            foreach (var record in family.Values)
            {
                record.ImportedFields.Clear();
                record.ImportedRecord = null;
            }
        }
        PruneEmptyOverlay(overlay);

        foreach (var deltaRecord in delta.Records)
        {
            var familyMap = GetOrCreateFamily(overlay, deltaRecord.Family);
            if (!familyMap.TryGetValue(deltaRecord.RecordId, out var recordOverlay))
            {
                recordOverlay = new RealmGateRecordOverlay();
                familyMap[deltaRecord.RecordId] = recordOverlay;
            }

            recordOverlay.ImportedRecord = deltaRecord.AddedRecord is null
                ? null
                : (JsonObject)deltaRecord.AddedRecord.DeepClone();
            foreach (var field in deltaRecord.Fields)
                recordOverlay.ImportedFields[field.Key] = field.Value?.DeepClone();
        }

        PruneEmptyOverlay(overlay);
        overlay.LastImportedMdbFileName = delta.FileName;
        overlay.LastImportedMdbSha256 = delta.Sha256;
        overlay.LastImportedMdbAtUtc = delta.ImportedAtUtc;
        overlay.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveOverlay(context, overlay);
    }

    public static bool IsReforgedConnection(ClientSessionConnection session)
    {
        if (!string.IsNullOrWhiteSpace(session.ServerCatalogWorld) ||
            !string.IsNullOrWhiteSpace(session.ServerCatalogCompileHash) ||
            session.ServerCatalogFamilies.Count > 0 ||
            session.ServerCatalogUpdatedAt is not null)
        {
            return true;
        }

        // The stock local profile is created by Reforged itself and is safe to identify
        // before the server catalog response has arrived. Remote servers are not guessed.
        return string.Equals(session.ServerProfileName, "Local Host", StringComparison.OrdinalIgnoreCase) &&
               IsLoopbackHost(session.HostName);
    }

    private static RealmGateGameDataContext RequireEditableExternalContext(ClientSessionConnection session)
    {
        var context = new RealmGateClientGameDataOverlayService().ResolveContext(session);
        if (!context.IsEditable)
            throw new InvalidOperationException("RealmGate world game data is server-authoritative and cannot be edited from Play.");
        return context;
    }

    private static RealmGateBbsOverlayDocument LoadOverlay(RealmGateGameDataContext context)
    {
        if (string.IsNullOrWhiteSpace(context.OverlayPath) || !File.Exists(context.OverlayPath))
            return NewOverlay(context);

        try
        {
            var document = JsonSerializer.Deserialize<RealmGateBbsOverlayDocument>(File.ReadAllText(context.OverlayPath), JsonOptions)
                           ?? NewOverlay(context);
            NormalizeOverlay(document, context);
            return document;
        }
        catch (JsonException)
        {
            var backup = context.OverlayPath + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            File.Move(context.OverlayPath, backup, true);
            return NewOverlay(context);
        }
    }

    private static void SaveOverlay(RealmGateGameDataContext context, RealmGateBbsOverlayDocument overlay)
    {
        NormalizeOverlay(overlay, context);
        var directory = Path.GetDirectoryName(context.OverlayPath)
                        ?? throw new InvalidOperationException("Overlay path has no directory.");
        Directory.CreateDirectory(directory);
        var temporary = context.OverlayPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(overlay, JsonOptions));
        File.Move(temporary, context.OverlayPath, true);
    }

    private static RealmGateBbsOverlayDocument NewOverlay(RealmGateGameDataContext context) => new()
    {
        Version = 2,
        BaseWorld = "Classic",
        ServerKey = context.ServerKey,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static void NormalizeOverlay(RealmGateBbsOverlayDocument overlay, RealmGateGameDataContext context)
    {
        overlay.Version = 2;
        overlay.BaseWorld = "Classic";
        overlay.ServerKey = context.ServerKey;
        overlay.Families ??= new Dictionary<string, Dictionary<string, RealmGateRecordOverlay>>(StringComparer.OrdinalIgnoreCase);

        overlay.Families = overlay.Families.ToDictionary(
            pair => pair.Key,
            pair => (pair.Value ?? new Dictionary<string, RealmGateRecordOverlay>())
                .ToDictionary(
                    record => record.Key,
                    record => record.Value ?? new RealmGateRecordOverlay(),
                    StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var family in overlay.Families.Values)
        {
            foreach (var record in family.Values)
            {
                record.Fields ??= new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
                record.Fields = record.Fields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                record.ImportedFields ??= new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
                record.ImportedFields = record.ImportedFields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static int ApplyRecordOverlay(JsonObject effective, RealmGateRecordOverlay overlay, bool applyImportedRecordFields = true)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (applyImportedRecordFields)
        {
            foreach (var pair in overlay.ImportedFields)
            {
                SetPath(effective, pair.Key, pair.Value?.DeepClone());
                paths.Add(pair.Key);
            }
        }

        foreach (var pair in overlay.Fields)
        {
            SetPath(effective, pair.Key, pair.Value?.DeepClone());
            paths.Add(pair.Key);
        }
        return paths.Count;
    }

    private static void PruneEmptyOverlay(RealmGateBbsOverlayDocument overlay)
    {
        foreach (var familyPair in overlay.Families.ToArray())
        {
            foreach (var recordPair in familyPair.Value.ToArray())
            {
                var record = recordPair.Value;
                if (record.Fields.Count == 0 &&
                    record.ImportedFields.Count == 0 &&
                    record.ImportedRecord is null)
                {
                    familyPair.Value.Remove(recordPair.Key);
                }
            }

            if (familyPair.Value.Count == 0)
                overlay.Families.Remove(familyPair.Key);
        }
    }

    private static Dictionary<string, RealmGateRecordOverlay> GetOrCreateFamily(RealmGateBbsOverlayDocument overlay, string family)
    {
        if (!overlay.Families.TryGetValue(family, out var familyMap))
        {
            familyMap = new Dictionary<string, RealmGateRecordOverlay>(StringComparer.OrdinalIgnoreCase);
            overlay.Families[family] = familyMap;
        }
        return familyMap;
    }

    private static IEnumerable<JsonNode> EnumerateRecords(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path));
        if (root is JsonArray direct)
        {
            foreach (var record in direct)
                if (record is not null)
                    yield return record;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        if (obj["records"] is JsonArray records)
        {
            foreach (var record in records)
                if (record is not null)
                    yield return record;
            yield break;
        }

        if (obj["record_parts"] is not JsonArray parts)
            yield break;

        foreach (var partNode in parts)
        {
            var relative = partNode?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            var partPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(path) ?? string.Empty,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(partPath))
                throw new FileNotFoundException($"Generated part listed by {Path.GetFileName(path)} was not found.", partPath);

            foreach (var record in EnumerateRecords(partPath))
                yield return record;
        }
    }

    private static string ResolveRecordId(string family, JsonObject record)
    {
        var id = ScalarText(record["id"]);
        if (id.Length > 0)
            return id;

        var legacy = ScalarText(record["legacy_id"]);
        if (legacy.Length > 0)
            return FamilyPrefix(family) + legacy;

        foreach (var candidate in new[] { "item_id", "spell_id", "ability_id", "npc_id", "room_id", "shop_id", "bank_id", "quest_id", "spawn_id", "interaction_id" })
        {
            var candidateValue = ScalarText(record[candidate]);
            if (candidateValue.Length == 0)
                continue;
            return candidateValue.Contains('.', StringComparison.Ordinal)
                ? candidateValue
                : FamilyPrefix(family) + candidateValue;
        }

        if (record["sourceNumbers"] is JsonObject sourceNumbers)
        {
            var number = ScalarText(sourceNumbers["number"]);
            if (number.Length > 0)
                return FamilyPrefix(family) + number;
        }

        var name = ResolveRecordName(record);
        return name.Length == 0 ? string.Empty : FamilyPrefix(family) + Slug(name);
    }

    private static string ResolveRecordName(JsonObject record)
    {
        var name = ScalarText(record["name"]);
        if (name.Length > 0)
            return name;

        if (record["public_reference_0139"] is JsonObject reference &&
            reference["fields"] is JsonObject fields)
        {
            name = ScalarText(fields["name"]);
            if (name.Length > 0)
                return name;
        }

        return string.Empty;
    }

    private static void FlattenNode(JsonNode? node, string path, List<RealmGateGameDataField> output, HashSet<string> overridden)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var pair in obj)
                {
                    var childPath = path.Length == 0 ? pair.Key : path + "." + pair.Key;
                    FlattenNode(pair.Value, childPath, output, overridden);
                }
                break;
            case JsonArray array:
                if (path.Length > 0)
                    output.Add(new RealmGateGameDataField(path, array.ToJsonString(), overridden.Contains(path)));
                break;
            default:
                if (path.Length > 0)
                    output.Add(new RealmGateGameDataField(path, node?.ToJsonString() ?? "null", overridden.Contains(path)));
                break;
        }
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return;

        JsonObject current = root;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (current[parts[index]] is not JsonObject next)
            {
                next = new JsonObject();
                current[parts[index]] = next;
            }
            current = next;
        }

        current[parts[^1]] = value;
    }

    private static string FindWorldDataRoot(string requestedWorld, bool allowClassicFallback, out string resolvedWorld)
    {
        foreach (var start in CandidateStarts())
        {
            for (var current = start; current is not null; current = current.Parent)
            {
                var dataRoot = Path.Combine(current.FullName, "Data");
                if (!Directory.Exists(dataRoot))
                    continue;

                var exact = Directory.GetDirectories(dataRoot)
                    .FirstOrDefault(path => string.Equals(Path.GetFileName(path), requestedWorld, StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    resolvedWorld = Path.GetFileName(exact);
                    return exact;
                }

                if (allowClassicFallback)
                {
                    var classic = Directory.GetDirectories(dataRoot)
                        .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "Classic", StringComparison.OrdinalIgnoreCase));
                    if (classic is not null)
                    {
                        resolvedWorld = "Classic";
                        return classic;
                    }
                }
            }
        }

        resolvedWorld = requestedWorld;
        throw new DirectoryNotFoundException(
            $"Generated Data/{requestedWorld} was not found. RealmGate world data fails closed instead of silently using Classic for a different world.");
    }

    private static IEnumerable<DirectoryInfo> CandidateStarts()
    {
        yield return new DirectoryInfo(AppContext.BaseDirectory);
        yield return new DirectoryInfo(Directory.GetCurrentDirectory());
    }

    private static string BuildServerKey(ClientSessionConnection session)
    {
        var raw = $"{session.ServerProfileName.Trim()}|{session.HostName.Trim().ToLowerInvariant()}|{session.Port.ToString(CultureInfo.InvariantCulture)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..12];
        return $"{Slug(session.ServerProfileName)}-{hash}";
    }

    private static string BuildOverlayPath(ClientSessionConnection session, string serverKey)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(local, "RealmGate", "Client", "GameDataOverlays", serverKey);
        var path = Path.Combine(root, "overlay.json");

        // One-time compatibility migration. Existing external-BBS deltas remain valid;
        // moving the product identity must not force users to re-import their MDBs.
        var legacyPath = Path.Combine(
            local,
            "MajorMUD Reforged",
            "Client",
            "GameDataOverlays",
            serverKey,
            "overlay.json");
        if (!File.Exists(path) && File.Exists(legacyPath))
        {
            try
            {
                Directory.CreateDirectory(root);
                File.Copy(legacyPath, path, overwrite: false);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return path;
    }

    private static bool IsLoopbackHost(string host)
    {
        var normalized = (host ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "127.0.0.1" or "localhost" or "::1";
    }

    private static string FirstNonBlank(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string ScalarText(JsonNode? node)
    {
        if (node is null)
            return string.Empty;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text?.Trim() ?? string.Empty;
            return value.ToJsonString().Trim('"');
        }
        return string.Empty;
    }

    private static string FamilyPrefix(string family) => family.ToLowerInvariant() switch
    {
        "items" => "item.",
        "equipment" => "equipment.",
        "spells" => "spell.",
        "abilities" => "ability.",
        "monsters / npcs" => "npc.",
        "shops" => "shop.",
        "banks" => "bank.",
        "rooms" => "room.",
        "exits / paths" => "exit.",
        "quests" => "quest.",
        "classes" => "class.",
        "races" => "race.",
        "skills" => "skill.",
        "spawns" => "spawn.",
        "status effects" => "status.",
        "room interactions" => "interaction.",
        _ => Slug(family) + "."
    };

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in (value ?? string.Empty).Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        return builder.ToString().Trim('-');
    }
}

public sealed class RealmGateBbsOverlayDocument
{
    public int Version { get; set; } = 2;
    public string BaseWorld { get; set; } = "Classic";
    public string ServerKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? LastImportedMdbFileName { get; set; }
    public string? LastImportedMdbSha256 { get; set; }
    public DateTimeOffset? LastImportedMdbAtUtc { get; set; }
    public Dictionary<string, Dictionary<string, RealmGateRecordOverlay>> Families { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RealmGateRecordOverlay
{
    // Manual edits are retained separately from MDB-derived values so a re-import
    // can refresh the imported layer without destroying user corrections.
    public Dictionary<string, JsonNode?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, JsonNode?> ImportedFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public JsonObject? ImportedRecord { get; set; }
}
