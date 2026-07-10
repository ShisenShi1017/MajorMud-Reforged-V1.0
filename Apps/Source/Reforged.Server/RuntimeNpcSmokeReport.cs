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
    public int GroupSpawnCount { get; init; }
    public int BossSpawnCount { get; init; }
    public int ResolvedSpawnCount { get; init; }
    public int UnresolvedSpawnCount { get; init; }
    public int UnresolvedGroupCount { get; init; }
    public int UnresolvedBossCount { get; init; }
    public int SpawnRoomCount { get; init; }
    public int AvailablePrimaryRoomCount { get; init; }
    public int UnavailablePrimaryRoomCount { get; init; }
    public int MissingMapIdentityCount { get; init; }
    public int LinkedPopulationRecordCount { get; init; }
    public int MapQualifiedLinkedPopulationCount { get; init; }
    public int MissingLinkedMapContextCount { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    public bool Passed => NpcDefinitionCount > 0 && SpawnCount > 0 && MissingMapIdentityCount == 0 && MissingLinkedMapContextCount == 0 && UnresolvedGroupCount == 0 && UnresolvedBossCount == 0;
}
