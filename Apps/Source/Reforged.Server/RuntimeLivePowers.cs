using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeLivePowerState(
    RuntimeSpellState Spells,
    IReadOnlyDictionary<string, RuntimeActorAbilitySnapshot> Abilities,
    IReadOnlyDictionary<string, DateTimeOffset> SpellCooldowns,
    DateTimeOffset SavedAtUtc);

public sealed class RuntimeLivePowerStateRepository
{
    private readonly string _path;
    public RuntimeLivePowerStateRepository(string projectRoot, string worldId) =>
        _path = Path.Combine(projectRoot, "Saves", worldId, "runtime-live-powers.json");

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

    private static RuntimeLivePowerState Empty() => new(
        new RuntimeSpellState(Array.Empty<RuntimeKnownSpell>(), Array.Empty<RuntimeActiveSpellEffect>(), new Dictionary<string, RuntimeSpellResources>()),
        new Dictionary<string, RuntimeActorAbilitySnapshot>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase), DateTimeOffset.MinValue);
}

public sealed partial class RuntimeLiveGameplayFacade
{
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
                "cast" => CastLiveSpell(session, command),
                "use" => UseLiveItemOrAbility(session, command),
                "abilities" or "skills" => RenderLiveAbilities(session),
                "prepare" => RenderLiveSpells(session),
                "dispel" => DispelLiveEffect(session, command.ArgumentText),
                _ => _policyGateway.Execute(session, command)
            };
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
        var expiredOnRestore = _liveSpellEngine.Expire(restoreNow);
        if (expiredOnRestore > 0)
        {
            SynchronizeAllCharacterSpellStatuses(restoreNow, "runtime.live_powers.restore_expire");
            PersistLivePowers(session.World.Definition.Id);
        }
    }

    private string CastLiveSpell(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (command.Arguments.Count == 0) return "Cast which spell?";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        if (RuntimeClassicStatusProjection.IsSpellCastBlocked(character)) return "You cannot cast while affected by the current status.";
        var spell = ResolveSpell(command.Arguments[0]);
        EnsureKnownSpells(character, actor.Id);
        var now = DateTimeOffset.UtcNow;
        var cooldownKey = actor.Id + "\u001f" + spell.Id;
        if (_liveSpellCooldowns.TryGetValue(cooldownKey, out var ready) && ready > now) return $"{spell.Name} is on cooldown until {ready:O}.";
        var target = ResolvePowerTarget(session, command.Arguments.Skip(1).ToArray(), spell.TargetType);
        _liveSpellEngine!.SetResources(actor.Id, new RuntimeSpellResources(character.Mana, ReadEnergy(character)));
        var execution = new RuntimeSpellExecutionEngine(_liveSpellCatalog!, _liveSpellEngine,
            new GeneratedLiveSpellOperationPolicy(this, session), new LiveSpellEventSink(this, session));
        var before = _liveSpellEngine.Snapshot();
        try
        {
            var result = execution.Cast(new RuntimeSpellCastRequest(actor.Id, spell.Id, target, now));
            if (!result.Succeeded) return result.FailureMessage ?? result.FailureCode ?? "The spell failed.";
            var remaining = result.Cast.Resources;
            SaveCharacterPowerResources(character, remaining.Mana, remaining.Energy);
            var normalizedCastEffect = NormalizeDecodedPlayerStatusCast(session, result.Cast.Effect, now);
            var removedByCureOrDispel = ApplyDecodedCureAndDispel(session, spell, target);
            if ((IsCurePoisonSpell(spell) || removedByCureOrDispel > 0) && normalizedCastEffect is not null)
            {
                try { _liveSpellEngine.RemoveEffect(normalizedCastEffect.EffectId); } catch (KeyNotFoundException) { }
            }
            SynchronizeCharacterSpellStatuses(target, now, "runtime.live_powers.cast");
            var cooldownSeconds = Math.Max(0, GeneratedInt(spell, "cooldown", "cooldown_seconds", "recast"));
            if (cooldownSeconds > 0) _liveSpellCooldowns[cooldownKey] = now.AddSeconds(cooldownSeconds);
            PersistLivePowers(session.World.Definition.Id);
            return $"You cast {spell.Name} on {target.Id}. " + string.Join(" ", result.Events.Select(RenderSpellEvent));
        }
        catch
        {
            _liveSpellEngine.RestoreSnapshot(before);
            throw;
        }
    }


    private string UseLiveItemOrAbility(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (command.Arguments.Count == 0) return "Use which item or ability?";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        if (TryUseSpellLearningItem(character, command.ArgumentText, out var itemResult)) return itemResult;
        return UseLiveAbility(session, command);
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

        engine.Remove(selected.InstanceId);
        if (selected.StackQuantity > 1)
            engine.Add(selected with { StackQuantity = selected.StackQuantity - 1 });
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
        _liveSpellEngine.Expire(now);
        foreach (var characterId in affectedCharacterIds)
            SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", characterId), now, "runtime.live_powers.expire");
        foreach (var key in _liveSpellCooldowns.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) _liveSpellCooldowns.Remove(key);
        foreach (var state in _liveAbilityStates.Values) state.ClearExpiredCooldowns(now);
        PersistLivePowers(session.World.Definition.Id);
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

    private string RenderLiveSpells(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "No spellbook is available.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        EnsureKnownSpells(character, actor.Id);
        var rows = _liveSpellEngine!.Spellbook(actor.Id).Select(x => _liveSpellCatalog!.Require(x.SpellId)).OrderBy(x => x.Name).Select(x => $"{x.Id}: {x.Name} mana={x.ManaCost} energy={x.EnergyCost} duration={x.Duration}");
        return rows.Any() ? "Spells:\n" + string.Join("\n", rows) : "You know no generated spells.";
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
            var resolution = ResolveNpcHitSpellStatusDelivery(spell, poisonImmune, existing.Length > 0);
            if (!resolution.Applied) return resolution;

            var catalog = ClassicLiveStatusCatalog(session);
            if (!catalog.TryGet(spell.CanonicalSpellId, out var definition))
                throw new InvalidOperationException($"Decoded status spell '{spell.CanonicalSpellId}' is missing from the Classic status catalog.");
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
                        nowUtc.AddSeconds(checked((long)spell.DurationRaw * RuntimeClassicLiveStatusCatalog.CombatRoundSeconds)),
                        BuildClassicStatusMetadata(definition, spell, nowUtc))
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
            : $"{e.Operation} {e.Magnitude}.";

    internal static bool IsCurePoisonSpell(RuntimeSpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.LegacyId.Equals("31", StringComparison.OrdinalIgnoreCase)
            || spell.LegacyId.Equals("222", StringComparison.OrdinalIgnoreCase)
            || spell.Name.Equals("antidote", StringComparison.OrdinalIgnoreCase)
            || spell.Name.Equals("cure poison", StringComparison.OrdinalIgnoreCase);
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
            if (IsCurePoisonSpell(spell))
                return new[] { new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, "cure_poison", 0, request.RequestedAtUtc, effect.EffectId, effect.Metadata) };
            var targetMr = 0;
            if (request.Target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase) && _owner._liveCombats.TryGetValue(_session.Id, out var encounter))
                targetMr = ResolveEncounterNpc(_session, encounter).MagicResistance;
            else if (request.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase) && _owner._characters.LoadCharacter(request.Target.Id) is { } targetCharacter)
                targetMr = ReadMagicResistance(targetCharacter);
            var resolution = RuntimeClassicPlayerSpellCombat.Resolve(spell, targetMr, false, false, false, Random.Shared);
            var metadata = new Dictionary<string,string>(effect.Metadata ?? new Dictionary<string,string>(), StringComparer.OrdinalIgnoreCase)
            {
                ["element"] = resolution.Element,
                ["resistance_chance"] = resolution.ResistanceChancePercent.ToString(),
                ["resistance_roll"] = resolution.ResistanceRoll.ToString(),
                ["resisted"] = resolution.Resisted.ToString(),
                ["reflection"] = "absent-from-uploaded-classic-decode",
                ["absorption"] = "absent-from-uploaded-classic-decode",
                ["damage_shield"] = "absent-from-uploaded-classic-decode"
            };
            return new[] { new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, resolution.Operation, resolution.Magnitude, request.RequestedAtUtc, effect.EffectId, metadata) };
        }
    }

    private sealed class LiveSpellEventSink : IRuntimeSpellEventSink
    {
        private readonly RuntimeLiveGameplayFacade _owner; private readonly RuntimeSession _session;
        public LiveSpellEventSink(RuntimeLiveGameplayFacade owner, RuntimeSession session) { _owner = owner; _session = session; }
        public void Apply(RuntimeSpellExecutionEvent e) => _owner.ApplyPowerMagnitude(_session, e.Target, e.Operation, e.Magnitude);
        public void Revert(RuntimeSpellExecutionEvent e) => _owner.ApplyPowerMagnitude(_session, e.Target, e.Operation, -e.Magnitude);
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

    private void ApplyPowerMagnitude(RuntimeSession source, RuntimeSpellTarget target, string operation, int magnitude)
    {
        if (operation.Equals("modifier", StringComparison.OrdinalIgnoreCase) || magnitude == 0) return;
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
            Check(typeof(RuntimeSpellExecutionEngine).GetMethod(nameof(RuntimeSpellExecutionEngine.Cast)) is not null, "combat-spell-execution", r);
            Check(typeof(RuntimeAbilityEngine).GetMethod(nameof(RuntimeAbilityEngine.Execute)) is not null, "combat-ability-execution", r);
            var repo = new RuntimeLivePowerStateRepository(Path.Combine(Path.GetTempPath(), "mmr-live-powers-" + Guid.NewGuid().ToString("N")), worldId);
            var empty = new RuntimeLivePowerState(new RuntimeSpellState(Array.Empty<RuntimeKnownSpell>(), Array.Empty<RuntimeActiveSpellEffect>(), new Dictionary<string, RuntimeSpellResources>()), new Dictionary<string, RuntimeActorAbilitySnapshot>(), new Dictionary<string, DateTimeOffset>(), DateTimeOffset.UtcNow);
            repo.Save(empty); Check(repo.Load().Spells.ActiveEffects.Count == 0, "persistence", r); Check(repo.Load().Abilities.Count == 0, "reconnect", r);
            Check(typeof(RuntimeSpellEngine).GetMethod(nameof(RuntimeSpellEngine.Expire)) is not null, "duration-expiration", r); Check(typeof(RuntimeSpellExecutionEngine).GetMethod(nameof(RuntimeSpellExecutionEngine.Dispel)) is not null, "dispel", r);
            Check(typeof(RuntimeSpellEngine).GetMethod(nameof(RuntimeSpellEngine.Validate)) is not null, "validator", r); Check(typeof(RuntimeAbilityEngine).GetMethod(nameof(RuntimeAbilityEngine.Execute)) is not null, "rollback", r);
            var antidote = spells.Spells.Single(x => x.LegacyId == "31");
            var curePoison = spells.Spells.Single(x => x.LegacyId == "222");
            Check(RuntimeLiveGameplayFacade.IsCurePoisonSpell(antidote), "cure-poison-antidote-classification", r);
            Check(RuntimeLiveGameplayFacade.IsCurePoisonSpell(curePoison), "cure-poison-healer-classification", r);
            var poisonEffect = new RuntimeActiveSpellEffect("poison", "spell.poison", "npc.1", new RuntimeSpellTarget("character", "character.1"), 1, 2, 10, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(10), new Dictionary<string,string> { ["effect_kind"] = "poison" });
            var statusEffect = poisonEffect with { EffectId = "status", Metadata = new Dictionary<string,string> { ["effect_kind"] = "status" } };
            Check(RuntimeLiveGameplayFacade.IsPoisonEffect(poisonEffect), "cure-poison-removes-poison", r);
            Check(!RuntimeLiveGameplayFacade.IsPoisonEffect(statusEffect), "cure-poison-preserves-non-poison", r);
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
        Random random)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(random);
        var targetType = Parse(spell.TargetType);
        var attackType = Parse(spell.AttackType);
        var hostile = targetType is 0 or 8 or 12;
        var min = Math.Min(spell.MinimumEffect, spell.MaximumEffect);
        var max = Math.Max(spell.MinimumEffect, spell.MaximumEffect);
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
    private static string ResolveElement(int raw) => raw switch
    {
        0 => "cold", 1 => "fire", 2 => "stone", 3 => "lightning", 4 => "untyped", 5 => "acid", 6 => "poison", _ => $"attack-type-{raw}"
    };
}
