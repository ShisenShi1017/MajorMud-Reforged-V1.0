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

Console.WriteLine("RealmGate Client Core ANSI Regression");
foreach (var check in checks) Console.WriteLine("PASS " + check);
Console.WriteLine($"Passed {checks.Count} checks.");
return 0;

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidOperationException(code);
}
