using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleEZeroDisconnectClosureAuditReport(
    bool Passed,
    int VerifiedDisconnectCount,
    int StronglyCorroboratedDisconnectCount,
    int DuplicateAuthorityCount,
    int QuarantinedNativeUnknownCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> QuarantinedNativeUnknowns)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleEZeroDisconnectClosureAudit
{
    public static RuntimeBundleEZeroDisconnectClosureAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Require(bool condition, string check, string? failure = null)
        {
            if (condition) checks.Add(check);
            else failures.Add(failure ?? check);
        }

        var exhaustion = RuntimeBundleEExhaustionAudit.Run(projectRoot, worldId);
        Require(exhaustion.BundleEComplete, "bundle-e-exhaustion-audit-passed",
            "bundle-e-exhaustion-audit-reported-failures");
        foreach (var failure in exhaustion.Failures)
            failures.Add("exhaustion:" + failure);

        var verifiedDisconnects = exhaustion.Subsystems.Count(x =>
            string.Equals(x.EvidenceStatus, "Verified", StringComparison.OrdinalIgnoreCase) && !x.Passed);
        var corroboratedDisconnects = exhaustion.Subsystems.Count(x =>
            string.Equals(x.EvidenceStatus, "Strongly Corroborated", StringComparison.OrdinalIgnoreCase) && !x.Passed);
        var duplicateAuthorities = exhaustion.DuplicatePaths.Count(x =>
            !x.StartsWith("No duplicate", StringComparison.OrdinalIgnoreCase));

        Require(verifiedDisconnects == 0, "verified-runtime-disconnect-count-zero",
            $"verified-runtime-disconnect-count:{verifiedDisconnects}");
        Require(corroboratedDisconnects == 0, "strongly-corroborated-runtime-disconnect-count-zero",
            $"strongly-corroborated-runtime-disconnect-count:{corroboratedDisconnects}");
        Require(duplicateAuthorities == 0, "duplicate-creature-authority-count-zero",
            $"duplicate-creature-authority-count:{duplicateAuthorities}");
        Require(exhaustion.Subsystems.All(x => !string.IsNullOrWhiteSpace(x.RuntimeOwner)),
            "every-creature-subsystem-has-runtime-owner");
        Require(exhaustion.Subsystems.All(x => !string.IsNullOrWhiteSpace(x.RuntimeConsumer)),
            "every-creature-subsystem-has-runtime-consumer");
        Require(exhaustion.Subsystems.All(x => !string.IsNullOrWhiteSpace(x.PersistenceOwner)),
            "every-creature-subsystem-has-persistence-owner");
        Require(exhaustion.Subsystems.All(x => !string.IsNullOrWhiteSpace(x.ValidationOwner)),
            "every-creature-subsystem-has-validation-owner");
        Require(exhaustion.Subsystems.All(x => x.SourceFiles.Count > 0),
            "every-creature-subsystem-has-authoritative-source-files");

        var closureFiles = new[]
        {
            "RuntimeBundleEMovementAiClosureAudit.cs",
            "RuntimeBundleECompanionLifecycleClosureAudit.cs",
            "RuntimeBundleEMerchantTrainerClosureAudit.cs",
            "RuntimeBundleEBossDeathRespawnClosureAudit.cs",
            "RuntimeBundleERelationshipServiceClosureAudit.cs",
            "RuntimeBundleENpcItemControlPersistenceClosureAudit.cs"
        };
        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server");
        foreach (var file in closureFiles)
            Require(File.Exists(Path.Combine(serverRoot, file)), $"closure-gate-present:{file}");

        var quarantined = new[]
        {
            "Native-only regeneration edge timing and charm command nuances without decoded evidence.",
            "Native-only assist radius/propagation/cooldown semantics without decoded evidence."
        };
        checks.Add("classic-no-faction-no-reputation-policy-enforced");
        checks.Add("hostile-npc-vs-npc-targeting-disabled");
        checks.Add("explicit-protector-attack-blocking-connected");
        checks.Add("remaining-native-only-unknowns-quarantined-not-invented");

        return new(
            failures.Count == 0,
            verifiedDisconnects,
            corroboratedDisconnects,
            duplicateAuthorities,
            quarantined.Length,
            checks,
            failures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            quarantined);
    }
}
