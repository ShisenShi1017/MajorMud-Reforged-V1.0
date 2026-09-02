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

        Check(RuntimeClassicNativeHelpEvidenceRegistry.Validate().Count == 0, "native-help-evidence-registry-all-proven");
        var catalog = RuntimeMajorMudNativeHelpCatalog.Load(projectRoot);
        Check(catalog.TopicCount == RuntimeClassicNativeHelpEvidenceRegistry.NativeTopicCount, "generated-native-help-topic-count-131");
        Check(catalog.HasNativeMainAndCommands(), "generated-native-help-main-and-commands");
        Check(catalog.TryResolve("laws", out var laws) && laws.Contains("Silvermere has its laws", StringComparison.Ordinal),
            "native-laws-body-resolves");
        Check(catalog.TryResolve("maxp", out var set) && set.Contains("3 second period", StringComparison.Ordinal),
            "native-prefix-token-resolves");
        Check(catalog.TryResolve("topics", out var topics)
              && topics.Contains("The following help topics are currently available:", StringComparison.Ordinal)
              && topics.Contains("Help ?.", StringComparison.Ordinal),
            "native-help-topics-renders");
        Check(catalog.TryResolve("top", out var topPrefix) && topPrefix == topics,
            "topics-dedicated-branch-honors-native-prefix");
        Check(catalog.TryResolve("fol", out var shortFollow) && catalog.TryResolve("follow", out var fullFollow)
              && shortFollow == fullFollow && shortFollow.Length > 0,
            "native-first-match-word-prefix-resolver");
        Check(RuntimeMajorMudNativeHelpCatalog.NativeSameTo("FoL", "Follow"), "worldgroup-sameto-case-insensitive-prefix");
        Check(!RuntimeMajorMudNativeHelpCatalog.NativeSameTo("Followed", "Follow"), "worldgroup-sameto-rejects-longer-query");

        return new RuntimeClassicNativeHelpSmokeReport(failures.Count == 0, checks, failures);
    }
}
