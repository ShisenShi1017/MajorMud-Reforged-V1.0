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
    public static readonly TimeSpan RestCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.RestRegenerationFastTicks);
    public static readonly TimeSpan MeditateCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.MeditateRegenerationFastTicks);
    // HELP-only presentation guidance. Native scheduling uses fast counters below.
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
        // Native slow HP locally caps only at maximum; native mana clamps both bounds.
        var nextHitPoints = Math.Min(character.MaximumHitPoints, checked(character.HitPoints + hitPointRate));
        var nextMana = Math.Clamp(checked(character.Mana + manaRate), 0, character.MaximumMana);

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
        var next = Math.Clamp(checked(character.Mana + rate), 0, character.MaximumMana);
        return new(character with { Mana = next, UpdatedAtUtc = nowUtc }, 0, rate, 0, next - character.Mana,
            "decoded_data_and_multi_source_formula_evidence", FormulaId + ".meditate");
    }

    private static int CalculateManaRate(RuntimeCharacterRecord character, RuntimeCharacterDefinition characterClass, int percent, bool meditating)
    {
        var magic = characterClass.Magic;
        if (magic is null || magic.Type == 0) return 0;

        // Native WCCMMUD type 5 forces a one-point base. Passive regeneration then
        // applies MPRegen; MEDITATE bypasses MPRegen for every magic type, including 5.
        if (magic.Type == 5)
            return meditating ? 1 : ApplyPercent(1, percent);

        var seed = magic.Type switch
        {
            1 => Read(character.BaseStats, "intellect", "int"),
            2 => Read(character.BaseStats, "wisdom", "willpower", "wil"),
            3 => checked((Read(character.BaseStats, "intellect", "int") + Read(character.BaseStats, "wisdom", "willpower", "wil")) / 2),
            4 => Read(character.BaseStats, "charm", "cha"),
            _ => throw new InvalidDataException($"Unsupported decoded magic type {magic.Type} for regeneration.")
        };
        var baseRate = checked(((character.Level + 20) * seed * (magic.Level + 2)) / 1650);
        return meditating ? baseRate : ApplyPercent(baseRate, percent);
    }

    internal static int CalculateClassicHitPointBase(int level, int health) =>
        Math.Max(1, checked(((level + 20) * health) / 750));

    internal static int ApplyPercent(int basis, int percent) =>
        // WCCMMUD computes ((100 + modifier) * basis) / 100 as one signed integer
        // expression. This differs from basis + (modifier*basis)/100 for negatives.
        checked(checked((100 + percent) * basis) / 100);

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

public static class RuntimeClassicRegenerationCommandAuthority
{
    public const string RestTooSickMessage = "You are too sick to rest!";
    public const string MeditateTooSickMessage = "You are too sick to meditate!";
    public const string MeditationNotHelpfulMessage = "Meditation will not help at this time.";
    public const string RestSuccessMessage = "You are now resting.";
    public const string MeditateSuccessMessage = "You are now meditating.";

    public static bool IsTooSick(RuntimeCharacterRecord character) =>
        RuntimeClassicStatusProjection.Flag(character, "poisoned");

    public static bool MeditationWillNotHelp(RuntimeCharacterRecord character) =>
        character.Mana >= character.MaximumMana;

    public static string ObserverMessage(string actorName, string mode) =>
        mode.Equals("rest", StringComparison.OrdinalIgnoreCase)
            ? $"{actorName} stops to rest."
            : $"{actorName} kneels to meditate.";
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly RuntimeClassicNativeSchedulerClock _classicNativeSchedulerClock = new();
    private readonly ConcurrentDictionary<string, RuntimeFocusedRegenerationState> _focusedRegeneration = new(StringComparer.OrdinalIgnoreCase);
    private Func<RuntimeSession, string, string>? _classicPendingCommandExecutor;

    public void BindClassicPendingCommandExecutor(Func<RuntimeSession, string, string> executor) =>
        _classicPendingCommandExecutor = executor ?? throw new ArgumentNullException(nameof(executor));

    public string ExecuteRegenerationStateCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        if (_liveCombats.ContainsKey(session.Id) || _livePvpEncounterBySession.ContainsKey(session.Id)) return "You cannot rest or meditate while in combat.";

        var mode = command.Name.Equals("rest", StringComparison.OrdinalIgnoreCase) ? "rest" : "meditate";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");

        // Native WCCMMUD checks the Meditate capability before the shared sickness gate.
        if (mode == "meditate")
        {
            var abilities = RuntimeCharacterQuestAbilityCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var gates = new RuntimeCharacterWorldRewardCatalog();
            if (!abilities.CanUseCapability(character, RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, gates))
                return "You have not learned the Meditate ability for your class and level.";
        }

