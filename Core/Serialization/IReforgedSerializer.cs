namespace MajorMud.Reforged.Core.Serialization;

/// <summary>
/// Shared serializer contract for Reforged data files, manifests, reports, and future network-safe DTOs.
/// </summary>
public interface IReforgedSerializer
{
    string FormatName { get; }
    string Serialize<T>(T value);
    T? Deserialize<T>(string content);
}
