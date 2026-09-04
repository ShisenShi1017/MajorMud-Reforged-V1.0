using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeCommunicationSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeCommunicationSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        try
        {
            var now = DateTimeOffset.Parse("2026-07-15T18:00:00Z");
            var parties = new RuntimePartyCoordinationService();
            parties.Create("party.alpha", "actor.one");
            var invite = parties.Invite("party.alpha", "actor.one", "actor.two", now);
            parties.Accept(invite.InviteId, "actor.two", now.AddSeconds(1));
            var service = new RuntimeCommunicationService(parties: parties);
            service.UpsertActor(new("actor.one", "One", "room.1", true, false, false, false, false, "guild.a"));
            service.UpsertActor(new("actor.two", "Two", "room.1", true, false, false, false, false, "guild.a"));
            service.UpsertActor(new("actor.three", "Three", "room.2", true, false, false, false, true, "guild.b"));

            var say = service.Send(new(RuntimeCommunicationChannel.Say, "actor.one", "hello"), now);
            if (!say.Succeeded || say.Deliveries.Count != 2) throw new InvalidOperationException("say-routing-failed");
            checks.Add("local-room-routing");

            var yell = service.Send(new(RuntimeCommunicationChannel.Yell, "actor.one", "nearby"), now.AddSeconds(1));
            if (!yell.Succeeded || yell.Deliveries.Count != 2) throw new InvalidOperationException("yell-adjacency-default-failed");
            checks.Add("yell-current-room-routing");

            var party = service.Send(new(RuntimeCommunicationChannel.Party, "actor.one", "party"), now.AddSeconds(2));
            if (!party.Succeeded || party.Deliveries.Count != 2) throw new InvalidOperationException("party-routing-failed");
            checks.Add("party-channel-integration");

            var guild = service.Send(new(RuntimeCommunicationChannel.Guild, "actor.one", "guild"), now.AddSeconds(3));
            if (!guild.Succeeded || guild.Deliveries.Count != 2) throw new InvalidOperationException("guild-routing-failed");
            checks.Add("guild-routing");

            var whisper = service.Send(new(RuntimeCommunicationChannel.Whisper, "actor.one", "secret", "actor.two"), now.AddSeconds(4));
            if (whisper.Succeeded || whisper.FailureCode != "communication-whisper-native-unresolved") throw new InvalidOperationException("whisper-evidence-gate-failed");
            checks.Add("whisper-evidence-gate");

            var tell = service.Send(new(RuntimeCommunicationChannel.Tell, "actor.one", "secret", "actor.three"), now.AddSeconds(5));
            if (tell.Succeeded || tell.FailureCode != "communication-tell-native-unresolved") throw new InvalidOperationException("tell-evidence-gate-failed");
            checks.Add("tell-evidence-gate");

            var denied = service.Send(new(RuntimeCommunicationChannel.AdministrativeBroadcast, "actor.one", "notice"), now.AddSeconds(20));
            if (denied.Succeeded || denied.FailureCode != "communication-administrator-required") throw new InvalidOperationException("admin-boundary-failed");
            checks.Add("administrative-permission-boundary");

            if (service.Validate().Count != 0) throw new InvalidOperationException("communication-validator-failed");
            checks.Add("communication-state-validation");
        }
        catch (Exception ex)
        {
            failures.Add(ex.GetType().Name + ":" + ex.Message);
        }
        return new(failures.Count == 0, checks, failures);
    }
}
