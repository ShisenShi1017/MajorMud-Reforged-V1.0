using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Reforged.App.Models;
using Reforged.App.Services;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

/// <summary>
/// MudPlay-inspired navigation assistant rebuilt on Reforged's WPF/session architecture.
/// It plans only; actual movement always goes through PlayViewModel -> ClientSessionService
/// and therefore retains the existing route confirmation, command origin, combat pause,
/// emergency-stop, and desynchronization protections.
/// </summary>
public sealed class MudPlayNavigationAssistantWindow : Window
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly MudPlayNavigationService _navigation = new();
    private readonly MudPlayNavigationProfile _profile;
    private readonly DispatcherTimer _timer;

    private IReadOnlyList<MudPlayNavigationRoom> _rooms = [];
    private IReadOnlyList<MudPlayNavigationLair> _lairs = [];
    private IReadOnlyList<MudPlayNavigationBankLocation> _banks = [];
    private MudPlayNavigationPlan? _currentPlan;
    private MudPlayNavigationPlan? _currentLoop;

    private readonly TextBlock _contextText = NewText(string.Empty, 12, FontWeights.SemiBold);
    private readonly TextBlock _roomText = NewText(string.Empty, 12, FontWeights.Normal);
    private readonly TextBlock _routeStateText = NewText(string.Empty, 11, FontWeights.Normal);
    private readonly TextBlock _statusText = NewText("Loading navigation data...", 11, FontWeights.Normal);
    private readonly TextBox _roomSearch = NewTextBox();
    private readonly ListBox _roomList = NewListBox();
    private readonly TextBox _planDetails = NewReadOnlyTextBox();
    private readonly TextBox _savedName = NewTextBox();
    private readonly ListBox _savedList = NewListBox();
    private readonly ListBox _bankList = NewListBox();
    private readonly TextBox _lairSearch = NewTextBox();
    private readonly ListBox _lairList = NewListBox();
    private readonly TextBox _loopDetails = NewReadOnlyTextBox();
    private readonly TextBox _recoveryDetails = NewReadOnlyTextBox();

    private readonly CheckBox _avoidDamageTraps = NewCheck("Avoid damage traps");
    private readonly CheckBox _avoidSpellTraps = NewCheck("Avoid spell traps");
    private readonly CheckBox _avoidRestricted = NewCheck("Avoid restricted exits");
    private readonly CheckBox _avoidHidden = NewCheck("Avoid hidden exits");
    private readonly CheckBox _allowDoors = NewCheck("Allow ordinary doors");
    private readonly CheckBox _avoidHighNpcs = NewCheck("Avoid high-level NPC rooms");
    private readonly CheckBox _avoidRoomEffects = NewCheck("Avoid room spell/effect rooms");
    private readonly CheckBox _allowRiskyFallback = NewCheck("Allow least-risk fallback");
    private readonly TextBox _npcBuffer = NewSmallTextBox(42);
    private readonly TextBox _loopMin = NewSmallTextBox(42);
    private readonly TextBox _loopMax = NewSmallTextBox(42);

    public MudPlayNavigationAssistantWindow(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _profile = _navigation.LoadProfile(session);

        Title = $"Navigation Assistant — {DisplayCharacter(session)}";
        Width = 1060;
        Height = 760;
        MinWidth = 820;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;
        Content = BuildContent();

        ApplyPreferencesToControls();
        _savedList.ItemsSource = _profile.SavedDestinations;
        _savedList.DisplayMemberPath = nameof(MudPlaySavedDestination.DisplayName);

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => RefreshLiveStatus(), Dispatcher);
        Loaded += async (_, _) =>
        {
            _timer.Start();
            RefreshLiveStatus();
            await LoadNavigationDataAsync();
        };
        Closing += (_, _) =>
        {
            _timer.Stop();
            CapturePreferences();
            TrySaveProfile();
        };
        Session.PropertyChanged += SessionPropertyChanged;
        Closed += (_, _) => Session.PropertyChanged -= SessionPropertyChanged;
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(12) };

        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerText = new StackPanel();
        headerText.Children.Add(_contextText);
        headerText.Children.Add(_roomText);
        headerText.Children.Add(_routeStateText);
        header.Children.Add(headerText);
        var reload = NewButton("Reload Game Data", 130);
        reload.Click += async (_, _) =>
        {
            _navigation.Invalidate();
            await LoadNavigationDataAsync();
        };
        Grid.SetColumn(reload, 1);
        header.Children.Add(reload);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new Border
        {
            BorderThickness = new Thickness(1, 1, 0, 0),
            BorderBrush = SystemColors.ActiveBorderBrush,
            Padding = new Thickness(4, 7, 4, 0),
            Margin = new Thickness(0, 8, 0, 0),
            Child = _statusText
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Route Planner", Content = BuildRoutePlannerTab() });
        tabs.Items.Add(new TabItem { Header = "Saved / Stash", Content = BuildSavedTab() });
        tabs.Items.Add(new TabItem { Header = "Lairs", Content = BuildLairTab() });
        tabs.Items.Add(new TabItem { Header = "Loops / Recovery", Content = BuildLoopRecoveryTab() });
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildRoutePlannerTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.43, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.57, GridUnitType.Star) });

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition());
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.Children.Add(NewText("Destination search", 12, FontWeights.SemiBold));
        _roomSearch.Margin = new Thickness(0, 5, 0, 6);
        _roomSearch.ToolTip = "Search effective room names or map/room keys.";
        Grid.SetRow(_roomSearch, 1);
        left.Children.Add(_roomSearch);
        Grid.SetRow(_roomList, 2);
        left.Children.Add(_roomList);
        var routeButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0) };
        var plan = NewButton("Plan Safe Route", 110);
        var run = NewButton("Run Planned Route", 120);
        plan.Click += async (_, _) => await PlanSelectedRoomAsync();
        run.Click += async (_, _) => await RunPlanAsync(_currentPlan, autoRepeat: false);
        routeButtons.Children.Add(plan);
        routeButtons.Children.Add(run);
        Grid.SetRow(routeButtons, 3);
        left.Children.Add(routeButtons);
        root.Children.Add(left);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition());
        var prefs = BuildPreferencesPanel();
        right.Children.Add(prefs);
        _planDetails.Margin = new Thickness(0, 8, 0, 0);
        Grid.SetRow(_planDetails, 1);
        right.Children.Add(_planDetails);
        Grid.SetColumn(right, 2);
        root.Children.Add(right);

        _roomSearch.TextChanged += (_, _) => FilterRooms();
        _roomList.MouseDoubleClick += async (_, _) => await PlanSelectedRoomAsync();
        return root;
    }

    private UIElement BuildPreferencesPanel()
    {
        var box = new GroupBox { Header = "Route safety", Padding = new Thickness(8) };
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var check in new[]
                 {
                     _avoidDamageTraps, _avoidSpellTraps, _avoidRestricted, _avoidHidden,
                     _allowDoors, _avoidHighNpcs, _avoidRoomEffects, _allowRiskyFallback
                 })
        {
            check.Margin = new Thickness(0, 0, 14, 5);
            panel.Children.Add(check);
        }
        panel.Children.Add(NewText("NPC level buffer:", 11, FontWeights.Normal, new Thickness(0, 3, 4, 5)));
        panel.Children.Add(_npcBuffer);
        box.Content = panel;
        return box;
    }

    private UIElement BuildSavedTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        var saved = new Grid();
        saved.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        saved.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        saved.RowDefinitions.Add(new RowDefinition());
        saved.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        saved.Children.Add(NewText("Saved destinations", 12, FontWeights.SemiBold));
        _savedName.Margin = new Thickness(0, 5, 0, 6);
        _savedName.Text = "My location";
        Grid.SetRow(_savedName, 1);
        saved.Children.Add(_savedName);
        Grid.SetRow(_savedList, 2);
        saved.Children.Add(_savedList);
        var buttons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        var favorite = NewButton("Save Current Favorite", 135);
        var stash = NewButton("Save Current Stash", 125);
        var route = NewButton("Route Here", 90);
        var remove = NewButton("Remove", 75);
        favorite.Click += (_, _) => SaveCurrentDestination(MudPlaySavedDestinationKind.Favorite);
        stash.Click += (_, _) => SaveCurrentDestination(MudPlaySavedDestinationKind.Stash);
        route.Click += async (_, _) => await RouteToSavedAsync();
        remove.Click += (_, _) => RemoveSaved();
        buttons.Children.Add(favorite);
        buttons.Children.Add(stash);
        buttons.Children.Add(route);
        buttons.Children.Add(remove);
        Grid.SetRow(buttons, 3);
        saved.Children.Add(buttons);
        root.Children.Add(saved);

        var banks = new Grid();
        banks.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        banks.RowDefinitions.Add(new RowDefinition());
        banks.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        banks.Children.Add(NewText("Resolved bank / vault locations", 12, FontWeights.SemiBold));
        _bankList.Margin = new Thickness(0, 5, 0, 0);
        Grid.SetRow(_bankList, 1);
        banks.Children.Add(_bankList);
        var bankRoute = NewButton("Plan Route to Bank", 125);
        bankRoute.Margin = new Thickness(0, 7, 0, 0);
        bankRoute.Click += async (_, _) => await RouteToBankAsync();
        Grid.SetRow(bankRoute, 2);
        banks.Children.Add(bankRoute);
        Grid.SetColumn(banks, 2);
        root.Children.Add(banks);
        return root;
    }

    private UIElement BuildLairTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(NewText("Decoded lairs / areas from the effective room projection", 12, FontWeights.SemiBold));
        _lairSearch.Margin = new Thickness(0, 5, 0, 6);
        Grid.SetRow(_lairSearch, 1);
        root.Children.Add(_lairSearch);
        Grid.SetRow(_lairList, 2);
        root.Children.Add(_lairList);
        var buttons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        var selected = NewButton("Plan Selected Lair", 125);
        var nearest = NewButton("Plan Nearest Lair", 120);
        var run = NewButton("Run Planned Route", 120);
        var auto = NewButton("Auto-Roam Lair", 110);
        selected.Click += async (_, _) => await PlanSelectedLairAsync();
        nearest.Click += async (_, _) => await PlanNearestLairAsync();
        run.Click += async (_, _) => await RunPlanAsync(_currentPlan, autoRepeat: false);
        auto.Click += async (_, _) => await AutoRoamSelectedLairAsync();
        buttons.Children.Add(selected);
        buttons.Children.Add(nearest);
        buttons.Children.Add(run);
        buttons.Children.Add(auto);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);
        _lairSearch.TextChanged += (_, _) => FilterLairs();
        return root;
    }

    private UIElement BuildLoopRecoveryTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition());

        var loopBox = new GroupBox { Header = "Safe loop generator", Padding = new Thickness(8) };
        var loopGrid = new Grid();
        loopGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        loopGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        loopGrid.RowDefinitions.Add(new RowDefinition());
        var loopOptions = new WrapPanel();
        loopOptions.Children.Add(NewText("Minimum steps:", 11, FontWeights.Normal, new Thickness(0, 3, 4, 0)));
        loopOptions.Children.Add(_loopMin);
        loopOptions.Children.Add(NewText("Maximum steps:", 11, FontWeights.Normal, new Thickness(12, 3, 4, 0)));
        loopOptions.Children.Add(_loopMax);
        var build = NewButton("Generate Safe Loop", 120);
        var run = NewButton("Auto-Roam Loop", 105);
        build.Margin = new Thickness(12, 0, 0, 0);
        build.Click += async (_, _) => await GenerateLoopAsync();
        run.Click += async (_, _) => await RunPlanAsync(_currentLoop, autoRepeat: true);
        loopOptions.Children.Add(build);
        loopOptions.Children.Add(run);
        loopGrid.Children.Add(loopOptions);
        _loopDetails.Margin = new Thickness(0, 7, 0, 0);
        Grid.SetRow(_loopDetails, 2);
        loopGrid.Children.Add(_loopDetails);
        loopBox.Content = loopGrid;
        root.Children.Add(loopBox);

        var recoveryBox = new GroupBox { Header = "Route recovery", Padding = new Thickness(8) };
        var recoveryGrid = new Grid();
        recoveryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        recoveryGrid.RowDefinitions.Add(new RowDefinition());
        var recoveryButtons = new WrapPanel();
        var planRecovery = NewButton("Plan Safe Recovery", 120);
        var runRecovery = NewButton("Run Recovery", 95);
        planRecovery.Click += async (_, _) => await PlanRecoveryAsync();
        runRecovery.Click += async (_, _) => await RunPlanAsync(_currentPlan, autoRepeat: false);
        recoveryButtons.Children.Add(planRecovery);
        recoveryButtons.Children.Add(runRecovery);
        recoveryGrid.Children.Add(recoveryButtons);
        _recoveryDetails.Margin = new Thickness(0, 7, 0, 0);
        Grid.SetRow(_recoveryDetails, 1);
        recoveryGrid.Children.Add(_recoveryDetails);
        recoveryBox.Content = recoveryGrid;
        Grid.SetRow(recoveryBox, 2);
        root.Children.Add(recoveryBox);
        return root;
    }

    private async Task LoadNavigationDataAsync()
    {
        try
        {
            SetStatus("Loading effective rooms, exits, spawns, NPC levels, lairs, and bank locations...");
            var data = await Task.Run(() => new NavigationLoadResult(
                _navigation.GetRooms(Session),
                _navigation.GetLairs(Session),
                _navigation.GetBankLocations(Session),
                _navigation.ResolveContext(Session)));
            _rooms = data.Rooms;
            _lairs = data.Lairs;
            _banks = data.Banks;
            _contextText.Text = data.Context.Description;
            FilterRooms();
            FilterLairs();
            _bankList.ItemsSource = _banks;
            _bankList.DisplayMemberPath = nameof(MudPlayNavigationBankLocation.DisplayName);
            SetStatus($"Navigation index ready: {_rooms.Count:N0} rooms • {_lairs.Count:N0} lairs • {_banks.Count:N0} unambiguous bank/vault locations.");
        }
        catch (Exception exception)
        {
            SetStatus($"Navigation index failed safely: {exception.Message}");
        }
    }

    private void FilterRooms()
    {
        var filter = _roomSearch.Text.Trim();
        var results = _rooms
            .Where(room => filter.Length == 0 || room.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || room.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Take(500)
            .ToArray();
        _roomList.ItemsSource = results;
        _roomList.DisplayMemberPath = nameof(MudPlayNavigationRoom.DisplayName);
    }

    private void FilterLairs()
    {
        var filter = _lairSearch.Text.Trim();
        var results = _lairs
            .Where(lair => filter.Length == 0 || lair.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || lair.LairId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _lairList.ItemsSource = results;
        _lairList.DisplayMemberPath = nameof(MudPlayNavigationLair.DisplayName);
    }

    private async Task PlanSelectedRoomAsync()
    {
        if (_roomList.SelectedItem is not MudPlayNavigationRoom room)
        {
            SetStatus("Select a destination room first.");
            return;
        }
        await PlanRouteAsync(room.Key, _planDetails);
    }

    private async Task PlanRouteAsync(string destination, TextBox detailsTarget)
    {
        CapturePreferences();
        SetStatus($"Planning from {Session.CurrentRoomKey} to {destination}...");
        var plan = await Task.Run(() => _navigation.PlanRoute(Session, destination, _profile.Preferences));
        _currentPlan = plan;
        detailsTarget.Text = DescribePlan(plan);
        SetStatus(plan.Success ? $"Route planned: {plan.Summary}." : plan.Summary);
    }

    private async Task RunPlanAsync(MudPlayNavigationPlan? plan, bool autoRepeat)
    {
        if (plan?.Route is null)
        {
            SetStatus("Plan a route first. Nothing was sent.");
            return;
        }
        if (plan.Route.Steps.Count == 0)
        {
            SetStatus("The planned destination is already the current room. Nothing was sent.");
            return;
        }
        if (plan.UsedRiskyFallback && MessageBox.Show(this,
                "Strict hazard avoidance could not find a route. This plan uses the least-risk fallback and includes scored hazards.\n\nRun it anyway?",
                "Confirm Risky Navigation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            SetStatus("Risky route was not started.");
            return;
        }
        try
        {
            if (autoRepeat)
                await _host.StartAutoRoamAsync(Session, plan.Route);
            else
                await _host.StartRouteToDestinationAsync(Session, plan.Route, requireExactStart: false);
            SetStatus(_host.Status);
        }
        catch (Exception exception)
        {
            SetStatus($"Route start failed safely: {exception.Message}");
        }
    }

    private void SaveCurrentDestination(MudPlaySavedDestinationKind kind)
    {
        var key = MudPlayNavigationService.NormalizeRoomKey(Session.CurrentRoomKey);
        if (key.Length == 0)
        {
            SetStatus("The current room does not expose a map-qualified key, so it was not saved.");
            return;
        }
        var name = string.IsNullOrWhiteSpace(_savedName.Text) ? Session.CurrentRoomName : _savedName.Text.Trim();
        var existing = _profile.SavedDestinations.FirstOrDefault(destination =>
            destination.Kind == kind && destination.RoomKey.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _profile.SavedDestinations.Add(new MudPlaySavedDestination { Name = name, RoomKey = key, Kind = kind });
        }
        else
        {
            existing.Name = name;
            existing.SavedAtUtc = DateTimeOffset.UtcNow;
            // ObservableCollection does not notify field changes; refresh the view explicitly.
            _savedList.Items.Refresh();
        }
        TrySaveProfile();
        SetStatus($"Saved {kind.ToString().ToLowerInvariant()} '{name}' at {key}.");
    }

    private void RemoveSaved()
    {
        if (_savedList.SelectedItem is not MudPlaySavedDestination selected) return;
        _profile.SavedDestinations.Remove(selected);
        TrySaveProfile();
        SetStatus($"Removed saved destination '{selected.Name}'.");
    }

    private async Task RouteToSavedAsync()
    {
        if (_savedList.SelectedItem is not MudPlaySavedDestination selected)
        {
            SetStatus("Select a saved destination first.");
            return;
        }
        await PlanRouteAsync(selected.RoomKey, _planDetails);
    }

    private async Task RouteToBankAsync()
    {
        if (_bankList.SelectedItem is not MudPlayNavigationBankLocation bank)
        {
            SetStatus("Select a resolved bank/vault location first.");
            return;
        }
        await PlanRouteAsync(bank.RoomKey, _planDetails);
    }

    private async Task PlanSelectedLairAsync()
    {
        if (_lairList.SelectedItem is not MudPlayNavigationLair lair)
        {
            SetStatus("Select a lair first.");
            return;
        }
        await PlanRouteAsync(lair.EntryRoomKey, _planDetails);
    }

    private async Task PlanNearestLairAsync()
    {
        CapturePreferences();
        SetStatus("Searching for the nearest safe matching lair entry...");
        var filter = _lairSearch.Text.Trim();
        var plan = await Task.Run(() => _navigation.PlanNearestLair(Session, _profile.Preferences, filter));
        _currentPlan = plan;
        _planDetails.Text = DescribePlan(plan);
        SetStatus(plan.Success ? $"Nearest lair route planned: {plan.Summary}." : plan.Summary);
    }

    private async Task AutoRoamSelectedLairAsync()
    {
        if (_lairList.SelectedItem is not MudPlayNavigationLair lair)
        {
            SetStatus("Select a lair first.");
            return;
        }
        CapturePreferences();
        SetStatus($"Generating a closed safe loop inside lair {lair.LairId}...");
        var plan = await Task.Run(() => _navigation.BuildSafeLoop(Session, _profile.Preferences, lair.LairId));
        _currentLoop = plan;
        _loopDetails.Text = DescribePlan(plan);
        if (!plan.Success)
        {
            SetStatus(plan.Summary);
            return;
        }
        await RunPlanAsync(plan, autoRepeat: true);
    }

    private async Task GenerateLoopAsync()
    {
        CapturePreferences();
        SetStatus("Generating a closed hazard-aware loop from the current room...");
        var plan = await Task.Run(() => _navigation.BuildSafeLoop(Session, _profile.Preferences));
        _currentLoop = plan;
        _loopDetails.Text = DescribePlan(plan);
        SetStatus(plan.Success ? $"Loop generated: {plan.Summary}." : plan.Summary);
    }

    private async Task PlanRecoveryAsync()
    {
        var destination = MudPlayNavigationService.NormalizeRoomKey(Session.RouteDestinationKey);
        if (destination.Length == 0)
        {
            _recoveryDetails.Text = "The current/previous route does not expose a destination key.";
            SetStatus("No route destination is available for recovery.");
            return;
        }
        CapturePreferences();
        SetStatus($"Planning hazard-aware recovery to {destination}...");
        var plan = await Task.Run(() => _navigation.PlanRoute(Session, destination, _profile.Preferences));
        _currentPlan = plan;
        _recoveryDetails.Text = DescribePlan(plan);
        SetStatus(plan.Success ? $"Recovery planned: {plan.Summary}." : plan.Summary);
    }

    private string DescribePlan(MudPlayNavigationPlan plan)
    {
        var builder = new StringBuilder();
        builder.AppendLine(plan.Success ? "PLAN READY" : "NO PLAN");
        builder.AppendLine($"From: {plan.StartRoomKey}");
        builder.AppendLine($"To:   {plan.DestinationName} [{plan.DestinationRoomKey}]");
        builder.AppendLine(plan.Summary);
        if (plan.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Warnings / scored hazards:");
            foreach (var warning in plan.Warnings.Take(50)) builder.AppendLine("• " + warning);
        }
        if (plan.Route is { } route && route.Steps.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Commands:");
            foreach (var step in route.Steps.Take(120))
                builder.AppendLine($"{step.Number,3}. {step.Command}  →  {(step.IsMovement ? step.ExpectedNativeRoomKey : "action")}");
            if (route.Steps.Count > 120) builder.AppendLine($"... {route.Steps.Count - 120:N0} more step(s)");
        }
        return builder.ToString().TrimEnd();
    }

    private void ApplyPreferencesToControls()
    {
        var p = _profile.Preferences;
        _avoidDamageTraps.IsChecked = p.AvoidDamageTraps;
        _avoidSpellTraps.IsChecked = p.AvoidSpellTraps;
        _avoidRestricted.IsChecked = p.AvoidRestrictedExits;
        _avoidHidden.IsChecked = p.AvoidHiddenExits;
        _allowDoors.IsChecked = p.AllowOrdinaryDoors;
        _avoidHighNpcs.IsChecked = p.AvoidHighLevelNpcRooms;
        _avoidRoomEffects.IsChecked = p.AvoidRoomSpellEffects;
        _allowRiskyFallback.IsChecked = p.AllowRiskyFallback;
        _npcBuffer.Text = p.NpcLevelBuffer.ToString();
        _loopMin.Text = p.LoopMinimumSteps.ToString();
        _loopMax.Text = p.LoopMaximumSteps.ToString();
    }

    private void CapturePreferences()
    {
        var p = _profile.Preferences;
        p.AvoidDamageTraps = _avoidDamageTraps.IsChecked == true;
        p.AvoidSpellTraps = _avoidSpellTraps.IsChecked == true;
        p.AvoidRestrictedExits = _avoidRestricted.IsChecked == true;
        p.AvoidHiddenExits = _avoidHidden.IsChecked == true;
        p.AllowOrdinaryDoors = _allowDoors.IsChecked == true;
        p.AvoidHighLevelNpcRooms = _avoidHighNpcs.IsChecked == true;
        p.AvoidRoomSpellEffects = _avoidRoomEffects.IsChecked == true;
        p.AllowRiskyFallback = _allowRiskyFallback.IsChecked == true;
        if (int.TryParse(_npcBuffer.Text, out var buffer)) p.NpcLevelBuffer = Math.Clamp(buffer, 0, 99);
        if (int.TryParse(_loopMin.Text, out var min)) p.LoopMinimumSteps = Math.Clamp(min, 2, 24);
        if (int.TryParse(_loopMax.Text, out var max)) p.LoopMaximumSteps = Math.Clamp(max, p.LoopMinimumSteps, 32);
    }

    private void TrySaveProfile()
    {
        try
        {
            CapturePreferences();
            _navigation.SaveProfile(Session, _profile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Navigation preferences could not be saved: {exception.Message}");
        }
    }

    private void RefreshLiveStatus()
    {
        var key = MudPlayNavigationService.NormalizeRoomKey(Session.CurrentRoomKey);
        _roomText.Text = $"Current: {Session.CurrentRoomName} [{key}] • level {Session.CharacterLevel}";
        _routeStateText.Text = $"Route: {Session.RouteState} • {Session.RouteProgress} • recovery {(Session.RouteRecoveryAvailable ? "available" : "not required")}";
        Title = $"Navigation Assistant — {DisplayCharacter(Session)} — {Session.ServerProfileName} / {Session.WorldName}";
    }

    private void SessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClientSessionConnection.ServerCatalogWorld) or nameof(ClientSessionConnection.WorldName) or nameof(ClientSessionConnection.ServerProfileName))
            _navigation.Invalidate();
    }

    private void SetStatus(string text) => _statusText.Text = text;

    private static string DisplayCharacter(ClientSessionConnection session) =>
        string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;

    private static Button NewButton(string text, double width) => new()
    {
        Content = text,
        Width = width,
        Height = 27,
        Margin = new Thickness(0, 0, 7, 0),
        Padding = new Thickness(5, 1, 5, 1)
    };

    private static TextBlock NewText(string text, double size, FontWeight weight, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? new Thickness(0)
    };

    private static TextBox NewTextBox() => new()
    {
        Height = 26,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static TextBox NewSmallTextBox(double width) => new()
    {
        Width = width,
        Height = 24,
        Margin = new Thickness(0, 0, 8, 5),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static TextBox NewReadOnlyTextBox() => new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas"),
        Background = Brushes.White,
        Foreground = Brushes.Black
    };

    private static ListBox NewListBox() => new()
    {
        Background = Brushes.White,
        Foreground = Brushes.Black,
        BorderBrush = SystemColors.ActiveBorderBrush,
        BorderThickness = new Thickness(1)
    };

    private static CheckBox NewCheck(string text) => new() { Content = text, VerticalAlignment = VerticalAlignment.Center };

    private sealed record NavigationLoadResult(
        IReadOnlyList<MudPlayNavigationRoom> Rooms,
        IReadOnlyList<MudPlayNavigationLair> Lairs,
        IReadOnlyList<MudPlayNavigationBankLocation> Banks,
        MudPlayGameDataContext Context);
}
