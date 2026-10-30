using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeSpellDescriptionMessage(int MessageId, string WearOffMessage, string LookMessage, string StatusMessage);
public sealed record RuntimeSpellStartMessage(int MessageId, string CasterMessage, string TargetMessage, string ObserverMessage);

public sealed record RuntimeSpellDefinition(
    string Id,
    string LegacyId,
    string Name,
    string ShortName,
    int SpellLevel,
    string SpellType,
    string TargetType,
    string AttackType,
    int ManaCost,
    int EnergyCost,
    int MinimumEffect,
    int MaximumEffect,
    int Duration,
    IReadOnlyList<string> FormulaReferences,
    IReadOnlyList<RuntimeClassicLiveStatusAbility> DecodedAbilities,
    int LevelsForMinimumIncrease = 0,
    int MinimumIncrease = 0,
    int LevelsForMaximumIncrease = 0,
    int MaximumIncrease = 0,
    int LevelCap = 0,
    int CastMessageId = 0,
    string CastMessage = "",
    int DescriptionMessageId = 0,
    string DescriptionMessage = "",
    string DescriptionLookMessage = "",
    string DescriptionStatusMessage = "",
    IReadOnlyList<RuntimeSpellDescriptionMessage>? DescriptionMessages = null,
    RuntimeSpellStartMessage? StartMessage = null,
    int NativeResistTypeRaw = 0,
    int NativeResistAbilityRaw = 0);

