namespace Reforged.Server;

public static class RuntimeCombatSpellResourceLifecycleSmoke
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var policy = new RuntimeSpellResourcePolicy(
            "policy.smoke.spell.999999", "spell.999999", "mana", 12,
            RuntimeSpellResourceTiming.ReserveAtStartCommitAtCompletion, true, true,
            new[] { "Librarium/Objects/combat_spells/combat_spell_records.json#smoke-policy" },
            RuntimeResolutionConfidence.StronglyCorrelated,
            "Reject when a canonical spell lacks an evidence-backed timing policy.");
        var sink = new SmokeResourceSink(40);
        var lifecycle = new RuntimeSpellResourceLifecycle(new[] { policy }, sink);
        var started = lifecycle.Start("cast-1", "character:1", "spell.999999");
        var afterReserve = sink.Available == 28 && sink.Reserved == 12;
        var finished = lifecycle.Finish(started.Reservation, RuntimeSpellCastTerminalState.Completed);
        var completionCommits = finished.Committed && sink.Available == 28 && sink.Reserved == 0;

        var interrupted = lifecycle.Start("cast-2", "character:1", "spell.999999");
        lifecycle.Finish(interrupted.Reservation, RuntimeSpellCastTerminalState.Interrupted);
        var interruptionRefunds = sink.Available == 28 && sink.Reserved == 0;

        var unresolvedRejected = false;
        try
        {
            var unresolved = policy with { PolicyId = "policy.unresolved.spell.1304", SpellId = "spell.1304", Timing = RuntimeSpellResourceTiming.Unresolved };
            new RuntimeSpellResourceLifecycle(new[] { unresolved }, sink).Start("cast-3", "character:1", "spell.1304");
        }
        catch (InvalidOperationException ex) when (ex.Message == "SPELL_RESOURCE_TIMING_UNRESOLVED") { unresolvedRejected = true; }

        var duplicateFinishRejected = false;
        try { lifecycle.Finish(started.Reservation, RuntimeSpellCastTerminalState.Completed); }
        catch (InvalidOperationException ex) when (ex.Message == "SPELL_RESOURCE_CAST_ALREADY_FINISHED") { duplicateFinishRejected = true; }

        return new Dictionary<string, bool>
        {
            ["reserve_at_start"] = started.Reserved && afterReserve,
            ["commit_at_completion"] = completionCommits,
            ["interrupt_refund"] = interruptionRefunds,
            ["unresolved_timing_fails_closed"] = unresolvedRejected,
            ["duplicate_finish_fails_closed"] = duplicateFinishRejected
        };
    }

    private sealed class SmokeResourceSink : IRuntimeSpellResourceSink
    {
        public int Available { get; private set; }
        public int Reserved { get; private set; }
        public SmokeResourceSink(int available) => Available = available;
        public int GetAvailable(string actorId, string resourceType) => Available;
        public object Capture(string actorId, string resourceType) => (Available, Reserved);
        public void Reserve(RuntimeSpellResourceReservation reservation) { Available -= reservation.Cost; Reserved += reservation.Cost; }
        public void Commit(RuntimeSpellResourceReservation reservation)
        {
            if (reservation.Timing == RuntimeSpellResourceTiming.CommitAtCompletion) Available -= reservation.Cost;
            else Reserved -= reservation.Cost;
        }
        public void Refund(RuntimeSpellResourceReservation reservation) { Available += reservation.Cost; Reserved -= reservation.Cost; }
        public void Restore(string actorId, string resourceType, object snapshot)
        {
            var state = ((int available, int reserved))snapshot;
            Available = state.available;
            Reserved = state.reserved;
        }
    }
}
