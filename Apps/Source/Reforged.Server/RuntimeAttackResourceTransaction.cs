namespace Reforged.Server;

public enum RuntimeAttackResourceTiming { AtStart, OnHit, AtCompletion }
public enum RuntimeAttackResourceKind { Ammunition, Charge, Durability }
public sealed record RuntimeAttackResourcePolicy(string PolicyId, RuntimeAttackResourceKind Kind, RuntimeAttackResourceTiming? Timing, int? Amount, bool BreakAtZero, IReadOnlyList<string> EvidenceReferences, RuntimeResolutionConfidence Confidence, string SafeFallback);
public sealed record RuntimeAttackResourceMutation(string ResourceId, RuntimeAttackResourcePolicy Policy);
public sealed record RuntimeAttackResourceTrace(string AttackId, bool Hit, IReadOnlyList<string> AppliedResourceIds, IReadOnlyList<string> BrokenResourceIds, IReadOnlyList<string> EvidenceReferences);
public interface IRuntimeAttackResourceSink { object Capture(string resourceId); int GetAvailable(string resourceId); int Consume(string resourceId,int amount,string attackId); void Restore(string resourceId,object snapshot); }
public sealed class RuntimeAttackResourceTransaction
{
 private readonly IRuntimeAttackResourceSink _sink;
 public RuntimeAttackResourceTransaction(IRuntimeAttackResourceSink sink)=>_sink=sink??throw new ArgumentNullException(nameof(sink));
 public RuntimeAttackResourceTrace Execute(string attackId,bool hit,IEnumerable<RuntimeAttackResourceMutation> mutations,Action effectCommit)
 {
  RequireCanonical(attackId,"attack.","ATTACK_RESOURCE_ATTACK_ID_REQUIRED"); ArgumentNullException.ThrowIfNull(mutations); ArgumentNullException.ThrowIfNull(effectCommit);
  var list=mutations.ToArray(); if(list.Select(x=>x.ResourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=list.Length) throw new InvalidOperationException("ATTACK_RESOURCE_DUPLICATE_RESOURCE");
  foreach(var m in list) Validate(m);
  var selected=list.Where(x=>x.Policy.Timing==RuntimeAttackResourceTiming.AtStart || x.Policy.Timing==RuntimeAttackResourceTiming.AtCompletion || (hit&&x.Policy.Timing==RuntimeAttackResourceTiming.OnHit)).ToArray();
  foreach(var m in selected) if(_sink.GetAvailable(m.ResourceId)<m.Policy.Amount!.Value) throw new InvalidOperationException("ATTACK_RESOURCE_INSUFFICIENT");
  var snapshots=selected.ToDictionary(x=>x.ResourceId,x=>_sink.Capture(x.ResourceId),StringComparer.OrdinalIgnoreCase); var applied=new List<string>(); var broken=new List<string>();
  try { foreach(var m in selected.Where(x=>x.Policy.Timing==RuntimeAttackResourceTiming.AtStart)) Apply(m,attackId,applied,broken); effectCommit(); foreach(var m in selected.Where(x=>x.Policy.Timing!=RuntimeAttackResourceTiming.AtStart)) Apply(m,attackId,applied,broken); }
  catch { foreach(var m in selected.Reverse()) _sink.Restore(m.ResourceId,snapshots[m.ResourceId]); throw; }
  return new RuntimeAttackResourceTrace(attackId,hit,applied,broken,selected.SelectMany(x=>x.Policy.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
 }
 private void Apply(RuntimeAttackResourceMutation m,string attackId,List<string> applied,List<string> broken){var remaining=_sink.Consume(m.ResourceId,m.Policy.Amount!.Value,attackId);if(remaining<0)throw new InvalidOperationException("ATTACK_RESOURCE_NEGATIVE_BALANCE");applied.Add(m.ResourceId);if(remaining==0&&m.Policy.BreakAtZero)broken.Add(m.ResourceId);}
 private static void Validate(RuntimeAttackResourceMutation m){if(string.IsNullOrWhiteSpace(m.ResourceId)||!(m.ResourceId.StartsWith("item.",StringComparison.OrdinalIgnoreCase)||m.ResourceId.StartsWith("resource.",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("ATTACK_RESOURCE_CANONICAL_ID_REQUIRED");if(string.IsNullOrWhiteSpace(m.Policy.PolicyId)||m.Policy.Timing is null||m.Policy.Amount is null)throw new InvalidOperationException("ATTACK_RESOURCE_POLICY_UNRESOLVED");if(m.Policy.Amount<=0)throw new InvalidOperationException("ATTACK_RESOURCE_AMOUNT_INVALID");if(m.Policy.EvidenceReferences.Count==0)throw new InvalidOperationException("ATTACK_RESOURCE_EVIDENCE_REQUIRED");if(m.Policy.Confidence==RuntimeResolutionConfidence.StronglyCorrelated&&string.IsNullOrWhiteSpace(m.Policy.SafeFallback))throw new InvalidOperationException("ATTACK_RESOURCE_FALLBACK_REQUIRED");}
 private static void RequireCanonical(string value,string prefix,string error){if(string.IsNullOrWhiteSpace(value)||!value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(error);}
}
