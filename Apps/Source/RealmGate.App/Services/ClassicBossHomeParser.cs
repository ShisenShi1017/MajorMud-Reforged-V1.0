using RealmGate.App.Models;

namespace RealmGate.App.Services;

internal sealed class ClassicBossHomeParser
{
    private readonly List<ClientBossHomeState> _rows = [];
    private DateTimeOffset? _expiresAt;
    private bool _headerSeen;
    private bool _columnsSeen;

    public void NoteOutboundCommand(string? command)
    {
        Reset();
        var text = command?.Trim() ?? string.Empty;
        if (text.Equals("whohome", StringComparison.OrdinalIgnoreCase) || text.Equals("whoshome", StringComparison.OrdinalIgnoreCase))
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(15);
    }

    public IReadOnlyList<ClientBossHomeState>? Observe(string? line)
    {
        if (_expiresAt is null) return null;
        if (DateTimeOffset.UtcNow > _expiresAt.Value) { Reset(); return null; }
        var text = line?.Trim() ?? string.Empty;
        if (!_headerSeen) { if (text.Equals("Boss Home Status", StringComparison.OrdinalIgnoreCase)) _headerSeen = true; return null; }
        if (!_columnsSeen) { if (text.Equals("Boss | State | Current room | Home room | Respawn UTC", StringComparison.OrdinalIgnoreCase)) _columnsSeen = true; return null; }
        if (text.StartsWith("Total bosses:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(text["Total bosses:".Length..].Trim(), out var count) || count != _rows.Count) { Reset(); return null; }
            var result = _rows.ToArray(); Reset(); return result;
        }
        var parts = text.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 5 || parts[0].Length == 0 || parts[3].Length == 0 ||
            !parts[1].Equals("ALIVE", StringComparison.OrdinalIgnoreCase) && !parts[1].Equals("DEAD", StringComparison.OrdinalIgnoreCase)) return null;
        DateTimeOffset? respawnAt = null;
        if (!parts[4].Equals("-", StringComparison.Ordinal) && DateTimeOffset.TryParse(parts[4], out var parsed)) respawnAt = parsed.ToUniversalTime();
        _rows.Add(new ClientBossHomeState(parts[0], parts[1].ToUpperInvariant(), parts[2], parts[3], respawnAt));
        return null;
    }

    public void Reset()
    {
        _rows.Clear(); _expiresAt = null; _headerSeen = false; _columnsSeen = false;
    }
}
