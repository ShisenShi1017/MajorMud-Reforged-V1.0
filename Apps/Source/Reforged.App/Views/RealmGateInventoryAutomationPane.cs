using System.Collections.ObjectModel;
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
/// Inventory/equipment automation manager. This is a client configuration tool, not
/// a game-data editor. Every command is executed by PlayViewModel through RealmGate's
/// existing automation command origin, connection checks, and Emergency Stop.
/// </summary>
internal sealed class RealmGateInventoryAutomationPane : UserControl
{
    private readonly PlayViewModel _host;
    private readonly RealmGateInventoryAutomationService _service = new();
    public ClientSessionConnection Session { get; }
    private readonly RealmGateInventoryAutomationProfile _profile;
    private readonly DispatcherTimer _timer;

    private readonly TextBlock _context = NewText(string.Empty, 12, FontWeights.SemiBold);
    private readonly TextBlock _live = NewText(string.Empty, 11, FontWeights.Normal);
    private readonly TextBlock _status = NewText("Ready.", 11, FontWeights.Normal);

    private readonly ListBox _sets = NewListBox();
    private readonly ListBox _setItems = NewListBox();
    private readonly TextBox _setName = NewTextBox();
    private readonly CheckBox _setEnabled = NewCheck("Enabled");
    private readonly CheckBox _setAuto = NewCheck("Auto apply");
    private readonly ComboBox _inventoryItemPicker = NewComboBox();

    private readonly ListBox _rules = NewListBox();
    private readonly ComboBox _ruleDisposition = NewComboBox();
    private readonly ComboBox _ruleMatchMode = NewComboBox();
    private readonly TextBox _ruleMatchValue = NewTextBox();
    private readonly TextBox _rulePriority = NewSmallTextBox(65);
    private readonly TextBox _ruleQuantity = NewSmallTextBox(65);
    private readonly CheckBox _ruleEnabled = NewCheck("Enabled");

    private readonly CheckBox _enableRules = NewCheck("Enable automatic inventory/economy rules");
    private readonly CheckBox _autoApply = NewCheck("Auto-apply selected equipment set");
    private readonly CheckBox _autoReequip = NewCheck("Re-equip configured set after inventory changes");
    private readonly CheckBox _allowSell = NewCheck("Allow automatic SELL rules");
    private readonly CheckBox _allowStash = NewCheck("Allow automatic STASH rules only at saved stash rooms");
    private readonly CheckBox _allowDiscard = NewCheck("Allow automatic DISCARD rules");
    private readonly CheckBox _allowBuy = NewCheck("Allow automatic BUY rules at decoded shops");
    private readonly CheckBox _autoDeposit = NewCheck("Deposit currency above keep amount at decoded banks");
    private readonly TextBox _keepCurrency = NewSmallTextBox(90);
    private readonly TextBox _depositAll = NewTextBox();
    private readonly TextBox _depositExcess = NewTextBox();
    private readonly TextBox _sellTemplate = NewTextBox();
    private readonly TextBox _stashTemplate = NewTextBox();
    private readonly TextBox _discardTemplate = NewTextBox();
    private readonly TextBox _buyTemplate = NewTextBox();
    private readonly TextBox _removeTemplate = NewTextBox();
    private readonly TextBox _equipTemplate = NewTextBox();
    private readonly TextBox _corpseCommand = NewTextBox();
    private readonly TextBox _corpseNeedle = NewTextBox();
    private readonly TextBox _stateAge = NewSmallTextBox(70);
    private readonly TextBox _cooldown = NewSmallTextBox(70);
    private readonly TextBox _maxCommands = NewSmallTextBox(70);
    private readonly ComboBox _automaticSet = NewComboBox();
    private readonly TextBox _liveDetails = NewReadOnlyTextBox();
    private DateTimeOffset? _lastInventoryPickerUpdatedAt;
    private int _lastInventoryPickerCount = -1;

