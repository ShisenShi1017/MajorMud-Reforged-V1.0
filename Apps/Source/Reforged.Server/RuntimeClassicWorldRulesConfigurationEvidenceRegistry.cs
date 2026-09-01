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
/// CodeUpdate0368 closes the visible WCCMMUD world-rules/configuration boundary.
/// The retained 44-prompt MCV catalog is mapped to native option IDs, exact numeric ranges,
/// and _reload__wccmmud versus _init__wccmmud ownership. Reforged still does not parse the
/// legacy INI; the native map is evidence and typed compatibility authority.
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
            "Native LVLDIFF defaults to 100, accepts -1..100, uses -1 to disable PvP, and is consumed by the Classic PvP store.",
            "Default WCCMMUD.MSG/MCV retain 100; the supplied working 1.11p install retains 10 with the identical WCCMMUD.DLL SHA-256, proving site-configurable persistence without a binary change."),
        new("projection.ep-forgive-day-18", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact native maximum Evil Points forgiven per day default is 18 and is consumed by the alignment authority.",
            "WCCMMUD.mcv prompt at offset 1761 -> RuntimeCharacterAlignmentAuthority."),
        new("projection.ep-cycle-60", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact native Evil Point cycle default is 60 minutes and is consumed by the alignment authority.",
            "WCCMMUD.mcv prompt at offset 1792 -> RuntimeCharacterAlignmentAuthority."),
        new("projection.ep-per-cycle-1", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "The exact native Evil Points forgiven per cycle default is 1 and is consumed by the alignment authority.",
            "WCCMMUD.mcv prompt at offset 1825 -> RuntimeCharacterAlignmentAuthority."),
        new("native.range-contracts", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "Every visible numeric setup option consumed by WCCMMUD has its executable numopt minimum/maximum retained.",
            "RuntimeClassicConfigurationAuthority.VisibleOptionAuthorities mirrors _reload__wccmmud/_init__wccmmud call operands."),
        new("native.mutability-restart", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "Visible setup options are classified by executable load owner: reloadable, startup-only, or retained catalog-only with no native read call.",
            "29 visible options are reloadable, 13 are startup-only, and the 2 coin-sweep prompts have no direct 1.11p load call."),
        new("native.owner-effect-map", RuntimeClassicWorldRulesConfigurationEvidenceState.Proven,
            "All 44 visible prompts map to their WCCMMUD.h option identity and native reader/load boundary without importing Reforged generic rule semantics.",
            "The two retained-but-unconsumed prompts are explicitly represented as such rather than assigned guessed runtime effects."),
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
        if (Rows.Count(row => row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Proven) != 11) failures.Add("expected-eleven-proven-world-rules-rows");
        if (Rows.Any(row => row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Unknown)) failures.Add("world-rules-open-row-remains");
        if (Rows.Count(row => row.State == RuntimeClassicWorldRulesConfigurationEvidenceState.Excluded) != 1) failures.Add("expected-one-excluded-world-rules-row");
        return failures;
    }
}
