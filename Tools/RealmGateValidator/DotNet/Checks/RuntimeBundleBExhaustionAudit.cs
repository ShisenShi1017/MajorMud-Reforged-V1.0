using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleBFieldAuditEntry(
    string Family,
    string JsonPath,
    string OriginalStatus,
    string BundleBDisposition,
    int RuntimeConsumerCount,
    IReadOnlyList<string> RuntimeConsumers);

public sealed record RuntimeBundleBExhaustionAuditResult(
    bool Passed,
    string CombatAuditStatus,
    int BundleBOwnedFields,
    int ConnectedFields,
    int StructuralOrProjectionFields,
    int ExplicitlyAbsentFields,
    int RemainingRuntimeDisconnects,
    IReadOnlyList<RuntimeBundleBFieldAuditEntry> Fields,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Final Bundle B trace-and-connect gate. The repository-wide decode audit intentionally
/// scans every generated scalar field, including compiler summaries and provenance. This
/// gate assigns Bundle B ownership, excludes non-runtime projections, and requires every
/// real combat-owned field to have a live consumer or an explicit exhausted-decode absence.
/// </summary>
public static class RuntimeBundleBExhaustionAudit
{
    private static readonly string[] RequiredRuntimeFiles =
    {
        "ClassicCombatResolver.cs", "RuntimeCanonicalCombatSpellCatalog.cs",
        "RuntimeClassicFormulaAuthority.cs", "RuntimeClassicLiveStatusEffects.cs",
        "RuntimeCombatDecodeBundleAudit.cs", "RuntimeCombatProfiles.cs",
        "RuntimeLiveCombat.cs", "RuntimeLiveCombatOwnership.cs", "RuntimeLiveCombatRounds.cs",
        "RuntimeLivePowers.cs", "RuntimeNpcModels.cs", "RuntimeNpcThreat.cs",
        "RuntimePlayerCombatProfiles.cs"
    };

    private static readonly string[] ObsoleteRuntimeFiles =
    {
        "RuntimeClassicStealthResolution.cs", "RuntimeClassicPlayerSpellCombat.cs",
        "RuntimeCombatBundleClosure.cs", "RuntimeNpcAttackSelection.cs",
        "RuntimeNpcDefensiveProfiles.cs", "RuntimeClassicPhysicalFormulaCalculator.cs"
    };

    public static RuntimeBundleBExhaustionAuditResult Run(string repositoryRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        RuntimeCombatDecodeBundleAuditResult? combat = null;
        try
        {
            combat = RuntimeCombatDecodeBundleAudit.Run(repositoryRoot);
            Require(combat.Status == "PASS", "combat-authority-pass", checks, failures);
            Require(combat.RemainingDecodedButNotMapped.Count == 0, "combat-formulas-mapped", checks, failures);
            Require(combat.Conflicts.Count == 0, "combat-formulas-conflict-free", checks, failures);
        }
        catch (Exception ex)
        {
            failures.Add("combat-authority:" + ex.Message);
        }

        var serverRoot = Path.Combine(repositoryRoot, "Apps", "Source", "RealmGate.Server");
        foreach (var file in RequiredRuntimeFiles)
            Require(File.Exists(Path.Combine(serverRoot, file)), "runtime-owner:" + file, checks, failures);
        foreach (var file in ObsoleteRuntimeFiles)
            Require(!File.Exists(Path.Combine(serverRoot, file)), "duplicate-removed:" + file, checks, failures);

        var global = ClassicDecodeExhaustionAudit.Run(repositoryRoot);
        var owned = global.Fields.Where(IsBundleBOwned).ToArray();
        var entries = new List<RuntimeBundleBFieldAuditEntry>();
        foreach (var field in owned)
        {
            var disposition = Classify(field);
            entries.Add(new RuntimeBundleBFieldAuditEntry(
                field.Family, field.JsonPath, field.Status, disposition,
                field.RuntimeConsumerCount, field.RuntimeConsumers));
            if (disposition == "runtime-disconnected")
                failures.Add($"runtime-disconnected:{field.Family}:{field.JsonPath}");
        }

        ValidateExplicitAbsences(repositoryRoot, checks, failures);
        ValidateGeneratedCombatData(repositoryRoot, checks, failures);

        var connected = entries.Count(x => x.BundleBDisposition == "connected");
        var structural = entries.Count(x => x.BundleBDisposition == "structural-or-projection");
        var absent = entries.Count(x => x.BundleBDisposition == "explicitly-absent");
        var disconnected = entries.Count(x => x.BundleBDisposition == "runtime-disconnected");
        Require(disconnected == 0, "bundle-b-runtime-disconnects-zero", checks, failures,
            disconnected == 0 ? null : disconnected.ToString());

        return new RuntimeBundleBExhaustionAuditResult(
            failures.Count == 0,
            combat?.Status ?? "ERROR",
            entries.Count, connected, structural, absent, disconnected,
            entries.OrderBy(x => x.Family).ThenBy(x => x.JsonPath).ToArray(),
            checks, failures);
    }

    private static bool IsBundleBOwned(ClassicDecodeFieldCoverage field)
    {
        var family = field.Family;
        var path = field.JsonPath;
        if (family.Equals("formulas", StringComparison.OrdinalIgnoreCase)) return true;
        if (family.Equals("status_effects", StringComparison.OrdinalIgnoreCase)) return true;
        if (family.Equals("loot_rules", StringComparison.OrdinalIgnoreCase)) return true;
        if (family.Equals("ai_profiles", StringComparison.OrdinalIgnoreCase))
            return path.Contains("ability_counts", StringComparison.OrdinalIgnoreCase);
        if (family.Equals("character_foundation", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, "combat_level", "attack_reputation_penalties", "attack_penalty_rule");
        if (family.Equals("equipment", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, "minimum_strength", "two_handed");
        if (family.Equals("items", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, ".combat.", "combat_profile_authority", "classification.is_armor",
                "backstab", "critical", "weapon", "damage", "accuracy", "speed");
        if (family.Equals("npcs", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, "attack", "combat", "critical", "dodge", "armor", "armour",
                "damage", "energy", "hit_spell", "spell", "resist", "immun", "see_hidden",
                "seehidden", "stealth", "perception", "guard", "aggro", "threat", "flee");
        if (family.Equals("spells", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, "attack", "combat", "damage", "effect", "duration", "target",
                "resist", "element", "status", "poison", "critical", "energy", "recovery", "delay");
        if (family.Equals("commands", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, "attack", "backstab", "smash", "bash", "hide", "sneak", "flee", "assist");
        if (family.Equals("classic_bug_fixes", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, "spell_ref", "npc_ref", "cast_message_id");
        if (family.Equals("spawns", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(path, "qualified_room_ref", "room_ref", "npc_ref", "monster_ref");
        return false;
    }

    private static string Classify(ClassicDecodeFieldCoverage field)
    {
        if (field.Family.Equals("formulas", StringComparison.OrdinalIgnoreCase)
            && field.JsonPath.Equals("$.records[*].resolved_in_code_update", StringComparison.OrdinalIgnoreCase))
            return "structural-or-projection";
        if (field.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase)
            && field.JsonPath.Equals("$.records[*].combat.hp", StringComparison.OrdinalIgnoreCase))
            return "connected";
        if (field.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase)
            && field.JsonPath.Equals("$.records[*].behavior.charm_resistance", StringComparison.OrdinalIgnoreCase))
            return "connected";
        if (field.Family.Equals("npcs", StringComparison.OrdinalIgnoreCase)
            && field.JsonPath.Equals("$.records[*].combat.spell_ref_count", StringComparison.OrdinalIgnoreCase))
            return "structural-or-projection";
        if (field.Family.Equals("spawns", StringComparison.OrdinalIgnoreCase)
            && field.JsonPath.Equals("$.records[*].room_ref_semantics", StringComparison.OrdinalIgnoreCase))
            return "structural-or-projection";
        if (field.Status is "connected" or "connected-empty") return "connected";
        if (field.Status == "structural-metadata" || IsProjection(field.JsonPath))
            return "structural-or-projection";
        if (field.RuntimeConsumerCount > 0)
            return "connected"; // provenance-name mismatch, but live trace exists
        if (IsExplicitlyAbsentProjection(field))
            return "explicitly-absent";
        return "runtime-disconnected";
    }

    private static bool IsProjection(string path) => ContainsAny(path,
        ".evidence", ".generator_mapping", ".librarium_enrichment", ".relationship_summary",
        ".override_policy", ".runtimeMapping", ".runtime_mapping", ".source.",
        ".public_reference", ".mmexplorer_sql_reference", "ability_counts", "conflict_status",
        "expected_observation", "world_id");

    private static bool IsExplicitlyAbsentProjection(ClassicDecodeFieldCoverage field)
    {
        if (!field.Family.Equals("ai_profiles", StringComparison.OrdinalIgnoreCase)) return false;
        return ContainsAny(field.JsonPath, "DamageShield", "SpellImmu", "ReqToHit", "MonsGuards");
    }

    private static void ValidateExplicitAbsences(string repositoryRoot, List<string> checks, List<string> failures)
    {
        var authority = RuntimeClassicFormulaAuthority.Load(repositoryRoot);
        foreach (var id in new[] { "combat.shield_block_roll", "combat.parry_roll", "combat.critical_immunity" })
        {
            var record = authority.Records.SingleOrDefault(x => x.FormulaId.Equals(id, StringComparison.OrdinalIgnoreCase));
            Require(record is not null && !record.Executable && record.Grade == RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                "explicit-absence:" + id, checks, failures);
        }
    }

    private static void ValidateGeneratedCombatData(string repositoryRoot, List<string> checks, List<string> failures)
    {
        var dataRoot = ServerDataRootResolver.ResolveDefault(repositoryRoot).DataRoot;
        foreach (var file in new[] { "abilities.json", "character_foundation.json", "equipment.json", "items.json",
                     "npcs.json", "spells.json", "status_effects.json", "rooms.json", "spawns.json" })
            Require(File.Exists(Path.Combine(dataRoot, file)), "generated-data:" + file, checks, failures);

        var npcs = ReadGeneratedRecords(Path.Combine(dataRoot, "npcs.json")).Select(RuntimeNpcDefinition.FromJson).ToArray();
        Require(npcs.Length > 0 && npcs.All(x => x.HitPoints > 0), "npc-hp-live-consumer", checks, failures);
        Require(npcs.Any(x => x.CharmResistance > 0), "npc-charm-resistance-live-consumer", checks, failures);

        try
        {
            var roomIds = ReadGeneratedRecords(Path.Combine(dataRoot, "rooms.json"))
                .Select(x => JsonValueReader.ReadString(x, "id", string.Empty)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var spawnId in new[] { "spawn.288.43", "spawn.2306.213" })
            {
                var spawn = ReadGeneratedRecords(Path.Combine(dataRoot, "spawns.json")).Single(x =>
                    JsonValueReader.ReadString(x, "id", string.Empty).Equals(spawnId, StringComparison.OrdinalIgnoreCase));
                var roomRef = JsonValueReader.ReadString(spawn, "qualified_room_ref", string.Empty);
                Require(roomIds.Contains(roomRef), "spawn-resolved:" + spawnId, checks, failures, roomRef);
            }
        }
        catch (Exception ex)
        {
            failures.Add("spawn-validation:" + ex.Message);
        }
    }

    private static IReadOnlyList<JsonElement> ReadGeneratedRecords(string path)
    {
        if (!File.Exists(path)) return Array.Empty<JsonElement>();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var output = new List<JsonElement>();
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, relative.Replace('/', Path.DirectorySeparatorChar));
                output.AddRange(ReadGeneratedRecords(partPath));
            }

            return output;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("records", out var records) &&
            records.ValueKind == JsonValueKind.Array)
            return records.EnumerateArray().Select(x => x.Clone()).ToArray();

        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(x => x.Clone()).ToArray();

        return Array.Empty<JsonElement>();
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static void Require(bool condition, string code, List<string> checks, List<string> failures, string? detail = null)
    {
        if (condition) checks.Add(code);
        else failures.Add(detail is null ? code : code + ":" + detail);
    }
}
