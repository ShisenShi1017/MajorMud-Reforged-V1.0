using RealmGate.App.Models;

namespace RealmGate.App.Services;

public static class ReforgedPartyAutomationRegressionSmoke
{
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "reforged-party-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ReforgedPartyAutomationService(root);
            var session = Session();
            var profile = service.LoadProfile(session);
            profile.Preferences.Enabled = true;
            profile.Preferences.LeaderName = "Leader";
            profile.Preferences.AutoFollowLeader = true;
            profile.Preferences.AutoAssistLeader = true;
            session.PartyUpdatedAt = DateTimeOffset.UtcNow;
            session.PartyMembers.Add(new ClientPartyMemberState("Leader", "Warrior", 100, 100, "Mana", "Frontrank", "Ready", false));

            var plan = service.Plan(session, profile, DateTimeOffset.UtcNow);
            if (plan.Kind != ReforgedPartyActionKind.FollowLeader || !plan.Command.Contains("Leader", StringComparison.Ordinal)) return 71;
            service.MarkExecuted(session, plan, DateTimeOffset.UtcNow);

            session.InCombat = true;
            plan = service.Plan(session, profile, DateTimeOffset.UtcNow.AddSeconds(5));
            if (plan.Kind != ReforgedPartyActionKind.AssistLeader) return 72;

            profile.Preferences.AutoRescueMembers = true;
            profile.Preferences.RescueBelowPercent = 40;
            session.PartyMembers.Add(new ClientPartyMemberState("Mage", "Mage", 25, 50, "Mana", "Backrank", "Fighting", false));
            plan = service.Plan(session, profile, DateTimeOffset.UtcNow.AddSeconds(10));
            if (plan.Kind != ReforgedPartyActionKind.RescueMember || plan.Target != "Mage") return 73;

            profile.Preferences.Role = ReforgedPartyRole.LeadParty;
            profile.Preferences.AutoRescueMembers = false;
            profile.Preferences.AutoInviteNamedMembers = true;
            profile.Preferences.InviteNames = "Friend";
            session.OnlinePlayersUpdatedAt = DateTimeOffset.UtcNow.AddSeconds(15);
            session.OnlinePlayers.Add(new ClientOnlinePlayerState("Friend", "Cleric", 12));
            plan = service.Plan(session, profile, DateTimeOffset.UtcNow.AddSeconds(15));
            if (plan.Kind != ReforgedPartyActionKind.InviteMember || plan.Target != "Friend") return 74;

            service.SaveProfile(session, profile);
            var loaded = service.LoadProfile(session);
            if (!loaded.Preferences.Enabled || loaded.Preferences.Role != ReforgedPartyRole.LeadParty) return 75;
            return 0;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static ClientSessionConnection Session() => new()
    {
        CharacterName = "Tester",
        ServerProfileName = "Local Host",
        WorldName = "Classic",
        HostName = "127.0.0.1",
        Port = 4000,
        ConnectionState = ClientConnectionState.Connected,
        HealthPercent = 100,
        ManaPercent = 100,
        ManaCurrent = 100,
        ManaMaximum = 100,
        PromptUpdatedAt = DateTimeOffset.UtcNow
    };
}
