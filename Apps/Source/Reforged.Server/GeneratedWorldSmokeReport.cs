using System.Text.Json;

namespace Reforged.Server;

public sealed class GeneratedWorldSmokeReport
{
    public int WorldCount { get; init; }
    public IReadOnlyList<GeneratedWorldSmokeWorld> Worlds { get; init; } = Array.Empty<GeneratedWorldSmokeWorld>();
    public bool Passed => WorldCount > 0 && Worlds.All(world => world.Diagnostics.Count == 0 && world.RoomCount > 0);

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }
}

public sealed class GeneratedWorldSmokeWorld
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int RoomCount { get; init; }
    public string DataRoot { get; init; } = string.Empty;
    public int FamilyCount { get; init; }
    public int TotalGeneratedRecords { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}
