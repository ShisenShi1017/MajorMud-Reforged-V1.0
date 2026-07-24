namespace Reforged.Server;
public static class RuntimePhysicalAttackPathsSmokeReport
{
    private sealed class Sink:IRuntimePhysicalDamageSink
    {
        public readonly Dictionary<string,int> Hp=new(StringComparer.OrdinalIgnoreCase);
        public object Capture(string id)=>Hp[id];
        public int ApplyDamage(string id,int n,string a)=>Hp[id]=Math.Max(0,Hp[id]-n);
        public void Restore(string id,object s)=>Hp[id]=(int)s;
    }
    public static IReadOnlyDictionary<string,bool> Run()
    {
        var scheduling=new[]{new RuntimeCombatSchedulingPolicy("c","character",10,4,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject"),new RuntimeCombatSchedulingPolicy("n","npc",7,4,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject")};
        var combat=new RuntimeCombatInstanceLifecycle("combat.173","room.1",0,scheduling);combat.AddParticipant("character.1","character",0);combat.AddParticipant("npc.1","npc",0);var turn=combat.Advance(4).First();
        var sink=new Sink();sink.Hp["npc.1"]=100;
        var weapon=new RuntimePhysicalWeaponAttackExecutor(combat,sink);var exec=new RuntimePhysicalAttackPathExecutor(weapon);
        var profile=new RuntimePhysicalAttackProfile("attack.fixture","dual_wield_primary",10,10,100,0,1,1,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject");
        var binding=new RuntimePhysicalWeaponBinding("binding.item.1622","item.1622","dual_wield_primary",true,false,new[]{"fixture:item.1622"},RuntimeResolutionConfidence.StronglyCorrelated,"reject");
        var mitigation=new RuntimePhysicalMitigationProfile("mitigation.fixture",0,1,99,10000,99,99,99,0,0,0,0,new[]{"hit","defense","damage"},new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject");
        var template=new RuntimePhysicalWeaponAttackRequest(new RuntimePhysicalAttackRequest("attack.173",turn,"room.1","character.1","npc.1","item.1622",profile,0,0,1),binding,mitigation,true,true,0,0,0);
        var policy=new RuntimePhysicalAttackPathPolicy("path.dual.fixture",RuntimePhysicalAttackPath.DualWield,2,10000,5000,true,true,false,false,false,new[]{"fixture:dual"},RuntimeResolutionConfidence.Proven,"reject");
        var trace=exec.Execute(new RuntimePhysicalAttackPathRequest("attack-sequence.173",template,policy,true,true,false,true,false));
        var rangedRejects=Throws(()=>exec.Execute(new RuntimePhysicalAttackPathRequest("attack-sequence.174",template,policy with{Path=RuntimePhysicalAttackPath.Ranged,StrikeCount=1,RequiresOffHandWeapon=false,RequiresAmmunition=true},true,false,false,false,false)));
        return new Dictionary<string,bool>{["dual_wield_two_strikes"]=trace.ExecutedStrikeCount==2,["offhand_scaled"]=trace.Strikes[0].Trace.FinalDamage==10&&trace.Strikes[1].Trace.FinalDamage==5,["damage_applied"]=sink.Hp["npc.1"]==85,["single_use_sequence"]=Throws(()=>exec.Execute(new RuntimePhysicalAttackPathRequest("attack-sequence.173",template,policy,true,true,false,true,false))),["ranged_ammunition_fail_closed"]=rangedRejects};
    }
    private static bool Throws(Action a){try{a();return false;}catch{return true;}}
}
