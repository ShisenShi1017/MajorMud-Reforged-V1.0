using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass30BroadcastGroupSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass30BroadcastGroupSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { checks.Add(id); if (!condition) failures.Add(id); }

        Check(RuntimeClassicBroadcastGroupAuthority.ClassifySelection(0).IsSelectable, "broadcast-group-0-selectable");
        Check(RuntimeClassicBroadcastGroupAuthority.ClassifySelection(1).IsSelectable, "broadcast-group-1-selectable");
        Check(RuntimeClassicBroadcastGroupAuthority.ClassifySelection(126).IsSelectable, "broadcast-group-126-selectable");
        Check(RuntimeClassicBroadcastGroupAuthority.ClassifySelection(127).State == RuntimeClassicBroadcastGroupSelectionState.EvidenceConflict,
            "broadcast-group-127-remains-evidence-conflict");
        Check(RuntimeClassicBroadcastGroupAuthority.ClassifySelection(-1).State == RuntimeClassicBroadcastGroupSelectionState.Invalid,
            "broadcast-negative-invalid");
        Check(RuntimeClassicBroadcastGroupAuthority.ClassifySelection(128).State == RuntimeClassicBroadcastGroupSelectionState.Invalid,
            "broadcast-128-invalid");
        Check(RuntimeClassicBroadcastGroupAuthority.NormalizePersistedGroup(126) == 126, "broadcast-persisted-126-preserved");
        Check(RuntimeClassicBroadcastGroupAuthority.NormalizePersistedGroup(127) == 0, "broadcast-persisted-conflict-fails-closed-to-default");

        var br = RuntimeCommand.Parse("br hello");
        Check(br.Name.Equals("broadcast", StringComparison.OrdinalIgnoreCase) && br.ArgumentText == "hello", "br-alias-resolves-broadcast");
        Check(RuntimeCommandRuntimeRouteCatalog.Require("broadcast").Kind == RuntimeCommandRouteKind.CommunicationRuntime,
            "broadcast-routes-to-communication-runtime");
        Check(RuntimeCommand.Parse("join 126").Name.Equals("join", StringComparison.OrdinalIgnoreCase), "join-numeric-route-present");
        Check(RuntimeCommand.Parse("leave").Name.Equals("leave", StringComparison.OrdinalIgnoreCase), "leave-route-present");

        return new(failures.Count == 0, checks, failures);
    }
}
