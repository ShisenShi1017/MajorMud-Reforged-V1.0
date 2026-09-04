using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcActionIntent(
    string ActionId,
    string NpcInstanceId,
    long WorldTick,
    RuntimeNpcDecisionKind Kind,
    string? TargetId,
    string? Request,
    string EvidenceAuthority);

public sealed record RuntimeNpcActionClaim(
    RuntimeNpcActionIntent Intent,
    string ConsumerId,
    string ClaimId,
    int DeliveryCount,
    long ClaimedAtWorldTick);

public sealed record RuntimeNpcActionQuarantine(
    RuntimeNpcActionIntent Intent,
    int DeliveryCount,
    string FailureReason,
    long FailedAtWorldTick);

public sealed record RuntimeNpcAiTickResult(
    long WorldTick,
    int ProcessedNpcCount,
    IReadOnlyList<RuntimeNpcDecisionTrace> Decisions,
    IReadOnlyList<RuntimeNpcActionIntent> ActionIntents,
    IReadOnlyList<string> StateChanges);

/// <summary>
/// Deterministic Reforged coordination scheduler around evidence-backed NPC inputs.
/// Its world-tick admission, instance ordering, and delivery/retry bookkeeping are not
/// claimed as the native Classic WCCMMUD fast/medium/slow/energy dispatcher. Exact native
/// perception, pathing, action priority, and update cadence remain fail-closed.
/// </summary>
public sealed class RuntimeNpcAiScheduler
{
    private readonly RuntimeNpcAiDecisionEngine _engine = new();

