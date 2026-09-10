using System.Text.Json;
namespace RealmGate.Server;
public enum RuntimeSaveScope { Character, World, Service }
public enum RuntimeSavePhase { Prepare, Commit, Rollback }
public sealed record RuntimeSaveParticipant(string Id,RuntimeSaveScope Scope,int Order,bool Required,bool SupportsRollback);
public sealed record RuntimeSaveRequest(string CorrelationId,string Reason,IReadOnlyList<string> DirtyParticipantIds,DateTimeOffset RequestedUtc);
public sealed record RuntimeSaveOperation(string ParticipantId,RuntimeSavePhase Phase,string CorrelationId,string Reason);
public sealed record RuntimeSaveParticipantResult(string ParticipantId,bool Prepared,bool Committed,bool RolledBack,string? Error=null);
public sealed record RuntimeSaveResult(bool Success,string CorrelationId,IReadOnlyList<RuntimeSaveOperation> Operations,IReadOnlyList<string> Errors);
public sealed record RuntimeSaveSnapshot(int SchemaVersion,long Generation,DateTimeOffset SavedUtc,IReadOnlyDictionary<string,long> ParticipantGenerations);
/// <summary>
/// Generic RealmGate cross-system save coordination infrastructure.
///
/// Its configured participant order, prepare/commit phases, reverse rollback, generation counters,
/// and JSON snapshot replacement are RealmGate safety policy. They are not evidence of Classic
/// MajorMUD player/world save ordering, native "Buffer data" ownership, Worldgroup transaction
/// grouping, or native rollback semantics. Classic parity is gated by RuntimeClassicSaveCoordinationAuthority.
/// </summary>
public sealed class RuntimeSaveCoordinationService
{
 private readonly Dictionary<string,RuntimeSaveParticipant> _participants; private readonly Dictionary<string,long> _generations=new(StringComparer.OrdinalIgnoreCase); private long _generation;
 public RuntimeSaveCoordinationService(IEnumerable<RuntimeSaveParticipant> participants){_participants=participants.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);if(_participants.Count==0)throw new ArgumentException("participants-required");foreach(var p in _participants.Values){if(string.IsNullOrWhiteSpace(p.Id))throw new ArgumentException("participant-id-required");if(p.Order<0)throw new ArgumentOutOfRangeException(nameof(p.Order));_generations[p.Id]=0;}}
 public IReadOnlyList<RuntimeSaveParticipant> Participants=>_participants.Values.OrderBy(x=>x.Order).ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray();
 public RuntimeSaveResult Coordinate(RuntimeSaveRequest request,Func<RuntimeSaveOperation,bool> execute)
 {
  if(string.IsNullOrWhiteSpace(request.CorrelationId)||string.IsNullOrWhiteSpace(request.Reason))return new(false,request.CorrelationId,[],["invalid-request"]);
  var ids=request.DirtyParticipantIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); if(ids.Any(id=>!_participants.ContainsKey(id)))return new(false,request.CorrelationId,[],["unknown-participant"]);
  var selected=ids.Select(id=>_participants[id]).OrderBy(x=>x.Order).ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray(); var ops=new List<RuntimeSaveOperation>(); var errors=new List<string>(); var prepared=new List<RuntimeSaveParticipant>(); var committed=new List<RuntimeSaveParticipant>();
  foreach(var p in selected){var op=new RuntimeSaveOperation(p.Id,RuntimeSavePhase.Prepare,request.CorrelationId,request.Reason);ops.Add(op);if(!execute(op)){errors.Add($"prepare-failed:{p.Id}");if(p.Required){Rollback(prepared,ops,request,execute,errors);return new(false,request.CorrelationId,ops,errors);}}else prepared.Add(p);}
  foreach(var p in prepared){var op=new RuntimeSaveOperation(p.Id,RuntimeSavePhase.Commit,request.CorrelationId,request.Reason);ops.Add(op);if(!execute(op)){errors.Add($"commit-failed:{p.Id}");Rollback(committed,ops,request,execute,errors);return new(false,request.CorrelationId,ops,errors);}committed.Add(p);_generations[p.Id]++;}
  _generation++; return new(true,request.CorrelationId,ops,errors);
 }
 private static void Rollback(IEnumerable<RuntimeSaveParticipant> participants,List<RuntimeSaveOperation> ops,RuntimeSaveRequest request,Func<RuntimeSaveOperation,bool> execute,List<string> errors){foreach(var p in participants.Reverse()){if(!p.SupportsRollback){errors.Add($"rollback-unsupported:{p.Id}");continue;}var op=new RuntimeSaveOperation(p.Id,RuntimeSavePhase.Rollback,request.CorrelationId,request.Reason);ops.Add(op);if(!execute(op))errors.Add($"rollback-failed:{p.Id}");}}
 public RuntimeSaveSnapshot Snapshot(DateTimeOffset? nowUtc=null)=>new(1,_generation,nowUtc??DateTimeOffset.UtcNow,new Dictionary<string,long>(_generations,StringComparer.OrdinalIgnoreCase));
 public void PersistSnapshot(string path,DateTimeOffset? nowUtc=null){var full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);var temp=full+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(Snapshot(nowUtc),new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,full,true);}
 public bool RestoreSnapshot(string path){try{var s=JsonSerializer.Deserialize<RuntimeSaveSnapshot>(File.ReadAllText(path));if(s is null||s.SchemaVersion!=1||s.Generation<0)return false;foreach(var kv in s.ParticipantGenerations){if(!_participants.ContainsKey(kv.Key)||kv.Value<0)return false;}_generation=s.Generation;foreach(var id in _participants.Keys)_generations[id]=s.ParticipantGenerations.TryGetValue(id,out var g)?g:0;return true;}catch{return false;}}
 public IReadOnlyList<string> ValidateState(){var e=new List<string>();if(_participants.Values.Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=_participants.Count)e.Add("duplicate-participant");if(_generations.Any(x=>x.Value<0))e.Add("negative-generation");return e;}
}
