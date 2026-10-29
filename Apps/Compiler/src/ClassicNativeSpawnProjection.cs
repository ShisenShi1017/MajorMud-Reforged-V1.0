using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

/// <summary>
/// Read-only projection of verified Classic source records retained in Librarium.
/// The snapshots come from original WCCMP002 room pages and the original 1,100-monster
/// catalog. No native installations or generated Data folders are modified.
/// </summary>
internal static class ClassicNativeSpawnProjection
{
    private const string RoomRelativePath = "Librarium/NativeEvidence/classic_native_room_spawn_index.br64";
    private const string MonsterRelativePath = "Librarium/NativeEvidence/classic_native_monster_group_index.br64";
    private const string RoomCapRelativePath = "Librarium/NativeEvidence/classic_native_room_local_caps.br64";
    private const string RoomCapRowsSha256 = "e45013fcca7350d9ff1561e17f780a1a116b53d58c345cb055b7fda1e760fe2b";
    private const string RoomRowsSha256 = "d51f51d17c36cdc6bb8212dfab6a6217b831f7401d0d4e473352888d3798dea1";
    private const string RoomSpellRowsSha256 = "797237c16c294e082197adecd132696c675d1fbc9409cf3bd3ca7901d0bf4fca";
    private const string RoomSpellTriggerRowsSha256 = "59cca9ddbe76aca0d5d27611ed2dfcec68ced85457b38b0de179408fb4e1deb5";
    private const string MonsterRowsSha256 = "48ac6a8ac5b4df0edd2bf2288b0c20175c06a960ceb6b7f8c4cb40a5e7856cc5";

    internal sealed record ProjectionReport(
        int NativeRooms, int NativeMonsters, int QualifiedSpawns,
        int QualifiedNpcs, int SpawnRoomsNotInSource,
        int SpawnGroupDisagreements, int NpcGroupDisagreements,
        int NativeRoomSpellReferences, int RoomSpellRecordsProjected);

    private sealed record RoomRule(int GroupId, int MinimumIndex, int MaximumIndex,
        int DirectMonsterId, int AreaCap, int LinkedAreaRoom);
    private sealed record MonsterRule(int GroupId, int IndexNumber);

