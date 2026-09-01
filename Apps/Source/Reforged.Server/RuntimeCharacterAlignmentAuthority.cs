using System.Text.Json;

namespace Reforged.Server;

// Consolidated from RuntimeCharacterAlignment.cs
public sealed record RuntimeCharacterAlignmentState(
    int EvilPoints,
    string Reputation,
    bool PerformedEvilActionToday,
    int MinutesPlayedToday,
    int DailyReductionApplied,
    string EvidenceClassification,
    string? SourceRecordId = null);

public sealed record RuntimeCharacterAlignmentCleanupConfiguration(
    int EvilActionReduction,
    int NoEvilActionBaseReduction,
    int CycleMinutes,
    int CycleAmount,
    int MaximumDailyReduction)
{
    public static RuntimeCharacterAlignmentCleanupConfiguration ClassicDefault { get; } = new(
        2,
        3,
        RuntimeClassicConfigurationAuthority.EvilPointCycleMinutes,
        RuntimeClassicConfigurationAuthority.EvilPointsForgivenPerCycle,
        RuntimeClassicConfigurationAuthority.MaximumEvilPointsForgivenPerDay);
}

public static class RuntimeCharacterAlignmentResolver
{
    public const int LegacyFiendAttackLockoutThreshold = 300;

    public static IReadOnlyDictionary<string, int> ClassicReputationMinimumEvilPoints { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Saint"] = -220,
            ["Good"] = -200,
            ["Neutral"] = -50,
            ["Seedy"] = 30,
            ["Outlaw"] = 40,
            ["Criminal"] = 80,
            ["Villain"] = 120,
            ["Fiend"] = 210
        };

    public static RuntimeCharacterAlignmentState CreateInitial()
        => new(0, "Neutral", false, 0, 0, "proven", "character_creation_rules.alignment_evil_point_rules");

    public static RuntimeCharacterAlignmentState CreateInitial(RuntimeCharacterAlignmentRuleContract contract)
        => new(contract.InitialEvilPoints, contract.InitialReputation, false, 0, 0, "proven", "character_creation_rules.alignment_evil_point_rules");

    public static int AttackPenaltyForTargetReputation(string targetReputation)
        => AttackPenaltyForTargetReputation(targetReputation, new RuntimeCharacterAlignmentRuleContract(0, "Neutral", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Saint"] = 2, ["Good"] = 2, ["Neutral"] = 0, ["Seedy"] = 0, ["Outlaw"] = 0, ["Criminal"] = 0, ["Villain"] = 0, ["Fiend"] = 0
        }, ClassicReputationMinimumEvilPoints, "Legacy Classic attack-reputation penalty table.", RuntimeClassicDeathLawPvpAuthority.LawfulAttackPenaltyMultiplier, false, 2, 3, 60, 1, 60, 1, 18, "Negative EP suppresses the documented base reduction.", LegacyFiendAttackLockoutThreshold));

    public static int AttackPenaltyForTargetReputation(string targetReputation, RuntimeCharacterAlignmentRuleContract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.AttackPenaltyRule))
            throw new InvalidDataException("Decoded attack_penalty_rule is missing; alignment attacks fail closed.");
        var match = contract.AttackReputationPenalties.FirstOrDefault(pair => pair.Key.Equals(targetReputation.Trim(), StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(match.Key)) throw new InvalidOperationException($"Unknown target reputation '{targetReputation}'.");
        return match.Value;
    }

    public static RuntimeCharacterAlignmentState ApplyAttack(RuntimeCharacterAlignmentState state, string targetReputation)
    {
        var penalty = AttackPenaltyForTargetReputation(targetReputation);
        var evilPoints = checked(state.EvilPoints + penalty);
        return state with
        {
            EvilPoints = evilPoints,
            Reputation = ResolveReputationForEvilPoints(evilPoints, state.Reputation),
            PerformedEvilActionToday = state.PerformedEvilActionToday || penalty > 0
        };
    }

    public static RuntimeCharacterAlignmentState ApplyAttack(RuntimeCharacterAlignmentState state, string targetReputation, RuntimeCharacterAlignmentRuleContract contract)
        => ApplyAttack(state, targetReputation, targetIsLawful: false, contract);

    public static RuntimeCharacterAlignmentState ApplyAttack(RuntimeCharacterAlignmentState state, string targetReputation, bool targetIsLawful, RuntimeCharacterAlignmentRuleContract contract)
    {
        var basePenalty = AttackPenaltyForTargetReputation(targetReputation, contract);
        var penalty = targetIsLawful ? checked(basePenalty * contract.LawfulAttackPenaltyMultiplier) : basePenalty;
        var evilPoints = checked(state.EvilPoints + penalty);
        return state with
        {
            EvilPoints = evilPoints,
            Reputation = ResolveReputationForEvilPoints(evilPoints, state.Reputation, contract),
            PerformedEvilActionToday = state.PerformedEvilActionToday || penalty > 0
        };
    }

    public static string ResolveReputationForEvilPoints(int evilPoints, string? currentReputation = null)
        => ResolveReputationForEvilPoints(evilPoints, currentReputation, null);

    public static string ResolveReputationForEvilPoints(int evilPoints, string? currentReputation, RuntimeCharacterAlignmentRuleContract? contract)
    {
        // Lawful is a creation-time legal pledge rather than an Evil-Point band. Until that
        // separate native creation/action gate is fully recovered, never erase an explicitly
        // persisted Lawful state merely because its ordinary EP value is zero.
        if (string.Equals(currentReputation, "Lawful", StringComparison.OrdinalIgnoreCase)) return "Lawful";

        var thresholds = contract?.ReputationMinimumEvilPoints is { Count: > 0 }
            ? contract.ReputationMinimumEvilPoints
            : ClassicReputationMinimumEvilPoints;
        int Require(string name)
        {
            if (!thresholds.TryGetValue(name, out var value))
                throw new InvalidDataException($"Decoded Classic reputation threshold '{name}' is missing; EP reputation resolution fails closed.");
            return value;
        }

        if (evilPoints >= Require("Fiend")) return "Fiend";
        if (evilPoints >= Require("Villain")) return "Villain";
        if (evilPoints >= Require("Criminal")) return "Criminal";
        if (evilPoints >= Require("Outlaw")) return "Outlaw";
        if (evilPoints >= Require("Seedy")) return "Seedy";
        if (evilPoints >= Require("Neutral")) return "Neutral";
        if (evilPoints >= Require("Good")) return "Good";

        // The recovered reference table names Saint for the negative band below Good and gives
        // -220 as its documented reference floor. No lower reputation name is documented, so
        // values below that reference floor remain Saint rather than inventing a ninth band.
        _ = Require("Saint");
        return "Saint";
    }

    public static RuntimeCharacterAlignmentState ApplyEvilPointDelta(
        RuntimeCharacterAlignmentState state,
        int delta,
        RuntimeCharacterAlignmentRuleContract? contract = null,
        bool markPositiveDeltaAsEvilAction = true)
    {
        var evilPoints = checked(state.EvilPoints + delta);
        return state with
        {
            EvilPoints = evilPoints,
            Reputation = ResolveReputationForEvilPoints(evilPoints, state.Reputation, contract),
            PerformedEvilActionToday = state.PerformedEvilActionToday || (markPositiveDeltaAsEvilAction && delta > 0)
        };
    }

    public static RuntimeClassicLawConsequence ResolveLawConsequence(RuntimeCharacterAlignmentState state)
        => RuntimeClassicDeathLawPvpAuthority.ResolveLawConsequence(state.Reputation, state.EvilPoints);

    public static bool CanInitiateAttack(RuntimeCharacterAlignmentState state)
        => state.EvilPoints <= LegacyFiendAttackLockoutThreshold;

    public static bool CanInitiateAttack(RuntimeCharacterAlignmentState state, RuntimeCharacterAlignmentRuleContract contract)
        => state.EvilPoints <= contract.FiendAttackLockoutThreshold;

    public static RuntimeCharacterAlignmentCleanupConfiguration CreateCleanupConfiguration(RuntimeCharacterAlignmentRuleContract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.NegativeEpException))
            throw new InvalidDataException("Decoded negative_ep_exception is missing; alignment cleanup fails closed.");
        var cycleMinutes = contract.CycleMinutes > 0 ? contract.CycleMinutes : contract.DefaultEpCycleMinutes;
        var cycleAmount = contract.CycleAmount > 0 ? contract.CycleAmount : contract.DefaultEpAmount;
        return new(contract.EvilActionReduction, contract.NoEvilActionBaseReduction, cycleMinutes, cycleAmount, contract.MaximumDailyReduction);
    }

    public static RuntimeCharacterAlignmentState ApplyDailyCleanup(
        RuntimeCharacterAlignmentState state,
        RuntimeCharacterAlignmentCleanupConfiguration configuration)
    {
        if (configuration.EvilActionReduction < 0 || configuration.NoEvilActionBaseReduction < 0 ||
            configuration.CycleMinutes <= 0 || configuration.CycleAmount < 0 || configuration.MaximumDailyReduction < 0)
            throw new ArgumentOutOfRangeException(nameof(configuration));

        var requestedReduction = state.PerformedEvilActionToday
            ? configuration.EvilActionReduction
            : checked(configuration.NoEvilActionBaseReduction + (state.MinutesPlayedToday / configuration.CycleMinutes) * configuration.CycleAmount);
        requestedReduction = Math.Min(requestedReduction, configuration.MaximumDailyReduction);

        // Classic documentation preserves the negative-EP exception: when already moving from Neutral toward
        // Good/Saint, the documented base two-point portion is not applied. Cycle cleanup may still apply.
        if (state.EvilPoints < 0)
        {
            requestedReduction = state.PerformedEvilActionToday
                ? 0
                : Math.Max(0, requestedReduction - configuration.EvilActionReduction);
        }

        var evilPoints = checked(state.EvilPoints - requestedReduction);
        return state with
        {
            EvilPoints = evilPoints,
            Reputation = ResolveReputationForEvilPoints(evilPoints, state.Reputation),
            PerformedEvilActionToday = false,
            MinutesPlayedToday = 0,
            DailyReductionApplied = requestedReduction
        };
    }
}

