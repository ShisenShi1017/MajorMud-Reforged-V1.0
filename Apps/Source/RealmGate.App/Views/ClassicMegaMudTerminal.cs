using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RealmGate.App.Models;

namespace RealmGate.App.Views;

public sealed class ClassicMegaMudTerminalCommandEventArgs : EventArgs
{
    public ClassicMegaMudTerminalCommandEventArgs(string command, bool sensitive)
    {
        Command = command;
        Sensitive = sensitive;
    }

    public string Command { get; }
    public bool Sensitive { get; }
}

public sealed class ClassicMegaMudTerminalKeyEventArgs : EventArgs
{
    public ClassicMegaMudTerminalKeyEventArgs(string sequence)
    {
        Sequence = sequence;
    }

    public string Sequence { get; }
}

public sealed class ClassicMegaMudTerminalSizeEventArgs : EventArgs
{
    public ClassicMegaMudTerminalSizeEventArgs(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public int Columns { get; }
    public int Rows { get; }
}

public sealed class ClassicMegaMudTerminal : Control
{
    private static readonly Brush[] Palette =
    [
        MakeBrush(0x00, 0x00, 0x00),
        MakeBrush(0x80, 0x00, 0x00),
        MakeBrush(0x00, 0x80, 0x00),
        MakeBrush(0x80, 0x80, 0x00),
        MakeBrush(0x00, 0x00, 0x80),
        MakeBrush(0x80, 0x00, 0x80),
        MakeBrush(0x00, 0x80, 0x80),
        MakeBrush(0xC0, 0xC0, 0xC0),
        MakeBrush(0x80, 0x80, 0x80),
        MakeBrush(0xFF, 0x00, 0x00),
        MakeBrush(0x00, 0xFF, 0x00),
        MakeBrush(0xFF, 0xFF, 0x00),
        MakeBrush(0x00, 0x00, 0xFF),
        MakeBrush(0xFF, 0x00, 0xFF),
        MakeBrush(0x00, 0xFF, 0xFF),
        MakeBrush(0xFF, 0xFF, 0xFF)
    ];

    public static readonly DependencyProperty SnapshotProperty =
        DependencyProperty.Register(
            nameof(Snapshot),
            typeof(ClassicTerminalSnapshot),
            typeof(ClassicMegaMudTerminal),
            new FrameworkPropertyMetadata(
                ClassicTerminalSnapshot.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPasswordModeProperty =
        DependencyProperty.Register(
            nameof(IsPasswordMode),
            typeof(bool),
            typeof(ClassicMegaMudTerminal),
            new FrameworkPropertyMetadata(false, OnPasswordModeChanged));

    private readonly DispatcherTimer _caretTimer;
    private readonly List<string> _history = [];
    private string _input = string.Empty;
    private int _caretIndex;
    private int _historyIndex = -1;
    private bool _caretVisible = true;
    private int _lastColumns;
    private int _lastRows;

    public ClassicMegaMudTerminal()
    {
        Focusable = true;
        Cursor = Cursors.IBeam;
        Background = Brushes.Black;
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Lucida Console");
        FontSize = 18;

        _caretTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500),
            DispatcherPriority.Background,
            (_, _) =>
            {
                _caretVisible = !_caretVisible;
                InvalidateVisual();
            },
            Dispatcher);

        Loaded += (_, _) =>
        {
            _caretTimer.Start();
            Focus();
        };
        Unloaded += (_, _) => _caretTimer.Stop();
    }

    public ClassicTerminalSnapshot Snapshot
    {
        get => (ClassicTerminalSnapshot)GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public bool IsPasswordMode
    {
        get => (bool)GetValue(IsPasswordModeProperty);
        set => SetValue(IsPasswordModeProperty, value);
    }

    public event EventHandler<ClassicMegaMudTerminalCommandEventArgs>? CommandSubmitted;
    public event EventHandler<ClassicMegaMudTerminalKeyEventArgs>? KeySubmitted;
    public event EventHandler<ClassicMegaMudTerminalSizeEventArgs>? TerminalSizeChanged;

    public void ClearInput()
    {
        _input = string.Empty;
        _caretIndex = 0;
        _historyIndex = -1;
        InvalidateVisual();
    }

    public bool CopyInputToClipboard()
    {
        if (IsPasswordMode || string.IsNullOrEmpty(_input))
            return false;

        Clipboard.SetText(_input);
        return true;
    }

    public bool PasteFromClipboard()
    {
        if (!Clipboard.ContainsText())
            return false;

        var text = Clipboard.GetText()
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

        if (text.Length == 0)
            return false;

        _input = _input.Insert(_caretIndex, text);
        _caretIndex += text.Length;
        _caretVisible = true;
        Focus();
        InvalidateVisual();
        return true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRectangle(
            Background ?? Brushes.Black,
            null,
            new Rect(RenderSize));

        var metrics = GetMetrics();
        var columns = Math.Max(20, (int)Math.Floor(ActualWidth / metrics.CellWidth));
        var rows = Math.Max(5, (int)Math.Floor(ActualHeight / metrics.LineHeight));
        NotifySize(columns, rows);

        var snapshot = Snapshot ?? ClassicTerminalSnapshot.Empty;
        var visibleRows = Math.Min(rows, snapshot.Lines.Count);

        for (var row = 0; row < visibleRows; row++)
            DrawLine(drawingContext, snapshot.Lines[row], row, metrics);

        DrawInput(drawingContext, snapshot, metrics, columns, rows);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _caretIndex = _input.Length;
        _caretVisible = true;
        InvalidateVisual();
        if (e.ChangedButton == MouseButton.Right && ContextMenu is not null)
        {
            ContextMenu.PlacementTarget = this;
            ContextMenu.IsOpen = true;
        }
        e.Handled = true;
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text))
            return;

