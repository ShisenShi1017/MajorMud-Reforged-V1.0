namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderRoom(RuntimeSession session)
    {
        var lines = new List<string>
        {
            session.CurrentRoom.Name,
            session.CurrentRoom.Description
        };

        var npcs = LivingNpcsInRoom(session)
            .Select(npc => npc.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (npcs.Length > 0) lines.Add("NPCs: " + string.Join(", ", npcs));

        var players = _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Where(other => !other.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
            .Select(other => other.Actor.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (players.Length > 0) lines.Add("Players: " + string.Join(", ", players));

        var ground = LoadVisibleGround(session).ToArray();
        if (ground.Length > 0) lines.Add("Items: " + string.Join(", ", ground.Select(entry => entry.Name)));

        var exits = session.CurrentRoom.Exits.Count == 0
            ? "none"
            : string.Join(", ", session.CurrentRoom.Exits.Select(exit => exit.Direction).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value));
        lines.Add("Exits: " + exits);
        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    public string Examine(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Examine what?";
        var value = query.Trim();
        var playerMatches = _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Where(other => other.Actor.Id.Equals(value, StringComparison.OrdinalIgnoreCase)
                || other.Actor.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        var npcMatches = LivingNpcsInRoom(session)
            .Where(npc => npc.InstanceId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || npc.NpcId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || npc.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        var itemMatches = LoadVisibleGround(session)
            .Where(item => item.InstanceId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || item.CanonicalId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        var total = playerMatches.Length + npcMatches.Length + itemMatches.Length;
        if (total == 0) return $"You do not see '{value}' here.";
        if (total > 1) return $"More than one visible target matches '{value}'; use a stable ID.";
        if (playerMatches.Length == 1)
        {
            var actor = playerMatches[0].Actor;
            return $"{actor.DisplayName} [{actor.Kind}] id={actor.Id}";
        }
        if (npcMatches.Length == 1)
        {
            var spawn = npcMatches[0];
            return $"{spawn.Name} [instance={spawn.InstanceId}; npc={spawn.NpcId}; spawn={spawn.SpawnId}; hp={spawn.CurrentHitPoints}/{spawn.MaximumHitPoints}]";
        }
        var item = itemMatches[0];
        return $"{item.Name} [item={item.CanonicalId}; instance={item.InstanceId}]";
    }

    public string Say(RuntimeSession session, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Say what?";
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} says, '{text.Trim()}'");
        return $"You say, '{text.Trim()}'";
    }

    public string Tell(RuntimeSession session, IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2) return "Use: tell <character> <message>.";
        var target = arguments[0];
        var message = string.Join(" ", arguments.Skip(1));
        _sessions.SendTell(session, target, message, out var response);
        return response;
    }

    public string DrainMessages(RuntimeSession session)
    {
        var messages = session.DrainMessages();
        return messages.Count == 0 ? string.Empty : string.Join(Environment.NewLine, messages);
    }

    public void BroadcastDeparture(RuntimeSession session, string roomId, string direction)
    {
        foreach (var target in _sessions.ActiveSessions.Where(target =>
            target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
            && target.CurrentRoom.Id.Equals(roomId, StringComparison.OrdinalIgnoreCase)
            && !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
        {
            target.EnqueueMessage($"{session.Actor.DisplayName} leaves {direction}.");
        }
    }

    public void BroadcastArrival(RuntimeSession session)
        => _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} arrives.");

    private IEnumerable<VisibleGroundItem> LoadVisibleGround(RuntimeSession session)
    {
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var active in _sessions.ActiveSessions.Where(active => active.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (active.Actor is not RuntimeCharacterActor actor) continue;
            var state = _characters.LoadCharacter(actor.Id)?.ItemState;
            if (state is null) continue;
            foreach (var item in state.Items.Where(item =>
                item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                && item.LocationId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
                && seen.Add(item.InstanceId)))
            {
                yield return new VisibleGroundItem(item.InstanceId, item.CanonicalItemId, catalog.Require(item.CanonicalItemId).Name);
            }
        }
    }

    private sealed record VisibleGroundItem(string InstanceId, string CanonicalId, string Name);
}

public sealed class RuntimeLiveGameplayPart3SmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public static RuntimeLiveGameplayPart3SmokeReport Run()
    {
        var report = new RuntimeLiveGameplayPart3SmokeReport();
        try
        {
            Require(RuntimeCommand.Parse("say hello world").ArgumentText == "hello world", "say-route"); report.Checks.Add("say-route");
            Require(RuntimeCommand.Parse("tell Alice hello").Arguments.Count == 2, "tell-route"); report.Checks.Add("tell-route");
            Require(RuntimeCommand.SupportedCommands.Contains("examine"), "examine-route"); report.Checks.Add("examine-route");
            Require(RuntimeCommand.SupportedCommands.Contains("say") && RuntimeCommand.SupportedCommands.Contains("tell"), "communication-catalog"); report.Checks.Add("communication-catalog");
            Require(typeof(RuntimeSession).GetMethod(nameof(RuntimeSession.DrainMessages)) is not null, "message-mailbox"); report.Checks.Add("message-mailbox");
            Require(typeof(RuntimeSessionManager).GetMethod(nameof(RuntimeSessionManager.InRoom)) is not null, "room-presence"); report.Checks.Add("room-presence");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
