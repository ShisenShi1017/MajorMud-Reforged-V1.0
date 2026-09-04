using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RealmGate.App.Models;

public enum ClientTerminalChannel { Main, Combat, Chat, Tell, Party, System }

public sealed class ClientTerminalPreferences : INotifyPropertyChanged
{
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

    public bool EnableTelnetNegotiation { get => _enableTelnetNegotiation; set => Set(ref _enableTelnetNegotiation, value); }
    public string TerminalType
    {
        get => _terminalType;
        set => Set(ref _terminalType, string.IsNullOrWhiteSpace(value) ? "ANSI" : value.Trim());
    }
    public bool UseCodePage437 { get => _useCodePage437; set => Set(ref _useCodePage437, value); }
    public bool CaptureTelnetTraffic { get => _captureTelnetTraffic; set => Set(ref _captureTelnetTraffic, value); }
    public bool StripAnsiForParsing { get => _stripAnsiForParsing; set => Set(ref _stripAnsiForParsing, value); }
    public bool ShowAnsi { get => _showAnsi; set => Set(ref _showAnsi, value); }
    public bool ShowTimestamps { get => _showTimestamps; set => Set(ref _showTimestamps, value); }
    public bool LoggingEnabled { get => _loggingEnabled; set => Set(ref _loggingEnabled, value); }
    public int MaximumScrollbackLines { get => _maximumScrollbackLines; set => Set(ref _maximumScrollbackLines, Math.Clamp(value, 500, 100000)); }
    public bool SeparateChannels { get => _separateChannels; set => Set(ref _separateChannels, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed record ClientTerminalEntry(DateTimeOffset Timestamp, ClientTerminalChannel Channel, string Text, bool IsOutgoing = false)
{
    public string DisplayText(bool timestamps) => timestamps ? $"[{Timestamp:HH:mm:ss}] {Text}" : Text;
}
