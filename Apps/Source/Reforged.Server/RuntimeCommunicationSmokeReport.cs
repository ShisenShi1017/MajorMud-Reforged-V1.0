using System.Text.Json;

namespace Reforged.Server;

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

            service.SetIgnored("actor.two", "actor.one", true);
            var ignored = service.Send(new(RuntimeCommunicationChannel.Say, "actor.one", "ignored"), now.AddSeconds(1));
            if (ignored.Deliveries.Any(x => x.RecipientActorId == "actor.two")) throw new InvalidOperationException("ignore-filter-failed");
            checks.Add("ignore-filter");

            service.SetIgnored("actor.two", "actor.one", false);
            var party = service.Send(new(RuntimeCommunicationChannel.Party, "actor.one", "party"), now.AddSeconds(2));
            if (!party.Succeeded || party.Deliveries.Count != 2) throw new InvalidOperationException("party-routing-failed");
            checks.Add("party-channel-integration");

            var guild = service.Send(new(RuntimeCommunicationChannel.Guild, "actor.one", "guild"), now.AddSeconds(3));
            if (!guild.Succeeded || guild.Deliveries.Count != 2) throw new InvalidOperationException("guild-routing-failed");
            checks.Add("guild-routing");

            service.UpsertActor(new("actor.one", "One", "room.1", true, false, false, true, false, "guild.a"));
            var hidden = service.Send(new(RuntimeCommunicationChannel.Tell, "actor.one", "secret", "actor.three"), now.AddSeconds(4));
            if (!hidden.Succeeded || hidden.Deliveries.Single(x => x.RecipientActorId == "actor.three").SenderPresentation != "One") throw new InvalidOperationException("hidden-admin-visibility-failed");
            checks.Add("hidden-sender-policy");

            var denied = service.Send(new(RuntimeCommunicationChannel.AdministrativeBroadcast, "actor.one", "notice"), now.AddSeconds(5));
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
