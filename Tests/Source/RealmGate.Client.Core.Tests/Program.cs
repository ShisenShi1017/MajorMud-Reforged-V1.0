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

Console.WriteLine("RealmGate Client Core ANSI Regression");
foreach (var check in checks) Console.WriteLine("PASS " + check);
Console.WriteLine($"Passed {checks.Count} checks.");
return 0;

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidOperationException(code);
}
