using System.Text.Json;

namespace Reforged.Server;

public enum RuntimePartyDisconnectPolicy
{
    RetainMembership,
    RemoveMember,
    TransferLeaderThenRetain
}

public sealed record RuntimePartyPolicy(
    int MaximumMembers,
    TimeSpan? InviteLifetime,
    RuntimePartyDisconnectPolicy DisconnectPolicy,
    bool LeaderMayKick,
    bool LeaderMayTransfer,
    bool PersistAcrossRestart)
{
    /// <summary>
    /// Compatibility shell defaults only. Fields whose native values are unresolved are
    /// deliberately not treated as MajorMUD parity evidence; live Classic code must gate
    /// any behavior that depends on those unknowns.
    /// </summary>
    public static RuntimePartyPolicy CompatibilityShellDefault { get; } = new(
        MaximumMembers: int.MaxValue,
        InviteLifetime: null,
        DisconnectPolicy: RuntimePartyDisconnectPolicy.RetainMembership,
        LeaderMayKick: true,
        LeaderMayTransfer: false,
        PersistAcrossRestart: false);

    /// <summary>
    /// Classic MajorMUD strong-evidence policy. Native-era operator/reverse-engineering
    /// records consistently identify the original party cap as six total characters.
    /// Invite lifetime, leader succession, and disconnect persistence remain UNKNOWN.
    /// </summary>
    public static RuntimePartyPolicy ClassicStrongEvidence { get; } = new(
        MaximumMembers: 6,
        InviteLifetime: null,
        DisconnectPolicy: RuntimePartyDisconnectPolicy.RetainMembership,
        LeaderMayKick: true,
        LeaderMayTransfer: false,
        PersistAcrossRestart: false);
}

public sealed record RuntimePartyMemberState(string ActorId, bool Connected, string? RoomId, bool FollowingLeader, string Rank = "Midrank");
public sealed record RuntimePartyCoordinationSnapshot(
    string PartyId,
    string LeaderId,
    IReadOnlyList<RuntimePartyMemberState> Members,
    IReadOnlyList<RuntimePartyInviteSnapshot> Invitations,
    RuntimePartyPolicy Policy);

public sealed record RuntimePartyOperationResult(bool Succeeded, string? FailureCode, RuntimePartyCoordinationSnapshot? Snapshot);
public sealed record RuntimePartyRouteResult(bool Succeeded, string? FailureCode, IReadOnlyList<string> RecipientActorIds);
public sealed record RuntimePartyFollowMove(string ActorId, string FromRoomId, string DestinationRoomId);

