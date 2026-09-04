using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeRestActionKind { BeginRest, BeginSleep, Wake, RequestRecoveryModifier, RequestStatusCleanup, RequestCharge, RequestSpawnRoomChange, RequestLogout }
public enum RuntimeRestStateKind { Active, Resting, Sleeping }
public sealed record RuntimeRestRoomPolicy(string RoomId, bool AllowRest, bool AllowSleep, bool IsInn, bool IsSafeLogout, long InnCost = 0, string? LoginRoomId = null, double RecoveryMultiplier = 1.0);
public sealed record RuntimeRestActorContext(string CharacterId, string RoomId, bool InCombat, bool IsDead, bool IsConnected, RuntimeRestStateKind State);
public sealed record RuntimeRestRequest(RuntimeRestActionKind Kind, string CharacterId, string? RelatedId, long Amount, double Modifier, string Reason);
public sealed record RuntimeRestResult(bool Accepted, string Code, RuntimeRestStateKind State, IReadOnlyList<RuntimeRestRequest> Requests);

public sealed class RuntimeRestRecoveryPolicyService
{
    private readonly Dictionary<string, RuntimeRestRoomPolicy> _rooms;
    private readonly Dictionary<string, RuntimeRestStateKind> _states = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeRestRecoveryPolicyService(IEnumerable<RuntimeRestRoomPolicy> rooms)
    {
        _rooms = rooms.ToDictionary(x => x.RoomId, StringComparer.OrdinalIgnoreCase);
        if (_rooms.Keys.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("rest-room-id-required", nameof(rooms));
        if (_rooms.Values.Any(x => x.InnCost < 0 || x.RecoveryMultiplier <= 0)) throw new ArgumentException("invalid-rest-room-policy", nameof(rooms));
    }

    public RuntimeRestResult Rest(RuntimeRestActorContext actor)
    {
        if (!ValidateActor(actor, out var room, out var failure)) return failure!;
        if (actor.InCombat) return Reject(actor, "combat-blocks-rest");
        if (!room!.AllowRest) return Reject(actor, "rest-not-allowed");
        _states[actor.CharacterId] = RuntimeRestStateKind.Resting;
        return new(true, "resting", RuntimeRestStateKind.Resting, new[]
        {
            new RuntimeRestRequest(RuntimeRestActionKind.BeginRest, actor.CharacterId, actor.RoomId, 0, 1, "rest-policy-approved"),
            new RuntimeRestRequest(RuntimeRestActionKind.RequestRecoveryModifier, actor.CharacterId, actor.RoomId, 0, room.RecoveryMultiplier, "room-recovery-policy")
        });
    }

    public RuntimeRestResult Sleep(RuntimeRestActorContext actor)
    {
        if (!ValidateActor(actor, out var room, out var failure)) return failure!;
        if (actor.InCombat) return Reject(actor, "combat-blocks-sleep");
        if (!room!.AllowSleep) return Reject(actor, "sleep-not-allowed");
        _states[actor.CharacterId] = RuntimeRestStateKind.Sleeping;
        var requests = new List<RuntimeRestRequest>
        {
            new(RuntimeRestActionKind.BeginSleep, actor.CharacterId, actor.RoomId, 0, 1, "sleep-policy-approved"),
            new(RuntimeRestActionKind.RequestRecoveryModifier, actor.CharacterId, actor.RoomId, 0, room.RecoveryMultiplier, "room-recovery-policy")
        };
        if (room.IsInn && room.InnCost > 0) requests.Add(new(RuntimeRestActionKind.RequestCharge, actor.CharacterId, actor.RoomId, room.InnCost, 1, "inn-rental-charge"));
        if (room.IsInn && !string.IsNullOrWhiteSpace(room.LoginRoomId)) requests.Add(new(RuntimeRestActionKind.RequestSpawnRoomChange, actor.CharacterId, room.LoginRoomId, 0, 1, "inn-login-room-policy"));
        return new(true, "sleeping", RuntimeRestStateKind.Sleeping, requests);
    }

    public RuntimeRestResult Wake(RuntimeRestActorContext actor)
    {
        if (string.IsNullOrWhiteSpace(actor.CharacterId)) return Reject(actor, "invalid-character");
        var current = InspectState(actor.CharacterId);
        if (current == RuntimeRestStateKind.Active) return Reject(actor, "not-resting-or-sleeping");
        _states[actor.CharacterId] = RuntimeRestStateKind.Active;
        return new(true, "awake", RuntimeRestStateKind.Active, new[] { new RuntimeRestRequest(RuntimeRestActionKind.Wake, actor.CharacterId, actor.RoomId, 0, 1, "wake-policy-approved") });
    }

    public RuntimeRestResult RequestLogout(RuntimeRestActorContext actor)
    {
        if (!ValidateActor(actor, out var room, out var failure)) return failure!;
        if (actor.InCombat) return Reject(actor, "combat-blocks-logout");
        if (!room!.IsSafeLogout) return Reject(actor, "unsafe-logout-room");
        return new(true, "logout-requested", InspectState(actor.CharacterId), new[] { new RuntimeRestRequest(RuntimeRestActionKind.RequestLogout, actor.CharacterId, actor.RoomId, 0, 1, "safe-logout-policy") });
    }

    public RuntimeRestStateKind InspectState(string characterId) => _states.TryGetValue(characterId, out var state) ? state : RuntimeRestStateKind.Active;
    public IReadOnlyList<string> ValidateState() => _states.Where(x => string.IsNullOrWhiteSpace(x.Key)).Select(_ => "blank-character-state").ToArray();
    public string ToJson() => JsonSerializer.Serialize(new { states = _states }, new JsonSerializerOptions { WriteIndented = true });

    private bool ValidateActor(RuntimeRestActorContext actor, out RuntimeRestRoomPolicy? room, out RuntimeRestResult? failure)
    {
        room = null; failure = null;
        if (string.IsNullOrWhiteSpace(actor.CharacterId) || string.IsNullOrWhiteSpace(actor.RoomId)) { failure = Reject(actor, "invalid-actor-context"); return false; }
        if (!actor.IsConnected) { failure = Reject(actor, "character-not-connected"); return false; }
        if (actor.IsDead) { failure = Reject(actor, "dead-character-cannot-rest"); return false; }
        if (!_rooms.TryGetValue(actor.RoomId, out room)) { failure = Reject(actor, "unknown-room-policy"); return false; }
        return true;
    }
    private RuntimeRestResult Reject(RuntimeRestActorContext actor, string code) => new(false, code, InspectState(actor.CharacterId ?? string.Empty), Array.Empty<RuntimeRestRequest>());
}
