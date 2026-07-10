using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeRoomExitSmokeReport
{
    public IReadOnlyList<RuntimeRoomExitSmokeWorld> Worlds { get; init; } = Array.Empty<RuntimeRoomExitSmokeWorld>();
    public int WorldCount => Worlds.Count;
    public bool Passed => WorldCount > 0 && Worlds.All(world => world.Passed);

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }
}

public sealed class RuntimeRoomExitSmokeWorld
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string StartRoomId { get; init; } = string.Empty;
    public string StartRoomName { get; init; } = string.Empty;
    public int StartRoomNumber { get; init; }
    public int StartRoomExitCount { get; init; }
    public IReadOnlyList<RuntimeRoomExitMovementCheck> MovementChecks { get; init; } = Array.Empty<RuntimeRoomExitMovementCheck>();
    public bool Passed => !string.IsNullOrWhiteSpace(StartRoomId)
        && !string.IsNullOrWhiteSpace(StartRoomName)
        && !string.Equals(StartRoomName, "Unknown Room", StringComparison.OrdinalIgnoreCase)
        && StartRoomExitCount > 0
        && MovementChecks.Count > 0
        && MovementChecks.All(check => check.Resolved);
}

public sealed class RuntimeRoomExitMovementCheck
{
    public string Direction { get; init; } = string.Empty;
    public int TargetRoomNumber { get; init; }
    public string TargetRoomId { get; init; } = string.Empty;
    public string TargetRoomName { get; init; } = string.Empty;
    public bool Resolved { get; init; }
}
