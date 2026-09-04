namespace RealmGate.Server;

public enum RuntimePhysicalAttackPath
{
    OneHanded,
    TwoHanded,
    DualWield,
    Ranged,
    Thrown,
    Natural,
    Shield,
    Special
}

public sealed record RuntimePhysicalAttackPathPolicy(
    string PolicyId,
    RuntimePhysicalAttackPath Path,
    int? StrikeCount,
    int? PrimaryDamageBasisPoints,
    int? SecondaryDamageBasisPoints,
    bool RequiresWeapon,
    bool RequiresOffHandWeapon,
    bool RequiresShield,
    bool RequiresAmmunition,
    bool ConsumesThrownWeapon,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimePhysicalAttackPathRequest(
    string SequenceId,
    RuntimePhysicalWeaponAttackRequest Template,
    RuntimePhysicalAttackPathPolicy Policy,
    bool PrimaryWeaponReady,
    bool OffHandWeaponReady,
    bool ShieldReady,
    bool AmmunitionReady,
    bool ThrownWeaponReady);

public sealed record RuntimePhysicalAttackPathStrikeTrace(
    int StrikeIndex,
    string AttackId,
    string DefinitionId,
    int DamageScaleBasisPoints,
    RuntimePhysicalWeaponAttackTrace Trace);

public sealed record RuntimePhysicalAttackPathTrace(
    string SequenceId,
    RuntimePhysicalAttackPath Path,
    int RequestedStrikeCount,
    int ExecutedStrikeCount,
    bool DefenderDefeated,
    IReadOnlyList<RuntimePhysicalAttackPathStrikeTrace> Strikes,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimePhysicalAttackPathExecutor
{
    private readonly RuntimePhysicalWeaponAttackExecutor _weaponExecutor;
    private readonly HashSet<string> _consumedSequences = new(StringComparer.OrdinalIgnoreCase);

    public RuntimePhysicalAttackPathExecutor(RuntimePhysicalWeaponAttackExecutor weaponExecutor)
        => _weaponExecutor = weaponExecutor ?? throw new ArgumentNullException(nameof(weaponExecutor));

    public RuntimePhysicalAttackPathTrace Execute(RuntimePhysicalAttackPathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        if (!_consumedSequences.Add(request.SequenceId))
            throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_SEQUENCE_ALREADY_CONSUMED");

        var strikes = new List<RuntimePhysicalAttackPathStrikeTrace>();
        var defeated = false;
        try
        {
            for (var i = 0; i < request.Policy.StrikeCount!.Value; i++)
            {
                var scale = i == 0 ? request.Policy.PrimaryDamageBasisPoints!.Value : request.Policy.SecondaryDamageBasisPoints!.Value;
                var child = BuildStrike(request, i, scale);
                var trace = _weaponExecutor.Execute(child);
                strikes.Add(new RuntimePhysicalAttackPathStrikeTrace(i, child.BaseRequest.AttackId, child.BaseRequest.DefinitionId, scale, trace));
                if (trace.BaseTrace.DefenderDefeated)
                {
                    defeated = true;
                    break;
                }
            }
        }
        catch
        {
            _consumedSequences.Remove(request.SequenceId);
            throw;
        }

        return new RuntimePhysicalAttackPathTrace(
            request.SequenceId,
            request.Policy.Path,
            request.Policy.StrikeCount.Value,
            strikes.Count,
            defeated,
            strikes,
            request.Policy.EvidenceReferences
                .Concat(request.Template.Binding.EvidenceReferences)
                .Concat(request.Template.Mitigation.EvidenceReferences)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static RuntimePhysicalWeaponAttackRequest BuildStrike(RuntimePhysicalAttackPathRequest request, int index, int scale)
    {
        var template = request.Template;
        var baseRequest = template.BaseRequest;
        var profile = baseRequest.Profile;
        var scaledMinimum = ScaleBasisPoints(profile.MinimumDamage!.Value, scale);
        var scaledMaximum = ScaleBasisPoints(profile.MaximumDamage!.Value, scale);
        if (scaledMaximum < scaledMinimum) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_DAMAGE_RANGE_INVALID");

        var attackId = $"{baseRequest.AttackId}.strike.{index + 1}";
        var definitionId = SelectDefinitionId(request.Policy.Path, baseRequest.DefinitionId, index);
        var childTurn = baseRequest.ScheduledTurn with { Sequence = checked(baseRequest.ScheduledTurn.Sequence * 100L + index + 1L) };
        var childProfile = profile with
        {
            ProfileId = $"{profile.ProfileId}.strike.{index + 1}",
            AttackPath = PathToken(request.Policy.Path, index),
            MinimumDamage = scaledMinimum,
            MaximumDamage = scaledMaximum
        };
        var childBase = baseRequest with
        {
            AttackId = attackId,
            ScheduledTurn = childTurn,
            DefinitionId = definitionId,
            Profile = childProfile,
            RandomSeed = checked(baseRequest.RandomSeed + (ulong)(index * 7919))
        };
        var childBinding = template.Binding with
        {
            BindingId = $"{template.Binding.BindingId}.strike.{index + 1}",
            DefinitionId = definitionId,
            AttackPath = childProfile.AttackPath,
            RequiresWeapon = request.Policy.Path is not RuntimePhysicalAttackPath.Natural,
            RequiresAmmunition = request.Policy.Path is RuntimePhysicalAttackPath.Ranged
        };
        return template with
        {
            BaseRequest = childBase,
            Binding = childBinding,
            WeaponReady = request.PrimaryWeaponReady || request.Policy.Path == RuntimePhysicalAttackPath.Natural,
            AmmunitionReady = request.AmmunitionReady
        };
    }

    private static string SelectDefinitionId(RuntimePhysicalAttackPath path, string baseDefinitionId, int index)
        => path switch
        {
            RuntimePhysicalAttackPath.Natural => baseDefinitionId.StartsWith("attack.", StringComparison.OrdinalIgnoreCase) ? baseDefinitionId : $"attack.natural.{baseDefinitionId.Split('.').Last()}",
            RuntimePhysicalAttackPath.Shield => baseDefinitionId.StartsWith("item.", StringComparison.OrdinalIgnoreCase) ? baseDefinitionId : "item.shield.fixture",
            RuntimePhysicalAttackPath.DualWield when index > 0 => baseDefinitionId.StartsWith("item.", StringComparison.OrdinalIgnoreCase) ? baseDefinitionId : "item.offhand.fixture",
            _ => baseDefinitionId
        };

    private static string PathToken(RuntimePhysicalAttackPath path, int index)
        => path switch
        {
            RuntimePhysicalAttackPath.OneHanded => "one_handed",
            RuntimePhysicalAttackPath.TwoHanded => "two_handed",
            RuntimePhysicalAttackPath.DualWield => index == 0 ? "dual_wield_primary" : "dual_wield_offhand",
            RuntimePhysicalAttackPath.Ranged => "ranged",
            RuntimePhysicalAttackPath.Thrown => "thrown",
            RuntimePhysicalAttackPath.Natural => "natural",
            RuntimePhysicalAttackPath.Shield => "shield",
            RuntimePhysicalAttackPath.Special => "special",
            _ => throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_INVALID")
        };

    private static void Validate(RuntimePhysicalAttackPathRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SequenceId) || !request.SequenceId.StartsWith("attack-sequence.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_SEQUENCE_ID_REQUIRED");
        var p = request.Policy;
        if (string.IsNullOrWhiteSpace(p.PolicyId)) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_POLICY_ID_REQUIRED");
        if (p.StrikeCount is null || p.StrikeCount <= 0 || p.StrikeCount > 8) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_STRIKE_COUNT_UNRESOLVED");
        if (p.PrimaryDamageBasisPoints is null || p.SecondaryDamageBasisPoints is null) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_DAMAGE_SCALE_UNRESOLVED");
        if (p.PrimaryDamageBasisPoints < 0 || p.SecondaryDamageBasisPoints < 0) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_DAMAGE_SCALE_INVALID");
        if (p.EvidenceReferences.Count == 0) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_EVIDENCE_REQUIRED");
        if (p.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(p.SafeFallback)) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_FALLBACK_REQUIRED");
        if (p.Path != RuntimePhysicalAttackPath.DualWield && p.StrikeCount > 1 && p.Path != RuntimePhysicalAttackPath.Special)
            throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_MULTI_STRIKE_UNSUPPORTED");
        if (p.RequiresWeapon && !request.PrimaryWeaponReady) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_PRIMARY_WEAPON_NOT_READY");
        if (p.RequiresOffHandWeapon && !request.OffHandWeaponReady) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_OFFHAND_NOT_READY");
        if (p.RequiresShield && !request.ShieldReady) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_SHIELD_NOT_READY");
        if (p.RequiresAmmunition && !request.AmmunitionReady) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_AMMUNITION_NOT_READY");
        if (p.ConsumesThrownWeapon && !request.ThrownWeaponReady) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_THROWN_WEAPON_NOT_READY");
        if (p.Path == RuntimePhysicalAttackPath.DualWield && (!p.RequiresWeapon || !p.RequiresOffHandWeapon)) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_DUAL_WIELD_REQUIREMENTS_INVALID");
        if (p.Path == RuntimePhysicalAttackPath.Ranged && !p.RequiresAmmunition) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_RANGED_AMMUNITION_POLICY_REQUIRED");
        if (p.Path == RuntimePhysicalAttackPath.Thrown && !p.ConsumesThrownWeapon) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_THROWN_CONSUMPTION_POLICY_REQUIRED");
        if (p.Path == RuntimePhysicalAttackPath.Natural && (p.RequiresWeapon || p.RequiresOffHandWeapon || p.RequiresShield || p.RequiresAmmunition)) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_NATURAL_REQUIREMENTS_INVALID");
        if (p.Path == RuntimePhysicalAttackPath.Shield && !p.RequiresShield) throw new InvalidOperationException("PHYSICAL_ATTACK_PATH_SHIELD_POLICY_REQUIRED");
    }

    private static int ScaleBasisPoints(int value, int basisPoints)
        => checked((int)((long)value * basisPoints / 10000L));
}
