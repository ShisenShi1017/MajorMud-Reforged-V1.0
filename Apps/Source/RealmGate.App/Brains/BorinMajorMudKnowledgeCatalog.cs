using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RealmGate.App.Brains;

public sealed record BorinMajorMudKnowledgeStatus(
    string State,
    int Records,
    int Relationships,
    string StoragePath,
    DateTimeOffset? UpdatedUtc,
    string Detail);

public sealed record BorinMajorMudEntity(
    string Family,
    string Id,
    string Name,
    string Summary,
    BorinKnowledgeConfidence Confidence,
    BorinKnowledgeProvenance Provenance,
    IReadOnlyList<string> Relationships);

public sealed record BorinMajorMudPathStep(
    string ExitId,
    string Direction,
    string FromRoom,
    string ToRoom,
    BorinKnowledgeConfidence Confidence);

public sealed record BorinMajorMudPathResult(
    bool Found,
    string StartRoom,
    string DestinationRoom,
    IReadOnlyList<BorinMajorMudPathStep> Steps,
    string Detail)
{
    public string Directions => Found ? string.Join(' ', Steps.Select(step => step.Direction)) : string.Empty;
}

public sealed record BorinMajorMudItemRequirements(
    string ItemId,
    string ItemName,
    int MinimumLevel,
    int MaximumLevel,
    IReadOnlyList<string> AllowedClasses,
    IReadOnlyList<string> AllowedRaces,
    IReadOnlyDictionary<string, int> MinimumStats,
    BorinKnowledgeConfidence Confidence,
    BorinKnowledgeProvenance Provenance,
    string Boundary);

public sealed record BorinMasteryEvaluationCase(
    string Category,
    string Question,
    string ExpectedEvidence,
    bool Passed,
    string Detail,
    BorinKnowledgeConfidence Confidence,
    IReadOnlyList<BorinKnowledgeProvenance> Sources);

public sealed record BorinMasteryEvaluationReport(
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    string CatalogFingerprint,
    IReadOnlyList<BorinMasteryEvaluationCase> Cases,
    string StoragePath)
{
    public int Passed => Cases.Count(item => item.Passed);
    public int Failed => Cases.Count - Passed;
    public int Score => Cases.Count == 0 ? 0 : (int)Math.Round(Passed * 100d / Cases.Count);
    public string Summary => $"{Passed}/{Cases.Count} evidence tests passed ({Score}%) • {Failed} failed";
}

public interface IBorinMajorMudKnowledgeCatalog
{
    Task<BorinMajorMudKnowledgeStatus> GetStatusAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinMajorMudKnowledgeStatus> BuildAsync(string repositoryRoot, bool changedOnly, CancellationToken token = default);
    Task<IReadOnlyList<BorinKnowledgeEvidenceItem>> SearchAsync(
        string repositoryRoot,
        BorinKnowledgeQuery query,
        int maximumResults,
        CancellationToken token = default);
    Task<BorinMajorMudEntity?> GetEntityAsync(
        string repositoryRoot,
        string family,
        string idOrName,
        CancellationToken token = default);
    Task<IReadOnlyList<BorinMajorMudEntity>> GetSampleEntitiesAsync(
        string repositoryRoot,
        string family,
        int maximumResults = 5,
        CancellationToken token = default);
    Task<BorinMajorMudEntity?> GetExitAsync(
        string repositoryRoot,
        string fromRoom,
        string direction,
        CancellationToken token = default);
    Task<IReadOnlyList<BorinMajorMudEntity>> GetRelatedAsync(
        string repositoryRoot,
        string id,
        int maximumResults = 20,
        CancellationToken token = default);
    Task<BorinMajorMudPathResult> FindPathAsync(
        string repositoryRoot,
        string startRoom,
        string destinationRoom,
        int maximumHops = 100,
        CancellationToken token = default);
    Task<IReadOnlyList<BorinMajorMudEntity>> FindAcquisitionSourcesAsync(
        string repositoryRoot,
        string itemIdOrName,
        CancellationToken token = default);
    Task<IReadOnlyList<BorinMajorMudEntity>> FindNpcSpawnsAsync(
        string repositoryRoot,
        string npcIdOrName,
        CancellationToken token = default);
    Task<BorinMajorMudItemRequirements?> GetItemRequirementsAsync(
        string repositoryRoot,
        string itemIdOrNameOrQuestion,
        CancellationToken token = default);
    Task<BorinMasteryEvaluationReport> RunMasteryEvaluationAsync(
        string repositoryRoot,
        CancellationToken token = default);
    Task<IReadOnlyList<BorinMajorMudEntity>> TraceDependenciesAsync(
        string repositoryRoot,
        string idNameOrQuestion,
        int maximumDepth = 3,
        CancellationToken token = default);
}

