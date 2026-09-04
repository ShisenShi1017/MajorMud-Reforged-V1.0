namespace RealmGate.Server;

public sealed record NativeHostTextBindingEvidence(
    string Symbol, IReadOnlyList<int> TemplatePositions, IReadOnlyList<int> ConversionArities,
    bool TemplatePositionStable, bool? BoundedArgumentCapacitySufficient);

public static class NativeHostTextBindingRegistry
{
    public const string EvidenceId = "FinalNativeClosure0018";
    public const string Scope = "Proven caller text-binding shape only; native formatting and exact variadic types remain fail-closed.";
    public static readonly IReadOnlyList<NativeHostTextBindingEvidence> Proven = new NativeHostTextBindingEvidence[]
    {
        new("_prat", new int[] { 3 }, new int[] { 1 }, true, true),
        new("_prf", new int[] { 1 }, new int[] { 1, 2, 3 }, true, false),
        new("_shocst", new int[] { 2 }, new int[] { 1, 2, 4 }, true, false),
        new("_spr", new int[] { 1 }, new int[] { 1, 2, 3, 4 }, true, true),
    };
}
