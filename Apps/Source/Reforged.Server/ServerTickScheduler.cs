namespace Reforged.Server;

public sealed class ServerTickScheduler
{
    private readonly List<ScheduledRuntimeChannel> _channels = new();

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
                    await channel.ExecuteAsync(cancellationToken).ConfigureAwait(false);
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