/// <summary>
/// A reference index over generated Classic data. Full game records remain authoritative in
/// Data/Classic and Librarium; this catalog stores only searchable identities, concise summaries,
/// source pointers, and relationship edges.
/// </summary>
public sealed class BorinMajorMudKnowledgeCatalog : IBorinMajorMudKnowledgeCatalog
{
    private const int CatalogVersion = 7;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
    private static readonly Regex Terms = new("[A-Za-z0-9_.:/-]{2,}", RegexOptions.Compiled);
    private static readonly string[] Families =
    [
        "abilities", "ability_acquisitions", "classes", "commands", "economy_rules", "equipment",
        "exits", "formulas", "gang_houses", "items", "loot_rules", "majormud_plus", "npcs",
        "quests", "races", "room_interactions", "room_item_placements", "rooms", "scripts", "shops",
        "skills", "spawns", "spells", "status_effects", "sysop_commands", "triggers", "worlds"
    ];
    private static readonly string[] SummaryFields =
    [
        "id", "legacy_id", "name", "short_name", "object_type", "classification", "description",
        "room_ref", "qualified_room_ref", "npc_name", "npc_ref", "item_ref", "spell_ref", "service_kind",
        "trigger_type", "script_type", "quest_flag_name", "minimum_abbreviation", "direction", "gate_kind",
        "runtime_disposition", "status", "confidence"
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _cacheSync = new();
    private CatalogDocument? _cache;
    private string _cachePath = string.Empty;
    private long _cacheTicks;
    private readonly Dictionary<string, IReadOnlyList<BorinKnowledgeEvidenceItem>> _queryCache = new(StringComparer.Ordinal);
    private readonly Queue<string> _queryCacheOrder = new();

    public async Task<BorinMajorMudKnowledgeStatus> GetStatusAsync(string repositoryRoot, CancellationToken token = default)
    {
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path))
            return new("Not built", 0, 0, path, null, "Run Learn RealmGate to index generated Classic knowledge.");
        var document = await LoadAsync(path, token);
        return Status(document, path, "Generated Classic reference catalog is ready.");
    }

    public async Task<BorinMajorMudKnowledgeStatus> BuildAsync(
        string repositoryRoot,
        bool changedOnly,
        CancellationToken token = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var dataRoot = Path.Combine(root, "Data", "Classic");
        if (!Directory.Exists(dataRoot))
            return new("Unavailable", 0, 0, GetIndexPath(root), null, "Data/Classic was not found.");

        await _gate.WaitAsync(token);
        try
        {
            var sources = ResolveSources(dataRoot).ToArray();
            var fingerprint = Fingerprint(sources);
            var path = GetIndexPath(root);
            if (changedOnly && File.Exists(path))
            {
                var current = await LoadAsync(path, token);
                if (current.Version == CatalogVersion &&
                    current.SourceFingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase))
                    return Status(current, path, "Generated Classic data is unchanged; existing references were reused.");
            }

            var document = new CatalogDocument
            {
                Version = CatalogVersion,
                RepositoryRoot = root,
                SourceFingerprint = fingerprint,
                UpdatedUtc = DateTimeOffset.UtcNow,
            };
            foreach (var source in sources)
            {
                token.ThrowIfCancellationRequested();
                await IndexSourceAsync(root, source, document, token);
            }
            document.RelationshipCount = document.Records.Sum(record => record.Relationships.Count);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(document, JsonOptions), token);
            File.Move(temporary, path, true);
            SetCache(path, document);
            return Status(document, path, "Generated Classic identities and relationships were indexed by reference.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<BorinKnowledgeEvidenceItem>> SearchAsync(
        string repositoryRoot,
        BorinKnowledgeQuery query,
        int maximumResults,
        CancellationToken token = default)
    {
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return [];
        var document = await GetCachedAsync(path, token);
        var cacheKey = $"{document.SourceFingerprint}|{query.Mode}|{maximumResults}|{query.Text.Trim().ToLowerInvariant()}";
        lock (_cacheSync)
            if (_queryCache.TryGetValue(cacheKey, out var cached)) return cached;
        var terms = Tokenize(query.Text);
        if (terms.Count == 0) return [];
        var results = new List<BorinKnowledgeEvidenceItem>();
        foreach (var record in document.Records)
        {
            token.ThrowIfCancellationRequested();
            var exact = terms.Any(term => record.Id.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                                          record.Name.Equals(term, StringComparison.OrdinalIgnoreCase));
            var textHits = terms.Count(term => record.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase));
            var edgeHits = record.Relationships.Count(edge => terms.Any(term => edge.Contains(term, StringComparison.OrdinalIgnoreCase)));
            if (query.Mode == BorinKnowledgeSearchMode.Exact && !exact) continue;
            if (query.Mode == BorinKnowledgeSearchMode.Relationship && edgeHits == 0 && !exact) continue;
            if (!exact && textHits == 0 && edgeHits == 0) continue;
            var score = (exact ? 100d : 0d) + textHits * 10d + edgeHits * 8d;
            var confidence = MapConfidence(record.Confidence);
            var relationship = edgeHits > 0
                ? string.Join("; ", record.Relationships.Where(edge => terms.Any(term => edge.Contains(term, StringComparison.OrdinalIgnoreCase))).Take(8))
                : record.Relationships.Count > 0 ? $"{record.Relationships.Count} authoritative relationship(s) available" : string.Empty;
            results.Add(new BorinKnowledgeEvidenceItem(
                record.RelativePath,
                1,
                1,
                BorinKnowledgeAuthority.GeneratedClassic,
                record.Summary,
                score,
                confidence,
                [new BorinKnowledgeProvenance("Generated Classic", record.RelativePath, 1, 1, document.SourceFingerprint)],
                relationship));
        }
        var ranked = results.OrderByDescending(result => result.Score)
            .ThenBy(result => result.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximumResults, 1, 20))
            .ToArray();
        lock (_cacheSync)
        {
            _queryCache[cacheKey] = ranked;
            _queryCacheOrder.Enqueue(cacheKey);
            while (_queryCacheOrder.Count > 32) _queryCache.Remove(_queryCacheOrder.Dequeue());
        }
        return ranked;
    }

    public async Task<BorinMajorMudEntity?> GetEntityAsync(
        string repositoryRoot,
        string family,
        string idOrName,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(idOrName)) return null;
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return null;
        var document = await GetCachedAsync(path, token);
        var normalizedFamily = NormalizeFamily(family);
        var record = document.Records.FirstOrDefault(item =>
            item.Family.Equals(normalizedFamily, StringComparison.OrdinalIgnoreCase) &&
            (item.Id.Equals(idOrName.Trim(), StringComparison.OrdinalIgnoreCase) ||
             item.Name.Equals(idOrName.Trim(), StringComparison.OrdinalIgnoreCase)));
        return record is null ? null : ToEntity(document, record);
    }

    public async Task<IReadOnlyList<BorinMajorMudEntity>> GetSampleEntitiesAsync(
        string repositoryRoot,
        string family,
        int maximumResults = 5,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(family)) return [];
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return [];
        var document = await GetCachedAsync(path, token);
        var normalized = NormalizeFamily(family);
        return document.Records.Where(item => item.Family.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximumResults, 1, 20))
            .Select(item => ToEntity(document, item)).ToArray();
    }

    public async Task<BorinMajorMudEntity?> GetExitAsync(
        string repositoryRoot,
        string fromRoom,
        string direction,
        CancellationToken token = default)
    {
        var room = NormalizeRoomId(fromRoom);
        var canonicalDirection = (direction ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W",
            "NORTHEAST" => "NE", "NORTHWEST" => "NW", "SOUTHEAST" => "SE", "SOUTHWEST" => "SW",
            "UP" => "U", "DOWN" => "D", var value => value,
        };
        if (room.Length == 0 || canonicalDirection.Length == 0) return null;
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return null;
        var document = await GetCachedAsync(path, token);
        var record = document.Records.FirstOrDefault(item => item.Family.Equals("exits", StringComparison.OrdinalIgnoreCase) &&
                                                             item.FromRoom.Equals(room, StringComparison.OrdinalIgnoreCase) &&
                                                             item.Direction.Equals(canonicalDirection, StringComparison.OrdinalIgnoreCase));
        return record is null ? null : ToEntity(document, record);
    }

    public async Task<IReadOnlyList<BorinMajorMudEntity>> GetRelatedAsync(
        string repositoryRoot,
        string id,
        int maximumResults = 20,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return [];
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return [];
        var document = await GetCachedAsync(path, token);
        var needle = id.Trim();
        return document.Records
            .Where(record => record.Id.Equals(needle, StringComparison.OrdinalIgnoreCase) ||
                             record.Relationships.Any(edge => RelationshipTargets(edge, needle)))
            .OrderBy(record => record.Id.Equals(needle, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(record => record.Family, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximumResults, 1, 100))
            .Select(record => ToEntity(document, record))
            .ToArray();
    }

    public async Task<BorinMajorMudPathResult> FindPathAsync(
        string repositoryRoot,
        string startRoom,
        string destinationRoom,
        int maximumHops = 100,
        CancellationToken token = default)
    {
        var start = NormalizeRoomId(startRoom);
        var destination = NormalizeRoomId(destinationRoom);
        if (start.Length == 0 || destination.Length == 0)
            return new(false, startRoom, destinationRoom, [], "Use a map-qualified room ID such as room.map1.1847 or 1/1847.");
        if (start.Equals(destination, StringComparison.OrdinalIgnoreCase))
            return new(true, start, destination, [], "Start and destination are the same authoritative room.");
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return new(false, start, destination, [], "MajorMUD knowledge catalog is not built.");
        var document = await GetCachedAsync(path, token);
        var exits = document.Records.Where(record => record.Family.Equals("exits", StringComparison.OrdinalIgnoreCase) &&
                                                     record.FromRoom.Length > 0 && record.ToRoom.Length > 0)
            .GroupBy(record => record.FromRoom, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var prior = new Dictionary<string, CatalogRecord>(StringComparer.OrdinalIgnoreCase);
        var distance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [start] = 0 };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var room = queue.Dequeue();
            var hops = distance[room];
            if (hops >= Math.Clamp(maximumHops, 1, 500) || !exits.TryGetValue(room, out var candidates)) continue;
            foreach (var exit in candidates)
            {
                if (distance.ContainsKey(exit.ToRoom)) continue;
                distance[exit.ToRoom] = hops + 1;
                prior[exit.ToRoom] = exit;
                if (exit.ToRoom.Equals(destination, StringComparison.OrdinalIgnoreCase))
                    return BuildPath(start, destination, prior);
                queue.Enqueue(exit.ToRoom);
            }
        }
        return new(false, start, destination, [], $"No route was proven within {Math.Clamp(maximumHops, 1, 500)} exits. Locked, scripted, or conditional traversal may require separate validation.");
    }

    public async Task<IReadOnlyList<BorinMajorMudEntity>> FindAcquisitionSourcesAsync(
        string repositoryRoot,
        string itemIdOrName,
        CancellationToken token = default)
    {
        var item = await GetEntityAsync(repositoryRoot, "items", itemIdOrName, token);
        if (item is null)
        {
            var path = GetIndexPath(repositoryRoot);
            if (!File.Exists(path)) return [];
            var document = await GetCachedAsync(path, token);
            var mention = itemIdOrName.Trim();
            var candidate = document.Records.Where(record => record.Family.Equals("items", StringComparison.OrdinalIgnoreCase) &&
                                                              record.Name.Length >= 3 &&
                                                              mention.Contains(record.Name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.Name.Length)
                .FirstOrDefault();
            if (candidate is not null) item = ToEntity(document, candidate);
        }
        if (item is null) return [];
        var related = await GetRelatedAsync(repositoryRoot, item.Id, 100, token);
        return related.Where(entity => entity.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase) ||
                                       entity.Family is "shops" or "npcs" or "quests" or "room_item_placements" or "scripts")
            .ToArray();
    }

    public async Task<IReadOnlyList<BorinMajorMudEntity>> FindNpcSpawnsAsync(
        string repositoryRoot,
        string npcIdOrName,
        CancellationToken token = default)
    {
        var npc = await GetEntityAsync(repositoryRoot, "npcs", npcIdOrName, token);
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return [];
        var document = await GetCachedAsync(path, token);
        if (npc is null)
        {
            var mention = npcIdOrName.Trim();
            var candidate = document.Records.Where(record => record.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase) &&
                                                              record.Name.Length >= 3 &&
                                                              mention.Contains(record.Name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.Name.Length)
                .FirstOrDefault();
            if (candidate is not null) npc = ToEntity(document, candidate);
        }
        var name = npc?.Name ?? npcIdOrName.Trim();
        return document.Records
            .Where(record => record.Family.Equals("spawns", StringComparison.OrdinalIgnoreCase) &&
                             record.NpcName.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(record => ToEntity(document, record))
            .ToArray();
    }

    public async Task<BorinMajorMudItemRequirements?> GetItemRequirementsAsync(
        string repositoryRoot,
        string itemIdOrNameOrQuestion,
        CancellationToken token = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var itemPath = Path.Combine(root, "Data", "Classic", "items.json");
        if (!File.Exists(itemPath)) return null;
        await using var stream = File.OpenRead(itemPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (!document.RootElement.TryGetProperty("records", out var records)) return null;
        var mention = itemIdOrNameOrQuestion.Trim();
        JsonElement? selected = null;
        foreach (var record in records.EnumerateArray())
        {
            var id = Scalar(record, "id");
            var name = Scalar(record, "name");
            if (id.Equals(mention, StringComparison.OrdinalIgnoreCase) || name.Equals(mention, StringComparison.OrdinalIgnoreCase))
            {
                selected = record.Clone();
                break;
            }
            if (name.Length >= 3 && mention.Contains(name, StringComparison.OrdinalIgnoreCase) &&
                (selected is null || name.Length > Scalar(selected.Value, "name").Length)) selected = record.Clone();
        }
        if (selected is null) return null;
        var item = selected.Value;
        if (!item.TryGetProperty("public_reference_0139", out var reference) ||
            !reference.TryGetProperty("fields", out var fields)) return null;
        var classNumbers = ReadIntValues(fields, "class_restrictions");
        var raceNumbers = ReadIntValues(fields, "race_restrictions");
        var classNames = await ResolveLegacyNamesAsync(root, "classes", classNumbers, token);
        var raceNames = await ResolveLegacyNamesAsync(root, "races", raceNumbers, token);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (fields.TryGetProperty("stat_requirements", out var requirements) && requirements.ValueKind == JsonValueKind.Object)
            foreach (var property in requirements.EnumerateObject())
                if (property.Value.TryGetInt32(out var value) && value > 0) stats[property.Name.ToUpperInvariant()] = value;
        var minimumLevel = 0;
        var maximumLevel = 0;
        if (fields.TryGetProperty("abilities_named", out var abilities) && abilities.ValueKind == JsonValueKind.Array)
        {
            foreach (var ability in abilities.EnumerateArray())
            {
                var abilityId = Scalar(ability, "ability_id");
                var name = Scalar(ability, "name");
                var value = Scalar(ability, "value");
                if ((abilityId == "135" || name.Equals("MinLevel", StringComparison.OrdinalIgnoreCase)) && int.TryParse(value, out var minimum)) minimumLevel = minimum;
                if ((abilityId == "136" || name.Equals("MaxLevel", StringComparison.OrdinalIgnoreCase)) && int.TryParse(value, out var maximum)) maximumLevel = maximum;
            }
        }
        var relative = Path.GetRelativePath(root, itemPath).Replace('\\', '/');
        return new BorinMajorMudItemRequirements(
            Scalar(item, "id"),
            Scalar(item, "name"),
            minimumLevel,
            maximumLevel,
            classNames,
            raceNames,
            stats,
            BorinKnowledgeConfidence.Proven,
            new BorinKnowledgeProvenance("Generated Classic item eligibility", relative, 1, 1, "current generated catalog"),
            "These are decoded character eligibility inputs. Weapon/armour category compatibility, ownership, live equipment state, and temporary effects require runtime validation.");
    }

    public async Task<BorinMasteryEvaluationReport> RunMasteryEvaluationAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        var started = DateTimeOffset.UtcNow;
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) throw new InvalidOperationException("Build MajorMUD knowledge before running mastery evaluation.");
        var document = await GetCachedAsync(path, token);
        var cases = new List<BorinMasteryEvaluationCase>();
        foreach (var (family, category) in new[]
                 {
                     ("commands", "MajorMUD Commands"), ("rooms", "Rooms"), ("items", "Items"),
                     ("npcs", "Monsters/NPCs"), ("spells", "Spells"), ("quests", "Quests"),
                     ("scripts", "Scripts"), ("triggers", "Triggers")
                 })
        {
            token.ThrowIfCancellationRequested();
            var sample = document.Records.FirstOrDefault(record => record.Family.Equals(family, StringComparison.OrdinalIgnoreCase));
            if (sample is null) continue;
            var evidence = await SearchAsync(repositoryRoot, new BorinKnowledgeQuery(sample.Id, BorinKnowledgeSearchMode.Exact, 5), 5, token);
            var match = evidence.FirstOrDefault(item => item.Text.Contains($"ID: {sample.Id}", StringComparison.OrdinalIgnoreCase));
            cases.Add(new BorinMasteryEvaluationCase(
                category,
                $"Retrieve authoritative {family} record {sample.Id}.",
                sample.Id,
                match is not null,
                match is null ? "Exact retrieval did not return the expected generated identity." : $"Retrieved {match.Location} with {match.Confidence.ToDisplayText()} confidence.",
                match?.Confidence ?? BorinKnowledgeConfidence.Unknown,
                match?.Sources ?? []));
        }

        foreach (var (family, category, marker) in new[]
                 {
                     ("npcs", "Combat Mechanics", "combat:"),
                     ("spells", "Spell Mechanics", "effect:"),
                     ("spawns", "Spawn/Reset Timing", "spawn_model:"),
                     ("rooms", "Room Runtime Mechanics", "environment:"),
                 })
        {
            var sample = document.Records.FirstOrDefault(record => record.Family.Equals(family, StringComparison.OrdinalIgnoreCase) &&
                                                                 record.Summary.Contains(marker, StringComparison.OrdinalIgnoreCase));
            if (sample is null) continue;
            var evidence = await SearchAsync(repositoryRoot, new BorinKnowledgeQuery(sample.Id, BorinKnowledgeSearchMode.Exact, 5), 5, token);
            var match = evidence.FirstOrDefault(item => item.Text.Contains(marker, StringComparison.OrdinalIgnoreCase));
            cases.Add(new BorinMasteryEvaluationCase(category, $"Retrieve {marker.TrimEnd(':')} evidence for {sample.Id}.", marker,
                match is not null, match is null ? $"The {family} result omitted {marker} evidence." : $"Retrieved typed {marker.TrimEnd(':')} evidence.",
                match?.Confidence ?? BorinKnowledgeConfidence.Unknown, match?.Sources ?? []));
        }

        var ids = document.Records.Select(record => record.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relationshipSample = document.Records.Select(record => new
            {
                Record = record,
                Target = record.Relationships.SelectMany(ExtractCanonicalIds).FirstOrDefault(ids.Contains)
            })
            .FirstOrDefault(candidate => candidate.Target is not null);
        if (relationshipSample?.Target is not null)
        {
            var related = await GetRelatedAsync(repositoryRoot, relationshipSample.Target, 100, token);
            var passed = related.Any(entity => entity.Id.Equals(relationshipSample.Record.Id, StringComparison.OrdinalIgnoreCase));
            cases.Add(new BorinMasteryEvaluationCase(
                "Relationships",
                $"Find records related to {relationshipSample.Target}.",
                relationshipSample.Record.Id,
                passed,
                passed ? "Reverse relationship retrieval returned the expected source record." : "Reverse relationship retrieval missed the expected source record.",
                passed ? MapConfidence(relationshipSample.Record.Confidence) : BorinKnowledgeConfidence.Unknown,
                passed ? [ToEntity(document, relationshipSample.Record).Provenance] : []));
        }

        var exit = document.Records.FirstOrDefault(record => record.Family.Equals("exits", StringComparison.OrdinalIgnoreCase) &&
                                                             record.FromRoom.Length > 0 && record.ToRoom.Length > 0);
        if (exit is not null)
        {
            var route = await FindPathAsync(repositoryRoot, exit.FromRoom, exit.ToRoom, 1, token);
            var passed = route.Found && route.Steps.Count == 1 && route.Steps[0].ExitId.Equals(exit.Id, StringComparison.OrdinalIgnoreCase);
            cases.Add(new BorinMasteryEvaluationCase(
                "Navigation",
                $"Find the direct authoritative route from {exit.FromRoom} to {exit.ToRoom}.",
                exit.Id,
                passed,
                passed ? $"Returned direction {route.Directions} through {exit.Id}." : route.Detail,
                passed ? MapConfidence(exit.Confidence) : BorinKnowledgeConfidence.Unknown,
                passed ? [ToEntity(document, exit).Provenance] : []));
        }

        var completed = DateTimeOffset.UtcNow;
        var reportPath = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory", "Knowledge", "borin-mastery-evaluation.json");
        var report = new BorinMasteryEvaluationReport(started, completed, document.SourceFingerprint, cases, reportPath);
        var temporary = reportPath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), token);
        File.Move(temporary, reportPath, true);
        return report;
    }

    public async Task<IReadOnlyList<BorinMajorMudEntity>> TraceDependenciesAsync(
        string repositoryRoot,
        string idNameOrQuestion,
        int maximumDepth = 3,
        CancellationToken token = default)
    {
        var path = GetIndexPath(repositoryRoot);
        if (!File.Exists(path)) return [];
        var document = await GetCachedAsync(path, token);
        var mention = idNameOrQuestion.Trim();
        var start = document.Records.FirstOrDefault(record => record.Id.Equals(mention, StringComparison.OrdinalIgnoreCase) ||
                                                              record.Name.Equals(mention, StringComparison.OrdinalIgnoreCase))
                    ?? document.Records.Where(record => record.Name.Length >= 3 && mention.Contains(record.Name, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(record => record.Name.Length).FirstOrDefault();
        if (start is null) return [];
        var byId = document.Records.GroupBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var found = new Dictionary<string, CatalogRecord>(StringComparer.OrdinalIgnoreCase) { [start.Id] = start };
        var queue = new Queue<(CatalogRecord Record, int Depth)>();
        queue.Enqueue((start, 0));
        var depthLimit = Math.Clamp(maximumDepth, 1, 6);
        while (queue.Count > 0 && found.Count < 100)
        {
            token.ThrowIfCancellationRequested();
            var (current, depth) = queue.Dequeue();
            if (depth >= depthLimit) continue;
            var neighborIds = current.Relationships.SelectMany(ExtractCanonicalIds).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var incoming in document.Records.Where(record => record.Relationships.Any(edge => RelationshipTargets(edge, current.Id))))
                neighborIds.Add(incoming.Id);
            foreach (var id in neighborIds)
            {
                if (!byId.TryGetValue(id, out var neighbor) || !found.TryAdd(neighbor.Id, neighbor)) continue;
                queue.Enqueue((neighbor, depth + 1));
            }
        }
        return found.Values.Select(record => ToEntity(document, record)).ToArray();
    }

    private static async Task IndexSourceAsync(
        string repositoryRoot,
        SourceFile source,
        CatalogDocument catalog,
        CancellationToken token)
    {
        await using var stream = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            return;
        var index = 0;
        foreach (var record in records.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            index++;
            if (record.ValueKind != JsonValueKind.Object) continue;
            var id = Scalar(record, "id");
            var name = Scalar(record, "name");
            if (id.Length == 0) id = $"{source.Family}.{index}";
            if (name.Length == 0) name = id;
            var confidence = Scalar(record, "confidence");
            var relationships = ExtractRelationships(record)
                .Concat(ExtractSpecialRelationships(source.Family, record))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(64)
                .ToList();
            var summary = BuildSummary(source.Family, record, id, name);
            var fromRoom = RelationshipValue(record, "from_room");
            var toRoom = RelationshipValue(record, "to_room");
            var direction = Scalar(record, "direction");
            var npcName = Scalar(record, "npc_name");
            var relative = Path.GetRelativePath(repositoryRoot, source.Path).Replace('\\', '/');
            catalog.Records.Add(new CatalogRecord(
                source.Family,
                id,
                name,
                relative,
                confidence,
                summary,
                relationships,
                fromRoom,
                toRoom,
                direction,
                npcName));
        }
    }

    private static string BuildSummary(string family, JsonElement record, string id, string name)
    {
        var values = new List<string> { $"Family: {family}", $"ID: {id}", $"Name: {name}" };
        foreach (var field in SummaryFields)
        {
            if (field is "id" or "name") continue;
            var value = Scalar(record, field);
            if (value.Length > 0) values.Add($"{field}: {Limit(value, 240)}");
        }
        foreach (var field in family switch
                 {
                     "spells" => new[] { "classification", "costs", "effect", "abilities" },
                     "npcs" => new[] { "combat", "behavior", "rewards", "attacks", "abilities" },
                     "spawns" => new[] { "spawn_model" },
                     "rooms" => new[] { "environment" },
                     "status_effects" => new[] { "duration", "timing", "effects" },
                     "quests" => new[] { "script_flow", "ability_checks", "ability_mutations", "item_actions" },
                     _ => Array.Empty<string>(),
                 })
        {
            if (record.TryGetProperty(field, out var value) && value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                values.Add($"{field}: {Limit(value.GetRawText(), 700)}");
        }
        return string.Join("; ", values);
    }

    private static IEnumerable<string> ExtractRelationships(JsonElement record)
    {
        foreach (var property in record.EnumerateObject())
        {
            var directRelationship = property.Name.Equals("relationships", StringComparison.OrdinalIgnoreCase);
            var objectReference = property.Name.EndsWith("_ref", StringComparison.OrdinalIgnoreCase) ||
                                  property.Name.EndsWith("_refs", StringComparison.OrdinalIgnoreCase);
            var provenanceReference = property.Name.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
                                      property.Name.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                                      property.Name.Contains("formula", StringComparison.OrdinalIgnoreCase) ||
                                      property.Name.Contains("public", StringComparison.OrdinalIgnoreCase) ||
                                      property.Name.Contains("runtime", StringComparison.OrdinalIgnoreCase) ||
                                      property.Name.Contains("generator", StringComparison.OrdinalIgnoreCase);
            if (!directRelationship && (!objectReference || provenanceReference)) continue;
            foreach (var value in Flatten(property.Value))
                yield return $"{property.Name} -> {value}";
        }
    }

    private static IEnumerable<string> ExtractSpecialRelationships(string family, JsonElement record)
    {
        if (family.Equals("shops", StringComparison.OrdinalIgnoreCase) &&
            record.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in inventory.EnumerateArray())
            {
                var item = Scalar(entry, "item_ref");
                if (item.Length > 0) yield return $"inventory.item_ref -> {item}";
            }
        }

        if (family.Equals("npcs", StringComparison.OrdinalIgnoreCase) &&
            record.TryGetProperty("public_reference_0139", out var publicReference) &&
            publicReference.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty("loot_named", out var loot) && loot.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            var entries = loot.ValueKind == JsonValueKind.Array ? loot.EnumerateArray().ToArray() : [loot];
            foreach (var entry in entries)
            {
                var itemId = Scalar(entry, "item_id");
                if (itemId.Length > 0) yield return $"loot.item_ref -> item.{itemId}";
                var itemName = Scalar(entry, "name");
                if (itemName.Length > 0) yield return $"loot.name -> {itemName}";
                var percent = Scalar(entry, "drop_percent");
                if (percent.Length > 0) yield return $"loot.drop_percent -> {percent}";
            }
        }

        if (family.Equals("spawns", StringComparison.OrdinalIgnoreCase))
        {
            var npcName = Scalar(record, "npc_name");
            var room = Scalar(record, "qualified_room_ref");
            if (npcName.Length > 0) yield return $"npc_name -> {npcName}";
            if (room.Length > 0) yield return $"spawn_room -> {room}";
        }

        foreach (var containerName in new[] { "item_actions", "stages", "restrictions", "requirements" })
        {
            if (!record.TryGetProperty(containerName, out var container)) continue;
            foreach (var reference in ExtractNestedReferences(container, containerName, 0)) yield return reference;
        }
    }

    private static IEnumerable<string> ExtractNestedReferences(JsonElement element, string path, int depth)
    {
        if (depth > 6) yield break;
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                foreach (var value in ExtractNestedReferences(child, path, depth + 1)) yield return value;
            yield break;
        }
        if (element.ValueKind != JsonValueKind.Object) yield break;
        foreach (var property in element.EnumerateObject())
        {
            var childPath = $"{path}.{property.Name}";
            if (property.Name.EndsWith("_ref", StringComparison.OrdinalIgnoreCase) ||
                property.Name.EndsWith("_refs", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("item_id", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var value in Flatten(property.Value)) yield return $"{childPath} -> {value}";
            }
            else
            {
                foreach (var value in ExtractNestedReferences(property.Value, childPath, depth + 1)) yield return value;
            }
        }
    }

    private static IEnumerable<string> Flatten(JsonElement value, string prefix = "")
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                    foreach (var child in Flatten(property.Value, prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}"))
                        yield return child;
                break;
            case JsonValueKind.Array:
                foreach (var element in value.EnumerateArray())
                    foreach (var child in Flatten(element, prefix)) yield return child;
                break;
            case JsonValueKind.String:
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text)) yield return prefix.Length == 0 ? text : $"{prefix}={text}";
                break;
            case JsonValueKind.Number:
                yield return prefix.Length == 0 ? value.GetRawText() : $"{prefix}={value.GetRawText()}";
                break;
        }
    }

    private static string Scalar(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return string.Empty;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => string.Empty,
        };
    }

    private static IReadOnlyList<int> ReadIntValues(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var values)) return [];
        if (values.ValueKind == JsonValueKind.Number && values.TryGetInt32(out var single)) return [single];
        return values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Where(value => value.TryGetInt32(out _)).Select(value => value.GetInt32()).Distinct().ToArray()
            : [];
    }

    private static async Task<IReadOnlyList<string>> ResolveLegacyNamesAsync(
        string root,
        string family,
        IReadOnlyList<int> numbers,
        CancellationToken token)
    {
        if (numbers.Count == 0) return [];
        var path = Path.Combine(root, "Data", "Classic", family + ".json");
        if (!File.Exists(path)) return numbers.Select(number => $"{family.TrimEnd('s')} #{number}").ToArray();
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var names = new Dictionary<int, string>();
        if (document.RootElement.TryGetProperty("records", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                if (!record.TryGetProperty("sourceNumbers", out var sourceNumbers) ||
                    !sourceNumbers.TryGetProperty("number", out var number) || !number.TryGetInt32(out var legacy)) continue;
                names[legacy] = Scalar(record, "name");
            }
        }
        return numbers.Select(number => names.TryGetValue(number, out var name) && name.Length > 0
            ? $"{name} (#{number})"
            : $"{family.TrimEnd('s')} #{number}").ToArray();
    }

    private static string RelationshipValue(JsonElement element, string name)
    {
        if (!element.TryGetProperty("relationships", out var relationships) || relationships.ValueKind != JsonValueKind.Object)
            return string.Empty;
        return Scalar(relationships, name);
    }

    private static BorinMajorMudPathResult BuildPath(
        string start,
        string destination,
        IReadOnlyDictionary<string, CatalogRecord> prior)
    {
        var reversed = new List<BorinMajorMudPathStep>();
        var current = destination;
        while (!current.Equals(start, StringComparison.OrdinalIgnoreCase))
        {
            if (!prior.TryGetValue(current, out var exit))
                return new(false, start, destination, [], "The route predecessor chain was incomplete.");
            reversed.Add(new BorinMajorMudPathStep(exit.Id, exit.Direction, exit.FromRoom, exit.ToRoom, MapConfidence(exit.Confidence)));
            current = exit.FromRoom;
        }
        reversed.Reverse();
        return new(true, start, destination, reversed,
            $"Found a {reversed.Count}-exit route through authoritative generated topology. Door, lock, script, and runtime state must still be validated before movement.");
    }

    private static BorinMajorMudEntity ToEntity(CatalogDocument document, CatalogRecord record) =>
        new(record.Family, record.Id, record.Name, record.Summary, MapConfidence(record.Confidence),
            new BorinKnowledgeProvenance("Generated Classic", record.RelativePath, 1, 1, document.SourceFingerprint),
            record.Relationships);

    private static bool RelationshipTargets(string edge, string id) =>
        edge.EndsWith("=" + id, StringComparison.OrdinalIgnoreCase) ||
        edge.EndsWith("-> " + id, StringComparison.OrdinalIgnoreCase) ||
        edge.Contains("=" + id + ".", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ExtractCanonicalIds(string edge)
    {
        foreach (Match match in Regex.Matches(edge,
                     @"\b(?:room|exit|item|npc|quest|script|textblock|spell|trigger|shop|class|race|spawn|ability)\.[A-Za-z0-9_.-]+",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            yield return match.Value;
    }

    private static string NormalizeFamily(string family)
    {
        var normalized = family.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized switch
        {
            "room" => "rooms", "exit" => "exits", "monster" or "npc" => "npcs", "item" => "items",
            "spell" => "spells", "class" => "classes", "race" => "races", "quest" => "quests",
            "command" => "commands", "script" => "scripts", "trigger" => "triggers", "shop" => "shops",
            "spawn" => "spawns", "ability" => "abilities", _ => normalized,
        };
    }

    private static string NormalizeRoomId(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Regex.IsMatch(normalized, @"^room\.map\d+\.\d+$")) return normalized;
        var match = Regex.Match(normalized, @"^(?:map)?(\d+)\s*[/.:,-]\s*(?:room)?(\d+)$");
        return match.Success ? $"room.map{match.Groups[1].Value}.{match.Groups[2].Value}" : string.Empty;
    }

    private static IEnumerable<SourceFile> ResolveSources(string dataRoot)
    {
        foreach (var family in Families)
        {
            var manifest = Path.Combine(dataRoot, family + ".json");
            if (!File.Exists(manifest)) continue;
            using var stream = File.OpenRead(manifest);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("record_parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    var relative = part.GetString();
                    if (string.IsNullOrWhiteSpace(relative)) continue;
                    var path = Path.GetFullPath(Path.Combine(dataRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (File.Exists(path)) yield return new SourceFile(family, path);
                }
            }
            else
            {
                yield return new SourceFile(family, manifest);
            }
        }
    }

    private static string Fingerprint(IEnumerable<SourceFile> sources)
    {
        var value = string.Join('|', sources.Select(source =>
        {
            var info = new FileInfo(source.Path);
            return $"{source.Family}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
        }));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private async Task<CatalogDocument> GetCachedAsync(string path, CancellationToken token)
    {
        var info = new FileInfo(path);
        lock (_cacheSync)
        {
            if (_cache is not null && _cachePath.Equals(path, StringComparison.OrdinalIgnoreCase) && _cacheTicks == info.LastWriteTimeUtc.Ticks)
                return _cache;
        }
        var loaded = await LoadAsync(path, token);
        SetCache(path, loaded);
        return loaded;
    }

    private void SetCache(string path, CatalogDocument document)
    {
        var info = new FileInfo(path);
        lock (_cacheSync)
        {
            _cache = document;
            _cachePath = path;
            _cacheTicks = info.Exists ? info.LastWriteTimeUtc.Ticks : 0;
            _queryCache.Clear();
            _queryCacheOrder.Clear();
        }
    }

    private static async Task<CatalogDocument> LoadAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CatalogDocument>(stream, JsonOptions, token) ?? new CatalogDocument();
    }

    private static BorinKnowledgeConfidence MapConfidence(string value)
    {
        if (value.Contains("unknown", StringComparison.OrdinalIgnoreCase)) return BorinKnowledgeConfidence.Unknown;
        if (value.Contains("hypothesis", StringComparison.OrdinalIgnoreCase)) return BorinKnowledgeConfidence.Hypothesis;
        if (value.Contains("likely", StringComparison.OrdinalIgnoreCase)) return BorinKnowledgeConfidence.Likely;
        if (value.Contains("correlated", StringComparison.OrdinalIgnoreCase)) return BorinKnowledgeConfidence.StronglyCorrelated;
        return BorinKnowledgeConfidence.Proven;
    }

    private static HashSet<string> Tokenize(string value) =>
        Terms.Matches(value ?? string.Empty).Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "…";
    private static string GetIndexPath(string root) => Path.Combine(Path.GetFullPath(root), "AI", "Memory", "Knowledge", "borin-majormud-catalog.json");
    private static BorinMajorMudKnowledgeStatus Status(CatalogDocument document, string path, string detail) =>
        new("Ready", document.Records.Count, document.RelationshipCount, path, document.UpdatedUtc, detail);

    private sealed record SourceFile(string Family, string Path);
    private sealed record CatalogRecord(string Family, string Id, string Name, string RelativePath, string Confidence,
        string Summary, List<string> Relationships, string FromRoom, string ToRoom, string Direction, string NpcName)
    {
        [JsonIgnore]
        public string SearchText => $"{Family} {Id} {Name} {Summary} {string.Join(' ', Relationships)}";
    }
    private sealed class CatalogDocument
    {
        public int Version { get; set; } = CatalogVersion;
        public string RepositoryRoot { get; set; } = string.Empty;
        public string SourceFingerprint { get; set; } = string.Empty;
        public DateTimeOffset UpdatedUtc { get; set; }
        public int RelationshipCount { get; set; }
        public List<CatalogRecord> Records { get; set; } = [];
    }
}
