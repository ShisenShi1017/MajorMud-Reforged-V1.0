using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeWorldContainerSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeWorldContainerSmokeReport Run()
    {
        var now = DateTimeOffset.UtcNow;
        var definition = new RuntimeContainerDefinition("chest-1", RuntimeContainerKind.Chest, "room-1", RuntimeContainerScope.WorldShared, 2, 100,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "weapon" }, new HashSet<string>(StringComparer.OrdinalIgnoreCase), true, "key-1", "trap-1",
            TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), RuntimeContainerPersistence.Ephemeral, new Dictionary<string, int>());
        var service = new RuntimeWorldContainerService(new[] { definition });
        var actor = new RuntimeContainerActor("char-1", null, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "key-1" }, now);
        var checks = new List<string>(); var failures = new List<string>();
        Check(!service.Inspect("chest-1", actor).State.Items.Any(), "initial-empty");
        var unlock = service.Unlock("chest-1", actor); Check(unlock.Allowed && unlock.TrapTriggerRequest == "trap-1", "unlock-and-trap-route");
        var item = new RuntimeContainerItem("item-inst-1", "sword-1", "weapon", 1, 10, "char-1");
        var request = service.RequestDeposit("chest-1", actor, item); Check(request.Allowed && request.TransferRequest is not null, "deposit-request");
        var committed = service.Commit(request.TransferRequest!); Check(committed.Allowed && committed.State.Items.Count == 1, "deposit-commit");
        var withdrawal = service.RequestWithdrawal("chest-1", actor, "item-inst-1", 1); Check(withdrawal.Allowed, "withdraw-request");
        var withdrawn = service.Commit(withdrawal.TransferRequest!); Check(withdrawn.Allowed && withdrawn.State.Items.Count == 0, "withdraw-commit");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
