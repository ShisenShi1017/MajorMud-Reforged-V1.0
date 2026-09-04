using System.Collections.Concurrent;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimePartyInviteSnapshot(string InviteId, string PartyId, string InviterId, string TargetId, DateTimeOffset ExpiresAtUtc);
public sealed record RuntimeSharedNpcSnapshot(string NpcId, string RoomId, int CurrentHealth, int MaximumHealth, bool IsDead, string? TargetActorId, IReadOnlyDictionary<string,long> Threat);
public sealed record RuntimeSharedCorpseSnapshot(string CorpseId, string NpcId, string RoomId, IReadOnlyDictionary<string,int> Items, DateTimeOffset ExpiresAtUtc, bool IsClosed);
public sealed record RuntimeDisconnectSnapshot(string ActorId, string? PartyId, string? RoomId, DateTimeOffset DisconnectedAtUtc);
public sealed record RuntimeMultiplayerCompletionSave(
    IReadOnlyList<RuntimePartySnapshot> Parties,
    IReadOnlyList<RuntimePartyInviteSnapshot> Invites,
    IReadOnlyList<RuntimeSharedNpcSnapshot> Npcs,
    IReadOnlyList<RuntimeSharedCorpseSnapshot> Corpses,
    IReadOnlyList<RuntimeDisconnectSnapshot> Disconnects,
    IReadOnlyDictionary<string,long> Experience);

public sealed record RuntimeMultiplayerLootClaimResult(bool Succeeded, string? FailureCode, int QuantityRemaining);
public sealed record RuntimeDamageResult(int DamageApplied, int CurrentHealth, bool Died, string? CorpseId, string? TargetActorId);

/// <summary>
/// Generic Reforged multiplayer completion/smoke model. This type is NOT the Classic
/// MajorMUD party authority and its invite expiry, leader succession, XP split, corpse,
/// disconnect, and persistence choices must not be used as native parity evidence.
/// Classic live party behavior is owned by RuntimePartyCoordinationService plus the
/// evidence-gated RuntimeLiveGameplayFacade consumers.
/// </summary>
public sealed class RuntimeMultiplayerCompletionState
{
    private readonly object _partyGate = new();
    private readonly ConcurrentDictionary<string, object> _entityGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeParty> _parties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _membership = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimePartyInviteSnapshot> _invites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeSharedNpcSnapshot> _npcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeSharedCorpseSnapshot> _corpses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeDisconnectSnapshot> _disconnects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _experience = new(StringComparer.OrdinalIgnoreCase);

    private object Gate(string id) => _entityGates.GetOrAdd(id, _ => new object());

    public RuntimePartySnapshot CreateParty(string partyId, string leaderId)
    {
        lock (_partyGate)
        {
            Require(partyId); Require(leaderId);
            if (_parties.ContainsKey(partyId)) throw new InvalidOperationException("duplicate-party-id");
            if (_membership.ContainsKey(leaderId)) throw new InvalidOperationException("actor-already-in-party");
            var party = new RuntimeParty(partyId, leaderId);
            _parties.Add(partyId, party);
            _membership.Add(leaderId, partyId);
            return party.Capture();
        }
    }

