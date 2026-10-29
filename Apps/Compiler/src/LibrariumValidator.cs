using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

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


                if (string.Equals(family.Name, "spawns", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateSpawnIdentity(record, i, family.SourceFile, diagnostics);
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


    private static void ValidateSpawnIdentity(JsonObject record, int index, string sourceFile, List<CompilerDiagnostic> diagnostics)
    {
        var mapId = GetInt(record, "map_id");
        var roomNumber = GetInt(record, "room_number");
        if (mapId <= 0 || roomNumber <= 0)
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "SPN001", $"Spawn record {index} must define positive map_id and room_number.", sourceFile));
            return;
        }

        if (record["spawn_model"] is not JsonObject model) return;

        // A qualified native monster group must preserve its index range.
        // Do not accept malformed qualifiers that could silently expand a lair
        // into every member of its monster group.
        var minIndex = GetInt(model, "monster_index_min");
        var maxIndex = GetInt(model, "monster_index_max");
        if ((minIndex == 0) != (maxIndex == 0)
            || minIndex < 0 || maxIndex < minIndex)
            diagnostics.Add(new(DiagnosticSeverity.Error, "SPN003", $"Spawn record {index} has an invalid monster index range.", sourceFile));

        var groupIndex = GetString(model, "group_index");
        if (!string.IsNullOrWhiteSpace(groupIndex))
        {
            var parts = groupIndex.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || !int.TryParse(parts[0], out var qualifiedGroup)
                || !int.TryParse(parts[1], out var qualifiedMin)
                || !int.TryParse(parts[2], out var qualifiedMax)
                || qualifiedGroup != GetInt(model, "monster_type")
                || qualifiedMin <= 0 || qualifiedMax < qualifiedMin
                || (minIndex > 0 && (minIndex != qualifiedMin || maxIndex != qualifiedMax)))
                diagnostics.Add(new(DiagnosticSeverity.Error, "SPN004", $"Spawn record {index} has an inconsistent or invalid native group_index.", sourceFile));
        }

        var linkedRoom = GetInt(model, "linked_spawn_room");
        if (linkedRoom <= 0) return;

        var linkedMap = GetInt(model, "linked_spawn_map_id");
        var key = GetString(model, "linked_population_key");
        var expected = $"map{mapId}-room{linkedRoom}";
        if (linkedMap != mapId || !string.Equals(key, expected, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new(DiagnosticSeverity.Error, "SPN002", $"Spawn record {index} has an unqualified or inconsistent linked population key; expected '{expected}'.", sourceFile));
        }
    }

    private static int GetInt(JsonObject obj, string key)
    {
        var value = GetString(obj, key);
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static string? GetString(JsonObject obj, string key)
        => obj.TryGetPropertyValue(key, out var node) ? node?.ToString() : null;
}
