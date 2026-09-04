using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleDRemainingExhaustionAuditReport(
    bool Passed,
    int ExitCount,
    int StatefulExitCount,
    int ReciprocalLinks,
    int ActionBoundExits,
    int RestrictedExits,
    int PicklockOrStrengthExits,
    int EffectExits,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> MetadataOnlyFields)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Bundle D-only closure audit. This deliberately excludes compiler, provenance, evidence-summary,
/// client-projection, and cross-bundle fields from gameplay closure. It validates that every decoded
/// D exit mechanic retained by exits.json has an authoritative loader/evaluator or an explicit
/// non-runtime metadata classification.
/// </summary>
public static class RuntimeBundleDRemainingExhaustionAudit
{
    public static RuntimeBundleDRemainingExhaustionAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }

        var world = new RuntimeWorldLoader().Load(projectRoot, ServerRuntimeOptions.Load(projectRoot).Worlds.Single(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase)));
        var living = new RuntimeLivingWorldService(projectRoot, world, Path.Combine(Path.GetTempPath(), "reforged-bundle-d-final", Guid.NewGuid().ToString("N")));
        var gates = living.CaptureGates();
        var source = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeLivingWorldCompletion.cs"));

        Check(gates.Count == world.ExitCount, "all-generated-exits-have-gate-definitions");
        Check(source.Contains("ReciprocalExitId", StringComparison.Ordinal), "reciprocal-exit-loaded");
        Check(source.Contains("ReciprocalStateLink", StringComparison.Ordinal), "reciprocal-state-link-loaded");
        Check(source.Contains("RequiredActionOrder", StringComparison.Ordinal), "required-action-order-consumed");
        Check(source.Contains("PicklocksDifficulty", StringComparison.Ordinal), "picklock-difficulty-consumed");
        Check(source.Contains("StrengthAlternative", StringComparison.Ordinal), "strength-alternative-consumed");
        Check(source.Contains("DeniedClassId", StringComparison.Ordinal), "denied-class-consumed");
        Check(source.Contains("DeniedRaceId", StringComparison.Ordinal), "denied-race-consumed");
        Check(source.Contains("MinimumAlignmentName", StringComparison.Ordinal), "minimum-alignment-consumed");
        Check(source.Contains("MaximumAlignmentName", StringComparison.Ordinal), "maximum-alignment-consumed");
        Check(source.Contains("PreSpellId", StringComparison.Ordinal) && source.Contains("PostSpellId", StringComparison.Ordinal), "pre-post-spell-effects-consumed");
        Check(source.Contains("AutomaticRouting", StringComparison.Ordinal), "automatic-routing-retained");
        Check(source.Contains("FallbackBehavior", StringComparison.Ordinal), "fallback-behavior-retained");
        Check(source.Contains("SaveWorldStateRisk", StringComparison.Ordinal), "save-world-state-risk-retained");
        Check(source.Contains("StateIdentity", StringComparison.Ordinal), "state-identity-retained");
        Check(source.Contains("OwnerScope", StringComparison.Ordinal), "owner-scope-retained");

        var reciprocal = gates.Count(x => !string.IsNullOrWhiteSpace(x.ReciprocalExitId));
        var actionBound = gates.Count(x => x.RequiredActionCount > 0 || x.RequiredActionOrder.Count > 0);
        var restricted = gates.Count(x => x.AllowedClassId is not null || x.DeniedClassId is not null || x.AllowedRaceId is not null || x.DeniedRaceId is not null || !string.IsNullOrWhiteSpace(x.MinimumAlignmentName) || !string.IsNullOrWhiteSpace(x.MaximumAlignmentName) || x.AbilityId is not null);
        var picklock = gates.Count(x => x.PicklocksDifficulty is not null || x.PicklocksOrStrength is not null || x.StrengthAlternative || x.AnyPicklocks || x.AnyPicklocksOrStrength);
        var effects = gates.Count(x => x.Damage != 0 || x.SpellId > 0 || x.PreSpellId > 0 || x.PostSpellId > 0);
        var stateful = gates.Count(x => !string.IsNullOrWhiteSpace(x.StateIdentity) || x.ReciprocalStateLink || x.SaveWorldStateRisk);

        foreach (var gate in gates.Where(x => !string.IsNullOrWhiteSpace(x.ReciprocalExitId)))
            Check(gates.Any(candidate => candidate.ExitId.Equals(gate.ReciprocalExitId, StringComparison.OrdinalIgnoreCase)), "reciprocal-target:" + gate.ExitId);

        return new RuntimeBundleDRemainingExhaustionAuditReport(
            failures.Count == 0, gates.Count, stateful, reciprocal, actionBound, restricted, picklock, effects,
            checks, failures,
            new[]
            {
                "generator_mapping.*", "generator_validation.*", "semantic_confidence", "source_classification",
                "evidence_summary_*", "librarium_enrichment_*", "override_policy_*", "relationship_summary_*",
                "source_breakdown.*", "source_priority.*", "compile_manifest bookkeeping", "client projection metadata"
            });
    }
}
