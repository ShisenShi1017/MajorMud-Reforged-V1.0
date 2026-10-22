using System.Text.Json;

namespace RealmGate.Client.Navigation;

public sealed record ClientRouteRoom(
    string Key,
    string Name,
    int MapNumber,
    int RoomNumber,
    int RoomSpellId = 0,
    int MaximumNpcLevel = 0)
{
    public string DisplayName => $"{Name} [{Key}]";
}

public sealed record ClientRouteStep(
    int Number,
    string FromRoomKey,
    string ToRoomKey,
    string Command);

public sealed record ClientRoutePlan(
    string StartRoomKey,
    string DestinationRoomKey,
    IReadOnlyList<ClientRouteStep> Steps)
{
    public bool UsedRiskyFallback { get; init; }
    public int RiskScore { get; init; }
    public IReadOnlyList<ClientRouteHazard> Hazards { get; init; } = [];
}

public sealed record ClientRouteHazard(
    string RoomKey,
    string Category,
    string Description,
    int Penalty);

public sealed class ClientRoutePreferences
{
    public bool AvoidDamageTraps { get; set; } = true;
    public bool AvoidSpellTraps { get; set; } = true;
    public bool AvoidRestrictedExits { get; set; } = true;
    public bool AvoidHiddenExits { get; set; }
    public bool AllowOrdinaryDoors { get; set; } = true;
    public bool AvoidHighLevelNpcRooms { get; set; } = true;
    public int NpcLevelBuffer { get; set; } = 5;
    public bool AvoidRoomSpellEffects { get; set; }
    public bool AllowRiskyFallback { get; set; }
}

public sealed class ClientNavigationAutomationOptions
{
    public ClientRoutePreferences Preferences { get; set; } = new();
    public bool AutoRecoverDesynchronizedRoute { get; set; } = true;
    public int MaximumRecoveryAttempts { get; set; } = 3;
    public int AutoRoamDelayMs { get; set; } = 1500;
    public int CharacterLevel { get; set; } = 1;
}

public sealed class ClientRouteGraph
{
    private readonly Dictionary<string, ClientRouteRoom> _rooms;
    private readonly Dictionary<string, IReadOnlyList<RouteEdge>> _edges;

    private ClientRouteGraph(
        Dictionary<string, ClientRouteRoom> rooms,
        Dictionary<string, IReadOnlyList<RouteEdge>> edges,
        string worldId,
        string schema)
    {
        _rooms = rooms;
        _edges = edges;
        WorldId = worldId;
        Schema = schema;
    }

    public int RoomCount => _rooms.Count;
    public IReadOnlyCollection<ClientRouteRoom> Rooms => _rooms.Values;
    public string WorldId { get; }
    public string Schema { get; }
    public bool HasHazardMetadata => Schema.Equals(
        "realmgate_navigation_graph_v2",
        StringComparison.OrdinalIgnoreCase);

    public bool ContainsRoom(string roomKey) =>
        _rooms.ContainsKey(NormalizeRoomKey(roomKey));

