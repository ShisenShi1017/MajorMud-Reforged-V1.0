namespace Reforged.Server;
public static class RuntimePhysicalAttackExecutionSmokeReport
{
    private sealed class Sink : IRuntimePhysicalDamageSink { public readonly Dictionary<string,int> Hp=new(StringComparer.OrdinalIgnoreCase); public object Capture(string id)=>Hp[id]; public int ApplyDamage(string id,int n,string a)=>Hp[id]=Math.Max(0,Hp[id]-n); public void Restore(string id,object s)=>Hp[id]=(int)s; }
    public static IReadOnlyDictionary<string,bool> Run()
    {
        var policies=new[]{new RuntimeCombatSchedulingPolicy("c","character",10,4,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject"),new RuntimeCombatSchedulingPolicy("n","npc",7,4,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject")};
        var combat=new RuntimeCombatInstanceLifecycle("combat.159","room.1",0,policies); combat.AddParticipant("character.1","character",0); combat.AddParticipant("npc.1","npc",0); var turn=combat.Advance(4).First();
        var sink=new Sink(); sink.Hp["npc.1"]=3; var exec=new RuntimePhysicalAttackExecutor(combat,sink);
        var profile=new RuntimePhysicalAttackProfile("attack.fixture","unarmed",3,3,100,0,1,1,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject");
        var req=new RuntimePhysicalAttackRequest("attack.1",turn,"room.1","character.1","npc.1","ability.1",profile,0,0,1);
        var trace=exec.Execute(req);
        return new Dictionary<string,bool>{["scheduled_turn_executes"]=trace.Hit,["damage_is_atomic"]=sink.Hp["npc.1"]==0,["defeat_enters_combat_lifecycle"]=trace.DefenderDefeated&&combat.State==RuntimeCombatInstanceState.Terminated,["turn_is_single_use"]=Throws(()=>exec.Execute(req)),["unresolved_profile_rejects"]=Throws(()=>{var c=new RuntimeCombatInstanceLifecycle("combat.160","room.1",0,policies);c.AddParticipant("character.2","character",0);c.AddParticipant("npc.2","npc",0);var t=c.Advance(4).First();new RuntimePhysicalAttackExecutor(c,new Sink()).Execute(new RuntimePhysicalAttackRequest("attack.2",t,"room.1","character.2","npc.2","ability.1",profile with{MinimumDamage=null},0,0,1));})};
    }
    private static bool Throws(Action a){try{a();return false;}catch{return true;}}
}
