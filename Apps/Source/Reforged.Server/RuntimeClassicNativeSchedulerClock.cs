namespace Reforged.Server;

public sealed record RuntimeClassicSchedulerStep(
    long LogicalFastTick,
    bool FastDue,
    bool MediumDue,
    bool EnergyDue,
    bool MonsterCreateDue,
    bool SlowDue);

/// <summary>
/// Executable projection of the WCCMMUD.DLL background-fast root clock recovered in
/// CodeUpdate0336. _background_fast is a one-shot RTKICK(1) callback. Each callback
/// increments the native logical tick exactly once, services due internal work, then
/// re-arms from the execution moment. A delayed host callback therefore stretches
/// wall-clock time; it does not synthesize missed one-second ticks.
/// </summary>
public sealed class RuntimeClassicNativeSchedulerClock
{
    private bool _initialized;
    private DateTimeOffset _nextFastDueUtc;
    private long _logicalFastTick;

    public long LogicalFastTick => _logicalFastTick;
    public DateTimeOffset? NextFastDueUtc => _initialized ? _nextFastDueUtc : null;

    public RuntimeClassicSchedulerStep Advance(DateTimeOffset nowUtc)
    {
        if (!_initialized)
        {
            _initialized = true;
            _nextFastDueUtc = nowUtc.AddSeconds(RuntimeClassicTimingAuthority.FastSchedulerSeconds);
            return None();
        }

        if (nowUtc < _nextFastDueUtc) return None();

        _logicalFastTick = checked(_logicalFastTick + 1);
        _nextFastDueUtc = nowUtc.AddSeconds(RuntimeClassicTimingAuthority.FastSchedulerSeconds);
        return new RuntimeClassicSchedulerStep(
            _logicalFastTick,
            true,
            _logicalFastTick % RuntimeClassicTimingAuthority.MediumSchedulerFastTicks == 0,
            _logicalFastTick % RuntimeClassicTimingAuthority.EnergySchedulerFastTicks == 0,
            _logicalFastTick % RuntimeClassicTimingAuthority.MonsterCreateSchedulerFastTicks == 0,
            _logicalFastTick % RuntimeClassicTimingAuthority.SlowSchedulerFastTicks == 0);
    }

    private RuntimeClassicSchedulerStep None() => new(_logicalFastTick, false, false, false, false, false);
}

public static class RuntimeClassicNativeSchedulerClockAudit
{
    public static IReadOnlyList<string> Run()
    {
        var checks = new List<string>();
        var origin = DateTimeOffset.UnixEpoch;
        var clock = new RuntimeClassicNativeSchedulerClock();
        Require(!clock.Advance(origin).FastDue, "scheduler-initial-arm-only"); checks.Add("scheduler-initial-arm-only");
        var first = clock.Advance(origin.AddSeconds(1));
        Require(first.FastDue && first.LogicalFastTick == 1 && !first.MediumDue && !first.EnergyDue && !first.SlowDue, "scheduler-first-fast-tick"); checks.Add("scheduler-first-fast-tick");

        // Delaying the host by 99 seconds still executes exactly one fast callback.
        var delayed = clock.Advance(origin.AddSeconds(100));
        Require(delayed.FastDue && delayed.LogicalFastTick == 2, "scheduler-no-elapsed-catchup"); checks.Add("scheduler-no-elapsed-catchup");
        Require(!clock.Advance(origin.AddSeconds(100)).FastDue, "scheduler-one-shot-rearm"); checks.Add("scheduler-one-shot-rearm");

        var exact = new RuntimeClassicNativeSchedulerClock();
        exact.Advance(origin);
        RuntimeClassicSchedulerStep step = exact.Advance(origin.AddSeconds(1));
        for (var second = 2; second <= 30; second++) step = exact.Advance(origin.AddSeconds(second));
        Require(step.LogicalFastTick == 30 && step.FastDue && step.MediumDue && step.EnergyDue && step.MonsterCreateDue && step.SlowDue, "scheduler-owner-buckets-at-30"); checks.Add("scheduler-owner-buckets-at-30");
        return checks;
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
