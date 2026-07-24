using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Reforged.App.Models;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class ClientTerminalWindow : Window
{
    private readonly DispatcherTimer _classicRefreshTimer;
    private INotifyCollectionChanged? _attachedTerminalLines;
    private bool _classicPasswordMode;

    public ClientTerminalWindow()
    {
        InitializeComponent();
        _classicRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _classicRefreshTimer.Tick += (_, _) => RefreshClassicTerminal();
        Loaded += (_, _) =>
        {
            AttachTerminalRefresh();
            _classicRefreshTimer.Start();
            RefreshClassicTerminal();
        };
        DataContextChanged += (_, _) => AttachTerminalRefresh();
        Closed += (_, _) =>
        {
            _classicRefreshTimer.Stop();
            DetachTerminalRefresh();
        };
    }

    private void AttachTerminalRefresh()
    {
        var next = ViewModel?.SelectedLiveSession?.TerminalLines as INotifyCollectionChanged;
        if (ReferenceEquals(next, _attachedTerminalLines)) return;

        DetachTerminalRefresh();
        _attachedTerminalLines = next;
        if (_attachedTerminalLines is not null)
            _attachedTerminalLines.CollectionChanged += TerminalLinesChanged;
    }

    private void DetachTerminalRefresh()
    {
        if (_attachedTerminalLines is not null)
            _attachedTerminalLines.CollectionChanged -= TerminalLinesChanged;
        _attachedTerminalLines = null;
    }

    private void TerminalLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            AttachTerminalRefresh();
            RefreshClassicTerminal();
        }));
    }

    private void RefreshClassicTerminal()
    {
        AttachTerminalRefresh();
        if (ViewModel?.SelectedLiveSession?.TerminalLines is not { } lines) return;

        CollectionViewSource.GetDefaultView(lines)?.Refresh();
        ClassicTerminalLines?.UpdateLayout();

        if (lines.Count > 0)
        {
            ClassicTerminalLines?.ScrollIntoView(lines[^1]);
            FindVisualChild<ScrollViewer>(ClassicTerminalLines)?.ScrollToEnd();
            UpdateClassicPasswordMode(lines[^1]);
        }

        // The Classic play terminal always remains on the current Host output.
        // Historical scrolling is handled only by the separate Back-scroll window.
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                ClassicTerminalLines?.UpdateLayout();
                FindVisualChild<ScrollViewer>(ClassicTerminalLines)?.ScrollToEnd();

                if (_classicPasswordMode)
                    ClassicPasswordInput?.Focus();
                else
                {
                    if (ClassicCommandInput is not null)
                    {
                        ClassicCommandInput.Focus();
                        ClassicCommandInput.CaretIndex = ClassicCommandInput.Text?.Length ?? 0;
                    }
                }
            }));
    }


    private void ClassicTerminalMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // MegaMUD's live play window stays on the newest output. Use Back-scroll
        // for history instead of moving the live terminal away from the prompt.
        e.Handled = true;
        RefreshClassicTerminal();
    }

    private void ClassicTerminalNavigationKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Up or Key.Down)
        {
            e.Handled = true;
            RefreshClassicTerminal();
        }
    }

    private void BackScrollClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedLiveSession is not { } session)
            return;

        var history = new ListBox
        {
            ItemsSource = session.TerminalLines,
            Background = new SolidColorBrush(Color.FromRgb(2, 5, 10)),
            Foreground = new SolidColorBrush(Color.FromRgb(231, 231, 231)),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Lucida Console"),
            FontSize = 18,
            Padding = new Thickness(6, 4, 6, 4),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(history, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(history, ScrollBarVisibility.Auto);
        ScrollViewer.SetCanContentScroll(history, false);

        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.FocusableProperty, false));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(231, 231, 231))));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        history.ItemContainerStyle = itemStyle;

        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(AnsiTextBlock));
        factory.SetBinding(AnsiTextBlock.AnsiTextProperty, new Binding("."));
        factory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        factory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Lucida Console"));
        factory.SetValue(TextBlock.FontSizeProperty, 18d);
        factory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(231, 231, 231)));
        template.VisualTree = factory;
        history.ItemTemplate = template;

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
            history.UpdateLayout();
            if (history.Items.Count > 0)
                history.ScrollIntoView(history.Items[^1]);

            // Put the scroll position near current output when opened. After
            // opening, the user controls this window independently while play continues.
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => FindVisualChild<ScrollViewer>(history)?.ScrollToEnd()));
        };

        backScrollWindow.Show();
        ClassicCommandInput?.Focus();
    }

    private static T? FindVisualChild<T>(DependencyObject? parent)
        where T : DependencyObject
    {
        if (parent is null)
            return null;

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
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

        var normalInputRequested =
            lower.EndsWith("account:", StringComparison.Ordinal) ||
            lower.EndsWith("account >", StringComparison.Ordinal) ||
            lower.Contains("create this account?", StringComparison.Ordinal) ||
            lower.Contains("password accepted", StringComparison.Ordinal) ||
            lower.Contains("login successful", StringComparison.Ordinal) ||
            lower.Contains("welcome", StringComparison.Ordinal);

        if (normalInputRequested)
        {
            ResetClassicInputMode();
            return;
        }

        // Password entry must never remain active after the Host moves on to
        // ordinary output or a normal command/account prompt. The next explicit
        // password/confirmation prompt will enable hidden input again.
        if (!passwordRequested)
        {
            if (_classicPasswordMode)
                ResetClassicInputMode();
            return;
        }

        _classicPasswordMode = true;
        if (ClassicCommandInput is not null) ClassicCommandInput.Visibility = Visibility.Collapsed;
        if (ClassicPasswordInput is not null)
        {
            ClassicPasswordInput.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => ClassicPasswordInput.Focus()));
        }
    }

    private async void ClassicPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is null || sender is not PasswordBox box) return;
        e.Handled = true;
        var secret = box.Password;
        box.Clear();
        if (string.IsNullOrEmpty(secret)) return;

        await ViewModel.SendSensitiveTerminalCommandAsync(secret);

        // Remain in hidden mode until the Host emits a normal-input prompt or
        // confirms account/login completion. This prevents the confirmation
        // password from briefly appearing in the normal command box.
        _classicPasswordMode = true;
        await Dispatcher.InvokeAsync(() => box.Focus(), DispatcherPriority.Input);
    }

    private void ResetClassicInputMode()
    {
        _classicPasswordMode = false;
        if (ClassicPasswordInput is not null)
        {
            ClassicPasswordInput.Clear();
            ClassicPasswordInput.Visibility = Visibility.Collapsed;
        }
        if (ClassicCommandInput is not null)
        {
            ClassicCommandInput.Visibility = Visibility.Visible;
            ClassicCommandInput.Focus();
        }
    }

    private static void ScrollListBoxToEnd(DependencyObject parent, object lastItem)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is ListBox listBox && ReferenceEquals(listBox.ItemsSource, (parent as FrameworkElement)?.DataContext))
                listBox.ScrollIntoView(lastItem);
            else if (child is ListBox terminal && terminal.Items.Contains(lastItem))
                terminal.ScrollIntoView(lastItem);
            ScrollListBoxToEnd(child, lastItem);
        }
    }

    private PlayViewModel? ViewModel => DataContext as PlayViewModel;

    private async void ConnectClicked(object sender, RoutedEventArgs e)
    {
        ResetClassicInputMode();
        if (ViewModel is not null) await ViewModel.ConnectSelectedSessionAsync();
    }

    private async void DisconnectClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.DisconnectSelectedSessionAsync();
        ResetClassicInputMode();
    }

    private async void SendClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.SendTerminalCommandAsync();
    }

    private async void TerminalCommandKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is null) return;
        e.Handled = true;

        await ViewModel.SendTerminalCommandAsync();

        // Repaint immediately after the CR/LF is sent. The Host's next partial
        // statline will update LivePromptText as soon as it arrives.
        await Dispatcher.InvokeAsync(RefreshClassicTerminal, DispatcherPriority.Send);
    }

    private void ClearClicked(object sender, RoutedEventArgs e) => ViewModel?.ClearSelectedTerminal();

    private async void ExportLogClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.ExportSelectedTerminalLogAsync();
    }

    private void OptionsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        new ClientOptionsWindow { Owner = this, DataContext = ViewModel }.Show();
    }

    private void CombatOptionsClicked(object sender, RoutedEventArgs e)
    {
        var session = ViewModel?.SelectedLiveSession;
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
        for (var i = 0; i < 8; i++) wg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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

        help.Click += (_, _) => MessageBox.Show(dialog, "Classic MegaMUD combat profile. Values are stored per session and Auto-Combat is synchronized with the shared automation master state.", "Combat automation help", MessageBoxButton.OK, MessageBoxImage.Information);
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
            prefs.SpellCombatResetCommand = spellReset.Text; prefs.MaxTotalExperiencePerRoom = Parse(maxExp, prefs.MaxTotalExperiencePerRoom);
            prefs.MinimumMobsPerRoom = Parse(minMobs, prefs.MinimumMobsPerRoom); prefs.MaximumMobsPerRoom = Parse(maxMobs, prefs.MaximumMobsPerRoom);
            if (ViewModel is not null) await ViewModel.SaveClassicCombatPreferencesAsync(); dialog.Close();
        };
        dialog.Content = root; dialog.ShowDialog();
    }

    private void PartyOptionsClicked(object sender, RoutedEventArgs e)
    {
        var session = ViewModel?.SelectedLiveSession;
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
            if (ViewModel is not null) await ViewModel.SaveClassicPartyPreferencesAsync();
            dialog.Close();
        };

        dialog.Content = root;
        dialog.ShowDialog();
    }


    private void HealthOptionsClicked(object sender, RoutedEventArgs e)
    {
        var session = ViewModel?.SelectedLiveSession;
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
            "Classic MegaMUD health thresholds are stored per session. They are consumed by the shared Client automation runtime and never modify Host game data.",
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
            if (ViewModel is not null) await ViewModel.SaveClassicHealthPreferencesAsync();
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

    private void CopyClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedLiveSession is null) return;
        var text = string.Join(Environment.NewLine, ViewModel.SelectedLiveSession.TerminalLines);
        if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
    }

    private void PasteClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || !Clipboard.ContainsText()) return;
        ViewModel.TerminalCommand = Clipboard.GetText().Replace("\r", string.Empty).Replace("\n", " ");
    }

    private void GotoClicked(object sender, RoutedEventArgs e) => ShowPathSelector(loopOnly: false);

    private void LoopClicked(object sender, RoutedEventArgs e) => ShowPathSelector(loopOnly: true);

    private void ShowPathSelector(bool loopOnly)
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
            Title = loopOnly ? "Roam area..." : "Goto location...",
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

        var go = NewDialogButton(loopOnly ? "Run" : "Go", 58);
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
            await StartPathAsync(selected);
            dialog.Close();
        };

        async Task ExecuteSelectedAsync()
        {
            if (selected is null) return;
            await StartPathAsync(selected);
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

    private async Task StartPathAsync(MegaMudPathRecord path)
    {
        if (ViewModel is null) return;
        ViewModel.SelectedPath = path;
        await ViewModel.StartSelectedRouteAsync();
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
        await ViewModel.SaveSelectedTerminalViewModeAsync();
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
