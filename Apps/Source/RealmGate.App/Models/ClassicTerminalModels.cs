using System.Collections.Generic;

namespace RealmGate.App.Models;

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

public sealed record ClassicTerminalLine(
    IReadOnlyList<ClassicTerminalRun> Runs);

public sealed class ClassicTerminalSnapshot
{
    public static ClassicTerminalSnapshot Empty { get; } =
        new(80, 25, 0, 0, true, 0, []);

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
