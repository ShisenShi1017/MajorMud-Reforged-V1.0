namespace Reforged.Server;

public sealed record RuntimeDirectSpellExecutionSmokeReport(bool Passed, IReadOnlyList<string> Failures);
public static class RuntimeDirectSpellExecutionSmoke
{
    private sealed class Sink : IRuntimeDirectSpellStateSink
    {
        public readonly Dictionary<string,RuntimeDirectSpellTargetState> States=new(StringComparer.OrdinalIgnoreCase);
        public RuntimeDirectSpellTargetState Capture(string id)=>States[id];
        public void Restore(string id,RuntimeDirectSpellTargetState s)=>States[id]=s;
        public void Apply(RuntimeDirectSpellMutation m)
        {
            var s=States[m.EntityId];
            States[m.EntityId]=m.Resource switch {
                RuntimeDirectSpellResourceKind.HitPoints=>s with { CurrentHitPoints=checked(s.CurrentHitPoints+m.Delta)},
                RuntimeDirectSpellResourceKind.Mana=>s with { CurrentMana=checked(s.CurrentMana+m.Delta)},
                _=>s with { CurrentEnergy=checked(s.CurrentEnergy+m.Delta)}};
        }
    }
    public static RuntimeDirectSpellExecutionSmokeReport Run()
    {
        var failures=new List<string>(); var sink=new Sink();
        sink.States["character.1"]=new("character.1",50,100,10,100,20,100);
        sink.States["npc.1"]=new("npc.1",80,100,0,0,0,0);
        var policy=new RuntimeDirectSpellFormulaPolicy("spellformula.test.1","spell.1",RuntimeDirectSpellFormulaKind.Drain,
            RuntimeDamageDisposition.Damage,RuntimeDirectSpellResourceKind.HitPoints,10,10,0,1,1,0,1,0,1000,10000,5000,0,999,
            RuntimeDirectSpellRoundingMode.TowardZero,false,false,true,RuntimeDirectSpellResourceKind.HitPoints,
            RuntimeResolutionConfidence.StronglyCorrelated,new[]{"evidence.test"},"Reject unresolved production coefficients.");
        var engine=new RuntimeDirectSpellExecution(new RuntimeDirectSpellFormulaCatalog(new[]{policy}),sink);
        var trace=engine.Execute(new("spellexecution.test.1","combat.1",1,"character.1","npc.1","spell.1",5,0,0,RuntimeSaveOutcome.Failed,7));
        if(trace.FinalMagnitude!=15||sink.States["npc.1"].CurrentHitPoints!=65||sink.States["character.1"].CurrentHitPoints!=65) failures.Add("drain-mutation-mismatch");
        try { engine.Execute(new("spellexecution.test.2","combat.1",2,"character.1","npc.1","spell.1304",1,1,0,RuntimeSaveOutcome.Failed,1)); failures.Add("unresolved-policy-not-rejected"); }
        catch(InvalidOperationException e) when(e.Message=="DIRECT_SPELL_POLICY_UNRESOLVED") { }
        return new(failures.Count==0,failures);
    }
}
