namespace RealmGate.Server;

public enum RuntimeResistanceOutcome { Vulnerable, Normal, Partial, Full, Immune, Absorbed }
public enum RuntimeSavingThrowEffect { None, Negate, ReduceMagnitude, ReduceDuration, PreventSecondaryEffect, PartialEffect }
public enum RuntimeResistanceRounding { TowardZero, Floor, Ceiling }

public sealed record RuntimeResistancePolicy(
    string PolicyId, string SpellId, string ResistanceTypeId, int BaseDifficulty,
    int CasterLevelNumerator, int CasterLevelDenominator, int PenetrationBasisPoints,
    int VulnerabilityBasisPoints, int PartialThreshold, int FullThreshold, int ImmunityThreshold,
    int PartialReductionBasisPoints, int FullReductionBasisPoints, bool AllowAbsorption,
    RuntimeResistanceRounding Rounding, RuntimeResolutionConfidence Confidence,
    IReadOnlyList<string> EvidenceReferences, string SafeFallback);

public sealed record RuntimeSavingThrowPolicy(
    string PolicyId, string SpellId, string SaveTypeId, int BaseDifficulty,
    int CasterLevelNumerator, int CasterLevelDenominator, int TargetLevelNumerator,
    int TargetLevelDenominator, int TargetAttributeNumerator, int TargetAttributeDenominator,
    int AutomaticFailureRoll, int AutomaticSuccessRoll, int SuccessThreshold, int PartialThreshold,
    RuntimeSavingThrowEffect SuccessEffect, RuntimeSavingThrowEffect PartialEffect,
    int SuccessMagnitudeReductionBasisPoints, int PartialMagnitudeReductionBasisPoints,
    int SuccessDurationReductionBasisPoints, int PartialDurationReductionBasisPoints,
    RuntimeResolutionConfidence Confidence, IReadOnlyList<string> EvidenceReferences, string SafeFallback);

public sealed record RuntimeResistanceSavingThrowRequest(
    string ResolutionId, string CombatInstanceId, long WorldTick, string CasterId, string TargetId,
    string SpellId, string ResistanceTypeId, string SaveTypeId, int CasterLevel, int TargetLevel,
    int TargetAttribute, int TargetResistanceBasisPoints, int TargetImmunityBasisPoints,
    int TargetAbsorptionBasisPoints, ulong RandomSeed);

