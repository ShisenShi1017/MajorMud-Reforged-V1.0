using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNativeHelpSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicNativeHelpSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }

        Check(RuntimeClassicNativeHelpEvidenceRegistry.Validate().Count == 0, "native-help-evidence-registry-valid");
        var catalog = RuntimeMajorMudNativeHelpCatalog.Load(projectRoot);
        Check(catalog.TopicCount == RuntimeClassicNativeHelpEvidenceRegistry.NativeTopicCount, "generated-native-help-topic-count-131");
        Check(catalog.HasNativeMainAndCommands(), "generated-native-help-main-and-commands");
        Check(catalog.TryResolve("laws", out var laws) && laws.Contains("Silvermere has its laws", StringComparison.Ordinal),
            "native-laws-body-resolves");
        Check(catalog.TryResolve("maxpages", out var set) && set.Contains("3 second period", StringComparison.Ordinal),
            "unique-native-maxpages-token-resolves");
        Check(!catalog.TryResolve("topics", out _), "help-topics-rendering-remains-open");
        Check(!catalog.TryResolve("follow", out _), "ambiguous-native-token-remains-open");

        return new RuntimeClassicNativeHelpSmokeReport(failures.Count == 0, checks, failures);
    }
}
