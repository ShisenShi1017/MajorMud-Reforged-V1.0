using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

/// <summary>
/// CodeUpdate0375 navigation planner.
///
/// The existing ClientSessionService remains the only route executor. This service only
/// builds client-side route plans from the effective Reforged game-data view established
/// in 0372/0373. Reforged connections therefore read the active Reforged world projection;
/// external BBS connections read Classic plus that BBS's local MDB/manual overlay.
///
/// Route selection is weighted and fail-safe. Open exits are preferred. Known damage/spell
/// traps, hidden/restricted transitions, special room effects, and rooms whose captured NPC
/// level exceeds the configured character-relative threshold can be avoided. When strict
/// avoidance has no path, an optional risky-fallback pass may return the least-risk route and
/// surfaces every scored hazard before anything is sent.
/// </summary>
public sealed partial class RealmGateNavigationService
{
    private readonly RealmGateClientGameDataOverlayService _gameData = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _profileRoot;
    private NavigationSnapshot? _snapshot;
    private string _snapshotSignature = string.Empty;

    public RealmGateNavigationService(string? rootPath = null)
    {
        var root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate");
        _profileRoot = Path.Combine(root, "Client", "Navigation");
    }

    public RealmGateGameDataContext ResolveContext(ClientSessionConnection session) => _gameData.ResolveContext(session);

    public RealmGateNavigationProfile LoadProfile(ClientSessionConnection session)
    {
        var key = BuildProfileKey(session);
        var path = ProfilePath(key);
        if (!File.Exists(path))
            return NewProfile(key);

        try
        {
            var profile = JsonSerializer.Deserialize<RealmGateNavigationProfile>(File.ReadAllText(path), _json) ?? NewProfile(key);
            NormalizeProfile(profile, key);
            return profile;
        }
        catch (JsonException)
        {
            BackupCorrupt(path);
            return NewProfile(key);
        }
        catch (IOException)
        {
            return NewProfile(key);
        }
        catch (UnauthorizedAccessException)
        {
            return NewProfile(key);
        }
    }

