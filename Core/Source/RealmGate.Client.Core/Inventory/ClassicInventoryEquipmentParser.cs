using System.Text;
using System.Text.RegularExpressions;
using RealmGate.Client.Terminal;

namespace RealmGate.Client.Inventory;

public enum ClassicItemScreenKind { Inventory, Equipment, Wealth }

public readonly record struct ClassicItemScreenEntry(string Name, int Quantity, string Slot = "")
{
    public string DisplayText => Quantity > 1 ? $"{Quantity}x {Name}" : Name;
}

public sealed record ClassicItemScreenObservation(
    ClassicItemScreenKind Kind,
    IReadOnlyList<ClassicItemScreenEntry> Entries,
    long? Currency = null,
    long? CarriedWeight = null,
    long? CarryingCapacity = null,
    int? EncumbrancePercent = null,
    string EncumbranceBand = "");

/// <summary>Captures inventory/equipment only inside a bounded, explicitly requested screen.</summary>
public sealed partial class ClassicInventoryEquipmentParser
{
    private readonly List<ClassicItemScreenEntry> _entries = [];
    private readonly Func<DateTimeOffset> _utcNow;
    private ClassicItemScreenKind? _armed;
    private DateTimeOffset _expiresAt;
    private bool _started;
    private long? _currency;
    private long? _carriedWeight;
    private long? _carryingCapacity;
    private int? _encumbrancePercent;
    private string _encumbranceBand = string.Empty;

    public ClassicInventoryEquipmentParser(Func<DateTimeOffset>? utcNow = null) =>
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public void NoteOutboundCommand(string? command)
    {
        var text = command?.Trim() ?? string.Empty;
        _armed = IsInventoryCommand(text) ? ClassicItemScreenKind.Inventory
            : IsEquipmentCommand(text) ? ClassicItemScreenKind.Equipment
            : IsWealthCommand(text) ? ClassicItemScreenKind.Wealth
            : null;
        _expiresAt = _armed is null ? DateTimeOffset.MinValue : _utcNow().AddSeconds(8);
        _started = false;
        _entries.Clear();
        ClearMetrics();
    }

    public ClassicItemScreenObservation? Observe(string? line)
    {
        if (_armed is null || _utcNow() > _expiresAt) { Reset(); return null; }
        var text = line?.Trim() ?? string.Empty;
        return _armed == ClassicItemScreenKind.Inventory ? ObserveInventory(text)
            : _armed == ClassicItemScreenKind.Equipment ? ObserveEquipment(text)
            : ObserveWealth(text);
    }

    public void Reset()
    {
        _entries.Clear();
        _armed = null;
        _started = false;
        _expiresAt = DateTimeOffset.MinValue;
        ClearMetrics();
    }

    private ClassicItemScreenObservation? ObserveInventory(string text)
    {
        if (InventoryEmpty().IsMatch(text)) { _started = true; return null; }
        if (InventoryHeader().IsMatch(text)) { _started = true; return null; }
        if (!_started) return null;
        if (TryReadCurrency(text)) return Complete();
        if (InventoryFooter().IsMatch(text) || Prompt().IsMatch(text)) return Complete();
        if (TryParseEntry(text, out var equipmentEntry)) _entries.Add(equipmentEntry);
        return null;
    }

    private ClassicItemScreenObservation? ObserveEquipment(string text)
    {
        if (InventoryEmpty().IsMatch(text)) { _started = true; return null; }
        var carried = CarriedEquipment().Match(text);
        if (carried.Success)
        {
            _started = true;
            foreach (var part in SplitCommaList(carried.Groups["items"].Value))
                if (TryParseEntry(part, out var entry) && entry.Slot.Length > 0) _entries.Add(entry);
            return null;
        }
        if (EquipmentHeader().IsMatch(text)) { _started = true; return null; }
        if (!_started) return null;
        TryReadCurrency(text);
        if (TryReadEncumbrance(text)) return Complete();
        if (EquipmentFooter().IsMatch(text) || Prompt().IsMatch(text)) return Complete();
        if (WealthOrKeys().IsMatch(text)) return null;
        if (TryParseEntry(text, out var lineEntry)) _entries.Add(lineEntry);
        return null;
    }

    private ClassicItemScreenObservation? ObserveWealth(string text)
    {
        if (TryReadCurrency(text)) return Complete();
        return null;
    }

    private ClassicItemScreenObservation Complete()
    {
        var kind = _armed!.Value;
        var entries = _entries
            .GroupBy(entry => (entry.Name, entry.Slot), ItemEntryKeyComparer.Instance)
            .Select(group => new ClassicItemScreenEntry(group.Key.Name, group.Sum(entry => entry.Quantity), group.Key.Slot))
            .ToArray();
        var observation = new ClassicItemScreenObservation(kind, entries, _currency, _carriedWeight,
            _carryingCapacity, _encumbrancePercent, _encumbranceBand);
        Reset();
        return observation;
    }

