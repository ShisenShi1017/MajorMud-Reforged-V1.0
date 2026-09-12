using RealmGate.App.Models;
namespace RealmGate.App.Services;
public interface IWorkspace { string Id { get; } string Title { get; } object View { get; } bool CanClose { get; } Task ActivateAsync(CancellationToken token); Task DeactivateAsync(CancellationToken token); }
public interface IWorkspaceManager { IReadOnlyCollection<IWorkspace> Workspaces { get; } IWorkspace? ActiveWorkspace { get; } event EventHandler? ActiveWorkspaceChanged; void Register(IWorkspace workspace); Task ActivateAsync(string id, CancellationToken token=default); Task CloseAsync(string id, CancellationToken token=default); }
public interface IApplicationSettingsService { ApplicationSettings Current { get; } Task LoadAsync(); Task SaveAsync(); string RootPath { get; } }
public interface ILayoutPersistenceService { Task<LayoutState> LoadAsync(); Task SaveAsync(LayoutState state); }
public interface IReforgedHostController { HostControllerState State { get; } string StatusMessage { get; } int ConnectedSessionCount { get; } IReadOnlyList<HostSessionSnapshot> ActiveSessions { get; } event EventHandler? StateChanged; Task StartAsync(HostLaunchOptions options, CancellationToken cancellationToken); Task StopAsync(CancellationToken cancellationToken); Task RestartAsync(HostLaunchOptions options, CancellationToken cancellationToken); }
public interface IHostCleanupScheduler : IAsyncDisposable
{
    HostCleanupSchedule Schedule { get; }
    DateTimeOffset? NextCleanupAt { get; }
    HostCleanupSchedulerState State { get; }
    string StatusMessage { get; }
    event EventHandler? StateChanged;
    Task UpdateScheduleAsync(HostCleanupSchedule schedule, CancellationToken token);
    Task RunCleanupNowAsync(CancellationToken token);
    Task PostponeAsync(TimeSpan duration, CancellationToken token);
    Task CancelPendingCleanupAsync(CancellationToken token);
    void UpdateLaunchOptions(HostLaunchOptions options);
}
public interface IErrorReporter { void Report(Exception exception, string context); }
public interface IAppLogger { event EventHandler? Changed; void Info(string message); void Error(string message, Exception? exception=null); IReadOnlyList<string> Recent { get; } }
