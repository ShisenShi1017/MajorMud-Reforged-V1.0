using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcDefensiveProfile(
    string NpcId,
    int ArmorClass,
    int DamageResistance,
    int MagicResistance,
    int DefenseValue,
    int DodgeBonus,
    string DefenseAuthority,
    string DodgeAuthority)
{
    public ClassicCombatantStats ToCombatantStats(int attackValue = 0, int criticalChance = 0)
        => new(attackValue, DefenseValue, ArmorClass, DamageResistance, DodgeBonus, 0, criticalChance);
}

public static class RuntimeNpcDefensiveProfileResolver
{
    public static RuntimeNpcDefensiveProfile Resolve(RuntimeNpcDefinition npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (string.IsNullOrWhiteSpace(npc.Id))
            throw new InvalidOperationException("NPC_DEFENSE_ID_MISSING");
        if (npc.HitPoints <= 0)
            throw new InvalidOperationException($"NPC_DEFENSE_HP_INVALID:{npc.Id}");

        // Classic NPC defense is represented directly by the generated monster AC and DR fields.
        // The retained attack simulator's dodge input is explicitly user-side (nUserDodge), and
        // the generated NPC schema contains no separate NPC dodge field. Therefore no additional
        // defense or dodge value is synthesized here.
        return new RuntimeNpcDefensiveProfile(
            npc.Id,
            npc.ArmorClass,
            npc.DamageResistance,
            npc.MagicResistance,
            DefenseValue: 0,
            DodgeBonus: 0,
            DefenseAuthority: "generated-npc-armor-class-and-damage-resistance",
            DodgeAuthority: "not-applicable-to-npc-defender; retained-simulator-input-is-player-side");
    }
}

public sealed class RuntimeNpcDefensiveProfileSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public int NpcCount { get; private set; }
    public int ReadyProfileCount { get; private set; }
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeNpcDefensiveProfileSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcDefensiveProfileSmokeReport();
        try
        {
            var dataRoot = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "npcs.json")));
            var npcs = document.RootElement.GetProperty("records")
                .EnumerateArray()
                .Select(RuntimeNpcDefinition.FromJson)
                .ToArray();

            report.NpcCount = npcs.Length;
            Require(npcs.Length == 1100, "npc-count");
            report.Checks.Add("npc-count");

            var profiles = npcs.Select(RuntimeNpcDefensiveProfileResolver.Resolve).ToArray();
            report.ReadyProfileCount = profiles.Length;
            Require(profiles.Length == npcs.Length, "all-npc-defensive-profiles-ready");
            report.Checks.Add("all-npc-defensive-profiles-ready");

            Require(profiles.All(x => x.DefenseValue == 0), "no-synthetic-npc-defense-value");
            report.Checks.Add("no-synthetic-npc-defense-value");

            Require(profiles.All(x => x.DodgeBonus == 0), "no-synthetic-npc-dodge-value");
            report.Checks.Add("no-synthetic-npc-dodge-value");

            Require(profiles.Any(x => x.ArmorClass != 0), "generated-npc-armor-class");
            report.Checks.Add("generated-npc-armor-class");

            Require(profiles.Any(x => x.DamageResistance > 0), "generated-npc-damage-resistance");
            report.Checks.Add("generated-npc-damage-resistance");

            var sample = profiles.First(x => x.ArmorClass != 0 || x.DamageResistance > 0);
            var combatant = sample.ToCombatantStats();
            Require(combatant.ArmorClass == sample.ArmorClass && combatant.DamageResistance == sample.DamageResistance,
                "classic-combatant-mapping");
            report.Checks.Add("classic-combatant-mapping");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }

        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
