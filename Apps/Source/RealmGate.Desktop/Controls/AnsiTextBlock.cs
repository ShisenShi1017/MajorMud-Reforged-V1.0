using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using RealmGate.Client.Terminal;

namespace RealmGate.Desktop.Controls;

/// <summary>
/// Avalonia renderer for the UI-neutral RealmGate ANSI parser.
/// Terminal interpretation lives in RealmGate.Client.Core so Windows and Linux share identical rules.
/// </summary>
public sealed class AnsiTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> AnsiTextProperty =
        AvaloniaProperty.Register<AnsiTextBlock, string?>(nameof(AnsiText));

    private static readonly IBrush DefaultForeground = Brush(211, 211, 211);
    private static readonly IBrush DefaultBackground = Brush(0, 0, 0);

    public string? AnsiText
    {
        get => GetValue(AnsiTextProperty);
        set => SetValue(AnsiTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AnsiTextProperty)
            RenderAnsi();
    }

    private void RenderAnsi()
    {
        Inlines.Clear();
        foreach (var span in AnsiTextParser.Parse(AnsiText))
        {
            var foreground = ResolveForeground(span.Style.Foreground, span.Style.Bold);
            var background = ResolveBackground(span.Style.Background);
            if (span.Style.Reverse)
                (foreground, background) = (background, foreground);

            var run = new Run
            {
                Text = span.Text,
                FontWeight = span.Style.Bold ? FontWeight.Bold : FontWeight.Normal,
                Foreground = foreground,
                Background = background,
                TextDecorations = span.Style.Underline ? TextDecorations.Underline : null
            };

            // ANSI blink state is retained by RealmGate.Client.Core. The terminal-cell
            // renderer introduced later in the parity pass will own timed blink behavior;
            // a TextBlock run deliberately remains stable instead of flashing out of sync.
            Inlines.Add(run);
        }
    }

    private static IBrush ResolveForeground(AnsiForegroundColor color, bool intense)
    {
        if (intense)
        {
            color = color switch
            {
                AnsiForegroundColor.Black => AnsiForegroundColor.Gray,
                AnsiForegroundColor.Red => AnsiForegroundColor.OrangeRed,
                AnsiForegroundColor.Green => AnsiForegroundColor.LimeGreen,
                AnsiForegroundColor.Olive => AnsiForegroundColor.Yellow,
                AnsiForegroundColor.Blue => AnsiForegroundColor.DodgerBlue,
                AnsiForegroundColor.Magenta => AnsiForegroundColor.HotPink,
                AnsiForegroundColor.Cyan => AnsiForegroundColor.Aqua,
                AnsiForegroundColor.LightGray => AnsiForegroundColor.White,
                _ => color
            };
        }

        return color switch
        {
            AnsiForegroundColor.Default => DefaultForeground,
            AnsiForegroundColor.Black => Brush(0, 0, 0),
            AnsiForegroundColor.Red => Brush(170, 0, 0),
            AnsiForegroundColor.Green => Brush(0, 170, 0),
            AnsiForegroundColor.Olive => Brush(170, 85, 0),
            AnsiForegroundColor.Blue => Brush(0, 0, 170),
            AnsiForegroundColor.Magenta => Brush(170, 0, 170),
            AnsiForegroundColor.Cyan => Brush(0, 170, 170),
            AnsiForegroundColor.LightGray => Brush(211, 211, 211),
            AnsiForegroundColor.Gray => Brush(128, 128, 128),
            AnsiForegroundColor.OrangeRed => Brush(255, 85, 85),
            AnsiForegroundColor.LimeGreen => Brush(85, 255, 85),
            AnsiForegroundColor.Yellow => Brush(255, 255, 85),
            AnsiForegroundColor.DodgerBlue => Brush(85, 85, 255),
            AnsiForegroundColor.HotPink => Brush(255, 85, 255),
            AnsiForegroundColor.Aqua => Brush(85, 255, 255),
            AnsiForegroundColor.White => Brush(255, 255, 255),
            _ => DefaultForeground
        };
    }

    private static IBrush ResolveBackground(AnsiBackgroundColor color) => color switch
    {
        AnsiBackgroundColor.Default => DefaultBackground,
        AnsiBackgroundColor.Black => Brush(0, 0, 0),
        AnsiBackgroundColor.Red => Brush(170, 0, 0),
        AnsiBackgroundColor.Green => Brush(0, 170, 0),
        AnsiBackgroundColor.Olive => Brush(170, 85, 0),
        AnsiBackgroundColor.Blue => Brush(0, 0, 170),
        AnsiBackgroundColor.Magenta => Brush(170, 0, 170),
        AnsiBackgroundColor.Cyan => Brush(0, 170, 170),
        AnsiBackgroundColor.LightGray => Brush(211, 211, 211),
        AnsiBackgroundColor.Gray => Brush(128, 128, 128),
        AnsiBackgroundColor.OrangeRed => Brush(255, 85, 85),
        AnsiBackgroundColor.LimeGreen => Brush(85, 255, 85),
        AnsiBackgroundColor.Yellow => Brush(255, 255, 85),
        AnsiBackgroundColor.DodgerBlue => Brush(85, 85, 255),
        AnsiBackgroundColor.HotPink => Brush(255, 85, 255),
        AnsiBackgroundColor.Aqua => Brush(85, 255, 255),
        AnsiBackgroundColor.White => Brush(255, 255, 255),
        _ => DefaultBackground
    };

    private static IBrush Brush(byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromRgb(red, green, blue));
}
