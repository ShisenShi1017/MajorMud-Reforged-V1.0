using System.Diagnostics;

namespace RealmGate.Server;

public sealed class ServerTickScheduler
{
    private readonly List<ScheduledRuntimeChannel> _channels = new();
    public Action<string>? TimingDiagnostic { get; set; }

    public IReadOnlyList<ScheduledRuntimeChannel> Channels => _channels;

    public void Register(string name, TimeSpan interval, Func<CancellationToken, Task> callback)
    {
        _channels.Add(new ScheduledRuntimeChannel(name, interval, callback));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in _channels)
        {
            channel.MarkDueNow();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var channel in _channels)
            {
                if (channel.IsDue)
                {
                    var lateBy = channel.DelayPastDue;
                    var started = Stopwatch.GetTimestamp();
                    try
                    {
                        await channel.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        var elapsed = Stopwatch.GetElapsedTime(started);
                        // Ignore ordinary 100ms scheduler polling jitter. Log only
                        // significant lateness or long-running callbacks.
                        if (TimingDiagnostic is not null &&
                            (lateBy >= TimeSpan.FromMilliseconds(250) || elapsed >= TimeSpan.FromMilliseconds(250)))
                            TimingDiagnostic($"scheduler channel={channel.Name} lateMs={lateBy.TotalMilliseconds:F0} durationMs={elapsed.TotalMilliseconds:F0} intervalMs={channel.Interval.TotalMilliseconds:F0}");
                    }
                }
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class ScheduledRuntimeChannel
{
    private readonly Func<CancellationToken, Task> _callback;
    private DateTimeOffset _nextRun = DateTimeOffset.MaxValue;

    public ScheduledRuntimeChannel(string name, TimeSpan interval, Func<CancellationToken, Task> callback)
    {
        Name = name;
        Interval = interval;
        _callback = callback;
    }

    public string Name { get; }
    public TimeSpan Interval { get; }
    public DateTimeOffset LastRun { get; private set; }
    public bool IsDue => DateTimeOffset.UtcNow >= _nextRun;
    public TimeSpan DelayPastDue => _nextRun == DateTimeOffset.MaxValue
        ? TimeSpan.Zero
        : TimeSpan.FromTicks(Math.Max(0, (DateTimeOffset.UtcNow - _nextRun).Ticks));

    public void MarkDueNow()
    {
        _nextRun = DateTimeOffset.UtcNow;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        LastRun = DateTimeOffset.UtcNow;
        _nextRun = LastRun.Add(Interval);
        await _callback(cancellationToken).ConfigureAwait(false);
    }
}
