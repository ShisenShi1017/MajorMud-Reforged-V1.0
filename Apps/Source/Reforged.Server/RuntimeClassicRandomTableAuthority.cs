using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicRandomTableBranch(int Line, int Threshold, IReadOnlyList<RuntimeClassicTextblockOperation> Operations);
public sealed record RuntimeClassicRandomTable(int TableId, bool EmptyResult, bool NonMonotonicSourceOrder, IReadOnlyList<RuntimeClassicRandomTableBranch> Branches);

/// <summary>
/// Canonical Phase F12 random-table authority. Classic numeric rows are evaluated in decoded
/// source order: the first row whose threshold is greater than or equal to a 1..100 roll wins.
/// Source-order anomalies are preserved exactly. The decoded !!! sentinel produces an empty result.
/// Nested and self-referential random operations use the same authority and are bounded fail-closed.
/// </summary>
public sealed class RuntimeClassicRandomTableAuthority
{
    private readonly IReadOnlyDictionary<int, RuntimeClassicRandomTable> _tables;
    private readonly IRuntimeClassicDialogueRandomSource _random;

    private RuntimeClassicRandomTableAuthority(IReadOnlyDictionary<int, RuntimeClassicRandomTable> tables, IRuntimeClassicDialogueRandomSource random)
    {
        _tables = tables;
        _random = random;
    }

