namespace Reforged.App.Models;

public enum HostControllerState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Failed,
}

public enum CleanupFrequency
{
    Daily,
    SelectedDays,
}

public enum HostCleanupSchedulerState
{
    Disabled,
    Waiting,
    Warning,
    Running,
    Failed,
}

public sealed record HostLaunchOptions(
    string ProjectRoot,
    string BindAddress = "127.0.0.1",
    int Port = 4000,
    int MaximumSessions = 64,
    string? WorldId = null);

public sealed class HostCleanupSchedule
{
    public bool Enabled { get; set; }

    public string CleanupTime { get; set; } = "04:00";

    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

    public CleanupFrequency Frequency { get; set; } = CleanupFrequency.Daily;

    public DayOfWeek[] Days { get; set; } = Enum.GetValues<DayOfWeek>();

    public int[] WarningMinutes { get; set; } = [30, 15, 5, 1];

    public bool SaveBeforeCleanup { get; set; } = true;

    public bool RestartHostAfterCleanup { get; set; } = true;

    public string DisconnectMessage { get; set; } =
        "The server is performing scheduled cleanup.";
}

public sealed class ApplicationSettings
{
    public string Theme { get; set; } = "Dark";

    public double FontSize { get; set; } = 14;

    public string DefaultWorld { get; set; } = "classic";

    public string DefaultConnectionProfile { get; set; } = "Local";

    public bool AutoStartLocalHost { get; set; }

    public bool RestorePreviousLayout { get; set; } = true;

    public bool RestorePreviousSessions { get; set; } = true;

    public string LogLevel { get; set; } = "Information";

    public string DataDirectory { get; set; } = "Data";

    public string SavesDirectory { get; set; } = "Saves";

    public HostCleanupSchedule HostCleanup { get; set; } = new();
}

public sealed class LayoutState
{
    public string ActiveWorkspaceId { get; set; } = "home";

    public double Width { get; set; } = 1440;

    public double Height { get; set; } = 900;

    public double? Left { get; set; }

    public double? Top { get; set; }
}

public sealed record HostSessionSnapshot(
    string SessionId,
    string CharacterId,
    string CharacterName,
    string AccountId,
    string WorldId,
    string RoomId,
    string RoomName,
    DateTimeOffset ConnectedAtUtc,
    string Access);
