using System.Text.Json;

namespace Reforged.Server;

public interface IRuntimeClassicNarrativeLiveSession
{
    RuntimeClassicNarrativeExecutionContext ExecutionContext { get; }
    object CaptureAuthoritativeState();
    void RestoreAuthoritativeState(object snapshot);
    void CommitAuthoritativeState();
    bool EvaluateCondition(RuntimeClassicTextblockOperation operation, out string? failure);
    string? ResolvePresentation(RuntimeClassicTextblockOperation operation);
    RuntimeClassicNarrativeOperationResult ExecuteCharacterMutation(RuntimeClassicTextblockOperation operation);
    RuntimeClassicNarrativeOperationResult ExecuteCombatMutation(RuntimeClassicTextblockOperation operation);
    RuntimeClassicNarrativeOperationResult ExecuteItemEconomyMutation(RuntimeClassicTextblockOperation operation);
    RuntimeClassicNarrativeOperationResult ExecuteRoomWorldMutation(RuntimeClassicTextblockOperation operation);
    RuntimeClassicNarrativeOperationResult ExecuteNpcSpawnMutation(RuntimeClassicTextblockOperation operation);
    RuntimeClassicNarrativeOperationResult ExecuteControl(RuntimeClassicTextblockOperation operation);
}

/// <summary>
/// Resolves the live authoritative session for a narrative execution. The registry stores no
/// character/world state and therefore cannot become a parallel quest or persistence engine.
/// </summary>
public sealed class RuntimeClassicNarrativeLiveSessionRegistry
{
    private readonly Dictionary<string, IRuntimeClassicNarrativeLiveSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private static string Key(RuntimeClassicNarrativeExecutionContext c) => $"{c.WorldId}\n{c.CharacterId}\n{c.ViewerId}";

    public void Attach(IRuntimeClassicNarrativeLiveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var key = Key(session.ExecutionContext);
        if (!_sessions.TryAdd(key, session)) throw new InvalidOperationException("Classic narrative live session is already attached.");
    }

    public void Detach(RuntimeClassicNarrativeExecutionContext context) => _sessions.Remove(Key(context));

    public IRuntimeClassicNarrativeLiveSession Require(RuntimeClassicNarrativeExecutionContext context) =>
        _sessions.TryGetValue(Key(context), out var session)
            ? session
            : throw new InvalidOperationException("No authoritative live session is attached for Classic narrative execution.");
}

public sealed class RuntimeClassicNarrativeLiveTransactionFactory : IRuntimeClassicNarrativeTransactionFactory
{
    private readonly RuntimeClassicNarrativeLiveSessionRegistry _registry;
    public RuntimeClassicNarrativeLiveTransactionFactory(RuntimeClassicNarrativeLiveSessionRegistry registry) => _registry = registry;

    public IRuntimeClassicNarrativeTransaction Begin(RuntimeClassicNarrativeExecutionContext context)
    {
        var session = _registry.Require(context);
        return new Transaction(session, session.CaptureAuthoritativeState());
    }

    private sealed class Transaction : IRuntimeClassicNarrativeTransaction
    {
        private readonly IRuntimeClassicNarrativeLiveSession _session;
        private readonly object _snapshot;
        private bool _closed;
        public Transaction(IRuntimeClassicNarrativeLiveSession session, object snapshot) { _session = session; _snapshot = snapshot; }
        public void Commit() { EnsureOpen(); _session.CommitAuthoritativeState(); _closed = true; }
        public void Rollback() { EnsureOpen(); _session.RestoreAuthoritativeState(_snapshot); _closed = true; }
        public void Dispose() { if (!_closed) Rollback(); }
        private void EnsureOpen() { if (_closed) throw new InvalidOperationException("Classic narrative transaction is already closed."); }
    }
}

