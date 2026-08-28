using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass31NativeHelpAliasesSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass31NativeHelpAliasesSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { checks.Add(id); if (!condition) failures.Add(id); }

        var help = RuntimeMajorMudNativeHelpCatalog.Load(projectRoot);
        Check(help.HasNativeMainAndCommands(), "native-help-main-and-commands-present");
        Check(help.TryResolve("laws", out var laws) && laws.Contains("Like any respectful city, Silvermere has its laws.", StringComparison.Ordinal),
            "help-laws-direct-main-topic");
        Check(help.TryResolve("misc", out var misc) && misc.Contains("Suicide", StringComparison.Ordinal) && misc.Contains("Rest", StringComparison.Ordinal),
            "help-misc-direct-main-topic");
        Check(help.TryResolve("set", out var set) && set.Contains("command is used", StringComparison.Ordinal),
            "help-set-direct-main-topic");
        Check(help.TryResolve("help", out var helpBody) && helpBody.Contains("To get general", StringComparison.Ordinal)
            && helpBody.Contains("To get a list of topics available", StringComparison.Ordinal)
            && helpBody.Contains("HELP TOPICS", StringComparison.Ordinal), "help-help-direct-main-topic");
        Check(!help.TryResolve("topics", out _), "help-topics-rendering-remains-gated");
        Check(!help.TryResolve("follow", out _), "ambiguous-follow-resolver-remains-gated");

        return new(failures.Count == 0, checks, failures);
    }
}
