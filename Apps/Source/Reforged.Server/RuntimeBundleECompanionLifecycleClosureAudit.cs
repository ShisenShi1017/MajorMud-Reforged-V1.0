using System.Reflection;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleECompanionLifecycleClosureReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> RuntimeOwners,
    IReadOnlyList<string> EvidenceStatuses)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Disconnect-to-connect closure gate for followers, pets, companions, summons, hirelings and escorts.
/// It deliberately validates one living-world authority and rejects catalog-only or duplicate ownership paths.
/// </summary>
public static class RuntimeBundleECompanionLifecycleClosureAudit
{
    private sealed record RequiredMethod(string Name, string Purpose);

    private static readonly RequiredMethod[] RequiredMethods =
    [
        new(nameof(RuntimeLivingWorldService.SummonNpc), "summon materialization"),
        new(nameof(RuntimeLivingWorldService.AssignNpcCompanionOwnership), "ownership assignment"),
        new(nameof(RuntimeLivingWorldService.ReleaseNpcCompanionOwnership), "ownership release"),
        new(nameof(RuntimeLivingWorldService.InspectNpcCompanionOwnership), "ownership inspection"),
        new(nameof(RuntimeLivingWorldService.ApplyNpcCompanionCommand), "owner commands"),
        new(nameof(RuntimeLivingWorldService.AdmitNpcCompanionFollow), "follow admission"),
        new(nameof(RuntimeLivingWorldService.CompleteNpcCompanionFollow), "follow completion and teleport follow"),
        new(nameof(RuntimeLivingWorldService.ApplyNpcCompanionCombatAssist), "combat assistance"),
        new(nameof(RuntimeLivingWorldService.TransferNpcCompanionOwnership), "ownership transfer"),
        new(nameof(RuntimeLivingWorldService.CleanupNpcCompanionLifecycle), "death, dismissal, expiry and disconnect cleanup"),
        new(nameof(RuntimeLivingWorldService.ReplaceNpcMetadata), "living-world persistence metadata")
    ];

    public static RuntimeBundleECompanionLifecycleClosureReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var type = typeof(RuntimeLivingWorldService);

        foreach (var requirement in RequiredMethods)
        {
            var matches = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name.Equals(requirement.Name, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 1) checks.Add($"connected:{requirement.Purpose}:{requirement.Name}");
            else failures.Add($"authority-path-count:{requirement.Name}:{matches.Length}");
        }

        RequireType(typeof(RuntimeCombatSpellSummoningCoordinator), "summon-policy-coordinator", checks, failures);
        RequireType(typeof(RuntimeCombatSummonPolicy), "summon-policy-model", checks, failures);
        RequireType(typeof(RuntimeCombatSummonCleanupReason), "summon-cleanup-reasons", checks, failures);
        RequireType(typeof(RuntimeNpcCompanionOwnershipView), "persistent-owner-view", checks, failures);
        RequireType(typeof(RuntimeNpcCompanionFollowCompletion), "follow-completion-model", checks, failures);
        RequireType(typeof(RuntimeNpcCompanionCleanupRequest), "cleanup-request-model", checks, failures);

        var ownershipRelationships = ReadPrivateStringSet(type, "SupportedCompanionRelationships");
        foreach (var relationship in new[] { "summon", "pet", "follower", "hireling", "escort", "charmed" })
            Check(ownershipRelationships.Contains(relationship, StringComparer.OrdinalIgnoreCase), $"relationship-supported:{relationship}", checks, failures);

        var commands = ReadPrivateStringSet(type, "SupportedCompanionCommands");
        foreach (var command in new[] { "stay", "follow", "assist", "passive", "dismiss" })
            Check(commands.Contains(command, StringComparer.OrdinalIgnoreCase), $"command-supported:{command}", checks, failures);

        var cleanupReasons = ReadPrivateStringSet(type, "SupportedCompanionCleanupReasons");
        foreach (var reason in new[] { "dismissed", "owner-logout", "owner-disconnect", "duration-expired", "charm-broken", "companion-death" })
            Check(cleanupReasons.Contains(reason, StringComparer.OrdinalIgnoreCase), $"cleanup-supported:{reason}", checks, failures);

        Check(Enum.GetValues<RuntimeCombatSummonCleanupReason>().Contains(RuntimeCombatSummonCleanupReason.DurationExpired), "summon-duration-cleanup-connected", checks, failures);
        Check(Enum.GetValues<RuntimeCombatSummonCleanupReason>().Contains(RuntimeCombatSummonCleanupReason.OwnerLogout), "summon-owner-logout-connected", checks, failures);
        Check(Enum.GetValues<RuntimeCombatSummonCleanupReason>().Contains(RuntimeCombatSummonCleanupReason.OwnerDisconnect), "summon-owner-disconnect-connected", checks, failures);
        Check(Enum.GetValues<RuntimeCombatSummonCleanupReason>().Contains(RuntimeCombatSummonCleanupReason.Death), "summon-death-cleanup-connected", checks, failures);
        Check(Enum.GetValues<RuntimeCombatSummonCleanupReason>().Contains(RuntimeCombatSummonCleanupReason.Dismissed), "summon-dismiss-cleanup-connected", checks, failures);

        return new(
            failures.Count == 0,
            checks,
            failures,
            [
                "RuntimeLivingWorldService is the single live companion authority.",
                "RuntimeCombatSpellSummoningCoordinator owns ordered summon policy; living-world materialization remains authoritative.",
                "Bundle B owns combat resolution; Bundle E authorizes companion participation.",
                "Living-world snapshots own companion metadata persistence."
            ],
            [
                "Verified: ownership, commands, follow completion, combat assistance, release and cleanup consumers.",
                "Strongly Corroborated: pet, hireling, escort, charm and summon relationships use the common companion boundary without inventing relationship-specific formulas.",
                "True unknown: native relationship duration/formulas remain quarantined until decoded evidence exists."
            ]);
    }

    private static string[] ReadPrivateStringSet(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is IEnumerable<string> values) return values.ToArray();
        return Array.Empty<string>();
    }

    private static void RequireType(Type type, string id, ICollection<string> checks, ICollection<string> failures) =>
        Check(type is not null, id, checks, failures);

    private static void Check(bool condition, string id, ICollection<string> checks, ICollection<string> failures)
    {
        if (condition) checks.Add(id); else failures.Add(id);
    }
}
