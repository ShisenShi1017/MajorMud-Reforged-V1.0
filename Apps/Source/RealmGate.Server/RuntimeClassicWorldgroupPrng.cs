namespace RealmGate.Server;

public sealed record RuntimeClassicWorldgroupPrngDraw(
    uint PreviousSeed,
    uint NextSeed,
    ushort Value,
    long Sequence);

/// <summary>
/// Executable Classic Worldgroup/Borland C runtime PRNG for one native CRT thread.
///
/// The supplied Worldgroup 3 NT WGSERVER exports _genrdn at RVA 0x0000739C and
/// _lngrnd at RVA 0x000073EE. Both use the same accumulated-remainder loop over
/// CW3220MT rand(). A RuntimeClassicWorldgroupPrng instance therefore models one
/// native thread-local CRT random stream; callers must not treat it as proof of a
/// single process-global Classic stream.
/// </summary>
public sealed class RuntimeClassicWorldgroupPrng
{
    private readonly object _gate = new();
    private uint _seed;
    private long _sequence;

    public RuntimeClassicWorldgroupPrng(uint seed)
    {
        _seed = seed;
    }

    public uint Seed
    {
        get { lock (_gate) return _seed; }
    }

    public long Sequence
    {
        get { lock (_gate) return _sequence; }
    }

    public void Srand(uint seed)
    {
        lock (_gate)
        {
            _seed = seed;
            _sequence = 0;
        }
    }

    public RuntimeClassicWorldgroupPrngDraw Rand()
    {
        lock (_gate)
            return RandLocked();
    }

    /// <summary>
    /// Instruction-equivalent semantic port of WGSERVER!_genrdn for the recovered
    /// integer domain. exclusiveMaximum == 0 returns 0 without consuming rand();
    /// exclusiveMaximum &lt;= minimum returns minimum without consuming rand().
    /// Otherwise rand()%exclusiveMaximum is accumulated with further
    /// rand()%(exclusiveMaximum-current) draws until current &gt;= minimum.
    /// </summary>
    public int Genrdn(int minimum, int exclusiveMaximum)
    {
        lock (_gate)
            return RangeLocked(minimum, exclusiveMaximum);
    }

    /// <summary>WGSERVER!_lngrnd has the same recovered machine-code algorithm.</summary>
    public int Lngrnd(int minimum, int exclusiveMaximum)
    {
        lock (_gate)
            return RangeLocked(minimum, exclusiveMaximum);
    }

    private int RangeLocked(int minimum, int exclusiveMaximum)
    {
        if (exclusiveMaximum == 0)
            return 0;
        if (exclusiveMaximum <= minimum)
            return minimum;

        var current = RandLocked().Value % exclusiveMaximum;
        while (current < minimum)
        {
            var remaining = exclusiveMaximum - current;
            current += RandLocked().Value % remaining;
        }

        return current;
    }

    private RuntimeClassicWorldgroupPrngDraw RandLocked()
    {
        var previous = _seed;
        var next = WorldgroupPrngRuntimeRegistry.AdvanceSeed(previous);
        _seed = next;
        _sequence++;
        return new(previous, next, WorldgroupPrngRuntimeRegistry.ProjectRand15(next), _sequence);
    }
}


/// <summary>
/// WCCMMUTL.EXE contest-specific CRT stream. The utility seeds once from time(NULL)
/// before command dispatch, so contest draws are not interleaved with WCCMMUD gameplay RNG.
/// </summary>
public static class RuntimeClassicMajorMudPlusContestRandom
{
    public static RuntimeClassicWorldgroupPrng Create(DateTimeOffset utilityStartUtc) =>
        new(unchecked((uint)utilityStartUtc.ToUnixTimeSeconds()));
}
