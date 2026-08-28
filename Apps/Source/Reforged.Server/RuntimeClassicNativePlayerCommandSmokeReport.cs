using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNativePlayerCommandSmokeReport(
    bool Passed,
    int DocumentedCommandCount,
    int ExplicitAliasCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicNativePlayerCommandSmokeReport Run(string projectRoot)
    {
        var registry = RuntimeClassicNativePlayerCommandRegistry.Load(projectRoot);
        var failures = registry.Validate().ToList();
        var checks = new List<string>();

        if (failures.Count == 0)
        {
            checks.Add("63-native-help-command-identities-resolve-through-live-parser");
            checks.Add("explicit-native-aliases-bs-br-bg-resolve-to-documented-command");
            checks.Add("undocumented-prefix-and-precedence-rules-remain-unclaimed");
        }

        return new RuntimeClassicNativePlayerCommandSmokeReport(
            failures.Count == 0,
            registry.Records.Count,
            registry.Records.Sum(record => record.ExplicitAliases.Count),
            checks,
            failures);
    }
}
