using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

public sealed record BorinForgeActionPlan(
    bool IsActionRequest,
    IReadOnlyList<string> Commands,
    string Assignment,
    string Reason)
{
    public bool CanExecute => IsActionRequest && Commands.Count > 0;

    public static BorinForgeActionPlan NotAction() =>
        new(false, Array.Empty<string>(), string.Empty, string.Empty);

    public static BorinForgeActionPlan Blocked(string assignment, string reason) =>
        new(true, Array.Empty<string>(), assignment, reason);
}

/// <summary>
/// Converts natural player instructions and explicit command passthrough into
/// real terminal commands. The MajorMUD server remains authoritative for play.
/// </summary>
public enum BorinForgeConnectionAction
{
    None,
    ConnectAndLogin,
    Disconnect,
}

public static class BorinForgeActionRouter
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SequenceSeparator = new(
        @"\s*(?:,|;|\band\s+then\b|\bthen\b|\band\b)\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Movement = new(
        @"^(?:go|move|walk|head|travel)(?:\s+to)?\s+(?<direction>north|south|east|west|up|down|northeast|northwest|southeast|southwest|n|s|e|w|u|d|ne|nw|se|sw)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BareMovement = new(
        @"^(?<direction>north|south|east|west|up|down|northeast|northwest|southeast|southwest|n|s|e|w|u|d|ne|nw|se|sw)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Buy = new(
        @"^(?:buy|purchase)(?:\s+(?:a|an|the))?\s+(?<item>[a-z0-9][a-z0-9 '\-]{0,60})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Equip = new(
        @"^(?:equip|wield|wear)(?:\s+(?:a|an|the))?\s+(?<item>[a-z0-9][a-z0-9 '\-]{0,60})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Get = new(
        @"^(?:get|take)(?:\s+(?:a|an|the))?\s+(?<item>[a-z0-9][a-z0-9 '\-]{0,60})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DirectActionVerb = new(
        @"\b(?:go|move|walk|head|travel|look|list|buy|purchase|inventory|equipment|equip|wield|wear|rest|search|get|take)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ExplicitTerminalCommand = new(
        @"^(?:(?:type|enter|send)(?:\s+the)?(?:\s+command)?|use\s+the\s+command)\s+(?<command>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RequiresBorinComposition = new(
        @"\b(?:then|and\s+then)\s+(?:give|write|say|type|send)?\s*(?:your|his|an?)\s+(?:own\s+)?(?:introduction|message|reply|answer)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex GossipIntroduction = new(
        @"^(?:type\s+)?gos(?:sip)?\s+(?:(?:and\s+)?then\s+)?(?:(?:give|write|say|type|send)\s+)?(?:your|his|an?)\s+(?:own\s+)?introduction$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DiscussionPrefix = new(
        @"^(?:what|where|why|how|when|which|do\s+you|are\s+you|did\s+you|have\s+you|tell\s+me|explain|describe)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DiagnosticRequest = new(
        @"^(?:(?:find|figure|work)s+outs+why|looks+intos+why|searchs+(?:fors+)?why|investigate|diagnose|debug|audit)|\b(?:broken|bug|exception|compiler error|does not work|doesn't work|unavailable)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SimulatedCommandLine = new(
        @"(?m)^\s*(?:>|i\s+(?:go|went|move|moved|walk|walked|head|headed|travel|traveled|enter|entered|arrive|arrived|buy|bought|purchase|purchased|equip|equipped|wield|wielded)\b|you\s+(?:head|move|walk|travel|enter|arrive|purchase|buy|equip|wield)\b|your\s+character\s+now|the\s+(?:blacksmith|weapon|weapons)\s+shop\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ClaimedUnsentAction = new(
        @"\b(?:i['’]ll|i\s+will|let\s+me|i['’]m\s+going\s+to)\s+(?:go|move|walk|head|travel|enter|buy|purchase|equip|wield|get|take|rest|search)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns true only when the user is actually asking about the active server,
    /// connection, login, or play location. Merely mentioning a server name such as
    /// Omniverse must not hijack unrelated questions about rooms, exits, monsters,
    /// items, gameplay, or learning.
    /// </summary>
    public static bool IsServerStateQuestion(string? request)
    {
        var normalized = Normalize(request).Trim(' ', '.', '!', '?');
        if (normalized.Length == 0)
            return false;

        return Regex.IsMatch(normalized, @"^(?:which|what)\s+(?:game\s+)?(?:server|bbs)\s+(?:are|is)\s+(?:you|your\s+character)\s+(?:on|using|playing|connected|logged\s*in)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(normalized, @"^what\s+is\s+(?:your\s+)?(?:current\s+)?(?:server|bbs)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(normalized, @"^where\s+are\s+you\s+(?:playing|connected|logged\s*in)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(normalized, @"^are\s+you\s+(?:currently\s+)?(?:connected|logged\s*in|playing)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(normalized, @"^are\s+you\s+(?:currently\s+)?(?:on|in)\s+(?:omniverse|the\s+reforged\s+server|reforged\s+server)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(normalized, @"^is\s+(?:your\s+)?(?:character\s+)?(?:currently\s+)?(?:connected|logged\s*in|playing)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(normalized, @"^(?:what\s+is\s+)?(?:your\s+)?(?:connection|login)\s+status\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(normalized, @"^which\s+(?:server|bbs)\s+are\s+you\s+(?:connected|logged\s*in|playing|on)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static BorinForgeConnectionAction ClassifyConnectionAction(string? request)
    {
        var original = Normalize(request);
        if (original.Length == 0)
            return BorinForgeConnectionAction.None;

        var normalized = StripPolitePrefix(original).Trim(' ', '.', '!', '?');
        if (DiscussionPrefix.IsMatch(normalized) ||
            normalized.Contains("login status", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("connection status", StringComparison.OrdinalIgnoreCase))
        {
            return BorinForgeConnectionAction.None;
        }

        if (Regex.IsMatch(normalized, @"^(?:disconnect|log\s*out|logout)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return BorinForgeConnectionAction.Disconnect;

        if (Regex.IsMatch(normalized, @"^(?:connect|log\s*in|login)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return BorinForgeConnectionAction.ConnectAndLogin;

        return BorinForgeConnectionAction.None;
    }

    public static BorinForgeActionPlan CreatePlan(string? request)
    {
        var original = Normalize(request);
        if (original.Length == 0)
            return BorinForgeActionPlan.NotAction();

        var normalized = StripPolitePrefix(original);
        if (GossipIntroduction.IsMatch(normalized))
            return new BorinForgeActionPlan(true,
                ["GOS Hail, I'm Borin Ironbeard, a dwarven adventurer exploring MajorMUD and learning the realm. Well met!"],
                original[..Math.Min(original.Length, 500)],
                "Borin composed and sent his own Omniverse introduction on the requested global channel.");
        if (RequiresBorinComposition.IsMatch(normalized))
            return BorinForgeActionPlan.NotAction();
        var isExplicitTerminalCommand = ExplicitTerminalCommand.IsMatch(normalized);
        if (DiagnosticRequest.IsMatch(normalized) ||
            (!DirectActionVerb.IsMatch(normalized) && !BareMovement.IsMatch(normalized) && !isExplicitTerminalCommand) ||
            DiscussionPrefix.IsMatch(normalized))
        {
            return BorinForgeActionPlan.NotAction();
        }

        var segments = SequenceSeparator
            .Split(normalized)
            .Select(value => value.Trim(' ', '.', '!', '?'))
            .Where(value => value.Length > 0)
            .ToArray();
        if (segments.Length == 0)
            return BorinForgeActionPlan.Blocked(original, "No concrete gameplay action was found.");

        var commands = new List<string>();
        string lastItem = string.Empty;
        foreach (var rawSegment in segments)
        {
            var segment = StripPolitePrefix(rawSegment);
            if (!TryParseSegment(segment, ref lastItem, commands, out var reason))
            {
                return BorinForgeActionPlan.Blocked(
                    original,
                    $"I recognized a real gameplay instruction, but I could not safely convert '{rawSegment}' into one verified terminal command. {reason}");
            }
        }

        commands = AddVerificationCommands(commands);
        if (commands.Count == 0)
            return BorinForgeActionPlan.Blocked(original, "No safe terminal command was produced.");
        if (commands.Count > 10)
            return BorinForgeActionPlan.Blocked(original, "The instruction expands to too many terminal commands. Split it into a smaller assignment.");

        return new BorinForgeActionPlan(
            true,
            commands,
            original[..Math.Min(original.Length, 500)],
            "Explicit gameplay instruction from William; route through the real supervised terminal gateway.");
    }

    public static bool ContainsInventedTerminalTranscript(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var text = response.Replace("\r\n", "\n", StringComparison.Ordinal);
        return text.Contains("```", StringComparison.Ordinal) ||
               SimulatedCommandLine.IsMatch(text) ||
               ClaimedUnsentAction.IsMatch(text) ||
               text.Contains("you find yourself at", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("you purchase a", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("you purchase the", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("what's next, lad", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("equip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseSegment(
        string segment,
        ref string lastItem,
        List<string> commands,
        out string reason)
    {
        reason = string.Empty;
        if (segment.Length == 0)
        {
            reason = "The action segment was empty.";
            return false;
        }

        var movement = Movement.Match(segment);
        if (!movement.Success)
            movement = BareMovement.Match(segment);
        if (movement.Success)
        {
            commands.Add(NormalizeDirection(movement.Groups["direction"].Value));
            return true;
        }

        if (segment is "look" or "look around" or "check the room" or "check room")
        {
            commands.Add("LOOK");
            return true;
        }
        if (segment is "list" or "list the shop" or "list shop" or "check the shop" or "check shop")
        {
            commands.Add("LIST");
            return true;
        }
        if (segment is "inventory" or "check inventory" or "check my inventory" or "check your inventory" or
            "show inventory" or "show my inventory" or "show your inventory" or "look at inventory" or
            "look at my inventory" or "look at your inventory")
        {
            commands.Add("INVENTORY");
            return true;
        }
        if (segment is "equipment" or "check equipment" or "check my equipment" or "check your equipment" or
            "show equipment" or "show my equipment" or "show your equipment" or "look at equipment" or
            "look at my equipment" or "look at your equipment")
        {
            commands.Add("EQUIPMENT");
            return true;
        }
        if (segment is "rest" or "rest here")
        {
            commands.Add("REST");
            return true;
        }
        if (segment is "search" or "search here" or "search the room")
        {
            commands.Add("SEARCH");
            return true;
        }

        var buy = Buy.Match(segment);
        if (buy.Success)
        {
            lastItem = NormalizeItem(buy.Groups["item"].Value);
            if (lastItem.Length == 0)
            {
                reason = "The item to buy was empty.";
                return false;
            }
            commands.Add("BUY " + lastItem);
            return true;
        }

        var equip = Equip.Match(segment);
        if (equip.Success)
        {
            var rawItem = equip.Groups["item"].Value.Trim();
            var item = IsPronoun(rawItem) ? lastItem : NormalizeItem(rawItem);
            if (item.Length == 0)
            {
                reason = "Name the item to equip; there is no earlier item in this instruction for 'it' to reference.";
                return false;
            }
            lastItem = item;
            commands.Add("EQUIP " + item);
            return true;
        }

        var get = Get.Match(segment);
        if (get.Success)
        {
            lastItem = NormalizeItem(get.Groups["item"].Value);
            if (lastItem.Length == 0)
            {
                reason = "The item to take was empty.";
                return false;
            }
            commands.Add("GET " + lastItem);
            return true;
        }

        var explicitCommand = ExplicitTerminalCommand.Match(segment);
        if (explicitCommand.Success)
        {
            var command = NormalizeTerminalCommand(explicitCommand.Groups["command"].Value);
            if (command.Length == 0 || BorinPlayerCommandPolicy.IsProtectedControlCommand(command))
            {
                reason = "That is not a normal single-line MajorMUD player command.";
                return false;
            }
            commands.Add(command);
            return true;
        }

        reason = "Use a natural player instruction, or say `type <MajorMUD command>` for direct in-game command passthrough.";
        return false;
    }

    private static List<string> AddVerificationCommands(IReadOnlyList<string> source)
    {
        var output = new List<string>();
        foreach (var command in source)
        {
            if (command.StartsWith("BUY ", StringComparison.OrdinalIgnoreCase) &&
                (output.Count == 0 || !output[^1].Equals("LIST", StringComparison.OrdinalIgnoreCase)))
            {
                output.Add("LIST");
            }

            output.Add(command);

            if (command.StartsWith("BUY ", StringComparison.OrdinalIgnoreCase))
                output.Add("INVENTORY");
            else if (command.StartsWith("EQUIP ", StringComparison.OrdinalIgnoreCase))
                output.Add("EQUIPMENT");
        }

        var deduplicated = new List<string>();
        foreach (var command in output)
        {
            if (deduplicated.Count == 0 || !deduplicated[^1].Equals(command, StringComparison.OrdinalIgnoreCase))
                deduplicated.Add(command);
        }
        return deduplicated;
    }

    private static string Normalize(string? value) =>
        Whitespace.Replace(value?.Trim() ?? string.Empty, " ");

    private static string StripPolitePrefix(string value)
    {
        var normalized = value.Trim();
        var prefixes = new[]
        {
            "borin, ", "borin ", "please ", "can you ", "could you ", "will you ",
            "i want you to ", "i need you to ", "you need to ", "go ahead and ",
        };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var prefix in prefixes)
            {
                if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                normalized = normalized[prefix.Length..].TrimStart();
                changed = true;
                break;
            }
        }
        return normalized.ToLowerInvariant();
    }

    private static string NormalizeItem(string value)
    {
        var item = Whitespace.Replace(value.Trim(' ', '.', '!', '?'), " ");
        if (item.StartsWith("a ", StringComparison.OrdinalIgnoreCase)) item = item[2..];
        else if (item.StartsWith("an ", StringComparison.OrdinalIgnoreCase)) item = item[3..];
        else if (item.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) item = item[4..];
        return Regex.Replace(item, @"[^A-Za-z0-9 '\-]", string.Empty).Trim().ToUpperInvariant();
    }

    private static string NormalizeTerminalCommand(string value)
    {
        var command = Whitespace.Replace(value.Trim(' ', '.', '!', '?', '`', '"'), " ");
        return command.Length > 240 || command.Contains('\r') || command.Contains('\n') || command.IndexOfAny(['\0', '\u001b']) >= 0
            ? string.Empty
            : command.ToUpperInvariant();
    }

    private static bool IsPronoun(string value) =>
        value.Equals("it", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("that", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("that item", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("the item", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDirection(string value) => value.ToLowerInvariant() switch
    {
        "n" or "north" => "NORTH",
        "ne" or "northeast" => "NORTHEAST",
        "e" or "east" => "EAST",
        "se" or "southeast" => "SOUTHEAST",
        "s" or "south" => "SOUTH",
        "sw" or "southwest" => "SOUTHWEST",
        "w" or "west" => "WEST",
        "nw" or "northwest" => "NORTHWEST",
        "u" or "up" => "UP",
        "d" or "down" => "DOWN",
        _ => value.ToUpperInvariant(),
    };
}
