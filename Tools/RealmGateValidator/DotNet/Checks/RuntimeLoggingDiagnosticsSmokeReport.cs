using System.Text.Json;
namespace RealmGate.Server;
public sealed record RuntimeLoggingDiagnosticsSmokeReport(bool Passed,IReadOnlyList<string> Checks)
{
 public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
 public static RuntimeLoggingDiagnosticsSmokeReport Run(){var c=new List<string>();var s=new RuntimeLoggingDiagnosticsService(3);var ctx=new RuntimeLogContext("corr-1","world-1",42,"char-1",CommandId:"look");s.Write(RuntimeLogSeverity.Information,"command","executed",ctx);s.Write(RuntimeLogSeverity.Warning,"security","denied",ctx,administrative:true,securitySensitive:true);if(s.Query(new RuntimeLogQuery(CharacterId:"char-1"),false).Count!=1)c.Add("player-filter");if(s.Query(new RuntimeLogQuery(CharacterId:"char-1",IncludeAdministrative:true,IncludeSecuritySensitive:true),true).Count!=2)c.Add("admin-visibility");s.Write(RuntimeLogSeverity.Error,"save","failed",ctx);s.Write(RuntimeLogSeverity.Debug,"trace","state",ctx);if(s.Query(new RuntimeLogQuery(),true).Count!=2)c.Add("bounded-retention");c.AddRange(s.ValidateState());return new(c.Count==0,c);}
}
