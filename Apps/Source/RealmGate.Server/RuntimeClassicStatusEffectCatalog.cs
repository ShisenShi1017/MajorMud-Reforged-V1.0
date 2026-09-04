using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicStatusEffectDefinition(
    string Id,
    string Name,
    string Classification,
    int FrequencyCount,
    IReadOnlyList<int> SpellIds,
    IReadOnlyList<int> NpcIds,
    string MappingStatus,
    string Confidence,
    bool RuntimeAuthoritative,
    string QuarantineReason);

public sealed record RuntimeClassicStatusEffectCatalogDiagnostic(string Code, string Message, bool Fatal);

/// <summary>
/// World-scoped authority for generated Data/&lt;world&gt;/status_effects.json.
/// Every decoded status-effect record is live runtime authority. Provenance metadata is
/// retained for diagnostics but cannot suppress decoded gameplay data.
/// </summary>
public sealed class RuntimeClassicStatusEffectCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicStatusEffectDefinition> _byId;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<RuntimeClassicStatusEffectDefinition>> _bySpellId;

    private RuntimeClassicStatusEffectCatalog(
        int declaredRecordCount,
        IReadOnlyDictionary<string, RuntimeClassicStatusEffectDefinition> byId,
        IReadOnlyDictionary<int, IReadOnlyList<RuntimeClassicStatusEffectDefinition>> bySpellId,
        IReadOnlyList<RuntimeClassicStatusEffectCatalogDiagnostic> diagnostics)
    {
        DeclaredRecordCount = declaredRecordCount;
        _byId = byId;
        _bySpellId = bySpellId;
        Diagnostics = diagnostics;
    }

    public int DeclaredRecordCount { get; }
    public int DefinitionCount => _byId.Count;
    public int SpellRelationshipCount => _byId.Values.Sum(x => x.SpellIds.Count);
    public int NpcRelationshipCount => _byId.Values.Sum(x => x.NpcIds.Count);
    public int RuntimeAuthoritativeCount => _byId.Values.Count(x => x.RuntimeAuthoritative);
    public int QuarantinedCount => DefinitionCount - RuntimeAuthoritativeCount;
    public IReadOnlyList<RuntimeClassicStatusEffectCatalogDiagnostic> Diagnostics { get; }
    public bool Passed => Diagnostics.All(x => !x.Fatal);

    public RuntimeClassicStatusEffectDefinition? Find(string id) =>
        _byId.TryGetValue(id, out var definition) ? definition : null;

    public IReadOnlyList<RuntimeClassicStatusEffectDefinition> FindBySpellId(int spellId) =>
        _bySpellId.TryGetValue(spellId, out var definitions) ? definitions : Array.Empty<RuntimeClassicStatusEffectDefinition>();

    public static RuntimeClassicStatusEffectCatalog Load(string statusEffectsPath, string spellsPath)
    {
        if (!File.Exists(statusEffectsPath)) throw new FileNotFoundException("Generated status-effect catalog was not found.", statusEffectsPath);
        if (!File.Exists(spellsPath)) throw new FileNotFoundException("Generated spell catalog was not found.", spellsPath);

        var diagnostics = new List<RuntimeClassicStatusEffectCatalogDiagnostic>();
        var knownSpellIds = LoadKnownLegacyIds(spellsPath);
        using var document = JsonDocument.Parse(File.ReadAllText(statusEffectsPath));
        var root = document.RootElement;
        var declared = ReadInt(root, "record_count");
        var definitions = new Dictionary<string, RuntimeClassicStatusEffectDefinition>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new("records-missing", "status_effects.json does not contain a records array.", true));
        }
        else
        {
            foreach (var value in records.EnumerateArray())
            {
                var id = JsonValueReader.ReadString(value, "id", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostics.Add(new("definition-missing-id", "A status-effect record has no id.", true));
                    continue;
                }
                if (definitions.ContainsKey(id))
                {
                    diagnostics.Add(new("definition-duplicate-id", $"Duplicate status-effect id '{id}'.", true));
                    continue;
                }

                var mappingStatus = ReadNestedString(value, "runtime_mapping", "status");
                var confidence = ReadNestedString(value, "evidence_summary_0124", "confidence");
                var spellIds = ReadRelationshipIds(value, "spells");
                var npcIds = ReadRelationshipIds(value, "npcs");
                foreach (var spellId in spellIds.Where(x => !knownSpellIds.Contains(x)).Distinct())
                    diagnostics.Add(new("unknown-spell-relationship", $"{id}: linked spell {spellId} is not present in spells.json.", true));

                // Decoded status-effect relationships are runtime authority. Missing optional
                // fields use data-defined defaults in consumers rather than quarantining the record.
                var authoritative = true;
                var reason = string.Empty;
                definitions[id] = new(
                    id,
                    JsonValueReader.ReadString(value, "name", id),
                    JsonValueReader.ReadString(value, "classification", string.Empty),
                    ReadIntString(value, "frequency_count"),
                    spellIds,
                    npcIds,
                    mappingStatus,
                    confidence,
                    authoritative,
                    reason);
            }
        }

        if (declared != definitions.Count)
            diagnostics.Add(new("declared-count-mismatch", $"Declared record_count={declared}, loaded definitions={definitions.Count}.", true));

        var bySpell = definitions.Values
            .SelectMany(definition => definition.SpellIds.Distinct().Select(spellId => (spellId, definition)))
            .GroupBy(x => x.spellId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RuntimeClassicStatusEffectDefinition>)group.Select(x => x.definition)
                    .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray());

        return new(declared, definitions, bySpell, diagnostics);
    }

    private static HashSet<int> LoadKnownLegacyIds(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Generated spell file has no records array: {path}");
        var ids = new HashSet<int>();
        foreach (var value in records.EnumerateArray())
        {
            var raw = JsonValueReader.ReadString(value, "legacy_id", string.Empty);
            if (int.TryParse(raw, out var id) && id > 0) ids.Add(id);
        }
        return ids;
    }

    private static IReadOnlyList<int> ReadRelationshipIds(JsonElement value, string property)
    {
        if (!value.TryGetProperty("relationships", out var relationships) || relationships.ValueKind != JsonValueKind.Object ||
            !relationships.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();
        var result = new List<int>();
        foreach (var item in array.EnumerateArray())
        {
            var raw = item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText();
            if (int.TryParse(raw, out var id) && id > 0) result.Add(id);
        }
        return result;
    }

    private static string ReadNestedString(JsonElement value, string owner, string property) =>
        value.TryGetProperty(owner, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? JsonValueReader.ReadString(nested, property, string.Empty)
            : string.Empty;

    private static int ReadInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) && field.TryGetInt32(out var parsed) ? parsed : 0;

    private static int ReadIntString(JsonElement value, string property) =>
        int.TryParse(JsonValueReader.ReadString(value, property, "0"), out var parsed) ? parsed : 0;
}
