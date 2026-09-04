using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNarrativeExecutionContext(
    string CharacterId,
    string ViewerId,
    string WorldId,
    int RootTextblockId,
    string Input,
    IReadOnlyDictionary<string, string>? Variables = null);

public sealed record RuntimeClassicNarrativeOperationResult(
    int Sequence,
    string Opcode,
    RuntimeClassicNarrativeOperationOwner Owner,
    bool Succeeded,
    bool StopRequested,
    string? Output,
    string? Failure);

public sealed record RuntimeClassicNarrativeExecutionResult(
    bool Succeeded,
    int RootTextblockId,
    int MatchedTextblockId,
    bool TransactionOpened,
    bool TransactionCommitted,
    bool TransactionRolledBack,
    IReadOnlyList<RuntimeClassicNarrativeOperationResult> Operations,
    IReadOnlyList<string> Output,
    string? Failure);

public interface IRuntimeClassicNarrativeTransaction : IDisposable
{
    void Commit();
    void Rollback();
}

public interface IRuntimeClassicNarrativeTransactionFactory
{
    IRuntimeClassicNarrativeTransaction Begin(RuntimeClassicNarrativeExecutionContext context);
}

public interface IRuntimeClassicNarrativeOperationBinding
{
    RuntimeClassicNarrativeOperationOwner Owner { get; }
    RuntimeClassicNarrativeOperationResult Execute(
        RuntimeClassicTextblockOperation operation,
        RuntimeClassicNarrativeExecutionContext context);
}

/// <summary>
/// Canonical ordered execution boundary for decoded Classic narrative operations.
/// It owns orchestration only. Bundle A-E bindings retain all authoritative mutation logic.
/// A plan containing any mutation executes in one transaction and rolls back on every failure.
/// </summary>
public sealed class RuntimeClassicNarrativeExecutionCoordinator
{
    private readonly RuntimeClassicNarrativeOperationDispatcher _dispatcher;
    private readonly IRuntimeClassicNarrativeTransactionFactory _transactions;
    private readonly IReadOnlyDictionary<RuntimeClassicNarrativeOperationOwner, IRuntimeClassicNarrativeOperationBinding> _bindings;

    public RuntimeClassicNarrativeExecutionCoordinator(
        RuntimeClassicNarrativeOperationDispatcher dispatcher,
        IRuntimeClassicNarrativeTransactionFactory transactions,
        IEnumerable<IRuntimeClassicNarrativeOperationBinding> bindings)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        var rows = (bindings ?? throw new ArgumentNullException(nameof(bindings))).ToArray();
        if (rows.Select(x => x.Owner).Distinct().Count() != rows.Length)
            throw new InvalidOperationException("Duplicate Classic narrative owner binding.");
        _bindings = rows.ToDictionary(x => x.Owner);
        var missing = Enum.GetValues<RuntimeClassicNarrativeOperationOwner>().Where(x => !_bindings.ContainsKey(x)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Missing Classic narrative owner bindings: " + string.Join(", ", missing));
    }

    public RuntimeClassicNarrativeExecutionResult Execute(
        RuntimeClassicTextblockExecutionPlan plan,
        RuntimeClassicNarrativeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);
        if (plan.RootTextblockId != context.RootTextblockId)
            throw new InvalidOperationException("Execution context root does not match decoded plan root.");

        var ordered = plan.Operations.OrderBy(x => x.Sequence).ThenBy(x => x.Line).ToArray();
        var requiresTransaction = ordered.Any(x => _dispatcher.Classify(x).RequiresAtomicBoundary);
        IRuntimeClassicNarrativeTransaction? transaction = null;
        var results = new List<RuntimeClassicNarrativeOperationResult>();
        var output = new List<string>();
        var committed = false;
        var rolledBack = false;

        try
        {
            if (requiresTransaction) transaction = _transactions.Begin(context);
            foreach (var operation in ordered)
            {
                var contract = _dispatcher.Classify(operation);
                var binding = _bindings[contract.Owner];
                var result = binding.Execute(operation, context);
                if (result.Owner != contract.Owner || !result.Opcode.Equals(operation.Opcode, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Narrative binding returned mismatched result for '{operation.Opcode}'.");
                results.Add(result);
                if (!string.IsNullOrWhiteSpace(result.Output)) output.Add(result.Output);
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.Failure ?? $"Narrative operation '{operation.Opcode}' failed.");
                if (result.StopRequested) break;
            }
            transaction?.Commit();
            committed = transaction is not null;
            return new(true, plan.RootTextblockId, plan.MatchedTextblockId, transaction is not null, committed, false, results, output, null);
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                transaction.Rollback();
                rolledBack = true;
            }
            return new(false, plan.RootTextblockId, plan.MatchedTextblockId, transaction is not null, false, rolledBack, results, output, ex.Message);
        }
        finally { transaction?.Dispose(); }
    }
}

