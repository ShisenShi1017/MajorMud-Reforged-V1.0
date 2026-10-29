using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

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
    public bool GeneralMagicDispel => Abilities.Any(x => x.AbilityId == 73 && x.Value == 0);
    public IReadOnlyList<int> DispelledAbilityIds => Abilities
        .Where(x => x.AbilityId == 73 && x.Value > 0)
        .Select(x => x.Value)
        .Distinct()
        .ToArray();
    public bool GrantsFreedom => Abilities.Any(x => x.AbilityId == 81);
    public bool RemovedByFreedom => Abilities.Any(x => x.AbilityId is 68 or 74 or 87);
    public bool CausesFear => Abilities.Any(x => x.AbilityId == 60);
    public bool NonMagical => Abilities.Any(x => x.AbilityId == 144);
    public IReadOnlyList<int> TextblockIds => Abilities.Where(x => x.AbilityId == 148 && x.Value > 0).Select(x => x.Value).Distinct().ToArray();
    public bool RuneMarker => Abilities.Any(x => x.AbilityId == 152 && x.Value == 0);
    public IReadOnlyList<int> RemovedLegacySpellIds => Abilities
        .Where(x => x.AbilityId == 122 && x.Value > 0)
        .Select(x => x.Value)
        .Distinct()
        .ToArray();
    public IReadOnlyList<int> KilledLegacySpellIds => Abilities
        .Where(x => x.AbilityId == 153 && x.Value > 0)
        .Select(x => x.Value)
        .Distinct()
        .ToArray();

    public int RollMinimum => Math.Min(MinimumEffect, MaximumEffect);
    public int RollMaximum => Math.Max(MinimumEffect, MaximumEffect);
    public bool HasPositiveMagnitude => RollMaximum > 0;
    public bool HasPeriodicDamage => DurationRounds > 0 && HasPositiveMagnitude &&
        (IsPoison || Abilities.Any(x => x.AbilityId is 1 or 17));
    public bool HasCombatModifiers => Abilities.Any(x => x.AbilityId is 2 or 3 or 4 or 5 or 7 or 22 or 24 or 25 or 27 or 34 or 36 or 44 or 45 or 46 or 48 or 49 or 58 or 65 or 66 or 68 or 69 or 71 or 72 or 77 or 87 or 88 or 89 or 90 or 91 or 92 or 93 or 94 or 96 or 99 or 105 or 106 or 116 or 117 or 118 or 123 or 142 or 145);
}

public sealed class RuntimeClassicLiveStatusCatalog
{
    public const int CombatRoundSeconds = RuntimeClassicTimingAuthority.CombatRoundSeconds;
    public const int SpellRoundSeconds = RuntimeClassicTimingAuthority.SpellSystemRoundSeconds;
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
        var path = Path.Combine(ServerDataRootResolver.Resolve(projectRoot, worldId).DataRoot, "spells.json");
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
        var previousMaximumHitPointBonus = Numeric(character, "maximum_hit_points");
        var previousMaximumManaBonus = Numeric(character, "maximum_mana");
        var baseMaximumHitPoints = Math.Max(1, checked(character.MaximumHitPoints - previousMaximumHitPointBonus));
        var baseMaximumMana = Math.Max(0, checked(character.MaximumMana - previousMaximumManaBonus));
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

        var negatedAbilityIds = effects
            .SelectMany(effect => catalog.TryGet(effect.SpellId, out var definition)
                ? definition.Abilities.Where(ability => ability.AbilityId == 124 && ability.Value > 0).Select(ability => ability.Value)
                : Array.Empty<int>())
            .ToHashSet();

