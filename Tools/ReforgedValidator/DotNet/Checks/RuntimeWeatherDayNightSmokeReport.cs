using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeWeatherDayNightSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeWeatherDayNightSmokeReport Run()
    {
        var checks = new List<string>();
        var service = new RuntimeWeatherDayNightService(new[]
        {
            new RuntimeWorldConditionDefinition("classic", RuntimeWorldConditionMode.Disabled, RuntimeDayPhase.Unknown, RuntimeWeatherKind.Clear, false, false, false, false, false, false, false),
            new RuntimeWorldConditionDefinition("custom", RuntimeWorldConditionMode.Simulated, RuntimeDayPhase.Day, RuntimeWeatherKind.Clear, true, true, false, false, true, true, false)
        });
        var classic = service.SetState("classic", RuntimeDayPhase.Night, RuntimeWeatherKind.Fog);
        if (!classic.Allowed && classic.Code == "condition-disabled") checks.Add("classic-disabled-fails-closed");
        var updated = service.SetState("custom", RuntimeDayPhase.Night, RuntimeWeatherKind.Fog);
        if (updated.Allowed && updated.State?.Revision == 1) checks.Add("simulated-state-updated");
        if (updated.Requests.Any(x => x.OwnerSubsystem == "environmental-hazards") && updated.Requests.Any(x => x.OwnerSubsystem == "rooms") && updated.Requests.Any(x => x.OwnerSubsystem == "spells")) checks.Add("typed-owner-routing");
        if (service.ValidateState().Count == 0) checks.Add("state-invariants");
        return new(checks.Count == 4, checks);
    }
}
