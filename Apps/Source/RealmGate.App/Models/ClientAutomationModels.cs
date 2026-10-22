using System.Collections.ObjectModel;
using RealmGate.Client.Automation;

namespace RealmGate.App.Models;

public enum AutomationArea
{
    Health, Spells, Combat, PvP, Party, Roaming, LootAndCash, Utility, Events
}

public sealed class ClientAutomationProfile
{
    public int SchemaVersion { get; set; }
    public string Name { get; set; } = "Default";
    public bool Enabled { get; set; }
    public bool PauseAll { get; set; }
    public int MinimumCommandDelayMs { get; set; } = 500;
    public ObservableCollection<ClientAutomationRule> Rules { get; set; } = [];
    public ClientAutomationOptions Options { get; set; } = new();
    public ClientAutomationCapabilities Capabilities { get; set; } = new();
}

public sealed class ClientAutomationCapabilities
{
    public bool AliasesEnabled { get; set; }
    public bool MacrosEnabled { get; set; }
    public bool TextTriggersEnabled { get; set; }
    public bool TimedEventsEnabled { get; set; }
    public bool NavigationEnabled { get; set; }
    public bool CombatEnabled { get; set; }
    public bool HealthRecoveryEnabled { get; set; }
    public bool SpellMaintenanceEnabled { get; set; }
    public bool PartyAutomationEnabled { get; set; }
    public bool LootAutomationEnabled { get; set; }
    public bool RoamingAutomationEnabled { get; set; }
    public bool PvpAutomationEnabled { get; set; }
    public bool CommunicationsAutomationEnabled { get; set; }
    public bool EmergencyStop { get; set; } = true;
    public bool AwayFromKeyboardEnabled { get; set; }
    public string AwayFromKeyboardMessage { get; set; } = "I am currently away from the keyboard and will reply when I return.";
    public int AwayFromKeyboardReplyCooldownSeconds { get; set; } = 300;
    public ObservableCollection<ClientCommandAlias> Aliases { get; set; } = [];
    public ObservableCollection<ClientCommandMacro> Macros { get; set; } = [];
    public ObservableCollection<ClientTextTrigger> TextTriggers { get; set; } = [];
}

public sealed class ClientAutomationOptions
{
    public HealthAutomationOptions Health { get; set; } = new();
    public SpellAutomationOptions Spells { get; set; } = new();
    public CombatAutomationOptions Combat { get; set; } = new();
    public PvpAutomationOptions PvP { get; set; } = new();
    public PartyAutomationOptions Party { get; set; } = new();
    public LootCashAutomationOptions LootAndCash { get; set; } = new();
    public RoamingAutomationOptions Roaming { get; set; } = new();
    public UtilityAutomationOptions Utility { get; set; } = new();
    public EventAutomationOptions Events { get; set; } = new();
    public ClientPresentationOptions Presentation { get; set; } = new();
    public ClientSoundOptions Sounds { get; set; } = new();
    public ObservableCollection<ClientToolbarOption> Toolbar { get; set; } = [];
    public ObservableCollection<ClientCommandKeyBindingOption> KeyBindings { get; set; } = [];
    public ObservableCollection<ClientContextCommandOption> ContextCommands { get; set; } = [];
}

public sealed class HealthAutomationOptions
{
    public bool AutoHeal { get; set; }
    public int MinorHealAtPercent { get; set; } = 80;
    public int MajorHealAtPercent { get; set; } = 55;
    public int FleeAtPercent { get; set; } = 25;
    public bool AutoRest { get; set; }
    public int RestManaAtPercent { get; set; } = 25;
    public bool AutoMeditate { get; set; }
    public bool CurePoison { get; set; }
    public string CurePoisonCommand { get; set; } = "cast cure poison";
    public bool CureDisease { get; set; }
    public string CureDiseaseCommand { get; set; } = "cast cure disease";
    public bool CureBlindness { get; set; }
    public string CureBlindnessCommand { get; set; } = "cast cure blindness";
    public bool AutoUseHealingItem { get; set; }
    public string HealingItemCommand { get; set; } = "use healing potion";
    public int HealingItemAtPercent { get; set; } = 35;
    public bool StopRestingWhenRecovered { get; set; } = true;
    public int StopRestingManaPercent { get; set; } = 90;
    public bool CureSilence { get; set; }
    public string CureSilenceCommand { get; set; } = "cast remove silence";
    public bool AutoRecallAtCriticalHealth { get; set; }
    public string RecallCommand { get; set; } = "recall";
    public int RecallAtPercent { get; set; } = 15;
    public bool AutoWake { get; set; }
    public string WakeCommand { get; set; } = "wake";
}

