using System.Windows.Input; using Reforged.App.Commands; using Reforged.App.Models; using Reforged.App.Services;
namespace Reforged.App.ViewModels;
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IReforgedHostController _host; private readonly IHostCleanupScheduler _cleanup; private readonly IWorkspaceManager _workspaces; private readonly IApplicationSettingsService _settings;
    public string HostStatus=>_host.StatusMessage; public string CleanupStatus=>_cleanup.StatusMessage; public string NextCleanup=>_cleanup.NextCleanupAt?.ToString("ddd MMM d, h:mm tt")??"Not scheduled";
    public ICommand StartLocalHostCommand{get;} public ICommand OpenWorkspaceCommand{get;} public ICommand RunCleanupNowCommand{get;}
    public DashboardViewModel(IReforgedHostController host,IHostCleanupScheduler cleanup,IWorkspaceManager workspaces,IApplicationSettingsService settings){_host=host;_cleanup=cleanup;_workspaces=workspaces;_settings=settings;StartLocalHostCommand=new AsyncRelayCommand(()=>_host.StartAsync(new HostLaunchOptions(AppContext.BaseDirectory,WorldId:_settings.Current.DefaultWorld),CancellationToken.None));OpenWorkspaceCommand=new ParameterCommand(async p=>await _workspaces.ActivateAsync(p?.ToString()??"home"));RunCleanupNowCommand=new AsyncRelayCommand(()=>_cleanup.RunCleanupNowAsync(CancellationToken.None));_host.StateChanged+=(_,_)=>Raise(nameof(HostStatus));_cleanup.StateChanged+=(_,_)=>{Raise(nameof(CleanupStatus));Raise(nameof(NextCleanup));};}
}
