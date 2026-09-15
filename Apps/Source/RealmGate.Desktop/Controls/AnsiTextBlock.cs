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
            var run = new Run
            {
                Text = span.Text,
                FontWeight = span.Style.Bold ? FontWeight.Bold : FontWeight.Normal
            };

            var brush = ResolveBrush(span.Style.Foreground);
            if (brush is not null)
                run.Foreground = brush;

            Inlines.Add(run);
        }
    }

    private static IBrush? ResolveBrush(AnsiForegroundColor color) => color switch
    {
        AnsiForegroundColor.Default => null,
        AnsiForegroundColor.Black => Brush(0, 0, 0),
        AnsiForegroundColor.Red => Brush(255, 0, 0),
        AnsiForegroundColor.Green => Brush(0, 128, 0),
        AnsiForegroundColor.Olive => Brush(128, 128, 0),
        AnsiForegroundColor.Blue => Brush(0, 0, 255),
        AnsiForegroundColor.Magenta => Brush(255, 0, 255),
        AnsiForegroundColor.Cyan => Brush(0, 255, 255),
        AnsiForegroundColor.LightGray => Brush(211, 211, 211),
        AnsiForegroundColor.Gray => Brush(128, 128, 128),
        AnsiForegroundColor.OrangeRed => Brush(255, 69, 0),
        AnsiForegroundColor.LimeGreen => Brush(50, 205, 50),
        AnsiForegroundColor.Yellow => Brush(255, 255, 0),
        AnsiForegroundColor.DodgerBlue => Brush(30, 144, 255),
        AnsiForegroundColor.HotPink => Brush(255, 105, 180),
        AnsiForegroundColor.Aqua => Brush(0, 255, 255),
        AnsiForegroundColor.White => Brush(255, 255, 255),
        _ => null
    };

    private static IBrush Brush(byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromRgb(red, green, blue));
}
