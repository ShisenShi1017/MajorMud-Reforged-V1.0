using System.Text.Json;
using System.Text.Json.Nodes;

namespace MajorMud.Reforged.Compiler;

public sealed class LibrariumDataWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> NonRuntimeClassicFamilies = new(
        [
            "ai_profiles",
            "areas",
            "assets",
            "client_views",
            "conflicts",
            "events",
            "evidence_records",
            "guilds",
            "regions",
            "validation_rules",
            "world_overlays"
        ],
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NonRuntimeRecordProperties = new(
        [
            "librarium_enrichment_0124",
            "generator_mapping_0124",
            "validation_rules_0124",
            "override_policy_0124",
            "version_history_0124",
            "validation_rules_0130",
            "validation_0142",
            "validation_tests",
            "tests",
            "assumptions",
            "classic_bug_fixes_0135"
        ],
        StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, HashSet<string>> NonRuntimeFamilyRecordProperties =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["quests"] = new(
            [
                "ability_ref",
                "target_textblock_ref",
                "quest_ability_ref",
                "quest_stage_textblock_ids",
                "command_keywords",
                "quest_stage_textblocks",
                "root_textblock_ref"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["exits"] = new(
            [
                "toll_semantics",
                "message_policy",
                "native_exit_index"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["room_interactions"] = new(
            [
                "accepted_commands",
                "canonical_semantic_step_id",
                "decoded_chain",
                "verb_phrase",
                "duplicate_handling",
                "reasoning",
                "explicit_source_map_number",
                "explicit_source_room_number"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["scripts"] = new(
            [
                "branch_policy",
                "condition_combiner",
                "evaluation_model",
                "legacy_context_free_id",
                "ability_ref",
                "target_textblock_ref"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["npcs"] = new(
            [
                "spell_ref_count",
                "native_equipped_item_legacy_id",
                "native_message_8480_argument_authority"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["character_foundation"] = new(
            [
                "maximum_daily_reduction_config",
                "maximum_daily_reduction_default_evidence",
                "reputation_range_rule"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["classes"] = new(
            [
                "printableSegments"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["races"] = new(
            [
                "printableSegments"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["race_descriptions"] = new(
            [
                "native_message_number",
                "native_sha256",
                "native_symbol"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["majormud_plus"] = new(
            [
                "native_evidence",
                "default_action_evidence",
                "native_page",
                "native_slot"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["commands"] = new(
            [
                "rooms_seen_in"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["formulas"] = new(
            [
                "outputs"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["native_message_payloads"] = new(
            [
                "physical_header_offsets",
                "player_payload_is_empty",
                "slot_offsets"
            ],
                StringComparer.OrdinalIgnoreCase),
            ["textblocks"] = new(
            [
                "unknown_opcodes"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["rooms"] = new(
            [
                "decoded_slots"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["room_item_placements"] = new(
            [
                "search_required"
            ],
            StringComparer.OrdinalIgnoreCase),
            ["sysop_commands"] = new(
            [
                "child_evidence_path",
                "cleanup_semantics",
                "evidence_note",
                "native_binary_evidence",
                "native_presentation_evidence",
                "target_policy_note",
                "user_confirmed_semantics"
            ],
            StringComparer.OrdinalIgnoreCase)
        };

    private static void NormalizeRuntimeMetadata(JsonNode? node, string familyName)
    {
        if (node is not JsonObject record) return;

        if (familyName.Equals("status_effects", StringComparison.OrdinalIgnoreCase))
        {
            if (record["runtime_mapping"] is JsonObject runtimeMapping)
                runtimeMapping["status"] = "runtime_authoritative";
            if (record["evidence_summary_0124"] is JsonObject evidenceSummary)
                evidenceSummary["confidence"] = "decoded_relationship_evidence";
        }

        if (familyName.Equals("shops", StringComparison.OrdinalIgnoreCase) &&
            record["evidence_summary_0124"] is JsonObject shopEvidenceSummary)
            shopEvidenceSummary["confidence"] = "decoded_service_evidence";

        if (familyName.Equals("banks", StringComparison.OrdinalIgnoreCase) &&
            ReadInt(record, "room_number") > 0 &&
            record["evidence"] is JsonArray bankEvidence && bankEvidence.Count > 0)
        {
            if (record["runtime_mapping"] is JsonObject bankRuntimeMapping)
                bankRuntimeMapping["status"] = "runtime_authoritative";
            if (record["evidence_summary_0124"] is JsonObject bankEvidenceSummary)
                bankEvidenceSummary["confidence"] = "decoded_service_evidence";
        }

        if (familyName.Equals("worlds", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(record["data_output"]?.GetValue<string>()) &&
            record["world_scope"] is JsonArray worldScope && worldScope.Count > 0)
        {
            if (record["runtime_mapping"] is JsonObject worldRuntimeMapping)
                worldRuntimeMapping["status"] = "runtime_authoritative_world_configuration";
            if (record["evidence_summary_0124"] is JsonObject worldEvidenceSummary)
                worldEvidenceSummary["confidence"] = "canonical_librarium_world_contract";
        }

        if (familyName.Equals("abilities", StringComparison.OrdinalIgnoreCase) &&
            record["public_reference_0139"] is JsonObject abilityPublicReference &&
            abilityPublicReference["fields"] is JsonObject abilityFields &&
            string.Equals(abilityFields["description"]?.GetValue<string>(), "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            // "Unknown" means the public reference supplied no description. It is
            // evidence metadata, not an executable Classic value. Preserve it in
            // Librarium while omitting the placeholder from generated runtime data.
            abilityFields.Remove("description");
        }

        if (familyName.Equals("skills", StringComparison.OrdinalIgnoreCase) &&
            record["relationship_summary_0124"] is JsonArray skillRelationships &&
            skillRelationships.Count == 0)
        {
            // These records are the live, closed set of learned-skill identities.
            // Empty relationships deliberately keep acquisition/training fail-closed;
            // identity authority must not be mislabeled as a pending generator map.
            record["classification"] = "runtime_authoritative_learned_skill_identity";
            record["confidence"] = "runtime_identity_contract";
            if (record["runtime_mapping"] is JsonObject skillRuntimeMapping)
                skillRuntimeMapping["status"] = "runtime_authoritative_identity_registry";
            if (record["evidence_summary_0124"] is JsonObject skillEvidenceSummary)
                skillEvidenceSummary["confidence"] = "runtime_identity_contract";
        }

        if ((familyName.Equals("classes", StringComparison.OrdinalIgnoreCase) ||
             familyName.Equals("races", StringComparison.OrdinalIgnoreCase)) &&
            record["runtime_mapping"] is JsonObject progressionRuntimeMapping &&
            string.Equals(progressionRuntimeMapping["status"]?.GetValue<string>(), "pending", StringComparison.OrdinalIgnoreCase))
        {
            // The selected class/race records are consumed by character creation and
            // compatibility. The old label described an earlier cataloging phase and
            // must not survive into generated runtime data as a pending marker.
            progressionRuntimeMapping["status"] = "runtime_authoritative_identity_catalog";
        }

        if (familyName.Equals("character_foundation", StringComparison.OrdinalIgnoreCase) &&
            record["character_creation_rules"] is JsonObject characterCreationRules &&
            characterCreationRules["alignment_evil_point_rules"] is JsonObject alignmentRules &&
            alignmentRules["lawful_target_attack_penalty"] is JsonObject lawfulPenalty)
        {
            // Pass 28: retained native HL1B11/GOODTEXT closes the exact x3 modifier.
            // Project only the executable scalar into generated runtime data; provenance stays in Librarium.
            alignmentRules["lawful_target_attack_penalty"] = new JsonObject
            {
                ["multiplier"] = lawfulPenalty["multiplier"]?.DeepClone()
            };
        }

        if (familyName.Equals("loot_rules", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record["id"]?.GetValue<string>(), "loot_rule.catalog.phase2_3_0121", StringComparison.OrdinalIgnoreCase) &&
            record["runtime_mapping"] is JsonObject lootRuntimeMapping)
        {
            lootRuntimeMapping["status"] = "runtime_authoritative_evidence_catalog";
        }

        if (familyName.Equals("loot_rules", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record["id"]?.GetValue<string>(), "loot_rule.classic.kill_drop_percent_roll_0136", StringComparison.OrdinalIgnoreCase))
        {
            record.Remove("affected_scope");
            if (record["algorithm"] is JsonObject lootAlgorithm)
            {
                lootAlgorithm.Remove("implementation_contract");
                lootAlgorithm.Remove("known_bad_pattern");
            }
            if (record["example_drop_rates"] is JsonArray examples)
                foreach (var example in examples.OfType<JsonObject>()) example.Remove("monster_class");
        }

        if (familyName.Equals("economy_rules", StringComparison.OrdinalIgnoreCase) &&
            record["evidence"] is JsonArray economyEvidence && economyEvidence.Count > 0 &&
            record["relationships"] is JsonObject economyRelationships &&
            economyRelationships.Any(relationship => relationship.Value is JsonArray values && values.Count > 0) &&
            record["runtime_mapping"] is JsonObject economyRuntimeMapping)
        {
            economyRuntimeMapping["status"] = "runtime_authoritative_relationship_index";
            if (record["evidence_summary_0124"] is JsonObject economyEvidenceSummary)
                economyEvidenceSummary["confidence"] = "decoded_service_relationship_evidence";
        }

        if (familyName.Equals("npcs", StringComparison.OrdinalIgnoreCase) &&
            record["mmexplorer_sql_reference_0139"] is JsonObject npcSqlReference &&
            npcSqlReference["printable_segments"] is JsonArray printableSegments)
        {
            for (var index = printableSegments.Count - 1; index >= 0; index--)
                if (string.Equals(printableSegments[index]?.GetValue<string>(), "Unknown", StringComparison.OrdinalIgnoreCase))
                    printableSegments.RemoveAt(index);
        }

        if (familyName.Equals("commands", StringComparison.OrdinalIgnoreCase))
        {
            record["status"] = "user_confirmed_authoritative_contextual_room_action";
            if (record["runtime_mapping"] is JsonObject commandRuntimeMapping)
                commandRuntimeMapping["status"] = "runtime_authoritative_contextual_room_action";
            if (record["evidence_summary_0124"] is JsonObject commandEvidenceSummary)
                commandEvidenceSummary["confidence"] = "user_confirmed_classic_room_action";
            if (record["evidence"] is JsonArray commandEvidence && !commandEvidence.Any(item =>
                    item is JsonObject evidenceObject &&
                    string.Equals(evidenceObject["source"]?.GetValue<string>(), "UserConfirmationCodeUpdate0139", StringComparison.OrdinalIgnoreCase)))
            {
                commandEvidence.Add(new JsonObject
                {
                    ["source"] = "UserConfirmationCodeUpdate0139",
                    ["path"] = "Evidence/CodeUpdate0139-UserConfirmedRoomActionVerbs.md",
                    ["classification"] = "contextual_room_action_verb",
                    ["scope"] = "verb vocabulary only; exact room phrases and effects remain decoded-data-bound"
                });
            }
        }
    }

    public CompileResult Compile(LibrariumCompilerOptions options)
    {
        var result = new CompileResult { WorldId = LibrariumCompilerOptions.NormalizeWorld(options.WorldId), OutputRoot = options.OutputRoot };
        var reader = new LibrariumCatalogReader();
        var sourceFamilies = reader.ReadFamilies(options.LibrariumRoot, result.Diagnostics).ToArray();
        var effectiveSourceFamilies = new WorldOverrideEngine()
            .Apply(options.RepositoryRoot, result.WorldId, sourceFamilies, result.Diagnostics)
            .ToArray();
        var projectedFamilies = effectiveSourceFamilies
            .Where(family => !IsExcludedFromRuntime(result.WorldId, family.Name))
            .Select(family => ProjectRuntimeRecords(family, result.WorldId))
            .ToList();
        var families = projectedFamilies
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ApplyCrossFamilyRuntimeGuards(families);
        new LibrariumValidator().Validate(families, result.Diagnostics);

        if (!result.Success) return result;

        if (options.Clean && Directory.Exists(options.OutputRoot)) Directory.Delete(options.OutputRoot, recursive: true);
        Directory.CreateDirectory(options.OutputRoot);

        foreach (var family in families)
        {
            var output = CreateRuntimeFamilyProjection(options, result, family);

            var fileName = family.Name.Replace('-', '_').ToLowerInvariant() + ".json";
            WriteJson(Path.Combine(options.OutputRoot, fileName), output);
            result.FilesWritten++;
            result.ObjectFamiliesCompiled++;
            result.ObjectsCompiled += family.Records.Count;
        }

        result.FilesWritten += ClassicQuestRuntimeProjectionWriter.Write(
            options.RepositoryRoot,
            options.OutputRoot,
            WriteJson);

        // Specialized projections may consume authoring-only families that remain
        // in Librarium but must not become standalone runtime files.
        result.FilesWritten += RuntimeItemProjectionWriter.Write(options, effectiveSourceFamilies, WriteJson);

        if (options.ClientProjection)
        {
            WriteJson(Path.Combine(options.OutputRoot, "client_projection.json"), CreateClientProjection(result, families));
            result.FilesWritten++;
        }

        WriteJson(Path.Combine(options.OutputRoot, "world_manifest.json"), CreateWorldManifest(result, families));
        result.FilesWritten++;

        WriteJson(Path.Combine(options.OutputRoot, "compile_manifest.json"), CreateManifest(result, families));
        result.FilesWritten++;
        return result;
    }

    private static bool IsExcludedFromRuntime(string worldId, string familyName) =>
        familyName.Equals("room_messages", StringComparison.OrdinalIgnoreCase) ||
        familyName.Equals("classic_bug_fixes", StringComparison.OrdinalIgnoreCase) ||
        UsesClassicRuntimeShape(worldId) && NonRuntimeClassicFamilies.Contains(familyName);

    private static bool UsesClassicRuntimeShape(string worldId) =>
        worldId.Equals("Classic", StringComparison.OrdinalIgnoreCase) ||
        worldId.Equals("Custom1", StringComparison.OrdinalIgnoreCase);

    private static LibrariumFamily ProjectRuntimeRecords(LibrariumFamily family, string worldId)
    {
        if (family.Name.Equals("banks", StringComparison.OrdinalIgnoreCase))
        {
            var runtimeBanks = family.Records.Where(record =>
                !(string.Equals(record["id"]?.GetValue<string>(), "bank.2", StringComparison.OrdinalIgnoreCase) &&
                  string.Equals(record["name"]?.GetValue<string>(), "Leave this blank", StringComparison.OrdinalIgnoreCase) &&
                  ReadInt(record, "room_number") <= 0)).ToArray();
            return new LibrariumFamily(family.Name, family.SourceFile, runtimeBanks, family.SourceFiles);
        }

        if (family.Name.Equals("triggers", StringComparison.OrdinalIgnoreCase))
        {
            var runtimeTriggers = family.Records.Where(record =>
                string.Equals(record["trigger_type"]?.GetValue<string>(), "npc_quest_entry", StringComparison.OrdinalIgnoreCase) ||
                (worldId.Equals("Custom1", StringComparison.OrdinalIgnoreCase) && IsCustomAuthoredRecord(record))).ToArray();
            return new LibrariumFamily(family.Name, family.SourceFile, runtimeTriggers, family.SourceFiles);
        }

        if (family.Name.Equals("scripts", StringComparison.OrdinalIgnoreCase))
        {
            var runtimeScripts = family.Records.Where(record =>
                string.Equals(record["script_type"]?.GetValue<string>(), "mmexplorer_textblock_stage", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(record["script_type"]?.GetValue<string>(), "npc_hit_spell_textblock_chain", StringComparison.OrdinalIgnoreCase) ||
                (worldId.Equals("Custom1", StringComparison.OrdinalIgnoreCase) && IsCustomAuthoredRecord(record))).ToArray();
            return new LibrariumFamily(family.Name, family.SourceFile, runtimeScripts, family.SourceFiles);
        }

        if(family.Name is not ("quests" or "formulas")) return family;

        var runtimeRecords=family.Records.Where(record=>
        {
            if(record["runtime_mapping"] is not JsonObject mapping)return true;
            return !string.Equals(mapping["status"]?.GetValue<string>(),"pending",StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        return new LibrariumFamily(family.Name,family.SourceFile,runtimeRecords,family.SourceFiles);
    }

    private static bool IsCustomAuthoredRecord(JsonObject record)
    {
        if (string.Equals(record["confidence"]?.GetValue<string>(), "host_authored_custom", StringComparison.OrdinalIgnoreCase)) return true;
        if (record["runtime_mapping"] is JsonObject mapping &&
            string.Equals(mapping["status"]?.GetValue<string>(), "runtime_authoritative_custom", StringComparison.OrdinalIgnoreCase)) return true;
        if (record["world_scope"] is JsonArray scopes && scopes.Any(scope =>
                string.Equals(scope?.GetValue<string>(), "custom", StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }

    private static void ApplyCrossFamilyRuntimeGuards(IReadOnlyList<LibrariumFamily> families)
    {
        var rooms = families.FirstOrDefault(f => f.Name.Equals("rooms", StringComparison.OrdinalIgnoreCase));
        var exits = families.FirstOrDefault(f => f.Name.Equals("exits", StringComparison.OrdinalIgnoreCase));
        if (rooms is null || exits is null) return;

        var roomIds = rooms.Records
            .Select(record => record["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var exit in exits.Records)
        {
            var targetId = ReadExitTargetRoomId(exit);
            if (string.IsNullOrWhiteSpace(targetId) || roomIds.Contains(targetId)) continue;

            exit["runtime_disposition"] = "invalid_destination_blocked";
            exit["semantic_status"] = "blocked_unresolved_destination";
            exit["fallback_behavior"] = "deny traversal because generated target room is not present";
            exit["generator_validation"] = new JsonObject
            {
                ["status"] = "blocked_unresolved_destination",
                ["target_room_id"] = targetId,
                ["policy"] = "compiler cross-family validation prevents unresolved exits from becoming executable traversal"
            };

            if (exit["runtime_mapping"] is JsonObject runtimeMapping)
            {
                runtimeMapping["status"] = "blocked_unresolved_destination";
            }
        }
    }

    private static string? ReadExitTargetRoomId(JsonObject exit)
    {
        if (exit["relationships"] is JsonObject relationships)
        {
            var relationshipTarget = relationships["to_room"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(relationshipTarget)) return relationshipTarget;
        }

        var map = ReadInt(exit, "to_map_number");
        var room = ReadInt(exit, "to_room_number");
        return map > 0 && room > 0 ? $"room.map{map}.{room}" : null;
    }


    private static JsonObject CreateRuntimeFamilyProjection(
        LibrariumCompilerOptions options,
        CompileResult result,
        LibrariumFamily family)
    {
        if (family.Name.Equals("rooms", StringComparison.OrdinalIgnoreCase))
        {
            var audit = new NativeMapRoomTextResolver().ApplyDescriptions(options.RepositoryRoot, family);
            result.Diagnostics.Add(new(
                DiagnosticSeverity.Info,
                "ROOMTEXT001",
                $"Native room text recovery emitted {audit.EmittedNonEmptyDescriptions:N0}/{audit.SourceRooms:N0} non-empty descriptions; unresolved {audit.UnresolvedReferences:N0}; missing native map files {audit.MissingNativeMapFiles.Count:N0}.",
                family.SourceFile));
        }

        if (family.Name.Equals("character_foundation", StringComparison.OrdinalIgnoreCase))
        {
            var promoted = TryReadPromotedCharacterFoundation(family);
            if (promoted is not null)
            {
                promoted["generated_by"] = "MajorMud.Reforged.Compiler";
                promoted["compiler_code_update"] = "0138";
                promoted["world_id"] = result.WorldId;
                promoted["family"] = family.Name;
                promoted["source_catalog_file"] = Path.GetRelativePath(options.RepositoryRoot, family.SourceFile).Replace('\\', '/');
                NormalizeRuntimeMetadata(promoted, family.Name);
                StripNonRuntimeMetadata(promoted, family.Name);
                return promoted;
            }
        }

        var projection = new JsonObject
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "MajorMud.Reforged.Compiler",
            ["compiler_code_update"] = "0127",
            ["world_id"] = result.WorldId,
            ["family"] = family.Name,
            ["source_file"] = Path.GetRelativePath(options.RepositoryRoot, family.SourceFile).Replace('\\', '/'),
            ["record_count"] = family.Records.Count,
            ["records"] = new JsonArray(family.Records.Select(r =>
            {
                var clone=r.DeepClone();
                NormalizeRuntimeMetadata(clone, family.Name);
                StripNonRuntimeMetadata(clone, family.Name);
                return clone;
            }).ToArray())
        };

        var sourceFiles = family.SourceFiles ?? [family.SourceFile];
        if (sourceFiles.Count > 1)
        {
            projection["source_files"] = new JsonArray(sourceFiles
                .Select(path => JsonValue.Create(Path.GetRelativePath(options.RepositoryRoot, path).Replace('\\', '/')))
                .ToArray());
        }

        return projection;
    }

    internal static void StripNonRuntimeMetadata(JsonNode? node, string? familyName = null)
    {
        NonRuntimeFamilyRecordProperties.TryGetValue(familyName ?? string.Empty, out var familyProperties);
        if(node is JsonObject obj)
        {
            foreach(var key in obj.Select(property=>property.Key).ToArray())
            {
                if(NonRuntimeRecordProperties.Contains(key) || familyProperties?.Contains(key) == true)
                {
                    obj.Remove(key);
                    continue;
                }
                StripNonRuntimeMetadata(obj[key], familyName);
            }
            return;
        }
        if(node is JsonArray array)foreach(var child in array)StripNonRuntimeMetadata(child, familyName);
    }

    private static JsonObject? TryReadPromotedCharacterFoundation(LibrariumFamily family)
    {
        foreach (var record in family.Records)
        {
            var sourceFile = record["source_file"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(sourceFile)) continue;

            var sourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(family.SourceFile)!, sourceFile));
            if (!File.Exists(sourcePath)) continue;

            try
            {
                var source = JsonNode.Parse(File.ReadAllText(sourcePath)) as JsonObject;
                if (source is null) continue;
                if (source["races"] is not JsonArray || source["classes"] is not JsonArray) continue;
                return (JsonObject)source.DeepClone();
            }
            catch (JsonException)
            {
                // Librarium validation owns malformed JSON diagnostics.
            }
        }

        return null;
    }

    private static JsonObject CreateManifest(CompileResult result, IReadOnlyList<LibrariumFamily> families)
        => new()
        {
            ["code_update"] = "0127",
            ["compiler"] = "MajorMud.Reforged.Compiler",
            ["world_id"] = result.WorldId,
            ["librarium_authority"] = "Librarium is source of truth; /Data is generated runtime output.",
            ["object_families_compiled"] = result.ObjectFamiliesCompiled,
            ["objects_compiled"] = result.ObjectsCompiled,
            ["files_written"] = result.FilesWritten + 1,
            ["families"] = new JsonArray(families.Select(f => new JsonObject
            {
                ["family"] = f.Name,
                ["source_file"] = f.SourceFile.Replace('\\', '/'),
                ["source_files"] = new JsonArray((f.SourceFiles ?? [f.SourceFile])
                    .Select(path => JsonValue.Create(path.Replace('\\', '/')))
                    .ToArray()),
                ["record_count"] = CountGeneratedRecords(result.OutputRoot, f.Name) ?? f.Records.Count,
                ["field_inventory"] = CreateFieldInventory(f)
            }).ToArray()),
            ["diagnostics"] = new JsonArray(result.Diagnostics.Select(d => new JsonObject
            {
                ["severity"] = d.Severity.ToString(),
                ["code"] = d.Code,
                ["message"] = d.Message,
                ["path"] = d.Path
            }).ToArray())
        };

    private static JsonArray CreateFieldInventory(LibrariumFamily family)
    {
        var fields = new Dictionary<string, FieldInventoryEntry>(StringComparer.Ordinal);
        foreach (var record in family.Records)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            InventoryFields(record, string.Empty, present, fields);
            foreach (var path in present)
            {
                fields[path].RecordCount++;
            }
        }

        return new JsonArray(fields
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (JsonNode)new JsonObject
            {
                ["path"] = pair.Key,
                ["record_count"] = pair.Value.RecordCount
            })
            .ToArray());
    }

    private static void InventoryFields(
        JsonNode? node,
        string path,
        HashSet<string> present,
        Dictionary<string, FieldInventoryEntry> fields)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var childPath = path.Length == 0 ? property.Key : $"{path}.{property.Key}";
                InventoryFields(property.Value, childPath, present, fields);
            }
            return;
        }

        if (node is JsonArray array)
        {
            var arrayPath = path + "[]";
            if (!fields.TryGetValue(arrayPath, out var arrayEntry))
            {
                fields[arrayPath] = arrayEntry = new FieldInventoryEntry();
            }
            arrayEntry.ValueKinds.Add("array");
            present.Add(arrayPath);
            foreach (var value in array)
            {
                InventoryFields(value, arrayPath, present, fields);
            }
            return;
        }

        if (path.Length == 0) return;
        if (!fields.TryGetValue(path, out var entry))
        {
            fields[path] = entry = new FieldInventoryEntry();
        }
        entry.ValueKinds.Add(node switch
        {
            JsonValue value when value.TryGetValue<bool>(out _) => "boolean",
            JsonValue value when value.TryGetValue<long>(out _) => "integer",
            JsonValue value when value.TryGetValue<double>(out _) => "number",
            JsonValue => "string",
            null => "null",
            _ => "unknown"
        });
        present.Add(path);
    }

    private sealed class FieldInventoryEntry
    {
        public int RecordCount { get; set; }
        public HashSet<string> ValueKinds { get; } = new(StringComparer.Ordinal);
    }

    private static int? CountGeneratedRecords(string outputRoot, string familyName)
    {
        var path = Path.Combine(outputRoot, familyName.Replace('-', '_').ToLowerInvariant() + ".json");
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array) return root.GetArrayLength();
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("records", out var records) &&
                records.ValueKind == JsonValueKind.Array)
                return records.GetArrayLength();
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("record_count", out var declared) &&
                declared.TryGetInt32(out var count))
                return count;
            return 0;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject CreateClientProjection(CompileResult result, IReadOnlyList<LibrariumFamily> families)
        => new()
        {
            ["world_id"] = result.WorldId,
            ["projection"] = "read_only_client_librarium_projection_v1",
            ["families_available"] = new JsonArray(families.Select(f => JsonValue.Create(f.Name)).ToArray()),
            ["client_rule"] = "Client reads world-scoped Librarium/Data projections only; client never edits gameplay definitions."
        };

    private static JsonObject CreateWorldManifest(CompileResult result, IReadOnlyList<LibrariumFamily> families)
    {
        var manifest = new JsonObject
        {
            ["schema"] = "generated_world_manifest_v1",
            ["code_update"] = "0127",
            ["world_id"] = result.WorldId,
            ["output_root"] = result.OutputRoot.Replace('\\', '/'),
            ["object_families_compiled"] = result.ObjectFamiliesCompiled,
            ["objects_compiled"] = result.ObjectsCompiled,
            ["families"] = new JsonArray(families.Select(f => JsonValue.Create(f.Name)).ToArray()),
            ["runtime_loader_contract"] = "Librarium/RuntimeContracts/generated_world_loader_contract_0127.json",
            ["source_of_truth"] = "Librarium",
            ["generated_output"] = true
        };

        var characterStart = TryReadCharacterStart(families);
        if (characterStart is not null)
        {
            manifest["character_start_room"] = characterStart.RoomId;
            manifest["character_spawning"] = new JsonObject
            {
                ["start_room"] = characterStart.RoomId,
                ["map_id"] = characterStart.MapId,
                ["room_number"] = characterStart.RoomNumber,
                ["name"] = characterStart.Name,
                ["classification"] = "Librarium_promoted_classic_character_start"
            };
        }

        return manifest;
    }

    private static CharacterStart? TryReadCharacterStart(IReadOnlyList<LibrariumFamily> families)
    {
        var family = families.FirstOrDefault(f =>
            f.Name.Equals("character_foundation", StringComparison.OrdinalIgnoreCase));
        if (family is null) return null;

        foreach (var record in family.Records)
        {
            var sourceFile = record["source_file"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(sourceFile)) continue;

            var sourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(family.SourceFile)!, sourceFile));
            if (!File.Exists(sourcePath)) continue;

            try
            {
                var source = JsonNode.Parse(File.ReadAllText(sourcePath)) as JsonObject;
                var start = source?["character_creation_rules"]?["new_character_start"] as JsonObject;
                if (start is null) continue;

                var mapId = ReadInt(start, "map_id");
                var roomNumber = ReadInt(start, "room_number");
                var roomId = start["room_id"]?.GetValue<string>();
                var name = start["name"]?.GetValue<string>() ?? string.Empty;

                if (mapId <= 0 || roomNumber <= 0) continue;
                if (string.IsNullOrWhiteSpace(roomId)) roomId = $"room.map{mapId}.{roomNumber}";

                return new CharacterStart(roomId, mapId, roomNumber, name);
            }
            catch (JsonException)
            {
                // Librarium validation reports malformed source JSON. The manifest writer
                // remains deterministic and simply omits an unresolvable start location.
            }
        }

        return null;
    }

    private static int ReadInt(JsonObject obj, string key)
    {
        var value = obj[key];
        if (value is null) return 0;
        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out var number)) return number;
            if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out number)) return number;
        }
        return 0;
    }

    private sealed record CharacterStart(string RoomId, int MapId, int RoomNumber, string Name);

    private static void WriteJson(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var serialized = SerializeJson(node);
        if (serialized.Length < 90_000_000 || node is not JsonObject obj || obj["records"] is not JsonArray records)
        {
            File.WriteAllText(path, serialized);
            return;
        }

        var familyName = Path.GetFileNameWithoutExtension(path);
        var partDirectoryName = familyName + ".parts";
        var partDirectory = Path.Combine(Path.GetDirectoryName(path)!, partDirectoryName);
        if (Directory.Exists(partDirectory)) Directory.Delete(partDirectory, recursive: true);
        Directory.CreateDirectory(partDirectory);

        var partFiles = new JsonArray();
        var partRecords = new JsonArray();
        var partIndex = 1;
        foreach (var record in records.Select(record => record?.DeepClone()).ToArray())
        {
            partRecords.Add(record);
            if (partRecords.Count < 5_000) continue;
            WritePart(path, obj, partDirectoryName, partDirectory, partIndex++, partRecords, partFiles);
            partRecords = new JsonArray();
        }

        if (partRecords.Count > 0)
        {
            WritePart(path, obj, partDirectoryName, partDirectory, partIndex, partRecords, partFiles);
        }

        var manifest = new JsonObject();
        foreach (var property in obj)
        {
            if (property.Key.Equals("records", StringComparison.OrdinalIgnoreCase)) continue;
            manifest[property.Key] = property.Value?.DeepClone();
        }

        manifest["chunked_records"] = true;
        manifest["record_parts"] = partFiles;
        WriteJsonFile(path, manifest);
    }

    private static void WritePart(
        string originalPath,
        JsonObject original,
        string partDirectoryName,
        string partDirectory,
        int partIndex,
        JsonArray partRecords,
        JsonArray partFiles)
    {
        var fileName = $"{Path.GetFileNameWithoutExtension(originalPath)}.part{partIndex:0000}.json";
        var part = new JsonObject
        {
            ["authority"] = original["authority"]?.DeepClone(),
            ["generated_by"] = original["generated_by"]?.DeepClone(),
            ["world_id"] = original["world_id"]?.DeepClone(),
            ["family"] = original["family"]?.DeepClone(),
            ["record_count"] = partRecords.Count,
            ["records"] = partRecords
        };
        WriteJsonFile(Path.Combine(partDirectory, fileName), part);
        partFiles.Add($"{partDirectoryName}/{fileName}");
    }

    private static string SerializeJson(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = JsonOptions.WriteIndented }))
        {
            node.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJsonFile(string path, JsonNode node)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = JsonOptions.WriteIndented });
        node.WriteTo(writer);
    }
}
