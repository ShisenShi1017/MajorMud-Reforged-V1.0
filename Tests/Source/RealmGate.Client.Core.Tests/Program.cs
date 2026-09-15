using RealmGate.Client.Terminal;

var checks = new List<string>();

Require(AnsiTextParser.Parse("plain").Single() == new AnsiTextSpan("plain", AnsiTextStyle.Default), "plain-text");
checks.Add("plain-text");

var red = AnsiTextParser.Parse("\x1b[31mred\x1b[0m normal");
Require(red.Count == 2, "red-reset-span-count");
Require(red[0].Text == "red" && red[0].Style.Foreground == AnsiForegroundColor.Red, "red-foreground");
Require(red[1].Text == " normal" && red[1].Style == AnsiTextStyle.Default, "reset-default");
checks.Add("red-reset");

var bold = AnsiTextParser.Parse("\x1b[1mbold\x1b[22mnormal");
Require(bold.Count == 2 && bold[0].Style.Bold && !bold[1].Style.Bold, "bold-toggle");
checks.Add("bold-toggle");

var bright = AnsiTextParser.Parse("\x1b[97mwhite");
Require(bright.Single().Style.Foreground == AnsiForegroundColor.White, "bright-white");
checks.Add("bright-white");

var csiWithoutEscape = AnsiTextParser.Parse("[32mgreen[0m");
Require(csiWithoutEscape.Count == 1 && csiWithoutEscape[0].Text == "green" && csiWithoutEscape[0].Style.Foreground == AnsiForegroundColor.Green, "legacy-csi-without-escape");
checks.Add("legacy-csi-without-escape");

var stripped = AnsiTextParser.StripFormatting("A\x1b[2JB\x1b[31mC\x1b[0m");
Require(stripped == "ABC", "strip-control-and-sgr");
checks.Add("strip-control-and-sgr");

var resetForeground = AnsiTextParser.Parse("\x1b[34mblue\x1b[39mdefault");
Require(resetForeground.Count == 2 && resetForeground[1].Style.Foreground == AnsiForegroundColor.Default, "foreground-reset");
checks.Add("foreground-reset");

var background = AnsiTextParser.Parse("\x1b[44mblue-bg\x1b[49mdefault-bg");
Require(background.Count == 2, "background-span-count");
Require(background[0].Style.Background == AnsiBackgroundColor.Blue, "background-blue");
Require(background[1].Style.Background == AnsiBackgroundColor.Default, "background-reset");
checks.Add("background-color");

var brightBackground = AnsiTextParser.Parse("\x1b[103mbright-yellow-bg");
Require(brightBackground.Single().Style.Background == AnsiBackgroundColor.Yellow, "bright-background");
checks.Add("bright-background");

var underline = AnsiTextParser.Parse("\x1b[4munder\x1b[24mnormal");
Require(underline.Count == 2 && underline[0].Style.Underline && !underline[1].Style.Underline, "underline-toggle");
checks.Add("underline-toggle");

var blink = AnsiTextParser.Parse("\x1b[5mblink\x1b[25mnormal");
Require(blink.Count == 2 && blink[0].Style.Blink && !blink[1].Style.Blink, "blink-toggle");
checks.Add("blink-toggle");

var reverse = AnsiTextParser.Parse("\x1b[7mreverse\x1b[27mnormal");
Require(reverse.Count == 2 && reverse[0].Style.Reverse && !reverse[1].Style.Reverse, "reverse-toggle");
checks.Add("reverse-toggle");

var combined = AnsiTextParser.Parse("\x1b[1;31;40;4mmajor\x1b[0mmud");
Require(combined.Count == 2, "combined-span-count");
Require(combined[0].Style.Foreground == AnsiForegroundColor.Red &&
        combined[0].Style.Background == AnsiBackgroundColor.Black &&
        combined[0].Style.Bold && combined[0].Style.Underline,
        "combined-style");
Require(combined[1].Style == AnsiTextStyle.Default, "combined-reset");
checks.Add("combined-style");

// Original WCC evidence contract: stateful DOS/ANSI terminal behavior.
var terminal = new ClassicTerminalEmulator();
var initial = terminal.CreateSnapshot();
Require(initial.Columns == 80 && initial.Rows == 25, "wcc-default-80x25");
checks.Add("wcc-default-80x25");