public sealed record RuntimeKnownSpell(string CharacterId, string SpellId, DateTimeOffset LearnedAtUtc, IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record RuntimeSpellResources(int Mana, int Energy);
public sealed record RuntimeSpellTarget(string Kind, string Id, IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record RuntimeActiveSpellEffect(
    string EffectId,
    string SpellId,
    string CasterId,
    RuntimeSpellTarget Target,
    int MinimumEffect,
    int MaximumEffect,
    int Duration,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record RuntimeSpellCastRequest(string CasterId, string SpellId, RuntimeSpellTarget Target, DateTimeOffset RequestedAtUtc);
public sealed record RuntimeSpellCastResult(bool Succeeded, string? FailureCode, string? FailureMessage, RuntimeSpellResources Resources, RuntimeActiveSpellEffect? Effect);
public sealed record RuntimeSpellState(IReadOnlyList<RuntimeKnownSpell> KnownSpells, IReadOnlyList<RuntimeActiveSpellEffect> ActiveEffects, IReadOnlyDictionary<string, RuntimeSpellResources> Resources);
public sealed record RuntimeSpellValidationIssue(string Code, string Message, string? EntityId = null);
public sealed record RuntimeSpellValidationReport(bool Passed, IReadOnlyList<RuntimeSpellValidationIssue> Issues);

public interface IRuntimeSpellPolicy
{
    RuntimeSpellValidationIssue? ValidateCast(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request, RuntimeSpellState state);
}

public interface IRuntimeSpellEffectResolver
{
    RuntimeActiveSpellEffect Resolve(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request);
}

public sealed class GeneratedSpellEffectResolver : IRuntimeSpellEffectResolver
{
    public RuntimeActiveSpellEffect Resolve(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request)
    {
        DateTimeOffset? expires = spell.Duration > 0
            ? request.RequestedAtUtc.AddSeconds(spell.Duration)
            : null;
        return new RuntimeActiveSpellEffect(
            Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target,
            spell.MinimumEffect, spell.MaximumEffect, spell.Duration,
            request.RequestedAtUtc, expires,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["spell_type"] = spell.SpellType,
                ["target_type"] = spell.TargetType,
                ["attack_type"] = spell.AttackType,
                ["formula_refs"] = string.Join(",", spell.FormulaReferences)
            });
    }
}

public sealed class RuntimeSpellCatalog
{
    private readonly Dictionary<string, RuntimeSpellDefinition> _spells = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<RuntimeSpellDefinition> Spells => _spells.Values.ToArray();
    public RuntimeSpellDefinition Require(string id) => _spells.TryGetValue(id, out var spell) ? spell : throw new InvalidOperationException($"Unknown generated spell '{id}'.");

    public static RuntimeSpellCatalog Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "spells.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Librarium spell projection is missing.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Generated spell projection has no records array.");
        var catalog = new RuntimeSpellCatalog();
        RuntimeClassicNativeTextblockPayloadAuthority? nativeMessages = null;
        foreach (var record in records.EnumerateArray())
        {
            var id = String(record, "id") ?? throw new InvalidOperationException("Generated spell is missing id.");
            var classification = record.GetProperty("classification");
            var costs = record.GetProperty("costs");
            var effect = record.GetProperty("effect");
            var formulas = record.TryGetProperty("formula_refs", out var refs) && refs.ValueKind == JsonValueKind.Array
                ? refs.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray()
                : Array.Empty<string>();
            var decodedAbilities = new List<RuntimeClassicLiveStatusAbility>();
            var levelsForMinimumIncrease = 0;
            var minimumIncrease = 0;
            var levelsForMaximumIncrease = 0;
            var maximumIncrease = 0;
            var levelCap = 0;
            var nativeResistType = 0;
            var nativeResistAbility = 0;
            var castMessageId = 0;
            var castMessage = string.Empty;
            var descriptionMessageId = 0;
            var descriptionMessage = string.Empty;
            var descriptionLookMessage = string.Empty;
            var descriptionStatusMessage = string.Empty;
            IReadOnlyList<RuntimeSpellDescriptionMessage> descriptionMessages = Array.Empty<RuntimeSpellDescriptionMessage>();
            RuntimeSpellStartMessage? startMessage = null;
            if (record.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Object)
            {
                castMessageId = Integer(messages, "cast_message_id");
                descriptionMessageId = Integer(messages, "description_message_id");
                var castRequired = Boolean(messages, "cast_message_required");
                var descriptionRequired = Boolean(messages, "description_message_required");
                if (castMessageId > 0 || descriptionMessageId > 0)
                    nativeMessages ??= RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot, worldId);
                if (castMessageId > 0 && nativeMessages is not null) castMessage = nativeMessages.RequireMessage(castMessageId);
                else if (castRequired) throw new InvalidOperationException($"Generated spell '{id}' requires a resolved cast message.");
                if (descriptionMessageId <= 0 && descriptionRequired) throw new InvalidOperationException($"Generated spell '{id}' requires a resolved description message.");
            }
            if (record.TryGetProperty("public_reference_0139", out var publicReference)
                && publicReference.TryGetProperty("fields", out var publicFields)
                && publicFields.ValueKind == JsonValueKind.Object)
            {
                levelsForMinimumIncrease = Integer(publicFields, "levels_for_min_increase");
                minimumIncrease = Integer(publicFields, "min_increase");
                levelsForMaximumIncrease = Integer(publicFields, "levels_for_max_increase");
                maximumIncrease = Integer(publicFields, "max_increase");
                levelCap = Integer(publicFields, "level_cap");
                nativeResistType = Integer(publicFields, "resist_type");
                nativeResistAbility = Integer(publicFields, "resist_ability");
                if (publicFields.TryGetProperty("abilities_named", out var abilityArray)
                    && abilityArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ability in abilityArray.EnumerateArray())
                    {
                        var abilityId = Integer(ability, "ability_id");
                        if (abilityId <= 0) continue;
                        decodedAbilities.Add(new(abilityId, String(ability, "name") ?? $"ability.{abilityId}", Integer(ability, "value")));
                    }
                }
            }
            var startSelector = castMessageId > 0
                ? castMessageId
                : decodedAbilities.FirstOrDefault(ability => ability.AbilityId == 120 && ability.Value > 0)?.Value ?? 0;
            if (startSelector > 0)
            {
                nativeMessages ??= RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot, worldId);
                if (nativeMessages is null) throw new InvalidOperationException($"Generated spell '{id}' has a StartMsg selector without a native message authority.");
                var casterMessage = nativeMessages.RequireMessage(startSelector);
                nativeMessages.TryResolveSecondaryMessage(startSelector, out var targetMessage);
                nativeMessages.TryResolveTertiaryMessage(startSelector, out var observerMessage);
                startMessage = new(startSelector, casterMessage, targetMessage, observerMessage);
                castMessageId = startSelector;
                castMessage = casterMessage;
            }
            var descriptionSelectors = descriptionMessageId > 0
                ? new[] { descriptionMessageId }
                : decodedAbilities.Where(ability => ability.AbilityId == 115 && ability.Value > 0)
                    .Select(ability => ability.Value).Distinct().ToArray();
            if (descriptionSelectors.Length > 0)
            {
                nativeMessages ??= RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot, worldId);
                if (nativeMessages is null) throw new InvalidOperationException($"Generated spell '{id}' has description selectors without a native message authority.");
                descriptionMessages = descriptionSelectors.Select(selector =>
                {
                    var wearOff = nativeMessages.RequireMessage(selector);
                    nativeMessages.TryResolveSecondaryMessage(selector, out var look);
                    nativeMessages.TryResolveTertiaryMessage(selector, out var status);
                    return new RuntimeSpellDescriptionMessage(selector, wearOff, look, status);
                }).ToArray();
                descriptionMessageId = descriptionMessages[0].MessageId;
                descriptionMessage = descriptionMessages[0].WearOffMessage;
                descriptionLookMessage = descriptionMessages[0].LookMessage;
                descriptionStatusMessage = descriptionMessages[0].StatusMessage;
            }
            if (!catalog._spells.TryAdd(id, new RuntimeSpellDefinition(
                id,
                String(record, "legacy_id") ?? string.Empty,
                String(record, "name") ?? string.Empty,
                String(record, "short_name") ?? string.Empty,
                Integer(classification, "spell_level"),
                String(classification, "spell_type") ?? string.Empty,
                String(classification, "target_type") ?? string.Empty,
                String(classification, "attack_type") ?? string.Empty,
                Integer(costs, "mana_cost"),
                Integer(costs, "energy_cost"),
                Integer(effect, "min_effect"),
                Integer(effect, "max_effect"),
                Integer(effect, "duration"), formulas, decodedAbilities,
                levelsForMinimumIncrease, minimumIncrease,
                levelsForMaximumIncrease, maximumIncrease, levelCap,
                castMessageId, castMessage, descriptionMessageId, descriptionMessage,
                descriptionLookMessage, descriptionStatusMessage, descriptionMessages, startMessage,
                nativeResistType, nativeResistAbility)))
                throw new InvalidOperationException($"Duplicate generated spell id '{id}'.");
        }
        return catalog;
    }

    private static string? String(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int Integer(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }
    private static bool Boolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed;
    }
}

