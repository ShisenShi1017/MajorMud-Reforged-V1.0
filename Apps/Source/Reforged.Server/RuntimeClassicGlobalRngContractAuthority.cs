namespace Reforged.Server;

/// <summary>
/// Exact Classic MajorMUD / Worldgroup host RNG contract for the supplied WGSERV build.
///
/// This authority owns the host API contract only: CW3220MT rand/srand, packed-time seed,
/// TLS ownership, WGSERVER _genrdn/_lngrnd machine algorithm, and the active WCCMMUD
/// static import boundary. Gameplay-specific decisions and draw ordering remain with the
/// combat/spell/NPC/etc. assertions that consume this host API; they are not hidden here.
/// </summary>
public static class RuntimeClassicGlobalRngContractAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const string WbbMudSha256 = "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091";
    public const string ReferenceCw3220MtSha256 = "4a2f6889f9dc5cfdf4ac44156dde8db0d20c6a0f63db06c576ac0bbba2011a15";
    public const string SuppliedCw3220MtSha256 = "89eaa381a4a1c358f5c711fc31d873dd9a3c833af721a66cfaf01240bb4d9fd5";
    public const string SuppliedWgServerSha256 = "7f8d7e3675e5b754d146fad69f2a7d4cd667d87eef4ceafc62d7253b55dd79cb";

    public const uint BorlandMultiplier = 0x015A4E35u;
    public const uint BorlandIncrement = 1u;
    public const int RandMaximum = 0x7FFF;
    public const uint WgServerGenrdnRva = 0x0000739Cu;
    public const uint WgServerLngrndRva = 0x000073EEu;
    public const int GenrdnDirectCallsInActiveWcc = 146;
    public const int LngrndDirectCallsInActiveWcc = 42;

    public static bool RawBorlandPrngCoreResolved => true;
    public static bool PackedTimeStartupSeedResolved => true;
    public static bool ThreadLocalCrtStateOwnershipResolved => true;
    public static bool HostRangeEndpointContractResolved => true;
    public static bool InstructionEquivalentRangeLoopPortResolved => true;
    public static bool ActiveWccDirectRandomImportCensusResolved => true;
    public static bool ActiveWccStaticIndirectRandomReferencesAbsent => true;

    // Explicit non-claims: these are owner-local gameplay questions, not missing host contract.
    public static bool GameplaySemanticCallsiteMappingResolved => false;
    public static bool GameplayWideDrawOrderingResolved => false;
    public static bool SingleProcessGlobalClassicStreamProven => false;
    public static bool ReforgedGlobalRandomDefinesClassicContract => false;

    public static uint AdvanceSeed(uint seed) => unchecked(seed * BorlandMultiplier + BorlandIncrement);
    public static ushort ProjectRand15(uint seed) => (ushort)((seed >> 16) & RandMaximum);

    public static ushort PackStartupTimeSeed(int hour, int minute, int second)
    {
        if (hour is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(hour));
        if (minute is < 0 or > 59) throw new ArgumentOutOfRangeException(nameof(minute));
        if (second is < 0 or > 59) throw new ArgumentOutOfRangeException(nameof(second));
        return checked((ushort)((hour << 11) + (minute << 5) + (second >> 1)));
    }

    public static void RequireClassicBoundedRangeLoop()
    {
        if (!InstructionEquivalentRangeLoopPortResolved)
            throw new InvalidOperationException("native-global-rng-range-loop-unresolved");
    }

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (AdvanceSeed(1234u) != 2236414843u) failures.Add("borland-seed-vector-mismatch");
        if (ProjectRand15(2236414843u) != 1356) failures.Add("borland-output-vector-mismatch");
        if (PackStartupTimeSeed(23, 59, 58) != 49021) failures.Add("packed-time-seed-vector-mismatch");

        var p = new RuntimeClassicWorldgroupPrng(1234u);
        if (p.Genrdn(0, 100) != 56 || p.Sequence != 1) failures.Add("genrdn-basic-vector-mismatch");
        p.Srand(1234u);
        if (p.Genrdn(99, 100) != 99 || p.Sequence != 6) failures.Add("genrdn-accumulation-vector-mismatch");
        p.Srand(1234u);
        if (p.Lngrnd(10, 11) != 10 || p.Sequence != 3) failures.Add("lngrnd-accumulation-vector-mismatch");
        p.Srand(1234u);
        if (p.Genrdn(100, 100) != 100 || p.Sequence != 0) failures.Add("genrdn-max-le-min-consumption-mismatch");
        p.Srand(1234u);
        if (p.Genrdn(5, 0) != 0 || p.Sequence != 0) failures.Add("genrdn-zero-max-consumption-mismatch");

        var directRows = WorldgroupRngDirectCallsiteRegistry.Proven
            .Where(x => x.Module.Equals("wccmmud.dll", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (directRows.Single(x => x.Symbol == "_genrdn").DirectCallsiteCount != GenrdnDirectCallsInActiveWcc)
            failures.Add("active-wcc-genrdn-census-mismatch");
        if (directRows.Single(x => x.Symbol == "_lngrnd").DirectCallsiteCount != LngrndDirectCallsInActiveWcc)
            failures.Add("active-wcc-lngrnd-census-mismatch");

        if (!RawBorlandPrngCoreResolved || !PackedTimeStartupSeedResolved || !ThreadLocalCrtStateOwnershipResolved ||
            !HostRangeEndpointContractResolved || !InstructionEquivalentRangeLoopPortResolved ||
            !ActiveWccDirectRandomImportCensusResolved || !ActiveWccStaticIndirectRandomReferencesAbsent)
            failures.Add("host-rng-contract-demoted");

        if (GameplaySemanticCallsiteMappingResolved || GameplayWideDrawOrderingResolved ||
            SingleProcessGlobalClassicStreamProven || ReforgedGlobalRandomDefinesClassicContract)
            failures.Add("owner-local-rng-nonclaim-promoted");

        return failures;
    }
}
