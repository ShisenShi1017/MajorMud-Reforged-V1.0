using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeClassicNarrativePersistenceState { Prepared, Executing, Committed, RolledBack }

public sealed record RuntimeClassicNarrativePersistenceRecord(
    string IdempotencyKey,
    string CharacterId,
    string WorldId,
    int RootTextblockId,
    int MatchedTextblockId,
    RuntimeClassicNarrativePersistenceState State,
    string OperationDigest,
    string? ResultJson,
    DateTimeOffset UpdatedUtc);

public interface IRuntimeClassicNarrativePersistenceStore
{
    RuntimeClassicNarrativePersistenceRecord? Read(string idempotencyKey);
    void Write(RuntimeClassicNarrativePersistenceRecord record);
}

public sealed record RuntimeClassicNarrativePersistentExecutionResult(
    bool Succeeded,
    bool ReplayedCommittedResult,
    string IdempotencyKey,
    RuntimeClassicNarrativeExecutionResult? Execution,
    string? Failure);

/// <summary>
/// Phase F15 persistence/idempotency boundary. Bundle F creates deterministic execution keys
/// and coordinates reconciliation; Bundle G remains the sole durable storage owner.
/// </summary>
public sealed class RuntimeClassicNarrativePersistenceAuthority
{
    private readonly IRuntimeClassicNarrativePersistenceStore _store;
    private readonly RuntimeClassicNarrativeExecutionCoordinator _coordinator;

