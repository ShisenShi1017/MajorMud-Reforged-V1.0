using System.Collections.ObjectModel;

namespace Reforged.App.Models;

public enum ReforgedCombatTargetMode
{
    FirstObserved,
    LowestHealthPercent,
    LowestHealth,
    LowestLevel,
    HighestLevel,
    HighestExperience
}

public enum ReforgedCombatActionKind
{
    None,
    RefreshRoom,
    EquipSet,
    PreAttack,
    OpeningAction,
    Engage,
    RoundAction,
    DefensiveAction,
    Escape
}

public sealed class ReforgedCombatActionRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string Name { get; set; } = "Combat action";
    public string Command { get; set; } = string.Empty;
    public bool AppendTarget { get; set; } = true;
    public bool OpeningOnly { get; set; }
    public bool InCombatOnly { get; set; } = true;
    public bool IsAreaAction { get; set; }
    public int MinimumTargets { get; set; } = 1;
    public int MaximumTargets { get; set; } = 99;
    public int MinimumHealthPercent { get; set; }
    public int MinimumManaPercent { get; set; }
    public int CooldownSeconds { get; set; } = 3;
    public string TargetNameContains { get; set; } = string.Empty;
    public string EquipmentSetId { get; set; } = string.Empty;
    public string FallbackCommand { get; set; } = string.Empty;

    public string DisplayName => $"{Priority:0000} • {(Enabled ? "✓" : "—")} {Name}";
}

public sealed class ReforgedCombatAutomationPreferences
{
    public bool Enabled { get; set; }
    public int StateMaximumAgeSeconds { get; set; } = 8;
    public int EngagementCooldownSeconds { get; set; } = 3;
    public int RoundMinimumDelaySeconds { get; set; } = 1;
    public int ManaReservePercent { get; set; } = 20;
    public bool RequireLearnedSpellForCastCommands { get; set; } = true;

    public ReforgedCombatTargetMode TargetMode { get; set; } = ReforgedCombatTargetMode.FirstObserved;
    public string PriorityTargetWords { get; set; } = string.Empty;
    public string AvoidTargetWords { get; set; } = string.Empty;
    public bool StopWhenPlayersPresent { get; set; }
    public int MaximumHostiles { get; set; } = 20;
    public long MaximumTotalExperience { get; set; } = 2_000_000_000;
    public int MaximumTargetLevelAboveCharacter { get; set; } = 50;

    public bool AutoPreAttack { get; set; }
    public string PreAttackCommand { get; set; } = string.Empty;
    public string EngageCommand { get; set; } = "attack {target}";
    public bool AutoRetarget { get; set; } = true;

    public string DefaultEquipmentSetId { get; set; } = string.Empty;
    public string MultiTargetEquipmentSetId { get; set; } = string.Empty;
    public int MultiTargetEquipmentSetAtCount { get; set; } = 3;
    public string DefensiveEquipmentSetId { get; set; } = string.Empty;
    public int DefensiveEquipmentSetAtHealthPercent { get; set; } = 40;

    public bool AutoDefensiveAction { get; set; }
    public int DefensiveActionAtHealthPercent { get; set; } = 40;
    public string DefensiveActionCommand { get; set; } = "defend";
    public int DefensiveActionCooldownSeconds { get; set; } = 5;

    public bool AutoEscape { get; set; }
    public int EscapeAtHealthPercent { get; set; } = 25;
    public bool EscapeAtLowMana { get; set; }
    public int EscapeAtManaPercent { get; set; } = 8;
    public bool EscapeWhenOverwhelmed { get; set; }
    public int EscapeAtHostileCount { get; set; } = 8;
    public string EscapeCommand { get; set; } = "flee";
    public int EscapeCooldownSeconds { get; set; } = 4;

    public bool StopAfterActionFailure { get; set; }
    public int FailedActionHoldSeconds { get; set; } = 8;
}

public sealed class ReforgedCombatAutomationProfile
{
    public int Version { get; set; } = 1;
    public string ProfileKey { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string ServerProfileName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public ReforgedCombatAutomationPreferences Preferences { get; set; } = new();
    public ObservableCollection<ReforgedCombatActionRule> Actions { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ReforgedCombatAutomationPlan
{
    public ReforgedCombatActionKind Kind { get; init; }
    public string Command { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string ActionId { get; init; } = string.Empty;
    public string ActionName { get; init; } = string.Empty;
    public string EquipmentSetId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string FallbackCommand { get; init; } = string.Empty;
    public int CooldownSeconds { get; init; } = 2;
    public bool RefreshRoom { get; init; }
    public bool IsSpellCommand { get; init; }
    public bool HasAction => Kind != ReforgedCombatActionKind.None;
    public string Summary => string.IsNullOrWhiteSpace(Reason) ? Kind.ToString() : Reason;
}
