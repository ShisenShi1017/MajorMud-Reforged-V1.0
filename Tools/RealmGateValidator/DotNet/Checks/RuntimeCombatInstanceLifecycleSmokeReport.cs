namespace RealmGate.Server;

public static class RuntimeCombatInstanceLifecycleSmokeReport
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var policies = new[] {
            new RuntimeCombatSchedulingPolicy("combat.character.fixture", "character", 10, 4, new[]{"fixture"}, RuntimeResolutionConfidence.Proven, "reject"),
            new RuntimeCombatSchedulingPolicy("combat.npc.fixture", "npc", 7, 4, new[]{"fixture"}, RuntimeResolutionConfidence.Proven, "reject")
        };
        var combat = new RuntimeCombatInstanceLifecycle("combat.1", "room.1", 100, policies);
        combat.AddParticipant("character.1", "character", 100);
        combat.AddParticipant("npc.2", "npc", 100);
        var turns = combat.Advance(104);
        var ordered = turns.Select(x => x.ActorId).SequenceEqual(new[]{"character.1","npc.2"});
        var delayed = combat.Participants.All(x => x.NextActionTick == 108);
        combat.SetParticipantState("npc.2", RuntimeCombatParticipantState.Defeated, 105);
        return new Dictionary<string, bool> {
            ["first_participant_activates_instance"] = combat.Transitions.First().To == RuntimeCombatInstanceState.Active,
            ["initiative_breaks_same_tick_ties"] = ordered,
            ["turn_commit_advances_action_clock"] = delayed,
            ["round_advances_once_per_due_batch"] = combat.RoundNumber == 1,
            ["opposition_loss_terminates_and_cleans_up"] = combat.State == RuntimeCombatInstanceState.Terminated && combat.Participants.All(x => x.State != RuntimeCombatParticipantState.Active),
            ["unresolved_policy_rejects"] = Throws(() => new RuntimeCombatInstanceLifecycle("combat.2", "room.1", 0, new[]{new RuntimeCombatSchedulingPolicy("x","character",null,null,new[]{"fixture"},RuntimeResolutionConfidence.StronglyCorrelated,"reject")}).AddParticipant("character.2","character",0)),
            ["tick_regression_rejects"] = Throws(() => { var x=new RuntimeCombatInstanceLifecycle("combat.3","room.1",0,policies); x.AddParticipant("character.3","character",0); x.AddParticipant("npc.3","npc",0); x.Advance(4); x.Advance(3); })
        };
    }
    private static bool Throws(Action action) { try { action(); return false; } catch { return true; } }
}
