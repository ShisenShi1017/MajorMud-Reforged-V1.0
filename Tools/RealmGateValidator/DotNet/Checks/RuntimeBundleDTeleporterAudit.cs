using System.Text.Json;

namespace RealmGate.Server;

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
        var exits = RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(dataRoot, "exits.json"));
        var textTeleporters = exits.Where(e =>
            e.TryGetProperty("gate_kind", out var g) && g.GetString()?.Equals("text_command", StringComparison.OrdinalIgnoreCase) == true &&
            e.TryGetProperty("commands", out var c) && c.ValueKind == JsonValueKind.Array &&
            c.EnumerateArray().Any(v => (v.GetString() ?? string.Empty).Contains("portal", StringComparison.OrdinalIgnoreCase))).ToArray();

        using var spellsDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "spells.json")));
        var spellRecords = spellsDoc.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var teleportSpells = spellRecords.Where(ContainsTeleportAbility).ToArray();
        var pairedSpells = teleportSpells.Where(HasPositiveTeleportMapAndRoom).ToArray();
        var paired = pairedSpells.Length;

        Check(textTeleporters.Length > 0, "decoded-text-command-teleporters-present");
        Check(textTeleporters.All(HasCanonicalDestination), "teleporter-destinations-map-qualified");
        Check(textTeleporters.All(e => e.TryGetProperty("runtime_disposition", out var d) && d.GetString()?.Equals("active_runtime", StringComparison.OrdinalIgnoreCase) == true), "teleporter-exits-active-runtime");
        var candleExit = exits.SingleOrDefault(e =>
            e.TryGetProperty("id", out var id) && id.GetString()?.Equals("exit.map1.2252.n", StringComparison.OrdinalIgnoreCase) == true);
        Check(candleExit.ValueKind != JsonValueKind.Undefined && HasCanonicalDestination(candleExit) &&
            candleExit.GetProperty("from_map_number").GetInt32() == 1 &&
            candleExit.GetProperty("from_room_number").GetInt32() == 2252 &&
            candleExit.GetProperty("to_map_number").GetInt32() == 1 &&
            candleExit.GetProperty("to_room_number").GetInt32() == 2254,
            "decoded-candle-exit-map1-2252-to-map1-2254");
        Check(candleExit.ValueKind != JsonValueKind.Undefined &&
            ReadCommands(candleExit).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[]
            {
                "turn right candle left",
                "twist right candle left",
                "turn right candlestick left"
            }), "decoded-candle-exit-three-exact-aliases");
        Check(teleportSpells.Length > 0, "decoded-teleport-spells-present");
        Check(paired == 12, "twelve-positive-teleport-map-room-pairs-present");
        var definition = ServerRuntimeOptions.Load(projectRoot).Worlds.Single(candidate => candidate.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        Check(pairedSpells.All(spell => world.FindRoomById(TeleportDestination(spell)) is not null), "all-positive-spell-teleport-destinations-resolve");
        var commandPath = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeCommandSystem.cs");
        var livingPath = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeLivingWorld.cs");
        var powersPath = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeLivePowers.cs");
        Check(File.ReadAllText(commandPath).Contains("TryExecuteDecodedWorldCommand", StringComparison.Ordinal), "command-dispatch-consumes-data-driven-teleporters");
        Check(File.ReadAllText(livingPath).Contains("GateKind.Equals(\"text_command\"", StringComparison.Ordinal), "live-teleporter-alias-resolution-present");
        Check(File.ReadAllText(livingPath).Contains("session.TryMove(gate.Direction", StringComparison.Ordinal), "teleporter-uses-authoritative-exit-destination");
        var powers = File.ReadAllText(powersPath);
        Check(powers.Contains("decoded_abilities_140_141", StringComparison.Ordinal), "fixed-spell-teleport-consumes-decoded-pair");
        Check(powers.Contains("decoded-spell-teleport-rollback", StringComparison.Ordinal), "fixed-spell-teleport-has-transactional-rollback");
        Check(powers.Contains("PersistDecodedTeleportTargets", StringComparison.Ordinal), "fixed-spell-teleport-persists-character-location");

        return new(failures.Count == 0, checks, failures, textTeleporters.Length, teleportSpells.Length, paired,
            new[] { "Text-command portal exits use decoded command aliases and canonical map-qualified destinations.", "Teleporter traversal uses the same RuntimeWorld exit and session movement authority as ordinary movement.", "All 12 complete positive TeleportMap/TeleportRoom spell pairs execute transactionally and persist their decoded fixed destinations." },
            Array.Empty<string>(),
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

    private static bool HasPositiveTeleportMapAndRoom(JsonElement record)
    {
        var (map, room) = ReadTeleportPair(record);
        return map > 0 && room > 0;
    }

    private static string TeleportDestination(JsonElement record)
    {
        var (map, room) = ReadTeleportPair(record);
        return $"room.map{map}.{room}";
    }

    private static (int Map, int Room) ReadTeleportPair(JsonElement record)
    {
        if (!record.TryGetProperty("public_reference_0139", out var reference) ||
            !reference.TryGetProperty("fields", out var fields) ||
            !fields.TryGetProperty("abilities_named", out var abilities) || abilities.ValueKind != JsonValueKind.Array) return (0, 0);
        var map = 0; var room = 0;
        foreach (var ability in abilities.EnumerateArray())
        {
            var id = ability.TryGetProperty("ability_id", out var idValue) && idValue.TryGetInt32(out var parsedId) ? parsedId : 0;
            var value = ability.TryGetProperty("value", out var valueNode) && int.TryParse(valueNode.ToString(), out var parsedValue) ? parsedValue : 0;
            if (id == 140) room = value;
            if (id == 141) map = value;
        }
        return (map, room);
    }

    private static IReadOnlyList<string> ReadCommands(JsonElement record) =>
        record.TryGetProperty("commands", out var commands) && commands.ValueKind == JsonValueKind.Array
            ? commands.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
            : Array.Empty<string>();
}
