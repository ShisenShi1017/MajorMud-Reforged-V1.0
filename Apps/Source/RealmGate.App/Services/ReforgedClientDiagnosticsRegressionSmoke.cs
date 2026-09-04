using RealmGate.App.Models;

namespace RealmGate.App.Services;

public static class ReforgedClientDiagnosticsRegressionSmoke
{
    public static int Run()
    {
        var session = new ClientSessionConnection
        {
            CharacterName = "Smoke",
            DisplayName = "Smoke",
            ServerProfileName = "Local Host",
            WorldName = "Classic",
            ConnectionState = ClientConnectionState.Connected,
            ConnectedAt = DateTimeOffset.Now.AddHours(-2),
            CharacterLevel = 10,
            ExperienceCurrent = 1000,
            ExperienceNeeded = 2000,
            ExperienceRemaining = 1000,
            SessionExperienceGained = 500
        };
        session.TerminalEntries.Add(new ClientTerminalEntry(DateTimeOffset.Now.AddSeconds(-2), ClientTerminalChannel.Main, "Town Gates"));
        session.TerminalEntries.Add(new ClientTerminalEntry(DateTimeOffset.Now.AddSeconds(-1), ClientTerminalChannel.Combat, "Goblin hits you."));
        session.TerminalEntries.Add(new ClientTerminalEntry(DateTimeOffset.Now, ClientTerminalChannel.Chat, "hello", true));

        var combat = ReforgedClientDiagnosticsService.FilterScrollback(session, "goblin", ClientTerminalChannel.Combat, includeOutgoing: false);
        if (combat.Count != 1 || !combat[0].Text.Contains("Goblin", StringComparison.Ordinal)) return 71;

        if (!ReforgedClientDiagnosticsService.IsSafeUserCommand("look")) return 72;
        if (ReforgedClientDiagnosticsService.IsSafeUserCommand("clientdata stats")) return 73;
        if (ReforgedClientDiagnosticsService.IsSafeUserCommand("roomdata")) return 74;
        if (ReforgedClientDiagnosticsService.IsSafeUserCommand("look\nclientdata stats")) return 75;

        var redacted = ReforgedClientDiagnosticsService.Redact("token=abc123 Bearer qwerty");
        if (redacted.Contains("abc123", StringComparison.Ordinal) || redacted.Contains("qwerty", StringComparison.Ordinal)) return 76;

        var stats = ReforgedClientDiagnosticsService.RenderSessionStats(session, DateTimeOffset.Now);
        if (!stats.Contains("XP / hour", StringComparison.Ordinal) || !stats.Contains("Next-level ETA", StringComparison.Ordinal)) return 77;

        return 0;
    }
}
