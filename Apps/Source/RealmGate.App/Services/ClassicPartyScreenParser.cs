using System.Text.RegularExpressions;

namespace RealmGate.App.Services;

internal readonly record struct ClassicPartyMemberObservation(
    string Name, string ClassName, int HealthPercent, int? ResourcePercent,
    string ResourceKind, string Rank, string State, bool Poisoned);

/// <summary>Captures the native PAR travel-party table in an explicit response window.</summary>
internal sealed partial class ClassicPartyScreenParser
{
    private readonly List<ClassicPartyMemberObservation> _members = [];
    private bool _armed;
    private bool _started;
    private DateTimeOffset _expiresAt;

    public void NoteOutboundCommand(string? command)
    {
        var text = command?.Trim() ?? string.Empty;
        _armed = text.Length >= 3 && text.Length <= 5 && "party".StartsWith(text, StringComparison.OrdinalIgnoreCase);
        _started = false;
        _members.Clear();
        _expiresAt = _armed ? DateTimeOffset.UtcNow.AddSeconds(8) : DateTimeOffset.MinValue;
    }

    public IReadOnlyList<ClassicPartyMemberObservation>? Observe(string? line)
    {
        if (!_armed || DateTimeOffset.UtcNow > _expiresAt) { Reset(); return null; }
        var text = line?.TrimEnd() ?? string.Empty;
        if (NotInParty().IsMatch(text)) return Complete();
        if (Header().IsMatch(text)) { _started = true; return null; }
        if (!_started) return null;
        if (Prompt().IsMatch(text) || string.IsNullOrWhiteSpace(text)) return Complete();

        var match = Row().Match(text);
        if (!match.Success) return null;
        var rank = match.Groups["rank"].Value.Trim();
        rank = rank.Equals("Frontrank", StringComparison.OrdinalIgnoreCase) ? "Frontrunner"
            : rank.Equals("Backrank", StringComparison.OrdinalIgnoreCase) ? "Backrank"
            : "Midrange";
        var state = match.Groups["state"].Value switch { "R" => "Resting", "M" => "Meditating", _ => "Standing" };
        _members.Add(new ClassicPartyMemberObservation(
            match.Groups["name"].Value.Trim(), match.Groups["class"].Value.Trim(),
            int.Parse(match.Groups["hp"].Value),
            match.Groups["resource"].Success ? int.Parse(match.Groups["resource"].Value) : null,
            match.Groups["kind"].Value.Equals("K", StringComparison.OrdinalIgnoreCase) ? "Kai" : "Mana",
            rank, state, match.Groups["poison"].Success));
        return null;
    }

    public void Reset() { _armed = _started = false; _members.Clear(); _expiresAt = DateTimeOffset.MinValue; }

    private IReadOnlyList<ClassicPartyMemberObservation> Complete()
    {
        var result = _members.GroupBy(member => member.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.Last()).ToArray();
        Reset();
        return result;
    }

    [GeneratedRegex(@"^The following people are in your travel party:\s*$", RegexOptions.IgnoreCase)] private static partial Regex Header();
    [GeneratedRegex(@"^You are not in a party at the present time\.?$", RegexOptions.IgnoreCase)] private static partial Regex NotInParty();
    [GeneratedRegex(@"^\s*(?<name>\S[\w '-]*?)\s+\((?<class>[^)]+)\)\s*(?:\[(?<kind>[MK]):\s*(?<resource>\d+)%\])?\s*\[H:\s*(?<hp>\d+)%\]\s*(?<poison>P)?\s*(?<state>[RM])?\s*(?:-\s*(?<rank>\w+))?", RegexOptions.CultureInvariant)] private static partial Regex Row();
    [GeneratedRegex(@"^\s*\[(?:HP|H)\s*=", RegexOptions.IgnoreCase)] private static partial Regex Prompt();
}
