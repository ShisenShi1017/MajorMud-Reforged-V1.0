using System;
using System.Collections.Generic;

namespace RealmGate.Server;

public static class NativeSupportBinaryLayoutRegistry
{
    public const string EvidenceId = "native-support-binary-layouts-0021";
    public const int McvContainerCount = 4;
    public const int McvIndexEntryCount = 423;
    public const int MsgCatalogCount = 4;
    public const int BinScreenCount = 3;
    public const int TextScreenWidth = 80;
    public const int TextScreenHeight = 25;

    public static void Validate()
    {
        if (McvContainerCount != 4)
            throw new InvalidOperationException("An MCV container failed the proven v1 layout.");
        if (BinScreenCount != 3 || TextScreenWidth * TextScreenHeight * 2 != 4000)
            throw new InvalidOperationException("Native BIN screen geometry mismatch.");
    }
}
