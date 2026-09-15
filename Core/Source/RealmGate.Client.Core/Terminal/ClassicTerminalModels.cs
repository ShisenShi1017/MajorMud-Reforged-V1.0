namespace RealmGate.Client.Terminal;

public enum ClassicTerminalColor : byte
{
    Black = 0,
    DarkRed = 1,
    DarkGreen = 2,
    DarkYellow = 3,
    DarkBlue = 4,
    DarkMagenta = 5,
    DarkCyan = 6,
    Gray = 7,
    DarkGray = 8,
    Red = 9,
    Green = 10,
    Yellow = 11,
    Blue = 12,
    Magenta = 13,
    Cyan = 14,
    White = 15
}

public readonly record struct ClassicTerminalRgb(byte Red, byte Green, byte Blue);

public static class ClassicTerminalPalette
{
    private static readonly ClassicTerminalRgb[] Colors =
    [
        new(0x00, 0x00, 0x00),
        new(0x80, 0x00, 0x00),
        new(0x00, 0x80, 0x00),
        new(0x80, 0x80, 0x00),
        new(0x00, 0x00, 0x80),
        new(0x80, 0x00, 0x80),
        new(0x00, 0x80, 0x80),
        new(0xC0, 0xC0, 0xC0),
        new(0x80, 0x80, 0x80),
        new(0xFF, 0x00, 0x00),
        new(0x00, 0xFF, 0x00),
        new(0xFF, 0xFF, 0x00),
        new(0x00, 0x00, 0xFF),
        new(0xFF, 0x00, 0xFF),
        new(0x00, 0xFF, 0xFF),
        new(0xFF, 0xFF, 0xFF)
    ];

    /// <summary>
    /// Original WCC/DOS ANSI treats SGR 1 as foreground intensity. For base
    /// colors 0-7 this selects the bright 8-15 palette entry; backgrounds are
    /// unaffected. Explicit bright SGR colors (90-97) already arrive as 8-15.
    /// </summary>
    public static ClassicTerminalColor ResolveForeground(ClassicTerminalColor color, bool intense) =>
        intense && (byte)color < 8
            ? (ClassicTerminalColor)((byte)color + 8)
            : color;

    public static ClassicTerminalRgb ResolveRgb(ClassicTerminalColor color) => Colors[(byte)color];
}

public sealed record ClassicTerminalRun(
    string Text,
    ClassicTerminalColor Foreground,
    ClassicTerminalColor Background,
    ClassicTerminalRgb? ForegroundRgb,
    ClassicTerminalRgb? BackgroundRgb,
    bool Bold,
    bool Inverse,
    bool Underline,
    bool Blink,
    bool Concealed);

public sealed record ClassicTerminalLine(IReadOnlyList<ClassicTerminalRun> Runs);

public sealed class ClassicTerminalSnapshot
{
    public static ClassicTerminalSnapshot Empty { get; } = new(80, 25, 0, 0, true, 0, []);

    public ClassicTerminalSnapshot(
        int columns,
        int rows,
        int cursorColumn,
        int cursorRow,
        bool cursorVisible,
        long revision,
        IReadOnlyList<ClassicTerminalLine> lines)
    {
        Columns = columns;
        Rows = rows;
        CursorColumn = cursorColumn;
        CursorRow = cursorRow;
        CursorVisible = cursorVisible;
        Revision = revision;
        Lines = lines;
    }

    public int Columns { get; }
    public int Rows { get; }
    public int CursorColumn { get; }
    public int CursorRow { get; }
    public bool CursorVisible { get; }
    public long Revision { get; }
    public IReadOnlyList<ClassicTerminalLine> Lines { get; }
}