using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reforged.App.Models;

public sealed class ClientConnectionProfile : INotifyPropertyChanged
{
    private string _profileName = "New Character";
    private string _sessionDisplayName = "Session";
    private string _characterName = string.Empty;
    private string _serverProfileName = "New BBS";
    private string _worldName = "Classic";
    private string _hostName = "127.0.0.1";
    private int _port = 4000;
    private bool _autoReconnect = true;
    private int _reconnectDelaySeconds = 5;
    private ClientTerminalViewMode _terminalViewMode = ClientTerminalViewMode.Reforged;
    private int _terminalColumns = 80;
    private int _terminalRows = 25;
    private bool _enableTelnetNegotiation = true;
    private string _terminalType = "ANSI";
    private bool _useCodePage437 = true;
    private bool _captureTelnetTraffic;
    private bool _stripAnsiForParsing = true;
    private bool _showAnsi = true;
    private bool _showTimestamps;
    private bool _loggingEnabled;
    private int _maximumScrollbackLines = 10000;
    private bool _separateChannels = true;
    private string _notes = string.Empty;

    public string ProfileId { get; set; } = Guid.NewGuid().ToString("N");
    public string ProfileName { get => _profileName; set => SetSummary(ref _profileName, Clean(value, "New Character")); }
    public string SessionDisplayName { get => _sessionDisplayName; set => SetSummary(ref _sessionDisplayName, Clean(value, "Session")); }
    public string CharacterName { get => _characterName; set => SetSummary(ref _characterName, value?.Trim() ?? string.Empty); }
    public string ServerProfileName { get => _serverProfileName; set => SetSummary(ref _serverProfileName, Clean(value, "New BBS")); }
    public string WorldName { get => _worldName; set => SetSummary(ref _worldName, Clean(value, "Classic")); }
    public string HostName { get => _hostName; set => SetSummary(ref _hostName, Clean(value, "127.0.0.1")); }
    public int Port { get => _port; set => SetSummary(ref _port, Math.Clamp(value, 1, 65535)); }
    public bool AutoReconnect { get => _autoReconnect; set => Set(ref _autoReconnect, value); }
    public int ReconnectDelaySeconds { get => _reconnectDelaySeconds; set => Set(ref _reconnectDelaySeconds, Math.Clamp(value, 1, 120)); }
    public ClientTerminalViewMode TerminalViewMode { get => _terminalViewMode; set => Set(ref _terminalViewMode, value); }
    public int TerminalColumns { get => _terminalColumns; set => Set(ref _terminalColumns, Math.Clamp(value, 20, ushort.MaxValue)); }
    public int TerminalRows { get => _terminalRows; set => Set(ref _terminalRows, Math.Clamp(value, 5, ushort.MaxValue)); }
    public bool EnableTelnetNegotiation { get => _enableTelnetNegotiation; set => Set(ref _enableTelnetNegotiation, value); }
    public string TerminalType { get => _terminalType; set => Set(ref _terminalType, Clean(value, "ANSI")); }
    public bool UseCodePage437 { get => _useCodePage437; set => Set(ref _useCodePage437, value); }
    public bool CaptureTelnetTraffic { get => _captureTelnetTraffic; set => Set(ref _captureTelnetTraffic, value); }
    public bool StripAnsiForParsing { get => _stripAnsiForParsing; set => Set(ref _stripAnsiForParsing, value); }
    public bool ShowAnsi { get => _showAnsi; set => Set(ref _showAnsi, value); }
    public bool ShowTimestamps { get => _showTimestamps; set => Set(ref _showTimestamps, value); }
    public bool LoggingEnabled { get => _loggingEnabled; set => Set(ref _loggingEnabled, value); }
    public int MaximumScrollbackLines { get => _maximumScrollbackLines; set => Set(ref _maximumScrollbackLines, Math.Clamp(value, 500, 100000)); }
    public bool SeparateChannels { get => _separateChannels; set => Set(ref _separateChannels, value); }
    public string Notes { get => _notes; set => Set(ref _notes, value ?? string.Empty); }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public string DisplaySummary
    {
        get
        {
            var character = string.IsNullOrWhiteSpace(CharacterName) ? "unnamed character" : CharacterName;
            return $"{ProfileName} — {character} @ {ServerProfileName} / {WorldName}";
        }
    }

    public string EndpointSummary => $"{HostName}:{Port}";

    public ClientConnectionProfile Clone(string profileName)
    {
        return new ClientConnectionProfile
        {
            ProfileId = Guid.NewGuid().ToString("N"),
            ProfileName = profileName,
            SessionDisplayName = SessionDisplayName,
            CharacterName = CharacterName,
            ServerProfileName = ServerProfileName,
            WorldName = WorldName,
            HostName = HostName,
            Port = Port,
            AutoReconnect = AutoReconnect,
            ReconnectDelaySeconds = ReconnectDelaySeconds,
            TerminalViewMode = TerminalViewMode,
            TerminalColumns = TerminalColumns,
            TerminalRows = TerminalRows,
            EnableTelnetNegotiation = EnableTelnetNegotiation,
            TerminalType = TerminalType,
            UseCodePage437 = UseCodePage437,
            CaptureTelnetTraffic = CaptureTelnetTraffic,
            StripAnsiForParsing = StripAnsiForParsing,
            ShowAnsi = ShowAnsi,
            ShowTimestamps = ShowTimestamps,
            LoggingEnabled = LoggingEnabled,
            MaximumScrollbackLines = MaximumScrollbackLines,
            SeparateChannels = SeparateChannels,
            Notes = Notes,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string Clean(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private bool SetSummary<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!Set(ref field, value, name))
            return false;

        Raise(nameof(DisplaySummary));
        Raise(nameof(EndpointSummary));
        return true;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(name);
        return true;
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ClientSessionProfileAssignment
{
    public string SessionId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class ClientConnectionProfileCatalog
{
    public int Version { get; set; } = 1;
    public ObservableCollection<ClientConnectionProfile> Profiles { get; set; } = [];
    public ObservableCollection<ClientSessionProfileAssignment> SessionAssignments { get; set; } = [];
}
