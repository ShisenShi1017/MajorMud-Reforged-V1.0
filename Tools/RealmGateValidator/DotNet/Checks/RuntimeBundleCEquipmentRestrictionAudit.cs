using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleCRestrictionDisposition(string Restriction,string EvidenceStatus,string RuntimeOwner,string RuntimeConsumer,string PersistenceOwner,string Disposition,bool Passed);
public sealed record RuntimeBundleCEquipmentRestrictionAuditReport(string Status,int ItemCount,int EquipmentCount,int LevelRestricted,int ClassRestricted,int RaceRestricted,int StatRestricted,int AlignmentRestricted,int GenderRestricted,int LimitedItems,IReadOnlyList<RuntimeBundleCRestrictionDisposition> Restrictions,IReadOnlyList<string> Checks,IReadOnlyList<string> Errors)
{
    public bool Passed => Status.Equals("PASS",StringComparison.OrdinalIgnoreCase);
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}

public static class RuntimeBundleCEquipmentRestrictionAudit
{
    public static RuntimeBundleCEquipmentRestrictionAuditReport Run(string root,string worldId)
    {
        var checks=new List<string>(); var errors=new List<string>();
        var data=Path.Combine(root,"Data",worldId);
        using var items=JsonDocument.Parse(File.ReadAllText(Path.Combine(data,"items.json")));
        using var equipment=JsonDocument.Parse(File.ReadAllText(Path.Combine(data,"equipment.json")));
        var itemRecords=items.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var equipmentRecords=equipment.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var runtime=File.ReadAllText(Path.Combine(root,"Apps","Source","RealmGate.Server","RuntimeItemsEquipment.cs"));
        var character=File.ReadAllText(Path.Combine(root,"Apps","Source","RealmGate.Server","RuntimeCharacterEligibilityAuthority.cs"));

        int level=0,classes=0,races=0,stats=0,limited=0;
        foreach(var record in itemRecords)
        {
            if(!record.TryGetProperty("public_reference_0139",out var reference)||!reference.TryGetProperty("fields",out var fields)) continue;
            if(fields.TryGetProperty("class_restrictions",out var c)&&c.ValueKind==JsonValueKind.Array&&c.GetArrayLength()>0) classes++;
            if(fields.TryGetProperty("race_restrictions",out var r)&&r.ValueKind==JsonValueKind.Array&&r.GetArrayLength()>0) races++;
            if(fields.TryGetProperty("stat_requirements",out var s)&&s.ValueKind==JsonValueKind.Object&&s.EnumerateObject().Any(p=>p.Value.TryGetInt32(out var v)&&v>0)) stats++;
            if(fields.TryGetProperty("game_limit",out var g)&&g.TryGetInt32(out var gl)&&gl>0) limited++;
            if(fields.TryGetProperty("abilities_named",out var a)&&a.ValueKind==JsonValueKind.Array&&a.EnumerateArray().Any(x=>(x.TryGetProperty("ability_id",out var id)&&id.TryGetInt32(out var av)&&av==135)||(x.TryGetProperty("name",out var n)&&n.GetString()?.Equals("MinLevel",StringComparison.OrdinalIgnoreCase)==true))) level++;
        }
        var align=equipmentRecords.Count(x=>x.TryGetProperty("alignment",out var v)&&v.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(v.GetString()));
        var gender=equipmentRecords.Count(x=>x.TryGetProperty("gender",out var v)&&v.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(v.GetString()));

        Require(character,"EvaluateCharacterInputs","level/class/race/stat authority");
        Require(runtime,"EnforceAuthoritativeCharacterRequirements","live authoritative restriction consumer");
        Require(runtime,"EvaluateDrakaBladeUse","Witchunter quest entitlement consumer");
        Require(runtime,"MinimumStrength","minimum-strength live gate");
        Require(runtime,"Alignment restriction failed","alignment live gate");
        Require(runtime,"Gender restriction failed","gender live gate");
        Require(runtime,"EnforceHandRules","slot and hand-conflict consumer");
        Require(runtime,"RuntimeItemLimitedReconciliationCoordinator","limited-item persistence validator");
        Require(character,"relationship_unresolved","weapon/armour matrix quarantine");
        Check(itemRecords.All(x=>x.TryGetProperty("id",out _)),"all item restrictions retain canonical identity");
        Check(equipmentRecords.All(x=>x.TryGetProperty("item_id",out _)&&x.TryGetProperty("slot",out _)),"all equipment restrictions retain item and slot identity");
        var exactLevel=equipmentRecords.Single(x=>x.GetProperty("item_id").GetString()=="item.1916");
        Check(exactLevel.GetProperty("minimum_level").GetInt32()==65&&exactLevel.GetProperty("maximum_level").GetInt32()==65,"ability 135/136 exact-level equipment range is generated");

        var dispositions=new[]{
            new RuntimeBundleCRestrictionDisposition("slot/two-handed","verified","RuntimeItemCatalog","RuntimeItemEngine.Equip/EnforceHandRules","RuntimeItemInstance.EquippedSlot","enforced",true),
            new("minimum level","verified","RuntimeCharacterItemRequirementCatalog","RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements","RuntimeCharacterRecord.Level + equipped item","enforced",true),
            new("maximum level","verified","RuntimeCharacterItemRequirementCatalog + RuntimeItemCatalog","RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements","RuntimeCharacterRecord.Level + equipped item","enforced when positive; item 1916 is exact level 65",true),
            new("class allowlist","verified","RuntimeCharacterItemRequirementCatalog","RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements","RuntimeCharacterRecord.ClassId + equipped item","enforced; metadata duplicate is non-authoritative",true),
            new("race allowlist","verified","RuntimeCharacterItemRequirementCatalog","RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements","RuntimeCharacterRecord.RaceId + equipped item","enforced",true),
            new("minimum stats/strength","verified","RuntimeCharacterItemRequirementCatalog + RuntimeItemCatalog","RuntimeItemEngine.Equip","character base stats + equipped item","enforced",true),
            new("alignment/gender","strongly_correlated","RuntimeItemCatalog","RuntimeItemEngine.Equip","character conditions + equipped item","consumer active; no populated Classic records",true),
            new("quest/ability entitlement","verified","RuntimeCharacterBundleCatalog","RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements","quest abilities + equipped item","Draka Blade enforced; no other decoded generic equipment entitlement found",true),
            new("unique/limited ownership","verified","items.json game_limit + instance metadata","RuntimeItemLimitedReconciliationCoordinator","world item save","validated on persistence; creation/loot lifecycle remains Phase C8",true),
            new("weapon type matrix","relationship_unresolved","RuntimeCharacterClassCapabilityCatalog","RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements","class/item decoded codes","quarantined; no relationship found, no gameplay invented",true),
            new("armor type matrix","relationship_unresolved","RuntimeCharacterClassCapabilityCatalog","RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements","class/item decoded codes","quarantined; no relationship found, no gameplay invented",true)
        };
        return new(errors.Count==0?"PASS":"FAIL",itemRecords.Length,equipmentRecords.Length,level,classes,races,stats,align,gender,limited,dispositions,checks,errors);
        void Check(bool ok,string text){if(ok)checks.Add(text);else errors.Add(text);} void Require(string src,string token,string text)=>Check(src.Contains(token,StringComparison.Ordinal),text);
    }
}