// Consolidated from RuntimeCharacterAlignmentNpcQuestCatalog.cs
public sealed record RuntimeCharacterAlignmentNpcCommand(
    string Keyword,
    int TargetTextblockId,
    IReadOnlyList<int> ReachableTextblockIds,
    IReadOnlyList<int> ActionTextblockIds,
    IReadOnlyList<string> MutationOpcodes,
    string EvidenceStatus,
    string DecodedSource);

public sealed record RuntimeCharacterAlignmentNpcQuest(
    string AlignmentPath,
    int QuestAbilityId,
    string NpcId,
    string NpcName,
    int RootTextblockId,
    IReadOnlyList<RuntimeCharacterAlignmentNpcCommand> Commands,
    string EvidenceStatus,
    string DecodedSource);

/// <summary>
/// Authoritative projection of the decoded alignment-NPC command maps. This closes the
/// activation-side relationship between the named NPC, its root textblock, the player
/// command keyword, and the downstream action textblocks. Bundle F remains responsible
/// for ordered dialogue execution; Bundle A owns the resulting character mutations.
/// </summary>
public sealed class RuntimeCharacterAlignmentNpcQuestCatalog
{
    private readonly Dictionary<string, RuntimeCharacterAlignmentNpcQuest> _byPath =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<RuntimeCharacterAlignmentNpcQuest> Quests => _byPath.Values
        .OrderBy(x => x.QuestAbilityId)
        .ToArray();

    public static RuntimeCharacterAlignmentNpcQuestCatalog Load(string projectRoot)
    {
        var catalog = new RuntimeCharacterAlignmentNpcQuestCatalog();
        var path = Path.Combine(projectRoot, "Librarium", "SourceImports", "MMExplorerSQLData",
            "TextblocksDecoded0142", "textblocks_decoded.json");
        if (!File.Exists(path)) return catalog;

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array) return catalog;

        var records = document.RootElement.EnumerateArray()
            .Where(x => ReadInt(x, "textblock_id") > 0)
            .ToDictionary(x => ReadInt(x, "textblock_id"), x => x.Clone());

        catalog.Project(records, "Good", 126, "npc.244", "Chancellor Annotated", 335);
        catalog.Project(records, "Neutral", 127, "npc.251", "Grey Lord", 366);
        catalog.Project(records, "Evil", 128, "npc.263", "Balthazar", 428);
        return catalog;
    }

    public RuntimeCharacterAlignmentNpcQuest Require(string alignmentPath) =>
        _byPath.TryGetValue(alignmentPath, out var quest)
            ? quest
            : throw new InvalidOperationException($"Decoded alignment NPC quest path '{alignmentPath}' is unavailable.");

    private void Project(
        IReadOnlyDictionary<int, JsonElement> records,
        string alignmentPath,
        int questAbilityId,
        string npcId,
        string npcName,
        int rootTextblockId)
    {
        if (!records.TryGetValue(rootTextblockId, out var root)) return;
        if (!root.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array) return;

        var projected = new List<RuntimeCharacterAlignmentNpcCommand>();
        foreach (var command in commands.EnumerateArray())
        {
            var keyword = ReadString(command, "normalized_keyword");
            if (string.IsNullOrWhiteSpace(keyword)) keyword = ReadString(command, "keyword");
            var target = ReadInt(command, "target_textblock_id");
            if (string.IsNullOrWhiteSpace(keyword) || target <= 0) continue;

            var reachable = Traverse(records, target, 64);
            var actionTextblocks = new List<int>();
            var mutationOpcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var textblockId in reachable)
            {
                if (!records.TryGetValue(textblockId, out var record)) continue;
                if (!record.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array) continue;
                if (operations.GetArrayLength() > 0) actionTextblocks.Add(textblockId);
                foreach (var operation in operations.EnumerateArray())
                {
                    var opcode = ReadString(operation, "opcode");
                    if (IsCharacterMutation(opcode)) mutationOpcodes.Add(opcode);
                }
            }

            projected.Add(new RuntimeCharacterAlignmentNpcCommand(
                keyword,
                target,
                reachable,
                actionTextblocks.Distinct().OrderBy(x => x).ToArray(),
                mutationOpcodes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                "verified",
                $"MMExplorer decoded textblock.{rootTextblockId} command '{keyword}' -> textblock.{target}"));
        }

        _byPath[alignmentPath] = new RuntimeCharacterAlignmentNpcQuest(
            alignmentPath,
            questAbilityId,
            npcId,
            npcName,
            rootTextblockId,
            projected.OrderBy(x => x.Keyword, StringComparer.OrdinalIgnoreCase).ToArray(),
            "verified",
            $"MMExplorer decoded NPC command map textblock.{rootTextblockId}");
    }

    private static IReadOnlyList<int> Traverse(IReadOnlyDictionary<int, JsonElement> records, int start, int maximum)
    {
        var queue = new Queue<int>();
        var visited = new HashSet<int>();
        queue.Enqueue(start);
        while (queue.Count > 0 && visited.Count < maximum)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current) || !records.TryGetValue(current, out var record)) continue;

            var next = ReadInt(record, "next_textblock_id");
            if (next > 0 && !visited.Contains(next)) queue.Enqueue(next);

            if (record.TryGetProperty("commands", out var commands) && commands.ValueKind == JsonValueKind.Array)
            {
                foreach (var command in commands.EnumerateArray())
                {
                    var target = ReadInt(command, "target_textblock_id");
                    if (target > 0 && !visited.Contains(target)) queue.Enqueue(target);
                }
            }

            if (record.TryGetProperty("operations", out var operations) && operations.ValueKind == JsonValueKind.Array)
            {
                foreach (var operation in operations.EnumerateArray())
                {
                    var target = ReadInt(operation, "target_textblock_id");
                    if (target > 0 && !visited.Contains(target)) queue.Enqueue(target);
                }
            }
        }
        return visited.OrderBy(x => x).ToArray();
    }

    private static bool IsCharacterMutation(string opcode) => opcode.ToLowerInvariant() switch
    {
        "giveability" or "addability" or "giveitem" or "takeitem" or "addexp" or "addevil" or "learnspell" => true,
        _ => false
    };

    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

