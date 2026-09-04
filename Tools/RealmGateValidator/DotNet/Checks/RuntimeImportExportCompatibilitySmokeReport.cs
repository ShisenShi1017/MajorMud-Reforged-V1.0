using System.Text.Json;
namespace RealmGate.Server;
public sealed record RuntimeImportExportCompatibilitySmokeReport(bool Passed,IReadOnlyList<string> Checks,IReadOnlyList<string> Failures)
{
 public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
 public static RuntimeImportExportCompatibilitySmokeReport Run()
 {
  var c=new List<string>();var failures=new List<string>();var s=new RuntimeImportExportCompatibilityService();
  void Check(bool ok,string id){c.Add(id);if(!ok)failures.Add(id);}

  var inspect=s.Execute(new("op-1",RuntimeCompatibilityOperation.Inspect,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Classic,111,111,"room=1\r\n",false,true,true));
  Check(inspect.Success&&inspect.PayloadSha256 is not null,"classic-inspect-exact-version");

  var mismatch=s.Execute(new("op-2",RuntimeCompatibilityOperation.Validate,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Classic,110,111,"room=1",false,true,true));
  Check(!mismatch.Success&&mismatch.Code=="classic-native-version-mismatch-hard-failure","classic-version-mismatch-hard-failure");

  var nativeExact=s.Execute(new("op-3",RuntimeCompatibilityOperation.Validate,RuntimeCompatibilityFormat.Wcc,RuntimeCompatibilityMode.Classic,111,111,"decoded-native-fixture",false,true,true,"WCCMP002.DAT","WCCMP002.DAT"));
  Check(nativeExact.Success&&nativeExact.Issues.Any(x=>x.Code=="classic-native-file-exact-match"),"classic-native-file-exact-match");

  var nativeWrong=s.Execute(new("op-4",RuntimeCompatibilityOperation.Validate,RuntimeCompatibilityFormat.Wcc,RuntimeCompatibilityMode.Classic,111,111,"decoded-native-fixture",false,true,true,"WCCMP003.DAT","WCCMP002.DAT"));
  Check(!nativeWrong.Success&&nativeWrong.Code=="classic-native-file-mismatch-hard-failure","classic-native-file-mismatch-hard-failure");

  var classicImport=s.Execute(new("op-5",RuntimeCompatibilityOperation.Import,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Classic,111,111,"x",true,true,false));
  Check(!classicImport.Success&&classicImport.Code=="classic-native-file-replacement-is-external","classic-import-is-external-file-replacement");

  var classicMigrate=s.Execute(new("op-6",RuntimeCompatibilityOperation.Migrate,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Classic,110,111,"x",true,true,false));
  Check(!classicMigrate.Success&&classicMigrate.Code=="classic-native-file-replacement-is-external","classic-migration-unsupported");

  var import=s.Execute(new("op-7",RuntimeCompatibilityOperation.Import,RuntimeCompatibilityFormat.CustomData,RuntimeCompatibilityMode.Custom,1,1,"item=2",true,true,false));
  Check(import.Success&&import.RequiresOwnerCommit,"custom-import-extension-preserved");
  var migration=s.Execute(new("op-8",RuntimeCompatibilityOperation.Migrate,RuntimeCompatibilityFormat.CharacterSave,RuntimeCompatibilityMode.Custom,1,2,"save=1",true,true,true));
  Check(migration.Success&&migration.OutputVersion==2,"custom-migration-extension-preserved");
  var unauthorized=s.Execute(new("op-9",RuntimeCompatibilityOperation.Import,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Custom,1,1,"x",false,true,false));
  Check(!unauthorized.Success,"custom-authorization");
  var classicExtension=s.Execute(new("op-10",RuntimeCompatibilityOperation.Inspect,RuntimeCompatibilityFormat.ReforgedPackage,RuntimeCompatibilityMode.Classic,1,1,"x",false,true,true));
  Check(!classicExtension.Success,"classic-extension-boundary");
  var ids=s.Execute(new("op-11",RuntimeCompatibilityOperation.Validate,RuntimeCompatibilityFormat.WorldData,RuntimeCompatibilityMode.Classic,1,1,"x",false,false,true));
  Check(!ids.Success,"classic-ids");
  return new(failures.Count==0,c,failures);
 }
}
