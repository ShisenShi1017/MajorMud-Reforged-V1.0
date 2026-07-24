namespace Reforged.Server;

public enum RuntimeCharacterAbilityOwnership
{
    RaceCreation,
    ClassCreation
}

public sealed record RuntimeCharacterAbilityGrant(
    string AbilityId,
    int Value,
    RuntimeCharacterAbilityOwnership Ownership,
    bool Permanent,
    bool Saved,
    string ResetCondition,
    int SourceOrder);

public sealed record RuntimeCharacterAbilityFoundation(
    string RaceId,
    string ClassId,
    IReadOnlyList<RuntimeCharacterAbilityGrant> Grants)
{
    public IReadOnlyList<string> Validate(RuntimeAbilityCatalog catalog)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(RaceId)) errors.Add("race-id-required");
        if (string.IsNullOrWhiteSpace(ClassId)) errors.Add("class-id-required");
        if (Grants.Any(x => string.IsNullOrWhiteSpace(x.AbilityId))) errors.Add("ability-id-required");
        if (Grants.Any(x => !catalog.TryGet(x.AbilityId, out _))) errors.Add("unknown-character-ability");
        if (Grants.Any(x => !x.Permanent || !x.Saved || !x.ResetCondition.Equals("never", StringComparison.OrdinalIgnoreCase)))
            errors.Add("invalid-creation-ability-lifetime");
        if (Grants.Select(x => x.SourceOrder).Distinct().Count() != Grants.Count) errors.Add("duplicate-source-order");
        return errors;
    }
}

public static class RuntimeCharacterAbilityResolver
{
    public static RuntimeCharacterAbilityFoundation Resolve(RuntimeCharacterDefinition race, RuntimeCharacterDefinition characterClass)
    {
        var grants = new List<RuntimeCharacterAbilityGrant>();
        var order = 0;
        foreach (var grant in race.AbilitySlots)
            grants.Add(ToGrant(grant, RuntimeCharacterAbilityOwnership.RaceCreation, order++));
        foreach (var grant in characterClass.AbilitySlots)
            grants.Add(ToGrant(grant, RuntimeCharacterAbilityOwnership.ClassCreation, order++));
        return new RuntimeCharacterAbilityFoundation(race.Id, characterClass.Id, grants);
    }

    public static void ApplyPermanentCreationGrants(RuntimeActorAbilityState state, RuntimeCharacterAbilityFoundation foundation, DateTimeOffset createdAtUtc)
    {
        foreach (var grant in foundation.Grants)
        {
            state.Learn(grant.AbilityId, createdAtUtc, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ownership"] = grant.Ownership.ToString(),
                ["value"] = grant.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["permanent"] = "true",
                ["saved"] = "true",
                ["reset_condition"] = "never",
                ["source_order"] = grant.SourceOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }
    }

    private static RuntimeCharacterAbilityGrant ToGrant(RuntimeAbilityGrant grant, RuntimeCharacterAbilityOwnership owner, int order) =>
        new($"ability.{grant.AbilityId}", grant.Value, owner, true, true, "never", order);
}
