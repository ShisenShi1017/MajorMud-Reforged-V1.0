namespace RealmGate.App.Brains;

public sealed record BorinSelfPlayActionRecord(
    DateTimeOffset Timestamp,
    string Command,
    string Context,
    string BeforeRoom,
    string AfterRoom,
    long BeforeExperience,
    long AfterExperience,
    int BeforeLevel,
    int AfterLevel,
    IReadOnlyList<string> Classifications,
    bool FreshServerEvidence,
    bool MeaningfulProgress,
    bool ExplicitFailure,
    string Summary);

public sealed record BorinSelfPlayRecoveryGuidance(
    string Phase,
    int EscalationLevel,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<string> Evidence,
    string Detail)
{
    public string PromptText
    {
        get
        {
            var priorities = Priorities.Count == 0
                ? "- No recovery action is currently required."
                : string.Join("\n", Priorities.Select((value, index) => $"{index + 1}. {value}"));
            var evidence = Evidence.Count == 0
                ? "- No short-term stuck evidence is currently active."
                : string.Join("\n", Evidence.Select(value => $"- {value}"));
            return $"Self-play recovery phase: {Phase} (escalation {EscalationLevel})\n{Detail}\nRecovery priorities:\n{priorities}\nShort-term evidence:\n{evidence}";
        }
    }

    public static BorinSelfPlayRecoveryGuidance Disabled() => new(
        "disabled",
        0,
        Array.Empty<string>(),
        Array.Empty<string>(),
        "Autonomous self-play recovery is disabled.");
}

/// <summary>
/// Short-term deterministic anti-stuck memory for Borin's autonomous player loop.
/// This layer never sends a command and never invents game state. It records the outcomes
/// of Borin's own ordinary-player actions, identifies repeated no-progress patterns, and
/// supplies evidence-gated recovery guidance to the next model decision.
/// </summary>
public sealed class BorinSelfPlayRecoveryTracker
{
    private const int MaximumHistory = 24;
    private readonly Queue<BorinSelfPlayActionRecord> _history = new();
    private readonly Queue<string> _rejectedDecisions = new();

    public IReadOnlyList<BorinSelfPlayActionRecord> History => _history.ToArray();

    public void Reset()
    {
        _history.Clear();
        _rejectedDecisions.Clear();
    }

    public void RecordRejectedDecision(string reason, BorinGameStateSnapshot state)
    {
        var normalized = string.IsNullOrWhiteSpace(reason) ? "Decision was unusable." : reason.Trim();
        _rejectedDecisions.Enqueue($"{BuildContext(state)} • {normalized}");
        while (_rejectedDecisions.Count > 6)
            _rejectedDecisions.Dequeue();
    }

    public BorinSelfPlayActionRecord RecordOutcome(
        string command,
        BorinGameStateSnapshot before,
        BorinGameStateSnapshot after,
        IReadOnlyList<string> classifications,
        bool freshServerEvidence)
    {
        var normalizedCommand = NormalizeCommand(command);
        var normalizedClassifications = classifications
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var explicitFailure = !freshServerEvidence || normalizedClassifications.Any(IsFailureClassification);
        var meaningfulProgress = HasMeaningfulStateProgress(before, after) ||
                                 normalizedClassifications.Any(IsProgressClassification);
        var summary = BuildOutcomeSummary(normalizedCommand, before, after, normalizedClassifications, freshServerEvidence, meaningfulProgress, explicitFailure);
        var record = new BorinSelfPlayActionRecord(
            DateTimeOffset.Now,
            normalizedCommand,
            BuildContext(before),
            before.CurrentRoom,
            after.CurrentRoom,
            before.ExperienceCurrent,
            after.ExperienceCurrent,
            before.CharacterLevel,
            after.CharacterLevel,
            normalizedClassifications,
            freshServerEvidence,
            meaningfulProgress,
            explicitFailure,
            summary);

        _history.Enqueue(record);
        while (_history.Count > MaximumHistory)
            _history.Dequeue();

        if (meaningfulProgress)
            _rejectedDecisions.Clear();

        return record;
    }

