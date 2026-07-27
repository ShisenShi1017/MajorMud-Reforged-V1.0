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


    public string EquipFromEquipmentCommand(RuntimeSession session, string target)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (string.IsNullOrWhiteSpace(target)) return RenderEquipment(session);

        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var characterAuthority = RuntimeCharacterBundleCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses, characterAuthority);
        var item = ResolveClassicEquipmentCandidate(engine.Inventory(actor.Id), catalog, target);
        var definition = catalog.Require(item.CanonicalItemId);
        var forcedSlot = catalog.ResolveClassicEquipmentSlot(item.CanonicalItemId);
        var moved = engine.Equip(item.InstanceId, actor.Id, character, forcedSlot);
        var nowUtc = DateTimeOffset.UtcNow;
        var commit = RuntimeEquipmentRuntimeAuthority.Commit(character, engine, state.Gold, state.ActiveItemState, nowUtc);
        _characters.SaveCharacter(commit.Character);
        return $"You equip {catalog.Require(moved.CanonicalItemId).Name} ({ClassicEquipmentSlot(catalog, moved)}).";
    }


    private static string? ClassicForcedEquipmentSlot(string itemName)
    {
        var name = itemName.Trim().ToLowerInvariant();
        if (name.Contains("padded vest")) return "TORSO";
        if (name.Contains("padded pants")) return "LEGS";
        if (name.Contains("padded helm")) return "HEAD";
        if (name.Contains("padded gloves")) return "HANDS";
        if (name.Contains("padded boots")) return "FEET";
        if (name == "club") return "WIELD";
        if (name.Contains("quarterstaff")) return "WIELD";
        return null;
    }


    private static bool IsClassicWeaponSlot(string? slot) => (slot ?? string.Empty).ToUpperInvariant() is
        "MAIN_HAND" or "PRIMARY_HAND" or "WEAPON" or "HAND" or "WIELD" or "OFF_HAND" or "SECONDARY_HAND";

    private static string ClassicEquipmentSlot(RuntimeItemCatalog catalog, RuntimeItemInstance item)
    {
        var relation = catalog.Equipment(item.CanonicalItemId);
        if (relation?.TwoHanded == true) return "Two handed";
        return (item.EquippedSlot ?? relation?.Slot ?? "equipped").ToUpperInvariant() switch
        {
            "MAIN_HAND" or "PRIMARY_HAND" or "WEAPON" or "HAND" or "WIELD" => "One handed",
            "OFF_HAND" or "SECONDARY_HAND" => "Off hand",
            "ARMS" => "Arms",
            "TORSO" or "BODY" or "CHEST" => "Torso",
            "BACK" => "Back",
            "WRIST" or "WRISTS" => "Wrist",
            "FINGER" or "RING" => "Finger",
            "WAIST" or "BELT" => "Waist",
            "FEET" or "BOOTS" => "Feet",
            "HANDS" or "GLOVES" => "Hands",
            "HEAD" or "HELM" => "Head",
            "LEGS" => "Legs",
            "NECK" => "Neck",
            "SHIELD" => "Shield",
            var slot => slot.Replace('_', ' ').ToLowerInvariant()
        };
    }

    public string ExecuteItemCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (_liveCombats.ContainsKey(session.Id)) return "You cannot hide or sneak while in combat.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        character = RuntimeClassicRoomCurrencyStore.MigrateLegacyCurrency(_characters.ProjectRoot, character);
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());

        // Native room money is handled before the generated item catalog. Coins are
        // not items and must never be resolved through RuntimeItemCatalog.
        if (command.Name.Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            var money = RuntimeClassicRoomCurrencyStore.Take(
                _characters.ProjectRoot,
                character.WorldId,
                session.CurrentRoom.Id,
                command.ArgumentText);
            if (money.Matched)
            {
                var updatedState = RuntimeBundleCInventoryAuthority.Credit(state, money.Balance);
                _characters.SaveCharacter(character with { ItemState = updatedState, UpdatedAtUtc = DateTimeOffset.UtcNow });
                return $"You pick up {money.DisplayText}.";
            }
        }

        state = state with { Items = state.Items.Where(item => !RuntimeClassicRoomCurrencyStore.IsLegacyCurrency(item)).ToArray() };
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var characterAuthority = RuntimeCharacterBundleCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses, characterAuthority);
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
            case "equip":
            case "wear":
            case "wield":
            {
                var item = ResolveClassicEquipmentCandidate(engine.Inventory(actor.Id), catalog, command.ArgumentText);
                var forcedSlot = catalog.ResolveClassicEquipmentSlot(item.CanonicalItemId);
                var relationship = catalog.Equipment(item.CanonicalItemId)
                    ?? throw new InvalidOperationException("Item has no generated equipment relationship.");
                if (command.Name.Equals("wield", StringComparison.OrdinalIgnoreCase) && !IsClassicWeaponSlot(forcedSlot ?? relationship.Slot))
                    throw new InvalidOperationException($"{catalog.Require(item.CanonicalItemId).Name} is worn, not wielded.");
                if (command.Name.Equals("wear", StringComparison.OrdinalIgnoreCase) && IsClassicWeaponSlot(forcedSlot ?? relationship.Slot))
                    throw new InvalidOperationException($"{catalog.Require(item.CanonicalItemId).Name} must be wielded.");
                var moved = engine.Equip(item.InstanceId, actor.Id, character, forcedSlot);
                result = $"You equip {catalog.Require(moved.CanonicalItemId).Name} ({ClassicEquipmentSlot(catalog, moved)}).";
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
        var nowUtc = DateTimeOffset.UtcNow;
        var commit = RuntimeEquipmentRuntimeAuthority.Commit(character, engine, state.Gold, state.ActiveItemState, nowUtc);
        _characters.SaveCharacter(commit.Character);
        return result;
    }

    public string ExecutePolicyCommand(RuntimeSession session, RuntimeCommand command)
    {
        try
        {
            return command.Name is "hide" or "sneak"
                ? ExecuteStealthCommand(session, command)
                : command.Name is "attack" or "kill" or "bash" or "smash" or "backstab" or "flee" or "combat" or "round"
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



    public string ExecuteStealthCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var values = RuntimeCharacterAbilityValues.Restore(character.Conditions);
        var opponents = LivingNpcsInRoom(session)
            .Where(npc => npc.Alive && ResolveLiveNpcDisposition(npc).Equals("hostile", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var seeHiddenOpponent = opponents.FirstOrDefault(RuntimeClassicStealthResolver.HasSeeHidden);
        if (seeHiddenOpponent is not null)
        {
            RuntimeClassicStealthResolver.TryReadModifiedStealth(character, out var attemptedStealth);
            var detected = RuntimeClassicStealthResolver.ResolveSeeHidden(attemptedStealth);
            var detectedVisibleCharacter = ClearStealthState(character, detected);
            _characters.SaveCharacter(detectedVisibleCharacter);

            // Ability 57 explicitly keeps the detecting monster aware of the same player
            // after an escape and re-sneak. Persist that target relationship on the live
            // NPC instead of treating See Hidden as a one-command-only check.
            var pursuitMetadata = ApplySeeHiddenPursuit(seeHiddenOpponent.Metadata, actor.Id);
            if (!Living(session).ReplaceNpcMetadata(seeHiddenOpponent.InstanceId, pursuitMetadata))
                throw new InvalidOperationException("SEE_HIDDEN_PURSUIT_NPC_NOT_FOUND");

            var detectionEngagement = BeginLiveCombat(session, seeHiddenOpponent.InstanceId, ClassicAttackType.NormalWeapon)
                .Replace("You attack", seeHiddenOpponent.Name + " attacks", StringComparison.OrdinalIgnoreCase);
            return "Your concealment fails; " + seeHiddenOpponent.Name + " sees hidden characters and continues tracking you." + Environment.NewLine + detectionEngagement;
        }

        if (RuntimeCharacterAbilityValues.Read(values, RuntimeCharacterQuestAbilityCatalog.PerfectStealthAbilityId) > 0)
        {
            var perfect = ApplyPerfectStealth(character, command.Name);
            _characters.SaveCharacter(perfect);
            return command.Name.Equals("hide", StringComparison.OrdinalIgnoreCase)
                ? "You vanish into perfect concealment."
                : "You begin moving with perfect stealth.";
        }

        if (!RuntimeClassicStealthResolver.TryReadModifiedStealth(character, out var modifiedStealth))
            return $"{command.Name} is blocked until the decoded modified Stealth Rating is attached.";
        RuntimeClassicStealthResolver.TryReadRequiredClearance(character, out var requiredClearance);

        // WCC 1.1 explicitly states that every Hide/Sneak command is a new percentage roll.
        // Each hostile observer receives its own modified-Stealth versus modified-Perception
        // check, which also makes crowded rooms harder without inventing a population scalar.
        var observers = opponents.Length == 0
            ? new[] { (Npc: (RuntimeLivingNpcState?)null, Perception: 0) }
            : opponents.Select(npc =>
            {
                RuntimeClassicStealthResolver.TryReadModifiedPerception(npc, out var perception);
                return (Npc: (RuntimeLivingNpcState?)npc, Perception: perception);
            }).ToArray();

        RuntimeClassicStealthCheck? failedCheck = null;
        RuntimeLivingNpcState? detectingNpc = null;
        RuntimeClassicStealthCheck? successfulCheck = null;
        foreach (var observer in observers)
        {
            var check = RuntimeClassicStealthResolver.ResolvePercentile(
                modifiedStealth,
                observer.Perception,
                Random.Shared.Next(1, 101),
                requiredClearance);
            if (!check.Succeeded)
            {
                failedCheck = check;
                detectingNpc = observer.Npc;
                break;
            }
            successfulCheck = check;
        }

        if (failedCheck is null)
        {
            var updated = ApplyOrdinaryStealth(character, command.Name, successfulCheck!);
            _characters.SaveCharacter(updated);
            return command.Name.Equals("hide", StringComparison.OrdinalIgnoreCase)
                ? "You successfully conceal yourself."
                : "You begin moving unseen.";
        }

        var failedVisibleCharacter = ClearStealthState(character, failedCheck);
        _characters.SaveCharacter(failedVisibleCharacter);
        var aggressive = detectingNpc ?? opponents.FirstOrDefault();
        if (aggressive is null) return "You fail to conceal yourself and remain fully visible.";
        var failureEngagement = BeginLiveCombat(session, aggressive.InstanceId, ClassicAttackType.NormalWeapon)
            .Replace("You attack", aggressive.Name + " attacks", StringComparison.OrdinalIgnoreCase);
        return "You fail to conceal yourself and remain fully visible." + Environment.NewLine + failureEngagement;
    }


    internal static IReadOnlyDictionary<string, string> ApplySeeHiddenPursuit(
        IReadOnlyDictionary<string, string> existingMetadata,
        string characterId)
    {
        ArgumentNullException.ThrowIfNull(existingMetadata);
        if (string.IsNullOrWhiteSpace(characterId)) throw new ArgumentException("A character ID is required.", nameof(characterId));
        var metadata = new Dictionary<string, string>(existingMetadata, StringComparer.OrdinalIgnoreCase)
        {
            ["current_target_id"] = characterId,
            ["see_hidden_target_id"] = characterId,
            ["see_hidden_pursuit"] = "1",
            ["see_hidden_pursuit_authority"] = RuntimeClassicStealthResolver.SeeHiddenFormulaId
        };
        return metadata;
    }

    public string ExecutePerfectStealthCommand(RuntimeSession session, RuntimeCommand command)
        => ExecuteStealthCommand(session, command);

    internal static RuntimeCharacterRecord ApplyOrdinaryStealth(RuntimeCharacterRecord character, string commandName, RuntimeClassicStealthCheck check)
    {
        var updated = ApplyPerfectStealth(character, commandName);
        var conditions = new Dictionary<string, string>(updated.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            ["stealth.source"] = RuntimeClassicStealthResolver.FormulaId,
            ["stealth.modified_rating"] = check.ModifiedStealthRating.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stealth.opponent_perception"] = check.ModifiedPerceptionValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stealth.required_clearance"] = check.RequiredClearance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stealth.success_chance"] = check.SuccessChance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stealth.roll"] = check.Roll.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stealth.last_result"] = "success"
        };
        return updated with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };
    }

    internal static RuntimeCharacterRecord ClearStealthState(RuntimeCharacterRecord character, RuntimeClassicStealthCheck check)
    {
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "combat.hidden", "state.hidden", "hidden", "combat.sneaking", "state.sneaking", "sneaking" }) conditions[key] = "0";
        conditions["stealth.source"] = RuntimeClassicStealthResolver.FormulaId;
        conditions["stealth.modified_rating"] = check.ModifiedStealthRating.ToString(System.Globalization.CultureInfo.InvariantCulture);
        conditions["stealth.opponent_perception"] = check.ModifiedPerceptionValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        conditions["stealth.required_clearance"] = check.RequiredClearance.ToString(System.Globalization.CultureInfo.InvariantCulture);
        conditions["stealth.last_result"] = "failure-visible";
        return character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };
    }

    internal static RuntimeCharacterRecord ApplyPerfectStealth(RuntimeCharacterRecord character, string commandName)
    {
        ArgumentNullException.ThrowIfNull(character);
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        if (commandName.Equals("hide", StringComparison.OrdinalIgnoreCase))
        {
            conditions["combat.hidden"] = "1";
            conditions["state.hidden"] = "1";
            conditions["hidden"] = "1";
        }
        else if (commandName.Equals("sneak", StringComparison.OrdinalIgnoreCase))
        {
            conditions["combat.sneaking"] = "1";
            conditions["state.sneaking"] = "1";
            conditions["sneaking"] = "1";
        }
        else throw new InvalidOperationException($"Unsupported stealth command '{commandName}'.");
        conditions["stealth.source"] = $"ability.{RuntimeCharacterQuestAbilityCatalog.PerfectStealthAbilityId}";
        return character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };
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
        var candidates = source.Where(item =>
        {
            return item.InstanceId.Equals(q, StringComparison.OrdinalIgnoreCase)
                || catalog.Matches(item.CanonicalItemId, q);
        }).ToArray();
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
