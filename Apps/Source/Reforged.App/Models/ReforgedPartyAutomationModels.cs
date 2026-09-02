using System.Collections.ObjectModel;

namespace Reforged.App.Models;

public enum ReforgedPartyRole
{
    FollowLeader,
    LeadParty,
    SupportOnly
}

public enum ReforgedPartyActionKind
{
    None,
    RefreshParty,
    RefreshOnlinePlayers,
    WaitForLeader,
    ResumeWithLeader,
    FollowLeader,
    AssistLeader,
    Regroup,
    RescueMember,
    HealMember,
    CureMember,
    BuffMember,
    SetRank,
    InviteMember
}

public sealed class ReforgedPartyAutomationPreferences
{
    public bool Enabled { get; set; }
    public ReforgedPartyRole Role { get; set; } = ReforgedPartyRole.FollowLeader;
    public string LeaderName { get; set; } = string.Empty;

    public bool AutoFollowLeader { get; set; } = true;
    public bool AutoAssistLeader { get; set; } = true;
    public bool AutoAcceptTrustedInvites { get; set; }
    public string AcceptInviteCommandTemplate { get; set; } = "follow {target}";
    public bool WaitWhenLeaderMissing { get; set; } = true;
    public bool ResumeWhenLeaderReturns { get; set; } = true;
    public bool RegroupWhenSeparated { get; set; }
    public string FollowCommandTemplate { get; set; } = "follow {leader}";
    public string AssistCommandTemplate { get; set; } = "assist {leader}";
    public string WaitCommand { get; set; } = "wait";
    public string ResumeCommandTemplate { get; set; } = "follow {leader}";
    public string RegroupCommand { get; set; } = "where";

    public bool AutoRescueMembers { get; set; }
    public int RescueBelowPercent { get; set; } = 40;
    public string RescueCommandTemplate { get; set; } = "rescue {target}";
    public bool AutoHealMembers { get; set; }
    public int HealBelowPercent { get; set; } = 60;
    public int ReserveManaPercent { get; set; } = 20;
    public string HealCommandTemplate { get; set; } = "cast heal {target}";
    public bool CurePoisonedMembers { get; set; }
    public string CurePoisonCommandTemplate { get; set; } = "cast cure poison {target}";
    public bool AutoBuffMembers { get; set; }
    public string PartyBuffCommandTemplate { get; set; } = "cast bless {target}";
    public string PartyBuffTargetNames { get; set; } = string.Empty;
    public int PartyBuffIntervalMinutes { get; set; } = 10;
    public int PartyBuffMinimumManaPercent { get; set; } = 60;
    public bool PartyBuffOnlyOutOfCombat { get; set; } = true;

    public bool MaintainPreferredRank { get; set; }
    public string PreferredRank { get; set; } = "Backrank";
    public string FrontRankCommand { get; set; } = "frontrank";
    public string BackRankCommand { get; set; } = "backrank";

    public bool AutoInviteNamedMembers { get; set; }
    public string InviteNames { get; set; } = string.Empty;
    public string InviteCommandTemplate { get; set; } = "invite {target}";
    public int OnlineStateMaximumAgeSeconds { get; set; } = 30;

    public bool PauseRouteWhileLeaderMissing { get; set; } = true;
    public bool ResumeRouteWhenLeaderReturns { get; set; } = true;
    public bool ResyncAfterReconnect { get; set; } = true;
    public int PartyStateMaximumAgeSeconds { get; set; } = 20;
    public int CommandCooldownSeconds { get; set; } = 4;
    public int ActionHoldSeconds { get; set; } = 3;
}

public sealed class ReforgedPartyAutomationProfile
{
    public int SchemaVersion { get; set; } = 1;
    public string ProfileKey { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string ServerProfileName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ReforgedPartyAutomationPreferences Preferences { get; set; } = new();
    public ObservableCollection<string> Notes { get; set; } = [];
}

public sealed class ReforgedPartyAutomationPlan
{
    public ReforgedPartyActionKind Kind { get; init; }
    public string Command { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool RefreshParty { get; init; }
    public bool RefreshOnlinePlayers { get; init; }
    public bool PauseRoute { get; init; }
    public bool ResumeRoute { get; init; }
    public bool IsSpellCommand { get; init; }
    public int CooldownSeconds { get; init; }
    public bool HasAction => Kind != ReforgedPartyActionKind.None;
    public bool NeedsRefresh => RefreshParty || RefreshOnlinePlayers;
    public string Summary => string.IsNullOrWhiteSpace(Reason) ? Kind.ToString() : Reason;
}