public sealed class SpellAutomationOptions
{
    public ObservableCollection<ClientPriorityAction> PriorityActions { get; set; } = [];
    public ObservableCollection<ClientPriorityAction> DebuffPriorityActions { get; set; } = [];
    public ObservableCollection<ClientPriorityAction> NukePriorityActions { get; set; } = [];
    public string MinorHealCommand { get; set; } = "cast minor heal";
    public string MajorHealCommand { get; set; } = "cast major heal";
    public string BlessCommand { get; set; } = "cast bless";
    public string ArmorCommand { get; set; } = "cast armor";
    public string LightCommand { get; set; } = "cast light";
    public bool MaintainBless { get; set; }
    public bool MaintainArmor { get; set; }
    public bool MaintainRoomLight { get; set; }
    public int ReserveManaPercent { get; set; } = 20;
    public bool AutoNuke { get; set; }
    public int NukeCooldownSeconds { get; set; } = 3;
    public int AreaNukeMinimumTargets { get; set; } = 2;
    public bool CastMaintainedSpellsOnlyOutOfCombat { get; set; } = true;
    public int MaintainedSpellCooldownSeconds { get; set; } = 120;
    public bool RetryFailedCast { get; set; }
    public string RetryFailedCastCommand { get; set; } = "cast";
    public bool RecastAfterDispel { get; set; }
    public string RecastAfterDispelCommand { get; set; } = "cast bless";
}

public sealed class CombatAutomationOptions
{
    public ObservableCollection<ClientPriorityAction> PriorityActions { get; set; } = [];
    public bool AutoAttack { get; set; }
    public bool AutoPreAttack { get; set; }
    public string PreAttackCommand { get; set; } = string.Empty;
    public string PrimaryAttackCommand { get; set; } = "attack";
    public string SecondaryAttackCommand { get; set; } = "bash";
    public bool UseSecondaryAttack { get; set; }
    public bool StopOnUnknownTarget { get; set; } = true;
    public bool StopOnPlayerTarget { get; set; } = true;
    public bool AutoFlee { get; set; }
    public int FleeAtPercent { get; set; } = 25;
    public bool AutoRetarget { get; set; }
    public string RetargetCommand { get; set; } = "attack";
    public bool AutoRescueParty { get; set; }
    public string RescueCommand { get; set; } = "rescue";
    public bool UseDefensiveActionAtLowHealth { get; set; }
    public string DefensiveActionCommand { get; set; } = "defend";
    public int DefensiveActionAtPercent { get; set; } = 40;
    public bool EscapeAtLowMana { get; set; }
    public string LowManaEscapeCommand { get; set; } = "flee";
    public int LowManaEscapeAtPercent { get; set; } = 10;
}

public sealed class PvpAutomationOptions
{
    public bool Enabled { get; set; }
    public bool AlertWhenAttacked { get; set; } = true;
    public bool AutoFlee { get; set; }
      public bool AutoReturnAttack { get; set; }
      public bool DefendPartyMembers { get; set; }
    public string ReactionCommand { get; set; } = "flee";
    public bool IgnoreTrustedPlayers { get; set; } = true;
    public string ReturnAttackCommand { get; set; } = "attack";
    public bool ChaseFleeingAttacker { get; set; }
    public string ChaseCommand { get; set; } = "chase";
    public int FleeAtPercent { get; set; } = 35;
    public int ReturnAttackAtOrAbovePercent { get; set; } = 50;
    public int CommandCooldownSeconds { get; set; } = 3;
    public int AttackerStateMaximumAgeSeconds { get; set; } = 10;
      public bool UseDefensiveAction { get; set; }
    public string DefensiveActionCommand { get; set; } = "defend";
}

