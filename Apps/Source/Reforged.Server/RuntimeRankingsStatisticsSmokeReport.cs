using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeRankingsStatisticsSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeRankingsStatisticsSmokeReport Run()
    {
        var checks = new List<string>();
        var service = new RuntimeRankingsStatisticsService(new[]
        {
            new RuntimeRankingDefinition("level", RuntimeRankingMetric.Level, "Character", RuntimeRankingVisibility.Public, RuntimeRankingResetPolicy.Never),
            new RuntimeRankingDefinition("pvp", RuntimeRankingMetric.PvPKills, "PvpPolicy", RuntimeRankingVisibility.ParticipantsOnly, RuntimeRankingResetPolicy.Monthly),
            new RuntimeRankingDefinition("wealth-admin", RuntimeRankingMetric.Wealth, "Economy", RuntimeRankingVisibility.AdministratorOnly, RuntimeRankingResetPolicy.Manual)
        });
        var alpha = new RuntimeRankingSubject("character:alpha", "Alpha");
        var beta = new RuntimeRankingSubject("character:beta", "Beta");
        var hidden = new RuntimeRankingSubject("character:hidden", "Hidden", Hidden: true);
        checks.Add(service.SetValue("Character", "level", alpha, 50).Success ? "set-owner-value" : "FAIL:set-owner-value");
        checks.Add(service.SetValue("Character", "level", beta, 50).Success ? "set-tied-value" : "FAIL:set-tied-value");
        checks.Add(!service.SetValue("Economy", "level", alpha, 99).Success ? "reject-owner-mismatch" : "FAIL:reject-owner-mismatch");
        service.SetValue("Character", "level", hidden, 60);
        var playerView = service.Query(new RuntimeRankingActor("character:alpha", true, false), "level");
        checks.Add(playerView.Success && playerView.Snapshot!.Entries.Count == 2 ? "hide-private-subject" : "FAIL:hide-private-subject");
        checks.Add(playerView.Snapshot!.Entries.All(e => e.Rank == 1 && e.Tied) ? "deterministic-ties" : "FAIL:deterministic-ties");
        var participantDenied = service.Query(new RuntimeRankingActor("character:alpha", true, false), "pvp");
        checks.Add(!participantDenied.Success ? "participant-visibility-denied" : "FAIL:participant-visibility-denied");
        var participantAllowed = service.Query(new RuntimeRankingActor("character:alpha", true, false, new[] { "pvp" }), "pvp");
        checks.Add(participantAllowed.Success ? "participant-visibility-allowed" : "FAIL:participant-visibility-allowed");
        checks.Add(!service.Reset(new RuntimeRankingActor("character:alpha", true, false), "pvp", DateTimeOffset.UtcNow).Success ? "reset-admin-required" : "FAIL:reset-admin-required");
        checks.Add(service.Reset(new RuntimeRankingActor("host:1", true, true), "pvp", DateTimeOffset.UtcNow).Success ? "authorized-reset" : "FAIL:authorized-reset");
        checks.Add(service.ValidateState().Count == 0 ? "state-valid" : "FAIL:state-valid");
        return new(checks.All(c => !c.StartsWith("FAIL:", StringComparison.Ordinal)), checks);
    }
}
