using System.Text.RegularExpressions;

namespace Reforged.App.Services;

internal readonly record struct ClassicRoomDisplayObservation(
    string RoomName,
    string RoomKey,
    IReadOnlyList<string> Exits,
    string Description,
    IReadOnlyList<string> Npcs,
    IReadOnlyList<string> Players,
    IReadOnlyList<string> Items,
    string Currency);

/// <summary>
/// Buffers one server-output block and commits a room only when MajorMUD's
/// native Obvious exits line closes the display. This prevents arbitrary
/// capitalized prose from becoming navigation authority.
/// </summary>
internal sealed partial class ClassicRoomDisplayStreamParser
{
    private const int BufferCapacity = 20;
    private readonly List<string> _buffer = [];
    private bool _suppressNextDisplay;
    private DateTimeOffset _suppressionExpiresAt;

    public void NoteOutboundCommand(string? command)
    {
        _suppressNextDisplay = DirectionalLook().IsMatch(command?.Trim() ?? string.Empty);
        _suppressionExpiresAt = _suppressNextDisplay
            ? DateTimeOffset.UtcNow.AddSeconds(4)
            : DateTimeOffset.MinValue;
    }

    public ClassicRoomDisplayObservation? Observe(string? line)
    {
        var text = line?.Trim() ?? string.Empty;
        if (Prompt().IsMatch(text) || CombatBoundary().IsMatch(text))
        {
            _buffer.Clear();
            return null;
        }

        var exits = ExitLine().Match(text);
        if (!exits.Success)
        {
            if (_buffer.Count >= BufferCapacity)
                _buffer.RemoveAt(0);
            _buffer.Add(text);
            return null;
        }

        var identity = FindIdentity();
        var directions = ParseExits(exits.Groups["list"].Value);
        var contents = identity is null ? default : ParseContents(identity.Value.Index);
        _buffer.Clear();
        if (_suppressNextDisplay && DateTimeOffset.UtcNow <= _suppressionExpiresAt)
        {
            _suppressNextDisplay = false;
            _suppressionExpiresAt = DateTimeOffset.MinValue;
            return null;
        }
        _suppressNextDisplay = false;
        _suppressionExpiresAt = DateTimeOffset.MinValue;
        return identity is null
            ? null
            : new ClassicRoomDisplayObservation(identity.Value.Name, identity.Value.Key, directions,
                contents.Description, contents.Npcs, contents.Players, contents.Items, contents.Currency);
    }

    public void Reset()
    {
        _buffer.Clear();
        _suppressNextDisplay = false;
        _suppressionExpiresAt = DateTimeOffset.MinValue;
    }

    private (string Name, string Key, int Index)? FindIdentity()
    {
        var boundary = -1;
        for (var index = _buffer.Count - 1; index >= 0; index--)
        {
            // Blank lines are normal inside MajorMUD room displays, including
            // immediately before "Obvious exits". They must not detach the exit
            // line from the room title. Only actual transition/chat boundaries
            // end the candidate display.
            if (MovementTransition().IsMatch(_buffer[index]) ||
                ChatBoundary().IsMatch(_buffer[index]))
            {
                boundary = index;
                break;
            }
        }

        for (var index = boundary + 1; index < _buffer.Count; index++)
        {
            var candidate = _buffer[index].Trim();
            var keyed = KeyedRoom().Match(candidate);
            if (keyed.Success)
                return (keyed.Groups["name"].Value.Trim(), keyed.Groups["key"].Value.ToUpperInvariant(), index);
            var numbered = NumberedRoom().Match(candidate);
            if (numbered.Success)
                return (
                    numbered.Groups["name"].Value.Trim(),
                    $"{numbered.Groups["map"].Value}:{numbered.Groups["room"].Value}", index);
        }

        for (var index = boundary + 1; index < _buffer.Count; index++)
        {
            var candidate = _buffer[index].Trim();
            if (IsPlausibleTitle(candidate))
                return (candidate, string.Empty, index);
        }
        return null;
    }

    private (string Description, IReadOnlyList<string> Npcs, IReadOnlyList<string> Players, IReadOnlyList<string> Items, string Currency) ParseContents(int titleIndex)
    {
        var description = new List<string>();
        var npcs = new List<string>();
        var players = new List<string>();
        var items = new List<string>();
        var currency = string.Empty;
        for (var index = titleIndex + 1; index < _buffer.Count; index++)
        {
            var line = _buffer[index].Trim();
            if (line.Length == 0) continue;
            if (TryMarkedList(line, "Also here:", npcs) ||
                TryMarkedList(line, "Players:", players) ||
                TryMarkedList(line, "Items:", items)) continue;
            var money = RoomCurrency().Match(line);
            if (money.Success) { currency = money.Groups["value"].Value.Trim().TrimEnd('.'); continue; }
            if (!NonDescriptionRoomLine().IsMatch(line)) description.Add(line);
        }
        return (string.Join(" ", description), Distinct(npcs), Distinct(players), Distinct(items), currency);
    }

