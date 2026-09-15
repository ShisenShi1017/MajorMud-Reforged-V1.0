using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using RealmGate.Client.Terminal;

namespace RealmGate.Desktop.Controls;

/// <summary>
/// Lightweight Avalonia renderer for line-oriented ANSI text. The full Play terminal
/// uses the shared ClassicTerminalEmulator cell model; this control intentionally uses
/// the same original DOS 16-color palette so previews cannot drift visually.
/// </summary>
public sealed class AnsiTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> AnsiTextProperty =
        AvaloniaProperty.Register<AnsiTextBlock, string?>(nameof(AnsiText));

    private static readonly IBrush DefaultForeground = Brush(0xC0, 0xC0, 0xC0);
    private static readonly IBrush DefaultBackground = Brush(0x00, 0x00, 0x00);

    public string? AnsiText
    {
        get => GetValue(AnsiTextProperty);
        set => SetValue(AnsiTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AnsiTextProperty) RenderAnsi();
    }

    private void RenderAnsi()
    {
        var inlines = Inlines;
        if (inlines is null) return;
        inlines.Clear();

        foreach (var span in AnsiTextParser.Parse(AnsiText))
        {
            var foreground = ResolveForeground(span.Style.Foreground, span.Style.Bold);
            var background = ResolveBackground(span.Style.Background);
            if (span.Style.Reverse) (foreground, background) = (background, foreground);

            var run = new Run
            {
                Text = span.Text,
                FontWeight = FontWeight.Normal,
                Foreground = foreground,
                Background = background,
                TextDecorations = span.Style.Underline ? Avalonia.Media.TextDecorations.Underline : null
            };

            // WCC/DOS SGR 1 is color intensity. Do not substitute typographic bold for it.
            // Blink state remains in Client.Core; timed blink belongs to the cell renderer.
            inlines.Add(run);
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
            AnsiForegroundColor.Black => Brush(0x00, 0x00, 0x00),
            AnsiForegroundColor.Red => Brush(0x80, 0x00, 0x00),
            AnsiForegroundColor.Green => Brush(0x00, 0x80, 0x00),
            AnsiForegroundColor.Olive => Brush(0x80, 0x80, 0x00),
            AnsiForegroundColor.Blue => Brush(0x00, 0x00, 0x80),
            AnsiForegroundColor.Magenta => Brush(0x80, 0x00, 0x80),
            AnsiForegroundColor.Cyan => Brush(0x00, 0x80, 0x80),
            AnsiForegroundColor.LightGray => Brush(0xC0, 0xC0, 0xC0),
            AnsiForegroundColor.Gray => Brush(0x80, 0x80, 0x80),
            AnsiForegroundColor.OrangeRed => Brush(0xFF, 0x00, 0x00),
            AnsiForegroundColor.LimeGreen => Brush(0x00, 0xFF, 0x00),
            AnsiForegroundColor.Yellow => Brush(0xFF, 0xFF, 0x00),
            AnsiForegroundColor.DodgerBlue => Brush(0x00, 0x00, 0xFF),
            AnsiForegroundColor.HotPink => Brush(0xFF, 0x00, 0xFF),
            AnsiForegroundColor.Aqua => Brush(0x00, 0xFF, 0xFF),
            AnsiForegroundColor.White => Brush(0xFF, 0xFF, 0xFF),
            _ => DefaultForeground
        };
    }

    private static IBrush ResolveBackground(AnsiBackgroundColor color) => color switch
    {
        AnsiBackgroundColor.Default => DefaultBackground,
        AnsiBackgroundColor.Black => Brush(0x00, 0x00, 0x00),
        AnsiBackgroundColor.Red => Brush(0x80, 0x00, 0x00),
        AnsiBackgroundColor.Green => Brush(0x00, 0x80, 0x00),
        AnsiBackgroundColor.Olive => Brush(0x80, 0x80, 0x00),
        AnsiBackgroundColor.Blue => Brush(0x00, 0x00, 0x80),
        AnsiBackgroundColor.Magenta => Brush(0x80, 0x00, 0x80),
        AnsiBackgroundColor.Cyan => Brush(0x00, 0x80, 0x80),
        AnsiBackgroundColor.LightGray => Brush(0xC0, 0xC0, 0xC0),
        AnsiBackgroundColor.Gray => Brush(0x80, 0x80, 0x80),
        AnsiBackgroundColor.OrangeRed => Brush(0xFF, 0x00, 0x00),
        AnsiBackgroundColor.LimeGreen => Brush(0x00, 0xFF, 0x00),
        AnsiBackgroundColor.Yellow => Brush(0xFF, 0xFF, 0x00),
        AnsiBackgroundColor.DodgerBlue => Brush(0x00, 0x00, 0xFF),
        AnsiBackgroundColor.HotPink => Brush(0xFF, 0x00, 0xFF),
        AnsiBackgroundColor.Aqua => Brush(0x00, 0xFF, 0xFF),
        AnsiBackgroundColor.White => Brush(0xFF, 0xFF, 0xFF),
        _ => DefaultBackground
    };

    private static IBrush Brush(byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromRgb(red, green, blue));
}