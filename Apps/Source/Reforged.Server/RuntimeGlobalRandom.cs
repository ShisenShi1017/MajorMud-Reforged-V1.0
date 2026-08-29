namespace Reforged.Server;

public enum RuntimeRandomScope { World, Session, Action }
public sealed record RuntimeRandomDraw(string StreamId, RuntimeRandomScope Scope, long Sequence, ulong RawValue, int MinimumInclusive, int MaximumInclusive, int Result);
public sealed record RuntimeRandomStreamState(string StreamId, RuntimeRandomScope Scope, ulong State0, ulong State1, long Sequence);

/// <summary>
/// Reforged deterministic multi-stream RNG extension. PASS60 parity boundary:
/// this xorshift128+ world/session/action service, its snapshot/restore behavior,
/// and its inclusive rejection-sampling adapter do NOT define the Classic MajorMUD
/// / Worldgroup RNG contract and receive zero Classic parity credit.
/// </summary>
public sealed class RuntimeGlobalRandomService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeRandomStreamState> _streams = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeGlobalRandomService(ulong worldSeed)
    {
        if (worldSeed == 0) worldSeed = 0x9E3779B97F4A7C15UL;
        RegisterStream("world", RuntimeRandomScope.World, worldSeed);
    }

    public void RegisterStream(string streamId, RuntimeRandomScope scope, ulong seed)
    {
        if (string.IsNullOrWhiteSpace(streamId)) throw new ArgumentException("random-stream-id-required");
        lock (_gate)
        {
            if (_streams.ContainsKey(streamId)) throw new InvalidOperationException("duplicate-random-stream:" + streamId);
            var s0 = SplitMix64(ref seed); var s1 = SplitMix64(ref seed);
            if ((s0 | s1) == 0) s1 = 1;
            _streams.Add(streamId, new(streamId, scope, s0, s1, 0));
        }
    }

    public RuntimeRandomDraw NextInclusive(string streamId, int minimumInclusive, int maximumInclusive)
    {
        if (maximumInclusive < minimumInclusive) throw new ArgumentOutOfRangeException(nameof(maximumInclusive), "random-range-invalid");
        lock (_gate)
        {
            if (!_streams.TryGetValue(streamId, out var state)) throw new KeyNotFoundException("unknown-random-stream:" + streamId);
            var raw = NextRaw(state, out var next);
            var width = (ulong)((long)maximumInclusive - minimumInclusive + 1L);
            var limit = ulong.MaxValue - (ulong.MaxValue % width);
            while (raw >= limit) { raw = NextRaw(next, out next); }
            var result = checked((int)(minimumInclusive + (long)(raw % width)));
            next = next with { Sequence = state.Sequence + 1 };
            _streams[streamId] = next;
            return new(streamId, state.Scope, next.Sequence, raw, minimumInclusive, maximumInclusive, result);
        }
    }

    public RuntimeRandomDraw RollPercent(string streamId) => NextInclusive(streamId, 1, 100);

    public RuntimeRandomStreamState Snapshot(string streamId)
    {
        lock (_gate) return _streams.TryGetValue(streamId, out var state) ? state : throw new KeyNotFoundException("unknown-random-stream:" + streamId);
    }

    public void Restore(RuntimeRandomStreamState state)
    {
        if (string.IsNullOrWhiteSpace(state.StreamId) || (state.State0 | state.State1) == 0 || state.Sequence < 0) throw new ArgumentException("random-state-invalid");
        lock (_gate) _streams[state.StreamId] = state;
    }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate) return _streams.Values.Where(x => (x.State0 | x.State1) == 0 || x.Sequence < 0).Select(x => "invalid-random-stream:" + x.StreamId).ToArray();
    }

    private static ulong NextRaw(RuntimeRandomStreamState state, out RuntimeRandomStreamState next)
    {
        var s1 = state.State0; var s0 = state.State1;
        var result = s0 + s1;
        s1 ^= s1 << 23;
        var n1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
        next = state with { State0 = s0, State1 = n1 };
        return result;
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
