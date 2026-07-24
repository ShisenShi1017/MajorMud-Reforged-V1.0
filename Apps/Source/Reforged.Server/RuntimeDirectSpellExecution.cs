namespace Reforged.Server;

public enum RuntimeDirectSpellFormulaKind
{
    Fixed,
    RandomRange,
    LevelScaled,
    AttributeScaled,
    PercentageOfTargetMaximum,
    Drain,
    ResourceRestoration
}

public enum RuntimeDirectSpellResourceKind { HitPoints, Mana, Energy }
public enum RuntimeDirectSpellRoundingMode { TowardZero, Floor, Ceiling }

public sealed record RuntimeDirectSpellFormulaPolicy(
    string PolicyId,
    string SpellId,
    RuntimeDirectSpellFormulaKind FormulaKind,
    RuntimeDamageDisposition Disposition,
    RuntimeDirectSpellResourceKind TargetResource,
    int MinimumRoll,
    int MaximumRoll,
    int FlatMagnitude,
    int LevelNumerator,
    int LevelDenominator,
    int AttributeNumerator,
    int AttributeDenominator,
    int TargetMaximumPercent,
    int ResistanceBasisPoints,
    int SuccessfulSaveReductionBasisPoints,
    int PartialSaveReductionBasisPoints,
    int MinimumFinalMagnitude,
    int MaximumFinalMagnitude,
    RuntimeDirectSpellRoundingMode RoundingMode,
    bool AllowOverheal,
    bool AllowResourceOverflow,
    bool DrainRestoresCaster,
    RuntimeDirectSpellResourceKind DrainRestoreResource,
    RuntimeResolutionConfidence Confidence,
    IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeDirectSpellTargetState(
    string TargetId,
    int CurrentHitPoints,
    int MaximumHitPoints,
    int CurrentMana,
    int MaximumMana,
    int CurrentEnergy,
    int MaximumEnergy);

public interface IRuntimeDirectSpellStateSink
{
    RuntimeDirectSpellTargetState Capture(string targetId);
    void Apply(RuntimeDirectSpellMutation mutation);
    void Restore(string targetId, RuntimeDirectSpellTargetState snapshot);
}

public sealed record RuntimeDirectSpellExecutionRequest(
    string ExecutionId,
    string CombatInstanceId,
    long WorldTick,
    string CasterId,
    string TargetId,
    string SpellId,
    int CasterLevel,
    int CasterAttribute,
    int TargetResistance,
    RuntimeSaveOutcome SavingThrowOutcome,
    ulong RandomSeed);

public sealed record RuntimeDirectSpellMutation(
    string MutationId,
    string EntityId,
    RuntimeDirectSpellResourceKind Resource,
    int Delta,
    string SpellId,
    string ExecutionId);

public sealed record RuntimeDirectSpellExecutionTrace(
    string ExecutionId,
    string CombatInstanceId,
    long WorldTick,
    string CasterId,
    string TargetId,
    string SpellId,
    string PolicyId,
    RuntimeDirectSpellFormulaKind FormulaKind,
    int BaseMagnitude,
    int LevelContribution,
    int AttributeContribution,
    int PercentageContribution,
    int PreResistanceMagnitude,
    int ResistanceReduction,
    RuntimeSaveOutcome SavingThrowOutcome,
    int SavingThrowReduction,
    int FinalMagnitude,
    int TargetAppliedMagnitude,
    int CasterRestoredMagnitude,
    RuntimeDirectSpellResourceKind TargetResource,
    IReadOnlyList<RuntimeDirectSpellMutation> Mutations,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeDirectSpellFormulaCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeDirectSpellFormulaPolicy> _policies;
    public RuntimeDirectSpellFormulaCatalog(IEnumerable<RuntimeDirectSpellFormulaPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var values=policies.ToArray();
        foreach(var policy in values) Validate(policy);
        _policies=values.ToDictionary(x=>x.SpellId,StringComparer.OrdinalIgnoreCase);
        if(_policies.Count!=values.Length) throw new InvalidOperationException("DIRECT_SPELL_DUPLICATE_POLICY");
    }
    public RuntimeDirectSpellFormulaPolicy GetRequired(string spellId) =>
        _policies.TryGetValue(spellId,out var policy) ? policy : throw new InvalidOperationException("DIRECT_SPELL_POLICY_UNRESOLVED");

    private static void Validate(RuntimeDirectSpellFormulaPolicy p)
    {
        if(string.IsNullOrWhiteSpace(p.PolicyId)||!p.PolicyId.StartsWith("spellformula.",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DIRECT_SPELL_POLICY_ID_INVALID");
        if(string.IsNullOrWhiteSpace(p.SpellId)||!p.SpellId.StartsWith("spell.",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DIRECT_SPELL_CANONICAL_SPELL_ID_REQUIRED");
        if(p.MinimumRoll>p.MaximumRoll||p.MinimumRoll<0||p.FlatMagnitude<0)
            throw new InvalidOperationException("DIRECT_SPELL_BASE_RANGE_INVALID");
        if(p.LevelDenominator<=0||p.AttributeDenominator<=0)
            throw new InvalidOperationException("DIRECT_SPELL_SCALE_DENOMINATOR_INVALID");
        if(p.TargetMaximumPercent is <0 or >10000||p.ResistanceBasisPoints is <0 or >10000||
           p.SuccessfulSaveReductionBasisPoints is <0 or >10000||p.PartialSaveReductionBasisPoints is <0 or >10000)
            throw new InvalidOperationException("DIRECT_SPELL_BASIS_POINTS_INVALID");
        if(p.MinimumFinalMagnitude<0||p.MinimumFinalMagnitude>p.MaximumFinalMagnitude)
            throw new InvalidOperationException("DIRECT_SPELL_FINAL_RANGE_INVALID");
        if(p.EvidenceReferences.Count==0) throw new InvalidOperationException("DIRECT_SPELL_EVIDENCE_REQUIRED");
        if(p.Confidence==RuntimeResolutionConfidence.StronglyCorrelated&&string.IsNullOrWhiteSpace(p.SafeFallback))
            throw new InvalidOperationException("DIRECT_SPELL_FALLBACK_REQUIRED");
    }
}

public sealed class RuntimeDirectSpellExecution
{
    private readonly RuntimeDirectSpellFormulaCatalog _catalog;
    private readonly IRuntimeDirectSpellStateSink _sink;
    public RuntimeDirectSpellExecution(RuntimeDirectSpellFormulaCatalog catalog, IRuntimeDirectSpellStateSink sink)
    { _catalog=catalog??throw new ArgumentNullException(nameof(catalog)); _sink=sink??throw new ArgumentNullException(nameof(sink)); }

    public RuntimeDirectSpellExecutionTrace Execute(RuntimeDirectSpellExecutionRequest request) => Execute(request, null);

    public RuntimeDirectSpellExecutionTrace Execute(RuntimeDirectSpellExecutionRequest request, RuntimeResistanceSavingThrowTrace? resolution)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var p=_catalog.GetRequired(request.SpellId);
        var targetBefore=_sink.Capture(request.TargetId);
        RuntimeDirectSpellTargetState? casterBefore=null;
        if(p.DrainRestoresCaster) casterBefore=_sink.Capture(request.CasterId);
        var random=new RuntimeDeterministicRandom(request.RandomSeed);
        var baseMagnitude=p.FormulaKind==RuntimeDirectSpellFormulaKind.Fixed ? p.FlatMagnitude : random.NextInclusive(p.MinimumRoll,p.MaximumRoll);
        var levelContribution=Scale(request.CasterLevel,p.LevelNumerator,p.LevelDenominator,p.RoundingMode);
        var attributeContribution=Scale(request.CasterAttribute,p.AttributeNumerator,p.AttributeDenominator,p.RoundingMode);
        var targetMaximum=GetMaximum(targetBefore,p.TargetResource);
        var percentageContribution=Scale(targetMaximum,p.TargetMaximumPercent,10000,p.RoundingMode);
        var preResistance=checked(baseMagnitude+levelContribution+attributeContribution+percentageContribution);
        if(resolution is not null)
        {
            if(!resolution.SpellId.Equals(request.SpellId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("DIRECT_SPELL_RESOLUTION_SPELL_MISMATCH");
            if(!resolution.ResolutionId.StartsWith("resistsave.",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("DIRECT_SPELL_RESOLUTION_ID_INVALID");
        }
        var resistanceBp=resolution?.ResistanceMagnitudeBasisPoints ?? Math.Clamp(checked(request.TargetResistance+p.ResistanceBasisPoints),0,10000);
        var resistanceReduction=Scale(preResistance,resistanceBp,10000,p.RoundingMode);
        var postResistance=Math.Max(0,checked(preResistance-resistanceReduction));
        var effectiveSaveOutcome=resolution?.SavingThrowOutcome ?? request.SavingThrowOutcome;
        var saveBp=resolution?.MagnitudeReductionBasisPoints ?? (effectiveSaveOutcome switch {
            RuntimeSaveOutcome.Succeeded=>p.SuccessfulSaveReductionBasisPoints,
            RuntimeSaveOutcome.Partial=>p.PartialSaveReductionBasisPoints,
            _=>0 });
        var saveReduction=Scale(postResistance,saveBp,10000,p.RoundingMode);
        var suppressed=resolution is not null && (resolution.ResistanceOutcome==RuntimeResistanceOutcome.Immune || resolution.SavingThrowEffect==RuntimeSavingThrowEffect.Negate);
        var absorbed=resolution is not null && resolution.ResistanceOutcome==RuntimeResistanceOutcome.Absorbed;
        var final=suppressed ? 0 : Math.Clamp(checked(postResistance-saveReduction),p.MinimumFinalMagnitude,p.MaximumFinalMagnitude);
        var current=GetCurrent(targetBefore,p.TargetResource);
        var effectiveDisposition=absorbed && p.Disposition==RuntimeDamageDisposition.Damage ? RuntimeDamageDisposition.Healing : p.Disposition;
        var applied=effectiveDisposition==RuntimeDamageDisposition.Damage ? Math.Min(final,current) :
            (p.AllowOverheal||p.AllowResourceOverflow ? final : Math.Min(final,Math.Max(0,targetMaximum-current)));
        var targetDelta=effectiveDisposition==RuntimeDamageDisposition.Damage ? -applied : applied;
        var mutations=new List<RuntimeDirectSpellMutation>();
        var targetMutation=new RuntimeDirectSpellMutation($"mutation:{request.ExecutionId}:target",request.TargetId,p.TargetResource,targetDelta,p.SpellId,request.ExecutionId);
        try
        {
            _sink.Apply(targetMutation); mutations.Add(targetMutation);
            var restored=0;
            if(p.DrainRestoresCaster&&applied>0)
            {
                var cb=casterBefore!;
                var cmax=GetMaximum(cb,p.DrainRestoreResource); var ccur=GetCurrent(cb,p.DrainRestoreResource);
                restored=p.AllowResourceOverflow ? applied : Math.Min(applied,Math.Max(0,cmax-ccur));
                if(restored>0)
                {
                    var casterMutation=new RuntimeDirectSpellMutation($"mutation:{request.ExecutionId}:caster",request.CasterId,p.DrainRestoreResource,restored,p.SpellId,request.ExecutionId);
                    _sink.Apply(casterMutation); mutations.Add(casterMutation);
                }
            }
            return new RuntimeDirectSpellExecutionTrace(request.ExecutionId,request.CombatInstanceId,request.WorldTick,request.CasterId,
                request.TargetId,request.SpellId,p.PolicyId,p.FormulaKind,baseMagnitude,levelContribution,attributeContribution,
                percentageContribution,preResistance,resistanceReduction,effectiveSaveOutcome,saveReduction,final,applied,restored,
                p.TargetResource,mutations,p.EvidenceReferences);
        }
        catch
        {
            if(casterBefore is not null) _sink.Restore(request.CasterId,casterBefore);
            _sink.Restore(request.TargetId,targetBefore);
            throw;
        }
    }

    private static int GetCurrent(RuntimeDirectSpellTargetState s,RuntimeDirectSpellResourceKind r)=>r switch {
        RuntimeDirectSpellResourceKind.HitPoints=>s.CurrentHitPoints, RuntimeDirectSpellResourceKind.Mana=>s.CurrentMana, _=>s.CurrentEnergy};
    private static int GetMaximum(RuntimeDirectSpellTargetState s,RuntimeDirectSpellResourceKind r)=>r switch {
        RuntimeDirectSpellResourceKind.HitPoints=>s.MaximumHitPoints, RuntimeDirectSpellResourceKind.Mana=>s.MaximumMana, _=>s.MaximumEnergy};
    private static int Scale(int value,int numerator,int denominator,RuntimeDirectSpellRoundingMode mode)
    {
        if(denominator<=0) throw new InvalidOperationException("DIRECT_SPELL_SCALE_DENOMINATOR_INVALID");
        long product=checked((long)value*numerator);
        return mode switch {
            RuntimeDirectSpellRoundingMode.Floor=>(int)Math.Floor((decimal)product/denominator),
            RuntimeDirectSpellRoundingMode.Ceiling=>(int)Math.Ceiling((decimal)product/denominator),
            _=>checked((int)(product/denominator))};
    }
    private static void ValidateRequest(RuntimeDirectSpellExecutionRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.ExecutionId)||!r.ExecutionId.StartsWith("spellexecution.",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DIRECT_SPELL_EXECUTION_ID_INVALID");
        if(string.IsNullOrWhiteSpace(r.CombatInstanceId)||!r.CombatInstanceId.StartsWith("combat.",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DIRECT_SPELL_COMBAT_ID_INVALID");
        if(!(r.CasterId.StartsWith("character.",StringComparison.OrdinalIgnoreCase)||r.CasterId.StartsWith("npc.",StringComparison.OrdinalIgnoreCase))||
           !(r.TargetId.StartsWith("character.",StringComparison.OrdinalIgnoreCase)||r.TargetId.StartsWith("npc.",StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("DIRECT_SPELL_ENTITY_ID_INVALID");
        if(!r.SpellId.StartsWith("spell.",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("DIRECT_SPELL_CANONICAL_SPELL_ID_REQUIRED");
        if(r.WorldTick<0||r.CasterLevel<0||r.CasterAttribute<0||r.TargetResistance is <0 or >10000)
            throw new InvalidOperationException("DIRECT_SPELL_INPUT_INVALID");
    }
}
