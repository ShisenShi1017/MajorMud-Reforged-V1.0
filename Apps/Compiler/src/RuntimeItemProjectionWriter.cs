using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

internal static class RuntimeItemProjectionWriter
{
    // WCCMMUD native worn position codes. Repeated Finger/Wrist positions are
    // separate native locations, not fabricated Reforged equipment slots.
    // Native codes 1 and 16 are special WORN states; preserve their code
    // identity pending runtime treatment instead of guessing a body part.
    private static readonly IReadOnlyDictionary<int, string> Slots = new Dictionary<int, string>
    {
        [0] = "NONE",
        [1] = "WORN_1",
        [2] = "HEAD",
        [3] = "HANDS",
        [4] = "FINGER_1",
        [5] = "FEET",
        [6] = "ARMS",
        [7] = "BACK",
        [8] = "NECK",
        [9] = "LEGS",
        [10] = "WAIST",
        [11] = "TORSO",
        [12] = "OFF_HAND",
        [13] = "FINGER_2",
        [14] = "WRIST_1",
        [15] = "EARS",
        [16] = "WORN_16",
        [17] = "WRIST_2",
        [18] = "EYES",
        [19] = "FACE"
    };

    public static int Write(
        LibrariumCompilerOptions options,
        IReadOnlyList<LibrariumFamily> families,
        Action<string, JsonNode> writeJson)
    {
        var itemFamily = families.FirstOrDefault(x => x.Name.Equals("items", StringComparison.OrdinalIgnoreCase));
        if (itemFamily is null || itemFamily.Records.Count == 0) return 0;

        var bugFixFamily = families.FirstOrDefault(x => x.Name.Equals("classic_bug_fixes", StringComparison.OrdinalIgnoreCase));
        var slotOverrides = ReadSlotOverrides(bugFixFamily);
        var useEffects = ReadUseEffects(bugFixFamily);

        var items = new JsonArray();
        var equipment = new JsonArray();

        foreach (var sourceRecord in itemFamily.Records)
        {
            var id = ReadString(sourceRecord, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var projected = (JsonObject)sourceRecord.DeepClone();
            var classification = sourceRecord["classification"] as JsonObject;
            var equipmentData = sourceRecord["equipment"] as JsonObject;
            var flags = sourceRecord["flags"] as JsonObject;

            var itemType = ReadString(classification, "type") ?? "misc";
            // The copied Librarium equipment.worn_on was decoded through an
            // incorrect positional table. The retained original MMExplorer
            // property is authoritative when present (including a zero value).
            var nativeProperties = sourceRecord["public_reference_0139"]?["fields"]?["properties"] as JsonObject;
            var wornOn = nativeProperties?["worn_on"] is not null
                ? ReadInt(nativeProperties, "worn_on")
                : ReadInt(equipmentData, "worn_on");
            var weaponType = ReadWeaponType(sourceRecord);
            // Never emit an unrecognized native worn position as an
            // apparently usable runtime slot. Such a value needs fresh
            // DLL/database evidence before assigning equipment semantics.
            if (!Slots.TryGetValue(wornOn, out var slot))
                throw new InvalidDataException(
                    $"Item '{id}' has unknown native worn_on code {wornOn}.");
            // Preserve the unmodified source position separately from the
            // Reforged compatibility/bug-fix relationship used at runtime.
            var sourceWornOn = wornOn;
            var sourceNativeSlot = slot;
            // Transitional compatibility: ready weapons and light sources are
            // NOT native body slots. Keep their existing runtime relationships
            // until readiness and active-light state are migrated atomically.
            // They are deliberately absent from equipment_slots.json.
            var legacyReadyState = wornOn == 0 && itemType.Equals("weapon", StringComparison.OrdinalIgnoreCase);
            var legacyLightState = wornOn == 0 && itemType.Equals("light_source", StringComparison.OrdinalIgnoreCase);
            if (legacyReadyState) slot = "WIELD";
            if (legacyLightState) slot = "LIGHT";
            if (slotOverrides.TryGetValue(id, out var overrideSlot))
            {
                slot = overrideSlot;
                wornOn = Slots.FirstOrDefault(x => x.Value.Equals(slot, StringComparison.OrdinalIgnoreCase)).Key;
            }

            projected["item_type"] = itemType;
            projected["gettable"] = ReadBool(flags, "gettable");
            projected["container"] = itemType.Equals("container", StringComparison.OrdinalIgnoreCase);
            projected["consumable"] = itemType is "consumable" or "food" or "drink";
            projected["stackable"] = false;
            projected["default_charges"] = ReadInt(sourceRecord, "charges");
            projected["authority"] = "Librarium";
            projected["runtime_projection"] = "item_definition_v1";
            if (projected["economy"] is JsonObject economy)
                economy["cost_type"] = ReadPublicCostType(sourceRecord);

            if (slotOverrides.ContainsKey(id))
            {
                projected["equipment_projection"] = new JsonObject
                {
                    ["slot_name"] = slot.ToLowerInvariant(),
                    ["slot"] = slot,
                    ["evidence"] = "classic_bug_fixes"
                };
            }

            if (useEffects.TryGetValue(id, out var spellRef))
            {
                projected["use_effect"] = new JsonObject { ["spell_ref"] = spellRef };
            }

            projected.Remove("equipment");
            projected.Remove("equipment_projection");
            projected.Remove("classic_bug_fixes_0135");
            LibrariumDataWriter.StripNonRuntimeMetadata(projected, "items");

            items.Add(projected);

            if (wornOn == 0 && !legacyReadyState && !legacyLightState) continue;
            equipment.Add(new JsonObject
            {
                ["item_id"] = id,
                ["slot"] = slot,
                ["native_worn_on"] = sourceWornOn,
                ["native_slot"] = sourceNativeSlot,
                ["two_handed"] = ReadBool(equipmentData, "two_handed") || itemType.Equals("weapon", StringComparison.OrdinalIgnoreCase) && weaponType is 1 or 3,
                ["class_restricted"] = ReadBool(equipmentData, "class_restricted"),
                ["race_restricted"] = ReadBool(equipmentData, "race_restricted"),
                ["minimum_level"] = Math.Max(ReadInt(equipmentData, "level_requirement"), ReadAbilityValue(sourceRecord, 135)),
                ["maximum_level"] = ReadAbilityValue(sourceRecord, 136),
                ["minimum_strength"] = ReadInt(equipmentData, "str_requirement"),
                ["alignment"] = JsonValue.Create(ReadString(equipmentData, "alignment")),
                ["gender"] = JsonValue.Create(ReadString(equipmentData, "gender")),
                ["authority"] = "Librarium"
            });
        }

        var slots = new JsonArray(Slots.OrderBy(x => x.Key).Select(x => (JsonNode)new JsonObject
        {
            ["code"] = x.Key,
            ["id"] = x.Value
        }).ToArray());

        var rules = new JsonArray
        {
            new JsonObject { ["id"] = "two-handed-shield-conflict", ["rule"] = "TWO_HANDED_EXCLUDES_OFF_HAND_AND_SHIELD" },
            new JsonObject { ["id"] = "single-instance-single-location", ["rule"] = "ITEM_INSTANCE_HAS_EXACTLY_ONE_LOCATION" },
            new JsonObject { ["id"] = "mask-face-slot", ["rule"] = "MASK_EQUIPS_FACE" }
        };

        writeJson(Path.Combine(options.OutputRoot, "items.json"), Projection("items", itemFamily.SourceFile, items));
        writeJson(Path.Combine(options.OutputRoot, "equipment.json"), Projection("equipment", itemFamily.SourceFile, equipment));
        writeJson(Path.Combine(options.OutputRoot, "equipment_slots.json"), Projection("equipment_slots", itemFamily.SourceFile, slots));
        writeJson(Path.Combine(options.OutputRoot, "equipment_rules.json"), Projection("equipment_rules", itemFamily.SourceFile, rules));
        return 2; // items/equipment replace family files; slots/rules are additional files.
    }

    private static JsonObject Projection(string family, string sourceFile, JsonArray records)
        => new()
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "RealmGate.Compiler",
            ["compiler_code_update"] = "0138",
            ["family"] = family,
            ["source_file"] = sourceFile.Replace('\\', '/'),
            ["record_count"] = records.Count,
            ["records"] = records
        };

