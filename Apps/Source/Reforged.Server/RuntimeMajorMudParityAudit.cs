using System.Text;
using System.Text.Json;

namespace Reforged.Server;

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
    private const string AssertionLedgerRelativePath = "Librarium/Parity/majormud_parity_assertions_0313.json";
    private const string RemainingSystemsRelativePath = "Librarium/RemainingSystems/remaining_systems_registry.json";
    private const string RemainingAuditRelativePath = "Librarium/Reports/COMBINED_DECODE_REMAINING_AUDIT_AFTER_REMAINING_SYSTEMS.json";
    private const string OwnershipScopeRelativePath = "Librarium/Parity/majormud_ownership_scope_0298.json";

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
        var ownershipScope = LoadOwnershipScope(root, diagnostics);
        LoadCuratedAssertions(root, assertions, diagnostics, ownershipScope);
        LoadUnknownNativeDependencyGroups(root, assertions, diagnostics, ownershipScope);

        var remainingSystemCount = 0;
        var partialSystemShellCount = 0;
        LoadRemainingSystemShellCounts(root, diagnostics, ref remainingSystemCount, ref partialSystemShellCount);

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


    private sealed record OwnershipScope(
        HashSet<string> ExcludedCuratedAssertionIds,
        HashSet<string> ExcludedNativeDependencyIds,
        Dictionary<string, string[]> MixedNativeDependencyOverrides);

    private static OwnershipScope LoadOwnershipScope(string root, List<string> diagnostics)
    {
        var empty = new OwnershipScope(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
        var path = Path.Combine(root, OwnershipScopeRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            diagnostics.Add($"MajorMUD ownership scope missing: {OwnershipScopeRelativePath}");
            return empty;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;
            var curated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var native = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var overrides = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (rootElement.TryGetProperty("excludedCuratedAssertionIds", out var curatedArray) && curatedArray.ValueKind == JsonValueKind.Array)
                foreach (var value in curatedArray.EnumerateArray())
                    if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                        curated.Add(value.GetString()!.Trim());
            if (rootElement.TryGetProperty("excludedNativeDependencyIds", out var nativeArray) && nativeArray.ValueKind == JsonValueKind.Array)
                foreach (var value in nativeArray.EnumerateArray())
                    if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                        native.Add(value.GetString()!.Trim());
            if (rootElement.TryGetProperty("mixedNativeDependencyOverrides", out var overrideObject) && overrideObject.ValueKind == JsonValueKind.Object)
                foreach (var property in overrideObject.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.Array)
                        overrides[property.Name] = property.Value.EnumerateArray()
                            .Where(value => value.ValueKind == JsonValueKind.String)
                            .Select(value => value.GetString()?.Trim() ?? string.Empty)
                            .Where(value => value.Length > 0)
                            .ToArray();
            return new OwnershipScope(curated, native, overrides);
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Failed to read MajorMUD ownership scope: {exception.Message}");
            return empty;
        }
    }

    private static void LoadCuratedAssertions(string root, List<RuntimeMajorMudParityAssertion> assertions, List<string> diagnostics, OwnershipScope ownershipScope)
    {
        var path = Path.Combine(root, AssertionLedgerRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            diagnostics.Add($"Parity assertion ledger missing: {AssertionLedgerRelativePath}");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("assertions", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add("Parity assertion ledger has no assertions array.");
                return;
            }

            foreach (var item in array.EnumerateArray())
            {
                var id = Text(item, "id");
                var category = Text(item, "category");
                var state = Text(item, "state").ToUpperInvariant();
                var description = Text(item, "description");
                var evidence = item.TryGetProperty("evidence", out var evidenceArray) && evidenceArray.ValueKind == JsonValueKind.Array
                    ? evidenceArray.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                    : Array.Empty<string>();
                if (ownershipScope.ExcludedCuratedAssertionIds.Contains(id))
                    continue;
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

    private static void LoadUnknownNativeDependencyGroups(string root, List<RuntimeMajorMudParityAssertion> assertions, List<string> diagnostics, OwnershipScope ownershipScope)
    {
        var path = Path.Combine(root, RemainingAuditRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            diagnostics.Add($"Native dependency report missing: {RemainingAuditRelativePath}");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("deduplicatedRemainingSystemsNativeDependencies", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add("Native dependency report has no deduplicatedRemainingSystemsNativeDependencies array.");
                return;
            }

            foreach (var item in array.EnumerateArray())
            {
                var id = Text(item, "id");
                if (id.Length == 0) continue;
                if (ownershipScope.ExcludedNativeDependencyIds.Contains(id))
                    continue;
                var questions = item.TryGetProperty("questions", out var questionArray) && questionArray.ValueKind == JsonValueKind.Array
                    ? questionArray.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                    : Array.Empty<string>();
                if (ownershipScope.MixedNativeDependencyOverrides.TryGetValue(id, out var scopedQuestions))
                    questions = scopedQuestions;
                var description = questions.Length == 0
                    ? "Native dependency group remains unresolved and requires final native evidence."
                    : string.Join("; ", questions);
                var assertionId = $"native.{id}";
                if (assertions.Any(existing => existing.Id.Equals(assertionId, StringComparison.OrdinalIgnoreCase)))
                    continue;
                assertions.Add(new RuntimeMajorMudParityAssertion(
                    assertionId,
                    CategoryForNativeDependency(id),
                    "UNKNOWN",
                    description,
                    new[] { RemainingAuditRelativePath }));
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Failed to read native dependency report: {exception.Message}");
        }
    }

    private static void LoadRemainingSystemShellCounts(string root, List<string> diagnostics, ref int total, ref int partial)
    {
        var path = Path.Combine(root, RemainingSystemsRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            diagnostics.Add($"Remaining-systems registry missing: {RemainingSystemsRelativePath}");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("systems", out var array) || array.ValueKind != JsonValueKind.Array) return;
            foreach (var item in array.EnumerateArray())
            {
                total++;
                var classification = Text(item, "classification");
                var status = Text(item, "status");
                if (classification.Contains("partially", StringComparison.OrdinalIgnoreCase) || status.StartsWith("implemented", StringComparison.OrdinalIgnoreCase))
                    partial++;
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Failed to read remaining-systems registry: {exception.Message}");
        }
    }

    private static IReadOnlyDictionary<string, string> LoadCoverageMetrics(string root, List<string> diagnostics)
    {
        var metrics = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ReadMetricFile(root, "Reports/RuntimeVerification/classic_decode_exhaustion_report.json", diagnostics, document =>
        {
            var rootElement = document.RootElement;
            metrics["Decode families"] = $"{Int(rootElement, "ConnectedFamilyCount")}/{Int(rootElement, "FamilyCount")} connected";
            metrics["Decode scalar fields"] = $"{Int(rootElement, "ConnectedFieldCount")}/{Int(rootElement, "TotalFields")} connected/structural";
        });
        ReadMetricFile(root, "Reports/RuntimeVerification/classic_spell_effect_coverage.json", diagnostics, document =>
        {
            var rootElement = document.RootElement;
            metrics["Spells classified"] = $"{Int(rootElement, "ClassifiedSpellCount")}/{Int(rootElement, "SpellCount")}";
            metrics["Runtime status effects"] = $"{Int(rootElement, "RuntimeAuthoritativeStatusEffectCount")}/{Int(rootElement, "StatusEffectCount")}";
        });
        ReadMetricFile(root, "Reports/RuntimeVerification/classic_native_message_coverage.json", diagnostics, document =>
        {
            metrics["Native message catalog"] = $"{Int(document.RootElement, "NativeMessageCount")} messages loaded";
        });
        ReadMetricFile(root, "Reports/RuntimeVerification/classic_economy_service_coverage.json", diagnostics, document =>
        {
            var rootElement = document.RootElement;
            metrics["Economy services"] = $"{Int(rootElement, "ClassifiedServiceCount")} classified / {Int(rootElement, "UnclassifiedServiceCount")} unclassified";
        });
        return metrics;
    }

    private static void ReadMetricFile(string root, string relativePath, List<string> diagnostics, Action<JsonDocument> reader)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            reader(document);
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Failed to read coverage metric {relativePath}: {exception.Message}");
        }
    }

    private static int Int(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static string Text(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : string.Empty;

    private static string CategoryForNativeDependency(string id)
    {
        var value = id.ToLowerInvariant();
        if (value.Contains("party") || value.StartsWith("communications") || value.StartsWith("mail") || value.StartsWith("organizations")) return "Parties / Communication / Organizations";
        if (value.StartsWith("rooms") || value.StartsWith("doors") || value.StartsWith("traps") || value.StartsWith("environmental") || value.StartsWith("world-containers") || value.StartsWith("weather")) return "Rooms & World";
        if (value.StartsWith("clock") || value.StartsWith("world-reset") || value.StartsWith("events") || value.StartsWith("world-rules")) return "Timers / Resets / World Rules";
        if (value.StartsWith("pvp") || value.StartsWith("law") || value.StartsWith("rest-inn") || value.StartsWith("character-identity")) return "Character & Status";
        if (value.StartsWith("transport") || value.StartsWith("player-transactions")) return "Economy & Services";
        if (value.StartsWith("account-session")) return "Protocol & Legacy Clients";
        if (value.StartsWith("administrative") || value.StartsWith("security") || value.StartsWith("builder")) return "Administration / Security / Builder";
        if (value.StartsWith("logging") || value.StartsWith("save") || value.StartsWith("startup") || value.StartsWith("worldgroup") || value.StartsWith("global-rng") || value.StartsWith("global-world-state") || value.StartsWith("repository-inventory")) return "Host / Persistence / Worldgroup";
        if (value.StartsWith("import-export") || value.StartsWith("capacity") || value.StartsWith("error-handling")) return "Import / Export / Limits / Recovery";
        if (value.StartsWith("help")) return "Commands & Help";
        if (value.StartsWith("rankings") || value.StartsWith("games-arenas") || value.StartsWith("reforged-extension")) return "Optional / Rankings / Extensions";
        return "Native Closure";
    }
}
