using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicLiveStatusAbility(int AbilityId, string Name, int Value);

public sealed record RuntimeClassicLiveStatusDefinition(
    string SpellId,
    string LegacySpellId,
    string Name,
    int SpellTypeRaw,
    int AttackTypeRaw,
    int DurationRounds,
    int MinimumEffect,
    int MaximumEffect,
    IReadOnlyList<RuntimeClassicLiveStatusAbility> Abilities)
{
    public bool IsPoison => SpellTypeRaw == 1 || Abilities.Any(x => x.AbilityId == 19);
    public bool CuresPoison => Abilities.Any(x => x.AbilityId == 20);
    public bool DispelsMagic => Abilities.Any(x => x.AbilityId == 73);
    public bool NonMagical => Abilities.Any(x => x.AbilityId == 144);
    public IReadOnlyList<int> RemovedLegacySpellIds => Abilities
        .Where(x => x.AbilityId == 122 && x.Value > 0)
        .Select(x => x.Value)
        .Distinct()
        .ToArray();

    public int RollMinimum => Math.Min(MinimumEffect, MaximumEffect);
    public int RollMaximum => Math.Max(MinimumEffect, MaximumEffect);
    public bool HasPositiveMagnitude => RollMaximum > 0;
    public bool HasPeriodicDamage => DurationRounds > 0 && HasPositiveMagnitude &&
        (IsPoison || Abilities.Any(x => x.AbilityId is 1 or 17));
}

public sealed class RuntimeClassicLiveStatusCatalog
{
    public const int CombatRoundSeconds = 6;
    private readonly IReadOnlyDictionary<string, RuntimeClassicLiveStatusDefinition> _bySpellId;
    private readonly IReadOnlyDictionary<string, RuntimeClassicLiveStatusDefinition> _byLegacyId;

    private RuntimeClassicLiveStatusCatalog(
        IReadOnlyDictionary<string, RuntimeClassicLiveStatusDefinition> bySpellId,
        IReadOnlyDictionary<string, RuntimeClassicLiveStatusDefinition> byLegacyId)
    {
        _bySpellId = bySpellId;
        _byLegacyId = byLegacyId;
    }

    public IReadOnlyCollection<RuntimeClassicLiveStatusDefinition> Definitions => _bySpellId.Values.ToArray();
    public bool TryGet(string spellId, out RuntimeClassicLiveStatusDefinition definition) => _bySpellId.TryGetValue(spellId, out definition!);
    public bool TryGetLegacy(string legacyId, out RuntimeClassicLiveStatusDefinition definition) => _byLegacyId.TryGetValue(legacyId, out definition!);

    public static RuntimeClassicLiveStatusCatalog Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "spells.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Classic spell projection is missing.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Generated spell projection has no records array.");

        var bySpell = new Dictionary<string, RuntimeClassicLiveStatusDefinition>(StringComparer.OrdinalIgnoreCase);
        var byLegacy = new Dictionary<string, RuntimeClassicLiveStatusDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.EnumerateArray())
        {
            var id = ReadString(record, "id");
            var legacyId = ReadString(record, "legacy_id");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(legacyId)) continue;
            var classification = record.TryGetProperty("classification", out var c) ? c : default;
            var effect = record.TryGetProperty("effect", out var e) ? e : default;
            var fields = record.TryGetProperty("public_reference_0139", out var publicReference)
                && publicReference.TryGetProperty("fields", out var f) ? f : default;
            var abilities = new List<RuntimeClassicLiveStatusAbility>();
            if (fields.ValueKind == JsonValueKind.Object
                && fields.TryGetProperty("abilities_named", out var abilityArray)
                && abilityArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var ability in abilityArray.EnumerateArray())
                {
                    var abilityId = ReadInt(ability, "ability_id");
                    if (abilityId <= 0) continue;
                    abilities.Add(new RuntimeClassicLiveStatusAbility(
                        abilityId,
                        ReadString(ability, "name"),
                        ReadInt(ability, "value")));
                }
            }

            var definition = new RuntimeClassicLiveStatusDefinition(
                id,
                legacyId,
                ReadString(record, "name"),
                ReadInt(classification, "spell_type"),
                ReadInt(classification, "attack_type"),
                ReadInt(effect, "duration"),
                ReadInt(effect, "min_effect"),
                ReadInt(effect, "max_effect"),
                abilities);
            if (!bySpell.TryAdd(id, definition)) throw new InvalidOperationException($"Duplicate Classic status spell '{id}'.");
            if (!byLegacy.TryAdd(legacyId, definition)) throw new InvalidOperationException($"Duplicate Classic legacy status spell '{legacyId}'.");
        }
        return new RuntimeClassicLiveStatusCatalog(bySpell, byLegacy);
    }

    private static string ReadString(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var field)) return string.Empty;
        return field.ValueKind == JsonValueKind.String ? field.GetString() ?? string.Empty : field.ToString();
    }

    private static int ReadInt(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var field)) return 0;
        if (field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out var number)) return number;
        return int.TryParse(field.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : 0;
    }
}

