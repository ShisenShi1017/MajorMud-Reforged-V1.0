using System.Globalization;
using System.Text;

namespace RealmGate.Client.Terminal;

/// <summary>
/// Stateful ANSI/VT screen shared by every RealmGate desktop frontend.
/// This is the authoritative 80x25-compatible terminal state; presentation
/// layers render snapshots and do not reinterpret escape sequences themselves.
/// </summary>
public sealed class ClassicTerminalEmulator
{
    private enum ParserState { Ground, Escape, Csi, Osc, OscEscape }

    private readonly object _gate = new();
    private TerminalCell[,] _cells;
    private int _columns;
    private int _rows;
    private int _cursorColumn;
    private int _cursorRow;
    private int _savedColumn;
    private int _savedRow;
    private int _scrollTop;
    private int _scrollBottom;
    private bool _cursorVisible = true;
    private bool _originMode;
    private bool _autoWrap = true;
    private bool _wrapPending;
    private long _revision;
    private ParserState _parserState;
    private readonly StringBuilder _csi = new();

    private ClassicTerminalColor _foreground = ClassicTerminalColor.Gray;
    private ClassicTerminalColor _background = ClassicTerminalColor.Black;
    private ClassicTerminalRgb? _foregroundRgb;
    private ClassicTerminalRgb? _backgroundRgb;
    private bool _bold;
    private bool _inverse;
    private bool _underline;
    private bool _blink;
    private bool _concealed;

    private ClassicTerminalColor _savedForeground = ClassicTerminalColor.Gray;
    private ClassicTerminalColor _savedBackground = ClassicTerminalColor.Black;
    private ClassicTerminalRgb? _savedForegroundRgb;
    private ClassicTerminalRgb? _savedBackgroundRgb;
    private bool _savedBold;
    private bool _savedInverse;
    private bool _savedUnderline;
    private bool _savedBlink;
    private bool _savedConcealed;

    public ClassicTerminalEmulator(int columns = 80, int rows = 25)
    {
        _columns = Math.Clamp(columns, 20, 300);
        _rows = Math.Clamp(rows, 5, 120);
        _cells = new TerminalCell[_rows, _columns];
        _scrollBottom = _rows - 1;
        ClearAll();
    }

    public ClassicTerminalSnapshot Reset()
    {
        lock (_gate)
        {
            _cursorColumn = 0;
            _cursorRow = 0;
            _savedColumn = 0;
            _savedRow = 0;
            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            _cursorVisible = true;
            _originMode = false;
            _autoWrap = true;
            _wrapPending = false;
            _parserState = ParserState.Ground;
            _csi.Clear();
            ResetAttributes();
            ClearAll();
            _revision++;
            return CreateSnapshotCore();
        }
    }

    public ClassicTerminalSnapshot Resize(int columns, int rows)
    {
        lock (_gate)
        {
            columns = Math.Clamp(columns, 20, 300);
            rows = Math.Clamp(rows, 5, 120);
            if (columns == _columns && rows == _rows) return CreateSnapshotCore();

            var next = new TerminalCell[rows, columns];
            Fill(next, BlankCell());
            var copyRows = Math.Min(rows, _rows);
            var copyColumns = Math.Min(columns, _columns);
            var oldStart = Math.Max(0, _rows - copyRows);
            var newStart = Math.Max(0, rows - copyRows);
            for (var row = 0; row < copyRows; row++)
            for (var column = 0; column < copyColumns; column++)
                next[newStart + row, column] = _cells[oldStart + row, column];

            _cells = next;
            _columns = columns;
            _rows = rows;
            _cursorColumn = Math.Clamp(_cursorColumn, 0, _columns - 1);
            _cursorRow = Math.Clamp(_cursorRow - oldStart + newStart, 0, _rows - 1);
            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            _wrapPending = false;
            _revision++;
            return CreateSnapshotCore();
        }
    }

    public ClassicTerminalSnapshot Process(ReadOnlySpan<char> text)
    {
        lock (_gate)
        {
            foreach (var value in text) ProcessCharacter(value);
            _revision++;
            return CreateSnapshotCore();
        }
    }

    public ClassicTerminalSnapshot CreateSnapshot()
    {
        lock (_gate) return CreateSnapshotCore();
    }

