using System.Collections.ObjectModel;

namespace RealmGate.App.Models;

public enum ClientWorkspaceTheme
{
    Classic,
    Modern,
    Dark,
    Light,
    HighContrast,
    Custom
}

public enum ClientLayoutPreset
{
    SingleMonitor,
    DualMonitor,
    TripleMonitor,
    Laptop,
    Builder,
    Streaming,
    Combat
}

public sealed class ClientWorkspaceProfile
{
    public string ProfileName { get; set; } = "Default Workspace";
    public ClientWorkspaceTheme Theme { get; set; } = ClientWorkspaceTheme.Classic;
    public ClientLayoutPreset LayoutPreset { get; set; } = ClientLayoutPreset.SingleMonitor;
    public string AccentColor { get; set; } = "SteelBlue";
    public string TerminalFontFamily { get; set; } = "Consolas";
    public int TerminalFontSize { get; set; } = 14;
    public int InterfaceScalePercent { get; set; } = 100;
    public bool HighContrast { get; set; }
    public bool ColorblindSafePalette { get; set; }
    public bool ReduceMotion { get; set; }
    public bool KeyboardNavigationHints { get; set; } = true;
    public bool ScreenReaderDescriptions { get; set; } = true;
    public bool RestoreWindowsOnStartup { get; set; } = true;
    public bool RememberFloatingPositions { get; set; } = true;
    public bool KeepSessionWindowsIndependent { get; set; } = true;
    public ObservableCollection<ClientWorkspacePane> Panes { get; set; } = [];
    public ObservableCollection<ClientWorkspaceToolbarButton> ToolbarButtons { get; set; } = [];
}

public sealed class ClientWorkspacePane
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public bool Floating { get; set; }
    public bool AutoHide { get; set; }
    public int Order { get; set; }
}

public sealed class ClientWorkspaceToolbarButton
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public int Order { get; set; }
}
