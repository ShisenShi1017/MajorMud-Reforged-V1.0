using System.Collections.Generic;

namespace MajorMud.Reforged.Server;

public static class NativeSupportStaticReferenceRegistry
{
    public const string EvidenceId = "native_support_static_reference_matrix_0033";

    public static bool HasDirectHseFilenameLiteral => false;
    public static bool HasDirectStoFilenameLiteral => false;

    public static IReadOnlySet<string> DirectlyNamedWccFamilies { get; } = new HashSet<string>
    {
        "MCV", "MDF", "SYS", "REL", "RLN", "NOT", "BIN", "ANS"
    };

    public static void RequireExternalHseStoConsumerEvidence(string? evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
            throw new System.InvalidOperationException("HSE/STO native fallback is not present as a direct literal path in the supplied module set.");
    }
}
