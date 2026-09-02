using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App.Views;

/// <summary>
/// MudPlay-inspired Character Workshop for Reforged.
/// CodeUpdate0372 keeps Reforged world data authoritative on Reforged servers and applies
/// the current external-BBS overlay over Classic for client-side planning tools.
/// </summary>
public sealed class MudPlayCharacterWorkshopWindow : Window
{
    private static readonly string[] OverviewProperties =
    [
        "CharacterName", "WorldName", "ServerProfileName", "ConnectionState", "CurrentRoomName",
        "HealthCurrent", "HealthMaximum", "HealthPercent", "ManaCurrent", "ManaMaximum", "ManaPercent",
        "CharacterLevel", "ExperienceCurrent", "ExperienceNeeded", "ExperienceRemaining", "CharacterPoints", "Lives",
        "CarriedCurrency", "CarriedWeight", "CarryingCapacity", "EncumbrancePercent", "EncumbranceBand",
        "InCombat", "RouteState", "RouteProgress"
    ];

    private static readonly (string Header, string[] Candidates)[] CollectionTabs =
    [
        ("Inventory", ["InventoryItems", "InventoryEntries", "InventorySnapshot"]),
        ("Equipment", ["EquippedItems", "EquipmentEntries", "EquipmentSnapshot"]),
        ("Spells", ["LearnedSpellDetails", "LearnedSpells", "SpellbookEntries"]),
        ("Effects", ["ActiveEffectDetails", "ActiveEffects", "StatusEffects"]),
        ("Party", ["PartyMembers", "PartyRoster"])
    ];

    private readonly DispatcherTimer _refreshTimer;
    private readonly TextBlock _identity;
    private readonly TextBlock _freshness;
    private readonly TextBlock _dataStatus;
    private readonly Dictionary<string, TextBox> _tabOutputs = new(StringComparer.OrdinalIgnoreCase);

    private readonly DataGrid _currentEquipmentGrid;
    private readonly DataGrid _alternativeEquipmentGrid;
    private readonly TextBlock _equipmentSummary;

    private readonly TextBox _itemSearch;
    private readonly ComboBox _itemSlot;
    private readonly CheckBox _itemLevelFilter;
    private readonly DataGrid _itemGrid;
    private readonly TextBox _itemDetails;
    private readonly TextBlock _itemResultSummary;

    private readonly TextBlock _progressSummary;
    private readonly TextBlock _cpSummary;
    private readonly TextBox _timersProjection;
    private readonly TextBox _trialLoadoutInput;
    private readonly TextBox _trialLoadoutOutput;
    private readonly Dictionary<string, TextBox> _cpPlanInputs = new(StringComparer.OrdinalIgnoreCase);

    private MudPlayWorkshopDataCatalog? _catalog;
    private bool _catalogLoading;

