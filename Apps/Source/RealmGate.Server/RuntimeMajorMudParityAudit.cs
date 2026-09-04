using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeMajorMudParityAssertion(
    string Id,
    string Category,
    string State,
    string Description,
    IReadOnlyList<string> Evidence);

public sealed record RuntimeMajorMudParityCategorySummary(
    string Category,
    int Pass,
    int Strong,
    int Unknown,
    int Total,
    double StrictPercent,
    double EvidenceBackedPercent);

public sealed record RuntimeMajorMudParityReport(
    DateTimeOffset GeneratedAtUtc,
    string ProjectRoot,
    int PassCount,
    int StrongCount,
    int UnknownCount,
    int ScoredRequirementCount,
    double StrictPercent,
    double EvidenceBackedPercent,
    int PartialSystemShellCount,
    int RemainingSystemCount,
    IReadOnlyList<RuntimeMajorMudParityCategorySummary> Categories,
    IReadOnlyList<RuntimeMajorMudParityAssertion> Assertions,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyDictionary<string, string> CoverageMetrics);

/// <summary>
/// Conservative whole-project MajorMUD parity scorecard.
///
/// PASS receives strict parity credit. STRONG is evidence-backed implementation
/// that still requires native proof. UNKNOWN is an unresolved native dependency
/// group. PARTIAL remaining-system shells are reported separately and never receive
/// parity credit, preventing implementation presence from inflating the score.
/// </summary>
public static class RuntimeMajorMudParityAudit
{
    private const string AssertionLedgerRelativePath = "Data/Classic/parity.json";

    public static RuntimeMajorMudParityReport Run(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var assertions = new List<RuntimeMajorMudParityAssertion>();
        var diagnostics = new List<string>();
        var characterFormulaAudit = RuntimeClassicCharacterFormulaAudit.Run();
        if (!characterFormulaAudit.Passed)
            diagnostics.Add($"Character/Advancement formula audit failed: {string.Join(", ", characterFormulaAudit.Failures)}");
        try
        {
            var npcCadenceAudit = RuntimeClassicNpcCombatCadenceAudit.Run();
            if (!npcCadenceAudit.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase))
                diagnostics.Add("NPC combat cadence audit did not pass.");
        }
        catch (Exception exception)
        {
            diagnostics.Add($"NPC combat cadence audit failed: {exception.Message}");
        }
        try
        {
            var npcNativeAudit = RuntimeClassicNpcAiNativeClosureAudit.Run(root);
            if (!npcNativeAudit.Passed)
                diagnostics.Add($"NPC native field/AI closure audit failed: {string.Join(", ", npcNativeAudit.Failures)}");
        }
        catch (Exception exception)
        {
            diagnostics.Add($"NPC native field/AI closure audit failed: {exception.Message}");
        }
        try
        {
            var roomsDoorsTrapsAudit = RuntimeClassicRoomsDoorsTrapsParityAudit.Run(root);
            if (!roomsDoorsTrapsAudit.Passed)
                diagnostics.Add($"Rooms/Doors/Traps parity audit failed: {string.Join(", ", roomsDoorsTrapsAudit.Failures)}");
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Rooms/Doors/Traps parity audit failed: {exception.Message}");
        }
        try
        {
            var economyAudit = RuntimeClassicEconomyParityAudit.Run(root);
            if (!economyAudit.Passed)
                diagnostics.Add($"Economy/Transactions/Services parity audit failed: {string.Join(", ", economyAudit.Failures)}");
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Economy/Transactions/Services parity audit failed: {exception.Message}");
        }
        LoadCuratedAssertions(root, assertions, diagnostics);

        const int remainingSystemCount = 0;
        const int partialSystemShellCount = 0;

        var scored = assertions.Where(assertion => assertion.State is "PASS" or "STRONG" or "UNKNOWN").ToArray();
        var pass = scored.Count(assertion => assertion.State == "PASS");
        var strong = scored.Count(assertion => assertion.State == "STRONG");
        var unknown = scored.Count(assertion => assertion.State == "UNKNOWN");
        var total = scored.Length;

