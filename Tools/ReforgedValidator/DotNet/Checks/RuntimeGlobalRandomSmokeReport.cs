using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeGlobalRandomSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeGlobalRandomSmokeReport Run()
    {
        var checks = new List<string>(); var failures = new List<string>();
        var a = new RuntimeGlobalRandomService(12345); var b = new RuntimeGlobalRandomService(12345);
        var first = Enumerable.Range(0, 32).Select(_ => a.NextInclusive("world", -5, 5).Result).ToArray();
        var second = Enumerable.Range(0, 32).Select(_ => b.NextInclusive("world", -5, 5).Result).ToArray();
        Check(first.SequenceEqual(second), "deterministic-seed-replay");
        Check(first.All(x => x >= -5 && x <= 5), "inclusive-bounds");
        a.RegisterStream("session:1", RuntimeRandomScope.Session, 99);
        var snapshot = a.Snapshot("session:1"); var draw1 = a.RollPercent("session:1"); a.Restore(snapshot); var draw2 = a.RollPercent("session:1");
        Check(draw1.Result == draw2.Result && draw1.RawValue == draw2.RawValue, "snapshot-restore-replay");
        var unknown = false; try { a.NextInclusive("missing", 1, 2); } catch (KeyNotFoundException) { unknown = true; }
        Check(unknown, "unknown-stream-fail-closed");
        Check(a.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }
    }
}
