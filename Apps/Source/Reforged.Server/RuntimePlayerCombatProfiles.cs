using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePlayerCombatProfile(
    string CharacterId,
    int AttackValue,
    int DefenseValue,
    int ArmorClass,
    int DamageResistance,
    int DodgeBonus,
    int CriticalChance,
    int MinimumDamage,
    int MaximumDamage,
    int WeaponAccuracyModifier,
    IReadOnlyList<string> EquippedCombatItems,
    IReadOnlyList<string> Blockers)
{
    public bool Ready => Blockers.Count == 0;
}

public sealed class RuntimePlayerCombatProfileResolver
{
    private readonly RuntimeItemCombatCatalog _items;

    public RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog items) => _items = items;

    public RuntimePlayerCombatProfile Resolve(RuntimeCharacterRecord character)
    {
        var blockers = new List<string>();
        var attackValue = Required(character.Conditions, "combat.attack_value", blockers);
        var defenseValue = Required(character.Conditions, "combat.defense_value", blockers);
        var dodgeBonus = Required(character.Conditions, "combat.dodge_bonus", blockers);
        var criticalChance = Optional(character.Conditions, "combat.critical_chance");
        var armorClass = Optional(character.Conditions, "combat.armor_class");
        var damageResistance = Optional(character.Conditions, "combat.damage_resistance");
        var minimumDamage = Optional(character.Conditions, "combat.minimum_damage");
        var maximumDamage = Optional(character.Conditions, "combat.maximum_damage");
        var weaponAccuracy = 0;
        var equipped = new List<string>();
        var weaponProfiles = new List<RuntimeItemCombatProfile>();

        foreach (var item in character.ItemState?.Items ?? Array.Empty<RuntimeItemInstance>())
        {
            if (!string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase) || item.EquippedSlot is null) continue;
            if (!_items.TryGet(item.CanonicalItemId, out var profile)) continue;
            equipped.Add(item.CanonicalItemId);
            armorClass = checked(armorClass + profile.ArmorClassBonus);
            damageResistance = checked(damageResistance + profile.DamageResistanceBonus);
            if (profile.MaximumDamage > 0) weaponProfiles.Add(profile);
        }

        if (weaponProfiles.Count > 1)
        {
            blockers.Add("PLAYER_COMBAT_MULTIPLE_WEAPON_PROFILES");
        }
        else if (weaponProfiles.Count == 1)
        {
            var weapon = weaponProfiles[0];
            minimumDamage = weapon.MinimumDamage;
            maximumDamage = weapon.MaximumDamage;
            weaponAccuracy = weapon.Accuracy;
        }
        else if (maximumDamage <= 0 || minimumDamage < 0 || maximumDamage < minimumDamage)
        {
            blockers.Add("PLAYER_COMBAT_DAMAGE_RANGE_MISSING");
        }

        return new RuntimePlayerCombatProfile(
            character.CharacterId,
            attackValue,
            defenseValue,
            armorClass,
            damageResistance,
            dodgeBonus,
            criticalChance,
            minimumDamage,
            maximumDamage,
            weaponAccuracy,
            equipped,
            blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static int Required(IReadOnlyDictionary<string, string> values, string key, List<string> blockers)
    {
        if (!values.TryGetValue(key, out var raw) || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            blockers.Add($"PLAYER_COMBAT_FIELD_MISSING:{key}");
            return 0;
        }
        return value;
    }

    private static int Optional(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}

public sealed class RuntimePlayerCombatProfileSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimePlayerCombatProfileSmokeReport Run(string projectRoot)
    {
        var report = new RuntimePlayerCombatProfileSmokeReport();
        try
        {
            var catalog = RuntimeItemCombatCatalog.Load(projectRoot);
            var weapon = catalog.Profiles.First(x => x.MaximumDamage > 0);
            var character = new RuntimeCharacterRecord(
                "character.test", "account.test", "Test", "Classic", "race.test", "Test", "class.test", "Test", 1, 0,
                10, 10, 0, 0, "room.1", 1,
                new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string?>(),
                new Dictionary<string, string>
                {
                    ["combat.attack_value"] = "50",
                    ["combat.defense_value"] = "40",
                    ["combat.dodge_bonus"] = "3"
                }, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                ItemState: new RuntimeItemState(new[]
                {
                    new RuntimeItemInstance("instance.weapon", weapon.ItemId, "character.test", RuntimeItemLocations.Equipment, "character.test", "MAIN_HAND", 0, 1, null)
                }, 0, new Dictionary<string, string>()));
            var profile = new RuntimePlayerCombatProfileResolver(catalog).Resolve(character);
            Require(profile.Ready, "profile-ready"); report.Checks.Add("profile-ready");
            Require(profile.AttackValue == 50 && profile.DefenseValue == 40 && profile.DodgeBonus == 3, "authoritative-character-fields"); report.Checks.Add("authoritative-character-fields");
            Require(profile.MinimumDamage == weapon.MinimumDamage && profile.MaximumDamage == weapon.MaximumDamage, "generated-weapon-damage"); report.Checks.Add("generated-weapon-damage");
            Require(profile.WeaponAccuracyModifier == weapon.Accuracy, "generated-weapon-accuracy"); report.Checks.Add("generated-weapon-accuracy");
            Require(typeof(RuntimePlayerCombatProfileResolver).GetMethod(nameof(RuntimePlayerCombatProfileResolver.Resolve)) is not null, "runtime-resolver"); report.Checks.Add("runtime-resolver");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
