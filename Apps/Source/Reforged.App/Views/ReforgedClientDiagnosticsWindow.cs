using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Reforged.App.Models;
using Reforged.App.Services;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

/// <summary>
/// Reforged-native terminal workbench for scrollback, program activity, session statistics,
/// Telnet trace inspection, and per-profile client customization.
/// </summary>
public sealed class ReforgedClientDiagnosticsWindow : Window
{
    private readonly ClientTerminalWindow _terminal;
    private readonly PlayViewModel _host;
    private readonly ClientSessionConnection _session;
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<ClientTerminalEntry> _scrollbackRows = [];
    private readonly TextBox _scrollbackQuery;
    private readonly ComboBox _channelFilter;
    private readonly CheckBox _includeOutgoing;
    private readonly TextBlock _scrollbackSummary;
    private readonly TextBox _sessionStats;
    private readonly TextBox _programSummary;
    private readonly TextBox _wireOutput;
    private readonly TextBlock _wireHeader;
    private bool _wireRefreshing;
    private int _wireTick;

    private readonly ComboBox _fontFamily;
    private readonly Slider _fontSize;
    private readonly Slider _outputScale;
    private readonly CheckBox _showToolbar;
    private readonly CheckBox _showStatusBar;
    private readonly CheckBox _autoFocus;
    private readonly CheckBox _enableHotkeys;
    private readonly CheckBox _enableContext;
    private readonly CheckBox _showTimestamps;
    private readonly CheckBox _loggingEnabled;
    private readonly CheckBox _captureTelnet;
    private readonly TextBox _scrollbackLimit;

