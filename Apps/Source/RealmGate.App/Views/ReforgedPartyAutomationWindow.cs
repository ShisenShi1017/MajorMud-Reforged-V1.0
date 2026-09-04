using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RealmGate.App.Models;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

/// <summary>
/// RealmGate party status and coordination surface. It intentionally reuses the existing
/// ClientCoordinationService for permission-gated remote @ commands rather than
/// duplicating MudPlay's UI or remote-command model.
/// </summary>
public sealed class RealmGatePartyWindow : Window
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly ReforgedPartyAutomationProfile _profile;
    private readonly DispatcherTimer _timer;

    private readonly TextBlock _context = Text(string.Empty, FontWeights.SemiBold);
    private readonly TextBlock _live = Text(string.Empty, FontWeights.Normal);
    private readonly TextBlock _status = Text("Ready.", FontWeights.Normal);

    private readonly CheckBox _enabled = Check("Enable advanced RealmGate party coordinator");
    private readonly ComboBox _role = Combo();
    private readonly TextBox _leader = Box();
    private readonly CheckBox _follow = Check("Automatically follow the configured leader");
    private readonly CheckBox _assist = Check("Assist the configured leader during combat");
    private readonly CheckBox _acceptInvites = Check("Accept exact invitations from the configured leader or controllers with Invite permission");
    private readonly TextBox _acceptInviteCommand = Box();
    private readonly CheckBox _wait = Check("Wait when the configured leader leaves the observed party");
    private readonly CheckBox _resume = Check("Resume when the configured leader returns");
    private readonly CheckBox _regroup = Check("Request regroup information while separated");
    private readonly TextBox _followCommand = Box();
    private readonly TextBox _assistCommand = Box();
    private readonly TextBox _waitCommand = Box();
    private readonly TextBox _resumeCommand = Box();
    private readonly TextBox _regroupCommand = Box();

    private readonly CheckBox _rescue = Check("Rescue the lowest-health observed party member in combat");
    private readonly TextBox _rescueAt = Small();
    private readonly TextBox _rescueCommand = Box();
    private readonly CheckBox _heal = Check("Heal the lowest-health observed party member");
    private readonly TextBox _healAt = Small();
    private readonly TextBox _healCommand = Box();
    private readonly TextBox _manaReserve = Small();
    private readonly CheckBox _cure = Check("Cure party members explicitly marked poisoned");
    private readonly TextBox _cureCommand = Box();
    private readonly CheckBox _buff = Check("Apply an explicitly configured timed party buff (does not guess per-member effect state)");
    private readonly TextBox _buffCommand = Box();
    private readonly TextBox _buffTargets = Box();
    private readonly TextBox _buffInterval = Small();
    private readonly TextBox _buffMinMana = Small();
    private readonly CheckBox _buffOutOfCombat = Check("Party buff only outside combat");

    private readonly CheckBox _rank = Check("Maintain this character's preferred party rank when the roster reports it");
    private readonly ComboBox _preferredRank = Combo();
    private readonly TextBox _frontCommand = Box();
    private readonly TextBox _backCommand = Box();
    private readonly CheckBox _invite = Check("Leader mode: invite configured names only when a fresh WHO roster confirms they are online");
    private readonly TextBox _inviteNames = Box();
    private readonly TextBox _inviteCommand = Box();

    private readonly CheckBox _pauseRoute = Check("Pause this session's route while the configured leader is missing");
    private readonly CheckBox _resumeRoute = Check("Resume the route when the configured leader returns");
    private readonly CheckBox _resync = Check("Require a fresh party roster after reconnect");
    private readonly TextBox _partyAge = Small();
    private readonly TextBox _onlineAge = Small();
    private readonly TextBox _cooldown = Small();
    private readonly TextBox _hold = Small();

    private readonly CheckBox _classicHelpBash = Check("Help leader bash doors");
    private readonly CheckBox _classicSayPanic = Check("Say @panic when leading");
    private readonly CheckBox _classicIgnorePanic = Check("Ignore @panic when following");
    private readonly CheckBox _classicIgnoreParty = Check("Ignore @party when following");
    private readonly CheckBox _classicIgnoreWait = Check("Ignore @wait when leading");
    private readonly TextBox _classicMaxMonsters = Small();
    private readonly TextBox _classicMaxExperience = Small();
    private readonly TextBox _classicWaitPercent = Small();
    private readonly TextBox _classicWaitMinutes = Small();
    private readonly TextBox _classicParFrequency = Small();
    private readonly TextBox _classicWaitCommand = Box();
    private readonly TextBox _classicResumeCommand = Box();

    public RealmGatePartyWindow(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _profile = _host.GetAdvancedPartyAutomationProfile(session);

        Title = $"RealmGate Party — {DisplayCharacter(session)}";
        Width = 930;
        Height = 720;
        MinWidth = 760;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        _role.ItemsSource = Enum.GetValues<ReforgedPartyRole>();
        _preferredRank.ItemsSource = new[] { "Frontrank", "Backrank" };
        Content = BuildContent();
        LoadControls();
        LoadClassicControls();
        RefreshLive();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(750), DispatcherPriority.Background, (_, _) => RefreshLive(), Dispatcher);
        Loaded += (_, _) => _timer.Start();
        Closing += async (_, _) =>
        {
            _timer.Stop();
            CaptureControls();
            CaptureClassicControls();
            _host.SaveAdvancedPartyAutomationProfile(Session, _profile);
            await _host.SaveClassicPartyPreferencesAsync(Session);
        };
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hs = new StackPanel();
        hs.Children.Add(_context);
        hs.Children.Add(_live);
        header.Children.Add(hs);
        var refresh = Button("Refresh Party State", 125);
        refresh.Click += async (_, _) =>
        {
            CaptureControls();
            _host.SaveAdvancedPartyAutomationProfile(Session, _profile);
            await _host.RefreshAdvancedPartyStateAsync(Session, includeOnline: _profile.Preferences.Role == ReforgedPartyRole.LeadParty);
            _status.Text = "Fresh party state requested through the private RealmGate protocol when supported.";
        };
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(_status);
        var evaluate = Button("Evaluate Now", 105);
        evaluate.Click += async (_, _) =>
        {
            CaptureControls();
            _host.SaveAdvancedPartyAutomationProfile(Session, _profile);
            var plan = await _host.RunAdvancedPartyAutomationNowAsync(Session);
            _status.Text = plan.Summary;
        };
        Grid.SetColumn(evaluate, 1);
        footer.Children.Add(evaluate);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Status", Content = BuildStatusTab() });
        tabs.Items.Add(new TabItem { Header = "MegaMUD Options", Content = Scroll(BuildClassicOptionsTab()) });
        tabs.Items.Add(new TabItem { Header = "Leader / Follower", Content = Scroll(BuildLeaderTab()) });
        tabs.Items.Add(new TabItem { Header = "Support / Rank", Content = Scroll(BuildSupportTab()) });
        tabs.Items.Add(new TabItem { Header = "Safety / Permissions", Content = Scroll(BuildSafetyTab()) });
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildStatusTab()
    {
        var root = new DockPanel { Margin = new Thickness(10) };
        var refresh = Button("Refresh Party State", 125);
        refresh.Click += async (_, _) => await _host.SendClientProtocolCommandAsync(Session, "clientdata party");
        DockPanel.SetDock(refresh, Dock.Bottom);
        root.Children.Add(refresh);
        var grid = new DataGrid
        {
            ItemsSource = Session.PartyMembers,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 0, 0, 8)
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding(nameof(ClientPartyMemberState.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Class", Binding = new System.Windows.Data.Binding(nameof(ClientPartyMemberState.ClassName)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Vitals", Binding = new System.Windows.Data.Binding(nameof(ClientPartyMemberState.Vitals)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Rank", Binding = new System.Windows.Data.Binding(nameof(ClientPartyMemberState.Rank)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "State", Binding = new System.Windows.Data.Binding(nameof(ClientPartyMemberState.State)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        root.Children.Add(grid);
        return root;
    }

    private StackPanel BuildClassicOptionsTab()
    {
        var p = FormPanel();
        p.Children.Add(Note("Classic MegaMUD-style party behavior lives here together with RealmGate's newer party coordination. There is no separate Party Status or Party Configuration window."));
        p.Children.Add(_classicHelpBash);
        p.Children.Add(_classicSayPanic);
        p.Children.Add(_classicIgnorePanic);
        p.Children.Add(_classicIgnoreParty);
        p.Children.Add(_classicIgnoreWait);
        p.Children.Add(Row("Max monsters when partying", _classicMaxMonsters, "Max monster experience", _classicMaxExperience));
        p.Children.Add(Row("Wait below health %", _classicWaitPercent, "Leader wait minutes", _classicWaitMinutes));
        p.Children.Add(Row("PAR frequency seconds", _classicParFrequency, string.Empty, new TextBlock()));
        p.Children.Add(Row("Party wait command", _classicWaitCommand, "Party resume command", _classicResumeCommand));
        return p;
    }

    private StackPanel BuildLeaderTab()
    {
        var p = FormPanel();
        p.Children.Add(_enabled);
        p.Children.Add(Row("Party role", _role, "Configured leader", _leader));
        p.Children.Add(_follow);
        p.Children.Add(_assist);
        p.Children.Add(_acceptInvites);
        p.Children.Add(Row("Accept-invite command", _acceptInviteCommand, string.Empty, new TextBlock()));
        p.Children.Add(_wait);
        p.Children.Add(_resume);
        p.Children.Add(_regroup);
        p.Children.Add(Row("Follow command", _followCommand, "Assist command", _assistCommand));
        p.Children.Add(Row("Wait command", _waitCommand, "Resume command", _resumeCommand));
        p.Children.Add(Row("Regroup command", _regroupCommand, string.Empty, new TextBlock()));
        p.Children.Add(Note("Leader identity is never inferred. Follower mode requires the exact configured leader to appear in a fresh party roster before follow/assist is sent."));
        return p;
    }

    private StackPanel BuildSupportTab()
    {
        var p = FormPanel();
        p.Children.Add(_rescue);
        p.Children.Add(Row("Rescue below HP %", _rescueAt, "Rescue command", _rescueCommand));
        p.Children.Add(_heal);
        p.Children.Add(Row("Heal below HP %", _healAt, "Heal command", _healCommand));
        p.Children.Add(Row("Mana reserve %", _manaReserve, string.Empty, new TextBlock()));
        p.Children.Add(_cure);
        p.Children.Add(Row("Poison cure command", _cureCommand, string.Empty, new TextBlock()));
        p.Children.Add(_buff);
        p.Children.Add(Row("Party buff command", _buffCommand, "Target names (optional)", _buffTargets));
        p.Children.Add(Row("Buff interval (min)", _buffInterval, "Minimum mana %", _buffMinMana));
        p.Children.Add(_buffOutOfCombat);
        p.Children.Add(new Separator { Margin = new Thickness(0, 9, 0, 7) });
        p.Children.Add(_rank);
        p.Children.Add(Row("Preferred rank", _preferredRank, "Front command", _frontCommand));
        p.Children.Add(Row("Back command", _backCommand, string.Empty, new TextBlock()));
        p.Children.Add(new Separator { Margin = new Thickness(0, 9, 0, 7) });
        p.Children.Add(_invite);
        p.Children.Add(Row("Invite names", _inviteNames, "Invite command", _inviteCommand));
        p.Children.Add(Note("Invite automation only uses names you explicitly list and only after the fresh online-player roster confirms the exact name. No name is discovered or guessed from chat."));
        return p;
    }

    private StackPanel BuildSafetyTab()
    {
        var p = FormPanel();
        p.Children.Add(_pauseRoute);
        p.Children.Add(_resumeRoute);
        p.Children.Add(_resync);
        p.Children.Add(Row("Party state max age (s)", _partyAge, "WHO state max age (s)", _onlineAge));
        p.Children.Add(Row("Command cooldown (s)", _cooldown, "Post-action hold (s)", _hold));
        p.Children.Add(new Separator { Margin = new Thickness(0, 9, 0, 7) });
        p.Children.Add(Text("Remote party commands", FontWeights.SemiBold));
        p.Children.Add(Note("Trusted @ commands remain owned by RealmGate's existing Coordination profile. Movement, invite, query, and raw-command permissions are checked per controller there; this window does not create a second remote-command authority."));
        p.Children.Add(Note("Emergency Stop, verified-gameplay checks, and RealmGate private-protocol isolation remain mandatory. External BBS connections never receive CLIENTDATA/ROOMDATA text as player chat."));
        return p;
    }

    private void LoadControls()
    {
        var p = _profile.Preferences;
        _enabled.IsChecked = p.Enabled;
        _role.SelectedItem = p.Role;
        _leader.Text = p.LeaderName;
        _follow.IsChecked = p.AutoFollowLeader;
        _assist.IsChecked = p.AutoAssistLeader;
        _acceptInvites.IsChecked = p.AutoAcceptTrustedInvites;
        _acceptInviteCommand.Text = p.AcceptInviteCommandTemplate;
        _wait.IsChecked = p.WaitWhenLeaderMissing;
        _resume.IsChecked = p.ResumeWhenLeaderReturns;
        _regroup.IsChecked = p.RegroupWhenSeparated;
        _followCommand.Text = p.FollowCommandTemplate;
        _assistCommand.Text = p.AssistCommandTemplate;
        _waitCommand.Text = p.WaitCommand;
        _resumeCommand.Text = p.ResumeCommandTemplate;
        _regroupCommand.Text = p.RegroupCommand;
        _rescue.IsChecked = p.AutoRescueMembers;
        _rescueAt.Text = p.RescueBelowPercent.ToString();
        _rescueCommand.Text = p.RescueCommandTemplate;
        _heal.IsChecked = p.AutoHealMembers;
        _healAt.Text = p.HealBelowPercent.ToString();
        _healCommand.Text = p.HealCommandTemplate;
        _manaReserve.Text = p.ReserveManaPercent.ToString();
        _cure.IsChecked = p.CurePoisonedMembers;
        _cureCommand.Text = p.CurePoisonCommandTemplate;
        _buff.IsChecked = p.AutoBuffMembers;
        _buffCommand.Text = p.PartyBuffCommandTemplate;
        _buffTargets.Text = p.PartyBuffTargetNames;
        _buffInterval.Text = p.PartyBuffIntervalMinutes.ToString();
        _buffMinMana.Text = p.PartyBuffMinimumManaPercent.ToString();
        _buffOutOfCombat.IsChecked = p.PartyBuffOnlyOutOfCombat;
        _rank.IsChecked = p.MaintainPreferredRank;
        _preferredRank.SelectedItem = p.PreferredRank;
        _frontCommand.Text = p.FrontRankCommand;
        _backCommand.Text = p.BackRankCommand;
        _invite.IsChecked = p.AutoInviteNamedMembers;
        _inviteNames.Text = p.InviteNames;
        _inviteCommand.Text = p.InviteCommandTemplate;
        _pauseRoute.IsChecked = p.PauseRouteWhileLeaderMissing;
        _resumeRoute.IsChecked = p.ResumeRouteWhenLeaderReturns;
        _resync.IsChecked = p.ResyncAfterReconnect;
        _partyAge.Text = p.PartyStateMaximumAgeSeconds.ToString();
        _onlineAge.Text = p.OnlineStateMaximumAgeSeconds.ToString();
        _cooldown.Text = p.CommandCooldownSeconds.ToString();
        _hold.Text = p.ActionHoldSeconds.ToString();
    }

    private void CaptureControls()
    {
        var p = _profile.Preferences;
        p.Enabled = _enabled.IsChecked == true;
        if (_role.SelectedItem is ReforgedPartyRole role) p.Role = role;
        p.LeaderName = _leader.Text.Trim();
        p.AutoFollowLeader = _follow.IsChecked == true;
        p.AutoAssistLeader = _assist.IsChecked == true;
        p.AutoAcceptTrustedInvites = _acceptInvites.IsChecked == true;
        p.AcceptInviteCommandTemplate = _acceptInviteCommand.Text.Trim();
        p.WaitWhenLeaderMissing = _wait.IsChecked == true;
        p.ResumeWhenLeaderReturns = _resume.IsChecked == true;
        p.RegroupWhenSeparated = _regroup.IsChecked == true;
        p.FollowCommandTemplate = _followCommand.Text.Trim();
        p.AssistCommandTemplate = _assistCommand.Text.Trim();
        p.WaitCommand = _waitCommand.Text.Trim();
        p.ResumeCommandTemplate = _resumeCommand.Text.Trim();
        p.RegroupCommand = _regroupCommand.Text.Trim();
        p.AutoRescueMembers = _rescue.IsChecked == true;
        p.RescueBelowPercent = Int(_rescueAt, 40, 1, 100);
        p.RescueCommandTemplate = _rescueCommand.Text.Trim();
        p.AutoHealMembers = _heal.IsChecked == true;
        p.HealBelowPercent = Int(_healAt, 60, 1, 100);
        p.HealCommandTemplate = _healCommand.Text.Trim();
        p.ReserveManaPercent = Int(_manaReserve, 20, 0, 100);
        p.CurePoisonedMembers = _cure.IsChecked == true;
        p.CurePoisonCommandTemplate = _cureCommand.Text.Trim();
        p.AutoBuffMembers = _buff.IsChecked == true;
        p.PartyBuffCommandTemplate = _buffCommand.Text.Trim();
        p.PartyBuffTargetNames = _buffTargets.Text.Trim();
        p.PartyBuffIntervalMinutes = Int(_buffInterval, 10, 1, 1440);
        p.PartyBuffMinimumManaPercent = Int(_buffMinMana, 60, 0, 100);
        p.PartyBuffOnlyOutOfCombat = _buffOutOfCombat.IsChecked == true;
        p.MaintainPreferredRank = _rank.IsChecked == true;
        p.PreferredRank = _preferredRank.SelectedItem?.ToString() ?? "Backrank";
        p.FrontRankCommand = _frontCommand.Text.Trim();
        p.BackRankCommand = _backCommand.Text.Trim();
        p.AutoInviteNamedMembers = _invite.IsChecked == true;
        p.InviteNames = _inviteNames.Text.Trim();
        p.InviteCommandTemplate = _inviteCommand.Text.Trim();
        p.PauseRouteWhileLeaderMissing = _pauseRoute.IsChecked == true;
        p.ResumeRouteWhenLeaderReturns = _resumeRoute.IsChecked == true;
        p.ResyncAfterReconnect = _resync.IsChecked == true;
        p.PartyStateMaximumAgeSeconds = Int(_partyAge, 20, 2, 300);
        p.OnlineStateMaximumAgeSeconds = Int(_onlineAge, 30, 2, 300);
        p.CommandCooldownSeconds = Int(_cooldown, 4, 1, 60);
        p.ActionHoldSeconds = Int(_hold, 3, 1, 60);
    }

    private void LoadClassicControls()
    {
        var p = Session.ClassicPartyPreferences;
        _classicHelpBash.IsChecked = p.HelpLeaderBashDoors;
        _classicSayPanic.IsChecked = p.SayPanicWhenLeading;
        _classicIgnorePanic.IsChecked = p.IgnorePanicWhenFollowing;
        _classicIgnoreParty.IsChecked = p.IgnorePartyWhenFollowing;
        _classicIgnoreWait.IsChecked = p.IgnoreWaitWhenLeading;
        _classicMaxMonsters.Text = p.MaxMonstersWhenPartying.ToString();
        _classicMaxExperience.Text = p.MaxMonsterExperience.ToString();
        _classicWaitPercent.Text = p.WaitHealthPercent.ToString();
        _classicWaitMinutes.Text = p.LeaderWaitMinutes.ToString();
        _classicParFrequency.Text = p.PartyFrequencySeconds.ToString();
        _classicWaitCommand.Text = p.PartyWaitCommand;
        _classicResumeCommand.Text = p.PartyResumeCommand;
    }

    private void CaptureClassicControls()
    {
        var p = Session.ClassicPartyPreferences;
        p.HelpLeaderBashDoors = _classicHelpBash.IsChecked == true;
        p.SayPanicWhenLeading = _classicSayPanic.IsChecked == true;
        p.IgnorePanicWhenFollowing = _classicIgnorePanic.IsChecked == true;
        p.IgnorePartyWhenFollowing = _classicIgnoreParty.IsChecked == true;
        p.IgnoreWaitWhenLeading = _classicIgnoreWait.IsChecked == true;
        p.MaxMonstersWhenPartying = Int(_classicMaxMonsters, p.MaxMonstersWhenPartying, 0, 999);
        p.MaxMonsterExperience = Int(_classicMaxExperience, p.MaxMonsterExperience, 0, int.MaxValue);
        p.WaitHealthPercent = Int(_classicWaitPercent, p.WaitHealthPercent, 0, 100);
        p.LeaderWaitMinutes = Int(_classicWaitMinutes, p.LeaderWaitMinutes, 0, 1440);
        p.PartyFrequencySeconds = Int(_classicParFrequency, p.PartyFrequencySeconds, 1, 3600);
        p.PartyWaitCommand = _classicWaitCommand.Text.Trim();
        p.PartyResumeCommand = _classicResumeCommand.Text.Trim();
    }

    private void RefreshLive()
    {
        _context.Text = $"{Session.ServerProfileName} · {Session.WorldName} · {Session.CharacterName}";
        var leader = _profile.Preferences.LeaderName.Trim();
        var leaderState = leader.Length == 0 ? "no leader configured" :
            Session.PartyMembers.Any(m => m.Name.Equals(leader, StringComparison.OrdinalIgnoreCase)) ? $"leader {leader} present" : $"leader {leader} absent";
        _live.Text = $"Party {Session.PartyMembers.Count} · {leaderState} · HP {Session.HealthPercent}% · Mana {Session.ManaPercent}% · {(Session.InCombat ? "combat" : "not in combat")}\n{Session.PartyAutomationStatus}";
    }

    private static string DisplayCharacter(ClientSessionConnection session) =>
        string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;

    private static StackPanel FormPanel() => new() { Margin = new Thickness(10) };
    private static ScrollViewer Scroll(UIElement child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private static CheckBox Check(string text) => new() { Content = text, Margin = new Thickness(0, 4, 8, 4) };
    private static ComboBox Combo() => new() { MinWidth = 150, Margin = new Thickness(4) };
    private static TextBox Box() => new() { MinWidth = 180, Margin = new Thickness(4) };
    private static TextBox Small() => new() { Width = 70, Margin = new Thickness(4) };
    private static Button Button(string text, double width) => new() { Content = text, Width = width, Margin = new Thickness(4) };
    private static TextBlock Text(string text, FontWeight weight) => new() { Text = text, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2) };
    private static TextBlock Note(string text) => new() { Text = text, Opacity = .78, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 6, 2, 8) };

    private static UIElement Row(string labelA, UIElement controlA, string labelB, UIElement controlB)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var a = Text(labelA, FontWeights.Normal);
        a.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(a);
        Grid.SetColumn(controlA, 1); grid.Children.Add(controlA);
        if (labelB.Length > 0)
        {
            var b = Text(labelB, FontWeights.Normal);
            b.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(b, 2); grid.Children.Add(b);
            Grid.SetColumn(controlB, 3); grid.Children.Add(controlB);
        }
        return grid;
    }

    private static int Int(TextBox box, int fallback, int min, int max) =>
        int.TryParse(box.Text, out var value) ? Math.Clamp(value, min, max) : fallback;
}
