using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNativePlayerCommandRecord(
    string Id,
    string DocumentedInput,
    IReadOnlyList<string> ExplicitAliases,
    int SourceMessageNumber,
    string ResolutionBoundary,
    string EvidenceClassification);

public sealed class RuntimeClassicNativePlayerCommandRegistry
{
    public const int ExpectedCommandCount = 63;
    public const int ExpectedExplicitAliasCount = 3;

    private RuntimeClassicNativePlayerCommandRegistry(IReadOnlyList<RuntimeClassicNativePlayerCommandRecord> records) =>
        Records = records;

    public IReadOnlyList<RuntimeClassicNativePlayerCommandRecord> Records { get; }

    public static RuntimeClassicNativePlayerCommandRegistry Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "native_player_commands.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Generated native player-command catalog is missing: {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = document.RootElement.GetProperty("records").EnumerateArray()
            .Select(record => new RuntimeClassicNativePlayerCommandRecord(
                record.GetProperty("id").GetString() ?? string.Empty,
                record.GetProperty("documented_input").GetString() ?? string.Empty,
                record.GetProperty("explicit_aliases").EnumerateArray().Select(alias => alias.GetString() ?? string.Empty).ToArray(),
                record.GetProperty("source_message_number").GetInt32(),
                record.GetProperty("resolution_boundary").GetString() ?? string.Empty,
                record.GetProperty("evidence_classification").GetString() ?? string.Empty))
            .ToArray();

        return new RuntimeClassicNativePlayerCommandRegistry(records);
    }

    public IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Records.Count != ExpectedCommandCount)
            failures.Add($"native-command-count:{Records.Count}:{ExpectedCommandCount}");
        if (Records.Select(record => record.DocumentedInput).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Records.Count)
            failures.Add("native-command-identities-not-unique");

        foreach (var record in Records)
        {
            if (!record.EvidenceClassification.Equals("proven_native_help_identity", StringComparison.Ordinal))
                failures.Add("native-command-evidence:" + record.DocumentedInput);

            var parsed = RuntimeCommand.Parse(record.DocumentedInput);
            if (parsed.ResolutionError.Length != 0 || !RuntimeCommand.IsDefaultRegistration(parsed.Name))
                failures.Add($"native-command-unresolved:{record.DocumentedInput}:{parsed.Name}:{parsed.ResolutionError}");

            foreach (var alias in record.ExplicitAliases)
            {
                var aliasResult = RuntimeCommand.Parse(alias);
                if (aliasResult.ResolutionError.Length != 0 || !aliasResult.Name.Equals(parsed.Name, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"native-command-alias:{record.DocumentedInput}:{alias}:{aliasResult.Name}:{aliasResult.ResolutionError}");
            }
        }

        var aliasCount = Records.Sum(record => record.ExplicitAliases.Count);
        if (aliasCount != ExpectedExplicitAliasCount)
            failures.Add($"native-command-alias-count:{aliasCount}:{ExpectedExplicitAliasCount}");

        return failures;
    }
}