        foreach (var effect in effects)
        {
            if (!catalog.TryGet(effect.SpellId, out var definition)) continue;
            var confusion = definition.Abilities.FirstOrDefault(x => x.AbilityId == 71);
            if (confusion is not null && !negatedAbilityIds.Contains(71))
            {
                var chance = Math.Clamp(ResolveAbilityValue(confusion, effect), 0, 100);
                if (chance >= numeric.GetValueOrDefault("confusion_chance"))
                {
                    numeric["confusion_chance"] = chance;
                    numeric["confusion_message_id"] = definition.Abilities.FirstOrDefault(x => x.AbilityId == 101)?.Value ?? 0;
                }
            }
            foreach (var ability in definition.Abilities)
            {
                if (ability.AbilityId != 124 && negatedAbilityIds.Contains(ability.AbilityId)) continue;
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
                    case 24: Add(numeric, "protection_from_evil", resolved); break;
                    case 25: Add(numeric, "protection_from_good", resolved); break;
                    case 27: Add(numeric, "stealth", resolved); break;
                    case 34: Add(numeric, "dodge", resolved); break;
                    case 36: Add(numeric, "magic_resistance", resolved); break;
                    case 37: Add(numeric, "picklocks", resolved); break;
                    case 44: Add(numeric, "stat_intellect", resolved); break;
                    case 45: Add(numeric, "stat_wisdom", resolved); break;
                    case 46: Add(numeric, "stat_strength", resolved); break;
                    case 48: Add(numeric, "stat_agility", resolved); break;
                    case 49: Add(numeric, "stat_charm", resolved); break;
                    case 58: Add(numeric, "critical", resolved); break;
                    case 65: Add(numeric, "resist_stone", resolved); break;
                    case 66: Add(numeric, "resist_lightning", resolved); break;
                    case 68: Add(numeric, "slowness", resolved); break;
                    case 69: Add(numeric, "maximum_mana", resolved); break;
                    case 72: Add(numeric, "damage_shield", resolved); break;
                    case 77: Add(numeric, "perception", resolved); break;
                    case 87: Add(numeric, "speed", resolved); break;
                    case 88: Add(numeric, "maximum_hit_points", resolved); break;
                    case 89: Add(numeric, "punch_accuracy", resolved); break;
                    case 90: Add(numeric, "kick_accuracy", resolved); break;
                    case 91: Add(numeric, "jumpkick_accuracy", resolved); break;
                    case 92: Add(numeric, "punch_damage", resolved); break;
                    case 93: Add(numeric, "kick_damage", resolved); break;
                    case 94: Add(numeric, "jumpkick_damage", resolved); break;
                    case 96: Add(numeric, "encumbrance", resolved); break;
                    case 99: Add(numeric, "damage_resistance_percent", resolved); break;
                    case 116: Add(numeric, "backstab_accuracy", resolved); break;
                    case 117: Add(numeric, "backstab_minimum_damage", resolved); break;
                    case 118: Add(numeric, "backstab_maximum_damage", resolved); break;
                    case 123: Add(numeric, "hp_regen", resolved); break;
                    case 124:
                        if (resolved > 0) flags.Add($"negate_ability.{resolved}");
                        break;
                    case 145: Add(numeric, "mana_regeneration", resolved); break;
                    case 147: Add(numeric, "resist_water", resolved); break;
                    case 19: flags.Add("poisoned"); break;
                    case 21: flags.Add("poison_immune"); break;
                    case 57: flags.Add("see_hidden"); break;
                    case 60: flags.Add("feared"); break;
                    case 71:
                    case 101: break;
                    case 74: flags.Add("immobilized"); break;
                    case 76: flags.Add("muted"); break;
                    case 79: flags.Add("mage_bind_evidence"); break;
                    case 159: flags.Add("kai_bound"); break;
                    case 107: flags.Add("blinded"); break;
                    case 139: Add(numeric, "spell_immunity_level", resolved); break;
                    case 142: Add(numeric, "hit_magic", resolved); break;
                    case 158:
                        if (resolved > numeric.GetValueOrDefault("required_to_hit")) numeric["required_to_hit"] = resolved;
                        break;
                    case 178:
                        // Shadowform is a selector, not an additive statistic. The decoded value
                        // names the native look textblock; zero is the NPC/marker sentinel.
                        if (resolved > 0) numeric["shadowform_textblock"] = resolved;
                        break;
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

        var maximumHitPoints = Math.Max(1, checked(baseMaximumHitPoints + numeric.GetValueOrDefault("maximum_hit_points")));
        var maximumMana = Math.Max(0, checked(baseMaximumMana + numeric.GetValueOrDefault("maximum_mana")));
        return character with
        {
            MaximumHitPoints = maximumHitPoints,
            HitPoints = Math.Min(character.HitPoints, maximumHitPoints),
            MaximumMana = maximumMana,
            Mana = Math.Min(character.Mana, maximumMana),
            Conditions = conditions,
            UpdatedAtUtc = nowUtc
        };
    }

    public static RuntimeCharacterRecord Clear(RuntimeCharacterRecord character, DateTimeOffset nowUtc, string sourceRecordId)
    {
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        var maximumHitPoints = Math.Max(1, checked(character.MaximumHitPoints - Numeric(character, "maximum_hit_points")));
        var maximumMana = Math.Max(0, checked(character.MaximumMana - Numeric(character, "maximum_mana")));
        foreach (var key in conditions.Keys.Where(x => x.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            conditions.Remove(key);
        conditions[Prefix + "active_count"] = "0";
        conditions[Prefix + "source"] = sourceRecordId;
        return character with
        {
            MaximumHitPoints = maximumHitPoints,
            HitPoints = Math.Min(character.HitPoints, maximumHitPoints),
            MaximumMana = maximumMana,
            Mana = Math.Min(character.Mana, maximumMana),
            Conditions = conditions,
            UpdatedAtUtc = nowUtc
        };
    }

    public static bool IsPhysicalAttackBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "paralyzed");

    public static bool IsSpellCastBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "paralyzed");

    public static bool IsKaiPowerBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "kai_bound");