var mapStyle = terminal.Reset();
mapStyle = terminal.Process("\x1b[1;33;41mX");
var mapRun = mapStyle.Lines[0].Runs[0];
Require(mapRun.Text.StartsWith("X", StringComparison.Ordinal) &&
        mapRun.Foreground == ClassicTerminalColor.DarkYellow &&
        mapRun.Background == ClassicTerminalColor.DarkRed &&
        mapRun.Bold,
        "wcc-foreground-background-sgr");
checks.Add("wcc-foreground-background-sgr");

Require(ClassicTerminalPalette.ResolveForeground(mapRun.Foreground, mapRun.Bold) == ClassicTerminalColor.Yellow,
        "wcc-sgr1-bright-foreground");
Require(ClassicTerminalPalette.ResolveForeground(ClassicTerminalColor.DarkRed, false) == ClassicTerminalColor.DarkRed,
        "wcc-normal-red-stays-dark");
checks.Add("wcc-dos-intensity-palette");

terminal.Reset();
terminal.Process("ABC");
var cleared = terminal.Process("\x1b[2J");
Require(PlainLine(cleared, 0).Trim().Length == 0, "wcc-full-screen-clear");
checks.Add("wcc-full-screen-clear");

terminal.Reset();
var erasedLine = terminal.Process("ABCDE\x1b[3D\x1b[K");
Require(PlainLine(erasedLine, 0).StartsWith("AB   ", StringComparison.Ordinal), "wcc-erase-to-end-line");
checks.Add("wcc-erase-to-end-line");

terminal.Reset();
var rewind79 = terminal.Process("\x1b[80GZ\x1b[79DA");
Require(PlainLine(rewind79, 0)[0] == 'A' && PlainLine(rewind79, 0)[79] == 'Z', "wcc-79-column-rewind");
checks.Add("wcc-79-column-rewind");

terminal.Reset();
var positioned = terminal.Process("\x1b[6;1HX");
Require(PlainLine(positioned, 5)[0] == 'X' && positioned.CursorRow == 5 && positioned.CursorColumn == 1,
        "wcc-absolute-cursor-position");
checks.Add("wcc-absolute-cursor-position");

terminal.Reset();
var restored = terminal.Process("\x1b[5;10H\x1b[s\x1b[1;1H!\x1b[uX");
Require(PlainLine(restored, 0)[0] == '!' && PlainLine(restored, 4)[9] == 'X', "wcc-save-restore-cursor");
checks.Add("wcc-save-restore-cursor");

terminal.Reset();
var scrolled = terminal.Process("\x1b[22;24r\x1b[22;1HA\x1b[23;1HB\x1b[24;1HC\n");
Require(PlainLine(scrolled, 21)[0] == 'B' && PlainLine(scrolled, 22)[0] == 'C' && PlainLine(scrolled, 23)[0] == ' ',
        "wcc-scroll-region");
checks.Add("wcc-scroll-region");

terminal.Reset();
var carriageReturn = terminal.Process("ABC\rZ");
Require(PlainLine(carriageReturn, 0).StartsWith("ZBC", StringComparison.Ordinal), "wcc-carriage-return-rewrite");
checks.Add("wcc-carriage-return-rewrite");

terminal.Reset();
var wrapped = terminal.Process(new string('A', 80) + "B");
Require(PlainLine(wrapped, 0) == new string('A', 80) && PlainLine(wrapped, 1)[0] == 'B', "wcc-column-80-autowrap");
checks.Add("wcc-column-80-autowrap");

var cp437 = new CodePage437Encoding();
var decodedMapGlyphs = cp437.GetString(new byte[] { 0xC9, 0xCD, 0xBB, 0xBA, 0xDB, 0xBA, 0xC8, 0xCD, 0xBC });
Require(decodedMapGlyphs == "╔═╗║█║╚═╝", "wcc-cp437-byte-decoding");
checks.Add("wcc-cp437-byte-decoding");

terminal.Reset();
var cp437Glyphs = terminal.Process(decodedMapGlyphs);
Require(PlainLine(cp437Glyphs, 0).StartsWith("╔═╗║█║╚═╝", StringComparison.Ordinal), "wcc-cp437-glyph-preservation");
checks.Add("wcc-cp437-glyph-preservation");

Console.WriteLine("RealmGate Client Core ANSI Regression");
foreach (var check in checks) Console.WriteLine("PASS " + check);
Console.WriteLine($"Passed {checks.Count} checks.");
return 0;

static string PlainLine(ClassicTerminalSnapshot snapshot, int row) =>
    string.Concat(snapshot.Lines[row].Runs.Select(run => run.Text));

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidOperationException(code);
}