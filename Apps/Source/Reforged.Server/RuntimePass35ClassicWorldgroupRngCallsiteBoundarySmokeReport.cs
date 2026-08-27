using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass35ClassicWorldgroupRngCallsiteBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass35ClassicWorldgroupRngCallsiteBoundarySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(WorldgroupRngDirectCallsiteRegistry.Validate().Count == 0, "rng-direct-callsite-registry-valid");

        var rows = WorldgroupRngDirectCallsiteRegistry.Proven;
        Check(rows.Count == 4, "four-module-symbol-boundary-rows");
        Check(rows.Where(row => row.Symbol == "_genrdn").Sum(row => row.DirectCallsiteCount) == 292, "genrdn-combined-direct-call-count-292");
        Check(rows.Where(row => row.Symbol == "_lngrnd").Sum(row => row.DirectCallsiteCount) == 84, "lngrnd-combined-direct-call-count-84");
        Check(rows.Where(row => row.Symbol == "_genrdn").Sum(row => row.RetainedDirectAddressCount) == 128, "genrdn-retained-address-subset-128");
        Check(rows.Where(row => row.Symbol == "_genrdn").Sum(row => row.BoundedArgumentAnalyzerCallsiteCount) == 128, "genrdn-bounded-argument-coverage-128");
        Check(rows.Where(row => row.Symbol == "_lngrnd").Sum(row => row.RetainedDirectAddressCount) == 84, "lngrnd-retained-address-list-84");
        Check(rows.Where(row => row.Symbol == "_lngrnd").Sum(row => row.BoundedArgumentAnalyzerCallsiteCount) == 84, "lngrnd-bounded-argument-coverage-84");
        Check(rows.Where(row => row.Symbol == "_genrdn").All(row => row.ImportThunkVa == 0x00478CB2u), "genrdn-thunk-parity");
        Check(rows.Where(row => row.Symbol == "_lngrnd").All(row => row.ImportThunkVa == 0x004789E2u), "lngrnd-thunk-parity");
        Check(rows.Single(row => row.Module == "wccmmud.dll" && row.Symbol == "_genrdn").ModuleSha256 == "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077", "wccmmud-version-pinned");
        Check(rows.Single(row => row.Module == "wbbmmud.dll" && row.Symbol == "_genrdn").ModuleSha256 == "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091", "wbbmmud-version-pinned");

        Check(!WorldgroupRngDirectCallsiteRegistry.IndirectCallsitesResolved, "indirect-calls-stay-unresolved");
        Check(!WorldgroupRngDirectCallsiteRegistry.GameplaySemanticMappingResolved, "gameplay-callsite-semantics-stay-unresolved");
        Check(!WorldgroupRngDirectCallsiteRegistry.ExecutionThreadCrtMappingResolved, "execution-thread-crt-mapping-stays-unresolved");
        Check(!WorldgroupRngDirectCallsiteRegistry.GameplayDrawOrderingResolved, "gameplay-draw-order-stays-unresolved");
        Check(!WorldgroupRngDirectCallsiteRegistry.InstructionEquivalentRangeLoopPortResolved, "range-loop-port-stays-unresolved");

        return new(failures.Count == 0, checks, failures);
    }
}