        if (IsHostRawInputMode())
        {
            KeySubmitted?.Invoke(this, new ClassicMegaMudTerminalKeyEventArgs(e.Text));
            e.Handled = true;
            return;
        }

        foreach (var value in e.Text)
        {
            if (char.IsControl(value))
                continue;

            _input = _input.Insert(_caretIndex, value.ToString());
            _caretIndex++;
        }

        _caretVisible = true;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (e.Key == Key.V)
            {
                PasteFromClipboard();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.C)
            {
                CopyInputToClipboard();
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.Enter:
                if (IsHostRawInputMode())
                {
                    SubmitRaw("\r");
                    e.Handled = true;
                    break;
                }
                Submit();
                e.Handled = true;
                break;
            case Key.Back:
                if (IsHostRawInputMode())
                {
                    SubmitRaw("\b");
                    e.Handled = true;
                    break;
                }
                if (_caretIndex > 0)
                {
                    _input = _input.Remove(_caretIndex - 1, 1);
                    _caretIndex--;
                    _caretVisible = true;
                    InvalidateVisual();
                }
                e.Handled = true;
                break;
            case Key.Delete:
                if (_caretIndex < _input.Length)
                {
                    _input = _input.Remove(_caretIndex, 1);
                    InvalidateVisual();
                }
                e.Handled = true;
                break;
            case Key.Left:
                if (IsHostRawInputMode())
                {
                    SubmitRaw("\u001b[D");
                    e.Handled = true;
                    break;
                }
                _caretIndex = Math.Max(0, _caretIndex - 1);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Right:
                if (IsHostRawInputMode())
                {
                    SubmitRaw("\u001b[C");
                    e.Handled = true;
                    break;
                }
                _caretIndex = Math.Min(_input.Length, _caretIndex + 1);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Home:
                _caretIndex = 0;
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.End:
                _caretIndex = _input.Length;
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Up:
                if (IsHostRawInputMode())
                {
                    SubmitRaw("\u001b[A");
                    e.Handled = true;
                    break;
                }
                RecallHistory(-1);
                e.Handled = true;
                break;
            case Key.Down:
                if (IsHostRawInputMode())
                {
                    SubmitRaw("\u001b[B");
                    e.Handled = true;
                    break;
                }
                RecallHistory(1);
                e.Handled = true;
                break;
            case Key.Escape:
                if (IsHostRawInputMode())
                {
                    SubmitRaw("\u001b");
                    e.Handled = true;
                    break;
                }
                ClearInput();
                e.Handled = true;
                break;
            case Key.Space:
                if (IsHostRawInputMode())
                {
                    SubmitRaw(" ");
                    e.Handled = true;
                }
                break;
        }

        _caretVisible = true;
    }

    private void Submit()
    {
        var command = _input;
        if (!IsPasswordMode && command.Length > 0)
        {
            if (_history.Count == 0 ||
                !string.Equals(_history[^1], command, StringComparison.Ordinal))
                _history.Add(command);

            if (_history.Count > 200)
                _history.RemoveAt(0);
        }

        CommandSubmitted?.Invoke(
            this,
            new ClassicMegaMudTerminalCommandEventArgs(command, IsPasswordMode));

        ClearInput();
    }

    private void SubmitRaw(string sequence)
    {
        if (string.IsNullOrEmpty(sequence))
            return;

        KeySubmitted?.Invoke(this, new ClassicMegaMudTerminalKeyEventArgs(sequence));
        _caretVisible = true;
    }

    private void RecallHistory(int direction)
    {
        if (IsPasswordMode || _history.Count == 0)
            return;

        if (_historyIndex < 0)
            _historyIndex = _history.Count;

        _historyIndex = Math.Clamp(
            _historyIndex + direction,
            0,
            _history.Count);

        _input = _historyIndex == _history.Count
            ? string.Empty
            : _history[_historyIndex];

        _caretIndex = _input.Length;
        InvalidateVisual();
    }

