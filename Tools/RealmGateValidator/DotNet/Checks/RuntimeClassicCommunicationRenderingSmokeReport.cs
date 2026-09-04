using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicCommunicationRenderingSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicCommunicationRenderingSmokeReport Run()
    {
        var checks = new List<string>();
        void Require(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("classic-communication-rendering:" + label);
            checks.Add(label);
        }

        Require(RuntimeClassicCommunicationRenderingAuthority.Say("Shisenshi", "hello") == "Shisenshi says \"hello\"", "native-say-rendering");
        Require(RuntimeClassicCommunicationRenderingAuthority.YellCurrentRoom("Shisenshi", "hello") == "From Shisenshi (Yelling): \"hello\"", "native-current-room-yell-rendering");
        Require(RuntimeClassicCommunicationRenderingAuthority.YellAdjacent("N", "hello") == "Someone yells from the south \"hello\"", "native-adjacent-yell-direction-inversion");
        Require(RuntimeClassicCommunicationRenderingAuthority.YellAdjacent("U", "hello") == "Someone yells from below \"hello\"", "native-vertical-yell-rendering");
        Require(RuntimeClassicCommunicationRenderingAuthority.TelepathReceived("Shisenshi", "@join") == "Shisenshi telepaths: @join", "native-telepath-rendering");
        Require(RuntimeClassicCommunicationRenderingAuthority.TelepathSent("Shisenshi") == "--- Telepath Sent to Shisenshi ---", "native-telepath-sender-confirmation");
        Require(RuntimeClassicCommunicationRenderingAuthority.DirectToTarget("Shisenshi", "{ok}") == "Shisenshi says (to you) \"{ok}\"", "native-directed-target-rendering");
        Require(RuntimeClassicCommunicationRenderingAuthority.DirectToRoom("Shisenshi", "Borin", "hello") == "Shisenshi says (to Borin) \"hello\"", "native-directed-room-rendering");
        Require(RuntimeClassicCommunicationRenderingAuthority.Gossip("Gryndak", "Good hunting.") == "Gryndak gossips: Good hunting.", "native-gossip-rendering");
        Require(RuntimeClassicCommunicationRenderingAuthority.Auction("Gryndak", "Selling blade") == "Gryndak auctions: Selling blade", "native-auction-rendering");
        Require(RuntimeCommand.Parse("gos hello").Name == "gossip", "legacy-gos-alias-routes-to-gossip");
        Require(RuntimeCommand.Parse("auc selling").Name == "auction", "legacy-auc-alias-routes-to-auction");
        return new RuntimeClassicCommunicationRenderingSmokeReport(true, checks);
    }
}
