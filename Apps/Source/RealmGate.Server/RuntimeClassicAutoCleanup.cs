namespace RealmGate.Server;

public sealed record RuntimeClassicAutoCleanupResult(
    string WorldId,
    string CycleId,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyList<RuntimeClassicGangHouseUpkeepResult> GangHouseUpkeep,
    RuntimeClassicGangHouseWorldCleanupResult? GangHouseWorldCleanup,
    IReadOnlyList<string> DeletedDisbandedGangNames);

/// <summary>
/// MajorBBS/Worldgroup invokes each module's mcurou entry point once per day. The historical
/// host default is 03:00 local server time, but the host owns/configures that wall-clock time.
/// Reforged mirrors the default and deliberately performs no restart catch-up: if the process
/// starts after today's cleanup time, the next callback is tomorrow, matching a scheduled host
/// callback rather than manufacturing a missed event.
/// </summary>
public sealed class RuntimeClassicAutoCleanupScheduler
{
    public static readonly TimeSpan DefaultHostCleanupLocalTime = TimeSpan.FromHours(3);

    private readonly RuntimeCharacterService _characters;
    private readonly string _worldId;
    private readonly TimeSpan _hostCleanupLocalTime;
    private readonly TimeZoneInfo _hostTimeZone;
    private readonly Func<RuntimeClassicGangHouseWorldCleanupResult>? _gangHouseWorldCleanup;
    private DateTimeOffset _nextDueLocal;

    public RuntimeClassicAutoCleanupScheduler(
        RuntimeCharacterService characters,
        string worldId,
        DateTimeOffset bootLocal,
        TimeSpan? hostCleanupLocalTime = null,
        TimeZoneInfo? hostTimeZone = null,
        Func<RuntimeClassicGangHouseWorldCleanupResult>? gangHouseWorldCleanup = null)
    {
        _characters = characters ?? throw new ArgumentNullException(nameof(characters));
        _worldId = string.IsNullOrWhiteSpace(worldId) ? throw new ArgumentException("Classic world id is required.", nameof(worldId)) : worldId;
        _hostCleanupLocalTime = hostCleanupLocalTime ?? DefaultHostCleanupLocalTime;
        if (_hostCleanupLocalTime < TimeSpan.Zero || _hostCleanupLocalTime >= TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(hostCleanupLocalTime));
        _hostTimeZone = hostTimeZone ?? TimeZoneInfo.Local;
        _gangHouseWorldCleanup = gangHouseWorldCleanup;
        _nextDueLocal = NextOccurrenceAfter(bootLocal, _hostCleanupLocalTime, _hostTimeZone);
    }

    public DateTimeOffset NextDueLocal => _nextDueLocal;

    public RuntimeClassicAutoCleanupResult? TryRun(DateTimeOffset nowLocal)
    {
        if (nowLocal < _nextDueLocal) return null;

        var occurrence = _nextDueLocal;
        // Advance before executing so a thrown cleanup cannot turn a scheduler probe into a tight
        // repeated loop. Native host recovery/retry policy is outside the recovered mcurou ABI.
        _nextDueLocal = NextOccurrenceAfter(occurrence, _hostCleanupLocalTime, _hostTimeZone);
        var cycleId = $"mcurou:{occurrence:yyyy-MM-dd}";
        var upkeep = new RuntimeClassicGangHouseUpkeepService(_characters, _worldId)
            .ProcessDailyAutoCleanup(cycleId, occurrence.ToUniversalTime());
        // Native mcurou resolves gang-house tax/repossession first, then performs room/deed cleanup,
        // and only then deletes fully drained disbanded gang records.
        var worldCleanup = _gangHouseWorldCleanup?.Invoke();
        var deleted = new RuntimeClassicGangStateService(_characters.ProjectRoot).CleanupDisbandedGangs();
        return new(_worldId, cycleId, occurrence.ToUniversalTime(), upkeep, worldCleanup, deleted);
    }

    public static DateTimeOffset NextOccurrenceAfter(DateTimeOffset localNow, TimeSpan localTime)
        => NextOccurrenceAfter(localNow, localTime, TimeZoneInfo.Local);

    internal static DateTimeOffset NextOccurrenceAfter(DateTimeOffset now, TimeSpan localTime, TimeZoneInfo hostTimeZone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, hostTimeZone);
        var localDate = localNow.Date;
        while (true)
        {
            var candidateClock = DateTime.SpecifyKind(localDate.Add(localTime), DateTimeKind.Unspecified);
            // The recovered stock/default callback is 03:00, outside the usual DST skipped hour.
            // If an administrator configures a skipped local time, advance to the first valid minute
            // instead of fabricating an impossible wall-clock occurrence.
            while (hostTimeZone.IsInvalidTime(candidateClock)) candidateClock = candidateClock.AddMinutes(1);
            var candidate = new DateTimeOffset(candidateClock, hostTimeZone.GetUtcOffset(candidateClock));
            if (candidate > now) return candidate;
            localDate = localDate.AddDays(1);
        }
    }
}
