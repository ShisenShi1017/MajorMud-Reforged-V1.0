using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNativeTextblockPayload(int TextblockId, string Payload, long PageOffset, int PageIndex, string EvidenceState);
public sealed record RuntimeClassicNativeDialogueMessagePayload(
    int MessageId,
    long HeaderOffset,
    string PlayerPayload,
    string SecondaryPayload,
    string TertiaryPayload,
    int PhysicalOccurrenceCount,
    string EvidenceState);

public sealed class RuntimeClassicNativeTextblockPayloadAuthority
{
    private readonly IReadOnlyDictionary<int, RuntimeClassicNativeTextblockPayload> _records;
    private readonly IReadOnlySet<int> _verifiedNoOutputTextblocks;
    private readonly IReadOnlyDictionary<int, RuntimeClassicNativeDialogueMessagePayload> _messagePayloads;

    private RuntimeClassicNativeTextblockPayloadAuthority(
        IReadOnlyDictionary<int, RuntimeClassicNativeTextblockPayload> records,
        IReadOnlySet<int> verifiedNoOutputTextblocks,
        IReadOnlyDictionary<int, RuntimeClassicNativeDialogueMessagePayload> messagePayloads)
    {
        _records = records;
        _verifiedNoOutputTextblocks = verifiedNoOutputTextblocks;
        _messagePayloads = messagePayloads;
    }

    public int Count => _records.Count;
    public int VerifiedNoOutputCount => _verifiedNoOutputTextblocks.Count;
    public int NativeMessageCount => _messagePayloads.Count;
    public IReadOnlyCollection<int> TextblockIds => _records.Keys.ToArray();

