using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNpcAiRelationship(
    string SpawnId,
    string RoomReference,
    IReadOnlyList<string> NpcIds,
    int MonsterGroupId,
    int RespawnMinutes,
    string InteractionModel,
    IReadOnlyDictionary<string,string> Owners);

public sealed record RuntimeClassicNpcAiCatalogDiagnostic(string Code, string Message, bool Fatal);

/// <summary>
/// Typed, world-scoped projection of NPC behavior ownership from canonical generated spawns.
/// A legacy relationship sidecar can still be validated when supplied, but Classic normally
/// derives these relationships directly from Data/&lt;world&gt;/spawns.json. Native-only
/// perception, threat generation, flee routing, patrol selection, and ability priority are
/// intentionally not inferred here.
/// </summary>
public sealed class RuntimeClassicNpcAiCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicNpcAiRelationship> _bySpawnId;

    private RuntimeClassicNpcAiCatalog(
        string schemaVersion,
        int declaredNpcCount,
        int declaredSpawnCount,
        IReadOnlyDictionary<string, RuntimeClassicNpcAiRelationship> bySpawnId,
        IReadOnlyList<string> activeCapabilities,
        IReadOnlyList<string> blockedNativeCapabilities,
        IReadOnlyList<RuntimeClassicNpcAiCatalogDiagnostic> diagnostics)
    {
        SchemaVersion = schemaVersion;
        DeclaredNpcCount = declaredNpcCount;
        DeclaredSpawnCount = declaredSpawnCount;
        _bySpawnId = bySpawnId;
        ActiveCapabilities = activeCapabilities;
        BlockedNativeCapabilities = blockedNativeCapabilities;
        Diagnostics = diagnostics;
    }

    public string SchemaVersion { get; }
    public int DeclaredNpcCount { get; }
    public int DeclaredSpawnCount { get; }
    public int RelationshipCount => _bySpawnId.Count;
    public IReadOnlyList<string> ActiveCapabilities { get; }
    public IReadOnlyList<string> BlockedNativeCapabilities { get; }
    public IReadOnlyList<RuntimeClassicNpcAiCatalogDiagnostic> Diagnostics { get; }
    public bool Passed => Diagnostics.All(x => !x.Fatal);

    public RuntimeClassicNpcAiRelationship? FindBySpawnId(string spawnId) =>
        _bySpawnId.TryGetValue(spawnId, out var relationship) ? relationship : null;

    public static RuntimeClassicNpcAiCatalog Load(string path, IReadOnlyList<RuntimeNpcSpawn> generatedSpawns)
    {
        ArgumentNullException.ThrowIfNull(generatedSpawns);
        if (!File.Exists(path)) return FromGeneratedSpawns(generatedSpawns);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var diagnostics = new List<RuntimeClassicNpcAiCatalogDiagnostic>();
        var relationships = new Dictionary<string, RuntimeClassicNpcAiRelationship>(StringComparer.OrdinalIgnoreCase);
        var schemaVersion = JsonValueReader.ReadString(root, "schema_version", string.Empty);
        var declaredNpcCount = ReadInt(root, "npc_count");
        var declaredSpawnCount = ReadInt(root, "spawn_count");
        var active = ReadStringArray(root, "decision_runtime", "active");
        var blocked = ReadStringArray(root, "decision_runtime", "blocked_native");

        if (!root.TryGetProperty("relationships", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new("relationships-missing", "npc_ai_runtime.json does not contain a relationships array.", true));
        }
        else
        {
            foreach (var value in values.EnumerateArray())
            {
                var spawnId = JsonValueReader.ReadString(value, "spawn_id", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(spawnId))
                {
                    diagnostics.Add(new("relationship-missing-spawn-id", "An NPC AI relationship has no spawn_id.", true));
                    continue;
                }
                if (relationships.ContainsKey(spawnId))
                {
                    diagnostics.Add(new("relationship-duplicate-spawn-id", $"Duplicate NPC AI relationship for '{spawnId}'.", true));
                    continue;
                }

                relationships[spawnId] = new RuntimeClassicNpcAiRelationship(
                    spawnId,
                    JsonValueReader.ReadString(value, "room_ref", string.Empty),
                    ReadStringArray(value, "npc_ids"),
                    ReadInt(value, "monster_group_id"),
                    Math.Max(0, ReadInt(value, "respawn_minutes")),
                    JsonValueReader.ReadString(value, "interaction_model", string.Empty),
                    ReadStringMap(value, "owners"));
            }
        }

        var generatedIds = generatedSpawns.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var missing in generatedIds.Where(id => !relationships.ContainsKey(id)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            diagnostics.Add(new("spawn-relationship-missing", $"Generated spawn '{missing}' has no NPC AI relationship.", true));
        foreach (var extra in relationships.Keys.Where(id => !generatedIds.Contains(id)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            diagnostics.Add(new("orphan-ai-relationship", $"NPC AI relationship '{extra}' has no generated spawn.", true));

        if (declaredSpawnCount != relationships.Count)
            diagnostics.Add(new("declared-spawn-count-mismatch", $"Declared spawn_count={declaredSpawnCount}, relationships={relationships.Count}.", true));
        if (generatedSpawns.Count != relationships.Count)
            diagnostics.Add(new("generated-spawn-count-mismatch", $"Generated spawns={generatedSpawns.Count}, relationships={relationships.Count}.", true));

        foreach (var spawn in generatedSpawns)
        {
            if (!relationships.TryGetValue(spawn.Id, out var relationship)) continue;
            if (!string.IsNullOrWhiteSpace(relationship.RoomReference) &&
                !relationship.RoomReference.Equals(spawn.RoomId, StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new("room-reference-mismatch", $"{spawn.Id}: AI room '{relationship.RoomReference}' != spawn room '{spawn.RoomId}'.", true));
            if (relationship.MonsterGroupId != spawn.MonsterGroupId)
                diagnostics.Add(new("monster-group-mismatch", $"{spawn.Id}: AI group {relationship.MonsterGroupId} != spawn group {spawn.MonsterGroupId}.", true));
            if (relationship.RespawnMinutes != Math.Max(0, spawn.RespawnMinutes))
                diagnostics.Add(new("respawn-mismatch", $"{spawn.Id}: AI respawn {relationship.RespawnMinutes} != spawn respawn {spawn.RespawnMinutes}.", true));
            if (!string.IsNullOrWhiteSpace(relationship.InteractionModel) &&
                !relationship.InteractionModel.Equals(spawn.InteractionModel, StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new("interaction-model-mismatch", $"{spawn.Id}: AI interaction '{relationship.InteractionModel}' != spawn interaction '{spawn.InteractionModel}'.", true));
        }

        return new(schemaVersion, declaredNpcCount, declaredSpawnCount, relationships, active, blocked, diagnostics);
    }

    private static RuntimeClassicNpcAiCatalog FromGeneratedSpawns(IReadOnlyList<RuntimeNpcSpawn> generatedSpawns)
    {
        var relationships = generatedSpawns.ToDictionary(
            spawn => spawn.Id,
            spawn => new RuntimeClassicNpcAiRelationship(
                spawn.Id,
                string.IsNullOrWhiteSpace(spawn.ResolvedRuntimeRoomId) ? spawn.RoomId : spawn.ResolvedRuntimeRoomId,
                ReadGeneratedNpcIds(spawn),
                spawn.MonsterGroupId,
                Math.Max(0, spawn.RespawnMinutes),
                spawn.InteractionModel,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "generated-spawns",
                    ["authority"] = "Data/Classic/spawns.json",
                    ["projection_policy"] = "derived_from_canonical_generated_spawns"
                }),
            StringComparer.OrdinalIgnoreCase);

        return new(
            "derived-from-generated-spawns-v1",
            relationships.Values.SelectMany(value => value.NpcIds).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            generatedSpawns.Count,
            relationships,
            [
                "spawn_relationships",
                "home_room_identity",
                "respawn_minutes",
                "interaction_model"
            ],
            [
                "native_perception_priority",
                "native_threat_generation",
                "native_flee_routing",
                "native_patrol_selection",
                "native_ability_priority"
            ],
            []);
    }

    private static IReadOnlyList<string> ReadGeneratedNpcIds(RuntimeNpcSpawn spawn)
    {
        var ids = new List<string>();
        if (spawn.BossDefinition is not null && !string.IsNullOrWhiteSpace(spawn.BossDefinition.Id))
            ids.Add(spawn.BossDefinition.Id);
        ids.AddRange(spawn.GroupDefinitions
            .Select(definition => definition.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id)));
        return ids.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int ReadInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) && field.TryGetInt32(out var parsed) ? parsed : 0;

    private static IReadOnlyList<string> ReadStringArray(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return array.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement value, string objectProperty, string arrayProperty)
    {
        if (!value.TryGetProperty(objectProperty, out var owner) || owner.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        return ReadStringArray(owner, arrayProperty);
    }

    private static IReadOnlyDictionary<string,string> ReadStringMap(JsonElement value, string property)
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if (!value.TryGetProperty(property, out var map) || map.ValueKind != JsonValueKind.Object) return result;
        foreach (var field in map.EnumerateObject())
            if (field.Value.ValueKind == JsonValueKind.String) result[field.Name] = field.Value.GetString() ?? string.Empty;
        return result;
    }
}
