using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeNpcDecisionKind
{
    Invalid,
    Dead,
    AlreadyProcessed,
    AwaitingActionCompletion,
    Disabled,
    Frozen,
    BasicAttack,
    ServiceInteraction,
    HoldPosition
}

public sealed record RuntimeNpcDecisionContext(
    string NpcInstanceId,
    RuntimeNpcDefinition Definition,
    RuntimeNpcLifeState LifeState,
    string RoomId,
    long WorldTick,
    string? CurrentTargetId,
    string? InteractionRequest,
    long? LastProcessedWorldTick = null,
    string? PendingActionId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record RuntimeNpcDecisionCandidate(
    RuntimeNpcDecisionKind Kind,
    bool Eligible,
    string EvidenceAuthority,
    string Reason);

public sealed record RuntimeNpcDecisionTrace(
    string NpcInstanceId,
    long WorldTick,
    RuntimeNpcDecisionKind SelectedAction,
    IReadOnlyList<RuntimeNpcDecisionCandidate> Candidates,
    string EvidenceAuthority,
    IReadOnlyDictionary<string, string> StateChanges);

/// <summary>
/// Evidence-constrained Reforged NPC decision coordinator. It activates only decisions supported
/// by existing generated/runtime fields and explicit owner state, but its candidate order is not
/// claimed as the native Classic WCCMMUD dispatcher. Native flee/perception/patrol/tracking,
/// assist priority, and exact update/retry ordering remain blocked rather than inferred.
/// </summary>
public sealed class RuntimeNpcAiDecisionEngine
{
    public RuntimeNpcDecisionTrace Decide(RuntimeNpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Definition);

        var candidates = new List<RuntimeNpcDecisionCandidate>();
        if (string.IsNullOrWhiteSpace(context.NpcInstanceId) ||
            string.IsNullOrWhiteSpace(context.Definition.Id) ||
            string.IsNullOrWhiteSpace(context.RoomId) ||
            context.WorldTick < 0)
        {
            candidates.Add(new(RuntimeNpcDecisionKind.Invalid, true, "runtime-instance-validation", "required-runtime-identity-or-tick-invalid"));
            return Trace(context, RuntimeNpcDecisionKind.Invalid, candidates, "runtime-instance-validation");
        }

        var dead = context.LifeState is RuntimeNpcLifeState.Dead or RuntimeNpcLifeState.Despawned or RuntimeNpcLifeState.WaitingToRespawn;
        candidates.Add(new(RuntimeNpcDecisionKind.Dead, dead, "runtime-npc-life-state", dead ? "instance-not-actionable" : "instance-alive"));
        if (dead)
            return Trace(context, RuntimeNpcDecisionKind.Dead, candidates, "runtime-npc-life-state",
                new Dictionary<string, string>
                {
                    ["combat_hostility_state"] = "dead",
                    ["current_target_id"] = string.Empty,
                    ["interaction_request"] = string.Empty
                });

        var alreadyProcessed = context.LastProcessedWorldTick is long priorTick && priorTick >= context.WorldTick;
        candidates.Add(new(RuntimeNpcDecisionKind.AlreadyProcessed, alreadyProcessed, "persisted-monotonic-ai-tick",
            alreadyProcessed ? $"last-processed-tick-{context.LastProcessedWorldTick}-is-not-before-current" : "current-tick-is-new"));
        if (alreadyProcessed)
            return Trace(context, RuntimeNpcDecisionKind.AlreadyProcessed, candidates, "persisted-monotonic-ai-tick");

        var aiDisabled = ReadMetadata(context, "npc_ai_enabled").Equals("false", StringComparison.OrdinalIgnoreCase);
        candidates.Add(new(RuntimeNpcDecisionKind.Disabled, aiDisabled, "explicit-host-administrative-ai-control",
            aiDisabled ? "npc-ai-explicitly-disabled" : "npc-ai-enabled-or-unspecified"));
        if (aiDisabled)
            return Trace(context, RuntimeNpcDecisionKind.Disabled, candidates, "explicit-host-administrative-ai-control",
                new Dictionary<string,string> { ["combat_hostility_state"] = "disabled" });

        var frozen = ReadMetadata(context, "npc_frozen").Equals("true", StringComparison.OrdinalIgnoreCase);
        candidates.Add(new(RuntimeNpcDecisionKind.Frozen, frozen, "explicit-host-administrative-freeze-control",
            frozen ? "npc-explicitly-frozen" : "npc-not-frozen"));
        if (frozen)
            return Trace(context, RuntimeNpcDecisionKind.Frozen, candidates, "explicit-host-administrative-freeze-control",
                new Dictionary<string,string> { ["combat_hostility_state"] = "frozen" });

        var awaitingAction = !string.IsNullOrWhiteSpace(context.PendingActionId);
        candidates.Add(new(RuntimeNpcDecisionKind.AwaitingActionCompletion, awaitingAction, "persisted-pending-action-ownership",
            awaitingAction ? "prior-action-awaiting-owning-subsystem-completion" : "no-pending-action"));
        if (awaitingAction)
            return Trace(context, RuntimeNpcDecisionKind.AwaitingActionCompletion, candidates, "persisted-pending-action-ownership");

        var canAttack = !string.IsNullOrWhiteSpace(context.CurrentTargetId) && context.Definition.AttackCount > 0;
        candidates.Add(new(RuntimeNpcDecisionKind.BasicAttack, canAttack, "generated-npc-attack-count+explicit-runtime-target",
            canAttack ? "target-present-and-generated-attack-count-positive" : "missing-target-or-no-generated-attacks"));
        if (canAttack)
            return Trace(context, RuntimeNpcDecisionKind.BasicAttack, candidates, "generated-npc-attack-count+explicit-runtime-target",
                new Dictionary<string, string> { ["combat_hostility_state"] = "engaged" });

        var serviceModel = ReadMetadata(context, "interaction_model");
        var serviceRequested = !string.IsNullOrWhiteSpace(context.InteractionRequest) &&
            (serviceModel.Contains("service", StringComparison.OrdinalIgnoreCase) ||
             serviceModel.Contains("shop", StringComparison.OrdinalIgnoreCase) ||
             serviceModel.Contains("trainer", StringComparison.OrdinalIgnoreCase));
        candidates.Add(new(RuntimeNpcDecisionKind.ServiceInteraction, serviceRequested, "spawn-interaction-model+explicit-runtime-request",
            serviceRequested ? "supported-service-request-present" : "no-supported-service-request"));
        if (serviceRequested)
            return Trace(context, RuntimeNpcDecisionKind.ServiceInteraction, candidates, "spawn-interaction-model+explicit-runtime-request");

        candidates.Add(new(RuntimeNpcDecisionKind.HoldPosition, true, "safe-fallback-native-ai-unresolved",
            "no-proven-active-decision; movement-perception-threat-and-priority-remain-native-dependent"));
        return Trace(context, RuntimeNpcDecisionKind.HoldPosition, candidates, "safe-fallback-native-ai-unresolved",
            new Dictionary<string, string> { ["combat_hostility_state"] = "idle" });
    }

    private static string ReadMetadata(RuntimeNpcDecisionContext context, string key) =>
        context.Metadata is not null && context.Metadata.TryGetValue(key, out var value) ? value : string.Empty;

    private static RuntimeNpcDecisionTrace Trace(
        RuntimeNpcDecisionContext context,
        RuntimeNpcDecisionKind selected,
        IReadOnlyList<RuntimeNpcDecisionCandidate> candidates,
        string authority,
        IReadOnlyDictionary<string, string>? stateChanges = null) =>
        new(context.NpcInstanceId, context.WorldTick, selected, candidates, authority,
            stateChanges ?? new Dictionary<string, string>());
}

public sealed class RuntimeNpcAiDecisionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeNpcAiDecisionSmokeReport Run()
    {
        var report = new RuntimeNpcAiDecisionSmokeReport();
        try
        {
            var engine = new RuntimeNpcAiDecisionEngine();
            var npc = new RuntimeNpcDefinition { Id = "npc.test", Name = "Test", HitPoints = 10, AttackCount = 1 };
            Require(engine.Decide(new("i1", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 1, "player.1", null, null, null)).SelectedAction == RuntimeNpcDecisionKind.BasicAttack, "explicit-target-basic-attack");
            report.Checks.Add("explicit-target-basic-attack");

            var service = engine.Decide(new("i2", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 2, null, "buy", null, null,
                new Dictionary<string, string> { ["interaction_model"] = "shop-service" }));
            Require(service.SelectedAction == RuntimeNpcDecisionKind.ServiceInteraction, "explicit-service-interaction");
            report.Checks.Add("explicit-service-interaction");

            var blocked = engine.Decide(new("i3", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 3, null, null, null, null));
            Require(blocked.SelectedAction == RuntimeNpcDecisionKind.HoldPosition && blocked.EvidenceAuthority.Contains("safe-fallback", StringComparison.Ordinal), "native-ai-safe-fallback");
            report.Checks.Add("native-ai-safe-fallback");

            var dead = engine.Decide(new("i4", npc, RuntimeNpcLifeState.WaitingToRespawn, "map1-room1", 4, null, null, null, null));
            Require(dead.SelectedAction == RuntimeNpcDecisionKind.Dead, "dead-state-precedence");
            report.Checks.Add("dead-state-precedence");

            var duplicate = engine.Decide(new("i5", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 5, "player.1", null, 5, null));
            Require(duplicate.SelectedAction == RuntimeNpcDecisionKind.AlreadyProcessed, "duplicate-tick-suppressed");
            report.Checks.Add("duplicate-tick-suppressed");

            Require(dead.StateChanges.TryGetValue("combat_hostility_state", out var deadHostility) && deadHostility == "dead"
                && dead.StateChanges.TryGetValue("current_target_id", out var clearedTarget) && clearedTarget.Length == 0,
                "dead-runtime-state-cleanup");
            report.Checks.Add("dead-runtime-state-cleanup");


            var pending = engine.Decide(new("i7", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 7, "player.1", null, 6, "i7:6:BasicAttack"));
            Require(pending.SelectedAction == RuntimeNpcDecisionKind.AwaitingActionCompletion, "pending-action-blocks-new-decision");
            report.Checks.Add("pending-action-blocks-new-decision");

            var first = engine.Decide(new("i6", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 6, "player.1", null, null, null));
            var second = engine.Decide(new("i6", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 6, "player.1", null, null, null));
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second), "deterministic-replay");
            report.Checks.Add("deterministic-replay");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