    private void ProcessCharacter(char value)
    {
        switch (_parserState)
        {
            case ParserState.Ground:
                ProcessGround(value);
                break;
            case ParserState.Escape:
                ProcessEscape(value);
                break;
            case ParserState.Csi:
                if (value is >= '@' and <= '~')
                {
                    ExecuteCsi(value, _csi.ToString());
                    _csi.Clear();
                    _parserState = ParserState.Ground;
                }
                else if (_csi.Length < 96) _csi.Append(value);
                break;
            case ParserState.Osc:
                if (value == '\a') _parserState = ParserState.Ground;
                else if (value == '\x1B') _parserState = ParserState.OscEscape;
                break;
            case ParserState.OscEscape:
                _parserState = value == '\\' ? ParserState.Ground : ParserState.Osc;
                break;
        }
    }

    private void ProcessGround(char value)
    {
        switch (value)
        {
            case '\x1B': _parserState = ParserState.Escape; break;
            case '\u009B': _csi.Clear(); _parserState = ParserState.Csi; break;
            case '\r': _cursorColumn = 0; _wrapPending = false; break;
            case '\n':
            case '\v':
            case '\f': LineFeed(); break;
            case '\b': _cursorColumn = Math.Max(0, _cursorColumn - 1); _wrapPending = false; break;
            case '\t':
                _cursorColumn = Math.Min(_columns - 1, ((_cursorColumn / 8) + 1) * 8);
                _wrapPending = false;
                break;
            case '\0':
            case '\a': break;
            default:
                if (!char.IsControl(value)) WriteCharacter(value);
                break;
        }
    }

    private void ProcessEscape(char value)
    {
        _parserState = ParserState.Ground;
        switch (value)
        {
            case '[': _csi.Clear(); _parserState = ParserState.Csi; break;
            case ']': _parserState = ParserState.Osc; break;
            case '7': SaveCursor(); break;
            case '8': RestoreCursor(); break;
            case 'D': LineFeed(); break;
            case 'E': _cursorColumn = 0; LineFeed(); break;
            case 'M': ReverseIndex(); break;
            case 'c': Reset(); break;
        }
    }

    private void ExecuteCsi(char command, string parameters)
    {
        var privateMode = parameters.StartsWith("?", StringComparison.Ordinal);
        if (privateMode) parameters = parameters[1..];
        var values = ParseParameters(parameters);
        int Parameter(int index, int fallback = 1) => index >= values.Count || values[index] <= 0 ? fallback : values[index];

        switch (command)
        {
            case 'A': _cursorRow = Math.Max(VerticalTop, _cursorRow - Parameter(0)); break;
            case 'B': _cursorRow = Math.Min(VerticalBottom, _cursorRow + Parameter(0)); break;
            case 'C': _cursorColumn = Math.Min(_columns - 1, _cursorColumn + Parameter(0)); break;
            case 'D': _cursorColumn = Math.Max(0, _cursorColumn - Parameter(0)); break;
            case 'E': _cursorRow = Math.Min(VerticalBottom, _cursorRow + Parameter(0)); _cursorColumn = 0; break;
            case 'F': _cursorRow = Math.Max(VerticalTop, _cursorRow - Parameter(0)); _cursorColumn = 0; break;
            case 'G':
            case '`': _cursorColumn = Math.Clamp(Parameter(0) - 1, 0, _columns - 1); break;
            case 'H':
            case 'f':
                _cursorRow = ResolveAbsoluteRow(Parameter(0));
                _cursorColumn = Math.Clamp(Parameter(1) - 1, 0, _columns - 1);
                break;
            case 'd': _cursorRow = ResolveAbsoluteRow(Parameter(0)); break;
            case 'J': EraseDisplay(values.Count == 0 ? 0 : values[0]); break;
            case 'K': EraseLine(values.Count == 0 ? 0 : values[0]); break;
            case 'm': ApplySgr(values); break;
            case 's': SaveCursor(); break;
            case 'u': RestoreCursor(); break;
            case 'r': SetScrollRegion(values); break;
            case 'S': ScrollUp(Parameter(0)); break;
            case 'T': ScrollDown(Parameter(0)); break;
            case '@': InsertCharacters(Parameter(0)); break;
            case 'P': DeleteCharacters(Parameter(0)); break;
            case 'X': EraseCharacters(Parameter(0)); break;
            case 'L': InsertLines(Parameter(0)); break;
            case 'M': DeleteLines(Parameter(0)); break;
            case 'h': if (privateMode) SetPrivateModes(values, true); break;
            case 'l': if (privateMode) SetPrivateModes(values, false); break;
        }
        _wrapPending = false;
    }

