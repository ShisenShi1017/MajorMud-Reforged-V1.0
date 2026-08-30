using System.Text.Json;

namespace Reforged.Server;

/// <summary>
/// Trace-and-connect bridge between live combat and the decoded living-world threat,
/// companion ownership, assist, aggro, and reward ownership services already present
/// in the uploaded repository. It does not invent faction radii or native threat math:
/// direct damage and explicit combat admission are the only threat authorities used.
/// </summary>
public sealed partial class RuntimeLiveGameplayFacade
{
    public const string NpcTargetThreatFormulaId = "combat.npc_target_threat_selection";
    public const string PlayerAssistFormulaId = "combat.player_assist_join";
    public const string CompanionAssistFormulaId = "combat.companion_owner_assist";
    public const string GuardAssistFormulaId = "combat.explicit_npc_assist";
    public const string KillRewardAttributionFormulaId = "combat.kill_reward_attribution";
    private long _combatOwnershipTick;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuntimeCombatOwnerState> _combatOwners = new(StringComparer.OrdinalIgnoreCase);

    private sealed record RuntimeCombatOwnerState(string WorldId, string ActorId, string NpcInstanceId);

    private long NextCombatOwnershipTick() => checked(DateTimeOffset.UtcNow.UtcDateTime.Ticks + (Interlocked.Increment(ref _combatOwnershipTick) % 1000));

    private void RegisterCombatEngagement(RuntimeSession session, RuntimeLiveCombatEncounter encounter, long initialThreat)
    {
        var living = Living(session);
        _combatOwners[session.Id] = new RuntimeCombatOwnerState(session.World.Definition.Id, session.Actor.Id, encounter.NpcInstanceId);
        living.AcquireNpcTarget(encounter.NpcInstanceId, session.Actor.Id, session.CurrentRoom.Id, DateTimeOffset.UtcNow);
        living.ApplyNpcThreatGenerationSnapshot(new RuntimeNpcThreatGenerationSnapshot(
            encounter.NpcInstanceId,
            NextCombatOwnershipTick(),
            encounter.RoomId,
            [new RuntimeNpcThreatGenerationEvent(
                $"combat-admission:{session.Id}:{encounter.RoundNumber}",
                session.Actor.Id,
                session.CurrentRoom.Id,
                true,
                true,
                true,
                Math.Max(0, initialThreat),
                "explicit-combat-admission",
                "runtime.live_combat")]
        ));
    }

    private void RegisterCombatDamageThreat(RuntimeSession session, RuntimeLiveCombatEncounter encounter, int damage, int swing)
    {
        if (damage <= 0) return;
        Living(session).ApplyNpcThreatGenerationSnapshot(new RuntimeNpcThreatGenerationSnapshot(
            encounter.NpcInstanceId,
            NextCombatOwnershipTick(),
            encounter.RoomId,
            [new RuntimeNpcThreatGenerationEvent(
                $"combat-damage:{session.Id}:{encounter.RoundNumber}:{swing}",
                session.Actor.Id,
                session.CurrentRoom.Id,
                true,
                true,
                true,
                damage,
                "direct-damage",
                "runtime.live_combat")]
        ));
    }

