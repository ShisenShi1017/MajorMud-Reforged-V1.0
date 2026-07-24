using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNativeTextblockPayload(int TextblockId, string Payload, long PageOffset, int PageIndex, string EvidenceState);
public sealed record RuntimeClassicNativeDialogueMessagePayload(int MessageId, int RecordIndex, long RecordOffset, int PayloadOffset, string Payload, string EvidenceState);

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
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative WCCTEXT2 payload catalog is missing.", path);
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
        var path = Path.Combine(projectRoot, "Librarium", "Evidence", "native_presentation_reference_mapping_0140.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Bundle G native presentation mapping evidence is missing.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Bundle G native presentation mapping has no records array.");
        var ids = new HashSet<int>();
        foreach (var row in rows.EnumerateArray())
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
        var path = Path.Combine(projectRoot, "Librarium", "Evidence", "native_dialogue_message_payload_mapping_0141.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Bundle G native dialogue message mapping evidence is missing.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Bundle G native dialogue message mapping has no records array.");
        var map = new Dictionary<int, RuntimeClassicNativeDialogueMessagePayload>();
        foreach (var row in rows.EnumerateArray())
        {
            var id = row.GetProperty("message_id").GetInt32();
            var recordIndex = row.GetProperty("record_index").GetInt32();
            var recordOffset = row.GetProperty("record_offset").GetInt64();
            var payloadOffset = row.GetProperty("payload_offset").GetInt32();
            var payload = row.GetProperty("payload").GetString() ?? string.Empty;
            var evidence = row.GetProperty("evidence_state").GetString() ?? string.Empty;
            if (id <= 0 || recordIndex < 0 || recordOffset != (long)recordIndex * 256L || payloadOffset < 0 ||
                string.IsNullOrWhiteSpace(payload) || !evidence.Equals("verified_native_runtime", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Invalid native dialogue message mapping {id}.");
            if (!map.TryAdd(id, new(id, recordIndex, recordOffset, payloadOffset, payload, evidence)))
                throw new InvalidDataException($"Duplicate native dialogue message mapping {id}.");
        }
        if (map.Count != 2 || !map.ContainsKey(2044) || !map.ContainsKey(3290))
            throw new InvalidDataException("Expected exact native dialogue message mappings for 2044 and 3290.");
        return map;
    }

    public bool TryResolve(int textblockId, out string payload)
    {
        if (_messagePayloads.TryGetValue(textblockId, out var message)) { payload = message.Payload; return true; }
        if (_records.TryGetValue(textblockId, out var record)) { payload = record.Payload; return true; }
        if (_verifiedNoOutputTextblocks.Contains(textblockId)) { payload = string.Empty; return true; }
        payload = string.Empty; return false;
    }

    public bool IsVerifiedNoOutput(int textblockId) => _verifiedNoOutputTextblocks.Contains(textblockId);

    public string Require(int textblockId) => TryResolve(textblockId, out var payload)
        ? payload : throw new InvalidOperationException($"Native presentation payload {textblockId} is unresolved.");
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
                if (target <= 0 || !native.TryResolve(target, out _))
                    failures.Add($"textblock:{nodeId}:opcode:{op.Opcode}:target:{target}:presentation-namespace-unresolved");
            }
        }
        return failures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}
