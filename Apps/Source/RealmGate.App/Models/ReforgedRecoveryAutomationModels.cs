using System.Collections.ObjectModel;

namespace RealmGate.App.Models;

public enum ReforgedRecoveryActionKind
{
    None,
    RefreshVitals,
    RefreshEffects,
    RefreshSpellbook,
    RefreshParty,
    EmergencyRecall,
    EmergencyItem,
    CureSelf,
    HealSelf,
    HealParty,
    CureParty,
    MaintainBuff,
    BeginRest,
    BeginMeditate,
    FinishRecovery
}

public sealed class ReforgedBuffRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string Name { get; set; } = "Buff";
    public string EffectMatch { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int RefreshLeadSeconds { get; set; } = 15;
    public int MinimumManaPercent { get; set; } = 25;
    public bool OnlyOutOfCombat { get; set; } = true;

    public string DisplayName => $"{Priority:0000} • {(Enabled ? "✓" : "—")} {Name}";
}

public sealed class ReforgedRecoveryAutomationPreferences
{
    public bool Enabled { get; set; }
    public int StateMaximumAgeSeconds { get; set; } = 20;
    public int CommandCooldownSeconds { get; set; } = 4;
    public bool RequireLearnedSpellForCastCommands { get; set; } = true;
    public int ReserveManaPercent { get; set; } = 20;

    public bool AutoHealSelf { get; set; } = true;
    public int MinorHealAtPercent { get; set; } = 80;
    public int MajorHealAtPercent { get; set; } = 55;
    public string MinorHealCommand { get; set; } = "cast minor heal";
    public string MajorHealCommand { get; set; } = "cast major heal";
    public bool AllowSelfHealingInCombat { get; set; } = true;

    public bool AutoUseEmergencyHealingItem { get; set; }
    public int EmergencyHealingItemAtPercent { get; set; } = 30;
    public string EmergencyHealingItemCommand { get; set; } = "use healing potion";
    public bool AutoRecallAtCriticalHealth { get; set; }
    public int RecallAtPercent { get; set; } = 15;
    public string RecallCommand { get; set; } = "recall";

    public bool CurePoison { get; set; }
    public string CurePoisonCommand { get; set; } = "cast cure poison";
    public bool CureDisease { get; set; }
    public string CureDiseaseCommand { get; set; } = "cast cure disease";
    public bool CureBlindness { get; set; }
    public string CureBlindnessCommand { get; set; } = "cast cure blindness";
    public bool CureSilence { get; set; }
    public string CureSilenceCommand { get; set; } = string.Empty;

    public bool AutoRest { get; set; } = true;
    public bool AutoMeditate { get; set; }
    public int BeginRecoveryHealthPercent { get; set; } = 65;
    public int BeginRecoveryManaPercent { get; set; } = 25;
    public int FinishRecoveryHealthPercent { get; set; } = 95;
    public int FinishRecoveryManaPercent { get; set; } = 90;
    public string RestCommand { get; set; } = "rest";
    public string MeditateCommand { get; set; } = "meditate";
    public string StandCommand { get; set; } = "stand";
    public string PreRecoveryEquipmentSetId { get; set; } = string.Empty;
    public string PostRecoveryEquipmentSetId { get; set; } = string.Empty;

    public bool AutoHealParty { get; set; }
    public int HealPartyBelowPercent { get; set; } = 60;
    public string HealPartyCommandTemplate { get; set; } = "cast heal {target}";
    public bool CurePoisonedPartyMembers { get; set; }
    public string CurePartyPoisonCommandTemplate { get; set; } = "cast cure poison {target}";
    public int PartyStateMaximumAgeSeconds { get; set; } = 30;
}

public sealed class ReforgedRecoveryAutomationProfile
{
    public int Version { get; set; } = 1;
    public string ProfileKey { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string ServerProfileName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public ReforgedRecoveryAutomationPreferences Preferences { get; set; } = new();
    public ObservableCollection<ReforgedBuffRule> BuffRules { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ReforgedRecoveryAutomationPlan
{
    public ReforgedRecoveryActionKind Kind { get; init; }
    public string Command { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string PreEquipmentSetId { get; init; } = string.Empty;
    public string PostEquipmentSetId { get; init; } = string.Empty;
    public int CooldownSeconds { get; init; } = 4;
    public bool RefreshVitals { get; init; }
    public bool RefreshEffects { get; init; }
    public bool RefreshSpellbook { get; init; }
    public bool RefreshParty { get; init; }
    public bool IsSpellCommand { get; init; }
    public bool HasAction => Kind != ReforgedRecoveryActionKind.None;
    public bool NeedsRefresh => RefreshVitals || RefreshEffects || RefreshSpellbook || RefreshParty;
    public string Summary => string.IsNullOrWhiteSpace(Reason) ? Kind.ToString() : Reason;
}