    public static bool IsSpeechBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "muted");

    public static bool IsVisionBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "blinded");

    public static bool ConfusionFumbles(RuntimeCharacterRecord character, int roll) =>
        roll >= 1 && roll <= Math.Clamp(Numeric(character, "confusion_chance"), 0, 100);

    public static bool IsMovementBlocked(RuntimeCharacterRecord character) =>
        Flag(character, "paralyzed") || Flag(character, "immobilized");

    public static bool MustFleeCombat(RuntimeCharacterRecord character) =>
        Flag(character, "feared");

    public static bool IsPoisonImmune(RuntimeCharacterRecord character) =>
        Flag(character, "poison_immune") ||
        character.SemanticState?.DerivedStats.TryGetValue("poison_immunity_rank", out var equipmentRank) == true && equipmentRank > 0;

    public static bool IsSpellImmune(RuntimeCharacterRecord character, int incomingSpellLevel) =>
        incomingSpellLevel >= 0 && Numeric(character, "spell_immunity_level") >= incomingSpellLevel
        && Numeric(character, "spell_immunity_level") > 0;

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
        if (ability.AbilityId is 19 or 20 or 21 or 52 or 57 or 60 or 73 or 74 or 76 or 79 or 107 or 108 or 122 or 139 or 144)
            return 1;
        if (effect.Metadata is not null
            && effect.Metadata.TryGetValue("effect_roll", out var rawRoll)
            && int.TryParse(rawRoll, NumberStyles.Integer, CultureInfo.InvariantCulture, out var roll))
            return roll;
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

    private bool IsLiveNpcAffectedByAbility(RuntimeSession session, string npcInstanceId, int abilityId)
    {
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            var catalog = ClassicLiveStatusCatalog(session);
            return _liveSpellEngine!.EffectsFor(npcInstanceId)
                .Any(effect => catalog.TryGet(effect.SpellId, out var definition)
                    && definition.Abilities.Any(ability => ability.AbilityId == abilityId));
        }
    }

    private bool IsLiveNpcFeared(RuntimeSession session, string npcInstanceId) =>
        IsLiveNpcAffectedByAbility(session, npcInstanceId, 60);

    private int ResolveLiveSpellImmunityThreshold(RuntimeSession session, RuntimeSpellTarget target, string excludedEffectId)
    {
        if (target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase))
            return _characters.LoadCharacter(target.Id) is { } character
                ? RuntimeClassicStatusProjection.Numeric(character, "spell_immunity_level") : 0;
        if (!target.Kind.Equals("npc", StringComparison.OrdinalIgnoreCase)) return 0;
        var catalog = ClassicLiveStatusCatalog(session);
        var inherent = Living(session).Capture().Npcs
            .FirstOrDefault(npc => npc.InstanceId.Equals(target.Id, StringComparison.OrdinalIgnoreCase))?
            .Metadata?.GetValueOrDefault("spell_immunity_level");
        var activeLevel = _liveSpellEngine!.EffectsFor(target.Id)
            .Where(effect => !effect.EffectId.Equals(excludedEffectId, StringComparison.OrdinalIgnoreCase))
            .Select(effect => catalog.TryGet(effect.SpellId, out var definition)
                ? definition.Abilities.Where(ability => ability.AbilityId == 139).Select(ability => ability.Value).DefaultIfEmpty(0).Max()
                : 0)
            .DefaultIfEmpty(0)
            .Max();
        return ResolveNpcSpellImmunityLevel(inherent, activeLevel);
    }

    internal static int ResolveNpcSpellImmunityLevel(string? inherentValue, int activeEffectLevel)
    {
        var inherentLevel = int.TryParse(inherentValue, out var parsed) ? Math.Max(0, parsed) : 0;
        return Math.Max(inherentLevel, Math.Max(0, activeEffectLevel));
    }

    private static IReadOnlyDictionary<string, string> BuildClassicStatusMetadata(
        RuntimeClassicLiveStatusDefinition definition,
        RuntimeNpcHitSpellSelection spell,
        DateTimeOffset nowUtc,
        int effectRoll)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "npc_hit_spell",
            ["effect_kind"] = definition.IsPoison ? "poison" : "status",
            ["spell_type_raw"] = spell.SpellTypeRaw.ToString(CultureInfo.InvariantCulture),
            ["attack_type_raw"] = spell.AttackTypeRaw.ToString(CultureInfo.InvariantCulture),
            ["duration_unit"] = "spell_rounds",
            ["duration_rounds"] = spell.DurationRaw.ToString(CultureInfo.InvariantCulture),
            ["round_seconds"] = RuntimeClassicLiveStatusCatalog.SpellRoundSeconds.ToString(CultureInfo.InvariantCulture),
            ["stacking"] = "refresh_same_spell_independent_different_spells",
            ["remove_on_death"] = "1",
            ["persist_across_logout"] = "1",
            ["non_magical"] = definition.NonMagical ? "1" : "0"
        };
        metadata["effect_roll"] = effectRoll.ToString(CultureInfo.InvariantCulture);
        if (definition.HasPeriodicDamage)
        {
            metadata["periodic_operation"] = "damage";
            metadata["tick_interval_rounds"] = "1";
            metadata["next_tick_utc"] = nowUtc.AddSeconds(RuntimeClassicLiveStatusCatalog.SpellRoundSeconds).ToString("O", CultureInfo.InvariantCulture);
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
            ["duration_unit"] = "spell_rounds",
            ["duration_rounds"] = castEffect.Duration.ToString(CultureInfo.InvariantCulture),
            ["round_seconds"] = RuntimeClassicLiveStatusCatalog.SpellRoundSeconds.ToString(CultureInfo.InvariantCulture),
            ["stacking"] = "refresh_same_spell_independent_different_spells",
            ["remove_on_death"] = "1",
            ["persist_across_logout"] = "1",
            ["non_magical"] = definition.NonMagical ? "1" : "0"
        };
        if (definition.HasPeriodicDamage)
        {
            metadata["periodic_operation"] = "damage";
            metadata["tick_interval_rounds"] = "1";
            metadata["next_tick_utc"] = nowUtc.AddSeconds(RuntimeClassicLiveStatusCatalog.SpellRoundSeconds).ToString("O", CultureInfo.InvariantCulture);
        }
        foreach (var ability in definition.Abilities)
            metadata[$"ability.{ability.AbilityId}"] = ability.Value.ToString(CultureInfo.InvariantCulture);
        var normalized = castEffect with
        {
            ExpiresAtUtc = nowUtc.AddSeconds(checked((long)castEffect.Duration * RuntimeClassicLiveStatusCatalog.SpellRoundSeconds)),
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
                && unit.Equals("spell_rounds", StringComparison.OrdinalIgnoreCase)) return effect;
            changed = true;
            metadata["duration_unit"] = "spell_rounds";
            metadata["duration_rounds"] = effect.Duration.ToString(CultureInfo.InvariantCulture);
            metadata["round_seconds"] = RuntimeClassicLiveStatusCatalog.SpellRoundSeconds.ToString(CultureInfo.InvariantCulture);
            if (definition.HasPeriodicDamage)
            {
                metadata["periodic_operation"] = "damage";
                metadata["tick_interval_rounds"] = "1";
                metadata["next_tick_utc"] = effect.AppliedAtUtc.AddSeconds(RuntimeClassicLiveStatusCatalog.SpellRoundSeconds).ToString("O", CultureInfo.InvariantCulture);
            }
            return effect with
            {
                ExpiresAtUtc = effect.AppliedAtUtc.AddSeconds(checked((long)effect.Duration * RuntimeClassicLiveStatusCatalog.SpellRoundSeconds)),
                Metadata = metadata
            };
        }).ToArray();
        return changed ? state with { ActiveEffects = active } : state;
    }

    private readonly HashSet<int> _unresolvedClassicRoomSpellEffects = new();

    private void TickClassicNativeRoomSpell(RuntimeSession session, DateTimeOffset nowUtc)
    {
        if (!session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase)
            || session.Actor is not RuntimeCharacterActor actor
            || !session.CurrentRoom.EnvironmentRoomSpellNativeVerified
            || session.CurrentRoom.EnvironmentRoomSpellId <= 0)
            return;

        var legacyId = session.CurrentRoom.EnvironmentRoomSpellId;
        var spell = NpcSpellCatalog(session.World.Definition.Id).Spells.FirstOrDefault(candidate =>
            int.TryParse(candidate.LegacyId, out var parsed) && parsed == legacyId);
        if (spell is null)
            throw new InvalidDataException(
                $"Original room spell {legacyId} in {session.CurrentRoom.Id} is absent from compiled spells.");

        // Native _room_cast_on_user has separate chance/resistance/effect gates.
        // Only already-supported idempotent removal effects can be applied
        // without inventing damage, teleport, item scatter, or textblock scripts.
        var abilities = spell.DecodedAbilities;
        var safeRemoval = abilities.Any(ability => ability.AbilityId is 122 or 153)
            && abilities.All(ability => ability.AbilityId is 122 or 153 or 144 or 148 or 115);
        if (!safeRemoval)
        {
            lock (_livePowerGate)
            {
                if (_unresolvedClassicRoomSpellEffects.Add(legacyId))
                    System.Diagnostics.Trace.TraceWarning(
                        $"Native room spell {legacyId} ({spell.Name}) requires unverified execution gates; skipped unsafe ambient effects.");
            }
            return;
        }

        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            var target = new RuntimeSpellTarget("character", actor.Id);
            var removed = ApplyDecodedCureAndDispel(session, spell, target);
            if (removed <= 0) return;
            SynchronizeCharacterSpellStatuses(target, nowUtc, "runtime.classic.native_room_spell");
            PersistLivePowers(session.World.Definition.Id);
        }
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
                                if (targetSession is not null)
                                {
                                    // Online players use the same native death path as combat:
                                    // resolve death consequences and immediately revive at the
                                    // authoritative respawn destination. Do not leave them in a
                                    // manual respawn/recover state after periodic damage.
                                    var deathMessage = CompletePlayerDefeat(targetSession, character);
                                    targetSession.EnqueueMessage(deathMessage);
                                }
                                else
                                {
                                    // Offline characters cannot be moved to a live session.
                                    // Persist the corpse/dead state for their next login.
                                    var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
                                    var defeated = RuntimeCharacterDeathResolver.Defeat(
                                        RuntimeClassicStatusProjection.Clear(character, nextTick, "runtime.live_status.periodic_death"),
                                        itemCatalog,
                                        character.RoomId,
                                        nextTick);
                                    _characters.SaveCharacter(defeated.Character);
                                }
                                deadTargets.Add(character.CharacterId);
                                break;
                            }
                            _characters.SaveCharacter(character with { HitPoints = nextHp, UpdatedAtUtc = nextTick });
                        }
                        nextTick = nextTick.AddSeconds(RuntimeClassicLiveStatusCatalog.SpellRoundSeconds);
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
            ExpireAndExecuteDecodedEndCast(session, nowUtc);

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
        foreach (var legacyId in definition.KilledLegacySpellIds)
            if (catalog.TryGetLegacy(legacyId.ToString(CultureInfo.InvariantCulture), out var killedSpell))
                foreach (var effect in effects.Where(x => x.SpellId.Equals(killedSpell.SpellId, StringComparison.OrdinalIgnoreCase)))
                    remove.Add(effect.EffectId);
        foreach (var abilityId in definition.DispelledAbilityIds)
            foreach (var effect in effects)
                if (catalog.TryGet(effect.SpellId, out var activeDefinition)
                    && activeDefinition.Abilities.Any(ability => ability.AbilityId == abilityId))
                    remove.Add(effect.EffectId);
        if (definition.GeneralMagicDispel)
        {
            foreach (var effect in effects)
            {
                var nonMagical = effect.Metadata is not null
                    && effect.Metadata.TryGetValue("non_magical", out var raw)
                    && raw.Equals("1", StringComparison.OrdinalIgnoreCase);
                if (!nonMagical) remove.Add(effect.EffectId);
            }
        }
        if (definition.GrantsFreedom)
        {
            foreach (var effect in effects)
                if (catalog.TryGet(effect.SpellId, out var activeDefinition) && activeDefinition.RemovedByFreedom)
                    remove.Add(effect.EffectId);
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
            var removesSpellOwners = catalog.Definitions.Where(x => x.Abilities.Any(ability => ability.AbilityId == 122)).ToArray();
            Require(removesSpellOwners.Length == 107, "decoded-removes-spell-owner-count-107"); report.Checks.Add("decoded-removes-spell-owner-count-107");
            Require(removesSpellOwners.Sum(x => x.Abilities.Count(ability => ability.AbilityId == 122)) == 334, "decoded-removes-spell-relationship-count-334"); report.Checks.Add("decoded-removes-spell-relationship-count-334");
            Require(removesSpellOwners.Sum(x => x.RemovedLegacySpellIds.Count) == 331, "decoded-removes-spell-positive-target-count-331"); report.Checks.Add("decoded-removes-spell-positive-target-count-331");
            Require(removesSpellOwners.Sum(x => x.Abilities.Count(ability => ability.AbilityId == 122 && ability.Value == 0)) == 3, "decoded-removes-spell-zero-sentinel-count-3"); report.Checks.Add("decoded-removes-spell-zero-sentinel-count-3");
            Require(removesSpellOwners.SelectMany(x => x.RemovedLegacySpellIds).All(id => catalog.TryGetLegacy(id.ToString(CultureInfo.InvariantCulture), out _)), "decoded-removes-spell-all-positive-targets-resolve"); report.Checks.Add("decoded-removes-spell-all-positive-targets-resolve");
            Require(catalog.Definitions.Count(x => x.DispelsMagic) == 16, "decoded-dispel-policy-count"); report.Checks.Add("decoded-dispel-policy-count-16");
            Require(catalog.Definitions.Count(x => x.GeneralMagicDispel) == 8, "decoded-general-dispel-policy-count"); report.Checks.Add("decoded-general-dispel-policy-count-8");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 142 && a.Value == 10)) == 2, "decoded-hitmagic-spell-count"); report.Checks.Add("decoded-hitmagic-two-value-10-spells");
            Require(catalog.Definitions.All(x => x.Abilities.All(a => a.AbilityId != 143)), "decoded-clearitem-zero-owner-count"); report.Checks.Add("decoded-clearitem-zero-spell-owners");
            Require(catalog.Definitions.Count(x => x.NonMagical) == 318, "decoded-nonmagical-spell-count"); report.Checks.Add("decoded-nonmagical-spell-count-318");
            Require(catalog.Definitions.Count(x => x.DispelledAbilityIds.Contains(107)) == 4, "decoded-blindness-dispel-count"); report.Checks.Add("decoded-blindness-dispel-count-4");
            Require(catalog.Definitions.Count(x => x.DispelledAbilityIds.Contains(19)) == 4, "decoded-poison-dispel-count"); report.Checks.Add("decoded-poison-dispel-count-4");
            Require(catalog.Definitions.Any(x => x.HasPeriodicDamage), "decoded-periodic-status-policies"); report.Checks.Add("decoded-periodic-status-policies");
            var holdSpells = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 74)).ToArray();
            Require(holdSpells.Length == 41, "decoded-hold-person-policy-count"); report.Checks.Add("decoded-hold-person-policy-count-41");
            Require(catalog.Definitions.Any(x => x.Abilities.Any(a => a.AbilityId is 76 or 79)), "decoded-silence-policies"); report.Checks.Add("decoded-silence-policies");
            var confusionSpells = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 71)).ToArray();
            Require(confusionSpells.Length == 84, "decoded-confusion-spell-count"); report.Checks.Add("decoded-confusion-spell-count-84");
            Require(confusionSpells.Count(x => x.Abilities.Any(a => a.AbilityId == 101)) == 77, "decoded-confusion-message-count"); report.Checks.Add("decoded-confusion-message-count-77");
            Require(catalog.Definitions.Count(x => x.CausesFear) == 14, "decoded-fear-spell-count"); report.Checks.Add("decoded-fear-spell-count-14");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 76)) == 4, "decoded-mute-spell-count"); report.Checks.Add("decoded-mute-spell-count-4");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 77)) == 14, "decoded-perception-spell-count"); report.Checks.Add("decoded-perception-spell-count-14");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 80)) == 1, "decoded-affects-animals-spell-count"); report.Checks.Add("decoded-affects-animals-spell-count-1");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 84)) == 2, "decoded-remove-curse-spell-count"); report.Checks.Add("decoded-remove-curse-spell-count-2");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 97)) == 19, "decoded-good-only-spell-count"); report.Checks.Add("decoded-good-only-spell-count-19");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 98)) == 20, "decoded-evil-only-spell-count"); report.Checks.Add("decoded-evil-only-spell-count-20");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 110)) == 1, "decoded-not-good-spell-count"); report.Checks.Add("decoded-not-good-spell-count-1");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 111)) == 3, "decoded-not-evil-spell-count"); report.Checks.Add("decoded-not-evil-spell-count-3");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 112)) == 10, "decoded-neutral-only-spell-count"); report.Checks.Add("decoded-neutral-only-spell-count-10");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 113)) == 0, "decoded-not-neutral-spell-count"); report.Checks.Add("decoded-not-neutral-spell-count-0");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 116)) == 1, "decoded-backstab-accuracy-spell-count"); report.Checks.Add("decoded-backstab-accuracy-spell-count-1");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 117)) == 3, "decoded-backstab-minimum-spell-count"); report.Checks.Add("decoded-backstab-minimum-spell-count-3");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 118)) == 3, "decoded-backstab-maximum-spell-count"); report.Checks.Add("decoded-backstab-maximum-spell-count-3");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 123)) == 16, "decoded-hit-point-regeneration-spell-count"); report.Checks.Add("decoded-hit-point-regeneration-spell-count-16");
            var negateAbilitySpells = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 124)).ToArray();
            Require(negateAbilitySpells.Length == 5, "decoded-negate-ability-spell-count"); report.Checks.Add("decoded-negate-ability-spell-count-5");
            Require(negateAbilitySpells.Sum(x => x.Abilities.Count(a => a.AbilityId == 124 && a.Value > 0)) == 4, "decoded-negate-ability-positive-count"); report.Checks.Add("decoded-negate-ability-positive-count-4");
            Require(negateAbilitySpells.Sum(x => x.Abilities.Count(a => a.AbilityId == 124 && a.Value == 0)) == 1, "decoded-negate-ability-zero-sentinel-count"); report.Checks.Add("decoded-negate-ability-zero-sentinel-count-1");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 108)) == 64, "decoded-affects-living-spell-count"); report.Checks.Add("decoded-affects-living-spell-count-64");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 79)) == 3, "decoded-mage-bind-evidence-count"); report.Checks.Add("decoded-mage-bind-evidence-count-3");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 107)) == 20, "decoded-blind-user-spell-count"); report.Checks.Add("decoded-blind-user-spell-count-20");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 139)) == 2, "decoded-spell-immunity-spell-count"); report.Checks.Add("decoded-spell-immunity-spell-count-2");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 145)) == 26, "decoded-mana-regeneration-spell-count"); report.Checks.Add("decoded-mana-regeneration-spell-count-26");
            var kaiBindOwners = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 159)).ToArray();
            Require(kaiBindOwners.Length == 1 && kaiBindOwners[0].SpellId == "spell.858"
                && kaiBindOwners[0].Abilities.Single(a => a.AbilityId == 159).Value == 0,
                "decoded-kai-bind-single-owner-form-of-dragon");
            report.Checks.Add("decoded-kai-bind-single-owner-form-of-dragon");
            var requiredToHitSpells = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 158)).ToArray();
            Require(requiredToHitSpells.Length == 1
                && requiredToHitSpells[0].SpellId == "spell.307"
                && requiredToHitSpells[0].Abilities.Single(a => a.AbilityId == 158).Value == 9,
                "decoded-required-to-hit-spell-307-code-9");
            report.Checks.Add("decoded-required-to-hit-spell-307-code-9");
            var shadowformSpells = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 178)).ToArray();
            Require(shadowformSpells.Length == 2
                && shadowformSpells.All(x => x.Abilities.Single(a => a.AbilityId == 178).Value == 4157),
                "decoded-shadowform-spell-selector-count");
            report.Checks.Add("decoded-shadowform-two-spells-textblock-4157");
            Require(!RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot).TryResolveTextblock(4157, out _),
                "decoded-shadowform-textblock-remains-unresolved");
            report.Checks.Add("decoded-shadowform-textblock-4157-fails-closed");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 148)) == 180, "decoded-textblock-control-count"); report.Checks.Add("decoded-textblock-control-count-180");
            var textblockOwners = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 148)).ToArray();
            Require(textblockOwners.Sum(x => x.Abilities.Count(a => a.AbilityId == 148 && a.Value > 0)) == 171,
                "decoded-textblock-positive-link-count"); report.Checks.Add("decoded-textblock-positive-link-count-171");
            Require(textblockOwners.Sum(x => x.Abilities.Count(a => a.AbilityId == 148 && a.Value == 0)) == 28,
                "decoded-textblock-zero-sentinel-count"); report.Checks.Add("decoded-textblock-zero-sentinel-count-28");
            var nativeTextblocks = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
            var distinctTextblockTargets = textblockOwners.SelectMany(x => x.TextblockIds).Distinct().ToArray();
            Require(distinctTextblockTargets.Length == 144, "decoded-textblock-distinct-target-count"); report.Checks.Add("decoded-textblock-distinct-target-count-144");
            Require(distinctTextblockTargets.Count(id => nativeTextblocks.TryResolveTextblock(id, out _)) == 74,
                "decoded-textblock-native-payload-resolution-count"); report.Checks.Add("decoded-textblock-native-payload-target-count-74");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 150)) == 26, "decoded-heal-mana-count"); report.Checks.Add("decoded-heal-mana-count-26");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 151)) == 88, "decoded-endcast-control-count"); report.Checks.Add("decoded-endcast-control-count-88");
            var endCastOwners = catalog.Definitions.Where(x => x.Abilities.Any(a => a.AbilityId == 151)).ToArray();
            Require(endCastOwners.Sum(x => x.Abilities.Count(a => a.AbilityId == 151 && a.Value > 0)) == 77,
                "decoded-endcast-positive-count"); report.Checks.Add("decoded-endcast-positive-link-count-77");
            Require(endCastOwners.Sum(x => x.Abilities.Count(a => a.AbilityId == 151 && a.Value == 0)) == 11,
                "decoded-endcast-zero-count"); report.Checks.Add("decoded-endcast-zero-sentinel-count-11");
            Require(endCastOwners.SelectMany(x => x.Abilities.Where(a => a.AbilityId == 151 && a.Value > 0))
                .All(link => catalog.TryGetLegacy(link.Value.ToString(CultureInfo.InvariantCulture), out _)),
                "decoded-endcast-target-resolution"); report.Checks.Add("decoded-endcast-all-positive-targets-resolve");
            Require(!HasEndCastCycle(catalog), "decoded-endcast-cycle-check"); report.Checks.Add("decoded-endcast-graph-acyclic");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 152)) == 4, "decoded-rune-control-count"); report.Checks.Add("decoded-rune-control-count-4");
            Require(catalog.Definitions.Count(x => x.RuneMarker) == 4 && catalog.Definitions.All(x => x.Abilities.All(a => a.AbilityId != 152 || a.Value == 0)),
                "decoded-rune-zero-marker-contract"); report.Checks.Add("decoded-rune-four-zero-markers-no-invented-id");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 153)) == 3, "decoded-killspell-control-count"); report.Checks.Add("decoded-killspell-control-count-3");
            var killSpellOwners = catalog.Definitions.Where(x => x.KilledLegacySpellIds.Count > 0).ToArray();
            Require(killSpellOwners.Length == 3 && killSpellOwners.Sum(x => x.KilledLegacySpellIds.Count) == 5,
                "decoded-killspell-positive-link-count"); report.Checks.Add("decoded-killspell-three-owners-five-links");
            Require(killSpellOwners.SelectMany(x => x.KilledLegacySpellIds).All(id => catalog.TryGetLegacy(id.ToString(CultureInfo.InvariantCulture), out _)),
                "decoded-killspell-target-resolution"); report.Checks.Add("decoded-killspell-all-targets-resolve");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 69)) == 4, "decoded-maximum-mana-spell-count"); report.Checks.Add("decoded-maximum-mana-spell-count-4");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 88)) == 1, "decoded-maximum-hit-points-spell-count"); report.Checks.Add("decoded-maximum-hit-points-spell-count-1");
            Require(catalog.Definitions.Count(x => x.Abilities.Any(a => a.AbilityId == 96)) == 9, "decoded-encumbrance-spell-count"); report.Checks.Add("decoded-encumbrance-spell-count-9");
            var freedomSpells = catalog.Definitions.Where(x => x.GrantsFreedom).ToArray();
            Require(freedomSpells.Length == 8, "decoded-freedom-spell-count"); report.Checks.Add("decoded-freedom-spell-count-8");
            Require(catalog.Definitions.Count(x => x.RemovedByFreedom) > 0
                && catalog.Definitions.Any(x => x.Abilities.Any(a => a.AbilityId == 74) && x.RemovedByFreedom)
                && catalog.Definitions.Any(x => x.Abilities.Any(a => a.AbilityId == 68) && x.RemovedByFreedom)
                && catalog.Definitions.Any(x => x.Abilities.Any(a => a.AbilityId == 87) && x.RemovedByFreedom),
                "decoded-freedom-removal-classification"); report.Checks.Add("decoded-freedom-removes-hold-and-speed-effects");
            Require(RuntimeClassicLiveStatusCatalog.CombatRoundSeconds == 5, "combat-round-five-seconds"); report.Checks.Add("combat-round-five-seconds");
            Require(RuntimeClassicLiveStatusCatalog.SpellRoundSeconds == 3, "spell-round-three-seconds"); report.Checks.Add("spell-round-three-seconds");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;

        static bool HasEndCastCycle(RuntimeClassicLiveStatusCatalog catalog)
        {
            foreach (var root in catalog.Definitions)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var current = root;
                while (true)
                {
                    if (!seen.Add(current.SpellId)) return true;
                    var next = current.Abilities.FirstOrDefault(a => a.AbilityId == 151 && a.Value > 0);
                    if (next is null || !catalog.TryGetLegacy(next.Value.ToString(CultureInfo.InvariantCulture), out current!)) break;
                }
            }
            return false;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
