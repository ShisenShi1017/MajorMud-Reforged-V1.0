using RealmGate.Compiler;
using System.Text.Json;
using System.Text.Json.Nodes;

static string FindRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "Librarium")) && Directory.Exists(Path.Combine(dir.FullName, "Data"))) return dir.FullName;
        dir = dir.Parent;
    }
    throw new Exception("Repository root not found.");
}

var root = FindRoot();
var result = new LibrariumDataWriter().Compile(new LibrariumCompilerOptions(root, "CompilerSmokeTest", Clean: true));
if (!result.Success) throw new Exception("Compiler smoke test failed: " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
if (result.ObjectFamiliesCompiled < 10) throw new Exception("Too few object families compiled.");
if (!File.Exists(Path.Combine(result.OutputRoot, "compile_manifest.json"))) throw new Exception("Manifest was not generated.");
if (File.Exists(Path.Combine(result.OutputRoot, "room_messages.json"))) throw new Exception("Evidence-only room message offsets leaked into runtime output.");
if (File.Exists(Path.Combine(result.OutputRoot, "classic_bug_fixes.json"))) throw new Exception("Classic correction contracts leaked into runtime output.");

using (var helpDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "native_help.json"))))
{
    var helpRecords = helpDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (helpRecords.Length != 132) throw new Exception($"Native help projection expected 132 runtime records but found {helpRecords.Length}.");
    if (helpRecords.Count(record => ReadString(record, "record_kind").Equals("main", StringComparison.OrdinalIgnoreCase)) != 1)
        throw new Exception("Native help projection did not contain exactly one main record.");
    if (helpRecords.Count(record => ReadString(record, "record_kind").Equals("topic", StringComparison.OrdinalIgnoreCase)) != 131)
        throw new Exception("Native help projection did not contain 131 complete topic records.");
    if (helpRecords.Any(record => string.IsNullOrWhiteSpace(ReadString(record, "body"))))
        throw new Exception("Native help projection retained an empty body.");
}

using (var commandDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "native_player_commands.json"))))
{
    var commandRecords = commandDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (commandRecords.Length != 63) throw new Exception($"Native player-command projection expected 63 runtime records but found {commandRecords.Length}.");
    var explicitAliases = commandRecords.Sum(record => record.GetProperty("explicit_aliases").GetArrayLength());
    if (explicitAliases != 3) throw new Exception($"Native player-command projection expected 3 explicit aliases but found {explicitAliases}.");
}

foreach (var familyFile in Directory.EnumerateFiles(result.OutputRoot, "*.json"))
{
    var fileName = Path.GetFileName(familyFile);
    if (fileName is "compile_manifest.json" or "world_manifest.json" or "client_projection.json") continue;
    using var document = JsonDocument.Parse(File.ReadAllText(familyFile));
    if (!document.RootElement.TryGetProperty("authority", out var authority) || authority.GetString() != "Librarium")
        throw new Exception($"Generated family '{fileName}' is missing Librarium authority.");
    var serialized=document.RootElement.GetRawText();
    foreach(var metadataKey in new[]{"librarium_enrichment_0124","generator_mapping_0124","validation_rules_0124","override_policy_0124","version_history_0124","validation_rules_0130","validation_0142","validation_tests","tests","assumptions"})
        if(serialized.Contains($"\"{metadataKey}\"",StringComparison.Ordinal))throw new Exception($"Generated family '{fileName}' retained non-runtime metadata '{metadataKey}'.");
}

using (var itemDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "items.json"))))
{
    var items = itemDocument.RootElement.GetProperty("records");
    if (items.GetArrayLength() != 1950) throw new Exception("Runtime item projection did not contain 1950 items.");
    var first = items[0];
    if (!first.TryGetProperty("item_type", out _)) throw new Exception("Runtime item projection is missing item_type.");
    if (!items.EnumerateArray().Any(x => x.TryGetProperty("combat", out var combat) && combat.ValueKind == JsonValueKind.Object))
        throw new Exception("Runtime item projection lost promoted combat profiles.");
    if (items.EnumerateArray().Any(x => x.TryGetProperty("equipment", out _) || x.TryGetProperty("equipment_projection", out _) || x.TryGetProperty("classic_bug_fixes_0135", out _)))
        throw new Exception("Runtime item projection retained duplicate embedded equipment or bug-fix metadata.");
}

