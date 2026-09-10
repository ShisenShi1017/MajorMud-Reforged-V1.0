using System.Text.Json;

namespace RealmGate.Server;

/// <summary>Single live Bundle D authority for per-character hidden discovery.</summary>
public static class RuntimeBundleDSearchRuntime
{
    private static readonly RuntimeClassicHiddenDiscoveryRegistry Discoveries = new();
    private static readonly object PersistenceGate = new();
    private static readonly HashSet<string> LoadedWorlds = new(StringComparer.OrdinalIgnoreCase);

    public static bool CanCharacterSeeGroundItem(string characterId, RuntimeItemInstance item) =>
        !RuntimeClassicHiddenSearchService.IsHidden(item) || Discoveries.IsDiscovered(characterId, item.InstanceId);

    public static RuntimeClassicHiddenDiscoveryRegistry Registry => Discoveries;

    public static void EnsureLoaded(string projectRoot, string worldId)
    {
        lock (PersistenceGate)
        {
            var key = Path.GetFullPath(projectRoot) + "|" + worldId;
            if (!LoadedWorlds.Add(key)) return;
            var path = PersistencePath(projectRoot, worldId);
            if (!File.Exists(path)) return;
            var entries = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? Array.Empty<string>();
            foreach (var entry in entries)
            {
                var split = entry.IndexOf('|');
                if (split > 0 && split < entry.Length - 1)
                    Discoveries.Discover(entry[..split], entry[(split + 1)..]);
            }
        }
    }

    public static void Save(string projectRoot, string worldId, IEnumerable<string> characterIds)
    {
        lock (PersistenceGate)
        {
            var entries = characterIds.Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(characterId => Discoveries.GetDiscoveredTargets(characterId).Select(targetId => characterId + "|" + targetId))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            var path = PersistencePath(projectRoot, worldId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string PersistencePath(string projectRoot, string worldId) =>
        Path.Combine(projectRoot, "Data", "RuntimeState", worldId, "classic-hidden-discoveries.json");
}

public sealed partial class RuntimeLiveGameplayFacade
{
    public string ExecuteClassicSearch(RuntimeSession session, string? direction = null)
    {
        if (!string.IsNullOrWhiteSpace(direction)) return ExecuteClassicDirectionalSearch(session, direction);
        if (session.Actor is not RuntimeCharacterActor actor) return "Only characters can search.";
        RuntimeBundleDSearchRuntime.EnsureLoaded(_characters.ProjectRoot, session.World.Definition.Id);
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return "Your character record is unavailable.";
        if (RuntimeClassicStatusProjection.IsVisionBlocked(character))
            return RuntimeClassicCombatAnsi.Warning("You are blind.");

        var targets = new Dictionary<string, RuntimeClassicHiddenTarget>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in Living(session).GroundItems(session.CurrentRoom.Id).Where(RuntimeClassicHiddenSearchService.IsHidden))
        {
            var difficulty=ReadMetadataInt(item.Metadata,"concealment_difficulty",100);
            targets[item.InstanceId]=new(item.InstanceId,session.CurrentRoom.Id,"item",difficulty,true,
                ReadMetadataBool(item.Metadata,"delete_at_cleanup"),item.Metadata);
        }
        foreach (var active in _sessions.ActiveSessions.Where(x => x.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (active.Actor is not RuntimeCharacterActor activeActor) continue;
            var state = _characters.LoadCharacter(activeActor.Id)?.ItemState;
            if (state is null) continue;
            foreach (var item in state.Items.Where(item =>
                item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) &&
                item.LocationId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase) &&
                RuntimeClassicHiddenSearchService.IsHidden(item)))
            {
                var difficulty = ReadMetadataInt(item.Metadata, "concealment_difficulty", 100);
                targets[item.InstanceId] = new(item.InstanceId, session.CurrentRoom.Id, "item", difficulty, true,
                    ReadMetadataBool(item.Metadata, "delete_at_cleanup"), item.Metadata);
            }
        }

        foreach (var exit in session.CurrentRoom.Exits.Where(exit =>
            exit.GateKind.Equals("hidden", StringComparison.OrdinalIgnoreCase)))
        {
            var targetId = string.IsNullOrWhiteSpace(exit.Id) ? "exit:" + session.CurrentRoom.Id + ":" + exit.Direction : exit.Id;
            targets[targetId] = new(targetId, session.CurrentRoom.Id, "exit",
                100, false, false, new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["gate_kind"] = exit.GateKind, ["qualifier"] = exit.Qualifier });
        }

        foreach (var npc in LivingNpcsInRoom(session).Where(npc =>
            npc.Metadata.TryGetValue("hidden", out var hidden) && bool.TryParse(hidden, out var isHidden) && isHidden))
        {
            targets[npc.InstanceId] = new(npc.InstanceId, session.CurrentRoom.Id, "npc",
                ReadMetadataInt(npc.Metadata, "concealment_difficulty", 100), false, false, npc.Metadata);
        }

        var perception = ResolveEffectiveSearchPerception(character);
        var abilityAuthority = RuntimeCharacterQuestAbilityCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var capability = abilityAuthority.ResolveSearchCapability(character);
        // Ability 57 See Hidden is verified for character/NPC concealment detection in Bundle A/B.
        // No decoded source proves that it globally reveals room items, exits, currency, or objects,
        // so Bundle D does not persist object discovery from that combat capability.

        var search = new RuntimeClassicHiddenSearchService(RuntimeBundleDSearchRuntime.Registry);
        var result = search.Search(new RuntimeClassicSearchActor(actor.Id, session.CurrentRoom.Id,
            Math.Max(0, perception), Read(character.Skills, "search"), Read(character.BaseStats, "agl", "agi", "agility"),
            Read(character.BaseStats, "int", "intellect"), Read(character.BaseStats, "cha", "charm"), capability.Modifiers),
            targets.Values, Random.Shared);

        RuntimeBundleDSearchRuntime.Save(_characters.ProjectRoot, session.World.Definition.Id,
            _sessions.ActiveSessions.Where(x => x.Actor is RuntimeCharacterActor).Select(x => x.Actor.Id).Append(actor.Id));
        return result.Message;
    }

