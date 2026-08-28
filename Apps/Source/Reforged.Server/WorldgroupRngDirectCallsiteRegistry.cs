namespace Reforged.Server;

public sealed record WorldgroupRngDirectCallsiteEvidence(
    string Module,
    string ModuleSha256,
    string Symbol,
    uint ImportThunkVa,
    int DirectCallsiteCount,
    int RetainedDirectAddressCount,
    int BoundedArgumentAnalyzerCallsiteCount);

/// <summary>
/// Proven static MajorMUD -> WGSERVER randomness import boundary.
///
/// This registry intentionally stops at direct x86 calls to the imported WGSERVER
/// thunks. It does not assign gameplay semantics to individual callers, does not
/// resolve indirect calls, does not choose an execution-thread CRT owner, and does
/// not activate the still-unrecovered _genrdn/_lngrnd bounded-remainder loop.
/// </summary>
public static class WorldgroupRngDirectCallsiteRegistry
{
    public const string EvidenceId = "FinalNativeClosure0003/0014 + Pass35-0306";
    public const string DirectCallScope = "PE import table plus x86 direct-call references to WGSERVER.EXE import thunks; indirect register/data calls excluded.";
    public const string ArgumentScope = "Bounded caller-side stack argument analysis; coverage is not a complete prototype/callsite map.";

    public static readonly bool IndirectCallsitesResolved = false;
    public static readonly bool GameplaySemanticMappingResolved = false;
    public static readonly bool ExecutionThreadCrtMappingResolved = false;
    public static readonly bool GameplayDrawOrderingResolved = false;
    public static readonly bool InstructionEquivalentRangeLoopPortResolved = false;

    public static readonly IReadOnlyList<WorldgroupRngDirectCallsiteEvidence> Proven = new WorldgroupRngDirectCallsiteEvidence[]
    {
        new("wccmmud.dll", "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077", "_genrdn", 0x00478CB2u, 146, 64, 64),
        new("wccmmud.dll", "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077", "_lngrnd", 0x004789E2u, 42, 42, 42),
        new("wbbmmud.dll", "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091", "_genrdn", 0x00478CB2u, 146, 64, 64),
        new("wbbmmud.dll", "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091", "_lngrnd", 0x004789E2u, 42, 42, 42),
    };

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Proven.Count != 4)
            failures.Add("Expected four native module/symbol boundary rows.");

        foreach (var module in new[] { "wccmmud.dll", "wbbmmud.dll" })
        {
            var rows = Proven.Where(row => row.Module.Equals(module, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (rows.Length != 2)
                failures.Add($"{module}: expected _genrdn and _lngrnd rows.");
            if (rows.SingleOrDefault(row => row.Symbol == "_genrdn") is not { DirectCallsiteCount: 146, ImportThunkVa: 0x00478CB2u, RetainedDirectAddressCount: 64, BoundedArgumentAnalyzerCallsiteCount: 64 })
                failures.Add($"{module}: _genrdn boundary mismatch.");
            if (rows.SingleOrDefault(row => row.Symbol == "_lngrnd") is not { DirectCallsiteCount: 42, ImportThunkVa: 0x004789E2u, RetainedDirectAddressCount: 42, BoundedArgumentAnalyzerCallsiteCount: 42 })
                failures.Add($"{module}: _lngrnd boundary mismatch.");
        }

        var legacyGenrdn = NativeHostCallsiteRegistry.Proven.SingleOrDefault(row => row.Symbol == "_genrdn");
        var legacyLngrnd = NativeHostCallsiteRegistry.Proven.SingleOrDefault(row => row.Symbol == "_lngrnd");
        if (legacyGenrdn?.DirectCallsiteCount != Proven.Where(row => row.Symbol == "_genrdn").Sum(row => row.DirectCallsiteCount))
            failures.Add("Combined _genrdn direct-call census disagrees with retained host registry.");
        if (legacyLngrnd?.DirectCallsiteCount != Proven.Where(row => row.Symbol == "_lngrnd").Sum(row => row.DirectCallsiteCount))
            failures.Add("Combined _lngrnd direct-call census disagrees with retained host registry.");

        var boundedGenrdn = NativeHostArgumentPositionRegistry.Proven.SingleOrDefault(row => row.Symbol == "_genrdn");
        var boundedLngrnd = NativeHostArgumentPositionRegistry.Proven.SingleOrDefault(row => row.Symbol == "_lngrnd");
        if (boundedGenrdn?.DirectCallsiteCount != Proven.Where(row => row.Symbol == "_genrdn").Sum(row => row.BoundedArgumentAnalyzerCallsiteCount))
            failures.Add("Bounded _genrdn argument-analysis coverage disagrees with retained host registry.");
        if (boundedLngrnd?.DirectCallsiteCount != Proven.Where(row => row.Symbol == "_lngrnd").Sum(row => row.BoundedArgumentAnalyzerCallsiteCount))
            failures.Add("Bounded _lngrnd argument-analysis coverage disagrees with retained host registry.");

        if (IndirectCallsitesResolved || GameplaySemanticMappingResolved || ExecutionThreadCrtMappingResolved || GameplayDrawOrderingResolved || InstructionEquivalentRangeLoopPortResolved)
            failures.Add("Unresolved RNG integration boundary was incorrectly promoted.");

        return failures;
    }
}