    private bool TryReadCurrency(string text)
    {
        var match = Currency().Match(text);
        if (!match.Success || !TryLong(match.Groups["value"].Value, out var value)) return false;
        _currency = value;
        return true;
    }

    private bool TryReadEncumbrance(string text)
    {
        var match = Encumbrance().Match(text);
        if (!match.Success || !TryLong(match.Groups["weight"].Value, out var weight) ||
            !TryLong(match.Groups["capacity"].Value, out var capacity) ||
            !int.TryParse(match.Groups["percent"].Value, out var percent)) return false;
        _carriedWeight = weight;
        _carryingCapacity = capacity;
        _encumbrancePercent = Math.Clamp(percent, 0, 999);
        _encumbranceBand = match.Groups["band"].Value.Trim();
        return true;
    }

    private void ClearMetrics()
    {
        _currency = _carriedWeight = _carryingCapacity = null;
        _encumbrancePercent = null;
        _encumbranceBand = string.Empty;
    }

    private static bool TryLong(string value, out long result) =>
        long.TryParse(value.Replace(",", string.Empty, StringComparison.Ordinal), out result) && result >= 0;

    private static bool TryParseEntry(string text, out ClassicItemScreenEntry entry)
    {
        entry = default;
        var clean = text.Trim().TrimEnd('.');
        if (clean.Length == 0) return false;
        clean = Bullet().Replace(clean, string.Empty);

        var slot = string.Empty;
        var slotMatch = TrailingSlot().Match(clean);
        if (slotMatch.Success)
        {
            slot = slotMatch.Groups["slot"].Value.Trim();
            clean = clean[..slotMatch.Index].Trim();
        }
        else
        {
            var prefix = SlotPrefix().Match(clean);
            if (prefix.Success) { slot = prefix.Groups["slot"].Value.Trim(); clean = clean[prefix.Length..].Trim(); }
        }

        var quantity = 1;
        var leading = LeadingQuantity().Match(clean);
        if (leading.Success) { int.TryParse(leading.Groups["quantity"].Value, out quantity); clean = clean[leading.Length..].Trim(); }
        var trailing = TrailingQuantity().Match(clean);
        if (trailing.Success) { int.TryParse(trailing.Groups["quantity"].Value, out quantity); clean = clean[..trailing.Index].Trim(); }
        clean = InstanceAndCharges().Replace(clean, string.Empty).Trim();
        if (clean.Length == 0 || Noise().IsMatch(clean)) return false;
        entry = new ClassicItemScreenEntry(clean, Math.Max(1, quantity), slot);
        return true;
    }