using (var equipmentDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "equipment.json"))))
{
    var equipment = equipmentDocument.RootElement.GetProperty("records");
    if (equipment.GetArrayLength() < 900) throw new Exception("Runtime equipment projection is unexpectedly small.");
    var itemRecords = ReadGeneratedRecords(result.OutputRoot, "items.json");
    var weaponIds = itemRecords.Where(item => ReadString(item, "item_type").Equals("weapon", StringComparison.OrdinalIgnoreCase))
        .Select(item => ReadString(item, "id")).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var equipmentById = equipment.EnumerateArray().ToDictionary(record => ReadString(record, "item_id"), StringComparer.OrdinalIgnoreCase);
    if (weaponIds.Count != 383) throw new Exception($"Expected 383 generated weapon definitions but found {weaponIds.Count}.");
    var missingWeapons = weaponIds.Where(id => !equipmentById.ContainsKey(id)).ToArray();
    if (missingWeapons.Length != 0) throw new Exception($"Generated weapons missing equipment relationships: {string.Join(", ", missingWeapons.Take(8))}");
    var nonWieldWeapons = weaponIds.Where(id => !ReadString(equipmentById[id], "slot").Equals("WIELD", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (nonWieldWeapons.Length != 1 || !nonWieldWeapons[0].Equals("item.514", StringComparison.OrdinalIgnoreCase) ||
        !ReadString(equipmentById[nonWieldWeapons[0]], "slot").Equals("FACE", StringComparison.OrdinalIgnoreCase))
        throw new Exception($"Unexpected generated weapon slot overrides: {string.Join(", ", nonWieldWeapons)}");
    var originalSlots = new[]
    {
        "NONE", "WORN_1", "HEAD", "HANDS", "FINGER_1",
        "FEET", "ARMS", "BACK", "NECK", "LEGS",
        "WAIST", "TORSO", "OFF_HAND", "FINGER_2", "WRIST_1",
        "EARS", "WORN_16", "WRIST_2", "EYES", "FACE"
    };
    foreach (var record in equipment.EnumerateArray())
    {
        if (!record.TryGetProperty("item_id", out var itemId) || string.IsNullOrWhiteSpace(itemId.GetString()))
            throw new Exception("Runtime equipment projection contains a record without item_id.");
        if (!record.TryGetProperty("slot", out var slot) || string.IsNullOrWhiteSpace(slot.GetString()))
            throw new Exception("Runtime equipment projection contains a record without slot.");
        if (slot.GetString() == "UNKNOWN")
            throw new Exception("Unrecognized native worn_on code leaked into runtime equipment.");

        // Check EVERY generated item relationship, not just the 20-slot
        // dictionary. An override must not erase source worn_on evidence.
        var wornOn = record.GetProperty("native_worn_on").GetInt32();
        if (wornOn < 0 || wornOn >= originalSlots.Length ||
            ReadString(record, "native_slot") != originalSlots[wornOn])
            throw new Exception($"Classic item {itemId.GetString()} lost native worn_on identity {wornOn}.");
        var runtimeSlot = slot.GetString()!;
        var legacyReady = wornOn == 0 && (runtimeSlot is "WIELD" or "LIGHT");
        var nativeRelation = runtimeSlot == originalSlots[wornOn];
        // Existing Classic bug-fix overrides may intentionally choose a
        // different *native* position, e.g. masks FACE rather than HEAD.
        // Only the two readiness names may be synthetic runtime positions.
        if (!legacyReady && !nativeRelation && !originalSlots.Contains(runtimeSlot, StringComparer.Ordinal))
            throw new Exception($"Classic item {itemId.GetString()} has an unsupported override slot {runtimeSlot} for native worn_on {wornOn}.");
    }
}

// These are the 20 original worn_on codes, including two unresolved
// special WORN states. Synthetic WIELD/LIGHT are transitional relationships,
// never physical slots in the native equipment_slots projection.
using (var slotDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "equipment_slots.json"))))
{
    var nativeSlotNames = new[]
    {
        "NONE", "WORN_1", "HEAD", "HANDS", "FINGER_1",
        "FEET", "ARMS", "BACK", "NECK", "LEGS",
        "WAIST", "TORSO", "OFF_HAND", "FINGER_2", "WRIST_1",
        "EARS", "WORN_16", "WRIST_2", "EYES", "FACE"
    };
    var records = slotDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (records.Length != nativeSlotNames.Length)
        throw new Exception("Classic runtime equipment slots no longer match the 20 native worn_on codes.");
    for (var code = 0; code < nativeSlotNames.Length; code++)
    {
        var matches = records.Where(record => record.GetProperty("code").GetInt32() == code).ToArray();
        if (matches.Length != 1 || !ReadString(matches[0], "id").Equals(nativeSlotNames[code],
                StringComparison.Ordinal))
            throw new Exception($"Classic native worn_on code {code} is misidentified.");
    }
    if (records.Any(record => ReadString(record, "id") is "WIELD" or "LIGHT" or "UNKNOWN"))
        throw new Exception("Synthetic readiness or unknown positions leaked into native body slots.");
}

