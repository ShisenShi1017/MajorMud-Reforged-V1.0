using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNpcDialogueRoot(int NpcId, string NpcName, int RootTextblockId, string EvidenceState);
public sealed record RuntimeClassicNpcDialogueMatch(int NpcId, int RootTextblockId, int TargetTextblockId, string NormalizedInput, bool IsDirectRootResponse);

/// <summary>
/// Canonical Bundle F Phase F10 NPC-to-dialogue ownership and keyword-routing authority.
/// Ownership is loaded from the retained decoded source; text execution remains owned by
/// RuntimeClassicTextblockGraphAuthority so no parallel dialogue or quest engine is introduced.
/// </summary>
public sealed class RuntimeClassicNpcDialogueAuthority
{
    private readonly RuntimeClassicTextblockGraphAuthority _graph;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<RuntimeClassicNpcDialogueRoot>> _rootsByNpc;

    private RuntimeClassicNpcDialogueAuthority(RuntimeClassicTextblockGraphAuthority graph, IReadOnlyDictionary<int, IReadOnlyList<RuntimeClassicNpcDialogueRoot>> rootsByNpc)
    {
        _graph = graph;
        _rootsByNpc = rootsByNpc;
    }

    public static RuntimeClassicNpcDialogueAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Librarium", "SourceImports", "MMExplorerSQLData", "TextblocksDecoded0142", "npc_textblock_links.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative decoded NPC dialogue ownership catalog is required.", path);
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("NPC dialogue ownership root must be an array.");

        var exactPairs = new HashSet<(int NpcId, int RootId)>();
        var rows = new List<RuntimeClassicNpcDialogueRoot>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            var npcId = RequiredInt(row, "npc_id");
            var rootId = RequiredInt(row, "root_textblock_id");
            var npcName = OptionalText(row, "npc_name");
            var evidence = RequiredText(row, "evidence_state");
            if (npcId <= 0 || rootId <= 0) throw new InvalidDataException("NPC dialogue ownership contains a non-positive identity.");
            if (!exactPairs.Add((npcId, rootId))) throw new InvalidDataException($"Duplicate NPC dialogue ownership pair {npcId}->{rootId}.");
            graph.Require(rootId);
            rows.Add(new(npcId, npcName.Length > 0 ? npcName : $"npc.{npcId}", rootId, evidence));
        }
        if (rows.Count == 0) throw new InvalidDataException("Decoded NPC dialogue ownership catalog is empty.");
        var grouped = rows.GroupBy(x => x.NpcId).ToDictionary(g => g.Key, g => (IReadOnlyList<RuntimeClassicNpcDialogueRoot>)g.OrderBy(x => x.RootTextblockId).ToArray());
        return new(graph, grouped);
    }

    public int NpcCount => _rootsByNpc.Count;
    public int LinkCount => _rootsByNpc.Values.Sum(x => x.Count);
    public IReadOnlyList<RuntimeClassicNpcDialogueRoot> AllRoots => _rootsByNpc.Values.SelectMany(x => x).OrderBy(x => x.NpcId).ThenBy(x => x.RootTextblockId).ToArray();
    public IReadOnlyList<RuntimeClassicNpcDialogueRoot> GetRoots(int npcId) => _rootsByNpc.TryGetValue(npcId, out var roots) ? roots : Array.Empty<RuntimeClassicNpcDialogueRoot>();

    public bool TryResolve(int npcId, string input, out RuntimeClassicNpcDialogueMatch match)
    {
        match = null!;
        if (!_rootsByNpc.TryGetValue(npcId, out var roots)) return false;
        var normalized = Normalize(input);
        foreach (var root in roots)
        {
            var node = _graph.Require(root.RootTextblockId);
            if (node.Commands.Count == 0)
            {
                if (normalized.Length == 0)
                {
                    match = new(npcId, root.RootTextblockId, root.RootTextblockId, normalized, true);
                    return true;
                }
                continue;
            }
            if (_graph.TryResolveCommand(root.RootTextblockId, normalized, out var command))
            {
                match = new(npcId, root.RootTextblockId, command.TargetTextblockId, normalized, false);
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<string> ValidateAllRoots()
    {
        var failures = new List<string>();
        foreach (var roots in _rootsByNpc.Values)
        foreach (var root in roots)
        {
            var node = _graph.Require(root.RootTextblockId);
            var conflicts = node.Commands.GroupBy(x => x.NormalizedKeyword, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(x => x.TargetTextblockId).Distinct().Count() > 1);
            failures.AddRange(conflicts.Select(g => $"npc:{root.NpcId}:root:{root.RootTextblockId}:conflicting-keyword:{g.Key}"));
            failures.AddRange(_graph.MissingTextblockReferences(root.RootTextblockId).Select(x => $"npc:{root.NpcId}:root:{root.RootTextblockId}:{x}"));
        }
        return failures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string Normalize(string value) => string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static int RequiredInt(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : throw new InvalidDataException($"NPC dialogue ownership is missing numeric {key}.");
    private static string RequiredText(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!.Trim() : throw new InvalidDataException($"NPC dialogue ownership is missing {key}.");
    private static string OptionalText(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : string.Empty;
}

public sealed record RuntimeBundleFPhaseF10NpcDialogueAuditReport(bool Passed, int NpcCount, int LinkCount, int UniqueRootCount, int MultiRootNpcCount, int CommandRootCount, int DirectResponseRootCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF10NpcDialogueAudit
{
    public static RuntimeBundleFPhaseF10NpcDialogueAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var authority = RuntimeClassicNpcDialogueAuthority.Load(projectRoot);
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var allRoots = Enumerable.Range(1, 12000).SelectMany(authority.GetRoots).ToArray();
        var uniqueRoots = allRoots.Select(x => x.RootTextblockId).Distinct().ToArray();
        var multiRootNpcs = allRoots.GroupBy(x => x.NpcId).Count(g => g.Count() > 1);
        var commandRoots = uniqueRoots.Count(id => graph.Require(id).Commands.Count > 0);
        var directRoots = uniqueRoots.Length - commandRoots;
        var relationshipFailures = authority.ValidateAllRoots();
        failures.AddRange(relationshipFailures);

        Check(authority.LinkCount == 771, "all-771-decoded-npc-dialogue-links-projected");
        Check(authority.NpcCount == 768, "all-768-decoded-dialogue-npcs-projected");
        Check(uniqueRoots.Length == 346, "all-346-unique-dialogue-roots-projected");
        Check(multiRootNpcs == 3, "three-decoded-multi-root-npcs-preserved");
        Check(commandRoots == 327, "decoded-command-roots-preserved");
        Check(directRoots == 19, "decoded-direct-response-roots-preserved");
        Check(authority.GetRoots(716).Any(x => x.RootTextblockId == 2798), "gnome-merchant-root-2798-owned-by-npc-716");
        Check(authority.TryResolve(716, "MAGMA SCROLL", out var gnome) && gnome.RootTextblockId == 2798 && gnome.TargetTextblockId == 2803, "normalized-gnome-merchant-keyword-routing");
        Check(authority.GetRoots(27).Count == 2 && authority.GetRoots(33).Count == 2 && authority.GetRoots(702).Count == 2, "decoded-multiple-root-ownership-not-collapsed");
        Check(relationshipFailures.Count == 0, "no-missing-references-or-conflicting-keywords");
        return new(failures.Count == 0, authority.NpcCount, authority.LinkCount, uniqueRoots.Length, multiRootNpcs, commandRoots, directRoots, failures.Count, checks, failures);
    }
}
