namespace RealmGate.Server;

/// <summary>
/// Evidence-gated executor for room-owned MajorMUD textblock action scripts.
/// The executor now owns the recovered room-action families needed by Classic runtime:
///  - gang-house summons, healer purchases, emblem/key creation, and rubbish disposal;
///  - immediate room relocations whose decoded line is presentation -> teleport -> presentation;
///  - the exact dark-druid field transition in textblock 5001 (message -> cast 213 -> teleport -> message);
///  - exact room 8/461 statue commands from textblock 863, which summon NPC 347;
///  - exact room 16/1517 latch command from textblock 2892, which grants item 1281 then renders message 2519.
/// Other decoded room actions remain owned by their existing runtime consumers or fail closed until their
/// condition/timing semantics are recovered.
/// </summary>
public sealed partial class RuntimeLiveGameplayFacade
{
    private bool TryExecuteClassicRoomActionScript(RuntimeSession session, RuntimeCommand command, out string result)
    {
        result = string.Empty;
        var entered = string.Join(' ', new[] { command.Name }.Concat(command.Arguments)).Trim();
        var graph = TextblockGraph(session.World.Definition.Id);
        var plans = graph.ResolveRoomActionPlans(session.CurrentRoom.MapId, session.CurrentRoom.RoomNumber, entered);
        if (plans.Count == 0) return false;

        // Exact gang-house action scripts may expose alternative same-phrase lines. Keep those
        // alternatives together so CHECKITEM can select the authored credential branch.
        if (plans.All(IsRecoveredGangHouseServicePlan))
        {
            result = ExecuteRecoveredGangHousePlans(session, plans);
            return true;
        }

        if (plans.Count == 1 && IsRecoveredSimpleHealerPlan(plans[0]))
        {
            result = ExecuteRecoveredSimpleHealerPlan(session, plans[0]);
            return true;
        }

        if (plans.Count == 1
            && RuntimeClassicPaidCarriageAuthority.TryResolve(session.CurrentRoom.MapId, session.CurrentRoom.RoomNumber, entered, out var carriageRoute)
            && RuntimeClassicPaidCarriageAuthority.MatchesPlan(carriageRoute, plans[0]))
        {
            result = ExecuteRecoveredPaidCarriagePlan(session, plans[0], carriageRoute);
            return true;
        }

        if (plans.Count == 1 && IsRecoveredImmediateRelocationPlan(plans[0]))
        {
            result = ExecuteRecoveredImmediateRelocationPlan(session, plans[0]);
            return true;
        }

        var plan = plans.Count == 1 ? plans[0] : null;
        if (plan is not null && IsRecoveredDarkDruidFieldPlan(plan))
        {
            result = ExecuteRecoveredDarkDruidFieldPlan(session, plan);
            return true;
        }

        if (plan is not null && IsRecoveredMap8StatueSummonPlan(plan))
        {
            result = ExecuteRecoveredMap8StatueSummonPlan(session, plan);
            return true;
        }

        if (plan is not null && IsRecoveredMap16LatchRewardPlan(plan))
        {
            result = ExecuteRecoveredMap16LatchRewardPlan(session, plan);
            return true;
        }

        result = ExecuteDecodedNarrativePlans(
            session,
            plans,
            $"classic-room-textblock:{plans[0].RootTextblockId}:{plans[0].MatchedLine}");
        return true;
    }

