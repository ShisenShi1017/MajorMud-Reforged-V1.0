using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RealmGate.App.Models;
using RealmGate.Client.Automation;
using RealmGate.App.Services;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

/// <summary>
/// RealmGate automation rules pane over the existing client automation profile.
/// This is configuration UI only; all command transmission still flows through the
/// authoritative ClientAutomationService and ClientCommandIsolationPolicy.
/// </summary>
internal sealed class RealmGateAutomationRulesPane : UserControl
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly ObservableCollection<AliasRow> _aliases = [];
    private readonly ObservableCollection<MacroRow> _macros = [];
    private readonly ObservableCollection<TriggerRow> _triggers = [];
    private readonly ObservableCollection<EventRow> _events = [];
    private readonly CheckBox _aliasesEnabled = new() { Content = "Aliases enabled", Margin = new Thickness(4) };
    private readonly CheckBox _macrosEnabled = new() { Content = "Macros enabled", Margin = new Thickness(4) };
    private readonly CheckBox _triggersEnabled = new() { Content = "Triggers enabled", Margin = new Thickness(4) };
    private readonly CheckBox _eventsEnabled = new() { Content = "Events enabled", Margin = new Thickness(4) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4, 8, 4) };

    public RealmGateAutomationRulesPane(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        LoadRows();
        Content = BuildUi();
    }

    private ClientAutomationProfile Profile => _host.AutomationProfile;

    private UIElement BuildUi()
    {
        var root = new DockPanel();
        var header = new StackPanel { Margin = new Thickness(12) };
        header.Children.Add(new TextBlock { Text = "Aliases, Macros, Triggers & Events", FontSize = 20, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = "All rules are client-side. Automatic commands remain blocked until gameplay is verified, and Emergency Stop overrides this entire window.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });
        var switches = new StackPanel { Orientation = Orientation.Horizontal };
        switches.Children.Add(_aliasesEnabled);
        switches.Children.Add(_macrosEnabled);
        switches.Children.Add(_triggersEnabled);
        switches.Children.Add(_eventsEnabled);
        header.Children.Add(switches);
        _aliasesEnabled.IsChecked = Profile.Capabilities.AliasesEnabled;
        _macrosEnabled.IsChecked = Profile.Capabilities.MacrosEnabled;
        _triggersEnabled.IsChecked = Profile.Capabilities.TextTriggersEnabled;
        _eventsEnabled.IsChecked = Profile.Capabilities.TimedEventsEnabled;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new DockPanel { Margin = new Thickness(12, 6, 12, 12) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = "Save", MinWidth = 90, Margin = new Thickness(4), IsDefault = true };
        save.Click += async (_, _) => await SaveAsync();
        buttons.Children.Add(save);
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        footer.Children.Add(_status);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl { Margin = new Thickness(12, 0, 12, 0) };
        tabs.Items.Add(BuildAliasesTab());
        tabs.Items.Add(BuildMacrosTab());
        tabs.Items.Add(BuildTriggersTab());
        tabs.Items.Add(BuildEventsTab());
        root.Children.Add(tabs);
        return root;
    }

    private TabItem BuildAliasesTab()
    {
        var grid = NewGrid(_aliases);
        grid.Columns.Add(Check("On", nameof(AliasRow.Enabled)));
        grid.Columns.Add(Text("Name", nameof(AliasRow.Name), 150));
        grid.Columns.Add(Text("Expansion", nameof(AliasRow.Expansion), 650));
        return BuildTab("Aliases", grid, _aliases,
            () => _aliases.Add(new AliasRow { Enabled = true, Name = "newalias", Expansion = "look {0}" }),
            "First-word, case-insensitive aliases. {0}=all arguments; {1}, {2}...=individual words. Separate command steps with ';' or ^M.");
    }

    private TabItem BuildMacrosTab()
    {
        var grid = NewGrid(_macros);
        grid.Columns.Add(Check("On", nameof(MacroRow.Enabled)));
        grid.Columns.Add(Text("Name", nameof(MacroRow.Name), 180));
        grid.Columns.Add(Text("Command steps", nameof(MacroRow.Commands), 650));
        return BuildTab("Macros", grid, _macros,
            () => _macros.Add(new MacroRow { Enabled = true, Name = "newmacro", Commands = "score ; inventory" }),
            "Run with /macro name [arguments]. Macro steps accept {0}, {1}, {2}... and are capped at 25 commands per invocation.");
    }

    private TabItem BuildTriggersTab()
    {
        var grid = NewGrid(_triggers);
        grid.Columns.Add(Check("On", nameof(TriggerRow.Enabled)));
        grid.Columns.Add(Text("Name", nameof(TriggerRow.Name), 150));
        grid.Columns.Add(Combo("Mode", nameof(TriggerRow.Mode), Enum.GetNames<ClientTextTriggerMatchMode>(), 105));
        grid.Columns.Add(Check("Case", nameof(TriggerRow.CaseSensitive)));
        grid.Columns.Add(Text("Pattern", nameof(TriggerRow.Pattern), 300));
        grid.Columns.Add(Text("Response", nameof(TriggerRow.Response), 300));
        grid.Columns.Add(Text("Cooldown", nameof(TriggerRow.CooldownSeconds), 80));
        grid.Columns.Add(Text("Priority", nameof(TriggerRow.Priority), 70));
        grid.Columns.Add(Combo("Context", nameof(TriggerRow.Context), ["Any", "OutOfCombat", "InCombat"], 110));
        return BuildTab("Triggers", grid, _triggers,
            () => _triggers.Add(new TriggerRow { Enabled = true, Name = "New Trigger", Mode = nameof(ClientTextTriggerMatchMode.Contains), Pattern = "text", Response = "look", CooldownSeconds = 5, Priority = 100, Context = "Any" }),
            "Contains/Exact are literal. Wildcard supports * and named captures such as 'A {target} attacks you'; use {target} in the response. Regex supports .NET named groups (?<target>...). Regex execution is timeout-bounded.");
    }

    private TabItem BuildEventsTab()
    {
        var grid = NewGrid(_events);
        grid.Columns.Add(Check("On", nameof(EventRow.Enabled)));
        grid.Columns.Add(Text("Name", nameof(EventRow.Name), 190));
        grid.Columns.Add(Text("Interval sec", nameof(EventRow.IntervalSeconds), 90));
        grid.Columns.Add(Check("Immediate", nameof(EventRow.RunImmediately)));
        grid.Columns.Add(Combo("Context", nameof(EventRow.Context), ["Any", "OutOfCombat", "InCombat"], 110));
        grid.Columns.Add(Text("Priority", nameof(EventRow.Priority), 70));
        grid.Columns.Add(Text("Command steps", nameof(EventRow.Commands), 420));
        return BuildTab("Events", grid, _events,
            () => _events.Add(new EventRow { Name = "New Event", IntervalSeconds = 600, Context = "OutOfCombat", Priority = 100, Commands = "score" }),
            "Interval timers run only after verified gameplay. They pause behind Emergency Stop and the existing global event-in-combat policy. Multiple steps use ';' or ^M.");
    }

    private static DataGrid NewGrid(object source) => new()
    {
        ItemsSource = source as System.Collections.IEnumerable,
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        SelectionMode = DataGridSelectionMode.Single,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        Margin = new Thickness(0, 8, 0, 8)
    };

    private static DataGridCheckBoxColumn Check(string header, string path) => new()
    {
        Header = header,
        Binding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
        Width = DataGridLength.Auto
    };

    private static DataGridTextColumn Text(string header, string path, double width) => new()
    {
        Header = header,
        Binding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
        Width = width
    };

    private static DataGridComboBoxColumn Combo(string header, string path, IEnumerable<string> values, double width) => new()
    {
        Header = header,
        ItemsSource = values,
        SelectedItemBinding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
        Width = width
    };

    private static TabItem BuildTab<TRow>(string title, DataGrid grid, ObservableCollection<TRow> rows, Action add, string help)
    {
        var panel = new DockPanel { Margin = new Thickness(8) };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
        var addButton = new Button { Content = "Add", MinWidth = 72, Margin = new Thickness(2) };
        var removeButton = new Button { Content = "Remove", MinWidth = 72, Margin = new Thickness(2) };
        addButton.Click += (_, _) => add();
        removeButton.Click += (_, _) => { if (grid.SelectedItem is TRow row) rows.Remove(row); };
        toolbar.Children.Add(addButton);
        toolbar.Children.Add(removeButton);
        toolbar.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 5, 0, 0), MaxWidth = 820 });
        DockPanel.SetDock(toolbar, Dock.Top);
        panel.Children.Add(toolbar);
        panel.Children.Add(grid);
        return new TabItem { Header = title, Content = panel };
    }

    private void LoadRows()
    {
        foreach (var alias in Profile.Capabilities.Aliases)
            _aliases.Add(new AliasRow { Enabled = alias.Enabled, Name = alias.Name, Expansion = alias.Command });
        foreach (var macro in Profile.Capabilities.Macros)
            _macros.Add(new MacroRow { Enabled = macro.Enabled, Name = macro.Name, Commands = string.Join(" ; ", macro.Commands) });
        foreach (var trigger in Profile.Capabilities.TextTriggers)
            _triggers.Add(new TriggerRow
            {
                Enabled = trigger.Enabled,
                Name = trigger.Name,
                Mode = trigger.MatchMode.ToString(),
                CaseSensitive = trigger.CaseSensitive,
                Pattern = trigger.MatchText,
                Response = trigger.Command,
                CooldownSeconds = trigger.CooldownSeconds,
                Priority = trigger.Priority,
                Context = trigger.OnlyInCombat ? "InCombat" : trigger.OnlyOutOfCombat ? "OutOfCombat" : "Any"
            });
        foreach (var scheduledEvent in Profile.Options.Events.ScheduledEvents)
            _events.Add(new EventRow
            {
                Enabled = scheduledEvent.Enabled,
                Name = scheduledEvent.Name,
                IntervalSeconds = scheduledEvent.IntervalSeconds > 0 ? scheduledEvent.IntervalSeconds : Math.Max(1, scheduledEvent.IntervalMinutes) * 60,
                RunImmediately = scheduledEvent.RunImmediately,
                Context = scheduledEvent.OnlyInCombat ? "InCombat" : scheduledEvent.OnlyOutOfCombat ? "OutOfCombat" : "Any",
                Priority = scheduledEvent.Priority,
                Commands = scheduledEvent.Command
            });
    }

    private async Task SaveAsync()
    {
        _status.Text = string.Empty;
        var aliasNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _aliases)
        {
            var conflict = ClientAutomationExpansionEngine.AliasNameConflictReason(row.Name);
            if (conflict is not null) { _status.Text = conflict; return; }
            if (!aliasNames.Add(row.Name.Trim())) { _status.Text = $"Duplicate alias '{row.Name.Trim()}'."; return; }
            if (ClientAutomationExpansionEngine.SplitCommandSteps(row.Expansion).Count == 0) { _status.Text = $"Alias '{row.Name}' has no command."; return; }
        }

        var macroNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _macros)
        {
            if (string.IsNullOrWhiteSpace(row.Name) || row.Name.Any(char.IsWhiteSpace)) { _status.Text = "Macro names cannot be blank or contain whitespace."; return; }
            if (!macroNames.Add(row.Name.Trim())) { _status.Text = $"Duplicate macro '{row.Name.Trim()}'."; return; }
            if (ClientAutomationExpansionEngine.SplitCommandSteps(row.Commands).Count == 0) { _status.Text = $"Macro '{row.Name}' has no command steps."; return; }
        }

        foreach (var row in _triggers)
        {
            if (!Enum.TryParse<ClientTextTriggerMatchMode>(row.Mode, true, out var mode)) { _status.Text = $"Trigger '{row.Name}' has an unknown match mode."; return; }
            var error = ClientAutomationExpansionEngine.ValidateTriggerPattern(mode, row.Pattern, row.CaseSensitive);
            if (error is not null) { _status.Text = $"Trigger '{row.Name}': {error}"; return; }
            if (ClientAutomationExpansionEngine.SplitCommandSteps(row.Response).Count == 0) { _status.Text = $"Trigger '{row.Name}' has no response."; return; }
        }
        foreach (var row in _events)
            if (row.IntervalSeconds < 1 || ClientAutomationExpansionEngine.SplitCommandSteps(row.Commands).Count == 0)
            { _status.Text = $"Event '{row.Name}' needs an interval of at least 1 second and at least one command."; return; }

        var capabilities = Profile.Capabilities;
        capabilities.AliasesEnabled = _aliasesEnabled.IsChecked == true;
        capabilities.MacrosEnabled = _macrosEnabled.IsChecked == true;
        capabilities.TextTriggersEnabled = _triggersEnabled.IsChecked == true;
        capabilities.TimedEventsEnabled = _eventsEnabled.IsChecked == true;

        capabilities.Aliases.Clear();
        foreach (var row in _aliases)
            capabilities.Aliases.Add(new ClientCommandAlias { Enabled = row.Enabled, Name = row.Name.Trim(), Command = row.Expansion.Trim() });
        capabilities.Macros.Clear();
        foreach (var row in _macros)
        {
            var macro = new ClientCommandMacro { Enabled = row.Enabled, Name = row.Name.Trim() };
            foreach (var step in ClientAutomationExpansionEngine.SplitCommandSteps(row.Commands)) macro.Commands.Add(step);
            capabilities.Macros.Add(macro);
        }
        capabilities.TextTriggers.Clear();
        foreach (var row in _triggers)
        {
            _ = Enum.TryParse<ClientTextTriggerMatchMode>(row.Mode, true, out var mode);
            capabilities.TextTriggers.Add(new ClientTextTrigger
            {
                Name = string.IsNullOrWhiteSpace(row.Name) ? row.Pattern.Trim() : row.Name.Trim(),
                Enabled = row.Enabled,
                MatchMode = mode,
                CaseSensitive = row.CaseSensitive,
                MatchText = row.Pattern.Trim(),
                Command = row.Response.Trim(),
                CooldownSeconds = Math.Clamp(row.CooldownSeconds, 0, 86400),
                Priority = row.Priority,
                OnlyOutOfCombat = row.Context == "OutOfCombat",
                OnlyInCombat = row.Context == "InCombat"
            });
        }
        Profile.Options.Events.ScheduledEvents.Clear();
        foreach (var row in _events)
            Profile.Options.Events.ScheduledEvents.Add(new ClientScheduledEvent
            {
                Name = string.IsNullOrWhiteSpace(row.Name) ? "Event" : row.Name.Trim(),
                Enabled = row.Enabled,
                IntervalSeconds = Math.Max(1, row.IntervalSeconds),
                IntervalMinutes = Math.Max(1, (int)Math.Ceiling(row.IntervalSeconds / 60d)),
                Command = row.Commands.Trim(),
                RunImmediately = row.RunImmediately,
                OnlyOutOfCombat = row.Context == "OutOfCombat",
                OnlyInCombat = row.Context == "InCombat",
                Priority = row.Priority
            });

        await _host.SaveAutomationAsync();
        _status.Text = "Saved. RealmGate command isolation and Emergency Stop remain in force.";
    }

    private sealed class AliasRow { public bool Enabled { get; set; } public string Name { get; set; } = string.Empty; public string Expansion { get; set; } = string.Empty; }
    private sealed class MacroRow { public bool Enabled { get; set; } public string Name { get; set; } = string.Empty; public string Commands { get; set; } = string.Empty; }
    private sealed class TriggerRow
    {
        public bool Enabled { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Mode { get; set; } = nameof(ClientTextTriggerMatchMode.Contains);
        public bool CaseSensitive { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public int CooldownSeconds { get; set; } = 5;
        public int Priority { get; set; } = 100;
        public string Context { get; set; } = "Any";
    }
    private sealed class EventRow
    {
        public bool Enabled { get; set; }
        public string Name { get; set; } = string.Empty;
        public int IntervalSeconds { get; set; } = 600;
        public bool RunImmediately { get; set; }
        public string Context { get; set; } = "OutOfCombat";
        public int Priority { get; set; } = 100;
        public string Commands { get; set; } = string.Empty;
    }
}