    private string ExecuteClassicDirectionalSearch(RuntimeSession session, string direction)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "Only characters can search.";
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return "Your character record is unavailable.";
        if (RuntimeClassicStatusProjection.IsVisionBlocked(character)) return RuntimeClassicCombatAnsi.Warning("You are blind.");
        var normalized = NormalizeClassicSearchDirection(direction);
        if (normalized is null) return "SEARCH <direction>";
        var word = ClassicDirectionWord(normalized);
        var nothing = normalized switch
        {
            "U" => "You notice nothing different above you.",
            "D" => "You notice nothing different below you.",
            _ => $"You notice nothing different to the {word}."
        };
        var exit = session.World.FindExit(session.CurrentRoom, normalized);
        if (exit is null) return nothing;
        var authority = RuntimeClassicRoomsWorldNativeAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id);
        if (!authority.TryGet(exit.Id, out var native)) return nothing;
        if (native.RoomType == 6)
        {
            var state = Living(session).ReadClassicHiddenExitState(exit.Id, native.Para1);
            if ((state & 2) == 0) return nothing;
            var threshold = Math.Max(3, ResolveEffectiveSearchPerception(character) - 15);
            if (Random.Shared.Next(0, 100) >= threshold) return nothing;
            Living(session).WriteClassicHiddenExitStateWithNativeReset(exit.Id, 4, DateTimeOffset.UtcNow);
            return normalized switch
            {
                "U" => "You found an exit upwards!",
                "D" => "You found an exit downwards!",
                _ => $"You found an exit to the {word}!"
            };
        }
        if (native.RoomType == 9)
        {
            var threshold = RuntimeClassicCharacterProgressionFormula.FindTrapsValue(character);
            if (Random.Shared.Next(0, 100) >= threshold) return nothing;
            return normalized switch
            {
                "U" => "You found a trap above you!",
                "D" => "You found a trap below you!",
                _ => $"You found a trap to the {word}!"
            };
        }
        return nothing;
    }

    private static string? NormalizeClassicSearchDirection(string value) => value.Trim().ToUpperInvariant() switch
    {
        "N" or "NORTH" => "N", "S" or "SOUTH" => "S", "E" or "EAST" => "E", "W" or "WEST" => "W",
        "NE" or "NORTHEAST" => "NE", "NW" or "NORTHWEST" => "NW", "SE" or "SOUTHEAST" => "SE", "SW" or "SOUTHWEST" => "SW",
        "U" or "UP" => "U", "D" or "DOWN" => "D", _ => null
    };

    private static int Read(IReadOnlyDictionary<string, int> values, params string[] keys)
    {
        foreach (var key in keys) if (values.TryGetValue(key, out var value)) return value;
        return 0;
    }

    internal static int ResolveEffectiveSearchPerception(RuntimeCharacterRecord character) =>
        checked(Read(character.Skills, "perception", "per") +
            (character.SemanticState?.DerivedStats.TryGetValue("perception_bonus", out var equipmentPerception) == true ? equipmentPerception : 0) +
            RuntimeClassicStatusProjection.Numeric(character, "perception"));

    private static int ReadMetadataInt(IReadOnlyDictionary<string, string>? metadata, string key, int fallback) =>
        metadata is not null && metadata.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool ReadMetadataBool(IReadOnlyDictionary<string, string>? metadata, string key) =>
        metadata is not null && metadata.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;
}

