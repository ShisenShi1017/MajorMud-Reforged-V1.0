namespace Reforged.Server;

public enum RuntimeClassicWorldTimePhase
{
    Sundown
}

public sealed record RuntimeClassicWorldTimeEvent(
    RuntimeClassicWorldTimePhase Phase,
    string CycleId,
    DateTimeOffset OccurredAtUtc,
    string Authority);

public sealed record RuntimeClassicWorldTimeEventResult(
    RuntimeClassicWorldTimeEvent Event,
    IReadOnlyList<RuntimeClassicGangHouseUpkeepResult> GangHouseUpkeep);

/// <summary>
/// Compatibility adapter retained for older callers. CodeUpdate0343 recovered the real native
/// producer: gang-house taxation is owned by the daily MajorBBS/Worldgroup mcurou callback, not
/// an independently recovered in-game sunset scheduler.
/// </summary>
public sealed class RuntimeClassicWorldTimeEventService
{
    private readonly RuntimeClassicGangHouseUpkeepService _gangHouseUpkeep;

    public RuntimeClassicWorldTimeEventService(RuntimeCharacterService characters, string worldId)
    {
        _gangHouseUpkeep = new RuntimeClassicGangHouseUpkeepService(characters, worldId);
    }

    public RuntimeClassicWorldTimeEventResult Process(RuntimeClassicWorldTimeEvent worldTimeEvent)
    {
        if (string.IsNullOrWhiteSpace(worldTimeEvent.CycleId))
            throw new ArgumentException("A stable Classic world-time cycle ID is required.", nameof(worldTimeEvent));
        if (string.IsNullOrWhiteSpace(worldTimeEvent.Authority))
            throw new ArgumentException("An authoritative Classic world-time source is required.", nameof(worldTimeEvent));

        return worldTimeEvent.Phase switch
        {
            RuntimeClassicWorldTimePhase.Sundown => new(
                worldTimeEvent,
                _gangHouseUpkeep.ProcessDailyAutoCleanup(worldTimeEvent.CycleId, worldTimeEvent.OccurredAtUtc)),
            _ => throw new ArgumentOutOfRangeException(nameof(worldTimeEvent))
        };
    }
}
