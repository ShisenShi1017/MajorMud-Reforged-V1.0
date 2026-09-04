namespace RealmGate.Server;

public sealed record RuntimePhysicalAttackProfile(
    string ProfileId,
    string AttackPath,
    int? MinimumDamage,
    int? MaximumDamage,
    int? AttackModifier,
    int? DefenseModifier,
    int? MinimumHitRoll,
    int? MaximumHitRoll,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimePhysicalAttackRequest(
    string AttackId,
    RuntimeCombatScheduledTurn ScheduledTurn,
    string RoomId,
    string AttackerId,
    string DefenderId,
    string DefinitionId,
    RuntimePhysicalAttackProfile Profile,
    int AttackerAttackInput,
    int DefenderDefenseInput,
    ulong RandomSeed);

public sealed record RuntimePhysicalAttackTrace(
    string AttackId,
    string CombatId,
    int RoundNumber,
    long WorldTick,
    string AttackerId,
    string DefenderId,
    string DefinitionId,
    string ProfileId,
    int HitRoll,
    int AttackTotal,
    int DefenseTotal,
    bool Hit,
    int DamageRoll,
    int FinalDamage,
    bool DefenderDefeated,
    IReadOnlyList<string> EvidenceReferences);

public interface IRuntimePhysicalDamageSink
{
    object Capture(string targetId);
    int ApplyDamage(string targetId, int magnitude, string attackId);
    void Restore(string targetId, object snapshot);
}

public sealed class RuntimePhysicalAttackExecutor
{
    private readonly RuntimeCombatInstanceLifecycle _combat;
    private readonly IRuntimePhysicalDamageSink _sink;
    private readonly HashSet<string> _consumedTurnKeys = new(StringComparer.OrdinalIgnoreCase);

    public RuntimePhysicalAttackExecutor(RuntimeCombatInstanceLifecycle combat, IRuntimePhysicalDamageSink sink)
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public RuntimePhysicalAttackTrace Execute(RuntimePhysicalAttackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var key = $"{request.ScheduledTurn.CombatId}:{request.ScheduledTurn.RoundNumber}:{request.ScheduledTurn.ActorId}:{request.ScheduledTurn.Sequence}";
        if (!_consumedTurnKeys.Add(key)) throw new InvalidOperationException("PHYSICAL_ATTACK_TURN_ALREADY_CONSUMED");

        var profile = request.Profile;
        var random = new RuntimeDeterministicRandom(request.RandomSeed);
        var hitRoll = random.NextInclusive(profile.MinimumHitRoll!.Value, profile.MaximumHitRoll!.Value);
        var attackTotal = checked(hitRoll + request.AttackerAttackInput + profile.AttackModifier!.Value);
        var defenseTotal = checked(request.DefenderDefenseInput + profile.DefenseModifier!.Value);
        var hit = attackTotal >= defenseTotal;
        var damageRoll = hit ? random.NextInclusive(profile.MinimumDamage!.Value, profile.MaximumDamage!.Value) : 0;
        var finalDamage = Math.Max(0, damageRoll);
        var defeated = false;

        if (hit)
        {
            var snapshot = _sink.Capture(request.DefenderId);
            try
            {
                var remainingHp = _sink.ApplyDamage(request.DefenderId, finalDamage, request.AttackId);
                if (remainingHp <= 0)
                {
                    _combat.SetParticipantState(request.DefenderId, RuntimeCombatParticipantState.Defeated, request.ScheduledTurn.WorldTick);
                    defeated = true;
                }
            }
            catch
            {
                _sink.Restore(request.DefenderId, snapshot);
                _consumedTurnKeys.Remove(key);
                throw;
            }
        }

        return new RuntimePhysicalAttackTrace(request.AttackId, _combat.CombatId, request.ScheduledTurn.RoundNumber,
            request.ScheduledTurn.WorldTick, request.AttackerId, request.DefenderId, request.DefinitionId,
            profile.ProfileId, hitRoll, attackTotal, defenseTotal, hit, damageRoll, finalDamage, defeated,
            profile.EvidenceReferences);
    }

    private void ValidateRequest(RuntimePhysicalAttackRequest request)
    {
        RequireCanonical(request.AttackId, "attack.", "PHYSICAL_ATTACK_ID_REQUIRED");
        RequireCanonical(request.RoomId, "room.", "PHYSICAL_ATTACK_ROOM_ID_REQUIRED");
        if (!string.Equals(request.RoomId, _combat.RoomId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_ATTACK_ROOM_MISMATCH");
        if (!string.Equals(request.ScheduledTurn.CombatId, _combat.CombatId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_ATTACK_COMBAT_MISMATCH");
        if (!string.Equals(request.ScheduledTurn.ActorId, request.AttackerId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_ATTACK_TURN_OWNER_MISMATCH");
        if (request.AttackerId.Equals(request.DefenderId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_ATTACK_SELF_TARGET_INVALID");
        RequireActor(request.AttackerId); RequireActor(request.DefenderId);
        if (!_combat.Participants.Any(x => x.ActorId.Equals(request.AttackerId, StringComparison.OrdinalIgnoreCase) && x.State == RuntimeCombatParticipantState.Active)) throw new InvalidOperationException("PHYSICAL_ATTACK_ATTACKER_NOT_ACTIVE");
        if (!_combat.Participants.Any(x => x.ActorId.Equals(request.DefenderId, StringComparison.OrdinalIgnoreCase) && x.State == RuntimeCombatParticipantState.Active)) throw new InvalidOperationException("PHYSICAL_ATTACK_DEFENDER_NOT_ACTIVE");
        var p=request.Profile;
        if (string.IsNullOrWhiteSpace(p.ProfileId) || string.IsNullOrWhiteSpace(p.AttackPath)) throw new InvalidOperationException("PHYSICAL_ATTACK_PROFILE_REQUIRED");
        if (p.MinimumDamage is null || p.MaximumDamage is null || p.AttackModifier is null || p.DefenseModifier is null || p.MinimumHitRoll is null || p.MaximumHitRoll is null) throw new InvalidOperationException("PHYSICAL_ATTACK_PROFILE_UNRESOLVED");
        if (p.MinimumDamage < 0 || p.MinimumDamage > p.MaximumDamage || p.MinimumHitRoll > p.MaximumHitRoll) throw new InvalidOperationException("PHYSICAL_ATTACK_PROFILE_RANGE_INVALID");
        if (p.EvidenceReferences.Count == 0) throw new InvalidOperationException("PHYSICAL_ATTACK_EVIDENCE_REQUIRED");
        if (p.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(p.SafeFallback)) throw new InvalidOperationException("PHYSICAL_ATTACK_FALLBACK_REQUIRED");
    }

    private static void RequireActor(string id) { if (string.IsNullOrWhiteSpace(id) || !(id.StartsWith("character.", StringComparison.OrdinalIgnoreCase)||id.StartsWith("npc.", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("PHYSICAL_ATTACK_CANONICAL_ACTOR_ID_REQUIRED"); }
    private static void RequireCanonical(string value,string prefix,string error) { if(string.IsNullOrWhiteSpace(value)||!value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(error); }
}