        // WCCMMUD user +0xBE is the active poison/illness damage accumulator. The same
        // field is reduced by native Cure Poison (ability 20), consumed by the slow
        // `You feel ill.` damage owner, and blocks both REST and MEDITATE while positive.
        if (RuntimeClassicRegenerationCommandAuthority.IsTooSick(character))
            return mode == "rest"
                ? RuntimeClassicRegenerationCommandAuthority.RestTooSickMessage
                : RuntimeClassicRegenerationCommandAuthority.MeditateTooSickMessage;

        if (mode == "meditate" && RuntimeClassicRegenerationCommandAuthority.MeditationWillNotHelp(character))
        {
            // Native clamps an over-max current mana value back to max before rejecting.
            if (character.Mana > character.MaximumMana)
                _characters.SaveCharacter(character with { Mana = character.MaximumMana, UpdatedAtUtc = DateTimeOffset.UtcNow });
            return RuntimeClassicRegenerationCommandAuthority.MeditationNotHelpfulMessage;
        }

        var alreadyFocusedInMode = _focusedRegeneration.TryGetValue(actor.Id, out var current)
            && current.Mode.Equals(mode, StringComparison.OrdinalIgnoreCase);
        _focusedRegeneration[actor.Id] = new(mode, DateTimeOffset.UtcNow, 0);

        if (!alreadyFocusedInMode)
        {
            var observerMessage = RuntimeClassicRegenerationCommandAuthority.ObserverMessage(session.Actor.DisplayName, mode);
            _sessions.BroadcastRoom(session, observerMessage, includeSource: false, priority: RuntimeClassicRoomMessagePriority.High);
        }

        return mode == "rest"
            ? RuntimeClassicRegenerationCommandAuthority.RestSuccessMessage
            : RuntimeClassicRegenerationCommandAuthority.MeditateSuccessMessage;
    }

    /// <summary>
    /// Native REST/MEDITATE help names only three interruption causes: movement,
    /// attacking, or being attacked. Combat entry paths cancel focused regeneration
    /// themselves; ordinary non-movement commands do not reset the native fast counter.
    /// </summary>
    public void NotifyCharacterAction(RuntimeSession session, string commandName, bool isMovement)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return;
        if (!isMovement) return;
        _focusedRegeneration.TryRemove(actor.Id, out _);
    }

    private void CancelFocusedRegeneration(string characterId) => _focusedRegeneration.TryRemove(characterId, out _);

    private void TickActiveCharacterRegeneration(DateTimeOffset nowUtc, RuntimeClassicSchedulerStep schedulerStep)
    {
        if (!schedulerStep.FastDue) return;
        foreach (var session in _sessions.ActiveSessions)
        {
            if (session.Actor is not RuntimeCharacterActor actor) continue;
            // _fast_update_character decrements the native +0x6BB command-delay byte once on
            // every one-second fast callback. When the delay is zero, the same owner drains at
            // most one pending command through 0x450E7F, including on the tick that reaches zero.
            session.TickClassicCommandDelay();
            if (session.ClassicCommandDelay == 0
                && _classicPendingCommandExecutor is not null
                && session.TryDequeueClassicPendingCommand(out var pendingCommand))
            {
                var pendingOutput = _classicPendingCommandExecutor(session, pendingCommand);
                if (!string.IsNullOrWhiteSpace(pendingOutput)) session.EnqueueMessage(pendingOutput);
            }
            var character = _characters.LoadCharacter(actor.Id);
            if (character is null) continue;
            var catalog = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var definition = catalog.RequireClass(character.ClassId);
            var updated = character;

            // Native _background_fast executes due internal callbacks before
            // _fast_update_character. A 30-tick slow callback therefore regenerates
            // passive HP/MP before same-cycle REST/MEDITATE fast-counter work.
            if (schedulerStep.SlowDue)
            {
                updated = RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(updated, definition, nowUtc).Character;
                // Native slow-character update decrements the two persistent EAT/DRINK
                // fallback words at user +0xCE/+0xD0 once per 30-fast-tick generation.
                updated = TickClassicFoodDrinkFallbackCounters(updated);
            }

            if (_focusedRegeneration.TryGetValue(character.CharacterId, out var focused))
            {
                var counter = checked(focused.FastTickCounter + 1);
                var threshold = focused.Mode == "rest"
                    ? RuntimeClassicTimingAuthority.RestCounterThreshold
                    : RuntimeClassicTimingAuthority.MeditateCounterThreshold;
                if (counter > threshold)
                {
                    updated = focused.Mode == "rest"
                        ? RuntimeCharacterRegenerationAuthority.ApplyRestTick(updated, definition, nowUtc).Character
                        : RuntimeCharacterRegenerationAuthority.ApplyMeditateTick(updated, definition, nowUtc).Character;
                    counter = 0;
                }
                _focusedRegeneration[character.CharacterId] = focused with { FastTickCounter = counter };
            }

            if (updated != character)
                _characters.SaveCharacter(updated);
        }
    }
}

public sealed record RuntimeFocusedRegenerationState(string Mode, DateTimeOffset StartedAtUtc, int FastTickCounter);
