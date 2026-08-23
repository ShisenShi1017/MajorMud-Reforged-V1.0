using System.Text.RegularExpressions;

namespace Reforged.App.Services;

/// <summary>Captures learned spells only inside a bounded outbound SPELLS response.</summary>
internal sealed partial class ClassicSpellbookParser
{
    private readonly List<string> _spells = [];
    private bool _armed;
    private bool _started;
    private DateTimeOffset _expiresAt;

    public void NoteOutboundCommand(string? command)
    {
        var text = command?.Trim() ?? string.Empty;
        _armed = IsSpellCommand(text);
        _started = false;
        _spells.Clear();
        _expiresAt = _armed ? DateTimeOffset.UtcNow.AddSeconds(8) : DateTimeOffset.MinValue;
    }

    public IReadOnlyList<string>? Observe(string? line)
    {
        if (!_armed || DateTimeOffset.UtcNow > _expiresAt) { Reset(); return null; }
        var text = line?.Trim() ?? string.Empty;
        if (Empty().IsMatch(text)) return Complete();
        if (Header().IsMatch(text)) { _started = true; return null; }
        if (!_started) return null;
        if (Prompt().IsMatch(text) || Footer().IsMatch(text)) return Complete();
        var match = Entry().Match(text);
        if (match.Success) _spells.Add(match.Groups["name"].Value.Trim());
        return null;
    }

    public void Reset()
    {
        _armed = _started = false;
        _spells.Clear();
        _expiresAt = DateTimeOffset.MinValue;
    }

    private IReadOnlyList<string> Complete()
    {
        var result = _spells.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Reset();
        return result;
    }

    private static bool IsSpellCommand(string value) =>
        (value.Length >= 3 && value.Length <= 6 && "spells".StartsWith(value, StringComparison.OrdinalIgnoreCase)) ||
        value.Equals("spellbook", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^Spells\s*:$", RegexOptions.IgnoreCase)] private static partial Regex Header();
    [GeneratedRegex(@"^(?:You know no (?:generated )?spells|No spellbook is available)\.?$", RegexOptions.IgnoreCase)] private static partial Regex Empty();
    [GeneratedRegex(@"^(?:(?:spell\.)?\d+|[^:]{1,40})\s*:\s*(?<name>.+?)(?=\s+(?:mana|energy|duration)\s*=|$)", RegexOptions.IgnoreCase)] private static partial Regex Entry();
    [GeneratedRegex(@"^(?:End of spells|Spell list complete)\b", RegexOptions.IgnoreCase)] private static partial Regex Footer();
    [GeneratedRegex(@"^\s*\[(?:HP|H)\s*=", RegexOptions.IgnoreCase)] private static partial Regex Prompt();
}
