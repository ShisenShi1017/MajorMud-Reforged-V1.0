using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using RealmGate.App.Commands;
using RealmGate.App.Models;
using RealmGate.App.Services;

namespace RealmGate.App.ViewModels;

public sealed class ServerViewModel : ViewModelBase
{
    private readonly IReforgedHostController _host;
    private string _status = "Stopped";
    private string _hostState = "Stopped";

    public ServerViewModel(IReforgedHostController host)
    {
        _host = host;
        RefreshCommand = new RelayCommand(Refresh);
        _host.StateChanged += HostStateChanged;
        Refresh();
    }

    public ObservableCollection<HostSessionSnapshot> LoggedInCharacters { get; } = [];
    public ICommand RefreshCommand { get; }
    public string HostState { get => _hostState; private set => Set(ref _hostState, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public int LoggedInCount => LoggedInCharacters.Count;

    public void Refresh()
    {
        HostState = _host.State.ToString();
        Status = _host.StatusMessage;

        var sessions = _host.ActiveSessions;
        LoggedInCharacters.Clear();
        foreach (var session in sessions)
            LoggedInCharacters.Add(session);

        Raise(nameof(LoggedInCount));
    }

    private void HostStateChanged(object? sender, EventArgs eventArgs)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke((Action)Refresh);
            return;
        }

        Refresh();
    }
}
