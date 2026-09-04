namespace RealmGate.Server;
public sealed record RuntimeResolvedSpellExecutionSmokeReport(bool Passed,IReadOnlyList<string> Failures);
public static class RuntimeResolvedSpellExecutionSmoke
{
 public static RuntimeResolvedSpellExecutionSmokeReport Run()
 {
  var failures=new List<string>();
  var rp=new RuntimeResistancePolicy("resistancepolicy.test.168","spell.168","resistance.magic",0,1,1,0,1000,40,80,120,5000,10000,true,RuntimeResistanceRounding.TowardZero,RuntimeResolutionConfidence.StronglyCorrelated,new[]{"evidence.0168.resistance"},"reject");
  var sp=new RuntimeSavingThrowPolicy("savepolicy.test.168","spell.168","save.magic",0,1,1,1,1,1,1,1,100,50,25,RuntimeSavingThrowEffect.PreventSecondaryEffect,RuntimeSavingThrowEffect.ReduceDuration,10000,5000,10000,5000,RuntimeResolutionConfidence.StronglyCorrelated,new[]{"evidence.0168.save"},"reject");
  var formula=new RuntimeDirectSpellFormulaPolicy("spellformula.test.168","spell.168",RuntimeDirectSpellFormulaKind.Fixed,RuntimeDamageDisposition.Damage,RuntimeDirectSpellResourceKind.HitPoints,0,0,20,0,1,0,1,0,0,0,0,0,1000,RuntimeDirectSpellRoundingMode.TowardZero,false,false,false,RuntimeDirectSpellResourceKind.HitPoints,RuntimeResolutionConfidence.StronglyCorrelated,new[]{"evidence.0168.formula"},"reject");
  var sink=new RuntimeResolvedSpellSmokeSink(new[]{new RuntimeDirectSpellTargetState("character.1",50,100,10,10,10,10),new RuntimeDirectSpellTargetState("npc.1",50,100,0,0,0,0)});
  var engine=new RuntimeResolvedSpellExecution(new RuntimeSpellResistanceSavingThrow(new RuntimeResistanceSavingThrowCatalog(new[]{rp},new[]{sp})),new RuntimeDirectSpellExecution(new RuntimeDirectSpellFormulaCatalog(new[]{formula}),sink));
  var rr=new RuntimeResistanceSavingThrowRequest("resistsave.168","combat.168",1,"character.1","npc.1","spell.168","resistance.magic","save.magic",10,10,10,0,0,0,7);
  var dr=new RuntimeDirectSpellExecutionRequest("spellexecution.168","combat.168",1,"character.1","npc.1","spell.168",10,10,0,RuntimeSaveOutcome.Failed,9);
  var trace=engine.Execute(new(rr,dr,true,10));
  if(trace.EvidenceReferences.Count!=3) failures.Add("evidence-not-merged");
  if(trace.DirectExecution.FinalMagnitude<0) failures.Add("negative-final-magnitude");
  try { engine.Execute(new(rr,dr with { SpellId="spell.169" },true,10)); failures.Add("mismatch-not-rejected"); } catch(InvalidOperationException e) when(e.Message=="RESOLVED_SPELL_REQUEST_MISMATCH") { }
  return new(failures.Count==0,failures);
 }
 private sealed class RuntimeResolvedSpellSmokeSink : IRuntimeDirectSpellStateSink
 {
  private readonly Dictionary<string,RuntimeDirectSpellTargetState> _states;
  public RuntimeResolvedSpellSmokeSink(IEnumerable<RuntimeDirectSpellTargetState> states)=>_states=states.ToDictionary(x=>x.TargetId,StringComparer.OrdinalIgnoreCase);
  public RuntimeDirectSpellTargetState Capture(string id)=>_states[id];
  public void Restore(string id,RuntimeDirectSpellTargetState state)=>_states[id]=state;
  public void Apply(RuntimeDirectSpellMutation m){var s=_states[m.EntityId];_states[m.EntityId]=m.Resource switch{RuntimeDirectSpellResourceKind.HitPoints=>s with{CurrentHitPoints=s.CurrentHitPoints+m.Delta},RuntimeDirectSpellResourceKind.Mana=>s with{CurrentMana=s.CurrentMana+m.Delta},_=>s with{CurrentEnergy=s.CurrentEnergy+m.Delta}};}
 }
}
