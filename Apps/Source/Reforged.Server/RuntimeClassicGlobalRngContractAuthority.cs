namespace Reforged.Server;

/// <summary>
/// PASS60 evidence boundary for Classic MajorMUD / Worldgroup randomness.
///
/// Exact native evidence already proves the Borland/CW3220MT raw rand/srand core,
/// packed-time startup seeding, thread-local CRT state ownership, exclusive-maximum
/// host range endpoints, and the exact-version direct _genrdn/_lngrnd call census.
///
/// This authority intentionally does NOT invent a process-global Classic stream,
/// map unresolved gameplay callers to Worldgroup execution threads, recover indirect
/// randomness paths, claim an instruction-equivalent bounded-remainder helper port,
/// or claim gameplay-wide draw ordering. RuntimeGlobalRandomService remains Reforged
/// extension infrastructure and receives zero Classic parity credit.
/// </summary>
public static class RuntimeClassicGlobalRngContractAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const string WbbMudSha256 = "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091";
    public const string Cw3220MtSha256 = "4a2f6889f9dc5cfdf4ac44156dde8db0d20c6a0f63db06c576ac0bbba2011a15";

    public const uint BorlandMultiplier = 0x015A4E35u;
    public const uint BorlandIncrement = 1u;
    public const int RandMaximum = 0x7FFF;
    public const int GenrdnDirectCallsPerModule = 146;
    public const int LngrndDirectCallsPerModule = 42;
    public const int GenrdnRetainedAddressesPerModule = 64;
    public const int LngrndRetainedAddressesPerModule = 42;

    public static bool RawBorlandPrngCoreResolved => true;
    public static bool PackedTimeStartupSeedResolved => true;
    public static bool ThreadLocalCrtStateOwnershipResolved => true;
    public static bool HostRangeEndpointContractResolved => true;
    public static bool DirectRandomImportCensusResolved => true;

    public static bool CompleteGenrdnDirectAddressMapResolved => false;
    public static bool IndirectRandomCallsitesResolved => false;
    public static bool GameplaySemanticCallsiteMappingResolved => false;
    public static bool ExecutionThreadCrtMappingResolved => false;
    public static bool InstructionEquivalentRangeLoopPortResolved => false;
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

    public static void RequireClassicGameplayCallsiteThreadMapping()
    {
        if (!GameplaySemanticCallsiteMappingResolved || !ExecutionThreadCrtMappingResolved)
            throw new InvalidOperationException("native-global-rng-callsite-thread-mapping-unresolved");
    }

    public static void RequireClassicIndirectRandomPaths()
    {
        if (!IndirectRandomCallsitesResolved)
            throw new InvalidOperationException("native-global-rng-indirect-paths-unresolved");
    }

    public static void RequireClassicBoundedRangeLoop()
    {
        if (!InstructionEquivalentRangeLoopPortResolved)
            throw new InvalidOperationException("native-global-rng-range-loop-unresolved");
    }

    public static void RequireClassicGameplayDrawOrdering()
    {
        if (!GameplayWideDrawOrderingResolved)
            throw new InvalidOperationException("native-global-rng-draw-ordering-unresolved");
    }

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (AdvanceSeed(1234u) != 2236414843u) failures.Add("borland-seed-vector-mismatch");
        if (ProjectRand15(2236414843u) != 1356) failures.Add("borland-output-vector-mismatch");
        if (PackStartupTimeSeed(23, 59, 58) != 49021) failures.Add("packed-time-seed-vector-mismatch");
        if (WorldgroupRngDirectCallsiteRegistry.Validate().Count != 0) failures.Add("direct-rng-boundary-invalid");

        var rows = WorldgroupRngDirectCallsiteRegistry.Proven;
        if (rows.Where(x => x.Symbol == "_genrdn").Sum(x => x.DirectCallsiteCount) != GenrdnDirectCallsPerModule * 2)
            failures.Add("genrdn-direct-census-mismatch");
        if (rows.Where(x => x.Symbol == "_lngrnd").Sum(x => x.DirectCallsiteCount) != LngrndDirectCallsPerModule * 2)
            failures.Add("lngrnd-direct-census-mismatch");

        if (!RawBorlandPrngCoreResolved || !PackedTimeStartupSeedResolved || !ThreadLocalCrtStateOwnershipResolved ||
            !HostRangeEndpointContractResolved || !DirectRandomImportCensusResolved)
            failures.Add("proven-rng-boundary-demoted");

        if (CompleteGenrdnDirectAddressMapResolved || IndirectRandomCallsitesResolved || GameplaySemanticCallsiteMappingResolved ||
            ExecutionThreadCrtMappingResolved || InstructionEquivalentRangeLoopPortResolved || GameplayWideDrawOrderingResolved ||
            SingleProcessGlobalClassicStreamProven || ReforgedGlobalRandomDefinesClassicContract)
            failures.Add("unresolved-rng-contract-promoted");

        return failures;
    }
}
