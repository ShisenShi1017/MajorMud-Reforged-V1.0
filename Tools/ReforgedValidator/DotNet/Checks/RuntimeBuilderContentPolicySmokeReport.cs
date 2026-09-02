using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeBuilderContentPolicySmokeReport(bool Passed,IReadOnlyList<string> Checks)
{
 public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
 public static RuntimeBuilderContentPolicySmokeReport Run()
 {
  var c=new List<string>();var s=new RuntimeBuilderContentPolicyService();var actor=new RuntimeBuilderActor("host-1",true,true,true);
  var stage=s.Evaluate(new("chg-1",actor,RuntimeBuilderEnvironment.TestRealm,RuntimeBuilderSurface.Room,RuntimeBuilderOperation.Stage,"room-1",0,true,true));if(!stage.Success||stage.State?.Revision!=1)c.Add("stage");
  var live=s.Evaluate(new("chg-1",actor,RuntimeBuilderEnvironment.TestRealm,RuntimeBuilderSurface.Room,RuntimeBuilderOperation.LiveReload,"room-1",1,true,true));if(!live.Success||live.State?.Published!=true)c.Add("live-reload");
  var rollback=s.Evaluate(new("chg-1",actor,RuntimeBuilderEnvironment.TestRealm,RuntimeBuilderSurface.Room,RuntimeBuilderOperation.Rollback,"room-1",2,true,true));if(!rollback.Success||rollback.State?.RolledBack!=true)c.Add("rollback");
  var denied=s.Evaluate(new("chg-2",new("player",true,false,false),RuntimeBuilderEnvironment.Custom,RuntimeBuilderSurface.Item,RuntimeBuilderOperation.Stage,"item-1",0,true,true));if(denied.Success)c.Add("authorization");
  var classic=s.Evaluate(new("chg-3",actor,RuntimeBuilderEnvironment.Classic,RuntimeBuilderSurface.Exit,RuntimeBuilderOperation.Stage,"exit-1",0,false,true));if(classic.Success)c.Add("classic-boundary");
  var nonTest=s.Evaluate(new("chg-4",actor,RuntimeBuilderEnvironment.Custom,RuntimeBuilderSurface.Npc,RuntimeBuilderOperation.LiveReload,"npc-1",0,true,true));if(nonTest.Success)c.Add("test-realm-boundary");
  c.AddRange(s.ValidateState());return new(c.Count==0,c);
 }
}
