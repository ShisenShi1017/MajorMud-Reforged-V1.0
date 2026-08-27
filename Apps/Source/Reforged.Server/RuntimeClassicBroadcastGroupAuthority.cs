namespace Reforged.Server;

public enum RuntimeClassicBroadcastGroupSelectionState
{
    Selectable,
    EvidenceConflict,
    Invalid
}

public readonly record struct RuntimeClassicBroadcastGroupSelection(
    int RequestedGroup,
    RuntimeClassicBroadcastGroupSelectionState State)
{
    public bool IsSelectable => State == RuntimeClassicBroadcastGroupSelectionState.Selectable;
}

/// <summary>
/// Exact shared broadcast-group contract recovered from native WCCMMHLP.
/// Message 157 proves group 0 plus groups 1..126. Message 123 says 1..127.
/// Pass 30 therefore activates only the uncontested 0..126 intersection and
/// preserves 127 as an explicit evidence conflict rather than guessing.
/// </summary>
public static class RuntimeClassicBroadcastGroupAuthority
{
    public const int DefaultGroup = 0;
    public const int HighestUncontestedGroup = 126;
    public const int ConflictedUpperGroup = 127;

    public static RuntimeClassicBroadcastGroupSelection ClassifySelection(int requestedGroup) => requestedGroup switch
    {
        >= DefaultGroup and <= HighestUncontestedGroup => new(requestedGroup, RuntimeClassicBroadcastGroupSelectionState.Selectable),
        ConflictedUpperGroup => new(requestedGroup, RuntimeClassicBroadcastGroupSelectionState.EvidenceConflict),
        _ => new(requestedGroup, RuntimeClassicBroadcastGroupSelectionState.Invalid)
    };

    public static int NormalizePersistedGroup(int persistedGroup) =>
        ClassifySelection(persistedGroup).IsSelectable ? persistedGroup : DefaultGroup;
}