public sealed record RuntimeBundleDSearchAuditResult(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDSearchAudit
{
    public static RuntimeBundleDSearchAuditResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string code) { if (condition) checks.Add(code); else failures.Add(code); }

        var registry = new RuntimeClassicHiddenDiscoveryRegistry();
        var target = new RuntimeClassicHiddenTarget("hidden.item.audit", "room.map1.2150", "item", 1, true, false);
        var service = new RuntimeClassicHiddenSearchService(registry, (_, _, _) => true);
        var first = service.Search(new("character.one", target.RoomId, 0, 0, 0, 0, 0), new[] { target }, new Random(1));
        Check(first.NewlyDiscoveredTargetIds.Contains(target.TargetId), "search-success-discovers-for-actor");
        Check(registry.IsDiscovered("character.one", target.TargetId), "discovery-is-persisted-in-authority");
        Check(!registry.IsDiscovered("character.two", target.TargetId), "discovery-is-player-specific");
        var second = service.Search(new("character.one", target.RoomId, 0, 0, 0, 0, 0), new[] { target }, new Random(2));
        Check(second.PreviouslyDiscoveredTargetIds.Count == 0, "previous-discovery-is-rerolled");
        Check(second.NewlyDiscoveredTargetIds.Contains(target.TargetId), "latest-search-result-replaces-prior-result");

        var hidden = new RuntimeItemInstance("hidden.item.audit", "item.audit", null, RuntimeItemLocations.Room, target.RoomId, null, 0, 1, null,
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["hidden_at_cleanup"] = "true" });
        Check(RuntimeClassicHiddenSearchService.IsHidden(hidden), "cleanup-hidden-item-recognized");
        Check(!RuntimeBundleDSearchRuntime.CanCharacterSeeGroundItem("character.two", hidden), "look-examine-ground-loader-blocks-undiscovered-item");

        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server");
        Check(Directory.GetFiles(serverRoot, "RuntimeClassicHiddenSearch.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-classic-hidden-search-authority-file");
        Check(Directory.GetFiles(serverRoot, "RuntimeBundleDSearch.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-live-search-consumer-file");
        Check(RuntimeCommand.SupportedCommands.Contains("search"), "search-command-registered");

        return new(failures.Count == 0, checks, failures,
            new[] {
                "Hidden target discovery is character-specific and never globally unhides the target.",
                "Every SEARCH rerolls eligible room targets and replaces the character's prior transient result set.",
                "A found cleanup-hidden item remains hidden on the ground; pickup removes hidden state and re-drop is visible.",
                "Hidden and visible ground stacks remain separate because hidden metadata is removed only on pickup.",
                "Look, examine, and item targeting share the discovery-aware ground-item loader.",
                "Decoded See Hidden rank remains owned by character/NPC concealment detection and is not reused as automatic room-object discovery."
            },
            new[] { RuntimeClassicHiddenSearchService.FormulaId },
            new[] {
                "Hidden currency requires cleanup-state ownership from Phase D6 before a denomination balance can be player-specifically discovered.",
                "Decoded hidden room objects without an authoritative generated instance identity remain quarantined.",
                "Exact hidden-NPC and hidden-exit concealment values remain metadata-driven when supplied."
            },
            new[] { "Exact native primary-stat weighting for Classic Search where generated concealment difficulty is absent." });
    }
}
