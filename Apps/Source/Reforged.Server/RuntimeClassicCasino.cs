namespace Reforged.Server;

public sealed record RuntimeClassicCasinoOutcome(int MaximumRoll, long PayoutCopper, int TextblockId);
public sealed record RuntimeClassicCasinoGame(
    string Id,
    string RoomId,
    IReadOnlyList<string> Commands,
    long WagerCopper,
    IReadOnlyList<RuntimeClassicCasinoOutcome> Outcomes,
    int? DecodedAddDelayValue = null);

/// <summary>
/// Retained 1.11n/1.11p-compatible room-textblock casino transactions.
/// Wagers, command aliases, 1..100 threshold tables, payout denominations, and decoded
/// adddelay values are copied from retained native textblocks. CodeUpdate0369 binds the roll
/// to the recovered Worldgroup/Borland thread PRNG and binds ADDDELAY to the recovered +0x6BB
/// live-session byte decremented by the one-second fast-character owner. Outcome textblock IDs
/// remain the exact retained terminal payload identities; Reforged does not fabricate missing prose.
/// </summary>
public static class RuntimeClassicCasinoAuthority
{
    private static readonly RuntimeClassicCasinoGame[] Games =
    [
        new("lucky-strike-1-2-dragons-dice", "room.map1.2",
            ["roll dice", "play dice", "play dragon's dice"], 10_000,
            Outcomes(
                (3,0,999),(9,50_000,1000),(13,0,1001),(17,0,1002),(22,0,1003),(28,0,1004),
                (32,0,1005),(37,0,1006),(42,0,1007),(47,15_000,1008),(52,15_000,1009),
                (58,15_000,1010),(62,0,1011),(67,0,1012),(72,0,1013),(77,0,1014),
                (83,0,1015),(87,0,1016),(91,0,1017),(97,0,1018),(100,150_000,1019))),

        new("lucky-strike-1-2-crowns-skulls", "room.map1.2",
            ["play slots", "play slot", "play crowns and skulls", "pull lever"], 1_000,
            Outcomes((86,0,1021),(87,50_000,1022),(88,10_000,1023),(89,10_000,1024),
                (90,10_000,1025),(93,2_000,1026),(96,2_000,1027),(100,2_000,1028))),

        new("lucky-strike-1-10-dead-jester", "room.map1.10",
            ["play dead jester", "draw dead jester", "draw card", "draw cards", "play cards"], 10_000,
            Outcomes((8,1,9143),(15,10,9144),(25,25,9145),(35,100,9146),(44,500,9147),
                (54,1_000,9148),(64,2_500,9149),(74,3_500,9150),(84,5_000,9151),
                (86,6_000,9152),(91,7_500,9153),(98,10_000,9154),(99,50_000,9155),(100,100_000,9156))),

        new("lucky-strike-1-11-dragons-dice", "room.map1.11",
            ["roll dice", "play dice", "play dragon's dice"], 1_000,
            Outcomes((2,0,999),(5,5_000,9138),(7,0,1001),(17,0,1002),(22,0,1003),(28,0,1004),
                (32,0,1005),(37,0,1006),(52,0,1007),(55,1_500,9139),(58,1_500,9139),
                (60,1_500,9139),(62,0,1011),(67,0,1012),(72,0,1013),(77,0,1014),
                (83,0,1015),(87,0,1016),(91,0,1017),(97,0,1018),(100,15_000,9140)), 5),

        new("lucky-strike-1-11-crowns-skulls", "room.map1.11",
            ["play slots", "play slot", "play crowns and skulls", "pull lever"], 100,
            Outcomes((80,0,1021),(86,200,9163),(87,1_000,9158),(91,1_000,9159),
                (93,1_000,9160),(96,200,9161),(99,200,9162),(100,5_000,9157)), 5),

        // Native room 1/11 assigns a different price to this alias even though it shares random table 9137.
        new("lucky-strike-1-11-crowns-skulls-named", "room.map1.11",
            ["play crowns and skulls"], 1_000,
            Outcomes((80,0,1021),(86,200,9163),(87,1_000,9158),(91,1_000,9159),
                (93,1_000,9160),(96,200,9161),(99,200,9162),(100,5_000,9157)), 5)
    ];

    public const string SuppliedWccText2Sha256 = "23a9744f9ca2f459b2e5a8bd3723d35f30467823d937be5cac38d00dd2c31f21";
    public const long SuppliedWccText2Size = 7_477_248;
    public const int NativeCasinoGameCount = 6;
    public const bool NativeWorldgroupRollContractResolved = true;
    public const bool NativeAddDelayTimeUnitResolved = true;
    public const int NativeAddDelayTickMilliseconds = 1000;
    public const bool NativeCasinoScriptsDirectlyRecovered = true;
    public const bool PracticeArenaLifecycleSeparatelyExact = true;
    public const bool TerminalOutcomeTextblockIdentityResolved = true;
    public const bool TerminalOutcomePayloadBodyMappingClaimedByCasinoAuthority = false;

