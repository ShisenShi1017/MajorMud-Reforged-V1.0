using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RealmGate.App.Models;
using RealmGate.App.Services;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

/// <summary>
/// The one player-facing Librarium surface used by the RealmGate client.
/// RealmGate-hosted worlds read the server-selected authoritative catalog.
/// External BBS sessions read RealmGate Classic plus that BBS's local MDB delta.
/// This window is read-only; world/data editing belongs in Creator Studio.
/// </summary>
public sealed class RealmGateClientLibrariumWindow : Window
{
    private readonly PlayViewModel _host;
    private readonly ClientSessionConnection _session;
    private readonly RealmGateClientGameDataOverlayService _overlay = new();
    private readonly RealmGateGameDataContext _context;
    private readonly ComboBox _family = new();
    private readonly TextBox _search = new();
    private readonly ListBox _records = new();
    private readonly TextBox _details = new();
    private readonly TextBlock _authority = new();
    private readonly TextBlock _status = new();
    private readonly Button _previous = NewButton("Previous", 76);
    private readonly Button _next = NewButton("Next", 64);
    private readonly Button _refresh = NewButton("Refresh", 78);
    private readonly DispatcherTimer _timer;
    private bool _updatingFamilies;
    private int _requestedOffset;
    private string _requestedFamily = string.Empty;
    private string _requestedQuery = string.Empty;