    public static ClientRouteGraph Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static ClientRouteGraph Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var worldId = ReadString(root, "worldId").Trim();
        var schema = ReadString(root, "schema").Trim();
        var rooms = new Dictionary<string, ClientRouteRoom>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("rooms", out var roomArray) && roomArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in roomArray.EnumerateArray())
            {
                var key = NormalizeRoomKey(ReadString(element, "key"));
                if (key.Length == 0 || rooms.ContainsKey(key)) continue;
                var map = ReadInt(element, "map");
                var number = ReadInt(element, "room");
                var name = ReadString(element, "name").Trim();
                rooms[key] = new(
                    key,
                    name.Length == 0 ? $"Room {key}" : name,
                    map,
                    number,
                    ReadInt(element, "roomSpellId"),
                    ReadInt(element, "maximumNpcLevel"));
            }
        }

        var mutableEdges = new Dictionary<string, List<RouteEdge>>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("edges", out var edgeArray) && edgeArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in edgeArray.EnumerateArray())
            {
                var from = NormalizeRoomKey(ReadString(element, "from"));
                var to = NormalizeRoomKey(ReadString(element, "to"));
                var command = ReadString(element, "command").Trim();
                if (!rooms.ContainsKey(from) || !rooms.ContainsKey(to) || command.Length == 0) continue;
                if (!mutableEdges.TryGetValue(from, out var list)) mutableEdges[from] = list = [];
                if (!list.Any(edge => edge.To.Equals(to, StringComparison.OrdinalIgnoreCase) &&
                                      edge.Command.Equals(command, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(new(
                        to,
                        command,
                        ReadString(element, "gateKind").Trim().ToLowerInvariant(),
                        ReadString(element, "qualifier").Trim(),
                        ReadBoolean(element, "hasRestrictions")));
                }
            }
        }

        return new(
            rooms,
            mutableEdges.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<RouteEdge>)pair.Value
                    .OrderBy(edge => edge.Command, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(edge => edge.To, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase),
            worldId,
            schema);
    }

    public IReadOnlyList<ClientRouteRoom> Search(string? query, int limit = 500)
    {
        var text = query?.Trim() ?? string.Empty;
        var normalized = NormalizeRoomKey(text);
        return _rooms.Values
            .Where(room => text.Length == 0 ||
                           room.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                           normalized.Length > 0 && room.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(room => room.Key, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 5000))
            .ToArray();
    }

    public ClientRoutePlan? Plan(string startRoomKey, string destinationRoomKey) =>
        Plan(startRoomKey, destinationRoomKey, new ClientRoutePreferences(), 0);

    public ClientRoutePlan? Plan(
        string startRoomKey,
        string destinationRoomKey,
        ClientRoutePreferences preferences,
        int characterLevel)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var start = NormalizeRoomKey(startRoomKey);
        var destination = NormalizeRoomKey(destinationRoomKey);
        if (!_rooms.ContainsKey(start) || !_rooms.ContainsKey(destination)) return null;
        if (start.Equals(destination, StringComparison.OrdinalIgnoreCase))
            return new(start, destination, []);

        var strict = FindRoute(start, destination, preferences, characterLevel, ignoreForbidden: false);
        if (strict is not null) return BuildPlan(start, destination, strict, false);
        if (!preferences.AllowRiskyFallback) return null;
        var fallback = FindRoute(start, destination, preferences, characterLevel, ignoreForbidden: true);
        return fallback is null ? null : BuildPlan(start, destination, fallback, true);
    }

    public IReadOnlyList<ClientRouteStep> GetSafeRoamChoices(
        string currentRoomKey,
        string? previousRoomKey,
        ClientRoutePreferences preferences,
        int characterLevel)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var current = NormalizeRoomKey(currentRoomKey);
        var previous = NormalizeRoomKey(previousRoomKey);
        if (!_rooms.ContainsKey(current) || !_edges.TryGetValue(current, out var choices)) return [];
        return choices
            .Select(edge => (Edge: edge, Risk: EvaluateEdge(edge, preferences, characterLevel, false)))
            .Where(choice => !choice.Risk.Forbidden)
            .OrderBy(choice => choice.Edge.To.Equals(previous, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(choice => BasePenalty(choice.Edge.GateKind) + choice.Risk.Penalty)
            .ThenBy(choice => choice.Edge.Command, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.Edge.To, StringComparer.OrdinalIgnoreCase)
            .Select(choice => new ClientRouteStep(1, current, choice.Edge.To, choice.Edge.Command))
            .ToArray();
    }

    private ClientRouteSearchResult? FindRoute(
        string start,
        string destination,
        ClientRoutePreferences preferences,
        int characterLevel,
        bool ignoreForbidden)
    {
        var distances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [start] = 0 };
        var previous = new Dictionary<string, SearchLink>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, int>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue(start, 0);
        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            if (!visited.Add(room)) continue;
            if (room.Equals(destination, StringComparison.OrdinalIgnoreCase)) break;
            if (!_edges.TryGetValue(room, out var choices)) continue;
            foreach (var edge in choices)
            {
                var risk = EvaluateEdge(edge, preferences, characterLevel, ignoreForbidden);
                if (risk.Forbidden) continue;
                var nextCost = distances[room] + 1 + BasePenalty(edge.GateKind) + risk.Penalty;
                if (distances.TryGetValue(edge.To, out var existing) && existing <= nextCost) continue;
                distances[edge.To] = nextCost;
                previous[edge.To] = new(room, edge, risk.Hazards);
                queue.Enqueue(edge.To, nextCost);
            }
        }
        if (!previous.ContainsKey(destination)) return null;

        var chain = new List<SearchLink>();
        var totalRisk = 0;
        for (var cursor = destination; !cursor.Equals(start, StringComparison.OrdinalIgnoreCase);)
        {
            var link = previous[cursor];
            chain.Add(link);
            totalRisk += link.Hazards.Sum(hazard => hazard.Penalty);
            cursor = link.From;
        }
        chain.Reverse();
        return new(chain, totalRisk);
    }

    private static ClientRoutePlan BuildPlan(
        string start,
        string destination,
        ClientRouteSearchResult result,
        bool usedRiskyFallback) =>
        new(
            start,
            destination,
            result.Links.Select((link, index) =>
                    new ClientRouteStep(index + 1, link.From, link.Edge.To, link.Edge.Command))
                .ToArray())
        {
            UsedRiskyFallback = usedRiskyFallback,
            RiskScore = result.RiskScore,
            Hazards = result.Links.SelectMany(link => link.Hazards).ToArray()
        };

    private ClientRouteRiskResult EvaluateEdge(
        RouteEdge edge,
        ClientRoutePreferences preferences,
        int characterLevel,
        bool ignoreForbidden)
    {
        var hazards = new List<ClientRouteHazard>();
        var forbidden = false;
        var penalty = 0;
        void Add(string category, string description, int score, bool avoid)
        {
            penalty += score;
            hazards.Add(new(edge.To, category, description, score));
            if (avoid && !ignoreForbidden) forbidden = true;
        }

        switch (edge.GateKind)
        {
            case "":
            case "open":
                break;
            case "damage_trap":
                Add("Damage trap", Description(edge, "Decoded damage trap."), 500, preferences.AvoidDamageTraps);
                break;
            case "spell_trap":
                Add("Spell trap", Description(edge, "Decoded spell trap."), 500, preferences.AvoidSpellTraps);
                break;
            case "door":
                if (edge.HasRestrictions)
                    Add("Restricted door", Description(edge, "Door has decoded traversal prerequisites."), 45, preferences.AvoidRestrictedExits);
                else if (!preferences.AllowOrdinaryDoors)
                    Add("Door", Description(edge, "Ordinary door transition."), 20, true);
                break;
            case "hidden":
                Add("Hidden exit", Description(edge, "Decoded hidden exit."), 30,
                    preferences.AvoidHiddenExits || preferences.AvoidRestrictedExits);
                break;
            case "text_command":
                penalty += 3;
                break;
            default:
                Add("Restricted transition", Description(edge, edge.GateKind), 60, preferences.AvoidRestrictedExits);
                break;
        }

        if (_rooms.TryGetValue(edge.To, out var room))
        {
            if (preferences.AvoidRoomSpellEffects && room.RoomSpellId > 0)
                Add("Room effect", $"{room.Name} has decoded room effect {room.RoomSpellId}.", 80, true);
            if (characterLevel > 0)
            {
                var threshold = Math.Max(1, characterLevel + Math.Clamp(preferences.NpcLevelBuffer, 0, 99));
                if (room.MaximumNpcLevel > threshold)
                    Add("High-level NPC",
                        $"{room.Name} contains NPCs up to level {room.MaximumNpcLevel}; threshold is {threshold}.",
                        Math.Min(250, 20 + (room.MaximumNpcLevel - threshold) * 4),
                        preferences.AvoidHighLevelNpcRooms);
            }
        }

        return new(forbidden, penalty, hazards);
    }

    private static int BasePenalty(string gateKind) => gateKind switch
    {
        "" or "open" => 0,
        "door" => 1,
        "text_command" => 2,
        "hidden" => 4,
        "damage_trap" or "spell_trap" => 25,
        _ => 8
    };

    private static string Description(RouteEdge edge, string fallback) =>
        string.IsNullOrWhiteSpace(edge.Qualifier) ? fallback : edge.Qualifier;

    public static string NormalizeRoomKey(string? value)
    {
        var text = value?.Trim().Trim('[', ']', '(', ')') ?? string.Empty;
        if (text.StartsWith("room.map", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text[8..].Split('.', 2);
            return parts.Length == 2 && int.TryParse(parts[0], out var map) && int.TryParse(parts[1], out var room)
                ? $"{map}/{room}"
                : string.Empty;
        }
        if (text.StartsWith("map", StringComparison.OrdinalIgnoreCase)) text = text[3..];
        text = text.Replace(':', '/');
        var pair = text.Split('/', 2);
        return pair.Length == 2 && int.TryParse(pair[0], out var mapNumber) && int.TryParse(pair[1], out var roomNumber)
            ? $"{mapNumber}/{roomNumber}"
            : string.Empty;
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static bool ReadBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True;

    private sealed record RouteEdge(
        string To,
        string Command,
        string GateKind,
        string Qualifier,
        bool HasRestrictions);

    private sealed record SearchLink(
        string From,
        RouteEdge Edge,
        IReadOnlyList<ClientRouteHazard> Hazards);

    private sealed record ClientRouteSearchResult(
        IReadOnlyList<SearchLink> Links,
        int RiskScore);

    private sealed record ClientRouteRiskResult(
        bool Forbidden,
        int Penalty,
        IReadOnlyList<ClientRouteHazard> Hazards);
}
