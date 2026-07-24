using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reforged.App.Models;

public enum ClientMapPolicy { DiscoveryOnly, FullMapWhenHostPermits }
public enum ClientMapStyle { Classic, Modern, Compact, HighContrast }

public sealed class ClientMapProfile : INotifyPropertyChanged
{
    private string _serverName = "Local Host";
    private string _worldName = "Classic";
    private ClientMapPolicy _policy = ClientMapPolicy.DiscoveryOnly;
    private bool _hostPermitsFullMap;
    private bool _showSpawnTimers = true;
    private string _activeLayer = "Surface";
    private double _zoom = 1.0;
    private ClientMapStyle _style = ClientMapStyle.Classic;
    private bool _showRoomLabels = true;
    private bool _showRoomKeys;
    private bool _animateRouteReplay = true;
    private int _replayStepDelayMilliseconds = 450;

    public string ServerName { get => _serverName; set => Set(ref _serverName, value); }
    public string WorldName { get => _worldName; set => Set(ref _worldName, value); }
    public ClientMapPolicy Policy { get => _policy; set => Set(ref _policy, value); }
    public bool HostPermitsFullMap { get => _hostPermitsFullMap; set => Set(ref _hostPermitsFullMap, value); }
    public bool ShowSpawnTimers { get => _showSpawnTimers; set => Set(ref _showSpawnTimers, value); }
    public string ActiveLayer { get => _activeLayer; set => Set(ref _activeLayer, value); }
    public double Zoom { get => _zoom; set => Set(ref _zoom, Math.Clamp(value, 0.25, 4.0)); }
    public ClientMapStyle Style { get => _style; set => Set(ref _style, value); }
    public bool ShowRoomLabels { get => _showRoomLabels; set => Set(ref _showRoomLabels, value); }
    public bool ShowRoomKeys { get => _showRoomKeys; set => Set(ref _showRoomKeys, value); }
    public bool AnimateRouteReplay { get => _animateRouteReplay; set => Set(ref _animateRouteReplay, value); }
    public int ReplayStepDelayMilliseconds { get => _replayStepDelayMilliseconds; set => Set(ref _replayStepDelayMilliseconds, Math.Clamp(value, 100, 5000)); }
    public ObservableCollection<string> Layers { get; set; } = ["Surface", "Upper", "Lower", "Underground", "Special"];
    public ObservableCollection<ClientRoomMetadata> Rooms { get; set; } = [];
    public string EffectivePolicy => Policy == ClientMapPolicy.FullMapWhenHostPermits && HostPermitsFullMap ? "Full map (Host permitted)" : "Discovery only";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); Raise(nameof(EffectivePolicy)); return true;
    }
}

public sealed class ClientRoomMetadata : INotifyPropertyChanged
{
    private string _roomKey = string.Empty;
    private string _roomName = "Unknown Room";
    private string _layer = "Surface";
    private bool _discovered = true;
    private bool _favorite;
    private string _notes = string.Empty;
    private DateTimeOffset? _lastVisited;
    private DateTimeOffset? _spawnReadyAt;
    private string _spawnLabel = string.Empty;
    private string _knownNpcs = string.Empty;
    private string _knownMonsters = string.Empty;
    private string _knownShops = string.Empty;

    public string RoomKey { get => _roomKey; set => Set(ref _roomKey, value); }
    public string RoomName { get => _roomName; set => Set(ref _roomName, value); }
    public string Layer { get => _layer; set => Set(ref _layer, value); }
    public bool Discovered { get => _discovered; set => Set(ref _discovered, value); }
    public bool Favorite { get => _favorite; set => Set(ref _favorite, value); }
    public string Notes { get => _notes; set => Set(ref _notes, value); }
    public DateTimeOffset? LastVisited { get => _lastVisited; set => Set(ref _lastVisited, value); }
    public DateTimeOffset? SpawnReadyAt { get => _spawnReadyAt; set { if (Set(ref _spawnReadyAt, value)) Raise(nameof(SpawnSummary)); } }
    public string SpawnLabel { get => _spawnLabel; set { if (Set(ref _spawnLabel, value)) Raise(nameof(SpawnSummary)); } }
    public string KnownNpcs { get => _knownNpcs; set => Set(ref _knownNpcs, value); }
    public string KnownMonsters { get => _knownMonsters; set => Set(ref _knownMonsters, value); }
    public string KnownShops { get => _knownShops; set => Set(ref _knownShops, value); }
    public string DisplayLabel => string.IsNullOrWhiteSpace(RoomKey) ? RoomName : $"{RoomName} [{RoomKey}]";
    public string SpawnSummary => SpawnReadyAt is null ? "No Client timer" : SpawnReadyAt <= DateTimeOffset.Now ? $"{(string.IsNullOrWhiteSpace(SpawnLabel) ? "Spawn" : SpawnLabel)}: READY" : $"{(string.IsNullOrWhiteSpace(SpawnLabel) ? "Spawn" : SpawnLabel)}: {SpawnReadyAt.Value.LocalDateTime:g}";
    public string RoomDetailSummary => string.Join(" • ", new[] { KnownNpcs, KnownMonsters, KnownShops }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); Raise(nameof(DisplayLabel)); return true;
    }
}
