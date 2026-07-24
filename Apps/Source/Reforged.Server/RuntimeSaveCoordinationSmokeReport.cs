using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeSaveCoordinationSmokeReport(bool Passed,IReadOnlyList<string> Checks)
{
 public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
 public static RuntimeSaveCoordinationSmokeReport Run(){var c=new List<string>();var s=new RuntimeSaveCoordinationService([new("character",RuntimeSaveScope.Character,10,true,true),new("world",RuntimeSaveScope.World,20,true,true),new("events",RuntimeSaveScope.Service,30,false,true)]);var seen=new List<RuntimeSaveOperation>();var r=s.Coordinate(new("save-1","autosave",["world","character"],DateTimeOffset.UnixEpoch),op=>{seen.Add(op);return true;});if(!r.Success)c.Add("successful-save");if(seen.Where(x=>x.Phase==RuntimeSavePhase.Commit).Select(x=>x.ParticipantId).SequenceEqual(["character","world"])==false)c.Add("deterministic-order");var r2=s.Coordinate(new("save-2","shutdown",["character","world"],DateTimeOffset.UnixEpoch),op=>op.Phase!=RuntimeSavePhase.Commit||op.ParticipantId!="world");if(r2.Success||!r2.Operations.Any(x=>x.Phase==RuntimeSavePhase.Rollback&&x.ParticipantId=="character"))c.Add("rollback");if(s.Coordinate(new("save-3","bad",["missing"],DateTimeOffset.UnixEpoch),_=>true).Success)c.Add("unknown-fail-closed");c.AddRange(s.ValidateState());return new(c.Count==0,c);}
}
