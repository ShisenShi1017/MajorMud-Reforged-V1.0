using System;
using System.Collections.Generic;

namespace MajorMud.Reforged.Server;

public static class NativeWccArtifactExhaustionRegistry
{
    public const string EvidenceId = "native_wcc_available_artifact_exhaustion_0034";
    public const bool AvailableWccArtifactsExhausted = true;

    public static IReadOnlySet<string> HostOwnedRemainingDomains { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "authorization-token-generation",
        "host-collation-and-token-comparison",
        "hse-sto-external-consumer",
        "rng",
        "persistence",
        "scheduling",
        "message-formatting",
        "locking",
        "polling",
        "callbacks",
        "host-global-types"
    };

    public static void RequireWorldgroupEvidence(string domain, string? evidenceId)
    {
        if (!HostOwnedRemainingDomains.Contains(domain))
            throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown post-WCC native domain.");
        if (string.IsNullOrWhiteSpace(evidenceId))
            throw new InvalidOperationException($"{domain} is beyond the exhausted WCC artifact set and requires Worldgroup or new original-binary evidence.");
    }
}
