using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeSemanticDomainExport(
    string Domain,
    IReadOnlySet<string> Commands,
    IReadOnlySet<string> Opcodes,
    int PersistentCommandCount,
    int RuntimeMutationCommandCount);

public sealed class RuntimeSemanticDownstreamIntegration
{
    private readonly IReadOnlyDictionary<string, RuntimeSemanticDomainExport> _domains;

    private RuntimeSemanticDownstreamIntegration(IReadOnlyDictionary<string, RuntimeSemanticDomainExport> domains)
    {
        _domains = domains;
    }

    public IReadOnlyCollection<RuntimeSemanticDomainExport> Domains => _domains.Values.ToArray();

    public RuntimeSemanticDomainExport GetDomain(string domain) =>
        _domains.TryGetValue(domain, out var export)
            ? export
            : throw new KeyNotFoundException($"Unknown semantic integration domain '{domain}'.");

    public static RuntimeSemanticDownstreamIntegration Validate(JsonElement contract)
    {
        if (Text(contract, "authority") != "Librarium")
            throw new InvalidOperationException("Downstream semantic integration is not Librarium-authoritative.");
        if (Int(contract, "domain_count") != 7 || Int(contract, "command_count") != 77 || Int(contract, "opcode_count") != 25)
            throw new InvalidOperationException("Downstream semantic integration counts are invalid.");
        if (!contract.TryGetProperty("domains", out var domains) || domains.ValueKind != JsonValueKind.Array || domains.GetArrayLength() != 7)
            throw new InvalidOperationException("Downstream semantic integration domains are incomplete.");

        var byDomain = new Dictionary<string, RuntimeSemanticDomainExport>(StringComparer.OrdinalIgnoreCase);
        var commandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var opcodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var domain in domains.EnumerateArray())
        {
            var id = Text(domain, "domain");
            if (string.IsNullOrWhiteSpace(id) || !byDomain.TryAdd(id, new RuntimeSemanticDomainExport(
                    id,
                    ReadIds(domain, "commands", commandIds, "command"),
                    ReadIds(domain, "opcodes", opcodeIds, "opcode"),
                    Int(domain, "persistent_command_count"),
                    Int(domain, "runtime_mutation_command_count"))))
                throw new InvalidOperationException($"Invalid or duplicate integration domain '{id}'.");
            if (string.IsNullOrWhiteSpace(Text(domain, "integration_policy")))
                throw new InvalidOperationException($"Integration domain '{id}' has no policy.");
        }
        if (commandIds.Count != 77 || opcodeIds.Count != 25)
            throw new InvalidOperationException("Downstream integration does not cover every managed command and opcode exactly once.");
        if (Text(contract, "native_boundary") != "native-only payload, remoteaction, and original command-table dependencies remain opaque and non-executable")
            throw new InvalidOperationException("Native dependency boundary was weakened.");
        return new RuntimeSemanticDownstreamIntegration(byDomain);
    }

    private static IReadOnlySet<string> ReadIds(JsonElement element, string property, HashSet<string> global, string kind)
    {
        if (!element.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Integration {kind} list is missing.");
        var local = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values.EnumerateArray())
        {
            var id = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !local.Add(id) || !global.Add(id))
                throw new InvalidOperationException($"Invalid or duplicate integration {kind} '{id}'.");
        }
        return local;
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int Int(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