    public static RuntimeClassicNativeTextblockPayloadAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "native_textblock_payloads.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Classic WCCTEXT2 payload catalog is missing.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("WCCTEXT2 payload catalog has no records array.");
        var map = new Dictionary<int, RuntimeClassicNativeTextblockPayload>();
        foreach (var row in records.EnumerateArray())
        {
            var id = row.GetProperty("textblock_id").GetInt32();
            var payload = row.GetProperty("payload").GetString() ?? string.Empty;
            var pageOffset = row.GetProperty("page_offset").GetInt64();
            var pageIndex = row.GetProperty("page_index").GetInt32();
            var evidence = row.GetProperty("evidence_state").GetString() ?? string.Empty;
            if (id <= 0 || string.IsNullOrEmpty(payload) || !evidence.Equals("verified_native_runtime", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Invalid WCCTEXT2 payload record {id}.");
            if (!map.TryAdd(id, new(id, payload, pageOffset, pageIndex, evidence)))
                throw new InvalidOperationException($"Duplicate WCCTEXT2 payload record {id}.");
        }
        if (map.Count != 3467) throw new InvalidOperationException($"Expected 3467 WCCTEXT2 payloads, found {map.Count}.");

        var noOutput = LoadVerifiedNoOutputTextblocks(projectRoot);
        var messages = LoadNativeDialogueMessages(projectRoot);
        return new(map, noOutput, messages);
    }

    private static IReadOnlySet<int> LoadVerifiedNoOutputTextblocks(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "native_presentation.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Classic native presentation mapping is missing.", path);
        var rows = RuntimeChunkedClassicCatalog.LoadRecords(path);
        var ids = new HashSet<int>();
        foreach (var row in rows)
        {
            var state = row.GetProperty("runtime_state").GetString() ?? string.Empty;
            var namespaceName = row.GetProperty("namespace").GetString() ?? string.Empty;
            var opcode = row.GetProperty("opcode").GetString() ?? string.Empty;
            if (!state.Equals("executable_no_output", StringComparison.OrdinalIgnoreCase)) continue;
            if (!opcode.Equals("text", StringComparison.OrdinalIgnoreCase) ||
                !namespaceName.Equals("decoded_textblock_graph_empty_dialogue_node", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Only decoded empty dialogue text nodes may be executable no-output targets.");
            var target = row.GetProperty("target_id").GetInt32();
            if (target <= 0) throw new InvalidDataException("Verified no-output target is non-positive.");
            ids.Add(target);
        }
        if (ids.Count != 124) throw new InvalidDataException($"Expected 124 distinct verified no-output dialogue nodes, found {ids.Count}.");
        return ids;
    }


    private static IReadOnlyDictionary<int, RuntimeClassicNativeDialogueMessagePayload> LoadNativeDialogueMessages(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "native_message_payloads.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Classic WCCMSG2 payload catalog is missing.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("WCCMSG2 payload catalog has no records array.");
        var map = new Dictionary<int, RuntimeClassicNativeDialogueMessagePayload>();
        foreach (var row in rows.EnumerateArray())
        {
            var id = row.GetProperty("message_id").GetInt32();
            var headerOffset = row.GetProperty("header_offset").GetInt64();
            var playerPayload = row.GetProperty("player_payload").GetString() ?? string.Empty;
            var secondaryPayload = row.GetProperty("secondary_payload").GetString() ?? string.Empty;
            var tertiaryPayload = row.GetProperty("tertiary_payload").GetString() ?? string.Empty;
            var slotSize = row.GetProperty("slot_size").GetInt32();
            var occurrenceCount = row.GetProperty("physical_occurrence_count").GetInt32();
            var evidence = row.GetProperty("evidence_state").GetString() ?? string.Empty;
            if (id <= 0 || headerOffset < 0 || slotSize != 82 || occurrenceCount <= 0 ||
                !evidence.Equals("verified_native_runtime", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Invalid WCCMSG2 payload mapping {id}.");
            if (!map.TryAdd(id, new(id, headerOffset, playerPayload, secondaryPayload, tertiaryPayload, occurrenceCount, evidence)))
                throw new InvalidDataException($"Duplicate WCCMSG2 payload mapping {id}.");
        }
        if (map.Count != 3869)
            throw new InvalidDataException($"Expected 3869 logical WCCMSG2 payloads, found {map.Count}.");
        return map;
    }

    public bool TryResolveTextblock(int textblockId, out string payload)
    {
        if (_records.TryGetValue(textblockId, out var record)) { payload = record.Payload; return true; }
        if (_verifiedNoOutputTextblocks.Contains(textblockId)) { payload = string.Empty; return true; }
        payload = string.Empty; return false;
    }

    public bool TryResolveMessage(int messageId, out string payload)
    {
        if (_messagePayloads.TryGetValue(messageId, out var message)) { payload = message.PlayerPayload; return true; }
        payload = string.Empty; return false;
    }

    public bool TryResolveSecondaryMessage(int messageId, out string payload)
    {
        if (_messagePayloads.TryGetValue(messageId, out var message)) { payload = message.SecondaryPayload; return true; }
        payload = string.Empty; return false;
    }

    public bool TryResolveTertiaryMessage(int messageId, out string payload)
    {
        if (_messagePayloads.TryGetValue(messageId, out var message)) { payload = message.TertiaryPayload; return true; }
        payload = string.Empty; return false;
    }

    public bool TryResolve(int textblockId, out string payload) => TryResolveTextblock(textblockId, out payload);

    public bool IsVerifiedNoOutput(int textblockId) => _verifiedNoOutputTextblocks.Contains(textblockId);
    public bool IsVerifiedNoOutputMessage(int messageId) =>
        _messagePayloads.TryGetValue(messageId, out var message) && string.IsNullOrEmpty(message.PlayerPayload);

    public string RequireTextblock(int textblockId) => TryResolveTextblock(textblockId, out var payload)
        ? payload : throw new InvalidOperationException($"Native WCCTEXT2 payload {textblockId} is unresolved.");

    public string RequireMessage(int messageId) => TryResolveMessage(messageId, out var payload)
        ? payload : throw new InvalidOperationException($"Native WCCMSG2 payload {messageId} is unresolved.");

    public string Require(int textblockId) => RequireTextblock(textblockId);
}

public static class RuntimeBundleFPhaseF10NativePayloadAudit
{
    public static IReadOnlyList<string> Validate(string projectRoot)
    {
        var failures = new List<string>();
        var native = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var dialogue = RuntimeClassicNpcDialogueAuthority.Load(projectRoot);
        foreach (var root in dialogue.AllRoots.Select(x => x.RootTextblockId).Distinct())
        foreach (var nodeId in graph.ReachableDecodedNodes(root))
        {
            var node = graph.Require(nodeId);
            foreach (var op in node.Operations.Where(x => x.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase) || x.Opcode.Equals("text", StringComparison.OrdinalIgnoreCase)))
            {
                var target = op.NumericArguments.FirstOrDefault();
                var resolved = op.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
                    ? native.TryResolveMessage(target, out _)
                    : native.TryResolveTextblock(target, out _);
                if (target <= 0 || !resolved)
                    failures.Add($"textblock:{nodeId}:opcode:{op.Opcode}:target:{target}:presentation-namespace-unresolved");
            }
        }
        return failures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}
