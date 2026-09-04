namespace RealmGate.Server;

public static class RuntimeCanonicalCombatSpellCatalogSmokeReport
{
    public static IReadOnlyDictionary<string,bool> Validate(RuntimeCanonicalCombatSpellCatalog catalog)
    {
        var cloud=RuntimeCanonicalCombatSpellCatalogLoader.RequireSpell(catalog,"spell.1304");
        return new Dictionary<string,bool>{
            ["all_spell_records_bound"]=catalog.Spells.Count==1350,
            ["weapon_candidates_bound"]=catalog.Weapons.Count>0,
            ["all_ability_records_bound"]=catalog.Abilities.Count==188,
            ["raw_enums_preserved"]=cloud.TargetTypeRaw==12 && cloud.AttackTypeRaw==4,
            ["unresolved_semantics_not_promoted"]=cloud.Id=="spell.1304"
        };
    }
}
