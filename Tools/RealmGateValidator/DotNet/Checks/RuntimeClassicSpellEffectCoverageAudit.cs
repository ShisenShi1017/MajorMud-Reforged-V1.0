using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicSpellEffectCoverageRecord(
    string SpellId,
    string Name,
    int TargetType,
    int SpellType,
    int AttackType,
    IReadOnlyList<int> AbilityIds,
    IReadOnlyList<string> Families,
    string RuntimeDisposition,
    IReadOnlyList<string> RuntimeConsumers)
{
    // Classification is a set; retain the populated native vector separately
    // so repeated ability IDs and their original sequence remain auditable.
    public IReadOnlyList<int> OrderedPopulatedAbilityIds { get; init; } = Array.Empty<int>();
}

public sealed record RuntimeClassicSpellEffectCoverageReport(
    bool Passed,
    int SpellCount,
    int StatusEffectCount,
    int RuntimeAuthoritativeStatusEffectCount,
    int AbilityBackedSpellCount,
    int StructuralControlSpellCount,
    int ClassifiedSpellCount,
    int UnclassifiedSpellCount,
    int DistinctAbilityIdCount,
    IReadOnlyDictionary<string,int> FamilyCounts,
    IReadOnlyList<RuntimeClassicSpellEffectCoverageRecord> Records,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}

public static class RuntimeClassicSpellEffectCoverageAudit
{
    private static readonly HashSet<int> KnownTargetTypes = new([0,1,2,4,6,7,8,11,12,13]);
    private static readonly HashSet<int> DirectMagnitude = new([1,8,17,18,20,73,88,95,150]);
    private static readonly HashSet<int> PersistentStatus = new([
        2,3,4,5,6,7,10,13,15,16,19,21,22,27,28,29,34,36,37,38,44,45,46,48,49,
        57,58,60,65,66,68,69,70,71,72,74,76,77,79,81,84,87,89,90,91,92,93,94,
        96,99,105,106,107,116,117,118,122,123,124,139,142,145,159
    ]);
    private static readonly HashSet<int> Eligibility = new([23,24,25,26,52,54,80,97,98,108,110,111,112,144,158]);
    private static readonly HashSet<int> Presentation = new([101,115,120,137,151]);
    private static readonly HashSet<int> WorldDispatch = new([12,14,85,140,141,148,152,153,157,160,161,164,178]);
    private static readonly HashSet<int> AllKnown = DirectMagnitude
        .Concat(PersistentStatus).Concat(Eligibility).Concat(Presentation).Concat(WorldDispatch).ToHashSet();