        var categories = scored
            .GroupBy(assertion => assertion.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var categoryPass = group.Count(assertion => assertion.State == "PASS");
                var categoryStrong = group.Count(assertion => assertion.State == "STRONG");
                var categoryUnknown = group.Count(assertion => assertion.State == "UNKNOWN");
                var categoryTotal = group.Count();
                return new RuntimeMajorMudParityCategorySummary(
                    group.Key,
                    categoryPass,
                    categoryStrong,
                    categoryUnknown,
                    categoryTotal,
                    Percent(categoryPass, categoryTotal),
                    Percent(categoryPass + categoryStrong, categoryTotal));
            })
            .ToArray();

        var metrics = LoadCoverageMetrics(root, diagnostics);
        return new RuntimeMajorMudParityReport(
            DateTimeOffset.UtcNow,
            root,
            pass,
            strong,
            unknown,
            total,
            Percent(pass, total),
            Percent(pass + strong, total),
            partialSystemShellCount,
            remainingSystemCount,
            categories,
            assertions.OrderBy(assertion => assertion.Category, StringComparer.OrdinalIgnoreCase).ThenBy(assertion => assertion.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
            diagnostics,
            metrics);
    }

    public static string Render(RuntimeMajorMudParityReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("MAJORMUD PARITY SCORECARD");
        builder.AppendLine($"Strict proven parity: {report.PassCount}/{report.ScoredRequirementCount} = {report.StrictPercent:0.0}%");
        builder.AppendLine($"Evidence-backed coverage: {report.PassCount + report.StrongCount}/{report.ScoredRequirementCount} = {report.EvidenceBackedPercent:0.0}%");
        builder.AppendLine($"Unresolved native dependency groups: {report.UnknownCount}/{report.ScoredRequirementCount} = {Percent(report.UnknownCount, report.ScoredRequirementCount):0.0}%");
        builder.AppendLine($"Remaining-system shells: {report.PartialSystemShellCount}/{report.RemainingSystemCount} implemented (reported separately; no parity credit)");
        builder.AppendLine();
        builder.AppendLine("Scoring rule: PASS = strict credit; STRONG = implemented/evidence-backed but not exact; UNKNOWN = blocks 100%. No half-credit is awarded.");
        builder.AppendLine("The denominator is the current known requirement set and may grow when new native behavior is discovered.");
        builder.AppendLine();
        builder.AppendLine("BY SUBSYSTEM");
        foreach (var category in report.Categories)
        {
            builder.AppendLine($"- {category.Category}: PASS {category.Pass}, STRONG {category.Strong}, UNKNOWN {category.Unknown} (strict {category.StrictPercent:0.0}%, evidence {category.EvidenceBackedPercent:0.0}%)");
        }

        if (report.CoverageMetrics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("IMPLEMENTATION / DATA COVERAGE (not strict parity credit)");
            foreach (var metric in report.CoverageMetrics)
                builder.AppendLine($"- {metric.Key}: {metric.Value}");
        }

        var blockers = report.Assertions
            .Where(assertion => assertion.State == "UNKNOWN")
            .Take(12)
            .ToArray();
        if (blockers.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("NEXT NATIVE BLOCKERS");
            foreach (var blocker in blockers)
                builder.AppendLine($"- {blocker.Id}: {blocker.Description}");
            if (report.UnknownCount > blockers.Length)
                builder.AppendLine($"- ... {report.UnknownCount - blockers.Length} additional unresolved native dependency groups");
        }

        if (report.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("AUDIT DIAGNOSTICS");
            foreach (var diagnostic in report.Diagnostics)
                builder.AppendLine($"- {diagnostic}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string ToJson(RuntimeMajorMudParityReport report) =>
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

    internal static double Percent(int numerator, int denominator) =>
        denominator <= 0 ? 0d : Math.Round(numerator * 100d / denominator, 1, MidpointRounding.AwayFromZero);


    private static void LoadCuratedAssertions(string root, List<RuntimeMajorMudParityAssertion> assertions, List<string> diagnostics)
    {
        var path = Path.Combine(root, AssertionLedgerRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            diagnostics.Add($"Parity assertion ledger missing: {AssertionLedgerRelativePath}");
            return;
        }

        try
        {
            foreach (var item in RuntimeChunkedClassicCatalog.LoadRecords(path))
            {
                var id = Text(item, "id");
                var category = Text(item, "category");
                var state = Text(item, "state").ToUpperInvariant();
                var description = Text(item, "description");
                var evidence = item.TryGetProperty("evidence", out var evidenceArray) && evidenceArray.ValueKind == JsonValueKind.Array
                    ? evidenceArray.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                    : Array.Empty<string>();
                if (id.Length == 0 || category.Length == 0 || description.Length == 0 || state is not ("PASS" or "STRONG" or "UNKNOWN"))
                {
                    diagnostics.Add($"Skipped invalid parity assertion '{id}'.");
                    continue;
                }
                assertions.Add(new RuntimeMajorMudParityAssertion(id, category, state, description, evidence));
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Failed to read parity assertion ledger: {exception.Message}");
        }
    }

    private static IReadOnlyDictionary<string, string> LoadCoverageMetrics(string root, List<string> diagnostics)
    {
        var metrics = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(root, "Data", "Classic", "compile_manifest.json");
        if (!File.Exists(path))
        {
            diagnostics.Add("Generated Classic compile manifest is missing.");
            return metrics;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var manifest = document.RootElement;
            metrics["Generated families"] = $"{ReadInt(manifest, "object_families_compiled")}";
            metrics["Generated records"] = $"{ReadInt(manifest, "objects_compiled")}";
            metrics["Generated files"] = $"{ReadInt(manifest, "files_written")}";
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Failed to read generated Classic compile manifest: {exception.Message}");
        }
        return metrics;
    }

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static string Text(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : string.Empty;

}
