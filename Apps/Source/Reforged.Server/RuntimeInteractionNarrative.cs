using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNarrativeDefinition(string Id,string Name,string Kind,JsonElement Raw);
public sealed class RuntimeNarrativeCatalog
{
    private readonly Dictionary<string,RuntimeNarrativeDefinition> _records;
    private RuntimeNarrativeCatalog(IEnumerable<RuntimeNarrativeDefinition> records) => _records=records.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);
    public int Count=>_records.Count;
    public bool TryGet(string id,out RuntimeNarrativeDefinition value)=>_records.TryGetValue(id,out value!);
    public static RuntimeNarrativeCatalog Load(string projectRoot,string worldId,string family)
    {
        var configured=ServerDataRootResolver.ResolveDefault(projectRoot); if(!string.IsNullOrWhiteSpace(worldId)&&!string.Equals(configured.WorldId,worldId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Configured world '{configured.WorldId}' does not match requested world '{worldId}'."); var path=Path.Combine(configured.DataRoot,$"{family}.json");
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        var records=doc.RootElement.GetProperty("records").EnumerateArray().Select(x=>new RuntimeNarrativeDefinition(
            x.GetProperty("id").GetString()!, x.TryGetProperty("name",out var n)?n.GetString()??x.GetProperty("id").GetString()!:x.GetProperty("id").GetString()!, family, x.Clone()));
        return new RuntimeNarrativeCatalog(records);
    }
}

public enum RuntimeQuestStatus { Available, Active, Completed, Failed, Abandoned }
public sealed record RuntimeQuestObjectiveProgress(string ObjectiveId,long Current,long Required,bool Complete,IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeQuestState(string QuestId,RuntimeQuestStatus Status,DateTimeOffset? AcceptedAtUtc,DateTimeOffset? CompletedAtUtc,IReadOnlyList<RuntimeQuestObjectiveProgress> Objectives,IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeQuestJournalSnapshot(string ActorId,IReadOnlyList<RuntimeQuestState> Quests);

public sealed class RuntimeQuestJournal
{
    private readonly object _gate=new(); private readonly Dictionary<string,RuntimeQuestState> _quests=new(StringComparer.OrdinalIgnoreCase);
    public RuntimeQuestJournal(string actorId)=>ActorId=string.IsNullOrWhiteSpace(actorId)?throw new ArgumentException("actor-id-required"):actorId;
    public string ActorId{get;}
    public bool Accept(string questId,IReadOnlyList<RuntimeQuestObjectiveProgress> objectives,DateTimeOffset now){lock(_gate){if(_quests.TryGetValue(questId,out var q)&&q.Status is RuntimeQuestStatus.Active or RuntimeQuestStatus.Completed)return false;_quests[questId]=new(questId,RuntimeQuestStatus.Active,now,null,objectives,new Dictionary<string,string>());return true;}}
    public bool Abandon(string questId){lock(_gate){if(!_quests.TryGetValue(questId,out var q)||q.Status!=RuntimeQuestStatus.Active)return false;_quests[questId]=q with{Status=RuntimeQuestStatus.Abandoned};return true;}}
    public bool ApplyProgress(string questId,string objectiveId,long delta){lock(_gate){if(delta<0||!_quests.TryGetValue(questId,out var q)||q.Status!=RuntimeQuestStatus.Active)return false;var list=q.Objectives.ToList();var i=list.FindIndex(x=>x.ObjectiveId.Equals(objectiveId,StringComparison.OrdinalIgnoreCase));if(i<0)return false;var o=list[i];var current=Math.Min(o.Required,checked(o.Current+delta));list[i]=o with{Current=current,Complete=current>=o.Required};_quests[questId]=q with{Objectives=list};return true;}}
    public bool Complete(string questId,DateTimeOffset now){lock(_gate){if(!_quests.TryGetValue(questId,out var q)||q.Status!=RuntimeQuestStatus.Active||q.Objectives.Any(x=>!x.Complete))return false;_quests[questId]=q with{Status=RuntimeQuestStatus.Completed,CompletedAtUtc=now};return true;}}
    public RuntimeQuestJournalSnapshot Capture(){lock(_gate)return new(ActorId,_quests.Values.ToArray());}
    public void Restore(RuntimeQuestJournalSnapshot snapshot){if(!ActorId.Equals(snapshot.ActorId,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("actor-mismatch");lock(_gate){_quests.Clear();foreach(var q in snapshot.Quests)_quests.Add(q.QuestId,q);}}
    public IReadOnlyList<string> Validate(RuntimeNarrativeCatalog catalog){lock(_gate){var e=new List<string>();if(_quests.Keys.Any(x=>!catalog.TryGet(x,out _)))e.Add("unknown-quest");if(_quests.Values.SelectMany(x=>x.Objectives).Any(x=>x.Current<0||x.Required<0||x.Current>x.Required))e.Add("invalid-objective-progress");return e;}}
}

public sealed record RuntimeDialogueRequest(string RequestId,string ActorId,string SpeakerId,string? NodeId,IReadOnlyDictionary<string,string>? Inputs=null);
public sealed record RuntimeDialogueChoice(string ChoiceId,string TextKey,string? NextNodeId,IReadOnlyDictionary<string,string>? Metadata=null);
public sealed record RuntimeDialoguePlan(bool Allowed,string? FailureCode,string TextKey,IReadOnlyList<RuntimeDialogueChoice> Choices,IReadOnlyList<RuntimeNarrativeEvent> Events);
public sealed record RuntimeNarrativeEvent(string EventId,string EventType,string ActorId,string? SubjectId,string? DefinitionId,long Magnitude,DateTimeOffset OccurredAtUtc,IReadOnlyDictionary<string,string>? Metadata=null);
public interface IRuntimeDialoguePolicy { RuntimeDialoguePlan Resolve(RuntimeDialogueRequest request,RuntimeQuestJournalSnapshot journal,DateTimeOffset nowUtc); }
public interface IRuntimeQuestPolicy
{
    IReadOnlyList<RuntimeQuestObjectiveProgress> ResolveObjectives(RuntimeNarrativeDefinition quest,string actorId,DateTimeOffset nowUtc);
    IReadOnlyList<RuntimeNarrativeEvent> ResolveRewards(RuntimeNarrativeDefinition quest,RuntimeQuestJournalSnapshot journal,DateTimeOffset nowUtc);
}
public interface IRuntimeNarrativeEventSink { void Apply(RuntimeNarrativeEvent value); void Revert(RuntimeNarrativeEvent value); }
public sealed record RuntimeNarrativeResult(bool Succeeded,string? FailureCode,string? TextKey,IReadOnlyList<RuntimeDialogueChoice> Choices,IReadOnlyList<RuntimeNarrativeEvent> Events);

public sealed class RuntimeInteractionNarrativeEngine
{
    private readonly RuntimeNarrativeCatalog _quests; private readonly IRuntimeQuestPolicy _questPolicy; private readonly IRuntimeDialoguePolicy _dialoguePolicy; private readonly IRuntimeNarrativeEventSink _sink;
    public RuntimeInteractionNarrativeEngine(RuntimeNarrativeCatalog quests,IRuntimeQuestPolicy questPolicy,IRuntimeDialoguePolicy dialoguePolicy,IRuntimeNarrativeEventSink sink){_quests=quests;_questPolicy=questPolicy;_dialoguePolicy=dialoguePolicy;_sink=sink;}
    public RuntimeNarrativeResult Talk(RuntimeQuestJournal journal,RuntimeDialogueRequest request,DateTimeOffset now)
    { var plan=_dialoguePolicy.Resolve(request,journal.Capture(),now); if(!plan.Allowed)return new(false,plan.FailureCode,null,Array.Empty<RuntimeDialogueChoice>(),Array.Empty<RuntimeNarrativeEvent>());var applied=new List<RuntimeNarrativeEvent>();try{foreach(var e in plan.Events){_sink.Apply(e);applied.Add(e);}return new(true,null,plan.TextKey,plan.Choices,applied);}catch{for(var i=applied.Count-1;i>=0;i--)try{_sink.Revert(applied[i]);}catch{}throw;} }
    public bool AcceptQuest(RuntimeQuestJournal journal,string questId,DateTimeOffset now){if(!_quests.TryGet(questId,out var q))return false;return journal.Accept(questId,_questPolicy.ResolveObjectives(q,journal.ActorId,now),now);}
    public RuntimeNarrativeResult CompleteQuest(RuntimeQuestJournal journal,string questId,DateTimeOffset now)
    {if(!_quests.TryGet(questId,out var q))return new(false,"unknown-quest",null,Array.Empty<RuntimeDialogueChoice>(),Array.Empty<RuntimeNarrativeEvent>());var before=journal.Capture();if(!journal.Complete(questId,now))return new(false,"quest-not-completable",null,Array.Empty<RuntimeDialogueChoice>(),Array.Empty<RuntimeNarrativeEvent>());var events=_questPolicy.ResolveRewards(q,journal.Capture(),now);var applied=new List<RuntimeNarrativeEvent>();try{foreach(var e in events){_sink.Apply(e);applied.Add(e);}return new(true,null,null,Array.Empty<RuntimeDialogueChoice>(),applied);}catch{for(var i=applied.Count-1;i>=0;i--)try{_sink.Revert(applied[i]);}catch{}journal.Restore(before);throw;}}
}
