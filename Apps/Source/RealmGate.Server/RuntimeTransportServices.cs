using System.Text.Json;

namespace RealmGate.Server;

// Generic immediate-transfer primitives remain available to custom worlds, but Classic carriage
// ownership below is exact decoded room-script data rather than a synthetic vehicle model.
public enum RuntimePaidRoomTransferRequestKind
{
    ChargeFare,
    MoveActor,
    Announce
}

public sealed record RuntimePaidRoomTransferDefinition(
    string TransferId,
    string SourceRoomId,
    string Command,
    string DestinationRoomId,
    long Cost,
    string Currency);

public sealed record RuntimePaidRoomTransferActor(string CharacterId, string RoomId, bool Connected);
public sealed record RuntimePaidRoomTransferRequest(RuntimePaidRoomTransferRequestKind Kind, string CharacterId, string? RelatedId, long Amount, string Currency, string Reason);
public sealed record RuntimePaidRoomTransferResult(bool Accepted, string Code, IReadOnlyList<RuntimePaidRoomTransferRequest> Requests);

public sealed class RuntimePaidRoomTransferService
{
    private readonly IReadOnlyList<RuntimePaidRoomTransferDefinition> _definitions;
    public RuntimePaidRoomTransferService(IEnumerable<RuntimePaidRoomTransferDefinition> definitions)
    {
        _definitions = definitions.ToArray();
        if (_definitions.Any(definition => string.IsNullOrWhiteSpace(definition.TransferId)
            || string.IsNullOrWhiteSpace(definition.SourceRoomId) || string.IsNullOrWhiteSpace(definition.Command)
            || string.IsNullOrWhiteSpace(definition.DestinationRoomId) || definition.Cost < 0 || string.IsNullOrWhiteSpace(definition.Currency)))
            throw new ArgumentException("invalid-paid-room-transfer-definition", nameof(definitions));
    }