    private void DrawLine(
        DrawingContext drawingContext,
        ClassicTerminalLine line,
        int row,
        TerminalMetrics metrics)
    {
        var column = 0;

        foreach (var run in line.Runs)
        {
            var foreground = run.Foreground;
            var background = run.Background;
            var foregroundRgb = run.ForegroundRgb;
            var backgroundRgb = run.BackgroundRgb;
            if (run.Inverse)
            {
                (foreground, background) = (background, foreground);
                (foregroundRgb, backgroundRgb) = (backgroundRgb, foregroundRgb);
            }

            var foregroundBrush = foregroundRgb is { } foregroundValue
                ? MakeBrush(foregroundValue.Red, foregroundValue.Green, foregroundValue.Blue)
                : Palette[(int)foreground];
            var backgroundBrush = backgroundRgb is { } backgroundValue
                ? MakeBrush(backgroundValue.Red, backgroundValue.Green, backgroundValue.Blue)
                : Palette[(int)background];
            var text = run.Text;

            if (backgroundRgb is not null || background != ClassicTerminalColor.Black)
            {
                drawingContext.DrawRectangle(
                    backgroundBrush,
                    null,
                    new Rect(
                        column * metrics.CellWidth,
                        row * metrics.LineHeight,
                        text.Length * metrics.CellWidth,
                        metrics.LineHeight));
            }

            var typeface = new Typeface(
                FontFamily,
                FontStyles.Normal,
                run.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);

            if (!run.Concealed && (!run.Blink || _caretVisible))
            {
                var formatted = CreateFormattedText(
                    text,
                    typeface,
                    foregroundBrush);

                drawingContext.DrawText(
                    formatted,
                    new Point(
                        column * metrics.CellWidth,
                        row * metrics.LineHeight));

                if (run.Underline)
                {
                    var underlineY = (row + 1) * metrics.LineHeight - 2;
                    drawingContext.DrawLine(
                        new Pen(foregroundBrush, 1),
                        new Point(column * metrics.CellWidth, underlineY),
                        new Point((column + text.Length) * metrics.CellWidth, underlineY));
                }
            }

            column += text.Length;
        }
    }

    private void DrawInput(
        DrawingContext drawingContext,
        ClassicTerminalSnapshot snapshot,
        TerminalMetrics metrics,
        int columns,
        int rows)
    {
        var display = IsPasswordMode
            ? new string('•', _input.Length)
            : _input;

        if (!snapshot.CursorVisible)
            return;

        var cursorColumn = Math.Clamp(snapshot.CursorColumn, 0, columns - 1);
        var cursorRow = Math.Clamp(snapshot.CursorRow, 0, rows - 1);
        var typeface = new Typeface(
            FontFamily,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        for (var index = 0; index < display.Length; index++)
        {
            var logical = cursorColumn + index;
            var row = cursorRow + logical / columns;
            var column = logical % columns;
            if (row >= rows)
                break;

            var formatted = CreateFormattedText(
                display[index].ToString(),
                typeface,
                Foreground ?? Brushes.White);

            drawingContext.DrawText(
                formatted,
                new Point(
                    column * metrics.CellWidth,
                    row * metrics.LineHeight));
        }

        if (!IsKeyboardFocused || !_caretVisible)
            return;

        var caretLogical = cursorColumn + _caretIndex;
        var caretRow = cursorRow + caretLogical / columns;
        var caretColumn = caretLogical % columns;
        if (caretRow >= rows)
            return;

        drawingContext.DrawRectangle(
            Foreground ?? Brushes.White,
            null,
            new Rect(
                caretColumn * metrics.CellWidth,
                caretRow * metrics.LineHeight + 2,
                Math.Max(1.5, metrics.CellWidth * 0.12),
                metrics.LineHeight - 4));
    }

    private TerminalMetrics GetMetrics()
    {
        var typeface = new Typeface(
            FontFamily,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        var formatted = CreateFormattedText(
            "M",
            typeface,
            Foreground ?? Brushes.White);

        return new TerminalMetrics(
            Math.Max(1, formatted.WidthIncludingTrailingWhitespace),
            Math.Max(1, formatted.Height));
    }

    private FormattedText CreateFormattedText(
        string text,
        Typeface typeface,
        Brush brush) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxLineCount = 1,
            Trimming = TextTrimming.None
        };

    private void NotifySize(int columns, int rows)
    {
        if (columns == _lastColumns && rows == _lastRows)
            return;

        _lastColumns = columns;
        _lastRows = rows;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
                TerminalSizeChanged?.Invoke(
                    this,
                    new ClassicMegaMudTerminalSizeEventArgs(columns, rows))));
    }

    private bool IsHostRawInputMode() =>
        Snapshot is { CursorVisible: false };

    private static void OnPasswordModeChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ClassicMegaMudTerminal terminal)
            return;

        // Password-mode changes can arrive while the user is interacting with
        // a menu or an owned window. Clear sensitive local input, but never
        // reclaim keyboard focus from another Reforged client control.
        terminal.ClearInput();
    }

    private static Brush MakeBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private readonly record struct TerminalMetrics(
        double CellWidth,
        double LineHeight);
}
