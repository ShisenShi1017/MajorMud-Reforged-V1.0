using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed record RuntimeCharacterRegenerationTick(
    RuntimeCharacterRecord Character,
    int HitPointRate,
    int ManaRate,
    int HitPointsRestored,
    int ManaRestored,
    string EvidenceClassification,
    string FormulaId);

public static class RuntimeCharacterRegenerationAuthority
{
    public static readonly TimeSpan PassiveCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.PassiveRegenerationSeconds);
    public static readonly TimeSpan RestCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.RestRegenerationSeconds);
    public static readonly TimeSpan MeditateCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.MeditateRegenerationSeconds);
    public static readonly TimeSpan FocusActivationDelay = RuntimeClassicTimingAuthority.FocusedRegenerationActivationDelay;
    public const string FormulaId = "character.passive_regeneration.decoded_30_seconds";

    public static RuntimeCharacterRegenerationTick ApplyPassiveTick(
        RuntimeCharacterRecord character,
        RuntimeCharacterDefinition characterClass,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(characterClass);
        var health = Read(character.BaseStats, "health", "hea");
        var hpPercent = Derived(character, "hit_point_regeneration_percent");
        var manaPercent = ResolveManaRegenerationPercent(character);

        // Stock MajorMUD branch of MMUD Explorer CalcRestingRate(..., False):
        // max(1, Fix(((Level + 20) * Health) / 750)), then HPRegen percent.
        var baseHitPointRate = CalculateClassicHitPointBase(character.Level, health);
        var hitPointRate = ApplyPercent(baseHitPointRate, hpPercent);
        var manaRate = CalculateManaRate(character, characterClass, manaPercent, meditating: false);
        var nextHitPoints = Math.Min(character.MaximumHitPoints, checked(character.HitPoints + hitPointRate));
        var nextMana = Math.Min(character.MaximumMana, checked(character.Mana + manaRate));

        return new RuntimeCharacterRegenerationTick(
            character with { HitPoints = nextHitPoints, Mana = nextMana, UpdatedAtUtc = nowUtc },
            hitPointRate,
            manaRate,
            nextHitPoints - character.HitPoints,
            nextMana - character.Mana,
            "decoded_data_and_multi_source_formula_evidence",
            FormulaId);
    }

    public static RuntimeCharacterRegenerationTick ApplyRestTick(RuntimeCharacterRecord character, RuntimeCharacterDefinition characterClass, DateTimeOffset nowUtc)
    {
        var health = Read(character.BaseStats, "health", "hea");
        var percent = Derived(character, "hit_point_regeneration_percent");
        // Stock MajorMUD does not use the GreaterMUD /500 branch. It calculates the
        // normal /750 rate, clamps it to at least one, multiplies by three while
        // resting, then applies HPRegen percent.
        var rate = ApplyPercent(checked(CalculateClassicHitPointBase(character.Level, health) * 3), percent);
        var next = Math.Min(character.MaximumHitPoints, checked(character.HitPoints + rate));
        return new(character with { HitPoints = next, UpdatedAtUtc = nowUtc }, rate, 0, next - character.HitPoints, 0,
            "decoded_data_and_multi_source_formula_evidence", FormulaId + ".rest");
    }

    public static RuntimeCharacterRegenerationTick ApplyMeditateTick(RuntimeCharacterRecord character, RuntimeCharacterDefinition characterClass, DateTimeOffset nowUtc)
    {
        var rate = CalculateManaRate(character, characterClass, ResolveManaRegenerationPercent(character), meditating: true);
        var next = Math.Min(character.MaximumMana, checked(character.Mana + rate));
        return new(character with { Mana = next, UpdatedAtUtc = nowUtc }, 0, rate, 0, next - character.Mana,
            "decoded_data_and_multi_source_formula_evidence", FormulaId + ".meditate");
    }

    private static int CalculateManaRate(RuntimeCharacterRecord character, RuntimeCharacterDefinition characterClass, int percent, bool meditating)
    {
        var magic = characterClass.Magic;
        if (magic is null || magic.Type == 0) return 0;

        // MMUD Explorer CalcManaRegen keeps Kai/type 5 on its own one-point branch.
        // For types 1-4, meditation exits after the base formula and before MPRegen
        // percentage application; passive regeneration applies MPRegen afterward.
        if (magic.Type == 5)
            return ApplyPercent(1, percent);

        var seed = magic.Type switch
        {
            1 => Read(character.BaseStats, "intellect", "int"),
            2 => Read(character.BaseStats, "wisdom", "willpower", "wil"),
            3 => checked((Read(character.BaseStats, "intellect", "int") + Read(character.BaseStats, "wisdom", "willpower", "wil")) / 2),
            4 => Read(character.BaseStats, "charm", "cha"),
            _ => throw new InvalidDataException($"Unsupported decoded magic type {magic.Type} for regeneration.")
        };
        var baseRate = Math.Max(0, checked(((character.Level + 20) * seed * (magic.Level + 2)) / 1650));
        return meditating ? baseRate : ApplyPercent(baseRate, percent);
    }

    internal static int CalculateClassicHitPointBase(int level, int health) =>
        Math.Max(1, checked(((level + 20) * health) / 750));

    private static int ApplyPercent(int basis, int percent) =>
        Math.Max(0, checked(basis + checked((percent * basis) / 100)));

    private static int Derived(RuntimeCharacterRecord character, string key) =>
        character.SemanticState?.DerivedStats.TryGetValue(key, out var value) == true ? value : 0;

    internal static int ResolveManaRegenerationPercent(RuntimeCharacterRecord character) =>
        checked(Derived(character, "mana_regeneration_percent") +
                RuntimeClassicStatusProjection.Numeric(character, "mana_regeneration"));

    private static int Read(IReadOnlyDictionary<string, int> values, params string[] keys)
    {
        foreach (var key in keys) if (values.TryGetValue(key, out var value)) return value;
        return 0;
    }
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _characterRegenerationDue = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuntimeFocusedRegenerationState> _focusedRegeneration = new(StringComparer.OrdinalIgnoreCase);

    public string ExecuteRegenerationStateCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (_liveCombats.ContainsKey(session.Id) || _livePvpEncounterBySession.ContainsKey(session.Id)) return "You cannot rest or meditate while in combat.";
        var mode = command.Name.Equals("rest", StringComparison.OrdinalIgnoreCase) ? "rest" : "meditate";
        if (mode == "meditate")
        {
            var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
            var abilities = RuntimeCharacterQuestAbilityCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var gates = new RuntimeCharacterWorldRewardCatalog();
            if (!abilities.CanUseCapability(character, RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, gates))
                return "You have not learned the Meditate ability for your class and level.";
        }
        var now = DateTimeOffset.UtcNow;
        _focusedRegeneration[actor.Id] = new(mode, now, now + RuntimeCharacterRegenerationAuthority.FocusActivationDelay);
        if (mode == "rest")
            return "You are now resting.";
        return mode == "rest"
            ? "You begin resting. Remain uninterrupted for 20 seconds to increase HP regeneration."
            : "You begin meditating. Remain uninterrupted for 20 seconds to increase mana regeneration.";
    }

    /// <summary>
    /// Native REST/MEDITATE help names only three interruption causes: movement,
    /// attacking, or being attacked. Actual combat entry paths cancel focused
    /// regeneration themselves, so ordinary non-movement commands must not silently
    /// cancel a pending/active rest or meditation state.
    /// </summary>
    public void NotifyCharacterAction(RuntimeSession session, string commandName, bool isMovement)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return;
        if (!isMovement) return;
        _focusedRegeneration.TryRemove(actor.Id, out _);
    }

    private void CancelFocusedRegeneration(string characterId) => _focusedRegeneration.TryRemove(characterId, out _);

    private void TickActiveCharacterRegeneration(DateTimeOffset nowUtc)
    {
        foreach (var session in _sessions.ActiveSessions)
        {
            if (session.Actor is not RuntimeCharacterActor actor) continue;
            var character = _characters.LoadCharacter(actor.Id);
            if (character is null) continue;
            var catalog = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var due = _characterRegenerationDue.GetOrAdd(character.CharacterId, nowUtc + RuntimeCharacterRegenerationAuthority.PassiveCadence);
            var updated = character;
            if (nowUtc >= due)
            {
                var elapsedTicks = Math.Max(1L, (long)((nowUtc - due).Ticks / RuntimeCharacterRegenerationAuthority.PassiveCadence.Ticks) + 1L);
                for (var tick = 0L; tick < elapsedTicks; tick++)
                    updated = RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(updated, catalog.RequireClass(updated.ClassId), nowUtc).Character;
                if (updated.HitPoints != character.HitPoints || updated.Mana != character.Mana)
                    _characters.SaveCharacter(updated);
                _characterRegenerationDue[character.CharacterId] = due + TimeSpan.FromTicks(checked(RuntimeCharacterRegenerationAuthority.PassiveCadence.Ticks * elapsedTicks));
            }

            if (!_focusedRegeneration.TryGetValue(character.CharacterId, out var focused) || nowUtc < focused.NextTickUtc) continue;
            var focusCadence = focused.Mode == "rest" ? RuntimeCharacterRegenerationAuthority.RestCadence : RuntimeCharacterRegenerationAuthority.MeditateCadence;
            var focusTicks = Math.Max(1L, (long)((nowUtc - focused.NextTickUtc).Ticks / focusCadence.Ticks) + 1L);
            var focusedCharacter = updated;
            for (var tick = 0L; tick < focusTicks; tick++)
                focusedCharacter = focused.Mode == "rest"
                    ? RuntimeCharacterRegenerationAuthority.ApplyRestTick(focusedCharacter, catalog.RequireClass(focusedCharacter.ClassId), nowUtc).Character
                    : RuntimeCharacterRegenerationAuthority.ApplyMeditateTick(focusedCharacter, catalog.RequireClass(focusedCharacter.ClassId), nowUtc).Character;
            if (focusedCharacter.HitPoints != updated.HitPoints || focusedCharacter.Mana != updated.Mana)
                _characters.SaveCharacter(focusedCharacter);
            _focusedRegeneration[character.CharacterId] = focused with
            {
                NextTickUtc = focused.NextTickUtc + TimeSpan.FromTicks(checked(focusCadence.Ticks * focusTicks))
            };
        }
    }
}

public sealed record RuntimeFocusedRegenerationState(string Mode, DateTimeOffset StartedAtUtc, DateTimeOffset NextTickUtc);
