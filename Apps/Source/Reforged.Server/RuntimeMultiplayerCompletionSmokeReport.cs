using System.Collections.Concurrent;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeMultiplayerCompletionSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeMultiplayerCompletionSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        try
        {
            var state = new RuntimeMultiplayerCompletionState();
            state.CreateParty("party.1", "actor.1");
            var invite = state.Invite("party.1", "actor.1", "actor.2", DateTimeOffset.UtcNow.AddMinutes(1));
            state.AcceptInvite(invite.InviteId, "actor.2", DateTimeOffset.UtcNow);
            checks.Add("party-invite-accept");

            var declined = state.Invite("party.1", "actor.1", "actor.3", DateTimeOffset.UtcNow.AddMinutes(1));
            if (!state.DeclineInvite(declined.InviteId, "actor.3")) throw new InvalidOperationException("party-decline-failed");
            checks.Add("party-invite-decline");

            state.RegisterNpc("npc.1", "room.1", 100);
            var first = state.ApplyDamage("npc.1", "actor.1", 30);
            var second = state.ApplyDamage("npc.1", "actor.2", 20);
            if (first.CurrentHealth != 70 || second.CurrentHealth != 50 || second.TargetActorId != "actor.1") throw new InvalidOperationException("shared-combat-failed");
            checks.Add("shared-npc-combat");

            var killed = state.ApplyDamage("npc.1", "actor.2", 50, new Dictionary<string,int> { ["item.1"] = 1 }, DateTimeOffset.UtcNow.AddMinutes(5));
            if (!killed.Died || string.IsNullOrWhiteSpace(killed.CorpseId)) throw new InvalidOperationException("shared-corpse-not-created");
            checks.Add("shared-corpse-creation");

            var winners = new ConcurrentBag<RuntimeLootClaimResult>();
            Parallel.For(0, 24, _ => winners.Add(state.ClaimLoot(killed.CorpseId!, "actor.1", "item.1", 1)));
            if (winners.Count(x => x.Succeeded) != 1) throw new InvalidOperationException("concurrent-loot-duplication");
            checks.Add("atomic-shared-loot");

            var awards = state.AwardPartyExperience("actor.1", 101);
            if (awards.Count != 2 || awards.Values.Sum() != 101) throw new InvalidOperationException("shared-xp-failed");
            checks.Add("shared-party-xp");

            var disconnected = state.Disconnect("actor.2", "room.1", DateTimeOffset.UtcNow);
            var reconnected = state.Reconnect("actor.2");
            if (reconnected is null || reconnected.PartyId != disconnected.PartyId || reconnected.RoomId != "room.1") throw new InvalidOperationException("disconnect-recovery-failed");
            checks.Add("disconnect-recovery");

            var json = state.ToJson();
            var restored = RuntimeMultiplayerCompletionState.FromJson(json);
            if (restored.Validate().Count != 0) throw new InvalidOperationException("restart-recovery-invalid");
            checks.Add("restart-recovery");

            var race = new RuntimeMultiplayerCompletionState();
            race.RegisterNpc("npc.race", "room.1", 1000);
            Parallel.For(0, 100, i => race.ApplyDamage("npc.race", $"actor.{i % 4}", 10));
            var raceSave = race.Capture();
            var npc = raceSave.Npcs.Single(x => x.NpcId == "npc.race");
            if (!npc.IsDead || npc.CurrentHealth != 0 || npc.Threat.Values.Sum() != 1000) throw new InvalidOperationException("concurrent-combat-state-corrupt");
            checks.Add("concurrent-combat-validation");

            if (state.Validate().Count != 0) throw new InvalidOperationException("multiplayer-validator-failed");
            checks.Add("multiplayer-state-validator");
        }
        catch (Exception ex)
        {
            failures.Add(ex.GetType().Name + ":" + ex.Message);
        }
        return new RuntimeMultiplayerCompletionSmokeReport(failures.Count == 0, checks, failures);
    }
}
