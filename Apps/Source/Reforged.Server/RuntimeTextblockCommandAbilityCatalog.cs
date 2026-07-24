using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeTextblockCommandAbilityContractSummary(
    int TextblockScriptCount,
    int TextblockOperationCount,
    int CommandRegistrationCount,
    int AbilityCatalogCount,
    int ReferencedAbilityCount);

public sealed class RuntimeTextblockCommandAbilityCatalog
{
    private readonly IReadOnlyDictionary<string, JsonElement> _records;

    private RuntimeTextblockCommandAbilityCatalog(IReadOnlyDictionary<string, JsonElement> records)
    {
        _records = records;
    }

    public static RuntimeTextblockCommandAbilityCatalog Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "textblock_command_ability_contracts.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated textblock/command/ability contract catalog was not found.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (ReadText(root, "authority") != "Librarium") throw new InvalidOperationException("Textblock/command/ability contracts are not Librarium-authoritative.");
        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Textblock/command/ability contract records are missing.");

        var byId = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.EnumerateArray())
        {
            var id = ReadText(record, "id");
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("contract.", StringComparison.Ordinal))
                throw new InvalidOperationException($"Invalid semantic contract id '{id}'.");
            if (!byId.TryAdd(id, record.Clone())) throw new InvalidOperationException($"Duplicate semantic contract id '{id}'.");
        }

        foreach (var required in new[] { "contract.textblock.quest_stage", "contract.command.runtime_registry", "contract.command.parser_dispatch", "contract.textblock.opcode_execution", "contract.ability.textblock_lifecycle", "contract.ability.ownership_lifecycle", "contract.textblock.output_reference", "contract.textblock.invocation", "contract.command.execution_outcome", "contract.ability.domain_boundary", "contract.semantic.closure_boundary", "contract.semantic.downstream_integration", "contract.semantic.native_dependency_quarantine" })
            if (!byId.ContainsKey(required)) throw new InvalidOperationException($"Required semantic contract '{required}' is missing.");

        ValidateCommands(byId["contract.command.runtime_registry"]);
        ValidateCommandParserDispatch(byId["contract.command.parser_dispatch"]);
        ValidateTextblocks(byId["contract.textblock.quest_stage"]);
        ValidateTextblockOpcodeExecution(byId["contract.textblock.opcode_execution"]);
        ValidateAbilities(byId["contract.ability.textblock_lifecycle"]);
        ValidateAbilityOwnershipLifecycle(byId["contract.ability.ownership_lifecycle"]);
        ValidateAbilityDomainBoundary(byId["contract.ability.domain_boundary"]);
        ValidateTextblockOutputReference(byId["contract.textblock.output_reference"]);
        ValidateTextblockInvocation(byId["contract.textblock.invocation"]);
        ValidateCommandExecutionOutcome(byId["contract.command.execution_outcome"]);
        RuntimeSemanticClosureBoundary.Validate(byId["contract.semantic.closure_boundary"]);
        RuntimeSemanticDownstreamIntegration.Validate(byId["contract.semantic.downstream_integration"]);
        RuntimeNativeDependencyQuarantine.Validate(byId["contract.semantic.native_dependency_quarantine"]);
        return new RuntimeTextblockCommandAbilityCatalog(byId);
    }

    public RuntimeTextblockCommandAbilityContractSummary Summary()
    {
        var textblocks = _records["contract.textblock.quest_stage"];
        var commands = _records["contract.command.runtime_registry"];
        var abilities = _records["contract.ability.textblock_lifecycle"];
        return new(
            ReadInt(textblocks, "script_count"),
            ReadInt(textblocks, "operation_count"),
            ReadInt(commands, "registration_count"),
            ReadInt(abilities, "catalog_count"),
            ReadInt(abilities, "referenced_count"));
    }

    private static void ValidateCommands(JsonElement contract)
    {
        if (!contract.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Runtime command registrations are missing.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands.EnumerateArray())
        {
            var name = ReadText(command, "name");
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name)) throw new InvalidOperationException($"Invalid or duplicate runtime command '{name}'.");
            var minimum = ReadInt(command, "minimum_prefix_length");
            if (minimum < 1 || minimum > name.Length) throw new InvalidOperationException($"Command '{name}' has invalid minimum prefix length {minimum}.");
        }
        if (names.Count != ReadInt(contract, "registration_count")) throw new InvalidOperationException("Runtime command registration count does not match contract records.");
    }


    private static void ValidateCommandParserDispatch(JsonElement contract)
    {
        if (!contract.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Command parser/dispatch records are missing.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands.EnumerateArray())
        {
            var name = ReadText(command, "name");
            var route = ReadText(command, "route");
            var argumentMode = ReadText(command, "argument_mode");
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                throw new InvalidOperationException($"Invalid or duplicate parser/dispatch command '{name}'.");
            if (string.IsNullOrWhiteSpace(route) || route == "unresolved")
                throw new InvalidOperationException($"Command '{name}' has no resolved dispatch route.");
            if (argumentMode is not ("ignored_by_dispatcher" or "joined_remaining_tokens" or "remaining_token_vector" or "opaque_runtime_command"))
                throw new InvalidOperationException($"Command '{name}' has invalid argument mode '{argumentMode}'.");
        }
        if (names.Count != ReadInt(contract, "registration_count"))
            throw new InvalidOperationException("Command parser/dispatch registration count does not match contract records.");
        if (!contract.TryGetProperty("parser", out var parser) || parser.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Command parser execution contract is missing.");
        if (ReadText(parser, "case_normalization") != "lower_invariant_command_token_only")
            throw new InvalidOperationException("Command parser case-normalization contract is invalid.");
        if (ReadText(contract, "unknown_policy") != "reject_without_gameplay_side_effects")
            throw new InvalidOperationException("Unknown command policy must reject without gameplay side effects.");
    }

    private static void ValidateTextblocks(JsonElement contract)
    {
        if (ReadInt(contract, "script_count") <= 0 || ReadInt(contract, "operation_count") <= 0)
            throw new InvalidOperationException("Textblock semantic contract has no executable corpus.");
        if (ReadText(contract, "unknown_opcode_policy") != "fail_closed")
            throw new InvalidOperationException("Textblock unknown opcode policy must fail closed.");
        if (ReadText(contract, "atomicity") != "condition_preflight_then_ordered_effects")
            throw new InvalidOperationException("Textblock atomic execution policy is not preserved.");
    }


    private static void ValidateTextblockOpcodeExecution(JsonElement contract)
    {
        if (ReadInt(contract, "opcode_count") != 25 || ReadInt(contract, "operation_count") != 2616)
            throw new InvalidOperationException("Textblock opcode execution contract counts are invalid.");
        if (ReadText(contract, "unknown_opcode_policy") != "fail_closed")
            throw new InvalidOperationException("Textblock opcode execution must fail closed for unknown opcodes.");
        if (!contract.TryGetProperty("opcodes", out var opcodes) || opcodes.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Textblock opcode execution records are missing.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        foreach (var opcode in opcodes.EnumerateArray())
        {
            var name = ReadText(opcode, "opcode");
            var phase = ReadText(opcode, "phase");
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                throw new InvalidOperationException($"Invalid or duplicate textblock opcode '{name}'.");
            if (phase is not ("condition" or "effect"))
                throw new InvalidOperationException($"Textblock opcode '{name}' has invalid phase '{phase}'.");
            if (!opcode.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"Textblock opcode '{name}' has no parameter schema.");
            if (!opcode.TryGetProperty("allowed_arities", out var arities) || arities.ValueKind != JsonValueKind.Array || arities.GetArrayLength() == 0)
                throw new InvalidOperationException($"Textblock opcode '{name}' has no allowed arities.");
            total += ReadInt(opcode, "count");
        }
        if (names.Count != ReadInt(contract, "opcode_count") || total != ReadInt(contract, "operation_count"))
            throw new InvalidOperationException("Textblock opcode execution aggregate counts do not match the contract.");
        if (!contract.TryGetProperty("clause_execution_order", out var order) || order.ValueKind != JsonValueKind.Array || order.GetArrayLength() != 8)
            throw new InvalidOperationException("Textblock clause execution order is incomplete.");
    }

    private static void ValidateTextblockInvocation(JsonElement contract)
    {
        if (ReadInt(contract, "invocation_count") != 389)
            throw new InvalidOperationException("Textblock invocation count is invalid.");
        if (ReadInt(contract, "resolved_executable_target_count") != 0 ||
            ReadInt(contract, "unresolved_payload_target_count") != 389)
            throw new InvalidOperationException("Textblock invocation resolution boundary is invalid.");
        if (ReadText(contract, "failure_policy") != "fail_closed_without_numeric_identifier_output_or_partial_commit")
            throw new InvalidOperationException("Textblock invocation failure policy is invalid.");
        if (ReadText(contract, "script_call_policy") != "text is a synchronous typed textblock invocation, not a generic command or arbitrary script dispatcher")
            throw new InvalidOperationException("Textblock invocation script-call policy is invalid.");
        if (!contract.TryGetProperty("execution_order", out var order) || order.ValueKind != JsonValueKind.Array || order.GetArrayLength() != 7)
            throw new InvalidOperationException("Textblock invocation execution order is incomplete.");
    }


    private static void ValidateCommandExecutionOutcome(JsonElement contract)
    {
        if (ReadInt(contract, "registration_count") != 77)
            throw new InvalidOperationException("Command execution outcome registration count is invalid.");
        if (ReadText(contract, "dispatcher_rollback_policy") != "no_general_dispatcher_rollback; handler/domain transaction owns mutation atomicity")
            throw new InvalidOperationException("Command dispatcher rollback ownership is invalid.");
        if (ReadText(contract, "exception_policy") != "publish_failed_event_and_return_exception_message_without_implicit_retry")
            throw new InvalidOperationException("Command exception policy is invalid.");
        if (!contract.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array || commands.GetArrayLength() != 77)
            throw new InvalidOperationException("Command execution outcome records are incomplete.");
        foreach (var command in commands.EnumerateArray())
        {
            if (string.IsNullOrWhiteSpace(ReadText(command, "name")) || string.IsNullOrWhiteSpace(ReadText(command, "mutation_class")))
                throw new InvalidOperationException("Command execution outcome record is invalid.");
        }
    }

    private static void ValidateAbilities(JsonElement contract)
    {
        if (ReadInt(contract, "catalog_count") <= 0) throw new InvalidOperationException("Ability catalog contract is empty.");
        if (!contract.TryGetProperty("grant_opcodes", out var grants) || grants.ValueKind != JsonValueKind.Array || grants.GetArrayLength() == 0)
            throw new InvalidOperationException("Ability grant opcode contract is missing.");
        if (!contract.TryGetProperty("check_opcodes", out var checks) || checks.ValueKind != JsonValueKind.Array || checks.GetArrayLength() == 0)
            throw new InvalidOperationException("Ability check opcode contract is missing.");
    }

    private static void ValidateAbilityDomainBoundary(JsonElement contract)
    {
        if (ReadInt(contract, "ability_modifier_catalog_count") != 188) throw new InvalidOperationException("Ability modifier domain count is invalid.");
        if (ReadInt(contract, "learned_skill_catalog_count") != 13) throw new InvalidOperationException("Learned skill domain count is invalid.");
        if (!contract.TryGetProperty("boundary_rules", out var rules) || rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() < 5)
            throw new InvalidOperationException("Ability domain boundary rules are incomplete.");
    }

    private static void ValidateAbilityOwnershipLifecycle(JsonElement contract)
    {
        if (ReadInt(contract, "catalog_count") != 188) throw new InvalidOperationException("Ability ownership/lifecycle catalog count is invalid.");
        if (ReadText(contract, "owner") != "character persisted ability map") throw new InvalidOperationException("Ability runtime owner is invalid.");
        if (ReadText(contract, "grant_semantics") != "replace" || ReadText(contract, "increment_semantics") != "add")
            throw new InvalidOperationException("Ability mutation semantics are invalid.");
        if (!contract.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array || records.GetArrayLength() != 188)
            throw new InvalidOperationException("Ability ownership/lifecycle records are incomplete.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.EnumerateArray())
        {
            var id = ReadText(record, "ability_id");
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("ability.", StringComparison.Ordinal) || !ids.Add(id))
                throw new InvalidOperationException($"Invalid or duplicate ability lifecycle id '{id}'.");
        }
    }

    private static void ValidateTextblockOutputReference(JsonElement contract)
    {
        if (ReadInt(contract, "reference_count") != 519 || ReadInt(contract, "message_reference_count") != 130 || ReadInt(contract, "textblock_reference_count") != 389)
            throw new InvalidOperationException("Textblock output reference counts are invalid.");
        if (ReadText(contract, "rollback_policy") != "output references are transactional and are discarded when any later effect in the selected clause fails")
            throw new InvalidOperationException("Textblock output rollback policy is invalid.");
        if (!contract.TryGetProperty("reference_kinds", out var kinds) || kinds.ValueKind != JsonValueKind.Array || kinds.GetArrayLength() != 2)
            throw new InvalidOperationException("Textblock output reference kinds are incomplete.");
        foreach (var kind in kinds.EnumerateArray())
            if (ReadText(kind, "resolution_status") != "native_dependency")
                throw new InvalidOperationException("Unproven text payload rendering cannot be promoted into runtime.");
    }

    private static string ReadText(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
