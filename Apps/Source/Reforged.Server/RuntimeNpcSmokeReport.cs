using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeNpcSmokeReport
{
    public IReadOnlyList<RuntimeNpcSmokeWorld> Worlds { get; init; } = Array.Empty<RuntimeNpcSmokeWorld>();
    public bool Passed => Worlds.Count > 0 && Worlds.All(world => world.Passed);
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class RuntimeNpcSmokeWorld
{
    public string Id { get; init; } = string.Empty;
    public int NpcDefinitionCount { get; init; }
    public int SpawnCount { get; init; }
    public int ResolvedSpawnCount { get; init; }
    public int UnresolvedSpawnCount { get; init; }
    public int SpawnRoomCount { get; init; }
    public int MissingRoomCount { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    public bool Passed => NpcDefinitionCount > 0 && SpawnCount > 0 && MissingRoomCount == 0;
}