    private static bool TryMarkedList(string line, string marker, ICollection<string> output)
    {
        if (!line.StartsWith(marker, StringComparison.OrdinalIgnoreCase)) return false;
        var value = line[marker.Length..].Trim().TrimEnd('.');
        foreach (var name in Regex.Split(value, @"\s*,\s*|\s+and\s+", RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(name)) output.Add(name.Trim());
        return true;
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values) =>
        values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool IsPlausibleTitle(string value)
    {
        if (value.Length is < 3 or > 120 ||
            value.EndsWith('.') || value.EndsWith('!') || value.EndsWith('?') ||
            CommandEcho().IsMatch(value) || NonRoomProse().IsMatch(value))
            return false;
        var words = Regex.Matches(value, @"[A-Za-z][A-Za-z'-]*");
        return words.Count is > 0 and <= 10 && char.IsUpper(value.First(char.IsLetter));
    }

    private static IReadOnlyList<string> ParseExits(string value)
    {
        if (value.Trim().TrimEnd('.').Equals("none", StringComparison.OrdinalIgnoreCase))
            return [];
        var results = new List<string>();
        foreach (var token in Regex.Split(value, @"(?:\s+and\s+)|[,;/\s]+", RegexOptions.IgnoreCase))
        {
            var clean = Regex.Replace(token.Trim().Trim('.', ':', ';'), @"[^A-Za-z]", string.Empty).ToLowerInvariant();
            var direction = clean switch
            {
                "n" or "north" => "north", "ne" or "northeast" => "northeast",
                "e" or "east" => "east", "se" or "southeast" => "southeast",
                "s" or "south" => "south", "sw" or "southwest" => "southwest",
                "w" or "west" => "west", "nw" or "northwest" => "northwest",
                "u" or "up" => "up", "d" or "down" => "down", _ => string.Empty
            };
            if (direction.Length > 0 && !results.Contains(direction, StringComparer.OrdinalIgnoreCase))
                results.Add(direction);
        }
        return results;
    }

    [GeneratedRegex(@"^\s*(?:Obvious\s+)?exits?\s*:\s*(?<list>.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExitLine();
    [GeneratedRegex(@"^\[(?<key>[0-9A-F]{8}|\d+[:/]\d+)\]\s*(?<name>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeyedRoom();
    [GeneratedRegex(@"^(?:Room\s+)?(?:Map\s*)?(?<map>\d+)\s*[-:/ ]\s*(?:Room\s*)?(?<room>\d+)\s*[:|\-]\s*(?<name>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberedRoom();
    [GeneratedRegex(@"^(?:\s*\[(?:HP|H)\s*=|>\s*$|Command\s*[:>])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Prompt();
    [GeneratedRegex(@"^\*Combat (?:Engaged|Off)\*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CombatBoundary();
    [GeneratedRegex(@"^(?:You|He|She|It|They)\s+(?:walk|head|go|run|swim|fly|crawl|climb|step|move|wander)s?\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MovementTransition();
    [GeneratedRegex(@"^(?:You tell|.+ tells you|.+ says|Party|Group|\[Party\]|\[Group\])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChatBoundary();
    [GeneratedRegex(@"^(?:n|ne|e|se|s|sw|w|nw|u|d|north|northeast|east|southeast|south|southwest|west|northwest|up|down|look|l|search|sea)(?:\s+.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CommandEcho();
    [GeneratedRegex(@"^(?:l|look)\s+(?:n|ne|e|se|s|sw|w|nw|u|d|north|northeast|east|southeast|south|southwest|west|northwest|up|down)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DirectionalLook();
    [GeneratedRegex(@"^(?:You|Your|There|Also here|You see|You hear|You feel|Current|Status|Party|Quest|System)\b|\b(?:is|are|was|were|has|have|can|could|would|surrounds?|leads?|stands?|contains?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonRoomProse();
    [GeneratedRegex(@"^You notice\s+(?<value>.+?)\s+here\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoomCurrency();
    [GeneratedRegex(@"^(?:Also here:|Players:|Items:|You notice )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonDescriptionRoomLine();
}