    public RuntimePartyInviteSnapshot Invite(string partyId, string inviterId, string targetId, DateTimeOffset expiresAtUtc)
    {
        lock (_partyGate)
        {
            if (!_parties.TryGetValue(partyId, out var party)) throw new InvalidOperationException("party-not-found");
            if (!party.LeaderId.Equals(inviterId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("party-invite-leader-required");
            if (_membership.ContainsKey(targetId)) throw new InvalidOperationException("actor-already-in-party");
            var invite = new RuntimePartyInviteSnapshot($"invite.{Guid.NewGuid():N}", partyId, inviterId, Require(targetId), expiresAtUtc);
            _invites[invite.InviteId] = invite;
            return invite;
        }
    }

    public RuntimePartySnapshot AcceptInvite(string inviteId, string targetId, DateTimeOffset nowUtc)
    {
        lock (_partyGate)
        {
            if (!_invites.TryGetValue(inviteId, out var invite)) throw new InvalidOperationException("party-invite-not-found");
            if (!invite.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("party-invite-target-mismatch");
            if (invite.ExpiresAtUtc <= nowUtc) { _invites.Remove(inviteId); throw new InvalidOperationException("party-invite-expired"); }
            if (_membership.ContainsKey(targetId)) throw new InvalidOperationException("actor-already-in-party");
            var party = _parties[invite.PartyId];
            party.Add(targetId);
            _membership[targetId] = party.PartyId;
            _invites.Remove(inviteId);
            return party.Capture();
        }
    }

    public bool DeclineInvite(string inviteId, string targetId)
    {
        lock (_partyGate)
        {
            if (!_invites.TryGetValue(inviteId, out var invite) || !invite.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase)) return false;
            return _invites.Remove(inviteId);
        }
    }

    public RuntimePartySnapshot LeaveParty(string actorId)
    {
        lock (_partyGate)
        {
            if (!_membership.TryGetValue(actorId, out var partyId)) throw new InvalidOperationException("actor-not-in-party");
            var party = _parties[partyId];
            if (party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase))
            {
                var replacement = party.Members.FirstOrDefault(member => !member.Equals(actorId, StringComparison.OrdinalIgnoreCase));
                if (replacement is null)
                {
                    _membership.Remove(actorId); _parties.Remove(partyId);
                    return new RuntimePartySnapshot(partyId, actorId, Array.Empty<string>(), new Dictionary<string,string>());
                }
                party.ChangeLeader(replacement);
            }
            party.Remove(actorId);
            _membership.Remove(actorId);
            return party.Capture();
        }
    }

    public bool Disband(string partyId, string leaderId)
    {
        lock (_partyGate)
        {
            if (!_parties.TryGetValue(partyId, out var party)) return false;
            if (!party.LeaderId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("party-disband-leader-required");
            foreach (var member in party.Members) _membership.Remove(member);
            _parties.Remove(partyId);
            foreach (var inviteId in _invites.Values.Where(x => x.PartyId.Equals(partyId, StringComparison.OrdinalIgnoreCase)).Select(x => x.InviteId).ToArray()) _invites.Remove(inviteId);
            return true;
        }
    }

    public RuntimeSharedNpcSnapshot RegisterNpc(string npcId, string roomId, int maximumHealth)
    {
        if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        lock (Gate(npcId))
        {
            var npc = new RuntimeSharedNpcSnapshot(Require(npcId), Require(roomId), maximumHealth, maximumHealth, false, null, new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase));
            if (!_npcs.TryAdd(npcId, npc)) throw new InvalidOperationException("duplicate-shared-npc");
            return npc;
        }
    }

    public RuntimeDamageResult ApplyDamage(string npcId, string actorId, int requestedDamage, IReadOnlyDictionary<string,int>? corpseItems = null, DateTimeOffset? corpseExpiresAtUtc = null)
    {
        if (requestedDamage <= 0) throw new ArgumentOutOfRangeException(nameof(requestedDamage));
        lock (Gate(npcId))
        {
            if (!_npcs.TryGetValue(npcId, out var npc)) throw new InvalidOperationException("shared-npc-not-found");
            if (npc.IsDead) return new RuntimeDamageResult(0, 0, true, null, npc.TargetActorId);
            var damage = Math.Min(requestedDamage, npc.CurrentHealth);
            var health = npc.CurrentHealth - damage;
            var threat = new Dictionary<string,long>(npc.Threat, StringComparer.OrdinalIgnoreCase);
            threat[actorId] = checked(threat.GetValueOrDefault(actorId) + damage);
            var target = threat.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).First().Key;
            string? corpseId = null;
            if (health == 0)
            {
                corpseId = $"corpse.{npcId}.{Guid.NewGuid():N}";
                var corpse = new RuntimeSharedCorpseSnapshot(corpseId, npcId, npc.RoomId,
                    new Dictionary<string,int>(corpseItems ?? new Dictionary<string,int>(), StringComparer.OrdinalIgnoreCase),
                    corpseExpiresAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(10), false);
                _corpses[corpseId] = corpse;
            }
            _npcs[npcId] = npc with { CurrentHealth = health, IsDead = health == 0, TargetActorId = health == 0 ? null : target, Threat = threat };
            return new RuntimeDamageResult(damage, health, health == 0, corpseId, health == 0 ? null : target);
        }
    }

    public RuntimeMultiplayerLootClaimResult ClaimLoot(string corpseId, string actorId, string itemId, int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        lock (Gate(corpseId))
        {
            if (!_corpses.TryGetValue(corpseId, out var corpse) || corpse.IsClosed) return new(false, "corpse-unavailable", 0);
            var items = new Dictionary<string,int>(corpse.Items, StringComparer.OrdinalIgnoreCase);
            var available = items.GetValueOrDefault(itemId);
            if (available < quantity) return new(false, "insufficient-corpse-quantity", available);
            var remaining = available - quantity;
            if (remaining == 0) items.Remove(itemId); else items[itemId] = remaining;
            _corpses[corpseId] = corpse with { Items = items, IsClosed = items.Count == 0 };
            return new(true, null, remaining);
        }
    }

