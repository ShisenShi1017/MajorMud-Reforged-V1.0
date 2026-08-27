using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass40ClassicIdentityShadowVerificationSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass40ClassicIdentityShadowVerificationSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicIdentityShadowEvidenceRegistry.Validate().Count == 0, "identity-shadow-evidence-registry-valid");
        Check(RuntimeClassicIdentityShadowEvidenceRegistry.ShadowAbilityId == 9, "shadow-ability-id-9");
        Check(RuntimeClassicIdentityShadowEvidenceRegistry.BlackEmbroideredRobesLegacyId == 1331, "black-embroidered-robes-item-1331");
        Check(RuntimeClassicIdentityShadowEvidenceRegistry.BlackEmbroideredRobesShadowValue == 100, "black-embroidered-robes-shadow-100");
        Check(RuntimeClassicCharacterAppearance.ShadowLookPayload == "A shadowy figure!", "shadow-look-payload-exact");

        return new RuntimePass40ClassicIdentityShadowVerificationSmokeReport(failures.Count == 0, checks, failures);
    }
}