public static class RuntimeClassicStatusProjection
{
    public const string Prefix = "combat.status.";

    public static RuntimeCharacterRecord Apply(
        RuntimeCharacterRecord character,
        IEnumerable<RuntimeActiveSpellEffect> activeEffects,
        RuntimeClassicLiveStatusCatalog catalog,
        DateTimeOffset nowUtc,
        string sourceRecordId)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(activeEffects);
        ArgumentNullException.ThrowIfNull(catalog);
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        foreach (var key in conditions.Keys.Where(x => x.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            conditions.Remove(key);

        var numeric = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var effects = activeEffects
            .Where(effect => effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)
                && effect.Target.Id.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase)
                && (effect.ExpiresAtUtc is null || effect.ExpiresAtUtc > nowUtc))
            .OrderBy(effect => effect.AppliedAtUtc)
            .ThenBy(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var effect in effects)
        {
            if (!catalog.TryGet(effect.SpellId, out var definition)) continue;
            foreach (var ability in definition.Abilities)
            {
                var resolved = ResolveAbilityValue(ability, effect);
                switch (ability.AbilityId)
                {
                    case 2: Add(numeric, "armor_class", resolved); break;
                    case 3: Add(numeric, "resist_cold", resolved); break;
                    case 4: Add(numeric, "maximum_damage", resolved); break;
                    case 5: Add(numeric, "resist_fire", resolved); break;
                    case 7: Add(numeric, "damage_resistance", resolved); break;
                    case 22:
                    case 105:
                    case 106: Add(numeric, "accuracy", resolved); break;
                    case 27: Add(numeric, "stealth", resolved); break;
                    case 34: Add(numeric, "dodge", resolved); break;
                    case 36: Add(numeric, "magic_resistance", resolved); break;
                    case 44: Add(numeric, "stat_intellect", resolved); break;
                    case 45: Add(numeric, "stat_wisdom", resolved); break;
                    case 46: Add(numeric, "stat_strength", resolved); break;
                    case 48: Add(numeric, "stat_agility", resolved); break;
                    case 49: Add(numeric, "stat_charm", resolved); break;
                    case 58: Add(numeric, "critical", resolved); break;
                    case 65: Add(numeric, "resist_stone", resolved); break;
                    case 66: Add(numeric, "resist_lightning", resolved); break;
                    case 68: Add(numeric, "slowness", resolved); break;
                    case 72: Add(numeric, "damage_shield", resolved); break;
                    case 77: Add(numeric, "perception", resolved); break;
                    case 87: Add(numeric, "speed", resolved); break;
                    case 96: Add(numeric, "encumbrance", resolved); break;
                    case 123: Add(numeric, "hp_regen", resolved); break;
                    case 19: flags.Add("poisoned"); break;
                    case 21: flags.Add("poison_immune"); break;
                    case 57: flags.Add("see_hidden"); break;
                    case 60: flags.Add("feared"); break;
                    case 71: flags.Add("confused"); break;
                    case 74: flags.Add("paralyzed"); break;
                    case 76:
                    case 79: flags.Add("silenced"); break;
                    case 107: flags.Add("blinded"); break;
                    case 139: flags.Add("spell_immune"); break;
                }
            }
            if (definition.IsPoison) flags.Add("poisoned");
        }

        foreach (var pair in numeric)
            conditions[Prefix + pair.Key] = pair.Value.ToString(CultureInfo.InvariantCulture);
        foreach (var flag in flags)
            conditions[Prefix + flag] = "1";
        conditions[Prefix + "active_count"] = effects.Length.ToString(CultureInfo.InvariantCulture);
        conditions[Prefix + "source"] = sourceRecordId;

