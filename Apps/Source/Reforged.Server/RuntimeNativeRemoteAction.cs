using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNativeRemoteActionIntent(
    int ActionRecordId,
    int OptionalMessageId,
    int Selector,
    int ActionSlot)
{
    public string Signature => $"{ActionRecordId}.{OptionalMessageId}.{Selector}.{ActionSlot}";

    public static RuntimeNativeRemoteActionIntent Parse(string value)
    {
        var parts = (value ?? string.Empty)
            .Split(new[] { '.', ' ', ',', ':', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != WorldgroupRemoteActionDispatchRegistry.ArgumentCount ||
            parts.Any(part => !int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            throw new InvalidOperationException("remoteaction requires exactly four integer arguments");

        var values = parts.Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();
        if (values[0] <= 0)
            throw new InvalidOperationException("remoteaction action record must be positive");
        if (values[1] < 0)
            throw new InvalidOperationException("remoteaction optional message id cannot be negative");
        if (values[2] < 0)
            throw new InvalidOperationException("remoteaction selector cannot be negative");
        if (values[3] < WorldgroupRemoteActionDispatchRegistry.MinimumActionSlot ||
            values[3] > WorldgroupRemoteActionDispatchRegistry.MaximumActionSlot)
            throw new InvalidOperationException(
                $"remoteaction action slot must be {WorldgroupRemoteActionDispatchRegistry.MinimumActionSlot} through {WorldgroupRemoteActionDispatchRegistry.MaximumActionSlot}");

        return new(values[0], values[1], values[2], values[3]);
    }
}

public sealed record RuntimeNativeRemoteActionAuditReport(
    bool Passed,
    int InvocationCount,
    int DistinctSignatureCount,
    int StructurallyValidCount,
    int ExecutableMutationCount,
    int EvidenceGatedCount,
    IReadOnlyList<string> Signatures,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeNativeRemoteActionAudit
{
    public static RuntimeNativeRemoteActionAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var invocationArguments = new List<IReadOnlyList<string>>();
        var decodedPath = Path.Combine(projectRoot, "Librarium", "SourceImports", "MMExplorerSQLData", "TextblocksDecoded0142", "textblocks_decoded.json");
        using var decoded = JsonDocument.Parse(File.ReadAllText(decodedPath));
        foreach (var script in decoded.RootElement.EnumerateArray())
        {
            if (!script.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var operation in operations.EnumerateArray())
            {
                if (!JsonValueReader.ReadString(operation, "opcode", string.Empty)
                    .Equals("remoteaction", StringComparison.OrdinalIgnoreCase))
                    continue;
                var arguments = operation.TryGetProperty("arguments", out var values) && values.ValueKind == JsonValueKind.Array
                    ? values.EnumerateArray().Select(value => value.ToString()).ToArray()
                    : Array.Empty<string>();
                invocationArguments.Add(arguments);
            }
        }

        var signatures = new HashSet<string>(StringComparer.Ordinal);
        var structurallyValid = 0;
        foreach (var arguments in invocationArguments)
        {
            try
            {
                signatures.Add(RuntimeNativeRemoteActionIntent.Parse(string.Join('.', arguments)).Signature);
                structurallyValid++;
            }
            catch (InvalidOperationException ex)
            {
                failures.Add($"{string.Join(' ', arguments)}:{ex.Message}");
            }
        }

        if (invocationArguments.Count == 139) checks.Add("full-decoded-remoteaction-invocation-corpus-enumerated");
        else failures.Add($"decoded-remoteaction-invocation-count:{invocationArguments.Count}:expected:139");
        if (signatures.Count == 85) checks.Add("full-decoded-remoteaction-signature-corpus-enumerated");
        else failures.Add($"decoded-remoteaction-signature-count:{signatures.Count}:expected:85");
        if (structurallyValid == invocationArguments.Count) checks.Add("all-remoteaction-arguments-structurally-valid");
        checks.Add("native-parser-argument-and-action-slot-contract-enforced");
        var contracts=RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(projectRoot,"Data","Classic","room_interactions.json"))
            .Where(value=>JsonValueReader.ReadString(value,"interaction_kind",string.Empty).Equals("remote_action_mutation",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var executableSignatures=contracts
            .Select(value=>JsonValueReader.ReadString(value,"remote_action_signature",string.Empty))
            .Where(value=>!string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        if(signatures.SetEquals(executableSignatures))checks.Add("all-decoded-remoteactions-have-generated-mutation-contracts");
        else failures.Add("decoded-remoteaction-mutation-contract-mismatch");
        var effects=contracts.GroupBy(value=>
        {
            if(!value.TryGetProperty("command_semantics",out var semantics)||semantics.ValueKind!=JsonValueKind.Object)return string.Empty;
            return JsonValueReader.ReadString(semantics,"state_effect",string.Empty);
        }).ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.Ordinal);
        if(effects.GetValueOrDefault("toggle_paired_door_state")==62
           &&effects.GetValueOrDefault("clear_native_state_bit")==22
           &&effects.GetValueOrDefault("native_common_finalization")==1)
            checks.Add("native-action-classification-counts-exact");
        else failures.Add("native-action-classification-count-drift");

        return new(
            failures.Count == 0,
            invocationArguments.Count,
            signatures.Count,
            structurallyValid,
            signatures.Count(executableSignatures.Contains),
            signatures.Count(signature=>!executableSignatures.Contains(signature)),
            signatures.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            checks,
            failures);
    }
}
