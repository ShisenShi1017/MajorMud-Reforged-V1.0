namespace RealmGate.Server;

public static class ClassicShopPricingPolicy
{
    public const int MinimumEffectivePercent = 10;
    public static int ClampEffectivePercent(int percent) => Math.Max(MinimumEffectivePercent, percent);
}

public static class ClassicVisibilityPolicy
{
    public static bool CanDetect(bool observerHasSeeHidden, bool targetIsHidden, bool targetIsSneaking)
    {
        // WCC correction: sneak must not suppress SEE HIDDEN's hidden-state check.
        if (targetIsHidden && observerHasSeeHidden) return true;
        return !targetIsHidden;
    }
}

public sealed record ClassicAttackPreparationResult(
    ClassicAttackType AttackType,
    bool EngageTarget,
    bool BackstabQualified,
    string? Message);

public static class ClassicAttackPreparationPolicy
{
    public static ClassicAttackPreparationResult PrepareBackstab(
        bool attackerHidden,
        bool alreadyEngaged,
        bool barehanded,
        bool weaponSupportsBackstab,
        bool weaponIsTwoHanded)
    {
        if (!attackerHidden || alreadyEngaged)
            return new(ClassicAttackType.NormalWeapon, true, false, null);

        if (barehanded || (weaponSupportsBackstab && !weaponIsTwoHanded))
            return new(ClassicAttackType.Backstab, true, true, null);

        // Fixed surprise-round exploit: invalid backstab still becomes an engaged normal attack.
        return new(ClassicAttackType.NormalWeapon, true, false,
            weaponIsTwoHanded ? "You cannot backstab with this weapon." : "You cannot backstab with this weapon.");
    }
}

public sealed class ClassicDualLeverCoordinator
{
    private readonly object _gate = new();
    public T ExecuteAtomic<T>(Func<T> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        lock (_gate) return mutation();
    }
}

public static class ClassicGangDeedPolicy
{
    public static bool MayRetainDeed(bool isCurrentGangLeader, bool ownsReferencedGangHouse, bool gangExists)
        => gangExists && isCurrentGangLeader && ownsReferencedGangHouse;
}