// Consolidated from RuntimeCharacterAlignmentQuestGateCatalog.cs
public sealed record RuntimeCharacterAlignmentQuestLevelBranch(
    string AlignmentPath,
    int QuestAbilityId,
    int MinimumLevel,
    int RequiredStageValue,
    int SourceTextblockId,
    int SourceLine,
    int TargetTextblockId,
    string EvidenceStatus,
    string DecodedSource);

public sealed record RuntimeCharacterAlignmentQuestTransition(
    string AlignmentPath,
    int QuestAbilityId,
    int MinimumLevel,
    int PreviousStageValue,
    int GrantedStageValue,
    int SourceTextblockId,
    int SourceLine,
    IReadOnlyList<string> GrantedItemIds,
    IReadOnlyList<string> RequiredItemIds,
    int ExperienceReward,
    int EvilPointChange,
    int ResultTextblockId,
    string EvidenceStatus,
    string DecodedSource);

/// <summary>
/// Typed projection of decoded alignment-quest level/stage transitions. This catalog does not
/// invent missing stages: it records only lines where minlevel, the alignment quest ability,
/// and giveability are present together in generated Classic quest data.
/// </summary>
public sealed class RuntimeCharacterAlignmentQuestGateCatalog
{
    private readonly List<RuntimeCharacterAlignmentQuestTransition> _transitions = new();
    private readonly List<RuntimeCharacterAlignmentQuestLevelBranch> _branches = new();

    public IReadOnlyList<RuntimeCharacterAlignmentQuestLevelBranch> Branches => _branches
        .OrderBy(x => x.QuestAbilityId)
        .ThenBy(x => x.MinimumLevel)
        .ThenBy(x => x.SourceTextblockId)
        .ThenBy(x => x.SourceLine)
        .ToArray();

    public IReadOnlyList<RuntimeCharacterAlignmentQuestTransition> Transitions => _transitions
        .OrderBy(x => x.QuestAbilityId)
        .ThenBy(x => x.MinimumLevel)
        .ThenBy(x => x.GrantedStageValue)
        .ThenBy(x => x.SourceTextblockId)
        .ToArray();

