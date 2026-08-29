namespace Reforged.Server;

public sealed class RuntimeClassicGangGameplayService
{
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeSessionManager _sessions;
    private readonly RuntimeClassicGangStateService _gangs;

    public RuntimeClassicGangGameplayService(RuntimeCharacterService characters, RuntimeSessionManager sessions)
    {
        _characters = characters;
        _sessions = sessions;
        _gangs = new RuntimeClassicGangStateService(characters.ProjectRoot);
    }

    public int ClearClassicDepartureInvitations(string characterId) =>
        _gangs.ClearPendingInvitationsForCharacter(characterId);

    public string Execute(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) throw new InvalidOperationException("A persistent character is required.");
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        return command.Name switch
        {
            "create" => Create(character, command.Arguments),
            "invite" => Invite(character, command.Arguments, false),
            "uninvite" => Invite(character, command.Arguments, true),
            "join" => Join(character, command.Arguments),
            "leave" => Leave(character, command.Arguments),
            "broadgang" => Broadgang(session, character, command.ArgumentText),
            "disband" => Disband(session, character, command.Arguments),
            "promote" => SetRole(character, command.ArgumentText, "Lieutenant"),
            "demote" => SetRole(character, command.ArgumentText, "Member"),
            "topten" => RenderTop(character, command.Arguments),
            _ => "Unsupported gang command."
        };
    }

    private string Create(RuntimeCharacterRecord character, IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !args[0].Equals("gang", StringComparison.OrdinalIgnoreCase)) return "Usage: CREATE GANG <gang name>";
        var gang = _gangs.Create(string.Join(' ', args.Skip(1)), character);
        return $"You create the gang '{gang.Name}'.";
    }

    private string Invite(RuntimeCharacterRecord actor, IReadOnlyList<string> args, bool remove)
    {
        if (args.Count < 2 || !args[0].Equals("member", StringComparison.OrdinalIgnoreCase)) return $"Usage: {(remove ? "UNINVITE" : "INVITE")} MEMBER <user name>";
        var query = string.Join(' ', args.Skip(1));
        var targets = _characters.ListAllCharacters().Where(character => character.CharacterId.Equals(query, StringComparison.OrdinalIgnoreCase) || character.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (targets.Length != 1) return "No saved character uniquely matches that exact name or character ID.";
        var target = targets[0];
        var gang = _gangs.FindByMember(actor.CharacterId) ?? throw new InvalidOperationException("You do not belong to a gang.");
        var leaderOnline = _sessions.FindByActor(gang.LeaderCharacterId) is not null;
        var targetSession = _sessions.FindByActor(target.CharacterId);

        if (remove)
        {
            var updated = _gangs.Uninvite(gang, actor.CharacterId, target.CharacterId, leaderOnline, targetSession is not null);
            targetSession?.EnqueueMessage($"You have been removed from {updated.Name}.");
            return $"{target.DisplayName} was removed from {updated.Name}.";
        }

        var added = _gangs.Invite(gang, actor.CharacterId, target.CharacterId, leaderOnline);
        if (added)
            targetSession?.EnqueueMessage($"You have been invited to join {gang.Name}. Type JOIN GANG {gang.Name}.");
        // Native INVITE's caller-side confirmation path still runs when _invite_to_gang rejects
        // an exact duplicate node; only the target notification is suppressed.
        return $"{target.DisplayName} was invited to {gang.Name}.";
    }

    private string Join(RuntimeCharacterRecord character, IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !args[0].Equals("gang", StringComparison.OrdinalIgnoreCase)) return "Usage: JOIN GANG <gang name>";
        var name = string.Join(' ', args.Skip(1));
        var gang = _gangs.FindExact(name) ?? throw new InvalidOperationException("No gang exactly matches that name.");
        var updated = _gangs.Join(gang, character);
        return $"You join {updated.Name}.";
    }

    private string Leave(RuntimeCharacterRecord character, IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !args[0].Equals("gang", StringComparison.OrdinalIgnoreCase)) return "Usage: LEAVE GANG";
        var gang = _gangs.FindByMember(character.CharacterId) ?? throw new InvalidOperationException("You do not belong to a gang.");
        _gangs.Leave(gang, character.CharacterId);
        return $"You leave {gang.Name}.";
    }

    private string Broadgang(RuntimeSession session, RuntimeCharacterRecord character, string text)
    {
        var gang = _gangs.FindByMember(character.CharacterId) ?? throw new InvalidOperationException("You do not belong to a gang.");
        if (string.IsNullOrWhiteSpace(text))
            return string.Join(Environment.NewLine, gang.Members.Select(member => $"{member.DisplayName}; role={member.Role}; online={_sessions.FindByActor(member.CharacterId) is not null}"));
        foreach (var member in gang.Members)
            _sessions.FindByActor(member.CharacterId)?.EnqueueMessage($"[Gang] {session.Actor.DisplayName}: {text.Trim()}");
        return $"You tell {gang.Name}: {text.Trim()}";
    }

    private string Disband(RuntimeSession session, RuntimeCharacterRecord character, IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !args[0].Equals("gang", StringComparison.OrdinalIgnoreCase)) return "Syntax: DISBAND {Party/Gang}";
        var gang = _gangs.FindByMember(character.CharacterId) ?? throw new InvalidOperationException("You are not in a gang!");
        if (!gang.LeaderCharacterId.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("You are not the leader of the gang; You may not disband it!");

        session.BeginClassicGangDisbandConfirmation(gang.Name);
        return $"Are you sure you want to disband {gang.Name}? ";
    }

    public bool TryHandleClassicModalInput(RuntimeSession session, string input, out string result)
    {
        result = string.Empty;
        if (!session.TryGetClassicGangDisbandConfirmation(out var pendingGangName)) return false;

        try
        {
            if (!RuntimeClassicGangModalAuthority.ConfirmsDisband(input))
            {
                result = "Your gang has not been disbanded.";
                return true;
            }

            if (session.Actor is not RuntimeCharacterActor actor)
            {
                result = string.Empty;
                return true;
            }

            var character = _characters.LoadCharacter(actor.Id);
            var gang = character is null ? null : _gangs.FindByMember(character.CharacterId);
            if (gang is null || !gang.Name.Equals(pendingGangName, StringComparison.OrdinalIgnoreCase))
            {
                // Native 0x004083DC silently completes the modal if current user/gang lookup
                // fails between prompt and reply.
                result = string.Empty;
                return true;
            }

            var first = $"The gang {gang.Name} has now been disbanded.";
            var second = "The name may not be used again until all members have entered the game!";
            foreach (var member in gang.Members)
            {
                var target = _sessions.FindByActor(member.CharacterId);
                if (target is null || target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)) continue;
                target.EnqueueMessage(first + Environment.NewLine + second);
            }

            var onlineIds = _sessions.ActiveSessions
                .Where(value => value.Actor.IsPersistentCharacter)
                .Select(value => value.Actor.Id)
                .ToArray();
            _gangs.Disband(gang, onlineIds);
            // 0341 regression owner: offline former member(s) remain until character load/cleanup.
            result = first + Environment.NewLine + second;
            return true;
        }
        finally
        {
            // Every state-0x88 path restores active gameplay state 0x82.
            session.CompleteClassicModalInput();
        }
    }

    private string SetRole(RuntimeCharacterRecord leader, string query, string role)
    {
        query = query.Trim();
        if (query.Length == 0) return $"Usage: {(role == "Lieutenant" ? "PROMOTE" : "DEMOTE")} <player name>";
        var targets = _characters.ListAllCharacters().Where(character => character.CharacterId.Equals(query, StringComparison.OrdinalIgnoreCase) || character.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (targets.Length != 1) return "No saved character uniquely matches that exact name or character ID.";
        var gang = _gangs.FindByMember(leader.CharacterId) ?? throw new InvalidOperationException("You do not belong to a gang.");
        var updated = _gangs.SetMemberRole(gang, leader.CharacterId, targets[0].CharacterId, role);
        _sessions.FindByActor(targets[0].CharacterId)?.EnqueueMessage($"Your role in {updated.Name} is now {role}.");
        return $"{targets[0].DisplayName} is now a {role} in {updated.Name}.";
    }

    private string RenderTop(RuntimeCharacterRecord actor, IReadOnlyList<string> args)
    {
        // Native help proves TOPTEN adventurers and TOPTEN/TOP <n> GANGS. Operator-confirmed
        // play proves class-filtered forms such as TOP 10 WITCHHUNTER. Retained WCC setup also
        // has an "Include experience in topten command?" switch. Experience-descending is
        // therefore active as STRONG behavior; exact tie order/output remain unresolved.
        int count;
        if (args.Count == 1 && args[0].Equals("gangs", StringComparison.OrdinalIgnoreCase)) count = 10;
        else if (args.Count == 2 && int.TryParse(args[0], out count) && args[1].Equals("gangs", StringComparison.OrdinalIgnoreCase)) { }
        else
        {
            count = 10;
            string? classFilter = null;
            int parsedCount;
            if (args.Count == 0) { }
            else if (args.Count == 1 && int.TryParse(args[0], out parsedCount)) count = parsedCount;
            else if (args.Count == 1) classFilter = args[0];
            else if (args.Count == 2 && int.TryParse(args[0], out parsedCount))
            {
                count = parsedCount;
                classFilter = args[1];
            }
            else return "Native TOP syntax is recognized, but this form is not yet recovered.";

            if (count < 1) return "TOP count must be at least 1.";
            var characters = _characters.ListAllCharacters()
                .Where(character => character.WorldId.Equals(actor.WorldId, StringComparison.OrdinalIgnoreCase))
                .Where(character => !RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.ToptenDisabled));
            if (!string.IsNullOrWhiteSpace(classFilter))
                characters = characters.Where(character =>
                    character.ClassName.Equals(classFilter, StringComparison.OrdinalIgnoreCase)
                    || character.ClassId.Equals(classFilter, StringComparison.OrdinalIgnoreCase));
            var ranked = characters
                .OrderByDescending(character => character.Experience)
                .ThenBy(character => character.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Clamp(count, 1, 100))
                .ToArray();
            if (ranked.Length == 0) return "No TOPTEN-eligible adventurers match that list.";
            return string.Join(Environment.NewLine, ranked.Select((character, index) =>
                $"{index + 1}. {character.DisplayName}; class={character.ClassName}; experience={character.Experience}"));
        }

        if (count < 1) return "TOP count must be at least 1.";
        var gangs = _gangs.Top(count);
        if (gangs.Count == 0) return "No TOPTEN-eligible gangs exist.";
        return string.Join(Environment.NewLine, gangs.Select((gang, index) => $"{index + 1}. {gang.Name}; experience={gang.Experience}; members={gang.Members.Count}"));
    }
}
