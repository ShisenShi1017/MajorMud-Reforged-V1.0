using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App.Views;

/// <summary>
/// Reforged creature-reference surface built from the effective world/BBS overlay plus the
/// live room snapshot. It deliberately exposes evidence rather than inventing a universal
/// "safe to fight" formula.
/// </summary>
public sealed class ReforgedCreatureIntelWindow : Window
{
    public ClientSessionConnection Session { get; }
    private readonly ObservableCollection<ReforgedCreatureIntelEntry> _rows = [];
    private IReadOnlyList<ReforgedCreatureIntelEntry> _all = [];
    private RealmGateWorkshopDataCatalog? _itemCatalog;
    private readonly TextBox _search = new() { MinWidth = 220 };
    private readonly TextBox _minLevel = new() { Width = 55, Text = "0" };
    private readonly TextBox _maxLevel = new() { Width = 55, Text = "999" };
    private readonly CheckBox _hereOnly = new() { Content = "Current room only" };
    private readonly CheckBox _undeadOnly = new() { Content = "Undead only" };
    private readonly TextBlock _summary = new();
    private readonly DataGrid _grid;
    private readonly TextBox _details;

    public ReforgedCreatureIntelWindow(ClientSessionConnection session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Title = $"Creature Intel — {DisplayCharacter(session)}";
        Width = 1180;
        Height = 720;
        MinWidth = 850;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        var root = new DockPanel { Margin = new Thickness(12) };
        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        head.Children.Add(new TextBlock { Text = "Creature Intel", FontSize = 18, FontWeight = FontWeights.SemiBold });
        head.Children.Add(new TextBlock
        {
            Text = "Search the effective RealmGate/Classic+BBS creature catalog and compare decoded combat values with the current character. Live room matches are highlighted by the Current column.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray
        });
        DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);

        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        filters.Children.Add(Label("Search", _search));
        filters.Children.Add(Label("Min level", _minLevel));
        filters.Children.Add(Label("Max level", _maxLevel));
        _hereOnly.Margin = new Thickness(12, 5, 8, 0); _undeadOnly.Margin = new Thickness(6, 5, 8, 0);
        filters.Children.Add(_hereOnly); filters.Children.Add(_undeadOnly);
        var reload = Button("Reload effective data", 130); filters.Children.Add(reload);
        DockPanel.SetDock(filters, Dock.Top); root.Children.Add(filters);
        _summary.Margin = new Thickness(2, 0, 0, 6); _summary.Foreground = Brushes.DimGray;
        DockPanel.SetDock(_summary, Dock.Top); root.Children.Add(_summary);