    private static Dictionary<string, string> ReadSlotOverrides(LibrariumFamily? family)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (family is null) return result;
        foreach (var record in family.Records)
        {
            if (!string.Equals(ReadString(record, "id"), "classic_fix.mask_face_slot", StringComparison.OrdinalIgnoreCase)) continue;
            var slot = (ReadString(record, "slot") ?? "FACE").ToUpperInvariant();
            if (record["item_refs"] is not JsonArray refs) continue;
            foreach (var itemRef in refs)
            {
                var id = itemRef?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(id)) result[id] = slot;
            }
        }
        return result;
    }

    private static int ReadWeaponType(JsonObject sourceRecord)
    {
        if (sourceRecord["public_reference_0139"] is not JsonObject reference ||
            reference["fields"] is not JsonObject fields ||
            fields["properties"] is not JsonObject properties) return 0;
        return ReadInt(properties, "weapon_type");
    }

    private static int ReadPublicCostType(JsonObject sourceRecord)
    {
        if (sourceRecord["public_reference_0139"] is not JsonObject reference ||
            reference["fields"] is not JsonObject fields) return 0;
        return ReadInt(fields, "cost_type");
    }

    private static int ReadAbilityValue(JsonObject sourceRecord, int abilityId)
    {
        if (sourceRecord["public_reference_0139"] is not JsonObject reference ||
            reference["fields"] is not JsonObject fields ||
            fields["abilities_named"] is not JsonArray abilities) return 0;
        foreach (var node in abilities)
        {
            if (node is not JsonObject ability || ReadInt(ability, "ability_id") != abilityId) continue;
            return ReadInt(ability, "value");
        }
        return 0;
    }

    private static Dictionary<string, string> ReadUseEffects(LibrariumFamily? family)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (family is null) return result;
        foreach (var record in family.Records)
        {
            if (!string.Equals(ReadString(record, "id"), "classic_fix.lyrists_companion", StringComparison.OrdinalIgnoreCase)) continue;
            var itemRef = ReadString(record, "item_ref");
            var spellRef = ReadString(record, "spell_ref");
            if (!string.IsNullOrWhiteSpace(itemRef) && !string.IsNullOrWhiteSpace(spellRef)) result[itemRef] = spellRef;
        }
        return result;
    }

    private static string? ReadString(JsonObject? obj, string name)
    {
        if (obj?[name] is null) return null;
        try { return obj[name]!.GetValue<string>(); }
        catch { return obj[name]!.ToJsonString().Trim('"'); }
    }

    private static int ReadInt(JsonObject? obj, string name)
    {
        if (obj?[name] is null) return 0;
        try { return obj[name]!.GetValue<int>(); }
        catch { return int.TryParse(ReadString(obj, name), out var value) ? value : 0; }
    }

    private static bool ReadBool(JsonObject? obj, string name)
    {
        if (obj?[name] is null) return false;
        try { return obj[name]!.GetValue<bool>(); }
        catch { return bool.TryParse(ReadString(obj, name), out var value) && value; }
    }
}
