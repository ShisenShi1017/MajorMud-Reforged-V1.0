using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicRewardOperation(
    int TextblockId,
    int Line,
    int Sequence,
    string Opcode,
    IReadOnlyList<string> Arguments,
    string Raw,
    string Owner,
    string ConsumerContract,
    bool MutatesState,
    bool RequiresAtomicBoundary,
    string DuplicatePolicy);

/// <summary>
/// Canonical Phase F14 reward-orchestration authority. Bundle F coordinates decoded reward
/// plans but delegates every mutation to the pre-existing Bundle A-E owner binding. It never
/// implements character, combat, inventory, world, or NPC mutation logic itself.
/// </summary>
public sealed class RuntimeClassicRewardExecutionAuthority
{
    private readonly IReadOnlyDictionary<(int TextblockId, int Line, int Sequence), RuntimeClassicRewardOperation> _operations;
    private readonly RuntimeClassicNarrativeExecutionCoordinator _coordinator;

    private RuntimeClassicRewardExecutionAuthority(
        IReadOnlyDictionary<(int, int, int), RuntimeClassicRewardOperation> operations,
        RuntimeClassicNarrativeExecutionCoordinator coordinator)
    {
        _operations = operations;
        _coordinator = coordinator;
    }

    public int Count => _operations.Count;

    public static RuntimeClassicRewardExecutionAuthority Load(string projectRoot, RuntimeClassicNarrativeExecutionCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        var path = Path.Combine(projectRoot, "Data", "Classic", "reward_execution_authority.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Classic reward execution catalog is required.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (Text(root, "schema") != "classic-reward-execution-authority.v1") throw new InvalidDataException("Unsupported reward authority schema.");
        if (Text(root, "coordinator_owner") != "Bundle F" || !root.GetProperty("fail_closed").GetBoolean()) throw new InvalidDataException("Reward execution ownership is invalid.");

        var rows = new Dictionary<(int, int, int), RuntimeClassicRewardOperation>();
        foreach (var item in root.GetProperty("records").EnumerateArray())
        {
            var row = new RuntimeClassicRewardOperation(
                Int(item, "textblock_id"), Int(item, "line"), Int(item, "sequence"), Text(item, "opcode").ToLowerInvariant(),
                item.GetProperty("arguments").EnumerateArray().Select(x => x.ToString()).ToArray(),
                Text(item, "raw"), Text(item, "owner"), Text(item, "consumer_contract"),
                item.GetProperty("mutates_state").GetBoolean(), item.GetProperty("requires_atomic_boundary").GetBoolean(), Text(item, "duplicate_policy"));
            if (row.TextblockId <= 0 || string.IsNullOrWhiteSpace(row.Opcode) || string.IsNullOrWhiteSpace(row.Owner) || string.IsNullOrWhiteSpace(row.ConsumerContract))
                throw new InvalidDataException("Malformed decoded reward operation.");
            if (row.MutatesState && !row.RequiresAtomicBoundary) throw new InvalidDataException("State-mutating reward operation lacks atomic boundary.");
            if (!rows.TryAdd((row.TextblockId, row.Line, row.Sequence), row)) throw new InvalidDataException("Duplicate reward operation identity.");
        }
        if (rows.Count != Int(root, "reward_operation_count")) throw new InvalidDataException("Reward operation count mismatch.");
        return new(rows, coordinator);
    }

    public IReadOnlyList<RuntimeClassicRewardOperation> ResolvePlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        var result = new List<RuntimeClassicRewardOperation>();
        foreach (var op in plan.Operations.OrderBy(x => x.Sequence).ThenBy(x => x.Line))
        {
            if (_operations.TryGetValue((plan.MatchedTextblockId, op.Line, op.Sequence), out var reward)) result.Add(reward);
        }
        return result;
    }

    public RuntimeClassicNarrativeExecutionResult Execute(RuntimeClassicTextblockExecutionPlan plan, RuntimeClassicNarrativeExecutionContext context)
    {
        var rewards = ResolvePlan(plan);
        if (rewards.Count == 0) throw new InvalidOperationException($"Textblock {plan.MatchedTextblockId} has no decoded reward operations.");
        if (rewards.Any(x => x.MutatesState) && rewards.Any(x => !x.RequiresAtomicBoundary))
            throw new InvalidOperationException("Decoded reward plan contains a mutation outside the transaction boundary.");
        return _coordinator.Execute(plan, context);
    }