    public void SaveProfile(ClientSessionConnection session, RealmGateNavigationProfile profile)
    {
        var key = BuildProfileKey(session);
        NormalizeProfile(profile, key);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_profileRoot);
        var path = ProfilePath(key);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, _json), new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    public IReadOnlyList<RealmGateNavigationRoom> GetRooms(ClientSessionConnection session)
    {
        var snapshot = GetSnapshot(session);
        return snapshot.Rooms.Values
            .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(room => room.Key, StringComparer.OrdinalIgnoreCase)
            .Select(room => room.Public)
            .ToArray();
    }

    public IReadOnlyList<RealmGateNavigationLair> GetLairs(ClientSessionConnection session)
    {
        var snapshot = GetSnapshot(session);
        var inbound = new Dictionary<int, List<RoomNode>>();
        foreach (var lair in snapshot.Rooms.Values.Where(room => room.LairId > 0))
            inbound.TryAdd(lair.LairId, []);

        foreach (var edge in snapshot.AllEdges)
        {
            if (!snapshot.Rooms.TryGetValue(edge.To, out var to) || to.LairId <= 0)
                continue;
            var fromLair = snapshot.Rooms.TryGetValue(edge.From, out var from) ? from.LairId : 0;
            if (fromLair != to.LairId)
                inbound[to.LairId].Add(to);
        }

        return snapshot.Rooms.Values
            .Where(room => room.LairId > 0)
            .GroupBy(room => room.LairId)
            .Select(group =>
            {
                var entry = inbound.TryGetValue(group.Key, out var candidates) && candidates.Count > 0
                    ? candidates.OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase).First()
                    : group.OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase).First();
                return new RealmGateNavigationLair(group.Key, entry.Key, entry.Name, group.Count());
            })
            .OrderBy(lair => lair.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(lair => lair.LairId)
            .ToArray();
    }

    public IReadOnlyList<RealmGateNavigationBankLocation> GetBankLocations(ClientSessionConnection session)
    {
        var snapshot = GetSnapshot(session);
        var banks = _gameData.LoadFamily(session, "Banks");
        var result = new List<RealmGateNavigationBankLocation>();
        foreach (var bank in banks)
        {
            var bankId = ReadInt(bank.EffectiveRecord, "legacy_id");
            var serviceMatches = bankId > 0
                ? snapshot.Rooms.Values.Where(room => room.ShopId == bankId).ToArray()
                : Array.Empty<RoomNode>();
            if (serviceMatches.Length == 1)
            {
                result.Add(new RealmGateNavigationBankLocation(bank.DisplayName, serviceMatches[0].Key));
                continue;
            }

            var number = ReadInt(bank.EffectiveRecord, "room_number");
            if (number <= 0) continue;
            if (!snapshot.RoomsByNumber.TryGetValue(number, out var matches) || matches.Count != 1)
                continue; // Never invent a map number for an ambiguous legacy room id.
            result.Add(new RealmGateNavigationBankLocation(bank.DisplayName, matches[0].Key));
        }
        return result.OrderBy(bank => bank.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public RealmGateNavigationPlan PlanRoute(
        ClientSessionConnection session,
        string destinationRoomKey,
        RealmGateNavigationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(preferences);
        var snapshot = GetSnapshot(session);
        var start = NormalizeRoomKey(session.CurrentRoomKey);
        var destination = NormalizeRoomKey(destinationRoomKey);
        if (start.Length == 0)
            return Failed(start, destination, "The current room does not expose a map-qualified room key.");
        if (destination.Length == 0)
            return Failed(start, destination, "The destination does not expose a map-qualified room key.");
        if (!snapshot.Rooms.ContainsKey(start))
            return Failed(start, destination, $"Current room {start} is not present in the effective game-data projection.");
        if (!snapshot.Rooms.ContainsKey(destination))
            return Failed(start, destination, $"Destination {destination} is not present in the effective game-data projection.");
        if (start.Equals(destination, StringComparison.OrdinalIgnoreCase))
        {
            return new RealmGateNavigationPlan
            {
                StartRoomKey = start,
                DestinationRoomKey = destination,
                DestinationName = snapshot.Rooms[destination].Name,
                Route = new MegaMudPathRecord
                {
                    FileName = "client-safe-route.mp",
                    Author = "RealmGate Client",
                    StartLabel = snapshot.Rooms[start].Name,
                    EndLabel = snapshot.Rooms[destination].Name,
                    NativeStartKey = start,
                    NativeEndKey = destination
                }
            };
        }

        var strict = FindRoute(snapshot, session, start, destination, preferences, strictAvoidance: true);
        if (strict is not null)
            return BuildPlan(snapshot, start, destination, strict, false);

        if (!preferences.AllowRiskyFallback)
            return Failed(start, destination, "No route satisfies the current hazard-avoidance rules. Risky fallback is disabled.");

        var fallback = FindRoute(snapshot, session, start, destination, preferences, strictAvoidance: false);
        if (fallback is null)
            return Failed(start, destination, "No connected route exists between the selected rooms in the effective game-data graph.");

        return BuildPlan(snapshot, start, destination, fallback, true);
    }

    public RealmGateNavigationPlan PlanNearestLair(
        ClientSessionConnection session,
        RealmGateNavigationPreferences preferences,
        string? nameFilter = null)
    {
        var snapshot = GetSnapshot(session);
        var start = NormalizeRoomKey(session.CurrentRoomKey);
        var lairs = GetLairs(session)
            .Where(lair => string.IsNullOrWhiteSpace(nameFilter) ||
                           lair.Name.Contains(nameFilter.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (lairs.Length == 0)
            return Failed(start, string.Empty, "No matching lair is present in the effective game data.");
        if (start.Length == 0 || !snapshot.Rooms.ContainsKey(start))
            return Failed(start, string.Empty, "A confirmed map-qualified current room is required to find the nearest lair.");

        var targets = lairs.Select(lair => lair.EntryRoomKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var strict = FindRouteToAny(snapshot, session, start, targets, preferences, strictAvoidance: true, out var destination);
        if (strict is not null && destination.Length > 0)
            return BuildPlan(snapshot, start, destination, strict, false);
        if (!preferences.AllowRiskyFallback)
            return Failed(start, string.Empty, "No matching lair entry satisfies the current hazard-avoidance rules.");
        var fallback = FindRouteToAny(snapshot, session, start, targets, preferences, strictAvoidance: false, out destination);
        return fallback is not null && destination.Length > 0
            ? BuildPlan(snapshot, start, destination, fallback, true)
            : Failed(start, string.Empty, "No reachable matching lair was found in the effective game-data graph.");
    }

    public RealmGateNavigationPlan BuildSafeLoop(
        ClientSessionConnection session,
        RealmGateNavigationPreferences preferences,
        int? requiredLairId = null)
    {
        var snapshot = GetSnapshot(session);
        var start = NormalizeRoomKey(session.CurrentRoomKey);
        if (start.Length == 0 || !snapshot.Rooms.TryGetValue(start, out var startRoom))
            return Failed(start, start, "A confirmed map-qualified current room is required to build a loop.");
        if (requiredLairId is > 0 && startRoom.LairId != requiredLairId.Value)
            return Failed(start, start, $"The character is not currently inside lair {requiredLairId.Value}.");

        var minSteps = Math.Clamp(preferences.LoopMinimumSteps, 2, 24);
        var maxSteps = Math.Clamp(preferences.LoopMaximumSteps, minSteps, 32);
        var queue = new Queue<LoopState>();
        queue.Enqueue(new LoopState(start, [], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start }, 0));
        var examined = 0;
        const int stateLimit = 30000;
        LoopState? bestRisky = null;

        while (queue.Count > 0 && examined++ < stateLimit)
        {
            var state = queue.Dequeue();
            if (state.Edges.Count >= maxSteps) continue;
            if (!snapshot.Edges.TryGetValue(state.Room, out var choices)) continue;

            foreach (var edge in choices.OrderBy(edge => edge.BasePenalty))
            {
                var risk = EvaluateEdge(snapshot, session, edge, preferences);
                if (risk.Forbidden) continue;
                if (requiredLairId is > 0 && snapshot.Rooms.TryGetValue(edge.To, out var nextRoom) && nextRoom.LairId != requiredLairId.Value)
                    continue;

                var scoredEdge = edge.WithHazards(risk.Hazards);
                var nextEdges = new List<NavEdge>(state.Edges) { scoredEdge };
                var nextRisk = state.Risk + risk.Penalty;
                if (edge.To.Equals(start, StringComparison.OrdinalIgnoreCase))
                {
                    if (nextEdges.Count >= minSteps)
                    {
                        var loop = BuildPlan(snapshot, start, start, new SearchResult(nextEdges, nextRisk), false);
                        loop.Warnings = loop.Warnings.Concat(new[] { "Generated loop returns to the exact confirmed start room." }).ToArray();
                        return loop;
                    }
                    continue;
                }
                if (state.Visited.Contains(edge.To)) continue;
                var visited = new HashSet<string>(state.Visited, StringComparer.OrdinalIgnoreCase) { edge.To };
                queue.Enqueue(new LoopState(edge.To, nextEdges, visited, nextRisk));
            }
        }

        if (!preferences.AllowRiskyFallback)
            return Failed(start, start, $"No {minSteps}-{maxSteps} step loop satisfies the current hazard rules.");

        // A second bounded search allows avoided hazards but still chooses the first shortest cycle,
        // with queue ordering favoring lower scored risk.
        var riskyQueue = new PriorityQueue<LoopState, (int Steps, int Risk)>();
        riskyQueue.Enqueue(new LoopState(start, [], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start }, 0), (0, 0));
        examined = 0;
        while (riskyQueue.Count > 0 && examined++ < stateLimit)
        {
            var state = riskyQueue.Dequeue();
            if (state.Edges.Count >= maxSteps) continue;
            if (!snapshot.Edges.TryGetValue(state.Room, out var choices)) continue;
            foreach (var edge in choices)
            {
                if (requiredLairId is > 0 && snapshot.Rooms.TryGetValue(edge.To, out var nextRoom) && nextRoom.LairId != requiredLairId.Value)
                    continue;
                var risk = EvaluateEdge(snapshot, session, edge, preferences, ignoreForbidden: true);
                var scoredEdge = edge.WithHazards(risk.Hazards);
                var nextEdges = new List<NavEdge>(state.Edges) { scoredEdge };
                var nextRisk = state.Risk + risk.Penalty;
                if (edge.To.Equals(start, StringComparison.OrdinalIgnoreCase))
                {
                    if (nextEdges.Count >= minSteps)
                    {
                        bestRisky = new LoopState(start, nextEdges, state.Visited, nextRisk);
                        break;
                    }
                    continue;
                }
                if (state.Visited.Contains(edge.To)) continue;
                var visited = new HashSet<string>(state.Visited, StringComparer.OrdinalIgnoreCase) { edge.To };
                riskyQueue.Enqueue(new LoopState(edge.To, nextEdges, visited, nextRisk), (nextEdges.Count, nextRisk));
            }
            if (bestRisky is not null) break;
        }

        if (bestRisky is null)
            return Failed(start, start, "No closed loop could be generated from the current room.");
        var riskyPlan = BuildPlan(snapshot, start, start, new SearchResult(bestRisky.Edges, bestRisky.Risk), true);
        riskyPlan.Warnings = riskyPlan.Warnings.Concat(new[] { "Generated loop uses risky fallback and returns to the exact confirmed start room." }).ToArray();
        return riskyPlan;
    }

    public RealmGateNavigationPlan PlanRandomSafeStep(
        ClientSessionConnection session,
        RealmGateNavigationPreferences preferences,
        string? avoidDestinationRoomKey = null)
    {
        var snapshot = GetSnapshot(session);
        var start = NormalizeRoomKey(session.CurrentRoomKey);
        if (start.Length == 0 || !snapshot.Rooms.ContainsKey(start))
            return Failed(start, string.Empty, "Auto-Roam is waiting for a confirmed map-qualified current room.");
        if (!snapshot.Edges.TryGetValue(start, out var outgoing) || outgoing.Count == 0)
            return Failed(start, string.Empty, "Auto-Roam found no decoded exits from the current room.");

        var safe = outgoing
            .Select(edge => (Edge: edge, Risk: EvaluateEdge(snapshot, session, edge, preferences, ignoreForbidden: false)))
            .Where(candidate => !candidate.Risk.Forbidden)
            .ToList();

        var usedRiskyFallback = false;
        if (safe.Count == 0 && preferences.AllowRiskyFallback)
        {
            usedRiskyFallback = true;
            var risky = outgoing
                .Select(edge => (Edge: edge, Risk: EvaluateEdge(snapshot, session, edge, preferences, ignoreForbidden: true)))
                .OrderBy(candidate => candidate.Risk.Penalty)
                .ToList();
            if (risky.Count > 0)
            {
                var bestPenalty = risky[0].Risk.Penalty;
                safe = risky.Where(candidate => candidate.Risk.Penalty == bestPenalty).ToList();
            }
        }

        if (safe.Count == 0)
            return Failed(start, string.Empty, "Auto-Roam is waiting because every decoded exit is blocked by the current navigation safety rules.");

        var avoid = NormalizeRoomKey(avoidDestinationRoomKey);
        if (avoid.Length > 0 && safe.Count > 1)
        {
            var withoutImmediateBacktrack = safe
                .Where(candidate => !candidate.Edge.To.Equals(avoid, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (withoutImmediateBacktrack.Count > 0)
                safe = withoutImmediateBacktrack;
        }

        var selected = safe[Random.Shared.Next(safe.Count)];
        var edge = selected.Edge.WithHazards(selected.Risk.Hazards);
        return BuildPlan(
            snapshot,
            start,
            edge.To,
            new SearchResult([edge], selected.Risk.Penalty),
            usedRiskyFallback);
    }

    public string DescribeRoom(ClientSessionConnection session, string roomKey)
    {
        var snapshot = GetSnapshot(session);
        var key = NormalizeRoomKey(roomKey);
        if (!snapshot.Rooms.TryGetValue(key, out var room)) return key;
        var details = new List<string> { room.Name, room.Key };
        if (room.LairId > 0) details.Add($"lair {room.LairId}");
        if (room.RoomSpellId > 0) details.Add($"room effect {room.RoomSpellId}");
        if (room.MaximumNpcLevel > 0) details.Add($"max NPC level {room.MaximumNpcLevel}");
        details.Add($"{room.ExitCount} exit(s)");
        return string.Join(" • ", details);
    }

    public void Invalidate() => _snapshotSignature = string.Empty;

    private RealmGateNavigationPlan BuildPlan(
        NavigationSnapshot snapshot,
        string start,
        string destination,
        SearchResult result,
        bool riskyFallback)
    {
        var steps = new ObservableCollection<MegaMudPathStep>();
        var hazards = new List<RealmGateNavigationHazard>();
        var warnings = new List<string>();
        var index = 1;
        foreach (var edge in result.Edges)
        {
            if (edge.PreCommands.Count > 0)
            {
                // Only commands explicitly decoded as a deterministic transition command are included.
                // We never synthesize open/unlock/cast/toll actions from a qualifier.
                foreach (var command in edge.PreCommands)
                {
                    steps.Add(new MegaMudPathStep
                    {
                        Number = index++,
                        NativeRoomKey = edge.From,
                        Command = command,
                        FriendlyCommand = command,
                        IsMovement = false,
                        ExpectedNativeRoomKey = string.Empty
                    });
                }
            }
            steps.Add(new MegaMudPathStep
            {
                Number = index++,
                NativeRoomKey = edge.From,
                Command = edge.Command,
                FriendlyCommand = edge.Command,
                IsMovement = true,
                ExpectedNativeRoomKey = edge.To
            });

            foreach (var hazard in edge.Hazards)
                if (!hazards.Any(existing => existing.RoomKey == hazard.RoomKey && existing.Category == hazard.Category && existing.Description == hazard.Description))
                    hazards.Add(hazard);
        }
        foreach (var hazard in hazards)
            warnings.Add($"{hazard.Category}: {hazard.Description}");
        if (riskyFallback)
            warnings.Insert(0, "Strict hazard avoidance found no route; this is the least-risk connected fallback found by the client.");

        var startName = snapshot.Rooms.TryGetValue(start, out var startRoom) ? startRoom.Name : start;
        var destinationName = snapshot.Rooms.TryGetValue(destination, out var endRoom) ? endRoom.Name : destination;
        return new RealmGateNavigationPlan
        {
            StartRoomKey = start,
            DestinationRoomKey = destination,
            DestinationName = destinationName,
            UsedRiskyFallback = riskyFallback,
            RiskScore = result.Risk,
            Hazards = hazards,
            Warnings = warnings,
            Route = new MegaMudPathRecord
            {
                FileName = destination.Equals(start, StringComparison.OrdinalIgnoreCase) ? "client-safe-loop.mp" : "client-safe-route.mp",
                Author = "RealmGate Client Navigation",
                StartLabel = startName,
                EndLabel = destinationName,
                NativeStartKey = start,
                NativeEndKey = destination,
                DeclaredStepCount = steps.Count,
                Steps = steps
            }
        };
    }

    private SearchResult? FindRoute(
        NavigationSnapshot snapshot,
        ClientSessionConnection session,
        string start,
        string destination,
        RealmGateNavigationPreferences preferences,
        bool strictAvoidance)
    {
        var distances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [start] = 0 };
        var previous = new Dictionary<string, NavEdge>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, int>();
        queue.Enqueue(start, 0);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            if (!visited.Add(room)) continue;
            if (room.Equals(destination, StringComparison.OrdinalIgnoreCase)) break;
            if (!snapshot.Edges.TryGetValue(room, out var edges)) continue;
            foreach (var edge in edges)
            {
                var risk = EvaluateEdge(snapshot, session, edge, preferences, ignoreForbidden: !strictAvoidance);
                if (strictAvoidance && risk.Forbidden) continue;
                var stepCost = 1 + edge.BasePenalty + risk.Penalty;
                var nextCost = distances[room] + stepCost;
                if (distances.TryGetValue(edge.To, out var existing) && existing <= nextCost) continue;
                distances[edge.To] = nextCost;
                previous[edge.To] = edge.WithHazards(risk.Hazards);
                queue.Enqueue(edge.To, nextCost);
            }
        }

        if (!previous.ContainsKey(destination)) return null;
        var chain = new List<NavEdge>();
        var cursor = destination;
        var totalRisk = 0;
        while (!cursor.Equals(start, StringComparison.OrdinalIgnoreCase))
        {
            if (!previous.TryGetValue(cursor, out var edge)) return null;
            chain.Add(edge);
            foreach (var hazard in edge.Hazards) totalRisk += hazard.Penalty;
            cursor = edge.From;
        }
        chain.Reverse();
        return new SearchResult(chain, totalRisk);
    }

    private RiskResult EvaluateEdge(
        NavigationSnapshot snapshot,
        ClientSessionConnection session,
        NavEdge edge,
        RealmGateNavigationPreferences preferences,
        bool ignoreForbidden = false)
    {
        var hazards = new List<RealmGateNavigationHazard>();
        var forbidden = false;
        var penalty = 0;
        void Add(string category, string description, int score, bool avoid)
        {
            penalty += score;
            hazards.Add(new RealmGateNavigationHazard(edge.To, category, description, score));
            if (avoid && !ignoreForbidden) forbidden = true;
        }

        switch (edge.GateKind)
        {
            case "damage_trap":
                Add("Damage trap", string.IsNullOrWhiteSpace(edge.Qualifier) ? $"Trapped exit into {edge.To}." : edge.Qualifier, 500, preferences.AvoidDamageTraps);
                break;
            case "spell_trap":
                Add("Spell trap", string.IsNullOrWhiteSpace(edge.Qualifier) ? $"Spell-trapped exit into {edge.To}." : edge.Qualifier, 500, preferences.AvoidSpellTraps);
                break;
            case "door":
                if (edge.HasRestrictions)
                    Add("Restricted door", string.IsNullOrWhiteSpace(edge.Qualifier) ? "Door has decoded traversal prerequisites." : edge.Qualifier, 45, preferences.AvoidRestrictedExits);
                else if (!preferences.AllowOrdinaryDoors)
                    Add("Door", string.IsNullOrWhiteSpace(edge.Qualifier) ? "Ordinary door transition." : edge.Qualifier, 20, true);
                else
                    penalty += 1;
                break;
            case "hidden":
                Add("Hidden exit", edge.Qualifier, 30, preferences.AvoidHiddenExits || preferences.AvoidRestrictedExits);
                break;
            case "open":
                break;
            case "text_command":
                penalty += 3;
                break;
            default:
                Add("Restricted transition", $"{edge.GateKind}: {edge.Qualifier}".TrimEnd(':', ' '), 60, preferences.AvoidRestrictedExits);
                break;
        }

        if (snapshot.Rooms.TryGetValue(edge.To, out var room))
        {
            if (preferences.AvoidRoomSpellEffects && room.RoomSpellId > 0)
                Add("Room effect", $"{room.Name} has decoded room spell/effect {room.RoomSpellId}.", 80, true);

            if (session.CharacterLevel > 0)
            {
                var threshold = Math.Max(1, session.CharacterLevel + Math.Clamp(preferences.NpcLevelBuffer, 0, 99));
                if (room.MaximumNpcLevel > threshold)
                    Add("High-level NPC", $"{room.Name} contains captured NPCs up to level {room.MaximumNpcLevel}; threshold is {threshold}.",
                        Math.Min(250, 20 + (room.MaximumNpcLevel - threshold) * 4), preferences.AvoidHighLevelNpcRooms);
            }
        }

        return new RiskResult(forbidden, penalty, hazards);
    }

    private NavigationSnapshot GetSnapshot(ClientSessionConnection session)
    {
        var context = _gameData.ResolveContext(session);
        var signature = BuildSnapshotSignature(context);
        if (_snapshot is not null && string.Equals(_snapshotSignature, signature, StringComparison.Ordinal))
            return _snapshot;

        var rooms = new Dictionary<string, RoomNode>(StringComparer.OrdinalIgnoreCase);
        var byNumber = new Dictionary<int, List<RoomNode>>();
        foreach (var record in _gameData.LoadFamily(session, "Rooms"))
        {
            var map = ReadInt(record.EffectiveRecord, "map_number");
            var number = ReadInt(record.EffectiveRecord, "room_number");
            if (map <= 0 || number <= 0) continue;
            var key = CanonicalKey(map, number);
            var environment = record.EffectiveRecord["environment"] as JsonObject;
            var node = new RoomNode
            {
                Key = key,
                Name = FirstNonBlank(ReadString(record.EffectiveRecord, "name"), record.DisplayName, key),
                MapNumber = map,
                RoomNumber = number,
                LairId = ReadInt(environment, "room_lair_id"),
                RoomSpellId = ReadInt(environment, "room_spell_id"),
                ShopId = ReadInt(environment, "shop_id")
            };
            rooms[key] = node;
            if (!byNumber.TryGetValue(number, out var list)) byNumber[number] = list = [];
            list.Add(node);
        }

        var npcLevelsById = new Dictionary<int, int>();
        var npcLevelsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in _gameData.LoadFamily(session, "Monsters / NPCs"))
        {
            var id = ReadInt(npc.EffectiveRecord, "legacy_id");
            var level = ReadInt(npc.EffectiveRecord["combat"] as JsonObject, "level");
            var name = FirstNonBlank(ReadString(npc.EffectiveRecord, "name"), npc.DisplayName);
            if (id > 0) npcLevelsById[id] = level;
            if (name.Length > 0) npcLevelsByName[name] = level;
        }
        foreach (var spawn in _gameData.LoadFamily(session, "Spawns"))
        {
            var map = ReadInt(spawn.EffectiveRecord, "map_id");
            var roomNumber = ReadInt(spawn.EffectiveRecord, "room_number");
            var roomKey = map > 0 && roomNumber > 0 ? CanonicalKey(map, roomNumber) : NormalizeRoomKey(ReadString(spawn.EffectiveRecord, "qualified_room_ref"));
            if (roomKey.Length == 0 || !rooms.TryGetValue(roomKey, out var room)) continue;
            var npcId = ReadInt(spawn.EffectiveRecord, "npc_legacy_id");
            var npcName = ReadString(spawn.EffectiveRecord, "npc_name");
            var level = npcId > 0 && npcLevelsById.TryGetValue(npcId, out var byId)
                ? byId
                : npcLevelsByName.TryGetValue(npcName, out var byName) ? byName : 0;
            room.MaximumNpcLevel = Math.Max(room.MaximumNpcLevel, level);
        }

        var edges = new Dictionary<string, List<NavEdge>>(StringComparer.OrdinalIgnoreCase);
        var allEdges = new List<NavEdge>();
        foreach (var exit in _gameData.LoadFamily(session, "Exits / Paths"))
        {
            var fromMap = ReadInt(exit.EffectiveRecord, "from_map_number");
            var fromRoom = ReadInt(exit.EffectiveRecord, "from_room_number");
            var toMap = ReadInt(exit.EffectiveRecord, "to_map_number");
            var toRoom = ReadInt(exit.EffectiveRecord, "to_room_number");
            if (fromMap <= 0 || fromRoom <= 0 || toMap <= 0 || toRoom <= 0) continue;
            var from = CanonicalKey(fromMap, fromRoom);
            var to = CanonicalKey(toMap, toRoom);
            if (!rooms.ContainsKey(from) || !rooms.ContainsKey(to)) continue;
            var direction = ReadString(exit.EffectiveRecord, "direction");
            var gate = ReadString(exit.EffectiveRecord, "gate_kind").ToLowerInvariant();
            var qualifier = ReadString(exit.EffectiveRecord, "qualifier");
            var explicitCommands = ReadStringArray(exit.EffectiveRecord["commands"]);
            var hasRestrictions = HasMeaningfulRestrictions(exit.EffectiveRecord["prerequisites"] as JsonObject);
            var movementCommand = ResolveMovementCommand(direction, gate, explicitCommands);
            if (movementCommand.Length == 0) continue;
            var preCommands = Array.Empty<string>();
            var edge = new NavEdge(from, to, movementCommand, gate, qualifier, preCommands, BasePenalty(gate), hasRestrictions, []);
            if (!edges.TryGetValue(from, out var list)) edges[from] = list = [];
            if (list.Any(existing => existing.To.Equals(to, StringComparison.OrdinalIgnoreCase) && existing.Command.Equals(movementCommand, StringComparison.OrdinalIgnoreCase)))
                continue;
            list.Add(edge);
            allEdges.Add(edge);
            rooms[from].ExitCount++;
        }

        _snapshot = new NavigationSnapshot(rooms, byNumber, edges, allEdges);
        _snapshotSignature = signature;
        return _snapshot;
    }

    private string BuildSnapshotSignature(RealmGateGameDataContext context)
    {
        var builder = new StringBuilder();
        builder.Append(context.Mode).Append('|').Append(context.BaseDataRoot).Append('|').Append(context.ServerKey);
        foreach (var file in new[] { "rooms.json", "exits.json", "spawns.json", "npcs.json", "banks.json" })
        {
            var path = Path.Combine(context.BaseDataRoot, file);
            if (File.Exists(path)) builder.Append('|').Append(File.GetLastWriteTimeUtc(path).Ticks);
        }
        if (!string.IsNullOrWhiteSpace(context.OverlayPath) && File.Exists(context.OverlayPath))
            builder.Append('|').Append(File.GetLastWriteTimeUtc(context.OverlayPath).Ticks);
        return builder.ToString();
    }

    private SearchResult? FindRouteToAny(
        NavigationSnapshot snapshot,
        ClientSessionConnection session,
        string start,
        HashSet<string> targets,
        RealmGateNavigationPreferences preferences,
        bool strictAvoidance,
        out string destination)
    {
        destination = string.Empty;
        if (targets.Contains(start))
        {
            destination = start;
            return new SearchResult([], 0);
        }
        var distances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [start] = 0 };
        var previous = new Dictionary<string, NavEdge>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, int>();
        queue.Enqueue(start, 0);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            if (!visited.Add(room)) continue;
            if (targets.Contains(room))
            {
                destination = room;
                break;
            }
            if (!snapshot.Edges.TryGetValue(room, out var edges)) continue;
            foreach (var edge in edges)
            {
                var risk = EvaluateEdge(snapshot, session, edge, preferences, ignoreForbidden: !strictAvoidance);
                if (strictAvoidance && risk.Forbidden) continue;
                var nextCost = distances[room] + 1 + edge.BasePenalty + risk.Penalty;
                if (distances.TryGetValue(edge.To, out var existing) && existing <= nextCost) continue;
                distances[edge.To] = nextCost;
                previous[edge.To] = edge.WithHazards(risk.Hazards);
                queue.Enqueue(edge.To, nextCost);
            }
        }
        if (destination.Length == 0) return null;
        if (destination.Equals(start, StringComparison.OrdinalIgnoreCase)) return new SearchResult([], 0);
        var chain = new List<NavEdge>();
        var cursor = destination;
        var totalRisk = 0;
        while (!cursor.Equals(start, StringComparison.OrdinalIgnoreCase))
        {
            if (!previous.TryGetValue(cursor, out var edge)) return null;
            chain.Add(edge);
            foreach (var hazard in edge.Hazards) totalRisk += hazard.Penalty;
            cursor = edge.From;
        }
        chain.Reverse();
        return new SearchResult(chain, totalRisk);
    }

    private static RealmGateNavigationPlan Failed(string start, string destination, string warning) => new()
    {
        StartRoomKey = start,
        DestinationRoomKey = destination,
        Warnings = [warning]
    };

    private static string ResolveMovementCommand(string direction, string gateKind, IReadOnlyList<string> explicitCommands)
    {
        if (gateKind.Equals("text_command", StringComparison.OrdinalIgnoreCase) && explicitCommands.Count > 0)
            return explicitCommands[0].Trim(); // commands are decoded alternatives; choose the first canonical form only.
        return direction.Trim().ToUpperInvariant() switch
        {
            "N" => "north", "NE" => "northeast", "E" => "east", "SE" => "southeast",
            "S" => "south", "SW" => "southwest", "W" => "west", "NW" => "northwest",
            "U" => "up", "D" => "down",
            _ => string.Empty
        };
    }

    private static bool HasMeaningfulRestrictions(JsonObject? prerequisites)
    {
        if (prerequisites is null || prerequisites.Count == 0) return false;
        foreach (var pair in prerequisites)
        {
            if (pair.Value is null) continue;
            if (pair.Value is JsonValue value)
            {
                if (value.TryGetValue<bool>(out var b) && b) return true;
                if (value.TryGetValue<int>(out var i) && i != 0) return true;
                if (value.TryGetValue<long>(out var l) && l != 0) return true;
                if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) && text != "0") return true;
            }
            else
            {
                var json = pair.Value.ToJsonString();
                if (json.Length > 2 && json != "[]" && json != "{}") return true;
            }
        }
        return false;
    }

    private static int BasePenalty(string gateKind) => gateKind.ToLowerInvariant() switch
    {
        "open" => 0,
        "door" => 1,
        "text_command" => 2,
        "hidden" => 4,
        "damage_trap" or "spell_trap" => 25,
        _ => 8
    };

    public static string NormalizeRoomKey(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return string.Empty;
        var match = CanonicalRoomIdRegex().Match(text);
        if (match.Success) return CanonicalKey(ParseGroup(match, "map"), ParseGroup(match, "room"));
        match = MapRoomRegex().Match(text);
        if (match.Success) return CanonicalKey(ParseGroup(match, "map"), ParseGroup(match, "room"));
        match = SlashRoomRegex().Match(text);
        if (match.Success) return CanonicalKey(ParseGroup(match, "map"), ParseGroup(match, "room"));
        return text.ToUpperInvariant();
    }

    private static int ParseGroup(Match match, string name) =>
        int.TryParse(match.Groups[name].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static string CanonicalKey(int map, int room) => map > 0 && room > 0 ? $"{map}/{room}" : string.Empty;

    private static int ReadInt(JsonObject? obj, string property)
    {
        if (obj is null || !TryGetProperty(obj, property, out var node) || node is null) return 0;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<long>(out var l) && l is >= int.MinValue and <= int.MaxValue) return (int)l;
            if (value.TryGetValue<double>(out var d) && d is >= int.MinValue and <= int.MaxValue) return (int)d;
            if (value.TryGetValue<string>(out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }
        return int.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fallback) ? fallback : 0;
    }

    private static string ReadString(JsonObject? obj, string property)
    {
        if (obj is null || !TryGetProperty(obj, property, out var node) || node is null) return string.Empty;
        if (node is JsonValue value && value.TryGetValue<string>(out var s)) return s ?? string.Empty;
        return node.ToString();
    }

    private static bool TryGetProperty(JsonObject obj, string property, out JsonNode? node)
    {
        if (obj.TryGetPropertyValue(property, out node)) return true;
        var pair = obj.FirstOrDefault(pair => pair.Key.Equals(property, StringComparison.OrdinalIgnoreCase));
        node = pair.Value;
        return pair.Key is not null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
    {
        if (node is not JsonArray array) return [];
        var result = new List<string>();
        foreach (var item in array)
        {
            if (item is null) continue;
            var text = item is JsonValue value && value.TryGetValue<string>(out var s) ? s ?? string.Empty : item.ToString();
            if (!string.IsNullOrWhiteSpace(text)) result.Add(text.Trim());
        }
        return result;
    }

    private string BuildProfileKey(ClientSessionConnection session)
    {
        var context = _gameData.ResolveContext(session);
        var raw = context.Mode == RealmGateGameDataAuthorityMode.ReforgedWorld
            ? $"reforged|{context.ServerKey}|{context.BaseWorld}"
            : $"bbs|{context.ServerKey}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..16];
        var label = Sanitize(FirstNonBlank(session.ServerProfileName, session.HostName, "server"));
        return $"{label}-{hash}";
    }

    private string ProfilePath(string key) => Path.Combine(_profileRoot, key + ".json");

    private static RealmGateNavigationProfile NewProfile(string key) => new() { ServerKey = key };

    private static void NormalizeProfile(RealmGateNavigationProfile profile, string key)
    {
        profile.Version = 1;
        profile.ServerKey = key;
        profile.Preferences ??= new RealmGateNavigationPreferences();
        profile.Preferences.NpcLevelBuffer = Math.Clamp(profile.Preferences.NpcLevelBuffer, 0, 99);
        profile.Preferences.LoopMinimumSteps = Math.Clamp(profile.Preferences.LoopMinimumSteps, 2, 24);
        profile.Preferences.LoopMaximumSteps = Math.Clamp(profile.Preferences.LoopMaximumSteps, profile.Preferences.LoopMinimumSteps, 32);
        profile.SavedDestinations ??= [];
        foreach (var destination in profile.SavedDestinations)
        {
            if (string.IsNullOrWhiteSpace(destination.Id)) destination.Id = Guid.NewGuid().ToString("N");
            destination.RoomKey = NormalizeRoomKey(destination.RoomKey);
            destination.Name = FirstNonBlank(destination.Name, destination.RoomKey, "Saved location");
        }
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-').ToArray();
        var result = Regex.Replace(new string(chars), "-+", "-").Trim('-');
        return result.Length == 0 ? "server" : result.Length <= 40 ? result : result[..40];
    }

    private static void BackupCorrupt(string path)
    {
        try
        {
            if (File.Exists(path)) File.Move(path, path + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}", true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    [GeneratedRegex(@"^\s*room\.map(?<map>\d+)\.(?<room>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalRoomIdRegex();

    [GeneratedRegex(@"^\s*map(?<map>\d+)\s*[-_.]\s*room(?<room>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MapRoomRegex();

    [GeneratedRegex(@"^\s*(?<map>\d+)\s*[/:-]\s*(?<room>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SlashRoomRegex();

    private sealed class RoomNode
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int MapNumber { get; init; }
        public int RoomNumber { get; init; }
        public int LairId { get; init; }
        public int RoomSpellId { get; init; }
        public int ShopId { get; init; }
        public int MaximumNpcLevel { get; set; }
        public int ExitCount { get; set; }
        public RealmGateNavigationRoom Public => new(Key, Name, MapNumber, RoomNumber, LairId, RoomSpellId, MaximumNpcLevel, ExitCount);
    }

    private sealed record NavEdge(
        string From,
        string To,
        string Command,
        string GateKind,
        string Qualifier,
        IReadOnlyList<string> PreCommands,
        int BasePenalty,
        bool HasRestrictions,
        IReadOnlyList<RealmGateNavigationHazard> Hazards)
    {
        public NavEdge WithHazards(IReadOnlyList<RealmGateNavigationHazard> hazards) => this with { Hazards = hazards };
    }

    private sealed record NavigationSnapshot(
        IReadOnlyDictionary<string, RoomNode> Rooms,
        IReadOnlyDictionary<int, List<RoomNode>> RoomsByNumber,
        IReadOnlyDictionary<string, List<NavEdge>> Edges,
        IReadOnlyList<NavEdge> AllEdges);

    private sealed record SearchResult(IReadOnlyList<NavEdge> Edges, int Risk);
    private sealed record RiskResult(bool Forbidden, int Penalty, IReadOnlyList<RealmGateNavigationHazard> Hazards);
    private sealed record LoopState(string Room, IReadOnlyList<NavEdge> Edges, HashSet<string> Visited, int Risk);
}
