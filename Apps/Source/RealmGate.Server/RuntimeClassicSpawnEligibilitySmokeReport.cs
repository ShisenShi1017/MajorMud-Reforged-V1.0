using System.Text.Json;

namespace RealmGate.Server;

/// <summary>
/// Deterministic admission checks for source-qualified Classic lair candidates.
/// This is deliberately separate from the unresolved native group-roll algorithm.
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

        return failures;
    }
}
