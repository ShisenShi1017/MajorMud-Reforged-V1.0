namespace Reforged.Server;

public static class NativeBinConsoleRenderingRegistry
{
    public const string EvidenceId = "native_bin_console_rendering_0031";
    public const int ScreenWidth = 80;
    public const int CellStrideBytes = 2;

    public static byte MapAttribute(byte attribute, bool alternateMode)
    {
        if (alternateMode)
            return (byte)(attribute & 0x7F);

        if ((attribute & 0x70) == 0x70)
            return 0x70;

        return (byte)((attribute & 0x88) | 0x07);
    }

    public static (short X, short Y) MapByteOffsetToCoordinate(int byteOffset)
    {
        var aligned = byteOffset & ~1;
        var cellIndex = aligned / CellStrideBytes;
        return ((short)(cellIndex % ScreenWidth), (short)(cellIndex / ScreenWidth));
    }

    public static int CellCountForByteCount(int byteCount) => byteCount >> 1;

    public static bool CharacterWritePrecedesAttributeWrite => true;
    public static bool NativeWriterChecksApiResults => false;
}
