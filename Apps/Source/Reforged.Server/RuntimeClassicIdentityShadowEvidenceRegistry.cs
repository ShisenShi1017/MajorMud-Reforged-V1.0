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
/// Pass 40 narrows Classic identity/disguise behavior around the decoded Shadow ability.
/// The retained public ability reference says Shadow displays "A shadowy figure!" when LOOKed
/// at by other players.  That evidence is deliberately not generalized to the room occupant list.
/// </summary>
public static class RuntimeClassicIdentityShadowEvidenceRegistry
{
    public static readonly int ShadowAbilityId = 9;
    public const int BlackEmbroideredRobesLegacyId = 1331;
    public const int BlackEmbroideredRobesShadowValue = 100;
    public const string ShadowLookPayload = "A shadowy figure!";

    public static IReadOnlyList<RuntimeClassicIdentityShadowEvidenceRow> Rows { get; } =
    [
        new("identity.first-name-unique", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Character first names must be unique.",
            "Retained native identity evidence from Pass 28."),
        new("identity.userrec-name-widths", RuntimeClassicIdentityShadowEvidenceState.Proven,
            "Native UserRec stores Given/FirstName in 10 bytes and Family/LastName in 18 bytes.",
            "Recovered native UserRec structure from Pass 28."),
        new("item.1331-shadow-100", RuntimeClassicIdentityShadowEvidenceState.Strong,
            "Black embroidered robes carry Shadow ability 9 with value 100.",
            "Retained item.1331 public-reference relationship plus independent historical MajorMUD item tables."),
        new("ability.9-look-payload", RuntimeClassicIdentityShadowEvidenceState.Strong,
            "Shadow displays 'A shadowy figure!' when looked at by other players.",
            "Retained ability.9 public-reference description; promoted only as a LOOK presentation boundary."),
        new("identity.shadow-look-live", RuntimeClassicIdentityShadowEvidenceState.Strong,
            "An equipped positive Shadow item masks the target identity in LOOK for other players.",
            "Runtime consumes equipped item ability 9; self LOOK is not masked."),
        new("identity.shadow-room-list", RuntimeClassicIdentityShadowEvidenceState.Unknown,
            "Whether native room occupant rendering replaces the character name for Shadow.",
            "The retained Shadow description is scoped to being looked at. No native evidence proves room-list masking, so Pass 40 leaves room occupant names unchanged."),
        new("identity.name-validation-details", RuntimeClassicIdentityShadowEvidenceState.Unknown,
            "Minimum name length, allowed characters, complete reserved-name list and exact failure text.",
            "User-provided page remains hypothesis-only until native parser/creation evidence is recovered."),
        new("identity.title-invisibility-precedence", RuntimeClassicIdentityShadowEvidenceState.Unknown,
            "Exact title precedence and hidden/invisible/disguise interaction ordering.",
            "Existing visibility and appearance work is partial; exact native precedence remains open.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Rows.Count != 8) failures.Add("expected-eight-identity-shadow-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicIdentityShadowEvidenceState.Proven) != 2) failures.Add("expected-two-proven-identity-rows");
        if (Rows.Count(row => row.State == RuntimeClassicIdentityShadowEvidenceState.Strong) != 3) failures.Add("expected-three-strong-shadow-rows");
        if (Rows.Count(row => row.State == RuntimeClassicIdentityShadowEvidenceState.Unknown) != 3) failures.Add("expected-three-open-identity-rows");
        if (ShadowAbilityId != RuntimeClassicCharacterAppearance.ShadowAbilityId) failures.Add("shadow-ability-id-mismatch");
        if (!ShadowLookPayload.Equals(RuntimeClassicCharacterAppearance.ShadowLookPayload, StringComparison.Ordinal)) failures.Add("shadow-look-payload-mismatch");
        return failures;
    }
}