public sealed class PartyAutomationOptions
{
    public bool AutoAssistLeader { get; set; }
    public bool AutoFollowLeader { get; set; }
    public bool AutoAcceptTrustedInvites { get; set; }
    public bool AutoHealParty { get; set; }
    public int HealPartyBelowPercent { get; set; } = 60;
    public string LeaderName { get; set; } = string.Empty;
    public string AssistCommand { get; set; } = "assist";
    public string FollowCommand { get; set; } = "follow";
    public string AcceptInviteCommand { get; set; } = "follow";
    public string HealPartyCommand { get; set; } = "cast heal";
    public bool RegroupWhenSeparated { get; set; }
    public string RegroupCommand { get; set; } = "where";
    public bool WaitWhenLeaderMissing { get; set; }
    public string WaitCommand { get; set; } = "wait";
    public bool ResumeWhenLeaderReturns { get; set; }
    public string ResumeCommand { get; set; } = "follow";
    public int PartyStateMaximumAgeSeconds { get; set; } = 30;
    public int CommandCooldownSeconds { get; set; } = 5;
}

public sealed class LootCashAutomationOptions
{
    public ObservableCollection<ClientCurrencyRule> CurrencyRules { get; set; } = [];
    public bool GetCash { get; set; }
    public bool GetItems { get; set; }
    public bool LootCorpses { get; set; }
    public bool DepositAtBank { get; set; }
    public long DepositWhenCarrying { get; set; } = 5000;
    public long KeepOnCharacter { get; set; } = 500;
    public bool IgnoreHeavyItems { get; set; }
    public int MaximumItemWeight { get; set; } = 50;
    public string IncludeWords { get; set; } = string.Empty;
    public string ExcludeWords { get; set; } = string.Empty;
    public string GetCashCommand { get; set; } = "get coins";
    public string GetItemsCommand { get; set; } = "get {item}";
    public string LootCorpseCommand { get; set; } = "get all corpse";
    public string DepositCommand { get; set; } = "deposit all";
    public bool ConvertCurrencyAtBank { get; set; }
    public string ConvertCurrencyCommand { get; set; } = "exchange all";
    public int StopLootingAtWeightPercent { get; set; } = 90;
    public bool AutoSellAtShop { get; set; }
    public string SellCommand { get; set; } = "sell {item}";
    public string SellIncludeWords { get; set; } = string.Empty;
    public string SellExcludeWords { get; set; } = string.Empty;
    public bool DropWhenOverweight { get; set; }
    public string DropOverweightCommand { get; set; } = "drop heavy";
}

public sealed class ClientPriorityAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string Name { get; set; } = "New Action";
    public string Command { get; set; } = string.Empty;
    public int MinimumHealthPercent { get; set; }
    public int MinimumManaPercent { get; set; }
    public int MinimumTargetCount { get; set; } = 1;
    public int MaximumTargetCount { get; set; } = 99;
    public bool IsAreaAction { get; set; }
    public bool AppendTarget { get; set; } = true;
    public string FallbackCommand { get; set; } = string.Empty;
    public bool OnlyInCombat { get; set; }
    public bool OnlyOutOfCombat { get; set; }
}

public sealed class ClientCurrencyRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Gold";
    public string RecognitionWords { get; set; } = "gold";
    public bool PickUp { get; set; } = true;
    public bool Deposit { get; set; } = true;
    public long KeepAmount { get; set; }
    public int SortOrder { get; set; }
}


