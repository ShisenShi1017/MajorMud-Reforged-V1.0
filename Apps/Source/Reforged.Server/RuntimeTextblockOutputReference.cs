namespace Reforged.Server;

public enum RuntimeTextblockOutputReferenceKind
{
    Message = 0,
    Textblock = 1
}

public sealed record RuntimeTextblockOutputReference(
    RuntimeTextblockOutputReferenceKind Kind,
    int CanonicalId,
    int SourceOrder);

public static class RuntimeClassicResolvedTextblockOutput
{
    public static string Resolve(
        RuntimeTextblockOutputReference reference,
        RuntimeClassicNativeTextblockPayloadAuthority nativePayloads) =>
        reference.Kind switch
        {
            RuntimeTextblockOutputReferenceKind.Message => nativePayloads.Require(reference.CanonicalId),
            RuntimeTextblockOutputReferenceKind.Textblock => nativePayloads.Require(reference.CanonicalId),
            _ => throw new InvalidOperationException($"Unsupported textblock output kind {reference.Kind}.")
        };

    public static IReadOnlyList<string> ResolveCommitted(
        IReadOnlyList<RuntimeTextblockOutputReference> references,
        RuntimeClassicNativeTextblockPayloadAuthority nativePayloads) =>
        references
            .OrderBy(x => x.SourceOrder)
            .Select(x => Resolve(x, nativePayloads))
            .ToArray();
}
