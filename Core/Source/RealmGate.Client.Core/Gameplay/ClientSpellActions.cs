namespace RealmGate.Client.Gameplay;

public static class ClientSpellActions
{
    public static bool TryCreateCastCommand(
        IEnumerable<string> learnedSpells,
        string? selectedSpell,
        string? target,
        out string command,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(learnedSpells);
        command = string.Empty;
        error = string.Empty;
        var spell = selectedSpell?.Trim() ?? string.Empty;
        var castTarget = target?.Trim() ?? string.Empty;
        if (spell.Length == 0)
        {
            error = "Select a learned spell first.";
            return false;
        }
        if (!IsPlainArgument(spell) || !IsPlainArgument(castTarget))
        {
            error = "Spell and target must be plain single-line text.";
            return false;
        }
        var known = learnedSpells.Select(DisplayName)
            .Any(name => name.Equals(spell, StringComparison.OrdinalIgnoreCase));
        if (!known)
        {
            error = $"'{spell}' is not in the authoritative learned-spell list.";
            return false;
        }
        command = castTarget.Length == 0 ? $"cast {spell}" : $"cast {spell} {castTarget}";
        if (!ClientGameplayAutomationPlanner.IsSingleCommand(command))
        {
            command = string.Empty;
            error = "The generated cast command was rejected by the client safety check.";
            return false;
        }
        return true;
    }

    public static IReadOnlyList<string> DisplayNames(IEnumerable<string> learnedSpells)
    {
        ArgumentNullException.ThrowIfNull(learnedSpells);
        var values = learnedSpells.ToArray();
        var named = values.Where(value => !value.Trim().StartsWith("spell.", StringComparison.OrdinalIgnoreCase))
            .Select(DisplayName).Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        return named.Length > 0
            ? named
            : values.Select(DisplayName).Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string DisplayName(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        var separator = normalized.IndexOf(':');
        return (separator >= 0 ? normalized[(separator + 1)..] : normalized).Trim();
    }

    private static bool IsPlainArgument(string value) =>
        !value.Any(char.IsControl) && value.IndexOfAny([';', '|', '&']) < 0;
}
