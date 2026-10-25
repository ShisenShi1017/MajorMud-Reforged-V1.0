using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace RealmGate.Client.Gameplay;

public enum ClientCombatTargetMode
{
    FirstObserved,
    LowestHealthPercent,
    LowestHealth,
    LowestLevel,
    HighestLevel,
    HighestExperience
}

public enum ClientGameplayActionKind
{
    None,
    RefreshState,
    Emergency,
    Cure,
    Heal,
    RestoreMana,
    MaintainBuff,
    Rest,
    Engage,
    CombatAction,
    Follow,
    Assist,
    PartyHeal
}

public sealed record ClientGameplayEffectState(
    string EffectId,
    string SpellId,
    string Name,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record ClientGameplayNpcState(
    string InstanceId,
    string NpcId,
    string Name,
    int CurrentHitPoints,
    int MaximumHitPoints,
    bool Alive,
    string Disposition,
    int Level,
    long ExperienceReward)
{
    public int HealthPercent => MaximumHitPoints <= 0
        ? 0
        : (int)Math.Clamp((long)CurrentHitPoints * 100 / MaximumHitPoints, 0, 100);
    public string DisplayName => $"{Name} - L{Level} - {HealthPercent}% HP - {Disposition}";
}

public sealed record ClientGameplayPlayerState(
    string CharacterId,
    string Name,
    string RoomId,
    int Level);

public sealed record ClientGameplayPartyMemberState(
    string CharacterId,
    string Name,
    string RoomId,
    bool Online,
    bool Invited,
    bool IsLeader,
    string Rank,
    int HealthPercent,
    int ManaPercent,
    IReadOnlyList<string> Conditions)
{
    public bool InCombat { get; init; }
    public string DisplayName =>
        $"{Name} - {HealthPercent}% HP - {ManaPercent}% resource - {Rank}";
}

public sealed record ClientGameplayAutomationSnapshot(
    string WorldId,
    string CharacterId,
    string CharacterName,
    string RoomId,
    int Level,
    int HealthCurrent,
    int HealthMaximum,
    int ManaCurrent,
    int ManaMaximum,
    [property: JsonRequired] bool InCombat,
    [property: JsonRequired] bool IsResting,
    [property: JsonRequired] IReadOnlyList<string> Conditions,
    [property: JsonRequired] IReadOnlyList<string> LearnedSpells,
    [property: JsonRequired] IReadOnlyList<ClientGameplayEffectState> ActiveEffects,
    [property: JsonRequired] IReadOnlyList<ClientGameplayNpcState> Npcs,
    [property: JsonRequired] IReadOnlyList<ClientGameplayPlayerState> Players,
    [property: JsonRequired] IReadOnlyList<ClientGameplayPartyMemberState> PartyMembers,
    [property: JsonRequired] DateTimeOffset CapturedAtUtc)
{
    public bool CanMeditate { get; init; }
    public bool InPlayerCombat { get; init; }
    public int HealthPercent => HealthMaximum <= 0
        ? 0
        : (int)Math.Clamp((long)HealthCurrent * 100 / HealthMaximum, 0, 100);
    public int ManaPercent => ManaMaximum <= 0
        ? 100
        : (int)Math.Clamp((long)ManaCurrent * 100 / ManaMaximum, 0, 100);
}

public sealed class ClientBuffRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string Name { get; set; } = "Buff";
    public string EffectName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int MinimumManaPercent { get; set; } = 25;
    public bool OnlyOutOfCombat { get; set; } = true;
    public string DisplayName => $"{Priority:0000} - {Name}";
}

public sealed class ClientRecoveryAutomationPreferences
{
    public bool Enabled { get; set; }
    public int EmergencyHealthPercent { get; set; } = 15;
    public string EmergencyCommand { get; set; } = "flee";
    public bool CurePoison { get; set; }
    public string CurePoisonCommand { get; set; } = "cast antidote";
    public int HealBelowPercent { get; set; } = 45;
    public string HealCommand { get; set; } = string.Empty;
    public int RestBelowHealthPercent { get; set; } = 70;
    public int MeditateBelowManaPercent { get; set; } = 35;
    public string RestCommand { get; set; } = "rest";
    public string MeditateCommand { get; set; } = "meditate";
    public bool MaintainBuffs { get; set; }
    public bool RequireLearnedSpellForCastCommands { get; set; } = true;
}

public sealed class ClientCombatAutomationPreferences
{
    public bool Enabled { get; set; }
    public ClientCombatTargetMode TargetMode { get; set; } = ClientCombatTargetMode.FirstObserved;
    public string EngageCommand { get; set; } = "kill {target}";
    public string RoundCommand { get; set; } = string.Empty;
    public string EscapeCommand { get; set; } = "flee";
    public int EscapeBelowHealthPercent { get; set; } = 20;
    public int MinimumManaPercent { get; set; } = 15;
    public int MaximumHostiles { get; set; } = 3;
    public int MaximumTargetLevelAboveCharacter { get; set; } = 3;
    public bool StopWhenOtherPlayersPresent { get; set; } = true;
    public bool RequireLearnedSpellForCastCommands { get; set; } = true;
    public string AvoidNames { get; set; } = string.Empty;
}

public sealed class ClientPartyAutomationPreferences
{
    public bool Enabled { get; set; }
    public string LeaderName { get; set; } = string.Empty;
    public bool AutoFollowLeader { get; set; }
    public bool AutoAssistLeader { get; set; }
    public bool AutoHealMembers { get; set; }
    public int HealBelowPercent { get; set; } = 40;
    public string FollowCommand { get; set; } = "follow {target}";
    public string AssistCommand { get; set; } = "assist {target}";
    public string HealCommand { get; set; } = string.Empty;
    public bool RequireLearnedSpellForCastCommands { get; set; } = true;
}

public sealed class ClientGameplayAutomationProfile
{
    public int Version { get; set; } = 1;
    public string ProfileKey { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public int StateMaximumAgeSeconds { get; set; } = 8;
    public int MinimumActionDelayMilliseconds { get; set; } = 1500;
    public ClientRecoveryAutomationPreferences Recovery { get; set; } = new();
    public ClientCombatAutomationPreferences Combat { get; set; } = new();
    public ClientPartyAutomationPreferences Party { get; set; } = new();
    public ObservableCollection<ClientBuffRule> Buffs { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ClientGameplayAutomationCatalog
{
    public int Version { get; set; } = 1;
    public ObservableCollection<ClientGameplayAutomationProfile> Profiles { get; set; } = [];
}

public sealed record ClientGameplayActionPlan(
    ClientGameplayActionKind Kind,
    string Command,
    string Reason,
    string Target = "")
{
    public bool HasAction => Kind is not ClientGameplayActionKind.None and not ClientGameplayActionKind.RefreshState
                             && !string.IsNullOrWhiteSpace(Command);
    public static ClientGameplayActionPlan None(string reason) =>
        new(ClientGameplayActionKind.None, string.Empty, reason);
    public static ClientGameplayActionPlan Refresh(string reason) =>
        new(ClientGameplayActionKind.RefreshState, string.Empty, reason);
}

public sealed record ClientDiagnosticCheck(
    string Name,
    bool Passed,
    string Details)
{
    public string DisplayName => $"{(Passed ? "PASS" : "CHECK")} - {Name}: {Details}";
}
