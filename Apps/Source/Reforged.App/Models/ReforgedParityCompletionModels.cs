namespace Reforged.App.Models;

public sealed record ReforgedConversationRow(
    DateTimeOffset Timestamp,
    string Channel,
    string Direction,
    string Text,
    ClientTerminalChannel SourceChannel);

public sealed record ReforgedCreatureAttackIntel(
    int Slot,
    string AttackType,
    int MinimumDamage,
    int MaximumDamage,
    int Accuracy,
    int SelectionPercent,
    int EnergyCost,
    string WeaponName)
{
    public string DamageRange => MaximumDamage > 0 ? $"{MinimumDamage}-{MaximumDamage}" : "Unknown";
}

public sealed record ReforgedCreatureIntelEntry(
    string Id,
    string Name,
    int Level,
    int HitPoints,
    int Energy,
    int ArmorClass,
    int DamageResistance,
    int MagicResistance,
    long ExperienceReward,
    bool Undead,
    string Alignment,
    int RespawnHours,
    int AttackCount,
    int MaximumSingleHit,
    int MaximumProfileDamage,
    IReadOnlyList<ReforgedCreatureAttackIntel> Attacks,
    IReadOnlyList<string> Abilities,
    bool IsInCurrentRoom,
    int? LiveHitPoints,
    int? LiveMaximumHitPoints,
    string LiveDisposition,
    string Confidence)
{
    public string LiveState => IsInCurrentRoom
        ? LiveMaximumHitPoints is > 0
            ? $"Here · HP {LiveHitPoints.GetValueOrDefault()}/{LiveMaximumHitPoints.Value}"
            : "Here"
        : string.Empty;

    public string AttackSummary => Attacks.Count == 0
        ? "No decoded attack profile"
        : $"{Attacks.Count} attack(s) · max hit {MaximumSingleHit:N0} · profile max {MaximumProfileDamage:N0}";
}

public sealed class ReforgedBugReportRequest
{
    public string Area { get; set; } = "Client";
    public string Summary { get; set; } = string.Empty;
    public string WhatHappened { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Steps { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IncludeRecentScrollback { get; set; } = true;
    public bool IncludeInventoryNames { get; set; } = true;
    public int MaximumScrollbackLines { get; set; } = 750;
}
