namespace Reforged.Server;

public static class NativeBinScreenNormalizationRegistry
{
    public const string EvidenceId = "native_bin_screen_normalization_0030";
    public const int Columns = 80;
    public const int Rows = 25;
    public const int CellCount = 2000;
    public const int BytesPerCell = 2;
    public const int BufferBytes = 4000;
    public const byte BlinkBit = 0x08;
    public const byte LowerHalfBlock = 0xDC;
    public const byte UpperHalfBlock = 0xDF;
    public const byte Space = 0x20;

    public static bool RequiresSpaceReplacement(byte character, byte attribute)
        => (attribute & BlinkBit) == 0
           && (character == LowerHalfBlock || character == UpperHalfBlock);

    public static bool HasExactGeometry(ReadOnlySpan<byte> screen)
        => screen.Length == BufferBytes;

    public static bool CanClaimExactPaletteMapping => false;
}
