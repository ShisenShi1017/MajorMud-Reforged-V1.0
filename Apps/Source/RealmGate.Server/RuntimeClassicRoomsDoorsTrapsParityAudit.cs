namespace RealmGate.Server;

public sealed record RuntimeClassicRoomsDoorsTrapsParityAuditResult(
    bool Passed,
    int SemanticExitCount,
    int ClassifiedSemanticExitCount,
    int UnknownSemanticExitCount,
    int ClassicExitTrapCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures);

public static class RuntimeClassicRoomsDoorsTrapsParityAudit
{
    public static RuntimeClassicRoomsDoorsTrapsParityAuditResult Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }

        var dataRoot = Path.Combine(Path.GetFullPath(projectRoot), "Data", "Classic");
        var exits = RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(dataRoot, "exits.json"));
        var nativeExits = RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(dataRoot, "native_room_exits.json"));
        var interactions = RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(dataRoot, "room_interactions.json"));

        var activeExitCount = exits.Count(row =>
            !JsonValueReader.ReadString(row, "semantic_status", string.Empty)
                .Equals("blocked_unresolved_destination", StringComparison.OrdinalIgnoreCase));
        var blockedExitCount = exits.Count - activeExitCount;
        var preservedUnknown = exits.Where(row =>
            JsonValueReader.ReadString(row, "semantic_status", string.Empty)
                .Equals("unknown_preserved_safe_runtime", StringComparison.OrdinalIgnoreCase)).ToArray();
        var traps = nativeExits.Where(row => JsonValueReader.ReadInt(row, "room_type", -1) is 9 or 24).ToArray();
        var damageTraps = traps.Count(row => JsonValueReader.ReadInt(row, "room_type", -1) == 9);
        var spellTraps = traps.Count(row => JsonValueReader.ReadInt(row, "room_type", -1) == 24);
        var boundActionRows = interactions.Count(row =>
            JsonValueReader.ReadString(row, "runtime_disposition", string.Empty)
                .Equals("active_bound_action_latch_step", StringComparison.OrdinalIgnoreCase));

        Check(exits.Count == 61976, "semantic-exit-count-61976");
        Check(activeExitCount == 61974 && blockedExitCount == 2, "unresolved-destinations-fail-closed-2");
        Check(preservedUnknown.Length == 1
              && JsonValueReader.ReadString(preservedUnknown[0], "id", string.Empty) == RuntimeClassicRoomsWorldNativeAuthority.RugExitId,
            "preserved-unknown-is-map12-2248-down");
        Check(boundActionRows == 264, "bound-action-latch-rows-264");
        Check(traps.Length == RuntimeClassicRoomsWorldNativeAuthority.ExpectedTrapCount, "classic-exit-trap-count-295");
        Check(damageTraps == 273, "classic-damage-trap-count-273");
        Check(spellTraps == 22, "classic-spell-trap-count-22");

        var nativeAuthority = RuntimeClassicRoomsWorldNativeAuthority.Load(projectRoot);
        Check(nativeAuthority.Count == RuntimeClassicRoomsWorldNativeAuthority.ExpectedRecordCount, "native-roomrec-count-3490");
        var rug = nativeAuthority.Require(RuntimeClassicRoomsWorldNativeAuthority.RugExitId);
        Check(rug.RoomType == 6 && rug.Para1 == 16 && rug.Para2 == 0 && rug.Para3 == 2687 && rug.Para4 == 2688,
            "native-rug-roomrec-exact");

        return new(
            failures.Count == 0,
            exits.Count,
            activeExitCount,
            blockedExitCount + preservedUnknown.Length,
            traps.Length,
            checks,
            failures);
    }
}
