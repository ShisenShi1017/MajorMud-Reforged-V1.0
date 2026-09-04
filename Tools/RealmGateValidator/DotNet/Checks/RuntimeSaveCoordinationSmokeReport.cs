using System.Text.Json;
namespace RealmGate.Server;
public sealed record RuntimeSaveCoordinationSmokeReport(bool Passed,IReadOnlyList<string> Checks)
{
 public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
 public static RuntimeSaveCoordinationSmokeReport Run()
 {
  var c=new List<string>();
  var s=new RuntimeSaveCoordinationService([new("character",RuntimeSaveScope.Character,10,true,true),new("world",RuntimeSaveScope.World,20,true,true),new("events",RuntimeSaveScope.Service,30,false,true)]);
  var seen=new List<RuntimeSaveOperation>();
  var r=s.Coordinate(new("save-1","reforged-periodic-save-smoke",["world","character"],DateTimeOffset.UnixEpoch),op=>{seen.Add(op);return true;});
  if(!r.Success)c.Add("successful-save");
  if(seen.Where(x=>x.Phase==RuntimeSavePhase.Commit).Select(x=>x.ParticipantId).SequenceEqual(["character","world"])==false)c.Add("deterministic-reforged-order");
  var r2=s.Coordinate(new("save-2","reforged-shutdown-save-smoke",["character","world"],DateTimeOffset.UnixEpoch),op=>op.Phase!=RuntimeSavePhase.Commit||op.ParticipantId!="world");
  if(r2.Success||!r2.Operations.Any(x=>x.Phase==RuntimeSavePhase.Rollback&&x.ParticipantId=="character"))c.Add("reforged-rollback");
  if(s.Coordinate(new("save-3","bad",["missing"],DateTimeOffset.UnixEpoch),_=>true).Success)c.Add("unknown-fail-closed");
  if(RuntimeClassicSaveCoordinationAuthority.GenericCoordinatorOwnsClassicOrdering)c.Add("classic-order-misclassified");
  try{RuntimeClassicSaveCoordinationAuthority.RequireClassicPlayerWorldSaveOrder();c.Add("classic-order-not-gated");}
  catch(InvalidOperationException ex) when(ex.Message=="native-player-world-save-order-unresolved"){}
  c.AddRange(s.ValidateState());
  return new(c.Count==0,c);
 }
}
