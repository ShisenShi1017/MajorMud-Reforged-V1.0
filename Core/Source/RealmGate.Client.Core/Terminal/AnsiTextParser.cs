using System.Globalization;
using System.Text.RegularExpressions;

namespace RealmGate.Client.Terminal;

public enum AnsiForegroundColor
{
    Default,
    Black,
    Red,
    Green,
    Olive,
    Blue,
    Magenta,
    Cyan,
    LightGray,
    Gray,
    OrangeRed,
    LimeGreen,
    Yellow,
    DodgerBlue,
    HotPink,
    Aqua,
    White
}

public readonly record struct AnsiTextStyle(AnsiForegroundColor Foreground, bool Bold)
{
    public static AnsiTextStyle Default => new(AnsiForegroundColor.Default, false);
}

public sealed record AnsiTextSpan(string Text, AnsiTextStyle Style);

/// <summary>
/// UI-neutral ANSI SGR interpreter shared by Windows and Linux RealmGate clients.
/// Rendering remains the responsibility of the desktop presentation layer.
/// </summary>
public static class AnsiTextParser
{
    private static readonly Regex SgrSequence = new(
        @"(?:\x1B)?\[(?<codes>[0-9;]*)m",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OtherAnsiSequence = new(
        @"\x1B(?:[@-Z\-_]|\[[0-?]*[ -/]*[@-~])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<AnsiTextSpan> Parse(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<AnsiTextSpan>();

        var spans = new List<AnsiTextSpan>();
        var state = AnsiTextStyle.Default;
        var position = 0;

        foreach (Match match in SgrSequence.Matches(input))
        {
            if (match.Index > position)
                AddSpan(spans, OtherAnsiSequence.Replace(input[position..match.Index], string.Empty), state);

            ApplyCodes(match.Groups["codes"].Value, ref state);
            position = match.Index + match.Length;
        }

        if (position < input.Length)
            AddSpan(spans, OtherAnsiSequence.Replace(input[position..], string.Empty), state);

        return spans;
    }

    public static string StripFormatting(string? input) =>
        string.Concat(Parse(input).Select(span => span.Text));

    private static void AddSpan(List<AnsiTextSpan> spans, string text, AnsiTextStyle style)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (spans.Count > 0 && spans[^1].Style == style)
        {
            var previous = spans[^1];
            spans[^1] = previous with { Text = previous.Text + text };
            return;
        }

        spans.Add(new AnsiTextSpan(text, style));
    }

    private static void ApplyCodes(string codesText, ref AnsiTextStyle state)
    {
        var codes = ParseCodes(codesText);
        if (codes.Count == 0) codes.Add(0);

        foreach (var code in codes)
        {
            switch (code)
            {
                case 0:
                    state = AnsiTextStyle.Default;
                    break;
                case 1:
                    state = state with { Bold = true };
                    break;
                case 2:
                case 22:
                    state = state with { Bold = false };
                    break;
                case 39:
                    state = state with { Foreground = AnsiForegroundColor.Default };
                    break;
                default:
                    if (TryGetForeground(code, out var foreground))
                        state = state with { Foreground = foreground };
                    break;
            }
        }
    }

    private static List<int> ParseCodes(string value)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        foreach (var part in value.Split(';'))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                result.Add(code);
        }

        return result;
    }

    private static bool TryGetForeground(int code, out AnsiForegroundColor foreground)
    {
        foreground = code switch
        {
            30 => AnsiForegroundColor.Black,
            31 => AnsiForegroundColor.Red,
            32 => AnsiForegroundColor.Green,
            33 => AnsiForegroundColor.Olive,
            34 => AnsiForegroundColor.Blue,
            35 => AnsiForegroundColor.Magenta,
            36 => AnsiForegroundColor.Cyan,
            37 => AnsiForegroundColor.LightGray,
            90 => AnsiForegroundColor.Gray,
            91 => AnsiForegroundColor.OrangeRed,
            92 => AnsiForegroundColor.LimeGreen,
            93 => AnsiForegroundColor.Yellow,
            94 => AnsiForegroundColor.DodgerBlue,
            95 => AnsiForegroundColor.HotPink,
            96 => AnsiForegroundColor.Aqua,
            97 => AnsiForegroundColor.White,
            _ => AnsiForegroundColor.Default
        };

        return code is >= 30 and <= 37 or >= 90 and <= 97;
    }
}
