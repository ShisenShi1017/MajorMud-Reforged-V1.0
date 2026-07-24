using System;

namespace MajorMud.Reforged.Server;

public static class NativeSupportConsumerRegistry
{
    public const string EvidenceId = "native-support-consumers-0023";
    public const int GravestoneOverrideCount = 7;
    public const int HouseDescriptionOverrideCount = 47;
    public const int StaticNativeReferenceCount = 6;
    public const int HouseFilenameFixedSuffix = 15;

    public static void Validate()
    {
        if (GravestoneOverrideCount != 7) throw new InvalidOperationException("STO override inventory mismatch.");
        if (HouseDescriptionOverrideCount < 1) throw new InvalidOperationException("HSE override inventory is empty.");
        if (StaticNativeReferenceCount < 1) throw new InvalidOperationException("Native support-file references were not recovered.");
    }
}
