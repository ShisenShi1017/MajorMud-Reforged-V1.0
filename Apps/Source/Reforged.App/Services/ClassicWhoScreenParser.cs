using Reforged.App.Models;

namespace Reforged.App.Services;

public sealed class ClassicWhoScreenParser
{
    private readonly List<ClientOnlinePlayerState> _players = [];
    private DateTimeOffset? _expiresAt;
    private bool _headerSeen;
    private bool _columnsSeen;

    public void NoteOutboundCommand(string command)
    {
        Reset();
        if (string.Equals(command?.Trim(), "who", StringComparison.OrdinalIgnoreCase))
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(8);
    }

    public IReadOnlyList<ClientOnlinePlayerState>? Observe(string line)
    {
        if (_expiresAt is null) return null;
        if (DateTimeOffset.UtcNow > _expiresAt.Value) { Reset(); return null; }
        var text = (line ?? string.Empty).Trim();
        if (!_headerSeen)
        {
            if (text.Equals("Who's in the Realm", StringComparison.OrdinalIgnoreCase))
                _headerSeen = true;
            return null;
        }
        if (!_columnsSeen)
        {
            if (text.Equals("Name | Class | Level", StringComparison.OrdinalIgnoreCase))
                _columnsSeen = true;
            return null;
        }
        if (text.EndsWith(" in the Realm.", StringComparison.OrdinalIgnoreCase))
        {
            var countToken = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!int.TryParse(countToken, out var declaredCount) || declaredCount != _players.Count)
            {
                Reset();
                return null;
            }
            var result = _players.ToArray();
            Reset();
            return result;
        }
        var parts = text.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length == 3 && parts[0].Length > 0 && parts[1].Length > 0 && int.TryParse(parts[2], out var level) && level >= 0)
            _players.Add(new ClientOnlinePlayerState(parts[0], parts[1], level));
        return null;
    }

    public void Reset()
    {
        _players.Clear();
        _expiresAt = null;
        _headerSeen = false;
        _columnsSeen = false;
    }
}
