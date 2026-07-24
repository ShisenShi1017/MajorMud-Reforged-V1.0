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
            if(string.IsNullOrWhiteSpace(pendingId))
            {
                checks.Add("pending-evidence-resolved");
            }
            else if(!engine.AcceptQuest(new RuntimeQuestJournal("actor.3"),pendingId,DateTimeOffset.UtcNow))
            {
                checks.Add("pending-evidence-blocked");
            }
            else
            {
                failures.Add("pending-evidence-blocked");
            }

            var rollbackJournal=new RuntimeQuestJournal("actor.4");
            engine.AcceptQuest(rollbackJournal,quest.Id,DateTimeOffset.UtcNow);
            foreach(var objective in rollbackJournal.Capture().Quests.Single().Objectives)rollbackJournal.ApplyProgress(quest.Id,objective.ObjectiveId,objective.Required);
            sink.Fail=true;
            try{engine.CompleteQuest(rollbackJournal,quest.Id,DateTimeOffset.UtcNow);failures.Add("rollback");}
            catch{if(rollbackJournal.Capture().Quests.Single().Status==RuntimeQuestStatus.Active)checks.Add("rollback");else failures.Add("rollback");}

            var dialogue = RuntimeQuestDialogueCatalog.Load(root, world);
            if (dialogue.StageScriptCount == 182) checks.Add("quest-dialogue-stage-scripts-182"); else failures.Add($"quest-dialogue-stage-scripts:{dialogue.StageScriptCount}");
            if (dialogue.NpcQuestEntryTriggerCount == 83) checks.Add("npc-quest-entry-triggers-83"); else failures.Add($"npc-quest-entry-triggers:{dialogue.NpcQuestEntryTriggerCount}");
            if (dialogue.LinkedStageScriptCount == 182) checks.Add("npc-quest-linked-stage-scripts-182"); else failures.Add($"npc-quest-linked-stage-scripts:{dialogue.LinkedStageScriptCount}");

            var itemCatalog = RuntimeItemCatalog.Load(root, world);
            var branchCharacter = SmokeCharacter(world, "actor.dialogue.branch", new Dictionary<string,string> { ["quest.ability.134"] = "8" });
            var branch = dialogue.Dispatch("npc.106", new RuntimeQuestDialogueContext(branchCharacter, itemCatalog));
            if (branch.Matched && branch.Lines.Contains("textblock.2617")) checks.Add("npc-dialogue-branch-dispatch"); else failures.Add("npc-dialogue-branch-dispatch");

            var itemCharacter = SmokeCharacter(world, "actor.dialogue.item", new Dictionary<string,string>
            {
                ["quest.ability.128"] = "9",
                ["alignment"] = "0"
            }, new RuntimeItemState(new[]
            {
                new RuntimeItemInstance("quest-smoke-item-948", "item.948", "actor.dialogue.item", RuntimeItemLocations.Character, "actor.dialogue.item", null, 0, 1, null)
            }, 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>()));
            var itemContext = new RuntimeQuestDialogueContext(itemCharacter, itemCatalog);
            var itemResult = dialogue.Dispatch("npc.456", itemContext, "quest_flow.EvilQuest.npc456.tb1296");
            var updated = itemContext.ToCharacterRecord();
            var updatedItems = updated.ItemState?.Items ?? Array.Empty<RuntimeItemInstance>();
            if (itemResult.Matched && !updatedItems.Any(x => x.CanonicalItemId == "item.948") && updatedItems.Any(x => x.CanonicalItemId == "item.963"))
                checks.Add("quest-item-consume-reward-transition");
            else failures.Add("quest-item-consume-reward-transition");
            if (itemResult.Events.Any(x => x.EventType == "world_intent.cast_spell" && x.SubjectId == "spell.582") &&
                itemResult.Events.Any(x => x.EventType == "world_intent.summon_npc" && x.SubjectId == "npc.465"))
                checks.Add("quest-world-intent-cast-summon");
            else failures.Add("quest-world-intent-cast-summon");

            var randomCharacter = SmokeCharacter(world, "actor.dialogue.random", new Dictionary<string,string>
            {
                ["quest.ability.50"] = "4",
                ["runtime.random.roll.9382"] = "1",
                ["runtime.random.roll.9572"] = "100"
            }, new RuntimeItemState(new[]
            {
                new RuntimeItemInstance("quest-smoke-item-1746", "item.1746", "actor.dialogue.random", RuntimeItemLocations.Character, "actor.dialogue.random", null, 0, 1, null)
            }, 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>()));
            var randomContext = new RuntimeQuestDialogueContext(randomCharacter, itemCatalog, RuntimeQuestWorldIntentValidator.Load(root, world));
            var randomResult = dialogue.Dispatch("npc.831", randomContext, "quest_flow.MageBaneQuest.npc831.tb9286");
            var randomItems = randomContext.ToCharacterRecord().ItemState?.Items ?? Array.Empty<RuntimeItemInstance>();
            if (randomResult.Matched &&
                randomResult.Events.Count(x => x.EventType == "random_textblock_selected") == 2 &&
                randomItems.Any(x => x.CanonicalItemId == "item.1336") &&
                randomItems.Any(x => x.CanonicalItemId == "item.1334") &&
                !randomItems.Any(x => x.CanonicalItemId is "item.1746" or "item.512"))
                checks.Add("quest-random-textblock-tables");
            else failures.Add("quest-random-textblock-tables");

            var remoteCharacter = SmokeCharacter(world, "actor.dialogue.remote", new Dictionary<string,string> { ["quest.ability.133"] = "4" });
            var remoteResult = dialogue.Dispatch("npc.503", new RuntimeQuestDialogueContext(remoteCharacter, itemCatalog, RuntimeQuestWorldIntentValidator.Load(root, world)), "quest_flow.PhoenixQuest.npc503.tb1433");
            if (remoteResult.Matched && remoteResult.Events.Any(x => x.EventType == "world_intent.remote_action"))
                checks.Add("quest-world-intent-remoteaction");
            else failures.Add("quest-world-intent-remoteaction");

            var intentValidator = RuntimeQuestWorldIntentValidator.Load(root, world);
            try
            {
                intentValidator.Validate("cast", "spell.582");
                intentValidator.Validate("summon", "npc.465");
                intentValidator.Validate("teleport", "room.map1.155");
                intentValidator.Validate("remoteaction", "1423.66.0.3");
                checks.Add("quest-world-intent-target-validation");
            }
            catch (Exception ex)
            {
                failures.Add("quest-world-intent-target-validation:" + ex.Message);
            }

            var failureCharacter = SmokeCharacter(world, "actor.dialogue.failure", new Dictionary<string,string> { ["quest.ability.128"] = "9", ["alignment"] = "0" });
            var failureContext = new RuntimeQuestDialogueContext(failureCharacter, itemCatalog);
            var beforeFailure = failureContext.ToCharacterRecord();
            var failure = dialogue.Dispatch("npc.456", failureContext, "quest_flow.EvilQuest.npc456.tb1296");
            var afterFailure = failureContext.ToCharacterRecord();
            if (!failure.Matched && beforeFailure.Experience == afterFailure.Experience && beforeFailure.Conditions.Count == afterFailure.Conditions.Count && (afterFailure.ItemState?.Items.Count ?? 0) == 0)
                checks.Add("quest-failure-no-mutation");
            else failures.Add("quest-failure-no-mutation");

            var operationCounts = CountScriptOpcodes(root, world);
            var requiredOpcodes = new[] { "random", "cast", "summon", "teleport", "remoteaction" };
            if (requiredOpcodes.All(operationCounts.ContainsKey) && operationCounts["random"] == 2 && operationCounts["cast"] == 11 && operationCounts["summon"] == 3 && operationCounts["teleport"] == 2 && operationCounts["remoteaction"] == 1)
                checks.Add("quest-edge-opcode-coverage");
            else failures.Add("quest-edge-opcode-coverage");

            if (ValidateAllQuestEntries(root, world, dialogue, itemCatalog).Count == 0)
                checks.Add("quest-entry-exhaustive-dispatch-validation");
            else failures.Add("quest-entry-exhaustive-dispatch-validation");

            return new(failures.Count==0,quests.Count,quests.ExecutableCount,texts.Count,checks,failures);
        }
        catch(Exception ex)
        {
            failures.Add(ex.GetType().Name+":"+ex.Message);
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                failures.Add("stack:" + string.Join(" | ", ex.StackTrace.Split(Environment.NewLine).Take(6).Select(line => line.Trim())));
            }
            return new(false,0,0,0,checks,failures);
        }
    }

    private static RuntimeCharacterRecord SmokeCharacter(
        string world,
        string id,
        IReadOnlyDictionary<string,string>? conditions = null,
        RuntimeItemState? itemState = null) =>
        new(
            id,
            "account.smoke",
            id,
            world,
            "race.1",
            "Human",
            "class.1",
            "Warrior",
            50,
            0,
            100,
            100,
            100,
            100,
            "room.map1.1",
            1,
            new Dictionary<string,int>(),
            new Dictionary<string,int>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string,string?>(),
            conditions ?? new Dictionary<string,string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ItemState: itemState);

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

    private static IReadOnlyDictionary<string,int> CountScriptOpcodes(string root,string world)
    {
        var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Data",world,"scripts.json")));
        foreach(var record in doc.RootElement.GetProperty("records").EnumerateArray())
        {
            if(!record.TryGetProperty("operations",out var operations)||operations.ValueKind!=JsonValueKind.Array)continue;
            foreach(var op in operations.EnumerateArray())
            {
                var code=op.TryGetProperty("opcode",out var opcode)?opcode.GetString():null;
                if(string.IsNullOrWhiteSpace(code))continue;
                counts[code]=counts.TryGetValue(code,out var current)?current+1:1;
            }
        }
        return counts;
    }

    private static IReadOnlyList<string> ValidateAllQuestEntries(string root,string world,RuntimeQuestDialogueCatalog dialogue,RuntimeItemCatalog items)
    {
        var failures=new List<string>();
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Data",world,"triggers.json")));
        foreach(var trigger in doc.RootElement.GetProperty("records").EnumerateArray().Where(x=>x.TryGetProperty("trigger_type",out var type)&&type.GetString()=="npc_quest_entry"))
        {
            var relationships=trigger.GetProperty("relationships");
            var npc=relationships.GetProperty("npc").GetString()??string.Empty;
            var quest=relationships.GetProperty("quest").GetString()??string.Empty;
            if(string.IsNullOrWhiteSpace(npc)||string.IsNullOrWhiteSpace(quest)){failures.Add("missing-trigger-scope");continue;}
            try
            {
                _=dialogue.Dispatch(npc,new RuntimeQuestDialogueContext(SmokeCharacter(world,"actor.exhaustive."+npc.Replace('.','_')),items,RuntimeQuestWorldIntentValidator.Load(root,world)),quest);
            }
            catch(Exception ex)
            {
                failures.Add($"{quest}:{npc}:{ex.Message}");
            }
        }
        return failures;
    }
}
