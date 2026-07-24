namespace Reforged.Server;

public static class RuntimeCombatSpellCastExecutionSmokeReport
{
    private sealed class Sink : IRuntimeCombatSpellMutationSink
    {
        public readonly Dictionary<string,int> Values = new(StringComparer.OrdinalIgnoreCase);
        public string? FailTarget;
        public object Capture(string id) => Values.GetValueOrDefault(id);
        public void Apply(RuntimeCombatSpellMutation m)
        {
            if (m.TargetId == FailTarget) throw new InvalidOperationException("fixture_sink_failure");
            Values[m.TargetId] = Values.GetValueOrDefault(m.TargetId) + m.Magnitude;
        }
        public void Restore(string id, object snapshot) => Values[id] = (int)snapshot;
    }

    public static IReadOnlyDictionary<string,bool> Run()
    {
        var profile = new RuntimeResolutionProfile("p", RuntimeResolutionConfidence.Proven, "arcane", null, null,
            4,4,0,0,0,0,0,99,false,100,1,1,new[]{"fixture"},"reject");
        var binding = new RuntimeCombatSpellDefinitionBinding("b", RuntimeCombatSpellDefinitionKind.Spell,
            "spell.9003","p",4,4,"arcane",null,null,new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject");
        var resolver = new RuntimeCombatSpellBoundResolver(new RuntimeCombatSpellBindingCatalog(new[]{binding}),
            new[]{profile},new RuntimeCombatSpellResolutionEngine());
        var policies = new[]{new RuntimeSpellTargetPolicy("party","spell.9003",RuntimeSpellTargetMode.Party,
            new HashSet<RuntimeSpellTargetKind>{RuntimeSpellTargetKind.Character},2,true,true,true,true,true,false,
            new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject")};
        var sink = new Sink();
        var lifecycle = ReadyLifecycle();
        var execution = new RuntimeSpellCastExecution(lifecycle,new RuntimeSpellTargeting(policies),resolver,sink);
        var result = execution.Execute(Request());

        var rollbackLifecycle = ReadyLifecycle("cast.rollback");
        var rollbackSink = new Sink { FailTarget = "character.2" };
        var rollback = new RuntimeSpellCastExecution(rollbackLifecycle,new RuntimeSpellTargeting(policies),resolver,rollbackSink);
        var rolledBack = Throws(() => rollback.Execute(Request("cast.rollback")));
        return new Dictionary<string,bool>
        {
            ["multi_target_atomic_commit"] = result.Committed && sink.Values.Values.SequenceEqual(new[]{4,4}),
            ["stable_per_target_resolution_ids"] = result.Commits.Select(x=>x.Trace.ResolutionId).SequenceEqual(new[]{"cast.ok:target:0","cast.ok:target:1"}),
            ["cast_completed_after_commit"] = result.Cast.State == RuntimeSpellCastState.Completed,
            ["sink_failure_rolls_back_all_targets"] = rolledBack && rollbackSink.Values.Values.All(x=>x==0),
            ["sink_failure_marks_cast_failed"] = rollbackLifecycle.Get("cast.rollback").State == RuntimeSpellCastState.Failed
        };
    }

    private static RuntimeSpellCastLifecycle ReadyLifecycle(string castId="cast.ok")
    {
        var life = new RuntimeSpellCastLifecycle(new[]{new RuntimeSpellCastTimingPolicy("t","spell.9003",0,
            new HashSet<RuntimeSpellInterruptionReason>(),new[]{"fixture"},RuntimeResolutionConfidence.Proven,"reject")});
        life.Start(castId,"character.1","spell.9003",1); life.Advance(1); return life;
    }
    private static RuntimeSpellCastExecutionRequest Request(string castId="cast.ok")
    {
        var candidates = new[]{
            new RuntimeSpellTargetCandidate("character.1",RuntimeSpellTargetKind.Character,"room.1",true,true,true,"party.1"),
            new RuntimeSpellTargetCandidate("character.2",RuntimeSpellTargetKind.Character,"room.1",true,true,true,"party.1")};
        return new RuntimeSpellCastExecutionRequest(castId,"combat.1",1,
            new RuntimeSpellTargetRequest(castId,"character.1","spell.9003","room.1","party.1",null,candidates),
            new Dictionary<string,RuntimeSpellTargetResolutionInput>(StringComparer.OrdinalIgnoreCase){
                ["character.1"]=new("character.1",0,0),["character.2"]=new("character.2",0,0)},
            RuntimeDamageDisposition.Healing,0,7);
    }
    private static bool Throws(Action action){try{action();return false;}catch{return true;}}
}
