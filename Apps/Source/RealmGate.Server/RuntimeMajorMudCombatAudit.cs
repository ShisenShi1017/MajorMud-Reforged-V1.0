using System.Text;

namespace RealmGate.Server;

public sealed record RuntimeMajorMudCombatAuditReport(
    int CombatRoundMilliseconds,
    int SpellRoundSeconds,
    int SpellSystemRoundMilliseconds,
    int StockHitMinimum,
    int StockHitMaximum,
    int StockDodgeMaximum,
    int MaxSwingsPerRound,
    bool HitInclusiveBoundary,
    bool DodgeInclusiveBoundary,
    bool CriticalInclusiveBoundary,
    bool FormulaSmokePassed,
    IReadOnlyList<string> FormulaSmokeFailures,
    IReadOnlyList<string> RemainingNativeBoundaries);

/// <summary>
/// Focused stock MajorMUD physical-combat parity audit. Values in this audit are
/// limited to recovered/source-correlated rules with executable regression checks.
/// Broad NPC message wording, death ordering, class accuracy reconstruction and
/// remaining DLL-only branches stay explicitly outside strict closure.
/// </summary>
public static class RuntimeMajorMudCombatAudit
{
    public const int StockHitMinimum = 8;
    public const int StockHitMaximum = 99;
    public const int StockDodgeMaximum = 95;
    public const int MaxSwingsPerRound = 5;

    public static RuntimeMajorMudCombatAuditReport Run(string projectRoot)
    {
        var physical = RuntimeClassicPhysicalFormulaSmokeReport.Run();
        var options = ServerRuntimeOptions.Load(projectRoot);
        var boundaries = new List<string>();
        if (options.CombatTickMilliseconds != RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds)
            boundaries.Add($"Configured combat scheduler is {options.CombatTickMilliseconds}ms but the stock combat authority is {RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds}ms.");
        boundaries.Add("Exact native player/NPC combat narration remains partially blocked by caller-owned WCC message arguments.");
        boundaries.Add("Class-combat accuracy base-stat ordering remains strongly correlated rather than strict native proof.");
        boundaries.Add("General backstab damage and some death/reward ordering remain evidence-gated even though the recovered formula path is live.");

        return new RuntimeMajorMudCombatAuditReport(
            RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds,
            RuntimeClassicLiveStatusCatalog.SpellRoundSeconds,
            RuntimeLiveGameplayFacade.ClassicSpellSystemRoundMilliseconds,
            StockHitMinimum,
            StockHitMaximum,
            StockDodgeMaximum,
            MaxSwingsPerRound,
            RuntimeClassicPhysicalFormulaCalculator.IsHitRollSuccessful(StockHitMinimum, StockHitMinimum)
                && !RuntimeClassicPhysicalFormulaCalculator.IsHitRollSuccessful(StockHitMinimum, StockHitMinimum + 1),
            RuntimeClassicPhysicalFormulaCalculator.IsDodgeRollSuccessful(1, 1)
                && !RuntimeClassicPhysicalFormulaCalculator.IsDodgeRollSuccessful(1, 2),
            RuntimeClassicPhysicalFormulaCalculator.IsCriticalRollSuccessful(1, 1)
                && !RuntimeClassicPhysicalFormulaCalculator.IsCriticalRollSuccessful(1, 2),
            physical.Passed,
            physical.Failures.ToArray(),
            boundaries);
    }

    public static string Render(RuntimeMajorMudCombatAuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("MAJORMUD COMBAT PARITY AUDIT");
        builder.AppendLine($"Physical combat round: {report.CombatRoundMilliseconds} ms");
        builder.AppendLine($"Spell/system round: {report.SpellRoundSeconds} s (scheduler {report.SpellSystemRoundMilliseconds} ms)");
        builder.AppendLine($"Stock physical hit clamp: {report.StockHitMinimum}%..{report.StockHitMaximum}%");
        builder.AppendLine($"Stock Dodge cap: {report.StockDodgeMaximum}%");
        builder.AppendLine($"Classic max swings/round: {report.MaxSwingsPerRound}");
        builder.AppendLine($"Inclusive hit boundary: {(report.HitInclusiveBoundary ? "PASS" : "FAIL")}");
        builder.AppendLine($"Inclusive Dodge boundary: {(report.DodgeInclusiveBoundary ? "PASS" : "FAIL")}");
        builder.AppendLine($"Inclusive critical boundary: {(report.CriticalInclusiveBoundary ? "PASS" : "FAIL")}");
        builder.AppendLine($"Physical formula regression suite: {(report.FormulaSmokePassed ? "PASS" : "FAIL")}");
        foreach (var failure in report.FormulaSmokeFailures)
            builder.AppendLine($"- formula failure: {failure}");
        builder.AppendLine();
        builder.AppendLine("REMAINING NATIVE BOUNDARIES");
        foreach (var boundary in report.RemainingNativeBoundaries)
            builder.AppendLine($"- {boundary}");
        return builder.ToString().TrimEnd();
    }
}
