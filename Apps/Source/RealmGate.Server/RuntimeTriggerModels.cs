using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealmGate.Server;

public sealed record RuntimeMovementTrigger(
    string Id,
    int FromMapId,
    int FromRoomNumber,
    int ToMapId,
    int ToRoomNumber,
    string Direction,
    string RuntimeStatus,
    IReadOnlyList<string> EvidenceResults,
    IReadOnlyList<string> Clues,
    bool TargetRoomExists)
{
    private static readonly Regex IdPattern = new(@"^trigger\.movement_(?<from>\d+)_(?<direction>[a-z]+)_(?<to>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public RuntimeRoomKey SourceKey => new(FromMapId, FromRoomNumber);
    public RuntimeRoomKey TargetKey => new(ToMapId, ToRoomNumber);
    public bool HasInvalidDestination => EvidenceResults.Any(value => value.Equals("invalid_destination", StringComparison.OrdinalIgnoreCase));
    public bool HasUnresolvedPrerequisiteEvidence => EvidenceResults.Any(value => value.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase));

    public static RuntimeMovementTrigger FromJson(JsonElement element, IReadOnlyDictionary<RuntimeRoomKey, RuntimeRoom> roomsByKey)
    {
        var id = JsonValueReader.ReadString(element, "id", string.Empty);
        var fromMap = JsonValueReader.ReadInt(element, "from_map_number", 0);
        var from = JsonValueReader.ReadInt(element, "from_room_number", 0);
        var toMap = JsonValueReader.ReadInt(element, "to_map_number", 0);
        var to = JsonValueReader.ReadInt(element, "to_room_number", 0);
        var direction = JsonValueReader.ReadString(element, "direction", string.Empty);

        if (element.TryGetProperty("relationships", out var relationships) && relationships.ValueKind == JsonValueKind.Object)
        {
            fromMap = JsonValueReader.ReadInt(relationships, "from_map", fromMap);
            from = JsonValueReader.ReadInt(relationships, "from_room", from);
            toMap = JsonValueReader.ReadInt(relationships, "to_map", toMap);
            to = JsonValueReader.ReadInt(relationships, "to_room", to);
        }

        var match = IdPattern.Match(id);
        if (match.Success)
        {
            if (from <= 0) int.TryParse(match.Groups["from"].Value, out from);
            if (to <= 0) int.TryParse(match.Groups["to"].Value, out to);
            if (string.IsNullOrWhiteSpace(direction)) direction = match.Groups["direction"].Value;
        }

        // A trigger without map identity is never attached by room number alone. It remains loaded
        // as evidence, but cannot govern movement until the compiler emits a map-qualified relationship.
        var sourceStatus = JsonValueReader.ReadString(element, "runtime_disposition", string.Empty);
        if (element.TryGetProperty("runtime_mapping", out var mapping) && mapping.ValueKind == JsonValueKind.Object)
            sourceStatus = JsonValueReader.ReadString(mapping, "status", sourceStatus);

        var evidenceResults = ReadStrings(element, "evidence_results");
        var clues = new HashSet<string>(ReadStrings(element, "clues"), StringComparer.OrdinalIgnoreCase);
        if (element.TryGetProperty("evidence", out var evidence) && evidence.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in evidence.EnumerateArray())
            {
                var result = JsonValueReader.ReadString(item, "result", string.Empty);
                if (!string.IsNullOrWhiteSpace(result)) evidenceResults.Add(result);
                foreach (var clue in JsonValueReader.ReadString(item, "clues", string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) clues.Add(clue);
            }
        }

        var qualified = fromMap > 0 && from > 0 && toMap > 0 && to > 0;
        var runtimeStatus = !qualified ? "loaded-unqualified-evidence" : sourceStatus.Equals("pending", StringComparison.OrdinalIgnoreCase) ? "loaded-evidence-projection" : string.IsNullOrWhiteSpace(sourceStatus) ? "loaded" : sourceStatus;
        return new RuntimeMovementTrigger(id, fromMap, from, toMap, to, NormalizeDirection(direction), runtimeStatus, evidenceResults, clues.OrderBy(x => x).ToArray(), qualified && roomsByKey.ContainsKey(new RuntimeRoomKey(toMap, to)));
    }

    private static List<string> ReadStrings(JsonElement element, string name)
    {
        var values = new List<string>();
        if (element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array)
            foreach (var value in array.EnumerateArray()) if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())) values.Add(value.GetString()!);
        return values;
    }

    private static string NormalizeDirection(string value) => value.Trim().ToUpperInvariant() switch { "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W", "UP" => "U", "DOWN" => "D", var direction => direction };
}
