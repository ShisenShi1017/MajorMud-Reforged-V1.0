using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeCharacterIdentityPolicySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeCharacterIdentityPolicySmokeReport Run()
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(string id, bool value) { checks.Add(id); if (!value) failures.Add(id); }
        var service = new RuntimeCharacterIdentityPolicyService(
            new RuntimeIdentityPolicy(3, 16, ReservedNames: new[] { "Sysop" }, ReservedPrefixes: new[] { "Admin" }),
            new[] {
                new RuntimeTitleDefinition("class.archmage", "Archmage", RuntimeTitlePosition.Prefix, "Character", RuntimeIdentityAccess.Administrator),
                new RuntimeTitleDefinition("guild.tag", "[Wardens]", RuntimeTitlePosition.GuildTag, "Organizations", RuntimeIdentityAccess.Builder),
                new RuntimeTitleDefinition("admin.hidden", "Staff", RuntimeTitlePosition.Suffix, "AdministrativePolicy", RuntimeIdentityAccess.Host, RuntimeIdentityVisibility.AdministratorsOnly)
            });
        Check("valid-name", service.ValidateName("aLiCe").Success);
        Check("reserved-name", service.ValidateName("Sysop").Code == "identity-name-reserved");
        Check("reserved-prefix", service.ValidateName("AdminBob").Code == "identity-name-prefix-reserved");
        Check("invalid-characters", service.ValidateName("Bob_1").Code == "identity-name-characters-invalid");
        Check("duplicate-name", service.ValidateName("Alice", _ => true).Code == "identity-name-duplicate");
        var subject = new RuntimeIdentitySubject("c1", "Alice", new[] { "class.archmage", "guild.tag", "admin.hidden" }, "Old Woman");
        var player = service.Present(new RuntimeIdentityActor("c2", RuntimeIdentityAccess.Player), subject);
        Check("disguise-presentation", player.Success && player.Presentation?.DisplayName == "Archmage Old Woman [Wardens]" && player.Presentation.Disguised);
        var admin = service.Present(new RuntimeIdentityActor("a1", RuntimeIdentityAccess.Administrator), subject);
        Check("admin-sees-canonical", admin.Success && admin.Presentation?.DisplayName.Contains("Alice") == true && admin.Presentation.DisplayName.Contains("Staff"));
        var hidden = service.Present(new RuntimeIdentityActor("c2", RuntimeIdentityAccess.Player), subject with { HiddenIdentity = true });
        Check("hidden-identity", hidden.Presentation?.DisplayName == "Someone");
        Check("grant-owner-enforced", service.AuthorizeTitleGrant(new("a1", RuntimeIdentityAccess.Administrator), "Wrong", "class.archmage").Code == "identity-title-owner-mismatch");
        Check("catalog-valid", service.ValidateCatalog().Count == 0);
        return new(failures.Count == 0, checks, failures);
    }
}
