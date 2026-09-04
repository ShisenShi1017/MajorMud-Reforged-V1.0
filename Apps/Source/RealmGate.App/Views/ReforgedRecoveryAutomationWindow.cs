using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RealmGate.App.Models;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

/// <summary>
/// Reforged recovery/buff configuration window. The UI deliberately uses RealmGate's
/// own session model and terminology; it does not reproduce MudPlay's layout.
/// </summary>
internal sealed class RealmGateRecoveryAutomationPane : UserControl
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly ReforgedRecoveryAutomationProfile _profile;
    private readonly DispatcherTimer _timer;

    private readonly TextBlock _context = Text(string.Empty, FontWeights.SemiBold);
    private readonly TextBlock _live = Text(string.Empty, FontWeights.Normal);
    private readonly TextBlock _status = Text("Ready.", FontWeights.Normal);

    private readonly CheckBox _enabled = Check("Enable enhanced recovery/buff automation");
    private readonly CheckBox _healSelf = Check("Auto-heal self");
    private readonly CheckBox _healCombat = Check("Allow self-healing during combat");
    private readonly TextBox _minorAt = Small();
    private readonly TextBox _majorAt = Small();
    private readonly TextBox _minorCommand = Box();
    private readonly TextBox _majorCommand = Box();
    private readonly TextBox _reserve = Small();
    private readonly CheckBox _requireLearned = Check("Require cast commands to resolve to a learned spell");
    private readonly CheckBox _emergencyItem = Check("Use configured emergency healing item");
    private readonly TextBox _emergencyAt = Small();
    private readonly TextBox _emergencyCommand = Box();
    private readonly CheckBox _recall = Check("Emergency recall at critical health");
    private readonly TextBox _recallAt = Small();
    private readonly TextBox _recallCommand = Box();

    private readonly CheckBox _curePoison = Check("Poison");
    private readonly CheckBox _cureDisease = Check("Disease");
    private readonly CheckBox _cureBlind = Check("Blindness");
    private readonly CheckBox _cureSilence = Check("Silence (non-spell command recommended)");
    private readonly TextBox _curePoisonCommand = Box();
    private readonly TextBox _cureDiseaseCommand = Box();
    private readonly TextBox _cureBlindCommand = Box();
    private readonly TextBox _cureSilenceCommand = Box();

    private readonly CheckBox _autoRest = Check("Auto-rest for health/mana recovery");
    private readonly CheckBox _autoMeditate = Check("Prefer meditate when mana is low");
    private readonly TextBox _beginHp = Small();
    private readonly TextBox _beginMana = Small();
    private readonly TextBox _finishHp = Small();
    private readonly TextBox _finishMana = Small();
    private readonly TextBox _restCommand = Box();
    private readonly TextBox _meditateCommand = Box();
    private readonly TextBox _standCommand = Box();
    private readonly ComboBox _preSet = Combo();
    private readonly ComboBox _postSet = Combo();

    private readonly ListBox _buffs = new() { MinHeight = 190, DisplayMemberPath = nameof(ReforgedBuffRule.DisplayName) };
    private readonly CheckBox _buffEnabled = Check("Enabled");
    private readonly CheckBox _buffOutCombat = Check("Only out of combat");
    private readonly TextBox _buffName = Box();
    private readonly TextBox _buffEffect = Box();
    private readonly TextBox _buffCommand = Box();
    private readonly TextBox _buffPriority = Small();
    private readonly TextBox _buffLead = Small();
    private readonly TextBox _buffMana = Small();
    private readonly TextBox _buffWatchdog = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 12, Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(8) };

    private readonly CheckBox _partyHeal = Check("Heal lowest party member below threshold");
    private readonly TextBox _partyHealAt = Small();
    private readonly TextBox _partyHealCommand = Box();
    private readonly CheckBox _partyPoison = Check("Cure party members explicitly marked poisoned");
    private readonly TextBox _partyPoisonCommand = Box();

    private readonly TextBox _stateAge = Small();
    private readonly TextBox _partyAge = Small();
    private readonly TextBox _cooldown = Small();

    public RealmGateRecoveryAutomationPane(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _profile = _host.GetRecoveryAutomationProfile(session);
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;
        Content = BuildContent();

        PopulateEquipmentSets();
        LoadControls();
        RefreshBuffList();
        RefreshLive();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(750), DispatcherPriority.Background, (_, _) => RefreshLive(), Dispatcher);
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            CaptureControls();
            _host.SaveRecoveryAutomationProfile(Session, _profile);
        };
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        text.Children.Add(_context);
        text.Children.Add(_live);
        header.Children.Add(text);
        var refresh = Button("Refresh State", 110);
        refresh.Click += async (_, _) =>
        {
            await _host.RefreshRecoveryAutomationStateAsync(Session, includeParty: true);
            _status.Text = "State refresh requested through the active connection.";
        };
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(_status);
        var run = Button("Evaluate Now", 110);
        run.Click += async (_, _) =>
        {
            CaptureControls();
            _host.SaveRecoveryAutomationProfile(Session, _profile);
            var plan = await _host.RunRecoveryAutomationNowAsync(Session);
            _status.Text = plan.Summary;
        };
        Grid.SetColumn(run, 1);
        footer.Children.Add(run);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Healing / Safety", Content = BuildHealingTab() });
        tabs.Items.Add(new TabItem { Header = "Cures / Recovery", Content = BuildRecoveryTab() });
        tabs.Items.Add(new TabItem { Header = "Buff Maintenance", Content = BuildBuffTab() });
        tabs.Items.Add(new TabItem { Header = "Buff Watchdog", Content = BuildBuffWatchdogTab() });
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildHealingTab()
    {
        var panel = FormPanel();
        panel.Children.Add(_enabled);
        panel.Children.Add(Note("When enabled, this RealmGate planner owns the self-heal/recovery/buff lane for this character so the legacy rest/heal lane does not double-cast."));
        panel.Children.Add(_healSelf);
        panel.Children.Add(_healCombat);
        panel.Children.Add(Row("Minor heal at HP %", _minorAt, "Command", _minorCommand));
        panel.Children.Add(Row("Major heal at HP %", _majorAt, "Command", _majorCommand));
        panel.Children.Add(Row("Mana reserve %", _reserve, "State max age (s)", _stateAge));
        panel.Children.Add(Row("Command cooldown (s)", _cooldown, string.Empty, _requireLearned));
        panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 8) });
        panel.Children.Add(_emergencyItem);
        panel.Children.Add(Row("Use item at HP %", _emergencyAt, "Command", _emergencyCommand));
        panel.Children.Add(_recall);
        panel.Children.Add(Row("Recall at HP %", _recallAt, "Command", _recallCommand));
        return Scroll(panel);
    }

    private UIElement BuildRecoveryTab()
    {
        var panel = FormPanel();
        panel.Children.Add(Text("Condition cures", FontWeights.SemiBold));
        panel.Children.Add(CheckCommandRow(_curePoison, _curePoisonCommand));
        panel.Children.Add(CheckCommandRow(_cureDisease, _cureDiseaseCommand));
        panel.Children.Add(CheckCommandRow(_cureBlind, _cureBlindCommand));
        panel.Children.Add(CheckCommandRow(_cureSilence, _cureSilenceCommand));
        panel.Children.Add(Note("Cast-based cures are sent only when the spell is learned, affordable above the configured mana reserve, and the character is not silenced. A silence cure can be a non-spell command/item."));
        panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 8) });
        panel.Children.Add(Text("Rest / meditation", FontWeights.SemiBold));
        panel.Children.Add(_autoRest);
        panel.Children.Add(_autoMeditate);
        panel.Children.Add(Row("Begin below HP %", _beginHp, "Begin below mana %", _beginMana));
        panel.Children.Add(Row("Finish at HP %", _finishHp, "Finish at mana %", _finishMana));
        panel.Children.Add(Row("Rest command", _restCommand, "Meditate command", _meditateCommand));
        panel.Children.Add(Row("Stand command", _standCommand, string.Empty, new TextBlock()));
        panel.Children.Add(Row("Pre-recovery equipment set", _preSet, "Post-recovery equipment set", _postSet));
        panel.Children.Add(Note("Equipment-set hooks reuse RealmGate's 0376 equipment planner; they never bypass inventory/equipment verification."));
        return Scroll(panel);
    }

    private UIElement BuildBuffTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.38, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star) });
        var left = new DockPanel();
        var buttons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        var add = Button("New Buff", 85);
        var remove = Button("Delete", 70);
        add.Click += (_, _) => AddBuff();
        remove.Click += (_, _) => DeleteBuff();
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);
        _buffs.SelectionChanged += (_, _) => LoadSelectedBuff();
        left.Children.Add(_buffs);
        root.Children.Add(left);

        var right = FormPanel();
        right.Children.Add(_buffEnabled);
        right.Children.Add(_buffOutCombat);
        right.Children.Add(Row("Name", _buffName, "Priority", _buffPriority));
        right.Children.Add(Row("Effect match", _buffEffect, "Command", _buffCommand));
        right.Children.Add(Row("Refresh lead (s)", _buffLead, "Minimum mana %", _buffMana));
        var save = Button("Save Buff", 90);
        save.Margin = new Thickness(0, 8, 0, 0);
        save.Click += (_, _) => SaveSelectedBuff();
        right.Children.Add(save);
        right.Children.Add(Note("A buff is cast only when its authoritative effect is absent or inside the configured expiry lead window. Matching can use effect name, spell id, or effect id."));
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        return root;
    }


    private UIElement BuildBuffWatchdogTab()
    {
        var root = new DockPanel { Margin = new Thickness(10) };
        var note = Note("Live, read-only comparison of configured buff rules against authoritative active-effect state. A rule is marked DUE only when its effect is absent or inside that rule's refresh lead window; this view sends no commands.");
        DockPanel.SetDock(note, Dock.Top);
        root.Children.Add(note);
        root.Children.Add(_buffWatchdog);
        return root;
    }

    private UIElement BuildPartyTab()
    {
        var panel = FormPanel();
        panel.Children.Add(Note("This pass adds safe party-healing hooks only. Full party coordination remains the next dedicated party pass."));
        panel.Children.Add(_partyHeal);
        panel.Children.Add(Row("Heal below HP %", _partyHealAt, "Command template", _partyHealCommand));
        panel.Children.Add(_partyPoison);
        panel.Children.Add(Row("Poison cure template", _partyPoisonCommand, "Party max age (s)", _partyAge));
        panel.Children.Add(Note("Use {target} in party command templates. RealmGate chooses only a member present in the captured party state; it does not invent names or infer ailments other than the explicit poisoned flag."));
        return Scroll(panel);
    }

    private void LoadControls()
    {
        var p = _profile.Preferences;
        _enabled.IsChecked = p.Enabled;
        _healSelf.IsChecked = p.AutoHealSelf;
        _healCombat.IsChecked = p.AllowSelfHealingInCombat;
        _minorAt.Text = p.MinorHealAtPercent.ToString();
        _majorAt.Text = p.MajorHealAtPercent.ToString();
        _minorCommand.Text = p.MinorHealCommand;
        _majorCommand.Text = p.MajorHealCommand;
        _reserve.Text = p.ReserveManaPercent.ToString();
        _requireLearned.IsChecked = p.RequireLearnedSpellForCastCommands;
        _emergencyItem.IsChecked = p.AutoUseEmergencyHealingItem;
        _emergencyAt.Text = p.EmergencyHealingItemAtPercent.ToString();
        _emergencyCommand.Text = p.EmergencyHealingItemCommand;
        _recall.IsChecked = p.AutoRecallAtCriticalHealth;
        _recallAt.Text = p.RecallAtPercent.ToString();
        _recallCommand.Text = p.RecallCommand;
        _curePoison.IsChecked = p.CurePoison;
        _cureDisease.IsChecked = p.CureDisease;
        _cureBlind.IsChecked = p.CureBlindness;
        _cureSilence.IsChecked = p.CureSilence;
        _curePoisonCommand.Text = p.CurePoisonCommand;
        _cureDiseaseCommand.Text = p.CureDiseaseCommand;
        _cureBlindCommand.Text = p.CureBlindnessCommand;
        _cureSilenceCommand.Text = p.CureSilenceCommand;
        _autoRest.IsChecked = p.AutoRest;
        _autoMeditate.IsChecked = p.AutoMeditate;
        _beginHp.Text = p.BeginRecoveryHealthPercent.ToString();
        _beginMana.Text = p.BeginRecoveryManaPercent.ToString();
        _finishHp.Text = p.FinishRecoveryHealthPercent.ToString();
        _finishMana.Text = p.FinishRecoveryManaPercent.ToString();
        _restCommand.Text = p.RestCommand;
        _meditateCommand.Text = p.MeditateCommand;
        _standCommand.Text = p.StandCommand;
        SelectSet(_preSet, p.PreRecoveryEquipmentSetId);
        SelectSet(_postSet, p.PostRecoveryEquipmentSetId);
        _partyHeal.IsChecked = p.AutoHealParty;
        _partyHealAt.Text = p.HealPartyBelowPercent.ToString();
        _partyHealCommand.Text = p.HealPartyCommandTemplate;
        _partyPoison.IsChecked = p.CurePoisonedPartyMembers;
        _partyPoisonCommand.Text = p.CurePartyPoisonCommandTemplate;
        _stateAge.Text = p.StateMaximumAgeSeconds.ToString();
        _partyAge.Text = p.PartyStateMaximumAgeSeconds.ToString();
        _cooldown.Text = p.CommandCooldownSeconds.ToString();
    }

    private void CaptureControls()
    {
        SaveSelectedBuff();
        var p = _profile.Preferences;
        p.Enabled = _enabled.IsChecked == true;
        p.AutoHealSelf = _healSelf.IsChecked == true;
        p.AllowSelfHealingInCombat = _healCombat.IsChecked == true;
        p.MinorHealAtPercent = Int(_minorAt, p.MinorHealAtPercent);
        p.MajorHealAtPercent = Int(_majorAt, p.MajorHealAtPercent);
        p.MinorHealCommand = _minorCommand.Text.Trim();
        p.MajorHealCommand = _majorCommand.Text.Trim();
        p.ReserveManaPercent = Int(_reserve, p.ReserveManaPercent);
        p.RequireLearnedSpellForCastCommands = _requireLearned.IsChecked == true;
        p.AutoUseEmergencyHealingItem = _emergencyItem.IsChecked == true;
        p.EmergencyHealingItemAtPercent = Int(_emergencyAt, p.EmergencyHealingItemAtPercent);
        p.EmergencyHealingItemCommand = _emergencyCommand.Text.Trim();
        p.AutoRecallAtCriticalHealth = _recall.IsChecked == true;
        p.RecallAtPercent = Int(_recallAt, p.RecallAtPercent);
        p.RecallCommand = _recallCommand.Text.Trim();
        p.CurePoison = _curePoison.IsChecked == true;
        p.CureDisease = _cureDisease.IsChecked == true;
        p.CureBlindness = _cureBlind.IsChecked == true;
        p.CureSilence = _cureSilence.IsChecked == true;
        p.CurePoisonCommand = _curePoisonCommand.Text.Trim();
        p.CureDiseaseCommand = _cureDiseaseCommand.Text.Trim();
        p.CureBlindnessCommand = _cureBlindCommand.Text.Trim();
        p.CureSilenceCommand = _cureSilenceCommand.Text.Trim();
        p.AutoRest = _autoRest.IsChecked == true;
        p.AutoMeditate = _autoMeditate.IsChecked == true;
        p.BeginRecoveryHealthPercent = Int(_beginHp, p.BeginRecoveryHealthPercent);
        p.BeginRecoveryManaPercent = Int(_beginMana, p.BeginRecoveryManaPercent);
        p.FinishRecoveryHealthPercent = Int(_finishHp, p.FinishRecoveryHealthPercent);
        p.FinishRecoveryManaPercent = Int(_finishMana, p.FinishRecoveryManaPercent);
        p.RestCommand = _restCommand.Text.Trim();
        p.MeditateCommand = _meditateCommand.Text.Trim();
        p.StandCommand = _standCommand.Text.Trim();
        p.PreRecoveryEquipmentSetId = SelectedSetId(_preSet);
        p.PostRecoveryEquipmentSetId = SelectedSetId(_postSet);
        p.AutoHealParty = _partyHeal.IsChecked == true;
        p.HealPartyBelowPercent = Int(_partyHealAt, p.HealPartyBelowPercent);
        p.HealPartyCommandTemplate = _partyHealCommand.Text.Trim();
        p.CurePoisonedPartyMembers = _partyPoison.IsChecked == true;
        p.CurePartyPoisonCommandTemplate = _partyPoisonCommand.Text.Trim();
        p.StateMaximumAgeSeconds = Int(_stateAge, p.StateMaximumAgeSeconds);
        p.PartyStateMaximumAgeSeconds = Int(_partyAge, p.PartyStateMaximumAgeSeconds);
        p.CommandCooldownSeconds = Int(_cooldown, p.CommandCooldownSeconds);
    }

    private void PopulateEquipmentSets()
    {
        var sets = _host.GetInventoryAutomationProfile(Session).EquipmentSets.ToArray();
        var values = new List<SetChoice> { new(string.Empty, "(no equipment-set hook)") };
        values.AddRange(sets.Select(set => new SetChoice(set.Id, set.Name)));
        _preSet.ItemsSource = values;
        _postSet.ItemsSource = values.ToArray();
        _preSet.DisplayMemberPath = nameof(SetChoice.Name);
        _postSet.DisplayMemberPath = nameof(SetChoice.Name);
    }

    private void RefreshBuffList()
    {
        var selected = (_buffs.SelectedItem as ReforgedBuffRule)?.Id;
        _buffs.ItemsSource = null;
        _buffs.ItemsSource = _profile.BuffRules.OrderBy(rule => rule.Priority).ThenBy(rule => rule.Name).ToArray();
        if (selected is not null)
            _buffs.SelectedItem = _profile.BuffRules.FirstOrDefault(rule => rule.Id == selected);
        if (_buffs.SelectedItem is null && _profile.BuffRules.Count > 0) _buffs.SelectedIndex = 0;
    }

    private void AddBuff()
    {
        var rule = new ReforgedBuffRule { Name = "New Buff", Priority = (_profile.BuffRules.Count + 1) * 100, Enabled = false };
        _profile.BuffRules.Add(rule);
        RefreshBuffList();
        _buffs.SelectedItem = rule;
    }

    private void DeleteBuff()
    {
        if (_buffs.SelectedItem is not ReforgedBuffRule rule) return;
        _profile.BuffRules.Remove(rule);
        RefreshBuffList();
    }

    private void LoadSelectedBuff()
    {
        if (_buffs.SelectedItem is not ReforgedBuffRule rule) return;
        _buffEnabled.IsChecked = rule.Enabled;
        _buffOutCombat.IsChecked = rule.OnlyOutOfCombat;
        _buffName.Text = rule.Name;
        _buffEffect.Text = rule.EffectMatch;
        _buffCommand.Text = rule.Command;
        _buffPriority.Text = rule.Priority.ToString();
        _buffLead.Text = rule.RefreshLeadSeconds.ToString();
        _buffMana.Text = rule.MinimumManaPercent.ToString();
    }

    private void SaveSelectedBuff()
    {
        if (_buffs.SelectedItem is not ReforgedBuffRule rule) return;
        rule.Enabled = _buffEnabled.IsChecked == true;
        rule.OnlyOutOfCombat = _buffOutCombat.IsChecked == true;
        rule.Name = string.IsNullOrWhiteSpace(_buffName.Text) ? "Buff" : _buffName.Text.Trim();
        rule.EffectMatch = _buffEffect.Text.Trim();
        rule.Command = _buffCommand.Text.Trim();
        rule.Priority = Int(_buffPriority, rule.Priority);
        rule.RefreshLeadSeconds = Int(_buffLead, rule.RefreshLeadSeconds);
        rule.MinimumManaPercent = Int(_buffMana, rule.MinimumManaPercent);
        RefreshBuffList();
    }

    private void RefreshLive()
    {
        _context.Text = $"{DisplayCharacter(Session)} • {Session.ServerProfileName} • {Session.WorldName}";
        _live.Text = $"HP {Session.HealthPercent}% • Mana/Kai {Session.ManaPercent}% • " +
                     $"{(Session.InCombat ? "Combat" : Session.IsResting ? "Recovering" : "Ready")} • " +
                     $"Effects {Session.ActiveEffectDetails.Count} • Party {Session.PartyMembers.Count}";
        RefreshBuffWatchdog();
    }


    private void RefreshBuffWatchdog()
    {
        var now = DateTimeOffset.UtcNow;
        var lines = new List<string>
        {
            $"Captured effects: {Session.ActiveEffectDetails.Count:N0}",
            $"Configured buff rules: {_profile.BuffRules.Count:N0}",
            string.Empty,
            "State     Buff                         Effect match                  Expires / reason",
            "--------  ---------------------------  ----------------------------  ------------------------------"
        };

        foreach (var rule in _profile.BuffRules.OrderBy(rule => rule.Priority).ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase))
        {
            var match = Session.ActiveEffectDetails.FirstOrDefault(effect =>
                !string.IsNullOrWhiteSpace(rule.EffectMatch) &&
                (effect.Name.Contains(rule.EffectMatch, StringComparison.OrdinalIgnoreCase) ||
                 effect.EffectId.Equals(rule.EffectMatch, StringComparison.OrdinalIgnoreCase) ||
                 effect.SpellId.Equals(rule.EffectMatch, StringComparison.OrdinalIgnoreCase)));
            string state;
            string reason;
            if (!rule.Enabled)
            {
                state = "OFF";
                reason = "rule disabled";
            }
            else if (match is null)
            {
                state = "DUE";
                reason = string.IsNullOrWhiteSpace(rule.EffectMatch) ? "no effect match configured" : "effect absent";
            }
            else if (match.ExpiresAtUtc is null)
            {
                state = "ACTIVE";
                reason = "no expiry supplied by server";
            }
            else
            {
                var remaining = match.ExpiresAtUtc.Value - now;
                if (remaining <= TimeSpan.FromSeconds(Math.Max(0, rule.RefreshLeadSeconds)))
                {
                    state = "DUE";
                    reason = $"{Math.Max(0, (int)remaining.TotalSeconds)}s remaining · inside {rule.RefreshLeadSeconds}s lead";
                }
                else
                {
                    state = "ACTIVE";
                    reason = remaining.TotalHours >= 1 ? $"{remaining.TotalHours:0.0}h remaining" : $"{Math.Max(0, (int)remaining.TotalMinutes)}m {Math.Max(0, remaining.Seconds)}s remaining";
                }
            }
            lines.Add($"{state,-8}  {Trim(rule.Name, 27),-27}  {Trim(rule.EffectMatch, 28),-28}  {reason}");
        }

        if (_profile.BuffRules.Count == 0) lines.Add("No buff rules are configured.");
        _buffWatchdog.Text = string.Join(Environment.NewLine, lines);
    }

    private static string Trim(string? value, int width)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= width ? text : text[..Math.Max(0, width - 1)] + "…";
    }

    private static string DisplayCharacter(ClientSessionConnection s) => string.IsNullOrWhiteSpace(s.CharacterName) ? s.DisplayName : s.CharacterName;
    private static int Int(TextBox box, int fallback) => int.TryParse(box.Text, out var value) ? value : fallback;
    private static StackPanel FormPanel() => new() { Margin = new Thickness(12) };
    private static ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private static TextBlock Text(string text, FontWeight weight) => new() { Text = text, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.78, Margin = new Thickness(0, 6, 0, 8) };
    private static CheckBox Check(string text) => new() { Content = text, Margin = new Thickness(0, 4, 0, 4) };
    private static TextBox Box() => new() { MinWidth = 150, Margin = new Thickness(4, 2, 8, 2) };
    private static TextBox Small() => new() { Width = 70, Margin = new Thickness(4, 2, 8, 2) };
    private static ComboBox Combo() => new() { MinWidth = 180, Margin = new Thickness(4, 2, 8, 2) };
    private static Button Button(string text, double width) => new() { Content = text, Width = width, Margin = new Thickness(4, 2, 4, 2), Padding = new Thickness(6, 3, 6, 3) };

    private static UIElement Row(string label1, UIElement control1, string label2, UIElement control2)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l1 = Text(label1, FontWeights.Normal); l1.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(l1);
        Grid.SetColumn(control1, 1); g.Children.Add(control1);
        var l2 = Text(label2, FontWeights.Normal); l2.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(l2, 2); g.Children.Add(l2);
        Grid.SetColumn(control2, 3); g.Children.Add(control2);
        return g;
    }

    private static UIElement CheckCommandRow(CheckBox check, TextBox command)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(check);
        Grid.SetColumn(command, 1); g.Children.Add(command);
        return g;
    }

    private static void SelectSet(ComboBox combo, string id)
    {
        if (combo.ItemsSource is not IEnumerable<SetChoice> values) return;
        combo.SelectedItem = values.FirstOrDefault(item => item.Id.Equals(id ?? string.Empty, StringComparison.OrdinalIgnoreCase)) ?? values.FirstOrDefault();
    }

    private static string SelectedSetId(ComboBox combo) => (combo.SelectedItem as SetChoice)?.Id ?? string.Empty;
    private sealed record SetChoice(string Id, string Name);
}
