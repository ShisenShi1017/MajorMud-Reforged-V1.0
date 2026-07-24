using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeImportExportCompatibilitySmokeReport(bool Passed,IReadOnlyList<string> Checks)
{
 public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
 public static RuntimeImportExportCompatibilitySmokeReport Run()
 {
  var c=new List<string>();var s=new RuntimeImportExportCompatibilityService();
  var inspect=s.Execute(new("op-1",RuntimeCompatibilityOperation.Inspect,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Classic,1,1,"room=1\r\n",false,true,true));if(!inspect.Success||inspect.PayloadSha256 is null)c.Add("inspect");
  var import=s.Execute(new("op-2",RuntimeCompatibilityOperation.Import,RuntimeCompatibilityFormat.CustomData,RuntimeCompatibilityMode.Custom,1,1,"item=2",true,true,false));if(!import.Success||!import.RequiresOwnerCommit)c.Add("import");
  var migration=s.Execute(new("op-3",RuntimeCompatibilityOperation.Migrate,RuntimeCompatibilityFormat.CharacterSave,RuntimeCompatibilityMode.Custom,1,2,"save=1",true,true,true));if(!migration.Success||migration.OutputVersion!=2)c.Add("migration");
  var unauthorized=s.Execute(new("op-4",RuntimeCompatibilityOperation.Import,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Custom,1,1,"x",false,true,false));if(unauthorized.Success)c.Add("authorization");
  var classicExtension=s.Execute(new("op-5",RuntimeCompatibilityOperation.Import,RuntimeCompatibilityFormat.ReforgedPackage,RuntimeCompatibilityMode.Classic,1,1,"x",true,true,false));if(classicExtension.Success)c.Add("classic-boundary");
  var ids=s.Execute(new("op-6",RuntimeCompatibilityOperation.Import,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Classic,1,1,"x",true,false,false));if(ids.Success)c.Add("classic-ids");
  return new(c.Count==0,c);
 }
}
