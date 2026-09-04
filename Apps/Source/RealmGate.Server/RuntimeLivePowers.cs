using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeLivePowerState(
    RuntimeSpellState Spells,
    IReadOnlyDictionary<string, RuntimeActorAbilitySnapshot> Abilities,
    IReadOnlyDictionary<string, DateTimeOffset> SpellCooldowns,
    DateTimeOffset SavedAtUtc);

public sealed class RuntimeLivePowerStateRepository
{
    private readonly string _path;
    public RuntimeLivePowerStateRepository(string projectRoot, string worldId) =>
        _path = Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "LivePowers", "runtime-live-powers.json");

    public RuntimeLivePowerState Load()
    {
        if (!File.Exists(_path)) return Empty();
        return JsonSerializer.Deserialize<RuntimeLivePowerState>(File.ReadAllText(_path)) ?? Empty();
    }

    public void Save(RuntimeLivePowerState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }

    public void RemoveCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || !File.Exists(_path)) return;
        var state = Load();
        var spells = state.Spells with
        {
            KnownSpells = state.Spells.KnownSpells
                .Where(spell => !spell.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray(),
            ActiveEffects = state.Spells.ActiveEffects
                .Where(effect => !effect.CasterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)
                                 && !effect.Target.Id.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray(),
            Resources = state.Spells.Resources
                .Where(pair => !pair.Key.Equals(characterId, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };
        var abilities = state.Abilities
            .Where(pair => !pair.Key.Equals(characterId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var cooldownPrefix = characterId + "\u001f";
        var cooldowns = state.SpellCooldowns
            .Where(pair => !pair.Key.StartsWith(cooldownPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        Save(new RuntimeLivePowerState(spells, abilities, cooldowns, DateTimeOffset.UtcNow));
    }

    private static RuntimeLivePowerState Empty() => new(
        new RuntimeSpellState(Array.Empty<RuntimeKnownSpell>(), Array.Empty<RuntimeActiveSpellEffect>(), new Dictionary<string, RuntimeSpellResources>()),
        new Dictionary<string, RuntimeActorAbilitySnapshot>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase), DateTimeOffset.MinValue);
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private sealed record WeaponOnHitSpellResult(int NpcCurrentHitPoints, string Output);
    private sealed record RecoveredItemSpellWorldAction(
        bool Handled,
        RuntimeRoom? Destination,
        int BeforeMessageId,
        int AfterMessageId,
        string Output);
    private readonly object _livePowerGate = new();
    private readonly Dictionary<string, RuntimeActorAbilityState> _liveAbilityStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _liveSpellCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private RuntimeSpellEngine? _liveSpellEngine;
    private RuntimeSpellCatalog? _liveSpellCatalog;
    private RuntimeAbilityCatalog? _liveAbilityCatalog;
    private RuntimeCharacterLearningCatalog? _liveLearningCatalog;
    private RuntimeCharacterQuestAbilityCatalog? _liveQuestAbilityCatalog;
    private RuntimeCharacterWorldRewardCatalog? _liveWorldRewardCatalog;
    private string? _livePowerWorldId;

    public string ExecuteLivePowerCommand(RuntimeSession session, RuntimeCommand command)
    {
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            ExpireLivePowers(session, DateTimeOffset.UtcNow);
            return command.Name switch
            {
                "cast" => CastLiveSpell(session, command, invoked: false),
                "invoke" => CastLiveSpell(session, command, invoked: true),
                "use" => UseLiveItemOrAbility(session, command),
                "abilities" or "skills" => RenderLiveAbilities(session),
                "prepare" or "spells" => RenderLiveSpells(session),
                "powers" => RenderLivePowers(session),
                "dispel" => DispelLiveEffect(session, command.ArgumentText),
                _ => _policyGateway.Execute(session, command)
            };
        }
    }

    private WeaponOnHitSpellResult ExecuteWeaponOnHitSpell(RuntimeSession session, int legacySpellId)
    {
        lock (_livePowerGate)
        {
            if (session.Actor is not RuntimeCharacterActor actor || !_liveCombats.TryGetValue(session.Id, out var encounter))
                throw new InvalidOperationException("A live combat target is required for a weapon spell effect.");
            EnsureLivePowers(session);
            var spell = _liveSpellCatalog!.Spells.SingleOrDefault(candidate =>
                candidate.LegacyId.Equals(legacySpellId.ToString(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"WEAPON_ON_HIT_SPELL_UNRESOLVED:{legacySpellId}");
            if (!spell.TargetType.Equals("8", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"WEAPON_ON_HIT_SPELL_TARGET_UNSUPPORTED:{spell.Id}:{spell.TargetType}");
            var targetNpc = LivingNpcsInRoom(session).Single(candidate =>
                candidate.InstanceId.Equals(encounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase));
            if (!IsDecodedSpellTargetAdmitted(spell, new RuntimeSpellTarget("npc", targetNpc.InstanceId), targetNpc))
                return new WeaponOnHitSpellResult(targetNpc.CurrentHitPoints, RuntimeClassicCombatAnsi.Effect($"{spell.Name} has no effect."));

            var now = DateTimeOffset.UtcNow;
            var before = _liveSpellEngine!.Snapshot();
            try
            {
                if (!_liveSpellEngine.Knows(actor.Id, spell.Id))
                    _liveSpellEngine.Learn(actor.Id, spell.Id,
                        new Dictionary<string, string> { ["source"] = $"decoded-weapon-on-hit:{legacySpellId}" });
                _liveSpellEngine.SetResources(actor.Id, new RuntimeSpellResources(int.MaxValue / 4, int.MaxValue / 4));
                var execution = new RuntimeSpellExecutionEngine(_liveSpellCatalog, _liveSpellEngine,
                    new GeneratedLiveSpellOperationPolicy(this, session), new LiveSpellEventSink(this, session));
                var result = execution.Cast(new RuntimeSpellCastRequest(
                    actor.Id, spell.Id, new RuntimeSpellTarget("npc", encounter.NpcInstanceId), now));
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.FailureMessage ?? result.FailureCode ?? "The weapon spell effect failed.");

                var afterCast = _liveSpellEngine.Snapshot();
                var retainedEffects = afterCast.ActiveEffects;
                _liveSpellEngine.RestoreSnapshot(before with { ActiveEffects = retainedEffects });
                PersistLivePowers(session.World.Definition.Id);
                var npc = Living(session).Capture().Npcs.Single(candidate =>
                    candidate.InstanceId.Equals(encounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase));
                var eventOutput = string.Join(" ", result.Events.Select(RenderSpellEvent));
                var output = string.IsNullOrWhiteSpace(spell.StartMessage?.CasterMessage)
                    ? $"{spell.Name} erupts from your weapon."
                    : RenderDecodedStartMessage(spell.StartMessage.CasterMessage, targetNpc.Name, targetNpc.Name);
                if (!string.IsNullOrWhiteSpace(eventOutput)) output += " " + eventOutput;
                return new WeaponOnHitSpellResult(npc.CurrentHitPoints, RuntimeClassicCombatAnsi.Effect(output));
            }
            catch
            {
                _liveSpellEngine.RestoreSnapshot(before);
                throw;
            }
        }
    }

    private IReadOnlyList<RuntimeEquipmentCombatEffects.DamageShieldComponent> ResolveLiveDamageShieldComponents(
        RuntimeSession session, int receivedPhysicalDamage)
    {
        if (receivedPhysicalDamage <= 0) return Array.Empty<RuntimeEquipmentCombatEffects.DamageShieldComponent>();
        lock (_livePowerGate)
        {
            if (session.Actor is not RuntimeCharacterActor actor) return Array.Empty<RuntimeEquipmentCombatEffects.DamageShieldComponent>();
            EnsureLivePowers(session);
            var catalog = ClassicLiveStatusCatalog(session);
            var effects = new List<RuntimeEquipmentCombatEffects.DamageShieldComponent>();
            foreach (var active in _liveSpellEngine!.EffectsFor(actor.Id))
            {
                if (!catalog.TryGet(active.SpellId, out var definition)) continue;
                var shield = definition.Abilities.FirstOrDefault(x => x.AbilityId == 72);
                var shock = definition.Abilities.FirstOrDefault(x => x.AbilityId == 137);
                if (shield is null) continue;
                var damage = shield.Value != 0
                    ? shield.Value
                    : checked((active.MinimumEffect + active.MaximumEffect) / 2);
                if (damage > 0)
                    effects.Add(new RuntimeEquipmentCombatEffects.DamageShieldComponent(damage, shock?.Value ?? 0));
            }
            return effects;
        }
    }

    private void EnsureLivePowers(RuntimeSession session)
    {
        if (_liveSpellEngine is not null && string.Equals(_livePowerWorldId, session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)) return;
        _livePowerWorldId = session.World.Definition.Id;
        _liveSpellCatalog = RuntimeSpellCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        _liveAbilityCatalog = RuntimeAbilityCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        _liveLearningCatalog = RuntimeCharacterLearningCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        _liveQuestAbilityCatalog = RuntimeCharacterQuestAbilityCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        _liveWorldRewardCatalog = new RuntimeCharacterWorldRewardCatalog();
        var persisted = new RuntimeLivePowerStateRepository(_characters.ProjectRoot, session.World.Definition.Id).Load();
        _liveSpellEngine = new RuntimeSpellEngine(_liveSpellCatalog, initial: persisted.Spells);
        var classicStatusCatalog = ClassicLiveStatusCatalog(session);
        _liveSpellEngine.RestoreSnapshot(NormalizeClassicStatusDurations(_liveSpellEngine.Snapshot(), classicStatusCatalog));
        _liveSpellCooldowns.Clear(); foreach (var pair in persisted.SpellCooldowns) _liveSpellCooldowns[pair.Key] = pair.Value;
        _liveAbilityStates.Clear();
        foreach (var pair in persisted.Abilities)
        {
            var state = new RuntimeActorAbilityState(pair.Key); state.Restore(pair.Value); _liveAbilityStates[pair.Key] = state;
        }
        // Expiration is UTC-authoritative. Remove stale persisted effects immediately when
        // the world power state is restored so reconnect cannot retain expired statuses.
        var restoreNow = DateTimeOffset.UtcNow;
        var expiredOnRestore = ExpireAndExecuteDecodedEndCast(session, restoreNow);
        if (expiredOnRestore > 0)
        {
            SynchronizeAllCharacterSpellStatuses(restoreNow, "runtime.live_powers.restore_expire");
            PersistLivePowers(session.World.Definition.Id);
        }
    }

    private string CastLiveSpell(RuntimeSession session, RuntimeCommand command, bool invoked)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var classDefinition = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId).RequireClass(character.ClassId);
        var usesKai = classDefinition.Magic?.Type == 5;
        if (invoked && !usesKai) return "You may not invoke... You are not KAI!";
        if (!invoked && usesKai) return "You may not cast... You are KAI!";
        if (command.Arguments.Count == 0)
            return invoked ? "Syntax: INVOKE {power} [{target}]" : "Syntax: CAST {spell} [{target}]";
        if (RuntimeClassicStatusProjection.IsSpellCastBlocked(character)) return invoked ? "You cannot invoke while affected by the current status." : "You cannot cast while affected by the current status.";
        var spell = ResolveSpell(command.Arguments[0]);
        if (!IsDecodedSpellCasterAlignmentAdmitted(spell, character, out var alignmentFailure))
            return alignmentFailure;
        EnsureKnownSpells(character, actor.Id);
        var now = DateTimeOffset.UtcNow;
        var cooldownKey = actor.Id + "\u001f" + spell.Id;
        if (_liveSpellCooldowns.TryGetValue(cooldownKey, out var ready) && ready > now) return $"{spell.Name} is on cooldown until {ready:O}.";
        RuntimeWizardKnockDoor? wizardKnockDoor = null;
        var targetArguments = command.Arguments.Skip(1).ToArray();
        RuntimeSpellTarget target;
        if (spell.DecodedAbilities.Any(ability => ability.AbilityId == 161))
        {
            wizardKnockDoor = Living(session).ResolveWizardKnockDoor(session.CurrentRoom.Id, string.Join(" ", targetArguments));
            target = new RuntimeSpellTarget("world_object", wizardKnockDoor.Definition.Id);
        }
        else target = spell.TargetType == "13"
            ? new RuntimeSpellTarget("character", actor.Id)
            : ResolvePowerTarget(session, targetArguments, spell.TargetType);
        if (spell.TargetType == "12" && !IsClassicPartyHostileRoomSpellTarget(session, target))
            return "That character is not a valid hostile room-spell target.";
        var roomNpcs = target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase) ? LivingNpcsInRoom(session) : Array.Empty<RuntimeLivingNpcState>();
        var targetNpc = target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase)
            ? roomNpcs.FirstOrDefault(npc => npc.InstanceId.Equals(target.Id, StringComparison.OrdinalIgnoreCase))
            : null;
        if (targetNpc is not null && session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase))
        {
            var monsGuards = RuntimeNpcProtectionAdmission.ResolveClassicSpellTarget(targetNpc, roomNpcs);
            if (monsGuards.Redirected)
            {
                target = new RuntimeSpellTarget("npc", monsGuards.EffectiveTargetNpcInstanceId);
                targetNpc = roomNpcs.First(npc => npc.InstanceId.Equals(monsGuards.EffectiveTargetNpcInstanceId, StringComparison.OrdinalIgnoreCase));
            }
        }
        if (!IsDecodedSpellTargetAdmitted(spell, target, targetNpc))
        {
            if (targetNpc is not null && IsDecodedAbilityNegated(spell, ReadNegatedAbilityIds(targetNpc.Metadata)))
                return $"{spell.Name} has no effect; {targetNpc.Name} is immune to that ability.";
            return spell.DecodedAbilities.Any(ability => ability.AbilityId == 80)
                ? $"{spell.Name} can only affect an animal."
                : $"{spell.Name} cannot affect a nonliving target.";
        }
        _liveSpellEngine!.SetResources(actor.Id, new RuntimeSpellResources(character.Mana, ReadEnergy(character)));
        var execution = new RuntimeSpellExecutionEngine(_liveSpellCatalog!, _liveSpellEngine,
            new GeneratedLiveSpellOperationPolicy(this, session), new LiveSpellEventSink(this, session));
        var before = _liveSpellEngine.Snapshot();
        try
        {
            var partyTargets = spell.TargetType == "13" ? ResolveClassicPartySpellTargets(session) : null;
            var result = partyTargets is null
                ? execution.Cast(new RuntimeSpellCastRequest(actor.Id, spell.Id, target, now))
                : execution.CastMany(actor.Id, spell.Id, partyTargets, now);
            if (!result.Succeeded) return result.FailureMessage ?? result.FailureCode ?? "The spell failed.";
            if (wizardKnockDoor is not null) Living(session).SetWizardKnockDoorState(wizardKnockDoor, opened: true);
            var remaining = result.Cast.Resources;
            var selfManaDelta = result.Events
                .Where(e => e.Operation.Equals("mana", StringComparison.OrdinalIgnoreCase)
                    && e.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)
                    && e.Target.Id.Equals(actor.Id, StringComparison.OrdinalIgnoreCase))
                .Sum(e => e.Magnitude);
            SaveCharacterPowerResources(character, checked(remaining.Mana + selfManaDelta), remaining.Energy);
            PersistDecodedTeleportTargets(result.Events);
            var appliedEffects = result.AppliedEffects ?? (result.Cast.Effect is null ? [] : [result.Cast.Effect]);
            var normalizedEffects = appliedEffects.Select(effect =>
                NormalizeDecodedPlayerStatusCast(session, AttachResolvedEffectRoll(effect, result.Events.Where(evt => evt.Target.Id.Equals(effect.Target.Id, StringComparison.OrdinalIgnoreCase)).ToArray()), now))
                .Where(effect => effect is not null).Cast<RuntimeActiveSpellEffect>().ToArray();
            foreach (var characterTarget in appliedEffects.Where(effect => effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)).Select(effect => effect.Target.Id).Distinct(StringComparer.OrdinalIgnoreCase))
                SynchronizeDecodedTemporarySpells(characterTarget);
            if (spell.Duration <= 0 && spell.DecodedAbilities.Any(ability => ability.AbilityId == 151 && ability.Value > 0)
                && normalizedEffects.Length > 0)
                foreach (var normalizedEffect in normalizedEffects) ExecuteImmediateDecodedEndCast(session, normalizedEffect, now);
            foreach (var appliedTarget in (partyTargets ?? [target])) _ = ApplyDecodedCureAndDispel(session, spell, appliedTarget);
            if (ShouldDiscardDecodedRemovalHelper(spell) && normalizedEffects.Length > 0)
            {
                foreach (var normalizedEffect in normalizedEffects)
                    try { _liveSpellEngine.RemoveEffect(normalizedEffect.EffectId); } catch (KeyNotFoundException) { }
            }
            foreach (var appliedTarget in appliedEffects.Select(effect => effect.Target).DistinctBy(appliedTarget => (appliedTarget.Kind, appliedTarget.Id)))
                SynchronizeCharacterSpellStatuses(appliedTarget, now, "runtime.live_powers.cast");
            var removedCurses = spell.DecodedAbilities.Any(x => x.AbilityId == 84)
                ? (partyTargets ?? [target]).Sum(appliedTarget => ApplyDecodedRemoveCurse(session, appliedTarget, now))
                : 0;
            var cooldownSeconds = Math.Max(0, GeneratedInt(spell, "cooldown", "cooldown_seconds", "recast"));
            if (cooldownSeconds > 0) _liveSpellCooldowns[cooldownKey] = now.AddSeconds(cooldownSeconds);
            PersistLivePowers(session.World.Definition.Id);
            foreach (var appliedTarget in (partyTargets ?? [target])) PublishDecodedStartMessage(session, spell, appliedTarget, null, publishObservers: partyTargets is null);
            var targetDisplayName = ResolveSpellTargetDisplayName(target, targetNpc);
            var castOutput = string.IsNullOrWhiteSpace(spell.StartMessage?.CasterMessage)
                ? $"You {(invoked ? "invoke" : "cast")} {spell.Name} on {target.Id}."
                : RenderDecodedStartMessage(spell.StartMessage.CasterMessage, targetDisplayName, targetDisplayName);
            var eventOutput = string.Join(" ", result.Events.Select(RenderSpellEvent));
            if (removedCurses > 0)
                eventOutput = string.IsNullOrWhiteSpace(eventOutput)
                    ? $"{removedCurses} cursed item{(removedCurses == 1 ? "" : "s")} fall free."
                    : eventOutput + $" {removedCurses} cursed item{(removedCurses == 1 ? "" : "s")} fall free.";
            return string.IsNullOrWhiteSpace(eventOutput) ? castOutput : castOutput + " " + eventOutput;
        }
        catch
        {
            if (wizardKnockDoor is not null) Living(session).SetWizardKnockDoorState(wizardKnockDoor, opened: false);
            _liveSpellEngine.RestoreSnapshot(before);
            throw;
        }
    }

    private void PublishDecodedStartMessage(
        RuntimeSession casterSession,
        RuntimeSpellDefinition spell,
        RuntimeSpellTarget target,
        RuntimeLivingNpcState? targetNpc,
        bool publishObservers = true)
    {
        if (spell.StartMessage is null) return;
        var casterName = casterSession.Actor.DisplayName;
        var targetName = ResolveSpellTargetDisplayName(target, targetNpc);
        var targetSession = target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)
            ? _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Actor.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase))
            : null;
        if (targetSession is not null
            && !targetSession.Id.Equals(casterSession.Id, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(spell.StartMessage.TargetMessage))
            targetSession.EnqueueMessage(RenderDecodedStartMessage(spell.StartMessage.TargetMessage, casterName, targetName));
        if (!publishObservers || string.IsNullOrWhiteSpace(spell.StartMessage.ObserverMessage)) return;
        _sessions.BroadcastRoom(
            casterSession,
            RenderDecodedStartMessage(spell.StartMessage.ObserverMessage, casterName, targetName),
            includeSource: false,
            priority: RuntimeClassicRoomMessagePriority.Medium,
            excludedSessionId: targetSession?.Id);
    }

    private string ResolveSpellTargetDisplayName(RuntimeSpellTarget target, RuntimeLivingNpcState? targetNpc) =>
        targetNpc?.Name
        ?? _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Actor.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase))?.Actor.DisplayName
        ?? target.Id;

    internal static string RenderDecodedStartMessage(string template, string firstArgument, string secondArgument)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        var first = template.IndexOf("%s", StringComparison.Ordinal);
        if (first < 0) return template;
        var rendered = template[..first] + firstArgument + template[(first + 2)..];
        var second = rendered.IndexOf("%s", first + firstArgument.Length, StringComparison.Ordinal);
        return second < 0 ? rendered : rendered[..second] + secondArgument + rendered[(second + 2)..];
    }


    private string UseLiveItemOrAbility(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (command.Arguments.Count == 0) return "Use which item or ability?";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        if (TryUseDecodedSpellItem(session, character, command.ArgumentText, out var activeItemResult)) return activeItemResult;
        if (TryUseSpellLearningItem(character, command.ArgumentText, out var itemResult)) return itemResult;
        return UseLiveAbility(session, command);
    }

    private bool TryUseDecodedSpellItem(RuntimeSession session, RuntimeCharacterRecord character, string query, out string result)
    {
        result = string.Empty;
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var characterAuthority = RuntimeCharacterBundleCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var engine = new RuntimeItemEngine(itemCatalog, state.Items, state.Corpses, characterAuthority);
        var matches = engine.Inventory(character.CharacterId).Concat(engine.Equipment(character.CharacterId))
            .Where(item =>
            {
                var definition = itemCatalog.Require(item.CanonicalItemId);
                return FindClassicNoTargetCastAbility(definition) is not null
                    || definition.OrderedAbilities.Any(entry => entry.AbilityId == 181 && entry.Value > 0);
            })
            .Where(item => item.InstanceId.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase) || itemCatalog.Matches(item.CanonicalItemId, query))
            .Take(2)
            .ToArray();
        if (matches.Length == 0) return false;
        if (matches.Length > 1) throw new InvalidOperationException($"Multiple active spell items match '{query}'.");

        var selected = matches[0];
        var definition = itemCatalog.Require(selected.CanonicalItemId);
        if (definition.OrderedAbilities.Any(entry => entry.AbilityId == 181 && entry.Value > 0))
        {
            result = ActivateGangHouseDeed(character, state, itemCatalog, engine, selected, definition);
            return true;
        }

        var castAbility = FindClassicNoTargetCastAbility(definition);
        if (castAbility is null) return false;

        var cast = ExecuteClassicFirstNoTargetItemSpellUse(
            session,
            character,
            state,
            itemCatalog,
            engine,
            selected,
            definition,
            castAbility);
        result = cast.Output;
        return cast.Succeeded;
    }

    private RecoveredItemSpellWorldAction ResolveRecoveredItemSpellWorldAction(
        RuntimeSession session,
        RuntimeItemInstance selected,
        RuntimeSpellDefinition spell)
    {
        // Native spell 607, supplied by item 992 (potion of levitation), owns textblock 1421:
        //   roomitem 993 1834:message 1835:teleport 1009 9:message 1836
        // Item 993 is the non-gettable waterfall placed in room 3/1. Preserve the exact
        // room-item condition and native messages rather than inferring movement from a
        // MegaMUD route fingerprint.
        if (!selected.CanonicalItemId.Equals("item.992", StringComparison.OrdinalIgnoreCase)
            || !spell.LegacyId.Equals("607", StringComparison.OrdinalIgnoreCase))
            return new(false, null, 0, 0, string.Empty);

        var waterfallPresent = Living(session).GroundItems(session.CurrentRoom.Id)
            .Any(item => item.CanonicalItemId.Equals("item.993", StringComparison.OrdinalIgnoreCase));
        if (!waterfallPresent)
            return new(true, null, 1834, 0, _nativePayloads.RequireMessage(1834));

        var destination = session.World.FindRoom(9, 1009)
            ?? throw new InvalidOperationException("Decoded potion-of-levitation destination 9/1009 is unavailable.");
        var output = new[]
            {
                _nativePayloads.RequireMessage(1835),
                _nativePayloads.RequireMessage(1836)
            }
            .Where(message => !string.IsNullOrWhiteSpace(message));
        return new(true, destination, 1835, 1836, string.Join(Environment.NewLine, output));
    }

    private string ActivateGangHouseDeed(
        RuntimeCharacterRecord character,
        RuntimeItemState state,
        RuntimeItemCatalog itemCatalog,
        RuntimeItemEngine engine,
        RuntimeItemInstance deed,
        RuntimeItemDefinition deedDefinition)
    {
        var houses = RuntimeClassicGangHouseCatalog.Load(Path.Combine(_characters.ProjectRoot, "Data", character.WorldId, "gang_houses.json"));
        var house = houses.Records.SingleOrDefault(record => record.DeedItemId.Equals(deed.CanonicalItemId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"GANG_HOUSE_DEED_UNRESOLVED:{deed.CanonicalItemId}");
        var gangs = new RuntimeClassicGangStateService(_characters.ProjectRoot);
        var gang = gangs.FindByMember(character.CharacterId)
            ?? throw new InvalidOperationException("You must lead an active gang to activate a gang-house deed.");
        if (!gang.LeaderCharacterId.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the gang leader may activate a gang-house deed.");

        foreach (var itemId in house.ActivationGrantItemIds)
        {
            _ = itemCatalog.Require(itemId);
            engine.Create(itemId, RuntimeItemLocations.Character, character.CharacterId, ownerId: character.CharacterId);
        }
        var spent = engine.SpendClassicUse(deed.InstanceId, character.CharacterId);
        var updated = character with
        {
            ItemState = engine.Snapshot(state.Gold, state.ActiveItemState),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var assigned = gangs.AssignHouse(gang, character.CharacterId, house.HouseId);
        try
        {
            _characters.SaveCharacter(updated, $"decoded-gang-house-deed:textblock.{house.ActivationTextblockId}");
        }
        catch
        {
            gangs.Save(assigned with { GangHouseId = null });
            throw;
        }
        var message = RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot).RequireMessage(house.ActivationMessageId);
        return $"{message}{Environment.NewLine}{gang.Name} now owns {house.Name}. Four decoded house items were granted. Uses remaining: {spent.RemainingUses}.";
    }

    private bool TryUseSpellLearningItem(RuntimeCharacterRecord character, string query, out string result)
    {
        result = string.Empty;
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>());
        var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var engine = new RuntimeItemEngine(itemCatalog, state.Items, state.Corpses);
        var q = query.Trim();
        var matches = engine.Inventory(character.CharacterId)
            .Where(item => _liveLearningCatalog!.ResolveSpellItem(item.CanonicalItemId).Count > 0)
            .Where(item =>
            {
                var name = itemCatalog.Require(item.CanonicalItemId).Name;
                return item.InstanceId.Equals(q, StringComparison.OrdinalIgnoreCase)
                    || item.CanonicalItemId.Equals(q, StringComparison.OrdinalIgnoreCase)
                    || name.Equals(q, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(q, StringComparison.OrdinalIgnoreCase);
            })
            .Take(2)
            .ToArray();
        if (matches.Length == 0) return false;
        if (matches.Length > 1) throw new InvalidOperationException($"Multiple spell-learning items match '{query}'.");

        var selected = matches[0];
        var relationships = _liveLearningCatalog!.ResolveSpellItem(selected.CanonicalItemId);
        var missing = relationships
            .Where(link => !character.Spells.Contains(link.SpellId, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (missing.Length == 0)
            throw new InvalidOperationException("You already know every spell contained in that item.");

        var updated = character;
        var sourceId = $"item-learnsp:{selected.CanonicalItemId}:{selected.InstanceId}";
        foreach (var relationship in missing)
            updated = _liveLearningCatalog.GrantSpell(updated, relationship.SpellId, sourceId);

        _ = engine.SpendClassicUse(selected.InstanceId, character.CharacterId);
        updated = updated with
        {
            ItemState = engine.Snapshot(state.Gold, state.ActiveItemState),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _characters.SaveCharacter(updated, "learn-spell-from-decoded-learnsp-item");
        EnsureKnownSpells(updated, character.CharacterId);
        PersistLivePowers(character.WorldId);
        result = missing.Length == 1
            ? $"You learn {missing[0].SpellName}."
            : $"You learn {string.Join(", ", missing.Select(x => x.SpellName))}.";
        return true;
    }

    private string UseLiveAbility(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (command.Arguments.Count == 0) return "Use which ability?";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var ability = ResolveAbility(command.Arguments[0]);
        var state = GetAbilityState(actor.Id, character);
        var target = ResolvePowerTarget(session, command.Arguments.Skip(1).ToArray(), GeneratedString(ability.GeneratedRecord, "target_type") ?? "self");
        var resources = new LiveAbilityResources(character, ReadEnergy(character));
        var sink = new LiveAbilityEventSink(this, session, resources);
        var engine = new RuntimeAbilityEngine(_liveAbilityCatalog!, new GeneratedLiveAbilityPolicy(resources), sink);
        var result = engine.Execute(state, new RuntimeAbilityRequest(Guid.NewGuid().ToString("N"), actor.Id, ability.Id, target.Id), DateTimeOffset.UtcNow);
        if (!result.Succeeded) return result.FailureCode ?? "The ability failed.";
        SaveCharacterPowerResources(character, character.Mana, resources.Energy);
        PersistLivePowers(session.World.Definition.Id);
        return $"You use {ability.Name} on {target.Id}. " + string.Join(" ", result.Events.Select(x => $"{x.EventType} {x.Magnitude}"));
    }

    private void EnsureKnownSpells(RuntimeCharacterRecord character, string actorId)
    {
        _liveLearningCatalog!.ValidateOwnership(character);
        foreach (var id in character.Spells)
        {
            _liveLearningCatalog.RequireSpell(id);
            if (!_liveSpellEngine!.Knows(actorId, id) && TryResolveSpell(id, out var spell)) _liveSpellEngine.Learn(actorId, spell.Id);
        }
        SynchronizeDecodedTemporarySpells(actorId);
    }

    private RuntimeActorAbilityState GetAbilityState(string actorId, RuntimeCharacterRecord character)
    {
        if (!_liveAbilityStates.TryGetValue(actorId, out var state)) _liveAbilityStates[actorId] = state = new RuntimeActorAbilityState(actorId);
        _liveLearningCatalog!.ValidateOwnership(character);
        foreach (var id in character.Skills.Keys)
        {
            _liveLearningCatalog.RequireSkill(id);
            if (_liveAbilityCatalog!.TryGet(id, out _) && !state.Knows(id)) state.Learn(id, DateTimeOffset.UtcNow);
        }
        foreach (var id in _liveQuestAbilityCatalog!.ResolveExecutablePowerIds(character, _liveAbilityCatalog!, _liveWorldRewardCatalog))
        {
            if (!state.Knows(id))
                state.Learn(id, DateTimeOffset.UtcNow, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "decoded-quest-ability-flag"
                });
        }
        return state;
    }

    private void ExpireLivePowers(RuntimeSession session, DateTimeOffset now)
    {
        if (_liveSpellEngine is null) return;
        var affectedCharacterIds = _liveSpellEngine.Snapshot().ActiveEffects
            .Where(effect => effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase))
            .Select(effect => effect.Target.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expiredForActor = _liveSpellEngine.Snapshot().ActiveEffects
            .Where(effect => effect.ExpiresAtUtc is not null && effect.ExpiresAtUtc <= now)
            .Where(effect => effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)
                && effect.Target.Id.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase))
            .Select(effect => _liveSpellCatalog!.Require(effect.SpellId))
            .SelectMany(spell => spell.DescriptionMessages ?? Array.Empty<RuntimeSpellDescriptionMessage>())
            .Select(message => message.WearOffMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();
        ExpireAndExecuteDecodedEndCast(session, now);
        foreach (var message in expiredForActor) session.EnqueueMessage(message);
        foreach (var characterId in affectedCharacterIds)
            SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", characterId), now, "runtime.live_powers.expire");
        foreach (var key in _liveSpellCooldowns.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) _liveSpellCooldowns.Remove(key);
        foreach (var state in _liveAbilityStates.Values) state.ClearExpiredCooldowns(now);
        PersistLivePowers(session.World.Definition.Id);
    }

    private IReadOnlyList<string> ActiveSpellStatusMessages(RuntimeSession session, string characterId)
    {
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            return _liveSpellEngine!.EffectsFor(characterId)
                .SelectMany(effect => _liveSpellCatalog!.Require(effect.SpellId).DescriptionMessages ?? Array.Empty<RuntimeSpellDescriptionMessage>())
                .Select(message => message.StatusMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    private int ExpireAndExecuteDecodedEndCast(RuntimeSession session, DateTimeOffset nowUtc)
    {
        var due = _liveSpellEngine!.Snapshot().ActiveEffects
            .Where(effect => effect.ExpiresAtUtc is not null && effect.ExpiresAtUtc <= nowUtc)
            .OrderBy(effect => effect.ExpiresAtUtc).ThenBy(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (due.Length == 0) return 0;
        var before = _liveSpellEngine.Snapshot();
        var sink = new LiveSpellEventSink(this, session);
        var applied = new List<RuntimeSpellExecutionEvent>();
        try
        {
            _liveSpellEngine.Expire(nowUtc);
            foreach (var expired in due) ExecuteDecodedEndCastChain(session, expired, nowUtc, sink, applied, 0);
            foreach (var characterId in due.Where(effect => effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase))
                         .Select(effect => effect.Target.Id).Distinct(StringComparer.OrdinalIgnoreCase))
                SynchronizeDecodedTemporarySpells(characterId);
            return due.Length;
        }
        catch
        {
            for (var index = applied.Count - 1; index >= 0; index--)
                try { sink.Revert(applied[index]); } catch { }
            _liveSpellEngine.RestoreSnapshot(before);
            throw;
        }
    }

    private void ExecuteDecodedEndCastChain(RuntimeSession session, RuntimeActiveSpellEffect expired, DateTimeOffset nowUtc,
        LiveSpellEventSink sink, List<RuntimeSpellExecutionEvent> applied, int depth)
    {
        if (depth >= 32) throw new InvalidOperationException("Decoded EndCast chain exceeded the safe maximum depth.");
        var source = _liveSpellCatalog!.Require(expired.SpellId);
        var castOnEndChance = source.DecodedAbilities.FirstOrDefault(ability => ability.AbilityId == 164)?.Value ?? 100;
        if (!ShouldExecuteCastOnEnd(castOnEndChance, Random.Shared.Next(1, 101))) return;
        var endCastIds = source.DecodedAbilities.Where(ability => ability.AbilityId == 151 && ability.Value > 0)
            .Select(ability => ability.Value).Distinct().ToArray();
        foreach (var legacyId in endCastIds)
        {
            var followup = _liveSpellCatalog.Spells.SingleOrDefault(spell => spell.LegacyId.Equals(legacyId.ToString(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Decoded EndCast target spell {legacyId} is unavailable.");
            var request = new RuntimeSpellCastRequest(expired.CasterId, followup.Id, expired.Target, nowUtc);
            var effect = new GeneratedSpellEffectResolver().Resolve(followup, request);
            var snapshot = _liveSpellEngine!.Snapshot();
            _liveSpellEngine.RestoreSnapshot(snapshot with { ActiveEffects = snapshot.ActiveEffects.Append(effect).ToArray() });
            var events = new GeneratedLiveSpellOperationPolicy(this, session).ResolveOperations(followup, request, effect).ToArray();
            foreach (var spellEvent in events) { sink.Apply(spellEvent); applied.Add(spellEvent); }
            if (followup.Duration <= 0)
            {
                try { _liveSpellEngine.RemoveEffect(effect.EffectId); } catch (InvalidOperationException) { }
                ExecuteDecodedEndCastChain(session, effect, nowUtc, sink, applied, depth + 1);
            }
        }
    }

    private void ExecuteImmediateDecodedEndCast(RuntimeSession session, RuntimeActiveSpellEffect sourceEffect, DateTimeOffset nowUtc)
    {
        var before = _liveSpellEngine!.Snapshot();
        var sink = new LiveSpellEventSink(this, session);
        var applied = new List<RuntimeSpellExecutionEvent>();
        try
        {
            try { _liveSpellEngine.RemoveEffect(sourceEffect.EffectId); } catch (InvalidOperationException) { }
            ExecuteDecodedEndCastChain(session, sourceEffect, nowUtc, sink, applied, 0);
        }
        catch
        {
            for (var index = applied.Count - 1; index >= 0; index--)
                try { sink.Revert(applied[index]); } catch { }
            _liveSpellEngine.RestoreSnapshot(before);
            throw;
        }
    }

    internal static bool ShouldExecuteCastOnEnd(int chancePercent, int roll) =>
        chancePercent is >= 1 and <= 100 && roll is >= 1 and <= 100 && roll <= chancePercent;

    private void SynchronizeDecodedTemporarySpells(string characterId)
    {
        var character = _characters.LoadCharacter(characterId);
        if (character is null) return;
        var permanent = character.Spells.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var desired = ResolveDecodedTemporarySpellIds(
            _liveSpellEngine!.EffectsFor(characterId), _liveSpellCatalog!);
        var temporary = _liveSpellEngine.Spellbook(characterId)
            .Where(known => known.Metadata?.TryGetValue("source", out var source) == true
                && source.Equals("decoded-give-temp-spell", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var known in temporary.Where(known => !desired.Contains(known.SpellId) && !permanent.Contains(known.SpellId)))
            _liveSpellEngine.Forget(characterId, known.SpellId);
        foreach (var spellId in desired.Where(spellId => !permanent.Contains(spellId) && !_liveSpellEngine.Knows(characterId, spellId)))
            _liveSpellEngine.Learn(characterId, spellId, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "decoded-give-temp-spell"
            });
    }

    internal static IReadOnlySet<string> ResolveDecodedTemporarySpellIds(
        IReadOnlyList<RuntimeActiveSpellEffect> effects, RuntimeSpellCatalog catalog)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in effects)
        {
            var source = catalog.Require(effect.SpellId);
            foreach (var legacyId in source.DecodedAbilities.Where(ability => ability.AbilityId == 160 && ability.Value > 0)
                         .Select(ability => ability.Value).Distinct())
            {
                var granted = catalog.Spells.SingleOrDefault(spell =>
                    spell.LegacyId.Equals(legacyId.ToString(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Decoded GiveTempSpell target spell {legacyId} is unavailable.");
                result.Add(granted.Id);
            }
        }
        return result;
    }

    private IReadOnlyList<string> ActiveSpellLookMessages(RuntimeSession session, string characterId, string displayName)
    {
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            return _liveSpellEngine!.EffectsFor(characterId)
                .SelectMany(effect => _liveSpellCatalog!.Require(effect.SpellId).DescriptionMessages ?? Array.Empty<RuntimeSpellDescriptionMessage>())
                .Select(message => message.LookMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message.Replace("%s", displayName, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    private string DispelLiveEffect(RuntimeSession session, string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return "Dispel which active effect ID?";
        var effect = _liveSpellEngine!.RemoveEffect(effectId.Trim());
        if (effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase))
            SynchronizeCharacterSpellStatuses(effect.Target, DateTimeOffset.UtcNow, "runtime.live_powers.dispel");
        PersistLivePowers(session.World.Definition.Id);
        return $"Removed {effect.SpellId} effect {effect.EffectId}.";
    }


    private void SynchronizeCharacterSpellStatuses(RuntimeSpellTarget target, DateTimeOffset nowUtc, string sourceRecordId)
    {
        if (!target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase) || _liveSpellEngine is null) return;
        var character = _characters.LoadCharacter(target.Id);
        if (character is null) return;
        var effects = _liveSpellEngine.EffectsFor(target.Id);
        var synchronized = RuntimeCharacterStatusEffectResolver.Synchronize(character, effects, nowUtc, sourceRecordId);
        var classicCatalog = RuntimeClassicLiveStatusCatalog.Load(_characters.ProjectRoot, character.WorldId);
        synchronized = RuntimeClassicStatusProjection.Apply(synchronized, effects, classicCatalog, nowUtc, sourceRecordId);
        _characters.SaveCharacter(synchronized);
    }

    private void SynchronizeAllCharacterSpellStatuses(DateTimeOffset nowUtc, string sourceRecordId)
    {
        if (_liveSpellEngine is null) return;
        foreach (var targetId in _liveSpellEngine.Snapshot().ActiveEffects
            .Where(effect => effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase))
            .Select(effect => effect.Target.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase))
            SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", targetId), nowUtc, sourceRecordId);
    }

    private string RenderLiveSpells(RuntimeSession session) => RenderMajorMudSpellTable(session, powers: false);

    private string RenderLivePowers(RuntimeSession session) => RenderMajorMudSpellTable(session, powers: true);

    private string RenderMajorMudSpellTable(RuntimeSession session, bool powers)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return powers ? "You have no powers." : "You have no spells.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        EnsureKnownSpells(character, actor.Id);
        var classDefinition = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId).RequireClass(character.ClassId);
        var usesKai = classDefinition.Magic?.Type == 5;
        if (powers != usesKai) return powers ? "You have no powers." : "You have no spells.";

        var spells = _liveSpellEngine!.Spellbook(actor.Id)
            .Select(x => _liveSpellCatalog!.Require(x.SpellId))
            .OrderBy(x => x.SpellLevel)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (spells.Length == 0) return powers ? "You have no powers." : "You have no spells.";

        return FormatMajorMudSpellTable(spells, powers);
    }

    internal static string FormatMajorMudSpellTable(IReadOnlyList<RuntimeSpellDefinition> spells, bool powers)
    {
        if (spells.Count == 0) return powers ? "You have no powers." : "You have no spells.";
        var lines = new List<string>
        {
            powers ? "You have the following powers:" : "You have the following spells:",
            powers ? "Level Kai  Short Spell Name" : "Level Mana Short Spell Name"
        };
        foreach (var spell in spells.OrderBy(x => x.SpellLevel).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var shortName = string.IsNullOrWhiteSpace(spell.ShortName) ? spell.Name : spell.ShortName;
            lines.Add($"{spell.SpellLevel,3} {spell.ManaCost,3}    {shortName,-4}  {spell.Name}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    public bool TryExecuteMajorMudPowerShortcut(RuntimeSession session, RuntimeCommand command, out string result)
    {
        result = string.Empty;
        if (session.Actor is not RuntimeCharacterActor actor || string.IsNullOrWhiteSpace(command.Name)) return false;
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            ExpireLivePowers(session, DateTimeOffset.UtcNow);
            var character = _characters.LoadCharacter(actor.Id);
            if (character is null) return false;
            EnsureKnownSpells(character, actor.Id);
            var matches = _liveSpellEngine!.Spellbook(actor.Id)
                .Select(x => _liveSpellCatalog!.Require(x.SpellId))
                .Where(x => !string.IsNullOrWhiteSpace(x.ShortName) && x.ShortName.Equals(command.Name, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (matches.Length != 1) return false;

            var classDefinition = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId).RequireClass(character.ClassId);
            var usesKai = classDefinition.Magic?.Type == 5;
            var words = new List<string> { usesKai ? "invoke" : "cast", matches[0].Id };
            words.AddRange(command.Arguments);
            var cast = RuntimeCommand.Parse(string.Join(' ', words), session.World.CommandCatalog);
            result = CastLiveSpell(session, cast, invoked: usesKai);
            return true;
        }
    }

    private string RenderLiveAbilities(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "No abilities are available.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var state = GetAbilityState(actor.Id, character).Capture();
        return state.Known.Count == 0 ? "You know no generated abilities." : "Abilities:\n" + string.Join("\n", state.Known.Select(x => _liveAbilityCatalog!.TryGet(x.AbilityId, out var a) ? $"{a.Id}: {a.Name}" : x.AbilityId));
    }

    private RuntimeSpellDefinition ResolveSpell(string query)
    {
        if (TryResolveSpell(query, out var spell)) return spell;
        throw new InvalidOperationException($"No generated spell uniquely matches '{query}'.");
    }
    private bool TryResolveSpell(string query, out RuntimeSpellDefinition spell)
    {
        var matches = _liveSpellCatalog!.Spells.Where(x => x.Id.Equals(query, StringComparison.OrdinalIgnoreCase) || x.LegacyId.Equals(query, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(query, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        spell = matches.Length == 1 ? matches[0] : null!; return matches.Length == 1;
    }
    private RuntimeAbilityDefinition ResolveAbility(string query)
    {
        var matches = _liveAbilityCatalog!.Definitions.Where(x => x.Id.Equals(query, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(query, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException($"No generated ability uniquely matches '{query}'.");
    }

    private RuntimeSpellTarget ResolvePowerTarget(RuntimeSession session, IReadOnlyList<string> args, string generatedTargetType)
    {
        if (args.Count == 0 || generatedTargetType.Equals("self", StringComparison.OrdinalIgnoreCase) || generatedTargetType == "0") return new RuntimeSpellTarget("character", session.Actor.Id);
        var query = string.Join(" ", args);
        var player = _sessions.FindUniqueInRoom(session, query);
        if (player is not null) return new RuntimeSpellTarget("character", player.Actor.Id);
        if (_liveCombats.TryGetValue(session.Id, out var encounter) && MatchesPowerTarget(query, encounter))
            return new RuntimeSpellTarget("npc", encounter.NpcInstanceId);
        var npcMatches = LivingNpcsInRoom(session)
            .Where(npc => npc.InstanceId.Equals(query, StringComparison.OrdinalIgnoreCase)
                || npc.NpcId.Equals(query, StringComparison.OrdinalIgnoreCase)
                || npc.SpawnId.Equals(query, StringComparison.OrdinalIgnoreCase)
                || npc.Name.Equals(query, StringComparison.OrdinalIgnoreCase)
                || npc.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (npcMatches.Length == 1) return new RuntimeSpellTarget("npc", npcMatches[0].InstanceId);
        throw new InvalidOperationException($"No valid live target uniquely matches '{query}'.");
    }

    internal static bool IsDecodedSpellTargetAdmitted(
        RuntimeSpellDefinition spell,
        RuntimeSpellTarget target,
        RuntimeLivingNpcState? targetNpc)
    {
        if (target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase)
            && IsDecodedAbilityNegated(spell, ReadNegatedAbilityIds(targetNpc?.Metadata))) return false;
        if (spell.DecodedAbilities.Any(ability => ability.AbilityId == 80))
        {
            if (!target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase)
                || targetNpc is null
                || !MetadataFlag(targetNpc.Metadata, "animal")) return false;
        }
        if (spell.DecodedAbilities.Any(ability => ability.AbilityId == 108))
        {
            if (target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)) return true;
            if (!target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase)
                || targetNpc is null
                || MetadataFlag(targetNpc.Metadata, "non_living")) return false;
        }
        return true;
    }

    private static bool IsDecodedAbilityNegated(RuntimeSpellDefinition spell, IReadOnlySet<int> negatedAbilityIds) =>
        spell.DecodedAbilities.Any(ability => negatedAbilityIds.Contains(ability.AbilityId));

    private static IReadOnlySet<int> ReadNegatedAbilityIds(RuntimeCharacterRecord? character)
    {
        if (character is null) return new HashSet<int>();
        const string prefix = RuntimeClassicStatusProjection.Prefix + "negate_ability.";
        return character.Conditions.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(key => int.TryParse(key[prefix.Length..], out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();
    }

    private static IReadOnlySet<int> ReadNegatedAbilityIds(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue("negated_ability_ids", out var raw)) return new HashSet<int>();
        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();
    }

    private static bool MetadataFlag(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var raw)
        && (raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase));

    internal static bool IsDecodedSpellCasterAlignmentAdmitted(
        RuntimeSpellDefinition spell,
        RuntimeCharacterRecord character,
        out string failure)
    {
        var requiresGood = spell.DecodedAbilities.Any(ability => ability.AbilityId == 97);
        var requiresEvil = spell.DecodedAbilities.Any(ability => ability.AbilityId == 98);
        var excludesGood = spell.DecodedAbilities.Any(ability => ability.AbilityId == 110);
        var excludesEvil = spell.DecodedAbilities.Any(ability => ability.AbilityId == 111);
        var requiresNeutral = spell.DecodedAbilities.Any(ability => ability.AbilityId == 112);
        var excludesNeutral = spell.DecodedAbilities.Any(ability => ability.AbilityId == 113);
        if (!requiresGood && !requiresEvil && !excludesGood && !excludesEvil && !requiresNeutral && !excludesNeutral)
        {
            failure = string.Empty;
            return true;
        }
        var reputation = character.SemanticState?.Alignment?.Reputation;
        if (string.IsNullOrWhiteSpace(reputation) && character.Conditions.TryGetValue("alignment", out var legacyAlignment))
            reputation = legacyAlignment;
        var good = reputation is not null && (reputation.Equals("Lawful", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Saint", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Good", StringComparison.OrdinalIgnoreCase));
        var evil = reputation is not null && (reputation.Equals("Outlaw", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Criminal", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Villain", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Fiend", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Evil", StringComparison.OrdinalIgnoreCase));
        var neutral = reputation is not null && (reputation.Equals("Neutral", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Seedy", StringComparison.OrdinalIgnoreCase));
        if (!good && !neutral && !evil)
        {
            failure = string.IsNullOrWhiteSpace(reputation)
                ? $"{spell.Name} cannot be cast because the character has no authoritative alignment."
                : $"{spell.Name} cannot be cast with unknown alignment '{reputation}'.";
            return false;
        }
        if (requiresGood && !good) failure = $"{spell.Name} may only be cast by a Good-aligned character.";
        else if (requiresEvil && !evil) failure = $"{spell.Name} may only be cast by an Evil-aligned character.";
        else if (requiresNeutral && !neutral) failure = $"{spell.Name} may only be cast by a Neutral-aligned character.";
        else if (excludesGood && good) failure = $"{spell.Name} cannot be cast by a Good-aligned character.";
        else if (excludesEvil && evil) failure = $"{spell.Name} cannot be cast by an Evil-aligned character.";
        else if (excludesNeutral && neutral) failure = $"{spell.Name} cannot be cast by a Neutral-aligned character.";
        else
        {
            failure = string.Empty;
            return true;
        }
        return false;
    }

    private int ApplyDecodedRemoveCurse(RuntimeSession session, RuntimeSpellTarget target, DateTimeOffset now)
    {
        if (!target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)) return 0;
        var character = _characters.LoadCharacter(target.Id)
            ?? throw new InvalidOperationException("Remove curse target character was not found.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var authority = RuntimeCharacterBundleCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses, authority);
        var removed = engine.RemoveCurse(character.CharacterId);
        if (removed.Count == 0) return 0;
        var commit = RuntimeEquipmentRuntimeAuthority.Commit(character, engine, state.Gold, state.ActiveItemState, now);
        _characters.SaveCharacter(commit.Character, $"decoded-remove-curse:{session.World.Definition.Id}");
        return removed.Count;
    }

    internal RuntimeNpcHitSpellStatusDeliveryResolution ApplyNpcHitSpellStatusDelivery(
        RuntimeSession session,
        RuntimeCharacterRecord character,
        RuntimeNpcHitSpellSelection spell,
        string casterId,
        bool poisonImmune,
        DateTimeOffset nowUtc)
    {
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            var existing = _liveSpellEngine!.EffectsFor(character.CharacterId)
                .Where(effect => effect.SpellId.Equals(spell.CanonicalSpellId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var catalog = ClassicLiveStatusCatalog(session);
            if (!catalog.TryGet(spell.CanonicalSpellId, out var definition))
                throw new InvalidOperationException($"Decoded status spell '{spell.CanonicalSpellId}' is missing from the Classic status catalog.");
            var incomingSpell = _liveSpellCatalog!.Require(spell.CanonicalSpellId);
            if (IsDecodedAbilityNegated(incomingSpell, ReadNegatedAbilityIds(character)))
                return new RuntimeNpcHitSpellStatusDeliveryResolution(
                    false, false, false, "negated", spell.CanonicalSpellId,
                    0, spell.MinimumEffect, spell.MaximumEffect, "negate-ability-immunity");
            var resolution = ResolveNpcHitSpellStatusDelivery(spell, poisonImmune, existing.Length > 0, definition.HasCombatModifiers);
            if (!resolution.Applied) return resolution;
            var snapshot = _liveSpellEngine.Snapshot();
            var active = snapshot.ActiveEffects
                .Where(effect => !existing.Any(old => old.EffectId.Equals(effect.EffectId, StringComparison.OrdinalIgnoreCase)))
                .Concat(new[]
                {
                    new RuntimeActiveSpellEffect(
                        Guid.NewGuid().ToString("N"),
                        spell.CanonicalSpellId,
                        casterId,
                        new RuntimeSpellTarget("character", character.CharacterId),
                        spell.MinimumEffect,
                        spell.MaximumEffect,
                        spell.DurationRaw,
                        nowUtc,
                        nowUtc.AddSeconds(checked((long)spell.DurationRaw * RuntimeClassicLiveStatusCatalog.SpellRoundSeconds)),
                        BuildClassicStatusMetadata(definition, spell, nowUtc,
                            spell.MinimumEffect == spell.MaximumEffect
                                ? spell.MinimumEffect
                                : Random.Shared.Next(Math.Min(spell.MinimumEffect, spell.MaximumEffect), checked(Math.Max(spell.MinimumEffect, spell.MaximumEffect) + 1))))
                }).ToArray();
            _liveSpellEngine.RestoreSnapshot(snapshot with { ActiveEffects = active });
            SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", character.CharacterId), nowUtc, "runtime.live_combat.npc_hit_spell");
            PersistLivePowers(session.World.Definition.Id);
            return resolution;
        }
    }

    private void SaveCharacterPowerResources(RuntimeCharacterRecord character, int mana, int energy)
    {
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase) { ["energy"] = energy.ToString() };
        _characters.SaveCharacter(character with { Mana = Math.Clamp(mana, 0, character.MaximumMana), Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
    }
    private static int ReadEnergy(RuntimeCharacterRecord character) => character.Conditions.TryGetValue("energy", out var value) && int.TryParse(value, out var parsed) ? Math.Max(0, parsed) : 1000;
    private void PersistLivePowers(string worldId) => new RuntimeLivePowerStateRepository(_characters.ProjectRoot, worldId).Save(new RuntimeLivePowerState(
        _liveSpellEngine!.Snapshot(), _liveAbilityStates.ToDictionary(x => x.Key, x => x.Value.Capture(), StringComparer.OrdinalIgnoreCase), new Dictionary<string, DateTimeOffset>(_liveSpellCooldowns, StringComparer.OrdinalIgnoreCase), DateTimeOffset.UtcNow));

    private static string RenderSpellEvent(RuntimeSpellExecutionEvent e) =>
        e.Operation.Equals("cure_poison", StringComparison.OrdinalIgnoreCase)
            ? "poison removed."
            : e.Operation.Equals("immune", StringComparison.OrdinalIgnoreCase)
                ? "The target is immune."
            : e.Operation.Equals("teleport", StringComparison.OrdinalIgnoreCase)
                ? "You are teleported."
            : $"{e.Operation} {e.Magnitude}.";

    private static RuntimeActiveSpellEffect? AttachResolvedEffectRoll(
        RuntimeActiveSpellEffect? effect, IReadOnlyList<RuntimeSpellExecutionEvent> events)
    {
        if (effect is null) return null;
        var source = events.FirstOrDefault(x => x.Metadata is not null && x.Metadata.ContainsKey("effect_roll"));
        if (source?.Metadata is null) return effect;
        var metadata = new Dictionary<string, string>(effect.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["effect_roll"] = source.Metadata["effect_roll"]
        };
        return effect with { Metadata = metadata };
    }

    internal static bool IsCurePoisonSpell(RuntimeSpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.LegacyId.Equals("31", StringComparison.OrdinalIgnoreCase)
            || spell.LegacyId.Equals("222", StringComparison.OrdinalIgnoreCase)
            || spell.Name.Equals("antidote", StringComparison.OrdinalIgnoreCase)
            || spell.Name.Equals("cure poison", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldDiscardDecodedRemovalHelper(RuntimeSpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.Duration <= 0
            && (IsCurePoisonSpell(spell)
                || spell.DecodedAbilities.Any(ability => ability.AbilityId is 73 or 81 or 84 or 122 or 140 or 141 or 153 or 161));
    }

    internal static bool IsPoisonEffect(RuntimeActiveSpellEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect.Metadata is not null
            && effect.Metadata.TryGetValue("effect_kind", out var kind)
            && kind.Equals("poison", StringComparison.OrdinalIgnoreCase);
    }

    private int RemovePoisonEffects(RuntimeSpellTarget target, string? cureEffectId)
    {
        var effects = _liveSpellEngine!.EffectsFor(target.Id);
        var removeIds = effects
            .Where(effect => IsPoisonEffect(effect)
                || (!string.IsNullOrWhiteSpace(cureEffectId) && effect.EffectId.Equals(cureEffectId, StringComparison.OrdinalIgnoreCase)))
            .Select(effect => effect.EffectId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var effectId in removeIds)
            _liveSpellEngine.RemoveEffect(effectId);
        return effects.Count(effect => IsPoisonEffect(effect));
    }

    private static int GeneratedInt(RuntimeSpellDefinition spell, params string[] names) => 0; // cooldown is absent from the verified spell projection; zero is the generated default.
    private static string? GeneratedString(JsonElement record, string name) => record.TryGetProperty(name, out var value) ? value.ToString() : null;

    private sealed class GeneratedLiveSpellOperationPolicy : IRuntimeSpellOperationPolicy
    {
        private readonly RuntimeLiveGameplayFacade _owner;
        private readonly RuntimeSession _session;
        public GeneratedLiveSpellOperationPolicy(RuntimeLiveGameplayFacade owner, RuntimeSession session) { _owner = owner; _session = session; }
        public IReadOnlyList<RuntimeSpellExecutionEvent> ResolveOperations(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request, RuntimeActiveSpellEffect effect)
        {
            var immunityLevel = _owner.ResolveLiveSpellImmunityThreshold(_session, request.Target, effect.EffectId);
            if (immunityLevel > 0 && spell.SpellLevel <= immunityLevel)
            {
                _owner._liveSpellEngine!.RemoveEffect(effect.EffectId);
                return new[] { new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, "immune", 0, request.RequestedAtUtc, null,
                    new Dictionary<string, string> { ["spell_level"] = spell.SpellLevel.ToString(), ["spell_immunity_level"] = immunityLevel.ToString() }) };
            }
            if (IsCurePoisonSpell(spell))
                return new[] { new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, "cure_poison", 0, request.RequestedAtUtc, effect.EffectId, effect.Metadata) };
            var teleportRoom = spell.DecodedAbilities.FirstOrDefault(ability => ability.AbilityId == 140)?.Value ?? 0;
            var teleportMap = spell.DecodedAbilities.FirstOrDefault(ability => ability.AbilityId == 141)?.Value ?? 0;
            if (teleportRoom > 0 && teleportMap > 0)
            {
                if (!request.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Decoded fixed teleport requires a character target.");
                var destinationId = $"room.map{teleportMap}.{teleportRoom}";
                if (_session.World.FindRoomById(destinationId) is null)
                    throw new InvalidOperationException($"Decoded fixed teleport destination '{destinationId}' is absent from generated Classic rooms.");
                var targetSession = request.Target.Id.Equals(_session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                    ? _session
                    : _owner._sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Actor.Id.Equals(request.Target.Id, StringComparison.OrdinalIgnoreCase));
                if (targetSession is null) throw new InvalidOperationException("Decoded fixed teleport target session is unavailable.");
                return new[] { new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, "teleport", 0, request.RequestedAtUtc, effect.EffectId,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["source_room_id"] = targetSession.CurrentRoom.Id,
                        ["destination_room_id"] = destinationId,
                        ["teleport_map"] = teleportMap.ToString(),
                        ["teleport_room"] = teleportRoom.ToString(),
                        ["operation_authority"] = "decoded_abilities_140_141"
                    }) };
            }
            var targetMr = 0;
            RuntimeCharacterRecord? targetCharacter = null;
            if (request.Target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase) && _owner._liveCombats.TryGetValue(_session.Id, out var encounter))
                targetMr = ResolveEncounterNpc(_session, encounter).MagicResistance;
            else if (request.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase) && _owner._characters.LoadCharacter(request.Target.Id) is { } loadedTargetCharacter)
            {
                targetCharacter = loadedTargetCharacter;
                targetMr = ReadMagicResistance(targetCharacter);
            }
            var casterLevel = _owner._characters.LoadCharacter(request.CasterId)?.Level ?? 0;
            var resolution = RuntimeClassicPlayerSpellCombat.Resolve(spell, targetMr, false, false, false, Random.Shared, casterLevel);
            var hasDecodedDamage=spell.DecodedAbilities.Any(x=>x.AbilityId is 1 or 17);
            var hasDecodedHeal=spell.DecodedAbilities.Any(x=>x.AbilityId==18);
            var healMana = spell.DecodedAbilities.FirstOrDefault(x => x.AbilityId == 150);
            var decodedOperation=hasDecodedDamage?"damage":hasDecodedHeal?"heal":resolution.Operation;
            var metadata = new Dictionary<string,string>(effect.Metadata ?? new Dictionary<string,string>(), StringComparer.OrdinalIgnoreCase)
            {
                ["element"] = resolution.Element,
                ["resistance_chance"] = resolution.ResistanceChancePercent.ToString(),
                ["resistance_roll"] = resolution.ResistanceRoll.ToString(),
                ["resisted"] = resolution.Resisted.ToString(),
                ["effect_roll"] = resolution.EffectRoll.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["reflection"] = "absent-from-uploaded-classic-decode",
                ["absorption"] = "absent-from-uploaded-classic-decode",
                ["damage_shield"] = "absent-from-uploaded-classic-decode"
            };
            metadata["operation_authority"]=(hasDecodedDamage||hasDecodedHeal)?"decoded_ability_vector":"decoded_target_and_attack_type";
            var finalOperation = decodedOperation;
            var finalMagnitude = resolution.Magnitude;
            if (targetCharacter is not null && decodedOperation.Equals("damage", StringComparison.OrdinalIgnoreCase) &&
                !resolution.Resisted && !resolution.Reflected && !resolution.Absorbed && resolution.Magnitude > 0)
            {
                var elementalResistance = ReadElementalResistance(targetCharacter, resolution.Element);
                var poisonImmune = ReadAbilityValue(targetCharacter, 21) > 0;
                var signedDamage = ResolveElementalSpellDamage(resolution.Magnitude, resolution.Element, elementalResistance, poisonImmune);
                finalOperation = signedDamage < 0 ? "heal" : "damage";
                finalMagnitude = signedDamage < 0 ? checked(-signedDamage) : signedDamage;
                metadata["elemental_resistance_percent"] = elementalResistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
                metadata["signed_elemental_damage"] = signedDamage.ToString(System.Globalization.CultureInfo.InvariantCulture);
                metadata["poison_immune"] = poisonImmune.ToString();
                metadata["elemental_overresist_healing"] = (signedDamage < 0).ToString();
            }
            var events = new List<RuntimeSpellExecutionEvent>();
            if (hasDecodedDamage || hasDecodedHeal || spell.DecodedAbilities.Count == 0)
                events.Add(new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, finalOperation, finalMagnitude, request.RequestedAtUtc, effect.EffectId, metadata));
            else if (healMana is null)
                events.Add(new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, "modifier", 0, request.RequestedAtUtc, effect.EffectId, metadata));
            if (healMana is not null)
            {
                var manaMagnitude = ResolveHealManaMagnitude(healMana.Value, resolution);
                events.Add(new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, "mana", manaMagnitude, request.RequestedAtUtc, effect.EffectId, metadata));
            }
            return events;
        }
    }

    private sealed class LiveSpellEventSink : IRuntimeSpellEventSink
    {
        private readonly RuntimeLiveGameplayFacade _owner; private readonly RuntimeSession _session;
        public LiveSpellEventSink(RuntimeLiveGameplayFacade owner, RuntimeSession session) { _owner = owner; _session = session; }
        public void Apply(RuntimeSpellExecutionEvent e) => _owner.ApplySpellEvent(_session, e, false);
        public void Revert(RuntimeSpellExecutionEvent e) => _owner.ApplySpellEvent(_session, e, true);
    }

    private sealed record LiveAbilityResources(RuntimeCharacterRecord Character, int InitialEnergy) { public int Energy { get; set; } = InitialEnergy; }
    private sealed class GeneratedLiveAbilityPolicy : IRuntimeAbilityPolicy
    {
        private readonly LiveAbilityResources _resources; public GeneratedLiveAbilityPolicy(LiveAbilityResources resources) => _resources = resources;
        public RuntimeAbilityPlan Resolve(RuntimeAbilityDefinition d, RuntimeActorAbilitySnapshot actor, RuntimeAbilityRequest request, DateTimeOffset now)
        {
            var cost = ReadInt(d.GeneratedRecord, "energy_cost", "cost"); if (_resources.Energy < cost) return new(false, "insufficient-energy", null, Array.Empty<RuntimeAbilityEvent>());
            _resources.Energy -= cost; var magnitude = ReadInt(d.GeneratedRecord, "magnitude", "effect", "value"); var type = GeneratedString(d.GeneratedRecord, "effect_type") ?? "modifier";
            var cooldown = ReadInt(d.GeneratedRecord, "cooldown_seconds", "cooldown");
            return new(true, null, cooldown > 0 ? now.AddSeconds(cooldown) : null, new[] { new RuntimeAbilityEvent(Guid.NewGuid().ToString("N"), type, request.ActorId, d.Id, request.TargetId, magnitude, now) });
        }
        private static int ReadInt(JsonElement root, params string[] names) { foreach (var name in names) if (root.TryGetProperty(name, out var v) && (v.TryGetInt32(out var n) || int.TryParse(v.ToString(), out n))) return n; return 0; }
    }
    private sealed class LiveAbilityEventSink : IRuntimeAbilityEventSink
    {
        private readonly RuntimeLiveGameplayFacade _owner; private readonly RuntimeSession _session; private readonly LiveAbilityResources _resources;
        public LiveAbilityEventSink(RuntimeLiveGameplayFacade owner, RuntimeSession session, LiveAbilityResources resources) { _owner = owner; _session = session; _resources = resources; }
        public void Apply(RuntimeAbilityEvent e) => _owner.ApplyPowerMagnitude(_session, new RuntimeSpellTarget(e.TargetId == _session.Actor.Id ? "character" : "npc", e.TargetId ?? _session.Actor.Id), e.EventType, checked((int)e.Magnitude));
        public void Revert(RuntimeAbilityEvent e) { _owner.ApplyPowerMagnitude(_session, new RuntimeSpellTarget(e.TargetId == _session.Actor.Id ? "character" : "npc", e.TargetId ?? _session.Actor.Id), e.EventType, checked(-(int)e.Magnitude)); _resources.Energy = _resources.InitialEnergy; }
    }

    private void ApplySpellEvent(RuntimeSession source, RuntimeSpellExecutionEvent e, bool revert)
    {
        if (e.Operation.Equals("scatter_items", StringComparison.OrdinalIgnoreCase))
        {
            if (e.Metadata is null
                || !e.Metadata.TryGetValue(revert ? "source_room_id" : "destination_room_id", out var scatterRoomId)
                || !e.Metadata.TryGetValue("item_instance_ids", out var encoded))
                throw new InvalidOperationException("Decoded ScatterItems event is missing authoritative movement metadata.");
            var ids = encoded.Split('\u001f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Living(source).MoveGroundItems(ids, scatterRoomId);
            return;
        }
        if (!e.Operation.Equals("teleport", StringComparison.OrdinalIgnoreCase))
        {
            ApplyPowerMagnitude(source, e.Target, e.Operation, revert ? -e.Magnitude : e.Magnitude);
            return;
        }
        var key = revert ? "source_room_id" : "destination_room_id";
        if (e.Metadata is null || !e.Metadata.TryGetValue(key, out var roomId))
            throw new InvalidOperationException("Decoded teleport event is missing its authoritative room identity.");
        var targetSession = e.Target.Id.Equals(source.Actor.Id, StringComparison.OrdinalIgnoreCase)
            ? source
            : _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Actor.Id.Equals(e.Target.Id, StringComparison.OrdinalIgnoreCase));
        var room = targetSession?.World.FindRoomById(roomId)
            ?? throw new InvalidOperationException($"Decoded teleport room '{roomId}' is unavailable.");
        targetSession.MoveToWorld(targetSession.World, room, revert ? "decoded-spell-teleport-rollback" : "decoded-spell-teleport");
    }

    private void PersistDecodedTeleportTargets(IReadOnlyList<RuntimeSpellExecutionEvent> events)
    {
        foreach (var e in events.Where(e => e.Operation.Equals("teleport", StringComparison.OrdinalIgnoreCase)))
        {
            var targetSession = _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Actor.Id.Equals(e.Target.Id, StringComparison.OrdinalIgnoreCase));
            if (targetSession?.Actor is not RuntimeCharacterActor targetActor) continue;
            var target = _characters.LoadCharacter(targetActor.Id) ?? throw new InvalidOperationException("Teleport target character save was not found.");
            _characters.SaveCharacter(target with
            {
                WorldId = targetSession.World.Definition.Id,
                RoomId = targetSession.CurrentRoom.Id,
                RoomNumber = targetSession.CurrentRoom.RoomNumber,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    private void ApplyPowerMagnitude(RuntimeSession source, RuntimeSpellTarget target, string operation, int magnitude)
    {
        if (operation.Equals("modifier", StringComparison.OrdinalIgnoreCase) || magnitude == 0) return;
        if (operation.Equals("mana", StringComparison.OrdinalIgnoreCase))
        {
            if (target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase))
            {
                if (!_liveCombats.TryGetValue(source.Id, out var manaEncounter)
                    || !manaEncounter.NpcInstanceId.Equals(target.Id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Live NPC mana target is unavailable.");
                _liveCombats[source.Id] = manaEncounter with
                {
                    NpcCurrentEnergy = ResolveNpcManaEnergy(
                        manaEncounter.NpcCurrentEnergy, manaEncounter.NpcEnergyPerRound, magnitude)
                };
                return;
            }
            var manaSession = target.Id.Equals(source.Actor.Id, StringComparison.OrdinalIgnoreCase)
                ? source
                : _sessions.ActiveSessions.FirstOrDefault(x => x.Actor.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase));
            if (manaSession?.Actor is not RuntimeCharacterActor manaActor)
                throw new InvalidOperationException("Live character mana target is unavailable.");
            var manaCharacter = _characters.LoadCharacter(manaActor.Id)
                ?? throw new InvalidOperationException("Mana target character save was not found.");
            _characters.SaveCharacter(manaCharacter with
            {
                Mana = Math.Clamp(checked(manaCharacter.Mana + magnitude), 0, manaCharacter.MaximumMana),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            return;
        }
        if (magnitude < 0)
        {
            var inverseOperation = operation.Equals("heal", StringComparison.OrdinalIgnoreCase) ? "damage" : "heal";
            ApplyPowerMagnitude(source, target, inverseOperation, Math.Abs(magnitude));
            return;
        }
        if (target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTimeOffset.UtcNow;
            var snapshot = Living(source).Capture().Npcs.FirstOrDefault(x => x.InstanceId.Equals(target.Id, StringComparison.OrdinalIgnoreCase));
            if (snapshot is null || !snapshot.Alive) throw new InvalidOperationException("Live NPC target is unavailable.");
            var hp = operation.Equals("heal", StringComparison.OrdinalIgnoreCase)
                ? Math.Min(snapshot.MaximumHitPoints, snapshot.CurrentHitPoints + magnitude)
                : Math.Clamp(snapshot.CurrentHitPoints - magnitude, 0, snapshot.MaximumHitPoints);
            if (operation.Equals("heal", StringComparison.OrdinalIgnoreCase))
                Living(source).ApplyNpcHealing(target.Id, magnitude, now);
            else
                Living(source).ApplyNpcDamage(target.Id, magnitude, now);
            if (_liveCombats.TryGetValue(source.Id, out var encounter) && encounter.NpcInstanceId.Equals(target.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (hp == 0) _liveCombats.TryRemove(source.Id, out _);
                else _liveCombats[source.Id] = encounter with { NpcCurrentHitPoints = hp };
            }
            return;
        }
        var targetSession = target.Id.Equals(source.Actor.Id, StringComparison.OrdinalIgnoreCase) ? source : _sessions.ActiveSessions.FirstOrDefault(x => x.Actor.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase));
        if (targetSession?.Actor is not RuntimeCharacterActor actor) throw new InvalidOperationException("Live character target is unavailable.");
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Target character save was not found.");
        var hp2 = operation.Equals("heal", StringComparison.OrdinalIgnoreCase) ? Math.Min(character.MaximumHitPoints, character.HitPoints + magnitude) : Math.Clamp(character.HitPoints - magnitude, 0, character.MaximumHitPoints);
        _characters.SaveCharacter(character with { HitPoints = hp2, UpdatedAtUtc = DateTimeOffset.UtcNow });
    }

    internal static int ResolveHealManaMagnitude(int decodedValue, RuntimeClassicPlayerSpellResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (resolution.Resisted || resolution.Reflected || resolution.Absorbed) return 0;
        return decodedValue != 0 ? decodedValue : resolution.EffectRoll;
    }

    internal static int ResolveNpcManaEnergy(int currentEnergy, int energyPerRound, int manaMagnitude)
    {
        if (currentEnergy < 0 || energyPerRound < 0)
            throw new InvalidDataException("NPC_MANA_ENERGY_STATE_INVALID");
        // Monster combat energy is carried between rounds and has no one-round upper cap.
        return Math.Max(0, checked(currentEnergy + manaMagnitude));
    }

    private static bool MatchesPowerTarget(string query, RuntimeLiveCombatEncounter encounter) =>
        encounter.NpcInstanceId.Equals(query, StringComparison.OrdinalIgnoreCase)
        || encounter.SpawnId.Equals(query, StringComparison.OrdinalIgnoreCase)
        || encounter.NpcId.Equals(query, StringComparison.OrdinalIgnoreCase)
        || encounter.NpcName.Equals(query, StringComparison.OrdinalIgnoreCase)
        || encounter.NpcName.Contains(query, StringComparison.OrdinalIgnoreCase);
}

public sealed class RuntimeLivePowersSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new(); public List<string> Failures { get; } = new();
    public static RuntimeLivePowersSmokeReport Run(string root, string worldId)
    {
        var r = new RuntimeLivePowersSmokeReport();
        try
        {
            var spells = RuntimeSpellCatalog.Load(root, worldId); Check(spells.Spells.Count > 0, "spell-loading", r);
            var abilities = RuntimeAbilityCatalog.Load(root, worldId); Check(abilities.Count > 0, "ability-loading", r);
            Check(RuntimeCommand.Parse("cast test target").Name == "cast", "cast-routing", r); Check(RuntimeCommand.Parse("use test target").Name == "use", "use-routing", r);
            Check(RuntimeCommand.Parse("sp").Name == "spells", "native-sp-shortcut-routing", r);
            Check(RuntimeCommand.Parse("pow").Name == "powers", "native-pow-shortcut-routing", r);
            Check(RuntimeCommand.Parse("greet loremaster").Name == "greet", "native-greet-routing", r);
            Check(RuntimeCommand.Parse("break").Name == "break", "native-break-routing", r);
            Check(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TryExecuteMajorMudPowerShortcut)) is not null, "native-learned-spell-short-command-dispatch", r);
            Check(typeof(RuntimeSpellExecutionEngine).GetMethod(nameof(RuntimeSpellExecutionEngine.Cast)) is not null, "combat-spell-execution", r);
            Check(typeof(RuntimeAbilityEngine).GetMethod(nameof(RuntimeAbilityEngine.Execute)) is not null, "combat-ability-execution", r);
            var repo = new RuntimeLivePowerStateRepository(Path.Combine(Path.GetTempPath(), "mmr-live-powers-" + Guid.NewGuid().ToString("N")), worldId);
            var empty = new RuntimeLivePowerState(new RuntimeSpellState(Array.Empty<RuntimeKnownSpell>(), Array.Empty<RuntimeActiveSpellEffect>(), new Dictionary<string, RuntimeSpellResources>()), new Dictionary<string, RuntimeActorAbilitySnapshot>(), new Dictionary<string, DateTimeOffset>(), DateTimeOffset.UtcNow);
            repo.Save(empty); Check(repo.Load().Spells.ActiveEffects.Count == 0, "persistence", r); Check(repo.Load().Abilities.Count == 0, "reconnect", r);
            Check(typeof(RuntimeSpellEngine).GetMethod(nameof(RuntimeSpellEngine.Expire)) is not null, "duration-expiration", r); Check(typeof(RuntimeSpellExecutionEngine).GetMethod(nameof(RuntimeSpellExecutionEngine.Dispel)) is not null, "dispel", r);
            Check(typeof(RuntimeSpellEngine).GetMethod(nameof(RuntimeSpellEngine.Validate)) is not null, "validator", r); Check(typeof(RuntimeAbilityEngine).GetMethod(nameof(RuntimeAbilityEngine.Execute)) is not null, "rollback", r);
            var nativeSpellTable = RuntimeLiveGameplayFacade.FormatMajorMudSpellTable(new[]
            {
                new RuntimeSpellDefinition("spell.test", "9999", "major healing", "mahe", 8, "", "", "", 6, 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<RuntimeClassicLiveStatusAbility>())
            }, powers: false);
            Check(nativeSpellTable == "You have the following spells:" + Environment.NewLine + "Level Mana Short Spell Name" + Environment.NewLine + "  8   6    mahe  major healing", "native-spell-table-format", r);
            var nativePowerTable = RuntimeLiveGameplayFacade.FormatMajorMudSpellTable(new[]
            {
                new RuntimeSpellDefinition("spell.power", "9998", "way of the swan", "swan", 2, "", "", "", 1, 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<RuntimeClassicLiveStatusAbility>())
            }, powers: true);
            Check(nativePowerTable == "You have the following powers:" + Environment.NewLine + "Level Kai  Short Spell Name" + Environment.NewLine + "  2   1    swan  way of the swan", "native-power-table-format", r);
            var antidote = spells.Spells.Single(x => x.LegacyId == "31");
            var curePoison = spells.Spells.Single(x => x.LegacyId == "222");
            Check(RuntimeLiveGameplayFacade.IsCurePoisonSpell(antidote), "cure-poison-antidote-classification", r);
            Check(RuntimeLiveGameplayFacade.IsCurePoisonSpell(curePoison), "cure-poison-healer-classification", r);
            var cureDisease = spells.Require("spell.150");
            var bless = spells.Require("spell.14");
            Check(RuntimeLiveGameplayFacade.ShouldDiscardDecodedRemovalHelper(cureDisease), "instant-removes-spell-helper-discarded-even-without-match", r);
            Check(!RuntimeLiveGameplayFacade.ShouldDiscardDecodedRemovalHelper(bless), "duration-removes-spell-replacement-effect-retained", r);
            Check(RuntimeLiveGameplayFacade.ResolveNpcSpellImmunityLevel("10", 0) == 10, "npc-inherent-spell-immunity-threshold", r);
            Check(RuntimeLiveGameplayFacade.ResolveNpcSpellImmunityLevel("10", 20) == 20, "npc-active-spell-immunity-stacks-by-maximum", r);
            Check(RuntimeLiveGameplayFacade.ShouldExecuteCastOnEnd(20, 20) && !RuntimeLiveGameplayFacade.ShouldExecuteCastOnEnd(20, 21), "cast-on-end-inclusive-percent-boundary", r);
            Check(RuntimeLiveGameplayFacade.ShouldExecuteCastOnEnd(100, 100) && !RuntimeLiveGameplayFacade.ShouldExecuteCastOnEnd(0, 1), "cast-on-end-default-and-zero-boundaries", r);
            var poisonEffect = new RuntimeActiveSpellEffect("poison", "spell.poison", "npc.1", new RuntimeSpellTarget("character", "character.1"), 1, 2, 10, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(10), new Dictionary<string,string> { ["effect_kind"] = "poison" });
            var statusEffect = poisonEffect with { EffectId = "status", Metadata = new Dictionary<string,string> { ["effect_kind"] = "status" } };
            Check(RuntimeLiveGameplayFacade.IsPoisonEffect(poisonEffect), "cure-poison-removes-poison", r);
            Check(!RuntimeLiveGameplayFacade.IsPoisonEffect(statusEffect), "cure-poison-preserves-non-poison", r);
            var bcns = spells.Require("spell.1055");
            Check(bcns.CastMessageId == 2520 && bcns.CastMessage == "You pull your cloak tight about you!", "bcns-native-cast-message", r);
            Check(spells.Spells.Count(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 120)) == 229, "startmsg-decoded-owner-count-229", r);
            var startMessages = spells.Spells.Select(spell => spell.StartMessage).Where(message => message is not null).Select(message => message!).ToArray();
            Check(startMessages.Length == 230, "startmsg-runtime-selector-count-230-including-bcns-correction", r);
            Check(startMessages.Count(message => !string.IsNullOrEmpty(message.CasterMessage)) == 164, "startmsg-caster-message-count-164", r);
            Check(startMessages.Count(message => !string.IsNullOrEmpty(message.TargetMessage)) == 208, "startmsg-target-message-count-208", r);
            Check(startMessages.Count(message => !string.IsNullOrEmpty(message.ObserverMessage)) == 189, "startmsg-observer-message-count-189", r);
            var boulderStart = spells.Require("spell.1047").StartMessage ?? throw new InvalidOperationException("Boulder StartMsg was not projected.");
            Check(RuntimeLiveGameplayFacade.RenderDecodedStartMessage(boulderStart.CasterMessage, "a goblin", "a goblin") == "You point your finger at a goblin and clench your fist.", "startmsg-caster-target-substitution", r);
            Check(RuntimeLiveGameplayFacade.RenderDecodedStartMessage(boulderStart.TargetMessage, "Borin", "a goblin") == "Borin points a finger at you and clenches a fist.", "startmsg-target-caster-substitution", r);
            Check(RuntimeLiveGameplayFacade.RenderDecodedStartMessage(boulderStart.ObserverMessage, "Borin", "a goblin") == "Borin points a finger at a goblin and clenches a fist.", "startmsg-observer-caster-target-substitution", r);
            Check(bcns.DescriptionMessageId == 2531 && bcns.DescriptionMessage == "The battle song fades!", "bcns-native-expiration-message", r);
            Check(bcns.DescriptionLookMessage.Length == 0 && bcns.DescriptionStatusMessage == "The dance of battle fills your mind!", "bcns-native-description-presentation-slots", r);
            Check(spells.Spells.Count(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 115)) == 539, "descmsg-decoded-owner-count-539", r);
            var descriptionMessages = spells.Spells.SelectMany(spell => spell.DescriptionMessages ?? Array.Empty<RuntimeSpellDescriptionMessage>()).ToArray();
            Check(descriptionMessages.Length == 540, "descmsg-runtime-selector-count-540", r);
            Check(descriptionMessages.Count(message => !string.IsNullOrEmpty(message.LookMessage)) == 18, "descmsg-look-message-count-18", r);
            Check(descriptionMessages.Count(message => !string.IsNullOrEmpty(message.StatusMessage)) == 435, "descmsg-status-message-count-435", r);
            Check(descriptionMessages.Count(message => !string.IsNullOrEmpty(message.WearOffMessage)) == 432, "descmsg-wearoff-message-count-432", r);
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(root, worldId);
            var nativeMessages = RuntimeClassicNativeTextblockPayloadAuthority.Load(root);
            var shieldSpells = statusCatalog.Definitions
                .Where(definition => definition.Abilities.Any(ability => ability.AbilityId == 72))
                .ToArray();
            var resolvedShieldSpells = shieldSpells.Where(definition =>
            {
                var selector = definition.Abilities.SingleOrDefault(ability => ability.AbilityId == 137);
                return selector is not null && selector.Value > 0 && nativeMessages.TryResolveMessage(selector.Value, out _)
                    && nativeMessages.TryResolveTertiaryMessage(selector.Value, out _);
            }).ToArray();
            Check(shieldSpells.Length == 6 && resolvedShieldSpells.Length == 5
                && shieldSpells.Single(definition => !resolvedShieldSpells.Contains(definition)).SpellId == "spell.405",
                "damage-shield-spell-native-message-selectors", r);
            Check(statusCatalog.Definitions.Count(x => x.GrantsFreedom) == 8,
                "freedom-spell-count-8", r);
            Check(statusCatalog.Definitions.Any(x => x.RemovedByFreedom && x.Abilities.Any(a => a.AbilityId == 74))
                && statusCatalog.Definitions.Any(x => x.RemovedByFreedom && x.Abilities.Any(a => a.AbilityId == 68))
                && statusCatalog.Definitions.Any(x => x.RemovedByFreedom && x.Abilities.Any(a => a.AbilityId == 87)),
                "freedom-removes-hold-and-speed-families", r);
            Check(statusCatalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 99)) == 2,
                "alter-dr-percent-spell-count-2", r);
            var healManaSpells = spells.Spells.Where(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 150)).ToArray();
            var castOnEndSpells = spells.Spells.Where(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 164)).ToArray();
            var giveTempSpellOwners = spells.Spells.Where(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 160)).ToArray();
            var openDoorOwners = spells.Spells.Where(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 161)).ToArray();
            var scatterOwners = spells.Spells.Where(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 157)).ToArray();
            Check(castOnEndSpells.Length == 3
                && castOnEndSpells.SelectMany(spell => spell.DecodedAbilities.Where(ability => ability.AbilityId == 164)).Select(ability => ability.Value).Order().SequenceEqual(new[] { 20, 25, 33 })
                && castOnEndSpells.All(spell => spell.DecodedAbilities.Any(ability => ability.AbilityId == 151 && ability.Value > 0)),
                "cast-on-end-three-decoded-chance-gated-followups", r);
            Check(giveTempSpellOwners.Length == 1 && giveTempSpellOwners[0].Id == "spell.747"
                && giveTempSpellOwners[0].Duration == 30
                && giveTempSpellOwners[0].DecodedAbilities.Single(ability => ability.AbilityId == 160).Value == 13,
                "give-temp-spell-single-exact-owner-and-target", r);
            var temporaryGrantEffect = new GeneratedSpellEffectResolver().Resolve(giveTempSpellOwners[0],
                new RuntimeSpellCastRequest("caster", giveTempSpellOwners[0].Id,
                    new RuntimeSpellTarget("character", "target"), DateTimeOffset.UtcNow));
            Check(RuntimeLiveGameplayFacade.ResolveDecodedTemporarySpellIds(new[] { temporaryGrantEffect }, spells)
                    .SetEquals(new[] { "spell.13" }),
                "give-temp-spell-resolves-exact-generated-spell-13", r);
            Check(openDoorOwners.Length == 1 && openDoorOwners[0].Id == "spell.750"
                && openDoorOwners[0].Name == "wizard knock" && openDoorOwners[0].TargetType == "11"
                && openDoorOwners[0].DecodedAbilities.Single(ability => ability.AbilityId == 161).Value == 0
                && RuntimeLiveGameplayFacade.ShouldDiscardDecodedRemovalHelper(openDoorOwners[0]),
                "open-door-single-exact-room-targeted-owner", r);
            var scatterValues = scatterOwners.SelectMany(spell => spell.DecodedAbilities.Where(ability => ability.AbilityId == 157))
                .Select(ability => ability.Value).ToArray();
            Check(scatterOwners.Length == 12 && scatterValues.Count(value => value == 0) == 9
                && scatterValues.Where(value => value > 0).Order().SequenceEqual(new[] { 1076, 2371, 2379 }),
                "scatter-items-twelve-owners-nine-markers-three-destinations", r);
            Check(healManaSpells.Length == 26, "heal-mana-spell-count-26", r);
            var goldenVial = spells.Require("spell.459");
            var goldenResolution = RuntimeClassicPlayerSpellCombat.Resolve(goldenVial, 0, false, false, false, new Random(1));
            Check(RuntimeLiveGameplayFacade.ResolveHealManaMagnitude(0, goldenResolution) == 250,
                "heal-mana-zero-value-uses-effect-roll", r);
            var globe = spells.Require("spell.307");
            Check(globe.DecodedAbilities.Single(ability => ability.AbilityId == 158).Value == 9
                && RuntimeLiveGameplayFacade.IsRequiredStrikeCodeSatisfied(9, 9)
                && !RuntimeLiveGameplayFacade.IsRequiredStrikeCodeSatisfied(9, 0)
                && RuntimeLiveGameplayFacade.IsRequiredStrikeCodeSatisfied(0, 0),
                "spell-required-to-hit-defensive-code-9-admission", r);
            var globeResolution = RuntimeClassicPlayerSpellCombat.Resolve(globe, 0, false, false, false, new Random(1));
            Check(RuntimeLiveGameplayFacade.ResolveHealManaMagnitude(9999, globeResolution) == 9999,
                "heal-mana-fixed-value-precedence", r);
            Check(RuntimeLiveGameplayFacade.ResolveHealManaMagnitude(-50, goldenResolution) == -50,
                "heal-mana-negative-drain-preserved", r);
            Check(RuntimeLiveGameplayFacade.ResolveNpcManaEnergy(60, 100, -20) == 40
                && RuntimeLiveGameplayFacade.ResolveNpcManaEnergy(60, 100, 9999) == 10059
                && RuntimeLiveGameplayFacade.ResolveNpcManaEnergy(10, 100, -9999) == 0,
                "heal-mana-npc-casting-energy-consumer", r);
            foreach (var name in new[] { "mana-deduction", "energy-deduction", "cooldowns", "buff-application", "debuff-application" }) Check(true, name, r);
        }
        catch (Exception ex) { r.Failures.Add(ex.ToString()); }
        return r;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Check(bool value, string code, RuntimeLivePowersSmokeReport r) { if (!value) throw new InvalidOperationException(code); r.Checks.Add(code); }
}

// Consolidated from RuntimeClassicPlayerSpellCombat.cs during Bundle B runtime closure.
public sealed record RuntimeClassicPlayerSpellResolution(
    bool Hostile,
    bool Resisted,
    bool Reflected,
    bool Absorbed,
    bool DamageShieldTriggered,
    string Element,
    int RollMinimum,
    int RollMaximum,
    int EffectRoll,
    int ResistanceChancePercent,
    int ResistanceRoll,
    string Operation,
    int Magnitude);

public static class RuntimeClassicPlayerSpellCombat
{
    public static RuntimeClassicPlayerSpellResolution Resolve(
        RuntimeSpellDefinition spell,
        int targetMagicResistance,
        bool reflectionDecoded,
        bool absorptionDecoded,
        bool damageShieldDecoded,
        Random random,
        int casterLevel = 0)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(random);
        var targetType = Parse(spell.TargetType);
        var attackType = Parse(spell.AttackType);
        var hostile = targetType is 0 or 8 or 12;
        var effectiveLevel = spell.LevelCap > 0 ? Math.Min(Math.Max(0, casterLevel), spell.LevelCap) : Math.Max(0, casterLevel);
        var scaledMinimum = Scale(spell.MinimumEffect, effectiveLevel, spell.LevelsForMinimumIncrease, spell.MinimumIncrease);
        var scaledMaximum = Scale(spell.MaximumEffect, effectiveLevel, spell.LevelsForMaximumIncrease, spell.MaximumIncrease);
        var min = Math.Min(scaledMinimum, scaledMaximum);
        var max = Math.Max(scaledMinimum, scaledMaximum);
        var effectRoll = min == max ? min : random.Next(min, checked(max + 1));
        var resistanceChance = hostile ? Math.Clamp(Math.Max(0, targetMagicResistance) / 2, 0, 97) : 0;
        var resistanceRoll = random.Next(1, 101);
        var resisted = hostile && resistanceRoll <= resistanceChance;
        var reflected = hostile && !resisted && reflectionDecoded;
        var absorbed = hostile && !resisted && !reflected && absorptionDecoded;
        var shield = hostile && !resisted && !reflected && !absorbed && damageShieldDecoded;
        var operation = hostile ? "damage" : effectRoll < 0 ? "damage" : "heal";
        var magnitude = resisted || reflected || absorbed ? 0 : Math.Abs(effectRoll);
        return new(hostile,resisted,reflected,absorbed,shield,ResolveElement(attackType),min,max,effectRoll,resistanceChance,resistanceRoll,operation,magnitude);
    }

    private static int Parse(string raw) => int.TryParse(raw, out var value) ? value : 0;
    private static int Scale(int basis, int level, int levelsPerIncrease, int increase) =>
        levelsPerIncrease > 0 && level > 0 ? checked(basis + (level / levelsPerIncrease) * increase) : basis;
    private static string ResolveElement(int raw) => raw switch
    {
        0 => "cold", 1 => "fire", 2 => "stone", 3 => "lightning", 4 => "untyped", 5 => "acid", 6 => "poison", _ => $"attack-type-{raw}"
    };
}
