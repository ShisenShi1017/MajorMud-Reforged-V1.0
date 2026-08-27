namespace Reforged.Server;

public enum RuntimeClassicWorldRulesConfigurationEvidenceState
{
    Proven,
    Strong,
    Unknown,
    Excluded
}

public sealed record RuntimeClassicWorldRulesConfigurationEvidenceRow(
    string Id,
    RuntimeClassicWorldRulesConfigurationEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Pass 42 promotes the broad Classic world-rules/configuration ownership boundary without
/// inventing WCCMMUD.INI parsing, undocumented ranges, or live-reload semantics.  The retained
/// WCCMMUD.mcv catalog proves the native setup surface and exact defaults; six recovered defaults
/// are already consumed by live Classic runtime owners.
/// </summary>
public static class RuntimeClassicWorldRulesConfigurationEvidenceRegistry
{
    public const int RetainedNativePromptCount = 44;
    public const int ActiveExactDefaultProjectionCount = 6;

    public static IReadOnlyList<RuntimeClassicWorldRulesConfigurationEvidenceRow> Rows { get; } =
    [
        new("native.setup-source", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "WCCMMUD.mcv retains 44 printable MajorMUD setup prompts/defaults for the native WCCMMUD.INI configuration surface.",
            "Native support catalog SHA-pins WCCMMUD.mcv and the exact 44 prompt strings/offsets."),
        new("reforged.no-legacy-ini", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "Reforged Classic does not ship, create, read, or write WCCMMUD.INI.",
            "Native INI is evidence only; proven values are projected into Reforged-owned typed/runtime configuration."),
        new("projection.player-save-60", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact 60-second native Player-data save default is consumed by RuntimeClassicTimingAuthority.",
            "WCCMMUD.mcv prompt at offset 264 -> RuntimeClassicConfigurationAuthority.PlayerDataSaveIntervalSeconds."),
        new("projection.buffer-save-300", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact 300-second native Buffer-data save default is consumed by RuntimeClassicTimingAuthority.",
            "WCCMMUD.mcv prompt at offset 303 -> RuntimeClassicConfigurationAuthority.BufferDataSaveIntervalSeconds."),
        new("projection.pvp-difference-100", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact native maximum PvP level-difference default of 100 is consumed by the Classic PvP store.",
            "WCCMMUD.mcv prompt at offset 1363 -> RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference."),
        new("projection.ep-forgive-day-18", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact native maximum Evil Points forgiven per day default is 18 and is consumed by the alignment authority.",
            "WCCMMUD.mcv prompt at offset 1761 -> RuntimeCharacterAlignmentAuthority."),
        new("projection.ep-cycle-60", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact native Evil Point cycle default is 60 minutes and is consumed by the alignment authority.",
            "WCCMMUD.mcv prompt at offset 1792 -> RuntimeCharacterAlignmentAuthority."),
        new("projection.ep-per-cycle-1", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact native Evil Points forgiven per cycle default is 1 and is consumed by the alignment authority.",
            "WCCMMUD.mcv prompt at offset 1825 -> RuntimeCharacterAlignmentAuthority."),
        new("remaining.range-contracts", RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown,
            "Exact allowed ranges for setup values not separately recovered.",
            "Do not infer ranges from Reforged's generic typed rule service."),
        new("remaining.mutability-restart", RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown,
            "Exact native live-mutable versus restart-only semantics for the remaining setup values.",
            "SYSOP RELOAD proves a native configuration reload command exists, but not each setting's live effect boundary."),
        new("remaining.owner-effect-map", RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown,
            "Exact runtime owner/effect mapping for setup prompts that are cataloged but not yet connected to a proven gameplay owner.",
            "Cataloged defaults are evidence, not permission to activate guessed behavior."),
        new("reforged.generic-world-rule-contract", RuntimeClassicWorldRulesConfigurationEvidenceState.Excluded,
            "Reforged Custom/AI typed rule ranges and mutability metadata are not native MajorMUD evidence.",
            "They may remain as Reforged features but cannot fill Classic evidence gaps.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (RuntimeClassicConfigurationAuthority.ReforgedUsesNativeConfigurationFile) failures.Add("reforged-must-not-use-native-ini");
        if (RuntimeClassicConfigurationAuthority.NativePrompts.Count != RetainedNativePromptCount) failures.Add("native-prompt-count");
        if (Rows.Count != 12) failures.Add("expected-twelve-world-rules-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Proven) != 8) failures.Add("expected-eight-proven-world-rules-rows");
        if (Rows.Count(row => row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown) != 3) failures.Add("expected-three-open-world-rules-rows");
        if (Rows.Count(row => row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Excluded) != 1) failures.Add("expected-one-excluded-world-rules-row");
        return failures;
    }
}
