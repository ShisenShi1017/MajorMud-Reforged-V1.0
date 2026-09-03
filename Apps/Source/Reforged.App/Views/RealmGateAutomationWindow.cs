using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Reforged.App.Models;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

/// <summary>
/// Single player-facing automation surface. The playing client exposes this one window
/// instead of separate legacy combat, recovery, inventory, and rule dialogs.
/// </summary>
public sealed class RealmGateAutomationWindow : Window
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly CheckBox _masterEnabled = Check("Master automation enabled");
    private readonly CheckBox _aliases = Check("Aliases");
    private readonly CheckBox _macros = Check("Macros");
    private readonly CheckBox _triggers = Check("Text / wildcard / regex triggers");
    private readonly CheckBox _events = Check("Timed events");
    private readonly CheckBox _navigation = Check("Navigation / route automation");
    private readonly CheckBox _combat = Check("Combat automation");
    private readonly CheckBox _recovery = Check("Health / recovery automation");
    private readonly CheckBox _spells = Check("Buff / maintained-spell automation");
    private readonly CheckBox _loot = Check("Loot / inventory / banking automation");
    private readonly CheckBox _roaming = Check("Roaming helpers (search / sneak / track)");
    private readonly CheckBox _pvp = Check("PvP automation");
    private readonly CheckBox _communications = Check("Tell / AFK automation");
    private readonly CheckBox _afk = Check("Automatic AFK reply to direct tells");
    private readonly TextBox _afkMessage = new() { MinWidth = 420, MaxLength = 240, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 70, Margin = new Thickness(4) };
    private readonly TextBox _afkCooldown = new() { Width = 80, Margin = new Thickness(4) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4, 8, 4) };

    public RealmGateAutomationWindow(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Title = $"RealmGate Automation — {DisplayCharacter(session)}";
        Width = 1180;
        Height = 820;
        MinWidth = 900;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;
        LoadOverview();
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(10) };
        var header = new StackPanel { Margin = new Thickness(4, 2, 4, 10) };
        header.Children.Add(new TextBlock { Text = "Automation", FontSize = 21, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = "One place for aliases, macros, triggers, timed events, combat, recovery, buffs, loot, banking, PvP and roaming. Emergency Stop still overrides every automatic command.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Overview", Content = BuildOverview() });
        tabs.Items.Add(new TabItem { Header = "Aliases / Macros / Triggers / Events", Content = new RealmGateAutomationRulesPane(_host, Session) });
        tabs.Items.Add(new TabItem { Header = "Combat", Content = new RealmGateCombatAutomationPane(_host, Session) });
        tabs.Items.Add(new TabItem { Header = "Recovery / Buffs", Content = new RealmGateRecoveryAutomationPane(_host, Session) });
        tabs.Items.Add(new TabItem { Header = "Loot / Inventory / Banking", Content = new RealmGateInventoryAutomationPane(_host, Session) });
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildOverview()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = true };
        DockPanel.SetDock(footer, Dock.Bottom);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var stop = Button("Emergency Stop", 115);
        var save = Button("Save", 80);
        var close = Button("Close", 80);
        stop.Click += async (_, _) =>
        {
            await _host.StopAllAutomationAsync();
            LoadOverview();
            _status.Text = "Emergency Stop applied. All automation and active routes are stopped.";
        };
        save.Click += async (_, _) => await SaveOverviewAsync();
        close.Click += (_, _) => Close();
        buttons.Children.Add(stop); buttons.Children.Add(save); buttons.Children.Add(close);
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        footer.Children.Add(_status);
        root.Children.Add(footer);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel { Margin = new Thickness(6) };
        scroll.Content = panel;
        root.Children.Add(scroll);

        panel.Children.Add(new TextBlock { Text = "Master Safety", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
        panel.Children.Add(_masterEnabled);
        panel.Children.Add(new TextBlock
        {
            Text = "Clearing Master Automation applies the same fail-closed Emergency Stop used by the Stop command. Enabling it clears Emergency Stop but individual feature switches still control what may run.",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.78, Margin = new Thickness(4, 0, 4, 12)
        });

        panel.Children.Add(new TextBlock { Text = "Feature Switches", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 5) });
        foreach (var control in new[] { _aliases, _macros, _triggers, _events, _navigation, _combat, _recovery, _spells, _loot, _roaming, _pvp, _communications })
            panel.Children.Add(control);

        panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
        panel.Children.Add(new TextBlock { Text = "Away From Keyboard", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
        panel.Children.Add(_afk);
        panel.Children.Add(new TextBlock { Text = "Reply text (RealmGate adds [AFK]):", Margin = new Thickness(4, 3, 0, 2) });
        panel.Children.Add(_afkMessage);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 3, 0, 0) };
        row.Children.Add(new TextBlock { Text = "Repeat cooldown (seconds):", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_afkCooldown);
        panel.Children.Add(row);
        return root;
    }

    private void LoadOverview()
    {
        var profile = _host.AutomationProfile;
        var c = profile.Capabilities;
        _masterEnabled.IsChecked = profile.Enabled && !c.EmergencyStop;
        _aliases.IsChecked = c.AliasesEnabled;
        _macros.IsChecked = c.MacrosEnabled;
        _triggers.IsChecked = c.TextTriggersEnabled;
        _events.IsChecked = c.TimedEventsEnabled;
        _navigation.IsChecked = c.NavigationEnabled;
        _combat.IsChecked = c.CombatEnabled;
        _recovery.IsChecked = c.HealthRecoveryEnabled;
        _spells.IsChecked = c.SpellMaintenanceEnabled;
        _loot.IsChecked = c.LootAutomationEnabled;
        _roaming.IsChecked = c.RoamingAutomationEnabled;
        _pvp.IsChecked = c.PvpAutomationEnabled;
        _communications.IsChecked = c.CommunicationsAutomationEnabled;
        _afk.IsChecked = c.AwayFromKeyboardEnabled;
        _afkMessage.Text = c.AwayFromKeyboardMessage;
        _afkCooldown.Text = c.AwayFromKeyboardReplyCooldownSeconds.ToString();
        _status.Text = c.EmergencyStop ? "Emergency Stop is ON." : "Automation is ready. Individual switches still apply.";
    }

    private async Task SaveOverviewAsync()
    {
        var profile = _host.AutomationProfile;
        var c = profile.Capabilities;
        c.AliasesEnabled = _aliases.IsChecked == true;
        c.MacrosEnabled = _macros.IsChecked == true;
        c.TextTriggersEnabled = _triggers.IsChecked == true;
        c.TimedEventsEnabled = _events.IsChecked == true;
        c.NavigationEnabled = _navigation.IsChecked == true;
        c.CombatEnabled = _combat.IsChecked == true;
        c.HealthRecoveryEnabled = _recovery.IsChecked == true;
        c.SpellMaintenanceEnabled = _spells.IsChecked == true;
        c.LootAutomationEnabled = _loot.IsChecked == true;
        c.RoamingAutomationEnabled = _roaming.IsChecked == true;
        c.PvpAutomationEnabled = _pvp.IsChecked == true;
        c.CommunicationsAutomationEnabled = _communications.IsChecked == true || _afk.IsChecked == true;
        c.AwayFromKeyboardEnabled = _afk.IsChecked == true;
        c.AwayFromKeyboardMessage = string.Join(" ", _afkMessage.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        c.AwayFromKeyboardReplyCooldownSeconds = int.TryParse(_afkCooldown.Text, out var seconds) ? Math.Clamp(seconds, 30, 86400) : 300;

        var enabled = _masterEnabled.IsChecked == true;
        await _host.SetMasterAutomationAsync(enabled);
        await _host.SaveAutomationAsync();
        LoadOverview();
        _status.Text = enabled ? "Automation settings saved." : "Settings saved with Emergency Stop applied.";
    }

    private static string DisplayCharacter(ClientSessionConnection session) =>
        string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;

    private static CheckBox Check(string text) => new() { Content = text, Margin = new Thickness(4, 3, 4, 3) };
    private static Button Button(string text, double width) => new() { Content = text, Width = width, Margin = new Thickness(4), Padding = new Thickness(7, 3, 7, 3) };
}
