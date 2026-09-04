using Reforged.App.Models;

namespace Reforged.App.Services;

public static class ReforgedRecoveryAutomationRegressionSmoke
{
    public static Task<int> RunAsync(IApplicationSettingsService settings)
    {
        _ = settings;
        var root = Path.Combine(Path.GetTempPath(), "RealmGate-recovery-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ReforgedRecoveryAutomationService(root);
            var session = BuildSession();
            var profile = service.LoadProfile(session);
            profile.Preferences.Enabled = true;
            profile.Preferences.AutoHealSelf = true;
            profile.Preferences.MajorHealAtPercent = 55;
            profile.Preferences.MinorHealAtPercent = 80;
            profile.Preferences.ReserveManaPercent = 20;
            profile.Preferences.RequireLearnedSpellForCastCommands = true;
            profile.Preferences.MajorHealCommand = "cast major heal";
            profile.Preferences.MinorHealCommand = "cast minor heal";
            service.SaveProfile(session, profile);

            var now = DateTimeOffset.Now;
            session.PromptUpdatedAt = now;
            session.ActiveEffectsUpdatedAt = now;
            session.SpellbookUpdatedAt = now;
            session.PartyUpdatedAt = now;
            session.HealthMaximum = 100;
            session.HealthCurrent = 40;
            session.HealthPercent = 40;
            session.ManaMaximum = 100;
            session.ManaCurrent = 80;
            session.ManaPercent = 80;
            session.LearnedSpellDetails.Add(new ClientLearnedSpellState("spell-major-heal", "major heal", 20, 0));
            session.LearnedSpellDetails.Add(new ClientLearnedSpellState("spell-minor-heal", "minor heal", 10, 0));

            var heal = service.Plan(session, profile, now);
            if (heal.Kind != ReforgedRecoveryActionKind.HealSelf || !heal.Command.Contains("major heal", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(77);

            session.HealthCurrent = 100;
            session.HealthPercent = 100;
            profile.Preferences.AutoRest = true;
            profile.Preferences.AutoMeditate = true;
            session.ManaCurrent = 10;
            session.ManaPercent = 10;
            var meditate = service.Plan(session, profile, now);
            if (meditate.Kind != ReforgedRecoveryActionKind.BeginMeditate)
                return Task.FromResult(78);

            session.ManaCurrent = 100;
            session.ManaPercent = 100;
            profile.BuffRules.Clear();
            profile.BuffRules.Add(new ReforgedBuffRule
            {
                Enabled = true,
                Name = "Bless",
                EffectMatch = "bless",
                Command = "cast bless",
                MinimumManaPercent = 10
            });
            session.LearnedSpellDetails.Add(new ClientLearnedSpellState("spell-bless", "bless", 5, 0));
            var buff = service.Plan(session, profile, now);
            if (buff.Kind != ReforgedRecoveryActionKind.MaintainBuff)
                return Task.FromResult(79);

            return Task.FromResult(0);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static ClientSessionConnection BuildSession() => new()
    {
        SessionId = "recovery-smoke",
        DisplayName = "Smoke",
        CharacterName = "Smoke",
        ServerProfileName = "Local Host",
        WorldName = "Classic",
        HostName = "127.0.0.1",
        Port = 4000,
        ConnectionState = ClientConnectionState.Connected
    };
}