    internal static ProjectionReport Apply(string repositoryRoot, IReadOnlyList<LibrariumFamily> families)
    {
        var roomRules = ReadRooms(repositoryRoot);
        var roomCaps = ReadRoomCaps(repositoryRoot);
        var roomSpells = ReadRoomSpells(repositoryRoot, roomRules);
        var roomSpellTriggers = ReadRoomSpellTriggers(repositoryRoot, roomRules);
        var monsterRules = ReadMonsters(repositoryRoot);
        if (roomCaps.Count != roomRules.Count || roomRules.Keys.Any(key => !roomCaps.ContainsKey(key)))
            throw new InvalidDataException("Native room capacity keys do not match original room source.");

        // The original Silver River record is a cross-source regression anchor:
        // native room Group 21 indexes 4..7 must admit silverfish, dragonfish
        // and water elemental, but never fungus lizard (native index 1).
        if (!roomRules.TryGetValue((1, 2342), out var river)
            || river.GroupId != 21 || river.MinimumIndex != 4 || river.MaximumIndex != 7
            || river.AreaCap != 15 || river.LinkedAreaRoom != 2342
            || !monsterRules.TryGetValue(264, out var fungus) || fungus.GroupId != 21 || fungus.IndexNumber != 1
            || !monsterRules.TryGetValue(266, out var silverfish) || silverfish.GroupId != 21 || silverfish.IndexNumber != 4
            || !monsterRules.TryGetValue(267, out var dragonfish) || dragonfish.GroupId != 21 || dragonfish.IndexNumber != 5
            || !monsterRules.TryGetValue(268, out var waterElemental) || waterElemental.GroupId != 21 || waterElemental.IndexNumber != 6)
            throw new InvalidDataException("Classic native Silver River population evidence failed reconciliation.");

        var spellLegacyIds = families
            .Where(f => f.Name.Equals("spells", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.Records)
            .Select(r => Number(r, "legacy_id")).Where(id => id > 0).ToHashSet();
        var missingSpellIds = roomSpells.Values.Concat(roomSpellTriggers.Values.Select(v => v.PicklockSpellId))
            .Distinct().Where(id => id > 0 && !spellLegacyIds.Contains(id)).ToArray();
        if (missingSpellIds.Length > 0)
            throw new InvalidDataException("Original rooms reference missing spell definitions: "
                + string.Join(", ", missingSpellIds.OrderBy(id => id)));
        var roomSpellRecordsProjected = 0;
        var qualifiedSpawns = 0;
        var qualifiedNpcs = 0;
        var missingRooms = 0;
        var spawnGroupDisagreements = 0;
        var npcGroupDisagreements = 0;

        foreach (var family in families)
        {
            if (family.Name.Equals("npcs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var record in family.Records)
                {
                    var legacyId = Number(record, "legacy_id");
                    if (!monsterRules.TryGetValue(legacyId, out var native))
                        continue;

                    if (record["behavior"] is not JsonObject behavior)
                    {
                        behavior = new JsonObject();
                        record["behavior"] = behavior;
                    }
                    if (Number(behavior, "group_id") != native.GroupId)
                        npcGroupDisagreements++;

                    behavior["group_id"] = native.GroupId;

                    if (record["public_reference_0139"] is not JsonObject reference)
                    {
                        reference = new JsonObject();
                        record["public_reference_0139"] = reference;
                    }
                    if (reference["fields"] is not JsonObject fields)
                    {
                        fields = new JsonObject();
                        reference["fields"] = fields;
                    }
                    fields["index_number"] = native.IndexNumber;
                    qualifiedNpcs++;
                }
            }
            else if (family.Name.Equals("rooms", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var record in family.Records)
                {
                    var map = Number(record, "map_number");
                    if (map <= 0) map = Number(record, "map_id");
                    var room = Number(record, "room_number");
                    if (map <= 0 || room <= 0 || !roomRules.ContainsKey((map, room))) continue;
                    var spellId = roomSpells.TryGetValue((map, room), out var original) ? original : 0;
                    if (record["environment"] is not JsonObject environment)
                    {
                        environment = new JsonObject();
                        record["environment"] = environment;
                    }
                    environment["room_spell_id"] = spellId;
                    // A native zero must survive every later environment overlay.
                    environment["native_room_spell_verified"] = true;
                    var trigger = roomSpellTriggers.GetValueOrDefault((map, room));
                    environment["native_room_spell_entry_on_change"] = trigger?.EntryOnChange ?? false;
                    environment["native_room_picklock_spell_id"] = trigger?.PicklockSpellId ?? 0;
                    roomSpellRecordsProjected++;
                }
            }
            else if (family.Name.Equals("spawns", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var record in family.Records)
                {
                    if (record["spawn_model"] is not JsonObject model)
                        continue;

                    var map = Number(record, "map_id");
                    var room = Number(record, "room_number");
                    if (map <= 0) map = Number(model, "map_id");
                    if (map <= 0 || room <= 0 || !roomRules.TryGetValue((map, room), out var native))
                    {
                        missingRooms++;
                        continue;
                    }

                    // Never invent a direct/boss NPC identity or a permanent home room.
                    // Group 0 means this native room has no ordinary group regeneration.
                    if (Number(model, "monster_type") != native.GroupId)
                        spawnGroupDisagreements++;
                    model["monster_type"] = native.GroupId;
                    model["native_room_generation_cap"] = roomCaps[(map, room)];
                    model["monster_index_min"] = native.MinimumIndex;
                    model["monster_index_max"] = native.MaximumIndex;
                    model["group_index"] = string.Format(CultureInfo.InvariantCulture,
                        "{0}-{1}-{2}", native.GroupId, native.MinimumIndex, native.MaximumIndex);

                    // Native +0x5BE and +0x5C4 are separate area population limits
                    // and linked controller references. Preserve them without
                    // introducing any artificial NPC home-room assignment.
                    model["max_area_cap"] = native.AreaCap;
                    // The original area gate reads the designated controller's
                    // cap, not the minimum across all rooms in that area.
                    roomRules.TryGetValue((map, native.LinkedAreaRoom), out var controller);
                    var controllerResolved = native.LinkedAreaRoom > 0 && controller is not null;
                    model["native_area_controller_resolved"] = controllerResolved;
                    model["native_area_controller_cap"] = controllerResolved ? controller!.AreaCap : 0;
                    model["linked_spawn_room"] = native.LinkedAreaRoom;
                    model["linked_spawn_map_id"] = native.LinkedAreaRoom > 0 ? map : 0;
                    model["linked_population_key"] = native.LinkedAreaRoom > 0
                        ? $"map{map}-room{native.LinkedAreaRoom}" : string.Empty;

                    // Native +0x468 is the direct monster argument to _generate_monster.
                    // Restore an exact direct identity only when the original
                    // 1,100-monster catalog contains that identity.
                    if (native.DirectMonsterId > 0 &&
                        monsterRules.ContainsKey(native.DirectMonsterId))
                        record["npc_legacy_id"] = native.DirectMonsterId;

                    qualifiedSpawns++;
                }
            }
        }

        if (qualifiedNpcs < 1000 || qualifiedSpawns == 0)
            throw new InvalidDataException("Native Classic spawn projection is unexpectedly incomplete.");

        return new ProjectionReport(roomRules.Count, monsterRules.Count, qualifiedSpawns,
            qualifiedNpcs, missingRooms, spawnGroupDisagreements, npcGroupDisagreements,
            roomSpells.Count, roomSpellRecordsProjected);
    }

    private static Dictionary<(int Map, int Room), RoomRule> ReadRooms(string root)
    {
        var rows = LoadVerifiedLines(root, RoomRelativePath, RoomRowsSha256);
        var result = new Dictionary<(int, int), RoomRule>();
        foreach (var line in rows)
        {
            var n = ParseNumbers(line, 8);
            if (n[0] <= 0 || n[1] <= 0 || n[3] > n[4])
                throw new InvalidDataException("Native room spawn index has invalid map/room/index values.");
            if (!result.TryAdd((n[0], n[1]), new RoomRule(n[2], n[3], n[4], n[5], n[6], n[7])))
                throw new InvalidDataException("Duplicate native room spawn index key.");
        }
        if (result.Count != 13360)
            throw new InvalidDataException($"Expected 13,360 native rooms; recovered {result.Count}.");
        return result;
    }

    private static Dictionary<(int Map, int Room), int> ReadRoomSpells(
        string root, IReadOnlyDictionary<(int Map, int Room), RoomRule> rooms)
    {
        var rows = LoadVerifiedLines(root, RoomRelativePath, RoomSpellRowsSha256, 1);
        var spells = new Dictionary<(int Map, int Room), int>();
        foreach (var line in rows)
        {
            var n = ParseNumbers(line, 3);
            if (n[0] <= 0 || n[1] <= 0 || n[2] <= 0 || n[2] > ushort.MaxValue
                || !rooms.ContainsKey((n[0], n[1])) || !spells.TryAdd((n[0], n[1]), n[2]))
                throw new InvalidDataException("Invalid or duplicate original room spell identity.");
        }
        if (spells.Count != 1865 || spells.GetValueOrDefault((1, 288)) != 753
            || spells.GetValueOrDefault((1, 2342)) != 753)
            throw new InvalidDataException("Original room spell evidence failed reconciliation.");
        return spells;
    }

    private sealed record RoomSpellTrigger(bool EntryOnChange, int PicklockSpellId);

    private static Dictionary<(int Map, int Room), RoomSpellTrigger> ReadRoomSpellTriggers(
        string root, IReadOnlyDictionary<(int Map, int Room), RoomRule> rooms)
    {
        var rows = LoadVerifiedLines(root, RoomRelativePath, RoomSpellTriggerRowsSha256, 2);
        var result = new Dictionary<(int Map, int Room), RoomSpellTrigger>();
        foreach (var line in rows)
        {
            var n = ParseNumbers(line, 4);
            if (!rooms.ContainsKey((n[0], n[1])) || n[2] is not (0 or 1)
                || n[3] < 0 || n[3] > ushort.MaxValue
                || (n[2] == 0 && n[3] == 0)
                || !result.TryAdd((n[0], n[1]), new(n[2] == 1, n[3])))
                throw new InvalidDataException("Native room spell entry/picklock evidence is invalid.");
        }
        if (result.Count != 116 || result.Values.Count(x => x.EntryOnChange) != 97
            || result.Values.Count(x => x.PicklockSpellId > 0) != 19
            || result.GetValueOrDefault((1, 288)) is not null
            || result.GetValueOrDefault((1, 2342)) is not null)
            throw new InvalidDataException("Original room spell entry/picklock evidence disagrees with DLL.");
        return result;
    }

    private static Dictionary<(int Map, int Room), int> ReadRoomCaps(string root)
    {
        var rows = LoadVerifiedLines(root, RoomCapRelativePath, RoomCapRowsSha256);
        var result = new Dictionary<(int, int), int>();
        foreach (var line in rows)
        {
            var n = ParseNumbers(line, 3);
            if (n[0] <= 0 || n[1] <= 0 || n[2] < 0 || n[2] > 15
                || !result.TryAdd((n[0], n[1]), n[2]))
                throw new InvalidDataException("Invalid or duplicated native room capacity.");
        }
        if (result.Count != 13360)
            throw new InvalidDataException("Native room caps must cover exactly 13,360 original rooms.");
        return result;
    }

    private static Dictionary<int, MonsterRule> ReadMonsters(string root)
    {
        var rows = LoadVerifiedLines(root, MonsterRelativePath, MonsterRowsSha256);
        var result = new Dictionary<int, MonsterRule>();
        foreach (var line in rows)
        {
            var n = ParseNumbers(line, 3);
            if (n[0] <= 0 || !result.TryAdd(n[0], new MonsterRule(n[1], n[2])))
                throw new InvalidDataException("Invalid or duplicate native monster index key.");
        }
        if (result.Count != 1100)
            throw new InvalidDataException($"Expected 1,100 native monsters; recovered {result.Count}.");
        return result;
    }

    private static string[] LoadVerifiedLines(string repositoryRoot, string relativePath, string expectedSha256, int sectionIndex = 0)
    {
        var path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException("Classic source-derived spawn index is missing.", path);
        var sections = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (sectionIndex >= sections.Length)
            throw new InvalidDataException($"Classic source index section {sectionIndex} is missing: {relativePath}");
        var compressed = Convert.FromBase64String(sections[sectionIndex].Trim());
        using var source = new MemoryStream(compressed);
        using var brotli = new BrotliStream(source, CompressionMode.Decompress);
        using var destination = new MemoryStream();
        brotli.CopyTo(destination);
        var raw = destination.ToArray();
        var actualSha = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        if (!actualSha.Equals(expectedSha256, StringComparison.Ordinal))
            throw new InvalidDataException($"Classic source index checksum mismatch: {relativePath}");
        return Encoding.ASCII.GetString(raw).Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static int[] ParseNumbers(string line, int length)
    {
        var parts = line.Split(':');
        if (parts.Length != length)
            throw new InvalidDataException("Malformed native index record.");
        var values = new int[length];
        for (var i = 0; i < length; i++)
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                throw new InvalidDataException("Invalid native index integer.");
        return values;
    }

    private static int Number(JsonObject obj, string key) =>
        int.TryParse(obj[key]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : 0;
}
