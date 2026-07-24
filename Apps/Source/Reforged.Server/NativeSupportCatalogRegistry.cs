using System;
using System.Collections.Generic;

namespace MajorMud.Reforged.Server;

public static class NativeSupportCatalogRegistry
{
    public const string EvidenceId = "native-support-catalog-0020";
    public const int HeaderFileCount = 4;
    public const int MessageSymbolCount = 414;
    public const int ModuleDescriptorCount = 2;
    public const int UicOwnedFileCount = 3;

    public static IReadOnlyDictionary<string, int> MessageCatalogCounts { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["WCCMMHLP.h"] = 263,
            ["WCCMMPLS.h"] = 65,
            ["WCCMMUD.h"] = 78,
            ["WCCTEXT.h"] = 8,
        };

    public static void Validate()
    {
        if (HeaderFileCount <= 0 || MessageSymbolCount <= 0 || ModuleDescriptorCount <= 0 || UicOwnedFileCount <= 0)
            throw new InvalidOperationException("Native support evidence is incomplete.");
    }
}
