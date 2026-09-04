using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeClassicNarrativeOperationKind
{
    Condition,
    Effect,
    Output,
    Branch,
    WorldIntent,
    Unknown
}

public sealed record RuntimeClassicNarrativeOperation(
    int Line,
    int Sequence,
    string Opcode,
    RuntimeClassicNarrativeOperationKind Kind,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<long> NumericArguments,
    string Raw,
    string Status);

public sealed record RuntimeClassicNarrativeClause(
    string ClauseId,
    int SourceLine,
    IReadOnlyList<RuntimeClassicNarrativeOperation> Conditions,
    IReadOnlyList<RuntimeClassicNarrativeOperation> ExecutionSteps,
    bool StateMutating,
    string TransactionPolicy,
    string FailurePolicy);

public sealed record RuntimeClassicNarrativeRandomClause(
    int Threshold,
    int SourceLine,
    IReadOnlyList<RuntimeClassicNarrativeOperation> Operations);

public sealed record RuntimeClassicNarrativeRandomTable(
    string TextblockId,
    string SelectionModel,
    IReadOnlyList<RuntimeClassicNarrativeRandomClause> Clauses);

public sealed record RuntimeClassicNarrativeScript(
    string Id,
    string QuestId,
    string NpcId,
    string RootTextblockId,
    string TextblockId,
    int StageIndex,
    IReadOnlyList<RuntimeClassicNarrativeOperation> Operations,
    IReadOnlyList<RuntimeClassicNarrativeClause> Clauses,
    IReadOnlyDictionary<string, RuntimeClassicNarrativeRandomTable> RandomTables,
    JsonElement SourceRecord);

public static class RuntimeClassicNarrativeTypedProjection
{
    public static RuntimeClassicNarrativeScript ParseScript(JsonElement script)
    {
        var id = RequiredText(script, "id");
        var rel = RequireObject(script, "relationships", id);
        var operations = ReadArray(script, "operations")
            .Select(ParseOperation)
            .OrderBy(x => x.Line)
            .ThenBy(x => x.Sequence)
            .ToArray();
        var operationIndex = operations.ToDictionary(x => (x.Line, x.Sequence));

        var clauses = new List<RuntimeClassicNarrativeClause>();
        foreach (var clause in ReadArray(script, "compiled_clauses_0200").OrderBy(x => ReadInt(x, "source_line")))
        {
            var conditions = ReadArray(clause, "conditions")
                .Select(x => ResolveOperation(x, operationIndex, id))
                .ToArray();
            var steps = ReadArray(clause, "execution_steps")
                .Select(x => ResolveOperation(x, operationIndex, id))
                .ToArray();
            clauses.Add(new RuntimeClassicNarrativeClause(
                RequiredText(clause, "clause_id"),
                ReadInt(clause, "source_line"),
                conditions,
                steps,
                ReadBool(clause, "state_mutating"),
                ReadText(clause, "transaction_policy"),
                ReadText(clause, "failure_policy")));
        }

        if (clauses.Count == 0)
            throw new InvalidOperationException($"Narrative script '{id}' has no compiled clauses.");

        var randomTables = ParseRandomTables(script, id);

        return new RuntimeClassicNarrativeScript(
            id,
            RequiredText(rel, "quest"),
            RequiredText(rel, "npc"),
            ReadText(rel, "root_textblock"),
            ReadText(rel, "textblock"),
            ReadInt(rel, "stage_index"),
            operations,
            clauses,
            randomTables,
            script.Clone());
    }