using (var npcDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "npcs.json"))))
{
    var npcs = npcDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (npcs.Length != 1100) throw new Exception($"Runtime NPC projection expected 1,100 records but found {npcs.Length}.");
    var unknownPrintableSegments = npcs.Where(npc =>
        npc.TryGetProperty("mmexplorer_sql_reference_0139", out var sqlReference) &&
        sqlReference.TryGetProperty("printable_segments", out var segments) &&
        segments.EnumerateArray().Any(segment => segment.ValueKind == JsonValueKind.String &&
            string.Equals(segment.GetString(), "Unknown", StringComparison.OrdinalIgnoreCase))).ToArray();
    if (unknownPrintableSegments.Length != 0)
        throw new Exception($"Runtime NPC projection retained {unknownPrintableSegments.Length} non-value 'Unknown' printable segments.");
    var acidSlime = npcs.Single(x => ReadString(x, "id").Equals("npc.5", StringComparison.OrdinalIgnoreCase));
    var acidSegments = acidSlime.GetProperty("mmexplorer_sql_reference_0139").GetProperty("printable_segments").EnumerateArray()
        .Select(segment => segment.GetString() ?? string.Empty).ToArray();
    if (!acidSegments.Contains("acid slime", StringComparer.OrdinalIgnoreCase) ||
        !acidSegments.Contains("whips you with its pseudopod", StringComparer.OrdinalIgnoreCase))
        throw new Exception("Runtime NPC projection removed meaningful MMExplorer printable segments with the sentinel.");
    var cloakedFigure = npcs.Single(x => x.GetProperty("id").GetString() == "npc.1086");
    if (cloakedFigure.GetProperty("rewards").GetProperty("xp_base").GetString() != "10000")
        throw new Exception("Cloaked figure correction was not projected into authoritative NPC XP.");
    var nahr = npcs.Single(x => x.GetProperty("id").GetString() == "npc.1006");
    var nahrAttacks = nahr.GetProperty("attacks").EnumerateArray().ToArray();
    if (nahrAttacks.Length != 5 || nahrAttacks.Any(attack =>
            attack.GetProperty("miss_message_legacy_id").GetInt32() != 8480 ||
            attack.GetProperty("miss_verb3").GetString() != string.Empty ||
            attack.GetProperty("weapon_name").GetString() != string.Empty ||
            attack.GetProperty("native_equipped_item_legacy_id").GetInt32() != 0 ||
            !attack.GetProperty("native_empty_miss_arguments_verified").GetBoolean()))
        throw new Exception("Nahr native message-8480 empty arguments were not projected exactly.");
}

using (var economyDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "economy_rules.json"))))
{
    var economyRules = economyDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (economyRules.Length != 23) throw new Exception($"Runtime economy-rule projection expected 23 records but found {economyRules.Length}.");
    var relationshipIndexes = economyRules.Where(rule => !ReadString(rule, "id").Equals("economy_rule.bard_shop_minimum_percent", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (relationshipIndexes.Length != 22) throw new Exception($"Expected 22 decoded economy relationship indexes but found {relationshipIndexes.Length}.");
    foreach (var rule in relationshipIndexes)
    {
        if (!ReadString(rule.GetProperty("runtime_mapping"), "status").Equals("runtime_authoritative_relationship_index", StringComparison.OrdinalIgnoreCase) ||
            !ReadString(rule.GetProperty("evidence_summary_0124"), "confidence").Equals("decoded_service_relationship_evidence", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Economy relationship index '{ReadString(rule, "id")}' retained stale pending/unscored metadata.");
        var relationships = rule.GetProperty("relationships");
        if (!relationships.EnumerateObject().Any(relationship => relationship.Value.ValueKind == JsonValueKind.Array && relationship.Value.GetArrayLength() > 0))
            throw new Exception($"Economy relationship index '{ReadString(rule, "id")}' has no generated relationship targets.");
    }
    var bardFloor = economyRules.Single(x => x.GetProperty("id").GetString() == "economy_rule.bard_shop_minimum_percent");
    if (bardFloor.GetProperty("minimum_percent").GetInt32() != 10)
        throw new Exception("Bard shop correction was not projected into the authoritative economy rule.");
}

using (var abilitiesDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "abilities.json"))))
{
    var abilities = abilitiesDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (abilities.Length != 188) throw new Exception($"Runtime ability projection expected 188 records but found {abilities.Length}.");
    var unknownDescriptions = abilities.Where(ability =>
        ability.TryGetProperty("public_reference_0139", out var publicReference) &&
        publicReference.TryGetProperty("fields", out var fields) &&
        fields.TryGetProperty("description", out var description) &&
        string.Equals(description.GetString(), "Unknown", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (unknownDescriptions.Length != 0)
        throw new Exception($"Runtime ability projection retained {unknownDescriptions.Length} non-value 'Unknown' description placeholders.");
    if (!abilities.Any(ability =>
            ability.TryGetProperty("public_reference_0139", out var publicReference) &&
            publicReference.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty("description", out var description) &&
            !string.IsNullOrWhiteSpace(description.GetString())))
        throw new Exception("Runtime ability projection removed meaningful public-reference descriptions.");
}

using (var skillsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "skills.json"))))
{
    var skills = skillsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (skills.Length != 13) throw new Exception($"Runtime learned-skill identity projection expected 13 records but found {skills.Length}.");
    foreach (var skill in skills)
    {
        if (!ReadString(skill, "classification").Equals("runtime_authoritative_learned_skill_identity", StringComparison.OrdinalIgnoreCase) ||
            !ReadString(skill, "confidence").Equals("runtime_identity_contract", StringComparison.OrdinalIgnoreCase) ||
            !ReadString(skill.GetProperty("runtime_mapping"), "status").Equals("runtime_authoritative_identity_registry", StringComparison.OrdinalIgnoreCase) ||
            !ReadString(skill.GetProperty("evidence_summary_0124"), "confidence").Equals("runtime_identity_contract", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Learned-skill identity '{ReadString(skill, "id")}' retained stale candidate/pending metadata.");
        if (skill.GetProperty("relationship_summary_0124").GetArrayLength() != 0)
            throw new Exception($"Learned-skill identity '{ReadString(skill, "id")}' invented an executable acquisition relationship.");
    }
}

using (var characterFoundationDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "character_foundation.json"))))
{
    var foundationRoot = characterFoundationDocument.RootElement;
    var alignmentRules = foundationRoot.GetProperty("character_creation_rules").GetProperty("alignment_evil_point_rules");
    if (!alignmentRules.TryGetProperty("lawful_target_attack_penalty", out var lawfulPenalty) ||
        lawfulPenalty.GetProperty("multiplier").GetInt32() != 3 || lawfulPenalty.EnumerateObject().Count() != 1)
        throw new Exception("Runtime character foundation did not project the exact native x3 Lawful-target multiplier cleanly.");
    var penalties = alignmentRules.GetProperty("attack_reputation_penalties");
    if (penalties.GetProperty("Saint").GetInt32() != 2 ||
        penalties.GetProperty("Good").GetInt32() != 2 ||
        penalties.GetProperty("Neutral").GetInt32() != 0 ||
        penalties.GetProperty("Fiend").GetInt32() != 0)
        throw new Exception("Runtime character foundation did not preserve the authoritative decoded reputation penalty table.");
    var reputationMinimums = alignmentRules.GetProperty("reputation_minimum_evil_points");
    if (reputationMinimums.GetProperty("Saint").GetInt32() != -220 ||
        reputationMinimums.GetProperty("Good").GetInt32() != -200 ||
        reputationMinimums.GetProperty("Neutral").GetInt32() != -50 ||
        reputationMinimums.GetProperty("Seedy").GetInt32() != 30 ||
        reputationMinimums.GetProperty("Outlaw").GetInt32() != 40 ||
        reputationMinimums.GetProperty("Criminal").GetInt32() != 80 ||
        reputationMinimums.GetProperty("Villain").GetInt32() != 120 ||
        reputationMinimums.GetProperty("Fiend").GetInt32() != 210)
        throw new Exception("Runtime character foundation lost the recovered Classic Evil-Point reputation transition points.");
    var cleanDay = alignmentRules.GetProperty("daily_cleanup").GetProperty("no_evil_action_that_day");
    if (cleanDay.GetProperty("maximum_daily_reduction").GetInt32() != 18 ||
        cleanDay.GetProperty("default_epcycle_minutes").GetInt32() != 60 ||
        cleanDay.GetProperty("default_epamount").GetInt32() != 1)
        throw new Exception("Runtime character foundation lost MAXEPDAY/EPCYCLE/EPAMOUNT Classic defaults.");
}

