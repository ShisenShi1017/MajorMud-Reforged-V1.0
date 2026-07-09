using System.Text.Json.Serialization;

namespace MajorMud.Reforged.Compiler;

public sealed record OverlayDefinition(
    [property: JsonPropertyName("overlay_id")] string OverlayId,
    [property: JsonPropertyName("world_id")] string WorldId,
    [property: JsonPropertyName("world_type")] string WorldType,
    [property: JsonPropertyName("base_world")] string? BaseWorld,
    [property: JsonPropertyName("locked")] bool Locked,
    [property: JsonPropertyName("precedence")] int Precedence);