public sealed record RuntimeResistanceSavingThrowTrace(
    string ResolutionId, string SpellId, string ResistanceTypeId, string SaveTypeId,
    int ResistanceRoll, int ResistanceScore, RuntimeResistanceOutcome ResistanceOutcome,
    int ResistanceMagnitudeBasisPoints, int AbsorptionBasisPoints, int SaveRoll, int SaveScore,
    RuntimeSaveOutcome SavingThrowOutcome, RuntimeSavingThrowEffect SavingThrowEffect,
    int MagnitudeReductionBasisPoints, int DurationReductionBasisPoints, bool SecondaryEffectPrevented,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeResistanceSavingThrowCatalog
{
    private readonly IReadOnlyDictionary<string,RuntimeResistancePolicy> _resistance;
    private readonly IReadOnlyDictionary<string,RuntimeSavingThrowPolicy> _saves;
    public RuntimeResistanceSavingThrowCatalog(IEnumerable<RuntimeResistancePolicy> resistance, IEnumerable<RuntimeSavingThrowPolicy> saves)
    {
        var r=resistance?.ToArray()??throw new ArgumentNullException(nameof(resistance));
        var s=saves?.ToArray()??throw new ArgumentNullException(nameof(saves));
        foreach(var x in r) Validate(x); foreach(var x in s) Validate(x);
        _resistance=r.ToDictionary(x=>Key(x.SpellId,x.ResistanceTypeId),StringComparer.OrdinalIgnoreCase);
        _saves=s.ToDictionary(x=>Key(x.SpellId,x.SaveTypeId),StringComparer.OrdinalIgnoreCase);
        if(_resistance.Count!=r.Length) throw new InvalidOperationException("RESISTANCE_DUPLICATE_POLICY");
        if(_saves.Count!=s.Length) throw new InvalidOperationException("SAVING_THROW_DUPLICATE_POLICY");
    }
    public RuntimeResistancePolicy GetResistance(string spellId,string typeId)=>_resistance.TryGetValue(Key(spellId,typeId),out var p)?p:throw new InvalidOperationException("RESISTANCE_POLICY_UNRESOLVED");
    public RuntimeSavingThrowPolicy GetSave(string spellId,string typeId)=>_saves.TryGetValue(Key(spellId,typeId),out var p)?p:throw new InvalidOperationException("SAVING_THROW_POLICY_UNRESOLVED");
    private static string Key(string a,string b)=>a+"|"+b;
    private static void Validate(RuntimeResistancePolicy p)
    {
        if(!p.PolicyId.StartsWith("resistancepolicy.",StringComparison.OrdinalIgnoreCase)||!p.SpellId.StartsWith("spell.",StringComparison.OrdinalIgnoreCase)||!p.ResistanceTypeId.StartsWith("resistance.",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("RESISTANCE_CANONICAL_ID_INVALID");
        if(p.CasterLevelDenominator<=0||p.PenetrationBasisPoints is <0 or >10000||p.VulnerabilityBasisPoints is <0 or >10000||p.PartialReductionBasisPoints is <0 or >10000||p.FullReductionBasisPoints is <0 or >10000||p.PartialThreshold>p.FullThreshold||p.FullThreshold>p.ImmunityThreshold) throw new InvalidOperationException("RESISTANCE_POLICY_INVALID");
        if(p.EvidenceReferences.Count==0||(p.Confidence==RuntimeResolutionConfidence.StronglyCorrelated&&string.IsNullOrWhiteSpace(p.SafeFallback))) throw new InvalidOperationException("RESISTANCE_EVIDENCE_INVALID");
    }
    private static void Validate(RuntimeSavingThrowPolicy p)
    {
        if(!p.PolicyId.StartsWith("savepolicy.",StringComparison.OrdinalIgnoreCase)||!p.SpellId.StartsWith("spell.",StringComparison.OrdinalIgnoreCase)||!p.SaveTypeId.StartsWith("save.",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("SAVING_THROW_CANONICAL_ID_INVALID");
        if(p.CasterLevelDenominator<=0||p.TargetLevelDenominator<=0||p.TargetAttributeDenominator<=0||p.AutomaticFailureRoll<1||p.AutomaticSuccessRoll>100||p.PartialThreshold>p.SuccessThreshold) throw new InvalidOperationException("SAVING_THROW_POLICY_INVALID");
        foreach(var bp in new[]{p.SuccessMagnitudeReductionBasisPoints,p.PartialMagnitudeReductionBasisPoints,p.SuccessDurationReductionBasisPoints,p.PartialDurationReductionBasisPoints}) if(bp is <0 or >10000) throw new InvalidOperationException("SAVING_THROW_BASIS_POINTS_INVALID");
        if(p.EvidenceReferences.Count==0||(p.Confidence==RuntimeResolutionConfidence.StronglyCorrelated&&string.IsNullOrWhiteSpace(p.SafeFallback))) throw new InvalidOperationException("SAVING_THROW_EVIDENCE_INVALID");
    }
}

public sealed class RuntimeSpellResistanceSavingThrow
{
    private readonly RuntimeResistanceSavingThrowCatalog _catalog;
    public RuntimeSpellResistanceSavingThrow(RuntimeResistanceSavingThrowCatalog catalog)=>_catalog=catalog??throw new ArgumentNullException(nameof(catalog));
    public RuntimeResistanceSavingThrowTrace Resolve(RuntimeResistanceSavingThrowRequest r)
    {
        Validate(r); var rp=_catalog.GetResistance(r.SpellId,r.ResistanceTypeId); var sp=_catalog.GetSave(r.SpellId,r.SaveTypeId);
        var random=new RuntimeDeterministicRandom(r.RandomSeed);
        var resistanceRoll=random.NextInclusive(1,100);
        var casterContribution=Scale(r.CasterLevel,rp.CasterLevelNumerator,rp.CasterLevelDenominator,rp.Rounding);
        var effectiveResistance=Math.Clamp(checked(r.TargetResistanceBasisPoints-rp.PenetrationBasisPoints),0,10000);
        var resistanceScore=checked(rp.BaseDifficulty+resistanceRoll+effectiveResistance/100-casterContribution);
        RuntimeResistanceOutcome ro;
        if(r.TargetImmunityBasisPoints>=10000||resistanceScore>=rp.ImmunityThreshold) ro=RuntimeResistanceOutcome.Immune;
        else if(rp.AllowAbsorption&&r.TargetAbsorptionBasisPoints>0&&resistanceScore>=rp.FullThreshold) ro=RuntimeResistanceOutcome.Absorbed;
        else if(resistanceScore>=rp.FullThreshold) ro=RuntimeResistanceOutcome.Full;
        else if(resistanceScore>=rp.PartialThreshold) ro=RuntimeResistanceOutcome.Partial;
        else if(rp.VulnerabilityBasisPoints>0) ro=RuntimeResistanceOutcome.Vulnerable;
        else ro=RuntimeResistanceOutcome.Normal;
        var resistanceBp=ro switch { RuntimeResistanceOutcome.Immune=>10000, RuntimeResistanceOutcome.Full=>rp.FullReductionBasisPoints, RuntimeResistanceOutcome.Partial=>rp.PartialReductionBasisPoints, RuntimeResistanceOutcome.Vulnerable=>-rp.VulnerabilityBasisPoints, _=>0 };
        var absorptionBp=ro==RuntimeResistanceOutcome.Absorbed?Math.Clamp(r.TargetAbsorptionBasisPoints,0,10000):0;
        var saveRoll=random.NextInclusive(1,100);
        var saveScore=checked(saveRoll+Scale(r.TargetLevel,sp.TargetLevelNumerator,sp.TargetLevelDenominator,RuntimeResistanceRounding.TowardZero)+Scale(r.TargetAttribute,sp.TargetAttributeNumerator,sp.TargetAttributeDenominator,RuntimeResistanceRounding.TowardZero)-Scale(r.CasterLevel,sp.CasterLevelNumerator,sp.CasterLevelDenominator,RuntimeResistanceRounding.TowardZero)-sp.BaseDifficulty);
        RuntimeSaveOutcome so=saveRoll<=sp.AutomaticFailureRoll?RuntimeSaveOutcome.Failed:saveRoll>=sp.AutomaticSuccessRoll?RuntimeSaveOutcome.Succeeded:saveScore>=sp.SuccessThreshold?RuntimeSaveOutcome.Succeeded:saveScore>=sp.PartialThreshold?RuntimeSaveOutcome.Partial:RuntimeSaveOutcome.Failed;
        var effect=so==RuntimeSaveOutcome.Succeeded?sp.SuccessEffect:so==RuntimeSaveOutcome.Partial?sp.PartialEffect:RuntimeSavingThrowEffect.None;
        var mag=so==RuntimeSaveOutcome.Succeeded?sp.SuccessMagnitudeReductionBasisPoints:so==RuntimeSaveOutcome.Partial?sp.PartialMagnitudeReductionBasisPoints:0;
        var dur=so==RuntimeSaveOutcome.Succeeded?sp.SuccessDurationReductionBasisPoints:so==RuntimeSaveOutcome.Partial?sp.PartialDurationReductionBasisPoints:0;
        if(effect==RuntimeSavingThrowEffect.Negate) mag=dur=10000;
        var secondary=effect==RuntimeSavingThrowEffect.PreventSecondaryEffect||effect==RuntimeSavingThrowEffect.Negate;
        return new(r.ResolutionId,r.SpellId,r.ResistanceTypeId,r.SaveTypeId,resistanceRoll,resistanceScore,ro,resistanceBp,absorptionBp,saveRoll,saveScore,so,effect,mag,dur,secondary,rp.EvidenceReferences.Concat(sp.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
    private static int Scale(int value,int numerator,int denominator,RuntimeResistanceRounding mode)
    {
        if(denominator<=0) throw new InvalidOperationException("RESISTANCE_SCALE_DENOMINATOR_INVALID"); long p=checked((long)value*numerator);
        return mode switch { RuntimeResistanceRounding.Floor=>(int)Math.Floor((decimal)p/denominator),RuntimeResistanceRounding.Ceiling=>(int)Math.Ceiling((decimal)p/denominator),_=>(int)(p/denominator)};
    }
    private static void Validate(RuntimeResistanceSavingThrowRequest r)
    {
        if(!r.ResolutionId.StartsWith("resistsave.",StringComparison.OrdinalIgnoreCase)||!r.CombatInstanceId.StartsWith("combat.",StringComparison.OrdinalIgnoreCase)||!r.SpellId.StartsWith("spell.",StringComparison.OrdinalIgnoreCase)||!r.ResistanceTypeId.StartsWith("resistance.",StringComparison.OrdinalIgnoreCase)||!r.SaveTypeId.StartsWith("save.",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("RESISTANCE_SAVE_CANONICAL_ID_INVALID");
        if(!(r.CasterId.StartsWith("character.",StringComparison.OrdinalIgnoreCase)||r.CasterId.StartsWith("npc.",StringComparison.OrdinalIgnoreCase))||!(r.TargetId.StartsWith("character.",StringComparison.OrdinalIgnoreCase)||r.TargetId.StartsWith("npc.",StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("RESISTANCE_SAVE_ENTITY_ID_INVALID");
        if(r.WorldTick<0||r.CasterLevel<0||r.TargetLevel<0||r.TargetAttribute<0||r.TargetResistanceBasisPoints is <0 or >10000||r.TargetImmunityBasisPoints is <0 or >10000||r.TargetAbsorptionBasisPoints is <0 or >10000) throw new InvalidOperationException("RESISTANCE_SAVE_INPUT_INVALID");
    }
}