    public static RuntimeCharacterAlignmentQuestGateCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterAlignmentQuestGateCatalog();
        var path = Path.Combine(projectRoot, "Data", worldId, "quests.json");
        if (!File.Exists(path)) return catalog;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        catalog.Scan(document.RootElement, 0);
        catalog.Deduplicate();
        return catalog;
    }

    public IReadOnlyList<RuntimeCharacterAlignmentQuestTransition> ForPath(string alignmentPath) =>
        Transitions.Where(x => x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<RuntimeCharacterAlignmentQuestLevelBranch> ForPathAtLevel(string alignmentPath, int minimumLevel) =>
        Branches.Where(x =>
            x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase) &&
            x.MinimumLevel == minimumLevel).ToArray();

    public bool TryGetBranch(string alignmentPath, int minimumLevel, out RuntimeCharacterAlignmentQuestLevelBranch branch)
    {
        branch = ForPathAtLevel(alignmentPath, minimumLevel).FirstOrDefault()!;
        return branch is not null;
    }

    public bool TryGetTransition(string alignmentPath, int minimumLevel, out RuntimeCharacterAlignmentQuestTransition transition)
    {
        transition = Transitions.SingleOrDefault(x =>
            x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase) &&
            x.MinimumLevel == minimumLevel)!;
        return transition is not null;
    }

    public bool IsEligible(RuntimeCharacterRecord character, RuntimeCharacterQuestAbilityCatalog abilities, RuntimeCharacterAlignmentQuestTransition transition) =>
        character.Level >= transition.MinimumLevel &&
        abilities.HasAtLeast(character, transition.QuestAbilityId, transition.PreviousStageValue);

    private void Scan(JsonElement element, int inheritedTextblockId)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) Scan(child, inheritedTextblockId);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        var textblockId = ReadInt(element, "textblock_id");
        if (textblockId <= 0) textblockId = inheritedTextblockId;

        if (element.TryGetProperty("operations", out var operations) && operations.ValueKind == JsonValueKind.Array)
        {
            foreach (var lineGroup in operations.EnumerateArray().GroupBy(operation => ReadInt(operation, "line")))
                ProjectLine(textblockId, lineGroup.OrderBy(operation => ReadInt(operation, "sequence")).ToArray());
        }

        foreach (var property in element.EnumerateObject())
            Scan(property.Value, textblockId);
    }

    private void ProjectLine(int textblockId, IReadOnlyList<JsonElement> line)
    {
        ProjectLevelBranch(textblockId, line);
        var minLevelOperation = line.FirstOrDefault(operation => Opcode(operation, "minlevel"));
        var grantOperation = line.FirstOrDefault(operation => Opcode(operation, "giveability") && IsAlignmentAbility(AbilityId(operation)));
        if (minLevelOperation.ValueKind == JsonValueKind.Undefined || grantOperation.ValueKind == JsonValueKind.Undefined) return;

        var minimumLevel = FirstNumericArgument(minLevelOperation);
        var abilityId = AbilityId(grantOperation);
        var grantedValue = AbilityValue(grantOperation);
        if (minimumLevel <= 0 || grantedValue <= 0) return;

        var previous = line
            .Where(operation => (Opcode(operation, "checkability") || Opcode(operation, "testability")) && AbilityId(operation) == abilityId)
            .Select(AbilityValue)
            .Where(value => value > 0)
            .DefaultIfEmpty(Math.Max(0, grantedValue - 1))
            .Min();

        var grantedItems = line.Where(operation => Opcode(operation, "giveitem"))
            .Select(operation => $"item.{FirstNumericArgument(operation)}")
            .Where(id => !id.EndsWith(".0", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requiredItems = line.Where(operation => Opcode(operation, "checkitem") || Opcode(operation, "takeitem"))
            .Select(operation => $"item.{FirstNumericArgument(operation)}")
            .Where(id => !id.EndsWith(".0", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var experienceReward = line.Where(operation => Opcode(operation, "addexp"))
            .Select(FirstNumericArgument).DefaultIfEmpty(0).Max();
        var evilPointChange = line.Where(operation => Opcode(operation, "addevil"))
            .Select(FirstNumericArgument).DefaultIfEmpty(0).Sum();
        var resultTextblockId = line.Where(operation => Opcode(operation, "text"))
            .Select(operation => ReadInt(operation, "target_textblock_id"))
            .FirstOrDefault(value => value > 0);

        _transitions.Add(new RuntimeCharacterAlignmentQuestTransition(
            AlignmentPath(abilityId), abilityId, minimumLevel, previous, grantedValue,
            textblockId, ReadInt(grantOperation, "line"), grantedItems, requiredItems,
            experienceReward, evilPointChange, resultTextblockId, "verified",
            $"decoded textblock.{textblockId} line {ReadInt(grantOperation, "line")}: minlevel/checkability-or-testability/giveability plus same-line reward operations"));
    }


    private void ProjectLevelBranch(int textblockId, IReadOnlyList<JsonElement> line)
    {
        var minLevelOperation = line.FirstOrDefault(operation => Opcode(operation, "minlevel"));
        if (minLevelOperation.ValueKind == JsonValueKind.Undefined) return;
        var minimumLevel = FirstNumericArgument(minLevelOperation);
        if (minimumLevel <= 0) return;

        var stageOperation = line.FirstOrDefault(operation =>
            (Opcode(operation, "checkability") || Opcode(operation, "testability")) &&
            IsAlignmentAbility(AbilityId(operation)) &&
            AbilityValue(operation) > 0 && AbilityValue(operation) < 100);
        if (stageOperation.ValueKind == JsonValueKind.Undefined) return;

        var abilityId = AbilityId(stageOperation);
        var requiredStage = AbilityValue(stageOperation);
        var targetTextblockId = line.Where(operation => Opcode(operation, "text"))
            .Select(operation => ReadInt(operation, "target_textblock_id") > 0 ? ReadInt(operation, "target_textblock_id") : FirstNumericArgument(operation))
            .FirstOrDefault(value => value > 0);
        if (targetTextblockId <= 0) return;

        _branches.Add(new RuntimeCharacterAlignmentQuestLevelBranch(
            AlignmentPath(abilityId), abilityId, minimumLevel, requiredStage,
            textblockId, ReadInt(stageOperation, "line"), targetTextblockId,
            "verified",
            $"decoded textblock.{textblockId} line {ReadInt(stageOperation, "line")}: alignment stage check + minlevel + text branch"));
    }

    private void Deduplicate()
    {
        var unique = _transitions
            .GroupBy(x => (x.QuestAbilityId, x.MinimumLevel, x.PreviousStageValue, x.GrantedStageValue, x.SourceTextblockId, x.SourceLine))
            .Select(group => group.First())
            .ToArray();
        _transitions.Clear();
        _transitions.AddRange(unique);

        var branchUnique = _branches
            .GroupBy(x => (x.QuestAbilityId, x.MinimumLevel, x.RequiredStageValue, x.SourceTextblockId, x.SourceLine, x.TargetTextblockId))
            .Select(group => group.First())
            .ToArray();
        _branches.Clear();
        _branches.AddRange(branchUnique);
    }

    private static bool IsAlignmentAbility(int abilityId) => abilityId is 126 or 127 or 128;
    private static string AlignmentPath(int abilityId) => abilityId switch
    {
        126 => "Good",
        127 => "Neutral",
        128 => "Evil",
        _ => throw new ArgumentOutOfRangeException(nameof(abilityId))
    };
    private static bool Opcode(JsonElement operation, string expected) =>
        ReadString(operation, "opcode").Equals(expected, StringComparison.OrdinalIgnoreCase);
    private static int AbilityId(JsonElement operation)
    {
        var value = ReadInt(operation, "ability_id");
        return value > 0 ? value : FirstNumericArgument(operation);
    }
    private static int AbilityValue(JsonElement operation)
    {
        var value = ReadInt(operation, "ability_value");
        return value > 0 ? value : NumericArgument(operation, 1);
    }
    private static int FirstNumericArgument(JsonElement operation) => NumericArgument(operation, 0);
    private static int NumericArgument(JsonElement operation, int index)
    {
        if (!operation.TryGetProperty("numeric_arguments", out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() <= index) return 0;
        var value = values[index];
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }
    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }
    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}

// Consolidated from RuntimeCharacterAlignmentQuestPayloadCatalog.cs
public sealed record RuntimeCharacterAlignmentQuestPayload(
    string AlignmentPath,
    int QuestAbilityId,
    int PreviousStageValue,
    int GrantedStageValue,
    int SourceTextblockId,
    int SourceLine,
    IReadOnlyList<string> RequiredItemIds,
    IReadOnlyList<string> RemovedItemIds,
    IReadOnlyList<string> GrantedItemIds,
    int ExperienceReward,
    int EvilPointChange,
    int ResultTextblockId,
    string EvidenceStatus,
    string DecodedSource);

/// <summary>
/// Complete typed projection of decoded Good/Neutral/Evil quest mutation lines.
/// Unlike the level routing catalog, this includes reward/completion stages that do not repeat
/// a minlevel opcode on the same line. It is character-side evidence only; Bundle F owns the
/// dialogue activation and ordered script execution.
/// </summary>
public sealed class RuntimeCharacterAlignmentQuestPayloadCatalog
{
    private readonly List<RuntimeCharacterAlignmentQuestPayload> _payloads = new();

    public IReadOnlyList<RuntimeCharacterAlignmentQuestPayload> Payloads => _payloads
        .OrderBy(x => x.QuestAbilityId)
        .ThenBy(x => x.PreviousStageValue)
        .ThenBy(x => x.GrantedStageValue)
        .ThenBy(x => x.SourceTextblockId)
        .ThenBy(x => x.SourceLine)
        .ToArray();

    public static RuntimeCharacterAlignmentQuestPayloadCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterAlignmentQuestPayloadCatalog();
        var path = Path.Combine(projectRoot, "Data", worldId, "quests.json");
        if (!File.Exists(path)) return catalog;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        catalog.Scan(document.RootElement, 0);
        catalog.Deduplicate();
        return catalog;
    }

    public IReadOnlyList<RuntimeCharacterAlignmentQuestPayload> ForPath(string alignmentPath) =>
        Payloads.Where(x => x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<RuntimeCharacterAlignmentQuestPayload> FromStage(string alignmentPath, int previousStageValue) =>
        Payloads.Where(x => x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase) && x.PreviousStageValue == previousStageValue).ToArray();

    private void Scan(JsonElement element, int inheritedTextblockId)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) Scan(child, inheritedTextblockId);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        var textblockId = ReadInt(element, "textblock_id");
        if (textblockId <= 0) textblockId = inheritedTextblockId;

        if (element.TryGetProperty("operations", out var operations) && operations.ValueKind == JsonValueKind.Array)
        {
            foreach (var lineGroup in operations.EnumerateArray().GroupBy(operation => ReadInt(operation, "line")))
                ProjectLine(textblockId, lineGroup.OrderBy(operation => ReadInt(operation, "sequence")).ToArray());
        }

        foreach (var property in element.EnumerateObject()) Scan(property.Value, textblockId);
    }

    private void ProjectLine(int textblockId, IReadOnlyList<JsonElement> line)
    {
        var grant = line.FirstOrDefault(operation => Opcode(operation, "giveability") && IsAlignmentAbility(AbilityId(operation)));
        if (grant.ValueKind == JsonValueKind.Undefined) return;

        var abilityId = AbilityId(grant);
        var grantedValue = AbilityValue(grant);
        if (grantedValue <= 0) return;

        var previous = line
            .Where(operation => (Opcode(operation, "checkability") || Opcode(operation, "testability")) && AbilityId(operation) == abilityId)
            .Select(AbilityValue)
            .Where(value => value > 0)
            .DefaultIfEmpty(Math.Max(0, grantedValue - 1))
            .Min();

        var required = line.Where(operation => Opcode(operation, "checkitem"))
            .Select(operation => ItemId(operation)).Where(id => id is not null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var removed = line.Where(operation => Opcode(operation, "takeitem"))
            .Select(operation => ItemId(operation)).Where(id => id is not null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var granted = line.Where(operation => Opcode(operation, "giveitem"))
            .Select(operation => ItemId(operation)).Where(id => id is not null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var experience = line.Where(operation => Opcode(operation, "addexp"))
            .Select(FirstNumericArgument).DefaultIfEmpty(0).Max();
        var evil = line.Where(operation => Opcode(operation, "addevil"))
            .Select(FirstNumericArgument).DefaultIfEmpty(0).Sum();
        var resultText = line.Where(operation => Opcode(operation, "text"))
            .Select(operation => ReadInt(operation, "target_textblock_id") > 0 ? ReadInt(operation, "target_textblock_id") : FirstNumericArgument(operation))
            .FirstOrDefault(value => value > 0);

        _payloads.Add(new RuntimeCharacterAlignmentQuestPayload(
            AlignmentPath(abilityId), abilityId, previous, grantedValue, textblockId,
            ReadInt(grant, "line"), required, removed, granted, experience, evil, resultText,
            "verified", $"decoded textblock.{textblockId} line {ReadInt(grant, "line")}: alignment giveability and same-line reward payload"));
    }

    private void Deduplicate()
    {
        var unique = _payloads.GroupBy(x => (x.QuestAbilityId, x.PreviousStageValue, x.GrantedStageValue, x.SourceTextblockId, x.SourceLine))
            .Select(group => group.First()).ToArray();
        _payloads.Clear();
        _payloads.AddRange(unique);
    }

    private static bool IsAlignmentAbility(int abilityId) => abilityId is 126 or 127 or 128;
    private static string AlignmentPath(int abilityId) => abilityId switch { 126 => "Good", 127 => "Neutral", 128 => "Evil", _ => "Unknown" };
    private static bool Opcode(JsonElement operation, string opcode) =>
        operation.TryGetProperty("opcode", out var value) && value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), opcode, StringComparison.OrdinalIgnoreCase);
    private static int AbilityId(JsonElement operation) => ReadInt(operation, "ability_id") is var value && value > 0 ? value : NumericArgument(operation, 0);
    private static int AbilityValue(JsonElement operation) => ReadInt(operation, "ability_value") is var value && value != 0 ? value : NumericArgument(operation, 1);
    private static string? ItemId(JsonElement operation)
    {
        var value = FirstNumericArgument(operation);
        return value > 0 ? $"item.{value}" : null;
    }
    private static int FirstNumericArgument(JsonElement operation) => NumericArgument(operation, 0);
    private static int NumericArgument(JsonElement operation, int index)
    {
        if (!operation.TryGetProperty("numeric_arguments", out var values) || values.ValueKind != JsonValueKind.Array) return 0;
        var array = values.EnumerateArray().ToArray();
        return index >= 0 && index < array.Length && array[index].TryGetInt32(out var value) ? value : 0;
    }
    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }
}