    public IReadOnlyDictionary<string,long> AwardPartyExperience(string actorId, long totalExperience)
    {
        if (totalExperience < 0) throw new ArgumentOutOfRangeException(nameof(totalExperience));
        lock (_partyGate)
        {
            var recipients = _membership.TryGetValue(actorId, out var partyId)
                ? _parties[partyId].Members.Order(StringComparer.OrdinalIgnoreCase).ToArray()
                : new[] { actorId };
            if (recipients.Length == 0) return new Dictionary<string,long>();
            var baseShare = totalExperience / recipients.Length;
            var remainder = totalExperience % recipients.Length;
            var awards = new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < recipients.Length; i++)
            {
                var award = baseShare + (i < remainder ? 1 : 0);
                _experience[recipients[i]] = checked(_experience.GetValueOrDefault(recipients[i]) + award);
                awards[recipients[i]] = award;
            }
            return awards;
        }
    }

    public RuntimeDisconnectSnapshot Disconnect(string actorId, string? roomId, DateTimeOffset disconnectedAtUtc)
    {
        lock (_partyGate)
        {
            _membership.TryGetValue(actorId, out var partyId);
            var snapshot = new RuntimeDisconnectSnapshot(actorId, partyId, roomId, disconnectedAtUtc);
            _disconnects[actorId] = snapshot;
            return snapshot;
        }
    }

    public RuntimeDisconnectSnapshot? Reconnect(string actorId)
    {
        lock (_partyGate)
        {
            if (!_disconnects.Remove(actorId, out var snapshot)) return null;
            return snapshot;
        }
    }

    public int ExpireCorpses(DateTimeOffset nowUtc)
    {
        var expired = 0;
        foreach (var corpseId in _corpses.Keys.ToArray())
        {
            lock (Gate(corpseId))
            {
                if (_corpses.TryGetValue(corpseId, out var corpse) && corpse.ExpiresAtUtc <= nowUtc && _corpses.Remove(corpseId)) expired++;
            }
        }
        return expired;
    }

    public RuntimeMultiplayerCompletionSave Capture()
    {
        lock (_partyGate)
        {
            return new RuntimeMultiplayerCompletionSave(
                _parties.Values.Select(x => x.Capture()).OrderBy(x => x.PartyId, StringComparer.OrdinalIgnoreCase).ToArray(),
                _invites.Values.OrderBy(x => x.InviteId, StringComparer.OrdinalIgnoreCase).ToArray(),
                _npcs.Values.OrderBy(x => x.NpcId, StringComparer.OrdinalIgnoreCase).ToArray(),
                _corpses.Values.OrderBy(x => x.CorpseId, StringComparer.OrdinalIgnoreCase).ToArray(),
                _disconnects.Values.OrderBy(x => x.ActorId, StringComparer.OrdinalIgnoreCase).ToArray(),
                new Dictionary<string,long>(_experience, StringComparer.OrdinalIgnoreCase));
        }
    }

    public static RuntimeMultiplayerCompletionState Restore(RuntimeMultiplayerCompletionSave save)
    {
        var state = new RuntimeMultiplayerCompletionState();
        lock (state._partyGate)
        {
            foreach (var snapshot in save.Parties)
            {
                var party = RuntimeParty.Restore(snapshot);
                state._parties.Add(party.PartyId, party);
                foreach (var member in party.Members) state._membership.Add(member, party.PartyId);
            }
            foreach (var invite in save.Invites) state._invites.Add(invite.InviteId, invite);
            foreach (var npc in save.Npcs) state._npcs.Add(npc.NpcId, npc);
            foreach (var corpse in save.Corpses) state._corpses.Add(corpse.CorpseId, corpse);
            foreach (var disconnect in save.Disconnects) state._disconnects.Add(disconnect.ActorId, disconnect);
            foreach (var pair in save.Experience) state._experience.Add(pair.Key, pair.Value);
        }
        return state;
    }

    public IReadOnlyList<string> Validate()
    {
        lock (_partyGate)
        {
            var failures = new List<string>();
            foreach (var pair in _membership)
                if (!_parties.TryGetValue(pair.Value, out var party) || !party.Members.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)) failures.Add("membership-index-mismatch:" + pair.Key);
            foreach (var party in _parties.Values)
                if (!party.Members.Contains(party.LeaderId, StringComparer.OrdinalIgnoreCase)) failures.Add("party-leader-not-member:" + party.PartyId);
            foreach (var npc in _npcs.Values)
                if (npc.CurrentHealth < 0 || npc.CurrentHealth > npc.MaximumHealth || npc.IsDead != (npc.CurrentHealth == 0)) failures.Add("invalid-shared-npc-state:" + npc.NpcId);
            foreach (var corpse in _corpses.Values)
                if (corpse.Items.Any(x => x.Value <= 0)) failures.Add("invalid-corpse-quantity:" + corpse.CorpseId);
            return failures;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(Capture(), new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeMultiplayerCompletionState FromJson(string json) => Restore(JsonSerializer.Deserialize<RuntimeMultiplayerCompletionSave>(json) ?? throw new InvalidOperationException("multiplayer-completion-save-invalid"));
    private static string Require(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("value-required") : value.Trim();
}
