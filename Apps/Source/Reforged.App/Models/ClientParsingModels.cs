using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reforged.App.Models;

public enum ClientParsingRuleKind
{
    RoomTitle,
    Health,
    Mana,
    CombinedVitals,
    Currency,
    CombatStart,
    CombatEnd,
    Resting,
    MovementRejected,
    Experience,
    Level,
    InventoryHeader,
    InventoryFooter,
    InventoryItem,
    EquipmentHeader,
    EquipmentFooter,
    EquipmentItem
}

public sealed class ClientParsingRule : INotifyPropertyChanged
{
    private bool _enabled = true;
    private string _name = string.Empty;
    private string _pattern = string.Empty;
    private string _description = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ClientParsingRuleKind Kind { get; set; }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Pattern { get => _pattern; set => Set(ref _pattern, value); }
    public string Description { get => _description; set => Set(ref _description, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class ClientParsingProfile : INotifyPropertyChanged
{
    private string _name = "Classic MajorMUD";
    private string _serverName = "Local Host";
    private string _worldName = "Classic";
    private bool _stripAnsi = true;
    private bool _caseInsensitive = true;

    public string Name { get => _name; set => Set(ref _name, value); }
    public string ServerName { get => _serverName; set => Set(ref _serverName, value); }
    public string WorldName { get => _worldName; set => Set(ref _worldName, value); }
    public bool StripAnsi { get => _stripAnsi; set => Set(ref _stripAnsi, value); }
    public bool CaseInsensitive { get => _caseInsensitive; set => Set(ref _caseInsensitive, value); }
    public ObservableCollection<ClientParsingRule> Rules { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}


public sealed record ClientParsingTestMatch(
    bool Matched,
    ClientParsingRuleKind? Kind,
    string RuleName,
    string PreparedLine,
    string Captures,
    string Message);

public sealed class ClientParsingProfileSummary
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public string WorldName { get; init; } = string.Empty;
    public string DisplayName => $"{Name} • {ServerName} / {WorldName}";
}
