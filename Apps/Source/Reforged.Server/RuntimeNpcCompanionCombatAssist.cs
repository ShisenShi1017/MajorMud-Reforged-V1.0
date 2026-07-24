using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcCompanionCombatAssistRequest(
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string RoomId,
    string TargetEntityId,
    string TargetRoomId,
    bool TargetAlive,
    bool TargetDetectable,
    bool TargetExplicitlyHostile,
    bool AssistanceAuthorized,
    string Authority,
    long WorldTick);

public sealed record RuntimeNpcCompanionCombatAssistResult(
    bool Accepted,
    string NpcInstanceId,
    string? SelectedTargetId,
    IReadOnlyList<string> Rejections,
    string Reason);

/// <summary>
/// Exact companion combat-assistance ownership boundary. The owning subsystem supplies an explicit
/// owner match, assistance authorization, and already-observed hostile target. This service does not
/// infer native pet commands, assist radius, threat transfer, faction rules, targeting priority,
/// duration, dismissal, or combat formulas.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcCompanionCombatAssistResult ApplyNpcCompanionCombatAssist(RuntimeNpcCompanionCombatAssistRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var rejections = new List<string>();
            if (!_npcs.TryGetValue(request.NpcInstanceId ?? string.Empty, out var npc))
                rejections.Add("instance-not-found");
            else
            {
                if (!npc.Alive) rejections.Add("npc-is-not-alive");
                if (string.IsNullOrWhiteSpace(request.Authority)) rejections.Add("authority-required");
                if (!request.AssistanceAuthorized) rejections.Add("assistance-not-explicitly-authorized");
                if (!npc.RoomId.Equals(request.RoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("npc-room-mismatch");
                if (!npc.Metadata.TryGetValue("npc_companion_owner_kind", out var storedKind) ||
                    !npc.Metadata.TryGetValue("npc_companion_owner_id", out var storedId))
                    rejections.Add("companion-owner-not-assigned");
                else
                {
                    if (!storedKind.Equals(request.OwnerKind ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("companion-owner-kind-mismatch");
                    if (!storedId.Equals(request.OwnerId ?? string.Empty, StringComparison.Ordinal)) rejections.Add("companion-owner-id-mismatch");
                }
                if (string.IsNullOrWhiteSpace(request.TargetEntityId)) rejections.Add("target-required");
                if (request.TargetEntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) rejections.Add("self-target");
                if (!request.TargetAlive) rejections.Add("target-dead");
                if (!request.TargetRoomId.Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("target-different-room");
                if (!request.TargetDetectable) rejections.Add("target-not-explicitly-detectable");
                if (!request.TargetExplicitlyHostile) rejections.Add("target-not-explicitly-hostile");
                if (request.WorldTick < 0) rejections.Add("world-tick-invalid");
            }

            if (npc is null || rejections.Count != 0)
                return new(false, request.NpcInstanceId ?? string.Empty, null, rejections, "companion-combat-assist-rejected");

            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["current_target_id"] = request.TargetEntityId!,
                ["npc_companion_last_combat_assist_owner_kind"] = request.OwnerKind!.Trim().ToLowerInvariant(),
                ["npc_companion_last_combat_assist_owner_id"] = request.OwnerId!.Trim(),
                ["npc_companion_last_combat_assist_target_id"] = request.TargetEntityId!,
                ["npc_companion_last_combat_assist_authority"] = request.Authority!.Trim(),
                ["npc_companion_last_combat_assist_tick"] = request.WorldTick.ToString()
            };
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, request.TargetEntityId, Array.Empty<string>(), "companion-combat-assist-applied");
        }
    }
}

public sealed class RuntimeNpcCompanionCombatAssistSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcCompanionCombatAssistSmokeReport Run()
    {
        var report = new RuntimeNpcCompanionCombatAssistSmokeReport();
        try
        {
            var method = typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.ApplyNpcCompanionCombatAssist));
            Require(method is not null, "companion-combat-assist-exposed");
            report.Checks.Add("companion-combat-assist-exposed");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
