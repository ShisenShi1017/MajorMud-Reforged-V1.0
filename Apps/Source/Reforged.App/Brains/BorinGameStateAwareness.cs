using System.Collections.Generic;
using System.Linq;
using System.Net;
using Reforged.App.Models;

namespace Reforged.App.Brains;

/// <summary>
/// The verified phase of Borin's existing Client session. This is intentionally
/// separate from remembered stories and personality state: live Client evidence
/// always wins, and missing live evidence remains unknown.
/// </summary>
public enum BorinGamePhase
{
    Unknown,
    Disconnected,
    Connecting,
    BbsLogin,
    BbsMainMenu,
    MajorMudMenu,
    EnteringRealm,
    InGame,
    Faulted,
}

/// <summary>
/// Identifies the server on which Borin's character is actually playing. The
/// Reforged application, Forge workspace, and connected game server are separate.
/// </summary>
public enum BorinGameHostKind
{
    Unknown,
    OmniverseBbs,
    ReforgedServer,
    ExternalBbs,
}

/// <summary>
/// A point-in-time, read-only view of Borin's active MajorMUD character and
/// verified terminal perception. Nothing in this record grants command permission.
/// </summary>
public sealed record BorinGameStateSnapshot(
    BorinGamePhase Phase,
    string PhaseDetail,
    string ConnectionState,
    BorinLoginState LoginState,
    bool IsConnected,
    bool IsLoggedIntoMajorMud,
    bool HasVerifiedLiveState,
    string CharacterName,
    string Server,
    string World,
    BorinGameHostKind HostKind,
    string HostName,
    string CurrentRoom,
    string CurrentRoomKey,
    IReadOnlyList<string> CurrentExits,
    bool HasVerifiedPrompt,
    bool HasVerifiedRoom,
    bool HasVerifiedExits,
    bool CanAutonomousPlay,
    string AutonomousPlayBlockReason,
    string RoomFreshness,
    string ExitFreshness,
    string PromptFreshness,
    int HealthPercent,
    int ManaPercent,
    int HealthCurrent,
    int HealthMaximum,
    int ManaCurrent,
    int ManaMaximum,
    bool InCombat,
    bool IsResting,
    bool IsMoving,
    int CharacterLevel,
    long ExperienceCurrent,
    long ExperienceNeeded,
    long CarriedCurrency,
    IReadOnlyList<string> InventoryItems,
    IReadOnlyList<string> EquippedItems,
    string InventoryFreshness,
    string EquipmentFreshness,
    string LastCommand,
    ClientCommandOrigin LastCommandOrigin,
    string LastCommandFreshness,
    DateTimeOffset CapturedAt)
{
    public const int HardInGameReplyCharacterLimit = 270;
    public const int PreferredInGameReplyCharacterTarget = 240;

    public static BorinGameStateSnapshot Empty { get; } = new(
        BorinGamePhase.Unknown,
        "No Client session evidence was supplied for this turn.",
        ClientConnectionState.Disconnected.ToString(),
        BorinLoginState.Disconnected,
        false,
        false,
        false,
        "Borin",
        "Unknown server",
        "Unknown world",
        BorinGameHostKind.Unknown,
        string.Empty,
        "Unknown",
        string.Empty,
        [],
        false,
        false,
        false,
        false,
        "No verified Client session is available.",
        "not captured",
        "not captured",
        "not captured",
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        0,
        0,
        0,
        0,
        [],
        [],
        "not captured",
        "not captured",
        string.Empty,
        ClientCommandOrigin.Human,
        "not captured",
        DateTimeOffset.MinValue);

    public string GameServerIdentity => HostKind switch
    {
        BorinGameHostKind.OmniverseBbs => $"Omniverse BBS ({Display(HostName, Server)})",
        BorinGameHostKind.ReforgedServer => $"the Reforged server ({Display(HostName, Server)})",
        BorinGameHostKind.ExternalBbs => $"{Server} ({Display(HostName, "external host")})",
        _ => Display(Server, "unknown game server"),
    };

    public string EnvironmentSummary
    {
        get
        {
            var target = HostKind switch
            {
                BorinGameHostKind.OmniverseBbs => $"Omniverse BBS • world {World}",
                BorinGameHostKind.ReforgedServer => $"the Reforged server • world {World}",
                BorinGameHostKind.ExternalBbs => $"external server {Server} • world {World}",
                _ => "game server not verified",
            };

            if (!IsConnected)
                return $"Reforged application • configured for {target} • disconnected";
            if (!IsLoggedIntoMajorMud)
                return $"Reforged application • connected to {target} • MajorMUD login not verified";
            return $"Reforged application • playing on {target}";
        }
    }

    public string PerceptionSummary =>
        $"Prompt {(HasVerifiedPrompt ? "seen" : "not verified")} • " +
        $"room {(HasVerifiedRoom ? CurrentRoom : "not verified")} • " +
        $"exits {(HasVerifiedExits ? FormatItems(CurrentExits) : "not verified")} • " +
        $"last command {(string.IsNullOrWhiteSpace(LastCommand) ? "none captured" : $"{LastCommandOrigin}: {LastCommand}")}";

    public string Summary =>
        $"{Phase} • {EnvironmentSummary} • connection {ConnectionState} • login {LoginState} • " +
        (HasVerifiedLiveState
            ? $"live at {Display(CurrentRoom, "unknown room")} • HP {HealthPercent}% MP {ManaPercent}%"
            : "live realm state not verified");

    public string PromptText => BuildPrompt(externalConversation: false);
    public string ExternalPromptText => BuildPrompt(externalConversation: true);

    public static BorinGameStateSnapshot Capture(
        BorinPlayerSettings settings,
        BorinLoginStatus login,
        ClientSessionConnection? session)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(login);

        var now = DateTimeOffset.Now;
        var connection = session?.ConnectionState ?? ClientConnectionState.Disconnected;
        var connected = connection == ClientConnectionState.Connected;
        var ready = connected && login.State == BorinLoginState.Ready;
        var phase = ResolvePhase(connection, login.State);
        var hostKind = ResolveHostKind(
            session?.ServerProfileName ?? settings.ServerProfileName,
            session?.HostName ?? settings.HostName);

        var promptVerified = ready && IsFresh(session?.PromptUpdatedAt, now, TimeSpan.FromMinutes(2));
        var roomVerified = ready &&
                           !string.IsNullOrWhiteSpace(session?.CurrentRoomName) &&
                           IsFresh(session?.RoomUpdatedAt, now, TimeSpan.FromMinutes(5));
        var exitsBelongToRoom = session?.ExitsUpdatedAt is not null &&
                                session.RoomUpdatedAt is not null &&
                                session.ExitsUpdatedAt >= session.RoomUpdatedAt;
        var exitsVerified = roomVerified && exitsBelongToRoom &&
                            IsFresh(session?.ExitsUpdatedAt, now, TimeSpan.FromMinutes(5));

        // Session parsers may retain the last recognized values after a disconnect.
        // Expose them only while the matching live evidence remains fresh.
        var room = roomVerified ? Normalize(session?.CurrentRoomName, "Unknown") : "Unknown";
        var roomKey = roomVerified ? Normalize(session?.CurrentRoomKey, string.Empty) : string.Empty;
        var exits = exitsVerified && session is not null
            ? SnapshotItems(session.CurrentRoomExits)
            : Array.Empty<string>();
        var inventory = ready && session is not null
            ? SnapshotItems(session.InventoryItems)
            : Array.Empty<string>();
        var equipment = ready && session is not null
            ? SnapshotItems(session.EquippedItems)
            : Array.Empty<string>();

        var inCombat = ready && session?.InCombat == true;
        var moving = ready && session?.IsMoving == true;
        var healthCurrent = ready ? session?.HealthCurrent ?? 0 : 0;
        var healthMaximum = ready ? session?.HealthMaximum ?? 0 : 0;
        var healthVerified = ready && healthMaximum > 0;
        var health = healthVerified ? session?.HealthPercent ?? 0 : 0;
        // Autonomous play is gated by authoritative connection/perception, not by gameplay state.
        // Combat, low health, resting, zero-exit rooms, and unresolved tactical situations are
        // conditions Borin must reason about and play through rather than reasons to stop him.
        var canAutonomousPlay = ready && promptVerified && roomVerified && !moving;
        var blockReason = ResolveAutonomousBlockReason(
            ready,
            promptVerified,
            roomVerified,
            exitsVerified,
            exits.Length,
            healthVerified,
            inCombat,
            moving,
            health,
            settings.DangerHealthPercent);

        return new BorinGameStateSnapshot(
            phase,
            Normalize(login.Detail, phase.ToString(), 240),
            connection.ToString(),
            login.State,
            connected,
            ready,
            ready && promptVerified,
            Normalize(session?.CharacterName, settings.CharacterName, 40),
            Normalize(session?.ServerProfileName, settings.ServerProfileName, 80),
            Normalize(session?.WorldName, settings.WorldName, 80),
            hostKind,
            Normalize(session?.HostName, settings.HostName, 160),
            room,
            roomKey,
            exits,
            promptVerified,
            roomVerified,
            exitsVerified,
            canAutonomousPlay,
            blockReason,
            ready ? DescribeFreshness(session?.RoomUpdatedAt, now) : "not live",
            ready ? DescribeFreshness(session?.ExitsUpdatedAt, now) : "not live",
            ready ? DescribeFreshness(session?.PromptUpdatedAt, now) : "not live",
            health,
            ready && (session?.ManaMaximum ?? 0) > 0 ? session?.ManaPercent ?? 0 : 0,
            healthCurrent,
            healthMaximum,
            ready ? session?.ManaCurrent ?? 0 : 0,
            ready ? session?.ManaMaximum ?? 0 : 0,
            inCombat,
            ready && session?.IsResting == true,
            moving,
            ready ? session?.CharacterLevel ?? 0 : 0,
            ready ? session?.ExperienceCurrent ?? 0 : 0,
            ready ? session?.ExperienceNeeded ?? 0 : 0,
            ready ? session?.CarriedCurrency ?? 0 : 0,
            inventory,
            equipment,
            ready ? DescribeFreshness(session?.InventoryUpdatedAt, now) : "not live",
            ready ? DescribeFreshness(session?.EquipmentUpdatedAt, now) : "not live",
            Normalize(session?.LastCommandText, string.Empty, 160),
            session?.LastCommandOrigin ?? ClientCommandOrigin.Human,
            DescribeFreshness(session?.LastCommandAt, now),
            now);
    }

    internal static BorinGameHostKind ResolveHostKind(string? serverProfile, string? hostName)
    {
        var profile = serverProfile?.Trim() ?? string.Empty;
        var host = hostName?.Trim() ?? string.Empty;
        // The connected endpoint outranks a stale display/profile label. This is
        // what lets Borin distinguish an Omniverse connection from the local
        // Reforged server even when a previously saved Forge form still has the
        // other profile name selected.
        if (host.Contains("omniversebbs", StringComparison.OrdinalIgnoreCase))
            return BorinGameHostKind.OmniverseBbs;
        if (IsLocalHost(host))
            return BorinGameHostKind.ReforgedServer;

        if (profile.Contains("omniverse", StringComparison.OrdinalIgnoreCase))
            return BorinGameHostKind.OmniverseBbs;

        if (profile.Contains("reforged", StringComparison.OrdinalIgnoreCase) ||
            profile.Contains("local host", StringComparison.OrdinalIgnoreCase))
        {
            return BorinGameHostKind.ReforgedServer;
        }

        return profile.Length > 0 || host.Length > 0
            ? BorinGameHostKind.ExternalBbs
            : BorinGameHostKind.Unknown;
    }

    private string BuildPrompt(bool externalConversation)
    {
        var identityRule = !IsConnected
            ? HostKind switch
            {
                BorinGameHostKind.OmniverseBbs =>
                    "- Omniverse BBS is the configured target, but the Client is disconnected. Do not say your character is playing there until a live connection and MajorMUD login are verified.",
                BorinGameHostKind.ReforgedServer =>
                    "- The Reforged server is the configured target, but the Client is disconnected. Do not say your character is playing there until a live connection and MajorMUD login are verified.",
                BorinGameHostKind.ExternalBbs =>
                    $"- {Server} is the configured target, but the Client is disconnected. Do not say your character is playing there until a live connection and MajorMUD login are verified.",
                _ =>
                    "- No live game-server connection is verified. Do not guess which server you are playing on.",
            }
            : !IsLoggedIntoMajorMud
                ? $"- The Client is connected to {GameServerIdentity}, but the MajorMUD realm login is not verified yet. Say connected or logging in, not playing in the realm."
                : HostKind switch
                {
                    BorinGameHostKind.OmniverseBbs =>
                        "- You are using the MajorMUD Reforged application, and your active character is verified playing on Omniverse BBS. Do not call Omniverse the Reforged server.",
                    BorinGameHostKind.ReforgedServer =>
                        "- Your active character is verified playing on the Reforged server. The Forge workspace is still an application workspace, not a room inside the game.",
                    BorinGameHostKind.ExternalBbs =>
                        $"- Your active character is verified playing on {Server}, an external game server. MajorMUD Reforged is the client application observing and controlling that session.",
                    _ =>
                        "- A MajorMUD realm login is verified, but the server identity is not. Do not guess the server name.",
                };

        var liveState = HasVerifiedLiveState
            ? $"""
- HP prompt verified: {HasVerifiedPrompt} ({PromptFreshness}).
- Current room: {(HasVerifiedRoom ? CurrentRoom : "not verified")} ({RoomFreshness}).
- Room key: {Display(CurrentRoomKey, "not recognized")}.
- Current visible exits: {(HasVerifiedExits ? FormatItems(CurrentExits) : "not verified")} ({ExitFreshness}).
- Vitals: HP {FormatVital(HealthCurrent, HealthMaximum, HealthPercent)}; MP {FormatVital(ManaCurrent, ManaMaximum, ManaPercent)}.
- Activity: {(InCombat ? "in combat" : IsMoving ? "moving" : IsResting ? "resting" : "not recognized as fighting, moving, or resting")}.
- Level/experience: {(CharacterLevel > 0 ? $"level {CharacterLevel}; XP {ExperienceCurrent:N0}" + (ExperienceNeeded > 0 ? $"/{ExperienceNeeded:N0}" : string.Empty) : "not captured")}.
- Carried currency: {(CarriedCurrency > 0 ? CarriedCurrency.ToString("N0") : "not captured or zero")}.
- Captured inventory ({InventoryFreshness}): {FormatItems(InventoryItems)}.
- Captured equipment ({EquipmentFreshness}): {FormatItems(EquippedItems)}.
- Last command observed by the Client: {(LastCommand.Length == 0 ? "none captured" : $"{LastCommandOrigin} sent {LastCommand} ({LastCommandFreshness})")}.
- Autonomous play readiness: {CanAutonomousPlay}. {AutonomousPlayBlockReason}
"""
            : "- Room, exits, vitals, combat, movement, inventory, equipment, level, experience, and currency are not verified live now. Do not reuse stale remembered values.";
        var privacy = externalConversation
            ? "- Do not volunteer private vitals, inventory, equipment, session details, host information, credentials, or control state. Use a live fact only when directly relevant."
            : "- William may ask about this live state directly. Answer from this snapshot instead of asking him to supply evidence already present here.";

        return $"""
CURRENT VERIFIED GAME-STATE AND ENVIRONMENT CONTRACT
- Application: MajorMUD Reforged.
- Character: {CharacterName}.
- Game server target/identity: {GameServerIdentity}; configured profile/world: {Server}/{World}.
- Client connection: {ConnectionState}; connected: {IsConnected}.
- Login state: {LoginState}; MajorMUD realm entered: {IsLoggedIntoMajorMud}.
- Current phase: {Phase}. Detail: {PhaseDetail}
- Fresh live realm state verified now: {HasVerifiedLiveState}.
{identityRule}
{liveState}
Authority and honesty rules:
- Reforged is the application. Omniverse BBS, a Reforged server, and any other BBS are distinct game servers. Use the connection and login flags to distinguish a configured target from a live connection and verified play.
- This point-in-time Client snapshot is the only authority for current server identity, login, room, exits, combat, vitals, inventory, and equipment. It overrides old memories and prior dialogue.
- Connected does not mean logged into MajorMUD. Only a connected session with login state Ready and a fresh HP prompt is verified in-game.
- Report only the exact visible exits listed above. Never invent where an exit leads. An exit destination is known only from verified transition evidence or decoded authority explicitly supplied in the turn.
- Command ownership is literal. Human means William typed it; Borin means Borin's player controller sent it; RouteAutomation means a Client route sent it; SystemLogin means the login controller sent it. Never claim a Human command as your own action.
- Unknown or uncaptured values remain unknown. Never infer them from a goal, memory, model reply, or previous session.
- Captured inventory and equipment names are Client observations, not proof of hidden item effects or server rules.
- This state grants no permission to send commands, move, fight, buy, equip, trade, drop, or modify anything.
{privacy}
- Every in-game spoken reply has a hard limit of {HardInGameReplyCharacterLimit} characters. Aim for {PreferredInGameReplyCharacterTarget} or fewer so the answer ends naturally; the sender truncates overflow safely.
""";
    }

    private static string ResolveAutonomousBlockReason(
        bool ready,
        bool promptVerified,
        bool roomVerified,
        bool exitsVerified,
        int exitCount,
        bool healthVerified,
        bool inCombat,
        bool moving,
        int health,
        int dangerHealthPercent)
    {
        if (!ready) return "Borin is not verified inside the MajorMUD realm.";
        if (!promptVerified) return "The HP prompt has not been seen recently.";
        if (!roomVerified) return "The current room title has not been seen recently. Send LOOK.";
        if (moving) return "Waiting for the current movement command to resolve.";
        if (!exitsVerified) return "Autonomous play is ready; exits are not currently verified, so movement remains unavailable until Borin refreshes them.";
        if (exitCount == 0) return "Autonomous play is ready in a room with no parsed exits; Borin may use other ordinary player actions.";
        if (!healthVerified) return "Autonomous play is ready with scalar HP only; Borin must treat health percentage as unknown until maximum HP is verified.";
        if (inCombat) return "Autonomous play is ready and combat is active; Borin must choose his own combat or survival action.";
        if (healthVerified && health <= dangerHealthPercent) return $"Autonomous play is ready; health is at or below {dangerHealthPercent}%, so Borin should prioritize survival.";
        return "Verified perception is ready for continuous autonomous play.";
    }

    private static bool IsFresh(DateTimeOffset? timestamp, DateTimeOffset now, TimeSpan maximumAge)
    {
        if (timestamp is null) return false;
        var age = now - timestamp.Value;
        return age >= TimeSpan.Zero && age <= maximumAge;
    }

    private static bool IsLocalHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private static string[] SnapshotItems(IEnumerable<string> source)
    {
        try
        {
            return source
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => Normalize(item, string.Empty, 100))
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToArray();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }

    private static BorinGamePhase ResolvePhase(ClientConnectionState connection, BorinLoginState login) =>
        connection switch
        {
            ClientConnectionState.Faulted => BorinGamePhase.Faulted,
            ClientConnectionState.Connecting or ClientConnectionState.Reconnecting => BorinGamePhase.Connecting,
            ClientConnectionState.Disconnected => BorinGamePhase.Disconnected,
            _ => login switch
            {
                BorinLoginState.WaitingForLoginPrompt or
                BorinLoginState.SendingLogin or
                BorinLoginState.WaitingForPasswordPrompt or
                BorinLoginState.SendingPassword => BorinGamePhase.BbsLogin,
                BorinLoginState.WaitingForMainMenu or
                BorinLoginState.SelectingMajorMud => BorinGamePhase.BbsMainMenu,
                BorinLoginState.WaitingForMajorMudMenu => BorinGamePhase.MajorMudMenu,
                BorinLoginState.EnteringRealm or
                BorinLoginState.WaitingForGamePrompt => BorinGamePhase.EnteringRealm,
                BorinLoginState.Ready => BorinGamePhase.InGame,
                BorinLoginState.Failed => BorinGamePhase.Faulted,
                BorinLoginState.Connecting => BorinGamePhase.Connecting,
                _ => BorinGamePhase.Unknown,
            },
        };

    private static string DescribeFreshness(DateTimeOffset? timestamp, DateTimeOffset now)
    {
        if (timestamp is null) return "capture time unknown";
        var age = now - timestamp.Value;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age <= TimeSpan.FromSeconds(15)) return "captured within 15 seconds";
        if (age <= TimeSpan.FromSeconds(30)) return "captured within 30 seconds";
        if (age <= TimeSpan.FromMinutes(2)) return "captured within 2 minutes";
        if (age <= TimeSpan.FromMinutes(5)) return "captured within 5 minutes";
        return "older client capture";
    }

    private static string FormatVital(int current, int maximum, int percent) =>
        maximum > 0
            ? $"{current}/{maximum} ({percent}%)"
            : current > 0
                ? $"{current} current; maximum not verified"
                : "not captured";

    private static string FormatItems(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none captured" : string.Join(", ", items);

    private static string Display(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string Normalize(string? value, string fallback, int maximumLength = 160)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return candidate[..Math.Min(maximumLength, candidate.Length)];
    }
}