        return character with { Conditions = conditions, UpdatedAtUtc = nowUtc };
    }

    public static RuntimeCharacterRecord Clear(RuntimeCharacterRecord character, DateTimeOffset nowUtc, string sourceRecordId)
    {
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        foreach (var key in conditions.Keys.Where(x => x.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            conditions.Remove(key);
        conditions[Prefix + "active_count"] = "0";
        conditions[Prefix + "source"] = sourceRecordId;
        return character with { Conditions = conditions, UpdatedAtUtc = nowUtc };
    }

    public static bool IsPhysicalAttackBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "paralyzed") || Flag(character, "confused");

    public static bool IsSpellCastBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "paralyzed") || Flag(character, "silenced") || Flag(character, "confused");

    public static bool IsMovementBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "paralyzed");

    public static bool IsPoisonImmune(RuntimeCharacterRecord character) => Flag(character, "poison_immune");

    public static int Numeric(RuntimeCharacterRecord character, string name)
        => character.Conditions.TryGetValue(Prefix + name, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static bool Flag(RuntimeCharacterRecord character, string name)
        => character.Conditions.TryGetValue(Prefix + name, out var raw)
            && (raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static int ResolveAbilityValue(RuntimeClassicLiveStatusAbility ability, RuntimeActiveSpellEffect effect)
    {
        if (ability.Value != 0) return ability.Value;
        if (ability.AbilityId is 19 or 20 or 21 or 52 or 57 or 60 or 71 or 73 or 74 or 76 or 79 or 107 or 108 or 122 or 139 or 144)
            return 1;
        return checked((effect.MinimumEffect + effect.MaximumEffect) / 2);
    }

    private static void Add(IDictionary<string, int> values, string key, int amount)
        => values[key] = checked(values.TryGetValue(key, out var current) ? current + amount : amount);
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private RuntimeClassicLiveStatusCatalog? _classicLiveStatusCatalog;
    private string? _classicLiveStatusWorldId;

    private RuntimeClassicLiveStatusCatalog ClassicLiveStatusCatalog(RuntimeSession session)
    {
        if (_classicLiveStatusCatalog is null
            || !_classicLiveStatusWorldId!.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            _classicLiveStatusCatalog = RuntimeClassicLiveStatusCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
            _classicLiveStatusWorldId = session.World.Definition.Id;
        }
        return _classicLiveStatusCatalog;
    }

    private static IReadOnlyDictionary<string, string> BuildClassicStatusMetadata(
        RuntimeClassicLiveStatusDefinition definition,
        RuntimeNpcHitSpellSelection spell,
        DateTimeOffset nowUtc)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "npc_hit_spell",
            ["effect_kind"] = definition.IsPoison ? "poison" : "status",
            ["spell_type_raw"] = spell.SpellTypeRaw.ToString(CultureInfo.InvariantCulture),
            ["attack_type_raw"] = spell.AttackTypeRaw.ToString(CultureInfo.InvariantCulture),
            ["duration_unit"] = "combat_rounds",
            ["duration_rounds"] = spell.DurationRaw.ToString(CultureInfo.InvariantCulture),
            ["round_seconds"] = RuntimeClassicLiveStatusCatalog.CombatRoundSeconds.ToString(CultureInfo.InvariantCulture),
            ["stacking"] = "refresh_same_spell_independent_different_spells",
            ["remove_on_death"] = "1",
            ["persist_across_logout"] = "1",
            ["non_magical"] = definition.NonMagical ? "1" : "0"
        };
        if (definition.HasPeriodicDamage)
        {
            metadata["periodic_operation"] = "damage";
            metadata["tick_interval_rounds"] = "1";
            metadata["next_tick_utc"] = nowUtc.AddSeconds(RuntimeClassicLiveStatusCatalog.CombatRoundSeconds).ToString("O", CultureInfo.InvariantCulture);
        }
        foreach (var ability in definition.Abilities)
            metadata[$"ability.{ability.AbilityId}"] = ability.Value.ToString(CultureInfo.InvariantCulture);
        return metadata;
    }

    private RuntimeActiveSpellEffect? NormalizeDecodedPlayerStatusCast(
        RuntimeSession session,
        RuntimeActiveSpellEffect? castEffect,
        DateTimeOffset nowUtc)
    {
        if (castEffect is null) return null;
        var catalog = ClassicLiveStatusCatalog(session);
        if (!catalog.TryGet(castEffect.SpellId, out var definition) || castEffect.Duration <= 0) return castEffect;
        var metadata = new Dictionary<string, string>(castEffect.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "player_cast_spell",
            ["effect_kind"] = definition.IsPoison ? "poison" : "status",
            ["duration_unit"] = "combat_rounds",
            ["duration_rounds"] = castEffect.Duration.ToString(CultureInfo.InvariantCulture),
            ["round_seconds"] = RuntimeClassicLiveStatusCatalog.CombatRoundSeconds.ToString(CultureInfo.InvariantCulture),
            ["stacking"] = "refresh_same_spell_independent_different_spells",
            ["remove_on_death"] = "1",
            ["persist_across_logout"] = "1",
            ["non_magical"] = definition.NonMagical ? "1" : "0"
        };
        if (definition.HasPeriodicDamage)
        {
            metadata["periodic_operation"] = "damage";
            metadata["tick_interval_rounds"] = "1";
            metadata["next_tick_utc"] = nowUtc.AddSeconds(RuntimeClassicLiveStatusCatalog.CombatRoundSeconds).ToString("O", CultureInfo.InvariantCulture);
        }
        foreach (var ability in definition.Abilities)
            metadata[$"ability.{ability.AbilityId}"] = ability.Value.ToString(CultureInfo.InvariantCulture);
        var normalized = castEffect with
        {
            ExpiresAtUtc = nowUtc.AddSeconds(checked((long)castEffect.Duration * RuntimeClassicLiveStatusCatalog.CombatRoundSeconds)),
            Metadata = metadata
        };
        var snapshot = _liveSpellEngine!.Snapshot();
        var active = snapshot.ActiveEffects
            .Where(effect => effect.EffectId.Equals(castEffect.EffectId, StringComparison.OrdinalIgnoreCase)
                || !effect.SpellId.Equals(castEffect.SpellId, StringComparison.OrdinalIgnoreCase)
                || !effect.Target.Kind.Equals(castEffect.Target.Kind, StringComparison.OrdinalIgnoreCase)
                || !effect.Target.Id.Equals(castEffect.Target.Id, StringComparison.OrdinalIgnoreCase))
            .Select(effect => effect.EffectId.Equals(castEffect.EffectId, StringComparison.OrdinalIgnoreCase) ? normalized : effect)
            .ToArray();
        _liveSpellEngine.RestoreSnapshot(snapshot with { ActiveEffects = active });
        return normalized;
    }

    private static RuntimeSpellState NormalizeClassicStatusDurations(
        RuntimeSpellState state,
        RuntimeClassicLiveStatusCatalog catalog)
    {
        var changed = false;
        var active = state.ActiveEffects.Select(effect =>
        {
            if (!catalog.TryGet(effect.SpellId, out var definition) || effect.Duration <= 0) return effect;
            var metadata = new Dictionary<string, string>(effect.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            if (metadata.TryGetValue("duration_unit", out var unit)
                && unit.Equals("combat_rounds", StringComparison.OrdinalIgnoreCase)) return effect;
            changed = true;
            metadata["duration_unit"] = "combat_rounds";
            metadata["duration_rounds"] = effect.Duration.ToString(CultureInfo.InvariantCulture);
            metadata["round_seconds"] = RuntimeClassicLiveStatusCatalog.CombatRoundSeconds.ToString(CultureInfo.InvariantCulture);
            if (definition.HasPeriodicDamage && !metadata.ContainsKey("next_tick_utc"))
            {
                metadata["periodic_operation"] = "damage";
                metadata["tick_interval_rounds"] = "1";
                metadata["next_tick_utc"] = effect.AppliedAtUtc.AddSeconds(RuntimeClassicLiveStatusCatalog.CombatRoundSeconds).ToString("O", CultureInfo.InvariantCulture);
            }
            return effect with
            {
                ExpiresAtUtc = effect.AppliedAtUtc.AddSeconds(checked((long)effect.Duration * RuntimeClassicLiveStatusCatalog.CombatRoundSeconds)),
                Metadata = metadata
            };
        }).ToArray();
        return changed ? state with { ActiveEffects = active } : state;
    }

    private void TickClassicLiveStatuses(RuntimeSession session, DateTimeOffset nowUtc)
    {
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            var catalog = ClassicLiveStatusCatalog(session);
            var snapshot = NormalizeClassicStatusDurations(_liveSpellEngine!.Snapshot(), catalog);
            var updatedEffects = new List<RuntimeActiveSpellEffect>();
            var affectedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deadTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var effect in snapshot.ActiveEffects.OrderBy(x => x.AppliedAtUtc).ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase))
            {
                if (effect.ExpiresAtUtc is not null && effect.ExpiresAtUtc <= nowUtc) { affectedTargets.Add(effect.Target.Id); continue; }
                var current = effect;
                if (effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)
                    && catalog.TryGet(effect.SpellId, out var definition)
                    && effect.Metadata is not null
                    && effect.Metadata.TryGetValue("periodic_operation", out var operation)
                    && effect.Metadata.TryGetValue("next_tick_utc", out var nextRaw)
                    && DateTimeOffset.TryParse(nextRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var nextTick))
                {
                    var metadata = new Dictionary<string, string>(effect.Metadata, StringComparer.OrdinalIgnoreCase);
                    var tickCount = 0;
                    while (nextTick <= nowUtc && (effect.ExpiresAtUtc is null || nextTick < effect.ExpiresAtUtc) && tickCount < 1024)
                    {
                        var character = _characters.LoadCharacter(effect.Target.Id);
                        if (character is null || character.HitPoints <= 0) { deadTargets.Add(effect.Target.Id); break; }
                        var magnitude = RollInclusive(definition.RollMinimum, definition.RollMaximum);
                        if (magnitude > 0)
                        {
                            var nextHp = operation.Equals("heal", StringComparison.OrdinalIgnoreCase)
                                ? Math.Min(character.MaximumHitPoints, character.HitPoints + magnitude)
                                : Math.Max(0, character.HitPoints - magnitude);
                            var targetSession = _sessions.ActiveSessions.FirstOrDefault(x => x.Actor.Id.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase));
                            targetSession?.EnqueueMessage(operation.Equals("heal", StringComparison.OrdinalIgnoreCase)
                                ? $"{definition.Name} restores {magnitude} hit points."
                                : $"{definition.Name} harms you for {magnitude} damage.");
                            if (nextHp == 0)
                            {
                                var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
                                var defeated = RuntimeCharacterDeathResolver.Defeat(
                                    RuntimeClassicStatusProjection.Clear(character, nextTick, "runtime.live_status.periodic_death"),
                                    itemCatalog,
                                    character.RoomId,
                                    nextTick);
                                _characters.SaveCharacter(defeated.Character);
                                targetSession?.EnqueueMessage($"Your corpse ({defeated.Corpse.CorpseId}) remains in this room. Use 'respawn', then return and use 'recover'.");
                                deadTargets.Add(character.CharacterId);
                                break;
                            }
                            _characters.SaveCharacter(character with { HitPoints = nextHp, UpdatedAtUtc = nextTick });
                        }
                        nextTick = nextTick.AddSeconds(RuntimeClassicLiveStatusCatalog.CombatRoundSeconds);
                        tickCount++;
                    }
                    metadata["next_tick_utc"] = nextTick.ToString("O", CultureInfo.InvariantCulture);
                    current = effect with { Metadata = metadata };
                    affectedTargets.Add(effect.Target.Id);
                }
                if (!deadTargets.Contains(effect.Target.Id)) updatedEffects.Add(current);
            }

            if (deadTargets.Count > 0)
                updatedEffects = updatedEffects.Where(x => !deadTargets.Contains(x.Target.Id)).ToList();
            _liveSpellEngine.RestoreSnapshot(snapshot with { ActiveEffects = updatedEffects });
            _liveSpellEngine.Expire(nowUtc);

            foreach (var targetId in affectedTargets.Concat(deadTargets).Distinct(StringComparer.OrdinalIgnoreCase))
                SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", targetId), nowUtc,
                    deadTargets.Contains(targetId) ? "runtime.live_status.target_death" : "runtime.live_status.periodic_tick");
            PersistLivePowers(session.World.Definition.Id);
        }
    }

    private static int RollInclusive(int minimum, int maximum)
    {
        var low = Math.Min(minimum, maximum);
        var high = Math.Max(minimum, maximum);
        if (high <= 0) return 0;
        low = Math.Max(0, low);
        return low == high ? low : Random.Shared.Next(low, checked(high + 1));
    }

    private int ApplyDecodedCureAndDispel(RuntimeSession session, RuntimeSpellDefinition castSpell, RuntimeSpellTarget target)
    {
        var catalog = ClassicLiveStatusCatalog(session);
        if (!catalog.TryGet(castSpell.Id, out var definition)) return 0;
        var effects = _liveSpellEngine!.EffectsFor(target.Id);
        var remove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (definition.CuresPoison)
            foreach (var effect in effects.Where(IsPoisonEffect)) remove.Add(effect.EffectId);
        foreach (var legacyId in definition.RemovedLegacySpellIds)
            if (catalog.TryGetLegacy(legacyId.ToString(CultureInfo.InvariantCulture), out var removedSpell))
                foreach (var effect in effects.Where(x => x.SpellId.Equals(removedSpell.SpellId, StringComparison.OrdinalIgnoreCase)))
                    remove.Add(effect.EffectId);
        if (definition.DispelsMagic && definition.RemovedLegacySpellIds.Count == 0)
        {
            foreach (var effect in effects)
            {
                var nonMagical = effect.Metadata is not null
                    && effect.Metadata.TryGetValue("non_magical", out var raw)
                    && raw.Equals("1", StringComparison.OrdinalIgnoreCase);
                if (!nonMagical) remove.Add(effect.EffectId);
            }
        }
        foreach (var effectId in remove) _liveSpellEngine.RemoveEffect(effectId);
        return remove.Count;
    }

    private void RemoveCharacterLiveStatusEffects(string characterId, DateTimeOffset nowUtc, string sourceRecordId)
    {
        if (_liveSpellEngine is null) return;
        var snapshot = _liveSpellEngine.Snapshot();
        var retained = snapshot.ActiveEffects.Where(x => !x.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)
            || !x.Target.Id.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (retained.Length == snapshot.ActiveEffects.Count) return;
        _liveSpellEngine.RestoreSnapshot(snapshot with { ActiveEffects = retained });
        var character = _characters.LoadCharacter(characterId);
        if (character is not null) _characters.SaveCharacter(RuntimeClassicStatusProjection.Clear(character, nowUtc, sourceRecordId));
        if (!string.IsNullOrWhiteSpace(_livePowerWorldId)) PersistLivePowers(_livePowerWorldId);
    }
}

