namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string ExecuteClassicPartyCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor) return "Only a character can join a travel party.";
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
        var party = _partyGameplay.FindByMember(session.Actor.Id);
        if (party is null) party = _partyGameplay.Create(
            $"party.{Guid.NewGuid():N}",
            session.Actor.Id,
            RuntimePartyPolicy.ClassicStrongEvidence);
        if (!party.LeaderId.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase)) return "Only the party leader may invite followers.";
        try
        {
            _partyGameplay.Invite(party.PartyId, session.Actor.Id, target.Actor.Id, DateTimeOffset.UtcNow);
            var pronoun = (session.Actor as RuntimeCharacterActor)?.Sex.Equals("Female", StringComparison.OrdinalIgnoreCase) == true ? "her" : "him";
            target.EnqueueMessage($"{session.Actor.DisplayName} has invited you to follow {pronoun}.");
            return $"You have invited {target.Actor.DisplayName} to follow you.";
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message == "party-full"
                ? "Native party-full failure text is not yet recovered."
                : exception.Message;
        }
    }

    private string FollowClassicPartyLeader(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Follow whom?";
        var leader = _sessions.FindUniqueInRoom(session, query);
        if (leader is null) return $"You don't see {query.Trim()} here!";

        var existing = _partyGameplay.FindByMember(session.Actor.Id);
        RuntimePartyOperationResult result;
        if (existing is not null && existing.LeaderId.Equals(leader.Actor.Id, StringComparison.OrdinalIgnoreCase))
            result = _partyGameplay.SetFollowing(session.Actor.Id, true);
        else
            result = _partyGameplay.AcceptFromLeader(session.Actor.Id, leader.Actor.Id, DateTimeOffset.UtcNow, followingLeader: true);

        if (!result.Succeeded) return result.FailureCode switch
        {
            "party-full" => "Native party-full failure text is not yet recovered.",
            _ => result.FailureCode ?? "You cannot follow that character."
        };
        leader.EnqueueMessage($"{session.Actor.DisplayName} started to follow you.");
        return $"You are now following {leader.Actor.DisplayName}.";
    }

    private string JoinClassicParty(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Join whom?";
        if (int.TryParse(query.Trim(), out var broadcastGroup))
            return SetClassicBroadcastGroup(session, broadcastGroup);

        var leader = _sessions.FindUniqueInRoom(session, query);
        if (leader is null) return $"You don't see {query.Trim()} here!";
        var result = _partyGameplay.AcceptFromLeader(session.Actor.Id, leader.Actor.Id, DateTimeOffset.UtcNow, followingLeader: true);
        if (!result.Succeeded) return result.FailureCode switch
        {
            "party-full" => "Native party-full failure text is not yet recovered.",
            _ => result.FailureCode ?? "You cannot join that character's party."
        };
        leader.EnqueueMessage($"{session.Actor.DisplayName} started to follow you.");
        return $"You are now following {leader.Actor.DisplayName}.";
    }

    private string UninviteFromClassicParty(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Uninvite whom?";
        var party = _partyGameplay.FindByMember(session.Actor.Id);
        if (party is null || !party.LeaderId.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase)) return "Only the party leader may remove followers.";
        var target = _sessions.FindByActor(query);
        if (target is null) return $"No connected character uniquely matches '{query.Trim()}'.";
        if (_partyGameplay.CancelInvite(session.Actor.Id, target.Actor.Id)) return $"{target.Actor.DisplayName} has been removed from your followers.";
        var result = _partyGameplay.Kick(session.Actor.Id, target.Actor.Id);
        if (!result.Succeeded) return result.FailureCode ?? "That character is not in your party.";
        target.EnqueueMessage($"You are no longer following {session.Actor.DisplayName}.");
        return $"{target.Actor.DisplayName} has been removed from your followers.";
    }

    private string LeaveClassicParty(RuntimeSession session)
    {
        var party = _partyGameplay.FindByMember(session.Actor.Id);
        if (party is null) return SetClassicBroadcastGroup(session, 0);
        var leader = _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Actor.Id.Equals(party.LeaderId, StringComparison.OrdinalIgnoreCase));
        if (party.LeaderId.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase) && party.Members.Count > 1)
            return "Native party-leader LEAVE succession behavior is not yet recovered.";
        _partyGameplay.Leave(session.Actor.Id);
        if (leader is not null && !leader.Actor.Id.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase))
            leader.EnqueueMessage($"{session.Actor.DisplayName} stops following you.");
        return leader is null || leader.Actor.Id.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase)
            ? "Your travel party has been dissolved."
            : $"You are no longer following {leader.Actor.DisplayName}.";
    }

    private string SetClassicPartyRank(RuntimeSession session, string rank)
    {
        var result = _partyGameplay.SetRank(session.Actor.Id, rank);
        if (!result.Succeeded) return result.FailureCode switch
        {
            "party-leader-rank-native-unknown" => "Native party-leader rank-change behavior is not yet recovered.",
            "party-backrank-front-balance-required" => "At least as many party members must remain in the Frontrank as the Backrank.",
            _ => "You are not in a party at the present time."
        };
        return $"You are now in the {rank switch { "frontrank" => "Frontrank", "backrank" => "Backrank", _ => "Midrank" }}.";
    }
    /// <summary>
    /// Native FOLLOW help explicitly says a follower stops following when they exit the
    /// game. This hook is only for an explicit resolved QUIT/LOGOUT command. Socket loss,
    /// linkdead handling, and leader-exit succession remain native UNKNOWNs.
    /// </summary>
    public void HandleExplicitClassicSessionExit(RuntimeSession session)
    {
        var party = _partyGameplay.FindByMember(session.Actor.Id);
        if (party is null || party.LeaderId.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase)) return;
        _partyGameplay.Leave(session.Actor.Id);
    }

}
