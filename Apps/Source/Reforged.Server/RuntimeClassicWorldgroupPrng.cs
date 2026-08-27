namespace Reforged.Server;

public sealed record RuntimeClassicWorldgroupPrngDraw(
    uint PreviousSeed,
    uint NextSeed,
    ushort Value,
    long Sequence);

/// <summary>
/// Executable Classic Worldgroup/Borland C runtime PRNG core recovered from the
/// bound CW3220MT.DLL runtime. This class intentionally owns only the proven
/// raw rand()/srand() contract. High-level MajorMUD call ordering and the exact
/// _genrdn/_lngrnd range-transformation loop remain separate evidence boundaries.
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
        {
            var previous = _seed;
            var next = WorldgroupPrngRuntimeRegistry.AdvanceSeed(previous);
            _seed = next;
            _sequence++;
            return new(previous, next, WorldgroupPrngRuntimeRegistry.ProjectRand15(next), _sequence);
        }
    }
}