    private static IEnumerable<string> SplitCommaList(string value)
    {
        var start = 0; var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '(') depth++;
            else if (value[index] == ')') depth = Math.Max(0, depth - 1);
            else if (value[index] == ',' && depth == 0) { yield return value[start..index]; start = index + 1; }
        }
        yield return value[start..];
    }

    private static bool IsInventoryCommand(string value) => value.Length >= 1 && value.Length <= 9 && "inventory".StartsWith(value, StringComparison.OrdinalIgnoreCase);
    private static bool IsEquipmentCommand(string value) =>
        (value.Length >= 2 && value.Length <= 9 && "equipment".StartsWith(value, StringComparison.OrdinalIgnoreCase)) ||
        value.Equals("wearing", StringComparison.OrdinalIgnoreCase);
    private static bool IsWealthCommand(string value) =>
        (value.Length >= 3 && value.Length <= 6 && "wealth".StartsWith(value, StringComparison.OrdinalIgnoreCase)) ||
        value.Equals("money", StringComparison.OrdinalIgnoreCase);

    private sealed class ItemEntryKeyComparer : IEqualityComparer<(string Name, string Slot)>
    {
        public static ItemEntryKeyComparer Instance { get; } = new();
        public bool Equals((string Name, string Slot) x, (string Name, string Slot) y) =>
            x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase) && x.Slot.Equals(y.Slot, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Name, string Slot) value) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Name), StringComparer.OrdinalIgnoreCase.GetHashCode(value.Slot));
    }

    [GeneratedRegex(@"^(?:Inventory|Items carried|You have the following items|Contents of your inventory)\s*:?$", RegexOptions.IgnoreCase)] private static partial Regex InventoryHeader();
    [GeneratedRegex(@"^You are carrying nothing\.?$", RegexOptions.IgnoreCase)] private static partial Regex InventoryEmpty();
    [GeneratedRegex(@"^(?:Gold|Total weight|Encumbrance|End of inventory|Inventory complete)\b", RegexOptions.IgnoreCase)] private static partial Regex InventoryFooter();
    [GeneratedRegex(@"^(?:Equipment|Worn items|You are wearing|Equipped items|Your equipment)\s*:?$", RegexOptions.IgnoreCase)] private static partial Regex EquipmentHeader();
    [GeneratedRegex(@"^You are carrying\s+(?<items>.+?)\.?$", RegexOptions.IgnoreCase)] private static partial Regex CarriedEquipment();
    [GeneratedRegex(@"^(?:Encumbrance|End of equipment|Total armor|Armor class|Equipment complete)\b", RegexOptions.IgnoreCase)] private static partial Regex EquipmentFooter();
    [GeneratedRegex(@"^(?:Wealth|You have the following keys)\b", RegexOptions.IgnoreCase)] private static partial Regex WealthOrKeys();
    [GeneratedRegex(@"^(?:Gold|Wealth|Cash|Coins)\s*[:=]\s*(?<value>[0-9,]+)(?:\s+copper farthings)?\.?$", RegexOptions.IgnoreCase)] private static partial Regex Currency();
    [GeneratedRegex(@"^Encumbrance:\s*(?<weight>[0-9,]+)\s*/\s*(?<capacity>[0-9,]+)\s*-\s*(?<band>[^\[]+?)\s*\[(?<percent>\d+)%\]\.?$", RegexOptions.IgnoreCase)] private static partial Regex Encumbrance();
    [GeneratedRegex(@"^\s*[-*]\s*")] private static partial Regex Bullet();
    [GeneratedRegex(@"^(?<quantity>\d+)\s*(?:[xX]\s+|\s+)")] private static partial Regex LeadingQuantity();
    [GeneratedRegex(@"\s+[xX](?<quantity>\d+)(?:\s+charges=\d+)?(?:\s+\[[^\]]+\])?$")] private static partial Regex TrailingQuantity();
    [GeneratedRegex(@"(?:\s+charges=\d+)?\s+\[[^\]]+\]$")] private static partial Regex InstanceAndCharges();
    [GeneratedRegex(@"\s*\((?<slot>One handed|Two handed|Off hand|Arms|Torso|Back|Wrist|Finger|Waist|Feet|Hands|Head|Legs|Neck|Shield|[^()]*(?:hand|body|weapon|wield|equipped)[^()]*)\)$", RegexOptions.IgnoreCase)] private static partial Regex TrailingSlot();
    [GeneratedRegex(@"^(?:<|\[)?(?<slot>Head|Body|Torso|Hands|Feet|Waist|Back|Neck|Finger|Wrist|Weapon|Shield|Held|Light|Arms|Legs|Face|About body|Primary|Secondary)(?:>|\])?\s*:\s*", RegexOptions.IgnoreCase)] private static partial Regex SlotPrefix();
    [GeneratedRegex(@"^(?:Nothing|Total|Weight|Encumbrance|Gold|Coins|Cash|Wealth)\b", RegexOptions.IgnoreCase)] private static partial Regex Noise();
    [GeneratedRegex(@"^(?:>\s*|\[(?:HP|H)\s*=)", RegexOptions.IgnoreCase)] private static partial Regex Prompt();
}

/// <summary>Turns a chunked terminal stream into bounded item-screen observations.</summary>
public sealed class ClassicItemScreenTracker
{
    private readonly object _sync = new();
    private readonly ClassicInventoryEquipmentParser _parser;
    private readonly StringBuilder _buffer = new();

    public ClassicItemScreenTracker(Func<DateTimeOffset>? utcNow = null) =>
        _parser = new ClassicInventoryEquipmentParser(utcNow);

    public void NoteOutboundCommand(string? command)
    {
        lock (_sync)
        {
            _buffer.Clear();
            _parser.NoteOutboundCommand(command);
        }
    }

    public IReadOnlyList<ClassicItemScreenObservation> Append(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        lock (_sync)
        {
            _buffer.Append(text);
            var observations = new List<ClassicItemScreenObservation>();
            var buffered = _buffer.ToString();
            var start = 0;
            for (var index = 0; index < buffered.Length; index++)
            {
                if (buffered[index] is not ('\r' or '\n')) continue;
                Observe(buffered[start..index], observations);
                if (buffered[index] == '\r' && index + 1 < buffered.Length && buffered[index + 1] == '\n') index++;
                start = index + 1;
            }

            _buffer.Clear();
            if (start < buffered.Length) _buffer.Append(buffered[start..]);
            var tail = AnsiTextParser.StripFormatting(_buffer.ToString()).Trim();
            if (tail == ">" || tail.StartsWith("[HP=", StringComparison.OrdinalIgnoreCase) ||
                tail.StartsWith("[H=", StringComparison.OrdinalIgnoreCase))
            {
                Observe(tail, observations);
                _buffer.Clear();
            }
            if (_buffer.Length > 8192) _buffer.Remove(0, _buffer.Length - 8192);
            return observations;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _buffer.Clear();
            _parser.Reset();
        }
    }

    private void Observe(string line, ICollection<ClassicItemScreenObservation> observations)
    {
        if (_parser.Observe(AnsiTextParser.StripFormatting(line)) is { } observation)
            observations.Add(observation);
    }
}
