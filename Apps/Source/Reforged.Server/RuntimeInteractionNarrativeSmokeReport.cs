using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeInteractionNarrativeSmokeResult(bool Passed,int QuestCount,int ExecutableQuestCount,int TextRecordCount,IReadOnlyList<string> Checks,IReadOnlyList<string> Failures)
{
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}

public static class RuntimeInteractionNarrativeSmokeReport
{
    private sealed class Sink:IRuntimeNarrativeEventSink
    {
        public bool Fail;
        public readonly List<RuntimeNarrativeEvent> Applied=new();
        public void Apply(RuntimeNarrativeEvent value){if(Fail)throw new InvalidOperationException("sink-failure");Applied.Add(value);}
        public void Revert(RuntimeNarrativeEvent value)=>Applied.Remove(value);
    }
    private sealed class DialoguePolicy:IRuntimeDialoguePolicy
    {
        public RuntimeDialoguePlan Resolve(RuntimeDialogueRequest r,RuntimeQuestJournalSnapshot j,DateTimeOffset n)=>
            new(true,null,"generated.text.key",Array.Empty<RuntimeDialogueChoice>(),Array.Empty<RuntimeNarrativeEvent>());
    }

    public static RuntimeInteractionNarrativeSmokeResult Run(string root,string world)
    {
        var checks=new List<string>();var failures=new List<string>();
        try
        {
            var quests=RuntimeNarrativeCatalog.Load(root,world,"quests");
            var texts=RuntimeNarrativeCatalog.Load(root,world,"events");
            if(quests.Count>=2100)checks.Add("quest-catalog");else failures.Add("quest-catalog");
            if(quests.ExecutableCount>=57)checks.Add("decoded-executable-quests");else failures.Add($"decoded-executable-quests:{quests.ExecutableCount}");
            if(texts.Count>0)checks.Add("text-catalog");else failures.Add("text-catalog");

            var quest=quests.RequireFirstExecutable();
            if(quest.Raw.TryGetProperty("execution_status",out var status)&&status.GetString()=="authoritative_script_graph")checks.Add("authoritative-script-graph");else failures.Add("authoritative-script-graph");
            if(quest.Raw.TryGetProperty("stages",out var stages)&&stages.ValueKind==JsonValueKind.Array&&stages.GetArrayLength()>0)checks.Add("ordered-stages");else failures.Add("ordered-stages");

            var journal=new RuntimeQuestJournal("actor.1");
            var sink=new Sink();
            var engine=new RuntimeInteractionNarrativeEngine(quests,new RuntimeGeneratedQuestPolicy(),new DialoguePolicy(),sink);
            if(engine.AcceptQuest(journal,quest.Id,DateTimeOffset.UtcNow))checks.Add("accept");else failures.Add("accept");
            var active=journal.Capture().Quests.Single();
            foreach(var objective in active.Objectives)journal.ApplyProgress(quest.Id,objective.ObjectiveId,objective.Required);
            if(journal.Capture().Quests.Single().Objectives.All(x=>x.Complete))checks.Add("progress");else failures.Add("progress");
            var done=engine.CompleteQuest(journal,quest.Id,DateTimeOffset.UtcNow);
            if(done.Succeeded)checks.Add("complete-reward");else failures.Add("complete-reward");

            var persisted=RuntimeQuestJournalPersistence.Serialize(journal);
            var restored=RuntimeQuestJournalPersistence.Restore("actor.1",persisted);
            if(restored.Validate(quests).Count==0&&restored.Capture().Quests.Single().Status==RuntimeQuestStatus.Completed)checks.Add("persistence");else failures.Add("persistence");

            var abandoned=new RuntimeQuestJournal("actor.2");
            engine.AcceptQuest(abandoned,quest.Id,DateTimeOffset.UtcNow);
            if(abandoned.Abandon(quest.Id))checks.Add("abandon");else failures.Add("abandon");

            var pendingId=FindPendingQuestId(root,world);
            if(!string.IsNullOrWhiteSpace(pendingId)&&!engine.AcceptQuest(new RuntimeQuestJournal("actor.3"),pendingId,DateTimeOffset.UtcNow))checks.Add("pending-evidence-blocked");else failures.Add("pending-evidence-blocked");

            var rollbackJournal=new RuntimeQuestJournal("actor.4");
            engine.AcceptQuest(rollbackJournal,quest.Id,DateTimeOffset.UtcNow);
            foreach(var objective in rollbackJournal.Capture().Quests.Single().Objectives)rollbackJournal.ApplyProgress(quest.Id,objective.ObjectiveId,objective.Required);
            sink.Fail=true;
            try{engine.CompleteQuest(rollbackJournal,quest.Id,DateTimeOffset.UtcNow);failures.Add("rollback");}
            catch{if(rollbackJournal.Capture().Quests.Single().Status==RuntimeQuestStatus.Active)checks.Add("rollback");else failures.Add("rollback");}

            return new(failures.Count==0,quests.Count,quests.ExecutableCount,texts.Count,checks,failures);
        }
        catch(Exception ex){failures.Add(ex.GetType().Name+":"+ex.Message);return new(false,0,0,0,checks,failures);}
    }

    private static string? FindPendingQuestId(string root,string world)
    {
        var path=Path.Combine(root,"Data",world,"quests.json");
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        foreach(var record in doc.RootElement.GetProperty("records").EnumerateArray())
        {
            if(record.TryGetProperty("runtime_mapping",out var map)&&map.TryGetProperty("status",out var status)&&status.GetString()=="pending")return record.GetProperty("id").GetString();
        }
        return null;
    }
}