public sealed class RuntimePartyCoordinationService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PartyState> _parties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _membership = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimePartyInviteSnapshot> _invites = new(StringComparer.OrdinalIgnoreCase);

    public RuntimePartyCoordinationSnapshot Create(string partyId, string leaderId, RuntimePartyPolicy? policy = null)
    {
        lock (_gate)
        {
            Require(partyId, nameof(partyId));
            Require(leaderId, nameof(leaderId));
            if (_parties.ContainsKey(partyId)) throw new InvalidOperationException("duplicate-party-id");
            if (_membership.ContainsKey(leaderId)) throw new InvalidOperationException("actor-already-in-party");
            var effective = policy ?? RuntimePartyPolicy.CompatibilityShellDefault;
            ValidatePolicy(effective);
            var state = new PartyState(partyId, leaderId, effective);
            _parties.Add(partyId, state);
            _membership.Add(leaderId, partyId);
            return Capture(state);
        }
    }

    public RuntimePartyInviteSnapshot Invite(string partyId, string inviterId, string targetId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var party = GetParty(partyId);
            if (!party.LeaderId.Equals(inviterId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("party-invite-leader-required");
            if (_membership.ContainsKey(targetId)) throw new InvalidOperationException("actor-already-in-party");
            if (party.Members.Count >= party.Policy.MaximumMembers) throw new InvalidOperationException("party-full");
            foreach (var existing in _invites.Values.Where(x => x.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase)).ToArray())
                _invites.Remove(existing.InviteId);
            var expiresAtUtc = party.Policy.InviteLifetime is { } lifetime
                ? nowUtc.Add(lifetime)
                : DateTimeOffset.MaxValue;
            var invitation = new RuntimePartyInviteSnapshot(
                $"partyinvite.{Guid.NewGuid():N}", partyId, inviterId, Require(targetId, nameof(targetId)), expiresAtUtc);
            _invites.Add(invitation.InviteId, invitation);
            return invitation;
        }
    }

    public RuntimePartyOperationResult Accept(string inviteId, string targetId, DateTimeOffset nowUtc, bool followingLeader = true)
    {
        lock (_gate)
        {
            if (!_invites.TryGetValue(inviteId, out var invitation)) return Fail("party-invite-not-found");
            if (!invitation.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase)) return Fail("party-invite-target-mismatch");
            if (invitation.ExpiresAtUtc != DateTimeOffset.MaxValue && invitation.ExpiresAtUtc <= nowUtc)
            {
                _invites.Remove(inviteId);
                return Fail("party-invite-expired");
            }
            if (_membership.ContainsKey(targetId)) return Fail("actor-already-in-party");
            var party = GetParty(invitation.PartyId);
            if (party.Members.Count >= party.Policy.MaximumMembers) return Fail("party-full");
            party.Members.Add(targetId, new RuntimePartyMemberState(targetId, true, null, followingLeader));
            _membership.Add(targetId, party.PartyId);
            _invites.Remove(inviteId);
            return Success(party);
        }
    }

    public bool Decline(string inviteId, string targetId)
    {
        lock (_gate)
        {
            return _invites.TryGetValue(inviteId, out var invite)
                   && invite.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase)
                   && _invites.Remove(inviteId);
        }
    }

    public RuntimePartyOperationResult Kick(string leaderId, string targetId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(leaderId, out var partyId)) return Fail("actor-not-in-party");
            var party = GetParty(partyId);
            if (!party.Policy.LeaderMayKick) return Fail("party-kick-disabled");
            if (!party.LeaderId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)) return Fail("party-kick-leader-required");
            if (leaderId.Equals(targetId, StringComparison.OrdinalIgnoreCase)) return Fail("party-leader-cannot-kick-self");
            if (!party.Members.Remove(targetId)) return Fail("party-member-not-found");
            _membership.Remove(targetId);
            return Success(party);
        }
    }

    public RuntimePartyOperationResult TransferLeadership(string leaderId, string targetId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(leaderId, out var partyId)) return Fail("actor-not-in-party");
            var party = GetParty(partyId);
            if (!party.Policy.LeaderMayTransfer) return Fail("party-leadership-transfer-disabled");
            if (!party.LeaderId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)) return Fail("party-transfer-leader-required");
            if (!party.Members.ContainsKey(targetId)) return Fail("party-member-not-found");
            party.LeaderId = targetId;
            return Success(party);
        }
    }

    public RuntimePartyOperationResult Leave(string actorId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(actorId, out var partyId)) return Fail("actor-not-in-party");
            var party = GetParty(partyId);
            if (party.Members.Count == 1)
            {
                RemoveParty(party);
                return new(true, null, null);
            }
            if (party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase))
            {
                if (!party.Policy.LeaderMayTransfer) return Fail("party-leader-leave-native-unknown");
                party.LeaderId = SelectReplacementLeader(party, actorId);
            }
            party.Members.Remove(actorId);
            _membership.Remove(actorId);
            return Success(party);
        }
    }

    public RuntimePartyOperationResult SetConnected(string actorId, bool connected, string? roomId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(actorId, out var partyId)) return Fail("actor-not-in-party");
            var party = GetParty(partyId);
            var member = party.Members[actorId];
            party.Members[actorId] = member with { Connected = connected, RoomId = roomId };
            if (!connected && party.Policy.DisconnectPolicy == RuntimePartyDisconnectPolicy.RemoveMember)
                return Leave(actorId);
            if (!connected && party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                && party.Policy.DisconnectPolicy == RuntimePartyDisconnectPolicy.TransferLeaderThenRetain)
                party.LeaderId = SelectReplacementLeader(party, actorId);
            return Success(party);
        }
    }

    public RuntimePartyOperationResult SetFollowing(string actorId, bool following)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(actorId, out var partyId)) return Fail("actor-not-in-party");
            var party = GetParty(partyId);
            if (party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase) && following) return Fail("party-leader-cannot-follow-self");
            var member = party.Members[actorId];
            party.Members[actorId] = member with { FollowingLeader = following };
            return Success(party);
        }
    }

    public RuntimePartyOperationResult AcceptFromLeader(string targetId, string leaderId, DateTimeOffset nowUtc, bool followingLeader = true)
    {
        lock (_gate)
        {
            var invitation = _invites.Values
                .Where(invite => invite.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase)
                    && invite.InviterId.Equals(leaderId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(invite => invite.ExpiresAtUtc)
                .FirstOrDefault();
            if (invitation is null) return Fail("party-invite-not-found");
            return Accept(invitation.InviteId, targetId, nowUtc, followingLeader);
        }
    }

    public RuntimePartyOperationResult SetRank(string actorId, string rank)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(actorId, out var partyId)) return Fail("actor-not-in-party");
            var party = GetParty(partyId);
            if (party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase))
                return Fail("party-leader-rank-native-unknown");
            var normalized = rank.ToLowerInvariant() switch
            {
                "frontrank" => "Frontrank", "midrank" => "Midrank", "backrank" => "Backrank",
                _ => throw new ArgumentOutOfRangeException(nameof(rank))
            };
            if (normalized.Equals("Backrank", StringComparison.OrdinalIgnoreCase))
            {
                var frontAfter = party.Members.Values.Count(member =>
                    !member.ActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                    && member.Rank.Equals("Frontrank", StringComparison.OrdinalIgnoreCase));
                var backAfter = party.Members.Values.Count(member =>
                    !member.ActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                    && member.Rank.Equals("Backrank", StringComparison.OrdinalIgnoreCase)) + 1;
                // WCCMMHLP party help: at least as many users must be in the front rank as the back.
                if (frontAfter < backAfter) return Fail("party-backrank-front-balance-required");
            }
            party.Members[actorId] = party.Members[actorId] with { Rank = normalized };
            return Success(party);
        }
    }

    public bool CancelInvite(string leaderId, string targetId)
    {
        lock (_gate)
        {
            var invitation = _invites.Values.FirstOrDefault(invite =>
                invite.InviterId.Equals(leaderId, StringComparison.OrdinalIgnoreCase) &&
                invite.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase));
            return invitation is not null && _invites.Remove(invitation.InviteId);
        }
    }

    public RuntimePartyRouteResult RoutePartyMessage(string senderId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(senderId, out var partyId)) return new(false, "actor-not-in-party", Array.Empty<string>());
            var recipients = GetParty(partyId).Members.Values.Where(x => x.Connected).Select(x => x.ActorId)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
            return new(true, null, recipients);
        }
    }

    public IReadOnlyList<RuntimePartyFollowMove> ResolveFollowerMoves(string leaderId, string fromRoomId, string destinationRoomId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(leaderId, out var partyId)) return Array.Empty<RuntimePartyFollowMove>();
            var party = GetParty(partyId);
            if (!party.LeaderId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)) return Array.Empty<RuntimePartyFollowMove>();
            return party.Members.Values
                .Where(x => !x.ActorId.Equals(leaderId, StringComparison.OrdinalIgnoreCase) && x.Connected && x.FollowingLeader
                            && string.Equals(x.RoomId, fromRoomId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.ActorId, StringComparer.OrdinalIgnoreCase)
                .Select(x => new RuntimePartyFollowMove(x.ActorId, fromRoomId, destinationRoomId)).ToArray();
        }
    }

    public IReadOnlyList<RuntimePartyCoordinationSnapshot> CaptureAll()
    {
        lock (_gate) return _parties.Values.OrderBy(x => x.PartyId, StringComparer.OrdinalIgnoreCase).Select(Capture).ToArray();
    }

    public RuntimePartyCoordinationSnapshot? FindByMember(string actorId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(actorId, out var partyId) || !_parties.TryGetValue(partyId, out var party)) return null;
            return Capture(party);
        }
    }

    public RuntimePartyOperationResult SynchronizeConnection(string actorId, bool connected, string? roomId)
    {
        lock (_gate)
        {
            if (!_membership.TryGetValue(actorId, out var partyId)) return Fail("actor-not-in-party");
            var party = GetParty(partyId);
            party.Members[actorId] = party.Members[actorId] with { Connected = connected, RoomId = roomId };
            return Success(party);
        }
    }

    public IReadOnlyList<string> Validate()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var party in _parties.Values)
            {
                if (!party.Members.ContainsKey(party.LeaderId)) failures.Add("party-leader-not-member:" + party.PartyId);
                if (party.Members.Count > party.Policy.MaximumMembers) failures.Add("party-capacity-exceeded:" + party.PartyId);
                foreach (var actorId in party.Members.Keys)
                {
                    if (!seen.Add(actorId)) failures.Add("duplicate-party-membership:" + actorId);
                    if (!_membership.TryGetValue(actorId, out var owner) || !owner.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase))
                        failures.Add("party-membership-index-mismatch:" + actorId);
                }
            }
            foreach (var invite in _invites.Values)
                if (!_parties.ContainsKey(invite.PartyId)) failures.Add("orphan-party-invite:" + invite.InviteId);
            return failures;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(CaptureAll(), new JsonSerializerOptions { WriteIndented = true });

    private RuntimePartyCoordinationSnapshot Capture(PartyState party) => new(
        party.PartyId, party.LeaderId,
        party.Members.Values.OrderBy(x => x.ActorId, StringComparer.OrdinalIgnoreCase).ToArray(),
        _invites.Values.Where(x => x.PartyId.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ExpiresAtUtc).ThenBy(x => x.InviteId, StringComparer.OrdinalIgnoreCase).ToArray(),
        party.Policy);

    private RuntimePartyOperationResult Success(PartyState state) => new(true, null, Capture(state));
    private static RuntimePartyOperationResult Fail(string code) => new(false, code, null);
    private PartyState GetParty(string partyId) => _parties.TryGetValue(partyId, out var party) ? party : throw new InvalidOperationException("party-not-found");
    private string SelectReplacementLeader(PartyState party, string excludedActorId) => party.Members.Values
        .Where(x => !x.ActorId.Equals(excludedActorId, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x => x.Connected).ThenBy(x => x.ActorId, StringComparer.OrdinalIgnoreCase).First().ActorId;
    private void RemoveParty(PartyState party)
    {
        foreach (var actorId in party.Members.Keys) _membership.Remove(actorId);
        foreach (var inviteId in _invites.Values.Where(x => x.PartyId.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase)).Select(x => x.InviteId).ToArray()) _invites.Remove(inviteId);
        _parties.Remove(party.PartyId);
    }
    private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(name + "-required") : value;
    private static void ValidatePolicy(RuntimePartyPolicy policy)
    {
        if (policy.MaximumMembers < 1) throw new ArgumentOutOfRangeException(nameof(policy.MaximumMembers));
        if (policy.InviteLifetime is { } lifetime && lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(policy.InviteLifetime));
    }

    private sealed class PartyState
    {
        public PartyState(string partyId, string leaderId, RuntimePartyPolicy policy)
        {
            PartyId = partyId; LeaderId = leaderId; Policy = policy;
            Members.Add(leaderId, new RuntimePartyMemberState(leaderId, true, null, false, "Frontrank"));
        }
        public string PartyId { get; }
        public string LeaderId { get; set; }
        public RuntimePartyPolicy Policy { get; }
        public Dictionary<string, RuntimePartyMemberState> Members { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
