namespace RealmGate.Server;

public enum RuntimeControlEffectKind { Fear, Charm }
public enum RuntimeConcealmentKind { None, Hidden, Invisible }

public sealed record RuntimeCombatControlPolicy(
    string PolicyId, string SpellId, RuntimeControlEffectKind Kind,
    bool RequiresLivingTarget, bool BreaksExistingControl, bool EndsOnControllerDeath,
    bool EndsOnTargetDeath, bool EndsOnRoomSeparation, bool RequestsForcedMovement,
    RuntimeResolutionConfidence Confidence, IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeCombatControlRequest(
    string ControlEventId, string SpellId, string ControllerId, string TargetId,
    string RoomId, bool TargetLiving, bool ControllerLiving, bool SameRoom,
    IReadOnlyList<RuntimeSpellEffectInstance> ActiveEffects);

public sealed record RuntimeCombatControlDecision(
    string ControlEventId, RuntimeControlEffectKind Kind, string ControllerId,
    string TargetId, bool Accepted, bool ReplaceExistingControl,
    bool RequestForcedMovement, IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeVisibilityPolicy(
    string PolicyId, string SpellId, RuntimeConcealmentKind Concealment,
    int ConcealmentPerMagnitude, int DetectionPerMagnitude,
    bool BreaksOnPhysicalAttack, bool BreaksOnSpellCast, bool BreaksOnMovement,
    RuntimeResolutionConfidence Confidence, IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeVisibilityRequest(
    string ObserverId, string TargetId, bool SameRoom, bool TargetLiving,
    int BaseDetection, int BaseVisibility,
    IReadOnlyList<RuntimeSpellEffectInstance> ObserverEffects,
    IReadOnlyList<RuntimeSpellEffectInstance> TargetEffects);

public sealed record RuntimeVisibilityContribution(
    string EffectId, string SpellId, int DetectionDelta, int ConcealmentDelta,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeVisibilityDecision(
    string ObserverId, string TargetId, int FinalDetection, int FinalConcealment,
    bool CanObserve, bool CanTarget, IReadOnlyList<RuntimeVisibilityContribution> Contributions,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeCombatControlPerceptionCoordinator
{
    private readonly IReadOnlyDictionary<string, RuntimeCombatControlPolicy> _controlPolicies;
    private readonly IReadOnlyDictionary<string, RuntimeVisibilityPolicy> _visibilityPolicies;
    private readonly HashSet<string> _controlEvents = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatControlPerceptionCoordinator(
        IEnumerable<RuntimeCombatControlPolicy> controlPolicies,
        IEnumerable<RuntimeVisibilityPolicy> visibilityPolicies)
    {
        var controls = (controlPolicies ?? throw new ArgumentNullException(nameof(controlPolicies))).ToArray();
        var visibility = (visibilityPolicies ?? throw new ArgumentNullException(nameof(visibilityPolicies))).ToArray();
        _controlPolicies = controls.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        _visibilityPolicies = visibility.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_controlPolicies.Count != controls.Length) throw new InvalidOperationException("CONTROL_DUPLICATE_POLICY");
        if (_visibilityPolicies.Count != visibility.Length) throw new InvalidOperationException("VISIBILITY_DUPLICATE_POLICY");
        foreach (var p in controls) ValidateControlPolicy(p);
        foreach (var p in visibility) ValidateVisibilityPolicy(p);
    }

    public RuntimeCombatControlDecision ResolveControl(RuntimeCombatControlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireCanonical(request.ControlEventId, @"^controlevent\.\d+$", "CONTROL_EVENT_ID_INVALID");
        RequireActor(request.ControllerId); RequireActor(request.TargetId);
        RequireCanonical(request.SpellId, @"^spell\.\d+$", "CONTROL_SPELL_ID_INVALID");
        RequireCanonical(request.RoomId, @"^room\.\d+$", "CONTROL_ROOM_ID_INVALID");
        if (!_controlPolicies.TryGetValue(request.SpellId, out var policy)) throw new InvalidOperationException("CONTROL_POLICY_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("CONTROL_POLICY_NOT_PROMOTED");
        if (!_controlEvents.Add(request.ControlEventId)) throw new InvalidOperationException("CONTROL_EVENT_DUPLICATE");
        try
        {
            if (policy.RequiresLivingTarget && (!request.TargetLiving || !request.ControllerLiving)) throw new InvalidOperationException("CONTROL_LIVING_STATE_INVALID");
            if (!request.SameRoom) throw new InvalidOperationException("CONTROL_ROOM_SEPARATION_INVALID");
            var existing = request.ActiveEffects?.Where(x => x.State == RuntimeSpellEffectState.Active && x.TargetId.Equals(request.TargetId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.AppliedTick).ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).ToArray()
                ?? throw new InvalidOperationException("CONTROL_ACTIVE_EFFECTS_REQUIRED");
            var replace = existing.Any(x => _controlPolicies.ContainsKey(x.SpellId));
            if (replace && !policy.BreaksExistingControl) throw new InvalidOperationException("CONTROL_EXISTING_OWNER_REJECTED");
            return new RuntimeCombatControlDecision(request.ControlEventId, policy.Kind, request.ControllerId, request.TargetId, true,
                replace, policy.RequestsForcedMovement, policy.EvidenceReferences);
        }
        catch { _controlEvents.Remove(request.ControlEventId); throw; }
    }

    public RuntimeVisibilityDecision ResolveVisibility(RuntimeVisibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); RequireActor(request.ObserverId); RequireActor(request.TargetId);
        if (!request.SameRoom || !request.TargetLiving) return new RuntimeVisibilityDecision(request.ObserverId, request.TargetId,
            request.BaseDetection, request.BaseVisibility, false, false, Array.Empty<RuntimeVisibilityContribution>(), Array.Empty<string>());
        var detection = request.BaseDetection; var concealment = request.BaseVisibility;
        var contributions = new List<RuntimeVisibilityContribution>();
        Apply(request.ObserverEffects, request.ObserverId, true);
        Apply(request.TargetEffects, request.TargetId, false);
        var canObserve = detection >= concealment;
        return new RuntimeVisibilityDecision(request.ObserverId, request.TargetId, detection, concealment, canObserve, canObserve,
            contributions, contributions.SelectMany(x => x.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        void Apply(IReadOnlyList<RuntimeSpellEffectInstance> effects, string owner, bool observer)
        {
            if (effects is null) throw new InvalidOperationException("VISIBILITY_EFFECTS_REQUIRED");
            foreach (var effect in effects.Where(x => x.State == RuntimeSpellEffectState.Active && x.TargetId.Equals(owner, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.AppliedTick).ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase))
            {
                if (!_visibilityPolicies.TryGetValue(effect.SpellId, out var policy)) continue;
                if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("VISIBILITY_POLICY_NOT_PROMOTED");
                var magnitude = checked(effect.Magnitude * effect.StackCount);
                var dd = observer ? checked(policy.DetectionPerMagnitude * magnitude) : 0;
                var cd = observer ? 0 : checked(policy.ConcealmentPerMagnitude * magnitude);
                detection = checked(detection + dd); concealment = checked(concealment + cd);
                contributions.Add(new RuntimeVisibilityContribution(effect.EffectId, effect.SpellId, dd, cd, policy.EvidenceReferences));
            }
        }
    }

    private static void ValidateControlPolicy(RuntimeCombatControlPolicy p)
    {
        RequireCanonical(p.PolicyId, @"^controlpolicy\.\d+$", "CONTROL_POLICY_ID_INVALID");
        RequireCanonical(p.SpellId, @"^spell\.\d+$", "CONTROL_SPELL_ID_INVALID");
        if (p.EvidenceReferences.Count == 0 || !p.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("CONTROL_EVIDENCE_OR_FALLBACK_INVALID");
    }
    private static void ValidateVisibilityPolicy(RuntimeVisibilityPolicy p)
    {
        RequireCanonical(p.PolicyId, @"^visibilitypolicy\.\d+$", "VISIBILITY_POLICY_ID_INVALID");
        RequireCanonical(p.SpellId, @"^spell\.\d+$", "VISIBILITY_SPELL_ID_INVALID");
        if (p.ConcealmentPerMagnitude < 0 || p.DetectionPerMagnitude < 0) throw new InvalidOperationException("VISIBILITY_MAGNITUDE_INVALID");
        if (p.EvidenceReferences.Count == 0 || !p.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("VISIBILITY_EVIDENCE_OR_FALLBACK_INVALID");
    }
    private static void RequireActor(string id) => RequireCanonical(id, @"^(character|npc)\.\d+$", "CONTROL_VISIBILITY_ACTOR_ID_INVALID");
    private static void RequireCanonical(string value, string pattern, string error)
    { if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, pattern)) throw new InvalidOperationException(error); }
}
