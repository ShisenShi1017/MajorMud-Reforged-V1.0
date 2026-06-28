namespace Reforged.Core.Runtime;

public sealed class RuntimeState
{
    public bool IsLoaded { get; set; }
    public bool IsAcceptingPlayers { get; set; }
    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ActiveDatabaseVersion { get; set; } = string.Empty;
    public string ActiveCodeUpdate { get; set; } = string.Empty;
}
