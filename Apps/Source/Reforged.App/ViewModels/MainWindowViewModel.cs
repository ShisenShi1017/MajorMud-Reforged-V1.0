using System.Windows;
using System.Windows.Input;
using Reforged.App.Commands;
using Reforged.App.Services;

namespace Reforged.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IWorkspaceManager _workspaces;
    private readonly IReforgedHostController _host;
    private object? _activeView;
    private string _activeTitle = "Home";

    public object? ActiveView { get => _activeView; private set => Set(ref _activeView, value); }
    public string ActiveTitle { get => _activeTitle; private set => Set(ref _activeTitle, value); }
    public string HostState => $"Host: {_host.StatusMessage}";
    public string ConnectionState => _host.State == Models.HostControllerState.Running ? "Connection: Local" : "Connection: Offline";
    public string SessionState => $"Sessions: {_host.ConnectedSessionCount}";

    // The editor is a creator workspace, not a Borin dashboard. Give it the entire vertical
    // content area and keep only a narrow application navigation rail for leaving the editor.
    public bool IsEditorActive => string.Equals(ActiveTitle, "Editor", StringComparison.OrdinalIgnoreCase);
    public GridLength HeaderHeight => IsEditorActive ? new GridLength(0) : new GridLength(66);
    public GridLength FooterHeight => IsEditorActive ? new GridLength(0) : new GridLength(34);
    public GridLength PageTitleHeight => IsEditorActive ? new GridLength(0) : new GridLength(54);
    public GridLength NavigationWidth => IsEditorActive ? new GridLength(118) : new GridLength(220);

    public ICommand NavigateCommand { get; }
    public ICommand ExitCommand { get; }

    public MainWindowViewModel(IWorkspaceManager workspaces, IReforgedHostController host)
    {
        _workspaces = workspaces;
        _host = host;
        NavigateCommand = new ParameterCommand(async p => await _workspaces.ActivateAsync(p?.ToString() ?? "home"));
        ExitCommand = new RelayCommand(() => Application.Current.MainWindow?.Close());
        _workspaces.ActiveWorkspaceChanged += (_, _) => Refresh();
        _host.StateChanged += (_, _) =>
        {
            Raise(nameof(HostState));
            Raise(nameof(ConnectionState));
            Raise(nameof(SessionState));
        };
    }

    private void Refresh()
    {
        ActiveView = _workspaces.ActiveWorkspace?.View;
        ActiveTitle = _workspaces.ActiveWorkspace?.Title ?? "Home";
        Raise(nameof(IsEditorActive));
        Raise(nameof(HeaderHeight));
        Raise(nameof(FooterHeight));
        Raise(nameof(PageTitleHeight));
        Raise(nameof(NavigationWidth));
    }
}
