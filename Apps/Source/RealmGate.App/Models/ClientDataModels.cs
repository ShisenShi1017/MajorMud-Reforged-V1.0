using System.Collections.ObjectModel;

namespace RealmGate.App.Models;

public sealed class ClientDataRecord
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Name { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string HostDetails { get; init; } = string.Empty;
    public ClientDataOverride Local { get; set; } = new();
    public string EffectiveName => string.IsNullOrWhiteSpace(Local.DisplayName) ? Name : Local.DisplayName;
}

public sealed class ClientDataOverride
{
    public string DisplayName { get; set; } = string.Empty;
    public string Aliases { get; set; } = string.Empty;
    public string Classification { get; set; } = "Default";
    public string Highlight { get; set; } = "Default";
    public string Notes { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public bool AlertWhenSeen { get; set; }
    public bool IgnoreForAutomation { get; set; }
}

public sealed class ClientDataCategory
{
    public required string Name { get; init; }
    public required string SourceFile { get; init; }
    public ObservableCollection<ClientDataRecord> Records { get; } = [];
    public override string ToString() => $"{Name} ({Records.Count:N0})";
}
