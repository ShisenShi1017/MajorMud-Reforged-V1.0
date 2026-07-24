using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reforged.App.Models;

public sealed class ClientSysGotoProfile : INotifyPropertyChanged
{
    private string _serverName = "Local Host";
    private string _worldName = "Classic";
    private bool _enabled;
    private bool _confirmBeforeTravel = true;
    private bool _fallbackToRoute = true;
    private string _commandPrefix = "sys goto";

    public string ServerName { get => _serverName; set => Set(ref _serverName, value); }
    public string WorldName { get => _worldName; set => Set(ref _worldName, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool ConfirmBeforeTravel { get => _confirmBeforeTravel; set => Set(ref _confirmBeforeTravel, value); }
    public bool FallbackToRoute { get => _fallbackToRoute; set => Set(ref _fallbackToRoute, value); }
    public string CommandPrefix { get => _commandPrefix; set => Set(ref _commandPrefix, value); }
    public ObservableCollection<ClientSysGotoDestination> Destinations { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); return true;
    }
}

public sealed class ClientSysGotoDestination : INotifyPropertyChanged
{
    private string _displayName = "New City";
    private string _commandArgument = string.Empty;
    private string _aliases = string.Empty;
    private bool _enabled = true;

    public string DisplayName { get => _displayName; set => Set(ref _displayName, value); }
    public string CommandArgument { get => _commandArgument; set => Set(ref _commandArgument, value); }
    public string Aliases { get => _aliases; set => Set(ref _aliases, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string CommandPreview => string.IsNullOrWhiteSpace(CommandArgument) ? "Destination argument required" : CommandArgument;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); Raise(nameof(CommandPreview)); return true;
    }
}