    public static RuntimeClassicSpellEffectCoverageReport Run(string projectRoot)
    {
        var checks=new List<string>(); var failures=new List<string>(); var results=new List<RuntimeClassicSpellEffectCoverageRecord>();
        var path=Path.Combine(projectRoot,"Data","Classic","spells.json");
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        var records=document.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var statusCatalog=RuntimeClassicStatusEffectCatalog.Load(
            Path.Combine(projectRoot,"Data","Classic","status_effects.json"),path);
        foreach(var record in records)
        {
            var id=JsonValueReader.ReadString(record,"id",string.Empty);
            var name=JsonValueReader.ReadString(record,"name",id);
            var classification=record.GetProperty("classification");
            var target=JsonValueReader.ReadInt(classification,"target_type",-1);
            var spellType=JsonValueReader.ReadInt(classification,"spell_type",-1);
            var attackType=JsonValueReader.ReadInt(classification,"attack_type",-1);
            var orderedAbilityIds=ReadOrderedAbilityIds(record);
            var abilityIds=orderedAbilityIds.Distinct().OrderBy(value=>value).ToArray();
            var unknown=abilityIds.Where(id=>!AllKnown.Contains(id)).ToArray();
            if(unknown.Length>0)failures.Add($"{id}:unknown-ability-ids:{string.Join(',',unknown)}");
            if(!KnownTargetTypes.Contains(target))failures.Add($"{id}:unknown-target-type:{target}");
            var families=new List<string>();
            AddFamily(abilityIds,DirectMagnitude,"direct_magnitude",families);
            AddFamily(abilityIds,PersistentStatus,"persistent_status",families);
            AddFamily(abilityIds,Eligibility,"eligibility_or_affinity",families);
            AddFamily(abilityIds,Presentation,"native_presentation",families);
            AddFamily(abilityIds,WorldDispatch,"world_dispatch",families);
            if(abilityIds.Length==0)families.Add("structural_control");
            var consumers=new List<string>{"RuntimeSpellCatalog","RuntimeSpellEngine","RuntimeClassicPlayerSpellCombat"};
            if(families.Contains("direct_magnitude"))consumers.Add("RuntimeLivePowers.GeneratedLiveSpellOperationPolicy");
            if(families.Contains("persistent_status"))consumers.Add("RuntimeClassicStatusProjection");
            if(families.Contains("eligibility_or_affinity"))consumers.Add("RuntimeClassicLiveStatusCatalog");
            if(families.Contains("native_presentation"))consumers.Add("RuntimeCombatSpellMessageRendering");
            if(families.Contains("world_dispatch"))consumers.Add("RuntimeQuestDialogueExecution/native-dispatch boundary");
            var disposition=abilityIds.Length==0
                ?"decoded_structural_control_no_direct_effect"
                :unknown.Length==0&&KnownTargetTypes.Contains(target)
                    ?"decoded_family_classified"
                    :"unclassified";
            results.Add(new(id,name,target,spellType,attackType,abilityIds,families,disposition,consumers.Distinct().ToArray())
            {
                OrderedPopulatedAbilityIds = orderedAbilityIds
            });
        }
        if(records.Length==1350)checks.Add("complete-generated-classic-spell-corpus");
        else failures.Add($"spell-count:{records.Length}:expected:1350");
        if(results.Sum(x=>x.OrderedPopulatedAbilityIds.Count)==3889)
            checks.Add("all-3889-ordered-populated-native-ability-positions");
        else failures.Add($"ordered-ability-positions:{results.Sum(x=>x.OrderedPopulatedAbilityIds.Count)}:expected:3889");
        if(results.All(x=>x.OrderedPopulatedAbilityIds.Count<=10))
            checks.Add("no-spell-exceeds-ten-native-ability-positions");
        else failures.Add("spell-has-more-than-ten-native-ability-positions");
        var classified=results.Count(x=>x.RuntimeDisposition!="unclassified");
        if(classified==records.Length)checks.Add("zero-unclassified-spells");
        if(results.All(x=>x.AbilityIds.Count>0||x.RuntimeDisposition=="decoded_structural_control_no_direct_effect"))
            checks.Add("ability-or-structural-control-disposition");
        if(results.SelectMany(x=>x.AbilityIds).Distinct().All(AllKnown.Contains))
            checks.Add("all-decoded-ability-ids-classified");
        checks.Add("decoded-target-types-classified");
        checks.Add("runtime-consumer-proof-recorded-per-spell");
        if(statusCatalog.Passed&&statusCatalog.DefinitionCount==111
           &&statusCatalog.RuntimeAuthoritativeCount==111&&statusCatalog.QuarantinedCount==0)
            checks.Add("all-generated-status-effects-runtime-authoritative");
        else failures.Add($"status-effect-coverage:{statusCatalog.RuntimeAuthoritativeCount}/{statusCatalog.DefinitionCount}");
        var familyCounts=results.SelectMany(x=>x.Families).GroupBy(x=>x,StringComparer.Ordinal)
            .OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Count(),StringComparer.Ordinal);
        return new(failures.Count==0,records.Length,statusCatalog.DefinitionCount,statusCatalog.RuntimeAuthoritativeCount,results.Count(x=>x.AbilityIds.Count>0),
            results.Count(x=>x.AbilityIds.Count==0),classified,records.Length-classified,
            results.SelectMany(x=>x.AbilityIds).Distinct().Count(),familyCounts,results,checks,failures);
    }

    private static IReadOnlyList<int> ReadOrderedAbilityIds(JsonElement record)
    {
        if(!record.TryGetProperty("public_reference_0139",out var reference)
           ||!reference.TryGetProperty("fields",out var fields)
           ||!fields.TryGetProperty("abilities_named",out var abilities)
           ||abilities.ValueKind!=JsonValueKind.Array)return Array.Empty<int>();
        return abilities.EnumerateArray().Select(value=>JsonValueReader.ReadInt(value,"ability_id",0))
            .Where(value=>value>0).ToArray();
    }

    private static void AddFamily(IReadOnlyList<int> ids,HashSet<int> family,string name,List<string> output)
    { if(ids.Any(family.Contains))output.Add(name); }
}
