using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.Server;

public interface IRuntimeClassicDialogueRandomSource
{
    int NextInclusive(int minimum, int maximum);
}

public interface IRuntimeClassicDialogueSubstitutionSource
{
    string Resolve(int textblockId, string decodedPayload);
}

public sealed record RuntimeClassicNpcDialogueRuntimeRequest(
    int NpcId,
    string Input,
    RuntimeClassicNarrativeExecutionContext Context);

public sealed record RuntimeClassicNpcDialogueRuntimeResult(
    bool Succeeded,
    int NpcId,
    int RootTextblockId,
    int MatchedTextblockId,
    int RandomBranchCount,
    IReadOnlyList<int> RenderedTextblockIds,
    IReadOnlyList<string> Output,
    string? Failure);

/// <summary>
/// Canonical end-to-end Phase F10 consumer. NPC ownership and keyword selection remain in
/// RuntimeClassicNpcDialogueAuthority, decoded plans remain in the single textblock graph,
/// Bundle A-E mutations remain in the narrative coordinator, and player-facing payloads
/// resolve only through WCCTEXT2. Random target tables preserve decoded cumulative thresholds.
/// </summary>
public sealed class RuntimeClassicNpcDialogueRuntime
{
    private readonly RuntimeClassicNpcDialogueExecutionAuthority _dialogue;
    private readonly RuntimeClassicNativeTextblockPayloadAuthority _payloads;
    private readonly RuntimeClassicNarrativeExecutionCoordinator _coordinator;
    private readonly RuntimeClassicRandomTableAuthority _randomTables;
    private readonly IRuntimeClassicDialogueSubstitutionSource _substitutions;

    public RuntimeClassicNpcDialogueRuntime(
        RuntimeClassicNpcDialogueExecutionAuthority dialogue,
        RuntimeClassicNativeTextblockPayloadAuthority payloads,
        RuntimeClassicNarrativeExecutionCoordinator coordinator,
        RuntimeClassicRandomTableAuthority randomTables,
        IRuntimeClassicDialogueSubstitutionSource substitutions)
    {
        _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
        _payloads = payloads ?? throw new ArgumentNullException(nameof(payloads));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _randomTables = randomTables ?? throw new ArgumentNullException(nameof(randomTables));
        _substitutions = substitutions ?? throw new ArgumentNullException(nameof(substitutions));
    }

    public RuntimeClassicNpcDialogueRuntimeResult Execute(RuntimeClassicNpcDialogueRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_dialogue.TryResolve(new(request.NpcId, request.Input), out var resolution))
            return new(false, request.NpcId, 0, 0, 0, Array.Empty<int>(), Array.Empty<string>(), "npc-dialogue-route-unresolved");
        if (request.Context.RootTextblockId != resolution.Match.RootTextblockId)
            return new(false, request.NpcId, resolution.Match.RootTextblockId, resolution.Match.TargetTextblockId, 0, Array.Empty<int>(), Array.Empty<string>(), "dialogue-context-root-mismatch");

        var expanded = _randomTables.Expand(resolution.Plan.Operations, out var randomCount);
        var presentationOperations = expanded.Where(IsPresentation).ToArray();
        var renderedIds = presentationOperations.Select(RequireTarget).ToArray();
        foreach (var operation in presentationOperations) _ = Render(operation);
        var plan = resolution.Plan with
        {
            Operations = expanded,
            MessageTextblockIds = renderedIds,
            ContinuationTextblockId = renderedIds.LastOrDefault() is var last && last > 0 ? last : resolution.Plan.ContinuationTextblockId
        };
        var execution = _coordinator.Execute(plan, request.Context);
        if (!execution.Succeeded)
            return new(false, request.NpcId, plan.RootTextblockId, plan.MatchedTextblockId, randomCount, renderedIds, execution.Output, execution.Failure);

