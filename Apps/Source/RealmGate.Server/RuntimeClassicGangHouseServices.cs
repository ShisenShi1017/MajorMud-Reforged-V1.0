namespace RealmGate.Server;

/// <summary>
/// Executable owner for the retained gang-house action-script families in textblocks 1602-1642:
/// healer/guard summons, healer purchases, emblem/key creation, and rubbish-room disposal.
/// The action graph remains authoritative for per-house item/NPC ids, prices, delays, and messages.
/// </summary>
public sealed partial class RuntimeLiveGameplayFacade
{
    private static bool IsRecoveredGangHouseServicePlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        if (plan.RootTextblockId is < 1602 or > 1642 || plan.RootTextblockId == 1607) return false;
        // CAST healer purchase lines retain their specialized spell executor so status/item rollback
        // stays shared with the spell engine; every other gang-house line is owned here.
        if (plan.Operations.Any(operation => operation.Opcode.Equals("cast", StringComparison.OrdinalIgnoreCase))) return false;
        return plan.Operations.Count > 0 && plan.Operations.All(operation => operation.Opcode is
            "checkitem" or "monsters" or "nomonsters" or "price" or "summon" or
            "giveitem" or "takeitem" or "message" or "clearitem" or "adddelay");
    }

    private string ExecuteRecoveredGangHousePlans(
        RuntimeSession session,
        IReadOnlyList<RuntimeClassicTextblockExecutionPlan> plans)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent character is required for a gang-house service.");
        if (plans.Count == 0 || plans.Any(plan => !IsRecoveredGangHouseServicePlan(plan)))
            throw new InvalidOperationException("Gang-house service plan is outside the recovered executable family.");

        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found for the gang-house service.");
        var beforeState = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0,
            new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());

        foreach (var plan in plans.OrderBy(plan => plan.MatchedLine))
        {
            if (!TryAdmitGangHousePlan(session, actor.Id, beforeState, plan, out var priceFailureMessageId))
            {
                if (priceFailureMessageId > 0) return EmitClassicGangHouseMessage(session, priceFailureMessageId);
                continue;
            }
            return ExecuteAdmittedGangHousePlan(session, before, beforeState, plan);
        }

        // A matching Classic phrase whose CHECKITEM/MONSTERS/NOMONSTERS guard fails is consumed by
        // the room script. No generic fallback or invented failure prose is emitted.
        return string.Empty;
    }

    private bool TryAdmitGangHousePlan(
        RuntimeSession session,
        string characterId,
        RuntimeItemState state,
        RuntimeClassicTextblockExecutionPlan plan,
        out int terminalFailureMessageId)
    {
        terminalFailureMessageId = 0;
        var carried = state.Items.Where(item =>
            item.OwnerId?.Equals(characterId, StringComparison.OrdinalIgnoreCase) == true
            && item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)).ToArray();
        var hasMonsters = LivingNpcsInRoom(session).Any(npc => npc.Alive);

        foreach (var operation in plan.Operations)
        {
            switch (operation.Opcode)
            {
                case "checkitem":
                {
                    var itemId = operation.ItemId ?? operation.NumericArguments.FirstOrDefault();
                    if (!carried.Any(item => item.CanonicalItemId.Equals($"item.{itemId}", StringComparison.OrdinalIgnoreCase)))
                        return false;
                    break;
                }
                case "monsters":
                    if (!hasMonsters) return false;
                    break;
                case "nomonsters":
                    if (hasMonsters) return false;
                    break;
                case "price":
                {
                    var priceCopper = operation.NumericArguments.FirstOrDefault();
                    if (priceCopper < 0) throw new InvalidDataException("Decoded gang-house service price cannot be negative.");
                    if (RuntimeBundleCInventoryAuthority.ReadCurrency(state).CopperValue >= priceCopper) break;
                    terminalFailureMessageId = operation.NumericArguments.Skip(1).FirstOrDefault();
                    return false;
                }
            }
        }
        return true;
    }

    private string ExecuteAdmittedGangHousePlan(
        RuntimeSession session,
        RuntimeCharacterRecord before,
        RuntimeItemState beforeState,
        RuntimeClassicTextblockExecutionPlan plan)
    {
        var actorId = before.CharacterId;
        var workingState = beforeState;
        foreach (var price in plan.Operations.Where(operation => operation.Opcode == "price"))
            workingState = RuntimeBundleCEconomyAuthority.DebitCopperValue(workingState, price.NumericArguments.FirstOrDefault());

        var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, before.WorldId);
        var characterAuthority = RuntimeCharacterBundleCatalog.Load(_characters.ProjectRoot, before.WorldId);
        var itemEngine = new RuntimeItemEngine(itemCatalog, workingState.Items, workingState.Corpses, characterAuthority);
        var itemStateChanged = plan.Operations.Any(operation => operation.Opcode == "price");

        // Validate generated references before mutating any live world owner.
        foreach (var operation in plan.Operations)
        {
            if (operation.Opcode is "giveitem" or "takeitem")
            {
                var itemId = operation.ItemId ?? operation.NumericArguments.FirstOrDefault();
                _ = itemCatalog.Require($"item.{itemId}");
            }
            else if (operation.Opcode == "summon")
            {
                var npcId = operation.NpcId ?? operation.NumericArguments.FirstOrDefault();
                if (npcId <= 0 || !session.World.Npcs.ContainsKey(npcId))
                    throw new InvalidDataException($"Decoded gang-house summon npc.{npcId} is unavailable in the generated world.");
            }
            else if (operation.Opcode == "clearitem" && operation.NumericArguments.FirstOrDefault() != 0)
                throw new InvalidDataException("Only the natively recovered gang-house CLEARITEM 0 room-owner form is executable here.");
        }

        foreach (var operation in plan.Operations)
        {
            switch (operation.Opcode)
            {
                case "giveitem":
                {
                    var itemId = operation.ItemId ?? operation.NumericArguments.FirstOrDefault();
                    itemEngine.Create($"item.{itemId}", RuntimeItemLocations.Character, actorId, ownerId: actorId,
                        metadata: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["classic_textblock_grant"] = $"{plan.RootTextblockId}:{plan.MatchedLine}"
                        });
                    itemStateChanged = true;
                    break;
                }
                case "takeitem":
                {
                    var itemId = operation.ItemId ?? operation.NumericArguments.FirstOrDefault();
                    var item = itemEngine.Inventory(actorId).FirstOrDefault(candidate =>
                        candidate.CanonicalItemId.Equals($"item.{itemId}", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException($"takeitem missing item.{itemId}");
                    itemEngine.Remove(item.InstanceId);
                    itemStateChanged = true;
                    break;
                }
            }
        }

        if (itemStateChanged)
        {
            workingState = itemEngine.Snapshot(workingState.Gold, workingState.ActiveItemState);
            _characters.SaveCharacter(before with { ItemState = workingState, UpdatedAtUtc = DateTimeOffset.UtcNow },
                $"classic-ganghouse:textblock.{plan.RootTextblockId}:line.{plan.MatchedLine}");
        }

        var output = new List<string>();
        foreach (var operation in plan.Operations)
        {
            switch (operation.Opcode)
            {
                case "summon":
                {
                    var npcId = operation.NpcId ?? operation.NumericArguments.FirstOrDefault();
                    SummonLivingNpc(session, $"npc.{npcId}",
                        $"classic-ganghouse:textblock.{plan.RootTextblockId}:line.{plan.MatchedLine}");
                    break;
                }
                case "clearitem":
                    _ = Living(session).RemoveAllGroundItemTreesInRoom(session.CurrentRoom.Id);
                    break;
                case "adddelay":
                    session.AddClassicCommandDelay(operation.NumericArguments.FirstOrDefault());
                    break;
                case "message":
                {
                    var messageId = operation.NumericArguments.FirstOrDefault();
                    if (messageId <= 0) break;
                    var rendered = EmitClassicGangHouseMessage(session, messageId);
                    if (!string.IsNullOrWhiteSpace(rendered)) output.Add(rendered);
                    break;
                }
            }
        }
        return string.Join(Environment.NewLine, output);
    }
    private string EmitClassicGangHouseMessage(RuntimeSession session, int messageId)
    {
        var nativePayloads = NativePayloads(session.World.Definition.Id);
        var player = nativePayloads.RequireMessage(messageId);
        if (nativePayloads.TryResolveSecondaryMessage(messageId, out var secondary)
            && !string.IsNullOrWhiteSpace(secondary))
        {
            _sessions.BroadcastRoom(session,
                secondary.Replace("%s", session.Actor.DisplayName, StringComparison.Ordinal),
                includeSource: false);
        }
        return player;
    }

}
