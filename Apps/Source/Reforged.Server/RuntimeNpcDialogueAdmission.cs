using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcDialogueAdmission(
    bool Admitted,
    string InstanceId,
    string? RequestId,
    string ActorId,
    string RoomId,
    string Reason);

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcDialogueAdmission AdmitNpcDialogue(string instanceId, string actorId, string roomId, string authority, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current))
                return new(false, instanceId ?? string.Empty, null, actorId ?? string.Empty, roomId ?? string.Empty, "instance-not-found");
            if (!current.Alive)
                return new(false, current.InstanceId, null, actorId ?? string.Empty, roomId ?? string.Empty, "npc-is-not-alive");
            if (string.IsNullOrWhiteSpace(actorId))
                return new(false, current.InstanceId, null, string.Empty, roomId ?? string.Empty, "actor-id-required");
            if (!current.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase))
                return new(false, current.InstanceId, null, actorId, roomId ?? string.Empty, "room-ownership-mismatch");
            if (current.Metadata.TryGetValue("npc_dialogue_active_request_id", out var active) && !string.IsNullOrWhiteSpace(active))
                return new(false, current.InstanceId, active, actorId, roomId, "dialogue-already-active");

            var generation = ReadGeneration(current.Metadata) + 1;
            var requestId = $"{current.InstanceId}:dialogue:{generation.ToString(CultureInfo.InvariantCulture)}";
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_dialogue_generation"] = generation.ToString(CultureInfo.InvariantCulture),
                ["npc_dialogue_active_request_id"] = requestId,
                ["npc_dialogue_actor_id"] = actorId.Trim(),
                ["npc_dialogue_room_id"] = roomId,
                ["npc_dialogue_authority"] = string.IsNullOrWhiteSpace(authority) ? "host-dialogue-command" : authority.Trim(),
                ["npc_dialogue_admitted_at_utc"] = nowUtc.ToString("O")
            };
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return new(true, current.InstanceId, requestId, actorId.Trim(), roomId, "admitted");
        }
    }

    public bool CompleteNpcDialogue(string instanceId, string requestId, bool matched, string? outcome, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            if (!current.Metadata.TryGetValue("npc_dialogue_active_request_id", out var active) ||
                !active.Equals(requestId, StringComparison.Ordinal)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_dialogue_last_request_id"] = requestId,
                ["npc_dialogue_last_matched"] = matched ? "true" : "false",
                ["npc_dialogue_last_outcome"] = string.IsNullOrWhiteSpace(outcome) ? (matched ? "matched" : "not-matched") : outcome.Trim(),
                ["npc_dialogue_completed_at_utc"] = nowUtc.ToString("O")
            };
            metadata.Remove("npc_dialogue_active_request_id");
            metadata.Remove("npc_dialogue_actor_id");
            metadata.Remove("npc_dialogue_room_id");
            metadata.Remove("npc_dialogue_authority");
            metadata.Remove("npc_dialogue_admitted_at_utc");
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    private static long ReadGeneration(IReadOnlyDictionary<string,string> metadata) =>
        metadata.TryGetValue("npc_dialogue_generation", out var raw) && long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value : 0;
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private RuntimeNpcDialogueAdmission AdmitLivingNpcDialogue(RuntimeSession session, RuntimeLivingNpcState npc, string actorId) =>
        Living(session).AdmitNpcDialogue(npc.InstanceId, actorId, session.CurrentRoom.Id, "host-talk-command", DateTimeOffset.UtcNow);

    private bool CompleteLivingNpcDialogue(RuntimeSession session, RuntimeLivingNpcState npc, string requestId, bool matched, string? outcome) =>
        Living(session).CompleteNpcDialogue(npc.InstanceId, requestId, matched, outcome, DateTimeOffset.UtcNow);
}

public sealed class RuntimeNpcDialogueAdmissionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcDialogueAdmissionSmokeReport Run()
    {
        var report = new RuntimeNpcDialogueAdmissionSmokeReport();
        try
        {
            var type = typeof(RuntimeLivingWorldService);
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcDialogue)) is not null, "dialogue-admission-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcDialogue)) is not null, "dialogue-completion-exposed");
            report.Checks.AddRange(["dialogue-admission-exposed", "dialogue-completion-exposed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