public sealed class RuntimeSpellEngine
{
    private readonly RuntimeSpellCatalog _catalog;
    private readonly IRuntimeSpellEffectResolver _resolver;
    private readonly IReadOnlyList<IRuntimeSpellPolicy> _policies;
    private Dictionary<string, RuntimeKnownSpell> _known = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, RuntimeActiveSpellEffect> _effects = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, RuntimeSpellResources> _resources = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly bool _enforceClassicNativeActiveSlots;

    // Native _add_cast_spell_to_user holds ten active spells; the corresponding
    // monster routine holds five. This admission check applies only to Classic
    // duration-bearing effects, not immediate spells or Custom/AI rules.
    public static int ClassicNativeActiveSlotLimit(string targetKind) =>
        targetKind.Equals("character", StringComparison.OrdinalIgnoreCase) ? 10 :
        targetKind.Equals("npc", StringComparison.OrdinalIgnoreCase) ? 5 : 0;

    // Shared native admission rule, including NPC on-hit status delivery,
    // which bypasses the normal RuntimeSpellEngine.Cast entry point.
    internal static bool ClassicNativeCanAdmitDurationSpell(
        IEnumerable<RuntimeActiveSpellEffect> effects,
        string targetKind, string targetId, string spellId, DateTimeOffset nowUtc)
    {
        var limit = ClassicNativeActiveSlotLimit(targetKind);
        if (limit == 0) return true;
        var active = effects.Where(effect =>
            Same(effect.Target.Kind, targetKind) &&
            Same(effect.Target.Id, targetId) &&
            effect.Duration > 0 &&
            (effect.ExpiresAtUtc is null || effect.ExpiresAtUtc > nowUtc)).ToArray();
        // Existing same-spell candidates can refresh via the separately gated
        // native caller path; a genuinely new spell needs a free slot.
        return active.Length < limit || active.Any(effect => Same(effect.SpellId, spellId));
    }

