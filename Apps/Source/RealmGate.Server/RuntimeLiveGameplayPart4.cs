namespace RealmGate.Server;

public sealed class RuntimeLiveGameplayPart4SmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveGameplayPart4SmokeReport Run()
    {
        var report = new RuntimeLiveGameplayPart4SmokeReport();
        try
        {
            Require(RuntimeCommand.Parse("give sword to Alice").Name == "give", "give-route");
            report.Checks.Add("give-route");

            Require(RuntimeCommand.Parse("logout").Name == "quit", "logout-alias");
            report.Checks.Add("logout-alias");

            Require(RuntimeCommand.SupportedCommands.Contains("ready"), "integration-readiness-command");
            report.Checks.Add("integration-readiness-command");

            Require(RuntimeCommand.SupportedCommands.Contains("respawn") && RuntimeCommand.SupportedCommands.Contains("recover"), "death-loop-routes");
            report.Checks.Add("death-loop-routes");

            Require(typeof(RuntimeSessionManager).GetMethod(nameof(RuntimeSessionManager.FindUniqueInRoom)) is not null, "live-target-resolution");
            report.Checks.Add("live-target-resolution");

            Require(typeof(RuntimeCharacterService).GetMethod(nameof(RuntimeCharacterService.SaveSession)) is not null, "disconnect-save");
            report.Checks.Add("disconnect-save");

            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.RenderIntegrationStatus)) is not null, "integration-status");
            report.Checks.Add("integration-status");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }
        return report;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
