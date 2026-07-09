using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

public sealed class LibrariumValidator
{
    public void Validate(IReadOnlyList<LibrariumFamily> families, List<CompilerDiagnostic> diagnostics)
    {
        if (families.Count == 0)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "VAL001", "No Librarium object families were available to compile."));
            return;
        }

        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in families)
        {
            if (family.Records.Count == 0)
            {
                diagnostics.Add(new(DiagnosticSeverity.Warning, "VAL002", $"Family '{family.Name}' has no exportable records.", family.SourceFile));
                continue;
            }

            for (var i = 0; i < family.Records.Count; i++)
            {
                var record = family.Records[i];
                var id = GetString(record, "id") ?? GetString(record, "mechanic_id") ?? GetString(record, "legacy_id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostics.Add(new(DiagnosticSeverity.Warning, "VAL003", $"Family '{family.Name}' record {i} has no stable id; compiler will synthesize one.", family.SourceFile));
                    continue;
                }

                var composite = $"{family.Name}:{id}";
                if (ids.TryGetValue(composite, out var existing))
                {
                    diagnostics.Add(new(DiagnosticSeverity.Warning, "VAL004", $"Duplicate record id '{id}' in family '{family.Name}'. First seen in {existing}.", family.SourceFile));
                }
                else
                {
                    ids[composite] = family.SourceFile;
                }
            }
        }
    }

    private static string? GetString(JsonObject obj, string key)
        => obj.TryGetPropertyValue(key, out var node) ? node?.ToString() : null;
}
