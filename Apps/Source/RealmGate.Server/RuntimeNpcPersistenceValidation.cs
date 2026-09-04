using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcPersistenceIssue(string InstanceId, string Code, string Detail, bool Repairable);
public sealed record RuntimeNpcPersistenceValidationResult(
    bool Valid,
    int InstanceCount,
    int IssueCount,
    int RepairedCount,
    IReadOnlyList<RuntimeNpcPersistenceIssue> Issues,
    string Reason);

/// <summary>
/// Exact save/load integrity boundary for materialized NPC state. It validates only invariants already
/// owned by the runtime and may remove orphaned transient metadata under explicit host authority. It does
/// not derive native NPC policy, reconstruct missing routes, select targets, or invent respawn timing.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcPersistenceValidationResult ValidateNpcPersistenceState(bool repair, string? authority, DateTimeOffset inspectedAtUtc, long worldTick)
    {
        lock (_gate)
        {
            var issues = new List<RuntimeNpcPersistenceIssue>();
            var repaired = 0;
            if (repair && string.IsNullOrWhiteSpace(authority))
                return new(false, _npcs.Count, 1, 0,
                    [new("*", "repair-authority-required", "Repair was requested without an explicit authority.", false)],
                    "npc-persistence-validation-rejected");
            if (worldTick < 0)
                return new(false, _npcs.Count, 1, 0,
                    [new("*", "world-tick-invalid", "World tick must be nonnegative.", false)],
                    "npc-persistence-validation-rejected");

            foreach (var npc in _npcs.Values.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray())
            {
                var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
                var changed = false;
                void Add(string code, string detail, bool repairable = false)
                    => issues.Add(new(npc.InstanceId, code, detail, repairable));
                void Remove(string key)
                {
                    if (metadata.Remove(key)) { changed = true; repaired++; }
                }

                if (_world.FindRoomById(npc.RoomId) is null) Add("current-room-unresolved", npc.RoomId);
                if (_world.FindRoomById(npc.HomeRoomId) is null) Add("home-room-unresolved", npc.HomeRoomId);
                if (npc.MaximumHitPoints <= 0) Add("maximum-hit-points-invalid", npc.MaximumHitPoints.ToString());
                if (npc.CurrentHitPoints < 0 || npc.CurrentHitPoints > npc.MaximumHitPoints) Add("current-hit-points-invalid", npc.CurrentHitPoints.ToString());
                if (npc.Alive && npc.CurrentHitPoints <= 0) Add("alive-npc-has-no-hit-points", npc.CurrentHitPoints.ToString());
                if (!npc.Alive && npc.CurrentHitPoints > 0) Add("dead-npc-has-positive-hit-points", npc.CurrentHitPoints.ToString());
                if (npc.Alive && npc.RespawnAtUtc is not null) Add("alive-npc-has-respawn-timer", npc.RespawnAtUtc.Value.ToString("O"));
                if (!npc.Alive && npc.RespawnAtUtc is null && !metadata.ContainsKey("npc_respawn_block_reason") && !metadata.ContainsKey("npc_respawn_suppressed_at_utc"))
                    Add("dead-npc-missing-respawn-state", "No respawn timer, block, or suppression state exists.");

                var pendingAction = metadata.TryGetValue("pending_ai_action_id", out var actionId) && !string.IsNullOrWhiteSpace(actionId);
                var claimId = metadata.TryGetValue("pending_ai_action_claim_id", out var storedClaimId) && !string.IsNullOrWhiteSpace(storedClaimId);
                var claimConsumer = metadata.TryGetValue("pending_ai_action_claim_consumer", out var storedConsumer) && !string.IsNullOrWhiteSpace(storedConsumer);
                if (!pendingAction && (claimId || claimConsumer))
                {
                    Add("orphaned-action-claim", "Claim metadata exists without a pending action.", true);
                    if (repair)
                    {
                        Remove("pending_ai_action_claim_id"); Remove("pending_ai_action_claim_consumer");
                        Remove("pending_ai_action_claimed_tick"); Remove("pending_ai_action_delivery_count");
                    }
                }
                if (claimId != claimConsumer) Add("incomplete-action-claim", "Claim ID and consumer must exist together.");

                if (metadata.TryGetValue("npc_explicit_threat_ledger_json", out var threatRaw))
                {
                    try
                    {
                        var ledger = JsonSerializer.Deserialize<Dictionary<string,long>>(threatRaw);
                        if (ledger is null || ledger.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 0)) Add("threat-ledger-invalid", "Threat ledger contains an empty target or negative value.");
                    }
                    catch (JsonException) { Add("threat-ledger-malformed", "Threat ledger JSON cannot be parsed."); }
                }

                var routeActive = metadata.TryGetValue("npc_explicit_route_active", out var routeActiveRaw) && routeActiveRaw.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (routeActive)
                {
                    if (!metadata.TryGetValue("npc_explicit_route_id", out var routeId) || string.IsNullOrWhiteSpace(routeId)) Add("active-route-missing-id", "Active route has no route ID.");
                    if (!metadata.TryGetValue("npc_explicit_route_rooms_json", out var roomsRaw) || !TryReadRouteRooms(roomsRaw, out var rooms)) Add("active-route-state-invalid", "Active route room sequence is missing or malformed.");
                    else
                    {
                        if (!metadata.TryGetValue("npc_explicit_route_step_index", out var stepRaw) || !int.TryParse(stepRaw, out var step) || step < 0 || step >= rooms.Length - 1)
                            Add("active-route-step-invalid", stepRaw ?? string.Empty);
                        if (rooms.Any(room => _world.FindRoomById(room) is null)) Add("active-route-room-unresolved", "One or more route rooms are not canonical generated rooms.");
                    }
                }

                if (!npc.Alive)
                {
                    foreach (var key in new[] { "current_target_id", "npc_fleeing", "npc_flee_destination_room_id", "npc_returning_home", "npc_return_home_destination_room_id" })
                    {
                        if (!metadata.ContainsKey(key)) continue;
                        Add("dead-npc-retains-transient-state", key, true);
                        if (repair) Remove(key);
                    }
                }

                if (changed)
                {
                    metadata["npc_persistence_last_repaired_authority"] = authority!.Trim();
                    metadata["npc_persistence_last_repaired_at_utc"] = inspectedAtUtc.ToString("O");
                    metadata["npc_persistence_last_repaired_tick"] = worldTick.ToString();
                    _npcs[npc.InstanceId] = npc with { Metadata = metadata };
                }
            }

            if (repair && repaired > 0) Save();
            return new(issues.Count == 0, _npcs.Count, issues.Count, repaired, issues,
                issues.Count == 0 ? "npc-persistence-state-valid" : repair ? "npc-persistence-state-inspected-and-repaired" : "npc-persistence-state-invalid");
        }
    }
}

public sealed class RuntimeNpcPersistenceValidationSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcPersistenceValidationSmokeReport Run()
    {
        var report = new RuntimeNpcPersistenceValidationSmokeReport();
        try
        {
            var method = typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.ValidateNpcPersistenceState));
            Require(method is not null, "npc-persistence-validation-exposed");
            var parameters = method!.GetParameters();
            Require(parameters.Length == 4, "npc-persistence-validation-contract");
            report.Checks.AddRange(["npc-persistence-validation-exposed", "npc-persistence-validation-contract"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
