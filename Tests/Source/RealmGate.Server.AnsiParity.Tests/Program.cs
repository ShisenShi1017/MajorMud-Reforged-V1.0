using System.Text.RegularExpressions;
using RealmGate.Server;

var checks = new List<string>();

Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(0, 1000) == "None", "encumbrance-zero-none");
Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(160, 1000) == "None", "encumbrance-16-none");
Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(170, 1000) == "Light", "encumbrance-17-light");
Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(330, 1000) == "Light", "encumbrance-33-light");
Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(340, 1000) == "Medium", "encumbrance-34-medium");
Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(660, 1000) == "Medium", "encumbrance-66-medium");
Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(670, 1000) == "Heavy", "encumbrance-67-heavy");
checks.Add("wcc-encumbrance-thresholds");

const string reset = "\u001b[0m";
const string green = "\u001b[0;32m";
const string cyan = "\u001b[36m";
const string red = "\u001b[31m";
const string yellow = "\u001b[33m";
const string bandGreen = "\u001b[32m";
const string white = "\u001b[37m";

Require(
    RuntimeMajorMudCompatibility.FormatEncumberanceLineAnsi(100, 1000) ==
    $"{green}Encumbrance: {cyan}100/1000 - {white}None [10%]{reset}",
    "ansi-encumbrance-none");
Require(
    RuntimeMajorMudCompatibility.FormatEncumberanceLineAnsi(230, 1000) ==
    $"{green}Encumbrance: {cyan}230/1000 - {bandGreen}Light [23%]{reset}",
    "ansi-encumbrance-light");
Require(
    RuntimeMajorMudCompatibility.FormatEncumberanceLineAnsi(500, 1000) ==
    $"{green}Encumbrance: {cyan}500/1000 - {yellow}Medium [50%]{reset}",
    "ansi-encumbrance-medium");
Require(
    RuntimeMajorMudCompatibility.FormatEncumberanceLineAnsi(800, 1000) ==
    $"{green}Encumbrance: {cyan}800/1000 - {red}Heavy [80%]{reset}",
    "ansi-encumbrance-heavy");
checks.Add("wcc-encumbrance-ansi-colors");

Require(
    RuntimeMajorMudCompatibility.FormatWealthLineAnsi("41 gold crowns") ==
    $"{green}Wealth: {cyan}41 gold crowns{reset}",
    "ansi-wealth-colors");
checks.Add("wcc-wealth-ansi-colors");

var visible = StripAnsi(RuntimeMajorMudCompatibility.FormatEncumberanceLineAnsi(230, 1000));
Require(visible == RuntimeMajorMudCompatibility.FormatEncumberanceLine(230, 1000), "ansi-visible-text-preserved");
checks.Add("ansi-visible-text-preserved");

Console.WriteLine("RealmGate Server WCC ANSI Parity Regression");
foreach (var check in checks) Console.WriteLine("PASS " + check);
Console.WriteLine($"Passed {checks.Count} checks.");
return 0;

static string StripAnsi(string value) => Regex.Replace(value, "\\x1B\\[[0-?]*[ -/]*[@-~]", string.Empty);

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidOperationException(code);
}