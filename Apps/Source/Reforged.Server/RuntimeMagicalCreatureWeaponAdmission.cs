namespace Reforged.Server;

public sealed record RuntimeMagicalCreatureWeaponDecision(
    bool Allowed,
    string Reason,
    int CreatureMagicalRequirement,
    int HitMagicBypassRank,
    int RemainingMagicalRequirement,
    int WeaponMagicalRank,
    int CreatureRequiredToHitCode,
    int WeaponRequiredToHitCode);

public static class RuntimeMagicalCreatureWeaponAdmission
{
    public static RuntimeMagicalCreatureWeaponDecision Evaluate(
        RuntimeNpcDefinition creature,
        RuntimePlayerCombatProfile player,
        bool usesWieldedWeapon)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(player);
        var weaponMagical = usesWieldedWeapon ? player.WeaponMagicalRank : 0;
        var weaponRequiredToHit = usesWieldedWeapon ? player.WeaponRequiredToHitCode : 0;
        var remaining = Math.Max(0, checked(creature.MagicalRequirement - player.HitMagicBypassRank));

        if (creature.RequiredToHitCode != 0 && weaponRequiredToHit != creature.RequiredToHitCode)
            return new(false,
                $"Only a weapon bearing the required strike code {creature.RequiredToHitCode} can hit {creature.Name}.",
                creature.MagicalRequirement, player.HitMagicBypassRank, remaining, weaponMagical,
                creature.RequiredToHitCode, weaponRequiredToHit);

        if (remaining > weaponMagical)
            return new(false,
                $"Your attack cannot affect {creature.Name}; it requires Magical {creature.MagicalRequirement}, with {remaining} still required after HitMagic.",
                creature.MagicalRequirement, player.HitMagicBypassRank, remaining, weaponMagical,
                creature.RequiredToHitCode, weaponRequiredToHit);

        return new(true, "Decoded Magical, HitMagic, and ReqToHit admission passed.",
            creature.MagicalRequirement, player.HitMagicBypassRank, remaining, weaponMagical,
            creature.RequiredToHitCode, weaponRequiredToHit);
    }
}
