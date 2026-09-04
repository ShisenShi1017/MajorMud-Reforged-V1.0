using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicQuestConditionState(
    int Level,
    int ClassLegacyId,
    int RaceLegacyId,
    int AlignmentValue,
    IReadOnlyDictionary<int, int> AbilityValues,
    IReadOnlySet<int> SpellIds,
    IReadOnlyDictionary<int, int> InventoryCounts,
    IReadOnlyDictionary<int, int> RoomItemCounts,
    IReadOnlySet<int> PresentNpcIds,
    long SpendableCopper);

public sealed record RuntimeClassicQuestConditionDecision(bool Passed, int? FailureTextblockId, string? Failure);
public sealed record RuntimeClassicQuestMutationRequest(string Opcode, int Sequence, RuntimeClassicNarrativeOperationOwner Owner, IReadOnlyList<int> NumericArguments, bool RequiresAtomicTransaction);
public sealed record RuntimeClassicQuestTransactionProjection(IReadOnlyList<RuntimeClassicQuestMutationRequest> Requests, bool RequiresAtomicTransaction);

/// <summary>
/// Canonical Bundle F projection for decoded quest predicates and turn-in/reward mutations.
/// It reads authoritative Bundle A-E state, but never owns or duplicates that state. Mutations are
/// emitted as ordered owner requests and are executed by RuntimeClassicNarrativeExecutionCoordinator.
/// </summary>
public static class RuntimeClassicQuestConditionAndTransactionAuthority
{
    public static RuntimeClassicQuestConditionDecision Evaluate(RuntimeClassicTextblockOperation operation, RuntimeClassicQuestConditionState state)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(state);
        var a = operation.NumericArguments;
        int N(int index) => index < a.Count ? a[index] : 0;
        int? FailureId(int index) => index < a.Count && a[index] > 0 ? a[index] : null;
        var opcode = operation.Opcode.ToLowerInvariant();
        bool passed;
        int? failureTextblockId = null;
        string failure;
        switch (opcode)
        {
            case "minlevel": passed = state.Level >= N(0); failureTextblockId = FailureId(1); failure = $"Minimum level {N(0)} is required."; break;
            case "maxlevel": passed = state.Level <= N(0); failure = $"Maximum level {N(0)} was exceeded."; break;
            case "class": passed = state.ClassLegacyId == N(0); failureTextblockId = FailureId(1); failure = $"Class {N(0)} is required."; break;
            case "race": passed = state.RaceLegacyId == N(0); failure = $"Race {N(0)} is required."; break;
            case "goodaligned": passed = state.AlignmentValue >= N(0); failureTextblockId = FailureId(1); failure = "Good alignment requirement failed."; break;
            case "evilaligned": passed = state.AlignmentValue <= N(0); failureTextblockId = FailureId(1); failure = "Evil alignment requirement failed."; break;
            case "goodability": passed = state.AlignmentValue >= N(0); failureTextblockId = FailureId(1); failure = "Good-alignment ability requirement failed."; break;
            case "checkability": passed = state.AbilityValues.TryGetValue(N(0), out var exact) && exact == N(1); failure = $"Ability {N(0)} must equal {N(1)}."; break;
            case "testability": passed = state.AbilityValues.TryGetValue(N(0), out var minimum) && minimum >= N(1); failure = $"Ability {N(0)} must be at least {N(1)}."; break;
            case "failability": passed = !state.AbilityValues.ContainsKey(N(0)); failureTextblockId = FailureId(1); failure = $"Ability {N(0)} must not already be present."; break;
            case "checkspell": passed = state.SpellIds.Contains(N(0)); failureTextblockId = FailureId(1); failure = $"Spell {N(0)} is required."; break;
            case "checkitem": passed = state.InventoryCounts.TryGetValue(N(0), out var ci) && ci > 0; failureTextblockId = FailureId(1); failure = $"Item {N(0)} is required."; break;
            case "failitem": passed = !state.InventoryCounts.TryGetValue(N(0), out var fi) || fi <= 0; failureTextblockId = FailureId(1); failure = $"Item {N(0)} must not be carried."; break;
            case "roomitem": passed = state.RoomItemCounts.TryGetValue(N(0), out var ri) && ri > 0; failureTextblockId = FailureId(1); failure = $"Room item {N(0)} is required."; break;
            case "failroomitem": passed = !state.RoomItemCounts.TryGetValue(N(0), out var fri) || fri <= 0; failureTextblockId = FailureId(1); failure = $"Room item {N(0)} must not be present."; break;
            case "needmonster": passed = state.PresentNpcIds.Contains(N(0)); failureTextblockId = FailureId(1); failure = $"NPC {N(0)} is required."; break;
            case "nomonsters": passed = state.PresentNpcIds.Count == 0; failureTextblockId = FailureId(0); failure = "No monsters may be present."; break;
            case "monsters": passed = state.PresentNpcIds.Count > 0; failure = "A monster must be present."; break;
            case "price": passed = state.SpendableCopper >= N(0); failureTextblockId = FailureId(1); failure = $"Price {N(0)} cannot be paid."; break;
            case "levelcheck": passed = true; failure = string.Empty; break;
            default: return new(false, null, $"Unsupported decoded quest predicate '{operation.Opcode}' failed closed.");
        }
        return new(passed, passed ? null : failureTextblockId, passed ? null : failure);
    }

    public static RuntimeClassicQuestTransactionProjection Project(RuntimeClassicTextblockExecutionPlan plan, RuntimeClassicNarrativeOperationDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var requests = new List<RuntimeClassicQuestMutationRequest>();
        foreach (var operation in plan.Operations.OrderBy(x => x.Sequence))
        {
            var contract = dispatcher.RequireContract(operation.Opcode);
            if (!contract.MutatesState) continue;
            requests.Add(new(operation.Opcode, operation.Sequence, contract.Owner, operation.NumericArguments, true));
        }
        return new(requests, requests.Count > 0);
    }
}

