using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcCompanionCommandRequest(
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string CommandKind,
    string? TargetId,
    string Authority,
    long WorldTick);

public sealed record RuntimeNpcCompanionCommandResult(
    bool Accepted,
    string NpcInstanceId,
    string CommandKind,
    string? TargetId,
    IReadOnlyList<string> Rejections,
    string Reason);

/// <summary>
/// Exact owner-authorized command boundary for an already-owned companion. The caller owns command
/// interpretation, target selection, route selection, combat formulas, and timing. This service only
/// records an exact supported command and applies state changes whose ownership is already established.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    private static readonly HashSet<string> SupportedCompanionCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "stay", "follow", "assist", "passive", "dismiss"
    };

    public RuntimeNpcCompanionCommandResult ApplyNpcCompanionCommand(RuntimeNpcCompanionCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var rejections = new List<string>();
            if (!_npcs.TryGetValue(request.NpcInstanceId ?? string.Empty, out var npc)) rejections.Add("instance-not-found");
            else
            {
                if (!npc.Alive) rejections.Add("npc-is-not-alive");
                if (string.IsNullOrWhiteSpace(request.Authority)) rejections.Add("authority-required");
                if (!SupportedCompanionCommands.Contains(request.CommandKind ?? string.Empty)) rejections.Add("command-not-supported");
                if (!npc.Metadata.TryGetValue("npc_companion_owner_kind", out var storedKind) || !npc.Metadata.TryGetValue("npc_companion_owner_id", out var storedId)) rejections.Add("companion-owner-not-assigned");
                else
                {
                    if (!storedKind.Equals(request.OwnerKind ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("companion-owner-kind-mismatch");
                    if (!storedId.Equals(request.OwnerId ?? string.Empty, StringComparison.Ordinal)) rejections.Add("companion-owner-id-mismatch");
                }
                if (request.WorldTick < 0) rejections.Add("world-tick-invalid");
            }

            if (npc is null || rejections.Count != 0)
                return new(false, request.NpcInstanceId ?? string.Empty, request.CommandKind ?? string.Empty, request.TargetId, rejections, "companion-command-rejected");

            var command = request.CommandKind!.Trim().ToLowerInvariant();
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_companion_command_mode"] = command,
                ["npc_companion_last_command_authority"] = request.Authority!.Trim(),
                ["npc_companion_last_command_tick"] = request.WorldTick.ToString()
            };
            if (string.IsNullOrWhiteSpace(request.TargetId)) metadata.Remove("npc_companion_command_target_id");
            else metadata["npc_companion_command_target_id"] = request.TargetId!.Trim();

            if (command is "stay" or "passive")
            {
                foreach (var key in metadata.Keys.Where(k => k.StartsWith("npc_companion_follow_", StringComparison.OrdinalIgnoreCase)).ToArray()) metadata.Remove(key);
                metadata.Remove("current_target_id");
                metadata.Remove("threat_ledger_json");
                metadata["combat_hostility_state"] = "idle";
            }
            else if (command == "dismiss")
            {
                foreach (var key in metadata.Keys.Where(IsCompanionTransientMetadata).ToArray()) metadata.Remove(key);
                metadata.Remove("current_target_id");
                metadata.Remove("threat_ledger_json");
                metadata["combat_hostility_state"] = "idle";
                metadata["npc_companion_last_cleanup_reason"] = "dismissed";
                metadata["npc_companion_last_cleanup_authority"] = request.Authority!.Trim();
                metadata["npc_companion_last_cleanup_tick"] = request.WorldTick.ToString();
            }

            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, command, request.TargetId, Array.Empty<string>(), "companion-command-applied");
        }
    }
}

public sealed class RuntimeNpcCompanionCommandSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcCompanionCommandSmokeReport Run()
    {
        var report = new RuntimeNpcCompanionCommandSmokeReport();
        try
        {
            var method = typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.ApplyNpcCompanionCommand));
            Require(method is not null, "companion-command-exposed");
            report.Checks.Add("companion-command-exposed");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
