namespace RealmGate.Server;
public static class RuntimeCombatSpellSummoningSmokeReport
{
    private sealed class Sink : IRuntimeCombatSummonEventSink
    {
        public Sink(string id) => SinkId = id;
        public string SinkId { get; }
        public readonly List<string> Calls = new();
        public bool FailOnCommit { get; set; }
        public bool FailOnRollback { get; set; }
        public int ActiveCommits { get; private set; }
        public void Prepare(RuntimeCombatSummonEvent e) => Calls.Add($"prepare:{e.Sequence}:{e.Kind}");
        public void Commit(RuntimeCombatSummonEvent e)
        {
            Calls.Add($"commit:{e.Sequence}:{e.Kind}");
            ActiveCommits++;
            if (FailOnCommit) throw new InvalidOperationException("SUMMON_SMOKE_PARTIAL_COMMIT");
        }
        public void Rollback(RuntimeCombatSummonEvent e)
        {
            Calls.Add($"rollback:{e.Sequence}:{e.Kind}");
            if (FailOnRollback) throw new InvalidOperationException("SUMMON_SMOKE_ROLLBACK_FAILURE");
            ActiveCommits = Math.Max(0, ActiveCommits - 1);
        }
    }
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var order = Enum.GetValues<RuntimeCombatSummonEventKind>();
        var policy = new RuntimeCombatSummonPolicy("summon-policy.promoted.0177", RuntimeCombatSummonOwnerKind.Character,
            true, true, false, false, 2, 4, 20, 1, 30, order,
            Enum.GetValues<RuntimeCombatSummonCleanupReason>(), new[] { "evidence.combat-summon-order.0177" },
            RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved summon ownership or limits");
        var sinks = order.Select(x => (x, (IRuntimeCombatSummonEventSink)new Sink($"sink.{x}"))).ToArray();
        var coordinator = new RuntimeCombatSpellSummoningCoordinator(policy, sinks);
        var request = new RuntimeCombatSummonRequest("summon.smoke.1", "spell.1", "npc.1", "character.1", "room.1", "combat.1", 100, 0, 0, 0, 0, true, true, new[] { "test-vector.summon.0177" });
        var result = coordinator.Execute(request);
        var duplicateRejected = false; try { coordinator.Execute(request); } catch (InvalidOperationException) { duplicateRejected = true; }
        var limitRejected = false; try { new RuntimeCombatSpellSummoningCoordinator(policy, sinks).Execute(request with { SummonId = "summon.smoke.2", ExistingOwned = 2 }); } catch (InvalidOperationException) { limitRejected = true; }
        var retrySinks = order.Select(x => (x, (IRuntimeCombatSummonEventSink)new Sink($"retry.{x}"))).ToArray();
        var retryCoordinator = new RuntimeCombatSpellSummoningCoordinator(policy, retrySinks);
        var partial = (Sink)retrySinks[3].Item2;
        partial.FailOnCommit = true;
        var failureObserved = false;
        var failedRequest = request with { SummonId = "summon.smoke.partial" };
        try { retryCoordinator.Execute(failedRequest); }
        catch (InvalidOperationException ex) when (ex.Message == "SUMMON_SMOKE_PARTIAL_COMMIT")
        {
            failureObserved = true;
        }
        var rollbackComplete = retrySinks.All(x => ((Sink)x.Item2).ActiveCommits == 0);
        partial.FailOnCommit = false;
        var retrySucceeded = retryCoordinator.Execute(failedRequest).Committed;

        var uncertainSinks = order.Select(x => (x, (IRuntimeCombatSummonEventSink)new Sink($"uncertain.{x}"))).ToArray();
        ((Sink)uncertainSinks[3].Item2).FailOnCommit = true;
        ((Sink)uncertainSinks[1].Item2).FailOnRollback = true;
        var uncertainCoordinator = new RuntimeCombatSpellSummoningCoordinator(policy, uncertainSinks);
        var uncertainRequest = request with { SummonId = "summon.smoke.uncertain" };
        var reportedUncertain = false;
        try { uncertainCoordinator.Execute(uncertainRequest); }
        catch (AggregateException ex) when (ex.Message.Contains("COMBAT_SUMMON_ROLLBACK_UNCERTAIN", StringComparison.Ordinal))
        {
            reportedUncertain = true;
        }
        var unsafeRetryRejected = false;
        try { uncertainCoordinator.Execute(uncertainRequest); }
        catch (InvalidOperationException ex) when (ex.Message == "COMBAT_SUMMON_ALREADY_COMPLETED")
        {
            unsafeRetryRejected = true;
        }

        var overflowCoordinator = new RuntimeCombatSpellSummoningCoordinator(policy, sinks);
        var overflowRequest = request with { SummonId = "summon.smoke.overflow", WorldTick = long.MaxValue };
        var overflowObserved = false;
        try { overflowCoordinator.Execute(overflowRequest); }
        catch (OverflowException) { overflowObserved = true; }
        var overflowRetrySucceeded = overflowCoordinator.Execute(overflowRequest with { WorldTick = 100 }).Committed;

        return new Dictionary<string, bool>
        {
            ["committed"] = result.Committed,
            ["ordered"] = result.Events.Select(x => x.Kind).SequenceEqual(order),
            ["expires"] = result.ExpiresAtTick == 130,
            ["combat_inserted"] = result.InsertedIntoCombat,
            ["threat_inherited"] = result.ThreatInherited,
            ["duplicate_rejected"] = duplicateRejected,
            ["limit_rejected"] = limitRejected,
            ["partial_commit_rollback_and_safe_retry"] = failureObserved && rollbackComplete && retrySucceeded,
            ["rollback_failure_reports_uncertain_and_blocks_retry"] = reportedUncertain && unsafeRetryRejected,
            ["overflow_does_not_reserve_summon_id"] = overflowObserved && overflowRetrySucceeded
        };
    }
}
