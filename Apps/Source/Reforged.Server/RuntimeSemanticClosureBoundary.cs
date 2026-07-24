using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeSemanticClosureSummary(
    string Status,
    int ManagedContractCount,
    int TextblockScripts,
    int TextblockOperations,
    int TextblockOpcodes,
    int RuntimeCommands,
    int NumericAbilities,
    int LearnedSkills,
    int NativeDependencyCount);

public static class RuntimeSemanticClosureBoundary
{
    public static RuntimeSemanticClosureSummary Validate(JsonElement contract)
    {
        if (Text(contract, "authority") != "Librarium")
            throw new InvalidOperationException("Semantic closure boundary is not Librarium-authoritative.");
        if (Text(contract, "managed_runtime_status") != "closed_for_current_decoded_corpus")
            throw new InvalidOperationException("Managed semantic corpus is not closed.");
        if (Int(contract, "managed_contract_count") != 10)
            throw new InvalidOperationException("Semantic closure must cover all ten managed contracts.");
        if (!contract.TryGetProperty("managed_contract_ids", out var ids) || ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() != 10)
            throw new InvalidOperationException("Managed semantic contract list is incomplete.");
        if (!contract.TryGetProperty("counts", out var counts) || counts.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Semantic closure counts are missing.");
        Require(counts, "textblock_scripts", 182);
        Require(counts, "textblock_operations", 2616);
        Require(counts, "textblock_opcodes", 25);
        Require(counts, "runtime_commands", 77);
        Require(counts, "numeric_abilities", 188);
        Require(counts, "learned_skills", 13);
        Require(counts, "output_references", 519);
        Require(counts, "unresolved_text_invocations", 389);
        if (!contract.TryGetProperty("closure_assertions", out var assertions) || assertions.ValueKind != JsonValueKind.Array || assertions.GetArrayLength() < 5)
            throw new InvalidOperationException("Semantic closure assertions are incomplete.");
        if (!contract.TryGetProperty("native_dependencies", out var native) || native.ValueKind != JsonValueKind.Array || native.GetArrayLength() != 4)
            throw new InvalidOperationException("Native dependency boundary is incomplete.");
        var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in native.EnumerateArray())
        {
            var id = Text(item, "id");
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("native.", StringComparison.Ordinal) || !dependencyIds.Add(id))
                throw new InvalidOperationException($"Invalid or duplicate native dependency '{id}'.");
            if (string.IsNullOrWhiteSpace(Text(item, "runtime_policy")))
                throw new InvalidOperationException($"Native dependency '{id}' has no runtime policy.");
        }
        return new(
            Text(contract, "managed_runtime_status"),
            Int(contract, "managed_contract_count"),
            Int(counts, "textblock_scripts"),
            Int(counts, "textblock_operations"),
            Int(counts, "textblock_opcodes"),
            Int(counts, "runtime_commands"),
            Int(counts, "numeric_abilities"),
            Int(counts, "learned_skills"),
            native.GetArrayLength());
    }

    private static void Require(JsonElement element, string property, int expected)
    {
        var actual = Int(element, property);
        if (actual != expected) throw new InvalidOperationException($"Semantic closure count '{property}' is {actual}; expected {expected}.");
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int Int(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
