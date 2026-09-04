namespace RealmGate.Server;

/// <summary>
/// Parser-visible Classic MajorMUD communication rendering recovered from the retained
/// WCCMMUD.DLL. ANSI/color control fragments are presentation metadata; these helpers own the
/// visible text consumed by legacy Telnet clients such as MegaMUD.
///
/// CodeUpdate0345 evidence anchors:
/// _cmd_whisper @ 0x004571F2, _cmd_direct_message @ 0x0045748D,
/// _cmd_gossip @ 0x00469431, _cmd_auction @ 0x004695BF, and the SAY/YELL owner around
/// 0x00450A3F..0x00450D0E in the retained original DLL.
/// </summary>
public static class RuntimeClassicCommunicationRenderingAuthority
{
    public const string CannotSpeak = "You cannot speak!";
    public const string CommandNotRecognized = "Command not recognized.";
    public const string TalkBriefResponse = "Well Spoken...";

    public const string TelepathMissingMessage = "You have to telepath something!";
    public const string TelepathMissingTarget = "You have to specify a person to telepath to.";
    public const string TelepathSenderBlocking = "You may not telepath while you are blocking telepaths.";
    public const string CannotFindUser = "Cannot find user!";
    public const string TelepathSelf = "Why are you telepathing to yourself?";
    public const string TelepathNotSent = "--- Telepath Not Sent ---";

    public const string DirectMissingMessage = "You have to direct something!";
    public const string DirectMissingTarget = "You have to specify a person to direct to.";
    public const string DirectSpeechBlocked = "You may not direct any messages right now.";
    public const string DirectSelf = "Why are you directing messages to yourself?";
    public const string DirectNotSent = "--- Message Not Sent ---";

    public const string GossipReceiveOn = "You will now receive gossip messages.";
    public const string GossipReceiveOff = "You will no longer receive gossip messages.";
    public const string GossipSendWhileOff = "You may not use gossip while your gossip is turned off.";
    public const string GossipTurnOnHint = "Type 'gossip' to turn gossip on again.";
    public const string AuctionReceiveOn = "You will now receive auction messages.";
    public const string AuctionReceiveOff = "You will no longer receive auction messages.";
    public const string AuctionSendWhileOff = "You may not use auction while your auction is turned off.";
    public const string AuctionTurnOnHint = "Type 'auction' to turn auction on again.";
    public const string ProfanityRejected = "You are using too much profanity - your message is not sent.";

    public static string Say(string speaker, string message) => $"{speaker} says \"{message}\"";
    public static string SaySelf(string message) => $"You say \"{message}\"";
    public static string YellCurrentRoom(string speaker, string message) => $"From {speaker} (Yelling): \"{message}\"";
    public static string YellSelf(string message) => $"You yell \"{message}\"";

    public static string YellAdjacent(string sourceToTargetDirection, string message)
    {
        var heardFrom = OppositeDirection(sourceToTargetDirection);
        return heardFrom switch
        {
            "U" => $"Someone yells from above \"{message}\"",
            "D" => $"Someone yells from below \"{message}\"",
            _ => $"Someone yells from the {DirectionWord(heardFrom)} \"{message}\""
        };
    }

    public static string TelepathReceived(string speaker, string message) => $"{speaker} telepaths: {message}";
    public static string TelepathSent(string target) => $"--- Telepath Sent to {target} ---";

    public static string DirectToTarget(string speaker, string message) => $"{speaker} says (to you) \"{message}\"";
    public static string DirectToRoom(string speaker, string target, string message) => $"{speaker} says (to {target}) \"{message}\"";
    public static string DirectSent(string target) => $"--- Message Directed to {target} ---";

    public static string Gossip(string speaker, string message) => $"{speaker} gossips: {message}";
    public static string Auction(string speaker, string message) => $"{speaker} auctions: {message}";

    public static string OppositeDirection(string direction) => NormalizeDirection(direction) switch
    {
        "N" => "S", "S" => "N", "E" => "W", "W" => "E",
        "NE" => "SW", "NW" => "SE", "SE" => "NW", "SW" => "NE",
        "U" => "D", "D" => "U", _ => NormalizeDirection(direction)
    };

    private static string NormalizeDirection(string direction) => (direction ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W",
        "NORTHEAST" => "NE", "NORTHWEST" => "NW", "SOUTHEAST" => "SE", "SOUTHWEST" => "SW",
        "UP" => "U", "DOWN" => "D", var value => value
    };

    private static string DirectionWord(string direction) => direction switch
    {
        "N" => "north", "S" => "south", "E" => "east", "W" => "west",
        "NE" => "northeast", "NW" => "northwest", "SE" => "southeast", "SW" => "southwest",
        "U" => "above", "D" => "below", _ => direction.ToLowerInvariant()
    };
}
