using RealmGate.App.Models;

namespace RealmGate.App.Services;

public static class ReforgedParityCompletionRegressionSmoke
{
    public static async Task<int> RunAsync()
    {
        var session = new ClientSessionConnection
        {
            DisplayName = "Parity Smoke",
            CharacterName = "Smoke",
            ServerProfileName = "Local Host",
            WorldName = "Classic",
            CharacterLevel = 10,
            HealthCurrent = 90,
            HealthMaximum = 100,
            ExperienceCurrent = 1234,
            ExperienceNeeded = 2000,
            ConnectionState = ClientConnectionState.Connected
        };
        session.TerminalEntries.Add(new ClientTerminalEntry(DateTimeOffset.UtcNow, ClientTerminalChannel.Chat, "Alice gossips, 'hello'"));
        session.TerminalEntries.Add(new ClientTerminalEntry(DateTimeOffset.UtcNow, ClientTerminalChannel.Tell, "Bob tells you 'test'"));
        session.TerminalEntries.Add(new ClientTerminalEntry(DateTimeOffset.UtcNow, ClientTerminalChannel.System, "password=should-not-survive"));
        session.ProgramLogEntries.Add(new ReforgedClientProgramLogEntry(DateTimeOffset.UtcNow, "Smoke", "api_key=secret"));

        var rows = ReforgedParityCompletionService.FilterConversation(session, "hello", "Gossip", true, true);
        if (rows.Count != 1 || rows[0].Channel != "Gossip") return 81;

        var path = Path.Combine(Path.GetTempPath(), $"reforged-parity-smoke-{Guid.NewGuid():N}.md");
        var historyPath = Path.Combine(Path.GetTempPath(), $"reforged-parity-history-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllLinesAsync(historyPath,
            [
                $"[{DateTimeOffset.UtcNow:O}] [Chat] Alice gossips, 'recorded'",
                $"[{DateTimeOffset.UtcNow:O}] [Combat] Goblin hits you."
            ]);
            var history = await ReforgedParityCompletionService.ReadConversationHistoryAsync(historyPath);
            if (history.Count != 1 || history[0].Channel != "Gossip" || history[0].Direction != "Recorded") return 82;

            await ReforgedParityCompletionService.WriteBugReportAsync(
                session,
                new ClientAutomationOptions(),
                new ReforgedBugReportRequest
                {
                    Area = "Smoke",
                    Summary = "Parity completion regression",
                    WhatHappened = "Synthetic smoke state",
                    IncludeRecentScrollback = true
                },
                path);
            var text = await File.ReadAllTextAsync(path);
            if (!text.Contains("RealmGate Problem Report", StringComparison.Ordinal)) return 83;
            if (text.Contains("should-not-survive", StringComparison.Ordinal) || text.Contains("api_key=secret", StringComparison.Ordinal)) return 84;
            if (!text.Contains("[REDACTED]", StringComparison.Ordinal)) return 85;
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(historyPath); } catch { }
        }

        return 0;
    }
}
