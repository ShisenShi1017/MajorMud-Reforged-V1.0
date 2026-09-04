using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicItemUseSpendResult(
    string InstanceId,
    int PreviousUses,
    int RemainingUses,
    bool Destroyed);

public sealed partial class RuntimeItemEngine
{
    /// <summary>
    /// Exact Classic WCCMMUD _deduct_item_charge semantics for a decoded item copy.
    /// Current Uses == -1 is unlimited. Every other signed 16-bit value decrements by one.
    /// On a transition to zero the copy is retained only when RetainAfterUses is set; otherwise
    /// it is removed. A retained zero therefore becomes -1 on its next successful use.
    /// </summary>
    public RuntimeClassicItemUseSpendResult SpendClassicUse(string instanceId, string ownerId) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, ownerId);
        var definition = _catalog.Require(item.CanonicalItemId);

        // Native only special-cases the current per-copy value -1. Other signed 16-bit
        // values, including 0, are decremented normally. Thus a retained zero-use copy
        // becomes -1 (unlimited) on its next successful use.
        if (item.Charges == -1)
            return new RuntimeClassicItemUseSpendResult(item.InstanceId, -1, -1, false);

        var previous = unchecked((short)item.Charges);
        var remaining = unchecked((short)(previous - 1));
        if (remaining != 0 || definition.RetainAfterUses)
        {
            Replace(item with { Charges = remaining });
            return new RuntimeClassicItemUseSpendResult(item.InstanceId, previous, remaining, false);
        }

        // Canonical Classic item slots are per-copy, not generic stacks. Reforged may
        // contain an extension-created stack, so consume only one copy without making
        // that extension define Classic stack semantics.
        if (item.StackQuantity > 1)
        {
            var reset = definition.MaximumUses > 0 ? definition.MaximumUses : 0;
            Replace(item with { StackQuantity = item.StackQuantity - 1, Charges = reset });
            return new RuntimeClassicItemUseSpendResult(item.InstanceId, previous, 0, true);
        }

        RemoveCore(item.InstanceId, administrativeOverride: false);
        return new RuntimeClassicItemUseSpendResult(item.InstanceId, previous, 0, true);
    });
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private const string ClassicEatFallbackCounterMarker = "classic.native.item.counter_ce";
    private const string ClassicDrinkFallbackCounterMarker = "classic.native.item.counter_d0";
    private const int ClassicFoodDrinkCounterInitialValue = 1000;

    private string ExecuteClassicEatDrink(
        RuntimeSession session,
        RuntimeCharacterActor actor,
        RuntimeCharacterRecord character,
        RuntimeItemState state,
        RuntimeItemCatalog catalog,
        RuntimeItemEngine engine,
        RuntimeCommand command)
    {
        var selected = Resolve(engine.Inventory(actor.Id), catalog, command.ArgumentText);
        var definition = catalog.Require(selected.CanonicalItemId);
        var eating = command.Name.Equals("eat", StringComparison.OrdinalIgnoreCase);
        var expectedType = eating ? "food" : "drink";

        // Native _cmd_eat/_cmd_drink reject records other than Type 4/5 respectively.
        if (!definition.ItemType.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        // WCCMMUD emits the command success text before _use_no_target. Preserve that
        // observable order by queuing the actor message before any item-owned spell event.
        var success = eating
            ? $"You eat the {definition.Name}."
            : $"You drink the {definition.Name}.";
        session.EnqueueMessage(success);
        var observer = eating
            ? $"{session.Actor.DisplayName} eats {definition.Name}."
            : $"{session.Actor.DisplayName} drinks {definition.Name}.";
        _sessions.BroadcastRoom(session, observer, includeSource: false, priority: RuntimeClassicRoomMessagePriority.High);

        var castAbility = FindClassicNoTargetCastAbility(definition);
        if (castAbility is not null)
        {
            var cast = ExecuteClassicFirstNoTargetItemSpellUse(
                session, character, state, catalog, engine, selected, definition, castAbility);
            if (cast.Succeeded)
                return cast.Output;
        }

        // Native EAT/DRINK only deduct Uses when _use_no_target returns true. With no
        // handled effect (or a failed no-target cast), it increments the persistent user
        // word at +0xCE / +0xD0 instead and leaves item Uses unchanged.
        _ = CommitClassicEatDrinkFallback(character, state, engine, eating);
        return string.Empty;
    }

    private static RuntimeItemAbilityEntry? FindClassicNoTargetCastAbility(RuntimeItemDefinition definition)
    {
        foreach (var ability in definition.OrderedAbilities.OrderBy(entry => entry.Slot))
        {
            // Native ability 114 (%Spell) stops ordinary no-target scanning unless the
            // caller uses special mode 11. Player EAT/DRINK/ordinary USE must not turn
            // proc weapons into manually castable spell items.
            if (ability.AbilityId == 114)
                return null;
            if (ability.AbilityId == 43 && ability.Value > 0)
                return ability;
        }
        return null;
    }

    private string ExecuteClassicAppraise(
        RuntimeSession session,
        RuntimeCharacterActor actor,
        RuntimeCharacterRecord character,
        RuntimeItemState state,
        RuntimeItemCatalog items,
        RuntimeItemEngine engine,
        string query)
    {
        var services = ResolveServicesAtRoom(
                RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id),
                session.CurrentRoom.RoomNumber)
            .ToArray();
        if (services.Length == 0)
            return string.Empty;

        var shop = RequireShop(services);
        if (ReadGeneratedInteger(shop.GeneratedRecord, "service_type") == 11)
            return "You may not sell items to a gang shop.";
        // Native _find_item_in_inventory searches the player's fixed item arrays. Reforged
        // exposes carried and equipped locations separately, so include both for lookup.
        var selected = Resolve(
            engine.Inventory(actor.Id).Concat(engine.Equipment(actor.Id)).ToArray(),
            items,
            query);
        var definition = items.Require(selected.CanonicalItemId);

        if (!TryFindClassicShopInventoryEntry(shop.GeneratedRecord, selected.CanonicalItemId, out _))
            return $"You cannot sell {definition.Name} here.";

        var generated = RequireGeneratedItemRecord(_characters.ProjectRoot, character.WorldId, selected.CanonicalItemId);
        var baseCopper = checked(ReadItemCost(generated) * CostTypeCopperMultiplier(ReadItemCostType(generated)));
        var quoteCopper = RuntimeClassicEconomyPricing.QuoteSellCopper(baseCopper, ResolveEconomyCharm(character));
        var denomination = quoteCopper == 1 ? "copper farthing" : "copper farthings";
        return $"You would get {quoteCopper} {denomination} for your {definition.Name}.";
    }

    private RuntimeCharacterRecord CommitClassicEatDrinkFallback(
        RuntimeCharacterRecord character,
        RuntimeItemState state,
        RuntimeItemEngine engine,
        bool eating)
    {
        var marker = eating ? ClassicEatFallbackCounterMarker : ClassicDrinkFallbackCounterMarker;
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase);
        var current = markers.TryGetValue(marker, out var encoded) && short.TryParse(encoded, out var parsed)
            ? parsed
            : (short)ClassicFoodDrinkCounterInitialValue;
        var next = unchecked((short)(current + 1));
        markers[marker] = next.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var now = DateTimeOffset.UtcNow;
        var projected = character with { SemanticState = semantic with { RuntimeMarkers = markers } };
        var commit = RuntimeEquipmentRuntimeAuthority.Commit(projected, engine, state.Gold, state.ActiveItemState, now);
        _characters.SaveCharacter(commit.Character, eating ? "classic-eat-fallback-counter" : "classic-drink-fallback-counter");
        return commit.Character;
    }

    internal static RuntimeCharacterRecord TickClassicFoodDrinkFallbackCounters(RuntimeCharacterRecord character)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var key in new[] { ClassicEatFallbackCounterMarker, ClassicDrinkFallbackCounterMarker })
        {
            var current = markers.TryGetValue(key, out var encoded) && short.TryParse(encoded, out var parsed)
                ? parsed
                : (short)ClassicFoodDrinkCounterInitialValue;
            markers[key] = unchecked((short)(current - 1)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            changed = true;
        }
        return changed ? character with { SemanticState = semantic with { RuntimeMarkers = markers } } : character;
    }

    private sealed record ClassicNoTargetItemSpellResult(bool Succeeded, string Output);

    private ClassicNoTargetItemSpellResult ExecuteClassicFirstNoTargetItemSpellUse(
        RuntimeSession session,
        RuntimeCharacterRecord character,
        RuntimeItemState state,
        RuntimeItemCatalog catalog,
        RuntimeItemEngine engine,
        RuntimeItemInstance selected,
        RuntimeItemDefinition definition,
        RuntimeItemAbilityEntry ability)
    {
        EnsureLivePowers(session);
        var now = DateTimeOffset.UtcNow;
        var powerBefore = _liveSpellEngine!.Snapshot();
        try
        {
            var spell = _liveSpellCatalog!.Spells.SingleOrDefault(candidate =>
                    candidate.LegacyId.Equals(ability.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"ITEM_CAST_SPELL_UNRESOLVED:{selected.CanonicalItemId}:{ability.Value}");
            if (!spell.TargetType.Equals("1", StringComparison.OrdinalIgnoreCase)
                && !spell.TargetType.Equals("self", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"ITEM_CAST_NON_SELF_TARGET_UNSUPPORTED:{selected.CanonicalItemId}:{spell.Id}");

            _liveSpellEngine.SetResources(
                character.CharacterId,
                new RuntimeSpellResources(
                    Math.Max(character.Mana, spell.ManaCost),
                    Math.Max(ReadEnergy(character), spell.EnergyCost)));

            var temporarilyLearned = !_liveSpellEngine.Knows(character.CharacterId, spell.Id);
            if (temporarilyLearned)
                _liveSpellEngine.Learn(character.CharacterId, spell.Id,
                    new Dictionary<string, string> { ["source"] = $"classic-item-cast:{selected.CanonicalItemId}:slot.{ability.Slot}" });

            var execution = new RuntimeSpellExecutionEngine(
                _liveSpellCatalog,
                _liveSpellEngine,
                new GeneratedLiveSpellOperationPolicy(this, session),
                new LiveSpellEventSink(this, session));
            var cast = execution.Cast(new RuntimeSpellCastRequest(
                character.CharacterId,
                spell.Id,
                new RuntimeSpellTarget("character", character.CharacterId),
                now));
            if (!cast.Succeeded)
            {
                _liveSpellEngine.RestoreSnapshot(powerBefore);
                return new ClassicNoTargetItemSpellResult(false, cast.FailureMessage ?? cast.FailureCode ?? string.Empty);
            }

            if (temporarilyLearned)
                _liveSpellEngine.Forget(character.CharacterId, spell.Id);

            _ = NormalizeDecodedPlayerStatusCast(session, cast.Cast.Effect, now);
            var worldAction = ResolveRecoveredItemSpellWorldAction(session, selected, spell);

            _liveSpellEngine.SetResources(character.CharacterId, new RuntimeSpellResources(character.Mana, ReadEnergy(character)));
            _ = engine.SpendClassicUse(selected.InstanceId, character.CharacterId);

            var latest = _characters.LoadCharacter(character.CharacterId) ?? character;
            var effects = _liveSpellEngine.EffectsFor(character.CharacterId);
            var synchronized = RuntimeCharacterStatusEffectResolver.Synchronize(
                latest, effects, now, $"runtime.classic_item_use:{selected.CanonicalItemId}");
            synchronized = RuntimeClassicStatusProjection.Apply(
                synchronized, effects, ClassicLiveStatusCatalog(session), now,
                $"runtime.classic_item_use:{selected.CanonicalItemId}");
            var commit = RuntimeEquipmentRuntimeAuthority.Commit(
                synchronized, engine, state.Gold, state.ActiveItemState, now);
            _characters.SaveCharacter(commit.Character, $"classic-item-use:{selected.CanonicalItemId}");
            PersistLivePowers(character.WorldId);

            if (worldAction.Handled)
            {
                if (worldAction.BeforeMessageId > 0)
                    BroadcastRecoveredNativeSecondaryMessage(session, worldAction.BeforeMessageId);
                if (worldAction.Destination is not null)
                {
                    session.MoveToWorld(
                        session.World,
                        worldAction.Destination,
                        $"decoded-item-spell:{selected.CanonicalItemId}:textblock.1421");
                    _characters.SaveSession(session);
                    AdmitClassicRoomEntryNpcRespawns(session);
                }
                if (worldAction.AfterMessageId > 0)
                    BroadcastRecoveredNativeSecondaryMessage(session, worldAction.AfterMessageId);
            }

            return new ClassicNoTargetItemSpellResult(
                true,
                worldAction.Handled ? worldAction.Output : $"You use {definition.Name}. {spell.Name} takes effect.");
        }
        catch
        {
            _liveSpellEngine.RestoreSnapshot(powerBefore);
            throw;
        }
    }

    private static bool TryFindClassicShopInventoryEntry(JsonElement service, string itemId, out JsonElement entry)
    {
        if (service.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in inventory.EnumerateArray())
            {
                if (candidate.TryGetProperty("item_ref", out var itemRef)
                    && string.Equals(itemRef.GetString(), itemId, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate.Clone();
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

}