        // The coordinator remains the mutation boundary. Native rendering is canonicalized here
        // so host presentation bindings cannot fall back to numeric identifiers.
        var output = presentationOperations
            .Where(operation => operation.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
                ? !_payloads.IsVerifiedNoOutputMessage(RequireTarget(operation))
                : !_payloads.IsVerifiedNoOutput(RequireTarget(operation)))
            .Select(Render)
            .ToArray();
        return new(true, request.NpcId, plan.RootTextblockId, plan.MatchedTextblockId, randomCount, renderedIds, output, null);
    }

    private static bool IsPresentation(RuntimeClassicTextblockOperation operation) =>
        operation.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase) || operation.Opcode.Equals("text", StringComparison.OrdinalIgnoreCase);

    private static int RequireTarget(RuntimeClassicTextblockOperation operation) =>
        operation.TargetTextblockId is > 0 ? operation.TargetTextblockId.Value :
        operation.NumericArguments.FirstOrDefault() is var value && value > 0 ? value :
        throw new InvalidOperationException($"Presentation operation '{operation.Raw}' has no textblock target.");

    private string Render(RuntimeClassicTextblockOperation operation)
    {
        var id = RequireTarget(operation);
        var isMessage = operation.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase);
        var payload = isMessage ? _payloads.RequireMessage(id) : _payloads.RequireTextblock(id);
        var rendered = _substitutions.Resolve(id, payload);
        if (string.IsNullOrEmpty(rendered))
        {
            if (isMessage && _payloads.IsVerifiedNoOutputMessage(id)) return string.Empty;
            if (!isMessage && _payloads.IsVerifiedNoOutput(id)) return string.Empty;
            throw new InvalidOperationException($"Rendered dialogue {operation.Opcode} payload {id} is empty.");
        }
        if (Regex.IsMatch(rendered, @"%s", RegexOptions.CultureInvariant))
            throw new InvalidOperationException($"Rendered dialogue {operation.Opcode} payload {id} retains unresolved native substitution tokens.");
        return rendered;
    }
}

public sealed record RuntimeBundleFPhaseF10CompleteAuditReport(
    bool Passed, int NpcCount, int OwnershipLinks, int UniqueRoots, int ReachableNodes,
    int OperationCount, int PresentationReferenceCount, int RandomOperationCount,
    int DelayOperationCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF10CompleteAudit
{
    public static RuntimeBundleFPhaseF10CompleteAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var dialogue = RuntimeClassicNpcDialogueAuthority.Load(projectRoot);
        var execution = RuntimeClassicNpcDialogueExecutionAuthority.Load(projectRoot);
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var payloads = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
        failures.AddRange(dialogue.ValidateAllRoots());
        failures.AddRange(execution.ValidateAllOwnedDialoguePlans());
        failures.AddRange(RuntimeBundleFPhaseF10NativePayloadAudit.Validate(projectRoot));
        var roots = dialogue.AllRoots.Select(x => x.RootTextblockId).Distinct().ToArray();
        var reachable = roots.SelectMany(graph.ReachableDecodedNodes).Distinct().ToArray();
        var operations = reachable.SelectMany(x => graph.Require(x).Operations).ToArray();
        var presentation = operations.Where(x => x.Opcode is "message" or "text").ToArray();
        var random = operations.Where(x => x.Opcode.Equals("random", StringComparison.OrdinalIgnoreCase)).ToArray();
        var delay = operations.Where(x => x.Opcode.Equals("adddelay", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var operation in random)
        {
            var target = operation.NumericArguments.FirstOrDefault();
            if (target <= 0 || !graph.TryGet(target, out var node)) { failures.Add($"random-target-unresolved:{target}"); continue; }
            try
            {
                var rows = node.Script.Replace("\r", string.Empty).Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Regex.Match(x.TrimStart(), @"^(\d+)\s*:")).Where(x => x.Success).Select(x => int.Parse(x.Groups[1].Value)).ToArray();
                if (rows.Length == 0 || rows[^1] != 100 || rows.Zip(rows.Skip(1), (a,b) => a < b).Any(x => !x)) failures.Add($"random-thresholds-invalid:{target}");
            }
            catch { failures.Add($"random-thresholds-invalid:{target}"); }
        }
        Check(dialogue.NpcCount == 768, "all-dialogue-npcs-owned");
        Check(dialogue.LinkCount == 771, "all-dialogue-ownership-links-owned");
        Check(roots.Length == 346, "all-dialogue-roots-owned");
        Check(reachable.Length == 1303, "all-1303-dialogue-nodes-reachable");
        Check(operations.Length == 3363, "all-3363-dialogue-operations-consumed");
        Check(presentation.Length == 754, "all-754-message-and-text-references-renderable");
        Check(random.Length == 9, "all-random-branches-consumed");
        Check(delay.Length == 3, "all-decoded-delay-operations-owned");
        Check(payloads.Count == 3467, "native-dialogue-payload-catalog-complete");
        Check(failures.Count == 0, "zero-phase-f10-disconnects");
        return new(failures.Count == 0, dialogue.NpcCount, dialogue.LinkCount, roots.Length, reachable.Length, operations.Length, presentation.Length, random.Length, delay.Length, failures.Count, checks, failures.Distinct().OrderBy(x => x).ToArray());
    }
}
