using System.Text.Json;

namespace RealmGate.Server;

/// <summary>
/// Deterministic admission checks for source-qualified Classic lair candidates.
/// Uses exact native group/index fixtures and the recovered group-roll thresholds.
/// </summary>
public static class RuntimeClassicSpawnEligibilitySmokeReport
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();
        var members = new RuntimeNpcDefinition[]
        {
            new() { Id = "npc.264", LegacyId = 264, GroupId = 21, IndexNumber = 1, Name = "fungus lizard" },
            new() { Id = "npc.265", LegacyId = 265, GroupId = 21, IndexNumber = 2, Name = "vampire bat" },
            new() { Id = "npc.266", LegacyId = 266, GroupId = 21, IndexNumber = 4, Name = "silverfish" }
        };
        var byId = members.ToDictionary(member => member.LegacyId);
        var byGroup = new Dictionary<int, IReadOnlyList<RuntimeNpcDefinition>> { [21] = members };

        RuntimeNpcSpawn Decode(string model)
        {
            using var doc = JsonDocument.Parse(
                "{\"id\":\"spawn.test\",\"room_ref\":\"room.map1.2342\",\"map_id\":1,\"room_number\":2342,\"spawn_model\":" + model + "}");
            return RuntimeNpcSpawn.FromJson(doc.RootElement, byId, byGroup);
        }

        var silverfish = Decode("{\"monster_type\":21,\"group_index\":\"21-4-4\"}");
        if (!silverfish.HasQualifiedMonsterIndexRange
            || silverfish.GroupDefinitions.Count != 1
            || silverfish.GroupDefinitions[0].LegacyId != 266)
            failures.Add("qualified-index-must-select-silverfish-not-fungus-lizard");

        var range = Decode("{\"monster_type\":21,\"monster_index_min\":2,\"monster_index_max\":4}");
        if (range.GroupDefinitions.Count != 2 || range.GroupDefinitions.Any(member => member.LegacyId == 264))
            failures.Add("qualified-index-range-must-exclude-outside-monsters");

        // Native WCCMP002 Map 1 Room 2342: Group 21, index 4..7.
        var riverMembers = members.Concat(new[]
        {
            new RuntimeNpcDefinition { Id = "npc.269", LegacyId = 269, GroupId = 21, IndexNumber = 3, Name = "night hag" },
            new RuntimeNpcDefinition { Id = "npc.267", LegacyId = 267, GroupId = 21, IndexNumber = 5, Name = "dragonfish" },
            new RuntimeNpcDefinition { Id = "npc.268", LegacyId = 268, GroupId = 21, IndexNumber = 6, Name = "water elemental" }
        }).ToArray();
        using (var riverDocument = JsonDocument.Parse(
            "{\"id\":\"spawn.river\",\"room_ref\":\"room.map1.2342\",\"map_id\":1,\"room_number\":2342,\"spawn_model\":{\"monster_type\":21,\"group_index\":\"21-4-7\"}}"))
        {
            var originalRiver = RuntimeNpcSpawn.FromJson(riverDocument.RootElement,
                riverMembers.ToDictionary(npc => npc.LegacyId),
                new Dictionary<int, IReadOnlyList<RuntimeNpcDefinition>> { [21] = riverMembers });
            var accepted = originalRiver.GroupDefinitions.Select(npc => npc.LegacyId).OrderBy(id => id).ToArray();
            if (!originalRiver.HasQualifiedMonsterIndexRange ||
                !accepted.SequenceEqual(new[] { 266, 267, 268 }))
                failures.Add("native-river-index-4-through-7-must-include-only-silverfish-dragonfish-water-elemental");
        }

        var zeroLower = Decode("{\"monster_type\":21,\"group_index\":\"21-0-4\"}");
        if (!zeroLower.HasQualifiedMonsterIndexRange || zeroLower.GroupDefinitions.Count != 3)
            failures.Add("native-index-zero-lower-means-no-lower-bound");

        var zeroBoth = Decode("{\"monster_type\":21,\"group_index\":\"21-0-0\"}");
        if (!zeroBoth.HasQualifiedMonsterIndexRange || zeroBoth.GroupDefinitions.Count != 3)
            failures.Add("native-index-zero-zero-remains-qualified-unbounded");

        var unqualified = Decode("{\"monster_type\":21}");
        if (unqualified.HasQualifiedMonsterIndexRange || unqualified.GroupDefinitions.Count != 3)
            failures.Add("missing-evidence-must-not-invent-index-qualification");

        try
        {
            Decode("{\"monster_type\":21,\"group_index\":\"22-1-4\"}");
            failures.Add("wrong-group-qualification-must-fail");
        }
        catch (InvalidOperationException)
        {
            // Expected: do not silently use another group's index range.
        }

        // WCCMMUD native group-table scan: 29 replaces, 99 stops.
        var drawIndex = 0;
        var draws = new[] { 29, 99 };
        var nativeChoice = RuntimeLivingWorldService.SelectNativeIndexedLairCandidate(
            members, () => draws[drawIndex++]);
        if (nativeChoice?.LegacyId != 265 || drawIndex != 2)
            failures.Add("native-group-roll-29-replaces-99-stops");

        var stopCount = 0;
        var stopped = RuntimeLivingWorldService.SelectNativeIndexedLairCandidate(
            members, () => { stopCount++; return 99; });
        if (stopped?.LegacyId != 264 || stopCount != 1)
            failures.Add("native-group-roll-99-stops-without-replacing");

        var changed = RuntimeLivingWorldService.SelectNativeIndexedLairCandidate(members, () => 1);
        if (changed?.LegacyId != 266)
            failures.Add("native-group-roll-repeated-replacement");

        if (!RuntimeLivingWorldService.NativeRoomHasCapacity(9, 10, 14)
            || RuntimeLivingWorldService.NativeRoomHasCapacity(10, 10, 2)
            || RuntimeLivingWorldService.NativeRoomHasCapacity(0, 10, 15)
            || RuntimeLivingWorldService.NativeRoomHasCapacity(0, 0, 0))
            failures.Add("native-room-count-and-15-entry-slot-gates");

        if (!RuntimeLivingWorldService.NativeMonsterWithinGameLimit(1, 0)
            || RuntimeLivingWorldService.NativeMonsterWithinGameLimit(1, 1)
            || !RuntimeLivingWorldService.NativeMonsterWithinGameLimit(0, 200))
            failures.Add("native-individual-monster-global-game-limit-gate");

        return failures;
    }
}
