using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeItemCombatProfile(
    string ItemId,
    int MinimumDamage,
    int MaximumDamage,
    int ArmorClassBonus,
    int DamageResistanceBonus,
    int Accuracy,
    int Speed,
    int WeaponType,
    int ArmorType,
    bool IsArmor);

public sealed class RuntimeItemCombatCatalog
{
    private readonly Dictionary<string, RuntimeItemCombatProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<RuntimeItemCombatProfile> Profiles => _profiles.Values;

    public static RuntimeItemCombatCatalog Load(string projectRoot)
    {
        var resolver = ServerDataRootResolver.ResolveDefault(projectRoot);
        var path = Path.Combine(resolver.DataRoot, "items.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var catalog = new RuntimeItemCombatCatalog();
        foreach (var item in document.RootElement.GetProperty("records").EnumerateArray())
        {
            if (!item.TryGetProperty("combat", out var combat) || combat.ValueKind != JsonValueKind.Object) continue;
            var id = JsonValueReader.ReadString(item, "id", string.Empty);
            if (id.Length == 0) continue;
            catalog._profiles[id] = new RuntimeItemCombatProfile(
                id,
                JsonValueReader.ReadInt(combat, "minimum_damage", 0),
                JsonValueReader.ReadInt(combat, "maximum_damage", 0),
                JsonValueReader.ReadInt(combat, "armor_class_bonus", 0),
                JsonValueReader.ReadInt(combat, "damage_resistance_bonus", 0),
                JsonValueReader.ReadInt(combat, "accuracy", 0),
                JsonValueReader.ReadInt(combat, "speed", 0),
                JsonValueReader.ReadInt(combat, "weapon_type", 0),
                JsonValueReader.ReadInt(combat, "armor_type", 0),
                combat.TryGetProperty("is_armor", out var armor) && armor.ValueKind == JsonValueKind.True);
        }
        return catalog;
    }

    public bool TryGet(string itemId, out RuntimeItemCombatProfile profile) => _profiles.TryGetValue(itemId, out profile!);
}

public sealed class RuntimeCombatProfilePromotionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public int NpcCount { get; private set; }
    public int NpcAttackCount { get; private set; }
    public int ItemCombatProfileCount { get; private set; }
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeCombatProfilePromotionSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeCombatProfilePromotionSmokeReport();
        try
        {
            var resolver = ServerDataRootResolver.ResolveDefault(projectRoot);
            using var npcDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(resolver.DataRoot, "npcs.json")));
            var npcRecords = npcDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
            report.NpcCount = npcRecords.Length;
            report.NpcAttackCount = npcRecords.Sum(npc => npc.TryGetProperty("attacks", out var attacks) && attacks.ValueKind == JsonValueKind.Array ? attacks.GetArrayLength() : 0);
            Require(report.NpcCount == 1100, "npc-count"); report.Checks.Add("npc-count");
            Require(report.NpcAttackCount > 0, "npc-attacks-promoted"); report.Checks.Add("npc-attacks-promoted");
            Require(npcRecords.All(npc => npc.TryGetProperty("attack_profile_authority", out _)), "npc-attack-authority"); report.Checks.Add("npc-attack-authority");

            var itemCatalog = RuntimeItemCombatCatalog.Load(projectRoot);
            report.ItemCombatProfileCount = itemCatalog.Profiles.Count;
            Require(report.ItemCombatProfileCount == 1950, "item-combat-profiles-promoted"); report.Checks.Add("item-combat-profiles-promoted");
            Require(itemCatalog.Profiles.Any(x => x.MaximumDamage > 0), "weapon-damage-available"); report.Checks.Add("weapon-damage-available");
            Require(itemCatalog.Profiles.Any(x => x.ArmorClassBonus != 0 || x.DamageResistanceBonus != 0), "armor-defense-available"); report.Checks.Add("armor-defense-available");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
