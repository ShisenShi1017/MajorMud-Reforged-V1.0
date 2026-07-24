using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeTransportServiceSmokeReport(bool Passed,IReadOnlyList<string> Checks,IReadOnlyList<string> Failures)
{
 public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
 public static RuntimeTransportServiceSmokeReport Run(){var checks=new List<string>();var failures=new List<string>();
 var service=new RuntimeTransportService(new[]{new RuntimeTransportDefinition("ferry",new[]{new RuntimeTransportStop("west","room-west",TimeSpan.Zero),new RuntimeTransportStop("east","room-east",TimeSpan.Zero)},10,2,false,false,TimeSpan.FromMinutes(2))});
 var actor=new RuntimeTransportActor("hero","room-west",true,false,10,new HashSet<string>());var t=DateTimeOffset.Parse("2026-01-01T00:00:00Z");
 var board=service.Board("ferry","east",actor,t);Check(board.Accepted,"board");Check(board.Requests.Any(x=>x.Kind==RuntimeTransportRequestKind.ChargeFare&&x.Amount==10),"fare-routing");
 Check(service.Depart("ferry",t).Accepted,"depart");Check(!service.Disconnect("ferry","hero").Accepted,"disconnect-in-transit-preserved");
 var arrival=service.Advance("ferry",t.AddMinutes(2));Check(arrival.Accepted,"arrival");Check(arrival.Requests.Any(x=>x.Kind==RuntimeTransportRequestKind.MoveActor&&x.RelatedId=="room-east"),"movement-routing");
 Check(!service.Board("ferry","east",actor with{InCombat=true},t).Accepted,"combat-restriction");Check(service.ValidateState().Count==0,"state-invariants");
 return new(failures.Count==0,checks,failures);void Check(bool c,string n){checks.Add(n);if(!c)failures.Add(n);}}
}
