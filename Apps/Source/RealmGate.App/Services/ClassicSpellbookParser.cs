using System.Text.RegularExpressions;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

/// <summary>Captures learned spells only inside a bounded outbound SPELLS response.</summary>
internal sealed partial class ClassicSpellbookParser
{
    private readonly List<ClientLearnedSpellState> _spells = [];
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

    public IReadOnlyList<ClientLearnedSpellState>? Observe(string? line)
    {
        if (!_armed || DateTimeOffset.UtcNow > _expiresAt) { Reset(); return null; }
        var text = line?.Trim() ?? string.Empty;
        if (Empty().IsMatch(text)) return Complete();
        if (Header().IsMatch(text)) { _started = true; return null; }
        if (!_started) return null;
        if (Prompt().IsMatch(text) || Footer().IsMatch(text)) return Complete();
        var match = Entry().Match(text);
        if (match.Success)
            _spells.Add(new ClientLearnedSpellState(
                match.Groups["id"].Value.Trim(), match.Groups["name"].Value.Trim(),
                ParseCost(match.Groups["mana"].Value), ParseCost(match.Groups["energy"].Value)));
        return null;
    }

    public void Reset()
    {
        _armed = _started = false;
        _spells.Clear();
        _expiresAt = DateTimeOffset.MinValue;
    }

    private IReadOnlyList<ClientLearnedSpellState> Complete()
    {
        var result = _spells.GroupBy(spell => spell.CanonicalId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToArray();
        Reset();
        return result;
    }

    private static int ParseCost(string value) => int.TryParse(value, out var cost) ? Math.Max(0, cost) : 0;

    private static bool IsSpellCommand(string value) =>
        (value.Length >= 3 && value.Length <= 6 && "spells".StartsWith(value, StringComparison.OrdinalIgnoreCase)) ||
        value.Equals("spellbook", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^Spells\s*:$", RegexOptions.IgnoreCase)] private static partial Regex Header();
    [GeneratedRegex(@"^(?:You know no (?:generated )?spells|No spellbook is available)\.?$", RegexOptions.IgnoreCase)] private static partial Regex Empty();
    [GeneratedRegex(@"^(?<id>(?:spell\.)?\d+|[^:]{1,40})\s*:\s*(?<name>.+?)(?=\s+(?:mana|energy|duration)\s*=|$)(?:\s+mana=(?<mana>\d+))?(?:\s+energy=(?<energy>\d+))?(?:\s+duration=\d+)?$", RegexOptions.IgnoreCase)] private static partial Regex Entry();
    [GeneratedRegex(@"^(?:End of spells|Spell list complete)\b", RegexOptions.IgnoreCase)] private static partial Regex Footer();
    [GeneratedRegex(@"^\s*\[(?:HP|H)\s*=", RegexOptions.IgnoreCase)] private static partial Regex Prompt();
}