    private void WriteCharacter(char value)
    {
        if (_wrapPending && _autoWrap)
        {
            _cursorColumn = 0;
            LineFeed();
            _wrapPending = false;
        }
        _cells[_cursorRow, _cursorColumn] = new TerminalCell(value, _foreground, _background, _foregroundRgb, _backgroundRgb, _bold, _inverse, _underline, _blink, _concealed);
        if (_cursorColumn == _columns - 1) _wrapPending = _autoWrap;
        else _cursorColumn++;
    }

    private void LineFeed()
    {
        _wrapPending = false;
        if (_cursorRow == _scrollBottom) ScrollUp(1);
        else _cursorRow = Math.Min(_rows - 1, _cursorRow + 1);
    }

    private void ReverseIndex()
    {
        _wrapPending = false;
        if (_cursorRow == _scrollTop) ScrollDown(1);
        else _cursorRow = Math.Max(0, _cursorRow - 1);
    }

    private void ScrollUp(int count)
    {
        count = Math.Clamp(count, 1, _scrollBottom - _scrollTop + 1);
        for (var pass = 0; pass < count; pass++)
        {
            for (var row = _scrollTop; row < _scrollBottom; row++)
            for (var column = 0; column < _columns; column++)
                _cells[row, column] = _cells[row + 1, column];
            ClearRow(_scrollBottom);
        }
    }