using (var lootRulesDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "loot_rules.json"))))
{
    var lootRules = lootRulesDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (lootRules.Length != 2) throw new Exception($"Runtime loot-rule projection expected evidence catalog plus verified drop rule but found {lootRules.Length} records.");
    var catalogRule = lootRules.Single(rule => ReadString(rule, "id").Equals("loot_rule.catalog.phase2_3_0121", StringComparison.OrdinalIgnoreCase));
    if (!ReadString(catalogRule.GetProperty("runtime_mapping"), "status").Equals("runtime_authoritative_evidence_catalog", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Runtime loot evidence catalog retained its stale export-pending marker.");
    var dropRule = lootRules.Single(rule => ReadString(rule, "id").Equals("loot_rule.classic.kill_drop_percent_roll_0136", StringComparison.OrdinalIgnoreCase));
    var algorithm = dropRule.GetProperty("algorithm");
    if (!ReadString(algorithm, "roll_range").Equals("1..100 inclusive", StringComparison.Ordinal) ||
        !ReadString(algorithm, "success_condition").Equals("roll <= drop_percent", StringComparison.Ordinal))
        throw new Exception("Runtime loot projection lost the verified Classic integer-percent roll contract.");
    if (dropRule.TryGetProperty("affected_scope", out _) || algorithm.TryGetProperty("implementation_contract", out _) ||
        algorithm.TryGetProperty("known_bad_pattern", out _) ||
        dropRule.GetProperty("example_drop_rates").EnumerateArray().Any(example => example.TryGetProperty("monster_class", out _)))
        throw new Exception("Runtime loot projection retained Librarium-only explanatory metadata.");
    static bool Drops(int percent, int roll) => roll <= percent;
    if (!Drops(1, 1) || Drops(1, 2) || !Drops(3, 3) || Drops(3, 4))
        throw new Exception("Classic loot boundary regression failed for exact 1% and 3% drop rates.");
}

using (var statusEffectsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "status_effects.json"))))
{
    var statusEffects = statusEffectsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (statusEffects.Length != 111) throw new Exception($"Runtime status-effect projection expected 111 records but found {statusEffects.Length}.");
    foreach (var statusEffect in statusEffects)
    {
        if (!statusEffect.TryGetProperty("runtime_mapping", out var mapping) ||
            !ReadString(mapping, "status").Equals("runtime_authoritative", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Status effect '{ReadString(statusEffect, "id")}' retained a stale runtime-mapping status.");
        if (!statusEffect.TryGetProperty("evidence_summary_0124", out var evidenceSummary) ||
            !ReadString(evidenceSummary, "confidence").Equals("decoded_relationship_evidence", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Status effect '{ReadString(statusEffect, "id")}' retained stale unscored confidence.");
    }
}

using (var raceDescriptionsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "race_descriptions.json"))))
{
    var raceDescriptions = raceDescriptionsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (raceDescriptions.Length != 13 || raceDescriptions.Any(record =>
            record.TryGetProperty("native_message_number", out _) ||
            record.TryGetProperty("native_sha256", out _) ||
            record.TryGetProperty("native_symbol", out _)))
        throw new Exception("Runtime race descriptions retained Librarium-only native provenance metadata.");
    if (raceDescriptions.Any(record => string.IsNullOrWhiteSpace(ReadString(record, "description"))))
        throw new Exception("Runtime race descriptions lost player-facing native description payloads.");
}

using (var plusDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "majormud_plus.json"))))
{
    var record = plusDocument.RootElement.GetProperty("records")[0];
    if (record.TryGetProperty("default_action_evidence", out _) ||
        record.GetProperty("contest_contract").TryGetProperty("native_evidence", out _) ||
        record.GetProperty("default_actions").EnumerateArray().Any(action =>
            action.TryGetProperty("native_page", out _) || action.TryGetProperty("native_slot", out _)))
        throw new Exception("Runtime MajorMUD Plus projection retained Librarium-only native decode provenance.");
    if (record.GetProperty("default_actions").GetArrayLength() != 64)
        throw new Exception("Runtime MajorMUD Plus projection lost native default actions.");
}