    private bool IsSelectedNpcTarget(RuntimeSession session, RuntimeLiveCombatEncounter encounter)
    {
        var npc = Living(session).InspectNpc(encounter.NpcInstanceId);
        if (npc is null || !npc.Alive) return false;
        if (!npc.Metadata.TryGetValue("current_target_id", out var target) || string.IsNullOrWhiteSpace(target))
        {
            RegisterCombatEngagement(session, encounter, 1);
            return true;
        }
        return target.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase);
    }

    private void ReleaseCombatEngagement(RuntimeSession session, RuntimeLiveCombatEncounter encounter, string reason)
    {
        Living(session).ReleaseNpcCombatant(encounter.NpcInstanceId, session.Actor.Id, DateTimeOffset.UtcNow);
        _combatOwners.TryRemove(session.Id, out _);
    }

    private void PruneInactiveCombatOwnership()
    {
        var activeIds = _sessions.ActiveSessions.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _combatOwners.ToArray())
        {
            if (activeIds.Contains(pair.Key)) continue;
            if (_livingWorlds.TryGetValue(pair.Value.WorldId, out var living))
                living.ReleaseNpcCombatant(pair.Value.NpcInstanceId, pair.Value.ActorId, DateTimeOffset.UtcNow);
            _combatOwners.TryRemove(pair.Key, out _);
            _liveCombats.TryRemove(pair.Key, out _);
        }
    }

    private void AuthorizeOwnedCompanionAssistance(RuntimeSession session, RuntimeLiveCombatEncounter encounter)
    {
        foreach (var companion in LivingNpcsInRoom(session))
        {
            var ownership = Living(session).InspectNpcCompanionOwnership(companion.InstanceId);
            if (ownership is null
                || !ownership.OwnerKind.Equals("character", StringComparison.OrdinalIgnoreCase)
                || !ownership.OwnerId.Equals(session.Actor.Id, StringComparison.Ordinal)) continue;

            Living(session).ApplyNpcCompanionCombatAssist(new RuntimeNpcCompanionCombatAssistRequest(
                companion.InstanceId,
                "character",
                session.Actor.Id,
                session.CurrentRoom.Id,
                encounter.NpcInstanceId,
                encounter.RoomId,
                true,
                true,
                true,
                true,
                "runtime.live_combat.owner-assist",
                NextCombatOwnershipTick()));
        }
    }


    private void AuthorizeExplicitNpcAssistance(RuntimeSession session, RuntimeLiveCombatEncounter encounter)
    {
        var living = Living(session);
        var caller = living.InspectNpc(encounter.NpcInstanceId);
        if (caller is null) return;
        var classicWorld = session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase);
        caller.Metadata.TryGetValue("assist_group_id", out var callerGroup);
        caller.Metadata.TryGetValue("npc_legacy_id", out var callerLegacyId);
        foreach (var candidate in LivingNpcsInRoom(session))
        {
            if (candidate.InstanceId.Equals(encounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase)) continue;

            // Classic admission is data-owned by native ability 146 (MonsGuards), whose value is
            // the guarded monster legacy id. Reforged assist groups/instance links are extensions
            // and must not alter Classic behavior.
            var explicitlyLinked = !string.IsNullOrWhiteSpace(callerLegacyId)
                && candidate.Metadata.TryGetValue("guarded_npc_legacy_ids", out var guardedIds)
                && guardedIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(callerLegacyId, StringComparer.OrdinalIgnoreCase);

            if (!classicWorld && !explicitlyLinked)
            {
                if (candidate.Metadata.TryGetValue("assist_target_instance_id", out var linked)
                    && linked.Equals(encounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
                    explicitlyLinked = true;
                if (!explicitlyLinked
                    && !string.IsNullOrWhiteSpace(callerGroup)
                    && candidate.Metadata.TryGetValue("assist_group_id", out var candidateGroup)
                    && candidateGroup.Equals(callerGroup, StringComparison.OrdinalIgnoreCase))
                    explicitlyLinked = true;
            }
            if (!explicitlyLinked) continue;

            living.ApplyNpcAssistSnapshot(new RuntimeNpcAssistSnapshot(
                candidate.InstanceId,
                NextCombatOwnershipTick(),
                session.CurrentRoom.Id,
                encounter.NpcInstanceId,
                caller.Alive,
                true,
                session.Actor.Id,
                session.CurrentRoom.Id,
                true,
                true,
                true,
                classicWorld ? "classic.ability-146-monsguards" : "runtime.live_combat.explicit-guard-assist"));
        }
    }

    private string AssistLiveCombat(RuntimeSession session, string query)
    {
        if (_liveCombats.ContainsKey(session.Id)) return "You are already in combat.";
        if (string.IsNullOrWhiteSpace(query)) return "Assist whom?";
        var ally = _sessions.FindUniqueInRoom(session, query.Trim());
        if (ally is null) return $"No nearby combatant uniquely matches '{query.Trim()}'.";
        if (!_liveCombats.TryGetValue(ally.Id, out var source)) return $"{ally.Actor.DisplayName} is not in combat.";
        var livingNpc = Living(session).InspectNpc(source.NpcInstanceId);
        if (livingNpc is null || !livingNpc.Alive || !livingNpc.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase))
            return "That combat target is no longer available.";

        var encounter = source with
        {
            SessionId = session.Id,
            NpcCurrentHitPoints = livingNpc.CurrentHitPoints,
            PlayerCurrentEnergy = 0,
            RoundNumber = 0,
            PlayerAttackType = ClassicAttackType.NormalWeapon,
            PlayerAutocombatActive = true,
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        if (!_liveCombats.TryAdd(session.Id, encounter)) return "You could not join that combat.";
        RegisterCombatEngagement(session, encounter, 1);
        AuthorizeOwnedCompanionAssistance(session, encounter);
        AuthorizeExplicitNpcAssistance(session, encounter);
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} assists {ally.Actor.DisplayName} against {encounter.NpcName}.", includeSource: true);
        return $"You assist {ally.Actor.DisplayName} against {encounter.NpcName}.";
    }
}

public sealed class RuntimeLiveCombatOwnershipSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeLiveCombatOwnershipSmokeReport Run()
    {
        var report = new RuntimeLiveCombatOwnershipSmokeReport();
        try
        {
            var type = typeof(RuntimeLiveGameplayFacade);
            Require(type.GetMethod("AssistLiveCombat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null, "assist-live-combat-connected");
            Require(type.GetMethod("RegisterCombatDamageThreat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null, "damage-threat-connected");
            Require(type.GetMethod("PruneInactiveCombatOwnership", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null, "disconnect-combat-cleanup-connected");
            Require(typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.ApplyNpcThreatGenerationSnapshot)) is not null, "threat-generation-authority-connected");
            Require(typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.ApplyNpcCompanionCombatAssist)) is not null, "companion-assist-authority-connected");
            Require(typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.ApplyNpcAssistSnapshot)) is not null, "explicit-guard-assist-authority-connected");
            report.Checks.AddRange(["assist-live-combat-connected", "damage-threat-connected", "disconnect-combat-cleanup-connected", "threat-generation-authority-connected", "companion-assist-authority-connected", "explicit-guard-assist-authority-connected"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}

public sealed partial class RuntimeLivingWorldService
{
    public bool ReleaseNpcCombatant(string instanceId, string actorId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var npc)) return false;
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            var ledger = new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
            if (metadata.TryGetValue(ThreatLedgerMetadataKey, out var encoded) && !string.IsNullOrWhiteSpace(encoded))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string,long>>(encoded);
                    if (parsed is not null) ledger = new Dictionary<string,long>(parsed, StringComparer.OrdinalIgnoreCase);
                }
                catch (JsonException) { }
            }
            ledger.Remove(actorId);
            metadata[ThreatLedgerMetadataKey] = JsonSerializer.Serialize(ledger.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
            var next = ledger.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(next.Key))
            {
                metadata.Remove("current_target_id");
                metadata.Remove("current_target_explicit_threat");
                if (_npcAi.TryGetValue(instanceId, out var ai))
                    _npcAi[instanceId] = ai with { Mode = npc.Alive ? "return-home" : "dead", TargetActorId = null, UpdatedAtUtc = nowUtc };
            }
            else
            {
                metadata["current_target_id"] = next.Key;
                metadata["current_target_explicit_threat"] = next.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (_npcAi.TryGetValue(instanceId, out var ai))
                    _npcAi[instanceId] = ai with { Mode = "combat", TargetActorId = next.Key, UpdatedAtUtc = nowUtc };
            }
            _npcs[instanceId] = npc with { Metadata = metadata };
            Save();
            SaveLivingWorldCompletionLocked();
            return true;
        }
    }
}
