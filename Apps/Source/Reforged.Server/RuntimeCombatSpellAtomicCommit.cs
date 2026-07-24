namespace Reforged.Server;

public sealed record RuntimeCombatSpellMutation(
    string MutationId,
    string TargetId,
    RuntimeDamageDisposition Disposition,
    int Magnitude,
    string DefinitionId,
    string ResolutionId);

public interface IRuntimeCombatSpellMutationSink
{
    object Capture(string targetId);
    void Apply(RuntimeCombatSpellMutation mutation);
    void Restore(string targetId, object snapshot);
}

public sealed record RuntimeCombatSpellCommitResult(
    RuntimeCombatSpellResolutionTrace Trace,
    RuntimeCombatSpellMutation Mutation,
    bool Committed);

public sealed class RuntimeCombatSpellAtomicExecutor
{
    private readonly RuntimeCombatSpellBoundResolver _resolver;
    private readonly IRuntimeCombatSpellMutationSink _sink;

    public RuntimeCombatSpellAtomicExecutor(RuntimeCombatSpellBoundResolver resolver, IRuntimeCombatSpellMutationSink sink)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public RuntimeCombatSpellCommitResult Execute(RuntimeCombatSpellBoundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trace = _resolver.Resolve(request); // all validation and randomness complete before mutation
        var snapshot = _sink.Capture(request.TargetId);
        var mutation = new RuntimeCombatSpellMutation(
            $"mutation:{request.ResolutionId}", request.TargetId, request.Disposition,
            trace.FinalMagnitude, request.DefinitionId, request.ResolutionId);
        try
        {
            _sink.Apply(mutation);
            return new RuntimeCombatSpellCommitResult(trace, mutation, true);
        }
        catch
        {
            _sink.Restore(request.TargetId, snapshot);
            throw;
        }
    }
}
