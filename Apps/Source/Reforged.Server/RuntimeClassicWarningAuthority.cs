namespace Reforged.Server;

/// <summary>
/// Exact character-side admission for native positive Evil-Point actions. Once an action is
/// known to award positive EP, WCCMMUD checks warning state, then the strict EP>300 lockout,
/// then the Lawful way-of-life marker before committing the EP mutation. Actions whose native
/// owner suppresses the EP award do not enter this gate.
/// </summary>
public static class RuntimeClassicWarningAuthority
{
    public const string ConditionKey = "ui.classic.warning.enabled";
    public const bool ExecutableEvilPointSafeguardConnected = true;
    public const string WarningBlockMessage = "To do this action, you must turn off your evil warnings.";
    public const string EvilSideLockoutMessage = "You have progressed too far to the evil side to do this action.";
    public const string LawfulWayOfLifeMessage = "You have chosen a way of life which does not allow this action.";

    public static bool IsEnabled(RuntimeCharacterRecord character) =>
        character.Conditions.TryGetValue(ConditionKey, out var raw)
        && raw.Equals("1", StringComparison.OrdinalIgnoreCase);

    public static bool IsLawful(RuntimeCharacterRecord character) =>
        character.SemanticState?.Alignment?.Reputation.Equals("Lawful", StringComparison.OrdinalIgnoreCase) == true ||
        (character.Conditions.TryGetValue("lawful", out var raw) &&
         (raw.Equals("1", StringComparison.OrdinalIgnoreCase) || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)));

    public static bool ShouldBlockPositiveEvilPointAction(
        RuntimeCharacterRecord character,
        int evilPointsBefore,
        int evilPointsAfter) =>
        IsEnabled(character) && evilPointsAfter > evilPointsBefore;

    public static bool ShouldBlockPositiveEvilPointAction(RuntimeCharacterRecord before, RuntimeCharacterRecord after) =>
        ShouldBlockPositiveEvilPointAction(before, ReadEvilPoints(before), ReadEvilPoints(after));

    public static bool TryResolvePositiveEvilPointActionBlock(
        RuntimeCharacterRecord character,
        int evilPointsBefore,
        int evilPointsAfter,
        out string message)
    {
        message = string.Empty;
        if (evilPointsAfter <= evilPointsBefore) return false;
        if (IsEnabled(character)) { message = WarningBlockMessage; return true; }
        if (evilPointsBefore > RuntimeClassicDeathLawPvpAuthority.FiendAttackLockoutEvilPoints) { message = EvilSideLockoutMessage; return true; }
        if (IsLawful(character)) { message = LawfulWayOfLifeMessage; return true; }
        return false;
    }

    public static bool TryResolvePositiveEvilPointActionBlock(RuntimeCharacterRecord before, RuntimeCharacterRecord after, out string message) =>
        TryResolvePositiveEvilPointActionBlock(before, ReadEvilPoints(before), ReadEvilPoints(after), out message);

    public static int ReadEvilPoints(RuntimeCharacterRecord character)
    {
        if (character.SemanticState?.Alignment is { } alignment) return alignment.EvilPoints;
        return character.Conditions.TryGetValue("evil_points", out var raw) && int.TryParse(raw, out var parsed) ? parsed : 0;
    }
}
