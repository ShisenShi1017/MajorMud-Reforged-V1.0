using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Reforged.App.Models;
using Reforged.App.Services;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class ClientTerminalWindow : Window
{
    private readonly DispatcherTimer _classicStateTimer;
    private bool _classicPasswordMode;
    private bool _normalizingDataContext;
    private bool _soundStateInitialized;
    private int _observedTellCount;
    private bool _observedInCombat;
    private bool _lowHealthSoundLatched;
    private ClientConnectionState _observedConnectionState;
    private RouteExecutionState _observedRouteState;
    private DateTimeOffset _lastAutomationClockTick = DateTimeOffset.MinValue;
    private DateTimeOffset _nextPvpStateRefreshAt = DateTimeOffset.MinValue;
    private bool _pvpStateRefreshInFlight;

    public ClientTerminalWindow()
    {
        InitializeComponent();
        PreviewKeyDown += ClientTerminalWindowPreviewKeyDown;

        _classicStateTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Background,
            (_, _) => RefreshClassicTerminalState(),
            Dispatcher);

        Loaded += (_, _) =>
        {
            NormalizeDataContext();
            RefreshQuickAutomationChecks();
            _classicStateTimer.Start();
            RefreshClassicTerminalState();
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(RequestClassicTerminalFocus));
        };

        Activated += (_, _) =>
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(RequestClassicTerminalFocus));

        DataContextChanged += (_, _) =>
        {
            NormalizeDataContext();
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(RefreshClassicTerminalState));
        };

        Closed += (_, _) =>
        {
            _classicStateTimer.Stop();
            Context?.Dispose();
        };
    }

    public ClientTerminalWindow(
        PlayViewModel host,
        ClientSessionConnection session)
        : this()
    {
        DataContext = new ClientTerminalWindowContext(host, session);
    }

    public string? SessionId => BoundSession?.SessionId;

    /// <summary>
    /// Opens the one canonical terminal window for a live session, or activates the
    /// existing window when either the Play tab or Forge requests the same session.
    /// Both entry points therefore share the same pinned session context, live screen,
    /// command path, and renderer instead of constructing competing presentations.
    /// </summary>
    public static ClientTerminalWindow ShowForSession(
        PlayViewModel host,
        ClientSessionConnection session,
        Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(session);

        var existing = Application.Current.Windows
            .OfType<ClientTerminalWindow>()
            .FirstOrDefault(window =>
                string.Equals(
                    window.SessionId,
                    session.SessionId,
                    StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;

            existing.Show();
            existing.Activate();
            existing.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(existing.RequestClassicTerminalFocus));
            return existing;
        }

        var window = new ClientTerminalWindow(host, session);
        if (owner is not null && !ReferenceEquals(owner, window))
            window.Owner = owner;

        window.Show();
        window.Activate();
        return window;
    }

    private void NormalizeDataContext()
    {
        if (_normalizingDataContext || DataContext is ClientTerminalWindowContext)
            return;

        if (DataContext is not PlayViewModel host || host.SelectedLiveSession is null)
            return;

        try
        {
            _normalizingDataContext = true;
            DataContext = new ClientTerminalWindowContext(host, host.SelectedLiveSession);
        }
        finally
        {
            _normalizingDataContext = false;
        }
    }

    private void RefreshClassicTerminalState()
    {
        var session = BoundSession;
        if (session is null) return;

        UpdateClassicPasswordMode(
            string.IsNullOrWhiteSpace(session.LastReceivedLine)
                ? session.LivePromptText
                : session.LastReceivedLine);

        if (ClassicTerminalSurface is not null)
        {
            var presentation = ViewModel?.AutomationOptions.Presentation;
            if (presentation is not null)
            {
                var family = string.IsNullOrWhiteSpace(presentation.FontFamily) ? "Lucida Console" : presentation.FontFamily;
                if (!ClassicTerminalSurface.FontFamily.Source.Equals(family, StringComparison.OrdinalIgnoreCase))
                    ClassicTerminalSurface.FontFamily = new FontFamily(family);
                ClassicTerminalSurface.FontSize = Math.Clamp(presentation.FontSize, 8, 36);
                ClassicCommandInput.FontFamily = ClassicTerminalSurface.FontFamily;
                ClassicCommandInput.FontSize = Math.Clamp(presentation.FontSize, 8, 36);
            }
            ClassicTerminalSurface.IsPasswordMode = _classicPasswordMode;
            ClassicTerminalSurface.InvalidateVisual();
        }
        ObserveSoundTransitions(session);
        var now = DateTimeOffset.Now;
        if ((now - _lastAutomationClockTick).TotalSeconds >= 1)
        {
            _lastAutomationClockTick = now;
            ViewModel?.TickTimedEvents(session, now);
        }
        if (!_pvpStateRefreshInFlight && ViewModel is not null && session.ConnectionState == ClientConnectionState.Connected &&
            !string.IsNullOrWhiteSpace(session.CurrentRoomKey) && now >= _nextPvpStateRefreshAt)
        {
            _nextPvpStateRefreshAt = now.AddSeconds(5);
            _pvpStateRefreshInFlight = true;
            _ = RefreshPvpStateAsync(ViewModel, session);
        }
    }

    private async Task RefreshPvpStateAsync(PlayViewModel viewModel, ClientSessionConnection session)
    {
        try { await viewModel.SendClientProtocolCommandAsync(session, "clientdata pvp"); }
        finally { _pvpStateRefreshInFlight = false; }
    }

    private void ObserveSoundTransitions(ClientSessionConnection session)
    {
        var sounds = ViewModel?.AutomationOptions.Sounds;
        if (sounds is null) return;
        if (!_soundStateInitialized)
        {
            _soundStateInitialized = true;
            _observedTellCount = session.TellEntries.Count;
            _observedInCombat = session.InCombat;
            _observedConnectionState = session.ConnectionState;
            _observedRouteState = session.RouteState;
            _lowHealthSoundLatched = session.HealthPercent <= 25;
            return;
        }
        if (sounds.Enabled && sounds.TellSound && session.TellEntries.Count > _observedTellCount)
            System.Media.SystemSounds.Asterisk.Play();
        if (sounds.Enabled && sounds.CombatSound && session.InCombat && !_observedInCombat)
            System.Media.SystemSounds.Exclamation.Play();
        if (sounds.Enabled && sounds.LowHealthSound && session.HealthPercent <= 25 && !_lowHealthSoundLatched)
            System.Media.SystemSounds.Hand.Play();
        if (sounds.Enabled && sounds.RouteCompleteSound && session.RouteState == RouteExecutionState.Completed && _observedRouteState != RouteExecutionState.Completed)
            System.Media.SystemSounds.Asterisk.Play();
        if (sounds.Enabled && sounds.DisconnectSound &&
            _observedConnectionState == ClientConnectionState.Connected &&
            session.ConnectionState is ClientConnectionState.Disconnected or ClientConnectionState.Faulted)
            System.Media.SystemSounds.Hand.Play();
        _observedTellCount = session.TellEntries.Count;
        _observedInCombat = session.InCombat;
        _observedConnectionState = session.ConnectionState;
        _observedRouteState = session.RouteState;
        _lowHealthSoundLatched = session.HealthPercent <= 25;
    }

    private void BackScrollClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session)
            return;

        var history = new TextBox
        {
            Text = BuildBackScrollText(session.TerminalLines),
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            Background = new SolidColorBrush(Color.FromRgb(2, 5, 10)),
            Foreground = new SolidColorBrush(Color.FromRgb(231, 231, 231)),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Lucida Console"),
            FontSize = 18,
            Padding = new Thickness(8, 6, 8, 6),
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var copy = new MenuItem
        {
            Header = "Copy",
            InputGestureText = "Ctrl+C"
        };
        copy.Click += (_, _) => history.Copy();

        var selectAll = new MenuItem
        {
            Header = "Select All",
            InputGestureText = "Ctrl+A"
        };
        selectAll.Click += (_, _) => history.SelectAll();

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(copy);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(selectAll);
        contextMenu.Opened += (_, _) =>
            copy.IsEnabled = history.SelectionLength > 0;
        history.ContextMenu = contextMenu;

        history.PreviewKeyDown += (_, args) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            if (args.Key == Key.C)
            {
                if (history.SelectionLength > 0)
                    history.Copy();
                args.Handled = true;
            }
            else if (args.Key == Key.A)
            {
                history.SelectAll();
                args.Handled = true;
            }
        };

        NotifyCollectionChangedEventHandler terminalLinesChanged = (_, args) =>
        {
            var wasAtEnd =
                history.VerticalOffset + history.ViewportHeight >=
                history.ExtentHeight - 2;

            if (args.Action == NotifyCollectionChangedAction.Add &&
                args.NewItems is not null)
            {
                foreach (var value in args.NewItems.OfType<string>())
                {
                    if (history.Text.Length > 0)
                        history.AppendText(Environment.NewLine);
                    history.AppendText(NormalizeBackScrollLine(value));
                }
            }
            else
            {
                history.Text = BuildBackScrollText(session.TerminalLines);
            }

            if (wasAtEnd)
                history.ScrollToEnd();
        };
        session.TerminalLines.CollectionChanged += terminalLinesChanged;

        var backScrollWindow = new Window
        {
            Title = $"Back-scroll - {session.DisplayName}",
            Owner = this,
            Width = Math.Max(700, ActualWidth * 0.82),
            Height = Math.Max(500, ActualHeight * 0.78),
            MinWidth = 520,
            MinHeight = 360,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            Background = new SolidColorBrush(Color.FromRgb(2, 5, 10)),
            Content = history,
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        backScrollWindow.Loaded += (_, _) =>
        {
            history.ScrollToEnd();
            Keyboard.Focus(history);
        };
        backScrollWindow.Closed += (_, _) =>
        {
            session.TerminalLines.CollectionChanged -= terminalLinesChanged;
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(RequestClassicTerminalFocus));
        };

        backScrollWindow.Show();
        backScrollWindow.Activate();
    }

    private static string BuildBackScrollText(IEnumerable<string> lines) =>
        string.Join(
            Environment.NewLine,
            lines.Select(NormalizeBackScrollLine));

    private void FindClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session) return;

        var dialog = new Window
        {
            Title = $"Find in Scrollback - {session.DisplayName}",
            Owner = this,
            Width = 760,
            Height = 470,
            MinWidth = 540,
            MinHeight = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush,
            Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"),
            FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };
        DockPanel.SetDock(searchRow, Dock.Top);
        var previous = NewDialogButton("Previous", 76);
        var next = NewDialogButton("Next", 64);
        var search = NewDialogButton("Find", 60);
        DockPanel.SetDock(previous, Dock.Right);
        DockPanel.SetDock(next, Dock.Right);
        DockPanel.SetDock(search, Dock.Right);
        searchRow.Children.Add(previous);
        searchRow.Children.Add(next);
        searchRow.Children.Add(search);
        var query = new TextBox { Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        searchRow.Children.Add(query);

        var status = new TextBlock { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(status, Dock.Bottom);
        var results = new ListBox
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        void RunSearch()
        {
            var needle = query.Text.Trim();
            if (needle.Length == 0)
            {
                results.ItemsSource = null;
                status.Text = "Enter text to search the received scrollback.";
                return;
            }

            var matches = session.TerminalLines
                .Select((line, index) => new { Line = index + 1, Text = NormalizeBackScrollLine(line) })
                .Where(item => item.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .Select(item => $"{item.Line,6}: {item.Text}")
                .ToArray();
            results.ItemsSource = matches;
            results.SelectedIndex = matches.Length > 0 ? 0 : -1;
            if (results.SelectedItem is not null) results.ScrollIntoView(results.SelectedItem);
            status.Text = matches.Length == 1 ? "1 matching line." : $"{matches.Length:N0} matching lines.";
        }

        void MoveSelection(int delta)
        {
            if (results.Items.Count == 0) return;
            var current = results.SelectedIndex < 0 ? 0 : results.SelectedIndex;
            results.SelectedIndex = (current + delta + results.Items.Count) % results.Items.Count;
            results.ScrollIntoView(results.SelectedItem);
            status.Text = $"Match {results.SelectedIndex + 1:N0} of {results.Items.Count:N0}.";
        }

        search.Click += (_, _) => RunSearch();
        next.Click += (_, _) => MoveSelection(1);
        previous.Click += (_, _) => MoveSelection(-1);
        query.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            RunSearch();
            args.Handled = true;
        };
        results.MouseDoubleClick += (_, _) =>
        {
            if (results.SelectedItem is string selected) Clipboard.SetText(selected);
        };
        root.Children.Add(searchRow);
        root.Children.Add(status);
        root.Children.Add(results);
        dialog.Content = root;
        dialog.Loaded += (_, _) => Keyboard.Focus(query);
        dialog.Show();
    }

    private static string NormalizeBackScrollLine(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var reconstructed = ReconstructBackScrollLine(value);
        var exitsMarker = reconstructed.IndexOf(
            "obvious exits:",
            StringComparison.OrdinalIgnoreCase);

        if (exitsMarker < 0)
            return reconstructed;

        var prefixLength = exitsMarker + "obvious exits:".Length;
        var prefix = reconstructed[..prefixLength];
        var exits = reconstructed[prefixLength..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeClassicExitToken);

        return $"{prefix} {string.Join(", ", exits)}";
    }

    private static string NormalizeClassicExitToken(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
            return trimmed;

        var normalized = trimmed.ToLowerInvariant();
        if (ClassicDirections.Contains(normalized))
            return normalized;

        // MajorMUD sometimes redraws the highlighted shortcut character over
        // an already-written exit name. A flattened transcript can therefore
        // contain nNorth, sSouth, wWest, and similar duplicated prefixes.
        if (trimmed.Length > 1)
        {
            var withoutShortcut = trimmed[1..].TrimStart();
            var candidate = withoutShortcut.ToLowerInvariant();
            if (ClassicDirections.Contains(candidate) &&
                char.ToLowerInvariant(trimmed[0]) ==
                char.ToLowerInvariant(withoutShortcut[0]))
            {
                return candidate;
            }

            foreach (var direction in ClassicDirections)
            {
                if (char.ToLowerInvariant(trimmed[0]) != direction[0] ||
                    candidate.Length != direction.Length)
                {
                    continue;
                }

                var differences = 0;
                for (var index = 0; index < candidate.Length; index++)
                {
                    if (candidate[index] == direction[index])
                        continue;

                    differences++;
                    if (differences > 1)
                        break;
                }

                if (differences <= 1)
                    return direction;
            }
        }

        return trimmed;
    }

    private static string ReconstructBackScrollLine(string value)
    {
        var cells = new List<char>(value.Length);
        var cursor = 0;
        var savedCursor = 0;
        var state = BackScrollParserState.Ground;
        var parameters = new StringBuilder();

        void Write(char character)
        {
            while (cells.Count < cursor)
                cells.Add(' ');

            if (cursor < cells.Count)
                cells[cursor] = character;
            else
                cells.Add(character);

            cursor++;
        }

        foreach (var character in value)
        {
            switch (state)
            {
                case BackScrollParserState.Ground:
                    switch (character)
                    {
                        case '\x1B':
                            state = BackScrollParserState.Escape;
                            break;
                        case '\u009B':
                            parameters.Clear();
                            state = BackScrollParserState.Csi;
                            break;
                        case '\r':
                            cursor = 0;
                            break;
                        case '\b':
                            cursor = Math.Max(0, cursor - 1);
                            break;
                        case '\t':
                            var nextTab = ((cursor / 8) + 1) * 8;
                            while (cursor < nextTab)
                                Write(' ');
                            break;
                        case '\n':
                        case '\0':
                        case '\a':
                            break;
                        default:
                            if (!char.IsControl(character))
                                Write(character);
                            break;
                    }
                    break;

                case BackScrollParserState.Escape:
                    if (character == '[')
                    {
                        parameters.Clear();
                        state = BackScrollParserState.Csi;
                    }
                    else if (character == ']')
                    {
                        state = BackScrollParserState.Osc;
                    }
                    else
                    {
                        if (character == '7')
                            savedCursor = cursor;
                        else if (character == '8')
                            cursor = savedCursor;
                        state = BackScrollParserState.Ground;
                    }
                    break;

                case BackScrollParserState.Csi:
                    if (character is >= '@' and <= '~')
                    {
                        ApplyBackScrollCsi(
                            character,
                            parameters.ToString(),
                            cells,
                            ref cursor,
                            ref savedCursor);
                        parameters.Clear();
                        state = BackScrollParserState.Ground;
                    }
                    else if (parameters.Length < 96)
                    {
                        parameters.Append(character);
                    }
                    break;

                case BackScrollParserState.Osc:
                    if (character == '\a')
                        state = BackScrollParserState.Ground;
                    else if (character == '\x1B')
                        state = BackScrollParserState.OscEscape;
                    break;

                case BackScrollParserState.OscEscape:
                    state = character == '\\'
                        ? BackScrollParserState.Ground
                        : BackScrollParserState.Osc;
                    break;
            }
        }

        return new string(cells.ToArray()).TrimEnd();
    }

    private static void ApplyBackScrollCsi(
        char command,
        string parameterText,
        List<char> cells,
        ref int cursor,
        ref int savedCursor)
    {
        var values = parameterText
            .TrimStart('?')
            .Split(';', StringSplitOptions.None)
            .Select(part => int.TryParse(
                new string(part.Where(char.IsDigit).ToArray()),
                out var parsed)
                    ? parsed
                    : 0)
            .ToArray();

        int Parameter(int index, int fallback = 1) =>
            index < values.Length && values[index] > 0
                ? values[index]
                : fallback;

        switch (command)
        {
            case 'C':
                cursor += Parameter(0);
                break;
            case 'D':
                cursor = Math.Max(0, cursor - Parameter(0));
                break;
            case 'G':
            case '`':
                cursor = Math.Max(0, Parameter(0) - 1);
                break;
            case 'H':
            case 'f':
                cursor = Math.Max(0, Parameter(1) - 1);
                break;
            case 'K':
            {
                var mode = values.Length == 0 ? 0 : values[0];
                if (mode == 1)
                {
                    var end = Math.Min(cursor, cells.Count - 1);
                    for (var index = 0; index <= end; index++)
                        cells[index] = ' ';
                }
                else if (mode == 2)
                {
                    cells.Clear();
                    cursor = 0;
                }
                else if (cursor < cells.Count)
                {
                    cells.RemoveRange(cursor, cells.Count - cursor);
                }
                break;
            }
            case '@':
            {
                var count = Parameter(0);
                while (cells.Count < cursor)
                    cells.Add(' ');
                cells.InsertRange(cursor, Enumerable.Repeat(' ', count));
                break;
            }
            case 'P':
            {
                var count = Math.Min(Parameter(0), Math.Max(0, cells.Count - cursor));
                if (count > 0)
                    cells.RemoveRange(cursor, count);
                break;
            }
            case 'X':
            {
                var count = Parameter(0);
                while (cells.Count < cursor + count)
                    cells.Add(' ');
                for (var index = cursor; index < cursor + count; index++)
                    cells[index] = ' ';
                break;
            }
            case 's':
                savedCursor = cursor;
                break;
            case 'u':
                cursor = savedCursor;
                break;
        }
    }

    private static readonly HashSet<string> ClassicDirections =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "north", "northeast", "east", "southeast",
            "south", "southwest", "west", "northwest",
            "up", "down", "in", "out"
        };

    private enum BackScrollParserState
    {
        Ground,
        Escape,
        Csi,
        Osc,
        OscEscape
    }


    private void UpdateClassicPasswordMode(string latestText)
    {
        var text = (latestText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text)) return;

        var lower = text.ToLowerInvariant();
        var passwordRequested =
            lower.EndsWith("password:", StringComparison.Ordinal) ||
            lower.EndsWith("password >", StringComparison.Ordinal) ||
            lower.Contains("enter password:", StringComparison.Ordinal) ||
            lower.Contains("create password:", StringComparison.Ordinal) ||
            lower.Contains("new password:", StringComparison.Ordinal) ||
            lower.Contains("new password (", StringComparison.Ordinal) ||
            lower.Contains("confirm password:", StringComparison.Ordinal) ||
            lower.Contains("re-enter password:", StringComparison.Ordinal) ||
            lower.Contains("verify password:", StringComparison.Ordinal);

        if (passwordRequested)
        {
            _classicPasswordMode = true;
            if (ClassicTerminalSurface is not null)
                ClassicTerminalSurface.IsPasswordMode = true;
            return;
        }

        if (_classicPasswordMode)
            ResetClassicInputMode();
    }

    private void ResetClassicInputMode()
    {
        _classicPasswordMode = false;
        if (ClassicTerminalSurface is not null)
        {
            ClassicTerminalSurface.IsPasswordMode = false;
            ClassicTerminalSurface.ClearInput();
        }
    }

    private void RequestClassicTerminalFocus()
    {
        if (!IsActive ||
            ClassicTerminalSurface is null ||
            !ClassicTerminalSurface.IsVisible ||
            !ClassicTerminalSurface.IsEnabled)
            return;

        // Menus and owned client windows must keep focus until the user closes
        // them. The old 100 ms refresh loop reclaimed focus continuously and
        // made menus, toolbar windows, and Back-scroll appear to blink closed.
        if (ClassicMenuBar.IsKeyboardFocusWithin ||
            Keyboard.FocusedElement is MenuItem ||
            Mouse.Captured is MenuItem)
            return;

        foreach (Window ownedWindow in OwnedWindows)
        {
            if (ownedWindow.IsActive)
                return;
        }

        Keyboard.Focus(ClassicTerminalSurface);
    }

    private ClientTerminalWindowContext? Context => DataContext as ClientTerminalWindowContext;
    private PlayViewModel? ViewModel => Context?.Host;
    private ClientSessionConnection? BoundSession => Context?.Session;

    private async void ClientTerminalWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ShowHelpCenter();
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Alt &&
            (e.Key == Key.T || (e.Key == Key.System && e.SystemKey == Key.T)))
        {
            e.Handled = true;
            LiveMapClicked(sender, e);
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.Control || e.Key != Key.S)
            return;

        e.Handled = true;
        if (ViewModel is not null)
            await ViewModel.SaveConnectionProfileAsync(BoundSession);
    }

    private void OpenCharacterProfilesClicked(object sender, RoutedEventArgs e) =>
        ShowCharacterProfiles();

    private void NewCharacterProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.CreateNewConnectionProfile(BoundSession);
        ShowCharacterProfiles();
    }

    private async void SaveCharacterProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.SaveConnectionProfileAsync(BoundSession);
        RequestClassicTerminalFocus();
    }

    private async void SaveCharacterAsProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        await ViewModel.SaveConnectionProfileAsAsync(BoundSession);
        ShowCharacterProfiles();
    }

    private void ShowCharacterProfiles()
    {
        if (ViewModel is null)
            return;

        if (BoundSession is not null)
            ViewModel.SelectedLiveSession = BoundSession;

        var existing = OwnedWindows.OfType<ClientProfileWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        var window = new ClientProfileWindow
        {
            Owner = this,
            DataContext = ViewModel
        };
        window.Show();
        window.Activate();
    }

    private async void ConnectClicked(object sender, RoutedEventArgs e)
    {
        ResetClassicInputMode();
        if (ViewModel is not null) await ViewModel.ConnectSessionAsync(BoundSession);
        RequestClassicTerminalFocus();
    }

    private async void DisconnectClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.DisconnectSessionAsync(BoundSession);
        ResetClassicInputMode();
        RequestClassicTerminalFocus();
    }

    private async void SendClicked(object sender, RoutedEventArgs e) =>
        await SendWindowCommandAsync();

    private async void TerminalCommandKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        await SendWindowCommandAsync();
    }

    private async Task SendWindowCommandAsync()
    {
        if (ViewModel is null || Context is null || BoundSession is null)
            return;

        var command = Context.TerminalCommand;
        Context.ClearCommand();
        await ViewModel.SendTerminalCommandAsync(BoundSession, command);
    }

    private async void ClassicTerminalCommandSubmitted(
        object? sender,
        ClassicMegaMudTerminalCommandEventArgs e)
    {
        if (ViewModel is null || BoundSession is null)
            return;

        if (e.Sensitive)
            await ViewModel.SendSensitiveTerminalCommandAsync(BoundSession, e.Command);
        else
            await ViewModel.SendTerminalCommandAsync(BoundSession, e.Command);

        RefreshClassicTerminalState();
    }

    private async void ClassicTerminalKeySubmitted(
        object? sender,
        ClassicMegaMudTerminalKeyEventArgs e)
    {
        if (ViewModel is null || BoundSession is null)
            return;

        await ViewModel.SendTerminalKeyAsync(BoundSession, e.Sequence);
        RefreshClassicTerminalState();
    }

    private void ClassicTerminalSizeChanged(
        object? sender,
        ClassicMegaMudTerminalSizeEventArgs e)
    {
        ViewModel?.ResizeClassicTerminal(BoundSession, e.Columns, e.Rows);
    }

    private void ClearClicked(object sender, RoutedEventArgs e) =>
        ViewModel?.ClearTerminal(BoundSession);

    private void ResetAnsiClicked(object sender, RoutedEventArgs e)
    {
        ViewModel?.ResetAnsi(BoundSession);
        RefreshClassicTerminalState();
    }

    private void ResetInternalsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is null) return;
        var result = MessageBox.Show(
            this,
            "Reset the local typed room, combat, item, spell, party, and WHO caches?\n\nThe connection and terminal scrollback will remain open. No @reset command will be sent.",
            "Reset Reforged client internals",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        ViewModel.ResetClientInternals(BoundSession);
        RefreshClassicTerminalState();
    }

    private void ResetWindowPositionsClicked(object sender, RoutedEventArgs e)
    {
        var work = SystemParameters.WorkArea;
        RecenterWindow(this, work);
        foreach (Window owned in OwnedWindows)
            RecenterWindow(owned, work);
    }

    private void ShowItemWeightsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"Reforged Item Weights — {session.CharacterName}", Owner = this,
            Width = 700, Height = 470, MinWidth = 520, MinHeight = 330,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new TextBlock { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(header, Dock.Top);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var inventory = NewDialogButton("Refresh inventory", 115);
        var equipment = NewDialogButton("Refresh equipment", 120);
        var close = NewDialogButton("Close", 70);
        footer.Children.Add(inventory);
        footer.Children.Add(equipment);
        footer.Children.Add(close);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Item", Binding = new Binding(nameof(ClientItemWeightState.Item)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Location", Binding = new Binding(nameof(ClientItemWeightState.Location)), Width = new DataGridLength(95) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Qty", Binding = new Binding(nameof(ClientItemWeightState.Quantity)), Width = new DataGridLength(55) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Each", Binding = new Binding(nameof(ClientItemWeightState.UnitWeight)), Width = new DataGridLength(70) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Total", Binding = new Binding(nameof(ClientItemWeightState.TotalWeight)), Width = new DataGridLength(80) });
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(grid);
        dialog.Content = root;

        void Refresh()
        {
            var rows = ViewModel.ResolveItemWeights(session);
            grid.ItemsSource = rows;
            var resolvedWeight = rows.Aggregate(0L, (sum, row) =>
                sum > long.MaxValue - row.TotalWeight ? long.MaxValue : sum + row.TotalWeight);
            header.Text = $"Resolved {rows.Count:N0} captured stack(s) · Item weight {resolvedWeight:N0} · Authoritative carried total {session.CarriedWeight:N0}/{session.CarryingCapacity:N0}";
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Refresh();
        dialog.Closed += (_, _) => timer.Stop();
        inventory.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata inventory");
        equipment.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata equipment");
        close.Click += (_, _) => dialog.Close();
        Refresh();
        timer.Start();
        dialog.Show();
    }

    private async void ResetCustomProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var result = MessageBox.Show(
            this,
            $"Replace the active recognition profile '{ViewModel.ParsingProfile.Name}' with the built-in Classic rules and save it?",
            "Reset custom profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        ViewModel.RestoreParsingDefaults();
        await ViewModel.SaveParsingProfileAsync();
    }

    private static void RecenterWindow(Window window, Rect work)
    {
        if (window.WindowState != WindowState.Normal)
            window.WindowState = WindowState.Normal;
        var width = double.IsFinite(window.ActualWidth) && window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = double.IsFinite(window.ActualHeight) && window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        width = Math.Min(Math.Max(320, width), work.Width);
        height = Math.Min(Math.Max(240, height), work.Height);
        window.Width = width;
        window.Height = height;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = work.Left + (work.Width - width) / 2;
        window.Top = work.Top + (work.Height - height) / 2;
    }

    private async void ExportLogClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.ExportTerminalLogAsync(BoundSession);
    }

    private void OptionsClicked(object sender, RoutedEventArgs e)
    {
        if (Context is null) return;
        new ClientOptionsWindow { Owner = this, DataContext = Context }.Show();
    }

    private void CombatOptionsClicked(object sender, RoutedEventArgs e)
    {
        var session = BoundSession;
        if (session is null) return;
        var prefs = session.ClassicCombatPreferences;

        var dialog = new Window
        {
            Owner = this, Title = "Combat...", Width = 680, Height = 610,
            MinWidth = 620, MinHeight = 540, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip, Background = SystemColors.ControlBrush,
            Foreground = SystemColors.ControlTextBrush, FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };

        var root = new DockPanel { Margin = new Thickness(10) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var ok = NewDialogButton("OK", 92); var cancel = NewDialogButton("Cancel", 92); var help = NewDialogButton("Help", 92);
        buttons.Children.Add(ok); buttons.Children.Add(cancel); buttons.Children.Add(help); root.Children.Add(buttons);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(columns);

        var left = new StackPanel(); Grid.SetColumn(left, 0); columns.Children.Add(left);
        var right = new StackPanel(); Grid.SetColumn(right, 2); columns.Children.Add(right);

        var weapons = new GroupBox { Header = "Weapon Combat", Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8) };
        var wg = new Grid(); weapons.Content = wg; left.Children.Add(weapons);
        wg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
        wg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 9; i++) wg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        TextBox AddWeapon(int row, string label, string value)
        {
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
            Grid.SetRow(l, row); wg.Children.Add(l);
            var b = new TextBox { Text = value, Height = 25, Margin = new Thickness(0, 3, 0, 3), VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetRow(b, row); Grid.SetColumn(b, 1); wg.Children.Add(b); return b;
        }
        var normalAttack = AddWeapon(0, "Normal attack", prefs.NormalAttackCommand);
        var bsWeapon = AddWeapon(1, "BS weapon", prefs.BackstabWeapon);
        var normalWeapon = AddWeapon(2, "Normal weapon", prefs.NormalWeapon);
        var alternateWeapon = AddWeapon(3, "Alternate", prefs.AlternateWeapon);
        var shield = AddWeapon(4, "Shield", prefs.Shield);
        var preBs = AddWeapon(5, "Pre-BS", prefs.PreBackstabCommand);
        var postBs = AddWeapon(6, "Post-BS", prefs.PostBackstabCommand);
        var spellReset = AddWeapon(7, "Spell combat", prefs.SpellCombatResetCommand);
        var preAttack = AddWeapon(8, "Auto pre-attack", ViewModel?.AutomationOptions.Combat.PreAttackCommand ?? string.Empty);

        var autoCombat = new CheckBox { Content = "Auto-Combat", IsChecked = prefs.AutoCombat, Margin = new Thickness(3) };
        left.Children.Add(autoCombat);

        var options = new GroupBox { Header = "Options", Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8) };
        var checks = new StackPanel(); options.Content = checks; right.Children.Add(options);
        CheckBox AddCheck(string text, bool value) { var c = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 3, 0, 3) }; checks.Children.Add(c); return c; }
        var doBs = AddCheck("Do BS attacks", prefs.DoBackstabAttacks);
        var runBs = AddCheck("Run if BS fails", prefs.RunIfBackstabFails);
        var noBsMulti = AddCheck("Don't BS if multi-attack", prefs.DontBackstabIfMultiAttack);
        var mainToAux = AddCheck("Main Spell Fails to Aux", prefs.MainSpellFallsToAux);
        var resetMulti = AddCheck("Reset Multi w/MobEntry", prefs.ResetMultiWithMobEntry);
        var clearEngaged = AddCheck("Clear Once Engaged", prefs.ClearOnceEngaged);

        var limits = new GroupBox { Header = "Room Limits", Padding = new Thickness(8) };
        var lg = new Grid(); limits.Content = lg; right.Children.Add(limits);
        lg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        for (var i = 0; i < 3; i++) lg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        TextBox AddLimit(int row, string label, int value)
        {
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 8, 5) };
            Grid.SetRow(l, row); lg.Children.Add(l);
            var b = new TextBox { Text = value.ToString(), Height = 25, Margin = new Thickness(0, 3, 0, 3), VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetRow(b, row); Grid.SetColumn(b, 1); lg.Children.Add(b); return b;
        }
        var maxExp = AddLimit(0, "Max total exp/room", prefs.MaxTotalExperiencePerRoom);
        var minMobs = AddLimit(1, "Min mobs/room", prefs.MinimumMobsPerRoom);
        var maxMobs = AddLimit(2, "Max mobs/room", prefs.MaximumMobsPerRoom);

        help.Click += (_, _) => MessageBox.Show(dialog, "Auto-Combat uses only living hostile creatures reported by the connected server. It pauses for movement, stops on unknown state, and remains blocked whenever Emergency Stop is active.", "Combat automation help", MessageBoxButton.OK, MessageBoxImage.Information);
        cancel.Click += (_, _) => dialog.Close();
        ok.Click += async (_, _) =>
        {
            static int Parse(TextBox box, int fallback) => int.TryParse(box.Text.Trim(), out var value) ? value : fallback;
            prefs.AutoCombat = autoCombat.IsChecked == true; prefs.DoBackstabAttacks = doBs.IsChecked == true;
            prefs.RunIfBackstabFails = runBs.IsChecked == true; prefs.DontBackstabIfMultiAttack = noBsMulti.IsChecked == true;
            prefs.MainSpellFallsToAux = mainToAux.IsChecked == true; prefs.ResetMultiWithMobEntry = resetMulti.IsChecked == true;
            prefs.ClearOnceEngaged = clearEngaged.IsChecked == true; prefs.NormalAttackCommand = normalAttack.Text;
            prefs.BackstabWeapon = bsWeapon.Text; prefs.NormalWeapon = normalWeapon.Text; prefs.AlternateWeapon = alternateWeapon.Text;
            prefs.Shield = shield.Text; prefs.PreBackstabCommand = preBs.Text; prefs.PostBackstabCommand = postBs.Text;
            if (ViewModel is not null) ViewModel.AutomationOptions.Combat.PreAttackCommand = preAttack.Text.Trim();
            prefs.SpellCombatResetCommand = spellReset.Text; prefs.MaxTotalExperiencePerRoom = Parse(maxExp, prefs.MaxTotalExperiencePerRoom);
            prefs.MinimumMobsPerRoom = Parse(minMobs, prefs.MinimumMobsPerRoom); prefs.MaximumMobsPerRoom = Parse(maxMobs, prefs.MaximumMobsPerRoom);
            if (ViewModel is not null) await ViewModel.SaveClassicCombatPreferencesAsync(session); dialog.Close();
        };
        dialog.Content = root; dialog.ShowDialog();
    }

    private void PartyOptionsClicked(object sender, RoutedEventArgs e)
    {
        var session = BoundSession;
        if (session is null) return;
        var prefs = session.ClassicPartyPreferences;

        var dialog = new Window
        {
            Owner = this,
            Title = "Party...",
            Width = 610,
            Height = 650,
            MinWidth = 560,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            Background = SystemColors.ControlBrush,
            Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"),
            FontSize = 12
        };

        var root = new DockPanel { Margin = new Thickness(10) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var ok = NewDialogButton("OK", 92);
        var cancel = NewDialogButton("Cancel", 92);
        var help = NewDialogButton("Help", 92);
        buttons.Children.Add(ok); buttons.Children.Add(cancel); buttons.Children.Add(help);
        root.Children.Add(buttons);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();
        scroll.Content = panel;
        root.Children.Add(scroll);

        var behavior = new GroupBox { Header = "Options", Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(8) };
        var checks = new StackPanel();
        behavior.Content = checks;
        panel.Children.Add(behavior);

        CheckBox AddCheck(string text, bool value)
        {
            var cb = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 3, 0, 3) };
            checks.Children.Add(cb);
            return cb;
        }

        var helpBash = AddCheck("Help leader bash doors", prefs.HelpLeaderBashDoors);
        var sayPanic = AddCheck("Say @panic when leading", prefs.SayPanicWhenLeading);
        var ignorePanic = AddCheck("Ignore @panic when following", prefs.IgnorePanicWhenFollowing);
        var ignoreParty = AddCheck("Ignore @party when following", prefs.IgnorePartyWhenFollowing);
        var ignoreWait = AddCheck("Ignore @wait when leading", prefs.IgnoreWaitWhenLeading);

        var grid = new Grid { Margin = new Thickness(8, 4, 8, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        panel.Children.Add(grid);

        for (var i = 0; i < 7; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        TextBox AddField(int row, string label, string value)
        {
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 10, 6) };
            Grid.SetRow(text, row); Grid.SetColumn(text, 0); grid.Children.Add(text);
            var box = new TextBox { Text = value, Height = 25, Margin = new Thickness(0, 4, 0, 4), VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetRow(box, row); Grid.SetColumn(box, 1); grid.Children.Add(box);
            return box;
        }

        var maxMonsters = AddField(0, "Max. monsters when partying", prefs.MaxMonstersWhenPartying.ToString());
        var maxExperience = AddField(1, "Max. monster experience", prefs.MaxMonsterExperience.ToString());
        var waitPercent = AddField(2, "Wait if members are below (%)", prefs.WaitHealthPercent.ToString());
        var waitMinutes = AddField(3, "If leading, wait only (mins)", prefs.LeaderWaitMinutes.ToString());
        var parFrequency = AddField(4, "'par' frequency (seconds)", prefs.PartyFrequencySeconds.ToString());
        var waitCommand = AddField(5, "Party wait", prefs.PartyWaitCommand);
        var resumeCommand = AddField(6, "Party resume", prefs.PartyResumeCommand);

        help.Click += (_, _) => MessageBox.Show(dialog,
            "These settings reproduce MegaMUD's Classic party automation controls. " +
            "Incoming @wait and @ok directives pause and resume the active route unless their corresponding ignore option is enabled.",
            "Party automation help", MessageBoxButton.OK, MessageBoxImage.Information);
        cancel.Click += (_, _) => dialog.Close();
        ok.Click += async (_, _) =>
        {
            static int Parse(TextBox box, int fallback) => int.TryParse(box.Text.Trim(), out var value) ? value : fallback;
            prefs.HelpLeaderBashDoors = helpBash.IsChecked == true;
            prefs.SayPanicWhenLeading = sayPanic.IsChecked == true;
            prefs.IgnorePanicWhenFollowing = ignorePanic.IsChecked == true;
            prefs.IgnorePartyWhenFollowing = ignoreParty.IsChecked == true;
            prefs.IgnoreWaitWhenLeading = ignoreWait.IsChecked == true;
            prefs.MaxMonstersWhenPartying = Parse(maxMonsters, prefs.MaxMonstersWhenPartying);
            prefs.MaxMonsterExperience = Parse(maxExperience, prefs.MaxMonsterExperience);
            prefs.WaitHealthPercent = Parse(waitPercent, prefs.WaitHealthPercent);
            prefs.LeaderWaitMinutes = Parse(waitMinutes, prefs.LeaderWaitMinutes);
            prefs.PartyFrequencySeconds = Parse(parFrequency, prefs.PartyFrequencySeconds);
            prefs.PartyWaitCommand = waitCommand.Text;
            prefs.PartyResumeCommand = resumeCommand.Text;
            if (ViewModel is not null) await ViewModel.SaveClassicPartyPreferencesAsync(session);
            dialog.Close();
        };

        dialog.Content = root;
        dialog.ShowDialog();
    }


    private void HealthOptionsClicked(object sender, RoutedEventArgs e)
    {
        var session = BoundSession;
        if (session is null) return;
        var prefs = session.ClassicHealthPreferences;

        var dialog = new Window
        {
            Owner = this, Title = "Health...", Width = 650, Height = 590,
            MinWidth = 590, MinHeight = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip, Background = SystemColors.ControlBrush,
            Foreground = SystemColors.ControlTextBrush, FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };

        var root = new DockPanel { Margin = new Thickness(10) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var ok = NewDialogButton("OK", 92); var cancel = NewDialogButton("Cancel", 92); var help = NewDialogButton("Help", 92);
        buttons.Children.Add(ok); buttons.Children.Add(cancel); buttons.Children.Add(help); root.Children.Add(buttons);

        var content = new StackPanel();
        var auto = new CheckBox { Content = "Auto-Rest / Heal", IsChecked = prefs.AutoRestHeal, Margin = new Thickness(2, 0, 0, 8) };
        content.Children.Add(auto);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(columns);

        GroupBox MakeThresholdGroup(string title, out Grid grid)
        {
            grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            var group = new GroupBox { Header = title, Padding = new Thickness(8), Content = grid };
            return group;
        }

        var healthGroup = MakeThresholdGroup("Health (HP's)", out var hg);
        Grid.SetColumn(healthGroup, 0); columns.Children.Add(healthGroup);
        var manaGroup = MakeThresholdGroup("Mana/Kai", out var mg);
        Grid.SetColumn(manaGroup, 2); columns.Children.Add(manaGroup);

        TextBox AddThreshold(Grid grid, int row, string label, int value)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
            Grid.SetRow(l, row); grid.Children.Add(l);
            var b = new TextBox { Text = value.ToString(), Height = 24, Margin = new Thickness(0, 2, 0, 2), VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetRow(b, row); Grid.SetColumn(b, 1); grid.Children.Add(b); return b;
        }

        var restHpMax = AddThreshold(hg, 0, "Rest max.", prefs.RestHealthMaximumPercent);
        var restHpBelow = AddThreshold(hg, 1, "Rest if below", prefs.RestHealthBelowPercent);
        var healRestHp = AddThreshold(hg, 2, "Heal (rest)", prefs.HealRestHealthPercent);
        var healMinorHp = AddThreshold(hg, 3, "Heal (Minor)", prefs.HealMinorHealthPercent);
        var healMajorHp = AddThreshold(hg, 4, "Heal (Major)", prefs.HealMajorHealthPercent);
        var runHp = AddThreshold(hg, 5, "Run if below", prefs.RunHealthBelowPercent);
        var hangHp = AddThreshold(hg, 6, "Hang if below", prefs.HangupHealthBelowPercent);

        var restManaMax = AddThreshold(mg, 0, "Rest max.", prefs.RestManaMaximumPercent);
        var restManaBelow = AddThreshold(mg, 1, "Rest if below", prefs.RestManaBelowPercent);
        var healRestMana = AddThreshold(mg, 2, "Heal (rest)", prefs.HealRestManaPercent);
        var healMinorMana = AddThreshold(mg, 3, "Heal (Minor)", prefs.HealMinorManaPercent);
        var healMajorMana = AddThreshold(mg, 4, "Heal (Major)", prefs.HealMajorManaPercent);
        var runMana = AddThreshold(mg, 5, "Run if below", prefs.RunManaBelowPercent);
        var blessMana = AddThreshold(mg, 6, "Bless if above", prefs.BlessManaAbovePercent);

        var lower = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 7; i++) lower.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(lower);

        TextBox AddField(int row, string label, string value)
        {
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
            Grid.SetRow(l, row); lower.Children.Add(l);
            var b = new TextBox { Text = value, Height = 24, Margin = new Thickness(0, 2, 0, 2), VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetRow(b, row); Grid.SetColumn(b, 1); lower.Children.Add(b); return b;
        }

        var healPeriod = AddField(0, "Heal period (seconds)", prefs.HealPeriodSeconds.ToString());
        var preRest = AddField(3, "Pre-rest", prefs.PreRestCommand);
        var postRest = AddField(4, "Post-rest", prefs.PostRestCommand);
        var preMed = AddField(5, "Pre med", prefs.PreMeditateCommand);
        var postMed = AddField(6, "Post med", prefs.PostMeditateCommand);

        var useMeditate = new CheckBox { Content = "Use meditate ability", IsChecked = prefs.UseMeditateAbility, Margin = new Thickness(0, 3, 0, 3) };
        Grid.SetRow(useMeditate, 1); Grid.SetColumn(useMeditate, 1); lower.Children.Add(useMeditate);
        var meditateFirst = new CheckBox { Content = "Meditate before resting", IsChecked = prefs.MeditateBeforeResting, Margin = new Thickness(0, 3, 0, 3) };
        Grid.SetRow(meditateFirst, 2); Grid.SetColumn(meditateFirst, 1); lower.Children.Add(meditateFirst);

        root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        help.Click += (_, _) => MessageBox.Show(dialog,
            "Health automation uses captured HP, mana/Kai, learned spells and active effects. It never guesses an unknown spell and Emergency Stop blocks every automatic recovery command.",
            "Health automation help", MessageBoxButton.OK, MessageBoxImage.Information);
        cancel.Click += (_, _) => dialog.Close();
        ok.Click += async (_, _) =>
        {
            static int Parse(TextBox box, int fallback) => int.TryParse(box.Text.Trim(), out var value) ? value : fallback;
            prefs.AutoRestHeal = auto.IsChecked == true;
            prefs.RestHealthMaximumPercent = Parse(restHpMax, prefs.RestHealthMaximumPercent);
            prefs.RestHealthBelowPercent = Parse(restHpBelow, prefs.RestHealthBelowPercent);
            prefs.HealRestHealthPercent = Parse(healRestHp, prefs.HealRestHealthPercent);
            prefs.HealMinorHealthPercent = Parse(healMinorHp, prefs.HealMinorHealthPercent);
            prefs.HealMajorHealthPercent = Parse(healMajorHp, prefs.HealMajorHealthPercent);
            prefs.RunHealthBelowPercent = Parse(runHp, prefs.RunHealthBelowPercent);
            prefs.HangupHealthBelowPercent = Parse(hangHp, prefs.HangupHealthBelowPercent);
            prefs.RestManaMaximumPercent = Parse(restManaMax, prefs.RestManaMaximumPercent);
            prefs.RestManaBelowPercent = Parse(restManaBelow, prefs.RestManaBelowPercent);
            prefs.HealRestManaPercent = Parse(healRestMana, prefs.HealRestManaPercent);
            prefs.HealMinorManaPercent = Parse(healMinorMana, prefs.HealMinorManaPercent);
            prefs.HealMajorManaPercent = Parse(healMajorMana, prefs.HealMajorManaPercent);
            prefs.RunManaBelowPercent = Parse(runMana, prefs.RunManaBelowPercent);
            prefs.BlessManaAbovePercent = Parse(blessMana, prefs.BlessManaAbovePercent);
            prefs.HealPeriodSeconds = Parse(healPeriod, prefs.HealPeriodSeconds);
            prefs.UseMeditateAbility = useMeditate.IsChecked == true;
            prefs.MeditateBeforeResting = meditateFirst.IsChecked == true;
            prefs.PreRestCommand = preRest.Text; prefs.PostRestCommand = postRest.Text;
            prefs.PreMeditateCommand = preMed.Text; prefs.PostMeditateCommand = postMed.Text;
            if (ViewModel is not null) await ViewModel.SaveClassicHealthPreferencesAsync(session);
            dialog.Close();
        };

        dialog.Content = root;
        dialog.ShowDialog();
    }

    private async void AutoToggleClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.SetMasterAutomationAsync(!ViewModel.AutomationProfile.Enabled);
    }

    private async void StopAllClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.StopAllAutomationAsync();
    }

    private async void QuickAutomationClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not MenuItem item || item.Tag is not string option) return;
        await ViewModel.SetQuickAutomationOptionAsync(option, item.IsChecked);
        RefreshQuickAutomationChecks();
    }

    private void RefreshQuickAutomationChecks()
    {
        if (ViewModel is null) return;
        var options = ViewModel.AutomationOptions;
        AutoCombatMenu.IsChecked = options.Combat.AutoAttack;
        AutoPreAttackMenu.IsChecked = options.Combat.AutoPreAttack;
        AutoNukeMenu.IsChecked = options.Spells.AutoNuke;
        AutoRestHealMenu.IsChecked = options.Health.AutoHeal && options.Health.AutoRest;
        AutoBlessMenu.IsChecked = options.Spells.MaintainBless;
        AutoLightMenu.IsChecked = options.Spells.MaintainRoomLight;
        AutoGetItemsMenu.IsChecked = options.LootAndCash.GetItems;
        AutoGetCashMenu.IsChecked = options.LootAndCash.GetCash;
        AutoSneakMenu.IsChecked = options.Roaming.AutoSneak;
        AutoHideMenu.IsChecked = options.Utility.AutoHide;
        AutoSearchMenu.IsChecked = options.Roaming.AutoSearch;
        AutoTrackMenu.IsChecked = options.Roaming.AutoTrack;
        EventsEnabledMenu.IsChecked = ViewModel.AutomationProfile.Capabilities.TimedEventsEnabled;
        AwayFromKeyboardMenu.IsChecked = ViewModel.AutomationProfile.Capabilities.AwayFromKeyboardEnabled;
    }

    private void AwayFromKeyboardClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var capabilities = ViewModel.AutomationProfile.Capabilities;
        var dialog = new Window
        {
            Title = "Away From Keyboard", Owner = this, Width = 510, Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush
        };
        var root = new StackPanel { Margin = new Thickness(16) };
        var enabled = new CheckBox { Content = "Reply automatically to direct tells", IsChecked = capabilities.AwayFromKeyboardEnabled, Margin = new Thickness(0, 0, 0, 10) };
        var message = new TextBox { Text = capabilities.AwayFromKeyboardMessage, MaxLength = 240, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 75 };
        var cooldown = new TextBox { Text = capabilities.AwayFromKeyboardReplyCooldownSeconds.ToString(), Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(enabled);
        root.Children.Add(new TextBlock { Text = "AFK reply (the client adds [AFK]):", Margin = new Thickness(0, 0, 0, 3) });
        root.Children.Add(message);
        root.Children.Add(new TextBlock { Text = "Seconds before replying to the same player again (30–86400):", Margin = new Thickness(0, 10, 0, 3) });
        root.Children.Add(cooldown);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = NewDialogButton("Cancel", 72); cancel.IsCancel = true;
        var save = NewDialogButton("Save", 72); save.IsDefault = true;
        buttons.Children.Add(cancel); buttons.Children.Add(save); root.Children.Add(buttons);
        save.Click += async (_, _) =>
        {
            capabilities.AwayFromKeyboardEnabled = enabled.IsChecked == true;
            if (capabilities.AwayFromKeyboardEnabled) capabilities.CommunicationsAutomationEnabled = true;
            capabilities.AwayFromKeyboardMessage = string.Join(" ", message.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            capabilities.AwayFromKeyboardReplyCooldownSeconds = int.TryParse(cooldown.Text, out var seconds) ? Math.Clamp(seconds, 30, 86400) : 300;
            await ViewModel.SaveAutomationAsync();
            RefreshQuickAutomationChecks();
            dialog.Close();
        };
        dialog.Content = root;
        dialog.ShowDialog();
        RefreshQuickAutomationChecks();
    }

    private async void BlessToggleClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.SetQuickAutomationOptionAsync("bless", !ViewModel.AutomationOptions.Spells.MaintainBless);
        RefreshQuickAutomationChecks();
    }

    private async void LightToggleClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.SetQuickAutomationOptionAsync("light", !ViewModel.AutomationOptions.Spells.MaintainRoomLight);
        RefreshQuickAutomationChecks();
    }

    private void AutomationControlsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var capabilities = ViewModel.AutomationProfile.Capabilities;
        var dialog = new Window
        {
            Title = "Reforged Automation Controls",
            Owner = this,
            Width = 470,
            Height = 690,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White
        };
        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(new TextBlock
        {
            Text = "Independent client automation switches",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        root.Children.Add(new TextBlock
        {
            Text = "These settings are saved per client profile. Each verified command family can be enabled independently, and Emergency Stop overrides all of them.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });
        var enabledNames = new List<string>();
        if (capabilities.NavigationEnabled) enabledNames.Add("Navigation");
        if (capabilities.CombatEnabled) enabledNames.Add("Combat");
        if (capabilities.HealthRecoveryEnabled) enabledNames.Add("Recovery");
        if (capabilities.SpellMaintenanceEnabled) enabledNames.Add("Maintained spells");
        if (capabilities.PartyAutomationEnabled) enabledNames.Add("Party");
        if (capabilities.LootAutomationEnabled) enabledNames.Add("Loot");
        if (capabilities.RoamingAutomationEnabled) enabledNames.Add("Roaming");
        if (capabilities.PvpAutomationEnabled) enabledNames.Add("PvP");
        if (capabilities.CommunicationsAutomationEnabled) enabledNames.Add("Communication");
        root.Children.Add(new TextBlock
        {
            Text = capabilities.EmergencyStop
                ? "CURRENT STATUS: EMERGENCY STOP — all automatic commands are blocked."
                : $"CURRENT STATUS: Running switches — {(enabledNames.Count == 0 ? "none" : string.Join(", ", enabledNames))}.",
            Foreground = capabilities.EmergencyStop ? Brushes.DarkRed : Brushes.DarkGreen,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var aliases = new CheckBox { Content = "Aliases", IsChecked = capabilities.AliasesEnabled, Margin = new Thickness(3) };
        var macros = new CheckBox { Content = "Approved macros", IsChecked = capabilities.MacrosEnabled, Margin = new Thickness(3) };
        var triggers = new CheckBox { Content = "Text triggers", IsChecked = capabilities.TextTriggersEnabled, Margin = new Thickness(3) };
        var eventsEnabled = new CheckBox { Content = "Timed events", IsChecked = capabilities.TimedEventsEnabled, Margin = new Thickness(3) };
        var navigation = new CheckBox
        {
            Content = "Verified navigation (GOTO, loops, backtrack and recovery)",
            IsChecked = capabilities.NavigationEnabled, Margin = new Thickness(3),
            ToolTip = "Runs only decoded or calculated routes and stops when the confirmed room does not match."
        };
        var combat = new CheckBox
        {
            Content = "Verified combat automation",
            IsChecked = capabilities.CombatEnabled, Margin = new Thickness(3),
            ToolTip = "Targets only living hostile creatures reported by the connected server."
        };
        var healthRecovery = new CheckBox
        {
            Content = "Health, healing and recovery",
            IsChecked = capabilities.HealthRecoveryEnabled, Margin = new Thickness(3),
            ToolTip = "Uses captured HP, mana/Kai, learned spells and active effects."
        };
        var spellMaintenance = new CheckBox
        {
            Content = "Buffs and maintained spells",
            IsChecked = capabilities.SpellMaintenanceEnabled, Margin = new Thickness(3),
            ToolTip = "Maintains configured blessing, armor and room-light spells only when they are learned, affordable and absent from authoritative live state."
        };
        var partyAutomation = new CheckBox
        {
            Content = "Party coordination and Defend",
            IsChecked = capabilities.PartyAutomationEnabled, Margin = new Thickness(3),
            ToolTip = "Uses the authoritative party roster and only accepts invitations from the configured leader or trusted players."
        };
        var lootAutomation = new CheckBox
        {
            Content = "Loot, cash, selling and deposits",
            IsChecked = capabilities.LootAutomationEnabled, Margin = new Thickness(3),
            ToolTip = "Uses live room items, explicit filters, encumbrance limits and decoded bank/shop services."
        };
        var roamingAutomation = new CheckBox
        {
            Content = "Room-entry actions and roaming utility",
            IsChecked = capabilities.RoamingAutomationEnabled, Margin = new Thickness(3),
            ToolTip = "Runs configured search, sneak, hide, track and proven trap actions after a room is confirmed."
        };
        var pvpAutomation = new CheckBox
        {
            Content = "PvP responses and party defense",
            IsChecked = capabilities.PvpAutomationEnabled, Margin = new Thickness(3),
            ToolTip = "Responds only to verified same-room attackers when SYSOP PvP is enabled and the room is not proven safe."
        };
        var communicationsAutomation = new CheckBox
        {
            Content = "AFK replies and trusted communication",
            IsChecked = capabilities.CommunicationsAutomationEnabled, Margin = new Thickness(3),
            ToolTip = "Allows AFK replies and explicitly authorized coordination commands."
        };
        var emergency = new CheckBox
        {
            Content = "EMERGENCY STOP — block every automatic command",
            IsChecked = capabilities.EmergencyStop,
            Foreground = Brushes.DarkRed,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(3, 12, 3, 3)
        };
        root.Children.Add(aliases);
        root.Children.Add(macros);
        root.Children.Add(triggers);
        root.Children.Add(eventsEnabled);
        root.Children.Add(navigation);
        root.Children.Add(combat);
        root.Children.Add(healthRecovery);
        root.Children.Add(spellMaintenance);
        root.Children.Add(partyAutomation);
        root.Children.Add(lootAutomation);
        root.Children.Add(roamingAutomation);
        root.Children.Add(pvpAutomation);
        root.Children.Add(communicationsAutomation);
        root.Children.Add(emergency);

        root.Children.Add(new TextBlock { Text = "Aliases — one per line: shortcut = command", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 3) });
        var aliasEditor = new TextBox
        {
            Text = string.Join(Environment.NewLine, capabilities.Aliases.Select(alias => $"{alias.Name} = {alias.Command}")),
            Height = 62,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.Children.Add(aliasEditor);
        root.Children.Add(new TextBlock { Text = "Approved macros — one per line: name = command ; command", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 3) });
        var macroEditor = new TextBox
        {
            Text = string.Join(Environment.NewLine, capabilities.Macros.Select(macro => $"{macro.Name} = {string.Join(" ; ", macro.Commands)}")),
            Height = 62,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.Children.Add(macroEditor);
        root.Children.Add(new TextBlock { Text = "Text triggers — one per line: phrase => command | cooldown seconds", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 3) });
        var triggerEditor = new TextBox
        {
            Text = string.Join(Environment.NewLine, capabilities.TextTriggers.Select(trigger => $"{trigger.MatchText} => {trigger.Command} | {trigger.CooldownSeconds}")),
            Height = 72,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.Children.Add(triggerEditor);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancel", MinWidth = 82, Margin = new Thickness(4), IsCancel = true };
        var save = new Button { Content = "Save", MinWidth = 82, Margin = new Thickness(4), IsDefault = true };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        root.Children.Add(buttons);
        save.Click += async (_, _) =>
        {
            capabilities.AliasesEnabled = aliases.IsChecked == true;
            capabilities.MacrosEnabled = macros.IsChecked == true;
            capabilities.TextTriggersEnabled = triggers.IsChecked == true;
            capabilities.TimedEventsEnabled = eventsEnabled.IsChecked == true;
            capabilities.NavigationEnabled = navigation.IsChecked == true;
            capabilities.CombatEnabled = combat.IsChecked == true;
            capabilities.HealthRecoveryEnabled = healthRecovery.IsChecked == true;
            capabilities.SpellMaintenanceEnabled = spellMaintenance.IsChecked == true;
            capabilities.PartyAutomationEnabled = partyAutomation.IsChecked == true;
            capabilities.LootAutomationEnabled = lootAutomation.IsChecked == true;
            capabilities.RoamingAutomationEnabled = roamingAutomation.IsChecked == true;
            capabilities.PvpAutomationEnabled = pvpAutomation.IsChecked == true;
            capabilities.CommunicationsAutomationEnabled = communicationsAutomation.IsChecked == true;
            capabilities.EmergencyStop = emergency.IsChecked == true;
            capabilities.Aliases.Clear();
            foreach (var line in aliasEditor.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1) continue;
                var name = line[..separator].Trim();
                var command = line[(separator + 1)..].Trim();
                if (name.Length > 0 && !name.Contains(' ') && command.Length > 0 && !capabilities.Aliases.Any(alias => alias.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    capabilities.Aliases.Add(new ClientCommandAlias { Name = name, Command = command });
            }
            capabilities.Macros.Clear();
            foreach (var line in macroEditor.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1) continue;
                var name = line[..separator].Trim();
                var commands = line[(separator + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (name.Length == 0 || commands.Length == 0 || capabilities.Macros.Any(macro => macro.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                var macro = new ClientCommandMacro { Name = name };
                foreach (var command in commands.Take(25)) macro.Commands.Add(command);
                capabilities.Macros.Add(macro);
            }
            capabilities.TextTriggers.Clear();
            foreach (var line in triggerEditor.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf("=>", StringComparison.Ordinal);
                if (separator <= 0 || separator >= line.Length - 2) continue;
                var matchText = line[..separator].Trim();
                var commandAndCooldown = line[(separator + 2)..].Trim();
                var cooldown = 5;
                var cooldownSeparator = commandAndCooldown.LastIndexOf('|');
                var command = commandAndCooldown;
                if (cooldownSeparator > 0 && int.TryParse(commandAndCooldown[(cooldownSeparator + 1)..].Trim(), out var parsedCooldown))
                {
                    cooldown = Math.Clamp(parsedCooldown, 0, 86400);
                    command = commandAndCooldown[..cooldownSeparator].Trim();
                }
                if (matchText.Length == 0 || command.Length == 0 || capabilities.TextTriggers.Any(trigger => trigger.MatchText.Equals(matchText, StringComparison.OrdinalIgnoreCase))) continue;
                capabilities.TextTriggers.Add(new ClientTextTrigger { MatchText = matchText, Command = command, CooldownSeconds = cooldown });
            }
            if (capabilities.EmergencyStop)
                await ViewModel.StopAllAutomationAsync();
            else
            {
                if (!capabilities.NavigationEnabled)
                    foreach (var session in ViewModel.LiveSessions)
                        ViewModel.StopRoute(session);
                await ViewModel.SaveAutomationAsync();
            }
            dialog.Close();
        };
        dialog.Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        dialog.ShowDialog();
    }

    private void CopyClicked(object sender, RoutedEventArgs e)
    {
        ClassicTerminalSurface?.CopyInputToClipboard();
        RequestClassicTerminalFocus();
    }

    private void PasteClicked(object sender, RoutedEventArgs e)
    {
        ClassicTerminalSurface?.PasteFromClipboard();
        RequestClassicTerminalFocus();
    }


    private void ClientStatusClicked(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Reforged client control coverage\n\n" +
            "Working now:\n" +
            "• Connect, disconnect, hangup, and exit\n" +
            "• Manual terminal input and clipboard paste/copy of command input\n" +
            "• Back-scroll and terminal log export\n" +
            "• Clear screen\n" +
            "• Find, conversations, online players, live map, statistics, calculators, spell book, and game data\n" +
            "• Client, combat, party, and health configuration windows\n" +
            "• Stop All\n\n" +
            "BBS-only upload, download, and hangup suppression controls are intentionally absent. " +
            "Who Is Home is reserved for the server-backed boss/home tracker. " +
            "Automation capabilities are controlled independently in Client Options. Emergency Stop blocks every automatic command without blocking manual play.",
            "Reforged Client Status",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void HelpCenterClicked(object sender, RoutedEventArgs e) => ShowHelpCenter();

    private void ShowHelpCenter(string initialQuery = "")
    {
        var dialog = new Window
        {
            Title = "Reforged Help Center",
            Owner = this,
            Width = 940,
            Height = 680,
            MinWidth = 680,
            MinHeight = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush,
            Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13
        };
        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(285) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var search = new TextBox
        {
            Text = initialQuery,
            MinHeight = 30,
            Padding = new Thickness(7, 4, 7, 4),
            ToolTip = "Search topics, commands, and features",
            VerticalContentAlignment = VerticalAlignment.Center
        };
        System.Windows.Automation.AutomationProperties.SetName(search, "Search Reforged help");
        Grid.SetColumnSpan(search, 3);
        root.Children.Add(search);

        var topics = new ListBox
        {
            Margin = new Thickness(0, 10, 0, 10),
            DisplayMemberPath = nameof(ClientHelpTopic.Title),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        System.Windows.Automation.AutomationProperties.SetName(topics, "Help topics");
        Grid.SetRow(topics, 1);
        root.Children.Add(topics);

        var article = new TextBox
        {
            Margin = new Thickness(0, 10, 0, 10),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(14),
            Background = Brushes.White,
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            FontSize = 14
        };
        System.Windows.Automation.AutomationProperties.SetName(article, "Selected help article");
        Grid.SetRow(article, 1);
        Grid.SetColumn(article, 2);
        root.Children.Add(article);

        var resultStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(resultStatus, 2);
        root.Children.Add(resultStatus);
        var close = NewDialogButton("Close", 84);
        close.IsDefault = true;
        close.IsCancel = true;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(close, 2);
        Grid.SetColumn(close, 2);
        root.Children.Add(close);

        void RenderArticle()
        {
            if (topics.SelectedItem is not ClientHelpTopic topic)
            {
                article.Text = "No help topic matches this search. Try a command such as EXP, PAR, WHOHOME, GOTO, or EMERGENCY STOP.";
                return;
            }
            article.Text = $"{topic.Title}\r\n{topic.Category}\r\n\r\n{topic.Summary}\r\n\r\n{topic.Content}";
            article.ScrollToHome();
        }

        void RefreshTopics()
        {
            var selectedId = (topics.SelectedItem as ClientHelpTopic)?.Id;
            var results = ClientHelpCatalog.Search(search.Text);
            topics.ItemsSource = results;
            topics.SelectedItem = results.FirstOrDefault(topic => topic.Id == selectedId) ?? results.FirstOrDefault();
            resultStatus.Text = results.Count == ClientHelpCatalog.Topics.Count
                ? $"{results.Count} help topics"
                : $"{results.Count} matching topic(s)";
            RenderArticle();
        }

        search.TextChanged += (_, _) => RefreshTopics();
        topics.SelectionChanged += (_, _) => RenderArticle();
        close.Click += (_, _) => dialog.Close();
        dialog.Content = root;
        RefreshTopics();
        dialog.Loaded += (_, _) => { search.Focus(); search.SelectAll(); };
        dialog.Show();
    }

    private void KeyboardHelpClicked(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Reforged terminal input\n\n" +
            "Enter — send the current command\n" +
            "Backspace / Delete — edit the command\n" +
            "Left / Right / Home / End — move the command caret\n" +
            "Up / Down — command history\n" +
            "Escape — clear local input\n" +
            "Ctrl+V — paste into the command line\n" +
            "Ctrl+C — copy the current command input (disabled for passwords)\n\n" +
            "Back-scroll supports mouse selection, Ctrl+C, Ctrl+A, and its right-click menu.",
            "Keyboard and Input Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void AboutClicked(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "MajorMUD Reforged Client\n\n" +
            "The single per-session client for MajorMUD Reforged, incorporating the required MegaMUD automation and MMExplorer data features.\n\n" +
            "Manual play, independent automation capabilities, verified routes, live maps, and server-backed Librarium reference tools are integrated in this client.",
            "About Reforged Client",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void GotoClicked(object sender, RoutedEventArgs e) => ShowPathSelector(loopOnly: false);

    private void LoopClicked(object sender, RoutedEventArgs e) => ShowPathSelector(loopOnly: true);

    private void AutoRoamClicked(object sender, RoutedEventArgs e) => ShowPathSelector(loopOnly: true, autoRepeat: true);

    private async void BackTrackClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is null) return;
        await ViewModel.StartBackTrackAsync(BoundSession);
    }

    private async void GetAllClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.RunManualMassActionAsync(BoundSession, "get");
    }

    private async void EquipAllClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.RunManualMassActionAsync(BoundSession, "equip");
    }

    private async void DropAllClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is null) return;
        if (MessageBox.Show(this,
                "Drop every item shown in the latest inventory snapshot?\n\nThe server will validate and drop each item separately.",
                "Confirm Drop All", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await ViewModel.RunManualMassActionAsync(BoundSession, "drop");
    }

    private async void DepositAllClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is null) return;
        if (MessageBox.Show(this,
                "Deposit all carried currency at the bank in this room?",
                "Confirm Deposit All", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await ViewModel.RunManualMassActionAsync(BoundSession, "deposit");
    }

    private void ShowPathSelector(bool loopOnly, bool autoRepeat = false)
    {
        if (ViewModel is null) return;

        var candidates = ViewModel.VisiblePaths
            .Where(path => loopOnly ? IsLoop(path) : !IsLoop(path))
            .OrderBy(path => RegionOf(path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path.EndLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dialog = new Window
        {
            Owner = this,
            Title = autoRepeat ? "Auto-Roam area..." : loopOnly ? "Roam area..." : "Goto location...",
            Width = 390,
            Height = 520,
            MinWidth = 330,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush,
            Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"),
            FontSize = 12,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };

        var root = new DockPanel { Margin = new Thickness(8) };
        var search = new TextBox { Height = 24, Margin = new Thickness(0, 0, 0, 7) };
        DockPanel.SetDock(search, Dock.Top);
        root.Children.Add(search);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 7, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var go = NewDialogButton(autoRepeat ? "Auto" : loopOnly ? "Run" : "Go", 58);
        var run = NewDialogButton("Run", 58);
        var how = NewDialogButton("How?", 58);
        var cancel = NewDialogButton("Cancel", 70);
        buttons.Children.Add(go);
        if (!loopOnly) buttons.Children.Add(run);
        buttons.Children.Add(how);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        var tree = new TreeView
        {
            Background = Brushes.White,
            BorderBrush = SystemColors.ActiveBorderBrush,
            BorderThickness = new Thickness(1)
        };
        root.Children.Add(tree);
        dialog.Content = root;

        MegaMudPathRecord? selected = null;

        void Populate(string filter)
        {
            tree.Items.Clear();
            var filtered = candidates.Where(path =>
                string.IsNullOrWhiteSpace(filter) ||
                path.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                path.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                RegionOf(path).Contains(filter, StringComparison.OrdinalIgnoreCase));

            foreach (var group in filtered.GroupBy(RegionOf, StringComparer.OrdinalIgnoreCase))
            {
                var folder = new TreeViewItem
                {
                    Header = group.Key,
                    IsExpanded = !string.IsNullOrWhiteSpace(filter)
                };

                foreach (var path in group)
                {
                    var label = loopOnly
                        ? $"{CleanLabel(path.StartLabel)}  [{path.FileName}]"
                        : $"{CleanLabel(path.EndLabel)}  [{path.FileName}]";
                    folder.Items.Add(new TreeViewItem { Header = label, Tag = path });
                }
                tree.Items.Add(folder);
            }
        }

        tree.SelectedItemChanged += (_, _) =>
        {
            selected = (tree.SelectedItem as TreeViewItem)?.Tag as MegaMudPathRecord;
            go.IsEnabled = selected is not null;
            run.IsEnabled = selected is not null;
            how.IsEnabled = selected is not null;
        };
        search.TextChanged += (_, _) => Populate(search.Text.Trim());
        tree.MouseDoubleClick += async (_, _) =>
        {
            if (selected is null) return;
            await StartPathAsync(selected, loopOnly, autoRepeat);
            dialog.Close();
        };

        async Task ExecuteSelectedAsync()
        {
            if (selected is null) return;
            await StartPathAsync(selected, loopOnly, autoRepeat);
            dialog.Close();
        }

        go.Click += async (_, _) => await ExecuteSelectedAsync();
        run.Click += async (_, _) => await ExecuteSelectedAsync();
        how.Click += (_, _) =>
        {
            if (selected is null) return;
            MessageBox.Show(dialog,
                $"From: {selected.StartLabel}\nTo: {selected.EndLabel}\nFile: {selected.FileName}\nAuthor: {selected.Author}\nSteps: {selected.Steps.Count}\n\n{selected.StepSummary}",
                "Path details", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        cancel.Click += (_, _) => dialog.Close();

        Populate(string.Empty);
        go.IsEnabled = false;
        run.IsEnabled = false;
        how.IsEnabled = false;
        dialog.ShowDialog();
    }

    private async Task StartPathAsync(MegaMudPathRecord path, bool requireExactStart, bool autoRepeat = false)
    {
        if (ViewModel is null || BoundSession is null) return;
        if (autoRepeat) await ViewModel.StartAutoRoamAsync(BoundSession, path);
        else await ViewModel.StartRouteToDestinationAsync(BoundSession, path, requireExactStart);
    }

    private static Button NewDialogButton(string text, double width) => new()
    {
        Content = text,
        Width = width,
        Height = 25,
        Margin = new Thickness(0, 0, 6, 0),
        Background = SystemColors.ControlBrush,
        Foreground = SystemColors.ControlTextBrush
    };

    private void SpellBookClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session || ViewModel is null) return;

        var dialog = new Window
        {
            Title = $"Reforged Spell Book — {session.CharacterName}",
            Owner = this,
            Width = 640,
            Height = 470,
            MinWidth = 480,
            MinHeight = 330,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush,
            Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"),
            FontSize = 12
        };

        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        var spellHeader = new TextBlock { Text = "Learned spells", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
        var effectHeader = new TextBlock { Text = "Active effects from STATS", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetColumn(effectHeader, 2);
        root.Children.Add(spellHeader);
        root.Children.Add(effectHeader);

        var spells = NewStateList(session.LearnedSpells);
        var effects = NewStateList(session.ActiveEffects);
        Grid.SetRow(spells, 1);
        Grid.SetRow(effects, 1);
        Grid.SetColumn(effects, 2);
        root.Children.Add(spells);
        root.Children.Add(effects);

        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        Grid.SetRow(footer, 2);
        Grid.SetColumnSpan(footer, 3);
        var refreshSpells = NewDialogButton("Refresh spells", 100);
        var refreshEffects = NewDialogButton("Refresh effects", 100);
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(refreshSpells);
        footer.Children.Add(refreshEffects);
        footer.Children.Add(close);
        root.Children.Add(footer);

        refreshSpells.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata spells");
        refreshEffects.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata stats");
        close.Click += (_, _) => dialog.Close();
        dialog.Content = root;
        dialog.Show();
    }

    private void PartyStatusClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session || ViewModel is null) return;
        var dialog = new Window
        {
            Title = $"Reforged Party Status — {session.CharacterName}", Owner = this,
            Width = 720, Height = 430, MinWidth = 560, MinHeight = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var refresh = NewDialogButton("Refresh (PAR)", 100);
        var close = NewDialogButton("Close", 70);
        buttons.Children.Add(refresh); buttons.Children.Add(close); root.Children.Add(buttons);
        var grid = new DataGrid
        {
            ItemsSource = session.PartyMembers, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(ClientPartyMemberState.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Class", Binding = new Binding(nameof(ClientPartyMemberState.ClassName)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Vitals", Binding = new Binding(nameof(ClientPartyMemberState.Vitals)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Rank", Binding = new Binding(nameof(ClientPartyMemberState.Rank)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "State", Binding = new Binding(nameof(ClientPartyMemberState.State)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        root.Children.Add(grid);
        refresh.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata party");
        close.Click += (_, _) => dialog.Close();
        dialog.Content = root;
        dialog.Show();
    }

    private void PlayerStatisticsClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session) return;
        ShowLiveStatisticsWindow($"Reforged Player Statistics — {session.CharacterName}", () => BuildPlayerStatistics(session));
    }

    private void SessionStatisticsClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session) return;
        ShowLiveStatisticsWindow($"Reforged Session Statistics — {session.CharacterName}", () => BuildSessionStatistics(session));
    }

    private void ShowLiveStatisticsWindow(string title, Func<string> render)
    {
        var dialog = new Window
        {
            Title = title, Owner = this, Width = 560, Height = 500,
            MinWidth = 440, MinHeight = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Consolas"), FontSize = 13
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Bottom);
        var output = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White, Foreground = Brushes.Black,
            Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10)
        };
        root.Children.Add(close);
        root.Children.Add(output);
        dialog.Content = root;

        void Refresh() => output.Text = render();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Refresh();
        dialog.Closed += (_, _) => timer.Stop();
        close.Click += (_, _) => dialog.Close();
        Refresh();
        timer.Start();
        dialog.Show();
    }

    private static string BuildPlayerStatistics(ClientSessionConnection session)
    {
        var room = DisplayOrUnknown(session.CurrentRoomName);
        var roomKey = DisplayOrUnknown(session.CurrentRoomKey);
        var exits = session.CurrentRoomExits.Count == 0 ? "None captured" : string.Join(", ", session.CurrentRoomExits);
        var encumbrance = DisplayOrUnknown(session.EncumbranceBand);
        return $"Character        {DisplayOrUnknown(session.CharacterName)}\n" +
               $"Connection       {session.ConnectionState}\n\n" +
               $"Level            {session.CharacterLevel}\n" +
               $"Experience       {session.ExperienceCurrent:N0}" + (session.ExperienceNeeded > 0 ? $" / {session.ExperienceNeeded:N0}" : string.Empty) + "\n" +
               $"Health           {session.HealthCurrent:N0} / {session.HealthMaximum:N0} ({session.HealthPercent}%)\n" +
               $"Mana / Kai       {session.ManaCurrent:N0} / {session.ManaMaximum:N0} ({session.ManaPercent}%)\n" +
               $"Currency         {session.CarriedCurrency:N0}\n" +
               $"Encumbrance      {session.CarriedWeight:N0} / {session.CarryingCapacity:N0} ({session.EncumbrancePercent}%, {encumbrance})\n\n" +
               $"Inventory        {session.InventoryItems.Count:N0} captured item(s)\n" +
               $"Equipment        {session.EquippedItems.Count:N0} captured item(s)\n" +
               $"Learned spells   {session.LearnedSpells.Count:N0}\n" +
               $"Active effects   {session.ActiveEffects.Count:N0}\n" +
               $"Party members    {session.PartyMembers.Count:N0}\n\n" +
               $"Room             {room}\n" +
               $"Room key         {roomKey}\n" +
               $"Exits            {exits}\n\n" +
               $"Stats captured   {FormatTimestamp(session.ExperienceUpdatedAt)}\n" +
               $"Items captured   {FormatTimestamp(session.InventoryUpdatedAt)}\n" +
               $"Spells captured  {FormatTimestamp(session.SpellbookUpdatedAt)}\n" +
               $"Effects captured {FormatTimestamp(session.ActiveEffectsUpdatedAt)}";
    }

    private string BuildSessionStatistics(ClientSessionConnection session)
    {
        var elapsed = session.ConnectedAt is { } connectedAt ? DateTimeOffset.Now - connectedAt : TimeSpan.Zero;
        var experiencePerHour = elapsed.TotalSeconds > 0 ? session.SessionExperienceGained / elapsed.TotalHours : 0;
        var activity = BuildAutomationActivity(session);
        return $"State              {session.ConnectionState}\n" +
               $"Connected at       {FormatTimestamp(session.ConnectedAt)}\n" +
               $"Elapsed            {FormatDuration(elapsed)}\n\n" +
               $"Experience gained  {session.SessionExperienceGained:N0}\n" +
               $"Experience / hour  {experiencePerHour:N0}\n" +
               $"Combat events      {session.SessionCombatEventCount:N0}\n" +
               $"Rooms observed     {session.SessionRoomsObserved:N0}\n" +
               $"Commands sent      {session.CommandHistory.Count:N0}\n" +
               $"Terminal entries   {session.TerminalEntries.Count:N0}\n\n" +
               $"Last command       {DisplayOrUnknown(session.LastCommandText)}\n" +
               $"Command origin     {session.LastCommandOrigin}\n" +
               $"Last command at    {FormatTimestamp(session.LastCommandAt)}\n" +
               $"Automation state   {activity.State}\n" +
               $"Enabled systems    {activity.EnabledSystems}\n" +
               $"Last auto action   {activity.LastAction}\n" +
               $"Action reason      {activity.Reason}\n" +
               $"Last combat event  {session.LastCombatEvent}" + (session.LastCombatAmount > 0 ? $" ({session.LastCombatAmount:N0})" : string.Empty) + "\n" +
               $"Last combat at     {FormatTimestamp(session.LastCombatEventAt)}\n" +
               $"Combat automation  {DisplayOrUnknown(session.CombatAutomationStatus)}\n" +
               $"Survival automation {DisplayOrUnknown(session.SurvivalAutomationStatus)}\n" +
               $"Spell automation    {DisplayOrUnknown(session.SpellAutomationStatus)}\n" +
               $"Party automation    {DisplayOrUnknown(session.PartyAutomationStatus)}\n" +
               $"PvP automation      {DisplayOrUnknown(session.PvpAutomationStatus)}\n" +
               $"Roaming automation  {DisplayOrUnknown(session.RoamingAutomationStatus)}\n" +
               $"Loot automation     {DisplayOrUnknown(session.LootAutomationStatus)}\n" +
               $"Last server data   {FormatTimestamp(session.LastServerActivityAt)}";
    }

    private (string State, string EnabledSystems, string LastAction, string Reason) BuildAutomationActivity(ClientSessionConnection session)
    {
        var capabilities = ViewModel?.AutomationProfile.Capabilities;
        if (capabilities is null) return ("Unavailable", "None", "None", "Client profile is unavailable.");
        var enabled = new List<string>();
        if (capabilities.NavigationEnabled) enabled.Add("Navigation");
        if (capabilities.CombatEnabled) enabled.Add("Combat");
        if (capabilities.HealthRecoveryEnabled) enabled.Add("Recovery");
        if (capabilities.SpellMaintenanceEnabled) enabled.Add("Spells");
        if (capabilities.PartyAutomationEnabled) enabled.Add("Party");
        if (capabilities.LootAutomationEnabled) enabled.Add("Loot");
        if (capabilities.RoamingAutomationEnabled) enabled.Add("Roaming");
        if (capabilities.PvpAutomationEnabled) enabled.Add("PvP");
        if (capabilities.CommunicationsAutomationEnabled) enabled.Add("Communication");
        if (capabilities.TimedEventsEnabled) enabled.Add("Events");
        var statuses = new[] { session.CombatAutomationStatus, session.SurvivalAutomationStatus, session.SpellAutomationStatus,
            session.PartyAutomationStatus, session.PvpAutomationStatus, session.RoamingAutomationStatus, session.LootAutomationStatus }
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var reason = statuses.LastOrDefault() ?? "No automatic decision has been recorded for this connection.";
        var state = session.ConnectionState != ClientConnectionState.Connected ? "Disconnected" :
            capabilities.EmergencyStop ? "Emergency stopped" :
            statuses.Any(value => value.Contains("failed", StringComparison.OrdinalIgnoreCase) || value.Contains("stopped safely", StringComparison.OrdinalIgnoreCase)) ? "Failed safely" :
            statuses.Any(value => value.Contains("waiting", StringComparison.OrdinalIgnoreCase) || value.Contains("paused", StringComparison.OrdinalIgnoreCase)) ? "Waiting" :
            session.LastCommandOrigin is ClientCommandOrigin.ClientAutomation or ClientCommandOrigin.RouteAutomation &&
                session.LastCommandAt is { } sentAt && DateTimeOffset.Now - sentAt < TimeSpan.FromSeconds(10) ? "Running" :
            enabled.Count > 0 ? "Enabled / idle" : "Disabled";
        var lastAction = session.LastCommandOrigin is ClientCommandOrigin.ClientAutomation or ClientCommandOrigin.RouteAutomation
            ? $"{DisplayOrUnknown(session.LastCommandText)} at {FormatTimestamp(session.LastCommandAt)}"
            : "No automatic command captured.";
        return (state, enabled.Count == 0 ? "None" : string.Join(", ", enabled), lastAction, reason);
    }

    private static string DisplayOrUnknown(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    private static string FormatTimestamp(DateTimeOffset? value) => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Not captured";
    private static string FormatDuration(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    private void TimeAnalysisClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session) return;
        ShowLiveStatisticsWindow($"Reforged Time Analysis — {session.CharacterName}", () => BuildTimeAnalysis(session));
    }

    private static string BuildTimeAnalysis(ClientSessionConnection session)
    {
        var now = DateTimeOffset.Now;
        var elapsed = session.ConnectedAt is { } connectedAt ? now - connectedAt : TimeSpan.Zero;
        static string Age(DateTimeOffset nowValue, DateTimeOffset? value) =>
            value is { } timestamp ? $"{FormatDuration(nowValue - timestamp)} ago" : "Not observed";
        var commandsPerHour = elapsed.TotalSeconds > 0 ? session.CommandHistory.Count / elapsed.TotalHours : 0;
        var roomsPerHour = elapsed.TotalSeconds > 0 ? session.SessionRoomsObserved / elapsed.TotalHours : 0;
        var combatsPerHour = elapsed.TotalSeconds > 0 ? session.SessionCombatEventCount / elapsed.TotalHours : 0;
        return $"Connected duration   {FormatDuration(elapsed)}\n\n" +
               $"Commands / hour      {commandsPerHour:N1}\n" +
               $"Rooms / hour         {roomsPerHour:N1}\n" +
               $"Combat events / hour {combatsPerHour:N1}\n\n" +
               $"Since server data    {Age(now, session.LastServerActivityAt)}\n" +
               $"Since command        {Age(now, session.LastCommandAt)}\n" +
               $"Since combat event   {Age(now, session.LastCombatEventAt)}\n" +
               $"Since room display   {Age(now, session.RoomUpdatedAt)}\n" +
               $"Since stat capture   {Age(now, session.ExperienceUpdatedAt)}\n" +
               $"Since item capture   {Age(now, session.InventoryUpdatedAt)}\n" +
               $"Since spell capture  {Age(now, session.SpellbookUpdatedAt)}\n" +
               $"Since party capture  {Age(now, session.PartyUpdatedAt)}";
    }

    private void ExperienceRateGraphClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"Reforged Experience Rate — {session.CharacterName}", Owner = this,
            Width = 760, Height = 480, MinWidth = 520, MinHeight = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var summary = new TextBlock { Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.Bold };
        DockPanel.SetDock(summary, Dock.Top);
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Bottom);
        var canvas = new Canvas { Background = Brushes.White, ClipToBounds = true };
        root.Children.Add(summary);
        root.Children.Add(close);
        root.Children.Add(canvas);
        dialog.Content = root;

        void RenderGraph()
        {
            canvas.Children.Clear();
            var width = Math.Max(100, canvas.ActualWidth);
            var height = Math.Max(100, canvas.ActualHeight);
            const double left = 58, right = 18, top = 18, bottom = 38;
            var plotWidth = Math.Max(1, width - left - right);
            var plotHeight = Math.Max(1, height - top - bottom);
            var now = DateTimeOffset.Now;
            var start = session.ConnectedAt ?? now;
            var totalSeconds = Math.Max(1, (now - start).TotalSeconds);
            var maximum = Math.Max(1L, session.SessionExperienceGained);

            AddGraphLine(canvas, left, top, left, top + plotHeight, Brushes.Gray);
            AddGraphLine(canvas, left, top + plotHeight, left + plotWidth, top + plotHeight, Brushes.Gray);
            AddGraphLabel(canvas, "XP", 8, top - 4);
            AddGraphLabel(canvas, maximum.ToString("N0"), 4, top + 10);
            AddGraphLabel(canvas, "0", 35, top + plotHeight - 10);
            AddGraphLabel(canvas, "0:00", left - 5, top + plotHeight + 6);
            AddGraphLabel(canvas, FormatDuration(now - start), left + plotWidth - 62, top + plotHeight + 6);

            var points = new PointCollection();
            foreach (var sample in session.SessionExperienceSamples)
            {
                var seconds = Math.Clamp((sample.Timestamp - start).TotalSeconds, 0, totalSeconds);
                var x = left + seconds / totalSeconds * plotWidth;
                var y = top + plotHeight - Math.Clamp(sample.CumulativeExperience / (double)maximum, 0, 1) * plotHeight;
                points.Add(new Point(x, y));
            }
            if (points.Count == 0) points.Add(new Point(left, top + plotHeight));
            points.Add(new Point(left + plotWidth, points[^1].Y));
            canvas.Children.Add(new System.Windows.Shapes.Polyline
            {
                Points = points, Stroke = Brushes.DarkGreen, StrokeThickness = 2
            });

            var rate = totalSeconds > 0 ? session.SessionExperienceGained / (totalSeconds / 3600d) : 0;
            summary.Text = $"Session XP: {session.SessionExperienceGained:N0}    Current rate: {rate:N0} XP/hour    Verified rewards: {Math.Max(0, session.SessionExperienceSamples.Count - 1):N0}";
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => RenderGraph();
        canvas.SizeChanged += (_, _) => RenderGraph();
        dialog.Closed += (_, _) => timer.Stop();
        close.Click += (_, _) => dialog.Close();
        timer.Start();
        dialog.Show();
    }

    private static void AddGraphLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush brush) =>
        canvas.Children.Add(new System.Windows.Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 1 });

    private static void AddGraphLabel(Canvas canvas, string text, double left, double top)
    {
        var label = new TextBlock { Text = text, Foreground = Brushes.Black, FontSize = 11 };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        canvas.Children.Add(label);
    }

    private void ConversationsClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"Reforged Conversations — {session.CharacterName}", Owner = this,
            Width = 820, Height = 520, MinWidth = 560, MinHeight = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        DockPanel.SetDock(footer, Dock.Bottom);
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(close);
        footer.Children.Add(status);

        var tabs = new TabControl();
        tabs.Items.Add(NewConversationTab("Chat", session.ChatEntries));
        tabs.Items.Add(NewConversationTab("Tells", session.TellEntries));
        tabs.Items.Add(NewConversationTab("Party", session.PartyEntries));
        tabs.Items.Add(NewConversationTab("System", session.SystemEntries));
        root.Children.Add(footer);
        root.Children.Add(tabs);
        dialog.Content = root;

        void RefreshStatus() => status.Text =
            $"Chat {session.ChatEntries.Count:N0}  ·  Tells {session.TellEntries.Count:N0}  ·  Party {session.PartyEntries.Count:N0}  ·  System {session.SystemEntries.Count:N0}";
        NotifyCollectionChangedEventHandler changed = (_, _) => RefreshStatus();
        session.ChatEntries.CollectionChanged += changed;
        session.TellEntries.CollectionChanged += changed;
        session.PartyEntries.CollectionChanged += changed;
        session.SystemEntries.CollectionChanged += changed;
        dialog.Closed += (_, _) =>
        {
            session.ChatEntries.CollectionChanged -= changed;
            session.TellEntries.CollectionChanged -= changed;
            session.PartyEntries.CollectionChanged -= changed;
            session.SystemEntries.CollectionChanged -= changed;
        };
        close.Click += (_, _) => dialog.Close();
        RefreshStatus();
        dialog.Show();
    }

    private static TabItem NewConversationTab(string title, IEnumerable<ClientTerminalEntry> entries)
    {
        var grid = new DataGrid
        {
            ItemsSource = entries, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Time", Binding = new Binding(nameof(ClientTerminalEntry.Timestamp)) { StringFormat = "HH:mm:ss" },
            Width = new DataGridLength(78)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Direction", Binding = new Binding(nameof(ClientTerminalEntry.IsOutgoing)) { Converter = new OutgoingDirectionConverter() },
            Width = new DataGridLength(76)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Message", Binding = new Binding(nameof(ClientTerminalEntry.Text)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        return new TabItem { Header = title, Content = grid };
    }

    private sealed class OutgoingDirectionConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is true ? "Outgoing" : "Incoming";
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private void OnlinePlayersClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session || ViewModel is null) return;
        var dialog = new Window
        {
            Title = $"Reforged Online Players — {session.WorldName}", Owner = this,
            Width = 620, Height = 430, MinWidth = 440, MinHeight = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new TextBlock { Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.Bold };
        DockPanel.SetDock(header, Dock.Top);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var refresh = NewDialogButton("Refresh (WHO)", 110);
        var close = NewDialogButton("Close", 70);
        buttons.Children.Add(refresh);
        buttons.Children.Add(close);
        var grid = new DataGrid
        {
            ItemsSource = session.OnlinePlayers, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(ClientOnlinePlayerState.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Class", Binding = new Binding(nameof(ClientOnlinePlayerState.ClassName)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Level", Binding = new Binding(nameof(ClientOnlinePlayerState.Level)), Width = new DataGridLength(80) });
        root.Children.Add(header);
        root.Children.Add(buttons);
        root.Children.Add(grid);
        dialog.Content = root;

        void RefreshHeader() => header.Text = session.OnlinePlayersUpdatedAt is { } updated
            ? $"{session.OnlinePlayers.Count:N0} online · Captured {updated.ToLocalTime():HH:mm:ss}"
            : "No WHO roster captured yet.";
        NotifyCollectionChangedEventHandler changed = (_, _) => RefreshHeader();
        session.OnlinePlayers.CollectionChanged += changed;
        dialog.Closed += (_, _) => session.OnlinePlayers.CollectionChanged -= changed;
        refresh.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata who");
        close.Click += (_, _) => dialog.Close();
        RefreshHeader();
        dialog.Show();
    }

    private void LiveMapClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session || ViewModel is null) return;
        var dialog = new Window
        {
            Title = $"Reforged Live Map — {session.CharacterName}", Owner = this,
            Width = 1180, Height = 760, MinWidth = 760, MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(14, 17, 24)), Foreground = Brushes.Gainsboro,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var header = new TextBlock { FontWeight = FontWeights.Bold, Foreground = Brushes.WhiteSmoke, FontSize = 16, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetColumnSpan(header, 3);
        root.Children.Add(header);
        var canvas = new Canvas { Background = new SolidColorBrush(Color.FromRgb(7, 12, 22)), ClipToBounds = true, MinWidth = 1100, MinHeight = 720 };
        var initialMapZoom = Math.Clamp(ViewModel.MapProfile.Zoom, .25, 3);
        var mapScale = new ScaleTransform(initialMapZoom, initialMapZoom);
        canvas.LayoutTransform = mapScale;
        var mapZoom = initialMapZoom;
        var mapScroll = new ScrollViewer
        {
            Content = canvas, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = canvas.Background, BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)), BorderThickness = new Thickness(1)
        };
        Grid.SetRow(mapScroll, 1);
        root.Children.Add(mapScroll);
        var filteredRooms = new ObservableCollection<ClientRoomMetadata>();
        var rooms = new DataGrid
        {
            ItemsSource = filteredRooms, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)), Foreground = Brushes.Gainsboro,
            RowBackground = new SolidColorBrush(Color.FromRgb(17, 24, 39)), AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(25, 34, 48))
        };
        rooms.Columns.Add(new DataGridTextColumn { Header = "Discovered room", Binding = new Binding(nameof(ClientRoomMetadata.RoomName)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        rooms.Columns.Add(new DataGridTextColumn { Header = "Key", Binding = new Binding(nameof(ClientRoomMetadata.RoomKey)), Width = new DataGridLength(105) });
        var roomSearch = new TextBox { Margin = new Thickness(0, 0, 0, 6), ToolTip = "Search discovered names, keys, notes, NPCs, monsters, and shops" };
        var favoritesOnly = new CheckBox { Content = "Favorites only", Margin = new Thickness(0, 0, 0, 6) };
        var selectedRoomDetail = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 112, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)), Foreground = Brushes.Gainsboro, Padding = new Thickness(6)
        };
        var discoveredPanel = new DockPanel();
        var discoveredTools = new StackPanel();
        DockPanel.SetDock(discoveredTools, Dock.Top);
        DockPanel.SetDock(selectedRoomDetail, Dock.Bottom);
        discoveredTools.Children.Add(roomSearch);
        discoveredTools.Children.Add(favoritesOnly);
        discoveredPanel.Children.Add(discoveredTools);
        discoveredPanel.Children.Add(selectedRoomDetail);
        discoveredPanel.Children.Add(rooms);
        var liveRoom = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)), Foreground = Brushes.Gainsboro,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(7)
        };
        var sideTabs = new TabControl();
        sideTabs.Items.Add(new TabItem { Header = "Current room", Content = liveRoom });
        sideTabs.Items.Add(new TabItem { Header = "Discovered", Content = discoveredPanel });
        var roomCommands = new DataGrid
        {
            ItemsSource = session.CurrentRoomCommandDetails, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)), Foreground = Brushes.Gainsboro,
            RowBackground = new SolidColorBrush(Color.FromRgb(17, 24, 39)), AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(25, 34, 48))
        };
        roomCommands.Columns.Add(new DataGridTextColumn { Header = "Command", Binding = new Binding(nameof(ClientRoomCommandState.Command)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        roomCommands.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Binding(nameof(ClientRoomCommandState.InteractionType)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        roomCommands.Columns.Add(new DataGridTextColumn { Header = "Requirements", Binding = new Binding(nameof(ClientRoomCommandState.Requirements)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
        sideTabs.Items.Add(new TabItem { Header = "Room commands", Content = roomCommands });
        var pvpStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.Bold };
        var pvpPlayers = new ComboBox { ItemsSource = session.CurrentRoomPlayers, Margin = new Thickness(0, 0, 0, 8), ToolTip = "Verified players currently in this room" };
        var pvpAttack = NewDialogButton("Attack selected", 110);
        var pvpFlee = NewDialogButton("Flee", 70);
        var pvpRefresh = NewDialogButton("Refresh PvP", 90);
        var pvpDefendParty = new CheckBox
        {
            Content = "MegaMUD Defend party members",
            IsChecked = ViewModel.AutomationOptions.PvP.DefendPartyMembers,
            Margin = new Thickness(0, 2, 0, 10),
            ToolTip = "Automatically attack a verified same-room player who attacks a current party member. PvP safety and Emergency Stop still apply."
        };
        var pvpButtons = new StackPanel { Orientation = Orientation.Horizontal };
        pvpButtons.Children.Add(pvpAttack);
        pvpButtons.Children.Add(pvpFlee);
        pvpButtons.Children.Add(pvpRefresh);
        var pvpPanel = new StackPanel { Margin = new Thickness(8) };
        pvpPanel.Children.Add(pvpStatus);
        pvpPanel.Children.Add(new TextBlock { Text = "Players in this room", Margin = new Thickness(0, 0, 0, 4) });
        pvpPanel.Children.Add(pvpPlayers);
        pvpPanel.Children.Add(pvpDefendParty);
        pvpPanel.Children.Add(pvpButtons);
        sideTabs.Items.Add(new TabItem { Header = "PvP", Content = pvpPanel });
        var inspectionKey = new TextBox { ToolTip = "Exact map:room key, for example 1:2189", Margin = new Thickness(0, 0, 0, 6) };
        var inspectRoom = NewDialogButton("Inspect live room", 115);
        var inspectionOutput = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)), Foreground = Brushes.Gainsboro, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(6)
        };
        var inspectionPanel = new DockPanel();
        var inspectionTools = new StackPanel();
        DockPanel.SetDock(inspectionTools, Dock.Top);
        inspectionTools.Children.Add(new TextBlock { Text = "Room key (map:room)", FontWeight = FontWeights.Bold });
        inspectionTools.Children.Add(inspectionKey);
        inspectionTools.Children.Add(inspectRoom);
        inspectionPanel.Children.Add(inspectionTools);
        inspectionPanel.Children.Add(inspectionOutput);
        sideTabs.Items.Add(new TabItem { Header = "Inspect any room", Content = inspectionPanel });
        Grid.SetRow(sideTabs, 1);
        Grid.SetColumn(sideTabs, 2);
        root.Children.Add(sideTabs);
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        Grid.SetRow(footer, 2);
        Grid.SetColumnSpan(footer, 3);
        var close = NewDialogButton("Close", 70);
        var refresh = NewDialogButton("Refresh room", 95);
        var centerCurrent = NewDialogButton("Select current", 95);
        var walkSelected = NewDialogButton("Walk to selected", 110);
        walkSelected.IsEnabled = false;
        var zoomOut = NewDialogButton("−", 34);
        var zoomReset = NewDialogButton("100%", 52);
        var zoomIn = NewDialogButton("+", 34);
        var fitMap = NewDialogButton("Fit map", 68);
        var mapSelector = new ComboBox
        {
            ItemsSource = session.ServerMaps, DisplayMemberPath = nameof(ClientMapCatalogEntry.DisplayName),
            Width = 155, Margin = new Thickness(0, 0, 8, 0), ToolTip = "View any decoded map without moving the character"
        };
        var topologyLegend = new TextBlock
        {
            Text = "Rooms: gold you · red active PvP · blue party · purple trusted · green players · cyan discovered  |  Links: gray normal · orange one-way · purple hidden · red blocked",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        var showHidden = new CheckBox
        {
            Content = "Show hidden objects",
            IsChecked = ViewModel.MapProfile.ShowHiddenObjects,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            ToolTip = "Map reference only. This does not reveal hidden objects to the character."
        };
        var fogOfWar = new CheckBox
        {
            Content = "Fog of war",
            IsChecked = ViewModel.MapProfile.FogOfWarEnabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            ToolTip = "On: discovered rooms only. Off: reveal decoded undiscovered topology without marking it discovered."
        };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(refresh, Dock.Right);
        footer.Children.Add(close);
        footer.Children.Add(refresh);
        footer.Children.Add(fitMap);
        footer.Children.Add(zoomIn);
        footer.Children.Add(zoomReset);
        footer.Children.Add(zoomOut);
        footer.Children.Add(centerCurrent);
        footer.Children.Add(walkSelected);
        footer.Children.Add(mapSelector);
        footer.Children.Add(fogOfWar);
        footer.Children.Add(showHidden);
        footer.Children.Add(topologyLegend);
        root.Children.Add(footer);
        dialog.Content = root;
        var selectedMapNodeKey = string.Empty;

        void ApplyMapZoom(double value)
        {
            mapZoom = Math.Clamp(value, .25, 3);
            ViewModel.MapProfile.Zoom = mapZoom;
            mapScale.ScaleX = mapZoom;
            mapScale.ScaleY = mapZoom;
            zoomReset.Content = $"{mapZoom * 100:0}%";
        }
        ApplyMapZoom(mapZoom);

        void Render()
        {
            var mappedPlayers = session.CurrentRoomTopologyNodes.Sum(node => node.Players.Length);
            var liveBosses = session.CurrentRoomTopologyNodes.Count(node => node.BossAlive);
            var roomFreshness = session.RoomUpdatedAt is not { } roomAt ? "loading live room" :
                DateTimeOffset.Now - roomAt > TimeSpan.FromSeconds(15) ? "live room data is stale" : "live now";
            header.Text = $"{DisplayOrUnknown(session.CurrentRoomName)}  [{DisplayOrUnknown(session.CurrentRoomKey)}]  ·  " +
                          $"{mappedPlayers:N0} other player(s) · {liveBosses:N0} boss(es) live · Exits: " +
                          (session.CurrentRoomExits.Count == 0 ? "none captured" : string.Join(", ", session.CurrentRoomExits)) + $" · {roomFreshness}";
            liveRoom.Text =
                $"{DisplayOrUnknown(session.CurrentRoomName)}\n" +
                $"Key: {DisplayOrUnknown(session.CurrentRoomKey)}\n\n" +
                (string.IsNullOrWhiteSpace(session.CurrentRoomDescription) ? "Description: not captured\n" : session.CurrentRoomDescription + "\n") +
                $"\nNPCs now: {(session.CurrentRoomNpcs.Count == 0 ? "none displayed" : string.Join(", ", session.CurrentRoomNpcs))}\n" +
                $"Players now: {(session.CurrentRoomPlayers.Count == 0 ? "none displayed" : string.Join(", ", session.CurrentRoomPlayers))}\n" +
                $"Items now: {(session.CurrentRoomItems.Count == 0 ? "none displayed" : string.Join(", ", session.CurrentRoomItems))}\n" +
                $"Currency now: {(string.IsNullOrWhiteSpace(session.CurrentRoomCurrency) ? "none displayed" : session.CurrentRoomCurrency)}\n" +
                $"Exits: {(session.CurrentRoomExits.Count == 0 ? "none" : string.Join(", ", session.CurrentRoomExits))}\n\n" +
                $"Room commands: {(session.CurrentRoomCommands.Count == 0 ? "none decoded" : string.Join(", ", session.CurrentRoomCommands))}\n\n" +
                $"Observed: {FormatTimestamp(session.RoomUpdatedAt)}";
            var pvp = session.PvpState;
            pvpStatus.Text = pvp is null
                ? "Awaiting private PvP state from the connected server."
                : $"{session.PvpRealmSummary}\n{session.PvpCombatSummary}\n" +
                  $"Room: {session.CurrentRoomName} [{session.CurrentRoomKey}]\n" +
                  $"HP {session.HealthCurrent}/{session.HealthMaximum} ({session.HealthPercent}%) · " +
                  $"Mana/Kai {session.ManaCurrent}/{session.ManaMaximum} ({session.ManaPercent}%)";
            var canInitiate = pvp is { WorldPvpEnabled: true, SafeRoom: false, Active: false } && pvpPlayers.SelectedItem is string;
            pvpAttack.IsEnabled = canInitiate;
            pvpFlee.IsEnabled = pvp?.Active == true;
            walkSelected.IsEnabled = selectedMapNodeKey.Length > 0 &&
                !selectedMapNodeKey.Equals(session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase) &&
                ViewModel.AutomationProfile.Capabilities.NavigationEnabled &&
                !ViewModel.AutomationProfile.Capabilities.EmergencyStop;
            RenderLiveMapCanvas(
                canvas, session, ViewModel.MapProfile, fogOfWar.IsChecked != true, selectedMapNodeKey,
                session.PartyMembers.Select(member => member.Name).ToHashSet(StringComparer.OrdinalIgnoreCase),
                ViewModel.CoordinationProfile.TrustedControllers.Where(controller => controller.Enabled)
                    .Select(controller => controller.PlayerName).ToHashSet(StringComparer.OrdinalIgnoreCase),
                node =>
                {
                    selectedMapNodeKey = node.RoomKey;
                    inspectionKey.Text = node.RoomKey;
                    sideTabs.SelectedIndex = 4;
                    _ = InspectRoomAsync(node.RoomKey);
                    Render();
                });
        }
        void RenderDiscoveredRooms()
        {
            var query = roomSearch.Text.Trim();
            var selected = rooms.SelectedItem as ClientRoomMetadata;
            filteredRooms.Clear();
            foreach (var room in ViewModel.MapProfile.Rooms
                         .Where(room => favoritesOnly.IsChecked != true || room.Favorite)
                         .Where(room => query.Length == 0 || new[] { room.RoomName, room.RoomKey, room.Notes, room.KnownNpcs, room.KnownMonsters, room.KnownShops }
                             .Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase)))
                         .OrderByDescending(room => room.RoomKey.Equals(session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase))
                         .ThenByDescending(room => room.Favorite)
                         .ThenBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase))
                filteredRooms.Add(room);
            if (selected is not null && filteredRooms.Contains(selected)) rooms.SelectedItem = selected;
        }
        void RenderInspection()
        {
            if (!string.IsNullOrWhiteSpace(session.RoomInspectionError))
            {
                inspectionOutput.Text = session.RoomInspectionError;
                return;
            }
            if (session.InspectedRoom is not { } room)
            {
                inspectionOutput.Text = "Enter any exact map:room key to inspect its current live server state without moving your character.";
                return;
            }
            inspectionOutput.Text =
                $"{room.Name} [{room.RoomKey}]\nCaptured: {room.CapturedAt.ToLocalTime():g}" +
                (DateTimeOffset.UtcNow - room.CapturedAt > TimeSpan.FromSeconds(15) ? " · STALE — refreshing" : " · LIVE") + $"\n\n{room.Description}\n\n" +
                $"Live NPCs: {(room.Npcs.Length == 0 ? "none" : string.Join(", ", room.Npcs.Select(npc => npc.Alive
                    ? $"{npc.Name} ALIVE HP {npc.CurrentHitPoints}/{npc.MaximumHitPoints} · {npc.Disposition} · level {npc.Level} · {npc.ExperienceReward:N0} XP"
                    : $"{npc.Name} DEAD, respawn {(npc.RespawnAtUtc is null ? "unknown" : FormatCountdown(npc.RespawnAtUtc.Value - DateTimeOffset.UtcNow))}")))}\n" +
                $"Live players: {(room.Players.Length == 0 ? "none" : string.Join(", ", room.Players))}\n" +
                $"Live items: {(room.Items.Length == 0 ? "none" : string.Join(", ", room.Items))}\n" +
                $"Currency: {(room.Currency.Length == 0 ? "none" : room.Currency)}\n" +
                $"Expected spawns: {(room.ExpectedSpawns.Length == 0 ? "none" : string.Join(", ", room.ExpectedSpawns.Select(spawn => $"{spawn.DisplayName} ({spawn.RespawnMinutes}m)")))}\n" +
                $"Exits: {(room.Exits.Length == 0 ? "none" : string.Join(", ", room.Exits.Select(exit => $"{exit.Direction} → {exit.RoomName} [{exit.RoomKey}]")))}\n" +
                $"Room commands: {(room.Commands.Length == 0 ? "none decoded" : string.Join(", ", room.Commands))}\n" +
                $"Shop: {(room.ImportedShopNumber is null ? "none" : room.ImportedShopNumber)} · Light: {room.EnvironmentLightLevel} · Room type: {room.EnvironmentRoomType}";
        }
        void RenderSelectedRoom()
        {
            if (rooms.SelectedItem is not ClientRoomMetadata room)
            {
                selectedRoomDetail.Text = "Select a discovered room to view its stored reference details.";
                return;
            }
            selectedRoomDetail.Text =
                $"{room.DisplayLabel}\nLayer: {room.Layer}\nLast visited: {FormatTimestamp(room.LastVisited)}\n" +
                $"NPCs: {DisplayOrUnknown(room.KnownNpcs)}\nMonsters: {DisplayOrUnknown(room.KnownMonsters)}\n" +
                $"Shops: {DisplayOrUnknown(room.KnownShops)}\nNotes: {DisplayOrUnknown(room.Notes)}";
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => { Render(); RenderInspection(); RenderDiscoveredRooms(); };
        var liveRefreshInProgress = false;
        int? viewedMapId = null;
        var liveRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        NotifyCollectionChangedEventHandler mapCatalogChanged = (_, _) =>
        {
            if (mapSelector.SelectedItem is not null || session.ServerMaps.Count == 0) return;
            var currentMap = int.TryParse(session.CurrentRoomKey.Split(':').FirstOrDefault(), out var parsedMap) ? parsedMap : session.ServerMaps[0].MapId;
            mapSelector.SelectedItem = session.ServerMaps.FirstOrDefault(map => map.MapId == currentMap) ?? session.ServerMaps[0];
        };
        session.ServerMaps.CollectionChanged += mapCatalogChanged;
        canvas.SizeChanged += (_, _) => Render();
        dialog.Closed += async (_, _) =>
        {
            timer.Stop();
            liveRefresh.Stop();
            session.ServerMaps.CollectionChanged -= mapCatalogChanged;
            await ViewModel.SaveMapProfileAsync();
        };
        async Task RefreshRoomDataAsync()
        {
            var currentMap = int.TryParse(session.CurrentRoomKey.Split(':').FirstOrDefault(), out var parsedMap) ? parsedMap : 0;
            var command = viewedMapId is { } selectedMap && selectedMap != currentMap
                ? $"roomdata map {selectedMap}"
                : showHidden.IsChecked == true ? "roomdata hidden" : "roomdata";
            await ViewModel.SendClientProtocolCommandAsync(session, command);
        }
        async Task InspectRoomAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            await ViewModel.SendClientProtocolCommandAsync(session, $"roomdata inspect {key.Trim()}" + (showHidden.IsChecked == true ? " hidden" : string.Empty));
            RenderInspection();
        }
        liveRefresh.Tick += async (_, _) =>
        {
            if (liveRefreshInProgress || session.ConnectionState != ClientConnectionState.Connected) return;
            liveRefreshInProgress = true;
            try
            {
                await RefreshRoomDataAsync();
                await ViewModel.SendClientProtocolCommandAsync(session, "clientdata pvp");
                var inspectedKey = inspectionKey.Text.Trim();
                if (inspectedKey.Length > 0)
                    await ViewModel.SendClientProtocolCommandAsync(session, $"roomdata inspect {inspectedKey}" + (showHidden.IsChecked == true ? " hidden" : string.Empty));
                Render();
                RenderInspection();
            }
            finally { liveRefreshInProgress = false; }
        };
        refresh.Click += async (_, _) => await RefreshRoomDataAsync();
        pvpRefresh.Click += async (_, _) =>
        {
            await ViewModel.SendClientProtocolCommandAsync(session, "clientdata pvp");
            await RefreshRoomDataAsync();
            Render();
        };
        pvpPlayers.SelectionChanged += (_, _) => Render();
        pvpDefendParty.Checked += async (_, _) =>
        {
            ViewModel.AutomationOptions.PvP.DefendPartyMembers = true;
            ViewModel.AutomationOptions.PvP.Enabled = true;
            ViewModel.AutomationProfile.Capabilities.PvpAutomationEnabled = true;
            await ViewModel.SaveAutomationOptionsAsync();
        };
        pvpDefendParty.Unchecked += async (_, _) =>
        {
            ViewModel.AutomationOptions.PvP.DefendPartyMembers = false;
            await ViewModel.SaveAutomationOptionsAsync();
        };
        pvpAttack.Click += async (_, _) =>
        {
            if (pvpPlayers.SelectedItem is not string target || string.IsNullOrWhiteSpace(target)) return;
            await ViewModel.SendTerminalCommandAsync(session, $"attack {target}");
            await ViewModel.SendClientProtocolCommandAsync(session, "clientdata pvp");
            Render();
        };
        pvpFlee.Click += async (_, _) =>
        {
            await ViewModel.SendTerminalCommandAsync(session, "flee");
            await ViewModel.SendClientProtocolCommandAsync(session, "clientdata pvp");
            Render();
        };
        showHidden.Checked += async (_, _) => { ViewModel.MapProfile.ShowHiddenObjects = true; await RefreshRoomDataAsync(); };
        showHidden.Unchecked += async (_, _) => { ViewModel.MapProfile.ShowHiddenObjects = false; await RefreshRoomDataAsync(); };
        fogOfWar.Checked += (_, _) => { ViewModel.MapProfile.FogOfWarEnabled = true; Render(); };
        fogOfWar.Unchecked += (_, _) => { ViewModel.MapProfile.FogOfWarEnabled = false; Render(); };
        mapSelector.SelectionChanged += async (_, _) =>
        {
            if (mapSelector.SelectedItem is not ClientMapCatalogEntry selected) return;
            viewedMapId = selected.MapId;
            await RefreshRoomDataAsync();
            Render();
        };
        zoomOut.Click += (_, _) => ApplyMapZoom(mapZoom - .1);
        zoomIn.Click += (_, _) => ApplyMapZoom(mapZoom + .1);
        zoomReset.Click += (_, _) => ApplyMapZoom(1);
        fitMap.Click += (_, _) =>
        {
            var availableWidth = Math.Max(1, mapScroll.ViewportWidth - 16);
            var availableHeight = Math.Max(1, mapScroll.ViewportHeight - 16);
            ApplyMapZoom(Math.Min(availableWidth / Math.Max(1, canvas.ActualWidth), availableHeight / Math.Max(1, canvas.ActualHeight)));
            mapScroll.ScrollToHorizontalOffset(0);
            mapScroll.ScrollToVerticalOffset(0);
        };
        mapScroll.PreviewMouseWheel += (_, args) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            ApplyMapZoom(mapZoom + (args.Delta > 0 ? .1 : -.1));
            args.Handled = true;
        };
        Point? panStart = null;
        double panHorizontal = 0;
        double panVertical = 0;
        mapScroll.PreviewMouseDown += (_, args) =>
        {
            if (args.ChangedButton != MouseButton.Middle) return;
            panStart = args.GetPosition(mapScroll);
            panHorizontal = mapScroll.HorizontalOffset;
            panVertical = mapScroll.VerticalOffset;
            mapScroll.CaptureMouse();
            args.Handled = true;
        };
        mapScroll.PreviewMouseMove += (_, args) =>
        {
            if (panStart is not { } start || args.MiddleButton != MouseButtonState.Pressed) return;
            var current = args.GetPosition(mapScroll);
            mapScroll.ScrollToHorizontalOffset(panHorizontal - (current.X - start.X));
            mapScroll.ScrollToVerticalOffset(panVertical - (current.Y - start.Y));
            args.Handled = true;
        };
        mapScroll.PreviewMouseUp += (_, args) =>
        {
            if (args.ChangedButton != MouseButton.Middle) return;
            panStart = null;
            mapScroll.ReleaseMouseCapture();
            args.Handled = true;
        };
        inspectRoom.Click += async (_, _) =>
        {
            var key = inspectionKey.Text.Trim();
            if (key.Length == 0) return;
            selectedMapNodeKey = key;
            await InspectRoomAsync(key);
            Render();
        };
        inspectionKey.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            inspectRoom.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            args.Handled = true;
        };
        roomSearch.TextChanged += (_, _) => { RenderDiscoveredRooms(); RenderSelectedRoom(); };
        favoritesOnly.Checked += (_, _) => { RenderDiscoveredRooms(); RenderSelectedRoom(); };
        favoritesOnly.Unchecked += (_, _) => { RenderDiscoveredRooms(); RenderSelectedRoom(); };
        rooms.SelectionChanged += (_, _) => RenderSelectedRoom();
        centerCurrent.Click += (_, _) =>
        {
            var currentMap = int.TryParse(session.CurrentRoomKey.Split(':').FirstOrDefault(), out var parsedMap) ? parsedMap : 0;
            viewedMapId = currentMap;
            mapSelector.SelectedItem = session.ServerMaps.FirstOrDefault(map => map.MapId == currentMap);
            roomSearch.Text = string.Empty;
            favoritesOnly.IsChecked = false;
            RenderDiscoveredRooms();
            rooms.SelectedItem = filteredRooms.FirstOrDefault(room => room.RoomKey.Equals(session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase));
            if (rooms.SelectedItem is not null) rooms.ScrollIntoView(rooms.SelectedItem);
            sideTabs.SelectedIndex = 0;
            Render();
        };
        walkSelected.Click += async (_, _) =>
        {
            if (selectedMapNodeKey.Length == 0) return;
            await ViewModel.StartRouteToRoomAsync(session, selectedMapNodeKey);
            Render();
        };
        close.Click += (_, _) => dialog.Close();
        Render();
        RenderDiscoveredRooms();
        RenderSelectedRoom();
        RenderInspection();
        timer.Start();
        liveRefresh.Start();
        dialog.Show();
        _ = RefreshRoomDataAsync();
        _ = ViewModel.SendClientProtocolCommandAsync(session, "clientdata pvp");
    }

    private void WhoIsHomeClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"Who Is Home? - {session.WorldName}", Owner = this,
            Width = 980, Height = 590, MinWidth = 700, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var search = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(search, Dock.Top);
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        DockPanel.SetDock(footer, Dock.Bottom);
        var refresh = NewDialogButton("Refresh from server", 125);
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Right); DockPanel.SetDock(refresh, Dock.Right);
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        footer.Children.Add(close); footer.Children.Add(refresh); footer.Children.Add(status);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Boss", Binding = new Binding("Name"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "State", Binding = new Binding("State"), Width = new DataGridLength(70) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Time until spawn", Binding = new Binding("Countdown"), Width = new DataGridLength(125) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Current room", Binding = new Binding("CurrentRoom"), Width = new DataGridLength(1.6, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Home room", Binding = new Binding("HomeRoom"), Width = new DataGridLength(1.6, DataGridLengthUnitType.Star) });

        void Render()
        {
            var now = DateTimeOffset.UtcNow;
            var query = search.Text.Trim();
            var rows = session.BossHomes
                .Where(boss => query.Length == 0 || boss.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               boss.CurrentRoom.Contains(query, StringComparison.OrdinalIgnoreCase) || boss.HomeRoom.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(boss => new
                {
                    boss.Name, boss.State, boss.CurrentRoom, boss.HomeRoom,
                    Countdown = boss.State.Equals("ALIVE", StringComparison.OrdinalIgnoreCase) ? "Alive" :
                        boss.RespawnAtUtc is not { } deadline ? "Unknown" : FormatCountdown(deadline - now)
                }).ToArray();
            grid.ItemsSource = rows;
            status.Text = session.BossHomes.Count == 0 ? "No server snapshot received." : $"{rows.Length:N0} of {session.BossHomes.Count:N0} tracked bosses";
        }

        refresh.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "whohome");
        close.Click += (_, _) => dialog.Close();
        search.TextChanged += (_, _) => Render();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Render();
        dialog.Closed += (_, _) => timer.Stop();
        root.Children.Add(search); root.Children.Add(footer); root.Children.Add(grid);
        dialog.Content = root;
        Render(); timer.Start(); dialog.Show();
        _ = ViewModel.SendClientProtocolCommandAsync(session, "whohome");
    }

    private static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return "Ready now";
        return remaining.TotalDays >= 1 ? $"{(int)remaining.TotalDays}d {remaining.Hours}h {remaining.Minutes}m" :
            remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m {remaining.Seconds}s" :
            $"{remaining.Minutes}m {remaining.Seconds}s";
    }

    private static void RenderLiveMapCanvas(
        Canvas canvas,
        ClientSessionConnection session,
        ClientMapProfile profile,
        bool showUndiscovered,
        string selectedRoomKey,
        IReadOnlySet<string> partyMembers,
        IReadOnlySet<string> trustedPlayers,
        Action<ClientRoomTopologyNode>? selectRoom)
    {
        canvas.Children.Clear();
        var width = Math.Max(240, canvas.ActualWidth);
        var height = Math.Max(220, canvas.ActualHeight);
        var center = new Point(width / 2, height / 2);
        if (session.RouteMapNodes.Count > 1)
        {
            var minX = session.RouteMapNodes.Min(node => node.X);
            var maxX = session.RouteMapNodes.Max(node => node.X);
            var minY = session.RouteMapNodes.Min(node => node.Y);
            var maxY = session.RouteMapNodes.Max(node => node.Y);
            double X(double value) => 35 + (value - minX) / Math.Max(1, maxX - minX) * (width - 70);
            double Y(double value) => 35 + (value - minY) / Math.Max(1, maxY - minY) * (height - 70);
            foreach (var segment in session.RouteMapSegments)
                AddGraphLine(canvas, X(segment.X1), Y(segment.Y1), X(segment.X2), Y(segment.Y2), Brushes.SlateGray);
            foreach (var node in session.RouteMapNodes)
                AddMapRoom(canvas, new Point(X(node.X), Y(node.Y)), node.ActualRoomName.Length > 0 ? node.ActualRoomName : node.ExpectedRoomKey, node.State == RoutePlaybackNodeState.Active ? Brushes.Gold : Brushes.LightSteelBlue);
            return;
        }

        if (session.CurrentRoomTopologyNodes.Count > 1)
        {
            var discovered = profile.Rooms.Select(room => room.RoomKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            discovered.Add(session.CurrentRoomKey);
            var nodes = session.CurrentRoomTopologyNodes
                .Where(node => showUndiscovered || discovered.Contains(node.RoomKey))
                .ToDictionary(node => node.RoomKey, StringComparer.OrdinalIgnoreCase);
            var links = session.CurrentRoomTopologyLinks
                .Where(link => nodes.ContainsKey(link.SourceRoomKey) && nodes.ContainsKey(link.TargetRoomKey)).ToArray();
            var rootNode = nodes.Values.OrderBy(node => node.Depth).FirstOrDefault();
            if (rootNode is null) return;
            var positions = new Dictionary<string, Vector>(StringComparer.OrdinalIgnoreCase) { [rootNode.RoomKey] = new Vector(0, 0) };
            for (var pass = 0; pass < 3; pass++)
                foreach (var link in links)
                {
                    if (!positions.TryGetValue(link.SourceRoomKey, out var source) || positions.ContainsKey(link.TargetRoomKey)) continue;
                    positions[link.TargetRoomKey] = source + ExitOffset(link.Direction, 1);
                }
            var positioned = nodes.Values.Where(node => positions.ContainsKey(node.RoomKey)).ToArray();
            if (positioned.Length > 0)
            {
                var minX = positioned.Min(node => positions[node.RoomKey].X);
                var maxX = positioned.Max(node => positions[node.RoomKey].X);
                var minY = positioned.Min(node => positions[node.RoomKey].Y);
                var maxY = positioned.Max(node => positions[node.RoomKey].Y);
                double X(double value) => 45 + (value - minX) / Math.Max(1, maxX - minX) * (width - 90);
                double Y(double value) => 45 + (value - minY) / Math.Max(1, maxY - minY) * (height - 90);
                foreach (var link in links.Where(link => positions.ContainsKey(link.SourceRoomKey) && positions.ContainsKey(link.TargetRoomKey)))
                {
                    var source = positions[link.SourceRoomKey];
                    var target = positions[link.TargetRoomKey];
                    var brush = link.Blocked ? Brushes.Firebrick : link.Hidden ? Brushes.DarkViolet : link.OneWay ? Brushes.DarkOrange : Brushes.SlateGray;
                    AddGraphLine(canvas, X(source.X), Y(source.Y), X(target.X), Y(target.Y), brush);
                }
                foreach (var node in positioned)
                {
                    var point = positions[node.RoomKey];
                    var hasActiveOpponent = session.PvpState is { Active: true } pvp &&
                        node.RoomKey.Equals(pvp.RoomId, StringComparison.OrdinalIgnoreCase) &&
                        node.Players.Any(player => player.Equals(pvp.OpponentName, StringComparison.OrdinalIgnoreCase));
                    var hasParty = node.Players.Any(partyMembers.Contains);
                    var hasTrusted = node.Players.Any(trustedPlayers.Contains);
                    var fill = hasActiveOpponent ? Brushes.OrangeRed : node.Current ? Brushes.Gold :
                        hasParty ? Brushes.DodgerBlue : hasTrusted ? Brushes.MediumPurple :
                        node.Players.Length > 0 ? Brushes.LimeGreen : discovered.Contains(node.RoomKey) ? Brushes.DeepSkyBlue : Brushes.SlateGray;
                    AddTopologyMapRoom(canvas, new Point(X(point.X), Y(point.Y)), node,
                        fill,
                        node.RoomKey.Equals(selectedRoomKey, StringComparison.OrdinalIgnoreCase), hasActiveOpponent, hasParty, hasTrusted, selectRoom);
                }
                return;
            }
        }

        if (session.CurrentRoomLinks.Count > 0)
        {
            foreach (var link in session.CurrentRoomLinks)
            {
                var offset = ExitOffset(link.Direction, 115);
                var target = new Point(center.X + offset.X, center.Y + offset.Y);
                AddGraphLine(canvas, center.X, center.Y, target.X, target.Y, Brushes.SlateGray);
                AddMapRoom(canvas, target, $"{link.RoomName} [{link.RoomKey}]", Brushes.LightSteelBlue);
            }
        }
        else foreach (var exit in session.CurrentRoomExits)
        {
            var offset = ExitOffset(exit, 115);
            var target = new Point(center.X + offset.X, center.Y + offset.Y);
            AddGraphLine(canvas, center.X, center.Y, target.X, target.Y, Brushes.SlateGray);
            AddMapRoom(canvas, target, exit.ToUpperInvariant(), Brushes.LightSteelBlue);
        }
        AddMapRoom(canvas, center, DisplayOrUnknown(session.CurrentRoomName), Brushes.Gold);
    }

    private static Vector ExitOffset(string exit, double distance) => exit.Trim().ToUpperInvariant() switch
    {
        "N" or "NORTH" => new Vector(0, -distance), "NE" or "NORTHEAST" => new Vector(distance, -distance),
        "E" or "EAST" => new Vector(distance, 0), "SE" or "SOUTHEAST" => new Vector(distance, distance),
        "S" or "SOUTH" => new Vector(0, distance), "SW" or "SOUTHWEST" => new Vector(-distance, distance),
        "W" or "WEST" => new Vector(-distance, 0), "NW" or "NORTHWEST" => new Vector(-distance, -distance),
        "U" or "UP" => new Vector(distance * .65, -distance * .65), "D" or "DOWN" => new Vector(-distance * .65, distance * .65),
        _ => new Vector(0, 0)
    };

    private static void AddMapRoom(Canvas canvas, Point point, string labelText, Brush fill)
    {
        var isCurrent = ReferenceEquals(fill, Brushes.Gold);
        var marker = new System.Windows.Shapes.Rectangle
        {
            Width = isCurrent ? 22 : 16, Height = isCurrent ? 22 : 16,
            RadiusX = 2, RadiusY = 2,
            Fill = isCurrent ? new SolidColorBrush(Color.FromRgb(250, 204, 21)) : new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Stroke = isCurrent ? Brushes.White : fill,
            StrokeThickness = isCurrent ? 2 : 1.5,
            ToolTip = labelText
        };
        Canvas.SetLeft(marker, point.X - marker.Width / 2);
        Canvas.SetTop(marker, point.Y - marker.Height / 2);
        canvas.Children.Add(marker);
        var firstLine = labelText.Split('\n', 2)[0];
        var keyStart = firstLine.LastIndexOf('[');
        var keyEnd = firstLine.LastIndexOf(']');
        var compactKey = keyStart >= 0 && keyEnd > keyStart ? firstLine[(keyStart + 1)..keyEnd] : string.Empty;
        var label = new TextBlock
        {
            Text = isCurrent ? "YOU" : compactKey,
            Foreground = isCurrent ? Brushes.Gold : Brushes.LightSteelBlue,
            FontSize = isCurrent ? 9 : 7, FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
            MaxWidth = isCurrent ? 55 : 42, TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = labelText
        };
        Canvas.SetLeft(label, point.X + (isCurrent ? 14 : 10));
        Canvas.SetTop(label, point.Y - 7);
        canvas.Children.Add(label);
    }

    private static void AddTopologyMapRoom(
        Canvas canvas,
        Point point,
        ClientRoomTopologyNode node,
        Brush fill,
        bool selected,
        bool hasActiveOpponent,
        bool hasParty,
        bool hasTrusted,
        Action<ClientRoomTopologyNode>? selectRoom)
    {
        var facts = new List<string> { $"{node.RoomName} [{node.RoomKey}]" };
        if (node.Current) facts.Add("YOU ARE HERE");
        if (hasActiveOpponent) facts.Add("ACTIVE PVP OPPONENT HERE");
        if (hasParty) facts.Add("PARTY MEMBER HERE");
        if (hasTrusted) facts.Add("TRUSTED PLAYER HERE");
        if (node.Players.Length > 0) facts.Add($"PLAYERS HERE NOW: {string.Join(", ", node.Players)}");
        if (node.BossAlive) facts.Add($"BOSS HERE NOW: {string.Join(", ", node.BossNames)}");
        else if (node.IsBossLair) facts.Add($"BOSS LAIR: {(node.BossNames.Length == 0 ? "decoded lair" : string.Join(", ", node.BossNames))}");
        if (node.LiveNpcs.Length > 0) facts.Add($"LIVE NPCS: {string.Join(", ", node.LiveNpcs)}");
        if (node.ExpectedMonsters.Length > 0) facts.Add($"SPAWNS HERE: {string.Join(", ", node.ExpectedMonsters)}");
        if (node.HasShop) facts.Add("SHOP");
        if (node.HasRoomCommand) facts.Add("ROOM COMMAND");
        facts.Add("Reference only; select Inspect any room for full live details.");
        AddMapRoom(canvas, point, string.Join(Environment.NewLine, facts), fill);
        if (selected)
        {
            var selection = new System.Windows.Shapes.Rectangle
            {
                Width = 28, Height = 28, RadiusX = 4, RadiusY = 4,
                Fill = Brushes.Transparent, Stroke = Brushes.White, StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(selection, point.X - 14);
            Canvas.SetTop(selection, point.Y - 14);
            canvas.Children.Add(selection);
        }
        var hitTarget = new System.Windows.Shapes.Rectangle
        {
            Width = 28, Height = 28, Fill = Brushes.Transparent,
            Cursor = Cursors.Hand, ToolTip = string.Join(Environment.NewLine, facts)
        };
        hitTarget.MouseLeftButtonUp += (_, args) => { selectRoom?.Invoke(node); args.Handled = true; };
        Canvas.SetLeft(hitTarget, point.X - 14);
        Canvas.SetTop(hitTarget, point.Y - 14);
        canvas.Children.Add(hitTarget);

        var badges = new List<Brush>();
        if (node.HasShop) badges.Add(Brushes.Cyan);
        if (node.BossAlive) badges.Add(Brushes.HotPink);
        else if (node.IsBossLair) badges.Add(Brushes.MediumPurple);
        if (node.HasRoomCommand) badges.Add(Brushes.Lime);
        if (node.LiveNpcs.Length > 0) badges.Add(Brushes.OrangeRed);
        for (var index = 0; index < badges.Count; index++)
        {
            var badge = new System.Windows.Shapes.Rectangle
            {
                Width = 5, Height = 5, Fill = badges[index], Stroke = Brushes.Black,
                StrokeThickness = .5, ToolTip = facts[0], IsHitTestVisible = false
            };
            Canvas.SetLeft(badge, point.X - 8 + index * 4);
            Canvas.SetTop(badge, point.Y + 7);
            canvas.Children.Add(badge);
        }
    }

    private static string FormatLibrariumRecord(string rawJson, out ClientLibrariumLink[] references)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var output = new StringBuilder();
            AppendLibrariumValue(document.RootElement, output, found, 0, string.Empty);
            references = found.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Select(value => new ClientLibrariumLink(value, FriendlyLibrariumReference(value))).ToArray();
            return output.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            references = [];
            return rawJson;
        }
    }

    private static readonly HashSet<string> HiddenLibrariumFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "canonical_id", "schema_version", "sha256", "compile_hash", "source_file", "source_files",
        "source_path", "provenance", "evidence", "evidence_classification", "runtime_disposition", "validation",
        "native_sha256", "native_symbol", "native_offset", "record_index"
    };

    private static void AppendLibrariumValue(JsonElement value, StringBuilder output, ISet<string> references, int depth, string name)
    {
        if (HiddenLibrariumFields.Contains(name) || name.EndsWith("_sha256", StringComparison.OrdinalIgnoreCase)) return;
        var indent = new string(' ', depth * 2);
        var label = string.IsNullOrWhiteSpace(name) ? string.Empty : name == "•" ? "• " : FriendlyLibrariumLabel(name) + ": ";
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (label.Length > 0) output.AppendLine(indent + label.TrimEnd());
                foreach (var property in value.EnumerateObject())
                    AppendLibrariumValue(property.Value, output, references, depth + (label.Length > 0 ? 1 : 0), property.Name);
                break;
            case JsonValueKind.Array:
                if (label.Length > 0) output.AppendLine(indent + label.TrimEnd());
                foreach (var item in value.EnumerateArray())
                    AppendLibrariumValue(item, output, references, depth + 1, "•");
                break;
            case JsonValueKind.String:
                var text = StripLibrariumFormatting(value.GetString() ?? string.Empty);
                output.AppendLine(indent + label + text);
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z][A-Za-z0-9_-]*\.\d+$")) references.Add(text);
                break;
            case JsonValueKind.Null:
                break;
            case JsonValueKind.True:
                output.AppendLine(indent + label + "Yes");
                break;
            case JsonValueKind.False:
                output.AppendLine(indent + label + "No");
                break;
            default:
                output.AppendLine(indent + label + value);
                break;
        }
    }

    private static string FriendlyLibrariumLabel(string value)
    {
        var known = value.ToLowerInvariant() switch
        {
            "name" or "title" => "Name", "description" or "desc" => "Description",
            "map_id" or "map" => "Map", "room_number" => "Room", "level" => "Level",
            "experience" or "experience_reward" => "Experience reward", "gold" or "gold_reward" => "Gold reward",
            "minimum_level" or "min_level" => "Minimum level", "maximum_level" or "max_level" => "Maximum level",
            "mana_cost" => "Mana cost", "energy_cost" or "kai_cost" => "Energy/Kai cost",
            "hit_points" or "maximum_hit_points" => "Hit points", "armor_class" => "Armor",
            "damage_minimum" or "minimum_damage" => "Minimum damage", "damage_maximum" or "maximum_damage" => "Maximum damage",
            "respawn_minutes" or "regen_time" => "Time until return", "requirements" => "Requirements",
            "rewards" => "Rewards", "drops" or "items" => "Items", "exits" => "Exits",
            "abilities" => "Abilities", "spells" => "Spells", "classes" => "Classes", "races" => "Races",
            "room_commands" or "commands" => "Things you can do here", "npcs" or "monsters" => "Creatures",
            _ => LibrariumFriendlyNames.Family(value)
        };
        return known;
    }

    private static string FriendlyLibrariumReference(string reference)
    {
        var separator = reference.IndexOf('.');
        if (separator < 1) return "Related information";
        var family = LibrariumFriendlyNames.Family(reference[..separator]);
        var number = reference[(separator + 1)..];
        return int.TryParse(number, out _) ? $"Related {family.ToLowerInvariant()} {number}" : $"Related {family.ToLowerInvariant()} information";
    }

    private static string StripLibrariumFormatting(string value)
    {
        var withoutAnsi = System.Text.RegularExpressions.Regex.Replace(value, "\\u001B\\[[0-?]*[ -/]*[@-~]", string.Empty);
        return withoutAnsi.Replace("_", " ").Trim();
    }

    private void QuickToolsClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session || ViewModel is null) return;
        var dialog = new Window
        {
            Title = $"Reforged Quick Tools — {session.CharacterName}", Owner = this,
            Width = 500, Height = 330, MinWidth = 420, MinHeight = 280,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(14) };
        var heading = new TextBlock
        {
            Text = "Manual read-only game queries", FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 5)
        };
        DockPanel.SetDock(heading, Dock.Top);
        var explanation = new TextBlock
        {
            Text = "Each button sends exactly one visible command through this terminal session. Nothing repeats automatically.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(explanation, Dock.Top);
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = true };
        DockPanel.SetDock(footer, Dock.Bottom);
        var status = new TextBlock { Text = "Ready.", VerticalAlignment = VerticalAlignment.Center };
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(close);
        footer.Children.Add(status);
        var tools = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Rows = 4 };

        void AddTool(string label, string command)
        {
            var button = new Button
            {
                Content = label, Height = 38, Margin = new Thickness(4),
                Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush
            };
            button.Click += async (_, _) =>
            {
                try
                {
                    button.IsEnabled = false;
                    await ViewModel.SendTerminalCommandAsync(session, command);
                    status.Text = $"Sent {command.ToUpperInvariant()} at {DateTime.Now:HH:mm:ss}.";
                }
                catch (Exception exception)
                {
                    status.Text = exception.Message;
                }
                finally { button.IsEnabled = true; }
            };
            tools.Children.Add(button);
        }

        AddTool("Look at room", "look");
        AddTool("Character stats", "stats");
        AddTool("Inventory", "inventory");
        AddTool("Equipment", "equipment");
        AddTool("Learned spells", "spells");
        AddTool("Party status", "party");
        AddTool("Online players", "who");
        AddTool("Experience", "experience");
        close.Click += (_, _) => dialog.Close();
        root.Children.Add(heading);
        root.Children.Add(explanation);
        root.Children.Add(footer);
        root.Children.Add(tools);
        dialog.Content = root;
        dialog.Show();
    }

    private void GameDataClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var dialog = new Window
        {
            Title = $"Reforged Game Data — {ViewModel.Categories.Sum(category => category.Records.Count):N0} local records", Owner = this,
            Width = 940, Height = 610, MinWidth = 680, MinHeight = 430,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var filters = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        filters.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumnSpan(filters, 3);
        var category = new ComboBox { ItemsSource = ViewModel.Categories, DisplayMemberPath = nameof(ClientDataCategory.Name) };
        var search = new TextBox { ToolTip = "Search exact generated IDs, names, summaries, and details." };
        Grid.SetColumn(search, 2);
        filters.Children.Add(category);
        filters.Children.Add(search);
        root.Children.Add(filters);
        var records = new ListBox
        {
            Background = Brushes.White, Foreground = Brushes.Black,
            DisplayMemberPath = nameof(ClientDataRecord.EffectiveName)
        };
        Grid.SetRow(records, 1);
        root.Children.Add(records);
        var details = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(9)
        };
        Grid.SetRow(details, 1);
        Grid.SetColumn(details, 2);
        root.Children.Add(details);
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        Grid.SetRow(footer, 2);
        Grid.SetColumnSpan(footer, 3);
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(close);
        footer.Children.Add(status);
        root.Children.Add(footer);

        void Populate()
        {
            var selectedCategory = category.SelectedItem as ClientDataCategory;
            var query = search.Text.Trim();
            var matches = (selectedCategory?.Records ?? [])
                .Where(record => query.Length == 0 ||
                    record.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    record.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    record.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    record.HostDetails.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(5000)
                .ToArray();
            records.ItemsSource = matches;
            records.SelectedIndex = matches.Length > 0 ? 0 : -1;
            status.Text = selectedCategory is null ? "Select a generated data family." : $"Showing {matches.Length:N0} of {selectedCategory.Records.Count:N0} {selectedCategory.Name} records.";
        }
        records.SelectionChanged += (_, _) =>
        {
            if (records.SelectedItem is not ClientDataRecord record) { details.Text = string.Empty; return; }
            details.Text = $"{record.EffectiveName}\n\nID: {record.Id}\nFamily: {record.Category}\n{record.Summary}\n\n{record.HostDetails}";
        };
        category.SelectionChanged += (_, _) => Populate();
        search.TextChanged += (_, _) => Populate();
        close.Click += (_, _) => dialog.Close();
        category.SelectedItem = ViewModel.Categories.FirstOrDefault();
        dialog.Content = root;
        dialog.Show();
    }

    private void LibrariumReferenceClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"Librarium Reference - {session.DisplayName}", Owner = this,
            Width = 1040, Height = 650, MinWidth = 720, MinHeight = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        var controls = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        controls.ColumnDefinitions.Add(new ColumnDefinition());
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        Grid.SetColumnSpan(controls, 3);
        var family = new ComboBox { DisplayMemberPath = nameof(ClientServerCatalogFamily.DisplayName) };
        var search = new TextBox { ToolTip = "Search names and descriptions." };
        Grid.SetColumn(search, 2);
        var searchButton = NewDialogButton("Search", 68); Grid.SetColumn(searchButton, 3);
        controls.Children.Add(family); controls.Children.Add(search); controls.Children.Add(searchButton); root.Children.Add(controls);
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        Grid.SetRow(footer, 2); Grid.SetColumnSpan(footer, 3);
        var previous = NewDialogButton("Previous", 76);
        var next = NewDialogButton("Next", 64);
        var back = NewDialogButton("Back", 58); back.IsEnabled = false;
        var refresh = NewDialogButton("Refresh catalog", 110);
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Right); DockPanel.SetDock(next, Dock.Right); DockPanel.SetDock(previous, Dock.Right); DockPanel.SetDock(back, Dock.Right); DockPanel.SetDock(refresh, Dock.Right);
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        footer.Children.Add(close); footer.Children.Add(next); footer.Children.Add(previous); footer.Children.Add(back); footer.Children.Add(refresh); footer.Children.Add(status);
        var records = new ListBox { Background = Brushes.White, Foreground = Brushes.Black, DisplayMemberPath = nameof(ClientLibrariumRecord.Name) };
        Grid.SetRow(records, 1); root.Children.Add(records);
        var details = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.White,
            Foreground = Brushes.Black, FontFamily = new FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(8)
        };
        var referenceList = new ListBox
        {
            Height = 105, Background = Brushes.White, Foreground = Brushes.Black,
            DisplayMemberPath = nameof(ClientLibrariumLink.DisplayName),
            ToolTip = "Double-click to open related information."
        };
        var detailPanel = new DockPanel();
        DockPanel.SetDock(referenceList, Dock.Bottom);
        detailPanel.Children.Add(referenceList);
        detailPanel.Children.Add(details);
        Grid.SetRow(detailPanel, 1); Grid.SetColumn(detailPanel, 2); root.Children.Add(detailPanel); root.Children.Add(footer);
        var requestedOffset = 0;
        string requestedFamily = string.Empty, requestedQuery = string.Empty;
        var navigationHistory = new Stack<(string Family, string Query, int Offset)>();

        void Render()
        {
            var selectedName = (family.SelectedItem as ClientServerCatalogFamily)?.Name;
            family.ItemsSource = session.ServerCatalogFamilies.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            if (selectedName is not null) family.SelectedItem = session.ServerCatalogFamilies.FirstOrDefault(item => item.Name == selectedName);
            if (session.ConnectionState != ClientConnectionState.Connected)
            {
                status.Text = "Disconnected — reconnect to request current server information.";
                records.IsEnabled = false;
                searchButton.IsEnabled = false;
                return;
            }
            records.IsEnabled = true;
            searchButton.IsEnabled = true;
            if (session.ServerCatalogUpdatedAt is null)
            {
                status.Text = "Waiting for the connected server catalog.";
                return;
            }
            if (family.SelectedItem is null) family.SelectedItem = session.ServerCatalogFamilies.FirstOrDefault();
            if (session.LibrariumPage is { } page && page.Family.Equals(requestedFamily, StringComparison.OrdinalIgnoreCase) &&
                page.Query.Equals(requestedQuery, StringComparison.Ordinal) && page.Offset == requestedOffset)
            {
                records.ItemsSource = page.Records;
                previous.IsEnabled = page.Offset > 0;
                next.IsEnabled = page.Offset + page.Records.Count < page.TotalCount;
                back.IsEnabled = navigationHistory.Count > 0;
                var freshness = DateTimeOffset.Now - page.CapturedAt > TimeSpan.FromMinutes(2) ? " · stale; search again to refresh" : " · current server page";
                status.Text = $"{session.ServerCatalogWorld} · showing {page.Offset + 1:N0}-{page.Offset + page.Records.Count:N0} of {page.TotalCount:N0} server records{freshness}";
            }
            else status.Text = $"{session.ServerCatalogWorld} · select Search to request a server page";
        }

        async Task RequestPage(int offset)
        {
            if (family.SelectedItem is not ClientServerCatalogFamily selected) return;
            requestedFamily = selected.Name;
            requestedQuery = search.Text.Trim();
            if (requestedQuery.Length > 80) requestedQuery = requestedQuery[..80];
            requestedOffset = Math.Max(0, offset);
            await ViewModel.SendClientProtocolCommandAsync(session, $"librarium page {requestedFamily} {requestedOffset}" + (requestedQuery.Length > 0 ? $" {requestedQuery}" : string.Empty));
        }

        refresh.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "librarium");
        searchButton.Click += async (_, _) => await RequestPage(0);
        search.KeyDown += async (_, args) => { if (args.Key == Key.Enter) { await RequestPage(0); args.Handled = true; } };
        previous.Click += async (_, _) => await RequestPage(Math.Max(0, requestedOffset - (session.LibrariumPage?.PageSize ?? 25)));
        next.Click += async (_, _) => await RequestPage(requestedOffset + (session.LibrariumPage?.PageSize ?? 25));
        records.SelectionChanged += (_, _) =>
        {
            if (records.SelectedItem is not ClientLibrariumRecord record)
            {
                details.Text = string.Empty;
                referenceList.ItemsSource = null;
                return;
            }
            var formatted = FormatLibrariumRecord(record.Summary, out var references);
            details.Text = $"{record.Name}\n{LibrariumFriendlyNames.Family(requestedFamily)}\n\n{formatted}";
            referenceList.ItemsSource = references;
        };
        referenceList.MouseDoubleClick += async (_, _) =>
        {
            if (referenceList.SelectedItem is not ClientLibrariumLink link || !link.Reference.Contains('.')) return;
            var reference = link.Reference;
            var prefix = reference[..reference.IndexOf('.')];
            var targetFamily = session.ServerCatalogFamilies.FirstOrDefault(candidate =>
                candidate.Name.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Equals(prefix + "s", StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase));
            if (targetFamily is null) return;
            if (!string.IsNullOrWhiteSpace(requestedFamily)) navigationHistory.Push((requestedFamily, requestedQuery, requestedOffset));
            family.SelectedItem = targetFamily;
            search.Text = reference;
            await RequestPage(0);
        };
        back.Click += async (_, _) =>
        {
            if (!navigationHistory.TryPop(out var prior)) return;
            var targetFamily = session.ServerCatalogFamilies.FirstOrDefault(candidate => candidate.Name.Equals(prior.Family, StringComparison.OrdinalIgnoreCase));
            if (targetFamily is null) return;
            family.SelectedItem = targetFamily;
            search.Text = prior.Query;
            await RequestPage(prior.Offset);
        };
        close.Click += (_, _) => dialog.Close();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Render();
        dialog.Closed += (_, _) => timer.Stop();
        dialog.Content = root;
        Render(); timer.Start(); dialog.Show();
        _ = ViewModel.SendClientProtocolCommandAsync(session, "librarium");
    }

    private void EventsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var options = ViewModel.AutomationOptions.Events;
        var dialog = new Window
        {
            Title = "Reforged Scheduled Events", Owner = this,
            Width = 820, Height = 480, MinWidth = 580, MinHeight = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var warning = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 244, 204)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 135, 35)),
            BorderThickness = new Thickness(1), Padding = new Thickness(9),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new TextBlock
            {
                Text = "Events run only while Timed Events is enabled and Emergency Stop is off. They pause while disconnected and can also pause during combat.",
                TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold
            }
        };
        DockPanel.SetDock(warning, Dock.Top);
        var policy = new TextBlock
        {
            Text = $"Configured policy: pause in combat = {options.PauseEventsInCombat}; pause while disconnected = {options.PauseEventsWhileDisconnected}",
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(policy, Dock.Top);
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        DockPanel.SetDock(footer, Dock.Bottom);
        var status = new TextBlock
        {
            Text = $"{options.ScheduledEvents.Count:N0} configured event(s) · Timed Events: {(ViewModel.AutomationProfile.Capabilities.TimedEventsEnabled ? "ON" : "OFF")}",
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = NewDialogButton("Close", 70);
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(close);
        footer.Children.Add(status);
        var grid = new DataGrid
        {
            ItemsSource = options.ScheduledEvents, AutoGenerateColumns = false, IsReadOnly = false,
            CanUserAddRows = true, CanUserDeleteRows = true,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Configured", Binding = new Binding(nameof(ClientScheduledEvent.Enabled)), Width = new DataGridLength(82) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(ClientScheduledEvent.Name)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Every (min)", Binding = new Binding(nameof(ClientScheduledEvent.IntervalMinutes)), Width = new DataGridLength(85) });
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Out of combat", Binding = new Binding(nameof(ClientScheduledEvent.OnlyOutOfCombat)), Width = new DataGridLength(100) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Command", Binding = new Binding(nameof(ClientScheduledEvent.Command)), Width = new DataGridLength(1.7, DataGridLengthUnitType.Star) });
        close.Click += async (_, _) => { await ViewModel.SaveAutomationAsync(); dialog.Close(); };
        root.Children.Add(warning);
        root.Children.Add(policy);
        root.Children.Add(footer);
        root.Children.Add(grid);
        dialog.Content = root;
        dialog.Show();
    }

    private void FontClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var presentation = ViewModel.AutomationOptions.Presentation;
        var dialog = new Window
        {
            Title = "Reforged Terminal Font", Owner = this, Width = 460, Height = 310,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock { Text = "Terminal font family", FontWeight = FontWeights.Bold });
        var families = Fonts.SystemFontFamilies.OrderBy(font => font.Source, StringComparer.OrdinalIgnoreCase).ToArray();
        var family = new ComboBox { ItemsSource = families, DisplayMemberPath = nameof(FontFamily.Source), Margin = new Thickness(0, 5, 0, 10) };
        family.SelectedItem = families.FirstOrDefault(font => font.Source.Equals(presentation.FontFamily, StringComparison.OrdinalIgnoreCase))
                              ?? families.FirstOrDefault(font => font.Source.Equals("Lucida Console", StringComparison.OrdinalIgnoreCase));
        root.Children.Add(family);
        root.Children.Add(new TextBlock { Text = "Size", FontWeight = FontWeights.Bold });
        var size = new Slider { Minimum = 8, Maximum = 36, TickFrequency = 1, IsSnapToTickEnabled = true, Value = Math.Clamp(presentation.FontSize, 8, 36), Margin = new Thickness(0, 5, 0, 6) };
        var preview = new TextBlock { Text = "MajorMUD Reforged  HP=100  ╔═ ANSI ═╗", Margin = new Thickness(0, 4, 0, 14) };
        root.Children.Add(size);
        root.Children.Add(preview);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var save = NewDialogButton("Apply & save", 100);
        var close = NewDialogButton("Cancel", 70);
        buttons.Children.Add(save);
        buttons.Children.Add(close);
        root.Children.Add(buttons);
        void Preview()
        {
            if (family.SelectedItem is FontFamily selected) preview.FontFamily = selected;
            preview.FontSize = size.Value;
        }
        family.SelectionChanged += (_, _) => Preview();
        size.ValueChanged += (_, _) => Preview();
        save.Click += async (_, _) =>
        {
            presentation.FontFamily = (family.SelectedItem as FontFamily)?.Source ?? "Lucida Console";
            presentation.FontSize = (int)Math.Round(size.Value);
            await ViewModel.SaveAutomationOptionsAsync();
            RefreshClassicTerminalState();
            dialog.Close();
        };
        close.Click += (_, _) => dialog.Close();
        Preview();
        dialog.Content = root;
        dialog.ShowDialog();
    }

    private void CharacterProgressCalculatorClicked(object sender, RoutedEventArgs e)
    {
        if (BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"CP / Experience Calculator - {session.DisplayName}", Owner = this,
            Width = 520, Height = 470, MinWidth = 440, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var recalculate = NewDialogButton("Recalculate", 90);
        var close = NewDialogButton("Close", 70);
        footer.Children.Add(recalculate);
        footer.Children.Add(close);
        var inputs = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(inputs, Dock.Top);
        inputs.Children.Add(new TextBlock { Text = "Average experience per victory (optional)", FontWeight = FontWeights.Bold });
        var averageReward = new TextBox { Text = "0", Margin = new Thickness(0, 5, 0, 5) };
        inputs.Children.Add(averageReward);
        inputs.Children.Add(new TextBlock
        {
            Text = "Values below come only from admitted STATS/EXPERIENCE output and verified session rewards.",
            TextWrapping = TextWrapping.Wrap
        });
        var report = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White, Foreground = Brushes.Black,
            FontFamily = new FontFamily("Consolas"), FontSize = 13, Padding = new Thickness(8)
        };

        void Calculate()
        {
            _ = long.TryParse(averageReward.Text.Replace(",", string.Empty, StringComparison.Ordinal), out var average);
            average = Math.Max(0, average);
            var remaining = session.ExperienceRemaining > 0
                ? session.ExperienceRemaining
                : session.ExperienceNeeded > 0 ? Math.Max(0, session.ExperienceNeeded - session.ExperienceCurrent) : 0;
            var percent = session.ExperienceNeeded > 0
                ? Math.Clamp(session.ExperienceCurrent * 100d / session.ExperienceNeeded, 0d, 100d)
                : 0d;
            var elapsed = session.ConnectedAt is { } connected ? DateTimeOffset.Now - connected : TimeSpan.Zero;
            var hourly = elapsed.TotalMinutes >= 1 ? session.SessionExperienceGained / elapsed.TotalHours : 0d;
            var victories = average > 0 && remaining > 0 ? (long)Math.Ceiling(remaining / (double)average) : 0;
            var hours = hourly > 0 && remaining > 0 ? remaining / hourly : 0d;
            report.Text =
                $"Level                 {(session.CharacterLevel > 0 ? session.CharacterLevel : "Not captured")}\n" +
                $"Lives                 {(session.ExperienceUpdatedAt is not null ? session.Lives : "Not captured")}\n" +
                $"Character points      {(session.ExperienceUpdatedAt is not null ? session.CharacterPoints : "Not captured")}\n\n" +
                $"Current experience    {(session.ExperienceUpdatedAt is not null ? session.ExperienceCurrent.ToString("N0") : "Not captured")}\n" +
                $"Next-level threshold  {(session.ExperienceNeeded > 0 ? session.ExperienceNeeded.ToString("N0") : "Not captured")}\n" +
                $"Experience remaining  {(session.ExperienceNeeded > 0 ? remaining.ToString("N0") : "Not captured")}\n" +
                $"Progress              {(session.ExperienceNeeded > 0 ? $"{percent:0.0}%" : "Not captured")}\n\n" +
                $"Session XP gained     {session.SessionExperienceGained:N0}\n" +
                $"Observed XP/hour      {(hourly > 0 ? hourly.ToString("N0") : "Insufficient timed data")}\n" +
                $"Estimated victories   {(victories > 0 ? victories.ToString("N0") : "Enter an average reward")}\n" +
                $"Estimated time        {(hours > 0 ? $"{hours:0.00} hours" : "Insufficient timed data")}\n\n" +
                "Next-level CP award is not predicted here because it is not part of the client projection. Current CP is observational only.";
        }

        recalculate.Click += (_, _) => Calculate();
        averageReward.KeyDown += (_, args) => { if (args.Key == Key.Enter) { Calculate(); args.Handled = true; } };
        close.Click += (_, _) => dialog.Close();
        root.Children.Add(footer);
        root.Children.Add(inputs);
        root.Children.Add(report);
        dialog.Content = root;
        Calculate();
        dialog.Show();
    }

    private void InventoryCalculatorClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"Inventory Calculator - {session.DisplayName}", Owner = this,
            Width = 570, Height = 540, MinWidth = 470, MinHeight = 430,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var recalculate = NewDialogButton("Recalculate", 90);
        var refreshInventory = NewDialogButton("Refresh inventory", 115);
        var refreshEquipment = NewDialogButton("Refresh equipment", 120);
        var close = NewDialogButton("Close", 70);
        footer.Children.Add(recalculate);
        footer.Children.Add(refreshInventory);
        footer.Children.Add(refreshEquipment);
        footer.Children.Add(close);

        var inputs = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputs.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inputs.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var weightLabel = new TextBlock { Text = "Hypothetical unit weight", FontWeight = FontWeights.Bold };
        var quantityLabel = new TextBlock { Text = "Quantity", FontWeight = FontWeights.Bold };
        Grid.SetColumn(quantityLabel, 1);
        var unitWeight = new TextBox { Text = "0", Margin = new Thickness(0, 5, 8, 0) };
        Grid.SetRow(unitWeight, 1);
        var quantity = new TextBox { Text = "1", Margin = new Thickness(8, 5, 0, 0) };
        Grid.SetRow(quantity, 1); Grid.SetColumn(quantity, 1);
        inputs.Children.Add(weightLabel); inputs.Children.Add(quantityLabel);
        inputs.Children.Add(unitWeight); inputs.Children.Add(quantity);

        var report = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White, Foreground = Brushes.Black,
            FontFamily = new FontFamily("Consolas"), FontSize = 13, Padding = new Thickness(8)
        };

        void Calculate()
        {
            var rows = ViewModel.ResolveItemWeights(session);
            var resolved = rows.Aggregate(0L, (sum, row) =>
                sum > long.MaxValue - row.TotalWeight ? long.MaxValue : sum + row.TotalWeight);
            _ = long.TryParse(unitWeight.Text.Replace(",", string.Empty, StringComparison.Ordinal), out var proposedUnit);
            _ = int.TryParse(quantity.Text.Replace(",", string.Empty, StringComparison.Ordinal), out var proposedQuantity);
            proposedUnit = Math.Max(0, proposedUnit);
            proposedQuantity = Math.Max(0, proposedQuantity);
            var proposed = proposedQuantity > 0 && proposedUnit > long.MaxValue / proposedQuantity
                ? long.MaxValue : proposedUnit * proposedQuantity;
            var projected = session.CarriedWeight > long.MaxValue - proposed ? long.MaxValue : session.CarriedWeight + proposed;
            var remaining = session.CarryingCapacity > 0 ? Math.Max(0, session.CarryingCapacity - session.CarriedWeight) : 0;
            var projectedRemaining = session.CarryingCapacity > 0 ? Math.Max(0, session.CarryingCapacity - projected) : 0;
            var projectedPercent = session.CarryingCapacity > 0
                ? Math.Clamp(projected * 100d / session.CarryingCapacity, 0d, 999d) : 0d;
            var capturedStacks = session.InventoryItems.Count + session.EquippedItems.Count;
            var unresolvedStacks = Math.Max(0, capturedStacks - rows.Count);
            report.Text =
                $"Captured inventory stacks   {session.InventoryItems.Count:N0}\n" +
                $"Captured equipped stacks    {session.EquippedItems.Count:N0}\n" +
                $"Catalog-resolved stacks     {rows.Count:N0}\n" +
                $"Unresolved captured stacks  {unresolvedStacks:N0}\n\n" +
                $"Authoritative carried       {session.CarriedWeight:N0}\n" +
                $"Authoritative capacity      {(session.CarryingCapacity > 0 ? session.CarryingCapacity.ToString("N0") : "Not captured")}\n" +
                $"Remaining capacity          {(session.CarryingCapacity > 0 ? remaining.ToString("N0") : "Not captured")}\n" +
                $"Resolved catalog subtotal   {resolved:N0}\n\n" +
                $"Hypothetical added weight   {proposed:N0}\n" +
                $"Projected carried weight    {projected:N0}\n" +
                $"Projected remaining         {(session.CarryingCapacity > 0 ? projectedRemaining.ToString("N0") : "Not captured")}\n" +
                $"Projected load              {(session.CarryingCapacity > 0 ? $"{projectedPercent:0.0}%" : "Not captured")}\n" +
                $"Fits captured capacity      {(session.CarryingCapacity > 0 ? (projected <= session.CarryingCapacity ? "Yes" : "No") : "Unknown")}\n\n" +
                "The authoritative carried total governs projections. Catalog subtotal is diagnostic only because containers or unresolved names may prevent a complete item join.";
        }

        recalculate.Click += (_, _) => Calculate();
        refreshInventory.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata inventory");
        refreshEquipment.Click += async (_, _) => await ViewModel.SendClientProtocolCommandAsync(session, "clientdata equipment");
        close.Click += (_, _) => dialog.Close();
        foreach (var input in new[] { unitWeight, quantity }) input.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            Calculate(); args.Handled = true;
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Calculate();
        dialog.Closed += (_, _) => timer.Stop();
        root.Children.Add(footer);
        root.Children.Add(inputs);
        root.Children.Add(report);
        dialog.Content = root;
        Calculate(); timer.Start(); dialog.Show();
    }

    private void SoundClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var sounds = ViewModel.AutomationOptions.Sounds;
        var dialog = new Window
        {
            Title = "Reforged Client Sounds", Owner = this, Width = 430, Height = 400,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new StackPanel { Margin = new Thickness(14) };
        var enabled = new CheckBox { Content = "Enable client notification sounds", IsChecked = sounds.Enabled, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) };
        var tell = new CheckBox { Content = "Incoming tell", IsChecked = sounds.TellSound, Margin = new Thickness(12, 3, 0, 3) };
        var combat = new CheckBox { Content = "Combat begins", IsChecked = sounds.CombatSound, Margin = new Thickness(12, 3, 0, 3) };
        var health = new CheckBox { Content = "Health reaches 25% or below", IsChecked = sounds.LowHealthSound, Margin = new Thickness(12, 3, 0, 3) };
        var route = new CheckBox { Content = "Route completes", IsChecked = sounds.RouteCompleteSound, Margin = new Thickness(12, 3, 0, 3) };
        var disconnect = new CheckBox { Content = "Connection is lost", IsChecked = sounds.DisconnectSound, Margin = new Thickness(12, 3, 0, 3) };
        root.Children.Add(enabled);
        foreach (var check in new[] { tell, combat, health, route, disconnect }) root.Children.Add(check);
        root.Children.Add(new TextBlock
        {
            Text = "Notifications use Windows system sounds and follow the Windows volume setting.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12)
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var test = NewDialogButton("Test", 65);
        var save = NewDialogButton("Save", 70);
        var close = NewDialogButton("Cancel", 70);
        buttons.Children.Add(test);
        buttons.Children.Add(save);
        buttons.Children.Add(close);
        root.Children.Add(buttons);
        test.Click += (_, _) => System.Media.SystemSounds.Asterisk.Play();
        save.Click += async (_, _) =>
        {
            sounds.Enabled = enabled.IsChecked == true;
            sounds.TellSound = tell.IsChecked == true;
            sounds.CombatSound = combat.IsChecked == true;
            sounds.LowHealthSound = health.IsChecked == true;
            sounds.RouteCompleteSound = route.IsChecked == true;
            sounds.DisconnectSound = disconnect.IsChecked == true;
            await ViewModel.SaveAutomationOptionsAsync();
            dialog.Close();
        };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = root;
        dialog.ShowDialog();
    }

    private void ImportPathClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var dialog = new Window
        {
            Title = "Import MegaMUD Path into Reforged", Owner = this,
            Width = 840, Height = 520, MinWidth = 600, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new DockPanel { Margin = new Thickness(12) };
        var note = new TextBlock
        {
            Text = "Imports a read-only MegaMUD .mp route into the local Reforged profile. It does not run the path or change the source file.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(note, Dock.Top);
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        DockPanel.SetDock(footer, Dock.Bottom);
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var close = NewDialogButton("Close", 70);
        var import = NewDialogButton("Import selected", 110);
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(import, Dock.Right);
        footer.Children.Add(close);
        footer.Children.Add(import);
        footer.Children.Add(status);
        var grid = new DataGrid
        {
            ItemsSource = ViewModel.VisiblePaths, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Path", Binding = new Binding(nameof(MegaMudPathRecord.DisplayName)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "File", Binding = new Binding(nameof(MegaMudPathRecord.FileName)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Steps", Binding = new Binding(nameof(MegaMudPathRecord.StepSummary)), Width = new DataGridLength(120) });
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Imported", Binding = new Binding(nameof(MegaMudPathRecord.IsImported)), Width = new DataGridLength(75) });
        import.IsEnabled = false;
        grid.SelectionChanged += (_, _) => import.IsEnabled = grid.SelectedItem is MegaMudPathRecord;
        import.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not MegaMudPathRecord route) return;
            ViewModel.SelectedPath = route;
            await ViewModel.ImportSelectedPathAsync();
            grid.Items.Refresh();
            status.Text = $"Imported {route.FileName}.";
        };
        close.Click += (_, _) => dialog.Close();
        root.Children.Add(note);
        root.Children.Add(footer);
        root.Children.Add(grid);
        dialog.Content = root;
        dialog.Show();
    }

    private void RecordPathClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || BoundSession is not { } session) return;
        var dialog = new Window
        {
            Title = $"Record Reforged Path — {session.CharacterName}", Owner = this,
            Width = 620, Height = 470, MinWidth = 480, MinHeight = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var explanation = new TextBlock
        {
            Text = "Records visible commands you manually submit in this terminal. It never sends, repeats, or replays a command.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
        };
        var name = new TextBox { Text = ViewModel.RecordingRouteName, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(name, 1);
        var commands = new ListBox
        {
            ItemsSource = ViewModel.RouteRecording.Commands,
            Background = Brushes.White, Foreground = Brushes.Black
        };
        Grid.SetRow(commands, 2);
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        Grid.SetRow(footer, 3);
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var close = NewDialogButton("Close", 70);
        var cancel = NewDialogButton("Cancel", 70);
        var finish = NewDialogButton("Finish & save", 100);
        var start = NewDialogButton("Start recording", 105);
        foreach (var button in new[] { close, cancel, finish, start })
        {
            DockPanel.SetDock(button, Dock.Right);
            footer.Children.Add(button);
        }
        footer.Children.Add(status);
        root.Children.Add(explanation);
        root.Children.Add(name);
        root.Children.Add(commands);
        root.Children.Add(footer);
        dialog.Content = root;

        void Refresh()
        {
            var recording = ViewModel.RouteRecording;
            var ownsRecording = recording.IsRecording && recording.SessionId.Equals(session.SessionId, StringComparison.OrdinalIgnoreCase);
            start.IsEnabled = !recording.IsRecording && session.ConnectionState == ClientConnectionState.Connected;
            finish.IsEnabled = ownsRecording && recording.Commands.Count > 0;
            cancel.IsEnabled = ownsRecording;
            name.IsEnabled = !recording.IsRecording;
            status.Text = recording.IsRecording
                ? ownsRecording ? $"Recording {recording.Commands.Count:N0} command(s) from this session." : "Another session owns the active recording."
                : session.ConnectionState == ClientConnectionState.Connected ? "Ready to record manual commands." : "Connect this session before recording.";
            if (recording.Commands.Count > 0)
                commands.ScrollIntoView(recording.Commands[^1]);
        }
        start.Click += (_, _) =>
        {
            ViewModel.RecordingRouteName = string.IsNullOrWhiteSpace(name.Text) ? "Recorded Route" : name.Text.Trim();
            ViewModel.StartRouteRecording(session);
            Refresh();
        };
        finish.Click += async (_, _) => { await ViewModel.FinishRouteRecordingAsync(session); Refresh(); };
        cancel.Click += (_, _) => { ViewModel.CancelRouteRecording(); Refresh(); };
        close.Click += (_, _) => dialog.Close();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) => Refresh();
        dialog.Closed += (_, _) => timer.Stop();
        Refresh();
        timer.Start();
        dialog.Show();
    }

    private void FixPathStepsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var dialog = new Window
        {
            Title = "Fix Reforged Path Steps", Owner = this,
            Width = 760, Height = 540, MinWidth = 560, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush,
            FontFamily = new FontFamily("Tahoma"), FontSize = 12
        };
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var paths = new ComboBox { ItemsSource = ViewModel.LocalPaths, DisplayMemberPath = nameof(ImportedPathEntry.DisplayName), Margin = new Thickness(0, 0, 0, 8) };
        var commands = new ListBox { Background = Brushes.White, Foreground = Brushes.Black };
        Grid.SetRow(commands, 1);
        var editor = new TextBox { Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetRow(editor, 2);
        var buttons = new WrapPanel();
        Grid.SetRow(buttons, 3);
        var add = NewDialogButton("Add", 65);
        var update = NewDialogButton("Update", 70);
        var remove = NewDialogButton("Remove", 70);
        var up = NewDialogButton("Move up", 75);
        var down = NewDialogButton("Move down", 85);
        var save = NewDialogButton("Save", 70);
        var close = NewDialogButton("Close", 70);
        foreach (var button in new[] { add, update, remove, up, down, save, close }) buttons.Children.Add(button);
        root.Children.Add(paths);
        root.Children.Add(commands);
        root.Children.Add(editor);
        root.Children.Add(buttons);

        void SelectPath(ImportedPathEntry? path)
        {
            ViewModel.SelectedLocalPath = path;
            commands.ItemsSource = path?.Commands;
            commands.SelectedIndex = path?.Commands.Count > 0 ? 0 : -1;
        }
        paths.SelectionChanged += (_, _) => SelectPath(paths.SelectedItem as ImportedPathEntry);
        commands.SelectionChanged += (_, _) =>
        {
            ViewModel.SelectedRouteCommand = commands.SelectedItem as string;
            editor.Text = ViewModel.SelectedRouteCommand ?? string.Empty;
        };
        add.Click += (_, _) => { ViewModel.RouteCommandEditor = editor.Text; ViewModel.AddLocalPathCommand(); commands.Items.Refresh(); };
        update.Click += (_, _) => { ViewModel.RouteCommandEditor = editor.Text; ViewModel.UpdateLocalPathCommand(); commands.Items.Refresh(); };
        remove.Click += (_, _) => { ViewModel.RemoveLocalPathCommand(); commands.Items.Refresh(); };
        up.Click += (_, _) => { ViewModel.MoveLocalPathCommand(-1); commands.Items.Refresh(); };
        down.Click += (_, _) => { ViewModel.MoveLocalPathCommand(1); commands.Items.Refresh(); };
        save.Click += async (_, _) => await ViewModel.SaveLocalPathAsync();
        close.Click += (_, _) => dialog.Close();
        paths.SelectedItem = ViewModel.SelectedLocalPath ?? ViewModel.LocalPaths.FirstOrDefault();
        dialog.Content = root;
        dialog.Show();
    }

    private static ListBox NewStateList(IEnumerable<string> source) => new()
    {
        ItemsSource = source,
        Background = Brushes.White,
        Foreground = Brushes.Black,
        BorderBrush = SystemColors.ActiveBorderBrush,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(4),
        HorizontalContentAlignment = HorizontalAlignment.Stretch
    };

    private static bool IsLoop(MegaMudPathRecord path)
    {
        if (path.FileName.Contains("LOOP", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrWhiteSpace(path.EndLabel)) return true;
        return string.Equals(CleanLabel(path.StartLabel), CleanLabel(path.EndLabel), StringComparison.OrdinalIgnoreCase);
    }

    private static string RegionOf(MegaMudPathRecord path)
    {
        if (!string.IsNullOrWhiteSpace(path.Start?.Region)) return path.Start.Region;
        var label = path.StartLabel;
        var comma = label.IndexOf(',');
        return comma > 0 ? label[..comma].Trim() : "Other";
    }

    private static string CleanLabel(string label) => string.IsNullOrWhiteSpace(label) ? "Unnamed route" : label.Trim();

    private async void ViewModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ViewModel is null) return;
        await ViewModel.SaveTerminalViewModeAsync(BoundSession);
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
