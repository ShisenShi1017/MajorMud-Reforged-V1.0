namespace Reforged.App.BorinCapabilities;

public enum BorinCapabilityOperation
{
    None,
    Overview,
    RealmMonitoring,
    PlayerTelemetry,
    Analysis,
    EvidenceSearch,
    DecodeEvidenceQueue,
    ExportAnalysisReport,
    ExportEvidenceReport,
    CapabilityDiagnostics,
    RunScheduledJobs,
    PauseScheduler,
    ResumeScheduler,
    StopAll,
    WorldsmithWithheld,
}

public sealed record BorinCapabilityRequest(
    BorinCapabilityOperation Capability,
    string Argument = "");

public sealed record BorinCapabilityResult(
    bool Handled,
    bool Succeeded,
    string Summary)
{
    public static BorinCapabilityResult NotHandled { get; } = new(false, false, string.Empty);
}

public interface IBorinCapabilities
{
    string StatusSummary { get; }
    Task InitializeAsync(CancellationToken token = default);
    Task<BorinCapabilityResult> ExecuteAsync(string request, CancellationToken token = default);
}

/// <summary>
/// Deterministic admission router for Borin's local capability services.
/// It recognizes explicit operational requests only; ordinary conversation and
/// gameplay remain with Borin's normal hybrid-brain/player routes.
/// </summary>
public static class BorinCapabilityRequestRouter
{
    public static BorinCapabilityRequest Route(string? request)
    {
        var text = request?.Trim() ?? string.Empty;
        var value = text.ToLowerInvariant();
        if (value.Length == 0)
            return new(BorinCapabilityOperation.None);

        if (ContainsAny(value, "worldsmith", "world smith", "edit the world", "change the test realm", "create a room"))
            return new(BorinCapabilityOperation.WorldsmithWithheld);
        if (ContainsAny(value, "stop all capabilities", "stop all guardians", "emergency stop guardians"))
            return new(BorinCapabilityOperation.StopAll);
        if (ContainsAny(value, "pause capability scheduler", "pause guardian scheduler"))
            return new(BorinCapabilityOperation.PauseScheduler);
        if (ContainsAny(value, "resume capability scheduler", "resume guardian scheduler"))
            return new(BorinCapabilityOperation.ResumeScheduler);
        if (ContainsAny(value, "run due jobs", "run scheduled guardian", "run capability jobs"))
            return new(BorinCapabilityOperation.RunScheduledJobs);
        if (ContainsAny(value, "capability self-test", "guardian self-test", "test guardian capabilities"))
            return new(BorinCapabilityOperation.CapabilityDiagnostics);
        if (ContainsAny(value, "decode evidence queue", "decode archivist queue", "process evidence queue"))
            return new(BorinCapabilityOperation.DecodeEvidenceQueue);
        if (ContainsAny(value, "export analysis report", "generate analysis report"))
            return new(BorinCapabilityOperation.ExportAnalysisReport);
        if (ContainsAny(value, "export evidence report", "generate evidence report"))
            return new(BorinCapabilityOperation.ExportEvidenceReport,
                value.Contains("json", StringComparison.Ordinal) ? "JSON" : value.Contains("html", StringComparison.Ordinal) ? "HTML" : "Markdown");

        if (TryEvidenceQuery(text, value, out var query))
            return new(BorinCapabilityOperation.EvidenceSearch, query);

        if (HasAction(value, "analyze", "analyse", "refresh analysis", "inspect trends") &&
            ContainsAny(value, "economy", "combat", "drop", "npc", "quest", "experience", "item", "progression", "balance", "analysis"))
            return new(BorinCapabilityOperation.Analysis, SelectAnalysisTarget(value));

        if (HasAction(value, "monitor", "refresh", "inspect", "check", "show") &&
            ContainsAny(value, "realm health", "world health", "server health", "realm telemetry", "world telemetry", "connected realms"))
            return new(BorinCapabilityOperation.RealmMonitoring);

        if (HasAction(value, "refresh", "inspect", "check", "show") &&
            ContainsAny(value, "player telemetry", "player operations", "mission telemetry"))
            return new(BorinCapabilityOperation.PlayerTelemetry);

        if (ContainsAny(value, "guardian capability status", "borin capability status", "all capability status"))
            return new(BorinCapabilityOperation.Overview);

        return new(BorinCapabilityOperation.None);
    }

    private static bool TryEvidenceQuery(string original, string value, out string query)
    {
        foreach (var prefix in new[] { "search evidence for ", "search librarium evidence for ", "find evidence for " })
        {
            var index = value.IndexOf(prefix, StringComparison.Ordinal);
            if (index < 0) continue;
            query = original[(index + prefix.Length)..].Trim().Trim('"', '\'');
            return query.Length > 0;
        }
        query = string.Empty;
        return false;
    }

    private static string SelectAnalysisTarget(string value)
    {
        if (value.Contains("economy", StringComparison.Ordinal)) return "Economy";
        if (value.Contains("combat", StringComparison.Ordinal)) return "Combat";
        if (value.Contains("drop", StringComparison.Ordinal)) return "Drops";
        if (value.Contains("npc", StringComparison.Ordinal)) return "NPC Balance";
        if (value.Contains("quest", StringComparison.Ordinal)) return "Quest Balance";
        if (value.Contains("experience", StringComparison.Ordinal)) return "Experience Curve";
        if (value.Contains("item", StringComparison.Ordinal)) return "Item Balance";
        return "Progression";
    }

    private static bool HasAction(string value, params string[] actions) => ContainsAny(value, actions);

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));
}