using (var sysopDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "sysop_commands.json"))))
{
    var records = sysopDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (records.Any(record => record.TryGetProperty("native_binary_evidence", out _) ||
            record.TryGetProperty("native_presentation_evidence", out _) ||
            record.TryGetProperty("target_policy_note", out _) ||
            record.TryGetProperty("user_confirmed_semantics", out _)))
        throw new Exception("Runtime SYSOP projection retained Librarium-only evidence notes.");
}

using (var shopsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "shops.json"))))
{
    var shops = shopsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (shops.Length != 178) throw new Exception($"Runtime shop projection expected 178 records but found {shops.Length}.");
    if (shops.Sum(shop => shop.GetProperty("inventory").GetArrayLength()) != 1204)
        throw new Exception("Runtime shop projection did not retain all 1,204 inventory relationships.");
    foreach (var shop in shops)
    {
        if (!shop.TryGetProperty("evidence_refs", out var evidenceRefs) || evidenceRefs.GetArrayLength() == 0)
            throw new Exception($"Shop '{ReadString(shop, "id")}' has no decoded evidence reference.");
        if (!shop.TryGetProperty("evidence_summary_0124", out var evidenceSummary) ||
            !ReadString(evidenceSummary, "confidence").Equals("decoded_service_evidence", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Shop '{ReadString(shop, "id")}' retained stale unscored confidence.");
    }
}

using (var banksDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "banks.json"))))
{
    var banks = banksDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (banks.Length != 26) throw new Exception($"Runtime bank projection expected 26 executable records but found {banks.Length}.");
    var promoted = banks.Where(bank => ReadString(bank.GetProperty("runtime_mapping"), "status").Equals("runtime_authoritative", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (promoted.Length != 26) throw new Exception($"Expected 26 room-bound authoritative banks but found {promoted.Length}.");
    if (promoted.Any(bank => ReadInt(bank, "room_number", 0) <= 0 || !bank.TryGetProperty("evidence", out var evidence) || evidence.GetArrayLength() == 0))
        throw new Exception("An authoritative bank lacks a decoded evidence row or room binding.");
    if (banks.Any(bank => ReadString(bank, "id").Equals("bank.2", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("The explicit blank bank sentinel entered generated runtime data.");
}

using (var commandsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "commands.json"))))
{
    var contextualCommands = commandsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (contextualCommands.Length != 25) throw new Exception($"Runtime contextual command projection expected 25 records but found {contextualCommands.Length}.");
    foreach (var command in contextualCommands)
    {
        if (!ReadString(command, "status").Equals("user_confirmed_authoritative_contextual_room_action", StringComparison.OrdinalIgnoreCase) ||
            !ReadString(command.GetProperty("runtime_mapping"), "status").Equals("runtime_authoritative_contextual_room_action", StringComparison.OrdinalIgnoreCase) ||
            !ReadString(command.GetProperty("evidence_summary_0124"), "confidence").Equals("user_confirmed_classic_room_action", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Contextual room-action verb '{ReadString(command, "name")}' was not promoted from user-confirmed evidence.");
        if (!command.GetProperty("evidence").EnumerateArray().Any(item => ReadString(item, "source").Equals("UserConfirmationCodeUpdate0139", StringComparison.OrdinalIgnoreCase)))
            throw new Exception($"Contextual room-action verb '{ReadString(command, "name")}' is missing the user-confirmation evidence reference.");
    }
}

using (var worldsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "worlds.json"))))
{
    var worlds = worldsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (worlds.Length != 3) throw new Exception($"Runtime world projection expected three world contracts but found {worlds.Length}.");
    foreach (var world in worlds)
    {
        if (!world.TryGetProperty("world_scope", out var scope) || scope.GetArrayLength() == 0 ||
            string.IsNullOrWhiteSpace(ReadString(world, "data_output")))
            throw new Exception($"World contract '{ReadString(world, "id")}' lacks its scope or generated data location.");
        if (!ReadString(world.GetProperty("runtime_mapping"), "status").Equals("runtime_authoritative_world_configuration", StringComparison.OrdinalIgnoreCase) ||
            !ReadString(world.GetProperty("evidence_summary_0124"), "confidence").Equals("canonical_librarium_world_contract", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"World contract '{ReadString(world, "id")}' retained stale pending/unscored runtime metadata.");
    }
}

{
    var rooms = ReadGeneratedRecords(result.OutputRoot, "rooms.json");
    var exits = ReadGeneratedRecords(result.OutputRoot, "exits.json");
    var triggers = ReadGeneratedRecords(result.OutputRoot, "triggers.json");

    if (rooms.Length != 26668) throw new Exception($"MMExplorer room projection expected 26668 rooms but found {rooms.Length}.");
    if (exits.Length != 61976) throw new Exception($"MMExplorer exit projection expected 61976 exits but found {exits.Length}.");
    if (triggers.Length != 83) throw new Exception($"Compact runtime projection expected 83 NPC quest-entry triggers but found {triggers.Length}.");
    if (triggers.Any(trigger => !ReadString(trigger, "trigger_type").Equals("npc_quest_entry", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Compact runtime trigger projection retained a duplicate room/exit analysis trigger.");

    var roomIds = rooms.Select(record => ReadString(record, "id")).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!roomIds.Contains("room.map1.1") || !roomIds.Contains("room.map2.1"))
        throw new Exception("Map-qualified duplicate room number identities were not preserved.");

    var roomNumberOneMaps = rooms
        .Where(record => ReadInt(record, "room_number", ReadInt(record, "roomNumber", 0)) == 1)
        .Select(record => ReadInt(record, "map_number", ReadInt(record, "map_id", 0)))
        .Where(value => value > 0)
        .Distinct()
        .Count();
    if (roomNumberOneMaps < 2) throw new Exception("Room number 1 was collapsed across maps.");

    var gateKinds = exits
        .Select(record => ReadString(record, "gate_kind"))
        .Where(value => value.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var gateKind in new[] { "door", "hidden", "required_item", "toll", "damage_trap", "spell_trap", "text_command", "cast_transition" })
    {
        if (!gateKinds.Contains(gateKind)) throw new Exception($"MMExplorer exit projection lost gate kind '{gateKind}'.");
    }

    var danglingExitIds = new[] { "exit.map1.62.u", "exit.map1.1809.d" };
    foreach (var exitId in danglingExitIds)
    {
        var exit = exits.FirstOrDefault(record => ReadString(record, "id").Equals(exitId, StringComparison.OrdinalIgnoreCase));
        if (exit.ValueKind == JsonValueKind.Undefined) throw new Exception($"Expected dangling exit '{exitId}' was not preserved.");
        if (!ReadString(exit, "runtime_disposition").Equals("invalid_destination_blocked", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Dangling exit '{exitId}' was not marked invalid_destination_blocked.");

        var targetRoomId = $"room.map{ReadInt(exit, "to_map_number", 0)}.{ReadInt(exit, "to_room_number", 0)}";
        if (roomIds.Contains(targetRoomId)) throw new Exception($"Dangling exit '{exitId}' target unexpectedly exists as '{targetRoomId}'.");
    }

    foreach (var recoveredExitId in new[] { "exit.map1.291.w", "exit.map1.2337.e" })
    {
        var exit = exits.FirstOrDefault(record => ReadString(record, "id").Equals(recoveredExitId, StringComparison.OrdinalIgnoreCase));
        if (exit.ValueKind == JsonValueKind.Undefined ||
            !ReadString(exit, "runtime_disposition").Equals("active_runtime", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Recovered imported-WCC room-anchor exit '{recoveredExitId}' is not active.");
    }
}

using (var roomInteractionsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "room_interactions.json"))))
{
    var interactions = roomInteractionsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (interactions.Length < 288) throw new Exception($"Room interaction projection expected strong sidecar records but found only {interactions.Length} records.");
    if (!interactions.Any(record => ReadString(record, "semantic_status").Equals("strongly_correlated_active", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Room interaction projection did not include strongly correlated active sidecar records.");
}


using (var formulasDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "formulas.json"))))
{
    var formulas = formulasDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (formulas.Length != 7) throw new Exception($"Compact formula projection expected 7 runtime records but found {formulas.Length}.");
    if (formulas.Any(record => record.TryGetProperty("runtime_mapping", out var mapping) && ReadString(mapping, "status").Equals("pending", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Compact formula projection retained evidence-only pending probes.");
}

using (var questsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "quests.json"))))
{
    var quests = questsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    var serializedQuests = questsDocument.RootElement.GetRawText();
    foreach (var duplicateReference in new[] { "ability_ref", "target_textblock_ref", "quest_ability_ref", "quest_stage_textblock_ids", "command_keywords", "quest_stage_textblocks", "root_textblock_ref" })
        if (serializedQuests.Contains($"\"{duplicateReference}\"", StringComparison.Ordinal))
            throw new Exception($"Compact quest projection retained duplicate reference property '{duplicateReference}'.");
    if (!serializedQuests.Contains("\"ability_id\"", StringComparison.Ordinal) || !serializedQuests.Contains("\"target_textblock_id\"", StringComparison.Ordinal))
        throw new Exception("Compact quest projection lost authoritative numeric execution references.");
    var decodedFlows = quests.Where(record => ReadString(record, "classification").Equals("npc_textblock_quest_flow", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (quests.Length != 83) throw new Exception($"Compact quest projection expected 83 executable records but found {quests.Length}.");
    if (decodedFlows.Length != 83) throw new Exception($"Quest projection expected 83 decoded flows but found {decodedFlows.Length}.");
    if (quests.Any(record => record.TryGetProperty("runtime_mapping", out var mapping) && ReadString(mapping, "status").Equals("pending", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Compact quest projection retained evidence-only pending records.");
    foreach (var flow in decodedFlows)
    {
        if (!flow.TryGetProperty("runtime_mapping", out var runtimeMapping) ||
            runtimeMapping.ValueKind != JsonValueKind.Object ||
            !ReadString(runtimeMapping, "status").Equals("executable", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Quest flow '{ReadString(flow, "id")}' was not promoted to executable runtime data.");

        if (!flow.TryGetProperty("script_flow", out var scriptFlow) ||
            scriptFlow.ValueKind != JsonValueKind.Object ||
            !ReadString(scriptFlow, "execution_status").Equals("authoritative_script_graph", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Quest flow '{ReadString(flow, "id")}' lost its authoritative decoded script graph.");

        if (scriptFlow.TryGetProperty("stages", out var stages) && stages.ValueKind == JsonValueKind.Array)
        {
            foreach (var stage in stages.EnumerateArray())
            {
                if (!stage.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array) continue;
                foreach (var operation in operations.EnumerateArray())
                {
                    if (!ReadString(operation, "status").Equals("decoded", StringComparison.OrdinalIgnoreCase))
                        throw new Exception($"Quest flow '{ReadString(flow, "id")}' contains a non-decoded operation.");
                }
            }
        }
    }
}

foreach (var (fileName, duplicateProperties) in new[]
{
    ("exits.json", new[] { "toll_semantics", "message_policy" }),
    ("room_interactions.json", new[] { "accepted_commands", "canonical_semantic_step_id", "decoded_chain", "verb_phrase", "duplicate_handling", "reasoning", "explicit_source_map_number", "explicit_source_room_number" }),
    ("scripts.json", new[] { "branch_policy", "condition_combiner", "evaluation_model", "legacy_context_free_id", "ability_ref", "target_textblock_ref" })
})
{
    var serialized = string.Join('\n', ReadGeneratedRecords(result.OutputRoot, fileName).Select(record => record.GetRawText()));
    foreach (var duplicateProperty in duplicateProperties)
        if (serialized.Contains($"\"{duplicateProperty}\"", StringComparison.Ordinal))
            throw new Exception($"Compact {fileName} projection retained duplicate property '{duplicateProperty}'.");
}

using (var scriptDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "scripts.json"))))
{
    var scripts = scriptDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    if (scripts.Length != 183) throw new Exception($"Compact runtime projection expected 182 decoded quest stages plus one NPC hit-spell textblock chain but found {scripts.Length}.");
    if (scripts.Count(script => ReadString(script, "script_type").Equals("mmexplorer_textblock_stage", StringComparison.OrdinalIgnoreCase)) != 182)
        throw new Exception("Compact runtime script projection retained a tooling catalog record.");
    var sphinx = scripts.SingleOrDefault(script => ReadString(script, "id").Equals("script.npc_hit_spell.spell686.textblock2510", StringComparison.OrdinalIgnoreCase));
    if (sphinx.ValueKind == JsonValueKind.Undefined ||
        ReadInt(sphinx, "spell_id", 0) != 686 ||
        ReadInt(sphinx, "root_textblock_id", 0) != 2510 ||
        sphinx.GetProperty("random_table").GetProperty("branches").GetArrayLength() != 4)
        throw new Exception("Compact runtime script projection lost the decoded sphinx spell-686 textblock chain.");
}

foreach (var (fileName, expectedCount) in new[] { ("classes.json", 15), ("races.json", 13) })
{
    var records = ReadGeneratedRecords(result.OutputRoot, fileName);
    if (records.Length != expectedCount || records.Any(record => string.IsNullOrWhiteSpace(ReadString(record, "name"))))
        throw new Exception($"Compact {fileName} projection lost canonical named records.");
    if (records.Any(record => record.TryGetProperty("printableSegments", out _)))
        throw new Exception($"Compact {fileName} projection retained duplicate printableSegments.");
}

using (var raceDescriptionsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "race_descriptions.json"))))
{
    var descriptions = raceDescriptionsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    var expected = new[]
    {
        (1, "Human"), (2, "Dwarf"), (3, "Gnome"), (4, "Halfling"), (5, "Elf"),
        (6, "Half-Elf"), (7, "Dark-Elf"), (8, "Half-Orc"), (9, "Goblin"),
        (10, "Half-Ogre"), (11, "Kang"), (12, "Nekojin"), (13, "Gaunt One")
    };
    if (descriptions.Length != expected.Length)
        throw new Exception($"Native race-description projection expected {expected.Length} records but found {descriptions.Length}.");
    foreach (var (number, name) in expected)
    {
        var record = descriptions.SingleOrDefault(row => ReadInt(row, "race_number", 0) == number);
        if (record.ValueKind == JsonValueKind.Undefined ||
            !ReadString(record, "race_name").Equals(name, StringComparison.Ordinal) ||
            ReadString(record, "description").Length < 1000 ||
            !ReadString(record, "evidence_classification").Equals("proven_native_help_payload", StringComparison.Ordinal))
            throw new Exception($"Native race description {number} is missing or attached to the wrong race.");
    }
}

var commands = ReadGeneratedRecords(result.OutputRoot, "commands.json");
if (commands.Length != 25 || commands.Any(command => command.TryGetProperty("relationships", out var relationships) && relationships.TryGetProperty("rooms_seen_in", out _)))
    throw new Exception("Compact command projection retained duplicate room-sighting relationships or lost commands.");

using (var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "compile_manifest.json"))))
{
    var manifestText = manifestDocument.RootElement.GetRawText();
    if (manifestText.Contains("\"value_kinds\"", StringComparison.Ordinal))
        throw new Exception("Compact compile manifest retained diagnostic value-kind arrays.");
    if (!manifestDocument.RootElement.GetProperty("families").EnumerateArray().All(family => family.TryGetProperty("field_inventory", out _)))
        throw new Exception("Compact compile manifest lost its family field inventories.");
}

foreach (var (fileName, forbiddenProperties) in new[]
{
    ("formulas.json", new[] { "outputs" }),
    ("native_message_payloads.json", new[] { "physical_header_offsets", "player_payload_is_empty", "slot_offsets" }),
    ("room_item_placements.json", new[] { "search_required" }),
    ("rooms.json", new[] { "decoded_slots" }),
    ("sysop_commands.json", new[] { "child_evidence_path", "cleanup_semantics", "evidence_note" })
})
{
    var serialized = string.Join('\n', ReadGeneratedRecords(result.OutputRoot, fileName).Select(record => record.GetRawText()));
    foreach (var property in forbiddenProperties)
        if (serialized.Contains($"\"{property}\"", StringComparison.Ordinal))
            throw new Exception($"Compact {fileName} projection retained duplicate property '{property}'.");
}

{
    var tempRoot = Path.Combine(Path.GetTempPath(), "mmr-world-override-test-" + Guid.NewGuid().ToString("N"));
    var overrideDirectory = Path.Combine(tempRoot, "Librarium", "WorldOverrides", "Custom1");
    Directory.CreateDirectory(overrideDirectory);
    File.WriteAllText(Path.Combine(overrideDirectory, "overrides.json"), """
    {
      "schema": "reforged_world_overrides_v1",
      "world_id": "Custom1",
      "base_world": "Classic",
      "overrides": [
        {
          "family": "rooms",
          "target_id": "room.map1.1",
          "operation": "patch",
          "fields": {
            "name": "Custom Town Gate",
            "environment": { "lighting": "dim" }
          }
        }
      ]
    }
    """);
    var family = new LibrariumFamily("rooms", "rooms.json", new[]
    {
        new JsonObject
        {
            ["id"] = "room.map1.1",
            ["name"] = "Town Gate",
            ["environment"] = new JsonObject { ["lighting"] = "bright", ["indoors"] = false }
        }
    });
    var diagnostics = new List<CompilerDiagnostic>();
    var applied = new WorldOverrideEngine().Apply(tempRoot, "Custom1", new[] { family }, diagnostics);
    var room = applied.Single().Records.Single();
    if (room["name"]?.GetValue<string>() != "Custom Town Gate" ||
        room["environment"]?["lighting"]?.GetValue<string>() != "dim" ||
        room["environment"]?["indoors"]?.GetValue<bool>() != false)
        throw new Exception("Custom world recursive override merge failed.");
    if (!diagnostics.Any(diagnostic => diagnostic.Code == "WORLDOVR100"))
        throw new Exception("Custom world override application did not report its applied override count.");
    Directory.Delete(tempRoot, recursive: true);
}

Console.WriteLine($"PASS: compiled {result.ObjectsCompiled} records across {result.ObjectFamiliesCompiled} families with authoritative runtime item/equipment projections.");

static string ReadString(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
        : string.Empty;
}

static int ReadInt(JsonElement element, string propertyName, int fallback)
{
    if (!element.TryGetProperty(propertyName, out var value)) return fallback;
    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
    return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : fallback;
}

static JsonElement[] ReadGeneratedRecords(string outputRoot, string fileName)
{
    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputRoot, fileName)));
    if (document.RootElement.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
        return records.EnumerateArray().Select(record => record.Clone()).ToArray();

    if (!document.RootElement.TryGetProperty("record_parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
        throw new Exception($"Generated projection '{fileName}' contains neither records nor record_parts.");

    var result = new List<JsonElement>();
    foreach (var part in parts.EnumerateArray())
    {
        var relativePath = part.GetString();
        if (string.IsNullOrWhiteSpace(relativePath)) throw new Exception($"Generated projection '{fileName}' has an empty part path.");
        using var partDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))));
        var partRecords = partDocument.RootElement.GetProperty("records");
        result.AddRange(partRecords.EnumerateArray().Select(record => record.Clone()));
    }
    return result.ToArray();
}
