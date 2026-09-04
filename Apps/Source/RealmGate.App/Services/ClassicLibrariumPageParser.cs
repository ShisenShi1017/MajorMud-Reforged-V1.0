using System.Text;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

internal sealed class ClassicLibrariumPageParser
{
    private readonly List<ClientLibrariumRecord> _records = [];
    private DateTimeOffset? _expiresAt;
    private bool _headerSeen;
    private bool _pageSeen;
    private bool _columnsSeen;
    private string _family = string.Empty;
    private string _query = string.Empty;
    private int _offset;
    private int _pageSize;
    private int _total;

    public void NoteOutboundCommand(string? command)
    {
        Reset();
        var text = command?.Trim() ?? string.Empty;
        if (text.StartsWith("librarium page ", StringComparison.OrdinalIgnoreCase) || text.StartsWith("library page ", StringComparison.OrdinalIgnoreCase))
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(20);
    }

    public ClientLibrariumPage? Observe(string? line)
    {
        if (_expiresAt is null) return null;
        if (DateTimeOffset.UtcNow > _expiresAt.Value) { Reset(); return null; }
        var text = line?.Trim() ?? string.Empty;
        if (!_headerSeen) { if (text.Equals("Librarium Server Records", StringComparison.OrdinalIgnoreCase)) _headerSeen = true; return null; }
        if (!_pageSeen)
        {
            var parts = text.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 6 && parts[0].Equals("Page", StringComparison.OrdinalIgnoreCase) && TryDecode(parts[1], out _family) &&
                int.TryParse(parts[2], out _offset) && _offset >= 0 && int.TryParse(parts[3], out _pageSize) && _pageSize is > 0 and <= 100 &&
                int.TryParse(parts[4], out _total) && _total >= 0 && TryDecode(parts[5], out _query)) _pageSeen = true;
            return null;
        }
        if (!_columnsSeen) { if (text.Equals("Record | ID | Name | Summary", StringComparison.OrdinalIgnoreCase)) _columnsSeen = true; return null; }
        if (text.StartsWith("Page records:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(text["Page records:".Length..].Trim(), out var count) || count != _records.Count || count > _pageSize) { Reset(); return null; }
            var result = new ClientLibrariumPage(_family, _query, _offset, _pageSize, _total, _records.ToArray(), DateTimeOffset.Now);
            Reset(); return result;
        }
        var row = text.Split('|', StringSplitOptions.TrimEntries);
        if (row.Length == 4 && row[0].Equals("Record", StringComparison.OrdinalIgnoreCase) &&
            TryDecode(row[1], out var id) && TryDecode(row[2], out var name) && TryDecode(row[3], out var summary) && id.Length > 0)
            _records.Add(new ClientLibrariumRecord(id, name, summary));
        return null;
    }

    private static bool TryDecode(string value, out string decoded)
    {
        try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value)); return true; }
        catch (FormatException) { decoded = string.Empty; return false; }
    }

    public void Reset()
    {
        _records.Clear(); _expiresAt = null; _headerSeen = _pageSeen = _columnsSeen = false;
        _family = _query = string.Empty; _offset = _pageSize = _total = 0;
    }
}
