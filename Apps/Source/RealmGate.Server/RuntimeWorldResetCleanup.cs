using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeResetScope { Room, Area, Zone, World }
public enum RuntimeResetActionKind { Door, Trap, Container, Spawn, Merchant, Quest, Npc, Event, Item, OrphanCleanup }
public enum RuntimeResetFailurePolicy { StopAndRollback, ContinueIndependent }

public sealed record RuntimeResetActionDefinition(string ActionId, RuntimeResetActionKind Kind, string OwnerSubsystem, int Order, bool Required, bool HardResetExcluded = false);
public sealed record RuntimeResetPlanDefinition(string PlanId, RuntimeResetScope Scope, IReadOnlyList<RuntimeResetActionDefinition> Actions, RuntimeResetFailurePolicy FailurePolicy, bool PersistHistory = true);
public sealed record RuntimeResetRequest(string PlanId, string RequestedBy, DateTimeOffset RequestedAtUtc, bool HardReset, IReadOnlySet<string>? ExcludedActionIds = null);
public sealed record RuntimeResetDispatch(string ExecutionId, string PlanId, string ActionId, RuntimeResetActionKind Kind, string OwnerSubsystem, int Order, bool HardReset, DateTimeOffset DispatchedAtUtc);
public sealed record RuntimeResetActionResult(string ActionId, bool Succeeded, string? Error = null);
public sealed record RuntimeResetExecution(string ExecutionId, string PlanId, string RequestedBy, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc, bool HardReset, bool Completed, bool RolledBack, IReadOnlyList<string> CompletedActionIds, IReadOnlyList<string> FailedActionIds, long Revision);
public sealed record RuntimeResetSnapshot(int SchemaVersion, IReadOnlyList<RuntimeResetExecution> Executions, DateTimeOffset SavedAtUtc);

/// <summary>
/// Generic Reforged reset-plan coordination infrastructure. Numeric action order, hard-reset
/// exclusion flags, and rollback policy are host-plan semantics only; they are not evidence of
/// Classic MajorMUD reset ordering or failure behavior. Classic parity callers must use the
/// owner-specific recovered authorities and keep unresolved universal orchestration fail-closed.
/// </summary>
public sealed class RuntimeWorldResetCleanupService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeResetPlanDefinition> _plans = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeResetExecution> _executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistencePath;

    public RuntimeWorldResetCleanupService(IEnumerable<RuntimeResetPlanDefinition> plans, string? persistencePath = null)
    {
        _persistencePath = persistencePath;
        foreach (var plan in plans)
        {
            ValidatePlan(plan);
            if (!_plans.TryAdd(plan.PlanId, plan)) throw new InvalidOperationException("duplicate-reset-plan:" + plan.PlanId);
        }
        Restore();
    }

    public IReadOnlyList<RuntimeResetDispatch> Begin(RuntimeResetRequest request)
    {
        lock (_gate)
        {
            if (!_plans.TryGetValue(request.PlanId, out var plan)) throw new InvalidOperationException("unknown-reset-plan:" + request.PlanId);
            if (string.IsNullOrWhiteSpace(request.RequestedBy)) throw new ArgumentException("reset-requestor-required");
            var executionId = Guid.NewGuid().ToString("N");
            var excluded = request.ExcludedActionIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actions = plan.Actions
                .Where(a => !excluded.Contains(a.ActionId))
                .Where(a => !(request.HardReset && a.HardResetExcluded))
                .OrderBy(a => a.Order)
                .ThenBy(a => a.ActionId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _executions[executionId] = new(executionId, plan.PlanId, request.RequestedBy, request.RequestedAtUtc, null, request.HardReset, false, false, Array.Empty<string>(), Array.Empty<string>(), 0);
            SaveLocked();
            return actions.Select(a => new RuntimeResetDispatch(executionId, plan.PlanId, a.ActionId, a.Kind, a.OwnerSubsystem, a.Order, request.HardReset, request.RequestedAtUtc)).ToArray();
        }
    }

    public RuntimeResetExecution Complete(string executionId, IEnumerable<RuntimeResetActionResult> results, DateTimeOffset completedAtUtc)
    {
        lock (_gate)
        {
            if (!_executions.TryGetValue(executionId, out var execution)) throw new InvalidOperationException("unknown-reset-execution:" + executionId);
            if (execution.Completed) throw new InvalidOperationException("reset-execution-already-complete:" + executionId);
            var plan = _plans[execution.PlanId];
            var resultMap = results.GroupBy(r => r.ActionId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
            var completed = new List<string>();
            var failed = new List<string>();
            foreach (var action in plan.Actions.OrderBy(a => a.Order).ThenBy(a => a.ActionId, StringComparer.OrdinalIgnoreCase))
            {
                if (!resultMap.TryGetValue(action.ActionId, out var result)) continue;
                if (result.Succeeded) completed.Add(action.ActionId); else failed.Add(action.ActionId);
            }
            var requiredFailure = plan.Actions.Any(a => a.Required && failed.Contains(a.ActionId, StringComparer.OrdinalIgnoreCase));
            var rollback = requiredFailure && plan.FailurePolicy == RuntimeResetFailurePolicy.StopAndRollback;
            var updated = execution with { CompletedAtUtc = completedAtUtc, Completed = true, RolledBack = rollback, CompletedActionIds = completed, FailedActionIds = failed, Revision = execution.Revision + 1 };
            _executions[executionId] = updated;
            SaveLocked();
            return updated;
        }
    }

    public IReadOnlyList<RuntimeResetExecution> SnapshotExecutions() { lock (_gate) return _executions.Values.OrderBy(x => x.StartedAtUtc).ToArray(); }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var execution in _executions.Values)
            {
                if (!_plans.ContainsKey(execution.PlanId)) failures.Add("missing-plan:" + execution.ExecutionId);
                if (execution.Completed && execution.CompletedAtUtc is null) failures.Add("completed-without-time:" + execution.ExecutionId);
                if (execution.Revision < 0) failures.Add("negative-revision:" + execution.ExecutionId);
            }
            return failures;
        }
    }

    private static void ValidatePlan(RuntimeResetPlanDefinition plan)
    {
        if (string.IsNullOrWhiteSpace(plan.PlanId)) throw new ArgumentException("reset-plan-id-required");
        if (plan.Actions.Count == 0) throw new ArgumentException("reset-plan-actions-required:" + plan.PlanId);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in plan.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.ActionId) || string.IsNullOrWhiteSpace(action.OwnerSubsystem)) throw new ArgumentException("reset-action-required-field:" + plan.PlanId);
            if (!ids.Add(action.ActionId)) throw new ArgumentException("duplicate-reset-action:" + action.ActionId);
            if (action.Order < 0) throw new ArgumentException("reset-action-order-invalid:" + action.ActionId);
        }
    }

    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var persisted = _executions.Values.Where(e => _plans[e.PlanId].PersistHistory).ToArray();
        var snapshot = new RuntimeResetSnapshot(1, persisted, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(_persistencePath!)!);
        var tmp = _persistencePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _persistencePath!, true);
    }

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeResetSnapshot>(File.ReadAllText(_persistencePath!));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var execution in snapshot.Executions)
                if (_plans.ContainsKey(execution.PlanId)) _executions[execution.ExecutionId] = execution;
        }
        catch (JsonException) { }
        catch (IOException) { }
    }
}
