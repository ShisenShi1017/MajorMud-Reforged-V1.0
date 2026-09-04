namespace RealmGate.Server;
public static class RuntimePhysicalWeaponAttackExecutionSmokeReport
{
    private sealed class Sink:IRuntimePhysicalDamageSink{public readonly Dictionary<string,int> Hp=new(StringComparer.OrdinalIgnoreCase);public object Capture(string id)=>Hp[id];public int ApplyDamage(string id,int n,string a)=>Hp[id]=Math.Max(0,Hp[id]-n);public void Restore(string id,object s)=>Hp[id]=(int)s;}
    public static IReadOnlyDictionary<string,bool> Run()
    {
        var policies=new[]{new RuntimeCombatSchedulingPolicy("c","character",10,4,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject"),new RuntimeCombatSchedulingPolicy("n","npc",7,4,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject")};
        var combat=new RuntimeCombatInstanceLifecycle("combat.160","room.1",0,policies);combat.AddParticipant("character.1","character",0);combat.AddParticipant("npc.1","npc",0);var turn=combat.Advance(4).First();
        var sink=new Sink();sink.Hp["npc.1"]=20;var exec=new RuntimePhysicalWeaponAttackExecutor(combat,sink);
        var attack=new RuntimePhysicalAttackProfile("attack.fixture","one_handed",10,10,100,0,1,1,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject");
        var binding=new RuntimePhysicalWeaponBinding("binding.item.1622","item.1622","one_handed",true,false,new[]{"fixture:item.1622"},RuntimeResolutionConfidence.StronglyCorrelated,"reject");
        var mitigation=new RuntimePhysicalMitigationProfile("mitigation.fixture",0,1,1,15000,99,99,0,2,1,2000,1,new[]{"hit","defense","critical","armor","resistance","block","minimum"},new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject");
        var req=new RuntimePhysicalWeaponAttackRequest(new RuntimePhysicalAttackRequest("attack.160",turn,"room.1","character.1","npc.1","item.1622",attack,0,0,1),binding,mitigation,true,true,0,0,1);
        var trace=exec.Execute(req);
        return new Dictionary<string,bool>{["binding_enforced"]=trace.BindingId=="binding.item.1622",["critical_and_mitigation_ordered"]=trace.Critical&&trace.RawDamage==10&&trace.CriticalDamage==15&&trace.ArmorReducedDamage==14&&trace.ResistanceReducedDamage==12&&trace.BlockReducedDamage==10&&trace.FinalDamage==10,["atomic_damage_applied"]=sink.Hp["npc.1"]==10,["single_use_turn"]=Throws(()=>exec.Execute(req)),["unresolved_policy_rejects"]=Throws(()=>exec.Execute(req with{BaseRequest=req.BaseRequest with{AttackId="attack.161"},Mitigation=mitigation with{ArmorFlatReduction=null}}))};
    }
    private static bool Throws(Action a){try{a();return false;}catch{return true;}}
}
