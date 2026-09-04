using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNarrativeSemanticContract(string Opcode, int OperationCount, IReadOnlyList<int> ArgumentCounts, IReadOnlyList<string> OperandRoles, string Evidence);
public sealed record RuntimeClassicNarrativeSemanticOperand(string Role, string RawValue, int? NumericValue);
public sealed record RuntimeClassicNarrativeSemanticRequest(string Opcode, RuntimeClassicNarrativeOperationOwner Owner, IReadOnlyList<RuntimeClassicNarrativeSemanticOperand> Operands, RuntimeClassicTextblockOperation Source);

/// <summary>Canonical decoded operand-shape and role projection. It translates raw decoded operations into typed owner requests without owning Bundle A-E state.</summary>
public sealed class RuntimeClassicNarrativeSemanticAuthority
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicNarrativeSemanticContract> _contracts;
    private readonly RuntimeClassicNarrativeOperationDispatcher _dispatcher = new();
    private RuntimeClassicNarrativeSemanticAuthority(IReadOnlyDictionary<string, RuntimeClassicNarrativeSemanticContract> contracts) => _contracts = contracts;
    public int Count => _contracts.Count;

    public static RuntimeClassicNarrativeSemanticAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "narrative_opcode_semantics.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative narrative opcode semantics are required.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var rows = new Dictionary<string, RuntimeClassicNarrativeSemanticContract>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var opcode = row.GetProperty("opcode").GetString() ?? string.Empty;
            var contract = new RuntimeClassicNarrativeSemanticContract(opcode, row.GetProperty("operation_count").GetInt32(),
                row.GetProperty("argument_counts").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
                row.GetProperty("operand_roles").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray(),
                row.GetProperty("evidence").GetString() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(opcode) || !rows.TryAdd(opcode, contract)) throw new InvalidDataException($"Duplicate or empty narrative semantic opcode '{opcode}'.");
        }
        return new(rows);
    }

    public RuntimeClassicNarrativeSemanticRequest Project(RuntimeClassicTextblockOperation operation)
    {
        if (!_contracts.TryGetValue(operation.Opcode, out var contract)) throw new InvalidOperationException($"No decoded semantic contract exists for '{operation.Opcode}'.");
        if (!contract.ArgumentCounts.Contains(operation.Arguments.Count)) throw new InvalidOperationException($"Opcode '{operation.Opcode}' has decoded argument count {operation.Arguments.Count}, expected {string.Join(',', contract.ArgumentCounts)}.");
        var operands = operation.Arguments.Select((raw, index) => new RuntimeClassicNarrativeSemanticOperand(OperandRole(contract, index), raw,
            int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null)).ToArray();
        return new(operation.Opcode, _dispatcher.Classify(operation).Owner, operands, operation);
    }

    private static string OperandRole(RuntimeClassicNarrativeSemanticContract contract, int index) =>
        index < contract.OperandRoles.Count && !string.IsNullOrWhiteSpace(contract.OperandRoles[index])
            ? contract.OperandRoles[index]
            : $"argument_{index + 1}";
}

public sealed record RuntimeBundleFPhaseF4SemanticAuditReport(bool Passed, int OperationCount, int OpcodeCount, int ProjectedCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{ public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }); }

public static class RuntimeBundleFPhaseF4SemanticAudit
{
    public static RuntimeBundleFPhaseF4SemanticAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>(); var projected = 0;
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot); var authority = RuntimeClassicNarrativeSemanticAuthority.Load(projectRoot);
        var operations = new List<RuntimeClassicTextblockOperation>();
        foreach (var id in Enumerable.Range(1, 20000)) if (graph.TryGet(id, out var node)) operations.AddRange(node.Operations);
        foreach (var operation in operations) try { var request = authority.Project(operation); projected++; if (request.Operands.Count != operation.Arguments.Count) failures.Add($"operand-count:{operation.Opcode}:{operation.Line}"); } catch (Exception ex) { failures.Add($"{operation.Opcode}:{operation.Line}:{ex.Message}"); }
        void Check(bool value, string name) { if (value) checks.Add(name); else failures.Add(name); }
        Check(authority.Count == operations.Select(x => x.Opcode).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "semantic-contract-count-matches-decoded-vocabulary");
        Check(projected == operations.Count, "all-decoded-operations-project-to-typed-owner-requests");
        Check(authority.Project(operations.First(x => x.Opcode == "takeitem")).Owner == RuntimeClassicNarrativeOperationOwner.BundleCItemsEconomy, "takeitem-semantic-request-routes-bundle-c");
        Check(authority.Project(operations.First(x => x.Opcode == "giveability")).Operands[0].Role == "ability_id", "giveability-ability-operand-typed");
        Check(authority.Project(operations.First(x => x.Opcode == "teleport")).Operands.Select(x => x.Role).SequenceEqual(new[] { "room_number", "map_number" }), "teleport-room-map-order-preserved");
        return new(failures.Count == 0, operations.Count, authority.Count, projected, failures.Count, checks, failures.Take(100).ToArray());
    }
}
