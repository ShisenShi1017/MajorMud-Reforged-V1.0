namespace Reforged.Server;

public interface IRuntimeLiveGameplayPolicyGateway
{
    string Execute(RuntimeSession session, RuntimeCommand command);
}

public sealed class RuntimeBlockedLiveGameplayPolicyGateway : IRuntimeLiveGameplayPolicyGateway
{
    public string Execute(RuntimeSession session, RuntimeCommand command) =>
        $"'{command.Name}' is routed to the live gameplay policy gateway, but its generated policy is not attached.";
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly IRuntimeLiveGameplayPolicyGateway _policyGateway = new RuntimeBlockedLiveGameplayPolicyGateway();

    public string ExecuteItemCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
        string result;
        switch (command.Name)
        {
            case "get":
            {
                var item = Resolve(engine.Ground(session.CurrentRoom.Id), catalog, command.ArgumentText);
                var moved = engine.Get(item.InstanceId, actor.Id);
                result = $"You get {catalog.Require(moved.CanonicalItemId).Name}.";
                break;
            }
            case "drop":
            {
                var item = Resolve(engine.Inventory(actor.Id), catalog, command.ArgumentText);
                var moved = engine.Drop(item.InstanceId, actor.Id, session.CurrentRoom.Id);
                result = $"You drop {catalog.Require(moved.CanonicalItemId).Name}.";
                break;
            }
            case "wear":
            case "wield":
            {
                var item = Resolve(engine.Inventory(actor.Id), catalog, command.ArgumentText);
                var moved = engine.Equip(item.InstanceId, actor.Id, character);
                result = $"You equip {catalog.Require(moved.CanonicalItemId).Name} in {moved.EquippedSlot}.";
                break;
            }
            case "remove":
            {
                var item = Resolve(engine.Equipment(actor.Id), catalog, command.ArgumentText);
                var moved = engine.Unequip(item.InstanceId, actor.Id);
                result = $"You remove {catalog.Require(moved.CanonicalItemId).Name}.";
                break;
            }
            case "put":
            {
                var parts = SplitAround(command.Arguments, "in", "into");
                var item = Resolve(engine.Inventory(actor.Id), catalog, parts.Left);
                var container = Resolve(engine.Inventory(actor.Id), catalog, parts.Right);
                engine.Put(item.InstanceId, actor.Id, container.InstanceId);
                result = $"You put {catalog.Require(item.CanonicalItemId).Name} into {catalog.Require(container.CanonicalItemId).Name}.";
                break;
            }
            case "give":
                return GiveToConnectedCharacter(session, actor, character, state, catalog, command);
            default:
                throw new InvalidOperationException("Unsupported item command.");
        }
        var snapshot = engine.Snapshot(state.Gold, state.ActiveItemState);
        _characters.SaveCharacter(character with { ItemState = snapshot, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return result;
    }

    public string ExecutePolicyCommand(RuntimeSession session, RuntimeCommand command)
    {
        try
        {
            return command.Name is "attack" or "kill" or "flee" or "combat" or "round"
                ? ExecuteLiveCombatCommand(session, command)
                : command.Name is "cast" or "use" or "abilities" or "skills" or "prepare" or "dispel"
                    ? ExecuteLivePowerCommand(session, command)
                    : command.Name is "bank" or "deposit" or "withdraw"
                    ? ExecuteBankCommand(session, command)
                    : command.Name is "services" or "list" or "trade" or "train" or "buy" or "sell" or "repair"
                        ? ExecuteLiveServicesBundleCommand(session, command)
                        : command.Name is "quest" or "accept" or "abandon" or "complete" or "journal" or "talk"
                            ? ExecuteWorldNarrativeCommand(session, command)
                        : command.Name == "respawn"
                            ? RespawnCharacter(session)
                            : command.Name == "recover"
                                ? RecoverCorpse(session)
                                : _policyGateway.Execute(session, command);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private string GiveToConnectedCharacter(
        RuntimeSession sourceSession,
        RuntimeCharacterActor sourceActor,
        RuntimeCharacterRecord sourceCharacter,
        RuntimeItemState sourceState,
        RuntimeItemCatalog catalog,
        RuntimeCommand command)
    {
        var parts = SplitAround(command.Arguments, "to");
        var targetSession = _sessions.FindUniqueInRoom(sourceSession, parts.Right)
            ?? throw new InvalidOperationException($"No connected character here uniquely matches '{parts.Right}'.");
        if (targetSession.Actor is not RuntimeCharacterActor targetActor)
            throw new InvalidOperationException("The target must be a persistent character.");
        if (sourceActor.Id.Equals(targetActor.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("You cannot give an item to yourself.");

        var itemName = _characters.ExecuteCharacterTransaction(sourceActor.Id, targetActor.Id, (freshSource, freshTarget) =>
        {
            var freshSourceState = freshSource.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
            var freshTargetState = freshTarget.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
            var sourceEngine = new RuntimeItemEngine(catalog, freshSourceState.Items, freshSourceState.Corpses);
            var selected = Resolve(sourceEngine.Inventory(sourceActor.Id), catalog, parts.Left);
            var removed = sourceEngine.Remove(selected.InstanceId);
            var targetEngine = new RuntimeItemEngine(catalog, freshTargetState.Items, freshTargetState.Corpses);
            var transferred = removed with
            {
                OwnerId = targetActor.Id,
                LocationKind = RuntimeItemLocations.Character,
                LocationId = targetActor.Id,
                EquippedSlot = null
            };
            targetEngine.Add(transferred);
            var sourceUpdated = freshSource with { ItemState = sourceEngine.Snapshot(freshSourceState.Gold, freshSourceState.ActiveItemState) };
            var targetUpdated = freshTarget with { ItemState = targetEngine.Snapshot(freshTargetState.Gold, freshTargetState.ActiveItemState) };
            return (sourceUpdated, targetUpdated, catalog.Require(transferred.CanonicalItemId).Name);
        });

        targetSession.EnqueueMessage($"{sourceSession.Actor.DisplayName} gives you {itemName}.");
        return $"You give {itemName} to {targetSession.Actor.DisplayName}.";
    }

    private static RuntimeItemInstance Resolve(IEnumerable<RuntimeItemInstance> source, RuntimeItemCatalog catalog, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new InvalidOperationException("An item name or instance ID is required.");
        var q = query.Trim();
        var candidates = source.Where(item => item.InstanceId.Equals(q, StringComparison.OrdinalIgnoreCase)
            || item.CanonicalItemId.Equals(q, StringComparison.OrdinalIgnoreCase)
            || catalog.Require(item.CanonicalItemId).Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();
        return candidates.Length switch
        {
            0 => throw new InvalidOperationException($"No matching item: {q}."),
            1 => candidates[0],
            _ => throw new InvalidOperationException($"Multiple items match '{q}'; use an instance ID.")
        };
    }

    private static (string Left, string Right) SplitAround(IReadOnlyList<string> args, params string[] separators)
    {
        var index = -1;
        for (var i = 0; i < args.Count; i++) if (separators.Contains(args[i], StringComparer.OrdinalIgnoreCase)) { index = i; break; }
        if (index <= 0 || index >= args.Count - 1) throw new InvalidOperationException("Use: put <item> in <container>.");
        return (string.Join(" ", args.Take(index)), string.Join(" ", args.Skip(index + 1)));
    }
}

public sealed class RuntimeLiveGameplayPart2SmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public static RuntimeLiveGameplayPart2SmokeReport Run()
    {
        var report = new RuntimeLiveGameplayPart2SmokeReport();
        try
        {
            Require(RuntimeCommand.Parse("get sword").Name == "get", "get-route"); report.Checks.Add("get-route");
            Require(RuntimeCommand.Parse("wear sword").Name == "wear", "wear-route"); report.Checks.Add("wear-route");
            Require(RuntimeCommand.Parse("cast fireball goblin").ArgumentText == "fireball goblin", "policy-arguments"); report.Checks.Add("policy-arguments");
            Require(RuntimeCommand.SupportedCommands.Contains("train"), "service-route"); report.Checks.Add("service-route");
            Require(RuntimeCommand.SupportedCommands.Contains("quest"), "quest-route"); report.Checks.Add("quest-route");
            Require(RuntimeCommand.SupportedCommands.Contains("attack"), "combat-route"); report.Checks.Add("combat-route");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
