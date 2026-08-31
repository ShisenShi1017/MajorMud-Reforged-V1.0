using System;
using System.Collections.Generic;

namespace MajorMud.Reforged.Server;

/// <summary>Exact supplied-WGSERV WCC artifact inventory gate.</summary>
public static class NativeWccArtifactExhaustionRegistry
{
    public const string EvidenceId = "native_wcc_available_artifact_exhaustion_0034";
    public static readonly bool AvailableWccArtifactsExhausted = true;
    public static readonly bool SuppliedWgservWccInventoryResolved = true;
    public static readonly int SuppliedWgservFileEntryCount = 3605;
    public static readonly int SuppliedWgservWccArtifactCount = 155;

    public static IReadOnlyDictionary<string, int> SuppliedWccExtensionCounts { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [".HSE"] = 53, [".CV_"] = 19, [".VIR"] = 16, [".DAT"] = 16,
            [".AD"] = 7, [".ZIP"] = 5, [".H"] = 4, [".MCV"] = 4,
            [".MSG"] = 4, [".BIN"] = 3, [".SYS"] = 2, [".TXT"] = 2,
            [".DLL"] = 2, [".DOC"] = 2, [".MDF"] = 2, [".REL"] = 2,
            [".NOT"] = 2, [".FLG"] = 1, [".RES"] = 1, [".ANS"] = 1,
            [".INI"] = 1, [".LIC"] = 1, [".LOG"] = 1, [".RLN"] = 1,
            [".UIC"] = 1, [".EXE"] = 1, [".NU1"] = 1
        };

    public static IReadOnlySet<string> HostOwnedRemainingDomains { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "authorization-token-generation", "host-collation-and-token-comparison",
        "hse-sto-external-consumer", "rng", "persistence", "scheduling",
        "message-formatting", "locking", "polling", "callbacks", "host-global-types"
    };

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (!AvailableWccArtifactsExhausted || !SuppliedWgservWccInventoryResolved) failures.Add("wcc-artifact-exhaustion-demoted");
        if (SuppliedWgservFileEntryCount != 3605) failures.Add("wgserv-entry-count-mismatch");
        if (SuppliedWgservWccArtifactCount != 155) failures.Add("wgserv-wcc-count-mismatch");
        if (SuppliedWccExtensionCounts.Values.Sum() != SuppliedWgservWccArtifactCount) failures.Add("wgserv-wcc-extension-sum-mismatch");
        if (SuppliedWccExtensionCounts[".DAT"] != 16 || SuppliedWccExtensionCounts[".HSE"] != 53) failures.Add("wgserv-wcc-family-count-mismatch");
        return failures;
    }

    public static void RequireWorldgroupEvidence(string domain, string? evidenceId)
    {
        if (!HostOwnedRemainingDomains.Contains(domain))
            throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown post-WCC native domain.");
        if (string.IsNullOrWhiteSpace(evidenceId))
            throw new InvalidOperationException($"{domain} is beyond the exhausted WCC artifact set and requires Worldgroup or new original-binary evidence.");
    }
}