        var split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.38, GridUnitType.Star) });
        _grid = new DataGrid
        {
            ItemsSource = _rows, IsReadOnly = true, AutoGenerateColumns = false,
            CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.Name)), Width = new DataGridLength(1.8, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Lvl", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.Level)), Width = 52 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "HP", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.HitPoints)), Width = 72 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "AC", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.ArmorClass)), Width = 55 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "DR", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.DamageResistance)), Width = 55 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "MR", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.MagicResistance)), Width = 55 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "XP", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.ExperienceReward)), Width = 78 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Max hit", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.MaximumSingleHit)), Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Current", Binding = new Binding(nameof(ReforgedCreatureIntelEntry.LiveState)), Width = 120 });
        split.Children.Add(_grid);

        _details = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(8)
        };
        Grid.SetColumn(_details, 2); split.Children.Add(_details);
        root.Children.Add(split);
        Content = root;

        _search.TextChanged += (_, _) => Filter();
        _minLevel.TextChanged += (_, _) => Filter();
        _maxLevel.TextChanged += (_, _) => Filter();
        _hereOnly.Checked += (_, _) => Filter(); _hereOnly.Unchecked += (_, _) => Filter();
        _undeadOnly.Checked += (_, _) => Filter(); _undeadOnly.Unchecked += (_, _) => Filter();
        _grid.SelectionChanged += (_, _) => RefreshDetails();
        reload.Click += (_, _) => Load();
        Load();
    }

    private void Load()
    {
        try
        {
            _all = ReforgedParityCompletionService.LoadCreatureIntel(Session);
            try { _itemCatalog = RealmGateWorkshopDataCatalog.Load(Session); } catch { _itemCatalog = null; }
            Filter();
        }
        catch (Exception ex)
        {
            _all = [];
            _rows.Clear();
            _summary.Text = "Creature data could not be loaded: " + ex.Message;
            _details.Text = string.Empty;
        }
    }

    private void Filter()
    {
        var query = _search.Text.Trim();
        _ = int.TryParse(_minLevel.Text, out var min); min = Math.Max(0, min);
        _ = int.TryParse(_maxLevel.Text, out var max); if (max <= 0) max = int.MaxValue;
        IEnumerable<ReforgedCreatureIntelEntry> rows = _all.Where(row => row.Level >= min && row.Level <= max);
        if (query.Length > 0) rows = rows.Where(row => row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || row.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || row.Abilities.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase)));
        if (_hereOnly.IsChecked == true) rows = rows.Where(row => row.IsInCurrentRoom);
        if (_undeadOnly.IsChecked == true) rows = rows.Where(row => row.Undead);
        var selectedId = (_grid.SelectedItem as ReforgedCreatureIntelEntry)?.Id;
        _rows.Clear();
        foreach (var row in rows.Take(5000)) _rows.Add(row);
        if (selectedId is not null) _grid.SelectedItem = _rows.FirstOrDefault(row => row.Id == selectedId);
        if (_grid.SelectedItem is null && _rows.Count > 0) _grid.SelectedIndex = 0;
        var here = _all.Count(row => row.IsInCurrentRoom);
        _summary.Text = $"Showing {_rows.Count:N0} of {_all.Count:N0} decoded creatures · {here:N0} matched to the current room · effective world {Session.WorldName}";
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (_grid.SelectedItem is not ReforgedCreatureIntelEntry creature)
        {
            _details.Text = "Select a creature to inspect decoded combat evidence.";
            return;
        }
        var text = ReforgedParityCompletionService.RenderCreatureDetails(creature, Session);
        if (_itemCatalog is not null)
        {
            var weapons = Session.EquippedItems
                .Select(value => _itemCatalog.FindByDisplayName(value))
                .Where(item => item is not null && (item.MinimumDamage > 0 || item.MaximumDamage > 0))
                .Cast<RealmGateWorkshopItem>()
                .OrderByDescending(item => item.MaximumDamage)
                .ThenByDescending(item => item.MinimumDamage)
                .ToArray();
            if (weapons.Length > 0)
            {
                var weapon = weapons[0];
                var rawAverage = (Math.Max(0, weapon.MinimumDamage) + Math.Max(0, weapon.MaximumDamage)) / 2d;
                var rawRounds = rawAverage > 0 && creature.HitPoints > 0 ? (int)Math.Ceiling(creature.HitPoints / rawAverage) : 0;
                text += $"\n\nCurrent decoded weapon projection\n  {weapon.Name}: {weapon.Damage} raw damage\n  Raw average: {rawAverage:0.0}" +
                        (rawRounds > 0 ? $"\n  Raw HP / average-damage rounds: {rawRounds:N0}" : string.Empty) +
                        "\n  This is intentionally pre-accuracy/pre-DR and is not a kill-time promise.";
            }
        }
        _details.Text = text;
    }

    private static FrameworkElement Label(string text, UIElement control)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(new TextBlock { Text = text + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        panel.Children.Add(control);
        return panel;
    }
    private static Button Button(string text, double width) => new() { Content = text, Width = width, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(6, 3, 6, 3) };
    private static string DisplayCharacter(ClientSessionConnection session) => string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;
}
