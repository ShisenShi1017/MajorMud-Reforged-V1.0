using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcExplicitRouteRequest(
    string NpcInstanceId,
    string RouteId,
    string RouteKind,
    IReadOnlyList<string> RoomIds,
    bool Repeat,
    string Authority,
    long WorldTick);

public sealed record RuntimeNpcExplicitRouteStepCompletion(
    string NpcInstanceId,
    string RouteId,
    string FromRoomId,
    string DestinationRoomId,
    bool TraversalCompleted,
    string Authority,
    long WorldTick);

public sealed record RuntimeNpcExplicitRouteResult(
    bool Accepted,
    string NpcInstanceId,
    string? RouteId,
    int StepIndex,
    string? NextRoomId,
    bool Completed,
    IReadOnlyList<string> Rejections,
    string Reason);

/// <summary>
/// Exact multi-step route ownership boundary. The caller owns patrol/wander/chase/escort/follow route
/// selection, exit legality, pathfinding, restrictions, costs, and cadence. This service only persists an
/// explicitly supplied canonical room sequence and commits one exact traversal completion at a time.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    private static readonly HashSet<string> SupportedExplicitRouteKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "patrol", "wander", "chase", "escort", "follow", "return-home", "scripted"
    };

    public RuntimeNpcExplicitRouteResult AdmitNpcExplicitRoute(RuntimeNpcExplicitRouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var rejections = new List<string>();
            if (!_npcs.TryGetValue(request.NpcInstanceId ?? string.Empty, out var npc)) rejections.Add("instance-not-found");
            else
            {
                if (!npc.Alive) rejections.Add("npc-is-not-alive");
                if (string.IsNullOrWhiteSpace(request.RouteId)) rejections.Add("route-id-required");
                if (!SupportedExplicitRouteKinds.Contains(request.RouteKind ?? string.Empty)) rejections.Add("route-kind-not-supported");
                if (string.IsNullOrWhiteSpace(request.Authority)) rejections.Add("authority-required");
                if (request.WorldTick < 0) rejections.Add("world-tick-invalid");
                if (request.RoomIds is null || request.RoomIds.Count == 0) rejections.Add("route-rooms-required");
                else
                {
                    if (!request.RoomIds[0].Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("route-origin-mismatch");
                    if (request.RoomIds.Count < 2) rejections.Add("route-requires-destination");
                    foreach (var roomId in request.RoomIds)
                    {
                        if (string.IsNullOrWhiteSpace(roomId)) rejections.Add("route-room-id-required");
                        else if (_world.FindRoomById(roomId) is null) rejections.Add($"route-room-not-generated:{roomId}");
                    }
                }
                if (npc.Metadata.TryGetValue("npc_explicit_route_active", out var active) && active.Equals("true", StringComparison.OrdinalIgnoreCase))
                    rejections.Add("route-already-active");
            }

            if (npc is null || rejections.Count != 0)
                return new(false, request.NpcInstanceId ?? string.Empty, null, 0, null, false, rejections, "explicit-route-rejected");

            var rooms = request.RoomIds!.ToArray();
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_explicit_route_active"] = "true",
                ["npc_explicit_route_id"] = request.RouteId!.Trim(),
                ["npc_explicit_route_kind"] = request.RouteKind!.Trim().ToLowerInvariant(),
                ["npc_explicit_route_rooms_json"] = JsonSerializer.Serialize(rooms),
                ["npc_explicit_route_step_index"] = "0",
                ["npc_explicit_route_repeat"] = request.Repeat ? "true" : "false",
                ["npc_explicit_route_authority"] = request.Authority!.Trim(),
                ["npc_explicit_route_admitted_tick"] = request.WorldTick.ToString()
            };
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, request.RouteId!.Trim(), 0, rooms[1], false, Array.Empty<string>(), "explicit-route-admitted");
        }
    }

    public RuntimeNpcExplicitRouteResult CompleteNpcExplicitRouteStep(RuntimeNpcExplicitRouteStepCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        lock (_gate)
        {
            var rejections = new List<string>();
            if (!_npcs.TryGetValue(completion.NpcInstanceId ?? string.Empty, out var npc)) rejections.Add("instance-not-found");
            string[] rooms = [];
            var stepIndex = 0;
            var repeat = false;
            if (npc is not null)
            {
                if (!npc.Alive) rejections.Add("npc-is-not-alive");
                if (string.IsNullOrWhiteSpace(completion.Authority)) rejections.Add("authority-required");
                if (completion.WorldTick < 0) rejections.Add("world-tick-invalid");
                if (!npc.RoomId.Equals(completion.FromRoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("source-room-mismatch");
                if (!npc.Metadata.TryGetValue("npc_explicit_route_active", out var active) || !active.Equals("true", StringComparison.OrdinalIgnoreCase)) rejections.Add("route-not-active");
                if (!npc.Metadata.TryGetValue("npc_explicit_route_id", out var routeId) || !routeId.Equals(completion.RouteId ?? string.Empty, StringComparison.Ordinal)) rejections.Add("route-id-mismatch");
                if (!npc.Metadata.TryGetValue("npc_explicit_route_rooms_json", out var routeJson) || !TryReadRouteRooms(routeJson, out rooms)) rejections.Add("route-state-invalid");
                if (!npc.Metadata.TryGetValue("npc_explicit_route_step_index", out var stepRaw) || !int.TryParse(stepRaw, out stepIndex) || stepIndex < 0) rejections.Add("route-step-invalid");
                if (rooms.Length > 0 && (stepIndex + 1 >= rooms.Length || !rooms[stepIndex].Equals(completion.FromRoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase) || !rooms[stepIndex + 1].Equals(completion.DestinationRoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase))) rejections.Add("route-step-mismatch");
                if (!completion.TraversalCompleted) rejections.Add("traversal-not-explicitly-completed");
                if (_world.FindRoomById(completion.DestinationRoomId!) is null) rejections.Add("destination-room-not-generated");
                repeat = npc.Metadata.TryGetValue("npc_explicit_route_repeat", out var repeatRaw) && repeatRaw.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (npc is null || rejections.Count != 0)
                return new(false, completion.NpcInstanceId ?? string.Empty, completion.RouteId, stepIndex, null, false, rejections, "explicit-route-step-rejected");

            var nextIndex = stepIndex + 1;
            var completedRoute = nextIndex >= rooms.Length - 1;
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_explicit_route_last_origin_room_id"] = completion.FromRoomId!,
                ["npc_explicit_route_last_destination_room_id"] = completion.DestinationRoomId!,
                ["npc_explicit_route_last_authority"] = completion.Authority!.Trim(),
                ["npc_explicit_route_last_completed_tick"] = completion.WorldTick.ToString()
            };
            string? nextRoom = null;
            if (completedRoute && repeat)
            {
                var looped = rooms.Skip(1).Concat([rooms[0]]).ToArray();
                metadata["npc_explicit_route_rooms_json"] = JsonSerializer.Serialize(looped);
                metadata["npc_explicit_route_step_index"] = "0";
                nextRoom = looped[1];
                completedRoute = false;
            }
            else if (completedRoute)
            {
                ClearExplicitRouteMetadata(metadata);
            }
            else
            {
                metadata["npc_explicit_route_step_index"] = nextIndex.ToString();
                nextRoom = rooms[nextIndex + 1];
            }

            _npcs[npc.InstanceId] = npc with { RoomId = completion.DestinationRoomId!, Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, completion.RouteId, nextIndex, nextRoom, completedRoute, Array.Empty<string>(), completedRoute ? "explicit-route-completed" : "explicit-route-step-completed");
        }
    }

    public RuntimeNpcExplicitRouteResult CancelNpcExplicitRoute(string npcInstanceId, string routeId, string authority, long worldTick)
    {
        lock (_gate)
        {
            var rejections = new List<string>();
            if (!_npcs.TryGetValue(npcInstanceId ?? string.Empty, out var npc)) rejections.Add("instance-not-found");
            else
            {
                if (string.IsNullOrWhiteSpace(authority)) rejections.Add("authority-required");
                if (worldTick < 0) rejections.Add("world-tick-invalid");
                if (!npc.Metadata.TryGetValue("npc_explicit_route_active", out var active) || !active.Equals("true", StringComparison.OrdinalIgnoreCase)) rejections.Add("route-not-active");
                if (!npc.Metadata.TryGetValue("npc_explicit_route_id", out var storedRouteId) || !storedRouteId.Equals(routeId ?? string.Empty, StringComparison.Ordinal)) rejections.Add("route-id-mismatch");
            }
            if (npc is null || rejections.Count != 0)
                return new(false, npcInstanceId ?? string.Empty, routeId, 0, null, false, rejections, "explicit-route-cancel-rejected");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_explicit_route_last_cancelled_id"] = routeId ?? string.Empty,
                ["npc_explicit_route_last_cancelled_authority"] = authority!.Trim(),
                ["npc_explicit_route_last_cancelled_tick"] = worldTick.ToString()
            };
            ClearExplicitRouteMetadata(metadata);
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, routeId, 0, null, true, Array.Empty<string>(), "explicit-route-cancelled");
        }
    }

    private static bool TryReadRouteRooms(string json, out string[] rooms)
    {
        try { rooms = JsonSerializer.Deserialize<string[]>(json) ?? []; return rooms.Length >= 2 && rooms.All(x => !string.IsNullOrWhiteSpace(x)); }
        catch (JsonException) { rooms = []; return false; }
    }

    private static void ClearExplicitRouteMetadata(IDictionary<string,string> metadata)
    {
        foreach (var key in new[] { "npc_explicit_route_active", "npc_explicit_route_id", "npc_explicit_route_kind", "npc_explicit_route_rooms_json", "npc_explicit_route_step_index", "npc_explicit_route_repeat", "npc_explicit_route_authority", "npc_explicit_route_admitted_tick" }) metadata.Remove(key);
    }
}

public sealed class RuntimeNpcExplicitRouteSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcExplicitRouteSmokeReport Run()
    {
        var report = new RuntimeNpcExplicitRouteSmokeReport();
        try
        {
            var type = typeof(RuntimeLivingWorldService);
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcExplicitRoute)) is not null, "explicit-route-admission-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcExplicitRouteStep)) is not null, "explicit-route-step-completion-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.CancelNpcExplicitRoute)) is not null, "explicit-route-cancel-exposed");
            report.Checks.AddRange(["explicit-route-admission-exposed", "explicit-route-step-completion-exposed", "explicit-route-cancel-exposed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
