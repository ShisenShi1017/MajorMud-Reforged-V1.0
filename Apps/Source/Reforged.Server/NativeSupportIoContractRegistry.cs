using System;

namespace MajorMud.Reforged.Server;

public static class NativeSupportIoContractRegistry
{
    public const string EvidenceId = "native-support-io-contracts-0024";
    public const int ProvenContractCount = 4;
    public const int BinScreenBytes = 4000;
    public const int BinColumns = 80;
    public const int BinRows = 25;
    public const int BinBytesPerCell = 2;

    public static bool InterpretAccessResult(int nativeResult) => nativeResult == 0;
    public static bool InterpretTfsOpenResult(int nativeResult) => nativeResult == 0;
    public static bool IsCompleteBinRead(int itemCount) => itemCount == 1;

    public static void Validate()
    {
        if (BinColumns * BinRows * BinBytesPerCell != BinScreenBytes)
            throw new InvalidOperationException("Native BIN geometry mismatch.");
        if (ProvenContractCount != 4)
            throw new InvalidOperationException("Native support I/O contract count mismatch.");
    }
}
