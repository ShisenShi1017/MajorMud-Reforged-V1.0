namespace Reforged.Server;

public enum RuntimePhysicalDefenseResult { None, Dodged, Parried, Blocked }

public sealed record RuntimePhysicalWeaponBinding(
    string BindingId,
    string DefinitionId,
    string AttackPath,
    bool RequiresWeapon,
    bool RequiresAmmunition,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimePhysicalMitigationProfile(
    string ProfileId,
    int? AutomaticMissMaximum,
    int? AutomaticHitMinimum,
    int? CriticalHitMinimum,
    int? CriticalMultiplierBasisPoints,
    int? DodgeThreshold,
    int? ParryThreshold,
    int? BlockThreshold,
    int? BlockFlatReduction,
    int? ArmorFlatReduction,
    int? ResistanceBasisPoints,
    int? MinimumDamageAfterMitigation,
    IReadOnlyList<string> EvaluationOrder,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimePhysicalWeaponAttackRequest(
    RuntimePhysicalAttackRequest BaseRequest,
    RuntimePhysicalWeaponBinding Binding,
    RuntimePhysicalMitigationProfile Mitigation,
    bool WeaponReady,
    bool AmmunitionReady,
    int DodgeInput,
    int ParryInput,
    int BlockInput);

public sealed record RuntimePhysicalWeaponAttackTrace(
    RuntimePhysicalAttackTrace BaseTrace,
    string BindingId,
    RuntimePhysicalDefenseResult DefenseResult,
    bool AutomaticMiss,
    bool AutomaticHit,
    bool Critical,
    int RawDamage,
    int CriticalDamage,
    int ArmorReducedDamage,
    int ResistanceReducedDamage,
    int BlockReducedDamage,
    int FinalDamage,
    IReadOnlyList<string> EvaluationOrder,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimePhysicalWeaponAttackExecutor
{
    private readonly RuntimeCombatInstanceLifecycle _combat;
    private readonly IRuntimePhysicalDamageSink _sink;
    private readonly HashSet<string> _consumedTurnKeys = new(StringComparer.OrdinalIgnoreCase);

    public RuntimePhysicalWeaponAttackExecutor(RuntimeCombatInstanceLifecycle combat, IRuntimePhysicalDamageSink sink)
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public RuntimePhysicalWeaponAttackTrace Execute(RuntimePhysicalWeaponAttackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var b = request.BaseRequest;
        var p = b.Profile;
        var m = request.Mitigation;
        var turnKey = $"{b.ScheduledTurn.CombatId}:{b.ScheduledTurn.RoundNumber}:{b.ScheduledTurn.ActorId}:{b.ScheduledTurn.Sequence}";
        if (!_consumedTurnKeys.Add(turnKey)) throw new InvalidOperationException("PHYSICAL_WEAPON_TURN_ALREADY_CONSUMED");

        var random = new RuntimeDeterministicRandom(b.RandomSeed);
        var hitRoll = random.NextInclusive(p.MinimumHitRoll!.Value, p.MaximumHitRoll!.Value);
        var attackTotal = checked(hitRoll + b.AttackerAttackInput + p.AttackModifier!.Value);
        var defenseTotal = checked(b.DefenderDefenseInput + p.DefenseModifier!.Value);
        var automaticMiss = hitRoll <= m.AutomaticMissMaximum!.Value;
        var automaticHit = !automaticMiss && hitRoll >= m.AutomaticHitMinimum!.Value;
        var hit = !automaticMiss && (automaticHit || attackTotal >= defenseTotal);
        var defenseResult = RuntimePhysicalDefenseResult.None;

        if (hit && request.DodgeInput >= m.DodgeThreshold!.Value) { defenseResult = RuntimePhysicalDefenseResult.Dodged; hit = false; }
        else if (hit && request.ParryInput >= m.ParryThreshold!.Value) { defenseResult = RuntimePhysicalDefenseResult.Parried; hit = false; }
        else if (hit && request.BlockInput >= m.BlockThreshold!.Value) defenseResult = RuntimePhysicalDefenseResult.Blocked;

        var critical = hit && defenseResult != RuntimePhysicalDefenseResult.Blocked && hitRoll >= m.CriticalHitMinimum!.Value;
        var rawDamage = hit ? random.NextInclusive(p.MinimumDamage!.Value, p.MaximumDamage!.Value) : 0;
        var criticalDamage = critical ? ScaleBasisPoints(rawDamage, m.CriticalMultiplierBasisPoints!.Value) : rawDamage;
        var armorReduced = Math.Max(0, checked(criticalDamage - m.ArmorFlatReduction!.Value));
        var resistanceReduced = Math.Max(0, checked(armorReduced - ScaleBasisPoints(armorReduced, m.ResistanceBasisPoints!.Value)));
        var blockReduced = defenseResult == RuntimePhysicalDefenseResult.Blocked ? Math.Max(0, checked(resistanceReduced - m.BlockFlatReduction!.Value)) : resistanceReduced;
        var finalDamage = hit && blockReduced > 0 ? Math.Max(m.MinimumDamageAfterMitigation!.Value, blockReduced) : 0;
        var defeated = false;

        if (hit)
        {
            var snapshot = _sink.Capture(b.DefenderId);
            try
            {
                var remainingHp = _sink.ApplyDamage(b.DefenderId, finalDamage, b.AttackId);
                if (remainingHp <= 0)
                {
                    _combat.SetParticipantState(b.DefenderId, RuntimeCombatParticipantState.Defeated, b.ScheduledTurn.WorldTick);
                    defeated = true;
                }
            }
            catch
            {
                _sink.Restore(b.DefenderId, snapshot);
                _consumedTurnKeys.Remove(turnKey);
                throw;
            }
        }

        var baseTrace = new RuntimePhysicalAttackTrace(b.AttackId, _combat.CombatId, b.ScheduledTurn.RoundNumber,
            b.ScheduledTurn.WorldTick, b.AttackerId, b.DefenderId, b.DefinitionId, p.ProfileId, hitRoll,
            attackTotal, defenseTotal, hit, rawDamage, finalDamage, defeated, p.EvidenceReferences);
        return new RuntimePhysicalWeaponAttackTrace(baseTrace, request.Binding.BindingId, defenseResult,
            automaticMiss, automaticHit, critical, rawDamage, criticalDamage, armorReduced, resistanceReduced,
            blockReduced, finalDamage, m.EvaluationOrder,
            request.Binding.EvidenceReferences.Concat(m.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private void Validate(RuntimePhysicalWeaponAttackRequest request)
    {
        var b=request.BaseRequest; var p=b.Profile; var binding=request.Binding; var m=request.Mitigation;
        RequireCanonical(b.AttackId,"attack.","PHYSICAL_WEAPON_ATTACK_ID_REQUIRED");
        RequireActor(b.AttackerId); RequireActor(b.DefenderId);
        RequireCanonical(b.RoomId,"room.","PHYSICAL_WEAPON_ROOM_ID_REQUIRED");
        if (!string.Equals(b.RoomId,_combat.RoomId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_WEAPON_ROOM_MISMATCH");
        if (!string.Equals(b.ScheduledTurn.CombatId,_combat.CombatId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_WEAPON_COMBAT_MISMATCH");
        if (!string.Equals(b.ScheduledTurn.ActorId,b.AttackerId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_WEAPON_TURN_OWNER_MISMATCH");
        if (!_combat.Participants.Any(x=>x.ActorId.Equals(b.AttackerId,StringComparison.OrdinalIgnoreCase)&&x.State==RuntimeCombatParticipantState.Active)) throw new InvalidOperationException("PHYSICAL_WEAPON_ATTACKER_NOT_ACTIVE");
        if (!_combat.Participants.Any(x=>x.ActorId.Equals(b.DefenderId,StringComparison.OrdinalIgnoreCase)&&x.State==RuntimeCombatParticipantState.Active)) throw new InvalidOperationException("PHYSICAL_WEAPON_DEFENDER_NOT_ACTIVE");
        if (string.IsNullOrWhiteSpace(binding.BindingId)||string.IsNullOrWhiteSpace(binding.DefinitionId)||string.IsNullOrWhiteSpace(binding.AttackPath)) throw new InvalidOperationException("PHYSICAL_WEAPON_BINDING_REQUIRED");
        if (!string.Equals(binding.DefinitionId,b.DefinitionId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_WEAPON_BINDING_DEFINITION_MISMATCH");
        if (!string.Equals(binding.AttackPath,p.AttackPath,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PHYSICAL_WEAPON_ATTACK_PATH_MISMATCH");
        if (binding.RequiresWeapon&&!request.WeaponReady) throw new InvalidOperationException("PHYSICAL_WEAPON_NOT_READY");
        if (binding.RequiresAmmunition&&!request.AmmunitionReady) throw new InvalidOperationException("PHYSICAL_WEAPON_AMMUNITION_NOT_READY");
        if (binding.EvidenceReferences.Count==0||m.EvidenceReferences.Count==0) throw new InvalidOperationException("PHYSICAL_WEAPON_EVIDENCE_REQUIRED");
        if (p.MinimumDamage is null||p.MaximumDamage is null||p.AttackModifier is null||p.DefenseModifier is null||p.MinimumHitRoll is null||p.MaximumHitRoll is null) throw new InvalidOperationException("PHYSICAL_WEAPON_ATTACK_PROFILE_UNRESOLVED");
        if (m.AutomaticMissMaximum is null||m.AutomaticHitMinimum is null||m.CriticalHitMinimum is null||m.CriticalMultiplierBasisPoints is null||m.DodgeThreshold is null||m.ParryThreshold is null||m.BlockThreshold is null||m.BlockFlatReduction is null||m.ArmorFlatReduction is null||m.ResistanceBasisPoints is null||m.MinimumDamageAfterMitigation is null) throw new InvalidOperationException("PHYSICAL_WEAPON_MITIGATION_PROFILE_UNRESOLVED");
        if (m.EvaluationOrder.Count==0) throw new InvalidOperationException("PHYSICAL_WEAPON_EVALUATION_ORDER_REQUIRED");
        if (m.CriticalMultiplierBasisPoints<0||m.ResistanceBasisPoints is <0 or >10000||m.BlockFlatReduction<0||m.ArmorFlatReduction<0||m.MinimumDamageAfterMitigation<0) throw new InvalidOperationException("PHYSICAL_WEAPON_MITIGATION_RANGE_INVALID");
        if ((binding.Confidence==RuntimeResolutionConfidence.StronglyCorrelated||m.Confidence==RuntimeResolutionConfidence.StronglyCorrelated)&&string.IsNullOrWhiteSpace(m.SafeFallback)) throw new InvalidOperationException("PHYSICAL_WEAPON_FALLBACK_REQUIRED");
    }

    private static int ScaleBasisPoints(int value,int basisPoints)=>checked((int)((long)value*basisPoints/10000L));
    private static void RequireActor(string id){if(string.IsNullOrWhiteSpace(id)||!(id.StartsWith("character.",StringComparison.OrdinalIgnoreCase)||id.StartsWith("npc.",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("PHYSICAL_WEAPON_CANONICAL_ACTOR_ID_REQUIRED");}
    private static void RequireCanonical(string value,string prefix,string error){if(string.IsNullOrWhiteSpace(value)||!value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(error);}
}
