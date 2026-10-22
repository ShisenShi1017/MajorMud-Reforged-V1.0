using System.Collections.ObjectModel;
using RealmGate.Client.Navigation;

namespace RealmGate.Client.Automation;

public sealed class ClientInputAutomationProfile
{
    public bool AliasesEnabled { get; set; }
    public bool MacrosEnabled { get; set; }
    public bool TextTriggersEnabled { get; set; }
    public bool TimedEventsEnabled { get; set; }
    public bool NavigationEnabled { get; set; }
    public bool EmergencyStop { get; set; } = true;
    public int MinimumCommandDelayMs { get; set; } = 750;
    public ObservableCollection<ClientCommandAlias> Aliases { get; set; } = [];
    public ObservableCollection<ClientCommandMacro> Macros { get; set; } = [];
    public ObservableCollection<ClientTextTrigger> TextTriggers { get; set; } = [];
    public EventAutomationOptions Events { get; set; } = new();
    public ClientNavigationAutomationOptions Navigation { get; set; } = new();
}

public sealed class ClientCommandAlias
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Name) ? "New alias" : Name;
}

public sealed class ClientCommandMacro
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<string> Commands { get; set; } = [];

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Name) ? "New macro" : Name;
}

public enum ClientTextTriggerMatchMode
{
    Contains,
    Exact,
    Wildcard,
    Regex
}

public sealed class ClientTextTrigger
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ClientTextTriggerMatchMode MatchMode { get; set; } = ClientTextTriggerMatchMode.Contains;
    public bool CaseSensitive { get; set; }
    public string MatchText { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int CooldownSeconds { get; set; } = 5;
    public int Priority { get; set; } = 100;
    public bool OnlyOutOfCombat { get; set; }
    public bool OnlyInCombat { get; set; }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Name)
            ? string.IsNullOrWhiteSpace(MatchText) ? "New trigger" : MatchText
            : Name;
}

public sealed record ClientInputAutomationMatch(
    string RuleId,
    string RuleName,
    IReadOnlyList<string> Commands,
    int Priority);
