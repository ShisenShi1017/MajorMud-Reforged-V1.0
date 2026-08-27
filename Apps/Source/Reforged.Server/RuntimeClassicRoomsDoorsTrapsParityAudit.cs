using System.Text.Json;

namespace Reforged.Server;

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
    private const string EvidenceRelativePath = "Librarium/Parity/majormud_rooms_doors_traps_evidence_0281.json";
    private const string NativeSchemaEvidenceRelativePath = "Librarium/Parity/majormud_room_exit_native_schema_evidence_0287.json";

    public static RuntimeClassicRoomsDoorsTrapsParityAuditResult Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }

        var rootPath = Path.GetFullPath(projectRoot);
        var path = Path.Combine(rootPath, EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var nativeSchemaPath = Path.Combine(rootPath, NativeSchemaEvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return new(false, 0, 0, 0, 0, checks, new[] { "pass11_evidence_file_exists" });
        if (!File.Exists(nativeSchemaPath))
            return new(false, 0, 0, 0, 0, checks, new[] { "pass17_native_schema_evidence_file_exists" });

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var exits = root.GetProperty("semantic_exit_corpus");
        var traps = root.GetProperty("classic_exit_traps");
        var latch = root.GetProperty("room_latch_closure").GetProperty("aggregate_action_gate_closure");

        var totalExits = exits.GetProperty("total_exit_records").GetInt32();
        var classifiedExits = exits.GetProperty("strongly_correlated_active").GetInt32();
        var unknownExits = exits.GetProperty("unknown_preserved_safe_runtime").GetInt32();
        var trapCount = traps.GetProperty("total").GetInt32();

        Check(latch.GetProperty("action_gate_count").GetInt32() == 206, "action_gate_count_206");
        Check(latch.GetProperty("bound_action_gate_count").GetInt32() == 206, "all_action_gates_bound");
        Check(latch.GetProperty("native_passive_placeholder_count").GetInt32() == 0, "zero_native_passive_placeholders");
        Check(totalExits == 61976, "semantic_exit_count_61976");
        Check(classifiedExits == 61975 && unknownExits == 1, "one_semantic_exit_unknown");
        Check(exits.GetProperty("remaining_unknown").GetProperty("exit_id").GetString() == "exit.map12.2248.d", "remaining_unknown_is_map12_2248_down");
        Check(trapCount == 295, "classic_exit_trap_count_295");
        Check(traps.GetProperty("damage_traps").GetInt32() == 273, "classic_damage_trap_count_273");
        Check(traps.GetProperty("spell_traps").GetInt32() == 22, "classic_spell_trap_count_22");

        using var nativeSchemaDocument = JsonDocument.Parse(File.ReadAllText(nativeSchemaPath));
        var nativeSchemaRoot = nativeSchemaDocument.RootElement;
        var nativeStorage = nativeSchemaRoot.GetProperty("native_room_exit_storage");
        var nativeTrapSchema = nativeSchemaRoot.GetProperty("trap_schema");
        Check(nativeStorage.GetProperty("RoomType").GetString() == "signed 16-bit Integer", "room-type-native-int16");
        Check(nativeStorage.GetProperty("Para2").GetString() == "signed 16-bit Integer", "room-para2-native-int16");
        Check(nativeStorage.GetProperty("Para1").GetString() == "signed 32-bit Long", "room-para1-native-int32");
        Check(nativeTrapSchema.GetProperty("current_classic_catalog").GetProperty("native_activation_state_projected").GetInt32() == 0, "trap-para2-not-projected");

        return new(failures.Count == 0, totalExits, classifiedExits, unknownExits, trapCount, checks, failures);
    }
}
