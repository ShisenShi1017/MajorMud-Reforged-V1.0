using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using RealmGate.Client.Terminal;

namespace RealmGate.Desktop.Controls;

public sealed class ClassicTerminalCommandSubmittedEventArgs(
    string command,
    bool sensitive) : EventArgs
{
    public string Command { get; } = command;
    public bool Sensitive { get; } = sensitive;
}

public sealed class ClassicTerminalKeySubmittedEventArgs(string sequence) : EventArgs
{
    public string Sequence { get; } = sequence;
}

public sealed class ClassicTerminalSizeChangedEventArgs(int columns, int rows) : EventArgs
{
    public int Columns { get; } = columns;
    public int Rows { get; } = rows;
}

/// <summary>
/// Cross-platform renderer for the authoritative RealmGate classic terminal cell buffer.
/// ANSI/VT interpretation and CP437 decoding live in RealmGate.Client.Core; this control
/// only draws the resulting snapshot on a deterministic terminal grid.
/// </summary>
public sealed class ClassicTerminalView : Control
{
    public static readonly StyledProperty<ClassicTerminalSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<ClassicTerminalView, ClassicTerminalSnapshot?>(nameof(Snapshot));
    public static readonly StyledProperty<bool> IsPasswordModeProperty =
        AvaloniaProperty.Register<ClassicTerminalView, bool>(nameof(IsPasswordMode));

    private const double CellWidth = 9.6;
    private const double CellHeight = 20.0;
    private const double TerminalFontSize = 16.0;
    private static readonly FontFamily TerminalFont =
        new("Cascadia Mono,Consolas,DejaVu Sans Mono,Monospace");
    private static readonly Typeface TerminalTypeface = new(
        TerminalFont,
        FontStyle.Normal,
        FontWeight.Normal,
        FontStretch.Normal);
    private static readonly IBrush[] Palette = BuildPalette();

    private readonly DispatcherTimer _blinkTimer;
    private readonly ClassicTerminalInputBuffer _input = new();
    private bool _blinkVisible = true;
    private int _lastColumns;
    private int _lastRows;

    public ClassicTerminalView()
    {
        ClipToBounds = true;
        Focusable = true;
        UpdateExtent(ClassicTerminalSnapshot.Empty);

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkVisible = !_blinkVisible;
            InvalidateVisual();
        };