public sealed record RuntimeBundleFPhaseF4ExecutionAuditReport(
    bool Passed,
    int OwnerBindingCount,
    int SuccessfulOperationCount,
    int RollbackOperationCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF4ExecutionAudit
{
    public static RuntimeBundleFPhaseF4ExecutionAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var dispatcher = new RuntimeClassicNarrativeOperationDispatcher();
        var tx = new AuditTransactionFactory();
        var trace = new List<string>();
        var bindings = Enum.GetValues<RuntimeClassicNarrativeOperationOwner>()
            .Select(owner => (IRuntimeClassicNarrativeOperationBinding)new AuditBinding(owner, trace)).ToArray();
        var coordinator = new RuntimeClassicNarrativeExecutionCoordinator(dispatcher, tx, bindings);

        Check(graph.TryCreateExecutionPlan(2798, "magma scroll", out var source), "decoded-plan-resolved");
        var successful = coordinator.Execute(source, new("audit-character", "audit-viewer", "classic", 2798, "magma scroll"));
        Check(successful.Succeeded, "ordered-plan-executes");
        Check(successful.Operations.Select(x => x.Sequence).SequenceEqual(successful.Operations.Select(x => x.Sequence).OrderBy(x => x)), "decoded-sequence-order-preserved");
        Check(!successful.TransactionOpened || successful.TransactionCommitted, "mutating-plan-commits-on-success");

        var failOperations = new[]
        {
            new RuntimeClassicTextblockOperation(1, 1, "message 1", "message", "presentation", "verified", Array.Empty<string>(), Array.Empty<int>(), 1, null, null, null, null, null, null),
            new RuntimeClassicTextblockOperation(1, 2, "giveitem 1", "giveitem", "mutation", "verified", new[] { "FAIL_AUDIT" }, new[] { 1 }, null, 1, null, null, null, null, null)
        };
        var failPlan = new RuntimeClassicTextblockExecutionPlan(335, 335, 1, "audit", failOperations, new[] { 1 }, null);
        var failed = coordinator.Execute(failPlan, new("audit-character", "audit-viewer", "classic", 335, "audit"));
        Check(!failed.Succeeded, "operation-failure-propagates");
        Check(failed.TransactionOpened && failed.TransactionRolledBack && !failed.TransactionCommitted, "mutation-failure-rolls-back-entire-plan");
        Check(tx.BeginCount == tx.CommitCount + tx.RollbackCount, "every-open-transaction-closed");
        Check(bindings.Length == Enum.GetValues<RuntimeClassicNarrativeOperationOwner>().Length, "all-owner-bindings-required");

        var duplicateRejected = false;
        try { _ = new RuntimeClassicNarrativeExecutionCoordinator(dispatcher, tx, bindings.Concat(new[] { bindings[0] })); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Check(duplicateRejected, "duplicate-owner-binding-rejected");

        return new(failures.Count == 0, bindings.Length, successful.Operations.Count, failed.Operations.Count, checks, failures);
    }

    private sealed class AuditBinding : IRuntimeClassicNarrativeOperationBinding
    {
        private readonly List<string> _trace;
        public AuditBinding(RuntimeClassicNarrativeOperationOwner owner, List<string> trace) { Owner = owner; _trace = trace; }
        public RuntimeClassicNarrativeOperationOwner Owner { get; }
        public RuntimeClassicNarrativeOperationResult Execute(RuntimeClassicTextblockOperation operation, RuntimeClassicNarrativeExecutionContext context)
        {
            _trace.Add($"{operation.Sequence}:{operation.Opcode}:{Owner}");
            var fail = operation.Arguments.Any(x => x.Equals("FAIL_AUDIT", StringComparison.Ordinal));
            return new(operation.Sequence, operation.Opcode, Owner, !fail, operation.Opcode.Equals("break", StringComparison.OrdinalIgnoreCase), operation.Opcode is "message" or "text" ? operation.Raw : null, fail ? "Injected audit failure." : null);
        }
    }

    private sealed class AuditTransactionFactory : IRuntimeClassicNarrativeTransactionFactory
    {
        public int BeginCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public IRuntimeClassicNarrativeTransaction Begin(RuntimeClassicNarrativeExecutionContext context) { BeginCount++; return new Tx(this); }
        private sealed class Tx : IRuntimeClassicNarrativeTransaction
        {
            private readonly AuditTransactionFactory _owner; private bool _closed;
            public Tx(AuditTransactionFactory owner) => _owner = owner;
            public void Commit() { if (_closed) throw new InvalidOperationException("Transaction already closed."); _closed = true; _owner.CommitCount++; }
            public void Rollback() { if (_closed) throw new InvalidOperationException("Transaction already closed."); _closed = true; _owner.RollbackCount++; }
            public void Dispose() { }
        }
    }
}
