using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeMegaMudConnectionSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static async Task<RuntimeMegaMudConnectionSmokeReport> RunAsync()
    {
        var checks = new List<string>();

        // CodeUpdate0345: parser-visible communication lines are recovered from the retained
        // original DLL and independently observed in imported MegaMUD runtime logs. Keep these
        // exact because MegaMUD/Borin both consume the terminal stream.
        if (RuntimeClassicCommunicationRenderingAuthority.TelepathReceived("Shisenshi", "@join") != "Shisenshi telepaths: @join" ||
            RuntimeClassicCommunicationRenderingAuthority.DirectToTarget("Azriel", "{ok}") != "Azriel says (to you) \"{ok}\"" ||
            RuntimeClassicCommunicationRenderingAuthority.Gossip("Gryndak", "Good hunting.") != "Gryndak gossips: Good hunting." ||
            RuntimeCommand.Parse("gos hello").Name != "gossip" ||
            RuntimeCommand.Parse("auc selling").Name != "auction")
            throw new InvalidOperationException("Recovered MegaMUD communication rendering/alias contract regressed.");
        checks.Add("Recovered MegaMUD-observed telepath, directed-message, gossip rendering and GOS/AUC aliases remain exact.");

        // CodeUpdate0347: legacy clients must see the native TALK resolver/response surface.
        // The live Classic dispatcher owns the FAST-vs-SLOW decision; this smoke locks the
        // defaults and parser-visible responses that MegaMUD consumes on the ordinary stream.
        if (!RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultTalkFast ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultTalkResponseBrief ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.RecordTalkModeOffset != 0x45 ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.TalkResponseBriefMask != 0x0100 ||
            RuntimeClassicCommunicationRenderingAuthority.CommandNotRecognized != "Command not recognized." ||
            RuntimeClassicCommunicationRenderingAuthority.TalkBriefResponse != "Well Spoken...")
            throw new InvalidOperationException("Recovered MajorMUD TALK/response compatibility contract regressed.");
        checks.Add("Native TALK defaults/persistence identities and SLOW/BRIEF parser-visible responses remain exact for legacy clients.");

        // CodeUpdate0349: SET MESSAGES is recipient-side room-output filtering. Legacy clients
        // do not negotiate a separate feature; they simply receive the MajorMUD-visible stream
        // after the native High/Medium/Low threshold has been applied for that character.
        if (RuntimeClassicCommunicationPersistenceLimitsAuthority.NativeMessagesThreshold("high") != 0 ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.NativeMessagesThreshold("medium") != 10 ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.NativeMessagesThreshold("low") != 20 ||
            !RuntimeClassicCommunicationPersistenceLimitsAuthority.ShouldDeliverRoomMessage("low", RuntimeClassicRoomMessagePriority.High) ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.ShouldDeliverRoomMessage("low", RuntimeClassicRoomMessagePriority.Medium) ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.ShouldDeliverRoomMessage("low", RuntimeClassicRoomMessagePriority.Low) ||
            !RuntimeClassicCommunicationPersistenceLimitsAuthority.ShouldDeliverRoomMessage("medium", RuntimeClassicRoomMessagePriority.Medium) ||
            RuntimeClassicCommunicationPersistenceLimitsAuthority.ShouldDeliverRoomMessage("medium", RuntimeClassicRoomMessagePriority.Low) ||
            !RuntimeClassicCommunicationPersistenceLimitsAuthority.ShouldDeliverRoomMessage("high", RuntimeClassicRoomMessagePriority.Low))
            throw new InvalidOperationException("Recovered MajorMUD SET MESSAGES threshold contract regressed.");
        checks.Add("SET MESSAGES High/Medium/Low recipient filtering remains native-compatible on the ordinary MegaMUD stream.");

        // CodeUpdate0350: MegaMUD's retained executable inventory independently contains the
        // native REST/MEDITATE success, sickness and observer tokens. Keep the server-visible
        // strings exact so recovery automation consumes the same ordinary terminal stream.
        if (RuntimeClassicRegenerationCommandAuthority.RestTooSickMessage != "You are too sick to rest!" ||
            RuntimeClassicRegenerationCommandAuthority.MeditateTooSickMessage != "You are too sick to meditate!" ||
            RuntimeClassicRegenerationCommandAuthority.MeditationNotHelpfulMessage != "Meditation will not help at this time." ||
            RuntimeClassicRegenerationCommandAuthority.RestSuccessMessage != "You are now resting." ||
            RuntimeClassicRegenerationCommandAuthority.MeditateSuccessMessage != "You are now meditating." ||
            RuntimeClassicRegenerationCommandAuthority.ObserverMessage("Shisenshi", "rest") != "Shisenshi stops to rest." ||
            RuntimeClassicRegenerationCommandAuthority.ObserverMessage("Shisenshi", "meditate") != "Shisenshi kneels to meditate.")
            throw new InvalidOperationException("Recovered MajorMUD REST/MEDITATE terminal compatibility contract regressed.");
        checks.Add("Native REST/MEDITATE terminal strings remain exact for MegaMUD recovery automation.");

        // CodeUpdate0351: MegaMUD issues stock BREAK on the ordinary terminal stream. The
        // original DLL and MegaMUD executable both expose the exact player/off and observer
        // text, so keep that presentation stable without a private-client compatibility path.
        if (RuntimeClassicBreakAutocombatAuthority.PlayerMessage != "*Combat Off*" ||
            RuntimeClassicBreakAutocombatAuthority.ObserverMessage("Shisenshi") != "Shisenshi breaks off combat." ||
            RuntimeClassicBreakAutocombatAuthority.ObserverPriority != RuntimeClassicRoomMessagePriority.High ||
            RuntimeClassicBreakAutocombatAuthority.Validate().Count != 0)
            throw new InvalidOperationException("Recovered MajorMUD BREAK terminal compatibility contract regressed.");
        checks.Add("Native BREAK player/observer presentation remains exact for MegaMUD combat automation.");

        // CodeUpdate0352: BREAK is character-only. Stopping one participant's native
        // autocombat must not clear the opponent's independent autocombat ownership.
        var pvpBreakProbe = new RuntimeLivePvpEncounter("probe", "Classic", "room.1",
            "attacker", "defender", ClassicAttackType.NormalWeapon, ClassicAttackType.NormalWeapon,
            true, true, 0, DateTimeOffset.UtcNow);
        var attackerStopped = RuntimeLiveGameplayFacade.SetLivePvpAutocombat(pvpBreakProbe, "attacker", false);
        if (!RuntimeClassicBreakAutocombatAuthority.CharacterOnly ||
            RuntimeClassicBreakAutocombatAuthority.NativeKillAgainstRva != 0x4C914 ||
            attackerStopped.AttackerAutocombatActive || !attackerStopped.DefenderAutocombatActive)
            throw new InvalidOperationException("Recovered MajorMUD character-only BREAK ownership contract regressed.");
        checks.Add("Native BREAK remains character-only while the opponent can continue attacking.");

        // CodeUpdate0353: stock GIVE remains ordinary terminal traffic. The native DLL and
        // MegaMUD executable agree on the capacity and success tokens, so recipient admission
        // must not be hidden behind a Reforged-only client-data signal.
        if (RuntimeClassicGiveAuthority.NativeCommandRva != 0x67744 ||
            RuntimeClassicGiveAuthority.NativeAddItemRva != 0x1B111 ||
            RuntimeClassicGiveAuthority.NativeInventoryRootSlotCount != 100 ||
            RuntimeClassicGiveAuthority.RecipientCapacityMessage != "You cannot carry that much!" ||
            RuntimeClassicGiveAuthority.RenderReceiveOff("Target") != "Target refuses your offer." ||
            RuntimeClassicGiveAuthority.RenderCannotAccept("Target") != "Target cannot accept your offer." ||
            RuntimeClassicGiveAuthority.RenderSender("a sword", "Target") != "You just gave a sword to Target." ||
            RuntimeClassicGiveAuthority.RenderRecipient("Source", "a sword") != "Source just gave you a sword." ||
            RuntimeClassicGiveAuthority.RenderObserver("Source", "Target") != "Source just gave Target something.")
            throw new InvalidOperationException("Recovered MajorMUD GIVE terminal compatibility contract regressed.");
        checks.Add("Native GIVE capacity/restriction/success presentation remains exact on the ordinary MegaMUD stream.");

        // CodeUpdate0354: BUY/SELL price arithmetic is server-authoritative. MegaMUD sends the
        // ordinary stock commands and must observe the same transaction result; no private
        // client-side price formula or Reforged-only compatibility channel is required.
        if (RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 0, 0) != 110 ||
            RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 100, 50) != 200 ||
            RuntimeClassicEconomyPricing.QuoteBuyCopper(100_001, 0, 50) != 100_000 ||
            RuntimeClassicEconomyPricing.QuoteSellCopper(100, 0) != 25 ||
            RuntimeClassicEconomyPricing.QuoteSellCopper(100, 50) != 50 ||
            RuntimeClassicEconomyPricing.QuoteSellCopper(100, 100) != 75)
            throw new InvalidOperationException("Recovered MajorMUD BUY/SELL Charm pricing contract regressed.");
        checks.Add("Native BUY/SELL Charm pricing remains server-authoritative on the ordinary MegaMUD command stream.");

        // CodeUpdate0355: physical combat arithmetic remains entirely server-side.
        // MegaMUD consumes the ordinary MajorMUD result text and does not need a private formula channel.
        if (RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(20, 120, 8, 3, true) != 85 ||
            RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(20, 120, 8, 3, false) != 63 ||
            ClassicCombatResolver.CalculateDodgeChance(ClassicAttackType.Backstab, 80, 10) != 2)
            throw new InvalidOperationException("Recovered native Backstab/physical-combat contract regressed.");
        checks.Add("Native Backstab and physical-combat math remains server-authoritative on the ordinary MegaMUD stream.");

        // CodeUpdate0356: raw player Dodge is also entirely server-side. The original DLL
        // constructs it before _calculate_attack; MegaMUD only consumes the resulting stock combat text.
        if (RuntimeClassicCharacterFormulaCalculator.CalculateRawDodge(20, 80, 60, 5, 32) != 28 ||
            RuntimeClassicCharacterFormulaCalculator.CalculateRawDodge(20, 80, 60, 5, 33) != 21 ||
            RuntimeClassicCharacterFormulaCalculator.CalculateRawDodge(1, 40, 40, 0, 33) != -5)
            throw new InvalidOperationException("Recovered native raw-Dodge construction regressed.");
        checks.Add("Native raw Dodge remains server-authoritative on the ordinary MegaMUD combat stream.");

        // CodeUpdate0357: exact stock player Accuracy construction stays server-side.
        if (RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(6, 1, 50, 50, 0, 0) != 46 ||
            RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(6, 20, 80, 60, 3, 0) != 109 ||
            RuntimeClassicPhysicalFormulaCalculator.CalculateStockPlayerAccuracyBase(4, 20, 49, 49, 1, 32) != 68)
            throw new InvalidOperationException("Recovered native stock Accuracy construction regressed.");
        checks.Add("Native stock player Accuracy remains server-authoritative on the ordinary MegaMUD combat stream.");

        // CodeUpdate0358: native LVLDIFF remains a server/realm rule on the ordinary game stream.
        // MegaMUD does not own or bypass realm PvP admission.
        if (RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference != 100 ||
            RuntimeClassicConfigurationAuthority.PvpDisabledLevelDifference != -1 ||
            RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(-1) ||
            !RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(0) ||
            !RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(20, 10, 10) ||
            RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(21, 10, 10) ||
            !RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(50, 10, 10, defenderIsFiend: true))
            throw new InvalidOperationException("Recovered native LVLDIFF PvP admission contract regressed.");
        checks.Add("Native LVLDIFF PvP admission remains server-authoritative on the ordinary MegaMUD stream.");

        if (!ServerNetworkHost.IsMegaMudLaunchCommand("/go majormud") ||
            !ServerNetworkHost.IsMegaMudLaunchCommand("go majormud") ||
            !ServerNetworkHost.IsMegaMudLaunchCommand("majormud") ||
            ServerNetworkHost.IsMegaMudLaunchCommand("/go somethingelse"))
            throw new InvalidOperationException("MegaMUD direct-login launch token recognition regressed.");
        checks.Add("Recovered MegaMUD `/go majormud` auto-login launch token is recognized explicitly.");

        if (!ServerNetworkHost.TryParseMegaMudLaunchCommand("/go majormud 2", out var numericSelector) ||
            !string.Equals(numericSelector, "2", StringComparison.Ordinal) ||
            !ServerNetworkHost.TryParseMegaMudLaunchCommand("/go majormud Shisen Shi", out var namedSelector) ||
            !string.Equals(namedSelector, "Shisen Shi", StringComparison.Ordinal))
            throw new InvalidOperationException("MegaMUD character-selector launch parsing regressed.");

        var characterNames = new[] { "Alpha", "Shisen Shi", "Gamma" };
        if (ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "1") != 0 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "2") != 1 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "shisen shi") != 1 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "4") != -1 ||
            ServerNetworkHost.ResolveMegaMudCharacterSelectorIndex(characterNames, "missing") != -1)
            throw new InvalidOperationException("MegaMUD deterministic character selection regressed.");
        checks.Add("MegaMUD direct login supports deterministic 1-based character slots or exact case-insensitive character names.");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, endpoint.Port, timeout.Token);
        using var accepted = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
        await connectTask.ConfigureAwait(false);
        using var clientStream = client.GetStream();
        await using var telnet = new TelnetConnection(accepted.GetStream());

        await telnet.NegotiateAsync(timeout.Token).ConfigureAwait(false);
        var negotiation = await ReadExactAsync(clientStream, 9, timeout.Token).ConfigureAwait(false);
        RequireSequence(negotiation, new byte[]
        {
            255, 251, 3,   // IAC WILL SUPPRESS-GO-AHEAD
            255, 251, 1,   // IAC WILL ECHO
            255, 251, 201  // IAC WILL GMCP; legacy clients may reject this option
        }, "initial Telnet negotiation");
        checks.Add("Telnet negotiation keeps classic server echo/SGA and offers the Reforged-only GMCP option without requiring it.");

        // Recovered MegaMUD is a legacy Telnet client. Rejecting the unknown GMCP option must
        // keep the private Reforged client-data channel disabled while ordinary line input works.
        var lineTask = telnet.ReadLineAsync(hideInput: false, timeout.Token);
        var echoReassert = await ReadExactAsync(clientStream, 3, timeout.Token).ConfigureAwait(false);
        RequireSequence(echoReassert, new byte[] { 255, 251, 1 }, "server echo reassertion");

        await clientStream.WriteAsync(new byte[] { 255, 254, 201 }, timeout.Token).ConfigureAwait(false); // IAC DONT GMCP
        var gmcpReject = await ReadExactAsync(clientStream, 3, timeout.Token).ConfigureAwait(false);
        RequireSequence(gmcpReject, new byte[] { 255, 252, 201 }, "GMCP rejection acknowledgement");

        var commandBytes = Encoding.ASCII.GetBytes("set statline full\r\n");
        await clientStream.WriteAsync(commandBytes, timeout.Token).ConfigureAwait(false);
        var line = await lineTask.ConfigureAwait(false);
        if (!string.Equals(line, "set statline full", StringComparison.Ordinal))
            throw new InvalidOperationException($"MegaMUD line input changed: '{line ?? "<null>"}'.");
        if (telnet.ReforgedClientDataEnabled)
            throw new InvalidOperationException("Legacy MegaMUD negotiation incorrectly enabled the private Reforged client-data channel.");

        var echoed = await ReadExactAsync(clientStream, commandBytes.Length, timeout.Token).ConfigureAwait(false);
        RequireSequence(echoed, commandBytes, "classic server-side input echo");
        checks.Add("A legacy client may reject GMCP and still send normal MajorMUD commands; no private client-data token is enabled.");

        await telnet.WriteAsync("é\n", timeout.Token).ConfigureAwait(false);
        var cp437 = await ReadExactAsync(clientStream, 3, timeout.Token).ConfigureAwait(false);
        RequireSequence(cp437, new byte[] { 0x82, 13, 10 }, "CP437/CRLF application output");
        checks.Add("Application output uses CP437 bytes and CRLF network line endings.");

        listener.Stop();
        return new RuntimeMegaMudConnectionSmokeReport(true, checks);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new IOException($"Connection closed while reading {count} bytes; received {offset}.");
            offset += read;
        }
        return buffer;
    }

    private static void RequireSequence(IReadOnlyList<byte> actual, IReadOnlyList<byte> expected, string label)
    {
        if (actual.Count == expected.Count && actual.SequenceEqual(expected)) return;
        throw new InvalidOperationException(
            $"Unexpected {label}: actual={Convert.ToHexString(actual.ToArray())}; expected={Convert.ToHexString(expected.ToArray())}.");
    }
}