    public RuntimeNpcAiTickResult Tick(long worldTick, IReadOnlyList<RuntimeLivingNpcState> npcs)
    {
        if (worldTick < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
        ArgumentNullException.ThrowIfNull(npcs);

        var traces = new List<RuntimeNpcDecisionTrace>();
        var intents = new List<RuntimeNpcActionIntent>();
        var changes = new List<string>();
        foreach (var npc in npcs.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase))
        {
            var definition = new RuntimeNpcDefinition
            {
                Id = npc.NpcId,
                Name = npc.Name,
                HitPoints = npc.MaximumHitPoints,
                AttackCount = ReadInt(npc.Metadata, "attack_count")
            };
            var target = Read(npc.Metadata, "current_target_id");
            var request = Read(npc.Metadata, "interaction_request");
            var lifeState = npc.Alive
                ? RuntimeNpcLifeState.Spawned
                : npc.RespawnAtUtc is null ? RuntimeNpcLifeState.Dead : RuntimeNpcLifeState.WaitingToRespawn;
            var metadata = new Dictionary<string, string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["interaction_model"] = npc.InteractionModel
            };
            var lastTick = ReadLong(npc.Metadata, "last_ai_tick");
            var pendingActionId = Read(npc.Metadata, "pending_ai_action_id");
            var trace = _engine.Decide(new(
                npc.InstanceId, definition, lifeState, npc.RoomId, worldTick,
                string.IsNullOrWhiteSpace(target) ? null : target,
                string.IsNullOrWhiteSpace(request) ? null : request,
                lastTick,
                string.IsNullOrWhiteSpace(pendingActionId) ? null : pendingActionId,
                metadata));
            traces.Add(trace);
            if (trace.SelectedAction is RuntimeNpcDecisionKind.BasicAttack or RuntimeNpcDecisionKind.ServiceInteraction)
            {
                intents.Add(new RuntimeNpcActionIntent(
                    $"{npc.InstanceId}:{worldTick}:{trace.SelectedAction}",
                    npc.InstanceId,
                    worldTick,
                    trace.SelectedAction,
                    trace.SelectedAction == RuntimeNpcDecisionKind.BasicAttack ? target : null,
                    trace.SelectedAction == RuntimeNpcDecisionKind.ServiceInteraction ? request : null,
                    trace.EvidenceAuthority));
            }
            foreach (var change in trace.StateChanges)
                changes.Add($"{npc.InstanceId}:{change.Key}={change.Value}");
        }
        return new(worldTick, traces.Count, traces, intents, changes);
    }

    private static string Read(IReadOnlyDictionary<string,string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) ? value : string.Empty;

    private static int ReadInt(IReadOnlyDictionary<string,string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : 0;

    private static long? ReadLong(IReadOnlyDictionary<string,string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && long.TryParse(value, out var parsed) ? parsed : null;
}

public sealed partial class RuntimeLivingWorldService
{
    public bool SetNpcExplicitTarget(string instanceId, string? targetId)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current) || !current.Alive) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(targetId)) metadata.Remove("current_target_id");
            else metadata["current_target_id"] = targetId.Trim();
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    public bool QueueNpcInteraction(string instanceId, string? request)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current) || !current.Alive) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(request)) metadata.Remove("interaction_request");
            else metadata["interaction_request"] = request.Trim();
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    public RuntimeNpcAiTickResult TickNpcAi(long worldTick)
    {
        lock (_gate)
        {
            var scheduler = new RuntimeNpcAiScheduler();
            var result = scheduler.Tick(worldTick, _npcs.Values.ToArray());
            var changed = false;
            var intentsByNpc = result.ActionIntents.ToDictionary(x => x.NpcInstanceId, StringComparer.OrdinalIgnoreCase);
            foreach (var trace in result.Decisions)
            {
                if (!_npcs.TryGetValue(trace.NpcInstanceId, out var current)) continue;
                if (trace.SelectedAction is RuntimeNpcDecisionKind.AlreadyProcessed or RuntimeNpcDecisionKind.AwaitingActionCompletion) continue;
                var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    ["last_ai_tick"] = worldTick.ToString(),
                    ["last_ai_decision"] = trace.SelectedAction.ToString(),
                    ["last_ai_evidence"] = trace.EvidenceAuthority
                };
                foreach (var change in trace.StateChanges)
                {
                    if (string.IsNullOrEmpty(change.Value)) metadata.Remove(change.Key);
                    else metadata[change.Key] = change.Value;
                }
                if (intentsByNpc.TryGetValue(trace.NpcInstanceId, out var intent))
                {
                    metadata["pending_ai_action_id"] = intent.ActionId;
                    metadata["pending_ai_action_kind"] = intent.Kind.ToString();
                    if (!string.IsNullOrWhiteSpace(intent.TargetId)) metadata["pending_ai_action_target_id"] = intent.TargetId;
                    else metadata.Remove("pending_ai_action_target_id");
                    if (!string.IsNullOrWhiteSpace(intent.Request)) metadata["pending_ai_action_request"] = intent.Request;
                    else metadata.Remove("pending_ai_action_request");
                }
                if (trace.SelectedAction == RuntimeNpcDecisionKind.ServiceInteraction)
                    metadata.Remove("interaction_request");
                _npcs[trace.NpcInstanceId] = current with { Metadata = metadata };
                changed = true;
            }
            if (changed) Save();
            return result;
        }
    }
    public IReadOnlyList<RuntimeNpcActionIntent> GetPendingNpcActions()
    {
        lock (_gate)
        {
            return _npcs.Values
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(ToPendingIntent)
                .Where(x => x is not null)
                .Cast<RuntimeNpcActionIntent>()
                .ToArray();
        }
    }

    private static RuntimeNpcActionIntent? ToPendingIntent(RuntimeLivingNpcState npc)
    {
        if (!npc.Metadata.TryGetValue("pending_ai_action_id", out var actionId) || string.IsNullOrWhiteSpace(actionId)) return null;
        if (!npc.Metadata.TryGetValue("pending_ai_action_kind", out var kindText) || !Enum.TryParse<RuntimeNpcDecisionKind>(kindText, out var kind)) return null;
        npc.Metadata.TryGetValue("pending_ai_action_target_id", out var target);
        npc.Metadata.TryGetValue("pending_ai_action_request", out var request);
        var tick = actionId.Split(':').Reverse().Skip(1).FirstOrDefault();
        var worldTick = long.TryParse(tick, out var parsed) ? parsed : -1;
        return new(actionId, npc.InstanceId, worldTick, kind, target, request, "persisted-pending-action-ownership");
    }

    public RuntimeNpcActionClaim? ClaimNpcAction(string instanceId, string actionId, string consumerId, long worldTick)
    {
        lock (_gate)
        {
            if (worldTick < 0 || string.IsNullOrWhiteSpace(consumerId) || !_npcs.TryGetValue(instanceId, out var current)) return null;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!metadata.TryGetValue("pending_ai_action_id", out var pending) || !string.Equals(pending, actionId, StringComparison.Ordinal)) return null;
            if (metadata.TryGetValue("pending_ai_action_claim_consumer", out var existing) && !string.IsNullOrWhiteSpace(existing)) return null;
            if (metadata.TryGetValue("pending_ai_action_quarantined", out var quarantined) && string.Equals(quarantined, "true", StringComparison.OrdinalIgnoreCase)) return null;
            var intent = ToPendingIntent(current);
            if (intent is null) return null;
            var normalizedConsumer = consumerId.Trim();
            var claimId = $"{actionId}:claim:{normalizedConsumer}";
            var deliveryCount = metadata.TryGetValue("pending_ai_action_delivery_count", out var countText) && int.TryParse(countText, out var priorCount)
                ? checked(priorCount + 1) : 1;
            metadata["pending_ai_action_claim_consumer"] = normalizedConsumer;
            metadata["pending_ai_action_claim_id"] = claimId;
            metadata["pending_ai_action_delivery_count"] = deliveryCount.ToString();
            metadata["pending_ai_action_claimed_at_tick"] = worldTick.ToString();
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return new RuntimeNpcActionClaim(intent, normalizedConsumer, claimId, deliveryCount, worldTick);
        }
    }

    public IReadOnlyList<RuntimeNpcActionClaim> GetClaimedNpcActions()
    {
        lock (_gate)
        {
            return _npcs.Values
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(ToClaim)
                .Where(x => x is not null)
                .Cast<RuntimeNpcActionClaim>()
                .ToArray();
        }
    }

    private static RuntimeNpcActionClaim? ToClaim(RuntimeLivingNpcState npc)
    {
        var intent = ToPendingIntent(npc);
        if (intent is null || !npc.Metadata.TryGetValue("pending_ai_action_claim_consumer", out var consumer) || string.IsNullOrWhiteSpace(consumer)) return null;
        if (!npc.Metadata.TryGetValue("pending_ai_action_claim_id", out var claimId) || string.IsNullOrWhiteSpace(claimId)) return null;
        var count = npc.Metadata.TryGetValue("pending_ai_action_delivery_count", out var countText) && int.TryParse(countText, out var parsed) ? parsed : 1;
        var claimedAt = npc.Metadata.TryGetValue("pending_ai_action_claimed_at_tick", out var tickText) && long.TryParse(tickText, out var parsedTick) ? parsedTick : -1;
        return new RuntimeNpcActionClaim(intent, consumer, claimId, count, claimedAt);
    }

    public bool ReleaseNpcActionClaim(string instanceId, string actionId, string claimId)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!ClaimMatches(metadata, actionId, claimId)) return false;
            metadata.Remove("pending_ai_action_claim_consumer");
            metadata.Remove("pending_ai_action_claim_id");
            metadata.Remove("pending_ai_action_claimed_at_tick");
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }


    public bool RenewNpcActionClaim(string instanceId, string actionId, string claimId, long worldTick)
    {
        lock (_gate)
        {
            if (worldTick < 0 || !_npcs.TryGetValue(instanceId, out var current)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!ClaimMatches(metadata, actionId, claimId)) return false;
            if (metadata.TryGetValue("pending_ai_action_claimed_at_tick", out var priorText) && long.TryParse(priorText, out var prior) && worldTick < prior) return false;
            metadata["pending_ai_action_claimed_at_tick"] = worldTick.ToString();
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    public IReadOnlyList<string> RecoverExpiredNpcActionClaims(long worldTick, long maxClaimAgeTicks)
    {
        if (worldTick < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
        if (maxClaimAgeTicks < 0) throw new ArgumentOutOfRangeException(nameof(maxClaimAgeTicks));
        lock (_gate)
        {
            var recovered = new List<string>();
            foreach (var instanceId in _npcs.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray())
            {
                var current = _npcs[instanceId];
                var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
                if (!metadata.TryGetValue("pending_ai_action_claim_id", out var claimId) || string.IsNullOrWhiteSpace(claimId)) continue;
                if (!metadata.TryGetValue("pending_ai_action_claimed_at_tick", out var tickText) || !long.TryParse(tickText, out var claimedAt)) continue;
                if (worldTick < claimedAt || worldTick - claimedAt <= maxClaimAgeTicks) continue;
                metadata.Remove("pending_ai_action_claim_consumer");
                metadata.Remove("pending_ai_action_claim_id");
                metadata.Remove("pending_ai_action_claimed_at_tick");
                _npcs[instanceId] = current with { Metadata = metadata };
                recovered.Add(instanceId);
            }
            if (recovered.Count > 0) Save();
            return recovered;
        }
    }


    public bool FailClaimedNpcAction(string instanceId, string actionId, string claimId, string reason, long worldTick, int maxDeliveryAttempts)
    {
        if (worldTick < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
        if (maxDeliveryAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxDeliveryAttempts));
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!ClaimMatches(metadata, actionId, claimId)) return false;
            var deliveryCount = metadata.TryGetValue("pending_ai_action_delivery_count", out var countText) && int.TryParse(countText, out var parsed) ? parsed : 1;
            metadata["pending_ai_action_last_failure"] = string.IsNullOrWhiteSpace(reason) ? "failed" : reason.Trim();
            metadata["pending_ai_action_failed_at_tick"] = worldTick.ToString();
            metadata.Remove("pending_ai_action_claim_consumer");
            metadata.Remove("pending_ai_action_claim_id");
            metadata.Remove("pending_ai_action_claimed_at_tick");
            if (deliveryCount >= maxDeliveryAttempts)
                metadata["pending_ai_action_quarantined"] = "true";
            else
                metadata.Remove("pending_ai_action_quarantined");
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    public IReadOnlyList<RuntimeNpcActionQuarantine> GetQuarantinedNpcActions()
    {
        lock (_gate)
        {
            return _npcs.Values
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(ToQuarantine)
                .Where(x => x is not null)
                .Cast<RuntimeNpcActionQuarantine>()
                .ToArray();
        }
    }

    private static RuntimeNpcActionQuarantine? ToQuarantine(RuntimeLivingNpcState npc)
    {
        if (!npc.Metadata.TryGetValue("pending_ai_action_quarantined", out var quarantined) || !string.Equals(quarantined, "true", StringComparison.OrdinalIgnoreCase)) return null;
        var intent = ToPendingIntent(npc);
        if (intent is null) return null;
        var deliveryCount = npc.Metadata.TryGetValue("pending_ai_action_delivery_count", out var countText) && int.TryParse(countText, out var parsed) ? parsed : 0;
        npc.Metadata.TryGetValue("pending_ai_action_last_failure", out var reason);
        var failedAt = npc.Metadata.TryGetValue("pending_ai_action_failed_at_tick", out var tickText) && long.TryParse(tickText, out var parsedTick) ? parsedTick : -1;
        return new RuntimeNpcActionQuarantine(intent, deliveryCount, reason ?? "failed", failedAt);
    }

    public bool RetryQuarantinedNpcAction(string instanceId, string actionId)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!metadata.TryGetValue("pending_ai_action_id", out var pending) || !string.Equals(pending, actionId, StringComparison.Ordinal)) return false;
            if (!metadata.TryGetValue("pending_ai_action_quarantined", out var quarantined) || !string.Equals(quarantined, "true", StringComparison.OrdinalIgnoreCase)) return false;
            metadata.Remove("pending_ai_action_quarantined");
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    public bool CompleteClaimedNpcAction(string instanceId, string actionId, string claimId, string outcome)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!ClaimMatches(metadata, actionId, claimId)) return false;
            CompleteMetadata(metadata, actionId, outcome);
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    public bool CompleteNpcAction(string instanceId, string actionId, string outcome)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!metadata.TryGetValue("pending_ai_action_id", out var pending) || !string.Equals(pending, actionId, StringComparison.Ordinal)) return false;
            if (metadata.TryGetValue("pending_ai_action_claim_id", out var activeClaim) && !string.IsNullOrWhiteSpace(activeClaim)) return false;
            CompleteMetadata(metadata, actionId, outcome);
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    private static bool ClaimMatches(IReadOnlyDictionary<string,string> metadata, string actionId, string claimId) =>
        metadata.TryGetValue("pending_ai_action_id", out var pending) && string.Equals(pending, actionId, StringComparison.Ordinal) &&
        metadata.TryGetValue("pending_ai_action_claim_id", out var persistedClaim) && string.Equals(persistedClaim, claimId, StringComparison.Ordinal);

    private static void CompleteMetadata(IDictionary<string,string> metadata, string actionId, string outcome)
    {
        metadata["last_ai_action_id"] = actionId;
        metadata["last_ai_action_outcome"] = string.IsNullOrWhiteSpace(outcome) ? "completed" : outcome.Trim();
        metadata.Remove("pending_ai_action_id");
        metadata.Remove("pending_ai_action_kind");
        metadata.Remove("pending_ai_action_target_id");
        metadata.Remove("pending_ai_action_request");
        metadata.Remove("pending_ai_action_claim_consumer");
        metadata.Remove("pending_ai_action_claim_id");
        metadata.Remove("pending_ai_action_claimed_at_tick");
        metadata.Remove("pending_ai_action_delivery_count");
        metadata.Remove("pending_ai_action_last_failure");
        metadata.Remove("pending_ai_action_failed_at_tick");
        metadata.Remove("pending_ai_action_quarantined");
    }

    public bool AcknowledgeNpcAction(string instanceId, string actionId)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!metadata.TryGetValue("pending_ai_action_id", out var pending) ||
                !string.Equals(pending, actionId, StringComparison.Ordinal)) return false;
            if (metadata.TryGetValue("pending_ai_action_claim_id", out var activeClaim) && !string.IsNullOrWhiteSpace(activeClaim)) return false;
            metadata.Remove("pending_ai_action_id");
            metadata.Remove("pending_ai_action_kind");
            metadata.Remove("pending_ai_action_target_id");
            metadata.Remove("pending_ai_action_request");
            metadata.Remove("pending_ai_action_delivery_count");
            metadata.Remove("pending_ai_action_last_failure");
            metadata.Remove("pending_ai_action_failed_at_tick");
            metadata.Remove("pending_ai_action_quarantined");
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

}

public sealed class RuntimeNpcAiSchedulerSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeNpcAiSchedulerSmokeReport Run()
    {
        var report = new RuntimeNpcAiSchedulerSmokeReport();
        try
        {
            var scheduler = new RuntimeNpcAiScheduler();
            RuntimeLivingNpcState Npc(string id, bool alive, string target = "", string request = "", string model = "combat", long? lastTick = null)
            {
                var metadata = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["attack_count"] = "1" };
                if (target.Length > 0) metadata["current_target_id"] = target;
                if (request.Length > 0) metadata["interaction_request"] = request;
                if (lastTick is long processed) metadata["last_ai_tick"] = processed.ToString();
                return new(id, "spawn.1", "npc.1", "Test", "map1-room1", "map1-room1", alive ? 10 : 0, 10, alive,
                    alive ? null : DateTimeOffset.UtcNow.AddMinutes(1), model, 1, metadata);
            }
            var result = scheduler.Tick(42, new[] { Npc("z", true), Npc("a", true, "player.1"), Npc("m", true, request:"buy", model:"shop-service") });
            Require(result.Decisions.Select(x => x.NpcInstanceId).SequenceEqual(new[] { "a", "m", "z" }), "stable-instance-order");
            report.Checks.Add("stable-instance-order");
            Require(result.Decisions[0].SelectedAction == RuntimeNpcDecisionKind.BasicAttack, "explicit-target-scheduled");
            report.Checks.Add("explicit-target-scheduled");
            Require(result.ActionIntents.Count == 2 && result.ActionIntents[0].ActionId == "a:42:BasicAttack"
                && result.ActionIntents[0].TargetId == "player.1", "stable-action-intent");
            report.Checks.Add("stable-action-intent");
            Require(typeof(RuntimeNpcActionClaim).GetProperty("ClaimId") is not null && typeof(RuntimeNpcActionClaim).GetProperty("DeliveryCount") is not null && typeof(RuntimeNpcActionClaim).GetProperty("ClaimedAtWorldTick") is not null && typeof(RuntimeNpcActionQuarantine).GetProperty("FailureReason") is not null, "claim-contract-exposed");
            report.Checks.Add("claim-contract-exposed");
            Require(result.Decisions[1].SelectedAction == RuntimeNpcDecisionKind.ServiceInteraction, "service-request-scheduled");
            report.Checks.Add("service-request-scheduled");
            var replay = scheduler.Tick(42, new[] { Npc("z", true), Npc("a", true, "player.1"), Npc("m", true, request:"buy", model:"shop-service") });
            Require(JsonSerializer.Serialize(result) == JsonSerializer.Serialize(replay), "scheduler-deterministic-replay");
            report.Checks.Add("scheduler-deterministic-replay");
            var duplicate = scheduler.Tick(42, new[] { Npc("a", true, "player.1", lastTick:42) });
            Require(duplicate.Decisions.Single().SelectedAction == RuntimeNpcDecisionKind.AlreadyProcessed && duplicate.ActionIntents.Count == 0, "persisted-duplicate-tick-suppressed");
            report.Checks.Add("persisted-duplicate-tick-suppressed");
            var pendingState = Npc("p", true, "player.2", lastTick:42) with { Metadata = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["attack_count"]="1", ["current_target_id"]="player.2", ["last_ai_tick"]="42", ["pending_ai_action_id"]="p:42:BasicAttack", ["pending_ai_action_kind"]="BasicAttack", ["pending_ai_action_target_id"]="player.2" } };
            var blockedPending = scheduler.Tick(43, new[] { pendingState });
            Require(blockedPending.Decisions.Single().SelectedAction == RuntimeNpcDecisionKind.AwaitingActionCompletion && blockedPending.ActionIntents.Count == 0, "pending-action-prevents-overwrite");
            report.Checks.Add("pending-action-prevents-overwrite");
            var dead = scheduler.Tick(43, new[] { Npc("d", false, "player.1", "buy") });
            Require(dead.Decisions.Single().StateChanges.ContainsKey("current_target_id"), "dead-state-cleanup-scheduled");
            report.Checks.Add("dead-state-cleanup-scheduled");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
