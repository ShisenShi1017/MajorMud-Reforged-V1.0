namespace RealmGate.Server;

public enum RuntimeCombatSpellDefinitionKind { Weapon, Spell }

public sealed record RuntimeCombatSpellDefinitionBinding(
    string BindingId,
    RuntimeCombatSpellDefinitionKind Kind,
    string DefinitionId,
    string ProfileId,
    int MinimumMagnitude,
    int MaximumMagnitude,
    string DamageType,
    string? ResistanceType,
    string? SavingThrowType,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public interface IRuntimeCombatSpellBindingCatalog
{
    RuntimeCombatSpellDefinitionBinding GetRequired(string definitionId);
}

public sealed class RuntimeCombatSpellBindingCatalog : IRuntimeCombatSpellBindingCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeCombatSpellDefinitionBinding> _bindings;

    public RuntimeCombatSpellBindingCatalog(IEnumerable<RuntimeCombatSpellDefinitionBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var materialized = bindings.ToArray();
        foreach (var binding in materialized) Validate(binding);
        _bindings = materialized.ToDictionary(x => x.DefinitionId, StringComparer.OrdinalIgnoreCase);
        if (_bindings.Count != materialized.Length) throw new InvalidOperationException("COMBAT_SPELL_DUPLICATE_DEFINITION_BINDING");
    }

    public RuntimeCombatSpellDefinitionBinding GetRequired(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId)) throw new InvalidOperationException("COMBAT_SPELL_DEFINITION_ID_REQUIRED");
        return _bindings.TryGetValue(definitionId, out var binding)
            ? binding
            : throw new InvalidOperationException("COMBAT_SPELL_DEFINITION_UNBOUND");
    }

    private static void Validate(RuntimeCombatSpellDefinitionBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.BindingId) || string.IsNullOrWhiteSpace(binding.DefinitionId) ||
            string.IsNullOrWhiteSpace(binding.ProfileId) || string.IsNullOrWhiteSpace(binding.DamageType))
            throw new InvalidOperationException("COMBAT_SPELL_BINDING_ID_REQUIRED");
        if (binding.MinimumMagnitude < 0 || binding.MinimumMagnitude > binding.MaximumMagnitude)
            throw new InvalidOperationException("COMBAT_SPELL_BINDING_RANGE_INVALID");
        if (binding.EvidenceReferences.Count == 0) throw new InvalidOperationException("COMBAT_SPELL_BINDING_EVIDENCE_REQUIRED");
        if (binding.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(binding.SafeFallback))
            throw new InvalidOperationException("COMBAT_SPELL_BINDING_FALLBACK_REQUIRED");
        var expectedPrefix = binding.Kind == RuntimeCombatSpellDefinitionKind.Spell ? "spell." : "item.";
        if (!binding.DefinitionId.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("COMBAT_SPELL_BINDING_CANONICAL_ID_INVALID");
    }
}

public sealed record RuntimeCombatSpellBoundRequest(
    string ResolutionId,
    string CombatInstanceId,
    int WorldTick,
    string ActorId,
    string TargetId,
    string DefinitionId,
    RuntimeDamageDisposition Disposition,
    int ActorPower,
    int TargetResistance,
    int TargetSavingThrow,
    ulong RandomSeed);

public sealed class RuntimeCombatSpellBoundResolver
{
    private readonly IRuntimeCombatSpellBindingCatalog _catalog;
    private readonly IReadOnlyDictionary<string, RuntimeResolutionProfile> _profiles;
    private readonly RuntimeCombatSpellResolutionEngine _engine;

    public RuntimeCombatSpellBoundResolver(
        IRuntimeCombatSpellBindingCatalog catalog,
        IEnumerable<RuntimeResolutionProfile> profiles,
        RuntimeCombatSpellResolutionEngine engine)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _profiles = (profiles ?? throw new ArgumentNullException(nameof(profiles)))
            .ToDictionary(x => x.ProfileId, StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeCombatSpellResolutionTrace Resolve(RuntimeCombatSpellBoundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = _catalog.GetRequired(request.DefinitionId);
        if (!_profiles.TryGetValue(binding.ProfileId, out var profile))
            throw new InvalidOperationException("COMBAT_SPELL_PROFILE_UNAVAILABLE");
        if (profile.MinimumRoll != binding.MinimumMagnitude || profile.MaximumRoll != binding.MaximumMagnitude ||
            !string.Equals(profile.DamageType, binding.DamageType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("COMBAT_SPELL_PROFILE_BINDING_MISMATCH");
        return _engine.Resolve(new RuntimeCombatSpellResolutionRequest(
            request.ResolutionId, request.CombatInstanceId, request.WorldTick, request.ActorId, request.TargetId,
            request.DefinitionId, request.Disposition, profile, request.ActorPower, request.TargetResistance,
            request.TargetSavingThrow, request.RandomSeed));
    }
}