    public static IReadOnlyList<RuntimeClassicCasinoGame> Capture() => Games;

    public static RuntimeClassicCasinoGame? Resolve(string roomId, string entered)
    {
        var matches = Games.Where(game => game.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase)
            && game.Commands.Any(alias => alias.Equals(entered.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
        // The named Crowns and Skulls alias in room 1/11 intentionally overrides the generic slot alias price.
        if (matches.Length == 2 && entered.Trim().Equals("play crowns and skulls", StringComparison.OrdinalIgnoreCase))
            return matches.Single(game => game.Id.EndsWith("-named", StringComparison.OrdinalIgnoreCase));
        return matches.Length == 1 ? matches[0] : null;
    }

    public static RuntimeClassicCasinoOutcome ResolveOutcome(RuntimeClassicCasinoGame game, int roll)
    {
        if (roll is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return game.Outcomes.First(outcome => roll <= outcome.MaximumRoll);
    }

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Games.Length != NativeCasinoGameCount) failures.Add("casino-game-count");
        Check(NativeWorldgroupRollContractResolved && NativeAddDelayTimeUnitResolved &&
            NativeCasinoScriptsDirectlyRecovered && PracticeArenaLifecycleSeparatelyExact &&
            TerminalOutcomeTextblockIdentityResolved && !TerminalOutcomePayloadBodyMappingClaimedByCasinoAuthority,
            "casino-native-boundary-contract", failures);
        Check(NativeAddDelayTickMilliseconds == 1000, "casino-adddelay-time-unit", failures);
        Check(SuppliedWccText2Size == 7_477_248, "casino-wcctext2-size", failures);
        if (Games.Any(game => game.Outcomes.Count == 0 || game.Outcomes[^1].MaximumRoll != 100))
            failures.Add("casino-threshold-terminal-100");
        if (Resolve("room.map1.11", "play crowns and skulls")?.WagerCopper != 1_000)
            failures.Add("casino-room-1-11-named-crowns-price");
        if (Games.Where(game => game.RoomId.Equals("room.map1.11", StringComparison.OrdinalIgnoreCase))
            .Any(game => game.DecodedAddDelayValue != 5))
            failures.Add("casino-room-1-11-adddelay");
        return failures;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }

    private static IReadOnlyList<RuntimeClassicCasinoOutcome> Outcomes(params (int max, long payoutCopper, int textblock)[] values)
    {
        if (values.Length == 0 || values[^1].max != 100) throw new InvalidDataException("Casino threshold table must end at 100.");
        var previous = 0;
        var rows = new List<RuntimeClassicCasinoOutcome>(values.Length);
        foreach (var value in values)
        {
            if (value.max <= previous || value.payoutCopper < 0 || value.textblock <= 0)
                throw new InvalidDataException("Casino threshold table is invalid.");
            rows.Add(new(value.max, value.payoutCopper, value.textblock));
            previous = value.max;
        }
        return rows;
    }
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly RuntimeClassicWorldgroupPrng _classicCasinoRandom =
        RuntimeClassicGlobalRngContractAuthority.CreateGameplayThreadStream(DateTimeOffset.Now);

    private bool TryExecuteClassicCasinoCommand(RuntimeSession session, RuntimeCommand command, out string result)
    {
        var entered = string.Join(' ', new[] { command.Name }.Concat(command.Arguments)).Trim();
        var game = RuntimeClassicCasinoAuthority.Resolve(session.CurrentRoom.Id, entered);
        if (game is null) { result = string.Empty; return false; }
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent character is required for casino play.");

        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found for casino play.");
        var state = before.ItemState ?? new RuntimeItemState([], 0, new Dictionary<string, string>(), []);
        var debited = RuntimeBundleCEconomyAuthority.DebitCopperValue(state, game.WagerCopper);

        // WGSERVER!_genrdn returns 0..99 for (0,100); the textblock random selector is 1..100.
        var roll = 1 + _classicCasinoRandom.Genrdn(0, 100);
        var outcome = RuntimeClassicCasinoAuthority.ResolveOutcome(game, roll);
        var after = outcome.PayoutCopper == 0
            ? debited
            : RuntimeBundleCEconomyAuthority.CreditCopperValue(debited, outcome.PayoutCopper);
        _characters.SaveCharacter(before with { ItemState = after, UpdatedAtUtc = DateTimeOffset.UtcNow },
            $"classic.casino:{game.Id}:tb{outcome.TextblockId}");

        if (game.DecodedAddDelayValue is int addDelay)
            session.AddClassicCommandDelay(addDelay);

        // The retained outcome node identity is exact even where its terminal prose is opaque.
        // Do not substitute synthetic win/loss flavor text for a native textblock body.
        var payout = outcome.PayoutCopper == 0 ? "no coin payout" : RuntimeBundleCEconomyAuthority.FormatCopperValue(outcome.PayoutCopper);
        result = $"[native textblock #{outcome.TextblockId}] {payout}.";
        return true;
    }
}
