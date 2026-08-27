using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass34ClassicWorldgroupPrngSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass34ClassicWorldgroupPrngSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(WorldgroupPrngRuntimeRegistry.Validate().Count == 0, "native-prng-registry-valid");
        Check(WorldgroupPrngRuntimeRegistry.BorlandMultiplier == 22695477u, "borland-multiplier-22695477");
        Check(WorldgroupPrngRuntimeRegistry.BorlandIncrement == 1u, "borland-increment-1");
        Check(WorldgroupPrngRuntimeRegistry.RandMax == 32767, "rand-max-32767");

        var random = new RuntimeClassicWorldgroupPrng(1234u);
        var expected = new (uint Seed, ushort Value)[]
        {
            (2236414843u, 1356),
            (4056779384u, 29133),
            (3589159641u, 21998),
            (1770671342u, 27018),
            (4139675975u, 30398),
            (904336820u, 13799),
            (3871102533u, 26300),
            (3230196298u, 16520),
            (1766679891u, 26957),
            (1410548784u, 21523),
        };
        for (var i = 0; i < expected.Length; i++)
        {
            var draw = random.Rand();
            Check(draw.NextSeed == expected[i].Seed && draw.Value == expected[i].Value, $"borland-vector-{i + 1:00}");
            Check(draw.Sequence == i + 1, $"borland-sequence-{i + 1:00}");
        }

        random.Srand(0u);
        Check(random.Seed == 0u && random.Sequence == 0, "srand-zero-preserved");
        var zeroDraw = random.Rand();
        Check(zeroDraw.NextSeed == 1u && zeroDraw.Value == 0, "uint32-zero-seed-transition");

        random.Srand(uint.MaxValue);
        var overflowDraw = random.Rand();
        Check(overflowDraw.NextSeed == unchecked(uint.MaxValue * 22695477u + 1u), "uint32-modulo-overflow");

        Check(WorldgroupPrngRuntimeRegistry.PackStartupTimeSeed(0, 0, 0) == 0, "startup-seed-midnight");
        Check(WorldgroupPrngRuntimeRegistry.PackStartupTimeSeed(0, 0, 1) == 0, "startup-seed-second-pair-low");
        Check(WorldgroupPrngRuntimeRegistry.PackStartupTimeSeed(0, 0, 2) == 1, "startup-seed-two-second-step");
        Check(WorldgroupPrngRuntimeRegistry.PackStartupTimeSeed(12, 34, 56) == 25692, "startup-seed-midday-vector");
        Check(WorldgroupPrngRuntimeRegistry.PackStartupTimeSeed(23, 59, 58) == 49021, "startup-seed-last-pair");
        Check(WorldgroupPrngRuntimeRegistry.PackStartupTimeSeed(23, 59, 59) == 49021, "startup-seed-two-second-resolution");

        var msvcSeed = unchecked(1234u * 214013u + 2531011u);
        var msvcValue = (ushort)((msvcSeed >> 16) & 0x7FFFu);
        Check(msvcSeed != 2236414843u && msvcValue != 1356, "msvc-lcg-candidate-rejected");

        var rangeRows = WorldgroupExportImplementationRegistry.PriorityExports
            .Where(x => x.Symbol is "_genrdn" or "_lngrnd")
            .ToArray();
        Check(rangeRows.Length == 2 && rangeRows.All(x => x.EvidenceClass == "proven"), "host-range-helpers-proven");
        Check(rangeRows.All(x => x.Semantics.Contains("exclusiveMaximum", StringComparison.Ordinal) && x.Semantics.Contains("shared host PRNG", StringComparison.Ordinal)), "host-range-exclusive-shared-contract");

        return new(failures.Count == 0, checks, failures);
    }
}
