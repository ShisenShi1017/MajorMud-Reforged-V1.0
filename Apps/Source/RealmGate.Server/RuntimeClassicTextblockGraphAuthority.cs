using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicTextblockCommand(string Keyword, string NormalizedKeyword, int TargetTextblockId);
public sealed record RuntimeClassicTextblockTrigger(int Line, string Raw, string Normalized, string TriggerKind, string Verb, IReadOnlyList<string> Arguments);
public sealed record RuntimeClassicTextblockOperation(int Line, int Sequence, string Raw, string Opcode, string OperationKind, string Status, IReadOnlyList<string> Arguments, IReadOnlyList<int> NumericArguments, int? TargetTextblockId, int? ItemId, int? SpellId, int? AbilityId, int? NpcId, int? RoomNumber, int? MapNumber);
public sealed record RuntimeClassicTextblockNode(int TextblockId, int NextTextblockId, string OwnerKind, string OwnerRaw, string ContentKind, string Script, IReadOnlyList<RuntimeClassicTextblockCommand> Commands, IReadOnlyList<RuntimeClassicTextblockTrigger> Triggers, IReadOnlyList<RuntimeClassicTextblockOperation> Operations, string EvidenceState);
public sealed record RuntimeClassicTextblockExecutionPlan(int RootTextblockId, int MatchedTextblockId, int MatchedLine, string MatchedInput, IReadOnlyList<RuntimeClassicTextblockOperation> Operations, IReadOnlyList<int> MessageTextblockIds, int? ContinuationTextblockId);
public sealed record RuntimeClassicRoomActionCommand(int TextblockId, string Command);

/// <summary>Single decoded Classic textblock graph projection and execution-plan authority. It preserves decoded ordering and delegates mutations to Bundle A-E consumers.</summary>
public sealed class RuntimeClassicTextblockGraphAuthority
{
    private readonly IReadOnlyDictionary<int, RuntimeClassicTextblockNode> _nodes;
    private RuntimeClassicTextblockGraphAuthority(IReadOnlyDictionary<int, RuntimeClassicTextblockNode> nodes) => _nodes = nodes;

    public static RuntimeClassicTextblockGraphAuthority Load(string projectRoot) => Load(projectRoot, "Classic");

    public static RuntimeClassicTextblockGraphAuthority Load(string projectRoot, string worldId)
    {
        var resolved = ServerDataRootResolver.Resolve(projectRoot, worldId);
        var path = Path.Combine(resolved.DataRoot, "textblocks.json");
        if (!File.Exists(path)) throw new FileNotFoundException($"Generated world '{resolved.WorldId}' textblock graph is required.", path);
        var records = RuntimeChunkedClassicCatalog.LoadRecords(path);
        var nodes = new Dictionary<int, RuntimeClassicTextblockNode>();
        foreach (var row in records)
        {
            var id = Int(row, "textblock_id");
            if (id <= 0) continue;
            if (nodes.ContainsKey(id)) throw new InvalidDataException($"Duplicate decoded textblock id {id}.");
            var commands = new List<RuntimeClassicTextblockCommand>();
            if (row.TryGetProperty("commands", out var commandRows) && commandRows.ValueKind == JsonValueKind.Array)
                foreach (var command in commandRows.EnumerateArray())
                {
                    var keyword = Text(command, "keyword");
                    var normalized = NormalizeCommand(Text(command, "normalized_keyword").Length > 0 ? Text(command, "normalized_keyword") : keyword);
                    var target = Int(command, "target_textblock_id");
                    if (keyword.Length > 0 && target > 0) commands.Add(new(keyword, normalized, target));
                }
            var triggers = new List<RuntimeClassicTextblockTrigger>();
            if (row.TryGetProperty("triggers", out var triggerRows) && triggerRows.ValueKind == JsonValueKind.Array)
                foreach (var trigger in triggerRows.EnumerateArray())
                    triggers.Add(new(Int(trigger, "line"), Text(trigger, "raw"), NormalizeCommand(Text(trigger, "normalized")), Text(trigger, "trigger_kind"), NormalizeCommand(Text(trigger, "verb")), Strings(trigger, "arguments")));
            var operations = new List<RuntimeClassicTextblockOperation>();
            if (row.TryGetProperty("operations", out var operationRows) && operationRows.ValueKind == JsonValueKind.Array)
                foreach (var operation in operationRows.EnumerateArray())
                    operations.Add(new(Int(operation, "line"), Int(operation, "sequence"), Text(operation, "raw"), Text(operation, "opcode").ToLowerInvariant(), Text(operation, "operation_kind"), Text(operation, "status"), Strings(operation, "arguments"), Ints(operation, "numeric_arguments"), NullableInt(operation, "target_textblock_id"), NullableInt(operation, "item_id"), NullableInt(operation, "spell_id"), NullableInt(operation, "ability_id"), NullableInt(operation, "npc_id"), NullableInt(operation, "room_number"), NullableInt(operation, "map_number")));
            var ownerKind = row.TryGetProperty("owner", out var owner) ? Text(owner, "kind") : string.Empty;
            nodes[id] = new(id, NullableInt(row, "next_textblock_id") ?? 0, ownerKind, Text(row, "owner_raw"), Text(row, "content_kind"), RawText(row, "script"), commands, triggers, operations.OrderBy(x => x.Line).ThenBy(x => x.Sequence).ToArray(), Text(row, "evidence_state"));
        }
        if (nodes.Count == 0) throw new InvalidDataException("Decoded textblock graph contains no records.");
        return new(nodes);
    }

