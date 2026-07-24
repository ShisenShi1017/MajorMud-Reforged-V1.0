using System.Text.Json;

namespace Reforged.Server;

public sealed class ClassicCombatSmokeReport
{
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public bool Passed => Failures.Count == 0;

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static ClassicCombatSmokeReport Run()
    {
        var report = new ClassicCombatSmokeReport();
        Check(report, "normal-hit-formula", ClassicCombatResolver.CalculateHitChance(
            ClassicAttackType.NormalWeapon,
            new ClassicCombatantStats(100, 0, 0, 0, 0),
            new ClassicCombatantStats(0, 20, 10, 0, 0)) == 88);
        Check(report, "backstab-ignores-ac", ClassicCombatResolver.CalculateHitChance(
            ClassicAttackType.Backstab,
            new ClassicCombatantStats(80, 0, 0, 0, 0),
            new ClassicCombatantStats(0, 20, 999, 0, 0)) == 60);
        Check(report, "backstab-dodge-divided-by-five",
            ClassicCombatResolver.CalculateDodgeChance(ClassicAttackType.Backstab, 80, 10) == 2);

        var glance = new ClassicCombatResolver(new SequenceRandom(1, 100, 5, 100)).Resolve(new ClassicAttackRequest(
            ClassicAttackType.NormalWeapon,
            new ClassicCombatantStats(100, 0, 0, 0, 0, CriticalChance: 0),
            new ClassicCombatantStats(0, 0, 0, 5, 1),
            5,
            5));
        Check(report, "dr-produces-glance", glance.Outcome == ClassicHitOutcome.Glance && glance.FinalDamage == 0);

        var dodge = new ClassicCombatResolver(new SequenceRandom(1, 100, 10, 1)).Resolve(new ClassicAttackRequest(
            ClassicAttackType.NormalWeapon,
            new ClassicCombatantStats(80, 0, 0, 0, 0, CriticalChance: 0),
            new ClassicCombatantStats(0, 0, 0, 0, 10),
            10,
            10));
        Check(report, "dodge-negates-post-dr-hit", dodge.Outcome == ClassicHitOutcome.Dodged && dodge.FinalDamage == 0);

        var smash = new ClassicCombatResolver(new SequenceRandom(1, 12, 100)).Resolve(new ClassicAttackRequest(
            ClassicAttackType.Smash,
            new ClassicCombatantStats(200, 0, 0, 0, 0),
            new ClassicCombatantStats(0, 0, 0, 2, 0),
            10,
            10));
        Check(report, "smash-multiplier-after-dr", smash.FinalDamage == 50);
        return report;
    }

    private static void Check(ClassicCombatSmokeReport report, string name, bool passed)
    {
        report.Checks.Add(name);
        if (!passed)
        {
            report.Failures.Add(name);
        }
    }

    private sealed class SequenceRandom : IClassicRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            if (_values.Count == 0) throw new InvalidOperationException("No deterministic random value remains.");
            var value = _values.Dequeue();
            if (value < minimum || value > maximum) throw new InvalidOperationException($"Deterministic value {value} outside {minimum}..{maximum}.");
            return value;
        }
    }
}