    public RuntimeClassicNarrativePersistenceAuthority(
        IRuntimeClassicNarrativePersistenceStore store,
        RuntimeClassicNarrativeExecutionCoordinator coordinator)
    { _store = store ?? throw new ArgumentNullException(nameof(store)); _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator)); }

    public RuntimeClassicNarrativePersistentExecutionResult Execute(
        RuntimeClassicTextblockExecutionPlan plan,
        RuntimeClassicNarrativeExecutionContext context)
    {
        var digest = ComputeDigest(plan);
        var key = $"{context.WorldId}/{context.CharacterId}/{plan.RootTextblockId}/{plan.MatchedTextblockId}/{digest}";
        var existing = _store.Read(key);
        if (existing?.State == RuntimeClassicNarrativePersistenceState.Committed)
        {
            var replay = string.IsNullOrWhiteSpace(existing.ResultJson) ? null : JsonSerializer.Deserialize<RuntimeClassicNarrativeExecutionResult>(existing.ResultJson);
            return new(true, true, key, replay, null);
        }
        if (existing is not null && existing.State is RuntimeClassicNarrativePersistenceState.Prepared or RuntimeClassicNarrativePersistenceState.Executing)
            return new(false, false, key, null, "Interrupted narrative transaction requires Bundle G reconciliation before retry.");

        var now = DateTimeOffset.UtcNow;
        _store.Write(new(key, context.CharacterId, context.WorldId, plan.RootTextblockId, plan.MatchedTextblockId,
            RuntimeClassicNarrativePersistenceState.Prepared, digest, null, now));
        _store.Write(new(key, context.CharacterId, context.WorldId, plan.RootTextblockId, plan.MatchedTextblockId,
            RuntimeClassicNarrativePersistenceState.Executing, digest, null, DateTimeOffset.UtcNow));
        var result = _coordinator.Execute(plan, context);
        if (!result.Succeeded)
        {
            _store.Write(new(key, context.CharacterId, context.WorldId, plan.RootTextblockId, plan.MatchedTextblockId,
                RuntimeClassicNarrativePersistenceState.RolledBack, digest, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
            return new(false, false, key, result, result.Failure);
        }
        _store.Write(new(key, context.CharacterId, context.WorldId, plan.RootTextblockId, plan.MatchedTextblockId,
            RuntimeClassicNarrativePersistenceState.Committed, digest, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        return new(true, false, key, result, null);
    }

    public static string ComputeDigest(RuntimeClassicTextblockExecutionPlan plan)
    {
        var canonical = string.Join("\n", plan.Operations.OrderBy(x => x.Sequence).ThenBy(x => x.Line)
            .Select(x => $"{x.Sequence}|{x.Line}|{x.Opcode.ToLowerInvariant()}|{string.Join('|', x.Arguments)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public sealed record RuntimeBundleFPhaseF15AuditReport(
    bool Passed, int ContractCount, int RewardOperationCount, int MutatingOperationCount,
    int DuplicateReplayMutationCount, int InterruptedExecutionMutationCount, int DisconnectCount,
    IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF15Audit
{
    public static RuntimeBundleFPhaseF15AuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var path = Path.Combine(projectRoot, "Data", "Classic", "narrative_persistence_authority.json");
        if (!File.Exists(path)) return new(false,0,0,0,0,0,1,checks,["narrative-persistence-authority-missing"]);
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        Check(root.GetProperty("schema").GetString() == "classic-narrative-persistence-authority.v1", "persistence-schema-authoritative");
        Check(root.GetProperty("storage_owner").GetString() == "Bundle G", "bundle-g-remains-storage-owner");
        Check(root.GetProperty("orchestration_owner").GetString() == "Bundle F", "bundle-f-remains-orchestration-owner");
        Check(root.GetProperty("fail_closed").GetBoolean(), "unreconciled-state-fails-closed");
        var validation = root.GetProperty("validation");
        foreach (var name in new[]{"all_mutations_atomic","all_operations_have_duplicate_policy","interrupted_transactions_reconcile","logout_rollback_required","restart_reconciliation_required","committed_result_replay_without_mutation"})
            Check(validation.GetProperty(name).GetBoolean(), name.Replace('_','-'));
        var contracts = root.GetProperty("contracts").EnumerateArray().ToArray();
        Check(contracts.All(x => !string.IsNullOrWhiteSpace(x.GetProperty("duplicate_guard").GetString())), "all-persistence-contracts-have-duplicate-guards");
        var rewardsPath = Path.Combine(projectRoot, "Data", "Classic", "reward_execution_authority.json");
        using var rewardsDoc = JsonDocument.Parse(File.ReadAllText(rewardsPath));
        var rewards = rewardsDoc.RootElement.GetProperty("records").EnumerateArray().ToArray();
        Check(rewards.All(x => !x.GetProperty("mutates_state").GetBoolean() || x.GetProperty("requires_atomic_boundary").GetBoolean()), "all-mutating-rewards-atomic");
        Check(rewards.All(x => !string.IsNullOrWhiteSpace(x.GetProperty("duplicate_policy").GetString())), "all-rewards-have-duplicate-policy");

        var store = new AuditStore(); var coordinator = AuditCoordinator.Create(out var session, out var context, out var plan);
        var authority = new RuntimeClassicNarrativePersistenceAuthority(store, coordinator);
        var first = authority.Execute(plan, context); var beforeReplay = session.MutationCount;
        var replay = authority.Execute(plan, context); var duplicateMutations = session.MutationCount - beforeReplay;
        Check(first.Succeeded && !first.ReplayedCommittedResult, "first-execution-commits");
        Check(replay.Succeeded && replay.ReplayedCommittedResult && duplicateMutations == 0, "committed-retry-does-not-repeat-mutations");
        var interruptedStore = new AuditStore(); var key = $"{context.WorldId}/{context.CharacterId}/{plan.RootTextblockId}/{plan.MatchedTextblockId}/{RuntimeClassicNarrativePersistenceAuthority.ComputeDigest(plan)}";
        interruptedStore.Write(new(key, context.CharacterId, context.WorldId, plan.RootTextblockId, plan.MatchedTextblockId, RuntimeClassicNarrativePersistenceState.Executing,
            RuntimeClassicNarrativePersistenceAuthority.ComputeDigest(plan), null, DateTimeOffset.UtcNow));
        var interruptedAuthority = new RuntimeClassicNarrativePersistenceAuthority(interruptedStore, coordinator); var beforeInterrupted = session.MutationCount;
        var interrupted = interruptedAuthority.Execute(plan, context); var interruptedMutations = session.MutationCount - beforeInterrupted;
        Check(!interrupted.Succeeded && interruptedMutations == 0, "interrupted-execution-fails-closed-before-mutation");
        Check(failures.Count == 0, "zero-phase-f15-disconnects");
        return new(failures.Count==0, contracts.Length, rewards.Length, rewards.Count(x=>x.GetProperty("mutates_state").GetBoolean()), duplicateMutations, interruptedMutations, failures.Count, checks, failures);
    }

    private sealed class AuditStore : IRuntimeClassicNarrativePersistenceStore
    { private readonly Dictionary<string,RuntimeClassicNarrativePersistenceRecord> _rows=[]; public RuntimeClassicNarrativePersistenceRecord? Read(string key)=>_rows.GetValueOrDefault(key); public void Write(RuntimeClassicNarrativePersistenceRecord row)=>_rows[row.IdempotencyKey]=row; }

    private static class AuditCoordinator
    {
        public static RuntimeClassicNarrativeExecutionCoordinator Create(out AuditSession session, out RuntimeClassicNarrativeExecutionContext context, out RuntimeClassicTextblockExecutionPlan plan)
        {
            context = new("f15-character","f15-viewer","classic",335,"audit"); session = new(context);
            var registry = new RuntimeClassicNarrativeLiveSessionRegistry(); registry.Attach(session);
            var coordinator = new RuntimeClassicNarrativeExecutionCoordinator(new RuntimeClassicNarrativeOperationDispatcher(), new RuntimeClassicNarrativeLiveTransactionFactory(registry), RuntimeClassicNarrativeLiveBindingFactory.Create(registry));
            var op = new RuntimeClassicTextblockOperation(1,1,"giveability 1 1","giveability","mutation","verified",["1","1"],[1,1],null,null,null,null,null,null,null);
            plan = new RuntimeClassicTextblockExecutionPlan(335,335,1,"audit",[op],Array.Empty<int>(),null); return coordinator;
        }
    }
    private sealed class AuditSession : IRuntimeClassicNarrativeLiveSession
    {
        public AuditSession(RuntimeClassicNarrativeExecutionContext context)=>ExecutionContext=context; public RuntimeClassicNarrativeExecutionContext ExecutionContext{get;} public int MutationCount{get;private set;}
        public object CaptureAuthoritativeState()=>MutationCount; public void RestoreAuthoritativeState(object snapshot)=>MutationCount=(int)snapshot; public void CommitAuthoritativeState(){} public bool EvaluateCondition(RuntimeClassicTextblockOperation o,out string? f){f=null;return true;} public string? ResolvePresentation(RuntimeClassicTextblockOperation o)=>null;
        public RuntimeClassicNarrativeOperationResult ExecuteCharacterMutation(RuntimeClassicTextblockOperation o){MutationCount++;return RuntimeClassicNarrativeLiveBindingFactory.Result(o,RuntimeClassicNarrativeOperationOwner.BundleACharacter,true);} public RuntimeClassicNarrativeOperationResult ExecuteCombatMutation(RuntimeClassicTextblockOperation o)=>throw new NotSupportedException(); public RuntimeClassicNarrativeOperationResult ExecuteItemEconomyMutation(RuntimeClassicTextblockOperation o)=>throw new NotSupportedException(); public RuntimeClassicNarrativeOperationResult ExecuteRoomWorldMutation(RuntimeClassicTextblockOperation o)=>throw new NotSupportedException(); public RuntimeClassicNarrativeOperationResult ExecuteNpcSpawnMutation(RuntimeClassicTextblockOperation o)=>throw new NotSupportedException(); public RuntimeClassicNarrativeOperationResult ExecuteControl(RuntimeClassicTextblockOperation o)=>RuntimeClassicNarrativeLiveBindingFactory.Result(o,RuntimeClassicNarrativeOperationOwner.BundleFControl,true);
    }
}
