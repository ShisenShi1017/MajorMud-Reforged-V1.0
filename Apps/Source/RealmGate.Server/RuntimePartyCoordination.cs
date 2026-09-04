using System.Text.Json;

namespace RealmGate.Server;

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
    /// Classic MajorMUD recovered compatibility policy. The native WCCMMUD owner stores
    /// five WORD group slots per live character (self + five visible slots = six characters),
    /// with pending invitations occupying those same slots and no invitation timestamp.
    /// Live Classic commands use the separate per-character native graph below; this policy
    /// remains for generic compatibility-shell party callers and smoke coverage.
    /// </summary>
    public static RuntimePartyPolicy ClassicStrongEvidence { get; } = new(
        MaximumMembers: 6,
        InviteLifetime: null,
        DisconnectPolicy: RuntimePartyDisconnectPolicy.RemoveMember,
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
public sealed record RuntimeClassicPartyDepartureCleanup(
    bool WasMember,
    bool WasLeader,
    bool PartyDissolved,
    IReadOnlyList<string> DetachedActorIds);


/// <summary>
/// Direct executable model of the WCCMMUD live-user party fields: +0x6AA leader WORD,
/// +0x6AC rank byte, and five +0x6AE group-slot WORDs. This is intentionally separate
/// from the generic compatibility-shell PartyState because Classic permits overlapping
/// and temporarily stale slot links that an exclusive party object cannot represent.
/// </summary>
public sealed record RuntimeClassicPartyActorSnapshot(
    string ActorId,
    bool Connected,
    string? RoomId,
    string? LeaderId,
    string Rank,
    IReadOnlyList<string?> GroupSlots);

public sealed record RuntimeClassicPartyCommandResult(
    bool Succeeded,
    string? FailureCode,
    string? RelatedActorId,
    IReadOnlyList<string> RecipientActorIds);

public sealed record RuntimeClassicPartyDisplayEntry(string ActorId, bool Invited, string Rank);
public sealed record RuntimeClassicPartyDisplaySnapshot(
    string ActorId,
    string? LeaderId,
    IReadOnlyList<RuntimeClassicPartyDisplayEntry> Entries,
    bool HasAnyGroupSlot);

public sealed class RuntimePartyCoordinationService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PartyState> _parties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _membership = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimePartyInviteSnapshot> _invites = new(StringComparer.OrdinalIgnoreCase);
    private const int ClassicNativeGroupSlotCount = 5;
    private readonly Dictionary<string, ClassicActorState> _classicActors = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Exact native _stop_following(leavingUsr,-1) + _remove_from_group(leavingUsr) departure boundary.
    /// Every actor whose +0x6AA leader pointer names the departing user is detached and run
    /// through _remove_from_group; the departing user is then removed globally from every
    /// remaining +0x6AE slot. No replacement leader is elected.
    /// </summary>
    public RuntimeClassicPartyDepartureCleanup CleanupClassicDeparture(string actorId)
    {
        lock (_gate)
        {
            if (!_classicActors.TryGetValue(actorId, out var actor))
                return new(false, false, false, Array.Empty<string>());

            var incomingSlot = _classicActors.Values.Any(candidate =>
                !candidate.ActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                && candidate.GroupSlots.Any(slot => SameClassicActor(slot, actorId)));
            var followers = _classicActors.Values
                .Where(candidate => !candidate.ActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                    && SameClassicActor(candidate.LeaderId, actorId))
                .Select(candidate => candidate.ActorId)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var wasLeader = followers.Length > 0 || (actor.LeaderId is null && actor.GroupSlots.Any(slot => slot is not null));
            var wasMember = actor.LeaderId is not null || actor.GroupSlots.Any(slot => slot is not null) || incomingSlot || followers.Length > 0;

            foreach (var followerId in followers)
            {
                if (!_classicActors.TryGetValue(followerId, out var follower)) continue;
                follower.LeaderId = null;
                RemoveFromClassicGroup(follower);
            }

            RemoveFromClassicGroup(actor);
            _classicActors.Remove(actorId);
            var detached = followers.Append(actorId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return new(wasMember, wasLeader, wasLeader, detached);
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
            {
                // Classic departure cleanup has no leader succession: a disconnected leader
                // detaches every follower and dissolves the party; a follower is simply removed.
                if (party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveParty(party);
                    return new(true, null, null);
                }
                party.Members.Remove(actorId);
                _membership.Remove(actorId);
                foreach (var inviteId in _invites.Values
                             .Where(invite => invite.TargetId.Equals(actorId, StringComparison.OrdinalIgnoreCase))
                             .Select(invite => invite.InviteId).ToArray())
                    _invites.Remove(inviteId);
                return Success(party);
            }
            if (!connected && party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                && party.Policy.DisconnectPolicy == RuntimePartyDisconnectPolicy.TransferLeaderThenRetain)
                party.LeaderId = SelectReplacementLeader(party, actorId);
            return Success(party);
        }
    }

    public void RemoveActorForCharacterReplacement(string actorId)
    {
        lock (_gate)
        {
            foreach (var invite in _invites.Values
                         .Where(invite => invite.TargetId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                                          || invite.InviterId.Equals(actorId, StringComparison.OrdinalIgnoreCase))
                         .ToArray())
                _invites.Remove(invite.InviteId);

            if (!_membership.TryGetValue(actorId, out var partyId)) return;
            var party = GetParty(partyId);
            if (party.LeaderId.Equals(actorId, StringComparison.OrdinalIgnoreCase))
            {
                RemoveParty(party);
                return;
            }
            party.Members.Remove(actorId);
            _membership.Remove(actorId);
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

    /// <summary>
    /// Registers or refreshes one live Classic user. Session departure is owned by
    /// CleanupClassicDeparture; this method never infers disconnect cleanup from absence.
    /// </summary>
    public RuntimeClassicPartyActorSnapshot SynchronizeClassicActor(string actorId, bool connected, string? roomId)
    {
        lock (_gate)
        {
            var actor = EnsureClassicActor(actorId);
            actor.Connected = connected;
            actor.RoomId = roomId;
            return CaptureClassic(actor);
        }
    }

    public RuntimeClassicPartyActorSnapshot? CaptureClassicActor(string actorId)
    {
        lock (_gate) return _classicActors.TryGetValue(actorId, out var actor) ? CaptureClassic(actor) : null;
    }

    /// <summary>
    /// Native _cmd_invite travel-party branch. The inviter is forced to Frontrank before
    /// _add_to_group(mode 0). A pending invitation is the raw one-way slot itself; there is
    /// no separate timer. When all five slots are occupied, native skips target notification
    /// but still reaches the inviter confirmation branch.
    /// </summary>
    public RuntimeClassicPartyCommandResult InviteClassic(string inviterId, string targetId)
    {
        lock (_gate)
        {
            var inviter = EnsureClassicActor(inviterId);
            _ = EnsureClassicActor(targetId);
            inviter.Rank = "Frontrank";
            var added = AddToClassicGroup(inviterId, targetId, propagate: false);
            return added
                ? ClassicSuccess(targetId, new[] { targetId })
                : ClassicFail("party-native-slot-add-failed", targetId);
        }
    }

    /// <summary>
    /// Native _cmd_follow owner. The follower first clears each of its own five slots except
    /// a slot naming the selected leader, then _add_to_group(leader,follower,mode 1) must find
    /// a prior invitation in the leader's slots. Success overwrites +0x6AA directly; it does
    /// not stop/clean an old leader first, so stale incoming links can remain until cleanup.
    /// </summary>
    public RuntimeClassicPartyCommandResult FollowClassic(string followerId, string leaderId)
    {
        lock (_gate)
        {
            if (SameClassicActor(followerId, leaderId)) return ClassicFail("party-cannot-follow-self", leaderId);
            var follower = EnsureClassicActor(followerId);
            _ = EnsureClassicActor(leaderId);
            for (var index = 0; index < follower.GroupSlots.Length; index++)
                if (!SameClassicActor(follower.GroupSlots[index], leaderId)) follower.GroupSlots[index] = null;

            if (!AddToClassicGroup(leaderId, followerId, propagate: true))
                return ClassicFail("party-invite-not-found", leaderId);

            follower.LeaderId = leaderId;
            return ClassicSuccess(leaderId, new[] { leaderId });
        }
    }

    /// <summary>
    /// Bare FOLLOW/LEAVE follower stop: _stop_following(currentLeader,follower) invokes
    /// _remove_from_group(follower), clears +0x6AA, and reports only to the follower.
    /// </summary>
    public RuntimeClassicPartyCommandResult StopFollowingClassic(string actorId)
    {
        lock (_gate)
        {
            if (!_classicActors.TryGetValue(actorId, out var actor) || actor.LeaderId is null)
                return ClassicFail("party-not-following");
            var leaderId = actor.LeaderId;
            StopFollowingSpecific(leaderId, actorId);
            return ClassicSuccess(leaderId);
        }
    }

    /// <summary>
    /// Native _stop_following(actor,-1), reached by DISBAND PARTY. Detach every live direct
    /// follower, run each through _remove_from_group, then remove the actor's group slots.
    /// The actor's own +0x6AA leader pointer is deliberately not changed by this -1 branch.
    /// </summary>
    public RuntimeClassicPartyCommandResult DisbandClassic(string actorId)
    {
        lock (_gate)
        {
            var actor = EnsureClassicActor(actorId);
            var followers = _classicActors.Values
                .Where(candidate => !candidate.ActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
                    && SameClassicActor(candidate.LeaderId, actorId))
                .Select(candidate => candidate.ActorId)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var followerId in followers)
            {
                if (!_classicActors.TryGetValue(followerId, out var follower)) continue;
                follower.LeaderId = null;
                RemoveFromClassicGroup(follower);
            }
            RemoveFromClassicGroup(actor);
            return ClassicSuccess(actorId, followers);
        }
    }

    /// <summary>
    /// Native UNINVITE first runs _stop_following(actor,target); that has an effect only when
    /// target directly follows actor. It then removes target from actor's own five slots and
    /// resets actor to Midrank when no slots remain. The resolved command succeeds even when
    /// the slot was already absent.
    /// </summary>
    public RuntimeClassicPartyCommandResult UninviteClassic(string actorId, string targetId)
    {
        lock (_gate)
        {
            var actor = EnsureClassicActor(actorId);
            var target = EnsureClassicActor(targetId);
            var stoppedDirectFollower = SameClassicActor(target.LeaderId, actorId);
            if (stoppedDirectFollower) StopFollowingSpecific(actorId, targetId);
            RemoveClassicSlot(actor, targetId);
            if (actor.GroupSlots.All(slot => slot is null)) actor.Rank = "Midrank";
            return ClassicSuccess(targetId, stoppedDirectFollower ? new[] { targetId } : Array.Empty<string>());
        }
    }

    public RuntimeClassicPartyCommandResult StopFollowingAfterFailedClassicMove(string actorId)
    {
        lock (_gate)
        {
            if (!_classicActors.TryGetValue(actorId, out var actor) || actor.LeaderId is null)
                return ClassicFail("party-not-following");
            var leaderId = actor.LeaderId;
            // _fast_update_character's guaranteed mode-3 movement failure effect clears only
            // the leader pointer. It does not call _remove_from_group at this outer owner.
            actor.LeaderId = null;
            return ClassicSuccess(leaderId);
        }
    }

    public RuntimeClassicPartyCommandResult SetClassicRank(string actorId, string rank)
    {
        lock (_gate)
        {
            if (!_classicActors.TryGetValue(actorId, out var actor))
                return ClassicFail("party-not-present-rank-reset");

            PruneInactiveClassicSlots(actor);
            var visible = actor.GroupSlots
                .Where(slot => slot is not null
                    && _classicActors.TryGetValue(slot, out var target)
                    && target.Connected
                    && IsClassicTravelRelation(actor, target))
                .Select(slot => _classicActors[slot!])
                .ToArray();
            if (visible.Length + 1 < 2)
            {
                actor.Rank = "Midrank";
                return ClassicFail("party-not-present-rank-reset");
            }

            var frontOthers = visible.Count(member => member.Rank.Equals("Frontrank", StringComparison.OrdinalIgnoreCase));
            var backOthers = visible.Count(member => member.Rank.Equals("Backrank", StringComparison.OrdinalIgnoreCase));
            var normalized = rank.Trim().ToLowerInvariant() switch
            {
                "frontrank" or "front" => "Frontrank",
                "backrank" or "back" => "Backrank",
                "midrank" or "middle" or "mid" => "Midrank",
                _ => throw new ArgumentOutOfRangeException(nameof(rank))
            };

            if (normalized == "Backrank")
            {
                if (actor.LeaderId is null) return ClassicFail("party-leader-cannot-enter-backrank");
                if (frontOthers == 0 && actor.Rank == "Frontrank") return ClassicFail("party-only-frontrank-cannot-leave");
                if (frontOthers < backOthers) return ClassicFail("party-backrank-front-balance-required");
            }
            else if (normalized == "Midrank")
            {
                if (frontOthers == 0 && actor.Rank == "Frontrank") return ClassicFail("party-only-frontrank-cannot-leave");
                if (backOthers == 0 && actor.Rank == "Backrank") return ClassicFail("party-only-backrank-cannot-leave");
                if (actor.LeaderId is null) return ClassicFail("party-leader-cannot-leave-current-rank");
            }

            actor.Rank = normalized;
            var recipients = actor.GroupSlots
                .Where(slot => slot is not null && _classicActors.TryGetValue(slot, out var target)
                    && target.Connected && IsClassicTravelRelation(actor, target))
                .Select(slot => slot!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return ClassicSuccess(null, recipients);
        }
    }

    public IReadOnlyList<RuntimePartyFollowMove> ResolveClassicFollowerMoves(string leaderId, string fromRoomId, string destinationRoomId)
    {
        lock (_gate)
        {
            return _classicActors.Values
                .Where(actor => actor.Connected
                    && SameClassicActor(actor.LeaderId, leaderId)
                    && string.Equals(actor.RoomId, fromRoomId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase)
                .Select(actor => new RuntimePartyFollowMove(actor.ActorId, fromRoomId, destinationRoomId))
                .ToArray();
        }
    }

    /// <summary>
    /// Native target type 12 excludes self, direct leader/follower links, and any raw actor id
    /// in the caster's five slots. Raw pending/stale slots intentionally count here.
    /// </summary>
    public bool IsClassicHostilePartyTargetExcluded(string casterId, string targetId)
    {
        lock (_gate)
        {
            if (SameClassicActor(casterId, targetId)) return true;
            if (!_classicActors.TryGetValue(casterId, out var caster)) return false;
            if (SameClassicActor(caster.LeaderId, targetId)) return true;
            if (_classicActors.TryGetValue(targetId, out var target) && SameClassicActor(target.LeaderId, casterId)) return true;
            return caster.GroupSlots.Any(slot => SameClassicActor(slot, targetId));
        }
    }

    /// <summary>
    /// Native target type 13 admits self, the direct leader, direct followers, and all raw
    /// +0x6AE slots. The caller applies room/world/activity filters after this relation step.
    /// </summary>
    public IReadOnlyList<string> ResolveClassicType13ActorIds(string casterId)
    {
        lock (_gate)
        {
            if (!_classicActors.TryGetValue(casterId, out var caster)) return new[] { casterId };
            var result = new List<string> { casterId };
            if (caster.LeaderId is not null) result.Add(caster.LeaderId);
            result.AddRange(_classicActors.Values
                .Where(candidate => SameClassicActor(candidate.LeaderId, casterId))
                .Select(candidate => candidate.ActorId));
            result.AddRange(caster.GroupSlots.Where(slot => slot is not null).Select(slot => slot!));
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    /// <summary>
    /// PARTY SHARE/display/rank notifications do not treat every raw slot as membership.
    /// Once a valid slot is found, WCCMMUD applies the leader-pointer relation below and
    /// excludes root-to-root invitation slots as [Invited].
    /// </summary>
    public IReadOnlyList<string> ResolveClassicTravelPartyActorIds(string actorId)
    {
        lock (_gate)
        {
            if (!_classicActors.TryGetValue(actorId, out var actor)) return Array.Empty<string>();
            PruneInactiveClassicSlots(actor);
            return actor.GroupSlots
                .Where(slot => slot is not null && _classicActors.TryGetValue(slot, out var target)
                    && target.Connected && IsClassicTravelRelation(actor, target))
                .Select(slot => slot!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public RuntimeClassicPartyDisplaySnapshot CaptureClassicDisplay(string actorId)
    {
        lock (_gate)
        {
            var actor = EnsureClassicActor(actorId);
            PruneInactiveClassicSlots(actor);
            var entries = actor.GroupSlots
                .Where(slot => slot is not null)
                .Select(slot => _classicActors[slot!])
                .Select(target => new RuntimeClassicPartyDisplayEntry(
                    target.ActorId,
                    Invited: !IsClassicTravelRelation(actor, target),
                    target.Rank))
                .ToArray();
            if (entries.Length == 0) actor.Rank = "Midrank";
            var visibleLeader = actor.LeaderId is not null
                && _classicActors.TryGetValue(actor.LeaderId, out var leader)
                && leader.Connected
                ? actor.LeaderId
                : null;
            // Native PARTY display clears +0x6AA when its stored leader no longer resolves
            // to a live user; this is an observable cleanup side effect of the display owner.
            if (actor.LeaderId is not null && visibleLeader is null) actor.LeaderId = null;
            return new(actor.ActorId, visibleLeader, entries, entries.Length > 0);
        }
    }

    public IReadOnlyList<string> ValidateClassicGraph()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var actor in _classicActors.Values)
            {
                if (actor.GroupSlots.Length != ClassicNativeGroupSlotCount) failures.Add("classic-party-slot-width:" + actor.ActorId);
                if (actor.GroupSlots.Any(slot => SameClassicActor(slot, actor.ActorId))) failures.Add("classic-party-self-slot:" + actor.ActorId);
                foreach (var slot in actor.GroupSlots.Where(slot => slot is not null))
                    if (!_classicActors.ContainsKey(slot!)) failures.Add("classic-party-missing-slot-user:" + actor.ActorId + ":" + slot);
            }
            return failures;
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

    private RuntimeClassicPartyActorSnapshot CaptureClassic(ClassicActorState actor) => new(
        actor.ActorId, actor.Connected, actor.RoomId, actor.LeaderId, actor.Rank, actor.GroupSlots.ToArray());

    private ClassicActorState EnsureClassicActor(string actorId)
    {
        Require(actorId, nameof(actorId));
        if (!_classicActors.TryGetValue(actorId, out var actor))
        {
            actor = new ClassicActorState(actorId);
            _classicActors.Add(actorId, actor);
        }
        return actor;
    }

    /// <summary>
    /// Exact _add_to_group modes recovered from WCCMMUD. Mode 0 inserts into the first empty
    /// one of five slots (or succeeds if already present). Mode 1 requires an existing owner
    /// slot for target, then attempts reciprocal owner/peer links for each current owner slot;
    /// reciprocal overflow failures are intentionally ignored by the native owner.
    /// </summary>
    private bool AddToClassicGroup(string ownerId, string targetId, bool propagate)
    {
        if (SameClassicActor(ownerId, targetId)) return false;
        var owner = EnsureClassicActor(ownerId);
        _ = EnsureClassicActor(targetId);
        var existingIndex = Array.FindIndex(owner.GroupSlots, slot => SameClassicActor(slot, targetId));
        if (existingIndex < 0)
        {
            if (propagate) return false;
            var emptyIndex = Array.FindIndex(owner.GroupSlots, slot => slot is null);
            if (emptyIndex < 0) return false;
            owner.GroupSlots[emptyIndex] = targetId;
            return true;
        }

        if (!propagate) return true;
        var ownerSlots = owner.GroupSlots.Where(slot => slot is not null).Select(slot => slot!).ToArray();
        foreach (var memberId in ownerSlots)
        {
            _ = AddToClassicGroup(memberId, ownerId, propagate: false);
            foreach (var peerId in ownerSlots) _ = AddToClassicGroup(memberId, peerId, propagate: false);
        }
        return true;
    }

    /// <summary>
    /// Exact _remove_from_group shape: reset actor rank, unlink actor from each actor in its
    /// own slots (resetting a linked actor to Midrank if it becomes slot-empty), clear the
    /// actor's slots, then scan every live user and remove the actor from all remaining slots.
    /// Leader pointers are not changed by this helper.
    /// </summary>
    private void RemoveFromClassicGroup(ClassicActorState actor)
    {
        actor.Rank = "Midrank";
        var linkedIds = actor.GroupSlots.Where(slot => slot is not null).Select(slot => slot!).ToArray();
        foreach (var linkedId in linkedIds)
        {
            if (_classicActors.TryGetValue(linkedId, out var linked))
            {
                RemoveClassicSlot(linked, actor.ActorId);
                if (linked.GroupSlots.All(slot => slot is null)) linked.Rank = "Midrank";
            }
        }
        Array.Fill(actor.GroupSlots, null);
        foreach (var candidate in _classicActors.Values) RemoveClassicSlot(candidate, actor.ActorId);
    }

    private bool StopFollowingSpecific(string leaderId, string followerId)
    {
        if (!_classicActors.TryGetValue(followerId, out var follower) || !SameClassicActor(follower.LeaderId, leaderId)) return false;
        RemoveFromClassicGroup(follower);
        follower.LeaderId = null;
        return true;
    }

    private void PruneInactiveClassicSlots(ClassicActorState actor)
    {
        for (var index = 0; index < actor.GroupSlots.Length; index++)
        {
            var slot = actor.GroupSlots[index];
            if (slot is null) continue;
            if (!_classicActors.TryGetValue(slot, out var target) || !target.Connected) actor.GroupSlots[index] = null;
        }
    }

    private static bool IsClassicTravelRelation(ClassicActorState owner, ClassicActorState target)
    {
        var related = SameClassicActor(target.LeaderId, owner.LeaderId)
            || SameClassicActor(target.LeaderId, owner.ActorId)
            || SameClassicActor(owner.LeaderId, target.ActorId);
        // Both +0x6AA values at -1/null means a raw leader->invitee slot, not an accepted member.
        return related && !(owner.LeaderId is null && target.LeaderId is null);
    }

    private static void RemoveClassicSlot(ClassicActorState actor, string targetId)
    {
        for (var index = 0; index < actor.GroupSlots.Length; index++)
            if (SameClassicActor(actor.GroupSlots[index], targetId)) actor.GroupSlots[index] = null;
    }

    private static bool SameClassicActor(string? left, string? right) =>
        left is not null && right is not null && left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static RuntimeClassicPartyCommandResult ClassicSuccess(
        string? relatedActorId = null,
        IReadOnlyList<string>? recipientActorIds = null) =>
        new(true, null, relatedActorId, recipientActorIds ?? Array.Empty<string>());

    private static RuntimeClassicPartyCommandResult ClassicFail(string code, string? relatedActorId = null) =>
        new(false, code, relatedActorId, Array.Empty<string>());

    private sealed class ClassicActorState
    {
        public ClassicActorState(string actorId)
        {
            ActorId = actorId;
            GroupSlots = new string?[ClassicNativeGroupSlotCount];
        }

        public string ActorId { get; }
        public bool Connected { get; set; }
        public string? RoomId { get; set; }
        public string? LeaderId { get; set; }
        public string Rank { get; set; } = "Midrank";
        public string?[] GroupSlots { get; }
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