    private void ScrollDown(int count)
    {
        count = Math.Clamp(count, 1, _scrollBottom - _scrollTop + 1);
        for (var pass = 0; pass < count; pass++)
        {
            for (var row = _scrollBottom; row > _scrollTop; row--)
            for (var column = 0; column < _columns; column++)
                _cells[row, column] = _cells[row - 1, column];
            ClearRow(_scrollTop);
        }
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 1:
                for (var row = 0; row < _cursorRow; row++) ClearRow(row);
                for (var column = 0; column <= _cursorColumn; column++) _cells[_cursorRow, column] = BlankCell();
                break;
            case 2:
            case 3:
                ClearAll();
                break;
            default:
                for (var column = _cursorColumn; column < _columns; column++) _cells[_cursorRow, column] = BlankCell();
                for (var row = _cursorRow + 1; row < _rows; row++) ClearRow(row);
                break;
        }
    }

    private void EraseLine(int mode)
    {
        switch (mode)
        {
            case 1:
                for (var column = 0; column <= _cursorColumn; column++) _cells[_cursorRow, column] = BlankCell();
                break;
            case 2: ClearRow(_cursorRow); break;
            default:
                for (var column = _cursorColumn; column < _columns; column++) _cells[_cursorRow, column] = BlankCell();
                break;
        }
    }

    private void EraseCharacters(int count)
    {
        count = Math.Clamp(count, 1, _columns - _cursorColumn);
        for (var column = _cursorColumn; column < _cursorColumn + count; column++) _cells[_cursorRow, column] = BlankCell();
    }

    private void InsertCharacters(int count)
    {
        count = Math.Clamp(count, 1, _columns - _cursorColumn);
        for (var column = _columns - 1; column >= _cursorColumn + count; column--) _cells[_cursorRow, column] = _cells[_cursorRow, column - count];
        for (var column = _cursorColumn; column < _cursorColumn + count; column++) _cells[_cursorRow, column] = BlankCell();
    }

    private void DeleteCharacters(int count)
    {
        count = Math.Clamp(count, 1, _columns - _cursorColumn);
        for (var column = _cursorColumn; column < _columns - count; column++) _cells[_cursorRow, column] = _cells[_cursorRow, column + count];
        for (var column = _columns - count; column < _columns; column++) _cells[_cursorRow, column] = BlankCell();
    }

    private void InsertLines(int count)
    {
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom) return;
        count = Math.Clamp(count, 1, _scrollBottom - _cursorRow + 1);
        for (var row = _scrollBottom; row >= _cursorRow + count; row--)
        for (var column = 0; column < _columns; column++) _cells[row, column] = _cells[row - count, column];
        for (var row = _cursorRow; row < _cursorRow + count; row++) ClearRow(row);
    }

    private void DeleteLines(int count)
    {
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom) return;
        count = Math.Clamp(count, 1, _scrollBottom - _cursorRow + 1);
        for (var row = _cursorRow; row <= _scrollBottom - count; row++)
        for (var column = 0; column < _columns; column++) _cells[row, column] = _cells[row + count, column];
        for (var row = _scrollBottom - count + 1; row <= _scrollBottom; row++) ClearRow(row);
    }

    private void SetScrollRegion(IReadOnlyList<int> values)
    {
        var top = values.Count > 0 && values[0] > 0 ? values[0] - 1 : 0;
        var bottom = values.Count > 1 && values[1] > 0 ? values[1] - 1 : _rows - 1;
        if (top < 0 || bottom >= _rows || top >= bottom) return;
        _scrollTop = top;
        _scrollBottom = bottom;
        _cursorColumn = 0;
        _cursorRow = _originMode ? _scrollTop : 0;
    }

    private int VerticalTop => _originMode ? _scrollTop : 0;
    private int VerticalBottom => _originMode ? _scrollBottom : _rows - 1;

    private int ResolveAbsoluteRow(int oneBasedRow)
    {
        var requested = Math.Max(1, oneBasedRow) - 1;
        return _originMode ? Math.Clamp(_scrollTop + requested, _scrollTop, _scrollBottom) : Math.Clamp(requested, 0, _rows - 1);
    }

    private void SetPrivateModes(IReadOnlyList<int> modes, bool enabled)
    {
        foreach (var mode in modes)
        {
            switch (mode)
            {
                case 6:
                    _originMode = enabled;
                    _cursorColumn = 0;
                    _cursorRow = enabled ? _scrollTop : 0;
                    break;
                case 7:
                    _autoWrap = enabled;
                    if (!enabled) _wrapPending = false;
                    break;
                case 25: _cursorVisible = enabled; break;
            }
        }
    }

    private void ApplySgr(IReadOnlyList<int> values)
    {
        if (values.Count == 0) { ResetAttributes(); return; }
        for (var index = 0; index < values.Count; index++)
        {
            var code = values[index];
            switch (code)
            {
                case 0: ResetAttributes(); break;
                case 1: _bold = true; break;
                case 2:
                case 21:
                case 22: _bold = false; break;
                case 4: _underline = true; break;
                case 5:
                case 6: _blink = true; break;
                case 7: _inverse = true; break;
                case 8: _concealed = true; break;
                case 24: _underline = false; break;
                case 25: _blink = false; break;
                case 27: _inverse = false; break;
                case 28: _concealed = false; break;
                case 39: _foreground = ClassicTerminalColor.Gray; _foregroundRgb = null; break;
                case 49: _background = ClassicTerminalColor.Black; _backgroundRgb = null; break;
                case 38:
                case 48: ApplyExtendedColor(values, ref index, code == 38); break;
                default:
                    if (code is >= 30 and <= 37) { _foreground = (ClassicTerminalColor)(code - 30); _foregroundRgb = null; }
                    else if (code is >= 40 and <= 47) { _background = (ClassicTerminalColor)(code - 40); _backgroundRgb = null; }
                    else if (code is >= 90 and <= 97) { _foreground = (ClassicTerminalColor)(code - 90 + 8); _foregroundRgb = null; }
                    else if (code is >= 100 and <= 107) { _background = (ClassicTerminalColor)(code - 100 + 8); _backgroundRgb = null; }
                    break;
            }
        }
    }

    private void ApplyExtendedColor(IReadOnlyList<int> values, ref int index, bool foreground)
    {
        if (index + 1 >= values.Count) return;
        ClassicTerminalRgb? rgb = null;
        var mode = values[index + 1];
        if (mode == 5 && index + 2 < values.Count)
        {
            rgb = ResolveIndexedColor(Math.Clamp(values[index + 2], 0, 255));
            index += 2;
        }
        else if (mode == 2 && index + 4 < values.Count)
        {
            rgb = new ClassicTerminalRgb((byte)Math.Clamp(values[index + 2], 0, 255), (byte)Math.Clamp(values[index + 3], 0, 255), (byte)Math.Clamp(values[index + 4], 0, 255));
            index += 4;
        }
        if (rgb is null) return;
        if (foreground) _foregroundRgb = rgb;
        else _backgroundRgb = rgb;
    }

    private static ClassicTerminalRgb ResolveIndexedColor(int value)
    {
        ReadOnlySpan<ClassicTerminalRgb> ansi =
        [
            new(0,0,0), new(128,0,0), new(0,128,0), new(128,128,0),
            new(0,0,128), new(128,0,128), new(0,128,128), new(192,192,192),
            new(128,128,128), new(255,0,0), new(0,255,0), new(255,255,0),
            new(0,0,255), new(255,0,255), new(0,255,255), new(255,255,255)
        ];
        if (value < 16) return ansi[value];
        if (value < 232)
        {
            var cube = value - 16;
            var red = cube / 36;
            var green = cube / 6 % 6;
            var blue = cube % 6;
            static byte Component(int component) => (byte)(component == 0 ? 0 : 55 + component * 40);
            return new ClassicTerminalRgb(Component(red), Component(green), Component(blue));
        }
        var gray = (byte)(8 + (value - 232) * 10);
        return new ClassicTerminalRgb(gray, gray, gray);
    }

    private void ResetAttributes()
    {
        _foreground = ClassicTerminalColor.Gray;
        _background = ClassicTerminalColor.Black;
        _foregroundRgb = null;
        _backgroundRgb = null;
        _bold = _inverse = _underline = _blink = _concealed = false;
    }

    private void SaveCursor()
    {
        _savedColumn = _cursorColumn;
        _savedRow = _cursorRow;
        _savedForeground = _foreground;
        _savedBackground = _background;
        _savedForegroundRgb = _foregroundRgb;
        _savedBackgroundRgb = _backgroundRgb;
        _savedBold = _bold;
        _savedInverse = _inverse;
        _savedUnderline = _underline;
        _savedBlink = _blink;
        _savedConcealed = _concealed;
    }

    private void RestoreCursor()
    {
        _cursorColumn = Math.Clamp(_savedColumn, 0, _columns - 1);
        _cursorRow = Math.Clamp(_savedRow, 0, _rows - 1);
        _foreground = _savedForeground;
        _background = _savedBackground;
        _foregroundRgb = _savedForegroundRgb;
        _backgroundRgb = _savedBackgroundRgb;
        _bold = _savedBold;
        _inverse = _savedInverse;
        _underline = _savedUnderline;
        _blink = _savedBlink;
        _concealed = _savedConcealed;
        _wrapPending = false;
    }

    private static List<int> ParseParameters(string value)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(value)) return result;
        foreach (var part in value.Split(';'))
        {
            var cleaned = new string(part.Where(char.IsDigit).ToArray());
            result.Add(int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0);
        }
        return result;
    }

    private void ClearAll() => Fill(_cells, BlankCell());
    private void ClearRow(int row)
    {
        for (var column = 0; column < _columns; column++) _cells[row, column] = BlankCell();
    }

    private TerminalCell BlankCell() => new(' ', _foreground, _background, _foregroundRgb, _backgroundRgb, _bold, _inverse, _underline, _blink, _concealed);

    private static void Fill(TerminalCell[,] cells, TerminalCell value)
    {
        for (var row = 0; row < cells.GetLength(0); row++)
        for (var column = 0; column < cells.GetLength(1); column++) cells[row, column] = value;
    }

    private ClassicTerminalSnapshot CreateSnapshotCore()
    {
        var lines = new List<ClassicTerminalLine>(_rows);
        for (var row = 0; row < _rows; row++)
        {
            var runs = new List<ClassicTerminalRun>();
            var start = 0;
            var style = _cells[row, 0];
            for (var column = 1; column <= _columns; column++)
            {
                var changed = column == _columns || !_cells[row, column].SameStyle(style);
                if (!changed) continue;
                var text = new char[column - start];
                for (var index = start; index < column; index++) text[index - start] = _cells[row, index].Value;
                runs.Add(new ClassicTerminalRun(new string(text), style.Foreground, style.Background, style.ForegroundRgb, style.BackgroundRgb, style.Bold, style.Inverse, style.Underline, style.Blink, style.Concealed));
                if (column < _columns) { start = column; style = _cells[row, column]; }
            }
            lines.Add(new ClassicTerminalLine(runs));
        }
        return new ClassicTerminalSnapshot(_columns, _rows, _cursorColumn, _cursorRow, _cursorVisible, _revision, lines);
    }

    private readonly record struct TerminalCell(
        char Value,
        ClassicTerminalColor Foreground,
        ClassicTerminalColor Background,
        ClassicTerminalRgb? ForegroundRgb,
        ClassicTerminalRgb? BackgroundRgb,
        bool Bold,
        bool Inverse,
        bool Underline,
        bool Blink,
        bool Concealed)
    {
        public bool SameStyle(TerminalCell other) =>
            Foreground == other.Foreground && Background == other.Background &&
            ForegroundRgb == other.ForegroundRgb && BackgroundRgb == other.BackgroundRgb &&
            Bold == other.Bold && Inverse == other.Inverse && Underline == other.Underline &&
            Blink == other.Blink && Concealed == other.Concealed;
    }
}