    public bool ShouldSuppressAction(string command, BorinGameStateSnapshot state, out string reason)
    {
        reason = string.Empty;
        var normalized = NormalizeCommand(command);
        if (normalized.Length == 0)
            return false;

        var context = BuildContext(state);
        var activeStreak = _history
            .Reverse()
            .TakeWhile(item => item.Context.Equals(context, StringComparison.OrdinalIgnoreCase))
            .TakeWhile(item => !item.MeaningfulProgress)
            .ToArray();
        var same = activeStreak
            .Where(item => item.Command.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToArray();

        // Borin is allowed one retry. A second verified failure/no-progress repeat in the
        // same context is enough to force inspection or an alternate strategy. A genuine
        // progress event ends this streak, so old failures do not become a permanent ban.
        if (same.Length >= 2 && same.Take(2).All(item => item.ExplicitFailure || !item.MeaningfulProgress))
        {
            reason = $"The exact action '{normalized}' already failed or produced no progress twice in the same verified context. Inspect, choose an alternative, or re-plan before trying it again.";
            return true;
        }

        var recent = _history.TakeLast(6).ToArray();
        if (recent.Length >= 4 &&
            activeStreak.Length >= 4 &&
            activeStreak.Take(4).Count(item => item.Command.Equals(normalized, StringComparison.OrdinalIgnoreCase)) >= 2)
        {
            reason = $"The action '{normalized}' is part of a repeated no-progress loop in the current context. Gather different evidence or choose a different action.";
            return true;
        }

        if (IsMovementCommand(normalized) &&
            DetectMovementOscillation(recent, out _) &&
            recent.Length >= 2 &&
            recent[^2].Command.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"The movement '{normalized}' would continue the detected two-room route oscillation. Inspect/re-plan rather than bouncing between the same rooms.";
            return true;
        }

        if (DetectEconomyLoop(recent, out _) && StartsWithAny(normalized, "BUY ", "SELL ", "EQUIP ", "WEAR ", "REMOVE "))
        {
            reason = $"The economy/equipment action '{normalized}' would continue a detected transaction/equipment loop. Refresh live state and re-plan before another irreversible economy action.";
            return true;
        }

        return false;
    }

    public string ChooseInspectionCommand(BorinGameStateSnapshot state)
    {
        var candidates = new[] { "LOOK", "ROOM INFORMATION", "INVENTORY", "EQUIPMENT", "STATS", "EXP" };
        var context = BuildContext(state);
        var recentCommands = _history
            .Reverse()
            .Where(item => item.Context.Equals(context, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .Select(item => item.Command)
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (!recentCommands.Take(3).Contains(candidate, StringComparer.OrdinalIgnoreCase) &&
                !ShouldSuppressAction(candidate, state, out _))
                return candidate;
        }

        return state.HasVerifiedPrompt ? "LOOK" : string.Empty;
    }

    public BorinSelfPlayRecoveryGuidance BuildGuidance(BorinGameStateSnapshot state)
    {
        var priorities = new List<string>();
        var evidence = new List<string>();
        var context = BuildContext(state);
        var contextual = _history
            .Reverse()
            .TakeWhile(item => item.Context.Equals(context, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Reverse()
            .ToArray();
        var recent = _history.TakeLast(10).ToArray();

        var noProgressStreak = contextual.Reverse().TakeWhile(item => !item.MeaningfulProgress).Reverse().ToArray();
        var exactRepeat = noProgressStreak
            .GroupBy(item => item.Command, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= 2 && group.TakeLast(2).All(item => item.ExplicitFailure || !item.MeaningfulProgress))
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        var noProgressCount = noProgressStreak.Length;
        var movementOscillation = DetectMovementOscillation(recent, out var movementEvidence);
        var economyLoop = DetectEconomyLoop(recent, out var economyEvidence);
        var interactionLoop = DetectInteractionLoop(contextual, out var interactionEvidence);
        var repeatedCombatFailure = recent.Count(item => item.Classifications.Any(value => value.Equals("CombatDefeat", StringComparison.OrdinalIgnoreCase))) >= 2;
        var responseTimeout = recent.LastOrDefault() is { FreshServerEvidence: false };
        var rejectedCount = _rejectedDecisions.Count;

        var escalation = 0;
        if (exactRepeat is not null || noProgressCount >= 3 || responseTimeout || rejectedCount >= 2)
            escalation = 1;
        if (noProgressCount >= 5 || movementOscillation || economyLoop || interactionLoop || repeatedCombatFailure)
            escalation = 2;
        if (noProgressCount >= 7 || (rejectedCount >= 4 && noProgressCount >= 4))
            escalation = 3;

        if (exactRepeat is not null)
        {
            evidence.Add($"REPEAT • {exactRepeat.Key} has repeated {exactRepeat.Count()} times in the same context without verified progress.");
            priorities.Add($"Do not immediately repeat {exactRepeat.Key}. Inspect the live state, consult verified memory/Librarium evidence, and choose a different ordinary-player action unless new evidence changes the context.");
        }

        if (responseTimeout)
        {
            evidence.Add("PACING • The last action did not produce the expected fresh server prompt/evidence.");
            priorities.Add("Re-establish live perception before issuing another state-changing action. Do not infer success from silence or resend a state-changing command blindly.");
        }

        if (movementOscillation)
        {
            evidence.Add($"ROUTE LOOP • {movementEvidence}");
            priorities.Add("Break the route oscillation. LOOK/ROOM INFORMATION as needed, consult verified route/Librarium evidence, then choose a different verified exit or abandon the current micro-route and re-plan the destination.");
        }

        if (economyLoop)
        {
            evidence.Add($"ECONOMY LOOP • {economyEvidence}");
            priorities.Add("Stop the buy/sell/equip inventory cycle, refresh LIST/INVENTORY/EQUIPMENT, and re-evaluate whether the transaction advances the progression goal before another irreversible economy action.");
        }

        if (interactionLoop)
        {
            evidence.Add($"INTERACTION LOOP • {interactionEvidence}");
            priorities.Add("Stop repeating the same NPC/quest interaction. Re-read live dialogue, inspect the NPC/item/room, consult quest evidence, and choose a different dialogue keyword/action or abandon this quest step for now.");
        }

        if (repeatedCombatFailure)
        {
            evidence.Add("COMBAT • Multiple recent combat defeats are present in short-term self-play history.");
            priorities.Add("Do not re-enter the same losing fight unchanged. Recover, reassess gear/spells/target/area, consult personal combat experience and Librarium evidence, and choose a safer progression plan.");
        }

        if (noProgressCount >= 3)
        {
            evidence.Add($"NO PROGRESS • {noProgressCount} consecutive actions in the current context produced no verified room/XP/level/item/equipment/combat-victory progress.");
            priorities.Add(noProgressCount >= 7
                ? "Abandon the current micro-goal and select a different progression objective. The priority is genuine game progress, not preserving a failing plan."
                : "Inspect and change tactics instead of continuing the same action pattern.");
        }

        foreach (var rejected in _rejectedDecisions.TakeLast(3))
            evidence.Add($"DECISION REJECTED • {rejected}");

        if (escalation == 0)
            priorities.Add("Continue the current autonomous plan, but evaluate each server result before choosing the next action.");
        else if (escalation == 1)
            priorities.Add("Recovery escalation 1: retry at most once when justified, then gather fresh reversible evidence before another attempt.");
        else if (escalation == 2)
            priorities.Add("Recovery escalation 2: change the immediate tactic or route using live state, verified personal experience, and Librarium guidance.");
        else
            priorities.Add("Recovery escalation 3: abandon the failing micro-goal and re-plan the larger leveling/quest/equipment objective rather than looping indefinitely.");

        var phase = escalation switch
        {
            0 => "normal self-play",
            1 => "inspect and verify",
            2 => "change tactic",
            _ => "abandon micro-goal and re-plan",
        };

        return new BorinSelfPlayRecoveryGuidance(
            phase,
            escalation,
            priorities.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray(),
            evidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray(),
            "This short-term recovery memory is contextual, not permanent truth. New verified progress clears the immediate stuck hypothesis. It may block an exact repeated failure, but it never invents exits, quest state, success, or server responses.");
    }

    private static bool DetectMovementOscillation(IReadOnlyList<BorinSelfPlayActionRecord> records, out string evidence)
    {
        evidence = string.Empty;
        var moves = records.Where(item => IsMovementCommand(item.Command) && item.FreshServerEvidence).TakeLast(6).ToArray();
        if (moves.Length < 4)
            return false;

        var rooms = moves.Select(item => string.IsNullOrWhiteSpace(item.AfterRoom) ? item.BeforeRoom : item.AfterRoom).ToArray();
        if (rooms.Length >= 4 &&
            rooms[^1].Equals(rooms[^3], StringComparison.OrdinalIgnoreCase) &&
            rooms[^2].Equals(rooms[^4], StringComparison.OrdinalIgnoreCase) &&
            !rooms[^1].Equals(rooms[^2], StringComparison.OrdinalIgnoreCase))
        {
            evidence = $"Recent movement alternates between '{rooms[^2]}' and '{rooms[^1]}' without a larger progression event.";
            return true;
        }

        return false;
    }

    private static bool DetectEconomyLoop(IReadOnlyList<BorinSelfPlayActionRecord> records, out string evidence)
    {
        evidence = string.Empty;
        var economy = records.Where(item => StartsWithAny(item.Command, "BUY ", "SELL ", "EQUIP ", "WEAR ", "REMOVE ")).TakeLast(6).ToArray();
        if (economy.Length < 4)
            return false;

        var lastFour = economy.TakeLast(4).ToArray();
        var verbs = lastFour.Select(item => item.Command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]).ToArray();
        var targets = lastFour.Select(item => item.Command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? string.Empty).ToArray();
        var sameTarget = targets.All(value => value.Equals(targets[0], StringComparison.OrdinalIgnoreCase));
        var alternatingBuySell = verbs[0] != verbs[1] && verbs[0] == verbs[2] && verbs[1] == verbs[3] &&
                                 new[] { verbs[0], verbs[1] }.All(value => value is "BUY" or "SELL");
        var alternatingWearRemove = verbs[0] != verbs[1] && verbs[0] == verbs[2] && verbs[1] == verbs[3] &&
                                    new[] { verbs[0], verbs[1] }.All(value => value is "WEAR" or "REMOVE" or "EQUIP");
        if (sameTarget && (alternatingBuySell || alternatingWearRemove))
        {
            evidence = $"Recent actions alternate on the same target: {string.Join(" -> ", lastFour.Select(item => item.Command))}.";
            return true;
        }

        var buySellNoAdvance = economy.Count(item => item.Command.StartsWith("BUY ", StringComparison.OrdinalIgnoreCase) || item.Command.StartsWith("SELL ", StringComparison.OrdinalIgnoreCase)) >= 3 &&
                               economy.TakeLast(4).All(item => !item.MeaningfulProgress);
        if (buySellNoAdvance)
        {
            evidence = $"Recent economy actions ({string.Join(" -> ", economy.Select(item => item.Command))}) have not produced verified progression.";
            return true;
        }
        return false;
    }

    private static bool DetectInteractionLoop(IReadOnlyList<BorinSelfPlayActionRecord> records, out string evidence)
    {
        evidence = string.Empty;
        var interactions = records.Where(item => StartsWithAny(item.Command, "TALK ", "ASK ", "GIVE ", "READ ", "EXAMINE ", "USE ")).TakeLast(5).ToArray();
        if (interactions.Length < 3)
            return false;

        var repeated = interactions
            .GroupBy(item => item.Command, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() >= 3 && group.All(item => !item.MeaningfulProgress));
        if (repeated is null)
            return false;

        evidence = $"'{repeated.Key}' repeated {repeated.Count()} times without verified progression.";
        return true;
    }

    private static bool HasMeaningfulStateProgress(BorinGameStateSnapshot before, BorinGameStateSnapshot after)
    {
        if (after.CharacterLevel > before.CharacterLevel) return true;
        if (after.ExperienceCurrent > before.ExperienceCurrent) return true;
        if (before.HasVerifiedRoom && after.HasVerifiedRoom &&
            !string.Equals(before.CurrentRoom, after.CurrentRoom, StringComparison.OrdinalIgnoreCase)) return true;
        if (!SequenceEqual(before.InventoryItems, after.InventoryItems)) return true;
        if (!SequenceEqual(before.EquippedItems, after.EquippedItems)) return true;
        return false;
    }

    private static bool SequenceEqual(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase);

    private static bool IsFailureClassification(string value) =>
        value.Equals("MovementFailed", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("PurchaseFailed", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("CombatDefeat", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsProgressClassification(string value) =>
        value.Equals("RoomEntered", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("ExperienceGained", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("LevelGained", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("ItemAcquired", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("PurchaseSucceeded", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("ItemEquipped", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("CombatVictory", StringComparison.OrdinalIgnoreCase);

    private static string BuildOutcomeSummary(
        string command,
        BorinGameStateSnapshot before,
        BorinGameStateSnapshot after,
        IReadOnlyList<string> classifications,
        bool fresh,
        bool progress,
        bool failure)
    {
        var result = failure ? "failure/no-response" : progress ? "verified progress" : "no verified progress";
        var room = string.Equals(before.CurrentRoom, after.CurrentRoom, StringComparison.OrdinalIgnoreCase)
            ? before.CurrentRoom
            : $"{before.CurrentRoom} -> {after.CurrentRoom}";
        var classes = classifications.Count == 0 ? "no classified event" : string.Join(", ", classifications);
        return $"{command}: {result}; fresh prompt={fresh}; room={room}; events={classes}.";
    }

    private static string BuildContext(BorinGameStateSnapshot state)
    {
        var room = !string.IsNullOrWhiteSpace(state.CurrentRoomKey) ? state.CurrentRoomKey : state.CurrentRoom;
        if (string.IsNullOrWhiteSpace(room)) room = "unknown-room";
        return $"{state.Server}|{state.World}|{room}|level:{state.CharacterLevel}|combat:{state.InCombat}";
    }

    private static string NormalizeCommand(string command) =>
        string.Join(' ', (command ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static bool IsMovementCommand(string command) => command is
        "N" or "S" or "E" or "W" or "U" or "D" or "NE" or "NW" or "SE" or "SW" or
        "NORTH" or "SOUTH" or "EAST" or "WEST" or "UP" or "DOWN" or
        "NORTHEAST" or "NORTHWEST" or "SOUTHEAST" or "SOUTHWEST";

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
