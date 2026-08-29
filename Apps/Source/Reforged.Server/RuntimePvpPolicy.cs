using System.Text.Json;

namespace Reforged.Server;

public enum RuntimePvpZonePolicy { Forbidden, ConsentOnly, Open }
public enum RuntimePvpDecisionCode { Allowed, UnknownActor, SelfTarget, ZoneForbidden, ConsentRequired, LevelRangeDenied, PartyDenied, GuildDenied, CooldownActive, LogoutRestricted }

public sealed record RuntimePvpActor(
    string CharacterId,
    int Level,
    string RoomId,
    string? PartyId = null,
    string? GuildId = null,
    bool PvpEnabled = false,
    bool IsCriminal = false,
    bool Connected = true);

public sealed record RuntimePvpZoneRule(
    string RoomId,
    RuntimePvpZonePolicy Policy,
    int MaximumLevelDifference = 0,
    bool DenySameParty = true,
    bool DenySameGuild = false,
    bool AllowCriminalOverride = false,
    TimeSpan? AttackCooldown = null,
    TimeSpan? LogoutRestriction = null);

public sealed record RuntimePvpDecision(bool Allowed, RuntimePvpDecisionCode Code, string Reason);
public sealed record RuntimePvpKillRequest(string KillerId, string VictimId, string RoomId, DateTimeOffset OccurredAtUtc, bool RequestKillCredit, bool RequestLawEvaluation, bool RequestLootPolicy);

/// <summary>
/// Reforged compatibility / extension PvP-policy shell. ConsentOnly/Open/Forbidden zones,
/// same-party/guild denials, per-target attack cooldowns, and logout restrictions are NOT
/// evidence of native Classic MajorMUD PvP behavior. Classic parity is bounded by
/// RuntimeClassicPvpPolicyAuthority plus narrower recovered alignment/arena/config owners.
/// </summary>
public sealed class RuntimePvpPolicyService
{
    private readonly Dictionary<string, RuntimePvpZoneRule> _zones;
    private readonly Dictionary<string, RuntimePvpActor> _actors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Attacker, string Target), DateTimeOffset> _lastAttacks = new();
    private readonly Dictionary<string, DateTimeOffset> _logoutRestrictedUntil = new(StringComparer.OrdinalIgnoreCase);

    public RuntimePvpPolicyService(IEnumerable<RuntimePvpZoneRule> zones)
    {
        _zones = zones.ToDictionary(z => z.RoomId, StringComparer.OrdinalIgnoreCase);
        if (_zones.Keys.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("zone-room-id-required", nameof(zones));
    }

    public void UpsertActor(RuntimePvpActor actor)
    {
        if (string.IsNullOrWhiteSpace(actor.CharacterId) || string.IsNullOrWhiteSpace(actor.RoomId) || actor.Level < 1)
            throw new ArgumentException("invalid-pvp-actor", nameof(actor));
        _actors[actor.CharacterId] = actor;
    }

    public RuntimePvpDecision EvaluateAttack(string attackerId, string targetId, DateTimeOffset nowUtc)
    {
        if (!_actors.TryGetValue(attackerId, out var attacker) || !_actors.TryGetValue(targetId, out var target))
            return new(false, RuntimePvpDecisionCode.UnknownActor, "unknown-actor");
        if (string.Equals(attackerId, targetId, StringComparison.OrdinalIgnoreCase))
            return new(false, RuntimePvpDecisionCode.SelfTarget, "self-target-denied");
        if (!string.Equals(attacker.RoomId, target.RoomId, StringComparison.OrdinalIgnoreCase))
            return new(false, RuntimePvpDecisionCode.ZoneForbidden, "different-room");
        if (!_zones.TryGetValue(attacker.RoomId, out var zone))
            return new(false, RuntimePvpDecisionCode.ZoneForbidden, "unknown-zone-fails-closed");
        if (zone.Policy == RuntimePvpZonePolicy.Forbidden)
            return new(false, RuntimePvpDecisionCode.ZoneForbidden, "pvp-forbidden");
        if (zone.Policy == RuntimePvpZonePolicy.ConsentOnly && !(attacker.PvpEnabled && target.PvpEnabled) && !(zone.AllowCriminalOverride && target.IsCriminal))
            return new(false, RuntimePvpDecisionCode.ConsentRequired, "mutual-consent-required");
        if (zone.MaximumLevelDifference > 0 && Math.Abs(attacker.Level - target.Level) > zone.MaximumLevelDifference)
            return new(false, RuntimePvpDecisionCode.LevelRangeDenied, "level-range-denied");
        if (zone.DenySameParty && attacker.PartyId is not null && string.Equals(attacker.PartyId, target.PartyId, StringComparison.OrdinalIgnoreCase))
            return new(false, RuntimePvpDecisionCode.PartyDenied, "same-party-denied");
        if (zone.DenySameGuild && attacker.GuildId is not null && string.Equals(attacker.GuildId, target.GuildId, StringComparison.OrdinalIgnoreCase))
            return new(false, RuntimePvpDecisionCode.GuildDenied, "same-guild-denied");
        if (zone.AttackCooldown is { } cooldown && _lastAttacks.TryGetValue((attackerId, targetId), out var last) && nowUtc < last + cooldown)
            return new(false, RuntimePvpDecisionCode.CooldownActive, "attack-cooldown-active");
        return new(true, RuntimePvpDecisionCode.Allowed, "allowed");
    }

    public RuntimePvpDecision RegisterAttack(string attackerId, string targetId, DateTimeOffset nowUtc)
    {
        var decision = EvaluateAttack(attackerId, targetId, nowUtc);
        if (!decision.Allowed) return decision;
        _lastAttacks[(attackerId, targetId)] = nowUtc;
        var roomId = _actors[attackerId].RoomId;
        if (_zones[roomId].LogoutRestriction is { } restriction)
        {
            _logoutRestrictedUntil[attackerId] = nowUtc + restriction;
            _logoutRestrictedUntil[targetId] = nowUtc + restriction;
        }
        return decision;
    }

    public RuntimePvpDecision EvaluateLogout(string characterId, DateTimeOffset nowUtc)
    {
        if (!_actors.ContainsKey(characterId)) return new(false, RuntimePvpDecisionCode.UnknownActor, "unknown-actor");
        if (_logoutRestrictedUntil.TryGetValue(characterId, out var until) && nowUtc < until)
            return new(false, RuntimePvpDecisionCode.LogoutRestricted, "pvp-logout-restricted");
        return new(true, RuntimePvpDecisionCode.Allowed, "allowed");
    }

    public RuntimePvpKillRequest CreateKillRequest(string killerId, string victimId, DateTimeOffset nowUtc)
    {
        var decision = EvaluateAttack(killerId, victimId, nowUtc);
        if (!decision.Allowed && decision.Code != RuntimePvpDecisionCode.CooldownActive)
            throw new InvalidOperationException(decision.Reason);
        return new(killerId, victimId, _actors[killerId].RoomId, nowUtc, true, true, true);
    }

    public IReadOnlyList<string> ValidateState()
    {
        var errors = new List<string>();
        foreach (var actor in _actors.Values)
        {
            if (actor.Level < 1) errors.Add($"invalid-level:{actor.CharacterId}");
            if (string.IsNullOrWhiteSpace(actor.RoomId)) errors.Add($"missing-room:{actor.CharacterId}");
        }
        return errors;
    }

    public string ToJson() => JsonSerializer.Serialize(new { actors = _actors.Values, lastAttacks = _lastAttacks.Count, logoutRestrictions = _logoutRestrictedUntil.Count }, new JsonSerializerOptions { WriteIndented = true });
}
