using System.Text.Json;
namespace Reforged.Server;
public enum RuntimeLogSeverity { Trace, Debug, Information, Warning, Error, Critical }
public sealed record RuntimeLogContext(string CorrelationId,string WorldId,long WorldTick,string? CharacterId=null,string? NpcInstanceId=null,string? CommandId=null);
public sealed record RuntimeLogEvent(string EventId,DateTimeOffset TimestampUtc,RuntimeLogSeverity Severity,string Category,string Message,RuntimeLogContext Context,IReadOnlyDictionary<string,string> Fields,bool Administrative,bool SecuritySensitive);
public sealed record RuntimeLogQuery(string? Category=null,string? CorrelationId=null,string? CharacterId=null,RuntimeLogSeverity MinimumSeverity=RuntimeLogSeverity.Trace,bool IncludeAdministrative=false,bool IncludeSecuritySensitive=false);
public sealed class RuntimeLoggingDiagnosticsService
{
 private readonly List<RuntimeLogEvent> _events=[]; private readonly int _capacity; private long _sequence;
 public RuntimeLoggingDiagnosticsService(int capacity=10000){if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));_capacity=capacity;}
 public RuntimeLogEvent Write(RuntimeLogSeverity severity,string category,string message,RuntimeLogContext context,IReadOnlyDictionary<string,string>? fields=null,bool administrative=false,bool securitySensitive=false,DateTimeOffset? nowUtc=null)
 {
  Validate(category,message,context); var id=$"log-{++_sequence:D12}"; var e=new RuntimeLogEvent(id,nowUtc??DateTimeOffset.UtcNow,severity,category.Trim(),message,context,fields??new Dictionary<string,string>(),administrative,securitySensitive); _events.Add(e); if(_events.Count>_capacity)_events.RemoveRange(0,_events.Count-_capacity); return e;
 }
 public IReadOnlyList<RuntimeLogEvent> Query(RuntimeLogQuery q,bool requesterIsAdministrator)
 {
  return _events.Where(e=>e.Severity>=q.MinimumSeverity && (q.Category is null||string.Equals(e.Category,q.Category,StringComparison.OrdinalIgnoreCase)) && (q.CorrelationId is null||e.Context.CorrelationId==q.CorrelationId) && (q.CharacterId is null||e.Context.CharacterId==q.CharacterId) && (!e.Administrative||requesterIsAdministrator&&q.IncludeAdministrative) && (!e.SecuritySensitive||requesterIsAdministrator&&q.IncludeSecuritySensitive)).ToArray();
 }
 public string ExportJsonLines(RuntimeLogQuery q,bool requesterIsAdministrator)=>string.Join("\n",Query(q,requesterIsAdministrator).Select(e=>JsonSerializer.Serialize(e)));
 public IReadOnlyList<string> ValidateState(){var errors=new List<string>();if(_events.Count>_capacity)errors.Add("capacity-exceeded");if(_events.Select(x=>x.EventId).Distinct().Count()!=_events.Count)errors.Add("duplicate-event-id");return errors;}
 private static void Validate(string category,string message,RuntimeLogContext c){if(string.IsNullOrWhiteSpace(category))throw new ArgumentException("category-required");if(string.IsNullOrWhiteSpace(message))throw new ArgumentException("message-required");if(string.IsNullOrWhiteSpace(c.CorrelationId))throw new ArgumentException("correlation-id-required");if(string.IsNullOrWhiteSpace(c.WorldId))throw new ArgumentException("world-id-required");if(c.WorldTick<0)throw new ArgumentOutOfRangeException(nameof(c.WorldTick));}
}