// Consolidated from RuntimeCharacterAlignmentRewardCatalog.cs
public sealed record RuntimeCharacterAlignmentItemReward(
    string AlignmentPath,
    int QuestAbilityId,
    int RequiredStageValue,
    int ClassLegacyNumber,
    string ClassName,
    string ItemId,
    string ItemName,
    int SourceTextblockId,
    int MessageTextblockId,
    string EvidenceStatus,
    string DecodedSource);

public sealed record RuntimeCharacterAlignmentItemEligibility(
    bool Eligible,
    string Reason,
    RuntimeCharacterAlignmentItemReward Reward);

/// <summary>
/// Projects class-specific alignment quest item rewards directly from decoded quest operations.
/// Bundle A owns the character entitlement and quest-stage relationship; Bundle F executes the
/// decoded script and Bundle C owns inventory/equipment enforcement.
/// </summary>
public sealed class RuntimeCharacterAlignmentRewardCatalog
{
    private readonly List<RuntimeCharacterAlignmentItemReward> _itemRewards = new();

    public IReadOnlyList<RuntimeCharacterAlignmentItemReward> ItemRewards => _itemRewards
        .OrderBy(x => x.AlignmentPath)
        .ThenBy(x => x.ClassLegacyNumber)
        .ToArray();

    public static RuntimeCharacterAlignmentRewardCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterAlignmentRewardCatalog();
        catalog.LoadDecodedClassItemRewards(projectRoot, worldId);
        return catalog;
    }

    public RuntimeCharacterAlignmentItemReward RequireItemReward(string alignmentPath, int classLegacyNumber) =>
        _itemRewards.SingleOrDefault(x =>
            x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase) &&
            x.ClassLegacyNumber == classLegacyNumber)
        ?? throw new KeyNotFoundException($"No decoded alignment item reward exists for path '{alignmentPath}' and class {classLegacyNumber}.");

    public RuntimeCharacterAlignmentItemEligibility EvaluateItemEligibility(
        RuntimeCharacterRecord character,
        RuntimeCharacterQuestAbilityCatalog abilities,
        string alignmentPath,
        int classLegacyNumber)
    {
        var reward = RequireItemReward(alignmentPath, classLegacyNumber);
        var classIdMatches = TryReadLegacyNumber(character.ClassId, "class.", out var characterClassNumber) && characterClassNumber == reward.ClassLegacyNumber;
        var classNameMatches = character.ClassName.Equals(reward.ClassName, StringComparison.OrdinalIgnoreCase);
        if (!classIdMatches && !classNameMatches)
            return new(false, $"Requires class '{reward.ClassName}' (class.{reward.ClassLegacyNumber}).", reward);
        if (!abilities.HasAtLeast(character, reward.QuestAbilityId, reward.RequiredStageValue))
            return new(false, $"Requires quest ability {reward.QuestAbilityId} stage {reward.RequiredStageValue}.", reward);
        return new(true, "Decoded class and quest-stage gates passed.", reward);
    }

    private static bool TryReadLegacyNumber(string canonicalId, string prefix, out int number)
    {
        number = 0;
        return canonicalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(canonicalId[prefix.Length..], out number) && number > 0;
    }

    private void LoadDecodedClassItemRewards(string projectRoot, string worldId)
    {
        var classes = LoadClassNames(Path.Combine(projectRoot, "Data", worldId, "classes.json"));
        var items = LoadItemNames(Path.Combine(projectRoot, "Data", worldId, "items.json"));
        var questsPath = Path.Combine(projectRoot, "Data", worldId, "quests.json");
        if (!File.Exists(questsPath)) return;

        using var document = JsonDocument.Parse(File.ReadAllText(questsPath));
        Scan(document.RootElement, textblockId: 0, classes, items);

        var unique = _itemRewards
            .GroupBy(x => (x.AlignmentPath.ToUpperInvariant(), x.QuestAbilityId, x.RequiredStageValue, x.ClassLegacyNumber, x.ItemId), EqualityComparer<(string, int, int, int, string)>.Default)
            .Select(group => group.First())
            .ToArray();
        _itemRewards.Clear();
        _itemRewards.AddRange(unique);
    }

    private void Scan(JsonElement element, int textblockId, IReadOnlyDictionary<int, string> classes, IReadOnlyDictionary<string, string> items)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) Scan(child, textblockId, classes, items);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        var localTextblockId = ReadInt(element, "textblock_id");
        if (localTextblockId <= 0) localTextblockId = textblockId;

        if (element.TryGetProperty("operations", out var operations) && operations.ValueKind == JsonValueKind.Array)
            ProjectOperationLines(localTextblockId, operations, classes, items);

        foreach (var property in element.EnumerateObject())
            Scan(property.Value, localTextblockId, classes, items);
    }

    private void ProjectOperationLines(int textblockId, JsonElement operations, IReadOnlyDictionary<int, string> classes, IReadOnlyDictionary<string, string> items)
    {
        foreach (var lineGroup in operations.EnumerateArray().GroupBy(operation => ReadInt(operation, "line")))
        {
            var line = lineGroup.OrderBy(operation => ReadInt(operation, "sequence")).ToArray();
            var classOperation = line.FirstOrDefault(operation => Opcode(operation, "class"));
            var checkAbility = line.FirstOrDefault(operation => Opcode(operation, "checkability"));
            var giveItem = line.FirstOrDefault(operation => Opcode(operation, "giveitem"));
            if (classOperation.ValueKind == JsonValueKind.Undefined ||
                checkAbility.ValueKind == JsonValueKind.Undefined ||
                giveItem.ValueKind == JsonValueKind.Undefined)
                continue;

            var classNumber = FirstNumericArgument(classOperation);
            var abilityId = ReadInt(checkAbility, "ability_id");
            if (abilityId <= 0) abilityId = FirstNumericArgument(checkAbility);
            var abilityValue = ReadInt(checkAbility, "ability_value");
            if (abilityValue <= 0) abilityValue = NumericArgument(checkAbility, 1);
            var itemNumber = ReadInt(giveItem, "item_id");
            if (itemNumber <= 0) itemNumber = FirstNumericArgument(giveItem);

            if (classNumber <= 0 || abilityId is not (126 or 127 or 128) || abilityValue <= 0 || itemNumber <= 0)
                continue;
            if (!classes.TryGetValue(classNumber, out var className))
                throw new InvalidDataException($"Decoded alignment reward textblock {textblockId} references unknown class {classNumber}.");

            var itemId = $"item.{itemNumber}";
            if (!items.TryGetValue(itemId, out var itemName))
                throw new InvalidDataException($"Decoded alignment reward textblock {textblockId} references unknown item {itemId}.");

            var failItem = line.FirstOrDefault(operation => Opcode(operation, "failitem"));
            if (failItem.ValueKind != JsonValueKind.Undefined && FirstNumericArgument(failItem) != itemNumber)
                throw new InvalidDataException($"Decoded alignment reward textblock {textblockId} checks a different item than it gives for class {classNumber}.");

            var message = line.FirstOrDefault(operation => Opcode(operation, "message"));
            var messageTextblockId = message.ValueKind == JsonValueKind.Undefined ? 0 : ReadInt(message, "target_textblock_id");
            if (messageTextblockId <= 0 && message.ValueKind != JsonValueKind.Undefined) messageTextblockId = FirstNumericArgument(message);

            _itemRewards.Add(new RuntimeCharacterAlignmentItemReward(
                AlignmentPath(abilityId), abilityId, abilityValue, classNumber, className, itemId, itemName,
                textblockId, messageTextblockId,
                "verified",
                $"decoded textblock.{textblockId} line {ReadInt(classOperation, "line")}: class/checkability/failitem/giveitem"));
        }
    }

    private static IReadOnlyDictionary<int, string> LoadClassNames(string path)
    {
        if (!File.Exists(path)) return new Dictionary<int, string>();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("records").EnumerateArray()
            .Select(record => (Number: ReadNestedInt(record, "sourceNumbers", "number"), Name: NormalizeName(ReadString(record, "name"))))
            .Where(x => x.Number > 0 && !string.IsNullOrWhiteSpace(x.Name))
            .ToDictionary(x => x.Number, x => x.Name);
    }

    private static IReadOnlyDictionary<string, string> LoadItemNames(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("records").EnumerateArray()
            .Select(record => (Id: ReadString(record, "id"), Name: ReadString(record, "name")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .ToDictionary(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string value)
    {
        var normalized = value.Trim();
        while (normalized.Length > 0 && !char.IsLetterOrDigit(normalized[^1])) normalized = normalized[..^1];
        return normalized;
    }

    private static string AlignmentPath(int abilityId) => abilityId switch
    {
        126 => "Good",
        127 => "Neutral",
        128 => "Evil",
        _ => throw new ArgumentOutOfRangeException(nameof(abilityId))
    };

    private static bool Opcode(JsonElement operation, string expected) =>
        ReadString(operation, "opcode").Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static int FirstNumericArgument(JsonElement operation) => NumericArgument(operation, 0);

    private static int NumericArgument(JsonElement operation, int index)
    {
        if (!operation.TryGetProperty("numeric_arguments", out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() <= index)
            return 0;
        var value = values[index];
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }

    private static int ReadNestedInt(JsonElement element, string objectProperty, string valueProperty)
    {
        if (!element.TryGetProperty(objectProperty, out var nested) || nested.ValueKind != JsonValueKind.Object) return 0;
        return ReadInt(nested, valueProperty);
    }

    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}

// Consolidated from RuntimeCharacterAlignmentRewardMatrixCatalog.cs
public sealed record RuntimeCharacterAlignmentRewardMatrixCell(
    string AlignmentPath,
    int QuestAbilityId,
    int QuestLevel,
    int ClassLegacyNumber,
    string ClassName,
    int RequiredStageValue,
    IReadOnlyList<string> RequiredItemIds,
    IReadOnlyList<string> RemovedItemIds,
    IReadOnlyList<string> GrantedItemIds,
    IReadOnlyList<int> GrantedAbilityIds,
    IReadOnlyList<string> LearnedSpellIds,
    int ExperienceReward,
    int EvilPointChange,
    bool RewardPayloadResolved,
    string EvidenceStatus,
    string DecodedSource);

/// <summary>
/// Field-exhaustion ledger for the complete Good/Neutral/Evil alignment reward matrix.
/// It combines the decoded level router, stage payloads, and class-specific reward records
/// into one 3 alignment x 5 level x 15 class view. Missing cells remain explicit and fail
/// closed instead of being mistaken for universal or player-selected rewards.
/// </summary>
public sealed class RuntimeCharacterAlignmentRewardMatrixCatalog
{
    private readonly List<RuntimeCharacterAlignmentRewardMatrixCell> _cells = new();

    public IReadOnlyList<RuntimeCharacterAlignmentRewardMatrixCell> Cells => _cells
        .OrderBy(x => x.QuestAbilityId)
        .ThenBy(x => x.QuestLevel)
        .ThenBy(x => x.ClassLegacyNumber)
        .ToArray();

    public IReadOnlyList<RuntimeCharacterAlignmentRewardMatrixCell> ResolvedCells =>
        Cells.Where(x => x.RewardPayloadResolved).ToArray();

    public IReadOnlyList<RuntimeCharacterAlignmentRewardMatrixCell> UnresolvedCells =>
        Cells.Where(x => !x.RewardPayloadResolved).ToArray();

    public static RuntimeCharacterAlignmentRewardMatrixCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterAlignmentRewardMatrixCatalog();
        var classes = RuntimeCharacterCatalog.Load(projectRoot, worldId).Classes
            .Where(x => x.LegacyNumber > 0)
            .OrderBy(x => x.LegacyNumber)
            .ToArray();
        var gates = RuntimeCharacterAlignmentQuestGateCatalog.Load(projectRoot, worldId);
        var payloads = RuntimeCharacterAlignmentQuestPayloadCatalog.Load(projectRoot, worldId);
        var itemRewards = RuntimeCharacterAlignmentRewardCatalog.Load(projectRoot, worldId);

        foreach (var (path, abilityId) in new[] { ("Good", 126), ("Neutral", 127), ("Evil", 128) })
        {
            foreach (var level in new[] { 10, 20, 30, 40, 50 })
            {
                var branch = gates.ForPathAtLevel(path, level).FirstOrDefault();
                var transition = gates.Transitions.FirstOrDefault(x =>
                    x.AlignmentPath.Equals(path, StringComparison.OrdinalIgnoreCase) && x.MinimumLevel == level);

                foreach (var characterClass in classes)
                {
                    var classItem = itemRewards.ItemRewards.FirstOrDefault(x =>
                        x.AlignmentPath.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                        x.ClassLegacyNumber == characterClass.LegacyNumber);

                    // The current decoded class-item table is Good level 40/stage 19. Keep it
                    // class-specific; never fan it out to Neutral/Evil or other levels.
                    if (classItem is not null && level == 40)
                    {
                        catalog._cells.Add(new RuntimeCharacterAlignmentRewardMatrixCell(
                            path, abilityId, level, characterClass.LegacyNumber, characterClass.Name,
                            classItem.RequiredStageValue, Array.Empty<string>(), Array.Empty<string>(),
                            new[] { classItem.ItemId }, Array.Empty<int>(), Array.Empty<string>(), 0, 0,
                            true, "verified",
                            $"decoded textblock.{classItem.SourceTextblockId}: class {characterClass.LegacyNumber}/ability {abilityId} stage {classItem.RequiredStageValue}/giveitem {classItem.ItemId}"));
                        continue;
                    }

                    // A same-line global transition payload is applicable to every class only
                    // when it contains no class branch. This safely captures shared level-10
                    // turn-ins and progression without inventing class rewards.
                    if (transition is not null)
                    {
                        var payload = payloads.Payloads.FirstOrDefault(x =>
                            x.AlignmentPath.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                            x.SourceTextblockId == transition.SourceTextblockId &&
                            x.SourceLine == transition.SourceLine &&
                            x.GrantedStageValue == transition.GrantedStageValue);
                        if (payload is not null && HasConcreteReward(payload))
                        {
                            catalog._cells.Add(new RuntimeCharacterAlignmentRewardMatrixCell(
                                path, abilityId, level, characterClass.LegacyNumber, characterClass.Name,
                                branch?.RequiredStageValue ?? transition.PreviousStageValue,
                                payload.RequiredItemIds, payload.RemovedItemIds, payload.GrantedItemIds,
                                Array.Empty<int>(), Array.Empty<string>(), payload.ExperienceReward,
                                payload.EvilPointChange, true, "verified",
                                $"decoded textblock.{payload.SourceTextblockId} line {payload.SourceLine}: shared alignment transition payload"));
                            continue;
                        }
                    }

                    catalog._cells.Add(new RuntimeCharacterAlignmentRewardMatrixCell(
                        path, abilityId, level, characterClass.LegacyNumber, characterClass.Name,
                        branch?.RequiredStageValue ?? transition?.PreviousStageValue ?? 0,
                        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                        Array.Empty<int>(), Array.Empty<string>(), 0, 0, false,
                        branch is null ? "decoded but not mapped" : "relationship unresolved",
                        branch is null
                            ? $"No decoded level branch has been promoted for {path} level {level}."
                            : $"decoded textblock.{branch.SourceTextblockId} line {branch.SourceLine} routes {path} level {level} stage {branch.RequiredStageValue} to textblock.{branch.TargetTextblockId}; class reward payload not yet projected"));
                }
            }
        }
        return catalog;
    }

    public RuntimeCharacterAlignmentRewardMatrixCell Require(string alignmentPath, int questLevel, int classLegacyNumber) =>
        Cells.Single(x => x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase) &&
                          x.QuestLevel == questLevel && x.ClassLegacyNumber == classLegacyNumber);

    private static bool HasConcreteReward(RuntimeCharacterAlignmentQuestPayload payload) =>
        payload.RequiredItemIds.Count > 0 || payload.RemovedItemIds.Count > 0 ||
        payload.GrantedItemIds.Count > 0 || payload.ExperienceReward != 0 || payload.EvilPointChange != 0;
}

// Consolidated from RuntimeCharacterAlignmentQuestCorroborationCatalog.cs
public sealed record RuntimeAlignmentQuestLevelGate(int QuestOrdinal, int MinimumLevel);

public sealed record RuntimeAlignmentClassStatReward(
    int QuestOrdinal,
    int MinimumLevel,
    int ClassLegacyNumber,
    string ClassName,
    IReadOnlyDictionary<string, int> StatChanges,
    string EvidenceStatus,
    string EvidenceSource);

/// <summary>
/// Corroborating alignment-quest evidence supplied from the retained MudInfo archives.
/// This catalog does not execute rewards by itself. Runtime execution remains gated on
/// the decoded textblock operations; this source verifies the sequential level gates and
/// class-dependent second-quest stat packages before those operations are promoted.
/// </summary>
public sealed class RuntimeCharacterAlignmentQuestCorroborationCatalog
{
    public IReadOnlyList<RuntimeAlignmentQuestLevelGate> LevelGates { get; }
    public IReadOnlyList<RuntimeAlignmentClassStatReward> SecondQuestClassRewards { get; }

    public RuntimeCharacterAlignmentQuestCorroborationCatalog()
    {
        LevelGates = new[]
        {
            new RuntimeAlignmentQuestLevelGate(1, 10),
            new RuntimeAlignmentQuestLevelGate(2, 20),
            new RuntimeAlignmentQuestLevelGate(3, 30),
            new RuntimeAlignmentQuestLevelGate(4, 40),
            new RuntimeAlignmentQuestLevelGate(5, 50)
        };

        const string source = "Good/Neutral/Evil Alignment Quests - mudinfo.net retained MHT archives; must agree with decoded alignment textblocks before runtime promotion";
        SecondQuestClassRewards = new[]
        {
            Reward(1, "Warrior", Stats(("max_damage", 1)), source),
            Reward(2, "Witchunter", Stats(("max_damage", 1)), source),
            Reward(3, "Paladin", Stats(("max_damage", 1)), source),
            Reward(4, "Cleric", Stats(("armour_class", 1), ("maximum_mana", 6)), source),
            Reward(5, "Priest", Stats(("spellcasting", 1), ("maximum_mana", 10)), source),
            Reward(6, "Missionary", Stats(("maximum_mana", 4), ("backstab_minimum", 6), ("backstab_maximum", 6), ("stealth", 1)), source),
            Reward(7, "Ninja", Stats(("backstab_minimum", 10), ("backstab_maximum", 10), ("stealth", 2)), source),
            Reward(8, "Thief", Stats(("backstab_minimum", 10), ("backstab_maximum", 10), ("stealth", 2)), source),
            Reward(9, "Bard", Stats(("maximum_mana", 4), ("backstab_minimum", 6), ("backstab_maximum", 6), ("stealth", 1)), source),
            Reward(10, "Gypsy", Stats(("maximum_mana", 4), ("backstab_minimum", 6), ("backstab_maximum", 6), ("stealth", 1)), source),
            Reward(11, "Warlock", Stats(("armour_class", 1), ("maximum_mana", 6)), source),
            Reward(12, "Mage", Stats(("spellcasting", 1), ("maximum_mana", 10)), source),
            Reward(13, "Druid", Stats(("spellcasting", 1), ("maximum_mana", 10)), source),
            Reward(14, "Ranger", Stats(("backstab_minimum", 10), ("backstab_maximum", 10), ("stealth", 2)), source),
            Reward(15, "Mystic", Stats(("max_damage", 1)), source)
        };
    }

    public RuntimeAlignmentQuestLevelGate RequireGate(int questOrdinal) =>
        LevelGates.Single(x => x.QuestOrdinal == questOrdinal);

    public RuntimeAlignmentClassStatReward RequireSecondQuestReward(int classLegacyNumber) =>
        SecondQuestClassRewards.Single(x => x.ClassLegacyNumber == classLegacyNumber);

    private static RuntimeAlignmentClassStatReward Reward(int classNumber, string className, IReadOnlyDictionary<string, int> stats, string source) =>
        new(2, 20, classNumber, className, stats, "strongly corroborated", source);

    private static IReadOnlyDictionary<string, int> Stats(params (string Key, int Value)[] values) =>
        values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
}


// Revision 39: full decoded alignment reward graph promotion from authoritative roots 335/366/428.
public sealed record RuntimeCharacterAlignmentPromotedEffect(
    string AlignmentPath,
    int QuestAbilityId,
    string CommandKeyword,
    int CommandTextblockId,
    int SourceTextblockId,
    int SourceLine,
    int? ClassLegacyNumber,
    int? MinimumLevel,
    int? RequiredQuestStage,
    string Opcode,
    IReadOnlyList<int> NumericArguments,
    int? RandomTableTextblockId,
    string EvidenceStatus,
    string DecodedSource);

public sealed record RuntimeCharacterAlignmentPromotedCommandGraph(
    string AlignmentPath,
    int QuestAbilityId,
    int RootTextblockId,
    string CommandKeyword,
    int CommandTextblockId,
    IReadOnlyList<int> ReachableTextblockIds,
    IReadOnlyList<RuntimeCharacterAlignmentPromotedEffect> Effects,
    string EvidenceStatus,
    string DecodedSource);

/// <summary>
/// Promotes every decoded effect reachable from the three authoritative alignment NPC roots.
/// Traversal follows command targets, next-textblock links, branch targets, random-table targets,
/// and reverse owner links such as "Textblock #N" and "Textblock(rndm) #N". This is the
/// authoritative operation graph for Good (335), Neutral (366), and Evil (428); no screenshot-only
/// or hand-authored class reward table is required at runtime.
/// </summary>
public sealed class RuntimeCharacterAlignmentFullRewardCatalog
{
    private static readonly HashSet<string> EffectOpcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "giveability", "addability", "giveitem", "takeitem", "learnspell", "addexp", "addevil",
        "failitem", "checkitem", "random", "message", "text"
    };

    private readonly List<RuntimeCharacterAlignmentPromotedCommandGraph> _graphs = new();
    public IReadOnlyList<RuntimeCharacterAlignmentPromotedCommandGraph> Graphs => _graphs
        .OrderBy(x => x.QuestAbilityId).ThenBy(x => x.CommandKeyword, StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<RuntimeCharacterAlignmentPromotedEffect> Effects => Graphs.SelectMany(x => x.Effects).ToArray();

    public static RuntimeCharacterAlignmentFullRewardCatalog Load(string projectRoot)
    {
        var catalog = new RuntimeCharacterAlignmentFullRewardCatalog();
        var path = Path.Combine(projectRoot, "Librarium", "SourceImports", "MMExplorerSQLData",
            "TextblocksDecoded0142", "textblocks_decoded.json");
        if (!File.Exists(path)) return catalog;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array) return catalog;

        var records = document.RootElement.EnumerateArray()
            .Where(x => ReadInt39(x, "textblock_id") > 0)
            .ToDictionary(x => ReadInt39(x, "textblock_id"), x => x.Clone());
        var ownedChildren = BuildOwnedChildren(records);

        catalog.Project(records, ownedChildren, "Good", 126, 335);
        catalog.Project(records, ownedChildren, "Neutral", 127, 366);
        catalog.Project(records, ownedChildren, "Evil", 128, 428);
        return catalog;
    }

    public RuntimeCharacterAlignmentPromotedCommandGraph Require(string alignmentPath, string commandKeyword) =>
        Graphs.Single(x => x.AlignmentPath.Equals(alignmentPath, StringComparison.OrdinalIgnoreCase) &&
                           x.CommandKeyword.Equals(commandKeyword, StringComparison.OrdinalIgnoreCase));

    private void Project(
        IReadOnlyDictionary<int, JsonElement> records,
        IReadOnlyDictionary<int, IReadOnlyList<int>> ownedChildren,
        string path,
        int abilityId,
        int rootTextblockId)
    {
        if (!records.TryGetValue(rootTextblockId, out var root) ||
            !root.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array)
            return;

        foreach (var command in commands.EnumerateArray())
        {
            var keyword = ReadString39(command, "normalized_keyword");
            if (string.IsNullOrWhiteSpace(keyword)) keyword = ReadString39(command, "keyword");
            var target = ReadInt39(command, "target_textblock_id");
            if (string.IsNullOrWhiteSpace(keyword) || target <= 0) continue;

            var reachable = Traverse39(records, ownedChildren, target, 4096);
            var effects = new List<RuntimeCharacterAlignmentPromotedEffect>();
            foreach (var textblockId in reachable)
            {
                if (!records.TryGetValue(textblockId, out var record) ||
                    !record.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var line in operations.EnumerateArray().GroupBy(x => ReadInt39(x, "line")))
                {
                    var ordered = line.OrderBy(x => ReadInt39(x, "sequence")).ToArray();
                    var classNumber = ordered.Where(x => Opcode39(x, "class")).Select(FirstNumber39).FirstOrDefault();
                    var minimumLevel = ordered.Where(x => Opcode39(x, "minlevel")).Select(FirstNumber39).Where(x => x > 0).DefaultIfEmpty(0).Max();
                    var requiredStage = ordered
                        .Where(x => (Opcode39(x, "checkability") || Opcode39(x, "testability")) && ReadInt39(x, "ability_id") == abilityId)
                        .Select(x => ReadInt39(x, "ability_value")).Where(x => x > 0 && x < 800).DefaultIfEmpty(0).Max();

                    foreach (var operation in ordered.Where(x => EffectOpcodes.Contains(ReadString39(x, "opcode"))))
                    {
                        var opcode = ReadString39(operation, "opcode");
                        var numbers = ReadNumbers39(operation);
                        var randomTarget = opcode.Equals("random", StringComparison.OrdinalIgnoreCase) && numbers.Count > 0
                            ? numbers[0]
                            : (int?)null;
                        effects.Add(new RuntimeCharacterAlignmentPromotedEffect(
                            path, abilityId, keyword, target, textblockId, line.Key,
                            classNumber > 0 ? classNumber : null,
                            minimumLevel > 0 ? minimumLevel : null,
                            requiredStage > 0 ? requiredStage : null,
                            opcode, numbers, randomTarget, "verified",
                            $"decoded alignment root textblock.{rootTextblockId} command '{keyword}' -> textblock.{textblockId} line {line.Key}: {ReadString39(operation, "raw")}"));
                    }
                }
            }

            _graphs.Add(new RuntimeCharacterAlignmentPromotedCommandGraph(
                path, abilityId, rootTextblockId, keyword, target, reachable,
                effects.OrderBy(x => x.SourceTextblockId).ThenBy(x => x.SourceLine).ToArray(),
                "verified",
                $"full decoded reward graph from textblock.{rootTextblockId} command '{keyword}', including owned/random child textblocks"));
        }
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<int>> BuildOwnedChildren(IReadOnlyDictionary<int, JsonElement> records)
    {
        var result = new Dictionary<int, List<int>>();
        foreach (var pair in records)
        {
            var owner = ReadString39(pair.Value, "owner_raw");
            if (string.IsNullOrWhiteSpace(owner)) continue;
            var hash = owner.LastIndexOf('#');
            if (hash < 0 || !int.TryParse(new string(owner[(hash + 1)..].TakeWhile(char.IsDigit).ToArray()), out var parent) || parent <= 0)
                continue;
            if (!owner.StartsWith("Textblock", StringComparison.OrdinalIgnoreCase)) continue;
            if (!result.TryGetValue(parent, out var children)) result[parent] = children = new List<int>();
            children.Add(pair.Key);
        }
        return result.ToDictionary(x => x.Key, x => (IReadOnlyList<int>)x.Value.Distinct().OrderBy(y => y).ToArray());
    }

    private static IReadOnlyList<int> Traverse39(
        IReadOnlyDictionary<int, JsonElement> records,
        IReadOnlyDictionary<int, IReadOnlyList<int>> ownedChildren,
        int start,
        int maximum)
    {
        var queue = new Queue<int>();
        var visited = new HashSet<int>();
        queue.Enqueue(start);
        while (queue.Count > 0 && visited.Count < maximum)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current) || !records.TryGetValue(current, out var record)) continue;
            var next = ReadInt39(record, "next_textblock_id");
            if (next > 0) queue.Enqueue(next);
            if (ownedChildren.TryGetValue(current, out var children)) foreach (var child in children) queue.Enqueue(child);
            if (record.TryGetProperty("commands", out var commands) && commands.ValueKind == JsonValueKind.Array)
                foreach (var command in commands.EnumerateArray()) { var target = ReadInt39(command, "target_textblock_id"); if (target > 0) queue.Enqueue(target); }
            if (record.TryGetProperty("operations", out var operations) && operations.ValueKind == JsonValueKind.Array)
                foreach (var operation in operations.EnumerateArray())
                {
                    var target = ReadInt39(operation, "target_textblock_id");
                    if (target > 0) queue.Enqueue(target);
                    if (Opcode39(operation, "random")) { var random = FirstNumber39(operation); if (random > 0) queue.Enqueue(random); }
                }
        }
        return visited.OrderBy(x => x).ToArray();
    }

    private static bool Opcode39(JsonElement element, string opcode) =>
        ReadString39(element, "opcode").Equals(opcode, StringComparison.OrdinalIgnoreCase);
    private static int FirstNumber39(JsonElement element) => ReadNumbers39(element).FirstOrDefault();
    private static IReadOnlyList<int> ReadNumbers39(JsonElement element)
    {
        if (element.TryGetProperty("numeric_arguments", out var values) && values.ValueKind == JsonValueKind.Array)
            return values.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _)).Select(x => x.GetInt32()).ToArray();
        return Array.Empty<int>();
    }
    private static int ReadInt39(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }
    private static string ReadString39(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