    private static IReadOnlyDictionary<string, RuntimeClassicNarrativeRandomTable> ParseRandomTables(JsonElement script, string scriptId)
    {
        if (!script.TryGetProperty("random_tables_0205", out var tables) || tables.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, RuntimeClassicNarrativeRandomTable>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, RuntimeClassicNarrativeRandomTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in tables.EnumerateObject())
        {
            var table = property.Value;
            var clauses = ReadArray(table, "clauses")
                .Select(clause => new RuntimeClassicNarrativeRandomClause(
                    ReadInt(clause, "threshold"),
                    ReadInt(clause, "source_line"),
                    ReadArray(clause, "operations").Select(ParseOperation).OrderBy(x => x.Line).ThenBy(x => x.Sequence).ToArray()))
                .OrderBy(x => x.Threshold)
                .ToArray();
            if (clauses.Any(x => x.Threshold < 1 || x.Threshold > 100))
                throw new InvalidOperationException($"Narrative script '{scriptId}' random table '{property.Name}' has an invalid threshold.");
            if (clauses.Length == 0 || clauses[^1].Threshold != 100)
                throw new InvalidOperationException($"Narrative script '{scriptId}' random table '{property.Name}' does not cover rolls through 100.");
            result[property.Name] = new RuntimeClassicNarrativeRandomTable(
                property.Name,
                ReadText(table, "selection_model"),
                clauses);
        }
        return result;
    }

    private static RuntimeClassicNarrativeOperation ParseOperation(JsonElement op)
    {
        var opcode = RequiredText(op, "opcode");
        var declaredKind = ReadText(op, "operation_kind");
        return new RuntimeClassicNarrativeOperation(
            ReadInt(op, "line"),
            ReadInt(op, "sequence"),
            opcode,
            Classify(opcode, declaredKind),
            ReadArray(op, "arguments").Select(x => x.ToString()).ToArray(),
            ReadArray(op, "numeric_arguments").Select(ReadLongScalar).ToArray(),
            ReadText(op, "raw"),
            ReadText(op, "status"));
    }

    private static RuntimeClassicNarrativeOperation ResolveOperation(
        JsonElement reference,
        IReadOnlyDictionary<(int Line, int Sequence), RuntimeClassicNarrativeOperation> operations,
        string scriptId)
    {
        var key = (Line: ReadInt(reference, "line"), Sequence: ReadInt(reference, "sequence"));
        if (!operations.TryGetValue(key, out var operation))
            throw new InvalidOperationException($"Narrative script '{scriptId}' clause references missing operation {key.Line}:{key.Sequence}.");
        var referencedOpcode = ReadText(reference, "opcode");
        if (!string.IsNullOrWhiteSpace(referencedOpcode) && !referencedOpcode.Equals(operation.Opcode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Narrative script '{scriptId}' clause opcode mismatch at {key.Line}:{key.Sequence}.");
        return operation;
    }

    private static RuntimeClassicNarrativeOperationKind Classify(string opcode, string declaredKind)
    {
        if (declaredKind.Equals("condition", StringComparison.OrdinalIgnoreCase))
            return RuntimeClassicNarrativeOperationKind.Condition;
        if (opcode is "message") return RuntimeClassicNarrativeOperationKind.Output;
        if (opcode is "text" or "random") return RuntimeClassicNarrativeOperationKind.Branch;
        if (opcode is "cast" or "summon" or "teleport" or "remoteaction") return RuntimeClassicNarrativeOperationKind.WorldIntent;
        if (opcode is "giveability" or "addability" or "giveitem" or "takeitem" or "addexp" or "addevil" or "price")
            return RuntimeClassicNarrativeOperationKind.Effect;
        return RuntimeClassicNarrativeOperationKind.Unknown;
    }

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.Clone()).ToArray()
            : Array.Empty<JsonElement>();

    private static JsonElement RequireObject(JsonElement element, string name, string owner)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Narrative record '{owner}' has no {name} object.");
        return value;
    }

    private static string RequiredText(JsonElement element, string name)
    {
        var value = ReadText(element, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Narrative record is missing required field '{name}'.");
        return value;
    }

    private static string ReadText(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : string.Empty;

    private static int ReadInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;

    private static bool ReadBool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) && value.GetBoolean();

    private static long ReadLongScalar(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        return long.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }
}
