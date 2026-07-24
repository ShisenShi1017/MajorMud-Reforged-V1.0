namespace Reforged.Server;

public enum RuntimeResolutionConfidence { Proven, StronglyCorrelated, WeakHypothesis }
public enum RuntimeDamageDisposition { Damage, Healing }
public enum RuntimeSaveOutcome { NotApplicable, Failed, Partial, Succeeded }

public sealed record RuntimeResolutionProfile(
    string ProfileId,
    RuntimeResolutionConfidence Confidence,
    string DamageType,
    string? ResistanceType,
    string? SavingThrowType,
    int MinimumRoll,
    int MaximumRoll,
    int FlatModifier,
    int ResistanceScale,
    int SavingThrowDifficulty,
    int SaveReductionPercent,
    int MinimumFinalMagnitude,
    int MaximumFinalMagnitude,
    bool AllowCritical,
    int CriticalThreshold,
    int CriticalMultiplierNumerator,
    int CriticalMultiplierDenominator,
    IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeCombatSpellResolutionRequest(
    string ResolutionId,
    string CombatInstanceId,
    int WorldTick,
    string ActorId,
    string TargetId,
    string DefinitionId,
    RuntimeDamageDisposition Disposition,
    RuntimeResolutionProfile Profile,
    int ActorPower,
    int TargetResistance,
    int TargetSavingThrow,
    ulong RandomSeed);

public sealed record RuntimeCombatSpellResolutionTrace(
    string ResolutionId,
    string CombatInstanceId,
    int WorldTick,
    string ActorId,
    string TargetId,
    string DefinitionId,
    string ProfileId,
    RuntimeResolutionConfidence Confidence,
    int BaseRoll,
    int ActorPower,
    int PreCriticalMagnitude,
    bool Critical,
    int PostCriticalMagnitude,
    int ResistanceInput,
    int ResistanceReduction,
    int PostResistanceMagnitude,
    string? SavingThrowType,
    int SavingThrowRoll,
    int SavingThrowDifficulty,
    RuntimeSaveOutcome SavingThrowOutcome,
    int SavingThrowReduction,
    int FinalMagnitude,
    RuntimeDamageDisposition Disposition,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeCombatSpellResolutionEngine
{
    public RuntimeCombatSpellResolutionTrace Resolve(RuntimeCombatSpellResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var random = new RuntimeDeterministicRandom(request.RandomSeed);
        var profile = request.Profile;
        var baseRoll = random.NextInclusive(profile.MinimumRoll, profile.MaximumRoll);
        var preCritical = checked(baseRoll + request.ActorPower + profile.FlatModifier);

        var criticalRoll = profile.AllowCritical ? random.NextInclusive(1, 100) : 0;
        var critical = profile.AllowCritical && criticalRoll >= profile.CriticalThreshold;
        var postCritical = critical
            ? Scale(preCritical, profile.CriticalMultiplierNumerator, profile.CriticalMultiplierDenominator)
            : preCritical;

        var resistanceReduction = profile.ResistanceType is null
            ? 0
            : Math.Max(0, checked(request.TargetResistance * profile.ResistanceScale));
        var postResistance = Math.Max(0, checked(postCritical - resistanceReduction));

        var saveRoll = profile.SavingThrowType is null ? 0 : random.NextInclusive(1, 100);
        var saveOutcome = RuntimeSaveOutcome.NotApplicable;
        var saveReduction = 0;
        if (profile.SavingThrowType is not null)
        {
            var total = checked(saveRoll + request.TargetSavingThrow);
            if (total >= profile.SavingThrowDifficulty)
            {
                saveOutcome = profile.SaveReductionPercent >= 100 ? RuntimeSaveOutcome.Succeeded : RuntimeSaveOutcome.Partial;
                saveReduction = Scale(postResistance, profile.SaveReductionPercent, 100);
            }
            else
            {
                saveOutcome = RuntimeSaveOutcome.Failed;
            }
        }

        var final = Math.Clamp(checked(postResistance - saveReduction), profile.MinimumFinalMagnitude, profile.MaximumFinalMagnitude);
        return new RuntimeCombatSpellResolutionTrace(
            request.ResolutionId, request.CombatInstanceId, request.WorldTick, request.ActorId, request.TargetId,
            request.DefinitionId, profile.ProfileId, profile.Confidence, baseRoll, request.ActorPower,
            preCritical, critical, postCritical, request.TargetResistance, resistanceReduction,
            postResistance, profile.SavingThrowType, saveRoll, profile.SavingThrowDifficulty,
            saveOutcome, saveReduction, final, request.Disposition, profile.EvidenceReferences);
    }

    private static int Scale(int value, int numerator, int denominator)
    {
        if (denominator <= 0) throw new InvalidOperationException("RESOLUTION_INVALID_DENOMINATOR");
        return checked((int)((long)value * numerator / denominator));
    }

    private static void Validate(RuntimeCombatSpellResolutionRequest request)
    {
        var profile = request.Profile;
        if (string.IsNullOrWhiteSpace(request.ResolutionId) || string.IsNullOrWhiteSpace(request.CombatInstanceId) ||
            string.IsNullOrWhiteSpace(request.ActorId) || string.IsNullOrWhiteSpace(request.TargetId) ||
            string.IsNullOrWhiteSpace(request.DefinitionId) || string.IsNullOrWhiteSpace(profile.ProfileId))
            throw new InvalidOperationException("RESOLUTION_ID_REQUIRED");
        if (profile.MinimumRoll > profile.MaximumRoll) throw new InvalidOperationException("RESOLUTION_INVALID_ROLL_RANGE");
        if (profile.MinimumFinalMagnitude < 0 || profile.MinimumFinalMagnitude > profile.MaximumFinalMagnitude)
            throw new InvalidOperationException("RESOLUTION_INVALID_FINAL_RANGE");
        if (profile.ResistanceScale < 0) throw new InvalidOperationException("RESOLUTION_INVALID_RESISTANCE_SCALE");
        if (profile.SaveReductionPercent is < 0 or > 100) throw new InvalidOperationException("RESOLUTION_INVALID_SAVE_REDUCTION");
        if (profile.AllowCritical && (profile.CriticalThreshold is < 1 or > 100 || profile.CriticalMultiplierNumerator < 0 || profile.CriticalMultiplierDenominator <= 0))
            throw new InvalidOperationException("RESOLUTION_INVALID_CRITICAL_PROFILE");
        if (profile.EvidenceReferences.Count == 0) throw new InvalidOperationException("RESOLUTION_EVIDENCE_REQUIRED");
        if (profile.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(profile.SafeFallback))
            throw new InvalidOperationException("RESOLUTION_FALLBACK_REQUIRED");
    }
}

public sealed class RuntimeDeterministicRandom
{
    private ulong _state;
    public RuntimeDeterministicRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    public int NextInclusive(int minimum, int maximum)
    {
        if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        var value = _state * 2685821657736338717UL;
        var width = checked((ulong)((long)maximum - minimum + 1));
        return checked(minimum + (int)(value % width));
    }
}