    public MudPlayCharacterWorkshopWindow(ClientSessionConnection session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));

        Title = "Character Workshop — Reforged";
        Width = 1080;
        Height = 760;
        MinWidth = 780;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        var root = new DockPanel { Margin = new Thickness(10) };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(36, 42, 48)),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = new CornerRadius(4)
        };
        DockPanel.SetDock(header, Dock.Top);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerStack = new StackPanel();
        _identity = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        };
        _freshness = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(200, 209, 218)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        _dataStatus = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(200, 209, 218)),
            Margin = new Thickness(0, 2, 0, 0)
        };
        headerStack.Children.Add(_identity);
        headerStack.Children.Add(_freshness);
        headerStack.Children.Add(_dataStatus);
        headerGrid.Children.Add(headerStack);

        var refreshData = new Button
        {
            Content = "Reload Game Data",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        refreshData.Click += async (_, _) => await LoadCatalogAsync(force: true);
        Grid.SetColumn(refreshData, 1);
        headerGrid.Children.Add(refreshData);

        header.Child = headerGrid;
        root.Children.Add(header);

        var footer = new TextBlock
        {
            Text = "Character Workshop uses Reforged live session state plus the effective game-data authority: exact Reforged active-world data on Reforged servers, or the current BBS overlay over Classic externally. Planning, timers, and projections send no game commands.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 8, 2, 0),
            Foreground = Brushes.DimGray
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl();
        AddTextTab(tabs, "Overview");
        foreach (var tab in CollectionTabs)
            AddTextTab(tabs, tab.Header);

        (_currentEquipmentGrid, _alternativeEquipmentGrid, _equipmentSummary) = AddEquipmentManagerTab(tabs);
        (_itemSearch, _itemSlot, _itemLevelFilter, _itemGrid, _itemDetails, _itemResultSummary) = AddItemFinderTab(tabs);
        (_progressSummary, _cpSummary) = AddProgressPlannerTab(tabs);
        (_trialLoadoutInput, _trialLoadoutOutput) = AddTrialLoadoutTab(tabs);
        _timersProjection = AddTimersProjectionTab(tabs);

        root.Children.Add(tabs);
        Content = root;

        _refreshTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(750),
            DispatcherPriority.Background,
            (_, _) => RefreshLiveState(),
            Dispatcher);

        Loaded += async (_, _) =>
        {
            RefreshLiveState();
            _refreshTimer.Start();
            await LoadCatalogAsync(force: false);
        };
        Closed += (_, _) => _refreshTimer.Stop();
    }

    public ClientSessionConnection Session { get; }

    private void AddTextTab(TabControl tabs, string header)
    {
        var output = new TextBox
        {
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(10),
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        _tabOutputs[header] = output;
        tabs.Items.Add(new TabItem { Header = header, Content = output });
    }

    private (DataGrid Current, DataGrid Alternatives, TextBlock Summary) AddEquipmentManagerTab(TabControl tabs)
    {
        var root = new Grid { Margin = new Thickness(8) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var summary = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 2, 2, 8),
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumnSpan(summary, 3);
        root.Children.Add(summary);

        var current = CreateItemGrid();
        var currentBox = new GroupBox { Header = "Current captured equipment", Content = current, Padding = new Thickness(5) };
        Grid.SetRow(currentBox, 1);
        root.Children.Add(currentBox);

        var alternatives = CreateItemGrid();
        var alternativeBox = new GroupBox
        {
            Header = "Generated-data alternatives for selected slot",
            Content = alternatives,
            Padding = new Thickness(5)
        };
        Grid.SetRow(alternativeBox, 1);
        Grid.SetColumn(alternativeBox, 2);
        root.Children.Add(alternativeBox);

        current.SelectionChanged += (_, _) => RefreshEquipmentAlternatives();
        tabs.Items.Add(new TabItem { Header = "Equipment Manager", Content = root });
        return (current, alternatives, summary);
    }

    private (TextBox Search, ComboBox Slot, CheckBox LevelFilter, DataGrid Grid, TextBox Details, TextBlock Summary) AddItemFinderTab(TabControl tabs)
    {
        var root = new Grid { Margin = new Thickness(8) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(145) });

        var filters = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
        var slot = new ComboBox { Width = 145, Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(slot, Dock.Right);
        var levelFilter = new CheckBox
        {
            Content = "Hide items above current level",
            IsChecked = true,
            Margin = new Thickness(10, 3, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(levelFilter, Dock.Right);
        var search = new TextBox { MinWidth = 240, ToolTip = "Search item name, type, slot, or item id" };
        filters.Children.Add(slot);
        filters.Children.Add(levelFilter);
        filters.Children.Add(search);
        root.Children.Add(filters);

        var resultSummary = new TextBlock { Margin = new Thickness(2, 0, 2, 6), Foreground = Brushes.DimGray };
        Grid.SetRow(resultSummary, 1);
        root.Children.Add(resultSummary);

        var grid = CreateItemGrid();
        Grid.SetRow(grid, 2);
        root.Children.Add(grid);

        var details = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 6, 0, 0),
            FontFamily = new FontFamily("Consolas")
        };
        Grid.SetRow(details, 3);
        root.Children.Add(details);

        search.TextChanged += (_, _) => RefreshItemFinder();
        slot.SelectionChanged += (_, _) => RefreshItemFinder();
        levelFilter.Checked += (_, _) => RefreshItemFinder();
        levelFilter.Unchecked += (_, _) => RefreshItemFinder();
        grid.SelectionChanged += (_, _) => RefreshItemDetails();

        tabs.Items.Add(new TabItem { Header = "Item Finder", Content = root });
        return (search, slot, levelFilter, grid, details, resultSummary);
    }

    private (TextBlock ProgressSummary, TextBlock CpSummary) AddProgressPlannerTab(TabControl tabs)
    {
        var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel { Margin = new Thickness(12) };
        root.Content = panel;

        panel.Children.Add(new TextBlock
        {
            Text = "Next-level projection",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var progress = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(progress);

        panel.Children.Add(new TextBlock
        {
            Text = "Character Point scratch plan",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Enter CP amounts you want to reserve for each category. This is only a budget scratchpad; it does not assume a stat-cost formula and it does not send commands.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var cpGrid = new Grid { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        cpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        cpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        var categories = new[] { "Strength", "Health", "Agility", "Intellect", "Willpower", "Charisma" };
        for (var index = 0; index < categories.Length; index++)
        {
            cpGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = categories[index],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 3, 8, 3)
            };
            Grid.SetRow(label, index);
            cpGrid.Children.Add(label);

            var input = new TextBox { Text = "0", Margin = new Thickness(0, 3, 0, 3) };
            input.TextChanged += (_, _) => RefreshCpPlan();
            Grid.SetRow(input, index);
            Grid.SetColumn(input, 1);
            cpGrid.Children.Add(input);
            _cpPlanInputs[categories[index]] = input;
        }
        panel.Children.Add(cpGrid);

        var cpSummary = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(cpSummary);

        tabs.Items.Add(new TabItem { Header = "Progress Planner", Content = root });
        return (progress, cpSummary);
    }

    private (TextBox Input, TextBox Output) AddTrialLoadoutTab(TabControl tabs)
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var note = new TextBlock
        {
            Text = "What-if loadout sandbox. Enter one decoded item name per line. Reforged compares catalogued weight/combat bonuses against captured equipment without equipping anything or inventing unknown stat formulas.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetColumnSpan(note, 3);
        root.Children.Add(note);

        var input = new TextBox
        {
            AcceptsReturn = true, AcceptsTab = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Background = Brushes.White, Foreground = Brushes.Black,
            ToolTip = "One exact or recognizable item name per line"
        };
        Grid.SetRow(input, 1); root.Children.Add(input);

        var output = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(8)
        };
        Grid.SetRow(output, 1); Grid.SetColumn(output, 2); root.Children.Add(output);

        var recalc = new Button { Content = "Recalculate", Width = 95, Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 8, 0, 0) };
        recalc.Click += (_, _) => RefreshTrialLoadout();
        Grid.SetRow(recalc, 2); root.Children.Add(recalc);
        input.TextChanged += (_, _) => RefreshTrialLoadout();

        tabs.Items.Add(new TabItem { Header = "Loadout Sandbox", Content = root });
        return (input, output);
    }

    private TextBox AddTimersProjectionTab(TabControl tabs)
    {
        var output = new TextBox
        {
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(10),
            Background = Brushes.White,
            Foreground = Brushes.Black
        };
        tabs.Items.Add(new TabItem { Header = "Timers & Projection", Content = output });
        return output;
    }

    private static DataGrid CreateItemGrid()
    {
        var grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(MudPlayWorkshopItem.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Slot", Binding = new Binding(nameof(MudPlayWorkshopItem.Slot)), Width = new DataGridLength(95) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Damage", Binding = new Binding(nameof(MudPlayWorkshopItem.Damage)), Width = new DataGridLength(75) });
        grid.Columns.Add(new DataGridTextColumn { Header = "AC", Binding = new Binding(nameof(MudPlayWorkshopItem.ArmorClassBonus)), Width = new DataGridLength(48) });
        grid.Columns.Add(new DataGridTextColumn { Header = "DR", Binding = new Binding(nameof(MudPlayWorkshopItem.DamageResistanceBonus)), Width = new DataGridLength(48) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Wt", Binding = new Binding(nameof(MudPlayWorkshopItem.Weight)), Width = new DataGridLength(62) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Min Lvl", Binding = new Binding(nameof(MudPlayWorkshopItem.MinimumLevel)), Width = new DataGridLength(65) });
        return grid;
    }

    private async Task LoadCatalogAsync(bool force)
    {
        if (_catalogLoading || (!force && _catalog is not null))
            return;

        _catalogLoading = true;
        _dataStatus.Text = "Loading generated Reforged game data…";
        try
        {
            var catalog = await Task.Run(() => MudPlayWorkshopDataCatalog.Load(Session));
            _catalog = catalog;
            var authority = new MudPlayClientGameDataOverlayService().ResolveContext(Session);
            var suffix = authority.Mode == MudPlayGameDataAuthorityMode.ExternalBbs
                ? " • BBS overlay -> Classic"
                : " • Reforged world authority";
            _dataStatus.Text = $"Game data: {catalog.WorldName} • {catalog.Items.Count:N0} items{suffix} • {catalog.DataRoot}";
            PopulateItemSlots();
            RefreshEquipmentManager();
            RefreshItemFinder();
        }
        catch (Exception exception)
        {
            _catalog = null;
            _dataStatus.Text = "Game data unavailable: " + exception.Message;
            _equipmentSummary.Text = "Generated equipment data is unavailable. Live captured equipment remains visible on the Equipment tab.";
            _itemResultSummary.Text = "Generated item data is unavailable.";
            _itemGrid.ItemsSource = null;
        }
        finally
        {
            _catalogLoading = false;
        }
    }

    private void PopulateItemSlots()
    {
        if (_catalog is null)
            return;

        var previous = _itemSlot.SelectedItem?.ToString() ?? "All slots";
        var values = new[] { "All slots" }
            .Concat(_catalog.Items.Select(item => item.Slot).Where(slot => !string.IsNullOrWhiteSpace(slot)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(slot => slot, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        _itemSlot.ItemsSource = values;
        _itemSlot.SelectedItem = values.FirstOrDefault(value => value.Equals(previous, StringComparison.OrdinalIgnoreCase)) ?? values[0];
    }

    private void RefreshLiveState()
    {
        var character = string.IsNullOrWhiteSpace(Session.CharacterName) ? Session.DisplayName : Session.CharacterName;
        _identity.Text = $"{character} — Character Workshop";
        _freshness.Text = $"{Session.ServerProfileName} / {Session.WorldName}   •   {Session.ConnectionState}   •   live refresh";

        _tabOutputs["Overview"].Text = BuildPropertyReport(Session, OverviewProperties);
        foreach (var tab in CollectionTabs)
            _tabOutputs[tab.Header].Text = BuildCollectionReport(Session, tab.Candidates);

        RefreshEquipmentManager();
        RefreshProgressPlanner();
        RefreshCpPlan();
        RefreshTimersProjection();
        RefreshTrialLoadout();
    }

    private void RefreshEquipmentManager()
    {
        if (_catalog is null)
        {
            _currentEquipmentGrid.ItemsSource = null;
            _alternativeEquipmentGrid.ItemsSource = null;
            _equipmentSummary.Text = "Load generated game data to match captured equipment against authoritative item records.";
            return;
        }

        var matched = Session.EquippedItems
            .Select(raw => new { Raw = raw, Item = _catalog.FindByDisplayName(raw) })
            .ToArray();

        var rows = matched.Where(value => value.Item is not null).Select(value => value.Item!).ToArray();
        var selectedId = (_currentEquipmentGrid.SelectedItem as MudPlayWorkshopItem)?.Id;
        _currentEquipmentGrid.ItemsSource = rows;
        if (rows.Length > 0)
        {
            _currentEquipmentGrid.SelectedItem = rows.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(selectedId) && item.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                ?? rows[0];
        }

        var unmatched = matched.Count(value => value.Item is null);
        var totalWeight = rows.Sum(item => item.Weight);
        var totalAc = rows.Sum(item => item.ArmorClassBonus);
        var totalDr = rows.Sum(item => item.DamageResistanceBonus);
        _equipmentSummary.Text =
            $"Matched {rows.Length:N0}/{Session.EquippedItems.Count:N0} captured entries • catalog weight {totalWeight:N0} • AC bonus sum {totalAc:+#;-#;0} • DR bonus sum {totalDr:+#;-#;0}" +
            (unmatched > 0 ? $" • {unmatched:N0} unmatched terminal name(s)" : string.Empty) +
            $" • live carried {Session.CarriedWeight:N0}/{Session.CarryingCapacity:N0}";

        RefreshEquipmentAlternatives();
    }

    private void RefreshEquipmentAlternatives()
    {
        if (_catalog is null || _currentEquipmentGrid.SelectedItem is not MudPlayWorkshopItem selected || string.IsNullOrWhiteSpace(selected.Slot))
        {
            _alternativeEquipmentGrid.ItemsSource = Array.Empty<MudPlayWorkshopItem>();
            return;
        }

        var level = Session.CharacterLevel;
        _alternativeEquipmentGrid.ItemsSource = _catalog.Items
            .Where(item => item.Slot.Equals(selected.Slot, StringComparison.OrdinalIgnoreCase))
            .Where(item => level <= 0 || item.MinimumLevel <= 0 || item.MinimumLevel <= level)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .ToArray();
    }

    private void RefreshItemFinder()
    {
        if (_catalog is null)
            return;

        var query = _itemSearch.Text.Trim();
        var slot = _itemSlot.SelectedItem?.ToString() ?? "All slots";
        var currentLevelOnly = _itemLevelFilter.IsChecked == true;
        var level = Session.CharacterLevel;

        IEnumerable<MudPlayWorkshopItem> filtered = _catalog.Items;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.ItemType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Slot.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!slot.Equals("All slots", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(item => item.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase));

        if (currentLevelOnly && level > 0)
            filtered = filtered.Where(item => item.MinimumLevel <= 0 || item.MinimumLevel <= level);

        var rows = filtered.Take(2_000).ToArray();
        _itemGrid.ItemsSource = rows;
        _itemResultSummary.Text = $"{rows.Length:N0} item(s) shown" + (rows.Length == 2_000 ? " (display capped at 2,000)" : string.Empty) +
                                  (currentLevelOnly && level > 0 ? $" • minimum-level filter ≤ {level}" : string.Empty) +
                                  " • class/race restrictions are displayed, not guessed";
        if (rows.Length > 0 && _itemGrid.SelectedItem is null)
            _itemGrid.SelectedIndex = 0;
    }

    private void RefreshItemDetails()
    {
        if (_itemGrid.SelectedItem is not MudPlayWorkshopItem item)
        {
            _itemDetails.Text = string.Empty;
            return;
        }

        _itemDetails.Text =
            $"{item.Name}\n" +
            $"ID: {item.Id}    Type: {item.ItemType}    Slot: {(string.IsNullOrWhiteSpace(item.Slot) ? "—" : item.Slot)}\n" +
            $"Damage: {item.Damage}    AC: {item.ArmorClassBonus:+#;-#;0}    DR: {item.DamageResistanceBonus:+#;-#;0}    Accuracy: {item.Accuracy:+#;-#;0}    Speed: {item.Speed:+#;-#;0}\n" +
            $"Weight: {item.Weight:N0}    Cost: {item.Cost:N0}\n" +
            $"Restrictions: {item.Restrictions}";
    }

    private void RefreshProgressPlanner()
    {
        var elapsed = Session.ConnectedAt is { } connectedAt
            ? DateTimeOffset.UtcNow - connectedAt
            : TimeSpan.Zero;
        var hours = Math.Max(0d, elapsed.TotalHours);
        var rate = hours > 0.01 ? Session.SessionExperienceGained / hours : 0d;

        string eta;
        if (Session.ExperienceRemaining <= 0)
            eta = "Next-level remainder has not been captured.";
        else if (rate <= 0.01)
            eta = "ETA unavailable until this session has a measurable XP rate.";
        else
        {
            var etaHours = Session.ExperienceRemaining / rate;
            eta = $"Estimated time at current session rate: {FormatDuration(TimeSpan.FromHours(etaHours))}.";
        }

        _progressSummary.Text =
            $"Level: {Session.CharacterLevel:N0}\n" +
            $"Current XP: {Session.ExperienceCurrent:N0}\n" +
            $"XP needed/threshold captured: {Session.ExperienceNeeded:N0}\n" +
            $"XP remaining: {Session.ExperienceRemaining:N0}\n" +
            $"Session XP gained: {Session.SessionExperienceGained:N0}\n" +
            $"Observed session XP/hour: {rate:N0}\n" +
            eta + "\n\n" +
            "Projection uses observed Reforged session state only; it does not invent future-level XP formulas.";
    }

    private void RefreshTrialLoadout()
    {
        if (_catalog is null)
        {
            _trialLoadoutOutput.Text = "Load effective game data to use the loadout sandbox.";
            return;
        }

        var current = Session.EquippedItems
            .Select(text => _catalog.FindByDisplayName(text))
            .Where(item => item is not null)
            .Cast<MudPlayWorkshopItem>()
            .ToArray();
        var requestedNames = _trialLoadoutInput.Text
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var trial = new List<MudPlayWorkshopItem>();
        var unresolved = new List<string>();
        foreach (var name in requestedNames)
        {
            var item = _catalog.FindByDisplayName(name);
            if (item is null) unresolved.Add(name); else trial.Add(item);
        }

        static long SumWeight(IEnumerable<MudPlayWorkshopItem> rows) => rows.Sum(item => Math.Max(0, item.Weight));
        static int SumAc(IEnumerable<MudPlayWorkshopItem> rows) => rows.Sum(item => item.ArmorClassBonus);
        static int SumDr(IEnumerable<MudPlayWorkshopItem> rows) => rows.Sum(item => item.DamageResistanceBonus);
        static int SumMinDamage(IEnumerable<MudPlayWorkshopItem> rows) => rows.Sum(item => Math.Max(0, item.MinimumDamage));
        static int SumMaxDamage(IEnumerable<MudPlayWorkshopItem> rows) => rows.Sum(item => Math.Max(0, item.MaximumDamage));

        var lines = new List<string>
        {
            "CATALOGUED LOADOUT COMPARISON",
            string.Empty,
            $"Captured equipment resolved: {current.Length:N0}/{Session.EquippedItems.Count:N0}",
            $"Trial items resolved:        {trial.Count:N0}/{requestedNames.Length:N0}",
            string.Empty,
            "Metric                 Captured       Trial       Difference",
            "--------------------  ------------  ----------  ------------",
            $"Item weight           {SumWeight(current),12:N0}  {SumWeight(trial),10:N0}  {SumWeight(trial)-SumWeight(current),12:+#;-#;0}",
            $"AC bonus              {SumAc(current),12:N0}  {SumAc(trial),10:N0}  {SumAc(trial)-SumAc(current),12:+#;-#;0}",
            $"DR bonus              {SumDr(current),12:N0}  {SumDr(trial),10:N0}  {SumDr(trial)-SumDr(current),12:+#;-#;0}",
            $"Min damage fields     {SumMinDamage(current),12:N0}  {SumMinDamage(trial),10:N0}  {SumMinDamage(trial)-SumMinDamage(current),12:+#;-#;0}",
            $"Max damage fields     {SumMaxDamage(current),12:N0}  {SumMaxDamage(trial),10:N0}  {SumMaxDamage(trial)-SumMaxDamage(current),12:+#;-#;0}",
            string.Empty
        };
        var aboveLevel = trial.Where(item => item.MinimumLevel > 0 && Session.CharacterLevel > 0 && item.MinimumLevel > Session.CharacterLevel).ToArray();
        if (aboveLevel.Length > 0) lines.Add("Above captured level: " + string.Join(", ", aboveLevel.Select(item => $"{item.Name} (L{item.MinimumLevel})")));
        if (unresolved.Count > 0) lines.Add("Unresolved names: " + string.Join(", ", unresolved));
        lines.Add("This sandbox compares decoded item fields only. It does not infer unproven stat formulas, proc effects, class/race eligibility, or whether simultaneous slot choices are legal.");
        _trialLoadoutOutput.Text = string.Join(Environment.NewLine, lines);
    }

    private void RefreshTimersProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = Session.ConnectedAt is { } connected ? now - connected : TimeSpan.Zero;
        var rate = elapsed.TotalMinutes >= 1
            ? Session.SessionExperienceGained / Math.Max(elapsed.TotalHours, 1d / 60d)
            : 0d;
        var remaining = Math.Max(0L, Session.ExperienceRemaining > 0
            ? Session.ExperienceRemaining
            : Session.ExperienceNeeded > Session.ExperienceCurrent
                ? Session.ExperienceNeeded - Session.ExperienceCurrent
                : 0L);
        var levelEta = rate > 0 && remaining > 0
            ? FormatDuration(TimeSpan.FromHours(remaining / rate))
            : "unavailable";

        var lines = new List<string>
        {
            "LEVEL / SESSION PROJECTION",
            $"Level: {Session.CharacterLevel:N0}",
            $"Session XP gained: {Session.SessionExperienceGained:N0}",
            $"Observed XP/hour: {rate:N0}",
            $"XP remaining: {remaining:N0}",
            $"ETA at current observed rate: {levelEta}",
            $"Lives captured: {Session.Lives:N0}",
            string.Empty,
            "BOSS / RESPAWN TIMERS"
        };

        if (Session.BossHomes.Count == 0)
            lines.Add("No authoritative boss-home snapshot captured yet.");
        else
        {
            foreach (var boss in Session.BossHomes.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
            {
                var countdown = boss.RespawnAtUtc is not { } deadline
                    ? "respawn unknown"
                    : deadline <= now
                        ? "respawn due"
                        : $"respawn in {FormatDuration(deadline - now)}";
                lines.Add($"{boss.Name,-28} {boss.State,-12} {countdown}  home={boss.HomeRoom}  current={boss.CurrentRoom}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("CURRENT INSPECTION TIMERS");
        var dead = Session.InspectedRoom?.Npcs.Where(npc => !npc.Alive).ToArray() ?? Array.Empty<ClientRoomInspectionNpc>();
        if (dead.Length == 0)
            lines.Add("No dead NPC respawn deadlines are present in the current inspected-room snapshot.");
        else
        {
            foreach (var npc in dead.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
            {
                var countdown = npc.RespawnAtUtc is not { } deadline
                    ? "unknown"
                    : deadline <= now ? "due" : FormatDuration(deadline - now);
                lines.Add($"{npc.Name,-28} respawn {countdown}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Projection is observation-based. Reforged does not invent missing respawn deadlines, level formulas, quest timers, or death timers.");
        _timersProjection.Text = string.Join(Environment.NewLine, lines);
    }

    private void RefreshCpPlan()
    {
        var planned = 0;
        foreach (var input in _cpPlanInputs.Values)
        {
            if (int.TryParse(input.Text.Trim(), out var value) && value > 0)
                planned += value;
        }

        var remaining = Session.CharacterPoints - planned;
        _cpSummary.Text = remaining >= 0
            ? $"Available CP: {Session.CharacterPoints:N0} • planned: {planned:N0} • unassigned: {remaining:N0}"
            : $"Available CP: {Session.CharacterPoints:N0} • planned: {planned:N0} • over budget by {-remaining:N0}";
        _cpSummary.Foreground = remaining >= 0 ? Brushes.DarkGreen : Brushes.DarkRed;
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalDays >= 1)
            return $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m";
        if (value.TotalHours >= 1)
            return $"{(int)value.TotalHours}h {value.Minutes}m";
        return $"{Math.Max(0, value.Minutes)}m";
    }

    private static string BuildPropertyReport(object source, IEnumerable<string> properties)
    {
        var type = source.GetType();
        var lines = new List<string>();

        foreach (var name in properties)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.GetIndexParameters().Length != 0)
                continue;

            object? value;
            try { value = property.GetValue(source); }
            catch { continue; }

            lines.Add($"{SplitName(name),-24} {FormatValue(value)}");
        }

        return lines.Count == 0
            ? "No compatible live character properties were exposed by this build."
            : string.Join(Environment.NewLine, lines);
    }

    private static string BuildCollectionReport(object source, IEnumerable<string> candidates)
    {
        var type = source.GetType();
        foreach (var name in candidates)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.GetIndexParameters().Length != 0)
                continue;

            object? value;
            try { value = property.GetValue(source); }
            catch { continue; }

            if (value is string)
                return value.ToString() ?? string.Empty;

            if (value is not IEnumerable sequence)
                continue;

            var rows = sequence.Cast<object?>().ToArray();
            if (rows.Length == 0)
                return "No entries captured yet.";

            return string.Join(Environment.NewLine, rows.Select(FormatRecord));
        }

        return "This state collection is not exposed by the current Reforged session model yet.";
    }

    private static string FormatRecord(object? item)
    {
        if (item is null)
            return "(null)";
        if (item is string text)
            return text;

        var properties = TypeDescriptor.GetProperties(item)
            .Cast<PropertyDescriptor>()
            .Where(property => property.IsBrowsable)
            .Take(10)
            .Select(property =>
            {
                object? value;
                try { value = property.GetValue(item); }
                catch { value = null; }
                return $"{SplitName(property.Name)}={FormatValue(value)}";
            })
            .ToArray();

        return properties.Length == 0
            ? item.ToString() ?? item.GetType().Name
            : string.Join("   ", properties);
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "—",
        DateTimeOffset date => date.LocalDateTime.ToString("g"),
        DateTime date => date.ToString("g"),
        bool flag => flag ? "Yes" : "No",
        _ => value.ToString() ?? "—"
    };

    private static string SplitName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var result = new System.Text.StringBuilder(value.Length + 8);
        result.Append(value[0]);
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                result.Append(' ');
            result.Append(value[i]);
        }
        return result.ToString();
    }
}