    public RealmGateClientLibrariumWindow(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _context = _overlay.ResolveContext(session);

        Title = $"RealmGate Librarium — {session.DisplayName}";
        Width = 1080;
        Height = 700;
        MinWidth = 760;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        Content = BuildContent();
        ConfigureSource();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshServerState();
        Closed += (_, _) => _timer.Stop();

        if (_context.Mode == RealmGateGameDataAuthorityMode.ReforgedWorld)
        {
            _timer.Start();
            _ = _host.SendClientProtocolCommandAsync(_session, "librarium");
        }
        else
        {
            LoadExternalFamily();
        }
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });

        _authority.TextWrapping = TextWrapping.Wrap;
        _authority.FontWeight = FontWeights.SemiBold;
        _authority.Margin = new Thickness(0, 0, 0, 8);
        Grid.SetColumnSpan(_authority, 3);
        root.Children.Add(_authority);

        var controls = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        controls.ColumnDefinitions.Add(new ColumnDefinition());
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
        Grid.SetRow(controls, 1);
        Grid.SetColumnSpan(controls, 3);

        _family.MinHeight = 28;
        _family.SelectionChanged += FamilyChanged;
        _search.MinHeight = 28;
        _search.VerticalContentAlignment = VerticalAlignment.Center;
        _search.ToolTip = "Search the selected Librarium family by name, ID, or record text.";
        _search.KeyDown += SearchKeyDown;
        Grid.SetColumn(_search, 2);
        var searchButton = NewButton("Search", 78);
        searchButton.Click += async (_, _) => await SearchAsync();
        Grid.SetColumn(searchButton, 3);
        controls.Children.Add(_family);
        controls.Children.Add(_search);
        controls.Children.Add(searchButton);
        root.Children.Add(controls);

        _records.Background = Brushes.White;
        _records.Foreground = Brushes.Black;
        _records.SelectionChanged += RecordChanged;
        Grid.SetRow(_records, 2);
        root.Children.Add(_records);

        _details.IsReadOnly = true;
        _details.AcceptsReturn = true;
        _details.TextWrapping = TextWrapping.Wrap;
        _details.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _details.Background = Brushes.White;
        _details.Foreground = Brushes.Black;
        _details.FontFamily = new FontFamily("Consolas");
        _details.FontSize = 12;
        _details.Padding = new Thickness(8);
        Grid.SetRow(_details, 2);
        Grid.SetColumn(_details, 2);
        root.Children.Add(_details);

        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        Grid.SetRow(footer, 3);
        Grid.SetColumnSpan(footer, 3);
        var close = NewButton("Close", 70);
        close.Click += (_, _) => Close();
        _refresh.Click += RefreshClicked;
        _previous.Click += async (_, _) => await RequestServerPageAsync(Math.Max(0, _requestedOffset - (_session.LibrariumPage?.PageSize ?? 25)));
        _next.Click += async (_, _) => await RequestServerPageAsync(_requestedOffset + (_session.LibrariumPage?.PageSize ?? 25));
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(_next, Dock.Right);
        DockPanel.SetDock(_previous, Dock.Right);
        DockPanel.SetDock(_refresh, Dock.Right);
        footer.Children.Add(close);
        footer.Children.Add(_next);
        footer.Children.Add(_previous);
        footer.Children.Add(_refresh);
        footer.Children.Add(_status);
        root.Children.Add(footer);

        return root;
    }

    private void ConfigureSource()
    {
        if (_context.Mode == RealmGateGameDataAuthorityMode.ExternalBbs)
        {
            _authority.Text = $"External BBS · Read-only effective data: RealmGate Classic + local MDB differences for {_session.ServerProfileName} ({_session.HostName}:{_session.Port}).";
            _family.ItemsSource = _overlay.Families.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            _family.SelectedIndex = _family.Items.Count > 0 ? 0 : -1;
            _previous.Visibility = Visibility.Collapsed;
            _next.Visibility = Visibility.Collapsed;
            _refresh.Content = "Reload";
            return;
        }

        _authority.Text = $"RealmGate server authority · {_context.BaseWorld}. Librarium records come from the server-selected world projection and cannot be edited here.";
        _family.DisplayMemberPath = nameof(ClientServerCatalogFamily.DisplayName);
        _previous.Visibility = Visibility.Visible;
        _next.Visibility = Visibility.Visible;
        RefreshServerState();
    }

    private async void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        await SearchAsync();
    }

    private async void FamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFamilies)
            return;

        _requestedOffset = 0;
        _records.ItemsSource = null;
        _details.Clear();
        if (_context.Mode == RealmGateGameDataAuthorityMode.ExternalBbs)
            LoadExternalFamily();
        else if (_session.ConnectionState == ClientConnectionState.Connected)
            await RequestServerPageAsync(0);
    }

    private async void RefreshClicked(object sender, RoutedEventArgs e)
    {
        if (_context.Mode == RealmGateGameDataAuthorityMode.ExternalBbs)
        {
            LoadExternalFamily();
            return;
        }

        await _host.SendClientProtocolCommandAsync(_session, "librarium");
        await RequestServerPageAsync(_requestedOffset);
    }

    private Task SearchAsync()
    {
        if (_context.Mode == RealmGateGameDataAuthorityMode.ExternalBbs)
        {
            LoadExternalFamily();
            return Task.CompletedTask;
        }

        return RequestServerPageAsync(0);
    }

    private void LoadExternalFamily()
    {
        if (_family.SelectedItem is not string family)
        {
            _records.ItemsSource = null;
            _status.Text = "Select a Librarium family.";
            return;
        }

        var query = _search.Text.Trim();
        var records = _overlay.LoadFamily(_session, family)
            .Where(record => query.Length == 0 ||
                record.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                record.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                record.EffectiveRecord.ToJsonString().Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(5000)
            .ToArray();

        _records.DisplayMemberPath = nameof(RealmGateGameDataRecord.DisplayName);
        _records.ItemsSource = records;
        _records.SelectedIndex = records.Length > 0 ? 0 : -1;
        _status.Text = $"{family} · {records.Length:N0} matching effective record(s) · read-only";
    }

    private void RefreshServerState()
    {
        if (_context.Mode != RealmGateGameDataAuthorityMode.ReforgedWorld)
            return;

        var selected = (_family.SelectedItem as ClientServerCatalogFamily)?.Name;
        var families = _session.ServerCatalogFamilies
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _updatingFamilies = true;
        try
        {
            _family.ItemsSource = families;
            if (selected is not null)
                _family.SelectedItem = families.FirstOrDefault(item => item.Name.Equals(selected, StringComparison.OrdinalIgnoreCase));
            if (_family.SelectedItem is null && families.Length > 0)
                _family.SelectedIndex = 0;
        }
        finally
        {
            _updatingFamilies = false;
        }

        if (_session.ConnectionState != ClientConnectionState.Connected)
        {
            _status.Text = "Disconnected — reconnect to request the authoritative RealmGate Librarium.";
            return;
        }

        if (_session.LibrariumPage is { } page &&
            page.Family.Equals(_requestedFamily, StringComparison.OrdinalIgnoreCase) &&
            page.Query.Equals(_requestedQuery, StringComparison.Ordinal) &&
            page.Offset == _requestedOffset)
        {
            _records.DisplayMemberPath = nameof(ClientLibrariumRecord.Name);
            _records.ItemsSource = page.Records;
            _previous.IsEnabled = page.Offset > 0;
            _next.IsEnabled = page.Offset + page.Records.Count < page.TotalCount;
            _status.Text = $"{_session.ServerCatalogWorld} · showing {page.Offset + 1:N0}-{page.Offset + page.Records.Count:N0} of {page.TotalCount:N0}";
        }
        else if (_session.ServerCatalogUpdatedAt is null)
        {
            _status.Text = "Waiting for the RealmGate server catalog.";
        }
    }

    private async Task RequestServerPageAsync(int offset)
    {
        if (_family.SelectedItem is not ClientServerCatalogFamily selected || _session.ConnectionState != ClientConnectionState.Connected)
            return;

        _requestedFamily = selected.Name;
        _requestedQuery = _search.Text.Trim();
        if (_requestedQuery.Length > 80)
            _requestedQuery = _requestedQuery[..80];
        _requestedOffset = Math.Max(0, offset);
        await _host.SendClientProtocolCommandAsync(
            _session,
            $"librarium page {_requestedFamily} {_requestedOffset}" + (_requestedQuery.Length > 0 ? $" {_requestedQuery}" : string.Empty));
    }

    private void RecordChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_context.Mode == RealmGateGameDataAuthorityMode.ExternalBbs)
        {
            if (_records.SelectedItem is not RealmGateGameDataRecord record)
            {
                _details.Clear();
                return;
            }

            var fields = _overlay.FlattenFields(_session, record.Family, record);
            var builder = new StringBuilder();
            builder.AppendLine(record.DisplayName);
            builder.AppendLine($"ID: {record.Id}");
            builder.AppendLine($"Family: {record.Family}");
            if (record.HasOverrides)
                builder.AppendLine($"BBS-specific fields: {record.OverrideCount}");
            builder.AppendLine();
            foreach (var field in fields)
                builder.AppendLine($"{field.Path}: {field.Value}{(field.IsOverridden ? "  [BBS]" : string.Empty)}");
            _details.Text = builder.ToString();
            return;
        }

        if (_records.SelectedItem is not ClientLibrariumRecord serverRecord)
        {
            _details.Clear();
            return;
        }

        _details.Text = FormatServerRecord(serverRecord);
    }

    private static string FormatServerRecord(ClientLibrariumRecord record)
    {
        try
        {
            using var document = JsonDocument.Parse(record.Summary);
            return $"{record.Name}\nID: {record.Id}\n\n" + JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return $"{record.Name}\nID: {record.Id}\n\n{record.Summary}";
        }
    }

    private static Button NewButton(string text, double width) => new()
    {
        Content = text,
        Width = width,
        Height = 28,
        Margin = new Thickness(4, 0, 0, 0)
    };
}