public sealed class RoamingAutomationOptions
{
    public bool AutoSearch { get; set; }
    public string SearchCommand { get; set; } = "search";
    public bool AutoSneak { get; set; }
    public string SneakCommand { get; set; } = "sneak";
    public bool AutoTrack { get; set; }
    public string TrackCommand { get; set; } = "track";
    public bool OpenClosedDoors { get; set; }
    public string OpenDoorCommand { get; set; } = "open {direction}";
    public bool PickLockedDoors { get; set; }
    public string PickLockCommand { get; set; } = "pick {direction}";
    public bool BashBlockedDoors { get; set; }
    public string BashDoorCommand { get; set; } = "bash {direction}";
    public bool DetectTraps { get; set; }
    public string DetectTrapCommand { get; set; } = "detect traps";
    public bool DisarmTraps { get; set; }
    public string DisarmTrapCommand { get; set; } = "disarm trap";
    public bool AutoRecoverDesynchronizedRoute { get; set; }
    public int RoomActionDelayMilliseconds { get; set; } = 750;
}

public sealed class UtilityAutomationOptions
{
    public bool AutoHide { get; set; }
    public string HideCommand { get; set; } = "hide";
    public bool AutoStand { get; set; }
    public string StandCommand { get; set; } = "stand";
    public bool AutoRecover { get; set; }
    public string RecoverCommand { get; set; } = "recover";
    public bool AutoLook { get; set; }
    public string LookCommand { get; set; } = "look";
}

public sealed class ClientPresentationOptions
{
    public string Theme { get; set; } = "Classic";
    public string FontFamily { get; set; } = "Consolas";
    public int FontSize { get; set; } = 14;
    public bool ShowTimestamps { get; set; }
    public bool HighlightPlayers { get; set; } = true;
    public bool HighlightMonsters { get; set; } = true;
    public bool HighlightLoot { get; set; } = true;
    public bool ReduceAnimations { get; set; }
    public bool HighContrast { get; set; }
    public int OutputScalePercent { get; set; } = 100;
    public bool ShowToolbar { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool AutoFocusTerminal { get; set; } = true;
    public bool EnableCommandHotkeys { get; set; } = true;
    public bool EnableCustomContextMenu { get; set; } = true;
}

public sealed class ClientSoundOptions
{
    public bool Enabled { get; set; } = true;
    public bool TellSound { get; set; } = true;
    public bool CombatSound { get; set; }
    public bool LowHealthSound { get; set; } = true;
    public bool RouteCompleteSound { get; set; } = true;
    public bool DisconnectSound { get; set; } = true;
    public int VolumePercent { get; set; } = 70;
}

public sealed class ClientToolbarOption
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string AutomationArea { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public int Order { get; set; }
}

public sealed class ClientCommandKeyBindingOption
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "Command hotkey";
    public string Gesture { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Order { get; set; }
}

public sealed class ClientContextCommandOption
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "Command";
    public string Command { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public int Order { get; set; }
}


public sealed class ClientAutomationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public AutomationArea Area { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int Threshold { get; set; }
    public int Priority { get; set; } = 100;
    public bool HostPermissionRequired { get; set; } = true;
    public int CooldownSeconds { get; set; } = 5;
    public bool OnlyOutOfCombat { get; set; }
    public bool OnlyInCombat { get; set; }
    public int MinimumHealthPercent { get; set; }
    public int MinimumManaPercent { get; set; }
}

public sealed record ClientAutomationContext(
    int HealthPercent,
    int ManaPercent,
    bool InCombat,
    bool IsResting,
    bool IsMoving,
    string LastLine,
    DateTimeOffset Timestamp,
    string SessionId = "");

public sealed record ClientAutomationCommandRequest(
    string RuleId,
    string RuleName,
    string Command,
    AutomationArea Area,
    DateTimeOffset CreatedAt,
    string SessionId = "",
    IReadOnlyList<string>? CommandBatch = null)
{
    public IReadOnlyList<string> Commands => CommandBatch is { Count: > 0 } ? CommandBatch : [Command];
}

public sealed class ClientAutomationPolicy
{
    public bool AutomationAllowed { get; set; } = true;
    public bool MovementAllowed { get; set; } = true;
    public bool CombatAllowed { get; set; } = true;
    public bool SpellcastingAllowed { get; set; } = true;
    public bool ItemUseAllowed { get; set; } = true;
    public int MinimumCommandDelayMs { get; set; } = 500;
}
