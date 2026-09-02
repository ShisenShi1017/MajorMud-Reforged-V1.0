using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.Server;

public sealed record RuntimeCanonicalSpellRecord(string Id, string LegacyId, string Name, int SpellLevel, int SpellTypeRaw, int TargetTypeRaw, int AttackTypeRaw, int ManaCost, int EnergyCost, int MinimumEffect, int MaximumEffect, int DurationRaw, int ResistTypeRaw, int ResistAbilityRaw, int DifficultyRaw, IReadOnlyList<string> EvidenceReferences, string Confidence)
{
    public int EffectRollMinimum => Math.Min(MinimumEffect, MaximumEffect);
    public int EffectRollMaximum => Math.Max(MinimumEffect, MaximumEffect);
}
public sealed record RuntimeCanonicalWeaponRecord(string Id, string LegacyId, string Name, int MinimumDamage, int MaximumDamage, int Accuracy, int Speed, int WeaponTypeRaw, IReadOnlyList<string> EvidenceReferences, string Confidence);
public sealed record RuntimeCanonicalAbilityRecord(string Id, string LegacyId, string Name, string? Description, string? ImplementationStatus, IReadOnlyList<string> EvidenceReferences, string Confidence);
public sealed record RuntimeCanonicalCombatSpellCatalog(IReadOnlyDictionary<string,RuntimeCanonicalSpellRecord> Spells, IReadOnlyDictionary<string,RuntimeCanonicalWeaponRecord> Weapons, IReadOnlyDictionary<string,RuntimeCanonicalAbilityRecord> Abilities);

public static class RuntimeCanonicalCombatSpellCatalogLoader
{
    private static readonly Regex SpellId = new(@"^spell\.\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ItemId = new(@"^item\.\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AbilityId = new(@"^ability\.\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static RuntimeCanonicalCombatSpellCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("CANONICAL_CATALOG_PATH_REQUIRED");
        var records=RuntimeChunkedClassicCatalog.LoadRecords(path);
        var spells=new Dictionary<string,RuntimeCanonicalSpellRecord>(StringComparer.OrdinalIgnoreCase);
        var weapons=new Dictionary<string,RuntimeCanonicalWeaponRecord>(StringComparer.OrdinalIgnoreCase);
        var abilities=new Dictionary<string,RuntimeCanonicalAbilityRecord>(StringComparer.OrdinalIgnoreCase);
        foreach(var e in records.Where(e=>S(e,"record_type").Equals("spell",StringComparison.OrdinalIgnoreCase)))
        {
            var r=new RuntimeCanonicalSpellRecord(S(e,"id"),S(e,"legacy_id"),S(e,"name"),I(e,"spell_level"),I(e,"spell_type_raw"),I(e,"target_type_raw"),I(e,"attack_type_raw"),I(e,"mana_cost"),I(e,"energy_cost"),I(e,"minimum_effect"),I(e,"maximum_effect"),I(e,"duration_raw"),I(e,"resist_type_raw"),I(e,"resist_ability_raw"),I(e,"difficulty_raw"),Refs(e),S(e,"confidence"));
            ValidateSpell(r); if(!spells.TryAdd(r.Id,r)) throw new InvalidOperationException("CANONICAL_SPELL_DUPLICATE");
        }
        foreach(var e in records.Where(e=>S(e,"record_type").Equals("weapon",StringComparison.OrdinalIgnoreCase)))
        {
            var r=new RuntimeCanonicalWeaponRecord(S(e,"id"),S(e,"legacy_id"),S(e,"name"),I(e,"minimum_damage"),I(e,"maximum_damage"),I(e,"accuracy"),I(e,"speed"),I(e,"weapon_type_raw"),Refs(e),S(e,"confidence"));
            Validate(r.Id,ItemId,r.MinimumDamage,r.MaximumDamage,r.EvidenceReferences); if(!weapons.TryAdd(r.Id,r)) throw new InvalidOperationException("CANONICAL_WEAPON_DUPLICATE");
        }
        foreach(var e in records.Where(e=>S(e,"record_type").Equals("abilitie",StringComparison.OrdinalIgnoreCase)
            || S(e,"record_type").Equals("ability",StringComparison.OrdinalIgnoreCase)))
        {
            var r=new RuntimeCanonicalAbilityRecord(S(e,"id"),S(e,"legacy_id"),S(e,"name"),N(e,"description"),N(e,"implementation_status"),Refs(e),S(e,"confidence"));
            if(!AbilityId.IsMatch(r.Id)||r.EvidenceReferences.Count==0) throw new InvalidOperationException("CANONICAL_ABILITY_INVALID"); if(!abilities.TryAdd(r.Id,r)) throw new InvalidOperationException("CANONICAL_ABILITY_DUPLICATE");
        }
        if(spells.Count==0||weapons.Count==0||abilities.Count==0) throw new InvalidOperationException("CANONICAL_CATALOG_EMPTY");
        return new(spells,weapons,abilities);
    }
    public static RuntimeCanonicalSpellRecord RequireSpell(RuntimeCanonicalCombatSpellCatalog c,string id)=>c.Spells.TryGetValue(id,out var x)?x:throw new InvalidOperationException("CANONICAL_SPELL_UNBOUND");
    public static RuntimeCanonicalWeaponRecord RequireWeapon(RuntimeCanonicalCombatSpellCatalog c,string id)=>c.Weapons.TryGetValue(id,out var x)?x:throw new InvalidOperationException("CANONICAL_WEAPON_UNBOUND");
    public static RuntimeCanonicalAbilityRecord RequireAbility(RuntimeCanonicalCombatSpellCatalog c,string id)=>c.Abilities.TryGetValue(id,out var x)?x:throw new InvalidOperationException("CANONICAL_ABILITY_UNBOUND");
    private static void ValidateSpell(RuntimeCanonicalSpellRecord record)
    {
        if(!SpellId.IsMatch(record.Id) || record.EvidenceReferences.Count == 0)
            throw new InvalidOperationException("CANONICAL_SPELL_INVALID");
        _ = checked(record.EffectRollMaximum - record.EffectRollMinimum);
    }
    private static void Validate(string id,Regex regex,int min,int max,IReadOnlyList<string> refs){if(!regex.IsMatch(id)||min>max||refs.Count==0)throw new InvalidOperationException("CANONICAL_RECORD_INVALID");}
    private static string S(JsonElement e,string n)=>e.GetProperty(n).GetString()??string.Empty;
    private static string? N(JsonElement e,string n)=>e.TryGetProperty(n,out var v)&&v.ValueKind!=JsonValueKind.Null?v.GetString():null;
    private static int I(JsonElement e,string n)
    {
        if(!e.TryGetProperty(n,out var value)) return 0;
        if(value.ValueKind==JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if(value.ValueKind==JsonValueKind.String && int.TryParse(value.GetString(),out number)) return number;
        return 0;
    }
    private static IReadOnlyList<string> Refs(JsonElement e)=>e.GetProperty("evidence_refs").EnumerateArray().Select(x=>x.GetProperty("path").GetString()+"#"+(x.TryGetProperty("row_key",out var r)?r.GetString():"record")).ToArray();
}
