using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNarrativeDefinition(string Id,string Name,string Kind,JsonElement Raw);
public sealed class RuntimeNarrativeCatalog
{
    private readonly Dictionary<string,RuntimeNarrativeDefinition> _records;
    private RuntimeNarrativeCatalog(IEnumerable<RuntimeNarrativeDefinition> records) => _records=records.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);
    public int Count=>_records.Count;
    public int ExecutableCount=>_records.Values.Count(IsExecutable);
    public bool TryGet(string id,out RuntimeNarrativeDefinition value)=>_records.TryGetValue(id,out value!);
    public bool TryGetExecutable(string id,out RuntimeNarrativeDefinition value)
    {
        if (_records.TryGetValue(id,out value!) && IsExecutable(value)) return true;
        value=null!;
        return false;
    }
    public RuntimeNarrativeDefinition RequireFirstExecutable() => _records.Values.FirstOrDefault(IsExecutable)
        ?? throw new InvalidOperationException("No executable generated quest definitions were found.");
    public IReadOnlyList<RuntimeNarrativeDefinition> ExecutableDefinitions() => _records.Values.Where(IsExecutable).OrderBy(x=>x.Name).ToArray();
    private static bool IsExecutable(RuntimeNarrativeDefinition value)
    {
        if (value.Raw.TryGetProperty("runtime_mapping",out var mapping) && mapping.ValueKind==JsonValueKind.Object && mapping.TryGetProperty("status",out var status))
            return string.Equals(status.GetString(),"executable",StringComparison.OrdinalIgnoreCase);
        return value.Raw.TryGetProperty("execution_status",out var execution) && string.Equals(execution.GetString(),"authoritative_script_graph",StringComparison.OrdinalIgnoreCase);
    }
    public static RuntimeNarrativeCatalog Load(string projectRoot,string worldId,string family)
    {
        var configured=ServerDataRootResolver.Resolve(projectRoot, worldId); var path=Path.Combine(configured.DataRoot,$"{family}.json");
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
    public bool AcceptQuest(RuntimeQuestJournal journal,string questId,DateTimeOffset now){if(!_quests.TryGetExecutable(questId,out var q))return false;return journal.Accept(questId,_questPolicy.ResolveObjectives(q,journal.ActorId,now),now);}
    public RuntimeNarrativeResult CompleteQuest(RuntimeQuestJournal journal,string questId,DateTimeOffset now)
    {if(!_quests.TryGetExecutable(questId,out var q))return new(false,"unknown-or-non-executable-quest",null,Array.Empty<RuntimeDialogueChoice>(),Array.Empty<RuntimeNarrativeEvent>());var before=journal.Capture();if(!journal.Complete(questId,now))return new(false,"quest-not-completable",null,Array.Empty<RuntimeDialogueChoice>(),Array.Empty<RuntimeNarrativeEvent>());var events=_questPolicy.ResolveRewards(q,journal.Capture(),now);var applied=new List<RuntimeNarrativeEvent>();try{foreach(var e in events){_sink.Apply(e);applied.Add(e);}return new(true,null,null,Array.Empty<RuntimeDialogueChoice>(),applied);}catch{for(var i=applied.Count-1;i>=0;i--)try{_sink.Revert(applied[i]);}catch{}journal.Restore(before);throw;}}
}


public sealed class RuntimeGeneratedQuestPolicy : IRuntimeQuestPolicy
{
    public IReadOnlyList<RuntimeQuestObjectiveProgress> ResolveObjectives(RuntimeNarrativeDefinition quest,string actorId,DateTimeOffset nowUtc)
    {
        var objectives=new List<RuntimeQuestObjectiveProgress>();
        if (quest.Raw.TryGetProperty("item_actions",out var items) && items.ValueKind==JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var id=ReadInt(item,"item_id");
                if(id<=0)continue;
                var objectiveId=$"item.{id}";
                if(objectives.Any(x=>x.ObjectiveId.Equals(objectiveId,StringComparison.OrdinalIgnoreCase)))continue;
                objectives.Add(new(objectiveId,0,1,false,new Dictionary<string,string>{{"kind","takeitem"},{"item_ref",objectiveId}}));
            }
        }
        if(objectives.Count==0)
        {
            var ability=ReadInt(quest.Raw,"quest_ability_id");
            objectives.Add(new($"ability.{ability}.stage",0,1,false,new Dictionary<string,string>{{"kind","script-stage"}}));
        }
        return objectives;
    }

    public IReadOnlyList<RuntimeNarrativeEvent> ResolveRewards(RuntimeNarrativeDefinition quest,RuntimeQuestJournalSnapshot journal,DateTimeOffset nowUtc)
    {
        var events=new List<RuntimeNarrativeEvent>();
        if(quest.Raw.TryGetProperty("ability_mutations",out var mutations)&&mutations.ValueKind==JsonValueKind.Array)
        {
            foreach(var mutation in mutations.EnumerateArray())
            {
                var ability=ReadInt(mutation,"ability_id");
                var value=ReadInt(mutation,"ability_value");
                if(ability<=0)continue;
                events.Add(new(Guid.NewGuid().ToString("N"),"quest-ability",journal.ActorId,$"ability.{ability}",quest.Id,value,nowUtc,new Dictionary<string,string>{{"opcode","giveability"}}));
            }
        }
        return events;
    }

    private static int ReadInt(JsonElement root,string name)=>root.TryGetProperty(name,out var value)&&int.TryParse(value.ToString(),out var parsed)?parsed:0;
}

public static class RuntimeQuestJournalPersistence
{
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=false};
    public static string Serialize(RuntimeQuestJournal journal)=>JsonSerializer.Serialize(journal.Capture(),Options);
    public static RuntimeQuestJournal Restore(string actorId,string? json)
    {
        var journal=new RuntimeQuestJournal(actorId);
        if(string.IsNullOrWhiteSpace(json))return journal;
        var snapshot=JsonSerializer.Deserialize<RuntimeQuestJournalSnapshot>(json,Options)??throw new InvalidOperationException("quest-journal-invalid");
        journal.Restore(snapshot);
        return journal;
    }
}
