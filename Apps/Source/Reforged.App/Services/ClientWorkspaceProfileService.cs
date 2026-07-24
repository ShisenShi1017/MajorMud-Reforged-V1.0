using System.IO;
using System.Text.Json;
using Reforged.App.Models;

namespace Reforged.App.Services;

public interface IClientWorkspaceProfileService
{
    ClientWorkspaceProfile Profile { get; }
    Task LoadAsync();
    Task SaveAsync();
    Task ResetAsync(ClientLayoutPreset preset);
}

public sealed class ClientWorkspaceProfileService : IClientWorkspaceProfileService
{
    private readonly IApplicationSettingsService _settings;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public ClientWorkspaceProfile Profile { get; private set; } = CreateDefault(ClientLayoutPreset.SingleMonitor);

    public ClientWorkspaceProfileService(IApplicationSettingsService settings) => _settings = settings;

    public async Task LoadAsync()
    {
        var path = GetPath();
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<ClientWorkspaceProfile>(await File.ReadAllTextAsync(path), _json);
                if (loaded is not null)
                {
                    Profile = loaded;
                    EnsureDefaults(Profile);
                    return;
                }
            }
            catch { }
        }
        Profile = CreateDefault(ClientLayoutPreset.SingleMonitor);
    }

    public async Task SaveAsync()
    {
        EnsureDefaults(Profile);
        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Profile, _json));
    }

    public async Task ResetAsync(ClientLayoutPreset preset)
    {
        Profile = CreateDefault(preset);
        await SaveAsync();
    }

    private string GetPath() => Path.Combine(_settings.RootPath, "Client", "Workspace", "workspace-profile.json");

    private static ClientWorkspaceProfile CreateDefault(ClientLayoutPreset preset)
    {
        var profile = new ClientWorkspaceProfile { LayoutPreset = preset, ProfileName = preset.ToString() };
        AddDefaultPanes(profile, preset);
        AddDefaultToolbar(profile);
        return profile;
    }

    private static void EnsureDefaults(ClientWorkspaceProfile profile)
    {
        profile.Panes ??= [];
        profile.ToolbarButtons ??= [];
        if (profile.Panes.Count == 0) AddDefaultPanes(profile, profile.LayoutPreset);
        if (profile.ToolbarButtons.Count == 0) AddDefaultToolbar(profile);
        profile.InterfaceScalePercent = Math.Clamp(profile.InterfaceScalePercent, 75, 200);
        profile.TerminalFontSize = Math.Clamp(profile.TerminalFontSize, 8, 36);
    }

    private static void AddDefaultPanes(ClientWorkspaceProfile profile, ClientLayoutPreset preset)
    {
        var names = new[] { "Main Game", "Map", "Inventory", "Equipment", "Character", "Party", "Chat", "Status", "Notes", "Combat", "Librarium" };
        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var visible = preset switch
            {
                ClientLayoutPreset.Laptop => name is "Main Game" or "Map" or "Status",
                ClientLayoutPreset.Combat => name is "Main Game" or "Combat" or "Status" or "Party" or "Map",
                ClientLayoutPreset.Streaming => name is "Main Game" or "Chat" or "Status" or "Map",
                ClientLayoutPreset.Builder => name is "Main Game" or "Map" or "Notes" or "Librarium",
                _ => name is not "Librarium"
            };
            profile.Panes.Add(new ClientWorkspacePane { Id = name.Replace(" ", string.Empty), DisplayName = name, Visible = visible, Order = i });
        }
    }

    private static void AddDefaultToolbar(ClientWorkspaceProfile profile)
    {
        var buttons = new[]
        {
            ("Connect", "session.connect"), ("Disconnect", "session.disconnect"), ("Auto", "automation.master"),
            ("Stop All", "automation.stop"), ("Map", "pane.map"), ("Inventory", "pane.inventory"),
            ("Equipment", "pane.equipment"), ("Party", "pane.party"), ("Scripts", "pane.scripts"),
            ("Librarium", "pane.librarium")
        };
        for (var i = 0; i < buttons.Length; i++)
            profile.ToolbarButtons.Add(new ClientWorkspaceToolbarButton { Label = buttons[i].Item1, Action = buttons[i].Item2, Order = i });
    }
}
