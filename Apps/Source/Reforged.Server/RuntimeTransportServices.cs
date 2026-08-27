using System.Text.Json;

namespace Reforged.Server;

// MajorMUD transportation is modeled as an immediate paid room transfer.
// There is deliberately no vehicle, passenger, boarding, departure, travel,
// arrival, disconnect, capacity, or persistent transport state in this owner.
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

public sealed record RuntimePaidRoomTransferActor(
    string CharacterId,
    string RoomId,
    bool Connected);

public sealed record RuntimePaidRoomTransferRequest(
    RuntimePaidRoomTransferRequestKind Kind,
    string CharacterId,
    string? RelatedId,
    long Amount,
    string Currency,
    string Reason);

public sealed record RuntimePaidRoomTransferResult(
    bool Accepted,
    string Code,
    IReadOnlyList<RuntimePaidRoomTransferRequest> Requests);

public sealed class RuntimePaidRoomTransferService
{
    private readonly IReadOnlyList<RuntimePaidRoomTransferDefinition> _definitions;

    public RuntimePaidRoomTransferService(IEnumerable<RuntimePaidRoomTransferDefinition> definitions)
    {
        _definitions = definitions.ToArray();
        if (_definitions.Any(definition =>
            string.IsNullOrWhiteSpace(definition.TransferId)
            || string.IsNullOrWhiteSpace(definition.SourceRoomId)
            || string.IsNullOrWhiteSpace(definition.Command)
            || string.IsNullOrWhiteSpace(definition.DestinationRoomId)
            || definition.Cost < 0
            || string.IsNullOrWhiteSpace(definition.Currency)))
        {
            throw new ArgumentException("invalid-paid-room-transfer-definition", nameof(definitions));
        }
    }

    public RuntimePaidRoomTransferResult Execute(string command, RuntimePaidRoomTransferActor actor)
    {
        if (string.IsNullOrWhiteSpace(actor.CharacterId) || !actor.Connected)
            return Reject("invalid-or-disconnected-actor");

        var normalizedCommand = (command ?? string.Empty).Trim();
        var matches = _definitions
            .Where(definition => definition.SourceRoomId.Equals(actor.RoomId, StringComparison.OrdinalIgnoreCase)
                && definition.Command.Equals(normalizedCommand, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length != 1) return Reject(matches.Length == 0 ? "paid-room-transfer-not-found" : "paid-room-transfer-ambiguous");

        var definition = matches[0];
        var requests = new List<RuntimePaidRoomTransferRequest>();
        if (definition.Cost > 0)
        {
            requests.Add(new(
                RuntimePaidRoomTransferRequestKind.ChargeFare,
                actor.CharacterId,
                definition.TransferId,
                definition.Cost,
                definition.Currency,
                "paid-room-transfer-fare"));
        }
        requests.Add(new(
            RuntimePaidRoomTransferRequestKind.MoveActor,
            actor.CharacterId,
            definition.DestinationRoomId,
            0,
            definition.Currency,
            "paid-room-transfer-move"));
        requests.Add(new(
            RuntimePaidRoomTransferRequestKind.Announce,
            actor.CharacterId,
            definition.TransferId,
            0,
            definition.Currency,
            "paid-room-transfer-success"));
        return new(true, "paid-room-transfer-ready", requests);
    }

    public IReadOnlyList<string> Validate() => _definitions
        .GroupBy(definition => $"{definition.SourceRoomId}\u001f{definition.Command}", StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => "duplicate-source-command:" + group.Key.Replace('\u001f', ':'))
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public string ToJson() => JsonSerializer.Serialize(new { definitions = _definitions }, new JsonSerializerOptions { WriteIndented = true });

    private static RuntimePaidRoomTransferResult Reject(string code) =>
        new(false, code, Array.Empty<RuntimePaidRoomTransferRequest>());
}