    public RuntimePaidRoomTransferResult Execute(string command, RuntimePaidRoomTransferActor actor)
    {
        if (string.IsNullOrWhiteSpace(actor.CharacterId) || !actor.Connected) return Reject("invalid-or-disconnected-actor");
        var normalized = (command ?? string.Empty).Trim();
        var matches = _definitions.Where(definition => definition.SourceRoomId.Equals(actor.RoomId, StringComparison.OrdinalIgnoreCase)
            && definition.Command.Equals(normalized, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (matches.Length != 1) return Reject(matches.Length == 0 ? "paid-room-transfer-not-found" : "paid-room-transfer-ambiguous");
        var definition = matches[0];
        var requests = new List<RuntimePaidRoomTransferRequest>();
        if (definition.Cost > 0) requests.Add(new(RuntimePaidRoomTransferRequestKind.ChargeFare, actor.CharacterId, definition.TransferId, definition.Cost, definition.Currency, "paid-room-transfer-fare"));
        requests.Add(new(RuntimePaidRoomTransferRequestKind.MoveActor, actor.CharacterId, definition.DestinationRoomId, 0, definition.Currency, "paid-room-transfer-move"));
        requests.Add(new(RuntimePaidRoomTransferRequestKind.Announce, actor.CharacterId, definition.TransferId, 0, definition.Currency, "paid-room-transfer-success"));
        return new(true, "paid-room-transfer-ready", requests);
    }

    public IReadOnlyList<string> Validate() => _definitions.GroupBy(definition => $"{definition.SourceRoomId}\u001f{definition.Command}", StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1).Select(group => "duplicate-source-command:" + group.Key.Replace('\u001f', ':')).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public string ToJson() => JsonSerializer.Serialize(new { definitions = _definitions }, new JsonSerializerOptions { WriteIndented = true });
    private static RuntimePaidRoomTransferResult Reject(string code) => new(false, code, Array.Empty<RuntimePaidRoomTransferRequest>());
}

public sealed record RuntimeClassicPaidCarriageRouteDefinition(
    string Id,
    int RootTextblockId,
    int SourceMap,
    int SourceRoom,
    string Command,
    long FareCopper,
    int InsufficientFundsMessageId,
    int DepartureMessageId,
    int RandomTableId);

/// <summary>
/// Exact paid-carriage records decoded from room textblocks 2923, 2937 and 2938.
/// PRICE is the fare/debit guard, MESSAGE is the native departure presentation, and RANDOM
/// selects the authored success/misdirection/bandit outcome. There is no persistent trip state.
/// </summary>
public static class RuntimeClassicPaidCarriageAuthority
{
    public static IReadOnlyList<RuntimeClassicPaidCarriageRouteDefinition> Routes { get; } = new[]
    {
        new RuntimeClassicPaidCarriageRouteDefinition("carriage.15.847.khazarad",2923,15,847,"buy passage to khazarad",4000,2576,2577,2926),
        new RuntimeClassicPaidCarriageRouteDefinition("carriage.15.847.rhudaur",2923,15,847,"buy passage to rhudaur",4500,2576,2578,2927),
        new RuntimeClassicPaidCarriageRouteDefinition("carriage.15.846.silvermere",2937,15,846,"buy passage to silvermere",8000,2576,2596,2926),
        // Preserve the decoded source literally: this Khazarad trigger references Rhudaur message 2578.
        new RuntimeClassicPaidCarriageRouteDefinition("carriage.15.846.khazarad",2937,15,846,"buy passage to khazarad",10000,2576,2578,2926),
        new RuntimeClassicPaidCarriageRouteDefinition("carriage.15.845.silvermere",2938,15,845,"buy passage to silvermere",4500,2576,2596,2927),
        new RuntimeClassicPaidCarriageRouteDefinition("carriage.15.845.rhudaur",2938,15,845,"buy passage to rhudaur",6500,2576,2578,2927)
    };

    public static bool TryResolve(int map, int room, string command, out RuntimeClassicPaidCarriageRouteDefinition route)
    {
        var normalized = Normalize(command);
        route = Routes.SingleOrDefault(candidate => candidate.SourceMap == map && candidate.SourceRoom == room
            && candidate.Command.Equals(normalized, StringComparison.OrdinalIgnoreCase))!;
        return route is not null;
    }

    public static bool MatchesPlan(RuntimeClassicPaidCarriageRouteDefinition route, RuntimeClassicTextblockExecutionPlan plan)
    {
        if (plan.RootTextblockId != route.RootTextblockId || !Normalize(plan.MatchedInput).Equals(route.Command, StringComparison.OrdinalIgnoreCase)
            || plan.Operations.Count != 3) return false;
        var price = plan.Operations[0]; var message = plan.Operations[1]; var random = plan.Operations[2];
        return price.Opcode.Equals("price", StringComparison.OrdinalIgnoreCase)
            && price.NumericArguments.FirstOrDefault() == route.FareCopper
            && price.NumericArguments.Skip(1).FirstOrDefault() == route.InsufficientFundsMessageId
            && message.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
            && (message.TargetTextblockId ?? message.NumericArguments.FirstOrDefault()) == route.DepartureMessageId
            && random.Opcode.Equals("random", StringComparison.OrdinalIgnoreCase)
            && random.NumericArguments.FirstOrDefault() == route.RandomTableId;
    }

    public static IReadOnlyList<string> ValidateDecodedGraph(string projectRoot)
    {
        var failures = new List<string>();
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        if (Routes.Count != 6) failures.Add($"route-count:{Routes.Count}:expected:6");
        if (Routes.Select(route => $"{route.SourceMap}/{route.SourceRoom}:{route.Command}").Distinct(StringComparer.OrdinalIgnoreCase).Count() != Routes.Count)
            failures.Add("duplicate-source-command");
        foreach (var route in Routes)
        {
            var plans = graph.ResolveRoomActionPlans(route.SourceMap, route.SourceRoom, route.Command);
            if (plans.Count != 1 || !MatchesPlan(route, plans[0])) failures.Add($"decoded-route-mismatch:{route.Id}");
        }
        var table2926 = NormalizeNewlines(graph.Require(2926).Script);
        var table2927 = NormalizeNewlines(graph.Require(2927).Script);
        const string expected2926 = "1:message 2580:teleport 215 6:summon 379:summon 379\n5:message 2580:teleport 144 6:summon 379\n9:message 2579:teleport 244 6\n13:message 2579:teleport 276 6\n100:message 2581:teleport 1249 6\n";
        const string expected2927 = "1:message 2580:teleport 61 2:summon 227:summon 223:summon 225:summon 224\n7:message 2580:teleport 63 2:summon 227:summon 227\n12:message 2579:teleport 150 2\n15:message 2579:teleport 69 2\n100:message 2581:teleport 2519 2\n";
        if (!table2926.Equals(expected2926, StringComparison.Ordinal)) failures.Add("random-table-2926-mismatch");
        if (!table2927.Equals(expected2927, StringComparison.Ordinal)) failures.Add("random-table-2927-mismatch");
        return failures;
    }

    private static string Normalize(string value) => string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string NormalizeNewlines(string value) => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
