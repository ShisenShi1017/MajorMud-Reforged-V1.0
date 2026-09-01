namespace Reforged.Server;

public enum RuntimeClassicIdentityShadowEvidenceState
{
    Proven,
    Strong,
    Unknown,
    Excluded
}

public sealed record RuntimeClassicIdentityShadowEvidenceRow(
    string Id,
    RuntimeClassicIdentityShadowEvidenceState State,
    string Claim,
    string EvidenceBoundary);

/// <summary>
/// Exact identity/Shadow closure from WCCMMUD.DLL plus the matching operator/user material.
/// Name-token validation is alphabetic-only, native widths are 10/18, first-name collision
/// admission spans users/monsters/commands, and Shadow ability 9 short-circuits ordinary LOOK
/// presentation to the exact payload "A shadowy figure". Hidden-room discovery is a separate
/// native owner and continues to expose the actual first name.
/// </summary>
public static class RuntimeClassicIdentityShadowEvidenceRegistry
{
    public static readonly int ShadowAbilityId = 9;
    public const int BlackEmbroideredRobesLegacyId = 1331;
    public const int BlackEmbroideredRobesShadowValue = 100;
    public const string ShadowLookPayload = "A shadowy figure";

    public static IReadOnlyList<RuntimeClassicIdentityShadowEvidenceRow> Rows { get; } =
    [
        new("identity.first-name-unique", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Character first names are unique against existing players, monster identities, and game-command roots.",
            "Matching WCCMMUD.DOC identity contract plus native already-in-use creation branch."),
        new("identity.userrec-name-widths", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Native UserRec stores Given/FirstName in 10 bytes and Family/LastName in 18 bytes.",
            "Recovered native UserRec structure."),
        new("identity.name-validation-details", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Given names are 1-10 alphabetic characters; family names are 1-18 alphabetic characters when required; blank family name is controlled by BLANK_LASTNAME_ALLOWED.",
            "Direct WCCMMUD.DLL validation branches and exact failure strings; target user capture has BLANK_LASTNAME_ALLOWED=1."),
        new("item.1331-shadow-100", RuntimeClassicIdentityShadowEvidenceState.Strong,
            "Black embroidered robes carry Shadow ability 9 with value 100.",
            "Retained item.1331 relationship and historical object tables; runtime consumes the decoded ability, not the item name."),
        new("ability.9-look-payload", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Shadow displays the exact payload 'A shadowy figure' when another player LOOKs at the target.",
            "Direct native LOOK branch and data string; punctuation is part of the recovered contract."),
        new("identity.shadow-look-live", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Positive equipped Shadow short-circuits ordinary target identity/description rendering in LOOK; self LOOK is not masked.",
            "Direct native branch ordering plus existing equipped-ability projection."),
        new("identity.shadow-room-list", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Native hidden-player room-list discovery does not replace the target's first name with the Shadow LOOK payload.",
            "Direct hidden-user display owner is separate from LOOK and renders the real first-name field."),
        new("identity.universal-title-disguise-pipeline", RuntimeClassicIdentityShadowEvidenceState.Excluded,
            "A synthetic universal title/invisibility/disguise precedence pipeline is not a Classic character-identity contract.",
            "Recovered owners are command/presentation specific; Character & Status closes only the exact Shadow/identity branches rather than inventing a global pipeline.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 8) failures.Add("expected-eight-identity-shadow-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicIdentityShadowEvidenceState.Proven) != 6) failures.Add("expected-six-proven-identity-rows");
        if (Rows.Count(row => row.State == RuntimeClassicIdentityShadowEvidenceState.Strong) != 1) failures.Add("expected-one-strong-item-row");
        if (Rows.Count(row => row.State == RuntimeClassicIdentityShadowEvidenceState.Excluded) != 1) failures.Add("expected-one-excluded-global-pipeline-row");
        if (Rows.Any(row => row.State == RuntimeClassicIdentityShadowEvidenceState.Unknown)) failures.Add("identity-shadow-unknowns-must-be-zero");
        if (ShadowAbilityId != RuntimeClassicCharacterAppearance.ShadowAbilityId) failures.Add("shadow-ability-id-mismatch");
        if (!ShadowLookPayload.Equals(RuntimeClassicCharacterAppearance.ShadowLookPayload, StringComparison.Ordinal)) failures.Add("shadow-look-payload-mismatch");
        return failures;
    }
}