    // Original WCCMMUD _add_cast_spell_to_monster at 0x43ED64 refreshes the
    // existing same-spell slot with its new duration and magnitude. Player
    // refresh is caller-flag-dependent and is intentionally not promoted here.
    private void RefreshClassicNativeMonsterDuplicate(
        RuntimeSpellDefinition spell, RuntimeSpellTarget target, DateTimeOffset nowUtc)
    {
        if (!_enforceClassicNativeActiveSlots || spell.Duration <= 0
            || !Same(target.Kind, "npc")) return;
        var duplicateIds = _effects.Values.Where(effect =>
                Same(effect.Target.Kind, target.Kind)
                && Same(effect.Target.Id, target.Id)
                && Same(effect.SpellId, spell.Id)
                && effect.Duration > 0
                && (effect.ExpiresAtUtc is null || effect.ExpiresAtUtc > nowUtc))
            .Select(effect => effect.EffectId).ToArray();
        foreach (var duplicateId in duplicateIds) _effects.Remove(duplicateId);
    }

    private bool HasClassicNativeActiveSlot(RuntimeSpellDefinition spell, RuntimeSpellTarget target, DateTimeOffset nowUtc)
    {
        if (!_enforceClassicNativeActiveSlots || spell.Duration <= 0) return true;
        return ClassicNativeCanAdmitDurationSpell(
            _effects.Values, target.Kind, target.Id, spell.Id, nowUtc);
    }

    public RuntimeSpellEngine(RuntimeSpellCatalog catalog, IRuntimeSpellEffectResolver? resolver = null,
        IEnumerable<IRuntimeSpellPolicy>? policies = null, RuntimeSpellState? initial = null,
        bool enforceClassicNativeActiveSlots = false)
    {
        _catalog = catalog;
        _enforceClassicNativeActiveSlots = enforceClassicNativeActiveSlots;
        _resolver = resolver ?? new GeneratedSpellEffectResolver();
        _policies = (policies ?? Array.Empty<IRuntimeSpellPolicy>()).ToArray();
        if (initial is not null) Restore(initial);
        EnsureValid();
    }

    public void SetResources(string characterId, RuntimeSpellResources resources) => Atomic(() =>
    {
        if (resources.Mana < 0 || resources.Energy < 0) throw new InvalidOperationException("Spell resources cannot be negative.");
        _resources[characterId] = resources;
        return 0;
    });

