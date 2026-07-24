using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcCompanionCleanupRequest(
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string CleanupReason,
    string Authority,
    DateTimeOffset OccurredAtUtc,
    long WorldTick);

public sealed record RuntimeNpcCompanionCleanupResult(
    bool Applied,
    string NpcInstanceId,
    string CleanupReason,
    IReadOnlyList<string> Rejections,
    string Reason);

/// <summary>
/// Exact lifecycle-cleanup boundary for an already-owned companion. The caller owns the decision that
/// dismissal, owner logout/disconnect, duration expiry, charm break, or companion death occurred. This
/// service clears companion ownership and companion-owned transient state only; it does not infer native
/// duration, despawn, corpse, logout, disconnect, charm-resistance, or persistence-deletion semantics.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    private static readonly HashSet<string> SupportedCompanionCleanupReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "dismissed", "owner-logout", "owner-disconnect", "duration-expired", "charm-broken", "companion-death"
    };

    public RuntimeNpcCompanionCleanupResult CleanupNpcCompanionLifecycle(RuntimeNpcCompanionCleanupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var rejections = new List<string>();
            if (!_npcs.TryGetValue(request.NpcInstanceId ?? string.Empty, out var npc))
                rejections.Add("instance-not-found");
            else
            {
                if (string.IsNullOrWhiteSpace(request.Authority)) rejections.Add("authority-required");
                if (!SupportedCompanionCleanupReasons.Contains(request.CleanupReason ?? string.Empty)) rejections.Add("cleanup-reason-not-supported");
                if (!npc.Metadata.TryGetValue("npc_companion_owner_kind", out var storedKind) ||
                    !npc.Metadata.TryGetValue("npc_companion_owner_id", out var storedId))
                    rejections.Add("companion-owner-not-assigned");
                else
                {
                    if (!storedKind.Equals(request.OwnerKind ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("companion-owner-kind-mismatch");
                    if (!storedId.Equals(request.OwnerId ?? string.Empty, StringComparison.Ordinal)) rejections.Add("companion-owner-id-mismatch");
                }
                if (string.Equals(request.CleanupReason, "companion-death", StringComparison.OrdinalIgnoreCase) && npc.Alive)
                    rejections.Add("companion-death-requires-dead-instance");
                if (request.WorldTick < 0) rejections.Add("world-tick-invalid");
            }

            if (npc is null || rejections.Count != 0)
                return new(false, request.NpcInstanceId ?? string.Empty, request.CleanupReason ?? string.Empty, rejections, "companion-lifecycle-cleanup-rejected");

            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_companion_last_cleanup_reason"] = request.CleanupReason!.Trim().ToLowerInvariant(),
                ["npc_companion_last_cleanup_owner_kind"] = request.OwnerKind!.Trim().ToLowerInvariant(),
                ["npc_companion_last_cleanup_owner_id"] = request.OwnerId!.Trim(),
                ["npc_companion_last_cleanup_authority"] = request.Authority!.Trim(),
                ["npc_companion_last_cleanup_at_utc"] = request.OccurredAtUtc.ToString("O"),
                ["npc_companion_last_cleanup_tick"] = request.WorldTick.ToString()
            };

            foreach (var key in metadata.Keys.Where(IsCompanionTransientMetadata).ToArray()) metadata.Remove(key);
            metadata.Remove("current_target_id");
            metadata.Remove("threat_ledger_json");
            metadata["combat_hostility_state"] = "idle";

            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, request.CleanupReason!.Trim().ToLowerInvariant(), Array.Empty<string>(), "companion-lifecycle-cleanup-applied");
        }
    }

    private static bool IsCompanionTransientMetadata(string key) =>
        key.StartsWith("npc_companion_owner_", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("npc_companion_relationship_kind", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("npc_companion_authority", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("npc_companion_assigned_at_utc", StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith("npc_companion_follow_", StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith("npc_companion_last_combat_assist_", StringComparison.OrdinalIgnoreCase);
}

public sealed class RuntimeNpcCompanionLifecycleCleanupSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcCompanionLifecycleCleanupSmokeReport Run()
    {
        var report = new RuntimeNpcCompanionLifecycleCleanupSmokeReport();
        try
        {
            var method = typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.CleanupNpcCompanionLifecycle));
            Require(method is not null, "companion-lifecycle-cleanup-exposed");
            report.Checks.Add("companion-lifecycle-cleanup-exposed");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
