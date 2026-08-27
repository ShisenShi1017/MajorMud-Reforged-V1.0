using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeTransportServiceSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeTransportServiceSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var service = new RuntimePaidRoomTransferService(new[]
        {
            new RuntimePaidRoomTransferDefinition("transfer.ferry-test", "room-west", "ferry east", "room-east", 10, "copper")
        });
        var actor = new RuntimePaidRoomTransferActor("hero", "room-west", true);
        var transfer = service.Execute("ferry east", actor);
        Check(transfer.Accepted, "immediate-transfer-accepted");
        Check(transfer.Requests.Count(request => request.Kind == RuntimePaidRoomTransferRequestKind.ChargeFare) == 1, "fare-request-once");
        Check(transfer.Requests.Any(request => request.Kind == RuntimePaidRoomTransferRequestKind.MoveActor && request.RelatedId == "room-east"), "immediate-move-request");
        Check(!service.Execute("ferry east", actor with { RoomId = "room-other" }).Accepted, "source-room-required");
        Check(!service.Execute("unknown", actor).Accepted, "recognized-command-required");
        Check(service.Validate().Count == 0, "no-persistent-trip-state");
        return new(failures.Count == 0, checks, failures);

        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }
    }
}
