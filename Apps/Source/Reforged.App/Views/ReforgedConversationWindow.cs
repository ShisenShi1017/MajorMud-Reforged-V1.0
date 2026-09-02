using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Reforged.App.Models;
using Reforged.App.Services;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

/// <summary>
/// Reforged communications desk: a searchable merged view over the session's already-classified
/// terminal channels. It does not replace the terminal or introduce a second chat parser.
/// </summary>
public sealed class ReforgedConversationWindow : Window
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly ObservableCollection<ReforgedConversationRow> _rows = [];
    private readonly DataGrid _grid;
    private readonly TextBox _search;
    private readonly ComboBox _channel;
    private readonly CheckBox _incoming;
    private readonly CheckBox _outgoing;
    private readonly CheckBox _logging;
    private readonly CheckBox _recorded;
    private readonly ObservableCollection<ReforgedConversationRow> _historyRows = [];
    private readonly TextBox _input;
    private readonly TextBlock _summary;

    public ReforgedConversationWindow(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));

        Title = $"Communications Desk — {DisplayCharacter(session)}";
        Width = 930;
        Height = 650;
        MinWidth = 700;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock { Text = "Reforged Communications Desk", FontSize = 18, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = "Merged chat/tell/party/system history from the active terminal session. Search and channel filters are local-only; sending uses the normal Reforged command path so aliases and safety rules still apply.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var filters = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _search = new TextBox { ToolTip = "Search visible communications", Margin = new Thickness(0, 0, 6, 0) };
        _channel = new ComboBox
        {
            ItemsSource = new[] { "All", "Chat", "Say", "Yell", "Gossip", "Auction", "Broadcast", "Gangpath", "Tell", "Party", "System" },
            SelectedIndex = 0,
            Margin = new Thickness(0, 0, 6, 0)
        };
        _incoming = new CheckBox { Content = "Incoming", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
        _outgoing = new CheckBox { Content = "Outgoing", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
        _logging = new CheckBox { Content = "Live log", IsChecked = session.TerminalPreferences.LoggingEnabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0), ToolTip = "Persist terminal entries using Reforged's existing per-character daily log." };
        _recorded = new CheckBox { Content = "Recorded", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Include historical rows loaded from a saved Reforged terminal log." };
        Grid.SetColumn(_channel, 1); Grid.SetColumn(_incoming, 2); Grid.SetColumn(_outgoing, 3); Grid.SetColumn(_logging, 4); Grid.SetColumn(_recorded, 5);
        filters.Children.Add(_search); filters.Children.Add(_channel); filters.Children.Add(_incoming); filters.Children.Add(_outgoing); filters.Children.Add(_logging); filters.Children.Add(_recorded);
        DockPanel.SetDock(filters, Dock.Top);
        root.Children.Add(filters);

        var historyTools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var loadToday = Button("Load today's history", 135);
        var openHistory = Button("Open history...", 105);
        var clearHistory = Button("Clear loaded", 95);
        historyTools.Children.Add(loadToday);
        historyTools.Children.Add(openHistory);
        historyTools.Children.Add(clearHistory);
        DockPanel.SetDock(historyTools, Dock.Top);
        root.Children.Add(historyTools);

        _summary = new TextBlock { Foreground = Brushes.DimGray, Margin = new Thickness(2, 0, 0, 6) };
        DockPanel.SetDock(_summary, Dock.Top);
        root.Children.Add(_summary);

        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _input = new TextBox { ToolTip = "Send through the normal Reforged command path", Margin = new Thickness(0, 0, 6, 0) };
        var send = Button("Send", 70);
        var copy = Button("Copy row", 80);
        var export = Button("Export visible...", 110);
        Grid.SetColumn(send, 1); Grid.SetColumn(copy, 2); Grid.SetColumn(export, 3);
        footer.Children.Add(_input); footer.Children.Add(send); footer.Children.Add(copy); footer.Children.Add(export);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        _grid = new DataGrid
        {
            ItemsSource = _rows,
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Time", Binding = new Binding(nameof(ReforgedConversationRow.Timestamp)) { StringFormat = "HH:mm:ss" }, Width = 82 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Channel", Binding = new Binding(nameof(ReforgedConversationRow.Channel)), Width = 95 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Direction", Binding = new Binding(nameof(ReforgedConversationRow.Direction)), Width = 82 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Message", Binding = new Binding(nameof(ReforgedConversationRow.Text)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        root.Children.Add(_grid);
        Content = root;

        _search.TextChanged += (_, _) => Refresh();
        _channel.SelectionChanged += (_, _) => Refresh();
        _incoming.Checked += (_, _) => Refresh(); _incoming.Unchecked += (_, _) => Refresh();
        _outgoing.Checked += (_, _) => Refresh(); _outgoing.Unchecked += (_, _) => Refresh();
        _logging.Checked += async (_, _) => await ToggleLoggingAsync(true);
        _logging.Unchecked += async (_, _) => await ToggleLoggingAsync(false);
        _recorded.Checked += (_, _) => Refresh(); _recorded.Unchecked += (_, _) => Refresh();
        loadToday.Click += async (_, _) => await LoadHistoryAsync(ReforgedParityCompletionService.GetDailyConversationLogPath(Session));
        openHistory.Click += async (_, _) => await OpenHistoryAsync();
        clearHistory.Click += (_, _) => { _historyRows.Clear(); Refresh(); };
        send.Click += async (_, _) => await SendAsync();
        _input.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await SendAsync(); } };
        copy.Click += (_, _) => CopySelected();
        export.Click += async (_, _) => await ExportVisibleAsync();

        NotifyCollectionChangedEventHandler changed = (_, _) => Dispatcher.BeginInvoke(new Action(Refresh));
        Session.TerminalEntries.CollectionChanged += changed;
        Closed += (_, _) => Session.TerminalEntries.CollectionChanged -= changed;
        Refresh();
    }

    private void Refresh()
    {
        var live = ReforgedParityCompletionService.FilterConversation(
            Session,
            _search.Text,
            _channel.SelectedItem?.ToString(),
            _incoming.IsChecked == true,
            _outgoing.IsChecked == true);
        var needle = _search.Text.Trim();
        var selectedChannel = _channel.SelectedItem?.ToString() ?? "All";
        var history = _recorded.IsChecked == true
            ? _historyRows
                .Where(row => selectedChannel.Equals("All", StringComparison.OrdinalIgnoreCase) || row.Channel.Equals(selectedChannel, StringComparison.OrdinalIgnoreCase))
                .Where(row => needle.Length == 0 || row.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            : Enumerable.Empty<ReforgedConversationRow>();
        var combined = live.Concat(history).OrderBy(row => row.Timestamp).TakeLast(10000).ToArray();
        var selected = (_grid.SelectedItem as ReforgedConversationRow)?.Timestamp;
        _rows.Clear();
        foreach (var row in combined) _rows.Add(row);
        if (selected is { } stamp) _grid.SelectedItem = _rows.FirstOrDefault(row => row.Timestamp == stamp);
        _summary.Text = $"Showing {_rows.Count:N0} entries · live terminal rows {Session.TerminalEntries.Count:N0} · loaded history {_historyRows.Count:N0} · logging {(Session.TerminalPreferences.LoggingEnabled ? "on" : "off")}";
        if (_rows.Count > 0) Dispatcher.BeginInvoke(new Action(() => { if (_rows.Count > 0) _grid.ScrollIntoView(_rows[^1]); }));
    }

    private async Task OpenHistoryAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Reforged communications history",
            Filter = "Reforged logs (*.log)|*.log|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(ReforgedParityCompletionService.ConversationHistoryFolder)
                ? ReforgedParityCompletionService.ConversationHistoryFolder
                : null
        };
        if (dialog.ShowDialog(this) != true) return;
        await LoadHistoryAsync(dialog.FileName);
    }

    private async Task LoadHistoryAsync(string path)
    {
        try
        {
            var loaded = await ReforgedParityCompletionService.ReadConversationHistoryAsync(path);
            _historyRows.Clear();
            foreach (var row in loaded) _historyRows.Add(row);
            Refresh();
            _summary.Text += $" · history: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "History could not be loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ToggleLoggingAsync(bool enabled)
    {
        Session.TerminalPreferences.LoggingEnabled = enabled;
        await _host.SaveConnectionProfileAsync(Session);
        Refresh();
    }

    private async Task SendAsync()
    {
        var value = _input.Text.Trim();
        if (value.Length == 0) return;
        _input.Clear();
        await _host.SendTerminalCommandAsync(Session, value);
    }

    private void CopySelected()
    {
        if (_grid.SelectedItem is not ReforgedConversationRow row) return;
        Clipboard.SetText($"[{row.Timestamp:HH:mm:ss}] [{row.Channel}] {row.Text}");
    }

    private async Task ExportVisibleAsync()
    {
        var safe = string.Concat(DisplayCharacter(Session).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var dialog = new SaveFileDialog
        {
            Title = "Export visible communications",
            Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"{safe}-communications-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dialog.ShowDialog(this) != true) return;
        var sb = new StringBuilder();
        foreach (var row in _rows) sb.AppendLine($"[{row.Timestamp:O}] [{row.Channel}] [{row.Direction}] {row.Text}");
        await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), new UTF8Encoding(false));
        _summary.Text = $"Exported {_rows.Count:N0} visible entries to {dialog.FileName}";
    }

    private static Button Button(string text, double width) => new() { Content = text, Width = width, Margin = new Thickness(3, 0, 0, 0), Padding = new Thickness(6, 3, 6, 3) };
    private static string DisplayCharacter(ClientSessionConnection session) => string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;
}
