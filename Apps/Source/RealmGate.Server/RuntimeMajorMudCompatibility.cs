namespace RealmGate.Server;

/// <summary>
/// Shared MajorMUD behavioral-compatibility baseline for every Reforged world.
/// Classic, Custom, and AI may keep richer internal state and world extensions,
/// but their legacy-facing room, prompt, movement, combat, and door semantics
/// remain MajorMUD-compatible so MegaMUD and other legacy clients stay synchronized.
/// </summary>
public static class RuntimeMajorMudCompatibility
{
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiGreen = "\u001b[0;32m";
    private const string AnsiCyan = "\u001b[36m";
    private const string AnsiRed = "\u001b[31m";
    private const string AnsiYellow = "\u001b[33m";
    private const string AnsiBandGreen = "\u001b[32m";
    private const string AnsiWhite = "\u001b[37m";

    public static bool IsObviousExit(RuntimeExit exit)
    {
        if (string.IsNullOrWhiteSpace(exit.Direction) || exit.TargetMapId <= 0 || exit.TargetRoomNumber <= 0)
            return false;

        // MegaMUD's native room signature deliberately does not include hidden
        // or text/action exits. Those routes remain available to the runtime when
        // their separate discovery/action rules allow them.
        return !exit.GateKind.Equals("hidden", StringComparison.OrdinalIgnoreCase)
               && !exit.GateKind.Equals("text_command", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDoorLike(RuntimeExit exit) =>
        exit.GateKind.Equals("door", StringComparison.OrdinalIgnoreCase)
        || exit.GateKind.Equals("key", StringComparison.OrdinalIgnoreCase)
        || exit.GateKind.Equals("gate", StringComparison.OrdinalIgnoreCase)
        || exit.Qualifier.StartsWith("Door", StringComparison.OrdinalIgnoreCase)
        || exit.Qualifier.StartsWith("Key:", StringComparison.OrdinalIgnoreCase)
        || exit.Qualifier.StartsWith("Gate", StringComparison.OrdinalIgnoreCase);

    public static string FormatObviousExit(RuntimeExit exit, string? livingDescription)
    {
        var expanded = ExpandDirection(exit.Direction);
        if (!IsDoorLike(exit)) return expanded;

        var closed = livingDescription?.Contains("closed", StringComparison.OrdinalIgnoreCase) == true
                     || livingDescription?.Contains("locked", StringComparison.OrdinalIgnoreCase) == true;
        return $"{(closed ? "closed" : "open")} door {expanded}";
    }

    public static string ExpandDirection(string direction) => direction.Trim().ToUpperInvariant() switch
    {
        "N" => "north", "S" => "south", "E" => "east", "W" => "west",
        "NE" => "northeast", "NW" => "northwest", "SE" => "southeast", "SW" => "southwest",
        "U" => "up", "D" => "down", _ => direction.Trim().ToLowerInvariant()
    };

    public static string OppositeDirection(string direction) => direction.Trim().ToUpperInvariant() switch
    {
        "N" or "NORTH" => "south", "S" or "SOUTH" => "north",
        "E" or "EAST" => "west", "W" or "WEST" => "east",
        "NE" or "NORTHEAST" => "southwest", "NW" or "NORTHWEST" => "southeast",
        "SE" or "SOUTHEAST" => "northwest", "SW" or "SOUTHWEST" => "northeast",
        "U" or "UP" => "down", "D" or "DOWN" => "up",
        _ => direction.Trim().ToLowerInvariant()
    };

    public static string FormatRoomOccupants(
        IEnumerable<(string Name, bool IsFiend)> players,
        IEnumerable<string> npcNames)
    {
        var occupants = players
            .Where(player => !string.IsNullOrWhiteSpace(player.Name))
            .Select(player => player.Name.TrimEnd('*') + (player.IsFiend ? "*" : string.Empty))
            .Concat(npcNames.Where(name => !string.IsNullOrWhiteSpace(name)))
            .ToArray();
        return occupants.Length == 0 ? string.Empty : "Also here: " + string.Join(", ", occupants) + ".";
    }

    public static string FormatDefaultStatlinePlain(
        int hitPoints,
        int resource,
        bool usesKai,
        bool resting,
        bool meditating = false)
    {
        var label = usesKai ? "KAI" : "MA";
        var position = meditating ? " (Meditating)" : resting ? " (Resting)" : string.Empty;
        return $"[HP={hitPoints}/{label}={resource}]:{position}";
    }

    public static long CalculateBaseMaximumEncumberance(int strength)
    {
        // Recovered stock MajorMUD v1.11n+ formula:
        //   STR <= 100 : STR * 48
        //   STR >  100 : 4800 + ((STR - 100) * 84)
        var value = Math.Max(0, strength);
        return value <= 100
            ? checked((long)value * 48L)
            : checked(4800L + ((long)value - 100L) * 84L);
    }

    public static long ApplyEncumberanceModifier(long baseCapacity, int modifierPercent)
    {
        if (baseCapacity <= 0) return 0;

        // Recovered CalcEncum applies positive Encumbrance bonuses to maximum
        // capacity. Timed/debuff semantics are kept separate until independently
        // proven rather than being folded into this number.
        if (modifierPercent <= 0) return baseCapacity;

        var scaled = baseCapacity * (100m + modifierPercent) / 100m;
        return checked((long)Math.Round(scaled, 0, MidpointRounding.ToEven));
    }

    public static int CalculateEncumberancePercent(long carriedWeight, long capacity)
    {
        var maximum = Math.Clamp(capacity, 1L, 999999L);
        var current = Math.Clamp(carriedWeight, 0L, maximum);
        return checked((int)(current * 100L / maximum));
    }

    public static string ClassifyEncumberance(long carriedWeight, long capacity)
    {
        if (carriedWeight <= 0) return "None";
        var percent = CalculateEncumberancePercent(carriedWeight, capacity);

        // Original WCC inventory code compares the computed percentage against
        // 0x10, 0x21 and 0x42: None <=16, Light 17-33, Medium 34-66, Heavy >=67.
        if (percent <= 16) return "None";
        if (percent <= 33) return "Light";
        if (percent <= 66) return "Medium";
        return "Heavy";
    }

    public static string FormatEncumberanceLine(long carriedWeight, long capacity, bool includePercent = true)
    {
        var percent = CalculateEncumberancePercent(carriedWeight, capacity);
        var band = ClassifyEncumberance(carriedWeight, capacity);
        return includePercent
            ? $"Encumbrance: {carriedWeight}/{capacity} - {band} [{percent}%]"
            : $"Encumbrance: {carriedWeight}/{capacity} - {band}";
    }

    /// <summary>
    /// Native WCC inventory presentation: green label, cyan values, then a
    /// band-specific normal-intensity color (red/yellow/green/white) and reset.
    /// </summary>
    public static string FormatEncumberanceLineAnsi(long carriedWeight, long capacity, bool includePercent = true)
    {
        var percent = CalculateEncumberancePercent(carriedWeight, capacity);
        var band = ClassifyEncumberance(carriedWeight, capacity);
        var bandColor = band switch
        {
            "Heavy" => AnsiRed,
            "Medium" => AnsiYellow,
            "Light" => AnsiBandGreen,
            _ => AnsiWhite
        };

        var suffix = includePercent ? $" [{percent}%]" : string.Empty;
        return $"{AnsiGreen}Encumbrance: {AnsiCyan}{carriedWeight}/{capacity} - {bandColor}{band}{suffix}{AnsiReset}";
    }

    public static string FormatWealthLineAnsi(string value) =>
        $"{AnsiGreen}Wealth: {AnsiCyan}{value}{AnsiReset}";

    public static string FormatInventoryCurrency(RuntimeClassicInventoryCurrency currency)
    {
        var parts = new List<string>();
        if (currency.Runic > 0) parts.Add($"{currency.Runic} Runic Coins");
        if (currency.Platinum > 0) parts.Add($"{currency.Platinum} platinum {(currency.Platinum == 1 ? "piece" : "pieces")}");
        if (currency.Gold > 0) parts.Add($"{currency.Gold} gold {(currency.Gold == 1 ? "crown" : "crowns")}");
        if (currency.Silver > 0) parts.Add($"{currency.Silver} silver {(currency.Silver == 1 ? "noble" : "nobles")}");
        if (currency.Copper > 0) parts.Add($"{currency.Copper} copper {(currency.Copper == 1 ? "farthing" : "farthings")}");
        return string.Join(", ", parts);
    }

    public static string ComputeMegaMudRoomHash(
        string roomName,
        bool hasWhirlingVortex = false,
        bool hasObsidianObelisk = false)
    {
        var normalizedRoomName = roomName ?? string.Empty;
        long checksum = 0;
        for (var index = 0; index < normalizedRoomName.Length; index++)
            checksum += (long)(index + 1) * normalizedRoomName[index];

        if (hasWhirlingVortex) checksum += 1;
        if (hasObsidianObelisk) checksum += 2;
        return ((int)(checksum & 0xFFF)).ToString("X3", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string ComputeMegaMudExitCode(IEnumerable<RuntimeExit> exits)
    {
        // MMUD Explorer's recovered MegaMUD exit code packs U/D, SE/SW,
        // NE/NW, E/W, N/S into five hexadecimal digits. Door/key edges use
        // the doubled value that marks a door without depending on open state.
        var values = new int[6];
        foreach (var exit in exits.Where(IsObviousExit))
        {
            var (position, directionValue) = exit.Direction.Trim().ToUpperInvariant() switch
            {
                "U" => (1, 1), "D" => (1, 4),
                "SE" => (2, 1), "SW" => (2, 4),
                "NE" => (3, 1), "NW" => (3, 4),
                "E" => (4, 1), "W" => (4, 4),
                "N" => (5, 1), "S" => (5, 4),
                _ => (0, 0)
            };
            if (position == 0) continue;
            values[position] += directionValue * (IsDoorLike(exit) ? 2 : 1);
        }

        return string.Concat(Enumerable.Range(1, 5).Select(position => values[position].ToString("X", System.Globalization.CultureInfo.InvariantCulture)));
    }

    public static string ComputeMegaMudRoomSignature(
        string roomName,
        IEnumerable<RuntimeExit> exits,
        bool hasWhirlingVortex = false,
        bool hasObsidianObelisk = false) =>
        ComputeMegaMudRoomHash(roomName, hasWhirlingVortex, hasObsidianObelisk)
        + ComputeMegaMudExitCode(exits);

    public static string RenderStatlineTemplate(
        string template,
        int hitPoints, int maximumHitPoints,
        int mana, int maximumMana,
        long wealth, long experience, long experienceToNext,
        string restingFlag)
    {
        var normalizedTemplate = template ?? string.Empty;
        var output = new System.Text.StringBuilder();
        for (var index = 0; index < normalizedTemplate.Length; index++)
        {
            var current = normalizedTemplate[index];
            if (current != '%' || index + 1 >= normalizedTemplate.Length)
            {
                output.Append(current);
                continue;
            }

            var code = normalizedTemplate[++index];
            if (code == '%' && index + 1 < normalizedTemplate.Length) code = normalizedTemplate[++index];
            if ((code is 'f' or 'b') && index + 1 < normalizedTemplate.Length && normalizedTemplate[index + 1] is >= '0' and <= '7')
            {
                var color = normalizedTemplate[++index] - '0';
                output.Append($"\u001b[{(code == 'f' ? 30 : 40) + color}m");
                continue;
            }

            output.Append(code switch
            {
                'h' => hitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture),
                'H' => maximumHitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture),
                'm' => mana.ToString(System.Globalization.CultureInfo.InvariantCulture),
                'M' => maximumMana.ToString(System.Globalization.CultureInfo.InvariantCulture),
                'c' => wealth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                'x' => experience.ToString(System.Globalization.CultureInfo.InvariantCulture),
                'X' => experienceToNext.ToString(System.Globalization.CultureInfo.InvariantCulture),
                'r' => restingFlag,
                'n' => Environment.NewLine,
                'B' => "\u001b[1m",
                'N' => "\u001b[0m",
                'U' => "\u001b[4m",
                'L' => "\u001b[5m",
                'R' => "\u001b[7m",
                'd' => "\u001b[0m",
                'w' => maximumHitPoints > 0 && hitPoints * 100 <= maximumHitPoints * 25 ? "!" : string.Empty,
                _ => "%" + code
            });
        }
        return output.ToString();
    }
}