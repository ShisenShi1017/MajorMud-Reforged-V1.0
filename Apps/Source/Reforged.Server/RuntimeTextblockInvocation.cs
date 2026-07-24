namespace Reforged.Server;

public sealed record RuntimeTextblockInvocation(
    int SourceTextblockId,
    int TargetTextblockId,
    int SourceLine,
    int SourceSequence);

public sealed record RuntimeTextblockInvocationResult(
    bool Accepted,
    string? TargetKey,
    string? FailureReason);

public static class RuntimeTextblockInvocationResolver
{
    public static RuntimeTextblockInvocationResult Resolve(
        RuntimeTextblockInvocation invocation,
        IReadOnlySet<int> executableTextblockPayloadIds)
    {
        if (invocation.SourceTextblockId <= 0)
            return new(false, null, "invalid_source_textblock_id");
        if (invocation.TargetTextblockId <= 0)
            return new(false, null, "invalid_target_textblock_id");
        if (invocation.SourceLine <= 0 || invocation.SourceSequence <= 0)
            return new(false, null, "invalid_source_position");
        if (!executableTextblockPayloadIds.Contains(invocation.TargetTextblockId))
            return new(false, $"textblock.{invocation.TargetTextblockId}", "target_payload_unresolved");

        return new(true, $"textblock.{invocation.TargetTextblockId}", null);
    }
}