        AttachedToVisualTree += (_, _) =>
        {
            _blinkTimer.Start();
            Focus();
        };
        DetachedFromVisualTree += (_, _) => _blinkTimer.Stop();
        GotFocus += (_, _) => ResetBlink();
        LostFocus += (_, _) => InvalidateVisual();
        PointerPressed += (_, args) =>
        {
            Focus();
            _input.MoveEnd();
            ResetBlink();
            args.Handled = true;
        };
    }

    public ClassicTerminalSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public bool IsPasswordMode
    {
        get => GetValue(IsPasswordModeProperty);
        set => SetValue(IsPasswordModeProperty, value);
    }

    public event EventHandler<ClassicTerminalCommandSubmittedEventArgs>? CommandSubmitted;
    public event EventHandler<ClassicTerminalKeySubmittedEventArgs>? KeySubmitted;
    public event EventHandler<ClassicTerminalSizeChangedEventArgs>? TerminalSizeChanged;

    public void ClearInput()
    {
        _input.Clear();
        ResetBlink();
    }

    public async Task<bool> CopyInputToClipboardAsync()
    {
        var value = _input.CopyableText;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (value is null || clipboard is null)
            return false;

        await clipboard.SetTextAsync(value);
        return true;
    }

    public async Task<bool> PasteFromClipboardAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return false;

        var value = await clipboard.TryGetTextAsync();
        if (!_input.PasteText(value))
            return false;

        ResetBlink();
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SnapshotProperty)
        {
            var snapshot = Snapshot ?? ClassicTerminalSnapshot.Empty;
            UpdateExtent(snapshot);
            NotifySize(snapshot.Columns, snapshot.Rows);
            InvalidateVisual();
        }
        else if (change.Property == IsPasswordModeProperty)
        {
            _input.SetPasswordMode(IsPasswordMode);
            ResetBlink();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var snapshot = Snapshot ?? ClassicTerminalSnapshot.Empty;
        context.DrawRectangle(
            Palette[(int)ClassicTerminalColor.Black],
            null,
            new Rect(0, 0, snapshot.Columns * CellWidth, snapshot.Rows * CellHeight));

        var visibleRows = Math.Min(snapshot.Rows, snapshot.Lines.Count);
        for (var row = 0; row < visibleRows; row++)
            DrawLine(context, snapshot.Lines[row], row, snapshot.Columns);

        DrawInput(context, snapshot);
    }

    protected override void OnTextInput(TextInputEventArgs args)
    {
        base.OnTextInput(args);
        if (string.IsNullOrEmpty(args.Text))
            return;

        if (IsHostRawInputMode())
            SubmitRaw(args.Text);
        else if (_input.InsertText(args.Text))
            ResetBlink();

        args.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);

        if (args.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (args.Key == Key.V)
            {
                _ = TryClipboardActionAsync(PasteFromClipboardAsync);
                args.Handled = true;
                return;
            }

            if (args.Key == Key.C)
            {
                _ = TryClipboardActionAsync(CopyInputToClipboardAsync);
                args.Handled = true;
                return;
            }
        }

        switch (args.Key)
        {
            case Key.Enter:
                if (IsHostRawInputMode())
                    SubmitRaw("\r");
                else
                    SubmitCommand();
                args.Handled = true;
                break;
            case Key.Back:
                if (IsHostRawInputMode())
                    SubmitRaw("\b");
                else
                    UpdateInput(_input.Backspace());
                args.Handled = true;
                break;
            case Key.Delete:
                UpdateInput(_input.Delete());
                args.Handled = true;
                break;
            case Key.Left:
                if (IsHostRawInputMode())
                    SubmitRaw("\u001b[D");
                else
                    UpdateInput(_input.MoveCaret(-1));
                args.Handled = true;
                break;
            case Key.Right:
                if (IsHostRawInputMode())
                    SubmitRaw("\u001b[C");
                else
                    UpdateInput(_input.MoveCaret(1));
                args.Handled = true;
                break;
            case Key.Home:
                UpdateInput(_input.MoveHome());
                args.Handled = true;
                break;
            case Key.End:
                UpdateInput(_input.MoveEnd());
                args.Handled = true;
                break;
            case Key.Up:
                if (IsHostRawInputMode())
                    SubmitRaw("\u001b[A");
                else
                    UpdateInput(_input.RecallHistory(-1));
                args.Handled = true;
                break;
            case Key.Down:
                if (IsHostRawInputMode())
                    SubmitRaw("\u001b[B");
                else
                    UpdateInput(_input.RecallHistory(1));
                args.Handled = true;
                break;
            case Key.Escape:
                if (IsHostRawInputMode())
                    SubmitRaw("\u001b");
                else
                    ClearInput();
                args.Handled = true;
                break;
            case Key.Space when IsHostRawInputMode():
                SubmitRaw(" ");
                args.Handled = true;
                break;
        }
    }

    private void DrawLine(
        DrawingContext context,
        ClassicTerminalLine line,
        int row,
        int terminalColumns)
    {
        var column = 0;
        foreach (var run in line.Runs)
        {
            if (column >= terminalColumns)
                break;

            var length = Math.Min(run.Text.Length, terminalColumns - column);
            if (length <= 0)
                continue;

            var (foreground, background) = ResolveColors(run);
            var runBounds = new Rect(
                column * CellWidth,
                row * CellHeight,
                length * CellWidth,
                CellHeight);

            // Spaces are cells too: paint every run background before deciding
            // whether its glyphs are visible.
            context.DrawRectangle(background, null, runBounds);

            if (!run.Concealed && (!run.Blink || _blinkVisible))
            {
                for (var index = 0; index < length; index++)
                    DrawCellGlyph(context, run.Text[index], column + index, row, foreground);

                if (run.Underline)
                {
                    var underlineY = (row + 1) * CellHeight - 2;
                    context.DrawLine(
                        new Pen(foreground, 1),
                        new Point(column * CellWidth, underlineY),
                        new Point((column + length) * CellWidth, underlineY));
                }
            }

            column += length;
        }
    }

    private static void DrawCellGlyph(
        DrawingContext context,
        char value,
        int column,
        int row,
        IBrush foreground)
    {
        if (value == ' ')
            return;

        var cellBounds = new Rect(
            column * CellWidth,
            row * CellHeight,
            CellWidth,
            CellHeight);
        var formatted = new FormattedText(
            value.ToString(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            TerminalTypeface,
            TerminalFontSize,
            foreground)
        {
            MaxLineCount = 1,
            MaxTextWidth = CellWidth,
            TextAlignment = TextAlignment.Left
        };

        using (context.PushClip(cellBounds))
            context.DrawText(formatted, cellBounds.Position);
    }

    private void DrawInput(DrawingContext context, ClassicTerminalSnapshot snapshot)
    {
        if (!snapshot.CursorVisible ||
            snapshot.CursorColumn < 0 || snapshot.CursorColumn >= snapshot.Columns ||
            snapshot.CursorRow < 0 || snapshot.CursorRow >= snapshot.Rows)
        {
            return;
        }

        var display = _input.DisplayText;
        for (var index = 0; index < display.Length; index++)
        {
            var logical = snapshot.CursorColumn + index;
            var row = snapshot.CursorRow + logical / snapshot.Columns;
            var column = logical % snapshot.Columns;
            if (row >= snapshot.Rows)
                break;

            DrawCellGlyph(
                context,
                display[index],
                column,
                row,
                Palette[(int)ClassicTerminalColor.White]);
        }

        if (!_blinkVisible)
            return;

        var caretLogical = snapshot.CursorColumn + _input.CaretIndex;
        var caretRow = snapshot.CursorRow + caretLogical / snapshot.Columns;
        var caretColumn = caretLogical % snapshot.Columns;
        if (caretRow >= snapshot.Rows)
            return;

        if (IsFocused)
        {
            context.DrawRectangle(
                Palette[(int)ClassicTerminalColor.White],
                null,
                new Rect(
                    caretColumn * CellWidth,
                    caretRow * CellHeight + 2,
                    Math.Max(1.5, CellWidth * 0.12),
                    CellHeight - 4));
        }
        else
        {
            context.DrawRectangle(
                null,
                new Pen(Palette[(int)ClassicTerminalColor.Gray], 1),
                new Rect(
                    caretColumn * CellWidth,
                    caretRow * CellHeight,
                    CellWidth,
                    CellHeight));
        }
    }

    private static (IBrush Foreground, IBrush Background) ResolveColors(ClassicTerminalRun run)
    {
        var foreground = run.ForegroundRgb is { } foregroundRgb
            ? ToBrush(foregroundRgb)
            : Palette[(int)ClassicTerminalPalette.ResolveForeground(run.Foreground, run.Bold)];
        var background = run.BackgroundRgb is { } backgroundRgb
            ? ToBrush(backgroundRgb)
            : Palette[(int)run.Background];

        return run.Inverse
            ? (background, foreground)
            : (foreground, background);
    }

    private void SubmitCommand()
    {
        var submission = _input.Submit();
        CommandSubmitted?.Invoke(
            this,
            new ClassicTerminalCommandSubmittedEventArgs(
                submission.Text,
                submission.Sensitive));
        ResetBlink();
    }

    private void SubmitRaw(string sequence)
    {
        if (sequence.Length == 0)
            return;

        KeySubmitted?.Invoke(this, new ClassicTerminalKeySubmittedEventArgs(sequence));
        ResetBlink();
    }

    private void UpdateInput(bool changed)
    {
        if (changed)
            ResetBlink();
    }

    private static async Task TryClipboardActionAsync(Func<Task<bool>> action)
    {
        try
        {
            await action();
        }
        catch (Exception)
        {
            // Clipboard availability is platform/session dependent. A failed
            // copy or paste must not terminate the terminal input surface.
        }
    }

    private void ResetBlink()
    {
        _blinkVisible = true;
        InvalidateVisual();
    }

    private bool IsHostRawInputMode() =>
        Snapshot is { CursorVisible: false };

    private void NotifySize(int columns, int rows)
    {
        if (columns == _lastColumns && rows == _lastRows)
            return;

        _lastColumns = columns;
        _lastRows = rows;
        Dispatcher.UIThread.Post(() =>
            TerminalSizeChanged?.Invoke(
                this,
                new ClassicTerminalSizeChangedEventArgs(columns, rows)));
    }

    private void UpdateExtent(ClassicTerminalSnapshot snapshot)
    {
        Width = snapshot.Columns * CellWidth;
        Height = snapshot.Rows * CellHeight;
    }

    private static IBrush[] BuildPalette() =>
        Enum.GetValues<ClassicTerminalColor>()
            .Select(color => ToBrush(ClassicTerminalPalette.ResolveRgb(color)))
            .ToArray();

    private static IBrush ToBrush(ClassicTerminalRgb rgb) =>
        new SolidColorBrush(Color.FromRgb(rgb.Red, rgb.Green, rgb.Blue));
}