    public RealmGateInventoryAutomationPane(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _profile = _host.GetInventoryAutomationProfile(session);
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;
        Content = BuildContent();

        LoadPreferenceControls();
        RefreshSetList();
        RefreshRuleList();
        RefreshInventoryPicker();
        RefreshLiveState();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(750), DispatcherPriority.Background, (_, _) => RefreshLiveState(), Dispatcher);
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            CapturePreferenceControls();
            _host.SaveInventoryAutomationProfile(Session, _profile);
        };
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new Grid { Margin = new Thickness(0, 0, 0, 9) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerText = new StackPanel();
        headerText.Children.Add(_context);
        headerText.Children.Add(_live);
        header.Children.Add(headerText);
        var refresh = NewButton("Refresh Inventory / Equipment", 165);
        refresh.Click += async (_, _) =>
        {
            await _host.RefreshInventoryEquipmentStateAsync(Session);
            SetStatus("Refresh requested through the current server connection.");
        };
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new Border
        {
            BorderThickness = new Thickness(1, 1, 0, 0),
            BorderBrush = SystemColors.ActiveBorderBrush,
            Padding = new Thickness(4, 7, 4, 0),
            Margin = new Thickness(0, 8, 0, 0),
            Child = _status
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Equipment Sets", Content = BuildEquipmentTab() });
        tabs.Items.Add(new TabItem { Header = "Inventory Rules", Content = BuildRulesTab() });
        tabs.Items.Add(new TabItem { Header = "Automation / Banking", Content = BuildPreferencesTab() });
        tabs.Items.Add(new TabItem { Header = "Live State / Recovery", Content = BuildRecoveryTab() });
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildEquipmentTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.38, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star) });

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition());
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.Children.Add(NewText("Saved equipment sets", 12, FontWeights.SemiBold));
        _sets.Margin = new Thickness(0, 6, 0, 0);
        _sets.DisplayMemberPath = nameof(RealmGateEquipmentSet.DisplayName);
        _sets.SelectionChanged += (_, _) => LoadSelectedSet();
        Grid.SetRow(_sets, 1);
        left.Children.Add(_sets);
        var buttons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        var capture = NewButton("Capture Current", 105);
        var add = NewButton("New Empty", 85);
        var remove = NewButton("Delete", 70);
        capture.Click += (_, _) => CaptureSet();
        add.Click += (_, _) => AddEmptySet();
        remove.Click += (_, _) => DeleteSelectedSet();
        buttons.Children.Add(capture);
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        Grid.SetRow(buttons, 2);
        left.Children.Add(buttons);
        root.Children.Add(left);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition());
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var editor = new Grid();
        editor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        editor.ColumnDefinitions.Add(new ColumnDefinition());
        editor.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        editor.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        editor.Children.Add(NewText("Set name", 11, FontWeights.Normal, new Thickness(0, 4, 6, 0)));
        Grid.SetColumn(_setName, 1);
        editor.Children.Add(_setName);
        _setEnabled.Margin = new Thickness(10, 3, 0, 0);
        Grid.SetColumn(_setEnabled, 2);
        editor.Children.Add(_setEnabled);
        _setAuto.Margin = new Thickness(10, 3, 0, 0);
        Grid.SetColumn(_setAuto, 3);
        editor.Children.Add(_setAuto);
        right.Children.Add(editor);

        var picker = new Grid { Margin = new Thickness(0, 8, 0, 6) };
        picker.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        picker.ColumnDefinitions.Add(new ColumnDefinition());
        picker.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        picker.Children.Add(NewText("Add carried item", 11, FontWeights.Normal, new Thickness(0, 4, 6, 0)));
        Grid.SetColumn(_inventoryItemPicker, 1);
        picker.Children.Add(_inventoryItemPicker);
        var addItem = NewButton("Add", 65);
        addItem.Margin = new Thickness(8, 0, 0, 0);
        addItem.Click += (_, _) => AddSelectedInventoryItem();
        Grid.SetColumn(addItem, 2);
        picker.Children.Add(addItem);
        Grid.SetRow(picker, 1);
        right.Children.Add(picker);

        _setItems.DisplayMemberPath = nameof(RealmGateEquipmentSetEntry.DisplayName);
        Grid.SetRow(_setItems, 2);
        right.Children.Add(_setItems);

        var rightButtons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        var save = NewButton("Save Set", 80);
        var removeItem = NewButton("Remove Item", 90);
        var apply = NewButton("Apply Set", 80);
        var useAuto = NewButton("Use for Auto", 90);
        save.Click += (_, _) => SaveSelectedSet();
        removeItem.Click += (_, _) => RemoveSelectedSetItem();
        apply.Click += async (_, _) => await ApplySelectedSetAsync();
        useAuto.Click += (_, _) => UseSelectedSetForAutomation();
        rightButtons.Children.Add(save);
        rightButtons.Children.Add(removeItem);
        rightButtons.Children.Add(apply);
        rightButtons.Children.Add(useAuto);
        Grid.SetRow(rightButtons, 3);
        right.Children.Add(rightButtons);
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        return root;
    }

    private UIElement BuildRulesTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.44, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.56, GridUnitType.Star) });

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition());
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.Children.Add(NewText("First matching enabled rule wins for each carried item.", 11, FontWeights.SemiBold));
        _rules.Margin = new Thickness(0, 6, 0, 0);
        _rules.DisplayMemberPath = nameof(RealmGateInventoryRule.DisplayName);
        _rules.SelectionChanged += (_, _) => LoadSelectedRule();
        Grid.SetRow(_rules, 1);
        left.Children.Add(_rules);
        var run = NewButton("Process Rules Now", 120);
        run.Margin = new Thickness(0, 7, 0, 0);
        run.Click += async (_, _) => await ProcessRulesAsync();
        Grid.SetRow(run, 2);
        left.Children.Add(run);
        root.Children.Add(left);

        var form = new Grid();
        for (var i = 0; i < 7; i++) form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        form.ColumnDefinitions.Add(new ColumnDefinition());
        AddFormRow(form, 0, "Action", _ruleDisposition);
        AddFormRow(form, 1, "Match mode", _ruleMatchMode);
        AddFormRow(form, 2, "Match value", _ruleMatchValue);
        AddFormRow(form, 3, "Priority", _rulePriority);
        AddFormRow(form, 4, "Target quantity", _ruleQuantity);
        _ruleEnabled.Margin = new Thickness(0, 5, 0, 5);
        Grid.SetRow(_ruleEnabled, 5);
        Grid.SetColumn(_ruleEnabled, 1);
        form.Children.Add(_ruleEnabled);
        var buttons = new WrapPanel { Margin = new Thickness(0, 9, 0, 0) };
        var add = NewButton("Add Rule", 80);
        var update = NewButton("Update", 75);
        var remove = NewButton("Delete", 70);
        add.Click += (_, _) => AddRule();
        update.Click += (_, _) => UpdateSelectedRule();
        remove.Click += (_, _) => DeleteSelectedRule();
        buttons.Children.Add(add);
        buttons.Children.Add(update);
        buttons.Children.Add(remove);
        Grid.SetRow(buttons, 6);
        Grid.SetColumn(buttons, 1);
        form.Children.Add(buttons);
        Grid.SetColumn(form, 2);
        root.Children.Add(form);
        return root;
    }

    private UIElement BuildPreferencesTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(NewText("Automation gates", 12, FontWeights.SemiBold));
        foreach (var check in new[] { _enableRules, _autoApply, _autoReequip, _allowSell, _allowStash, _allowDiscard, _allowBuy, _autoDeposit })
        {
            check.Margin = new Thickness(0, 4, 0, 0);
            panel.Children.Add(check);
        }

        panel.Children.Add(NewText("Automatic equipment set", 11, FontWeights.SemiBold, new Thickness(0, 12, 0, 4)));
        _automaticSet.DisplayMemberPath = nameof(RealmGateEquipmentSet.Name);
        panel.Children.Add(_automaticSet);

        var timing = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        timing.Children.Add(NewText("State max age (sec)", 11, FontWeights.Normal, new Thickness(0, 4, 5, 0)));
        timing.Children.Add(_stateAge);
        timing.Children.Add(NewText("Cooldown (sec)", 11, FontWeights.Normal, new Thickness(12, 4, 5, 0)));
        timing.Children.Add(_cooldown);
        timing.Children.Add(NewText("Max commands/pass", 11, FontWeights.Normal, new Thickness(12, 4, 5, 0)));
        timing.Children.Add(_maxCommands);
        panel.Children.Add(timing);

        panel.Children.Add(NewText("Banking", 12, FontWeights.SemiBold, new Thickness(0, 14, 0, 4)));
        panel.Children.Add(Labeled("Keep currency", _keepCurrency));
        panel.Children.Add(Labeled("Deposit all command", _depositAll));
        panel.Children.Add(Labeled("Deposit excess template", _depositExcess, "Use {amount}; leave blank rather than guessing denomination syntax."));

        panel.Children.Add(NewText("Command templates", 12, FontWeights.SemiBold, new Thickness(0, 14, 0, 4)));
        panel.Children.Add(Labeled("Sell", _sellTemplate, "{item}"));
        panel.Children.Add(Labeled("Stash", _stashTemplate, "{item}"));
        panel.Children.Add(Labeled("Discard", _discardTemplate, "{item}"));
        panel.Children.Add(Labeled("Buy", _buyTemplate, "{item}, {quantity}"));
        panel.Children.Add(Labeled("Remove", _removeTemplate, "{item}"));
        panel.Children.Add(Labeled("Equip", _equipTemplate, "{item}"));

        var save = NewButton("Save Automation Settings", 155);
        save.Margin = new Thickness(0, 14, 0, 0);
        save.Click += (_, _) => SavePreferences();
        panel.Children.Add(save);
        scroll.Content = panel;
        return scroll;
    }

    private UIElement BuildRecoveryTab()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var corpse = new Grid();
        corpse.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        corpse.ColumnDefinitions.Add(new ColumnDefinition());
        corpse.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        corpse.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddFormRow(corpse, 0, "Corpse name contains", _corpseNeedle);
        AddFormRow(corpse, 1, "Recovery loot command", _corpseCommand);
        root.Children.Add(corpse);

        var note = NewText(
            "Recovery never guesses which corpse belongs to you. Configure a unique corpse-name fragment before a recovery loot command can be generated.",
            11, FontWeights.Normal, new Thickness(0, 8, 0, 8));
        note.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(note, 1);
        root.Children.Add(note);

        Grid.SetRow(_liveDetails, 2);
        root.Children.Add(_liveDetails);
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var refresh = NewButton("Refresh State", 95);
        var recover = NewButton("Recover + Re-equip", 125);
        var apply = NewButton("Apply Auto Set", 105);
        refresh.Click += async (_, _) => await _host.RefreshInventoryEquipmentStateAsync(Session);
        recover.Click += async (_, _) => await RecoverAsync();
        apply.Click += async (_, _) => await ApplyAutomaticSetAsync();
        buttons.Children.Add(refresh);
        buttons.Children.Add(recover);
        buttons.Children.Add(apply);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);
        return root;
    }

    private void RefreshSetList(RealmGateEquipmentSet? select = null)
    {
        var chosen = select ?? _sets.SelectedItem as RealmGateEquipmentSet;
        _sets.ItemsSource = null;
        _sets.ItemsSource = _profile.EquipmentSets;
        if (chosen is not null && _profile.EquipmentSets.Contains(chosen)) _sets.SelectedItem = chosen;
        else _sets.SelectedItem = _profile.EquipmentSets.FirstOrDefault();
        _automaticSet.ItemsSource = null;
        _automaticSet.ItemsSource = _profile.EquipmentSets;
        _automaticSet.SelectedItem = _profile.EquipmentSets.FirstOrDefault(set => set.Id.Equals(_profile.Preferences.AutoEquipmentSetId, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadSelectedSet()
    {
        if (_sets.SelectedItem is not RealmGateEquipmentSet set)
        {
            _setName.Text = string.Empty;
            _setItems.ItemsSource = null;
            return;
        }
        _setName.Text = set.Name;
        _setEnabled.IsChecked = set.Enabled;
        _setAuto.IsChecked = set.AutoApply;
        _setItems.ItemsSource = null;
        _setItems.ItemsSource = set.Items;
    }

    private void CaptureSet()
    {
        if (Session.EquippedItems.Count == 0)
        {
            SetStatus("No captured equipment is available. Refresh Equipment first.");
            return;
        }
        var set = _host.CaptureCurrentEquipmentSet(Session, $"Captured {DateTime.Now:HHmm}");
        RefreshSetList(set);
        SetStatus($"Captured {set.Items.Count} equipped item(s) into '{set.Name}'.");
    }

    private void AddEmptySet()
    {
        var set = new RealmGateEquipmentSet { Name = $"Set {_profile.EquipmentSets.Count + 1}" };
        _profile.EquipmentSets.Add(set);
        RefreshSetList(set);
        SetStatus("New equipment set created locally. Save when finished.");
    }

    private void DeleteSelectedSet()
    {
        if (_sets.SelectedItem is not RealmGateEquipmentSet set) return;
        _profile.EquipmentSets.Remove(set);
        if (_profile.Preferences.AutoEquipmentSetId.Equals(set.Id, StringComparison.OrdinalIgnoreCase))
            _profile.Preferences.AutoEquipmentSetId = string.Empty;
        RefreshSetList();
        SaveProfile("Equipment set deleted.");
    }

    private void SaveSelectedSet()
    {
        if (_sets.SelectedItem is not RealmGateEquipmentSet set) return;
        set.Name = string.IsNullOrWhiteSpace(_setName.Text) ? "Equipment Set" : _setName.Text.Trim();
        set.Enabled = _setEnabled.IsChecked == true;
        set.AutoApply = _setAuto.IsChecked == true;
        set.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveProfile($"Saved equipment set '{set.Name}'.");
        RefreshSetList(set);
    }

    private void AddSelectedInventoryItem()
    {
        if (_sets.SelectedItem is not RealmGateEquipmentSet set || _inventoryItemPicker.SelectedItem is not string display) return;
        var observed = _service.ObserveInventory(Session).FirstOrDefault(item => !item.Equipped && item.DisplayText.Equals(display, StringComparison.Ordinal));
        if (observed is null) return;
        if (set.Items.Any(item =>
                (!string.IsNullOrWhiteSpace(observed.ItemId) && item.ItemId.Equals(observed.ItemId, StringComparison.OrdinalIgnoreCase)) ||
                item.ItemName.Equals(observed.Name, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus($"'{observed.Name}' is already in this set.");
            return;
        }
        set.Items.Add(new RealmGateEquipmentSetEntry
        {
            ItemId = observed.ItemId,
            ItemName = observed.Name,
            Slot = observed.Slot,
            Order = set.Items.Count
        });
        LoadSelectedSet();
        SetStatus($"Added '{observed.Name}' to '{set.Name}'.");
    }

    private void RemoveSelectedSetItem()
    {
        if (_sets.SelectedItem is not RealmGateEquipmentSet set || _setItems.SelectedItem is not RealmGateEquipmentSetEntry item) return;
        set.Items.Remove(item);
        var order = 0;
        foreach (var entry in set.Items) entry.Order = order++;
        LoadSelectedSet();
    }

    private async Task ApplySelectedSetAsync()
    {
        SaveSelectedSet();
        if (_sets.SelectedItem is not RealmGateEquipmentSet set) return;
        var plan = await _host.ApplyInventoryEquipmentSetAsync(Session, set);
        SetStatus(DescribePlan(plan));
    }

    private void UseSelectedSetForAutomation()
    {
        if (_sets.SelectedItem is not RealmGateEquipmentSet set) return;
        _profile.Preferences.AutoEquipmentSetId = set.Id;
        _profile.Preferences.AutoApplyEquipmentSet = true;
        set.AutoApply = true;
        SaveProfile($"'{set.Name}' is now the automatic equipment set.");
        LoadPreferenceControls();
        RefreshSetList(set);
    }

    private void RefreshInventoryPicker()
    {
        if (_lastInventoryPickerCount == Session.InventoryItems.Count &&
            _lastInventoryPickerUpdatedAt == Session.InventoryUpdatedAt)
            return;

        var current = _inventoryItemPicker.SelectedItem as string;
        _inventoryItemPicker.ItemsSource = null;
        _inventoryItemPicker.ItemsSource = Session.InventoryItems.ToArray();
        if (current is not null && Session.InventoryItems.Contains(current)) _inventoryItemPicker.SelectedItem = current;
        _lastInventoryPickerCount = Session.InventoryItems.Count;
        _lastInventoryPickerUpdatedAt = Session.InventoryUpdatedAt;
    }

    private void RefreshRuleList(RealmGateInventoryRule? select = null)
    {
        var chosen = select ?? _rules.SelectedItem as RealmGateInventoryRule;
        _rules.ItemsSource = null;
        _rules.ItemsSource = _profile.Rules.OrderBy(rule => rule.Priority).ThenBy(rule => rule.Id).ToArray();
        if (chosen is not null && _profile.Rules.Contains(chosen)) _rules.SelectedItem = chosen;
        else _rules.SelectedItem = _profile.Rules.OrderBy(rule => rule.Priority).FirstOrDefault();
    }

    private void LoadSelectedRule()
    {
        if (_rules.SelectedItem is not RealmGateInventoryRule rule) return;
        _ruleDisposition.SelectedItem = rule.Disposition;
        _ruleMatchMode.SelectedItem = rule.MatchMode;
        _ruleMatchValue.Text = rule.MatchValue;
        _rulePriority.Text = rule.Priority.ToString();
        _ruleQuantity.Text = rule.TargetQuantity.ToString();
        _ruleEnabled.IsChecked = rule.Enabled;
    }

    private RealmGateInventoryRule RuleFromControls(RealmGateInventoryRule? rule = null)
    {
        rule ??= new RealmGateInventoryRule();
        rule.Disposition = _ruleDisposition.SelectedItem is RealmGateInventoryDisposition disposition ? disposition : RealmGateInventoryDisposition.Keep;
        rule.MatchMode = _ruleMatchMode.SelectedItem is RealmGateInventoryRuleMatchMode mode ? mode : RealmGateInventoryRuleMatchMode.ExactName;
        rule.MatchValue = _ruleMatchValue.Text.Trim();
        rule.Priority = ParseInt(_rulePriority.Text, 100, -100000, 100000);
        rule.TargetQuantity = ParseInt(_ruleQuantity.Text, 1, 1, 9999);
        rule.Enabled = _ruleEnabled.IsChecked == true;
        return rule;
    }

    private void AddRule()
    {
        var rule = RuleFromControls();
        if (rule.MatchValue.Length == 0) { SetStatus("A rule needs an explicit match value."); return; }
        _profile.Rules.Add(rule);
        SaveProfile("Inventory rule added.");
        RefreshRuleList(rule);
    }

    private void UpdateSelectedRule()
    {
        if (_rules.SelectedItem is not RealmGateInventoryRule rule) return;
        RuleFromControls(rule);
        if (rule.MatchValue.Length == 0) { SetStatus("A rule needs an explicit match value."); return; }
        SaveProfile("Inventory rule updated.");
        RefreshRuleList(rule);
    }

    private void DeleteSelectedRule()
    {
        if (_rules.SelectedItem is not RealmGateInventoryRule rule) return;
        _profile.Rules.Remove(rule);
        SaveProfile("Inventory rule deleted.");
        RefreshRuleList();
    }

    private async Task ProcessRulesAsync()
    {
        CapturePreferenceControls();
        _host.SaveInventoryAutomationProfile(Session, _profile);
        var plan = await _host.ProcessInventoryAutomationRulesAsync(Session);
        SetStatus(DescribePlan(plan));
    }

    private void LoadPreferenceControls()
    {
        var p = _profile.Preferences;
        _enableRules.IsChecked = p.EnableAutomaticInventoryRules;
        _autoApply.IsChecked = p.AutoApplyEquipmentSet;
        _autoReequip.IsChecked = p.AutoReequipAfterInventoryChange;
        _allowSell.IsChecked = p.AllowAutomaticSell;
        _allowStash.IsChecked = p.AllowAutomaticStash;
        _allowDiscard.IsChecked = p.AllowAutomaticDiscard;
        _allowBuy.IsChecked = p.AllowAutomaticBuy;
        _autoDeposit.IsChecked = p.AutoDepositExcessCurrency;
        _keepCurrency.Text = p.KeepCurrency.ToString();
        _depositAll.Text = p.DepositAllCommand;
        _depositExcess.Text = p.DepositExcessCommandTemplate;
        _sellTemplate.Text = p.SellCommandTemplate;
        _stashTemplate.Text = p.StashCommandTemplate;
        _discardTemplate.Text = p.DiscardCommandTemplate;
        _buyTemplate.Text = p.BuyCommandTemplate;
        _removeTemplate.Text = p.RemoveCommandTemplate;
        _equipTemplate.Text = p.EquipCommandTemplate;
        _corpseCommand.Text = p.RecoveryCorpseCommand;
        _corpseNeedle.Text = p.RecoveryCorpseNameContains;
        _stateAge.Text = p.StateMaximumAgeSeconds.ToString();
        _cooldown.Text = p.CommandCooldownSeconds.ToString();
        _maxCommands.Text = p.MaximumCommandsPerPass.ToString();
        _ruleDisposition.ItemsSource = Enum.GetValues<RealmGateInventoryDisposition>();
        _ruleMatchMode.ItemsSource = Enum.GetValues<RealmGateInventoryRuleMatchMode>();
        if (_ruleDisposition.SelectedItem is null) _ruleDisposition.SelectedItem = RealmGateInventoryDisposition.Keep;
        if (_ruleMatchMode.SelectedItem is null) _ruleMatchMode.SelectedItem = RealmGateInventoryRuleMatchMode.ExactName;
    }

    private void CapturePreferenceControls()
    {
        var p = _profile.Preferences;
        p.EnableAutomaticInventoryRules = _enableRules.IsChecked == true;
        p.AutoApplyEquipmentSet = _autoApply.IsChecked == true;
        p.AutoReequipAfterInventoryChange = _autoReequip.IsChecked == true;
        p.AllowAutomaticSell = _allowSell.IsChecked == true;
        p.AllowAutomaticStash = _allowStash.IsChecked == true;
        p.AllowAutomaticDiscard = _allowDiscard.IsChecked == true;
        p.AllowAutomaticBuy = _allowBuy.IsChecked == true;
        p.AutoDepositExcessCurrency = _autoDeposit.IsChecked == true;
        p.KeepCurrency = Math.Max(0, ParseLong(_keepCurrency.Text, p.KeepCurrency));
        p.DepositAllCommand = _depositAll.Text.Trim();
        p.DepositExcessCommandTemplate = _depositExcess.Text.Trim();
        p.SellCommandTemplate = _sellTemplate.Text.Trim();
        p.StashCommandTemplate = _stashTemplate.Text.Trim();
        p.DiscardCommandTemplate = _discardTemplate.Text.Trim();
        p.BuyCommandTemplate = _buyTemplate.Text.Trim();
        p.RemoveCommandTemplate = _removeTemplate.Text.Trim();
        p.EquipCommandTemplate = _equipTemplate.Text.Trim();
        p.RecoveryCorpseCommand = _corpseCommand.Text.Trim();
        p.RecoveryCorpseNameContains = _corpseNeedle.Text.Trim();
        p.StateMaximumAgeSeconds = ParseInt(_stateAge.Text, p.StateMaximumAgeSeconds, 5, 600);
        p.CommandCooldownSeconds = ParseInt(_cooldown.Text, p.CommandCooldownSeconds, 1, 60);
        p.MaximumCommandsPerPass = ParseInt(_maxCommands.Text, p.MaximumCommandsPerPass, 1, 100);
        if (_automaticSet.SelectedItem is RealmGateEquipmentSet set) p.AutoEquipmentSetId = set.Id;
    }

    private void SavePreferences()
    {
        CapturePreferenceControls();
        SaveProfile("Inventory/equipment automation settings saved.");
    }

    private async Task RecoverAsync()
    {
        CapturePreferenceControls();
        _host.SaveInventoryAutomationProfile(Session, _profile);
        var set = _service.ResolveAutomaticSet(_profile) ?? _sets.SelectedItem as RealmGateEquipmentSet;
        var plan = await _host.RecoverInventoryEquipmentAsync(Session, set);
        SetStatus(DescribePlan(plan));
    }

    private async Task ApplyAutomaticSetAsync()
    {
        CapturePreferenceControls();
        var set = _service.ResolveAutomaticSet(_profile);
        if (set is null) { SetStatus("No enabled automatic equipment set is configured."); return; }
        var plan = await _host.ApplyInventoryEquipmentSetAsync(Session, set);
        SetStatus(DescribePlan(plan));
    }

    private void RefreshLiveState()
    {
        _context.Text = $"{DisplayCharacter(Session)} • {Session.ServerProfileName} / {Session.WorldName} • " +
                        $"{(_service.IsAtSavedStash(Session) ? "saved stash room" : Session.CurrentRoomName)}";
        _live.Text = $"Inventory {Session.InventoryItems.Count:N0} • Equipped {Session.EquippedItems.Count:N0} • " +
                     $"Weight {Session.CarriedWeight:N0}/{Session.CarryingCapacity:N0} ({Session.EncumbrancePercent}%) • Currency {Session.CarriedCurrency:N0}";
        RefreshInventoryPicker();

        var observed = _service.ObserveInventory(Session);
        var sb = new StringBuilder();
        sb.AppendLine($"Connection: {Session.ConnectionState}");
        sb.AppendLine($"Room: {Session.CurrentRoomName} [{Session.CurrentRoomKey}]");
        sb.AppendLine($"Services: {(Session.CurrentRoomServices.Count == 0 ? "none captured" : string.Join(", ", Session.CurrentRoomServices.Select(service => service.Kind + ":" + service.Name)))}");
        sb.AppendLine($"Inventory captured: {Session.InventoryUpdatedAt?.ToString("u") ?? "never"}");
        sb.AppendLine($"Equipment captured: {Session.EquipmentUpdatedAt?.ToString("u") ?? "never"}");
        sb.AppendLine($"Saved stash match: {(_service.IsAtSavedStash(Session) ? "YES" : "no")}");
        sb.AppendLine();
        sb.AppendLine("Observed carried/equipped items:");
        foreach (var item in observed.Take(80))
            sb.AppendLine($"{(item.Equipped ? "EQ " : "INV")} {item.Quantity}x {item.Name}" +
                          (string.IsNullOrWhiteSpace(item.Slot) ? string.Empty : $" [{item.Slot}]") +
                          (string.IsNullOrWhiteSpace(item.ItemId) ? string.Empty : $" • {item.ItemId}"));
        if (observed.Count > 80) sb.AppendLine($"... {observed.Count - 80} more");
        _liveDetails.Text = sb.ToString();
    }

    private void SaveProfile(string message)
    {
        CapturePreferenceControls();
        _host.SaveInventoryAutomationProfile(Session, _profile);
        SetStatus(message);
    }

    private static string DescribePlan(RealmGateInventoryAutomationPlan plan)
    {
        var text = plan.Summary;
        if (plan.MissingItems.Count > 0) text += " Missing: " + string.Join(", ", plan.MissingItems.Take(8));
        if (plan.Warnings.Count > 0) text += " Warning: " + plan.Warnings[0];
        return text;
    }

    private void SetStatus(string text) => _status.Text = text;

    private static string DisplayCharacter(ClientSessionConnection session) =>
        string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;

    private static int ParseInt(string? text, int fallback, int min, int max) =>
        int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : Math.Clamp(fallback, min, max);

    private static long ParseLong(string? text, long fallback) => long.TryParse(text, out var value) ? value : fallback;

    private static void AddFormRow(Grid grid, int row, string label, UIElement control)
    {
        var text = NewText(label, 11, FontWeights.Normal, new Thickness(0, 5, 6, 5));
        Grid.SetRow(text, row);
        grid.Children.Add(text);
        if (control is FrameworkElement element) element.Margin = new Thickness(0, 2, 0, 4);
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
    }

    private static UIElement Labeled(string label, Control control, string? hint = null)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var labelText = NewText(label, 11, FontWeights.Normal, new Thickness(0, 4, 7, 0));
        grid.Children.Add(labelText);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        if (!string.IsNullOrWhiteSpace(hint)) control.ToolTip = hint;
        return grid;
    }

    private static TextBlock NewText(string text, double size, FontWeight weight, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Margin = margin ?? new Thickness(0),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBox NewTextBox() => new()
    {
        MinHeight = 25,
        VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(4, 2, 4, 2)
    };

    private static TextBox NewSmallTextBox(double width) => new()
    {
        Width = width,
        MinHeight = 25,
        VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(4, 2, 4, 2)
    };

    private static TextBox NewReadOnlyTextBox() => new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Consolas"),
        Padding = new Thickness(6)
    };

    private static ListBox NewListBox() => new()
    {
        BorderThickness = new Thickness(1),
        Padding = new Thickness(3)
    };

    private static ComboBox NewComboBox() => new()
    {
        MinHeight = 25,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static CheckBox NewCheck(string text) => new()
    {
        Content = text,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Button NewButton(string text, double width) => new()
    {
        Content = text,
        Width = width,
        MinHeight = 27,
        Margin = new Thickness(0, 0, 6, 0),
        Padding = new Thickness(6, 2, 6, 2)
    };
}
