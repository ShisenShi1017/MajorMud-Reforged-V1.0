namespace Reforged.Server;
public sealed record RuntimeSpellResistanceSavingThrowSmokeReport(bool Passed,IReadOnlyList<string> Failures);
public static class RuntimeSpellResistanceSavingThrowSmoke
{
 public static RuntimeSpellResistanceSavingThrowSmokeReport Run()
 {
  var failures=new List<string>();
  var rp=new RuntimeResistancePolicy("resistancepolicy.test.1","spell.1","resistance.magic",0,1,1,0,0,40,80,120,5000,10000,true,RuntimeResistanceRounding.TowardZero,RuntimeResolutionConfidence.StronglyCorrelated,new[]{"evidence.test.resistance"},"Reject unresolved production resistance semantics.");
  var sp=new RuntimeSavingThrowPolicy("savepolicy.test.1","spell.1","save.magic",0,1,1,1,1,1,1,1,100,50,25,RuntimeSavingThrowEffect.Negate,RuntimeSavingThrowEffect.ReduceMagnitude,10000,5000,10000,0,RuntimeResolutionConfidence.StronglyCorrelated,new[]{"evidence.test.save"},"Reject unresolved production saving throw semantics.");
  var engine=new RuntimeSpellResistanceSavingThrow(new RuntimeResistanceSavingThrowCatalog(new[]{rp},new[]{sp}));
  var trace=engine.Resolve(new("resistsave.test.1","combat.1",1,"character.1","npc.1","spell.1","resistance.magic","save.magic",10,10,10,0,0,0,7));
  if(trace.EvidenceReferences.Count!=2) failures.Add("evidence-merge-failed");
  try { engine.Resolve(new("resistsave.test.2","combat.1",2,"character.1","npc.1","spell.1304","resistance.magic","save.magic",1,1,1,0,0,0,8)); failures.Add("unresolved-spell-not-rejected"); }
  catch(InvalidOperationException e) when(e.Message=="RESISTANCE_POLICY_UNRESOLVED") { }
  return new(failures.Count==0,failures);
 }
}
