using Reforged.App.Models;

namespace Reforged.App.Services;

public static class ClientHelpCatalog
{
    public static IReadOnlyList<ClientHelpTopic> Topics { get; } =
    [
        Topic("start", "Getting Started", "Getting started",
            "Connect a character and learn the main client areas.",
            "Choose File > Open character to select a saved character profile, then choose Connect. Type commands in the terminal and press Enter. The server remains authoritative: the client displays game output and sends ordinary commands. Use Disconnect before changing a session's character profile. Emergency Stop affects automatic commands only; manual play remains available.",
            "connect login character profile terminal emergency stop manual"),
        Topic("input", "Getting Started", "Terminal and keyboard",
            "Command entry, history, copy, paste, ANSI, and scrollback.",
            "Enter sends the current command. Up and Down browse command history. Left, Right, Home, End, Backspace, and Delete edit locally. Escape clears local input. Ctrl+V pastes. Ctrl+C copies command input unless password protection is active. Back-scroll supports selection, Ctrl+C, Ctrl+A, and search. Reset ANSI repairs local color state; Reset Internals clears client caches without sending @reset to the server.",
            "keyboard input ansi color scrollback copy paste history reset"),
        Topic("commands", "Gameplay", "Common MajorMUD commands",
            "Frequently used commands sent directly to the connected server.",
            "Movement: north, south, east, west, northeast, northwest, southeast, southwest, up, down; abbreviations such as n, s, e, w, u, and d are accepted by the server. Information: look, examine <target>, stats, exp, inventory or inv, equipment or eq, spells, par, who, and whohome. Combat: attack <target>, kill <target>, flee, and supported class abilities or cast <spell> [target]. Items: get <item>, drop <item>, use <item>, equip <item>, remove <item>, and put <item> <container>. Interaction: ask <npc> <keyword> and the exact room commands shown by the live map or Librarium. Server command availability always wins.",
            "look stats exp experience inv inventory eq equip spells par party who whohome ask attack cast movement"),
        Topic("automation", "Automation", "Automation controls",
            "Independent switches and Emergency Stop.",
            "Open Options > Automation controls. Navigation, Combat, Health Recovery, Spell Maintenance, Party, Loot, Roaming, PvP, Communications, aliases, macros, text triggers, and timed events can be enabled independently. Emergency Stop immediately blocks all automatic commands and stops active routes while leaving manual commands usable. Automatic actions are rate-limited and duplicate automatic commands are suppressed. Character statistics shows the last automatic action and reason.",
            "switches autonomous emergency stop aliases macros triggers automatic capabilities"),
        Topic("combat", "Automation", "Combat, health, and spells",
            "Safe configuration of combat priorities and recovery.",
            "Combat automation uses living hostile creatures reported by the server, pauses during movement, and stops on unknown or stale state. Configure attacks, pre-attacks, spell priorities, mana reserve, flee thresholds, healing, rest, meditation, cures, and maintained effects in their configuration windows. Learned spells and active effects are refreshed privately. Failed or stale verification causes the client to wait instead of guessing.",
            "combat heal health mana spell bless nuke flee cure rest meditate priority"),
        Topic("party", "Automation", "Party coordination and PAR",
            "Multiple characters, ranks, following, assisting, and reconnects.",
            "PAR displays current party members, HP percentage, mana or Kai percentage, and rank such as frontrunner, midrange, or backrank. Configure an explicit leader for follow and assist behavior. Each local character has an independent queue and state. A reconnect clears stale decisions, re-synchronizes that character, and privately refreshes the party roster. Party members who leave must be invited again when required by the server.",
            "par party leader follower assist heal rank frontrunner midrange backrank reconnect multicharacter"),
        Topic("pvp", "Automation", "PvP and Defend",
            "Verified player attacks and party protection.",
            "PvP responses work only when the SYSOP has enabled realm PvP and the current room is not a proven safe room. The attacker must be a fresh, verified same-room player. Auto flee and return attack use configured health thresholds. MegaMUD Defend means attacking the verified player who attacks a current party member; it is not an alignment command. Trusted players can be ignored. Stale room or PvP state blocks automatic action.",
            "pvp player versus player defend safe room sysop attacker trusted"),
        Topic("routes", "Navigation", "Routes, Goto, loops, and recovery",
            "Verified navigation using decoded and imported MegaMUD paths.",
            "Goto selects a route to a destination. Loop runs a loop route; Auto-Roam repeats one. Every movement step waits for a recognized room and checks the expected native room when available. Combat pauses a route. A rejection, timeout, unexpected room, or disconnect stops safely and offers recovery from the last verified room. Back Track reverses confirmed standard-direction steps; special exits fail closed. Record Path creates a local route and Import copies a read-only MegaMUD .mp route into the Reforged profile.",
            "goto loop auto roam route path back track navigation disconnect desync recovery"),
        Topic("map", "Reference", "Live map and fog of war",
            "Explore any room and view current server state.",
            "The Live Map combines decoded topology with live information from the connected server. Select any room to inspect it; Walk to Selected calculates a verified route from the current room. Fog of War can hide undiscovered rooms. Show Hidden Objects is off by default and reveals hidden room data only when explicitly enabled. Markers can show the current character, other players, shops, bosses, NPC assignments, room commands, gates, stairs, and exits. Live and stale labels indicate the age of server information.",
            "map fog war hidden objects players bosses spawn room inspect walk"),
        Topic("librarium", "Reference", "Librarium reference",
            "Search server-provided rooms, creatures, items, spells, quests, and services.",
            "Open Options > Librarium Reference. Search friendly categories such as Rooms and Areas, Creatures and Bosses, Items and Equipment, Spells and Abilities, Quests, Trainers, Shops and Services, Room Commands, Spawns, and Exits. Pages come from the server currently connected to this session. Related links and Back support browsing. The Librarium is reference-only and does not edit game data.",
            "librarium explorer mmexplorer search rooms npcs creatures items spells quests trainers shops"),
        Topic("bosses", "Reference", "Who Is Home and spawn status",
            "Boss location, alive state, and respawn timing.",
            "Who Is Home is the boss tracker, not the player list. It reports whether a boss is alive, its current room when known, its home room, and time remaining until respawn. Live map boss markers use the same server-backed state. Use Who or Online Players for players.",
            "whohome who is home boss lair alive respawn timer location"),
        Topic("events", "Automation", "Scheduled events",
            "Run periodic client commands safely.",
            "Open Options > Events to add, edit, enable, or remove scheduled commands. Turn on Events Enabled or the Timed Events capability. Events prime their timer after connection, pause while disconnected, and can pause during combat or be limited to out-of-combat use. Emergency Stop blocks them. Use conservative intervals so events do not interfere with normal play.",
            "events timed schedule interval overnight periodic commands"),
        Topic("privacy", "Safety", "Private client data",
            "How live map and Librarium data stay out of gameplay output.",
            "The Reforged server sends client-only frames for maps, reference pages, party state, PvP state, spells, and statistics. The protocol filter removes those frames before terminal rendering and logs. Only the normal gameplay text intended for the player appears in the terminal. Private refreshes use the same session and do not block ordinary play.",
            "private protocol terminal hidden data logs gameplay"),
        Topic("troubleshooting", "Troubleshooting", "Connections and stale information",
            "Recover from disconnects, stale panels, routes, or display problems.",
            "If disconnected, confirm the saved host and port, ensure the server is running, and reconnect. Auto Reconnect uses the configured delay. After reconnect the client discards transient combat, party, PvP, spell, loot, and roaming decisions and requests fresh state. If a route stops, inspect its recovery message and calculate a replacement route. Use Reset ANSI for damaged colors, Reset Internals for local parser/cache state, and Export Log when reporting visible gameplay problems. Emergency Stop is the fastest way to halt every automatic action.",
            "troubleshoot disconnect reconnect stale reset ansi route fault log stop"),
        Topic("limitations", "Safety", "Authority and fail-closed behavior",
            "What the client deliberately refuses to guess.",
            "The client does not invent commands, hidden objects, exits, targets, spells, boss timers, or room state. Unsupported server commands remain unavailable. Chasing a fleeing PvP attacker is withheld until the server exposes an authoritative chase route. Currency exchange is withheld when the server has no exchange command. BBS upload, download, WCCMUD.INI reload, and legacy BBS controls are outside the standalone Reforged client.",
            "limitations unsupported fail closed authority bbs chase exchange"),
    ];

    public static IReadOnlyList<ClientHelpTopic> Search(string? query)
    {
        var words = (query ?? string.Empty).Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? Topics
            : Topics.Where(topic => words.All(word => topic.SearchText.Contains(word, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    private static ClientHelpTopic Topic(string id, string category, string title, string summary, string content, string keywords) =>
        new(id, category, title, summary, content,
            keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