    private static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static int Int(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
}

public sealed record RuntimeBundleFPhaseF14AuditReport(
    bool Passed,
    int RewardOperationCount,
    int RewardOpcodeCount,
    int OwnerCount,
    int MutatingOperationCount,
    int DisconnectCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF14Audit
{
    private static readonly IReadOnlyDictionary<string, string> RequiredOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["giveability"] = "Bundle A", ["addability"] = "Bundle A", ["removeability"] = "Bundle A", ["addexp"] = "Bundle A", ["addevil"] = "Bundle A", ["goodability"] = "Bundle A",
        ["cast"] = "Bundle B",
        ["takeitem"] = "Bundle C", ["giveitem"] = "Bundle C", ["clearitem"] = "Bundle C", ["givecoins"] = "Bundle C",
        ["teleport"] = "Bundle D",
        ["summon"] = "Bundle E", ["remoteaction"] = "Bundle E",
        ["message"] = "Bundle F", ["text"] = "Bundle F"
    };

    public static RuntimeBundleFPhaseF14AuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var path = Path.Combine(projectRoot, "Data", "Classic", "reward_execution_authority.json");
        if (!File.Exists(path)) return new(false, 0, 0, 0, 0, 1, checks, ["reward-execution-authority-missing"]);
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        var records = root.GetProperty("records").EnumerateArray().ToArray();
        var keys = new HashSet<string>();
        foreach (var row in records)
        {
            var opcode = row.GetProperty("opcode").GetString() ?? string.Empty;
            var owner = row.GetProperty("owner").GetString() ?? string.Empty;
            var key = $"{row.GetProperty("textblock_id").GetInt32()}:{row.GetProperty("line").GetInt32()}:{row.GetProperty("sequence").GetInt32()}";
            if (!keys.Add(key)) failures.Add($"duplicate-reward-operation:{key}");
            if (!RequiredOwners.TryGetValue(opcode, out var expected)) failures.Add($"unowned-reward-opcode:{opcode}");
            else if (!string.Equals(owner, expected, StringComparison.OrdinalIgnoreCase)) failures.Add($"wrong-owner:{opcode}:{owner}");
            if (row.GetProperty("mutates_state").GetBoolean() && !row.GetProperty("requires_atomic_boundary").GetBoolean()) failures.Add($"non-atomic-mutation:{key}");
            if (string.IsNullOrWhiteSpace(row.GetProperty("duplicate_policy").GetString())) failures.Add($"missing-duplicate-policy:{key}");
            if (string.IsNullOrWhiteSpace(row.GetProperty("consumer_contract").GetString())) failures.Add($"missing-consumer-contract:{key}");
        }
        var opcodes = records.Select(x => x.GetProperty("opcode").GetString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var opcode in RequiredOwners.Keys) if (!opcodes.Contains(opcode)) failures.Add($"missing-reward-family:{opcode}");
        Check(records.Length == root.GetProperty("reward_operation_count").GetInt32(), "reward-count-matches-authority");
        Check(opcodes.SetEquals(RequiredOwners.Keys), "all-reward-opcodes-exhausted");
        Check(records.Where(x => x.GetProperty("mutates_state").GetBoolean()).All(x => x.GetProperty("requires_atomic_boundary").GetBoolean()), "all-reward-mutations-atomic");
        Check(root.GetProperty("coordinator_owner").GetString() == "Bundle F", "bundle-f-coordinates-without-owning-subsystem-state");
        Check(root.GetProperty("fail_closed").GetBoolean(), "unresolved-rewards-fail-closed");
        Check(failures.Count == 0, "zero-phase-f14-disconnects");
        return new(failures.Count == 0, records.Length, opcodes.Count, records.Select(x => x.GetProperty("owner").GetString()).Distinct(StringComparer.OrdinalIgnoreCase).Count(), records.Count(x => x.GetProperty("mutates_state").GetBoolean()), failures.Count, checks, failures.Distinct().OrderBy(x => x).ToArray());
    }
}
