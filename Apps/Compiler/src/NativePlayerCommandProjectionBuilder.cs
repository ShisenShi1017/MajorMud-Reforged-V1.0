using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MajorMud.Reforged.Compiler;

public static partial class NativePlayerCommandProjectionBuilder
{
    private const string EvidenceRelativePath = "Librarium/Evidence/native_message_catalog_correlation_0022.json";

    private static readonly string[] SummaryCommands =
    [
        "north", "east", "south", "west", "northeast", "southeast", "southwest", "northwest",
        "sneak", "hide", "drag", "go",
        "look", "status", "inventory", "bank", "topten", "statline", "help", "search", "health",
        "brief", "verbose", "map", "profile", "keys", "track",
        "attack", "bash", "punch", "kick", "jumpkick", "backstab", "break", "aid",
        "wear", "remove", "use", "get", "light", "drop", "stash", "give",
        "list", "buy", "sell",
        "invite", "join", "follow", "party",
        "spells", "cast", "mana", "invoke", "powers", "talk",
        "suicide", "reroll", "train", "greet", "ask"
    ];

    public static LibrariumFamily? Build(string repositoryRoot, List<CompilerDiagnostic> diagnostics)
    {
        var path = Path.Combine(repositoryRoot, EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "CMD001", "Decoded MajorMUD native command evidence is missing.", path));
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var catalog = document.RootElement.GetProperty("catalogs").EnumerateArray().SingleOrDefault(candidate =>
            string.Equals(candidate.GetProperty("mcv_file").GetString(), "WCCMMHLP.mcv", StringComparison.OrdinalIgnoreCase));
        if (catalog.ValueKind == JsonValueKind.Undefined)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "CMD002", "WCCMMHLP native command catalog was not found in retained evidence.", path));
            return null;
        }

        var entries = catalog.GetProperty("entries").EnumerateArray()
            .Where(entry => entry.TryGetProperty("defined", out var defined) && defined.GetBoolean())
            .ToDictionary(
                entry => entry.GetProperty("message_number").GetInt32(),
                entry => entry.TryGetProperty("decoded_cp437", out var text) ? text.GetString() ?? string.Empty : string.Empty);

        if (!entries.TryGetValue(3, out var summary) || !entries.TryGetValue(123, out var broadcasting))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "CMD003", "Native command summary 3 or broadcasting help 123 is missing.", path));
            return null;
        }

        var normalizedSummary = Normalize(summary);
        var missing = SummaryCommands.Where(command => !ContainsWord(normalizedSummary, command)).ToArray();
        if (missing.Length > 0)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "CMD004", $"Native command summary is missing expected identities: {string.Join(", ", missing)}.", path));
            return null;
        }

        var normalizedBroadcasting = Normalize(broadcasting);
        if (!normalizedSummary.Contains("backstab (bs)", StringComparison.Ordinal) ||
            !normalizedBroadcasting.Contains("broadcast (or br)", StringComparison.Ordinal) ||
            !normalizedBroadcasting.Contains("broadgang (bg)", StringComparison.Ordinal))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "CMD005", "One or more explicit native aliases (BS, BR, BG) are not proven by the retained help payloads.", path));
            return null;
        }

        var records = SummaryCommands
            .Select(command => CreateRecord(command, 3, command switch
            {
                "backstab" => ["bs"],
                _ => []
            }))
            .ToList();
        records.Add(CreateRecord("broadcast", 123, ["br"]));
        records.Add(CreateRecord("broadgang", 123, ["bg"]));

        if (records.Count != 63 || records.Select(record => record["documented_input"]!.GetValue<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != 63)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "CMD006", $"Expected 63 unique native player-command identities; found {records.Count}.", path));
            return null;
        }

        diagnostics.Add(new(DiagnosticSeverity.Info, "CMD100", "Generated 63 native player-command identities with three explicit aliases.", path));
        return new LibrariumFamily("native_player_commands", path, records);
    }

    private static JsonObject CreateRecord(string command, int sourceMessage, string[] aliases) => new()
    {
        ["id"] = $"native-command.{command}",
        ["documented_input"] = command,
        ["explicit_aliases"] = new JsonArray(aliases.Select(alias => JsonValue.Create(alias)).ToArray()),
        ["source_message_number"] = sourceMessage,
        ["resolution_boundary"] = aliases.Length == 0
            ? "full_name_proven_prefix_rules_open"
            : "full_name_and_listed_aliases_proven_other_prefix_rules_open",
        ["evidence_classification"] = "proven_native_help_identity"
    };

    private static string Normalize(string value) =>
        AnsiPattern().Replace(value, string.Empty).Replace('\r', ' ').Replace('\n', ' ').ToLowerInvariant();

    private static bool ContainsWord(string text, string value) =>
        Regex.IsMatch(text, $@"(?<![a-z]){Regex.Escape(value)}(?![a-z])", RegexOptions.CultureInvariant);

    [GeneratedRegex("\\x1B\\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiPattern();
}