    private bool CanExecuteClassicRoomAction(string worldId, int mapNumber, int roomNumber, string entered)
    {
        IReadOnlyList<RuntimeClassicTextblockExecutionPlan> plans;
        try
        {
            plans = TextblockGraph(worldId).ResolveRoomActionPlans(mapNumber, roomNumber, entered);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        if (plans.Count == 0) return false;
        if (plans.All(IsRecoveredGangHouseServicePlan)) return true;
        if (plans.All(IsFullyDecodedNarrativePlan)) return true;
        if (plans.Count != 1) return false;
        var plan = plans[0];
        return IsRecoveredSimpleHealerPlan(plan)
            || (RuntimeClassicPaidCarriageAuthority.TryResolve(mapNumber, roomNumber, entered, out var carriage)
                && RuntimeClassicPaidCarriageAuthority.MatchesPlan(carriage, plan))
            || IsRecoveredImmediateRelocationPlan(plan)
            || IsRecoveredDarkDruidFieldPlan(plan)
            || IsRecoveredMap8StatueSummonPlan(plan)
            || IsRecoveredMap16LatchRewardPlan(plan);
    }

    private static bool IsFullyDecodedNarrativePlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        var dispatcher = new RuntimeClassicNarrativeOperationDispatcher();
        foreach (var operation in plan.Operations)
        {
            if (!operation.Status.Equals("decoded", StringComparison.OrdinalIgnoreCase)) return false;
            if (!IsDecodedNarrativeOpcodeLiveBound(operation.Opcode)) return false;
            try { _ = dispatcher.Classify(operation); }
            catch (InvalidOperationException) { return false; }
        }
        return true;
    }

    public IReadOnlyList<RuntimeClassicRoomActionCommand> GetExecutableRoomActionCommands(string worldId, int mapNumber, int roomNumber) =>
        TextblockGraph(worldId).GetRoomActionCommands(mapNumber, roomNumber)
            .Where(action => CanExecuteClassicRoomAction(worldId, mapNumber, roomNumber, action.Command))
            .ToArray();

    private static bool IsRecoveredDarkDruidFieldPlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        if (plan.RootTextblockId != 5001 || plan.Operations.Count != 4) return false;
        if (!plan.MatchedInput.Equals("go arch", StringComparison.OrdinalIgnoreCase)
            && !plan.MatchedInput.Equals("enter arch", StringComparison.OrdinalIgnoreCase)
            && !plan.MatchedInput.Equals("go field", StringComparison.OrdinalIgnoreCase)
            && !plan.MatchedInput.Equals("enter field", StringComparison.OrdinalIgnoreCase)) return false;

