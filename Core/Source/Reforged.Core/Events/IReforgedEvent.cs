namespace Reforged.Core.Events;

/// <summary>
/// Marker contract for dependency-free Reforged domain and lifecycle events.
/// </summary>
public interface IReforgedEvent
{
    DateTimeOffset TimestampUtc { get; }
}
