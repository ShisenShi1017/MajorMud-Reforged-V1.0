using System.Text.Json;
using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

public static class NativeHelpProjectionBuilder
{
    private const string EvidenceRelativePath = "Librarium/Evidence/native_message_catalog_correlation_0022.json";

    public static LibrariumFamily? Build(string repositoryRoot, List<CompilerDiagnostic> diagnostics)
    {
        var path = Path.Combine(repositoryRoot, EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "HELP001", "Decoded MajorMUD native help evidence is missing.", path));
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var catalog = document.RootElement.GetProperty("catalogs").EnumerateArray().SingleOrDefault(candidate =>
            string.Equals(candidate.GetProperty("mcv_file").GetString(), "WCCMMHLP.mcv", StringComparison.OrdinalIgnoreCase));
        if (catalog.ValueKind == JsonValueKind.Undefined)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "HELP002", "WCCMMHLP native help catalog was not found in retained evidence.", path));
            return null;
        }

        var entries = catalog.GetProperty("entries").EnumerateArray()
            .Where(entry => entry.TryGetProperty("defined", out var defined) && defined.GetBoolean())
            .Select(entry => new NativeHelpEntry(
                entry.GetProperty("message_number").GetInt32(),
                entry.TryGetProperty("decoded_cp437", out var text) ? text.GetString() ?? string.Empty : string.Empty))
            .Where(entry => entry.Text.Length > 0)
            .ToDictionary(entry => entry.Number);

        if (entries.Count != 263 || !entries.TryGetValue(1, out var main))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "HELP003", $"Expected 263 nonempty WCCMMHLP records including message 1; found {entries.Count}.", path));
            return null;
        }

        var records = new List<JsonObject>
        {
            new()
            {
                ["id"] = "help.main",
                ["record_kind"] = "main",
                ["body_message_number"] = main.Number,
                ["body"] = main.Text,
                ["evidence_classification"] = "proven_native_help_payload"
            }
        };

        for (var titleNumber = 2; titleNumber <= 262; titleNumber += 2)
        {
            if (!entries.TryGetValue(titleNumber, out var title) || !entries.TryGetValue(titleNumber + 1, out var body))
            {
                diagnostics.Add(new(DiagnosticSeverity.Error, "HELP004", $"Native help title/body pair {titleNumber}/{titleNumber + 1} is incomplete.", path));
                continue;
            }

            records.Add(new JsonObject
            {
                ["id"] = $"help.topic.{body.Number}",
                ["record_kind"] = "topic",
                ["title_message_number"] = title.Number,
                ["body_message_number"] = body.Number,
                ["native_keywords"] = title.Text,
                ["body"] = body.Text,
                ["evidence_classification"] = "proven_native_help_pair"
            });
        }

        if (records.Count != 132)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "HELP005", $"Expected one native help main record plus 131 topic records; found {records.Count}.", path));
            return null;
        }

        diagnostics.Add(new(DiagnosticSeverity.Info, "HELP100", "Generated one native help main record and 131 exact title/body topic records.", path));
        return new LibrariumFamily("native_help", path, records);
    }

    private sealed record NativeHelpEntry(int Number, string Text);
}
