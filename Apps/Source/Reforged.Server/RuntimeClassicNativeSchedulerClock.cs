namespace Reforged.Server;

public sealed record RuntimeClassicSchedulerStep(
    long LogicalFastTick,
    bool FastDue,
    bool MediumDue,
    bool EnergyDue,
    bool MonsterCreateDue,
    bool SlowDue);

/// <summary>
/// Executable projection of the WCCMMUD.DLL background owner clocks.
///
/// Native _init__wccmmud freshly arms the fast, medium, energy, monster-create,
/// slow, other/save, inactivity, and buffer-save callbacks on every module start.
/// Each background callback is one-shot: when serviced it performs one owner
/// generation and re-arms from that execution moment. Delayed service therefore
/// stretches wall-clock phase and never synthesizes missed callback generations.
///
/// The five gameplay owner deadlines represented by RuntimeClassicSchedulerStep are
/// intentionally independent. They are not inferred from LogicalFastTick modulo;
/// that distinction matters after one owner callback is delayed independently.
/// </summary>
public sealed class RuntimeClassicNativeSchedulerClock
{
    private bool _initialized;
    private DateTimeOffset _nextFastDueUtc;
    private DateTimeOffset _nextMediumDueUtc;
    private DateTimeOffset _nextEnergyDueUtc;
    private DateTimeOffset _nextMonsterCreateDueUtc;
    private DateTimeOffset _nextSlowDueUtc;
    private long _logicalFastTick;

    public long LogicalFastTick => _logicalFastTick;
    public DateTimeOffset? NextFastDueUtc => _initialized ? _nextFastDueUtc : null;

    public RuntimeClassicSchedulerStep Advance(DateTimeOffset nowUtc)
    {
        if (!_initialized)
        {
            ArmFromStartup(nowUtc);
            return None();
        }

        var fastDue = ServiceOneShot(ref _nextFastDueUtc, nowUtc, RuntimeClassicTimingAuthority.FastSchedulerSeconds);
        var mediumDue = ServiceOneShot(ref _nextMediumDueUtc, nowUtc, RuntimeClassicTimingAuthority.MediumSchedulerFastTicks);
        var energyDue = ServiceOneShot(ref _nextEnergyDueUtc, nowUtc, RuntimeClassicTimingAuthority.EnergySchedulerFastTicks);
        var monsterCreateDue = ServiceOneShot(ref _nextMonsterCreateDueUtc, nowUtc, RuntimeClassicTimingAuthority.MonsterCreateSchedulerFastTicks);
        var slowDue = ServiceOneShot(ref _nextSlowDueUtc, nowUtc, RuntimeClassicTimingAuthority.SlowSchedulerFastTicks);

        if (fastDue) _logicalFastTick = checked(_logicalFastTick + 1);
        return new RuntimeClassicSchedulerStep(_logicalFastTick, fastDue, mediumDue, energyDue, monsterCreateDue, slowDue);
    }

    public void Restart(DateTimeOffset nowUtc)
    {
        _logicalFastTick = 0;
        ArmFromStartup(nowUtc);
    }

    private void ArmFromStartup(DateTimeOffset nowUtc)
    {
        _initialized = true;
        _nextFastDueUtc = nowUtc.AddSeconds(RuntimeClassicTimingAuthority.FastSchedulerSeconds);
        _nextMediumDueUtc = nowUtc.AddSeconds(RuntimeClassicTimingAuthority.MediumSchedulerFastTicks);
        _nextEnergyDueUtc = nowUtc.AddSeconds(RuntimeClassicTimingAuthority.EnergySchedulerFastTicks);
        _nextMonsterCreateDueUtc = nowUtc.AddSeconds(RuntimeClassicTimingAuthority.MonsterCreateSchedulerFastTicks);
        _nextSlowDueUtc = nowUtc.AddSeconds(RuntimeClassicTimingAuthority.SlowSchedulerFastTicks);
    }

    private static bool ServiceOneShot(ref DateTimeOffset deadlineUtc, DateTimeOffset nowUtc, int delaySeconds)
    {
        if (nowUtc < deadlineUtc) return false;
        deadlineUtc = nowUtc.AddSeconds(delaySeconds);
        return true;
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
        Require(!clock.Advance(origin).FastDue, "scheduler-startup-fresh-arm-only"); checks.Add("scheduler-startup-fresh-arm-only");
        var first = clock.Advance(origin.AddSeconds(1));
        Require(first.FastDue && first.LogicalFastTick == 1 && !first.MediumDue && !first.EnergyDue && !first.SlowDue, "scheduler-first-fast-tick"); checks.Add("scheduler-first-fast-tick");

        // Delaying the host by 99 seconds executes one generation for every overdue
        // one-shot owner, rather than 99 synthetic fast generations.
        var delayed = clock.Advance(origin.AddSeconds(100));
        Require(delayed.FastDue && delayed.MediumDue && delayed.EnergyDue && delayed.MonsterCreateDue && delayed.SlowDue
            && delayed.LogicalFastTick == 2, "scheduler-no-elapsed-catchup"); checks.Add("scheduler-no-elapsed-catchup");
        Require(!clock.Advance(origin.AddSeconds(100)).FastDue, "scheduler-one-shot-rearm"); checks.Add("scheduler-one-shot-rearm");

        // Independent owner deadlines: after the delayed callback, every owner re-arms
        // from its own execution moment rather than from fast-tick modulo phase.
        var afterThree = clock.Advance(origin.AddSeconds(103));
        Require(afterThree.FastDue && afterThree.MediumDue && !afterThree.EnergyDue && !afterThree.SlowDue,
            "scheduler-independent-owner-deadlines"); checks.Add("scheduler-independent-owner-deadlines");

        clock.Restart(origin.AddSeconds(200));
        Require(clock.LogicalFastTick == 0 && !clock.Advance(origin.AddSeconds(200)).FastDue,
            "scheduler-restart-fresh-arm"); checks.Add("scheduler-restart-fresh-arm");
        return checks;
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
