using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RealmGate.Client.Terminal;

namespace RealmGate.Desktop.Controls;

/// <summary>
/// Cross-platform renderer for the authoritative RealmGate classic terminal cell buffer.
/// ANSI/VT interpretation and CP437 decoding live in RealmGate.Client.Core; this control
/// only projects the resulting 80x25-compatible snapshot into Avalonia controls.
/// </summary>
public sealed class ClassicTerminalView : Canvas
{
    public static readonly StyledProperty<ClassicTerminalSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<ClassicTerminalView, ClassicTerminalSnapshot?>(nameof(Snapshot));

    private const double CellWidth = 9.6;
    private const double CellHeight = 20.0;
    private const double TerminalFontSize = 16.0;
    private static readonly FontFamily TerminalFont =
        new("Cascadia Mono,Consolas,DejaVu Sans Mono,Monospace");

    public ClassicTerminalView()
    {
        Background = Brushes.Black;
        ClipToBounds = true;
        Rebuild();
    }

    public ClassicTerminalSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SnapshotProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();
        var snapshot = Snapshot ?? ClassicTerminalSnapshot.Empty;
        Width = snapshot.Columns * CellWidth;
        Height = snapshot.Rows * CellHeight;

        for (var row = 0; row < snapshot.Lines.Count && row < snapshot.Rows; row++)
        {
            var column = 0;
            foreach (var run in snapshot.Lines[row].Runs)
            {
                if (run.Text.Length == 0)
                    continue;

                var foreground = ResolveForeground(run);
                var background = ResolveBackground(run);
                if (run.Inverse)
                    (foreground, background) = (background, foreground);

                var text = new TextBlock
                {
                    Text = run.Concealed ? new string(' ', run.Text.Length) : run.Text,
                    FontFamily = TerminalFont,
                    FontSize = TerminalFontSize,
                    FontWeight = FontWeight.Normal,
                    Foreground = ToBrush(foreground),
                    Background = ToBrush(background),
                    TextDecorations = run.Underline ? Avalonia.Media.TextDecorations.Underline : null,
                    TextWrapping = TextWrapping.NoWrap,
                    Width = run.Text.Length * CellWidth,
                    Height = CellHeight,
                    ClipToBounds = true,
                    IsHitTestVisible = false
                };

                SetLeft(text, column * CellWidth);
                SetTop(text, row * CellHeight);
                Children.Add(text);
                column += run.Text.Length;
            }
        }

        if (snapshot.CursorVisible &&
            snapshot.CursorColumn >= 0 && snapshot.CursorColumn < snapshot.Columns &&
            snapshot.CursorRow >= 0 && snapshot.CursorRow < snapshot.Rows)
        {
            var cursor = new Border
            {
                Width = CellWidth,
                Height = CellHeight,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            };
            SetLeft(cursor, snapshot.CursorColumn * CellWidth);
            SetTop(cursor, snapshot.CursorRow * CellHeight);
            Children.Add(cursor);
        }
    }

    private static ClassicTerminalRgb ResolveForeground(ClassicTerminalRun run)
    {
        if (run.ForegroundRgb is { } rgb)
            return rgb;

        var effective = ClassicTerminalPalette.ResolveForeground(run.Foreground, run.Bold);
        return ClassicTerminalPalette.ResolveRgb(effective);
    }

    private static ClassicTerminalRgb ResolveBackground(ClassicTerminalRun run) =>
        run.BackgroundRgb ?? ClassicTerminalPalette.ResolveRgb(run.Background);

    private static IBrush ToBrush(ClassicTerminalRgb rgb) =>
        new SolidColorBrush(Color.FromRgb(rgb.Red, rgb.Green, rgb.Blue));
}