    public static RuntimeClassicRandomTableAuthority Load(string projectRoot, IRuntimeClassicDialogueRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var path = Path.Combine(projectRoot, "Data", "Classic", "random_table_runtime_authority.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Classic random-table catalog is required.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (Int(root, "reference_count") != 595 || Int(root, "table_count") != 136 || Int(root, "branch_count") != 1222)
            throw new InvalidDataException("Classic random-table catalog counts do not match the exhausted decode.");
        var tables = new Dictionary<int, RuntimeClassicRandomTable>();
        foreach (var row in root.GetProperty("tables").EnumerateArray())
        {
            var id = Int(row, "table_id");
            if (id <= 0 || tables.ContainsKey(id)) throw new InvalidDataException($"Invalid or duplicate random table {id}.");
            var branches = new List<RuntimeClassicRandomTableBranch>();
            foreach (var branch in row.GetProperty("branches").EnumerateArray())
            {
                var operations = branch.GetProperty("operations").EnumerateArray().Select(ParseOperation).OrderBy(x => x.Sequence).ToArray();
                branches.Add(new(Int(branch, "line"), Int(branch, "threshold"), operations));
            }
            tables[id] = new(id, Bool(row, "empty_result"), Bool(row, "non_monotonic_source_order"), branches);
        }
        return new(tables, random);
    }

    public int Count => _tables.Count;
    public RuntimeClassicRandomTable Require(int tableId) => _tables.TryGetValue(tableId, out var table) ? table : throw new KeyNotFoundException($"Decoded random table {tableId} was not found.");

    public IReadOnlyList<RuntimeClassicTextblockOperation> Expand(IReadOnlyList<RuntimeClassicTextblockOperation> operations, out int randomSelectionCount)
    {
        var count = 0;
        var expanded = ExpandInternal(operations, ref count, 0);
        randomSelectionCount = count;
        return expanded;
    }

    private IReadOnlyList<RuntimeClassicTextblockOperation> ExpandInternal(IReadOnlyList<RuntimeClassicTextblockOperation> source, ref int count, int depth)
    {
        if (depth >= 256) throw new InvalidOperationException("Decoded random-table expansion exceeded the bounded reroll limit.");
        var result = new List<RuntimeClassicTextblockOperation>();
        foreach (var operation in source.OrderBy(x => x.Sequence).ThenBy(x => x.Line))
        {
            if (!operation.Opcode.Equals("random", StringComparison.OrdinalIgnoreCase)) { result.Add(operation); continue; }
            var tableId = operation.NumericArguments.FirstOrDefault();
            if (tableId <= 0) throw new InvalidOperationException($"Random operation '{operation.Raw}' has no table id.");
            var table = Require(tableId);
            count++;
            if (table.EmptyResult) continue;
            var roll = _random.NextInclusive(1, 100);
            if (roll < 1 || roll > 100) throw new InvalidOperationException("Classic random source returned a value outside 1..100.");
            var selected = table.Branches.FirstOrDefault(x => roll <= x.Threshold);
            if (selected is null) throw new InvalidOperationException($"Random table {tableId} has no decoded result for roll {roll}.");
            result.AddRange(ExpandInternal(selected.Operations, ref count, depth + 1));
        }
        return result;
    }

    private static RuntimeClassicTextblockOperation ParseOperation(JsonElement row) => new(
        Int(row, "line"), Int(row, "sequence"), Text(row, "raw"), Text(row, "opcode").ToLowerInvariant(), Text(row, "operation_kind"), Text(row, "status"),
        Strings(row, "arguments"), Ints(row, "numeric_arguments"), NullableInt(row, "target_textblock_id"), NullableInt(row, "item_id"), NullableInt(row, "spell_id"), NullableInt(row, "ability_id"), NullableInt(row, "npc_id"), NullableInt(row, "room_number"), NullableInt(row, "map_number"));
    private static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static int Int(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    private static int? NullableInt(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    private static bool Bool(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
    private static IReadOnlyList<string> Strings(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(x => x.ToString()).ToArray() : Array.Empty<string>();
    private static IReadOnlyList<int> Ints(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _)).Select(x => x.GetInt32()).ToArray() : Array.Empty<int>();
}


public sealed class RuntimeClassicRandomizedNarrativeRuntime
{
    private readonly RuntimeClassicRandomTableAuthority _randomTables;
    private readonly RuntimeClassicNarrativeExecutionCoordinator _coordinator;
    public RuntimeClassicRandomizedNarrativeRuntime(RuntimeClassicRandomTableAuthority randomTables, RuntimeClassicNarrativeExecutionCoordinator coordinator)
    {
        _randomTables = randomTables ?? throw new ArgumentNullException(nameof(randomTables));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }
    public RuntimeClassicNarrativeExecutionResult Execute(RuntimeClassicTextblockExecutionPlan plan, RuntimeClassicNarrativeExecutionContext context, out int randomSelectionCount)
    {
        var expanded = _randomTables.Expand(plan.Operations, out randomSelectionCount);
        var presentation = expanded.Where(x => x.Opcode is "message" or "text").Select(x => x.TargetTextblockId ?? x.NumericArguments.FirstOrDefault()).Where(x => x > 0).ToArray();
        var executable = plan with { Operations = expanded, MessageTextblockIds = presentation, ContinuationTextblockId = presentation.LastOrDefault() is var last && last > 0 ? last : plan.ContinuationTextblockId };
        return _coordinator.Execute(executable, context);
    }
}

public sealed record RuntimeBundleFPhaseF12AuditReport(bool Passed, int ReferenceCount, int TableCount, int BranchCount, int EmptyResultTableCount, int SourceOrderAnomalyCount, int RecursiveTableCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF12Audit
{
    public static RuntimeBundleFPhaseF12AuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var path = Path.Combine(projectRoot, "Data", "Classic", "random_table_runtime_authority.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        var references = root.GetProperty("references").EnumerateArray().ToArray();
        var tables = root.GetProperty("tables").EnumerateArray().ToArray();
        var ids = tables.Select(x => x.GetProperty("table_id").GetInt32()).ToHashSet();
        var branches = tables.Sum(x => x.GetProperty("branches").GetArrayLength());
        var empty = tables.Count(x => x.GetProperty("empty_result").GetBoolean());
        var anomalies = tables.Count(x => x.GetProperty("non_monotonic_source_order").GetBoolean());
        foreach (var reference in references)
        {
            var target = reference.GetProperty("target_table_id").GetInt32();
            if (!ids.Contains(target)) failures.Add($"random-target-unresolved:{target}");
        }
        foreach (var table in tables)
        {
            var id = table.GetProperty("table_id").GetInt32();
            var rows = table.GetProperty("branches").EnumerateArray().ToArray();
            var isEmpty = table.GetProperty("empty_result").GetBoolean();
            if (isEmpty && rows.Length != 0) failures.Add($"empty-table-has-branches:{id}");
            if (!isEmpty && rows.Length == 0) failures.Add($"table-has-no-branches:{id}");
            if (rows.Any(x => x.GetProperty("threshold").GetInt32() < 1 || x.GetProperty("threshold").GetInt32() > 100)) failures.Add($"threshold-out-of-range:{id}");
            if (!isEmpty && rows[^1].GetProperty("threshold").GetInt32() != 100) failures.Add($"table-does-not-cover-100:{id}");
        }
        var recursive = tables.Count(table => table.GetProperty("branches").EnumerateArray().SelectMany(x => x.GetProperty("operations").EnumerateArray()).Any(op => op.GetProperty("opcode").GetString() == "random" && op.GetProperty("numeric_arguments").EnumerateArray().FirstOrDefault().GetInt32() == table.GetProperty("table_id").GetInt32()));
        Check(references.Length == 595, "all-decoded-random-references-owned");
        Check(tables.Length == 136, "all-decoded-random-tables-owned");
        Check(branches == 1222, "all-decoded-random-branches-owned");
        Check(empty == 1, "decoded-empty-result-sentinel-preserved");
        Check(anomalies == 2, "decoded-source-order-anomalies-preserved-not-normalized");
        Check(root.GetProperty("ordering_rule").GetString() == "preserve-decoded-row-order", "decoded-ordering-authoritative");
        Check(failures.Count == 0, "zero-phase-f12-disconnects");
        return new(failures.Count == 0, references.Length, tables.Length, branches, empty, anomalies, recursive, failures.Count, checks, failures.Distinct().OrderBy(x => x).ToArray());
    }
}
