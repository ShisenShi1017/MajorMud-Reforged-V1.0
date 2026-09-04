namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string ExecuteClassicPartyCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor) return "Only a character can join a travel party.";
        SynchronizeClassicPartyActiveLocations();
        return command.Name switch
        {
            "invite" => InviteToClassicParty(session, command.ArgumentText),
            "uninvite" => UninviteFromClassicParty(session, command.ArgumentText),
            "follow" => FollowClassicPartyLeader(session, command.ArgumentText),
            "join" => JoinClassicParty(session, command.ArgumentText),
            "leave" => LeaveClassicParty(session),
            "frontrank" or "midrank" or "backrank" => SetClassicPartyRank(session, command.Name),
            _ => throw new InvalidOperationException($"Unsupported Classic party command '{command.Name}'.")
        };
    }

    private string InviteToClassicParty(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Invite whom?";
        var target = _sessions.FindUniqueInRoom(session, query);
        if (target is null) return $"You don't see {query.Trim()} here!";

        // Native _cmd_invite forces the inviter to Frontrank and calls _add_to_group(mode 0).
        // A pending invite is the raw one-way +0x6AE slot; there is no separate invite object
        // or timeout. On a full five-slot table the target receives nothing, but the inviter's
        // confirmation branch still runs.
        var result = _partyGameplay.InviteClassic(session.Actor.Id, target.Actor.Id);
        if (result.Succeeded)
        {
            var pronoun = (session.Actor as RuntimeCharacterActor)?.Sex.Equals("Female", StringComparison.OrdinalIgnoreCase) == true ? "her" : "him";
            target.EnqueueMessage($"{session.Actor.DisplayName} has invited you to follow {pronoun}.");
        }
        return $"You have invited {target.Actor.DisplayName} to follow you.";
    }

    private string FollowClassicPartyLeader(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            var actor = _partyGameplay.CaptureClassicActor(session.Actor.Id);
            if (actor?.LeaderId is { } currentLeaderId)
            {
                var leaderSession = _sessions.ActiveSessions.FirstOrDefault(candidate =>
                    candidate.Actor.Id.Equals(currentLeaderId, StringComparison.OrdinalIgnoreCase));
                var stopped = _partyGameplay.StopFollowingClassic(session.Actor.Id);
                if (stopped.Succeeded)
                {
                    var leaderName = leaderSession?.Actor.DisplayName ?? currentLeaderId;
                    // _stop_following reports only to the follower. There is no recovered
                    // leader-side "stops following you" notification in this owner.
                    return $"You are no longer following {leaderName}.";
                }
            }
            return "Syntax: FOLLOW {user/monster}";
        }

        var leader = _sessions.FindUniqueInRoom(session, query);
        if (leader is null)
        {
            var value = query.Trim();
            var monsterMatches = LivingNpcsInRoom(session)
                .Where(npc => npc.Alive && (npc.InstanceId.Equals(value, StringComparison.OrdinalIgnoreCase)
                    || npc.NpcId.Equals(value, StringComparison.OrdinalIgnoreCase)
                    || npc.Name.Equals(value, StringComparison.OrdinalIgnoreCase)
                    || npc.Name.Contains(value, StringComparison.OrdinalIgnoreCase)))
                .Take(2)
                .ToArray();
            // WCCMMUD parses monsters in FOLLOW but terminates that native branch with this
            // exact message; monster following itself was not implemented by Classic.
            if (monsterMatches.Length == 1) return "Following a monster - Not implemented yet.";
            return $"You don't see {value} here!";
        }
        if (leader.Actor.Id.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase))
            return "Why would you follow yourself?";

        var result = _partyGameplay.FollowClassic(session.Actor.Id, leader.Actor.Id);
        if (!result.Succeeded) return result.FailureCode switch
        {
            "party-invite-not-found" => "You must be invited first!",
            "party-cannot-follow-self" => "Why would you follow yourself?",
            _ => result.FailureCode ?? "You cannot follow that character."
        };

        // WCCMMUD strings at 0x489640 / 0x48965A.
        leader.EnqueueMessage($"{session.Actor.DisplayName} started to follow you.");
        return $"You are now following {leader.Actor.DisplayName}";
    }

    private string JoinClassicParty(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Syntax: JOIN {group number} or {user name} or GANG {gangname}";
        if (int.TryParse(query.Trim(), out var broadcastGroup))
            return SetClassicBroadcastGroup(session, broadcastGroup);

        // Native _cmd_join delegates the user-name branch directly to _cmd_follow.
        // JOIN GANG is routed before this partial; numeric JOIN remains broadcast-group selection.
        return FollowClassicPartyLeader(session, query);
    }

    private string UninviteFromClassicParty(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Uninvite whom?";
        var target = _sessions.FindByActor(query);
        if (target is null) return $"No connected character uniquely matches '{query.Trim()}'.";

        var result = _partyGameplay.UninviteClassic(session.Actor.Id, target.Actor.Id);
        if (result.RecipientActorIds.Contains(target.Actor.Id, StringComparer.OrdinalIgnoreCase))
            target.EnqueueMessage($"You are no longer following {session.Actor.DisplayName}.");

        // Native _cmd_uninvite reaches this confirmation for a resolved target even when the
        // caller merely removed a pending/raw slot rather than a direct follower.
        return $"{target.Actor.DisplayName} has been removed from your followers.";
    }

    private string DisbandClassicParty(RuntimeSession session)
    {
        var result = _partyGameplay.DisbandClassic(session.Actor.Id);
        foreach (var followerId in result.RecipientActorIds)
        {
            var follower = _sessions.FindByActor(followerId);
            if (follower is not null)
                follower.EnqueueMessage($"You are no longer following {session.Actor.DisplayName}.");
        }
        // Native DISBAND PARTY itself adds no caller-side status line after _stop_following(user,-1).
        return string.Empty;
    }

    private string LeaveClassicParty(RuntimeSession session)
    {
        var actor = _partyGameplay.CaptureClassicActor(session.Actor.Id);
        if (actor?.LeaderId is not { } leaderId)
        {
            // Native _cmd_leave only calls _stop_following when +0x6AA names a leader. A root
            // actor does not dissolve its travel graph with bare LEAVE; the command then reaches
            // the independent broadcast-group leave branch.
            return GetClassicBroadcastGroup(session) == 0
                ? string.Empty
                : SetClassicBroadcastGroup(session, 0);
        }

        var leader = _sessions.ActiveSessions.FirstOrDefault(candidate =>
            candidate.Actor.Id.Equals(leaderId, StringComparison.OrdinalIgnoreCase));
        var result = _partyGameplay.StopFollowingClassic(session.Actor.Id);
        if (!result.Succeeded) return "You are not in a party at the present time.";
        return $"You are no longer following {leader?.Actor.DisplayName ?? leaderId}.";
    }

    private string SetClassicPartyRank(RuntimeSession session, string rank)
    {
        var result = _partyGameplay.SetClassicRank(session.Actor.Id, rank);
        if (!result.Succeeded) return result.FailureCode switch
        {
            "party-leader-cannot-enter-backrank" => "You may not enter the backrank of your own party.",
            "party-only-frontrank-cannot-leave" => "You are the only one in the front rank.  You may not leave.",
            "party-backrank-front-balance-required" => "There would not be enough people left in the frontrank if you did that.",
            "party-only-backrank-cannot-leave" => "You are the only one in the back rank.  You may not leave.",
            "party-leader-cannot-leave-current-rank" => "You may not leave your current rank in your own party.",
            "party-not-present-rank-reset" => "You are not in a party at the present time.",
            _ => "You are not in a party at the present time."
        };

        var normalized = rank switch { "frontrank" => "front", "backrank" => "back", _ => "middle" };
        var otherMessage = normalized switch
        {
            "front" => $"{session.Actor.DisplayName} just moved to the front rank in your group.",
            "back" => $"{session.Actor.DisplayName} just moved to the back rank in your group.",
            _ => $"{session.Actor.DisplayName} just moved to the middle of your group."
        };
        foreach (var recipient in _sessions.ActiveSessions.Where(candidate =>
                     result.RecipientActorIds.Contains(candidate.Actor.Id, StringComparer.OrdinalIgnoreCase)))
            recipient.EnqueueMessage(otherMessage);

        return normalized switch
        {
            "front" => "You have moved to the front ranks of your group.",
            "back" => "You have moved to the back ranks of your group.",
            _ => "You have moved to the middle ranks of your group."
        };
    }

    /// <summary>
    /// Native _cleanup_when_user_leaves always calls _stop_following(user,-1). Every actor
    /// whose +0x6AA points at a departing leader is detached and removed from the native group
    /// graph, then the departing actor is globally removed from every +0x6AE slot. Death,
    /// graceful logoff, and unexpected hangup all converge on this same owner.
    /// </summary>
    public RuntimeClassicPartyDepartureCleanup HandleClassicSessionPartyCleanup(RuntimeSession session) =>
        _partyGameplay.CleanupClassicDeparture(session.Actor.Id);

    public void HandleCharacterRerollCleanup(RuntimeSession session, string oldCharacterId)
    {
        // The replacement is already durable, but the session still owns the departed actor.
        // Remove every live owner keyed to that identity before swapping actors.
        try { BreakLiveCombat(session); } catch { }
        try { _partyGameplay.RemoveActorForCharacterReplacement(oldCharacterId); } catch { }
        try { new RuntimeClassicGangStateService(_characters.ProjectRoot).RemoveCharacterForReroll(oldCharacterId); } catch { }

        try
        {
            var living = Living(session);
            foreach (var npc in living.Capture().Npcs.Where(npc =>
                         npc.Metadata.TryGetValue("npc_companion_owner_kind", out var kind)
                         && kind.Equals("character", StringComparison.OrdinalIgnoreCase)
                         && npc.Metadata.TryGetValue("npc_companion_owner_id", out var ownerId)
                         && ownerId.Equals(oldCharacterId, StringComparison.Ordinal)))
            {
                living.CleanupNpcCompanionLifecycle(new RuntimeNpcCompanionCleanupRequest(
                    npc.InstanceId, "character", oldCharacterId, "owner-reroll",
                    "runtime.reroll.character-replacement", DateTimeOffset.UtcNow, 0));
            }
        }
        catch { }

        try
        {
            lock (_livePowerGate)
            {
                if (_liveSpellEngine is not null
                    && _livePowerWorldId is not null
                    && _livePowerWorldId.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
                {
                    var snapshot = _liveSpellEngine.Snapshot();
                    var spells = snapshot.KnownSpells
                        .Where(spell => !spell.CharacterId.Equals(oldCharacterId, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    var effects = snapshot.ActiveEffects
                        .Where(effect => !effect.CasterId.Equals(oldCharacterId, StringComparison.OrdinalIgnoreCase)
                                         && !effect.Target.Id.Equals(oldCharacterId, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    var resources = snapshot.Resources
                        .Where(pair => !pair.Key.Equals(oldCharacterId, StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                    _liveSpellEngine.RestoreSnapshot(new RuntimeSpellState(spells, effects, resources));
                    _liveAbilityStates.Remove(oldCharacterId);
                    foreach (var key in _liveSpellCooldowns.Keys
                                 .Where(key => key.StartsWith(oldCharacterId + "\u001f", StringComparison.OrdinalIgnoreCase))
                                 .ToArray())
                        _liveSpellCooldowns.Remove(key);
                    PersistLivePowers(session.World.Definition.Id);
                }
                else
                {
                    new RuntimeLivePowerStateRepository(_characters.ProjectRoot, session.World.Definition.Id)
                        .RemoveCharacter(oldCharacterId);
                }
            }
        }
        catch { }
    }
}
