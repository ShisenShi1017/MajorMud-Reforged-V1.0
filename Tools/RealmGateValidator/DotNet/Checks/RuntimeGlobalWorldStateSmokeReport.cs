using System.Text.Json;
namespace RealmGate.Server;
public sealed record RuntimeGlobalWorldStateSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeGlobalWorldStateSmokeReport Run()
    {
        var checks = new List<string>(); var failures = new List<string>();
        var definitions = new[]
        {
            new RuntimeWorldStateDefinition("module.locked", RuntimeWorldStateScope.Module, "startup-shutdown", RuntimeWorldStateValueKind.Boolean, "false", RuntimeWorldStateResetPolicy.Never, true, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "administrative-policy" }),
            new RuntimeWorldStateDefinition("event.counter", RuntimeWorldStateScope.Event, "events", RuntimeWorldStateValueKind.Integer, "0", RuntimeWorldStateResetPolicy.EventEnd, true, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "events" })
        };
        var service = new RuntimeGlobalWorldStateService(definitions);
        var denied = false; try { service.Set("module.locked", "true", "player", DateTimeOffset.UtcNow); } catch (UnauthorizedAccessException) { denied = true; }
        Check(denied, "reforged-unauthorized-mutation-fails-closed");
        Check(service.Increment("event.counter", 2, "events", DateTimeOffset.UtcNow).CurrentValue == "2", "reforged-checked-counter-increment");
        Check(service.Reset(RuntimeWorldStateResetPolicy.EventEnd, "events", DateTimeOffset.UtcNow).Single().CurrentValue == "0", "reforged-deterministic-event-reset");
        var hidden = false; try { service.Read("module.locked"); } catch (UnauthorizedAccessException) { hidden = true; }
        Check(hidden, "reforged-hidden-state-visibility");
        var path = Path.Combine(Path.GetTempPath(), "reforged-world-state-smoke.json");
        service.Set("module.locked", "true", "administrative-policy", DateTimeOffset.UtcNow); service.Save(path);
        var restored = new RuntimeGlobalWorldStateService(definitions); Check(restored.Restore(path) && restored.Read("module.locked", true).Value == "true", "reforged-persistent-state-restore");
        try { File.Delete(path); } catch { }
        Check(restored.ValidateState().Count == 0, "reforged-state-invariants");
        Check(!RuntimeClassicGlobalWorldStateAuthority.GenericWorldStateServiceOwnsClassicSemantics, "classic-semantics-remain-quarantined");
        return new(failures.Count == 0, checks, failures);
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }
    }
}
