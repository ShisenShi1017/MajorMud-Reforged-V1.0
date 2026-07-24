using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNpcDialogueExecutionRequest(int NpcId, string Input);
public sealed record RuntimeClassicNpcDialogueExecutionResolution(RuntimeClassicNpcDialogueMatch Match, RuntimeClassicTextblockExecutionPlan Plan);

/// <summary>
/// Bundle F Phase F10 conditional NPC-dialogue execution authority. It joins the decoded
/// NPC ownership/keyword projection to the single decoded textblock execution-plan authority.
/// It does not execute mutations itself and therefore does not duplicate Bundle A-E owners.
/// </summary>
public sealed class RuntimeClassicNpcDialogueExecutionAuthority
{
    private readonly RuntimeClassicNpcDialogueAuthority _dialogue;
    private readonly RuntimeClassicTextblockGraphAuthority _graph;

    private RuntimeClassicNpcDialogueExecutionAuthority(RuntimeClassicNpcDialogueAuthority dialogue, RuntimeClassicTextblockGraphAuthority graph)
    {
        _dialogue = dialogue;
        _graph = graph;
    }

    public static RuntimeClassicNpcDialogueExecutionAuthority Load(string projectRoot) =>
        new(RuntimeClassicNpcDialogueAuthority.Load(projectRoot), RuntimeClassicTextblockGraphAuthority.Load(projectRoot));

    public bool TryResolve(RuntimeClassicNpcDialogueExecutionRequest request, out RuntimeClassicNpcDialogueExecutionResolution resolution)
    {
        resolution = null!;
        if (request.NpcId <= 0 || !_dialogue.TryResolve(request.NpcId, request.Input, out var match)) return false;
        if (!_graph.TryCreateExecutionPlan(match.RootTextblockId, request.Input, out var plan)) return false;
        if (plan.MatchedTextblockId != match.TargetTextblockId) return false;
        if (plan.Operations.Any(x => !x.Status.Equals("decoded", StringComparison.OrdinalIgnoreCase))) return false;
        resolution = new(match, plan);
        return true;
    }

    public IReadOnlyList<string> ValidateAllOwnedDialoguePlans()
    {
        var failures = new List<string>();
        foreach (var root in _dialogue.AllRoots)
        {
            var node = _graph.Require(root.RootTextblockId);
            if (node.Commands.Count == 0)
            {
                if (!TryResolve(new(root.NpcId, string.Empty), out var direct) || direct.Plan.MatchedTextblockId != root.RootTextblockId)
                    failures.Add($"npc:{root.NpcId}:root:{root.RootTextblockId}:direct-plan-unresolved");
                continue;
            }

            foreach (var command in node.Commands)
            {
                if (!TryResolve(new(root.NpcId, command.Keyword), out var resolved))
                {
                    failures.Add($"npc:{root.NpcId}:root:{root.RootTextblockId}:keyword:{command.NormalizedKeyword}:plan-unresolved");
                    continue;
                }
                if (resolved.Plan.MatchedTextblockId != command.TargetTextblockId)
                    failures.Add($"npc:{root.NpcId}:root:{root.RootTextblockId}:keyword:{command.NormalizedKeyword}:target-mismatch");
                if (resolved.Plan.Operations.Select(x => (x.Line, x.Sequence)).SequenceEqual(resolved.Plan.Operations.OrderBy(x => x.Line).ThenBy(x => x.Sequence).Select(x => (x.Line, x.Sequence))) is false)
                    failures.Add($"npc:{root.NpcId}:root:{root.RootTextblockId}:keyword:{command.NormalizedKeyword}:operation-order-changed");
            }
        }
        return failures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}

public sealed record RuntimeBundleFPhaseF10NpcDialogueExecutionAuditReport(
    bool Passed,
    int NpcCount,
    int OwnershipLinkCount,
    int UniqueRootCount,
    int ReachableNodeCount,
    int CommandRouteCount,
    int DirectRouteCount,
    int TriggerCount,
    int OperationCount,
    int UnknownOpcodeCount,
    int DisconnectCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF10NpcDialogueExecutionAudit
{
    public static RuntimeBundleFPhaseF10NpcDialogueExecutionAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }

        var dialogue = RuntimeClassicNpcDialogueAuthority.Load(projectRoot);
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var execution = RuntimeClassicNpcDialogueExecutionAuthority.Load(projectRoot);
        var roots = dialogue.AllRoots;
        var uniqueRootIds = roots.Select(x => x.RootTextblockId).Distinct().OrderBy(x => x).ToArray();
        var reachable = uniqueRootIds.SelectMany(graph.ReachableDecodedNodes).Distinct().OrderBy(x => x).ToArray();
        var nodes = reachable.Select(graph.Require).ToArray();
        var commandRoutes = uniqueRootIds.Sum(x => graph.Require(x).Commands.Count);
        var directRoutes = uniqueRootIds.Count(x => graph.Require(x).Commands.Count == 0);
        var triggers = nodes.Sum(x => x.Triggers.Count);
        var operations = nodes.Sum(x => x.Operations.Count);
        var unknown = nodes.Sum(x => x.Operations.Count(o => !o.Status.Equals("decoded", StringComparison.OrdinalIgnoreCase)));
        failures.AddRange(execution.ValidateAllOwnedDialoguePlans());

        Check(dialogue.NpcCount == 768, "all-768-dialogue-npcs-covered");
        Check(dialogue.LinkCount == 771, "all-771-npc-root-links-covered");
        Check(uniqueRootIds.Length == 346, "all-346-unique-roots-covered");
        Check(reachable.Length == 1303, "all-1303-owned-reachable-nodes-covered");
        Check(commandRoutes == 755, "all-755-decoded-command-routes-plan-resolvable");
        Check(directRoutes == 19, "all-19-direct-response-routes-plan-resolvable");
        Check(triggers == 19, "all-19-owned-dialogue-triggers-projected");
        Check(operations == 3363, "all-3363-owned-dialogue-operations-projected");
        Check(unknown == 0, "no-undecoded-owned-dialogue-operations");
        Check(execution.TryResolve(new(716, "MAGMA SCROLL"), out var gnome) && gnome.Plan.MatchedTextblockId == 2803, "gnome-merchant-live-plan-resolution");
        Check(failures.Count == 0, "no-owned-dialogue-plan-disconnects");

        return new(failures.Count == 0, dialogue.NpcCount, dialogue.LinkCount, uniqueRootIds.Length, reachable.Length, commandRoutes, directRoutes, triggers, operations, unknown, failures.Count, checks, failures);
    }
}
