using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

public sealed record BorinLibrariumPlayerGuidance(IReadOnlyList<string> Evidence, string Detail)
{
    public string PromptText => Evidence.Count == 0 ? Detail : $"{Detail}\n" + string.Join("\n", Evidence.Select(x => $"- {x}"));
    public static BorinLibrariumPlayerGuidance Disabled() => new([], "Librarium player guidance is disabled.");
}

/// <summary>
/// Read-only Librarium reference lookup for Borin's autonomous player decisions.
/// This intentionally reads catalog-listed Librarium object records instead of scanning
/// arbitrary repository text. Live terminal state remains authoritative for the current turn.
/// </summary>
public static class BorinLibrariumPlayerAdvisor
{
    private const long MaximumCanonicalRecordFileBytes = 64L * 1024L * 1024L;
    private static readonly string[] Families =
    [
        "rooms", "npcs", "spawns", "quests", "items", "shops", "spells", "classes", "races", "room_interactions"
    ];
    private static readonly Regex TokenPattern = new("[A-Za-z0-9_.:/-]{2,}", RegexOptions.Compiled);
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "this", "that", "your", "major", "mud", "majormud",
        "classic", "play", "playing", "autonomously", "learn", "world", "verified", "gameplay", "through"
    };
    private static readonly HashSet<string> FlattenSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "evidence_refs", "source_trace", "validation_rules_0124", "version_history_0124", "generator_mapping_0124",
        "librarium_enrichment_0124", "override_policy_0124", "evidence_summary_0124", "relationship_summary_0124"
    };
    private static readonly object CacheGate = new();
    private static CachedIndex? _cache;

    public static Task<BorinLibrariumPlayerGuidance> GatherAsync(
        string repositoryRoot,
        string goal,
        BorinGameStateSnapshot state,
        int maximumResults = 12,
        CancellationToken token = default) =>
        Task.Run(() => Gather(repositoryRoot, goal, state, maximumResults, token), token);

    private static BorinLibrariumPlayerGuidance Gather(
        string repositoryRoot,
        string goal,
        BorinGameStateSnapshot state,
        int maximumResults,
        CancellationToken token)
    {
        maximumResults = Math.Clamp(maximumResults, 4, 20);
        var root = string.IsNullOrWhiteSpace(repositoryRoot) ? string.Empty : Path.GetFullPath(repositoryRoot);
        var librariumRoot = Path.Combine(root, "Librarium");
        if (!Directory.Exists(librariumRoot))
            return new([], "Librarium is unavailable in this repository snapshot.");

        LibrariumIndex index;
        try
        {
            index = GetIndex(root, token);
        }
        catch (Exception ex)
        {
            return new([], $"Librarium structured lookup failed safely: {ex.Message}");
        }

        if (index.Records.Count == 0)
            return new([], "No catalog-listed structured Librarium records were available for player guidance.");

        var evidence = new List<string>();
        AddCurrentRoomContext(index, state, evidence, 3);
        AddQuestNpcGuidance(index, state, evidence, 5);
        AddLevelingGuidance(index, state, evidence, 4);
        AddEquipmentEconomyGuidance(index, state, evidence, 4);
        AddRelevantReferenceGuidance(index, goal, state, evidence, maximumResults);

        var distinct = evidence
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximumResults)
            .ToArray();

        var detail = $"Structured Librarium guidance: {index.Records.Count:N0} catalog-listed records indexed from {index.SourceFiles.Count:N0} authoritative object files. " +
                     "Leveling entries are planning candidates, not a guarantee of safety or optimal XP; live server evidence always wins.";
        return distinct.Length == 0
            ? new([], detail + " No relevant record matched this decision.")
            : new(distinct, detail);
    }

    private static LibrariumIndex GetIndex(string repositoryRoot, CancellationToken token)
    {
        var sourceFiles = ResolveCatalogRecordFiles(repositoryRoot, token);
        var fingerprint = Fingerprint(sourceFiles);
        lock (CacheGate)
        {
            if (_cache is not null &&
                _cache.RepositoryRoot.Equals(repositoryRoot, StringComparison.OrdinalIgnoreCase) &&
                _cache.Fingerprint == fingerprint)
                return _cache.Index;
        }

        var built = BuildIndex(repositoryRoot, sourceFiles, token);
        lock (CacheGate)
            _cache = new CachedIndex(repositoryRoot, fingerprint, built);
        return built;
    }

    private static IReadOnlyList<string> ResolveCatalogRecordFiles(string repositoryRoot, CancellationToken token)
    {
        var results = new List<string>();
        foreach (var family in Families)
        {
            token.ThrowIfCancellationRequested();
            var familyRoot = Path.Combine(repositoryRoot, "Librarium", "Objects", family);
            var catalogPath = Path.Combine(familyRoot, "catalog.json");
            if (!File.Exists(catalogPath)) continue;

            try
            {
                using var catalog = JsonDocument.Parse(File.ReadAllBytes(catalogPath));
                if (!catalog.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var entry in records.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.String) continue;
                    var name = entry.GetString()?.Trim() ?? string.Empty;
                    if (name.Length == 0 || !name.Contains("_records", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Contains("sql_mdb", StringComparison.OrdinalIgnoreCase)) continue;
                    var path = Path.GetFullPath(Path.Combine(familyRoot, name));
                    if (!path.StartsWith(Path.GetFullPath(familyRoot), StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
                    var info = new FileInfo(path);
                    if (info.Length <= MaximumCanonicalRecordFileBytes)
                        results.Add(path);
                }
            }
            catch
            {
                // A malformed family catalog must not stop Borin's player loop.
            }
        }
        return results.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static long Fingerprint(IReadOnlyList<string> files)
    {
        unchecked
        {
            long value = 1469598103934665603L;
            foreach (var path in files)
            {
                var info = new FileInfo(path);
                value ^= info.Length;
                value *= 1099511628211L;
                value ^= info.LastWriteTimeUtc.Ticks;
                value *= 1099511628211L;
            }
            return value;
        }
    }

    private static LibrariumIndex BuildIndex(string repositoryRoot, IReadOnlyList<string> sourceFiles, CancellationToken token)
    {
        var records = new List<LibrariumRecord>(14000);
        foreach (var path in sourceFiles)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                if (!document.RootElement.TryGetProperty("records", out var sourceRecords) || sourceRecords.ValueKind != JsonValueKind.Array)
                    continue;
                var family = ResolveFamily(repositoryRoot, path);
                foreach (var record in sourceRecords.EnumerateArray())
                {
                    token.ThrowIfCancellationRequested();
                    if (record.ValueKind != JsonValueKind.Object) continue;
                    var parsed = ParseRecord(repositoryRoot, family, path, record);
                    if (parsed is not null) records.Add(parsed);
                }
            }
            catch
            {
                // Ignore one unusable canonical source and keep the rest of the Librarium available.
            }
        }
        return new LibrariumIndex(records, sourceFiles);
    }

    private static LibrariumRecord? ParseRecord(string repositoryRoot, string family, string path, JsonElement record)
    {
        var id = Scalar(record, "id");
        var name = Scalar(record, "name");
        if (id.Length == 0 && name.Length == 0) return null;

        var confidence = Scalar(record, "confidence");
        var roomRef = FirstNonEmpty(Scalar(record, "room_ref"), Scalar(record, "qualified_room_ref"));
        var qualifiedRoomRef = Scalar(record, "qualified_room_ref");
        var npcName = Scalar(record, "npc_name");
        var classification = Scalar(record, "classification");
        var questFlagName = Scalar(record, "quest_flag_name");
        var relatedNpcRef = StringAt(record, "relationships", "npc");
        var relatedItemRef = StringAt(record, "relationships", "item");
        var commandKeywords = StringArrayAt(record, "relationships", "command_keywords");
        var level = IntAt(record, "combat", "level");
        if (level <= 0) level = IntAt(record, "classification", "spell_level");
        var experience = IntAt(record, "rewards", "xp_base");
        var hitPoints = IntAt(record, "combat", "hp");
        var maximumAttackDamage = MaxArrayInt(record, "attacks", "maximum_damage");
        var respawnMinutes = IntAt(record, "spawn_model", "respawn_minutes");
        var minimumLevel = IntAt(record, "level_range", "min");
        var maximumLevel = IntAt(record, "level_range", "max");
        var itemType = StringAt(record, "classification", "type");
        var cost = IntAt(record, "economy", "cost");
        var minimumDamage = IntAt(record, "combat", "minimum_damage");
        var maximumDamage = IntAt(record, "combat", "maximum_damage");
        var armorClassBonus = IntAt(record, "combat", "armor_class_bonus");
        var damageResistanceBonus = IntAt(record, "combat", "damage_resistance_bonus");
        var accuracy = IntAt(record, "combat", "accuracy");
        var speed = IntAt(record, "combat", "speed");
        var wornOn = StringAt(record, "equipment", "worn_on");
        var strengthRequirement = IntAt(record, "equipment", "str_requirement");
        var serviceKind = Scalar(record, "service_kind");
        var source = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
        var searchText = BuildSearchText(record, id, name, family);
        return new LibrariumRecord(
            family, id, name, confidence, roomRef, qualifiedRoomRef, npcName, classification, questFlagName, relatedNpcRef, relatedItemRef, commandKeywords,
            level, experience, hitPoints, maximumAttackDamage, respawnMinutes, minimumLevel, maximumLevel,
            itemType, cost, minimumDamage, maximumDamage, armorClassBonus, damageResistanceBonus, accuracy, speed, wornOn, strengthRequirement, serviceKind,
            source, searchText);
    }

    private static void AddCurrentRoomContext(LibrariumIndex index, BorinGameStateSnapshot state, ICollection<string> output, int maximum)
    {
        var aliases = RoomAliases(state.CurrentRoomKey, state.CurrentRoom);
        if (aliases.Count == 0) return;
        var localSpawns = index.Records
            .Where(record => record.Family.Equals("spawns", StringComparison.OrdinalIgnoreCase) && RoomMatches(record, aliases))
            .Take(maximum)
            .ToArray();
        foreach (var spawn in localSpawns)
        {
            var npc = index.FindNpc(spawn.NpcName);
            var npcDetail = npc is null ? string.Empty : $"; NPC level {DisplayNumber(npc.Level)}, XP {DisplayNumber(npc.Experience)}, HP {DisplayNumber(npc.HitPoints)}";
            output.Add($"CURRENT ROOM • {ConfidenceLabel(spawn.Confidence)} • {spawn.NpcName}{npcDetail}; spawn {DisplayRoom(index, spawn)}; source {spawn.Source}");
        }
    }

    private static void AddQuestNpcGuidance(
        LibrariumIndex index,
        BorinGameStateSnapshot state,
        ICollection<string> output,
        int maximum)
    {
        var emitted = 0;
        var aliases = RoomAliases(state.CurrentRoomKey, state.CurrentRoom);
        if (aliases.Count > 0)
        {
            var localSpawns = index.Records
                .Where(record => record.Family.Equals("spawns", StringComparison.OrdinalIgnoreCase) && RoomMatches(record, aliases))
                .Take(6)
                .ToArray();

            foreach (var spawn in localSpawns)
            {
                if (emitted >= maximum) break;
                var npc = index.FindNpc(spawn.NpcName);
                if (npc is null) continue;

                output.Add($"NPC SPAWN REFERENCE FOR CURRENT ROOM • {ConfidenceLabel(spawn.Confidence)} spawn / {ConfidenceLabel(npc.Confidence)} NPC • {npc.Name} [{npc.Id}]; Librarium places this NPC at {DisplayRoom(index, spawn)}, but live LOOK/TALK evidence must verify the NPC is actually present now; sources {npc.Source} + {spawn.Source}");
                emitted++;
                if (emitted >= maximum) break;

                foreach (var quest in index.FindQuestFlowsForNpc(npc.Id).Take(2))
                {
                    if (emitted >= maximum) break;
                    var keywords = quest.CommandKeywords.Count == 0
                        ? "no decoded dialogue keyword"
                        : string.Join(", ", quest.CommandKeywords.Take(8));
                    output.Add($"QUEST/NPC FLOW CANDIDATE • {ConfidenceLabel(quest.Confidence)} • {quest.Name}: NPC {npc.Name} [{npc.Id}], quest flag {Display(quest.QuestFlagName)}, decoded dialogue keywords [{keywords}]. This is a Librarium flow relationship, not proof of Borin's current quest stage; verify the NPC and live dialogue before using a keyword or transferring an item; source {quest.Source}");
                    emitted++;
                }
            }
        }

        if (emitted >= maximum) return;
        foreach (var ownedName in state.InventoryItems.Take(16))
        {
            if (emitted >= maximum) break;
            var item = index.FindItem(ownedName);
            if (item is null) continue;
            foreach (var quest in index.FindQuestCluesForItem(item).Take(1))
            {
                if (emitted >= maximum) break;
                output.Add($"QUEST ITEM CLUE • {ConfidenceLabel(quest.Confidence)} • owned item {item.Name} [{item.Id}] is referenced by {quest.Name} ({Display(quest.Classification)}). Preserve it and investigate with live dialogue/room evidence; do not GIVE, DROP, or USE it solely because this reference exists; source {quest.Source}");
                emitted++;
            }
        }
    }

    private static void AddLevelingGuidance(LibrariumIndex index, BorinGameStateSnapshot state, ICollection<string> output, int maximum)
    {
        if (state.CharacterLevel <= 0)
            output.Add("LEVELING • player level is not verified in the live snapshot. Use normal player status/experience commands before treating any target as level-appropriate.");

        var candidates = new List<(LibrariumRecord Npc, LibrariumRecord Spawn, double Score, int Delta)>();
        if (state.CharacterLevel > 0 && index.NpcLevelSignalReliable)
        {
            var low = Math.Max(1, state.CharacterLevel - 3);
            var high = state.CharacterLevel + 1;
            candidates.AddRange(index.Records
                .Where(record => record.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase) &&
                                 record.Level >= low && record.Level <= high && record.Experience > 0)
                .SelectMany(record => index.FindSpawns(record.Name).Take(1), (record, spawn) =>
                    (record, spawn, Score: 1000d - (Math.Abs(record.Level - state.CharacterLevel) * 100d) + record.Experience, Delta: Math.Abs(record.Level - state.CharacterLevel))));
        }
        else
        {
            if (!index.NpcLevelSignalReliable)
                output.Add($"LEVELING DATA QUALITY • NPC level is currently non-discriminating in the loaded Librarium corpus ({index.PositiveNpcLevelCount:N0} positive-level NPC records, {index.DistinctPositiveNpcLevels:N0} distinct level value). Borin must not pretend that field identifies level-appropriate monsters.");

            var healthReference = state.HealthMaximum > 0 ? state.HealthMaximum : Math.Max(state.HealthCurrent, 50);
            var maximumCandidateHp = Math.Max(120, healthReference * 4);
            var maximumCandidateHit = Math.Max(25, healthReference);
            var pool = index.Records
                .Where(record => record.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase) &&
                                 record.Experience > 0 && record.HitPoints > 0 &&
                                 record.HitPoints <= maximumCandidateHp &&
                                 (record.MaximumAttackDamage <= 0 || record.MaximumAttackDamage <= maximumCandidateHit))
                .SelectMany(record => index.FindSpawns(record.Name).Take(1), (record, spawn) =>
                    (record, spawn, Score: record.Experience / (double)Math.Max(1, record.HitPoints + (record.MaximumAttackDamage * 3)), Delta: 0))
                .OrderByDescending(candidate => candidate.Score)
                .Take(maximum)
                .ToArray();
            candidates.AddRange(pool);
        }

        foreach (var candidate in candidates
                     .OrderByDescending(candidate => candidate.Score)
                     .ThenBy(candidate => candidate.Delta)
                     .Take(maximum))
        {
            var location = DisplayRoom(index, candidate.Spawn);
            var respawn = candidate.Spawn.RespawnMinutes > 0 ? $", respawn {candidate.Spawn.RespawnMinutes}m" : string.Empty;
            var attack = candidate.Npc.MaximumAttackDamage > 0 ? $", max recorded hit {candidate.Npc.MaximumAttackDamage}" : string.Empty;
            output.Add(
                $"LEVELING/SCOUTING CANDIDATE • {ConfidenceLabel(candidate.Npc.Confidence)} NPC / {ConfidenceLabel(candidate.Spawn.Confidence)} spawn • " +
                $"{candidate.Npc.Name}: recorded level {DisplayNumber(candidate.Npc.Level)}, XP {candidate.Npc.Experience}, HP {DisplayNumber(candidate.Npc.HitPoints)}{attack}; " +
                $"location {location}{respawn}; verify combat suitability through live play before committing; sources {candidate.Npc.Source} + {candidate.Spawn.Source}");
        }

        if (candidates.Count == 0)
            output.Add("LEVELING • no conservative catalog-linked NPC/spawn candidate could be ranked from the currently loaded Librarium evidence. Borin should gather live combat/character evidence and use Librarium rooms, quests, and NPC references as clues rather than inventing a target.");
    }

    private static void AddEquipmentEconomyGuidance(
        LibrariumIndex index,
        BorinGameStateSnapshot state,
        ICollection<string> output,
        int maximum)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ownedName in state.EquippedItems.Concat(state.InventoryItems).Take(12))
        {
            var item = index.FindItem(ownedName);
            if (item is null || !seen.Add(item.Id)) continue;
            var combat = item.ItemType.Equals("weapon", StringComparison.OrdinalIgnoreCase)
                ? $"damage {DisplayNumber(item.MinimumDamage)}-{DisplayNumber(item.MaximumDamage)}, accuracy {item.Accuracy}, speed {DisplayNumber(item.Speed)}"
                : item.ItemType.Equals("armor", StringComparison.OrdinalIgnoreCase)
                    ? $"AC +{item.ArmorClassBonus}, DR +{item.DamageResistanceBonus}, worn_on {Display(item.WornOn)}"
                    : $"type {Display(item.ItemType)}";
            var requirement = item.StrengthRequirement > 0 ? $", STR requirement {item.StrengthRequirement}" : string.Empty;
            output.Add($"ITEM REFERENCE • {ConfidenceLabel(item.Confidence)} • {item.Name}: {combat}, recorded cost {DisplayNumber(item.Cost)}{requirement}; source {item.Source}");
            if (seen.Count >= maximum) break;
        }

        var aliases = RoomAliases(state.CurrentRoomKey, state.CurrentRoom);
        if (aliases.Count > 0)
        {
            foreach (var shop in index.Records
                         .Where(record => record.Family.Equals("shops", StringComparison.OrdinalIgnoreCase) && RoomMatches(record, aliases))
                         .Take(3))
            {
                output.Add($"SHOP IN CURRENT ROOM • {ConfidenceLabel(shop.Confidence)} • {shop.Name}: service {Display(shop.ServiceKind)}, level range {DisplayNumber(shop.MinimumLevel)}-{DisplayNumber(shop.MaximumLevel)}; use live LIST to verify stock/prices; source {shop.Source}");
            }
        }

        if (state.CharacterLevel > 0)
        {
            var candidates = index.Records
                .Where(record => record.Family.Equals("shops", StringComparison.OrdinalIgnoreCase) &&
                                 (record.ServiceKind.Equals("weapon_or_armor_shop", StringComparison.OrdinalIgnoreCase) ||
                                  record.ServiceKind.Equals("item_shop", StringComparison.OrdinalIgnoreCase) ||
                                  record.ServiceKind.Equals("generic_shop", StringComparison.OrdinalIgnoreCase)) &&
                                 (record.MinimumLevel <= 0 || record.MinimumLevel <= state.CharacterLevel) &&
                                 (record.MaximumLevel <= 0 || record.MaximumLevel >= state.CharacterLevel))
                .OrderBy(record => record.ServiceKind.Equals("weapon_or_armor_shop", StringComparison.OrdinalIgnoreCase) ? 0 :
                                   record.ServiceKind.Equals("item_shop", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();
            foreach (var shop in candidates)
                output.Add($"SHOP CANDIDATE • {ConfidenceLabel(shop.Confidence)} • {shop.Name}: service {Display(shop.ServiceKind)}, location {DisplayRoom(index, shop)}, level range {DisplayNumber(shop.MinimumLevel)}-{DisplayNumber(shop.MaximumLevel)}; destination reference only, not a route; source {shop.Source}");
        }
    }

    private static void AddRelevantReferenceGuidance(
        LibrariumIndex index,
        string goal,
        BorinGameStateSnapshot state,
        ICollection<string> output,
        int maximumResults)
    {
        var terms = BuildTerms(goal, state);
        if (terms.Count == 0) return;
        var remaining = Math.Max(0, maximumResults - output.Count);
        if (remaining == 0) return;

        var ranked = index.Records
            .Select(record => new { Record = record, Score = Score(record, terms, state) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Record.Family, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Min(remaining, 7))
            .ToArray();

        foreach (var candidate in ranked)
        {
            var record = candidate.Record;
            var detail = record.Family switch
            {
                "npcs" => $"recorded level {DisplayNumber(record.Level)}, XP {DisplayNumber(record.Experience)}, HP {DisplayNumber(record.HitPoints)}, max recorded hit {DisplayNumber(record.MaximumAttackDamage)}",
                "spawns" => $"NPC {Display(record.NpcName)}, room {DisplayRoom(index, record)}, respawn {DisplayNumber(record.RespawnMinutes)}m",
                "shops" => $"room {DisplayRoom(index, record)}, level range {DisplayNumber(record.MinimumLevel)}-{DisplayNumber(record.MaximumLevel)}",
                "spells" => $"spell level {DisplayNumber(record.Level)}",
                _ => record.SearchText.Length > 240 ? record.SearchText[..240] + "…" : record.SearchText,
            };
            output.Add($"REFERENCE • {record.Family} • {ConfidenceLabel(record.Confidence)} • {Display(record.Name, record.Id)} — {detail}; source {record.Source}");
        }
    }

    private static IReadOnlyList<string> BuildTerms(string goal, BorinGameStateSnapshot state)
    {
        var raw = string.Join(' ', new[]
        {
            goal ?? string.Empty,
            state.CurrentRoom,
            state.CurrentRoomKey,
            string.Join(' ', state.InventoryItems),
            string.Join(' ', state.EquippedItems),
        });
        return TokenPattern.Matches(raw)
            .Select(match => match.Value)
            .Where(term => !Noise.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
    }

    private static double Score(LibrariumRecord record, IReadOnlyList<string> terms, BorinGameStateSnapshot state)
    {
        double score = 0;
        foreach (var term in terms)
        {
            if (record.Name.Equals(term, StringComparison.OrdinalIgnoreCase)) score += 8;
            else if (record.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 4;
            if (record.Id.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 3;
            if (record.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 1;
        }
        if (state.CharacterLevel > 0 && record.Level > 0)
            score += Math.Max(0, 3 - Math.Abs(record.Level - state.CharacterLevel));
        if (record.Family is "npcs" or "spawns" or "rooms" or "quests") score += .25;
        return score;
    }

    private static string BuildSearchText(JsonElement record, string id, string name, string family)
    {
        var values = new List<string>(64) { family, id, name };
        Flatten(record, values, 0, 72);
        return string.Join(' ', values.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void Flatten(JsonElement element, ICollection<string> values, int depth, int remaining)
    {
        if (remaining <= 0 || depth > 3) return;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (values.Count >= remaining || FlattenSkip.Contains(property.Name)) continue;
                    values.Add(property.Name);
                    Flatten(property.Value, values, depth + 1, remaining);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray().Take(8))
                    Flatten(item, values, depth + 1, remaining);
                break;
            case JsonValueKind.String:
                var text = element.GetString()?.Trim() ?? string.Empty;
                if (text.Length > 0) values.Add(text[..Math.Min(120, text.Length)]);
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                values.Add(element.ToString());
                break;
        }
    }

    private static IReadOnlyList<string> RoomAliases(string roomKey, string roomName)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(roomName)) aliases.Add(roomName.Trim());
        var key = roomKey?.Trim() ?? string.Empty;
        if (key.Length == 0) return aliases.ToArray();
        aliases.Add(key);
        var parts = key.Replace('/', ':').Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var map) && int.TryParse(parts[1], out var room))
        {
            aliases.Add($"room.map{map}.{room}");
            aliases.Add($"map{map}-room{room}");
            if (map == 1) aliases.Add($"room.{room}");
        }
        return aliases.ToArray();
    }

    private static bool RoomMatches(LibrariumRecord record, IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (record.RoomRef.Equals(alias, StringComparison.OrdinalIgnoreCase) ||
                record.QualifiedRoomRef.Equals(alias, StringComparison.OrdinalIgnoreCase) ||
                record.SearchText.Contains(alias, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string DisplayRoom(LibrariumIndex index, LibrariumRecord record)
    {
        var roomRef = FirstNonEmpty(record.QualifiedRoomRef, record.RoomRef);
        if (roomRef.Length == 0) return "unknown room";
        var room = index.FindRoom(roomRef);
        return room is null || room.Name.Length == 0 ? roomRef : $"{room.Name} [{roomRef}]";
    }

    private static string ConfidenceLabel(string confidence)
    {
        if (confidence.Contains("proven", StringComparison.OrdinalIgnoreCase) ||
            confidence.Contains("verified", StringComparison.OrdinalIgnoreCase)) return "PROVEN";
        if (confidence.Contains("strongly_correlated", StringComparison.OrdinalIgnoreCase) ||
            confidence.Contains("strongly correlated", StringComparison.OrdinalIgnoreCase)) return "STRONGLY CORRELATED";
        return confidence.Length == 0 ? "REFERENCE / CONFIDENCE UNSPECIFIED" : $"REFERENCE / {confidence}";
    }

    private static string ResolveFamily(string repositoryRoot, string path)
    {
        var relative = Path.GetRelativePath(Path.Combine(repositoryRoot, "Librarium", "Objects"), path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault() ?? "unknown";
    }

    private static int IntAt(JsonElement root, string parent, string property)
    {
        if (!root.TryGetProperty(parent, out var nested) || nested.ValueKind != JsonValueKind.Object) return 0;
        var value = Scalar(nested, property);
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static string StringAt(JsonElement root, string parent, string property)
    {
        if (!root.TryGetProperty(parent, out var nested) || nested.ValueKind != JsonValueKind.Object) return string.Empty;
        return Scalar(nested, property);
    }

    private static IReadOnlyList<string> StringArrayAt(JsonElement root, string parent, string property)
    {
        if (!root.TryGetProperty(parent, out var nested) || nested.ValueKind != JsonValueKind.Object) return [];
        if (!nested.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return [];
        return array.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
    }

    private static int MaxArrayInt(JsonElement root, string arrayProperty, string valueProperty)
    {
        if (!root.TryGetProperty(arrayProperty, out var array) || array.ValueKind != JsonValueKind.Array) return 0;
        var maximum = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var value = Scalar(item, valueProperty);
            if (int.TryParse(value, out var parsed) && parsed > maximum) maximum = parsed;
        }
        return maximum;
    }

    private static string Scalar(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return string.Empty;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty,
        };
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string DisplayNumber(int value) => value > 0 ? value.ToString() : "unknown";
    private static string Display(string value, string fallback = "unknown") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private sealed record CachedIndex(string RepositoryRoot, long Fingerprint, LibrariumIndex Index);

    private sealed record LibrariumRecord(
        string Family,
        string Id,
        string Name,
        string Confidence,
        string RoomRef,
        string QualifiedRoomRef,
        string NpcName,
        string Classification,
        string QuestFlagName,
        string RelatedNpcRef,
        string RelatedItemRef,
        IReadOnlyList<string> CommandKeywords,
        int Level,
        int Experience,
        int HitPoints,
        int MaximumAttackDamage,
        int RespawnMinutes,
        int MinimumLevel,
        int MaximumLevel,
        string ItemType,
        int Cost,
        int MinimumDamage,
        int MaximumDamage,
        int ArmorClassBonus,
        int DamageResistanceBonus,
        int Accuracy,
        int Speed,
        string WornOn,
        int StrengthRequirement,
        string ServiceKind,
        string Source,
        string SearchText);

    private sealed class LibrariumIndex
    {
        private readonly Dictionary<string, LibrariumRecord> _npcsByName;
        private readonly Dictionary<string, List<LibrariumRecord>> _spawnsByNpcName;
        private readonly Dictionary<string, LibrariumRecord> _roomsByAlias;
        private readonly Dictionary<string, LibrariumRecord> _itemsByAlias;

        public LibrariumIndex(IReadOnlyList<LibrariumRecord> records, IReadOnlyList<string> sourceFiles)
        {
            Records = records;
            SourceFiles = sourceFiles;
            _npcsByName = records
                .Where(record => record.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase) && record.Name.Length > 0)
                .GroupBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            _spawnsByNpcName = records
                .Where(record => record.Family.Equals("spawns", StringComparison.OrdinalIgnoreCase) && record.NpcName.Length > 0)
                .GroupBy(record => record.NpcName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            _itemsByAlias = new Dictionary<string, LibrariumRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in records.Where(record => record.Family.Equals("items", StringComparison.OrdinalIgnoreCase)))
            {
                AddItemAlias(item.Id, item);
                AddItemAlias(item.Name, item);
                AddItemAlias(StripArticle(item.Name), item);
            }
            _roomsByAlias = new Dictionary<string, LibrariumRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in records.Where(record => record.Family.Equals("rooms", StringComparison.OrdinalIgnoreCase)))
            {
                AddRoomAlias(room.Id, room);
                AddRoomAlias(room.Name, room);
                if (room.Id.StartsWith("room.", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(room.Id[5..], out var legacy))
                {
                    AddRoomAlias($"room.map1.{legacy}", room);
                    AddRoomAlias($"1:{legacy}", room);
                    AddRoomAlias($"map1-room{legacy}", room);
                }
            }
        }

        public IReadOnlyList<LibrariumRecord> Records { get; }
        public IReadOnlyList<string> SourceFiles { get; }
        public int PositiveNpcLevelCount => Records.Count(record => record.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase) && record.Level > 0);
        public int DistinctPositiveNpcLevels => Records.Where(record => record.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase) && record.Level > 0).Select(record => record.Level).Distinct().Count();
        public bool NpcLevelSignalReliable => PositiveNpcLevelCount >= 10 && DistinctPositiveNpcLevels >= 3;
        public LibrariumRecord? FindNpc(string name) => name.Length > 0 && _npcsByName.TryGetValue(name, out var npc) ? npc : null;
        public IReadOnlyList<LibrariumRecord> FindSpawns(string npcName) => npcName.Length > 0 && _spawnsByNpcName.TryGetValue(npcName, out var spawns) ? spawns : [];
        public IReadOnlyList<LibrariumRecord> FindQuestFlowsForNpc(string npcRef) => Records
            .Where(record => record.Family.Equals("quests", StringComparison.OrdinalIgnoreCase) &&
                             record.Classification.Equals("npc_textblock_quest_flow", StringComparison.OrdinalIgnoreCase) &&
                             RefMatches(record.RelatedNpcRef, npcRef))
            .OrderBy(record => record.QuestFlagName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        public IReadOnlyList<LibrariumRecord> FindQuestCluesForItem(LibrariumRecord item) => Records
            .Where(record => record.Family.Equals("quests", StringComparison.OrdinalIgnoreCase) &&
                             (RefMatches(record.RelatedItemRef, item.Id) ||
                              (item.Name.Length > 2 && record.SearchText.Contains(item.Name, StringComparison.OrdinalIgnoreCase))))
            .OrderBy(record => record.Classification.Equals("key_item_or_gate_candidate", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        public LibrariumRecord? FindRoom(string roomRef) => _roomsByAlias.TryGetValue(roomRef, out var room) ? room : null;
        public LibrariumRecord? FindItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return null;
            var normalized = itemName.Trim();
            if (_itemsByAlias.TryGetValue(normalized, out var exact)) return exact;
            var stripped = StripArticle(normalized);
            return _itemsByAlias.TryGetValue(stripped, out var alias) ? alias : null;
        }

        private static bool RefMatches(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            var a = left.Trim();
            var b = right.Trim();
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
            var aTail = a.Contains('.') ? a[(a.LastIndexOf('.') + 1)..] : a;
            var bTail = b.Contains('.') ? b[(b.LastIndexOf('.') + 1)..] : b;
            return aTail.Equals(bTail, StringComparison.OrdinalIgnoreCase);
        }

        private void AddItemAlias(string alias, LibrariumRecord item)
        {
            if (!string.IsNullOrWhiteSpace(alias)) _itemsByAlias.TryAdd(alias.Trim(), item);
        }

        private static string StripArticle(string value)
        {
            var text = value?.Trim() ?? string.Empty;
            foreach (var prefix in new[] { "a ", "an ", "the " })
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return text[prefix.Length..].Trim();
            return text;
        }

        private void AddRoomAlias(string alias, LibrariumRecord room)
        {
            if (!string.IsNullOrWhiteSpace(alias)) _roomsByAlias.TryAdd(alias.Trim(), room);
        }
    }
}