public sealed class RuntimeClassicLiveStatusEffectsSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeClassicLiveStatusEffectsSmokeReport Run(string projectRoot, string worldId)
    {
        var report = new RuntimeClassicLiveStatusEffectsSmokeReport();
        try
        {
            var catalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            Require(catalog.Definitions.Any(x => x.IsPoison && x.DurationRounds > 0), "decoded-poison-policies"); report.Checks.Add("decoded-poison-policies");
            Require(catalog.Definitions.Any(x => x.CuresPoison), "decoded-cure-poison-policies"); report.Checks.Add("decoded-cure-poison-policies");
            Require(catalog.Definitions.Any(x => x.RemovedLegacySpellIds.Count > 0), "decoded-removes-spell-policies"); report.Checks.Add("decoded-removes-spell-policies");
            Require(catalog.Definitions.Any(x => x.DispelsMagic), "decoded-general-dispel-policies"); report.Checks.Add("decoded-general-dispel-policies");
            Require(catalog.Definitions.Any(x => x.HasPeriodicDamage), "decoded-periodic-status-policies"); report.Checks.Add("decoded-periodic-status-policies");
            Require(catalog.Definitions.Any(x => x.Abilities.Any(a => a.AbilityId == 74)), "decoded-paralysis-policies"); report.Checks.Add("decoded-paralysis-policies");
            Require(catalog.Definitions.Any(x => x.Abilities.Any(a => a.AbilityId is 76 or 79)), "decoded-silence-policies"); report.Checks.Add("decoded-silence-policies");
            Require(RuntimeClassicLiveStatusCatalog.CombatRoundSeconds == 6, "status-round-six-seconds"); report.Checks.Add("status-round-six-seconds");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