public static class RuntimeClassicNarrativeLiveBindingFactory
{
    public static IReadOnlyList<IRuntimeClassicNarrativeOperationBinding> Create(RuntimeClassicNarrativeLiveSessionRegistry registry) =>
    [
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleACharacter, registry, (s,o) => s.ExecuteCharacterMutation(o)),
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleBCombat, registry, (s,o) => s.ExecuteCombatMutation(o)),
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleCItemsEconomy, registry, (s,o) => s.ExecuteItemEconomyMutation(o)),
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleDRoomWorld, registry, (s,o) => s.ExecuteRoomWorldMutation(o)),
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleENpcSpawn, registry, (s,o) => s.ExecuteNpcSpawnMutation(o)),
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleFCondition, registry, ExecuteCondition),
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleFPresentation, registry, ExecutePresentation),
        new Binding(RuntimeClassicNarrativeOperationOwner.BundleFControl, registry, (s,o) => s.ExecuteControl(o))
    ];

    private static RuntimeClassicNarrativeOperationResult ExecuteCondition(IRuntimeClassicNarrativeLiveSession session, RuntimeClassicTextblockOperation operation)
    {
        var passed = session.EvaluateCondition(operation, out var failure);
        return Result(operation, RuntimeClassicNarrativeOperationOwner.BundleFCondition, passed, false, null, passed ? null : failure ?? $"Condition '{operation.Opcode}' failed.");
    }

    private static RuntimeClassicNarrativeOperationResult ExecutePresentation(IRuntimeClassicNarrativeLiveSession session, RuntimeClassicTextblockOperation operation) =>
        Result(operation, RuntimeClassicNarrativeOperationOwner.BundleFPresentation, true, false, session.ResolvePresentation(operation), null);

    public static RuntimeClassicNarrativeOperationResult Result(RuntimeClassicTextblockOperation operation, RuntimeClassicNarrativeOperationOwner owner,
        bool succeeded, bool stopRequested = false, string? output = null, string? failure = null) =>
        new(operation.Sequence, operation.Opcode, owner, succeeded, stopRequested, output, failure);

    private sealed class Binding : IRuntimeClassicNarrativeOperationBinding
    {
        private readonly RuntimeClassicNarrativeLiveSessionRegistry _registry;
        private readonly Func<IRuntimeClassicNarrativeLiveSession, RuntimeClassicTextblockOperation, RuntimeClassicNarrativeOperationResult> _execute;
        public Binding(RuntimeClassicNarrativeOperationOwner owner, RuntimeClassicNarrativeLiveSessionRegistry registry,
            Func<IRuntimeClassicNarrativeLiveSession, RuntimeClassicTextblockOperation, RuntimeClassicNarrativeOperationResult> execute)
        { Owner = owner; _registry = registry; _execute = execute; }
        public RuntimeClassicNarrativeOperationOwner Owner { get; }
        public RuntimeClassicNarrativeOperationResult Execute(RuntimeClassicTextblockOperation operation, RuntimeClassicNarrativeExecutionContext context)
        {
            var result = _execute(_registry.Require(context), operation);
            if (result.Owner != Owner) throw new InvalidOperationException($"Live binding for {Owner} returned owner {result.Owner}.");
            return result;
        }
    }
}

