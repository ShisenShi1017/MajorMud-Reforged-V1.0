namespace Reforged.Server;

/// <summary>
/// Classic SET WARNING player safeguard. Native help establishes that WARNING ON
/// warns/stops most evil actions and OFF permits them. Reforged applies the gate
/// only at action sites where the authoritative runtime can prove that the action
/// would award positive Evil Points; it does not guess at unresolved crime classes.
/// </summary>
public static class RuntimeClassicWarningAuthority
{
    public const string ConditionKey = "ui.classic.warning.enabled";
    public const bool ExecutableEvilPointSafeguardConnected = true;

    public static bool IsEnabled(RuntimeCharacterRecord character) =>
        character.Conditions.TryGetValue(ConditionKey, out var raw)
        && raw.Equals("1", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldBlockPositiveEvilPointAction(
        RuntimeCharacterRecord character,
        int evilPointsBefore,
        int evilPointsAfter) =>
        IsEnabled(character) && evilPointsAfter > evilPointsBefore;

    public static bool ShouldBlockPositiveEvilPointAction(RuntimeCharacterRecord before, RuntimeCharacterRecord after) =>
        ShouldBlockPositiveEvilPointAction(before, ReadEvilPoints(before), ReadEvilPoints(after));

    public static int ReadEvilPoints(RuntimeCharacterRecord character)
    {
        if (character.SemanticState?.Alignment is { } alignment) return alignment.EvilPoints;
        return character.Conditions.TryGetValue("evil_points", out var raw) && int.TryParse(raw, out var parsed) ? parsed : 0;
    }
}
