using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RealmGate.App.Views;

/// <summary>
/// Renders ANSI SGR terminal text as WPF Runs. Unsupported control sequences
/// are removed instead of being shown as raw text.
/// </summary>
public sealed class AnsiTextBlock : TextBlock
{
    private static readonly Regex SgrSequence = new(
        @"(?:\x1B)?\[(?<codes>[0-9;]*)m",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OtherAnsiSequence = new(
        @"\x1B(?:[@-Z\-_]|\[[0-?]*[ -/]*[@-~])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly DependencyProperty AnsiTextProperty =
        DependencyProperty.Register(
            nameof(AnsiText),
            typeof(string),
            typeof(AnsiTextBlock),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnAnsiTextChanged));

    public string AnsiText
    {
        get => (string)GetValue(AnsiTextProperty);
        set => SetValue(AnsiTextProperty, value);
    }

    private static void OnAnsiTextChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is AnsiTextBlock block)
            block.RenderAnsi(args.NewValue as string ?? string.Empty);
    }

    private void RenderAnsi(string input)
    {
        Inlines.Clear();

        if (string.IsNullOrEmpty(input))
        {
            Inlines.Add(new Run(string.Empty));
            return;
        }

        var state = new AnsiState(Foreground, FontWeights.Normal);
        var position = 0;

        foreach (Match match in SgrSequence.Matches(input))
        {
            if (match.Index > position)
            {
                var plain = input.Substring(position, match.Index - position);
                AddRun(OtherAnsiSequence.Replace(plain, string.Empty), state);
            }

            ApplyCodes(match.Groups["codes"].Value, ref state);
            position = match.Index + match.Length;
        }

        if (position < input.Length)
        {
            var remainder = input[position..];
            AddRun(OtherAnsiSequence.Replace(remainder, string.Empty), state);
        }

        if (Inlines.Count == 0)
            Inlines.Add(new Run(string.Empty));
    }

    private void AddRun(string text, AnsiState state)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Inlines.Add(new Run(text)
        {
            Foreground = state.Foreground,
            FontWeight = state.FontWeight
        });
    }

    private void ApplyCodes(string codesText, ref AnsiState state)
    {
        var codes = ParseCodes(codesText);
        if (codes.Count == 0)
            codes.Add(0);

        foreach (var code in codes)
        {
            switch (code)
            {
                case 0:
                    state = new AnsiState(Foreground, FontWeights.Normal);
                    break;
                case 1:
                    state = state with { FontWeight = FontWeights.Bold };
                    break;
                case 2:
                case 22:
                    state = state with { FontWeight = FontWeights.Normal };
                    break;
                case 39:
                    state = state with { Foreground = Foreground };
                    break;
                default:
                    if (TryGetForeground(code, out var brush))
                        state = state with { Foreground = brush };
                    break;
            }
        }
    }

    private static List<int> ParseCodes(string value)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var part in value.Split(';'))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                result.Add(code);
        }

        return result;
    }

    private static bool TryGetForeground(int code, out Brush brush)
    {
        brush = code switch
        {
            30 => Brushes.Black,
            31 => Brushes.Red,
            32 => Brushes.Green,
            33 => Brushes.Olive,
            34 => Brushes.Blue,
            35 => Brushes.Magenta,
            36 => Brushes.Cyan,
            37 => Brushes.LightGray,
            90 => Brushes.Gray,
            91 => Brushes.OrangeRed,
            92 => Brushes.LimeGreen,
            93 => Brushes.Yellow,
            94 => Brushes.DodgerBlue,
            95 => Brushes.HotPink,
            96 => Brushes.Aqua,
            97 => Brushes.White,
            _ => Brushes.Transparent
        };

        return code is >= 30 and <= 37 or >= 90 and <= 97;
    }

    private readonly record struct AnsiState(
        Brush Foreground,
        FontWeight FontWeight);
}
