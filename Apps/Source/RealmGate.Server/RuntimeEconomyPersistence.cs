using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeEconomySaveReason { Autosave, Logout, Disconnect, Shutdown, Administrative, RecoveryCheckpoint }
public enum RuntimeEconomyRecoveryMode { Strict, LastKnownGood, JournalReplay }
public enum RuntimeEconomyPersistenceStatus { Prepared, Committed, Recovered, Rejected }

public sealed record RuntimeEconomyComponentSnapshot(string ComponentId,int SchemaVersion,long Version,string Payload,string PayloadSha256);
public sealed record RuntimeEconomyJournalEntry(string TransactionId,string ReplayKey,DateTimeOffset CreatedUtc,RuntimeEconomySaveReason Reason,long Sequence,IReadOnlyList<RuntimeEconomyComponentSnapshot> Components,string EntrySha256);
public sealed record RuntimeEconomyCheckpoint(string CheckpointId,int SchemaVersion,long Sequence,DateTimeOffset CreatedUtc,RuntimeEconomySaveReason Reason,IReadOnlyList<RuntimeEconomyComponentSnapshot> Components,IReadOnlySet<string> AppliedReplayKeys,string CheckpointSha256);
public sealed record RuntimeEconomyPersistenceEnvelope(int SchemaVersion,RuntimeEconomyCheckpoint Current,RuntimeEconomyCheckpoint? Previous,IReadOnlyList<RuntimeEconomyJournalEntry> Journal);
public sealed record RuntimeEconomyPersistenceResult(string CheckpointId,long Sequence,RuntimeEconomyPersistenceStatus Status,RuntimeEconomySaveReason Reason,string ReplayKey,string Detail);

public interface IRuntimeEconomyPersistenceParticipant
{
    string ComponentId { get; }
    int SchemaVersion { get; }
    RuntimeEconomyComponentSnapshot Capture();
    void Validate(RuntimeEconomyComponentSnapshot snapshot);
    void Restore(RuntimeEconomyComponentSnapshot snapshot);
}

public interface IRuntimeEconomyPersistenceStore
{
    void WriteAtomic(string serializedEnvelope);
    string? ReadCurrent();
    string? ReadBackup();
}

public sealed class RuntimeEconomyPersistenceCoordinator
{
    public const int CurrentEnvelopeSchemaVersion=1;
    private readonly object _gate=new();
    private readonly IReadOnlyDictionary<string,IRuntimeEconomyPersistenceParticipant> _participants;
    private readonly IRuntimeEconomyPersistenceStore _store;
    private RuntimeEconomyPersistenceEnvelope? _envelope;
    private HashSet<string> _appliedReplayKeys=new(StringComparer.OrdinalIgnoreCase);

    public RuntimeEconomyPersistenceCoordinator(IEnumerable<IRuntimeEconomyPersistenceParticipant> participants,IRuntimeEconomyPersistenceStore store)
    {
        _participants=participants.ToDictionary(x=>x.ComponentId,StringComparer.OrdinalIgnoreCase);
        if(_participants.Count==0||_participants.Values.Any(x=>string.IsNullOrWhiteSpace(x.ComponentId)||x.SchemaVersion<=0)) throw new InvalidOperationException("economy-persistence-participant-invalid");
        _store=store??throw new ArgumentNullException(nameof(store));
    }

