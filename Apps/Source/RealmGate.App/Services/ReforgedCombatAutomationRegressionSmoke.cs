using RealmGate.App.Models;

namespace RealmGate.App.Services;

public static class ReforgedCombatAutomationRegressionSmoke
{
    public static Task<int> RunAsync(IApplicationSettingsService settings)
    {
        _ = settings;
        var root = Path.Combine(Path.GetTempPath(), "RealmGate-combat-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ReforgedCombatAutomationService(root);
            var session = BuildSession();
            var profile = service.LoadProfile(session);
            profile.Preferences.Enabled = true;
            profile.Preferences.TargetMode = ReforgedCombatTargetMode.HighestExperience;
            profile.Preferences.AvoidTargetWords = "guard";
            profile.Preferences.DefaultEquipmentSetId = "combat-default";
            profile.Preferences.AutoEscape = true;
            profile.Preferences.EscapeAtHealthPercent = 25;
            profile.Actions.Clear();
            profile.Actions.Add(new ReforgedCombatActionRule
            {
                Id = "spell",
                Enabled = true,
                Priority = 10,
                Name = "Shock",
                Command = "cast shock",
                AppendTarget = true,
                InCombatOnly = true,
                MinimumManaPercent = 20,
                CooldownSeconds = 3
            });
            service.SaveProfile(session, profile);

            var now = DateTimeOffset.Now;
            session.CurrentRoomNpcDetailsUpdatedAt = now;
            session.CurrentRoomNpcDetails.Add(new ClientRoomInspectionNpc(
                "1", "npc-guard", 1, "city guard", 100, 100, true, null, "hostile", "combat", 10, 5000));
            session.CurrentRoomNpcDetails.Add(new ClientRoomInspectionNpc(
                "2", "npc-rat", 2, "dire rat", 80, 100, true, null, "hostile", "combat", 8, 4000));
            session.CurrentRoomNpcDetails.Add(new ClientRoomInspectionNpc(
                "3", "npc-wolf", 3, "black wolf", 90, 100, true, null, "hostile", "combat", 9, 7000));

            var equip = service.Plan(session, profile, now);
            if (equip.Kind != ReforgedCombatActionKind.EquipSet || equip.EquipmentSetId != "combat-default")
                return Task.FromResult(80);
            service.MarkEquipmentSetApplied(session.SessionId, equip.EquipmentSetId);

            var engage = service.Plan(session, profile, now);
            if (engage.Target != "black wolf" || engage.Kind != ReforgedCombatActionKind.Engage)
                return Task.FromResult(81);
            service.MarkExecuted(session, profile, engage, now);

            session.InCombat = true;
            session.HealthPercent = 20;
            var escape = service.Plan(session, profile, now.AddSeconds(1));
            if (escape.Kind != ReforgedCombatActionKind.Escape)
                return Task.FromResult(82);

            service.ClearRuntime(session.SessionId);
            session.HealthPercent = 100;
            session.ManaMaximum = 100;
            session.ManaCurrent = 100;
            session.ManaPercent = 100;
            var noSpell = service.Plan(session, profile, now.AddSeconds(2));
            if (noSpell.Kind == ReforgedCombatActionKind.RoundAction)
                return Task.FromResult(83);

            session.LearnedSpellDetails.Add(new ClientLearnedSpellState("spell-shock", "shock", 10, 0));
            var spell = service.Plan(session, profile, now.AddSeconds(3));
            if (spell.Kind != ReforgedCombatActionKind.RoundAction || !spell.Command.Contains("black wolf", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(84);

            service.MarkExecuted(session, profile, spell, now.AddSeconds(3));
            service.ObserveLine(session, profile, "Your spell has no effect.", now.AddSeconds(4));
            var blocked = service.Plan(session, profile, now.AddSeconds(5));
            if (blocked.Kind == ReforgedCombatActionKind.RoundAction)
                return Task.FromResult(85);

            return Task.FromResult(0);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static ClientSessionConnection BuildSession() => new()
    {
        SessionId = "combat-smoke",
        DisplayName = "Combat Smoke",
        CharacterName = "CombatSmoke",
        ServerProfileName = "Local Host",
        WorldName = "Classic",
        HostName = "127.0.0.1",
        Port = 4000,
        ConnectionState = ClientConnectionState.Connected,
        CurrentRoomKey = "1/100",
        CharacterLevel = 20,
        HealthMaximum = 100,
        HealthCurrent = 100,
        HealthPercent = 100,
        ManaMaximum = 100,
        ManaCurrent = 100,
        ManaPercent = 100
    };
}