public sealed record RuntimeBundleFPhaseF4LiveBindingAuditReport(bool Passed, int OwnerCount, int MutationCount, int CommitCount, int RollbackCount,
    IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF4LiveBindingAudit
{
    public static RuntimeBundleFPhaseF4LiveBindingAuditReport Run()
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string name) { if (value) checks.Add(name); else failures.Add(name); }
        var registry = new RuntimeClassicNarrativeLiveSessionRegistry();
        var context = new RuntimeClassicNarrativeExecutionContext("character.audit", "viewer.audit", "classic", 335, "audit");
        var session = new AuditSession(context); registry.Attach(session);
        var dispatcher = new RuntimeClassicNarrativeOperationDispatcher();
        var coordinator = new RuntimeClassicNarrativeExecutionCoordinator(dispatcher,
            new RuntimeClassicNarrativeLiveTransactionFactory(registry), RuntimeClassicNarrativeLiveBindingFactory.Create(registry));

        var operations = new[]
        {
            Op(1, "minlevel", "condition"), Op(2, "message", "presentation"),
            Op(3, "giveability", "mutation", "1", "1"), Op(4, "giveitem", "mutation", "2"),
            Op(5, "cast", "mutation", "3"), Op(6, "teleport", "mutation", "4", "1"), Op(7, "summon", "mutation", "5"),
            Op(8, "break", "control")
        };
        var plan = new RuntimeClassicTextblockExecutionPlan(335, 335, 1, "audit", operations, Array.Empty<int>(), null);
        var ok = coordinator.Execute(plan, context);
        Check(ok.Succeeded && ok.TransactionCommitted && !ok.TransactionRolledBack, "live-owner-plan-commits");
        Check(session.MutationOwners.SetEquals(new[] { RuntimeClassicNarrativeOperationOwner.BundleACharacter, RuntimeClassicNarrativeOperationOwner.BundleBCombat,
            RuntimeClassicNarrativeOperationOwner.BundleCItemsEconomy, RuntimeClassicNarrativeOperationOwner.BundleDRoomWorld, RuntimeClassicNarrativeOperationOwner.BundleENpcSpawn }), "all-bundle-a-e-live-owners-invoked");
        Check(ok.Output.Single() == "decoded-message.audit", "presentation-resolved-by-live-session");
        Check(ok.Operations.Last().StopRequested, "control-stop-honored");

        session.FailOpcode = "giveitem";
        var failed = coordinator.Execute(plan, context);
        Check(!failed.Succeeded && failed.TransactionRolledBack && !failed.TransactionCommitted, "live-owner-failure-rolls-back");
        Check(session.RestoreCount == 1, "authoritative-state-restored-on-failure");
        registry.Detach(context);
        var detached = coordinator.Execute(plan, context);
        Check(!detached.Succeeded && detached.Failure is not null && detached.Failure.Contains("No authoritative live session", StringComparison.Ordinal), "detached-session-fails-closed");

        return new(failures.Count == 0, RuntimeClassicNarrativeLiveBindingFactory.Create(new()).Count, session.MutationCount,
            session.CommitCount, session.RestoreCount, checks, failures);
    }

    private static RuntimeClassicTextblockOperation Op(int sequence, string opcode, string kind, params string[] args) =>
        new(1, sequence, opcode + " " + string.Join(' ', args), opcode, kind, "verified", args,
            args.Select(x => int.TryParse(x, out var n) ? n : 0).ToArray(), null, null, null, null, null, null, null);

    private sealed class AuditSession : IRuntimeClassicNarrativeLiveSession
    {
        public AuditSession(RuntimeClassicNarrativeExecutionContext context) => ExecutionContext = context;
        public RuntimeClassicNarrativeExecutionContext ExecutionContext { get; }
        public HashSet<RuntimeClassicNarrativeOperationOwner> MutationOwners { get; } = [];
        public int MutationCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RestoreCount { get; private set; }
        public string? FailOpcode { get; set; }
        public object CaptureAuthoritativeState() => MutationCount;
        public void RestoreAuthoritativeState(object snapshot) { MutationCount = (int)snapshot; RestoreCount++; }
        public void CommitAuthoritativeState() => CommitCount++;
        public bool EvaluateCondition(RuntimeClassicTextblockOperation operation, out string? failure) { failure = null; return true; }
        public string? ResolvePresentation(RuntimeClassicTextblockOperation operation) => "decoded-message.audit";
        public RuntimeClassicNarrativeOperationResult ExecuteCharacterMutation(RuntimeClassicTextblockOperation o) => Mutate(o, RuntimeClassicNarrativeOperationOwner.BundleACharacter);
        public RuntimeClassicNarrativeOperationResult ExecuteCombatMutation(RuntimeClassicTextblockOperation o) => Mutate(o, RuntimeClassicNarrativeOperationOwner.BundleBCombat);
        public RuntimeClassicNarrativeOperationResult ExecuteItemEconomyMutation(RuntimeClassicTextblockOperation o) => Mutate(o, RuntimeClassicNarrativeOperationOwner.BundleCItemsEconomy);
        public RuntimeClassicNarrativeOperationResult ExecuteRoomWorldMutation(RuntimeClassicTextblockOperation o) => Mutate(o, RuntimeClassicNarrativeOperationOwner.BundleDRoomWorld);
        public RuntimeClassicNarrativeOperationResult ExecuteNpcSpawnMutation(RuntimeClassicTextblockOperation o) => Mutate(o, RuntimeClassicNarrativeOperationOwner.BundleENpcSpawn);
        public RuntimeClassicNarrativeOperationResult ExecuteControl(RuntimeClassicTextblockOperation o) => RuntimeClassicNarrativeLiveBindingFactory.Result(o,
            RuntimeClassicNarrativeOperationOwner.BundleFControl, true, o.Opcode.Equals("break", StringComparison.OrdinalIgnoreCase));
        private RuntimeClassicNarrativeOperationResult Mutate(RuntimeClassicTextblockOperation o, RuntimeClassicNarrativeOperationOwner owner)
        {
            MutationOwners.Add(owner); MutationCount++;
            var fail = o.Opcode.Equals(FailOpcode, StringComparison.OrdinalIgnoreCase);
            return RuntimeClassicNarrativeLiveBindingFactory.Result(o, owner, !fail, failure: fail ? "Injected live-owner failure." : null);
        }
    }
}
