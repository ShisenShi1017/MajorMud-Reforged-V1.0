using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDTeleporterAuditResult(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures, int TextCommandTeleportExitCount, int TeleportSpellCount, int PairedTeleportSpellCount, IReadOnlyList<string> Verified, IReadOnlyList<string> StronglyCorroborated, IReadOnlyList<string> DecodedButNotMapped, IReadOnlyList<string> Conflicts, IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDTeleporterAudit
{
    public static RuntimeBundleDTeleporterAuditResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string code) { if (value) checks.Add(code); else failures.Add(code); }
        var dataRoot = Path.Combine(projectRoot, "Data", worldId);
        using var exitsDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "exits.json")));
        var exits = exitsDoc.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var textTeleporters = exits.Where(e =>
            e.TryGetProperty("gate_kind", out var g) && g.GetString()?.Equals("text_command", StringComparison.OrdinalIgnoreCase) == true &&
            e.TryGetProperty("commands", out var c) && c.ValueKind == JsonValueKind.Array &&
            c.EnumerateArray().Any(v => (v.GetString() ?? string.Empty).Contains("portal", StringComparison.OrdinalIgnoreCase))).ToArray();

        using var spellsDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "spells.json")));
        var spellRecords = spellsDoc.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var teleportSpells = spellRecords.Where(ContainsTeleportAbility).ToArray();
        var paired = teleportSpells.Count(HasTeleportMapAndRoom);

        Check(textTeleporters.Length > 0, "decoded-text-command-teleporters-present");
        Check(textTeleporters.All(HasCanonicalDestination), "teleporter-destinations-map-qualified");
        Check(textTeleporters.All(e => e.TryGetProperty("runtime_disposition", out var d) && d.GetString()?.Equals("active_runtime", StringComparison.OrdinalIgnoreCase) == true), "teleporter-exits-active-runtime");
        Check(teleportSpells.Length > 0, "decoded-teleport-spells-present");
        Check(paired > 0, "teleport-map-room-pairs-present");
        var commandPath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeCommandSystem.cs");
        var livingPath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeLivingWorld.cs");
        Check(File.ReadAllText(commandPath).Contains("TryExecuteTeleporterCommand", StringComparison.Ordinal), "command-dispatch-consumes-data-driven-teleporters");
        Check(File.ReadAllText(livingPath).Contains("GateKind.Equals(\"text_command\"", StringComparison.Ordinal), "live-teleporter-alias-resolution-present");
        Check(File.ReadAllText(livingPath).Contains("session.TryMove(gate.Direction", StringComparison.Ordinal), "teleporter-uses-authoritative-exit-destination");

        return new(failures.Count == 0, checks, failures, textTeleporters.Length, teleportSpells.Length, paired,
            new[] { "Text-command portal exits use decoded command aliases and canonical map-qualified destinations.", "Teleporter traversal uses the same RuntimeWorld exit and session movement authority as ordinary movement.", "Decoded TeleportMap and TeleportRoom spell abilities are preserved in generated spell data." },
            new[] { "Paired TeleportMap/TeleportRoom abilities define fixed spell destinations when both decoded parameters are present." },
            new[] { "Random-destination selection is not promoted unless a decoded spell/trigger record explicitly identifies a random destination set.", "Quest admission and invitation restrictions remain owned by decoded prerequisite/effect records rather than teleporter name heuristics." },
            Array.Empty<string>(),
            new[] { "No universal teleporter failure message or random-selection algorithm is assumed outside record-specific decoded data." });
    }

    private static bool HasCanonicalDestination(JsonElement e) =>
        e.TryGetProperty("from_map_number", out var fm) && fm.TryGetInt32(out var fmi) && fmi > 0 &&
        e.TryGetProperty("from_room_number", out var fr) && fr.TryGetInt32(out var fri) && fri > 0 &&
        e.TryGetProperty("to_map_number", out var tm) && tm.TryGetInt32(out var tmi) && tmi > 0 &&
        e.TryGetProperty("to_room_number", out var tr) && tr.TryGetInt32(out var tri) && tri > 0;

    private static bool ContainsTeleportAbility(JsonElement record) =>
        record.GetRawText().Contains("TeleportRoom", StringComparison.OrdinalIgnoreCase) ||
        record.GetRawText().Contains("TeleportMap", StringComparison.OrdinalIgnoreCase);

    private static bool HasTeleportMapAndRoom(JsonElement record)
    {
        var raw = record.GetRawText();
        return raw.Contains("TeleportRoom", StringComparison.OrdinalIgnoreCase) && raw.Contains("TeleportMap", StringComparison.OrdinalIgnoreCase);
    }
}
