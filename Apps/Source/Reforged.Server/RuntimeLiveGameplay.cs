namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeSessionManager _sessions;
    private readonly Dictionary<string, RuntimeGameplayCompletionStateStore> _gameplayStores = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gameplayStoreGate = new();

    public RuntimeLiveGameplayFacade(RuntimeCharacterService characters, RuntimeSessionManager sessions)
    {
        _characters = characters;
        _sessions = sessions;
    }

    private RuntimeGameplayCompletionStateStore GameplayStore(string worldId)
    {
        lock (_gameplayStoreGate)
        {
            if (!_gameplayStores.TryGetValue(worldId, out var store))
            {
                store = new RuntimeGameplayCompletionStateStore(_characters.ProjectRoot, worldId);
                _gameplayStores[worldId] = store;
            }
            return store;
        }
    }

    public string RenderScore(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return $"{session.Actor.DisplayName}\nType: {session.Actor.Kind}\nWorld: {session.World.Definition.Name}\nRoom: {session.CurrentRoom.Name}";
        }

        var character = _characters.LoadCharacter(actor.Id);
        if (character is null)
        {
            return $"{actor.DisplayName}\nRace: {actor.Race}\nClass: {actor.CharacterClass}\nLevel: {actor.Level}\nWorld: {session.World.Definition.Name}\nRoom: {session.CurrentRoom.Name}";
        }

        return string.Join(Environment.NewLine, new[]
        {
            character.DisplayName,
            $"Race: {character.RaceName}",
            $"Class: {character.ClassName}",
            $"Level: {character.Level}",
            $"Experience: {character.Experience}",
            $"HP: {character.HitPoints}/{character.MaximumHitPoints}",
            $"Mana: {character.Mana}/{character.MaximumMana}",
            $"World: {session.World.Definition.Name}",
            $"Room: {session.CurrentRoom.Name} [{session.CurrentRoom.Id}]"
        });
    }

    public string RenderInventory(RuntimeSession session)
    {
        var state = LoadItemState(session);
        if (state is null)
        {
            return "You are carrying nothing.";
        }

        var inventory = state.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.LocationKind, RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                && item.EquippedSlot is null)
            .OrderBy(item => item.CanonicalItemId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (inventory.Length == 0)
        {
            return $"You are carrying nothing.\nGold: {state.Gold}";
        }

        return "Inventory:" + Environment.NewLine
            + string.Join(Environment.NewLine, inventory.Select(RenderItem))
            + Environment.NewLine + $"Gold: {state.Gold}";
    }

    public string RenderEquipment(RuntimeSession session)
    {
        var state = LoadItemState(session);
        if (state is null)
        {
            return "You have nothing equipped.";
        }

        var equipped = state.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                && item.EquippedSlot is not null)
            .OrderBy(item => item.EquippedSlot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return equipped.Length == 0
            ? "You have nothing equipped."
            : "Equipment:" + Environment.NewLine + string.Join(Environment.NewLine,
                equipped.Select(item => $"{item.EquippedSlot}: {item.CanonicalItemId} [{item.InstanceId}]"));
    }

    public string Save(RuntimeSession session)
    {
        if (!session.Actor.IsPersistentCharacter)
        {
            return "This session does not have a persistent character to save.";
        }

        _characters.SaveSession(session);
        return $"{session.Actor.DisplayName} saved at {session.CurrentRoom.Name}.";
    }

    public string RenderWho() => _sessions.RenderStatus();

    public string RenderIntegrationStatus(RuntimeSession session)
    {
        var persistent = session.Actor.IsPersistentCharacter ? "persistent" : "transient";
        var itemState = LoadItemState(session) is null ? "unavailable" : "attached";
        return string.Join(Environment.NewLine, new[]
        {
            $"Session: {session.Id}",
            $"Actor: {session.Actor.DisplayName} ({persistent})",
            $"World: {session.World.Definition.Id}",
            $"Room: {session.CurrentRoom.Id}",
            $"Item state: {itemState}",
            "Live domains: room, movement, presence, communication, inventory, equipment, banking, save/reconnect",
            "Live combat domains: generated NPC targeting, encounter admission, NPC attack-profile loading, status, flee, movement guard",
            "Policy-gated domains: player AV/DV derivation, combat damage rounds, spells, abilities, quests, shop/trainer transactions, death/respawn"
        });
    }

    private RuntimeItemState? LoadItemState(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return null;
        }

        return _characters.LoadCharacter(actor.Id)?.ItemState;
    }

    private static string RenderItem(RuntimeItemInstance item)
    {
        var quantity = item.StackQuantity > 1 ? $" x{item.StackQuantity}" : string.Empty;
        var charges = item.Charges > 0 ? $" charges={item.Charges}" : string.Empty;
        return $"- {item.CanonicalItemId}{quantity}{charges} [{item.InstanceId}]";
    }
}

public sealed class RuntimeLiveGameplaySmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveGameplaySmokeReport Run()
    {
        var report = new RuntimeLiveGameplaySmokeReport();
        try
        {
            var look = RuntimeCommand.Parse("look");
            Require(look.Name == "look" && look.Arguments.Count == 0, "parse-look");
            report.Checks.Add("parse-look");

            var examine = RuntimeCommand.Parse("examine ancient sword");
            Require(examine.Name == "examine" && examine.ArgumentText == "ancient sword", "parse-arguments");
            report.Checks.Add("parse-arguments");

            var north = RuntimeCommand.Parse("north");
            Require(north.IsMovement, "movement-alias");
            report.Checks.Add("movement-alias");

            var inventory = RuntimeCommand.Parse("i");
            Require(inventory.Name == "inventory", "command-alias");
            report.Checks.Add("command-alias");

            var save = RuntimeCommand.Parse("save");
            Require(save.Name == "save", "save-command");
            report.Checks.Add("save-command");

            Require(RuntimeCommand.SupportedCommands.Contains("score"), "supported-command-catalog");
            report.Checks.Add("supported-command-catalog");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }
        return report;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