public sealed record RuntimeBundleFPhaseF5ConditionTransactionAuditReport(bool Passed, int PredicateCount, int MutationRequestCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF5ConditionTransactionAudit
{
    public static RuntimeBundleFPhaseF5ConditionTransactionAuditReport Run()
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string name) { if (value) checks.Add(name); else failures.Add(name); }
        var state = new RuntimeClassicQuestConditionState(40, 3, 5, 100, new Dictionary<int,int>{{7,2}}, new HashSet<int>{11}, new Dictionary<int,int>{{101,2}}, new Dictionary<int,int>{{202,1}}, new HashSet<int>{303}, 5000);
        RuntimeClassicTextblockOperation Op(int seq, string opcode, params int[] args) => new(1, seq, opcode, opcode, "audit", "verified", args.Select(x=>x.ToString()).ToArray(), args, null, null, null, null, null, null, null);
        var predicates = new[]
        {
            Op(1,"minlevel",40,900), Op(2,"class",3,901), Op(3,"race",5), Op(4,"goodaligned",50,902),
            Op(5,"checkability",7,2), Op(6,"testability",7,2), Op(7,"checkspell",11,903), Op(8,"checkitem",101,904),
            Op(9,"roomitem",202,905), Op(10,"needmonster",303,906), Op(11,"price",4000,907)
        };
        foreach (var p in predicates) Check(RuntimeClassicQuestConditionAndTransactionAuthority.Evaluate(p,state).Passed, $"predicate-{p.Opcode}-uses-authoritative-state");
        var failed = RuntimeClassicQuestConditionAndTransactionAuthority.Evaluate(Op(12,"checkitem",999,950), state);
        Check(!failed.Passed && failed.FailureTextblockId == 950, "predicate-failure-preserves-decoded-failure-branch");
        var unsupported = RuntimeClassicQuestConditionAndTransactionAuthority.Evaluate(Op(13,"unknownpredicate",1), state);
        Check(!unsupported.Passed, "unknown-predicate-fails-closed");

        var mutations = new[] { Op(20,"takeitem",101), Op(21,"giveitem",102), Op(22,"giveability",7,3), Op(23,"addexp",1000) };
        var plan = new RuntimeClassicTextblockExecutionPlan(2798,2798,1,"work",mutations,Array.Empty<int>(),null);
        var projection = RuntimeClassicQuestConditionAndTransactionAuthority.Project(plan,new RuntimeClassicNarrativeOperationDispatcher());
        Check(projection.RequiresAtomicTransaction && projection.Requests.Count == 4, "turn-in-and-reward-projected-as-one-atomic-plan");
        Check(projection.Requests.Select(x=>x.Sequence).SequenceEqual(new[]{20,21,22,23}), "mutation-order-preserved");
        Check(projection.Requests.All(x=>x.RequiresAtomicTransaction), "all-quest-mutations-require-transaction");
        return new(failures.Count == 0, predicates.Length + 2, projection.Requests.Count, failures.Count, checks, failures);
    }
}
