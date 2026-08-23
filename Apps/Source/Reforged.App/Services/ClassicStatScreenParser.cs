using System.Text.RegularExpressions;

namespace Reforged.App.Services;

internal readonly record struct ClassicStatObservation(
    string CharacterName = "", int? HealthCurrent = null, int? HealthMaximum = null,
    int? ManaCurrent = null, int? ManaMaximum = null, long? Experience = null,
    long? ExperienceNeeded = null, int? Level = null,
    int? Lives = null, int? CharacterPoints = null, long? ExperienceRemaining = null,
    bool ScreenComplete = false, IReadOnlyList<string>? ActiveEffects = null);

/// <summary>Parses character values only in a bounded outbound STAT/EXP response window.</summary>
internal sealed partial class ClassicStatScreenParser
{
    private DateTimeOffset _expiresAt;
    private bool _armed;
    private bool _captured;
    private bool _statusSectionEligible;
    private bool _statusSection;
    private readonly List<string> _activeEffects = [];

    public void NoteOutboundCommand(string? command)
    {
        var text = command?.Trim() ?? string.Empty;
        _armed = IsStatCommand(text) || IsExperienceCommand(text);
        _captured = false;
        _statusSectionEligible = _statusSection = false;
        _activeEffects.Clear();
        _expiresAt = _armed ? DateTimeOffset.UtcNow.AddSeconds(8) : DateTimeOffset.MinValue;
    }

    public ClassicStatObservation? Observe(string? line)
    {
        if (!_armed || DateTimeOffset.UtcNow > _expiresAt) { Reset(); return null; }
        var text = line?.Trim() ?? string.Empty;
        if (Prompt().IsMatch(text))
        {
            if (!_captured) { Reset(); return null; }
            var effects = _activeEffects.Distinct(StringComparer.Ordinal).ToArray();
            Reset();
            return new ClassicStatObservation(ScreenComplete: true, ActiveEffects: effects);
        }
        if (text.Length == 0)
        {
            if (_statusSectionEligible) _statusSection = true;
            return null;
        }

        if (_statusSection && LooksLikeStatusMessage(text))
        {
            _activeEffects.Add(text);
            return null;
        }

        var packed = ExperienceLine().Match(text);
        if (packed.Success && TryLong(packed.Groups["exp"].Value, out var exp) &&
            int.TryParse(packed.Groups["level"].Value, out var level) &&
            TryLong(packed.Groups["remaining"].Value, out var remaining))
        {
            var hasTotal = TryLong(packed.Groups["total"].Value, out var parsedTotal);
            var total = hasTotal ? parsedTotal : SaturatingAdd(exp, remaining);
            _captured = true;
            return new ClassicStatObservation(Experience: exp, ExperienceNeeded: total, Level: level, ExperienceRemaining: remaining);
        }

        var nameMatch = Name().Match(text);
        var name = nameMatch.Success ? nameMatch.Groups["value"].Value.Trim() : string.Empty;
        ReadPair(Hits(), text, out var hp, out var maxHp);
        if (!ReadPair(Mana(), text, out var mana, out var maxMana)) ReadPair(Kai(), text, out mana, out maxMana);
        var parsedLevel = ReadInt(Level(), text);
        var parsedExperience = ReadLong(Experience(), text);
        ReadPair(LivesAndCharacterPoints(), text, out var lives, out var characterPoints);
        if (MagicResistance().IsMatch(text)) _statusSectionEligible = true;
        if (name.Length == 0 && hp is null && mana is null && parsedLevel is null && parsedExperience is null && lives is null) return null;
        _captured = true;
        return new ClassicStatObservation(name, hp, maxHp, mana, maxMana, parsedExperience, null, parsedLevel, lives, characterPoints);
    }

    public void Reset() { _armed = false; _captured = false; _statusSectionEligible = _statusSection = false; _activeEffects.Clear(); _expiresAt = DateTimeOffset.MinValue; }
    private static bool LooksLikeStatusMessage(string value) => value.Length is > 2 and <= 180 &&
        !value.Contains(':') && !value.StartsWith('[') && !value.StartsWith('*');
    private static bool IsStatCommand(string value) => value.Length is >= 2 and <= 4 && "stat".StartsWith(value, StringComparison.OrdinalIgnoreCase);
    private static bool IsExperienceCommand(string value) => value.Length is >= 3 and <= 10 && "experience".StartsWith(value, StringComparison.OrdinalIgnoreCase);
    private static bool ReadPair(Regex regex, string text, out int? current, out int? maximum)
    {
        current = maximum = null; var match = regex.Match(text);
        if (!match.Success || !int.TryParse(match.Groups["current"].Value, out var first) || !int.TryParse(match.Groups["maximum"].Value, out var second)) return false;
        current = Math.Max(0, first); maximum = Math.Max(0, second); return true;
    }
    private static int? ReadInt(Regex regex, string text) { var match = regex.Match(text); return match.Success && int.TryParse(match.Groups["value"].Value, out var value) ? Math.Max(0, value) : null; }
    private static long? ReadLong(Regex regex, string text) { var match = regex.Match(text); return match.Success && TryLong(match.Groups["value"].Value, out var value) ? value : null; }
    private static bool TryLong(string value, out long result) => long.TryParse(value.Replace(",", string.Empty, StringComparison.Ordinal), out result) && result >= 0;
    private static long SaturatingAdd(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;

    [GeneratedRegex(@"^Name:\s*(?<value>.+?)(?=\s{2,}(?:Lives/CP|Race|Class|Hits|Mana|Kai|Strength|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Name();
    [GeneratedRegex(@"\bHits:\s*\*?(?<current>\d+)\s*/\s*\*?(?<maximum>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Hits();
    [GeneratedRegex(@"\bMana:\s*\*?(?<current>\d+)\s*/\s*\*?(?<maximum>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Mana();
    [GeneratedRegex(@"\bKai:\s*\*?(?<current>\d+)\s*/\s*\*?(?<maximum>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Kai();
    [GeneratedRegex(@"\bLevel:\s*\*?(?<value>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Level();
    [GeneratedRegex(@"\bExp:\s*\*?(?<value>[0-9,]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Experience();
    [GeneratedRegex(@"\bLives/CP:\s*\*?(?<current>\d+)\s*/\s*\*?(?<maximum>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex LivesAndCharacterPoints();
    [GeneratedRegex(@"^(?:Exp|Experience):\s*(?<exp>[0-9,]+)\s+Level:\s*(?<level>\d+)\s+(?:Exp|Experience)\s+needed\s+for\s+next\s+level:\s*(?<remaining>[0-9,]+)(?:\s+\((?<total>[0-9,]+)\))?(?:\s+\[\d+%\])?\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex ExperienceLine();
    [GeneratedRegex(@"\bMagicRes:\s*\*?-?\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex MagicResistance();
    [GeneratedRegex(@"^\s*\[(?:HP|H)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Prompt();
}