        var firstMessage = plan.Operations[0];
        var cast = plan.Operations[1];
        var teleport = plan.Operations[2];
        var secondMessage = plan.Operations[3];
        return firstMessage.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
            && (firstMessage.TargetTextblockId ?? firstMessage.NumericArguments.FirstOrDefault()) == 287
            && cast.Opcode.Equals("cast", StringComparison.OrdinalIgnoreCase)
            && (cast.SpellId ?? cast.NumericArguments.FirstOrDefault()) == 213
            && teleport.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase)
            && (teleport.RoomNumber ?? teleport.NumericArguments.FirstOrDefault()) == 142
            && (teleport.MapNumber ?? teleport.NumericArguments.Skip(1).FirstOrDefault()) == 7
            && secondMessage.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
            && (secondMessage.TargetTextblockId ?? secondMessage.NumericArguments.FirstOrDefault()) == 288;
    }

    private string ExecuteRecoveredDarkDruidFieldPlan(RuntimeSession session, RuntimeClassicTextblockExecutionPlan plan)
    {
        if (session.Actor is not RuntimeCharacterActor)
            throw new InvalidOperationException("A persistent character is required for the decoded dark-druid field transition.");

        // Spell 213 ("opaque field") is a level-0, zero-cost, zero-magnitude, zero-duration
        // native environmental spell. The decoded room script uses it between its two messages
        // and the teleport, but no recovered field proves a persistent character mutation.
        // Preserve that fail-closed boundary instead of inventing a status effect or player cast.
        var output = new List<string>();
        var beforeMessage = NativePayloads(session.World.Definition.Id).RequireMessage(287);
        if (!string.IsNullOrWhiteSpace(beforeMessage)) output.Add(beforeMessage);
        BroadcastRecoveredNativeSecondaryMessage(session, 287);

        var destination = session.World.FindRoom(7, 142)
            ?? throw new InvalidOperationException("Decoded dark-druid field destination 7/142 is unavailable.");
        session.MoveToWorld(session.World, destination, $"classic-room-textblock:{plan.RootTextblockId}:spell.213");
        _characters.SaveSession(session);
        AdmitClassicRoomEntryNpcRespawns(session);

        var afterMessage = NativePayloads(session.World.Definition.Id).RequireMessage(288);
        if (!string.IsNullOrWhiteSpace(afterMessage)) output.Add(afterMessage);
        BroadcastRecoveredNativeSecondaryMessage(session, 288);
        return string.Join(Environment.NewLine, output);
    }

    private void BroadcastRecoveredNativeSecondaryMessage(RuntimeSession session, int messageId)
    {
        if (!NativePayloads(session.World.Definition.Id).TryResolveSecondaryMessage(messageId, out var observerTemplate)
            || string.IsNullOrWhiteSpace(observerTemplate)) return;
        var observerMessage = observerTemplate.Replace("%s", session.Actor.DisplayName, StringComparison.Ordinal);
        _sessions.BroadcastRoom(session, observerMessage, includeSource: false);
    }

    private static bool IsRecoveredSimpleHealerPlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        var opcodes = plan.Operations.Select(op => op.Opcode).ToArray();
        if (opcodes.Length != 4 ||
            !opcodes[0].Equals("monsters", StringComparison.OrdinalIgnoreCase) ||
            !opcodes[1].Equals("price", StringComparison.OrdinalIgnoreCase) ||
            !opcodes[2].Equals("cast", StringComparison.OrdinalIgnoreCase) ||
            !opcodes[3].Equals("adddelay", StringComparison.OrdinalIgnoreCase)) return false;
        var spellId = plan.Operations[2].SpellId ?? plan.Operations[2].NumericArguments.FirstOrDefault();
        return spellId is >= 219 and <= 224;
    }

    private string ExecuteRecoveredSimpleHealerPlan(RuntimeSession session, RuntimeClassicTextblockExecutionPlan plan)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent character is required for a healer service.");

        // The native `monsters` guard proves that the room's healer/NPC must be present. Its no-healer
        // failure presentation is not encoded on these lines, so fail closed with no invented prose.
        if (!LivingNpcsInRoom(session).Any(npc => npc.Alive)) return string.Empty;

        var priceOp = plan.Operations.Single(op => op.Opcode.Equals("price", StringComparison.OrdinalIgnoreCase));
        var castOp = plan.Operations.Single(op => op.Opcode.Equals("cast", StringComparison.OrdinalIgnoreCase));
        var priceCopper = priceOp.NumericArguments.FirstOrDefault();
        var insufficientFundsMessageId = priceOp.NumericArguments.Skip(1).FirstOrDefault();
        var spellId = castOp.SpellId ?? castOp.NumericArguments.FirstOrDefault();
        if (priceCopper < 0 || spellId is < 219 or > 224)
            throw new InvalidOperationException("Decoded healer transaction is invalid.");

        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found for the healer service.");
        var beforeState = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0,
            new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        if (RuntimeBundleCInventoryAuthority.ReadCurrency(beforeState).CopperValue < priceCopper)
            return insufficientFundsMessageId > 0 ? EmitClassicGangHouseMessage(session, insufficientFundsMessageId) : string.Empty;

        EnsureLivePowers(session);
        var liveBefore = _liveSpellEngine!.Snapshot();
        try
        {
            ApplyRecoveredHealerSpell(session, actor.Id, spellId);

            // Debit after the effect so remove-curse/item mutations are preserved, but affordability is
            // validated before any effect. Rollback restores both character and live-spell state.
            var afterEffect = _characters.LoadCharacter(actor.Id) ?? before;
            var afterState = afterEffect.ItemState ?? beforeState;
            var debited = RuntimeBundleCEconomyAuthority.DebitCopperValue(afterState, priceCopper);
            _characters.SaveCharacter(afterEffect with { ItemState = debited, UpdatedAtUtc = DateTimeOffset.UtcNow },
                $"classic-healer:textblock.{plan.RootTextblockId}:spell.{spellId}");
            var delayValue = plan.Operations.Single(op => op.Opcode.Equals("adddelay", StringComparison.OrdinalIgnoreCase))
                .NumericArguments.FirstOrDefault();
            session.AddClassicCommandDelay(delayValue);
            PersistLivePowers(session.World.Definition.Id);

            // The recovered healer textblock line has no MESSAGE opcode; native CAST delegates directly to the spell owner, so no extra gang-house service prose is invented for this room-script path.
            // Do not reuse player-cast observer prose with the player incorrectly acting as the healer.
            return string.Empty;
        }
        catch
        {
            _liveSpellEngine.RestoreSnapshot(liveBefore);
            _characters.SaveCharacter(before, $"classic-healer-rollback:textblock.{plan.RootTextblockId}:spell.{spellId}");
            PersistLivePowers(session.World.Definition.Id);
            throw;
        }
    }

    private void ApplyRecoveredHealerSpell(RuntimeSession session, string characterId, int legacySpellId)
    {
        var spell = _liveSpellCatalog!.Require($"spell.{legacySpellId}");
        var target = new RuntimeSpellTarget("character", characterId);
        var now = DateTimeOffset.UtcNow;

        if (legacySpellId is >= 219 and <= 221)
        {
            // These healer spells are non-hostile, level-0 generated spells. Reuse the same native-data
            // spell magnitude resolver used by live spellcasting rather than creating a healer formula.
            var resolution = RuntimeClassicPlayerSpellCombat.Resolve(spell, 0, false, false, false, Random.Shared, 0);
            var character = _characters.LoadCharacter(characterId)
                ?? throw new InvalidOperationException("Healer target character was not found.");
            var healed = Math.Clamp(checked(character.HitPoints + resolution.Magnitude), 0, character.MaximumHitPoints);
            _characters.SaveCharacter(character with { HitPoints = healed, UpdatedAtUtc = now },
                $"classic-healer-spell:{legacySpellId}");
            return;
        }

        if (legacySpellId is 222 or 223)
        {
            _ = ApplyDecodedCureAndDispel(session, spell, target);
            SynchronizeCharacterSpellStatuses(target, now, $"classic-healer-spell:{legacySpellId}");
            return;
        }

        if (legacySpellId == 224)
        {
            _ = ApplyDecodedRemoveCurse(session, target, now);
            return;
        }

        throw new InvalidOperationException($"Unsupported recovered healer spell {legacySpellId}.");
    }


    private static bool IsRecoveredMap8StatueSummonPlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        if (plan.RootTextblockId != 863 || plan.Operations.Count != 1) return false;
        if (!plan.MatchedInput.Equals("touch statue", StringComparison.OrdinalIgnoreCase)
            && !plan.MatchedInput.Equals("move statue", StringComparison.OrdinalIgnoreCase)) return false;
        var operation = plan.Operations[0];
        var npcId = operation.NpcId ?? operation.NumericArguments.FirstOrDefault();
        return operation.Opcode.Equals("summon", StringComparison.OrdinalIgnoreCase) && npcId == 347;
    }

    private string ExecuteRecoveredMap8StatueSummonPlan(RuntimeSession session, RuntimeClassicTextblockExecutionPlan plan)
    {
        _ = SummonLivingNpc(session, "npc.347", $"classic-room-textblock:{plan.RootTextblockId}");
        return string.Empty;
    }

    private static bool IsRecoveredMap16LatchRewardPlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        if (plan.RootTextblockId != 2892 || !plan.MatchedInput.Equals("lift latch", StringComparison.OrdinalIgnoreCase))
            return false;
        if (plan.Operations.Count != 2) return false;
        var giveItem = plan.Operations[0];
        var message = plan.Operations[1];
        var itemId = giveItem.ItemId ?? giveItem.NumericArguments.FirstOrDefault();
        var messageId = message.TargetTextblockId ?? message.NumericArguments.FirstOrDefault();
        return giveItem.Opcode.Equals("giveitem", StringComparison.OrdinalIgnoreCase)
            && itemId == 1281
            && message.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
            && messageId == 2519;
    }

    private string ExecuteRecoveredMap16LatchRewardPlan(RuntimeSession session, RuntimeClassicTextblockExecutionPlan plan)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent character is required for the decoded latch reward.");

        var character = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found for the decoded latch reward.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0,
            new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var characterAuthority = RuntimeCharacterBundleCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses, characterAuthority);
        _ = engine.Create("item.1281", RuntimeItemLocations.Character, actor.Id, ownerId: actor.Id);
        var updatedState = engine.Snapshot(state.Gold, state.ActiveItemState);
        _characters.SaveCharacter(character with
        {
            ItemState = updatedState,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }, $"classic-room-textblock:{plan.RootTextblockId}:giveitem:1281");

        BroadcastRecoveredNativeSecondaryMessage(session, 2519);
        return NativePayloads(session.World.Definition.Id).RequireMessage(2519);
    }

    private sealed class RuntimeRoomActionRandomSource : IRuntimeClassicDialogueRandomSource
    {
        public int NextInclusive(int minimum, int maximum) => Random.Shared.Next(minimum, checked(maximum + 1));
    }

    private string ExecuteRecoveredPaidCarriagePlan(
        RuntimeSession session,
        RuntimeClassicTextblockExecutionPlan plan,
        RuntimeClassicPaidCarriageRouteDefinition route)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent character is required for a decoded paid carriage route.");
        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found for the paid carriage route.");
        var beforeState = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0,
            new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        if (RuntimeBundleCInventoryAuthority.ReadCurrency(beforeState).CopperValue < route.FareCopper)
            return NativePayloads(session.World.Definition.Id).RequireMessage(route.InsufficientFundsMessageId);

        var sourceRoom = session.CurrentRoom;
        var debitedState = RuntimeBundleCEconomyAuthority.DebitCopperValue(beforeState, route.FareCopper);
        _characters.SaveCharacter(before with { ItemState = debitedState, UpdatedAtUtc = DateTimeOffset.UtcNow },
            $"classic-carriage:{route.Id}:fare");
        try
        {
            var output = new List<string>();
            var departure = NativePayloads(session.World.Definition.Id).RequireMessage(route.DepartureMessageId);
            if (!string.IsNullOrWhiteSpace(departure)) output.Add(departure);
            BroadcastRecoveredNativeSecondaryMessage(session, route.DepartureMessageId);

            var randomOperation = plan.Operations.Single(operation => operation.Opcode.Equals("random", StringComparison.OrdinalIgnoreCase));
            var randomTables = RuntimeClassicRandomTableAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id, new RuntimeRoomActionRandomSource());
            var outcome = randomTables.Expand(new[] { randomOperation }, out var selectionCount);
            if (selectionCount != 1) throw new InvalidDataException($"Decoded carriage route {route.Id} selected {selectionCount} random tables instead of one.");

            // Validate all references before applying the authored outcome.
            foreach (var operation in outcome)
            {
                if (operation.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase))
                {
                    var roomNumber = operation.RoomNumber ?? operation.NumericArguments.FirstOrDefault();
                    var mapNumber = operation.MapNumber ?? operation.NumericArguments.Skip(1).FirstOrDefault();
                    _ = session.World.FindRoom(mapNumber, roomNumber)
                        ?? throw new InvalidDataException($"Decoded carriage destination {mapNumber}/{roomNumber} is unavailable.");
                }
                else if (operation.Opcode.Equals("summon", StringComparison.OrdinalIgnoreCase))
                {
                    var npcId = operation.NpcId ?? operation.NumericArguments.FirstOrDefault();
                    if (npcId <= 0 || !session.World.Npcs.ContainsKey(npcId))
                        throw new InvalidDataException($"Decoded carriage encounter npc.{npcId} is unavailable.");
                }
                else if (!operation.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Unsupported decoded carriage outcome opcode '{operation.Opcode}'.");
            }

            foreach (var operation in outcome)
            {
                if (operation.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase))
                {
                    var messageId = operation.TargetTextblockId ?? operation.NumericArguments.FirstOrDefault();
                    if (messageId > 0)
                    {
                        var message = NativePayloads(session.World.Definition.Id).RequireMessage(messageId);
                        if (!string.IsNullOrWhiteSpace(message)) output.Add(message);
                    }
                }
                else if (operation.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase))
                {
                    var roomNumber = operation.RoomNumber ?? operation.NumericArguments.FirstOrDefault();
                    var mapNumber = operation.MapNumber ?? operation.NumericArguments.Skip(1).FirstOrDefault();
                    var destination = session.World.FindRoom(mapNumber, roomNumber)!;
                    session.MoveToWorld(session.World, destination, $"classic-carriage:{route.Id}");
                    _characters.SaveSession(session);
                    AdmitClassicRoomEntryNpcRespawns(session);
                }
                else if (operation.Opcode.Equals("summon", StringComparison.OrdinalIgnoreCase))
                {
                    var npcId = operation.NpcId ?? operation.NumericArguments.FirstOrDefault();
                    _ = SummonLivingNpc(session, $"npc.{npcId}", $"classic-carriage:{route.Id}");
                }
            }
            return string.Join(Environment.NewLine, output);
        }
        catch
        {
            if (!session.CurrentRoom.Id.Equals(sourceRoom.Id, StringComparison.OrdinalIgnoreCase))
            {
                session.MoveToWorld(session.World, sourceRoom, $"classic-carriage:{route.Id}:rollback");
                _characters.SaveSession(session);
            }
            _characters.SaveCharacter(before, $"classic-carriage:{route.Id}:rollback");
            throw;
        }
    }

    private static bool IsRecoveredImmediateRelocationPlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        if (plan.Operations.Count == 0 || plan.Operations.Count(op => op.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase)) != 1)
            return false;
        return plan.Operations.All(op => op.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
            || op.Opcode.Equals("text", StringComparison.OrdinalIgnoreCase)
            || op.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase));
    }

    private string ExecuteRecoveredImmediateRelocationPlan(RuntimeSession session, RuntimeClassicTextblockExecutionPlan plan)
    {
        if (session.Actor is not RuntimeCharacterActor)
            throw new InvalidOperationException("A persistent character is required for a decoded room relocation.");

        var output = new List<string>();
        foreach (var operation in plan.Operations)
        {
            if (operation.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                var messageId = operation.NumericArguments.FirstOrDefault();
                if (messageId > 0)
                {
                    var payload = NativePayloads(session.World.Definition.Id).RequireMessage(messageId);
                    if (!string.IsNullOrWhiteSpace(payload)) output.Add(payload);
                }
                continue;
            }
            if (operation.Opcode.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                var textblockId = operation.NumericArguments.FirstOrDefault();
                if (textblockId > 0)
                {
                    var payload = NativePayloads(session.World.Definition.Id).RequireTextblock(textblockId);
                    if (!string.IsNullOrWhiteSpace(payload)) output.Add(payload);
                }
                continue;
            }
            if (!operation.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase)) continue;

            var roomNumber = operation.RoomNumber ?? operation.NumericArguments.FirstOrDefault();
            var mapNumber = operation.MapNumber ?? operation.NumericArguments.Skip(1).FirstOrDefault();
            var destination = session.World.FindRoom(mapNumber, roomNumber)
                ?? throw new InvalidOperationException($"Decoded room relocation destination {mapNumber}/{roomNumber} is unavailable.");
            session.MoveToWorld(session.World, destination, $"classic-room-textblock:{plan.RootTextblockId}");
            _characters.SaveSession(session);
            AdmitClassicRoomEntryNpcRespawns(session);
        }
        return string.Join(Environment.NewLine, output);
    }
}