    public ReforgedClientDiagnosticsWindow(
        ClientTerminalWindow terminal,
        PlayViewModel host,
        ClientSessionConnection session)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        Title = $"RealmGate Diagnostics & Customization — {DisplayName(session)}";
        Width = 1180;
        Height = 790;
        MinWidth = 860;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        var root = new DockPanel { Margin = new Thickness(10) };
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(35, 43, 52)),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 9),
            CornerRadius = new CornerRadius(4)
        };
        DockPanel.SetDock(header, Dock.Top);
        var headerStack = new StackPanel();
        headerStack.Children.Add(new TextBlock
        {
            Text = "RealmGate Client Workbench",
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = "Searchable scrollback, command/program activity, live session statistics, opt-in Telnet trace inspection, toolbar/hotkey/context-menu customization, and privacy-safe support export.",
            Foreground = new SolidColorBrush(Color.FromRgb(202, 211, 220)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        header.Child = headerStack;
        root.Children.Add(header);

        var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var export = new Button { Content = "Export Privacy-Safe Support Bundle", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4, 0, 0, 0) };
        export.Click += async (_, _) => await ExportSupportBundleAsync();
        DockPanel.SetDock(export, Dock.Right);
        footer.Children.Add(export);
        footer.Children.Add(new TextBlock
        {
            Text = "Raw Telnet capture is opt-in and excluded from the support bundle. Password/sensitive sends remain redacted by the existing transport path.",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.DimGray
        });
        root.Children.Add(footer);

        var tabs = new TabControl();
        root.Children.Add(tabs);

        // Scrollback
        var scrollbackRoot = new Grid { Margin = new Thickness(8) };
        scrollbackRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scrollbackRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scrollbackRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var filters = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        _channelFilter = new ComboBox { Width = 125, Margin = new Thickness(6, 0, 0, 0) };
        _channelFilter.Items.Add("All");
        foreach (var value in Enum.GetValues<ClientTerminalChannel>()) _channelFilter.Items.Add(value.ToString());
        _channelFilter.SelectedIndex = 0;
        _channelFilter.SelectionChanged += (_, _) => RefreshScrollback();
        DockPanel.SetDock(_channelFilter, Dock.Right);
        filters.Children.Add(_channelFilter);
        _includeOutgoing = new CheckBox { Content = "Include outgoing", IsChecked = true, Margin = new Thickness(10, 2, 8, 0) };
        _includeOutgoing.Checked += (_, _) => RefreshScrollback();
        _includeOutgoing.Unchecked += (_, _) => RefreshScrollback();
        DockPanel.SetDock(_includeOutgoing, Dock.Right);
        filters.Children.Add(_includeOutgoing);
        _scrollbackQuery = new TextBox { ToolTip = "Case-insensitive text search across retained terminal entries" };
        _scrollbackQuery.TextChanged += (_, _) => RefreshScrollback();
        filters.Children.Add(_scrollbackQuery);
        scrollbackRoot.Children.Add(filters);
        _scrollbackSummary = new TextBlock { Foreground = Brushes.DimGray, Margin = new Thickness(2, 0, 0, 6) };
        Grid.SetRow(_scrollbackSummary, 1);
        scrollbackRoot.Children.Add(_scrollbackSummary);
        var scrollbackGrid = new DataGrid
        {
            ItemsSource = _scrollbackRows,
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        scrollbackGrid.Columns.Add(new DataGridTextColumn { Header = "Time", Binding = new Binding(nameof(ClientTerminalEntry.Timestamp)) { StringFormat = "HH:mm:ss" }, Width = 85 });
        scrollbackGrid.Columns.Add(new DataGridTextColumn { Header = "Channel", Binding = new Binding(nameof(ClientTerminalEntry.Channel)), Width = 90 });
        scrollbackGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Out", Binding = new Binding(nameof(ClientTerminalEntry.IsOutgoing)), Width = 48 });
        scrollbackGrid.Columns.Add(new DataGridTextColumn { Header = "Text", Binding = new Binding(nameof(ClientTerminalEntry.Text)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.SetRow(scrollbackGrid, 2);
        scrollbackRoot.Children.Add(scrollbackGrid);
        tabs.Items.Add(new TabItem { Header = "Scrollback", Content = scrollbackRoot });

        // Program log
        var programRoot = new Grid { Margin = new Thickness(8) };
        programRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(125) });
        programRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _programSummary = ReadOnlyOutput(wrap: true);
        programRoot.Children.Add(_programSummary);
        var programGrid = new DataGrid
        {
            ItemsSource = _session.ProgramLogEntries,
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            Margin = new Thickness(0, 8, 0, 0)
        };
        programGrid.Columns.Add(new DataGridTextColumn { Header = "Time", Binding = new Binding(nameof(ReforgedClientProgramLogEntry.Timestamp)) { StringFormat = "HH:mm:ss" }, Width = 85 });
        programGrid.Columns.Add(new DataGridTextColumn { Header = "Area", Binding = new Binding(nameof(ReforgedClientProgramLogEntry.Area)), Width = 95 });
        programGrid.Columns.Add(new DataGridTextColumn { Header = "Origin", Binding = new Binding(nameof(ReforgedClientProgramLogEntry.Origin)), Width = 120 });
        programGrid.Columns.Add(new DataGridTextColumn { Header = "Message", Binding = new Binding(nameof(ReforgedClientProgramLogEntry.Message)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.SetRow(programGrid, 1);
        programRoot.Children.Add(programGrid);
        tabs.Items.Add(new TabItem { Header = "Program Log", Content = programRoot });

        _sessionStats = ReadOnlyOutput(wrap: false);
        tabs.Items.Add(new TabItem { Header = "Session Stats", Content = _sessionStats });

        // Wire inspector
        var wireRoot = new Grid { Margin = new Thickness(8) };
        wireRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        wireRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var wireTop = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var refreshWire = new Button { Content = "Refresh", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6, 0, 0, 0) };
        refreshWire.Click += async (_, _) => await RefreshWireAsync();
        DockPanel.SetDock(refreshWire, Dock.Right);
        wireTop.Children.Add(refreshWire);
        _wireHeader = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        wireTop.Children.Add(_wireHeader);
        wireRoot.Children.Add(wireTop);
        _wireOutput = ReadOnlyOutput(wrap: false);
        Grid.SetRow(_wireOutput, 1);
        wireRoot.Children.Add(_wireOutput);
        tabs.Items.Add(new TabItem { Header = "Wire Inspector", Content = wireRoot });

        // Customization
        var presentation = _host.AutomationOptions.Presentation;
        var customRoot = new Grid { Margin = new Thickness(8) };
        customRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        customRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) });
        customRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
        customRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
        customRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var presentationBox = new GroupBox { Header = "Terminal presentation and capture", Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 7) };
        var presentationGrid = new Grid();
        for (var i = 0; i < 4; i++) presentationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        presentationGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        presentationGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        presentationGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _fontFamily = new ComboBox { ItemsSource = Fonts.SystemFontFamilies.OrderBy(font => font.Source, StringComparer.OrdinalIgnoreCase).ToArray(), DisplayMemberPath = nameof(FontFamily.Source), Margin = new Thickness(3) };
        _fontFamily.SelectedItem = ((IEnumerable<FontFamily>)_fontFamily.ItemsSource).FirstOrDefault(font => font.Source.Equals(presentation.FontFamily, StringComparison.OrdinalIgnoreCase)) ?? ((IEnumerable<FontFamily>)_fontFamily.ItemsSource).FirstOrDefault();
        AddLabeled(presentationGrid, 0, 0, "Font family", _fontFamily);
        _fontSize = new Slider { Minimum = 8, Maximum = 36, TickFrequency = 1, IsSnapToTickEnabled = true, Value = Math.Clamp(presentation.FontSize, 8, 36), Margin = new Thickness(3) };
        AddLabeled(presentationGrid, 0, 1, "Base font size", _fontSize);
        _outputScale = new Slider { Minimum = 75, Maximum = 200, TickFrequency = 5, IsSnapToTickEnabled = true, Value = Math.Clamp(presentation.OutputScalePercent, 75, 200), Margin = new Thickness(3) };
        AddLabeled(presentationGrid, 0, 2, "Output scale %", _outputScale);
        _scrollbackLimit = new TextBox { Text = _session.TerminalPreferences.MaximumScrollbackLines.ToString(), Margin = new Thickness(3) };
        AddLabeled(presentationGrid, 0, 3, "Scrollback lines", _scrollbackLimit);

        _showToolbar = AddCheck(presentationGrid, 1, 0, "Show toolbar", presentation.ShowToolbar);
        _showStatusBar = AddCheck(presentationGrid, 1, 1, "Show status bar", presentation.ShowStatusBar);
        _autoFocus = AddCheck(presentationGrid, 1, 2, "Auto-focus terminal", presentation.AutoFocusTerminal);
        _showTimestamps = AddCheck(presentationGrid, 1, 3, "Timestamp retained lines", _session.TerminalPreferences.ShowTimestamps);
        _enableHotkeys = AddCheck(presentationGrid, 2, 0, "Command hotkeys", presentation.EnableCommandHotkeys);
        _enableContext = AddCheck(presentationGrid, 2, 1, "Custom right-click menu", presentation.EnableCustomContextMenu);
        _loggingEnabled = AddCheck(presentationGrid, 2, 2, "Live terminal log", _session.TerminalPreferences.LoggingEnabled);
        _captureTelnet = AddCheck(presentationGrid, 2, 3, "Telnet trace (next connect)", _session.TerminalPreferences.CaptureTelnetTraffic);
        presentationBox.Content = presentationGrid;
        customRoot.Children.Add(presentationBox);

        var toolbarBox = new GroupBox { Header = "Editable RealmGate quick toolbar — set Visible, Label, and Order", Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 7) };
        var toolbarGrid = CreateEditableGrid(_host.AutomationOptions.Toolbar);
        toolbarGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Visible", Binding = new Binding(nameof(ClientToolbarOption.Visible)), Width = 65 });
        toolbarGrid.Columns.Add(new DataGridTextColumn { Header = "Label", Binding = new Binding(nameof(ClientToolbarOption.Label)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        toolbarGrid.Columns.Add(new DataGridTextColumn { Header = "Tool", Binding = new Binding(nameof(ClientToolbarOption.AutomationArea)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        toolbarGrid.Columns.Add(new DataGridTextColumn { Header = "Order", Binding = new Binding(nameof(ClientToolbarOption.Order)), Width = 70 });
        toolbarBox.Content = toolbarGrid;
        Grid.SetRow(toolbarBox, 1);
        customRoot.Children.Add(toolbarBox);

        var keyBox = new GroupBox { Header = "Command hotkeys — disabled by default; gameplay-only and internal CLIENTDATA/ROOMDATA commands are rejected", Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 7) };
        var keyDock = new DockPanel();
        var keyButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var addKey = new Button { Content = "Add", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
        addKey.Click += (_, _) => _host.AutomationOptions.KeyBindings.Add(new ClientCommandKeyBindingOption { Label = "Command", Order = _host.AutomationOptions.KeyBindings.Count * 10 });
        keyButtons.Children.Add(addKey);
        DockPanel.SetDock(keyButtons, Dock.Top);
        keyDock.Children.Add(keyButtons);
        var keyGrid = CreateEditableGrid(_host.AutomationOptions.KeyBindings);
        keyGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Enabled", Binding = new Binding(nameof(ClientCommandKeyBindingOption.Enabled)), Width = 65 });
        keyGrid.Columns.Add(new DataGridTextColumn { Header = "Label", Binding = new Binding(nameof(ClientCommandKeyBindingOption.Label)), Width = 150 });
        keyGrid.Columns.Add(new DataGridTextColumn { Header = "Gesture", Binding = new Binding(nameof(ClientCommandKeyBindingOption.Gesture)), Width = 110 });
        keyGrid.Columns.Add(new DataGridTextColumn { Header = "Command", Binding = new Binding(nameof(ClientCommandKeyBindingOption.Command)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        keyGrid.Columns.Add(new DataGridTextColumn { Header = "Order", Binding = new Binding(nameof(ClientCommandKeyBindingOption.Order)), Width = 65 });
        keyDock.Children.Add(keyGrid);
        keyBox.Content = keyDock;
        Grid.SetRow(keyBox, 2);
        customRoot.Children.Add(keyBox);

        var contextBox = new GroupBox { Header = "Terminal right-click commands", Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 7) };
        var contextDock = new DockPanel();
        var contextButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var addContext = new Button { Content = "Add", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
        addContext.Click += (_, _) => _host.AutomationOptions.ContextCommands.Add(new ClientContextCommandOption { Label = "Command", Order = _host.AutomationOptions.ContextCommands.Count * 10 });
        contextButtons.Children.Add(addContext);
        DockPanel.SetDock(contextButtons, Dock.Top);
        contextDock.Children.Add(contextButtons);
        var contextGrid = CreateEditableGrid(_host.AutomationOptions.ContextCommands);
        contextGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Visible", Binding = new Binding(nameof(ClientContextCommandOption.Visible)), Width = 65 });
        contextGrid.Columns.Add(new DataGridTextColumn { Header = "Label", Binding = new Binding(nameof(ClientContextCommandOption.Label)), Width = 170 });
        contextGrid.Columns.Add(new DataGridTextColumn { Header = "Command", Binding = new Binding(nameof(ClientContextCommandOption.Command)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        contextGrid.Columns.Add(new DataGridTextColumn { Header = "Order", Binding = new Binding(nameof(ClientContextCommandOption.Order)), Width = 65 });
        contextDock.Children.Add(contextGrid);
        contextBox.Content = contextDock;
        Grid.SetRow(contextBox, 3);
        customRoot.Children.Add(contextBox);

        var save = new Button { Content = "Save & Apply", Padding = new Thickness(18, 7, 18, 7), HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += async (_, _) => await SaveCustomizationAsync();
        Grid.SetRow(save, 4);
        customRoot.Children.Add(save);
        tabs.Items.Add(new TabItem { Header = "Customize", Content = customRoot });

        Content = root;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) =>
        {
            RefreshLiveViews();
            _wireTick++;
            if (_wireTick % 2 == 0) await RefreshWireAsync();
        };
        Loaded += async (_, _) =>
        {
            RefreshLiveViews();
            await RefreshWireAsync();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    public ClientSessionConnection Session => _session;

    private void RefreshLiveViews()
    {
        RefreshScrollback();
        _sessionStats.Text = ReforgedClientDiagnosticsService.RenderSessionStats(_session, DateTimeOffset.Now);
        _programSummary.Text = ReforgedClientDiagnosticsService.RenderProgramSummary(_session);
    }

    private void RefreshScrollback()
    {
        ClientTerminalChannel? channel = null;
        if (_channelFilter.SelectedIndex > 0 && Enum.TryParse<ClientTerminalChannel>(_channelFilter.SelectedItem?.ToString(), out var parsed))
            channel = parsed;
        var rows = ReforgedClientDiagnosticsService.FilterScrollback(
            _session,
            _scrollbackQuery.Text,
            channel,
            _includeOutgoing.IsChecked == true);
        _scrollbackRows.Clear();
        foreach (var row in rows) _scrollbackRows.Add(row);
        _scrollbackSummary.Text = $"Showing {rows.Count:N0} of {_session.TerminalEntries.Count:N0} retained entries · limit {_session.TerminalPreferences.MaximumScrollbackLines:N0}";
    }

    private async Task RefreshWireAsync()
    {
        if (_wireRefreshing) return;
        _wireRefreshing = true;
        try
        {
            _wireHeader.Text = $"{_session.TelnetStatus} · Trace: {(string.IsNullOrWhiteSpace(_session.TelnetTracePath) ? "not active" : _session.TelnetTracePath)}";
            _wireOutput.Text = await ReforgedClientDiagnosticsService.ReadTelnetTraceTailAsync(_session);
            _wireOutput.ScrollToEnd();
        }
        catch (Exception exception)
        {
            _wireOutput.Text = exception.Message;
        }
        finally { _wireRefreshing = false; }
    }

    private async Task SaveCustomizationAsync()
    {
        var presentation = _host.AutomationOptions.Presentation;
        presentation.FontFamily = (_fontFamily.SelectedItem as FontFamily)?.Source ?? "Consolas";
        presentation.FontSize = (int)Math.Round(_fontSize.Value);
        presentation.OutputScalePercent = (int)Math.Round(_outputScale.Value);
        presentation.ShowToolbar = _showToolbar.IsChecked == true;
        presentation.ShowStatusBar = _showStatusBar.IsChecked == true;
        presentation.AutoFocusTerminal = _autoFocus.IsChecked == true;
        presentation.EnableCommandHotkeys = _enableHotkeys.IsChecked == true;
        presentation.EnableCustomContextMenu = _enableContext.IsChecked == true;
        presentation.ShowTimestamps = _showTimestamps.IsChecked == true;

        _session.TerminalPreferences.ShowTimestamps = _showTimestamps.IsChecked == true;
        _session.TerminalPreferences.LoggingEnabled = _loggingEnabled.IsChecked == true;
        _session.TerminalPreferences.CaptureTelnetTraffic = _captureTelnet.IsChecked == true;
        _session.TerminalPreferences.MaximumScrollbackLines = int.TryParse(_scrollbackLimit.Text, out var limit)
            ? Math.Clamp(limit, 500, 100000)
            : _session.TerminalPreferences.MaximumScrollbackLines;

        foreach (var binding in _host.AutomationOptions.KeyBindings)
        {
            binding.Label = binding.Label?.Trim() ?? string.Empty;
            binding.Gesture = binding.Gesture?.Trim() ?? string.Empty;
            binding.Command = binding.Command?.Trim() ?? string.Empty;
            if (binding.Enabled && !ReforgedClientDiagnosticsService.IsSafeUserCommand(binding.Command))
                binding.Enabled = false;
        }
        foreach (var command in _host.AutomationOptions.ContextCommands)
        {
            command.Label = command.Label?.Trim() ?? string.Empty;
            command.Command = command.Command?.Trim() ?? string.Empty;
            if (!ReforgedClientDiagnosticsService.IsSafeUserCommand(command.Command))
                command.Visible = false;
        }

        await Task.WhenAll(_host.SaveAutomationOptionsAsync(), _host.SaveConnectionProfileAsync(_session));
        RefreshLiveViews();
        MessageBox.Show(this,
            _session.TerminalPreferences.CaptureTelnetTraffic
                ? "Customization saved. Telnet trace capture will start on the next connection/reconnect."
                : "Customization saved and applied.",
            "RealmGate", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task ExportSupportBundleAsync()
    {
        try
        {
            var path = await ReforgedClientDiagnosticsService.ExportSupportBundleAsync(_session, _host.AutomationOptions);
            MessageBox.Show(this, $"Support bundle written to:\n{path}", "RealmGate Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Support Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static TextBox ReadOnlyOutput(bool wrap) => new()
    {
        IsReadOnly = true,
        IsReadOnlyCaretVisible = true,
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        Padding = new Thickness(8),
        Background = Brushes.White,
        Foreground = Brushes.Black
    };

    private static DataGrid CreateEditableGrid(object items) => new()
    {
        ItemsSource = items as System.Collections.IEnumerable,
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
    };

    private static void AddLabeled(Grid grid, int row, int column, string label, Control control)
    {
        var stack = new StackPanel { Margin = new Thickness(2) };
        stack.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(control);
        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private static CheckBox AddCheck(Grid grid, int row, int column, string label, bool value)
    {
        var check = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(6, 5, 4, 5), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(check, row);
        Grid.SetColumn(check, column);
        grid.Children.Add(check);
        return check;
    }

    private static string DisplayName(ClientSessionConnection session) =>
        string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;
}