    public RuntimeKnownSpell Learn(string characterId, string spellId, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        _catalog.Require(spellId);
        var key = Key(characterId, spellId);
        if (_known.ContainsKey(key)) throw new InvalidOperationException("Character already knows this spell.");
        var learned = new RuntimeKnownSpell(characterId, spellId, DateTimeOffset.UtcNow, metadata);
        _known.Add(key, learned);
        return learned;
    });

    public RuntimeKnownSpell Forget(string characterId, string spellId) => Atomic(() =>
    {
        var key = Key(characterId, spellId);
        if (!_known.Remove(key, out var known)) throw new InvalidOperationException("Character does not know this spell.");
        return known;
    });

    public bool Knows(string characterId, string spellId) { lock (_gate) return _known.ContainsKey(Key(characterId, spellId)); }
    public IReadOnlyList<RuntimeKnownSpell> Spellbook(string characterId) { lock (_gate) return _known.Values.Where(x => Same(x.CharacterId, characterId)).OrderBy(x => x.SpellId).ToArray(); }
    public IReadOnlyList<RuntimeActiveSpellEffect> EffectsFor(string targetId) { lock (_gate) return _effects.Values.Where(x => Same(x.Target.Id, targetId)).OrderBy(x => x.AppliedAtUtc).ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).ToArray(); }

    public RuntimeSpellCastResult Cast(RuntimeSpellCastRequest request) => Atomic(() =>
    {
        var spell = _catalog.Require(request.SpellId);
        var resources = _resources.GetValueOrDefault(request.CasterId, new RuntimeSpellResources(0, 0));
        if (!Knows(request.CasterId, spell.Id)) return Failure("SPELL_NOT_KNOWN", "Caster does not know this spell.", resources);
        if (resources.Mana < spell.ManaCost) return Failure("INSUFFICIENT_MANA", "Caster does not have enough mana.", resources);
        if (resources.Energy < spell.EnergyCost) return Failure("INSUFFICIENT_ENERGY", "Caster does not have enough energy.", resources);
        if (!HasClassicNativeActiveSlot(spell, request.Target, request.RequestedAtUtc))
            return Failure("SPELL_ACTIVE_SLOTS_FULL", "Target has no free active spell slot.", resources);
        var state = SnapshotCore();
        foreach (var policy in _policies)
        {
            var issue = policy.ValidateCast(spell, request, state);
            if (issue is not null) return Failure(issue.Code, issue.Message, resources);
        }
        var remaining = new RuntimeSpellResources(resources.Mana - spell.ManaCost, resources.Energy - spell.EnergyCost);
        _resources[request.CasterId] = remaining;
        var effect = _resolver.Resolve(spell, request);
        RefreshClassicNativeMonsterDuplicate(spell, request.Target, request.RequestedAtUtc);
        if (!_effects.TryAdd(effect.EffectId, effect)) throw new InvalidOperationException("Duplicate spell effect id.");
        return new RuntimeSpellCastResult(true, null, null, remaining, effect);
    });

    public RuntimeActiveSpellEffect AddAdditionalTarget(RuntimeSpellCastRequest request) => Atomic(() =>
    {
        var spell = _catalog.Require(request.SpellId);
        if (!Knows(request.CasterId, spell.Id)) throw new InvalidOperationException("SPELL_NOT_KNOWN");
        if (!HasClassicNativeActiveSlot(spell, request.Target, request.RequestedAtUtc))
            throw new InvalidOperationException("SPELL_ACTIVE_SLOTS_FULL");
        var effect = _resolver.Resolve(spell, request);
        RefreshClassicNativeMonsterDuplicate(spell, request.Target, request.RequestedAtUtc);
        if (!_effects.TryAdd(effect.EffectId, effect)) throw new InvalidOperationException("Duplicate spell effect id.");
        return effect;
    });

    public int Expire(DateTimeOffset nowUtc) => Atomic(() =>
    {
        var expired = _effects.Values.Where(x => x.ExpiresAtUtc is not null && x.ExpiresAtUtc <= nowUtc).OrderBy(x => x.ExpiresAtUtc).ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).Select(x => x.EffectId).ToArray();
        foreach (var id in expired) _effects.Remove(id);
        return expired.Length;
    });

    public RuntimeActiveSpellEffect RemoveEffect(string effectId) => Atomic(() =>
    {
        if (!_effects.Remove(effectId, out var effect)) throw new InvalidOperationException("Unknown active spell effect.");
        return effect;
    });

    public RuntimeSpellState Snapshot() { lock (_gate) return SnapshotCore(); }
    public void RestoreSnapshot(RuntimeSpellState state) { lock (_gate) { _known = new(StringComparer.OrdinalIgnoreCase); _effects = new(StringComparer.OrdinalIgnoreCase); _resources = new(StringComparer.OrdinalIgnoreCase); Restore(state); EnsureValid(); } }
    public RuntimeSpellValidationReport Validate() { lock (_gate) return ValidateCore(); }

    private void Restore(RuntimeSpellState state)
    {
        foreach (var known in state.KnownSpells)
        {
            _catalog.Require(known.SpellId);
            if (!_known.TryAdd(Key(known.CharacterId, known.SpellId), known)) throw new InvalidOperationException("Duplicate known spell.");
        }
        foreach (var effect in state.ActiveEffects)
        {
            _catalog.Require(effect.SpellId);
            if (!_effects.TryAdd(effect.EffectId, effect)) throw new InvalidOperationException("Duplicate active spell effect id.");
        }
        foreach (var pair in state.Resources) _resources.Add(pair.Key, pair.Value);
    }

    private RuntimeSpellState SnapshotCore() => new(
        _known.Values.OrderBy(x => x.CharacterId, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.SpellId, StringComparer.OrdinalIgnoreCase).ToArray(),
        _effects.Values.OrderBy(x => x.AppliedAtUtc).ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).ToArray(),
        _resources.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
    private RuntimeSpellCastResult Failure(string code, string message, RuntimeSpellResources resources) => new(false, code, message, resources, null);
    private static string Key(string characterId, string spellId) => $"{characterId}\u001f{spellId}";
    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private RuntimeSpellValidationReport ValidateCore()
    {
        var issues = new List<RuntimeSpellValidationIssue>();
        foreach (var known in _known.Values)
        {
            try { _catalog.Require(known.SpellId); }
            catch (Exception ex) { issues.Add(new("UNKNOWN_KNOWN_SPELL", ex.Message, known.SpellId)); }
            if (string.IsNullOrWhiteSpace(known.CharacterId)) issues.Add(new("INVALID_SPELL_OWNER", "Known spell has no character owner.", known.SpellId));
        }
        foreach (var effect in _effects.Values)
        {
            try { _catalog.Require(effect.SpellId); }
            catch (Exception ex) { issues.Add(new("UNKNOWN_ACTIVE_SPELL", ex.Message, effect.EffectId)); }
            if (string.IsNullOrWhiteSpace(effect.CasterId) || string.IsNullOrWhiteSpace(effect.Target.Id) || string.IsNullOrWhiteSpace(effect.Target.Kind))
                issues.Add(new("INVALID_ACTIVE_EFFECT", "Active effect has an invalid caster or target.", effect.EffectId));
        }
        foreach (var pair in _resources)
            if (pair.Value.Mana < 0 || pair.Value.Energy < 0) issues.Add(new("INVALID_SPELL_RESOURCES", "Spell resources cannot be negative.", pair.Key));
        return new RuntimeSpellValidationReport(issues.Count == 0, issues);
    }

    private void EnsureValid()
    {
        var report = ValidateCore();
        if (!report.Passed) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(x => x.Message)));
    }

    private T Atomic<T>(Func<T> action)
    {
        lock (_gate)
        {
            var known = new Dictionary<string, RuntimeKnownSpell>(_known, StringComparer.OrdinalIgnoreCase);
            var effects = new Dictionary<string, RuntimeActiveSpellEffect>(_effects, StringComparer.OrdinalIgnoreCase);
            var resources = new Dictionary<string, RuntimeSpellResources>(_resources, StringComparer.OrdinalIgnoreCase);
            try { var result = action(); EnsureValid(); return result; }
            catch { _known = known; _effects = effects; _resources = resources; throw; }
        }
    }
}

public sealed class RuntimeSpellStateRepository
{
    private readonly string _path;
    public RuntimeSpellStateRepository(string saveRoot, string worldId) => _path = Path.Combine(saveRoot, worldId, "runtime-spells.json");
    public void Save(RuntimeSpellState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Normalize(state), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }
    public RuntimeSpellState Load() => File.Exists(_path)
        ? Normalize(JsonSerializer.Deserialize<RuntimeSpellState>(File.ReadAllText(_path)) ?? Empty())
        : Empty();
    private static RuntimeSpellState Normalize(RuntimeSpellState state) => new(
        state.KnownSpells.OrderBy(x => x.CharacterId, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.SpellId, StringComparer.OrdinalIgnoreCase).ToArray(),
        state.ActiveEffects.OrderBy(x => x.AppliedAtUtc).ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).ToArray(),
        state.Resources.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
    private static RuntimeSpellState Empty() => new(Array.Empty<RuntimeKnownSpell>(), Array.Empty<RuntimeActiveSpellEffect>(), new Dictionary<string, RuntimeSpellResources>());
}
