using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RealmGate.App.Models;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

/// <summary>
/// Reforged combat-decision configuration. The layout and terminology are Reforged-owned;
/// it edits the per-character planner introduced by CodeUpdate0378.
/// </summary>
internal sealed class RealmGateCombatAutomationPane : UserControl
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly ReforgedCombatAutomationProfile _profile;
    private readonly DispatcherTimer _timer;

    private readonly TextBlock _context = Text(string.Empty, FontWeights.SemiBold);
    private readonly TextBlock _live = Text(string.Empty, FontWeights.Normal);
    private readonly TextBlock _status = Text("Ready.", FontWeights.Normal);

    private readonly CheckBox _enabled = Check("Enable advanced RealmGate combat decision engine");
    private readonly ComboBox _targetMode = Combo();
    private readonly TextBox _priorityTargets = Box();
    private readonly TextBox _avoidTargets = Box();
    private readonly CheckBox _stopPlayers = Check("Do not initiate combat while another player is in the room");
    private readonly TextBox _maxHostiles = Small();
    private readonly TextBox _maxXp = Box();
    private readonly TextBox _maxLevelDelta = Small();
    private readonly TextBox _stateAge = Small();
    private readonly TextBox _manaReserve = Small();
    private readonly CheckBox _requireLearned = Check("Require cast commands to resolve to a learned spell");

    private readonly CheckBox _preAttack = Check("Run pre-attack command once per selected target");
    private readonly TextBox _preAttackCommand = Box();
    private readonly TextBox _engageCommand = Box();
    private readonly CheckBox _retarget = Check("Automatically select another hostile after target loss/combat end");
    private readonly TextBox _engageCooldown = Small();
    private readonly TextBox _roundDelay = Small();

    private readonly ComboBox _defaultSet = Combo();
    private readonly ComboBox _multiSet = Combo();
    private readonly TextBox _multiAt = Small();
    private readonly ComboBox _defensiveSet = Combo();
    private readonly TextBox _defensiveSetAt = Small();
    private readonly CheckBox _defensiveAction = Check("Use defensive command at low health");
    private readonly TextBox _defensiveActionAt = Small();
    private readonly TextBox _defensiveCommand = Box();
    private readonly TextBox _defensiveCooldown = Small();

    private readonly CheckBox _autoEscape = Check("Allow automatic escape");
    private readonly TextBox _escapeHp = Small();
    private readonly CheckBox _escapeManaEnabled = Check("Escape on critically low mana/Kai");
    private readonly TextBox _escapeMana = Small();
    private readonly CheckBox _escapeOverwhelmed = Check("Escape when hostile count reaches threshold");
    private readonly TextBox _escapeCount = Small();
    private readonly TextBox _escapeCommand = Box();
    private readonly TextBox _escapeCooldown = Small();
    private readonly CheckBox _stopFailure = Check("Pause the combat planner after an observed action failure");
    private readonly TextBox _failureHold = Small();

    private readonly ListBox _actions = new() { MinHeight = 250, DisplayMemberPath = nameof(ReforgedCombatActionRule.DisplayName) };
    private readonly CheckBox _actionEnabled = Check("Enabled");
    private readonly CheckBox _actionOpening = Check("Opening action (before engage)");
    private readonly CheckBox _actionInCombat = Check("Require active combat");
    private readonly CheckBox _actionArea = Check("Area action");
    private readonly CheckBox _actionAppendTarget = Check("Append/replace {target}");
    private readonly TextBox _actionName = Box();
    private readonly TextBox _actionCommand = Box();
    private readonly TextBox _actionPriority = Small();
    private readonly TextBox _actionMinHp = Small();
    private readonly TextBox _actionMinMana = Small();
    private readonly TextBox _actionMinTargets = Small();
    private readonly TextBox _actionMaxTargets = Small();
    private readonly TextBox _actionCooldown = Small();
    private readonly TextBox _actionTargetWords = Box();
    private readonly ComboBox _actionSet = Combo();
    private readonly TextBox _actionFallback = Box();

    public RealmGateCombatAutomationPane(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _profile = _host.GetAdvancedCombatAutomationProfile(session);
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;
        Content = BuildContent();

        _targetMode.ItemsSource = Enum.GetValues<ReforgedCombatTargetMode>();
        PopulateEquipmentSets();
        LoadControls();
        RefreshActions();
        RefreshLive();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(750), DispatcherPriority.Background, (_, _) => RefreshLive(), Dispatcher);
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            CaptureControls();
            _host.SaveAdvancedCombatAutomationProfile(Session, _profile);
        };
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var stack = new StackPanel();
        stack.Children.Add(_context);
        stack.Children.Add(_live);
        header.Children.Add(stack);
        var refresh = Button("Refresh Combat State", 135);
        refresh.Click += async (_, _) =>
        {
            await _host.RefreshAdvancedCombatStateAsync(Session);
            _status.Text = "Combat state refresh requested safely for this connection.";
        };
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(_status);
        var evaluate = Button("Evaluate Now", 110);
        evaluate.Click += async (_, _) =>
        {
            CaptureControls();
            _host.SaveAdvancedCombatAutomationProfile(Session, _profile);
            var plan = await _host.RunAdvancedCombatAutomationNowAsync(Session);
            _status.Text = plan.Summary;
        };
        Grid.SetColumn(evaluate, 1);
        footer.Children.Add(evaluate);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Targeting / Engagement", Content = BuildTargetingTab() });
        tabs.Items.Add(new TabItem { Header = "Combat Actions", Content = BuildActionsTab() });
        tabs.Items.Add(new TabItem { Header = "Equipment / Safety", Content = BuildSafetyTab() });
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildTargetingTab()
    {
        var p = FormPanel();
        p.Children.Add(_enabled);
        p.Children.Add(Note("When enabled, this planner takes over the combat-decision lane for this character. Existing terminal parsing, server-authoritative room state, command isolation, and Emergency Stop remain in control."));
        p.Children.Add(Row("Target selection", _targetMode, "State max age (s)", _stateAge));
        p.Children.Add(Row("Priority target words", _priorityTargets, "Avoid target words", _avoidTargets));
        p.Children.Add(_stopPlayers);
        p.Children.Add(Row("Maximum hostiles", _maxHostiles, "Maximum room XP", _maxXp));
        p.Children.Add(Row("Max target levels above you", _maxLevelDelta, "Mana reserve %", _manaReserve));
        p.Children.Add(_requireLearned);
        p.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 8) });
        p.Children.Add(Text("Engagement sequence", FontWeights.SemiBold));
        p.Children.Add(_preAttack);
        p.Children.Add(Row("Pre-attack command", _preAttackCommand, "Engage command", _engageCommand));
        p.Children.Add(_retarget);
        p.Children.Add(Row("Engage cooldown (s)", _engageCooldown, "Round minimum delay (s)", _roundDelay));
        p.Children.Add(Note("Use {target} explicitly when you want precise placement. Otherwise RealmGate appends the selected NPC name. Targets come only from observed hostile NPC state."));
        return Scroll(p);
    }

    private UIElement BuildActionsTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.35, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.65, GridUnitType.Star) });

        var left = new DockPanel();
        var buttons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        var add = Button("New Action", 90);
        var remove = Button("Delete", 70);
        add.Click += (_, _) => AddAction();
        remove.Click += (_, _) => DeleteAction();
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);
        _actions.SelectionChanged += (_, _) => LoadSelectedAction();
        left.Children.Add(_actions);
        root.Children.Add(left);

        var right = FormPanel();
        right.Children.Add(_actionEnabled);
        right.Children.Add(Row("Name", _actionName, "Priority", _actionPriority));
        right.Children.Add(Row("Command", _actionCommand, "Cooldown (s)", _actionCooldown));
        right.Children.Add(new WrapPanel { Children = { _actionOpening, _actionInCombat, _actionArea, _actionAppendTarget } });
        right.Children.Add(Row("Minimum HP %", _actionMinHp, "Minimum mana %", _actionMinMana));
        right.Children.Add(Row("Minimum targets", _actionMinTargets, "Maximum targets", _actionMaxTargets));
        right.Children.Add(Row("Target name contains", _actionTargetWords, "Equipment set", _actionSet));
        right.Children.Add(Row("Fallback command", _actionFallback, string.Empty, new TextBlock()));
        var save = Button("Save Action", 95);
        save.Margin = new Thickness(0, 8, 0, 0);
        save.Click += (_, _) => SaveSelectedAction();
        right.Children.Add(save);
        right.Children.Add(Note("Cast actions are skipped unless the spell is learned and affordable above the mana reserve (unless you explicitly disable that safety). Failed actions are temporarily held to prevent command thrash."));
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        return root;
    }

    private UIElement BuildSafetyTab()
    {
        var p = FormPanel();
        p.Children.Add(Text("Equipment strategy", FontWeights.SemiBold));
        p.Children.Add(Row("Default combat set", _defaultSet, "Multi-target set", _multiSet));
        p.Children.Add(Row("Use multi-target set at count", _multiAt, "Defensive set", _defensiveSet));
        p.Children.Add(Row("Defensive set at HP %", _defensiveSetAt, string.Empty, new TextBlock()));
        p.Children.Add(Note("Equipment sets reuse the 0376 inventory/equipment planner. The combat engine does not issue blind wear/equip commands."));
        p.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 8) });
        p.Children.Add(_defensiveAction);
        p.Children.Add(Row("Defensive action at HP %", _defensiveActionAt, "Command", _defensiveCommand));
        p.Children.Add(Row("Defensive cooldown (s)", _defensiveCooldown, string.Empty, new TextBlock()));
        p.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 8) });
        p.Children.Add(Text("Escape policy", FontWeights.SemiBold));
        p.Children.Add(_autoEscape);
        p.Children.Add(Row("Escape at HP %", _escapeHp, "Escape command", _escapeCommand));
        p.Children.Add(_escapeManaEnabled);
        p.Children.Add(Row("Escape at mana/Kai %", _escapeMana, string.Empty, new TextBlock()));
        p.Children.Add(_escapeOverwhelmed);
        p.Children.Add(Row("Escape at hostile count", _escapeCount, "Escape cooldown (s)", _escapeCooldown));
        p.Children.Add(_stopFailure);
        p.Children.Add(Row("Failure hold (s)", _failureHold, string.Empty, new TextBlock()));
        return Scroll(p);
    }

    private void PopulateEquipmentSets()
    {
        var sets = _host.GetInventoryAutomationProfile(Session).EquipmentSets.Where(s => s.Enabled).ToArray();
        foreach (var combo in new[] { _defaultSet, _multiSet, _defensiveSet, _actionSet })
        {
            combo.Items.Clear();
            combo.Items.Add(new SetChoice(string.Empty, "(none)"));
            foreach (var set in sets) combo.Items.Add(new SetChoice(set.Id, set.Name));
            combo.DisplayMemberPath = nameof(SetChoice.Name);
            combo.SelectedValuePath = nameof(SetChoice.Id);
        }
    }

    private void LoadControls()
    {
        var p = _profile.Preferences;
        _enabled.IsChecked = p.Enabled;
        _targetMode.SelectedItem = p.TargetMode;
        _priorityTargets.Text = p.PriorityTargetWords;
        _avoidTargets.Text = p.AvoidTargetWords;
        _stopPlayers.IsChecked = p.StopWhenPlayersPresent;
        _maxHostiles.Text = p.MaximumHostiles.ToString();
        _maxXp.Text = p.MaximumTotalExperience.ToString();
        _maxLevelDelta.Text = p.MaximumTargetLevelAboveCharacter.ToString();
        _stateAge.Text = p.StateMaximumAgeSeconds.ToString();
        _manaReserve.Text = p.ManaReservePercent.ToString();
        _requireLearned.IsChecked = p.RequireLearnedSpellForCastCommands;
        _preAttack.IsChecked = p.AutoPreAttack;
        _preAttackCommand.Text = p.PreAttackCommand;
        _engageCommand.Text = p.EngageCommand;
        _retarget.IsChecked = p.AutoRetarget;
        _engageCooldown.Text = p.EngagementCooldownSeconds.ToString();
        _roundDelay.Text = p.RoundMinimumDelaySeconds.ToString();
        SelectSet(_defaultSet, p.DefaultEquipmentSetId);
        SelectSet(_multiSet, p.MultiTargetEquipmentSetId);
        _multiAt.Text = p.MultiTargetEquipmentSetAtCount.ToString();
        SelectSet(_defensiveSet, p.DefensiveEquipmentSetId);
        _defensiveSetAt.Text = p.DefensiveEquipmentSetAtHealthPercent.ToString();
        _defensiveAction.IsChecked = p.AutoDefensiveAction;
        _defensiveActionAt.Text = p.DefensiveActionAtHealthPercent.ToString();
        _defensiveCommand.Text = p.DefensiveActionCommand;
        _defensiveCooldown.Text = p.DefensiveActionCooldownSeconds.ToString();
        _autoEscape.IsChecked = p.AutoEscape;
        _escapeHp.Text = p.EscapeAtHealthPercent.ToString();
        _escapeManaEnabled.IsChecked = p.EscapeAtLowMana;
        _escapeMana.Text = p.EscapeAtManaPercent.ToString();
        _escapeOverwhelmed.IsChecked = p.EscapeWhenOverwhelmed;
        _escapeCount.Text = p.EscapeAtHostileCount.ToString();
        _escapeCommand.Text = p.EscapeCommand;
        _escapeCooldown.Text = p.EscapeCooldownSeconds.ToString();
        _stopFailure.IsChecked = p.StopAfterActionFailure;
        _failureHold.Text = p.FailedActionHoldSeconds.ToString();
    }

    private void CaptureControls()
    {
        var p = _profile.Preferences;
        p.Enabled = _enabled.IsChecked == true;
        p.TargetMode = _targetMode.SelectedItem is ReforgedCombatTargetMode mode ? mode : ReforgedCombatTargetMode.FirstObserved;
        p.PriorityTargetWords = _priorityTargets.Text.Trim();
        p.AvoidTargetWords = _avoidTargets.Text.Trim();
        p.StopWhenPlayersPresent = _stopPlayers.IsChecked == true;
        p.MaximumHostiles = Int(_maxHostiles, p.MaximumHostiles, 1, 99);
        p.MaximumTotalExperience = Long(_maxXp, p.MaximumTotalExperience, 0, long.MaxValue);
        p.MaximumTargetLevelAboveCharacter = Int(_maxLevelDelta, p.MaximumTargetLevelAboveCharacter, 0, 999);
        p.StateMaximumAgeSeconds = Int(_stateAge, p.StateMaximumAgeSeconds, 1, 120);
        p.ManaReservePercent = Int(_manaReserve, p.ManaReservePercent, 0, 100);
        p.RequireLearnedSpellForCastCommands = _requireLearned.IsChecked == true;
        p.AutoPreAttack = _preAttack.IsChecked == true;
        p.PreAttackCommand = _preAttackCommand.Text.Trim();
        p.EngageCommand = _engageCommand.Text.Trim();
        p.AutoRetarget = _retarget.IsChecked == true;
        p.EngagementCooldownSeconds = Int(_engageCooldown, p.EngagementCooldownSeconds, 1, 60);
        p.RoundMinimumDelaySeconds = Int(_roundDelay, p.RoundMinimumDelaySeconds, 1, 30);
        p.DefaultEquipmentSetId = SelectedSet(_defaultSet);
        p.MultiTargetEquipmentSetId = SelectedSet(_multiSet);
        p.MultiTargetEquipmentSetAtCount = Int(_multiAt, p.MultiTargetEquipmentSetAtCount, 2, 99);
        p.DefensiveEquipmentSetId = SelectedSet(_defensiveSet);
        p.DefensiveEquipmentSetAtHealthPercent = Int(_defensiveSetAt, p.DefensiveEquipmentSetAtHealthPercent, 0, 100);
        p.AutoDefensiveAction = _defensiveAction.IsChecked == true;
        p.DefensiveActionAtHealthPercent = Int(_defensiveActionAt, p.DefensiveActionAtHealthPercent, 0, 100);
        p.DefensiveActionCommand = _defensiveCommand.Text.Trim();
        p.DefensiveActionCooldownSeconds = Int(_defensiveCooldown, p.DefensiveActionCooldownSeconds, 1, 300);
        p.AutoEscape = _autoEscape.IsChecked == true;
        p.EscapeAtHealthPercent = Int(_escapeHp, p.EscapeAtHealthPercent, 0, 100);
        p.EscapeAtLowMana = _escapeManaEnabled.IsChecked == true;
        p.EscapeAtManaPercent = Int(_escapeMana, p.EscapeAtManaPercent, 0, 100);
        p.EscapeWhenOverwhelmed = _escapeOverwhelmed.IsChecked == true;
        p.EscapeAtHostileCount = Int(_escapeCount, p.EscapeAtHostileCount, 1, 99);
        p.EscapeCommand = _escapeCommand.Text.Trim();
        p.EscapeCooldownSeconds = Int(_escapeCooldown, p.EscapeCooldownSeconds, 1, 300);
        p.StopAfterActionFailure = _stopFailure.IsChecked == true;
        p.FailedActionHoldSeconds = Int(_failureHold, p.FailedActionHoldSeconds, 1, 300);
    }

    private void RefreshActions()
    {
        var selected = _actions.SelectedItem as ReforgedCombatActionRule;
        _actions.ItemsSource = null;
        _actions.ItemsSource = _profile.Actions.OrderBy(a => a.Priority).ThenBy(a => a.Name).ToArray();
        _actions.SelectedItem = selected ?? _profile.Actions.OrderBy(a => a.Priority).FirstOrDefault();
    }

    private void AddAction()
    {
        var rule = new ReforgedCombatActionRule { Name = "New combat action", Priority = (_profile.Actions.Count + 1) * 10, Command = "bash {target}" };
        _profile.Actions.Add(rule);
        RefreshActions();
        _actions.SelectedItem = rule;
    }

    private void DeleteAction()
    {
        if (_actions.SelectedItem is not ReforgedCombatActionRule rule) return;
        _profile.Actions.Remove(rule);
        RefreshActions();
    }

    private void LoadSelectedAction()
    {
        if (_actions.SelectedItem is not ReforgedCombatActionRule a) return;
        _actionEnabled.IsChecked = a.Enabled;
        _actionOpening.IsChecked = a.OpeningOnly;
        _actionInCombat.IsChecked = a.InCombatOnly;
        _actionArea.IsChecked = a.IsAreaAction;
        _actionAppendTarget.IsChecked = a.AppendTarget;
        _actionName.Text = a.Name;
        _actionCommand.Text = a.Command;
        _actionPriority.Text = a.Priority.ToString();
        _actionMinHp.Text = a.MinimumHealthPercent.ToString();
        _actionMinMana.Text = a.MinimumManaPercent.ToString();
        _actionMinTargets.Text = a.MinimumTargets.ToString();
        _actionMaxTargets.Text = a.MaximumTargets.ToString();
        _actionCooldown.Text = a.CooldownSeconds.ToString();
        _actionTargetWords.Text = a.TargetNameContains;
        _actionFallback.Text = a.FallbackCommand;
        SelectSet(_actionSet, a.EquipmentSetId);
    }

    private void SaveSelectedAction()
    {
        if (_actions.SelectedItem is not ReforgedCombatActionRule a) return;
        a.Enabled = _actionEnabled.IsChecked == true;
        a.OpeningOnly = _actionOpening.IsChecked == true;
        a.InCombatOnly = _actionInCombat.IsChecked == true;
        a.IsAreaAction = _actionArea.IsChecked == true;
        a.AppendTarget = _actionAppendTarget.IsChecked == true;
        a.Name = string.IsNullOrWhiteSpace(_actionName.Text) ? "Combat action" : _actionName.Text.Trim();
        a.Command = _actionCommand.Text.Trim();
        a.Priority = Int(_actionPriority, a.Priority, 0, 100000);
        a.MinimumHealthPercent = Int(_actionMinHp, a.MinimumHealthPercent, 0, 100);
        a.MinimumManaPercent = Int(_actionMinMana, a.MinimumManaPercent, 0, 100);
        a.MinimumTargets = Int(_actionMinTargets, a.MinimumTargets, 1, 99);
        a.MaximumTargets = Int(_actionMaxTargets, a.MaximumTargets, a.MinimumTargets, 99);
        a.CooldownSeconds = Int(_actionCooldown, a.CooldownSeconds, 1, 300);
        a.TargetNameContains = _actionTargetWords.Text.Trim();
        a.EquipmentSetId = SelectedSet(_actionSet);
        a.FallbackCommand = _actionFallback.Text.Trim();
        RefreshActions();
        _host.SaveAdvancedCombatAutomationProfile(Session, _profile);
        _status.Text = $"Saved action '{a.Name}'.";
    }

    private void RefreshLive()
    {
        var hostiles = Session.CurrentRoomNpcDetails.Count(n => n.Alive && n.Disposition.Equals("hostile", StringComparison.OrdinalIgnoreCase));
        _context.Text = $"{Session.ServerProfileName} • {Session.WorldName} • {DisplayCharacter(Session)} • Room {Session.CurrentRoomKey}";
        _live.Text = $"HP {Session.HealthPercent}% • Mana/Kai {Session.ManaPercent}% • Hostiles {hostiles} • Combat {(Session.InCombat ? "active" : "idle")} • {Session.CombatAutomationStatus}";
    }

    private static string DisplayCharacter(ClientSessionConnection s) => string.IsNullOrWhiteSpace(s.CharacterName) ? s.DisplayName : s.CharacterName;
    private static StackPanel FormPanel() => new() { Margin = new Thickness(10) };
    private static ScrollViewer Scroll(UIElement child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private static TextBlock Text(string value, FontWeight weight) => new() { Text = value, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
    private static TextBlock Note(string value) => new() { Text = value, Opacity = 0.75, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 8) };
    private static CheckBox Check(string value) => new() { Content = value, Margin = new Thickness(0, 3, 10, 3) };
    private static TextBox Box() => new() { MinWidth = 170, Margin = new Thickness(4, 2, 8, 2) };
    private static TextBox Small() => new() { Width = 75, Margin = new Thickness(4, 2, 8, 2) };
    private static ComboBox Combo() => new() { MinWidth = 170, Margin = new Thickness(4, 2, 8, 2) };
    private static Button Button(string text, double width) => new() { Content = text, Width = width, Margin = new Thickness(4, 2, 4, 2), Padding = new Thickness(6, 3, 6, 3) };

    private static Grid Row(string l1, UIElement c1, string l2, UIElement c2)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var a = new TextBlock { Text = l1, VerticalAlignment = VerticalAlignment.Center, MinWidth = 155 };
        var b = new TextBlock { Text = l2, VerticalAlignment = VerticalAlignment.Center, MinWidth = string.IsNullOrWhiteSpace(l2) ? 0 : 155 };
        g.Children.Add(a); Grid.SetColumn(c1, 1); g.Children.Add(c1); Grid.SetColumn(b, 2); g.Children.Add(b); Grid.SetColumn(c2, 3); g.Children.Add(c2);
        return g;
    }

    private static int Int(TextBox box, int fallback, int min, int max) => int.TryParse(box.Text, out var value) ? Math.Clamp(value, min, max) : fallback;
    private static long Long(TextBox box, long fallback, long min, long max) => long.TryParse(box.Text, out var value) ? Math.Clamp(value, min, max) : fallback;
    private static string SelectedSet(ComboBox combo) => combo.SelectedItem is SetChoice choice ? choice.Id : string.Empty;
    private static void SelectSet(ComboBox combo, string id)
    {
        combo.SelectedItem = combo.Items.OfType<SetChoice>().FirstOrDefault(x => x.Id.Equals(id ?? string.Empty, StringComparison.OrdinalIgnoreCase)) ?? combo.Items.OfType<SetChoice>().FirstOrDefault();
    }
    private sealed record SetChoice(string Id, string Name);
}
