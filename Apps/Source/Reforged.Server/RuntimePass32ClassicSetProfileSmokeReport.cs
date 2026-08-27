using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass32ClassicSetProfileSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass32ClassicSetProfileSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { checks.Add(id); if (!condition) failures.Add(id); }

        var help = RuntimeMajorMudNativeHelpCatalog.Load(projectRoot);
        Check(help.TryResolve("set", out var setBody)
            && setBody.Contains("Set Messages", StringComparison.Ordinal)
            && setBody.Contains("Set Follow", StringComparison.Ordinal)
            && setBody.Contains("Set Entrances", StringComparison.Ordinal)
            && setBody.Contains("Set Speech", StringComparison.Ordinal),
            "native-set-profile-options-present");

        Check(help.TryResolve("messages", out var messagesBody)
            && messagesBody.Contains("Low message setting will only display high priority messages", StringComparison.OrdinalIgnoreCase)
            && messagesBody.Contains("Medium message setting will display high and medium", StringComparison.OrdinalIgnoreCase)
            && messagesBody.Contains("High message setting", StringComparison.OrdinalIgnoreCase),
            "native-set-messages-thresholds-present");

        Check(help.TryResolve("gossip", out var gossipBody)
            && gossipBody.Contains("SET GOSSIP OFF", StringComparison.Ordinal)
            && gossipBody.Contains("SET GOSSIP ON", StringComparison.Ordinal),
            "native-gossip-toggle-explicit-forms-present");
        Check(help.TryResolve("auction", out var auctionBody)
            && auctionBody.Contains("SET AUCTION OFF", StringComparison.Ordinal)
            && auctionBody.Contains("SET AUCTION ON", StringComparison.Ordinal),
            "native-auction-toggle-explicit-forms-present");

        var setMessages = RuntimeCommand.Parse("set messages high");
        Check(setMessages.Name.Equals("set", StringComparison.OrdinalIgnoreCase)
            && setMessages.ArgumentText.Equals("messages high", StringComparison.OrdinalIgnoreCase),
            "set-messages-command-route-present");
        var setEntrances = RuntimeCommand.Parse("set entrances off");
        Check(setEntrances.Name.Equals("set", StringComparison.OrdinalIgnoreCase)
            && setEntrances.ArgumentText.Equals("entrances off", StringComparison.OrdinalIgnoreCase),
            "set-entrances-command-route-present");
        var setSpeech = RuntimeCommand.Parse("set speech");
        Check(setSpeech.Name.Equals("set", StringComparison.OrdinalIgnoreCase)
            && setSpeech.ArgumentText.Equals("speech", StringComparison.OrdinalIgnoreCase),
            "bare-set-speech-route-present");
        var setFollow = RuntimeCommand.Parse("set follow");
        Check(setFollow.Name.Equals("set", StringComparison.OrdinalIgnoreCase)
            && setFollow.ArgumentText.Equals("follow", StringComparison.OrdinalIgnoreCase),
            "bare-set-follow-route-present");
        Check(RuntimeCommand.Parse("gossip").Name.Equals("gossip", StringComparison.OrdinalIgnoreCase),
            "bare-gossip-route-present");
        Check(RuntimeCommand.Parse("auction").Name.Equals("auction", StringComparison.OrdinalIgnoreCase),
            "bare-auction-route-present");

        // The retained records conflict on RECEIVE polarity and are ambiguous enough on
        // TELEPATH polarity that Pass 32 must not claim those explicit ON/OFF meanings.
        Check(setBody.Contains("Set Receive", StringComparison.Ordinal)
            && setBody.Contains("Set Telepath", StringComparison.Ordinal),
            "receive-telepath-evidence-boundary-retained");

        return new(failures.Count == 0, checks, failures);
    }
}
