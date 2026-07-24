using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNativeDependencySummary(string Status, int DependencyCount, int ReferencedNativeOperations);

public static class RuntimeNativeDependencyQuarantine
{
    private static readonly HashSet<string> RequiredIds = new(StringComparer.Ordinal)
    {
        "native.message_payload_catalog",
        "native.textblock_payload_catalog",
        "native.remoteaction_dispatcher",
        "native.player_command_table"
    };

    public static RuntimeNativeDependencySummary Validate(JsonElement contract)
    {
        if (Text(contract, "authority") != "Librarium")
            throw new InvalidOperationException("Native dependency quarantine is not Librarium-authoritative.");
        if (Text(contract, "status") != "managed_runtime_closed_native_dependencies_quarantined")
            throw new InvalidOperationException("Native dependency quarantine status is invalid.");
        if (Int(contract, "dependency_count") != 4)
            throw new InvalidOperationException("Native dependency quarantine must contain exactly four dependencies.");
        if (!contract.TryGetProperty("dependencies", out var dependencies) || dependencies.ValueKind != JsonValueKind.Array || dependencies.GetArrayLength() != 4)
            throw new InvalidOperationException("Native dependency quarantine records are incomplete.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var references = 0;
        foreach (var dependency in dependencies.EnumerateArray())
        {
            var id = Text(dependency, "id");
            if (!RequiredIds.Contains(id) || !ids.Add(id))
                throw new InvalidOperationException($"Invalid or duplicate quarantined native dependency '{id}'.");
            if (Text(dependency, "status") != "quarantined_native_only" ||
                Text(dependency, "activation_policy") != "non_executable_until_promotion_test_passes" ||
                Text(dependency, "fallback") != "fail_closed_without_invented_semantics")
                throw new InvalidOperationException($"Native dependency '{id}' is not safely quarantined.");
            if (!dependency.TryGetProperty("required_evidence", out var evidence) || evidence.ValueKind != JsonValueKind.Array || evidence.GetArrayLength() < 5)
                throw new InvalidOperationException($"Native dependency '{id}' has incomplete promotion evidence requirements.");
            if (string.IsNullOrWhiteSpace(Text(dependency, "promotion_test")))
                throw new InvalidOperationException($"Native dependency '{id}' has no promotion test.");
            references += Int(dependency, "current_reference_count");
        }
        if (!ids.SetEquals(RequiredIds))
            throw new InvalidOperationException("Native dependency quarantine set is incomplete.");
        if (!contract.TryGetProperty("global_rules", out var rules) || rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() < 5)
            throw new InvalidOperationException("Native dependency quarantine rules are incomplete.");
        return new(Text(contract, "status"), dependencies.GetArrayLength(), references);
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int Int(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