    public RuntimeEconomyPersistenceResult Save(RuntimeEconomySaveReason reason,string replayKey,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(replayKey)) throw new InvalidOperationException("economy-persistence-replay-key-required");
        lock(_gate)
        {
            if(_appliedReplayKeys.Contains(replayKey)) throw new InvalidOperationException("economy-persistence-replay-detected");
            var participantBefore=_participants.Values.ToDictionary(x=>x.ComponentId,x=>x.Capture(),StringComparer.OrdinalIgnoreCase);
            var envelopeBefore=_envelope;
            var replayBefore=new HashSet<string>(_appliedReplayKeys,StringComparer.OrdinalIgnoreCase);
            try
            {
                var components=CaptureAndValidate();
                var sequence=checked((_envelope?.Current.Sequence??0)+1);
                var transactionId=$"economy-save:{sequence}:{replayKey}";
                var journal=BuildJournalEntry(transactionId,replayKey,now,reason,sequence,components);
                _appliedReplayKeys.Add(replayKey);
                var checkpoint=BuildCheckpoint($"economy-checkpoint:{sequence}",sequence,now,reason,components,_appliedReplayKeys);
                var journalEntries=(_envelope?.Journal??Array.Empty<RuntimeEconomyJournalEntry>()).Concat(new[]{journal}).TakeLast(128).ToArray();
                var next=new RuntimeEconomyPersistenceEnvelope(CurrentEnvelopeSchemaVersion,checkpoint,_envelope?.Current,journalEntries);
                _store.WriteAtomic(Serialize(next));
                _envelope=next;
                return new(checkpoint.CheckpointId,sequence,RuntimeEconomyPersistenceStatus.Committed,reason,replayKey,"economy-state-committed");
            }
            catch
            {
                foreach(var p in _participants.Values) p.Restore(participantBefore[p.ComponentId]);
                _envelope=envelopeBefore;
                _appliedReplayKeys=replayBefore;
                throw;
            }
        }
    }

    public RuntimeEconomyPersistenceResult Autosave(string replayKey,DateTimeOffset now)=>Save(RuntimeEconomySaveReason.Autosave,replayKey,now);
    public RuntimeEconomyPersistenceResult OnLogout(string characterId,string replayKey,DateTimeOffset now)=>Save(RuntimeEconomySaveReason.Logout,ScopedReplay(characterId,replayKey),now);
    public RuntimeEconomyPersistenceResult OnDisconnect(string sessionId,string replayKey,DateTimeOffset now)=>Save(RuntimeEconomySaveReason.Disconnect,ScopedReplay(sessionId,replayKey),now);

    public RuntimeEconomyPersistenceResult Recover(RuntimeEconomyRecoveryMode mode,DateTimeOffset now)
    {
        lock(_gate)
        {
            var candidates=new[]{_store.ReadCurrent(),mode==RuntimeEconomyRecoveryMode.Strict?null:_store.ReadBackup()}.Where(x=>!string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray();
            Exception? last=null;
            foreach(var serialized in candidates)
            {
                try
                {
                    var envelope=Deserialize(serialized);
                    var checkpoint=mode==RuntimeEconomyRecoveryMode.JournalReplay?ReplayJournal(envelope):envelope.Current;
                    ValidateCheckpoint(checkpoint);
                    RestoreAll(checkpoint);
                    _appliedReplayKeys=new(checkpoint.AppliedReplayKeys,StringComparer.OrdinalIgnoreCase);
                    _envelope=envelope with{Current=checkpoint};
                    return new(checkpoint.CheckpointId,checkpoint.Sequence,RuntimeEconomyPersistenceStatus.Recovered,RuntimeEconomySaveReason.RecoveryCheckpoint,$"recovery:{checkpoint.Sequence}","economy-state-recovered");
                }
                catch(Exception ex){last=ex;}
            }
            throw new InvalidOperationException("economy-persistence-recovery-failed",last);
        }
    }

    private IReadOnlyList<RuntimeEconomyComponentSnapshot> CaptureAndValidate()
    {
        var result=new List<RuntimeEconomyComponentSnapshot>();
        foreach(var participant in _participants.Values.OrderBy(x=>x.ComponentId,StringComparer.OrdinalIgnoreCase))
        {
            var snapshot=participant.Capture();
            ValidateComponentIdentity(participant,snapshot);
            participant.Validate(snapshot);
            result.Add(snapshot with{PayloadSha256=Hash(snapshot.Payload)});
        }
        return result;
    }

    private void RestoreAll(RuntimeEconomyCheckpoint checkpoint)
    {
        var before=_participants.Values.ToDictionary(x=>x.ComponentId,x=>x.Capture(),StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach(var snapshot in checkpoint.Components)
            {
                if(!_participants.TryGetValue(snapshot.ComponentId,out var participant)) throw new InvalidOperationException("economy-persistence-component-missing");
                ValidateComponentIdentity(participant,snapshot); participant.Validate(snapshot); participant.Restore(snapshot);
            }
        }
        catch{foreach(var p in _participants.Values)p.Restore(before[p.ComponentId]);throw;}
    }

    private RuntimeEconomyCheckpoint ReplayJournal(RuntimeEconomyPersistenceEnvelope envelope)
    {
        var valid=envelope.Journal.OrderBy(x=>x.Sequence).Where(ValidateJournalEntry).ToArray();
        if(valid.Length==0) return envelope.Current;
        var latest=valid[^1];
        var keys=new HashSet<string>(envelope.Current.AppliedReplayKeys,StringComparer.OrdinalIgnoreCase){latest.ReplayKey};
        return BuildCheckpoint($"economy-replay:{latest.Sequence}",latest.Sequence,latest.CreatedUtc,latest.Reason,latest.Components,keys);
    }

    private static RuntimeEconomyJournalEntry BuildJournalEntry(string transactionId,string replayKey,DateTimeOffset now,RuntimeEconomySaveReason reason,long sequence,IReadOnlyList<RuntimeEconomyComponentSnapshot> components)
    {
        var unsigned=new RuntimeEconomyJournalEntry(transactionId,replayKey,now,reason,sequence,components,string.Empty);
        return unsigned with{EntrySha256=Hash(JsonSerializer.Serialize(unsigned))};
    }

    private static RuntimeEconomyCheckpoint BuildCheckpoint(string id,long sequence,DateTimeOffset now,RuntimeEconomySaveReason reason,IReadOnlyList<RuntimeEconomyComponentSnapshot> components,IReadOnlySet<string> replayKeys)
    {
        var unsigned=new RuntimeEconomyCheckpoint(id,CurrentEnvelopeSchemaVersion,sequence,now,reason,components,new HashSet<string>(replayKeys,StringComparer.OrdinalIgnoreCase),string.Empty);
        return unsigned with{CheckpointSha256=Hash(JsonSerializer.Serialize(unsigned))};
    }

    private static void ValidateCheckpoint(RuntimeEconomyCheckpoint checkpoint)
    {
        if(checkpoint.SchemaVersion!=CurrentEnvelopeSchemaVersion||checkpoint.Sequence<0) throw new InvalidOperationException("economy-persistence-checkpoint-schema-invalid");
        var unsigned=checkpoint with{CheckpointSha256=string.Empty};
        if(!FixedEquals(checkpoint.CheckpointSha256,Hash(JsonSerializer.Serialize(unsigned)))) throw new InvalidOperationException("economy-persistence-checkpoint-corrupt");
        if(checkpoint.Components.Select(x=>x.ComponentId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=checkpoint.Components.Count) throw new InvalidOperationException("economy-persistence-component-duplicate");
        foreach(var c in checkpoint.Components) if(!FixedEquals(c.PayloadSha256,Hash(c.Payload))) throw new InvalidOperationException("economy-persistence-component-corrupt");
    }

    private static bool ValidateJournalEntry(RuntimeEconomyJournalEntry entry)
    {
        var unsigned=entry with{EntrySha256=string.Empty};
        return entry.Sequence>=0&&FixedEquals(entry.EntrySha256,Hash(JsonSerializer.Serialize(unsigned)))&&entry.Components.All(c=>FixedEquals(c.PayloadSha256,Hash(c.Payload)));
    }

    private static void ValidateComponentIdentity(IRuntimeEconomyPersistenceParticipant participant,RuntimeEconomyComponentSnapshot snapshot)
    {
        if(!participant.ComponentId.Equals(snapshot.ComponentId,StringComparison.OrdinalIgnoreCase)||participant.SchemaVersion!=snapshot.SchemaVersion||snapshot.Version<0) throw new InvalidOperationException("economy-persistence-component-schema-mismatch");
    }

    public static string Serialize(RuntimeEconomyPersistenceEnvelope envelope)=>JsonSerializer.Serialize(envelope);
    public static RuntimeEconomyPersistenceEnvelope Deserialize(string json)
    {
        var envelope=JsonSerializer.Deserialize<RuntimeEconomyPersistenceEnvelope>(json)??throw new InvalidOperationException("economy-persistence-envelope-invalid");
        if(envelope.SchemaVersion!=CurrentEnvelopeSchemaVersion) throw new InvalidOperationException("economy-persistence-envelope-schema-unsupported");
        ValidateCheckpoint(envelope.Current); if(envelope.Previous is not null) ValidateCheckpoint(envelope.Previous);
        return envelope;
    }

    private static string ScopedReplay(string scope,string replayKey)=>string.IsNullOrWhiteSpace(scope)||string.IsNullOrWhiteSpace(replayKey)?throw new InvalidOperationException("economy-persistence-scope-required"):$"{scope}:{replayKey}";
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool FixedEquals(string left,string right)=>CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left),Encoding.ASCII.GetBytes(right));
}
