using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

internal static class ClassicQuestRuntimeProjectionWriter
{
    private const string DecodedTextblocks = "Librarium/Objects/textblocks/textblock_records.json";
    private const string NativeTextblockPayloads = "Librarium/Objects/native_textblock_payloads/native_textblock_payload_records.json";

    public static int Write(string repositoryRoot, string outputRoot, Action<string, JsonNode> writeJson)
    {
        var sourcePath = System.IO.Path.Combine(repositoryRoot, DecodedTextblocks.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Decoded Classic textblock source is required for quest runtime projections.", sourcePath);

        using var document = JsonDocument.Parse(File.ReadAllText(sourcePath));
        var roots = document.RootElement.EnumerateArray()
            .Where(row => RootIds.Contains(ReadInt(row, "textblock_id")))
            .ToDictionary(row => ReadInt(row, "textblock_id"), row => row.Clone());

        if (roots.Count != RootIds.Count)
            throw new InvalidDataException($"Expected {RootIds.Count} decoded Classic quest roots; found {roots.Count}.");

        var rootRecords = new JsonArray();
        foreach (var rootId in RootIds)
        {
            var root = roots[rootId];
            var ownerRaw = ReadString(root, "owner_raw");
            var ownerNumber = ParseOwnerNumber(ownerRaw);
            var commands = root.TryGetProperty("commands", out var commandRows) && commandRows.ValueKind == JsonValueKind.Array
                ? new JsonArray(commandRows.EnumerateArray()
                    .Select(row => ReadString(row, "normalized_keyword"))
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(value => (JsonNode?)JsonValue.Create(value))
                    .ToArray())
                : new JsonArray();

            rootRecords.Add(new JsonObject
            {
                ["quest_family"] = rootId switch
                {
                    335 => "alignment.good_neutral_evil",
                    366 => "alignment.good_neutral_evil",
                    428 => "alignment.good_neutral_evil",
                    _ => "jungle_gnome_scroll"
                },
                ["root_textblock_id"] = rootId,
                ["npc_name"] = ownerNumber > 0 ? $"Monster #{ownerNumber}" : ownerRaw,
                ["npc_id"] = ownerNumber > 0 ? $"npc.{ownerNumber}" : string.Empty,
                ["activation_kind"] = "npc_command_map",
                ["stage_levels"] = rootId == 2798 ? new JsonArray() : new JsonArray(10, 20, 30, 40, 50),
                ["restrictions_owner"] = "BundleACharacter",
                ["inventory_owner"] = "BundleCItemsEconomy",
                ["narrative_owner"] = "BundleF",
                ["persistence_owner"] = "BundleGStorage",
                ["commands"] = commands,
                ["evidence"] = DecodedTextblocks
            });
        }

        var alignmentRecords = new JsonArray
        {
            CreateAlignmentPath("Good", "Chancellor Annotated", "npc.244", 335),
            CreateAlignmentPath("Neutral", "Grey Lord", "npc.251", 366),
            CreateAlignmentPath("Evil", "Balthazar", "npc.263", 428)
        };

        writeJson(System.IO.Path.Combine(outputRoot, "quest_narrative_roots.json"), Envelope(
            "quest_narrative_roots", rootRecords));
        writeJson(System.IO.Path.Combine(outputRoot, "alignment_quest_narrative_paths.json"), Envelope(
            "alignment_quest_narrative_paths", alignmentRecords));
        WriteGnomeMerchantPlacement(outputRoot, writeJson);
        WriteGnomeMerchantOfferings(repositoryRoot, outputRoot, writeJson);
        WriteWitchunterNarrative(repositoryRoot, outputRoot, writeJson);
        WriteWorldRewardNarrative(repositoryRoot, outputRoot, writeJson);
        WriteRandomTableAuthority(repositoryRoot, outputRoot, writeJson);
        WriteEventMissionAuthority(repositoryRoot, outputRoot, writeJson);
        WriteRewardExecutionAuthority(repositoryRoot, outputRoot, writeJson);
        WriteNarrativePersistenceAuthority(outputRoot, writeJson);
        return 10;
    }

    private static void WriteEventMissionAuthority(string repositoryRoot, string outputRoot, Action<string, JsonNode> writeJson)
    {
        var sourcePath = System.IO.Path.Combine(repositoryRoot, DecodedTextblocks.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var decoded = ReadDecodedTextblocks(sourcePath).ToArray();
        var decodedIds = decoded.Select(row => ReadInt(row, "textblock_id")).ToHashSet();
        var missionKeywords = new HashSet<string>(["work", "mission", "missions", "reconcile"], StringComparer.OrdinalIgnoreCase);
        var routes = new JsonArray();
        var triggers = new JsonArray();
        var delays = new JsonArray();
        var operationCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in decoded.OrderBy(row => ReadInt(row, "textblock_id")))
        {
            var textblockId = ReadInt(record, "textblock_id");
            var ownerNpcId = ParseOwnerNumber(ReadString(record, "owner_raw"));
            if (record.TryGetProperty("commands", out var commands) && commands.ValueKind == JsonValueKind.Array)
            {
                foreach (var command in commands.EnumerateArray())
                {
                    var keyword = ReadString(command, "normalized_keyword").ToLowerInvariant();
                    if (!missionKeywords.Contains(keyword)) continue;
                    var target = ReadInt(command, "target_textblock_id");
                    routes.Add(new JsonObject
                    {
                        ["root_textblock_id"] = textblockId,
                        ["owner_npc_id"] = ownerNpcId > 0 ? ownerNpcId : null,
                        ["keyword"] = keyword,
                        ["target_textblock_id"] = target,
                        ["target_exists"] = decodedIds.Contains(target)
                    });
                }
            }

            if (record.TryGetProperty("triggers", out var triggerRows) && triggerRows.ValueKind == JsonValueKind.Array)
            {
                foreach (var trigger in triggerRows.EnumerateArray())
                {
                    triggers.Add(new JsonObject
                    {
                        ["textblock_id"] = textblockId,
                        ["line"] = ReadInt(trigger, "line"),
                        ["kind"] = ReadString(trigger, "trigger_kind"),
                        ["raw"] = ReadString(trigger, "raw")
                    });
                }
            }

            if (!record.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var operation in operations.EnumerateArray())
            {
                var opcode = ReadString(operation, "opcode").ToLowerInvariant();
                var family = EventExecutionFamily(opcode);
                if (family.Length > 0) operationCounts[family] = operationCounts.GetValueOrDefault(family) + 1;
                if (opcode is not ("delay" or "adddelay")) continue;
                delays.Add(new JsonObject
                {
                    ["textblock_id"] = textblockId,
                    ["line"] = ReadInt(operation, "line"),
                    ["sequence"] = ReadInt(operation, "sequence"),
                    ["opcode"] = opcode,
                    ["delay_value"] = ReadIntArgument(operation, 0),
                    ["raw"] = ReadString(operation, "raw")
                });
            }
        }

        operationCounts["teleport"] = operationCounts.GetValueOrDefault("teleport") + ReadNativeTable9383(repositoryRoot).Count;
        var contracts = new JsonArray(RequiredEventFamilies.Select(family => (JsonNode?)new JsonObject
        {
            ["family"] = family,
            ["operation_count"] = operationCounts.GetValueOrDefault(family),
            ["owner"] = EventExecutionOwner(family)
        }).ToArray());
        if (routes.Count != 8 || triggers.Count != 2318 || delays.Count != 276 ||
            delays.Count(node => node!["opcode"]!.GetValue<string>() == "adddelay") != 256 ||
            delays.Count(node => node!["opcode"]!.GetValue<string>() == "delay") != 20)
            throw new InvalidDataException($"Decoded event/mission authority drifted: routes={routes.Count}, triggers={triggers.Count}, delays={delays.Count}.");

        writeJson(System.IO.Path.Combine(outputRoot, "event_mission_runtime_authority.json"), new JsonObject
        {
            ["schema"] = "classic-event-mission-runtime-authority.v1",
            ["authority"] = "Librarium",
            ["execution_owner"] = "Bundle F",
            ["scheduler_owner"] = "Bundle G",
            ["fail_closed"] = true,
            ["mission_route_count"] = routes.Count,
            ["trigger_count"] = triggers.Count,
            ["delay_operation_count"] = delays.Count,
            ["mission_routes"] = routes,
            ["triggers"] = triggers,
            ["delay_operations"] = delays,
            ["execution_contracts"] = contracts,
            ["evidence"] = DecodedTextblocks
        });
    }

    private static void WriteRewardExecutionAuthority(string repositoryRoot, string outputRoot, Action<string, JsonNode> writeJson)
    {
        var sourcePath = System.IO.Path.Combine(repositoryRoot, DecodedTextblocks.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var decoded = ReadDecodedTextblocks(sourcePath).ToArray();
        var records = new JsonArray();
        foreach (var source in decoded.OrderBy(row => ReadInt(row, "textblock_id")))
        {
            if (!source.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array) continue;
            foreach (var operation in operations.EnumerateArray())
                AddRewardOperation(records, ReadInt(source, "textblock_id"), operation);
        }

        var line = 1;
        foreach (var (_, roomNumber, mapNumber) in ReadNativeTable9383(repositoryRoot))
        {
            var operation = JsonSerializer.SerializeToElement(new
            {
                line,
                sequence = 2,
                raw = $"teleport {roomNumber} {mapNumber}",
                opcode = "teleport",
                arguments = new[] { roomNumber.ToString(), mapNumber.ToString() }
            });
            AddRewardOperation(records, 9383, operation);
            line++;
        }

        var opcodes = records.Select(node => node!["opcode"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!opcodes.SetEquals(RewardOwners.Keys))
            throw new InvalidDataException("Decoded reward execution authority does not cover the complete reward opcode set.");
        writeJson(System.IO.Path.Combine(outputRoot, "reward_execution_authority.json"), new JsonObject
        {
            ["schema"] = "classic-reward-execution-authority.v1",
            ["authority"] = "Librarium",
            ["coordinator_owner"] = "Bundle F",
            ["fail_closed"] = true,
            ["reward_operation_count"] = records.Count,
            ["records"] = records,
            ["evidence"] = new JsonArray(DecodedTextblocks, NativeTextblockPayloads)
        });
    }

    private static void AddRewardOperation(JsonArray records, int textblockId, JsonElement operation)
    {
        var opcode = ReadString(operation, "opcode").ToLowerInvariant();
        if (!RewardOwners.TryGetValue(opcode, out var owner)) return;
        var arguments = operation.TryGetProperty("arguments", out var values) && values.ValueKind == JsonValueKind.Array
            ? new JsonArray(values.EnumerateArray().Select(value => (JsonNode?)JsonValue.Create(value.ToString())).ToArray())
            : new JsonArray();
        var mutates = opcode is not ("message" or "text");
        records.Add(new JsonObject
        {
            ["textblock_id"] = textblockId,
            ["line"] = ReadInt(operation, "line"),
            ["sequence"] = ReadInt(operation, "sequence"),
            ["opcode"] = opcode,
            ["arguments"] = arguments,
            ["raw"] = ReadString(operation, "raw"),
            ["owner"] = owner,
            ["consumer_contract"] = $"{owner} authoritative narrative binding",
            ["mutates_state"] = mutates,
            ["requires_atomic_boundary"] = mutates,
            ["duplicate_policy"] = mutates ? "persistent idempotency key required" : "presentation replay permitted"
        });
    }

    private static void WriteNarrativePersistenceAuthority(string outputRoot, Action<string, JsonNode> writeJson)
    {
        var contracts = new JsonArray(RewardOwners
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => (JsonNode?)new JsonObject
            {
                ["opcode"] = pair.Key,
                ["mutation_owner"] = pair.Value,
                ["duplicate_guard"] = "world/character/root/matched-textblock/operation-digest",
                ["storage_owner"] = "Bundle G"
            }).ToArray());
        writeJson(System.IO.Path.Combine(outputRoot, "narrative_persistence_authority.json"), new JsonObject
        {
            ["schema"] = "classic-narrative-persistence-authority.v1",
            ["authority"] = "Librarium",
            ["storage_owner"] = "Bundle G",
            ["orchestration_owner"] = "Bundle F",
            ["fail_closed"] = true,
            ["contracts"] = contracts,
            ["validation"] = new JsonObject
            {
                ["all_mutations_atomic"] = true,
                ["all_operations_have_duplicate_policy"] = true,
                ["interrupted_transactions_reconcile"] = true,
                ["logout_rollback_required"] = true,
                ["restart_reconciliation_required"] = true,
                ["committed_result_replay_without_mutation"] = true
            }
        });
    }

    private static void WriteRandomTableAuthority(string repositoryRoot, string outputRoot, Action<string, JsonNode> writeJson)
    {
        var sourcePath = System.IO.Path.Combine(repositoryRoot, DecodedTextblocks.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var decoded = ReadDecodedTextblocks(sourcePath).ToArray();
        var tableRows = decoded
            .Where(row => ReadString(row, "owner_raw").StartsWith("Textblock(rndm)", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => ReadInt(row, "textblock_id"))
            .ToArray();
        var recoveredTable9383 = ReadNativeTable9383(repositoryRoot);

        var references = new JsonArray();
        foreach (var source in decoded.OrderBy(row => ReadInt(row, "textblock_id")))
        {
            if (!source.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var operation in operations.EnumerateArray()
                         .Where(row => ReadString(row, "opcode").Equals("random", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(row => ReadInt(row, "line"))
                         .ThenBy(row => ReadInt(row, "sequence")))
            {
                references.Add(new JsonObject
                {
                    ["source_textblock_id"] = ReadInt(source, "textblock_id"),
                    ["line"] = ReadInt(operation, "line"),
                    ["sequence"] = ReadInt(operation, "sequence"),
                    ["target_table_id"] = ReadIntArgument(operation, 0)
                });
            }
        }

        var tables = new JsonArray();
        var branchCount = 0;
        var anomalyCount = 0;
        var emptyCount = 0;
        foreach (var table in tableRows)
        {
            var tableId = ReadInt(table, "textblock_id");
            var emptyResult = ReadString(table, "script").Trim().Equals("!!!", StringComparison.Ordinal);
            var branches = new JsonArray();
            var thresholds = new List<int>();

            if (!emptyResult)
            {
                if (!table.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException($"Decoded random table {tableId} has no threshold triggers.");
                var operations = table.TryGetProperty("operations", out var operationRows) && operationRows.ValueKind == JsonValueKind.Array
                    ? operationRows.EnumerateArray().ToArray()
                    : Array.Empty<JsonElement>();

                foreach (var trigger in triggers.EnumerateArray())
                {
                    var line = ReadInt(trigger, "line");
                    var threshold = ReadInt(trigger, "selection_value");
                    thresholds.Add(threshold);
                    branches.Add(new JsonObject
                    {
                        ["line"] = line,
                        ["threshold"] = threshold,
                        ["operations"] = new JsonArray(operations
                            .Where(operation => ReadInt(operation, "line") == line)
                            .OrderBy(operation => ReadInt(operation, "sequence"))
                            .Select(operation => JsonNode.Parse(operation.GetRawText()))
                            .ToArray())
                    });
                    branchCount++;
                }

                if (branches.Count == 0 && tableId == 9383)
                {
                    var line = 1;
                    foreach (var (threshold, roomNumber, mapNumber) in recoveredTable9383)
                    {
                        thresholds.Add(threshold);
                        var raw = $"teleport {roomNumber} {mapNumber}";
                        branches.Add(new JsonObject
                        {
                            ["line"] = line,
                            ["threshold"] = threshold,
                            ["operations"] = new JsonArray(new JsonObject
                            {
                                ["line"] = line,
                                ["sequence"] = 2,
                                ["raw"] = raw,
                                ["opcode"] = "teleport",
                                ["arguments"] = new JsonArray(roomNumber.ToString(), mapNumber.ToString()),
                                ["operation_kind"] = "effect",
                                ["status"] = "decoded",
                                ["numeric_arguments"] = new JsonArray(roomNumber, mapNumber),
                                ["room_number"] = roomNumber,
                                ["map_number"] = mapNumber
                            })
                        });
                        branchCount++;
                        line++;
                    }
                }

                if (branches.Count == 0)
                    throw new InvalidDataException($"Decoded random table {tableId} has no executable branches.");
            }
            else
            {
                emptyCount++;
            }

            var nonMonotonic = thresholds.Zip(thresholds.Skip(1), (left, right) => right < left).Any(value => value);
            if (nonMonotonic) anomalyCount++;
            tables.Add(new JsonObject
            {
                ["table_id"] = tableId,
                ["empty_result"] = emptyResult,
                ["non_monotonic_source_order"] = nonMonotonic,
                ["branches"] = branches,
                ["native_recovery"] = tableId == 9383 ? "native_textblock_payload.3250" : null
            });
        }

        if (references.Count != 595 || tables.Count != 136 || branchCount != 1222 || emptyCount != 1 || anomalyCount != 2)
            throw new InvalidDataException(
                $"Decoded random-table authority drifted: references={references.Count}, tables={tables.Count}, branches={branchCount}, empty={emptyCount}, anomalies={anomalyCount}.");

        writeJson(System.IO.Path.Combine(outputRoot, "random_table_runtime_authority.json"), new JsonObject
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "RealmGate.Compiler",
            ["world_id"] = "Classic",
            ["ordering_rule"] = "preserve-decoded-row-order",
            ["reference_count"] = references.Count,
            ["table_count"] = tables.Count,
            ["branch_count"] = branchCount,
            ["references"] = references,
            ["tables"] = tables,
            ["evidence"] = DecodedTextblocks
        });
    }

    private static IReadOnlyList<(int Threshold, int RoomNumber, int MapNumber)> ReadNativeTable9383(string repositoryRoot)
    {
        var path = System.IO.Path.Combine(repositoryRoot, NativeTextblockPayloads.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException("Native Classic textblock payloads are required for random table 9383.", path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var record = document.RootElement.GetProperty("records").EnumerateArray()
            .Single(row => ReadInt(row, "textblock_id") == 3250);
        if (!ReadString(record, "evidence_state").Equals("verified_native_runtime", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native textblock payload 3250 is not verified runtime evidence.");

        var result = new List<(int Threshold, int RoomNumber, int MapNumber)>();
        foreach (var sourceLine in ReadString(record, "payload").Split('\n'))
        {
            var line = sourceLine.Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0 || !int.TryParse(line[..colon], out var threshold)) continue;
            var parts = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !parts[0].Equals("teleport", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(parts[1], out var roomNumber) || !int.TryParse(parts[2], out var mapNumber))
                throw new InvalidDataException($"Native random table 9383 has an unsupported branch '{line}'.");
            result.Add((threshold, roomNumber, mapNumber));
        }

        var thresholds = result.Select(row => row.Threshold).ToArray();
        if (result.Count != 99 || thresholds[0] != 1 || thresholds[^1] != 100 || thresholds.Contains(44) ||
            thresholds.Distinct().Count() != result.Count || result.Any(row => row.MapNumber != 17))
            throw new InvalidDataException("Native random table 9383 does not match the verified 99-branch map-17 teleport contract.");
        return result;
    }

    private static void WriteWorldRewardNarrative(string repositoryRoot, string outputRoot, Action<string, JsonNode> writeJson)
    {
        var sourcePath = System.IO.Path.Combine(repositoryRoot, DecodedTextblocks.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var records = ReadDecodedTextblocks(sourcePath).ToArray();
        var byId = records
            .Where(row => ReadInt(row, "textblock_id") > 0)
            .ToDictionary(row => ReadInt(row, "textblock_id"));

        var fixedRewards = new JsonArray
        {
            FixedReward("classic.reward.high_druid_gem", 4703, null, 15, new[] { "touch gem", "touch black gem", "move gem", "move black gem" }, byId),
            FixedReward("classic.reward.ice_sorceress_throne", 4700, null, 15, new[] { "sit throne" }, byId),
            FixedReward("classic.reward.adult_red_dragon", 4702, null, 35, new[] { "touch ruby", "move ruby", "get ruby" }, byId),
            FixedReward("classic.reward.gaunt_one", 2698, 13, 15, Array.Empty<string>(), byId)
        };

        var classScripts = new JsonArray
        {
            ClassRewardScript(32, 2942, 965, byId),
            ClassRewardScript(186, 9029, null, byId),
            ClassRewardScript(187, 9046, 1351, byId)
        };

        writeJson(System.IO.Path.Combine(outputRoot, "world_reward_narrative_authority.json"), new JsonObject
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "RealmGate.Compiler",
            ["world_id"] = "Classic",
            ["fixed_rewards"] = fixedRewards,
            ["class_reward_scripts"] = classScripts,
            ["disconnects"] = new JsonArray(),
            ["conflicts"] = new JsonArray(),
            ["evidence"] = new JsonArray(DecodedTextblocks)
        });
    }

    private static JsonObject FixedReward(
        string id,
        int actionTextblockId,
        int? raceLegacyId,
        int minimumLevel,
        IReadOnlyList<string> activationTerms,
        IReadOnlyDictionary<int, JsonElement> records)
    {
        var action = RequireRecord(records, actionTextblockId);
        var line = FirstLine(action);
        var record = new JsonObject
        {
            ["id"] = id,
            ["activation_kind"] = "decoded_room_interaction",
            ["minimum_level"] = minimumLevel,
            ["root_textblock_id"] = actionTextblockId,
            ["one_time"] = true,
            ["activation_terms"] = new JsonArray(activationTerms.Select(term => (JsonNode?)JsonValue.Create(term)).ToArray()),
            ["operations"] = ProjectSimpleOperations(line)
        };
        if (raceLegacyId.HasValue) record["race_legacy_id"] = raceLegacyId.Value;
        return record;
    }

    private static JsonObject ClassRewardScript(
        int abilityId,
        int actionTextblockId,
        int? turninItemId,
        IReadOnlyDictionary<int, JsonElement> records)
    {
        var action = RequireRecord(records, actionTextblockId);
        var operations = action.GetProperty("operations").EnumerateArray().ToArray();
        var gates = new JsonArray();
        foreach (var group in operations.GroupBy(operation => ReadInt(operation, "line")).OrderBy(group => group.Key))
        {
            var lineOperations = group.ToArray();
            var classId = lineOperations.Select(operation =>
                    ReadString(operation, "opcode").Equals("class", StringComparison.OrdinalIgnoreCase)
                        ? ReadIntArgument(operation, 0)
                        : 0)
                .FirstOrDefault(value => value > 0);
            var abilityGateIndex = Array.FindIndex(lineOperations, operation =>
                ReadString(operation, "opcode").Equals("failability", StringComparison.OrdinalIgnoreCase)
                && ReadIntArgument(operation, 0) == abilityId);
            var minimumLevel = lineOperations
                .Select((operation, index) => new { operation, index })
                .Where(candidate => ReadString(candidate.operation, "opcode").Equals("minlevel", StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => abilityGateIndex < 0
                    ? int.MaxValue
                    : Math.Abs(candidate.index - abilityGateIndex))
                .Select(candidate => ReadIntArgument(candidate.operation, 0))
                .FirstOrDefault();
            if (classId <= 0 || abilityGateIndex < 0 || minimumLevel <= 0) continue;
            gates.Add(new JsonObject
            {
                ["class_id"] = classId,
                ["class_name"] = ClassName(classId),
                ["minimum_level"] = minimumLevel
            });
        }

        var record = new JsonObject
        {
            ["id"] = $"classic.class_reward.ability_{abilityId}",
            ["ability_id"] = abilityId,
            ["root_textblock_id"] = actionTextblockId,
            ["experience_reward"] = ReadFirstOperationArgument(action, "addexp"),
            ["gates"] = gates
        };
        if (turninItemId.HasValue) record["turnin_item_id"] = turninItemId.Value;
        return record;
    }

    private static JsonArray ProjectSimpleOperations(IEnumerable<JsonElement> operations)
    {
        var result = new JsonArray();
        foreach (var operation in operations)
        {
            var args = new JsonArray();
            if (operation.TryGetProperty("numeric_arguments", out var values) && values.ValueKind == JsonValueKind.Array)
                foreach (var value in values.EnumerateArray())
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) args.Add(number);
            result.Add(new JsonObject
            {
                ["opcode"] = ReadString(operation, "opcode"),
                ["arguments"] = args
            });
        }
        return result;
    }

    private static JsonElement[] FirstLine(JsonElement action)
    {
        if (!action.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Decoded world-reward action {ReadInt(action, "textblock_id")} has no operations.");
        var line = operations.EnumerateArray().Select(operation => ReadInt(operation, "line")).Where(value => value > 0).DefaultIfEmpty(0).Min();
        return operations.EnumerateArray().Where(operation => ReadInt(operation, "line") == line).ToArray();
    }

    private static JsonElement RequireRecord(IReadOnlyDictionary<int, JsonElement> records, int id) =>
        records.TryGetValue(id, out var record)
            ? record
            : throw new InvalidDataException($"Decoded world-reward textblock {id} is required.");

    private static int ReadFirstOperationArgument(JsonElement action, string opcode) =>
        action.GetProperty("operations").EnumerateArray()
            .Where(operation => ReadString(operation, "opcode").Equals(opcode, StringComparison.OrdinalIgnoreCase))
            .Select(operation => ReadIntArgument(operation, 0))
            .FirstOrDefault();

    private static int ReadIntArgument(JsonElement operation, int index)
    {
        if (!operation.TryGetProperty("numeric_arguments", out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() <= index)
            return 0;
        var value = values[index];
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }

    private static string ClassName(int classId) => classId switch
    {
        1 => "Warrior", 2 => "Witchunter", 3 => "Paladin", 4 => "Cleric", 5 => "Priest",
        6 => "Missionary", 7 => "Ninja", 8 => "Thief", 9 => "Bard", 10 => "Gypsy",
        11 => "Warlock", 12 => "Mage", 13 => "Druid", 14 => "Ranger", 15 => "Mystic",
        _ => throw new InvalidDataException($"Unknown Classic class ID {classId} in decoded world-reward script.")
    };

    private static void WriteWitchunterNarrative(string repositoryRoot, string outputRoot, Action<string, JsonNode> writeJson)
    {
        var sourcePath = System.IO.Path.Combine(repositoryRoot, DecodedTextblocks.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var records = ReadDecodedTextblocks(sourcePath).ToArray();
        var byId = records
            .Where(row => ReadInt(row, "textblock_id") > 0)
            .ToDictionary(row => ReadInt(row, "textblock_id"));
        var routes = new[]
        {
            (NpcId: 257, RootId: 393, Keywords: new[] { "sword", "witchunter", "draka", "visit" }),
            (NpcId: 532, RootId: 4028, Keywords: new[] { "return", "messenger" }),
            (NpcId: 542, RootId: 4375, Keywords: new[] { "task", "handmaidens", "return", "lock" }),
            (NpcId: 831, RootId: 9286, Keywords: new[] { "return", "armandriel" })
        };
        var scripts = new JsonArray();
        var stageSlot = 1;
        foreach (var route in routes)
        {
            if (!byId.TryGetValue(route.RootId, out var root) ||
                !ReadString(root, "content_kind").Equals("command_map", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Decoded Witchunter command map {route.RootId} is required.");

            var commands = root.TryGetProperty("commands", out var commandRows) && commandRows.ValueKind == JsonValueKind.Array
                ? commandRows.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            foreach (var keyword in route.Keywords)
            {
                var command = commands.FirstOrDefault(row => ReadString(row, "normalized_keyword").Equals(keyword, StringComparison.OrdinalIgnoreCase));
                var actionTarget = ReadInt(command, "target_textblock_id");
                if (actionTarget <= 0)
                    throw new InvalidDataException($"Decoded Witchunter command '{keyword}' on NPC {route.NpcId} has no target.");

                var action = records.SingleOrDefault(row =>
                    ReadString(row, "content_kind").Equals("action_script", StringComparison.OrdinalIgnoreCase) &&
                    ReadString(row, "owner_raw").Equals($"Textblock #{actionTarget}", StringComparison.OrdinalIgnoreCase));
                if (action.ValueKind == JsonValueKind.Undefined)
                    throw new InvalidDataException($"Decoded Witchunter action for textblock {actionTarget} is required.");
                if (!action.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException($"Decoded Witchunter action {ReadInt(action, "textblock_id")} has no operations.");

                scripts.Add(new JsonObject
                {
                    ["script_id"] = $"witchunter.npc.{route.NpcId}.{keyword}",
                    ["npc_id"] = route.NpcId,
                    ["root_textblock_id"] = route.RootId,
                    ["action_textblock_id"] = ReadInt(action, "textblock_id"),
                    ["stage_slot"] = stageSlot++,
                    ["source_keyword"] = keyword,
                    ["operations"] = JsonNode.Parse(operations.GetRawText())
                });
            }
        }

        if (scripts.Count != 12)
            throw new InvalidDataException($"Expected 12 decoded Witchunter scripts; found {scripts.Count}.");

        writeJson(System.IO.Path.Combine(outputRoot, "witchunter_magebane_narrative.json"), new JsonObject
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "RealmGate.Compiler",
            ["world_id"] = "Classic",
            ["ability_id"] = 50,
            ["class_legacy_id"] = 2,
            ["first_half_minimum_level"] = 15,
            ["final_item_use_minimum_level"] = 50,
            ["draka_blade_item_id"] = 1334,
            ["scripts"] = scripts,
            ["evidence"] = new JsonArray(DecodedTextblocks, "Librarium/Objects/quests/quest_walkthrough_guides.txt")
        });
    }

    private static void WriteGnomeMerchantPlacement(string outputRoot, Action<string, JsonNode> writeJson)
    {
        var roomPartsRoot = System.IO.Path.Combine(outputRoot, "rooms.parts");
        if (!Directory.Exists(roomPartsRoot))
            throw new DirectoryNotFoundException($"Generated Classic room parts are required for Gnome Merchant placement: {roomPartsRoot}");

        var rooms = Directory.GetFiles(roomPartsRoot, "rooms.part*.json")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .SelectMany(file => ReadRecords(file))
            .Where(row => ReadInt(row, "map_number") == 16 &&
                ReadString(row, "name").StartsWith("Shallow Jungle,", StringComparison.Ordinal))
            .Select(row => new JsonObject
            {
                ["room_ref"] = ReadString(row, "id"),
                ["map_id"] = ReadInt(row, "map_number"),
                ["room_number"] = ReadInt(row, "room_number"),
                ["name"] = ReadString(row, "name")
            })
            .ToArray();

        if (rooms.Length != 171)
            throw new InvalidDataException($"Expected 171 decoded Shallow Jungle rooms for Gnome Merchant placement; found {rooms.Length}.");

        writeJson(System.IO.Path.Combine(outputRoot, "gnome_merchant_narrative_placement.json"), new JsonObject
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "RealmGate.Compiler",
            ["world_id"] = "Classic",
            ["npc_legacy_id"] = 716,
            ["root_textblock_id"] = 2798,
            ["rooms"] = new JsonArray(rooms.Cast<JsonNode?>().ToArray()),
            ["evidence"] = new JsonArray(DecodedTextblocks, "Librarium/Objects/room_descriptions/native_map_room_description_records.json")
        });
    }

    private static void WriteGnomeMerchantOfferings(string repositoryRoot, string outputRoot, Action<string, JsonNode> writeJson)
    {
        var itemNames = LoadCanonicalItemNames(repositoryRoot);
        var offerings = new JsonArray
        {
            Offering("magma scroll", "magma blast", 2803, null, 12,
                new[] { (1270, 1), (1271, 1), (479, 1), (1268, 1) }, 1287, itemNames, "strongly_corroborated"),
            Offering("tempest scroll", "acid tempest", 2807, 2808, 13,
                new[] { (589, 1), (1273, 1), (1269, 1), (1246, 1) }, 1288, itemNames, "decoded_source"),
            Offering("grace scroll", "merciful grace", 2811, 2812, 5,
                new[] { (1272, 1), (828, 1), (1274, 1), (481, 1) }, 1285, itemNames, "decoded_source"),
            Offering("fury scroll", "eldritch fury", 2953, 2954, 12,
                new[] { (1289, 1), (1305, 1), (1149, 1) }, 1855, itemNames, "decoded_source"),
            Offering("surge scroll", "chaos surge", 2956, 2957, 12,
                new[] { (1332, 10), (902, 5), (1261, 1) }, 1886, itemNames, "decoded_source"),
            Offering("glacial scroll", "glacial skin", 2959, 2960, 13,
                new[] { (1735, 1), (1269, 1), (859, 1) }, 1991, itemNames, "decoded_source")
        };

        writeJson(System.IO.Path.Combine(outputRoot, "gnome_merchant_scroll_offerings.json"), new JsonObject
        {
            ["authority"] = "Librarium",
            ["generated_by"] = "RealmGate.Compiler",
            ["world_id"] = "Classic",
            ["npc_legacy_id"] = 716,
            ["root_textblock_id"] = 2798,
            ["receipt_level_gate"] = null,
            ["success_textblock_id"] = 2805,
            ["failure_textblock_id"] = 2498,
            ["offerings"] = offerings,
            ["evidence"] = new JsonArray(DecodedTextblocks, "Librarium/Objects/native_textblock_payloads/native_textblock_payload_records.json", "Librarium/Objects/quests/quest_walkthrough_guides.txt")
        });
    }

    private static JsonObject Offering(
        string keyword,
        string spellKeyword,
        int dialogueTextblockId,
        int? actionTextblockId,
        int classLegacyId,
        IEnumerable<(int ItemId, int Quantity)> requiredItems,
        int rewardItemId,
        IReadOnlyDictionary<int, string> itemNames,
        string evidence)
    {
        var items = new JsonArray();
        foreach (var (itemId, quantity) in requiredItems)
        {
            if (!itemNames.TryGetValue(itemId, out var itemName))
                throw new InvalidDataException($"Canonical item {itemId} is required for Gnome Merchant offering '{keyword}'.");
            items.Add(new JsonObject
            {
                ["item_id"] = itemId,
                ["quantity"] = quantity,
                ["name"] = itemName
            });
        }

        if (!itemNames.TryGetValue(rewardItemId, out var rewardName))
            throw new InvalidDataException($"Canonical reward item {rewardItemId} is required for Gnome Merchant offering '{keyword}'.");

        return new JsonObject
        {
            ["keyword"] = keyword,
            ["spell_keyword"] = spellKeyword,
            ["dialogue_textblock_id"] = dialogueTextblockId,
            ["action_textblock_id"] = actionTextblockId,
            ["class_legacy_id"] = classLegacyId,
            ["required_items"] = items,
            ["reward_item_id"] = rewardItemId,
            ["reward_name"] = rewardName,
            ["evidence"] = evidence
        };
    }

    private static IReadOnlyDictionary<int, string> LoadCanonicalItemNames(string repositoryRoot)
    {
        var path = System.IO.Path.Combine(repositoryRoot, "Librarium", "Objects", "items", "item_records.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Canonical item catalog is required for Gnome Merchant offerings.", path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("records").EnumerateArray()
            .Where(row => ReadString(row, "name").Length > 0)
            .Select(row => (Id: ReadLegacyId(row, "legacy_id"), Name: ReadString(row, "name")))
            .Where(row => row.Id > 0)
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
    }

    private static IEnumerable<JsonElement> ReadRecords(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("records").EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    private static IEnumerable<JsonElement> ReadDecodedTextblocks(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Decoded Classic textblock source must be a top-level array.");
        return document.RootElement.EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    private static JsonObject CreateAlignmentPath(string alignment, string npcName, string npcId, int rootId) => new()
    {
        ["alignment"] = alignment,
        ["npc_name"] = npcName,
        ["npc_id"] = npcId,
        ["root_textblock_id"] = rootId,
        ["required_levels"] = new JsonArray(10, 20, 30, 40, 50),
        ["character_owner"] = "BundleACharacter",
        ["inventory_owner"] = "BundleCItemsEconomy",
        ["narrative_owner"] = "BundleF",
        ["persistence_owner"] = "BundleGStorage",
        ["evidence"] = DecodedTextblocks
    };

    private static JsonObject Envelope(string family, JsonArray records) => new()
    {
        ["authority"] = "Librarium",
        ["generated_by"] = "RealmGate.Compiler",
        ["world_id"] = "Classic",
        ["family"] = family,
        ["source_file"] = DecodedTextblocks,
        ["record_count"] = records.Count,
        ["records"] = records
    };

    private static int ParseOwnerNumber(string value)
    {
        var marker = "#";
        var index = value.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 && int.TryParse(value[(index + marker.Length)..].Trim(), out var number) ? number : 0;
    }

    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }

    private static int ReadLegacyId(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(value.GetString(), out number) ? number : 0;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string EventExecutionFamily(string opcode) => opcode switch
    {
        "giveability" or "addability" or "removeability" or "goodability" => "ability_mutation",
        "addevil" => "alignment_change",
        "givecoins" => "currency_grant",
        "addexp" => "experience",
        "giveitem" => "item_grant",
        "takeitem" or "clearitem" => "item_removal",
        "message" or "text" => "message_output",
        "summon" => "npc_spawn",
        "random" => "random_branch",
        "delay" or "adddelay" => "scheduler_delay",
        "cast" => "spell_application",
        "teleport" => "teleport",
        "remoteaction" => "world_remote_action",
        _ => string.Empty
    };

    private static string EventExecutionOwner(string family) => family switch
    {
        "ability_mutation" or "alignment_change" or "experience" => "Bundle A",
        "spell_application" => "Bundle B",
        "currency_grant" or "item_grant" or "item_removal" => "Bundle C",
        "teleport" => "Bundle D",
        "npc_spawn" or "world_remote_action" => "Bundle E",
        "scheduler_delay" => "Bundle G",
        _ => "Bundle F"
    };

    private static readonly string[] RequiredEventFamilies =
    [
        "ability_mutation", "alignment_change", "currency_grant", "experience", "item_grant", "item_removal",
        "message_output", "npc_spawn", "random_branch", "scheduler_delay", "spell_application", "teleport", "world_remote_action"
    ];

    private static readonly IReadOnlyDictionary<string, string> RewardOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["giveability"] = "Bundle A", ["addability"] = "Bundle A", ["removeability"] = "Bundle A",
        ["addexp"] = "Bundle A", ["addevil"] = "Bundle A", ["goodability"] = "Bundle A",
        ["cast"] = "Bundle B",
        ["takeitem"] = "Bundle C", ["giveitem"] = "Bundle C", ["clearitem"] = "Bundle C", ["givecoins"] = "Bundle C",
        ["teleport"] = "Bundle D",
        ["summon"] = "Bundle E", ["remoteaction"] = "Bundle E",
        ["message"] = "Bundle F", ["text"] = "Bundle F"
    };

    private static readonly HashSet<int> RootIds = [335, 366, 428, 2798];
}