    public int Count => _nodes.Count;
    public RuntimeClassicTextblockNode Require(int textblockId) => _nodes.TryGetValue(textblockId, out var node) ? node : throw new KeyNotFoundException($"Decoded textblock {textblockId} was not found.");
    public bool TryGet(int textblockId, out RuntimeClassicTextblockNode node) => _nodes.TryGetValue(textblockId, out node!);

    public IReadOnlyList<(int MapNumber, int RoomNumber)> AllRoomActionRooms() => _nodes.Values
        .Where(node => node.OwnerKind.Equals("room", StringComparison.OrdinalIgnoreCase)
            && node.ContentKind.Equals("action_script", StringComparison.OrdinalIgnoreCase))
        .SelectMany(node => node.OwnerRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(owner => owner.StartsWith("Room ", StringComparison.OrdinalIgnoreCase))
        .Select(owner => owner[5..].Split('/'))
        .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
        .Select(parts => (MapNumber: int.Parse(parts[0]), RoomNumber: int.Parse(parts[1])))
        .Distinct()
        .OrderBy(room => room.MapNumber).ThenBy(room => room.RoomNumber)
        .ToArray();

    public bool TryResolveCommand(int rootTextblockId, string input, out RuntimeClassicTextblockCommand command)
    {
        command = null!;
        if (!_nodes.TryGetValue(rootTextblockId, out var root)) return false;
        var normalized = NormalizeCommand(input);
        command = root.Commands.Where(x => normalized.Equals(x.NormalizedKeyword, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.NormalizedKeyword.Length).FirstOrDefault()!;
        return command is not null;
    }

    public bool TryResolveRoomActionPlan(int mapNumber, int roomNumber, string input, out RuntimeClassicTextblockExecutionPlan plan)
    {
        var plans = ResolveRoomActionPlans(mapNumber, roomNumber, input);
        plan = plans.FirstOrDefault()!;
        return plan is not null;
    }

    public IReadOnlyList<RuntimeClassicRoomActionCommand> GetRoomActionCommands(int mapNumber, int roomNumber)
    {
        var ownerToken = $"Room {mapNumber}/{roomNumber}";
        return _nodes.Values
            .Where(node => node.OwnerKind.Equals("room", StringComparison.OrdinalIgnoreCase)
                && node.ContentKind.Equals("action_script", StringComparison.OrdinalIgnoreCase)
                && RoomOwnerContains(node.OwnerRaw, ownerToken))
            .SelectMany(node => node.Triggers.Select(trigger => new RuntimeClassicRoomActionCommand(node.TextblockId, trigger.Raw))
                .Concat(node.Operations.Where(operation => operation.Sequence == 1
                        && operation.Opcode.Equals("summon", StringComparison.OrdinalIgnoreCase)
                        && operation.NumericArguments.Count == 0)
                    .Select(operation => new RuntimeClassicRoomActionCommand(node.TextblockId, operation.Raw))))
            .Where(command => !string.IsNullOrWhiteSpace(command.Command))
            .DistinctBy(command => (command.TextblockId, NormalizeCommand(command.Command)))
            .OrderBy(command => command.Command, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Returns every exact line-level action plan for a room phrase in authored order. Classic
    /// action scripts may repeat the same player phrase on multiple lines to express alternative
    /// condition branches (the gang-house rubbish rooms use two accepted credentials). The older
    /// single-plan projection could silently discard those later alternatives.
    /// </summary>
    public IReadOnlyList<RuntimeClassicTextblockExecutionPlan> ResolveRoomActionPlans(int mapNumber, int roomNumber, string input)
    {
        var normalized = NormalizeCommand(input);
        var ownerToken = $"Room {mapNumber}/{roomNumber}";
        var roomNodes = _nodes.Values
            .Where(node => node.OwnerKind.Equals("room", StringComparison.OrdinalIgnoreCase)
                && node.ContentKind.Equals("action_script", StringComparison.OrdinalIgnoreCase)
                && RoomOwnerContains(node.OwnerRaw, ownerToken))
            .OrderBy(node => node.TextblockId)
            .ToArray();

        var explicitMatches = roomNodes
            .SelectMany(node => node.Triggers
                .Where(trigger => trigger.Normalized.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderBy(trigger => trigger.Line)
                .Select(trigger => (Node: node, Trigger: trigger)))
            .ToArray();
        if (explicitMatches.Length > 0)
        {
            var nodeIds = explicitMatches.Select(match => match.Node.TextblockId).Distinct().ToArray();
            if (nodeIds.Length > 1)
                throw new InvalidOperationException($"Decoded room command '{normalized}' is ambiguous in room {mapNumber}/{roomNumber}: {string.Join(", ", nodeIds)}.");
            return explicitMatches.Select(match => CreateLinePlan(match.Node, match.Trigger.Line, normalized)).ToArray();
        }

        // Preserve the pre-0344 verb-prefix projection for unrelated room scripts. The gang-house
        // service family is deliberately exact-phrase only because every authored BUY/MAKE/PULL
        // variant is independently decoded and broad verb matching can execute the wrong priced line.
        var legacyPrefixMatches = roomNodes
            .Where(node => node.TextblockId is < 1602 or > 1642)
            .SelectMany(node => node.Triggers
                .Where(trigger => TriggerMatches(trigger, normalized))
                .OrderByDescending(trigger => trigger.Normalized.Length)
                .ThenBy(trigger => trigger.Line)
                .Take(1)
                .Select(trigger => (Node: node, Trigger: trigger)))
            .ToArray();
        if (legacyPrefixMatches.Length > 0)
        {
            var nodeIds = legacyPrefixMatches.Select(match => match.Node.TextblockId).Distinct().ToArray();
            if (nodeIds.Length > 1)
                throw new InvalidOperationException($"Decoded room command '{normalized}' is ambiguous in room {mapNumber}/{roomNumber}: {string.Join(", ", nodeIds)}.");
            var match = legacyPrefixMatches[0];
            return [CreateLinePlan(match.Node, match.Trigger.Line, normalized)];
        }

        // MMExplorer's decoder treats lines such as "summon healer:..." and "summon guard:..."
        // as an opcode because SUMMON is also a valid textblock operation. In the original action
        // script grammar those string-valued sequence-1 forms are player command triggers; a
        // numeric SUMMON operation remains an effect. Recover that ambiguity without generalizing
        // arbitrary string-valued opcodes into commands.
        var implicitMatches = roomNodes
            .SelectMany(node => node.Operations
                .Where(operation => operation.Sequence == 1
                    && operation.Opcode.Equals("summon", StringComparison.OrdinalIgnoreCase)
                    && operation.NumericArguments.Count == 0
                    && NormalizeCommand(operation.Raw).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderBy(operation => operation.Line)
                .Select(operation => (Node: node, Operation: operation)))
            .ToArray();
        if (implicitMatches.Length == 0) return Array.Empty<RuntimeClassicTextblockExecutionPlan>();
        var implicitNodeIds = implicitMatches.Select(match => match.Node.TextblockId).Distinct().ToArray();
        if (implicitNodeIds.Length > 1)
            throw new InvalidOperationException($"Decoded implicit room command '{normalized}' is ambiguous in room {mapNumber}/{roomNumber}: {string.Join(", ", implicitNodeIds)}.");
        return implicitMatches.Select(match => CreateLinePlan(match.Node, match.Operation.Line, normalized, match.Operation.Sequence)).ToArray();
    }

    private static RuntimeClassicTextblockExecutionPlan CreateLinePlan(
        RuntimeClassicTextblockNode node,
        int line,
        string normalized,
        int skipThroughSequence = 1)
    {
        var operations = node.Operations
            .Where(operation => operation.Line == line && operation.Sequence > skipThroughSequence)
            .OrderBy(operation => operation.Sequence)
            .ToArray();
        var messages = operations
            .Where(operation => operation.Opcode is "message" or "text" or "route" or "random")
            .Select(operation => operation.TargetTextblockId ?? 0)
            .Where(id => id > 0)
            .ToArray();
        var continuation = messages.LastOrDefault();
        if (continuation == 0) continuation = node.NextTextblockId;
        return new(node.TextblockId, node.TextblockId, line, normalized,
            operations, messages, continuation > 0 ? continuation : null);
    }

    private static bool RoomOwnerContains(string ownerRaw, string ownerToken)
    {
        foreach (var part in ownerRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (part.Equals(ownerToken, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool TryCreateExecutionPlan(int rootTextblockId, string input, out RuntimeClassicTextblockExecutionPlan plan)
    {
        plan = null!;
        if (!_nodes.TryGetValue(rootTextblockId, out var root)) return false;
        var normalized = NormalizeCommand(input);
        RuntimeClassicTextblockNode node = root;
        if (root.Commands.Count > 0)
        {
            if (!TryResolveCommand(rootTextblockId, input, out var command) || !_nodes.TryGetValue(command.TargetTextblockId, out node!)) return false;
        }
        var matchingTrigger = node.Triggers.Where(x => TriggerMatches(x, normalized)).OrderByDescending(x => x.Normalized.Length).ThenBy(x => x.Line).FirstOrDefault();
        var line = matchingTrigger?.Line ?? (node.Operations.Select(x => x.Line).DefaultIfEmpty(0).Min());
        var operations = node.Operations.Where(x => line == 0 || x.Line == line).OrderBy(x => x.Sequence).ToArray();
        var messages = operations.Where(x => x.Opcode is "message" or "text" or "route" or "random").Select(x => x.TargetTextblockId ?? 0).Where(x => x > 0).ToArray();
        var continuation = messages.LastOrDefault();
        if (continuation == 0) continuation = node.NextTextblockId;
        plan = new(rootTextblockId, node.TextblockId, line, normalized, operations, messages, continuation > 0 ? continuation : null);
        return true;
    }

    public IReadOnlyList<int> ReachableDecodedNodes(int rootTextblockId)
    {
        var seen = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(rootTextblockId);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue(); if (!seen.Add(id) || !_nodes.TryGetValue(id, out var node)) continue;
            if (node.NextTextblockId > 0) queue.Enqueue(node.NextTextblockId);
            foreach (var target in node.Commands.Select(x => x.TargetTextblockId)) queue.Enqueue(target);
            foreach (var target in node.Operations.Where(ShouldTraverseOperationTarget).Select(x => x.TargetTextblockId ?? 0).Where(x => x > 0)) queue.Enqueue(target);
        }
        return seen.OrderBy(x => x).ToArray();
    }

    public IReadOnlyList<string> MissingTextblockReferences(int rootTextblockId)
    {
        var failures = new List<string>();
        foreach (var id in ReachableDecodedNodes(rootTextblockId))
        {
            var node = Require(id);
            if (node.NextTextblockId > 0 && !_nodes.ContainsKey(node.NextTextblockId)) failures.Add($"{id}:next:{node.NextTextblockId}");
            failures.AddRange(node.Commands.Where(x => !_nodes.ContainsKey(x.TargetTextblockId)).Select(x => $"{id}:command:{x.TargetTextblockId}"));
            failures.AddRange(node.Operations.Where(x => ShouldTraverseOperationTarget(x) && x.TargetTextblockId is > 0 && !_nodes.ContainsKey(x.TargetTextblockId.Value)).Select(x => $"{id}:operation:{x.TargetTextblockId}"));
        }
        return failures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static bool ShouldTraverseOperationTarget(RuntimeClassicTextblockOperation operation) =>
        operation.TargetTextblockId is > 0 && operation.Opcode is not ("message" or "text");

    private static bool TriggerMatches(RuntimeClassicTextblockTrigger trigger, string normalized)
    {
        if (trigger.TriggerKind.Equals("numeric_selection", StringComparison.OrdinalIgnoreCase)) return trigger.Normalized == normalized;
        return trigger.Normalized == normalized || (trigger.Verb.Length > 0 && normalized.StartsWith(trigger.Verb + " ", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCommand(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string RawText(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static string Text(JsonElement row, string key) => RawText(row, key).Trim();
    private static int Int(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    private static int? NullableInt(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    private static IReadOnlyList<string> Strings(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(x => x.ToString()).ToArray() : Array.Empty<string>();
    private static IReadOnlyList<int> Ints(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _)).Select(x => x.GetInt32()).ToArray() : Array.Empty<int>();
}

public sealed record RuntimeBundleFPhaseF2TextblockAuditReport(bool Passed, int DecodedTextblockCount, int KnownRootCount, int CommandCount, int TriggerCount, int OperationCount, int VerifiedDisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF2TextblockAudit
{
    public static RuntimeBundleFPhaseF2TextblockAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var roots = new[] { 335, 366, 428, 2798 };
        var commands = 0; var triggers = 0; var operations = 0;
        foreach (var rootId in roots)
        {
            var root = graph.Require(rootId); commands += root.Commands.Count;
            var reachable = graph.ReachableDecodedNodes(rootId);
            triggers += reachable.Select(graph.Require).Sum(x => x.Triggers.Count);
            operations += reachable.Select(graph.Require).Sum(x => x.Operations.Count);
            Check(root.OwnerKind.Equals("monster", StringComparison.OrdinalIgnoreCase), $"root-{rootId}-npc-owned");
            Check(root.ContentKind.Equals("command_map", StringComparison.OrdinalIgnoreCase), $"root-{rootId}-command-map");
            Check(root.Commands.Count > 0, $"root-{rootId}-commands-decoded");
            Check(reachable.Count > 1, $"root-{rootId}-full-graph-reachable");
        }
        foreach (var triggerId in new[] { 159, 181 })
        {
            var node = graph.Require(triggerId);
            triggers += node.Triggers.Count;
            operations += node.Operations.Count;
            Check(node.Triggers.Count > 0, $"trigger-node-{triggerId}-triggers-decoded");
        }
        Check(graph.TryCreateExecutionPlan(335, "Commander Markus", out var good) && good.MatchedTextblockId == 352, "good-root-execution-plan");
        Check(graph.TryCreateExecutionPlan(366, "CODE WORD", out var neutral) && neutral.MatchedTextblockId == 368, "neutral-root-execution-plan");
        Check(graph.TryCreateExecutionPlan(428, "work", out var evil) && evil.MatchedTextblockId == 430, "evil-root-execution-plan");
        Check(graph.TryCreateExecutionPlan(2798, "magma scroll", out var gnome) && gnome.MatchedTextblockId == 2803, "jungle-gnome-execution-plan");
        Check(graph.Count == 2279, "decoded-textblock-catalog-complete");
        Check(triggers > 0, "reachable-triggers-projected");
        Check(operations > 0, "reachable-operations-projected");
        return new(failures.Count == 0, graph.Count, roots.Length, commands, triggers, operations, failures.Count, checks, failures);
    }
}
