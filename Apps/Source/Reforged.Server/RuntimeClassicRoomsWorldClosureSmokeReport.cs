using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicRoomsWorldClosureSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicRoomsWorldClosureSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, bool value)
        {
            checks.Add(name + ":" + value);
            if (!value) failures.Add(name);
        }

        var authority = RuntimeClassicRoomsWorldNativeAuthority.Load(projectRoot);
        Check("native-exit-count-3490", authority.Count == RuntimeClassicRoomsWorldNativeAuthority.ExpectedRecordCount);
        Check("native-trap-count-295", authority.Records.Count(record => record.RoomType is 9 or 24) == RuntimeClassicRoomsWorldNativeAuthority.ExpectedTrapCount);
        Check("native-damage-traps-273", authority.Records.Count(record => record.RoomType == 9) == 273);
        Check("native-spell-traps-22", authority.Records.Count(record => record.RoomType == 24) == 22);
        Check("native-type2-key-exits-73", authority.Records.Count(record => record.RoomType == 2) == 73);
        Check("type2-key-field-positive", authority.Records.Where(record => record.RoomType == 2).All(record => record.Para1 > 0));
        Check("type2-stock-state-2", authority.Records.Where(record => record.RoomType == 2).All(record => record.Para2 == 2));
        Check("timer-quantum-300", RuntimeClassicRoomsWorldNativeAuthority.TimerSeconds(1) == 300);
        Check("timer-multiplier-900", RuntimeClassicRoomsWorldNativeAuthority.TimerSeconds(3) == 900);

        var rug = authority.Require(RuntimeClassicRoomsWorldNativeAuthority.RugExitId);
        Check("rug-roomtype-6", rug.RoomType == 6);
        Check("rug-native-fields-exact", rug.Para1 == 16 && rug.Para2 == 0 && rug.Para3 == 2687 && rug.Para4 == 2688);
        Check("rug-state8-traversable", RuntimeClassicRoomsWorldNativeAuthority.HiddenStateAllowsTraversal(8));
        Check("rug-state8-no-reset", RuntimeClassicRoomsWorldNativeAuthority.ResetHiddenState(8, rug.Para2) == 8);
        Check("searched-hidden-reset-4-to-2", RuntimeClassicRoomsWorldNativeAuthority.ResetHiddenState(4, 0) == 2);

        var type2 = authority.Records.First(record => record.RoomType == 2);
        Check("type2-reset-to-locked", RuntimeClassicRoomsWorldNativeAuthority.ResetDoorState(type2, 0) == 2);
        var doorNegative = authority.Records.First(record => record.RoomType == 7 && record.Para2 <= 0);
        Check("door-nonpositive-reset-to-locked", RuntimeClassicRoomsWorldNativeAuthority.ResetDoorState(doorNegative, 0) == 2);
        var doorPositive = authority.Records.First(record => record.RoomType == 7 && record.Para2 > 0);
        Check("door-positive-reset-preserves-state", RuntimeClassicRoomsWorldNativeAuthority.ResetDoorState(doorPositive, 0) == 0);

        Check("trap-evidence-zero-failures", RuntimeClassicTrapEvidenceRegistry.Validate().Count == 0);
        Check("trap-search-exact", RuntimeClassicTrapEvidenceRegistry.ExactSearchProbabilityResolved);
        Check("trap-disarm-exact", RuntimeClassicTrapEvidenceRegistry.ExactDisarmProbabilityResolved);
        Check("trap-presentation-exact", RuntimeClassicTrapEvidenceRegistry.ExactTrapMessageOwnershipResolved);
        Check("trap-reset-exact", RuntimeClassicTrapEvidenceRegistry.ExactTrapResetRearmResolved);

        var evidencePath = Path.Combine(projectRoot, "Librarium", "Parity", "majormud_rooms_world_complete_evidence_0367.json");
        Check("closure-evidence-exists", File.Exists(evidencePath));
        if (File.Exists(evidencePath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
            var audit = document.RootElement.GetProperty("description_audit");
            Check("room-description-count-26668", audit.GetProperty("canonical_room_count").GetInt32() == 26668);
            Check("room-description-inline-2", audit.GetProperty("inline_nonempty_count").GetInt32() == 2);
            Check("room-description-resolved-26666", audit.GetProperty("librarium_resolved_count").GetInt32() == 26666);
            Check("room-description-unresolved-zero", audit.GetProperty("unresolved_count").GetInt32() == 0);
            Check("room-description-empty-zero", audit.GetProperty("empty_count").GetInt32() == 0);
            Check("room-description-duplicate-zero", audit.GetProperty("duplicate_canonical_room_id_count").GetInt32() == 0);
        }

        return new RuntimeClassicRoomsWorldClosureSmokeReport(failures.Count == 0, checks, failures);
    }
}
