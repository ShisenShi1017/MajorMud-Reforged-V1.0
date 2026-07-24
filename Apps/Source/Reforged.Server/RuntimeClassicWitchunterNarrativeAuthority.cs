using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicWitchunterScript(string ScriptId, int NpcId, int RootTextblockId, int ActionTextblockId, int StageSlot, IReadOnlyList<RuntimeClassicTextblockOperation> Operations);
public sealed record RuntimeClassicWitchunterNarrativeReport(bool Passed, int ScriptCount, int NpcCount, int OperationCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{ public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }); }

/// <summary>Canonical Bundle F narrative authority for the decoded MageBane/Witchunter chain. Bundle A owns stage/entitlement, Bundle C owns item mutations and item-use gates, Bundle F owns decoded routing and atomic composition.</summary>
public sealed class RuntimeClassicWitchunterNarrativeAuthority
{
    public const int AbilityId = 50;
    public const int WitchunterClassLegacyId = 2;
    public const int FirstHalfMinimumLevel = 15;
    public const int FinalItemUseMinimumLevel = 50;
    public const int DrakaBladeItemId = 1334;
    public IReadOnlyList<RuntimeClassicWitchunterScript> Scripts { get; }
    private RuntimeClassicWitchunterNarrativeAuthority(IReadOnlyList<RuntimeClassicWitchunterScript> scripts) => Scripts = scripts;

    public static RuntimeClassicWitchunterNarrativeAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "witchunter_magebane_narrative.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Witchunter narrative catalog is required.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        if (root.GetProperty("ability_id").GetInt32() != AbilityId || root.GetProperty("class_legacy_id").GetInt32() != WitchunterClassLegacyId || root.GetProperty("first_half_minimum_level").GetInt32() != FirstHalfMinimumLevel || root.GetProperty("final_item_use_minimum_level").GetInt32() != FinalItemUseMinimumLevel || root.GetProperty("draka_blade_item_id").GetInt32() != DrakaBladeItemId)
            throw new InvalidDataException("Witchunter narrative identity/gates mismatch.");
        var scripts = new List<RuntimeClassicWitchunterScript>();
        foreach (var s in root.GetProperty("scripts").EnumerateArray())
        {
            var ops = new List<RuntimeClassicTextblockOperation>();
            foreach (var o in s.GetProperty("operations").EnumerateArray())
            {
                var opcode=o.GetProperty("opcode").GetString() ?? string.Empty;
                var nums=o.TryGetProperty("numeric_arguments",out var na)?na.EnumerateArray().Select(x=>x.GetInt32()).ToArray():Array.Empty<int>();
                var args=o.TryGetProperty("arguments",out var aa)?aa.EnumerateArray().Select(x=>x.GetString()??string.Empty).ToArray():Array.Empty<string>();
                int? target=o.TryGetProperty("target_textblock_id",out var tt)?tt.GetInt32():null;
                int? item=o.TryGetProperty("item_id",out var ii)?ii.GetInt32():null;
                int? ability=o.TryGetProperty("ability_id",out var ab)?ab.GetInt32():null;
                ops.Add(new(o.GetProperty("line").GetInt32(),o.GetProperty("sequence").GetInt32(),o.GetProperty("raw").GetString()??string.Empty,opcode,o.GetProperty("operation_kind").GetString()??string.Empty,"verified",args,nums,target,item,null,ability,null,null,null));
            }
            scripts.Add(new(s.GetProperty("script_id").GetString()??string.Empty,s.GetProperty("npc_id").GetInt32(),s.GetProperty("root_textblock_id").GetInt32(),s.GetProperty("action_textblock_id").GetInt32(),s.GetProperty("stage_slot").GetInt32(),ops));
        }
        if (scripts.Count != 12 || scripts.Select(x=>x.ScriptId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=12) throw new InvalidDataException("Complete twelve-script Witchunter chain required.");
        return new(scripts);
    }

    public RuntimeClassicTextblockExecutionPlan CreatePlan(string scriptId)
    {
        var s=Scripts.SingleOrDefault(x=>x.ScriptId.Equals(scriptId,StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException(scriptId);
        return new(s.RootTextblockId,s.ActionTextblockId,s.ActionTextblockId,"witchunter",s.Operations,s.Operations.Where(x=>x.TargetTextblockId.HasValue).Select(x=>x.TargetTextblockId!.Value).Distinct().ToArray(),null);
    }
}

public static class RuntimeBundleFPhaseF8WitchunterAudit
{
    public static RuntimeClassicWitchunterNarrativeReport Run(string projectRoot)
    {
        var a=RuntimeClassicWitchunterNarrativeAuthority.Load(projectRoot); var checks=new List<string>(); var failures=new List<string>();
        void Check(bool ok,string n){if(ok)checks.Add(n);else failures.Add(n);}
        Check(a.Scripts.Count==12,"all-twelve-decoded-magebane-scripts-projected");
        Check(a.Scripts.Select(x=>x.NpcId).Order().SequenceEqual(new[]{257,257,257,257,532,532,542,542,542,542,831,831}),"all-four-authoritative-npc-families-preserved");
        var ops=a.Scripts.SelectMany(x=>x.Operations).ToArray();
        Check(ops.Any(x=>x.Opcode=="minlevel"&&x.NumericArguments.Contains(15)),"level-15-first-half-gate-live");
        Check(ops.Any(x=>x.Opcode=="giveitem"&&x.ItemId==646),"first-half-item-646-grant-live");
        Check(ops.Any(x=>x.Opcode=="checkitem"&&x.ItemId==1183)&&ops.Any(x=>x.Opcode=="takeitem"&&x.ItemId==1183),"item-1183-atomic-turnin-live");
        Check(ops.Any(x=>x.Opcode=="checkability"&&x.AbilityId==50)&&ops.Any(x=>x.Opcode is "giveability" or "addability"&&x.AbilityId==50),"magebane-stage-condition-and-mutation-live");
        Check(ops.Any(x=>x.Opcode=="minlevel"&&x.NumericArguments.Contains(50)),"level-50-final-dialogue-gate-live");
        Check(ops.Count(x=>x.Opcode=="random")==2,"decoded-dual-random-reward-branches-preserved");
        var dispatcher=new RuntimeClassicNarrativeOperationDispatcher();
        foreach(var s in a.Scripts){var projected=RuntimeClassicQuestConditionAndTransactionAuthority.Project(a.CreatePlan(s.ScriptId),dispatcher);Check(projected.RequiresAtomicTransaction==$"{s.Operations.Any(x=>x.Opcode is "giveitem" or "takeitem" or "giveability" or "addability")}".Equals("True",StringComparison.OrdinalIgnoreCase) || projected.RequiresAtomicTransaction,$"{s.ScriptId}-owner-routing-projected");}
        return new(failures.Count==0,a.Scripts.Count,a.Scripts.Select(x=>x.NpcId).Distinct().Count(),ops.Length,failures.Count,checks,failures);
    }
}
