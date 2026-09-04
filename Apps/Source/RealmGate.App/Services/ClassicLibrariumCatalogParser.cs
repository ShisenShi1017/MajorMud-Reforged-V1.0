using RealmGate.App.Models;

namespace RealmGate.App.Services;

internal sealed class ClassicLibrariumCatalogParser
{
    private readonly List<ClientServerCatalogFamily> _families = [];
    private DateTimeOffset? _expiresAt;
    private bool _headerSeen;
    private bool _worldSeen;
    private bool _columnsSeen;
    private string _world = string.Empty;
    private string _compileHash = string.Empty;

    public void NoteOutboundCommand(string? command)
    {
        Reset();
        var text = command?.Trim() ?? string.Empty;
        if (text.Equals("librarium", StringComparison.OrdinalIgnoreCase) || text.Equals("library", StringComparison.OrdinalIgnoreCase))
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(15);
    }

    public ClientServerCatalogSnapshot? Observe(string? line)
    {
        if (_expiresAt is null) return null;
        if (DateTimeOffset.UtcNow > _expiresAt.Value) { Reset(); return null; }
        var text = line?.Trim() ?? string.Empty;
        if (!_headerSeen) { if (text.Equals("Librarium Server Catalog", StringComparison.OrdinalIgnoreCase)) _headerSeen = true; return null; }
        if (!_worldSeen)
        {
            var parts = text.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 3 && parts[0].Equals("World", StringComparison.OrdinalIgnoreCase) && parts[1].Length > 0)
            { _world = parts[1]; _compileHash = parts[2]; _worldSeen = true; }
            return null;
        }
        if (!_columnsSeen) { if (text.Equals("Family | Records | SHA256", StringComparison.OrdinalIgnoreCase)) _columnsSeen = true; return null; }
        if (text.StartsWith("Total families:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(text["Total families:".Length..].Trim(), out var count) || count != _families.Count) { Reset(); return null; }
            var snapshot = new ClientServerCatalogSnapshot(_world, _compileHash, _families.ToArray(), DateTimeOffset.Now);
            Reset(); return snapshot;
        }
        var row = text.Split('|', StringSplitOptions.TrimEntries);
        if (row.Length == 3 && row[0].Length > 0 && int.TryParse(row[1], out var records) && records >= 0 && row[2].Length == 64)
            _families.Add(new ClientServerCatalogFamily(row[0], records, row[2]));
        return null;
    }

    public void Reset()
    {
        _families.Clear(); _expiresAt = null; _headerSeen = _worldSeen = _columnsSeen = false;
        _world = _compileHash = string.Empty;
    }
}
