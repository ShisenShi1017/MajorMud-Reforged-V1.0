using System.Text.RegularExpressions;
using Reforged.App.Models;

namespace Reforged.App.Services;

/// <summary>
/// Keeps login traffic isolated from gameplay/automation and prevents
/// Reforged-only private protocol commands from leaking to legacy BBS hosts.
/// Human typing and the dedicated login controller remain available before
/// the MajorMUD game prompt is verified.
/// </summary>
public static partial class ClientCommandIsolationPolicy
{
    public static bool HasVerifiedGameplay(ClientSessionConnection? session)
    {
        if (session is null || session.ConnectionState != ClientConnectionState.Connected)
            return false;

        if (session.RoomUpdatedAt is not null &&
            (!string.IsNullOrWhiteSpace(session.CurrentRoomName) ||
             !string.IsNullOrWhiteSpace(session.CurrentRoomKey)))
            return true;

        return session.PromptUpdatedAt is not null &&
               MajorMudGamePrompt().IsMatch(session.LivePromptText?.Trim() ?? string.Empty);
    }

    public static bool SupportsReforgedPrivateProtocol(ClientSessionConnection? session)
    {
        if (session is null) return false;

        var profile = session.ServerProfileName?.Trim() ?? string.Empty;
        var host = session.HostName?.Trim() ?? string.Empty;

        // Fail closed for known legacy/external BBS identities. Their command
        // interpreters may treat unknown words such as ROOMDATA as player speech.
        if (profile.Contains("Omniverse", StringComparison.OrdinalIgnoreCase))
            return false;

        // RealmGate profiles explicitly opt into the private client protocol; legacy Reforged profiles remain compatible.
        if (profile.Contains("RealmGate", StringComparison.OrdinalIgnoreCase) ||
            profile.Contains("Reforged", StringComparison.OrdinalIgnoreCase))
            return true;

        // The bundled server is normally reached through the Local Host profile
        // or a loopback endpoint. Preserve that existing local-server behavior.
        if (profile.Equals("Local Host", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("::1", StringComparison.OrdinalIgnoreCase))
            return true;

        // Unknown remote servers must prove compatibility through configuration
        // before private commands are ever placed on their terminal stream.
        return false;
    }

    public static bool CanTransmit(ClientSessionConnection? session, ClientCommandOrigin origin)
    {
        if (session is null || session.ConnectionState != ClientConnectionState.Connected)
            return false;

        if (origin is ClientCommandOrigin.Human or ClientCommandOrigin.SystemLogin)
            return true;

        if (!HasVerifiedGameplay(session))
            return false;

        return origin != ClientCommandOrigin.ClientProtocol ||
               SupportsReforgedPrivateProtocol(session);
    }

    [GeneratedRegex(@"^\s*\[(?:HP|H)\s*[=:]\s*[^\]]+\]\s*:?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MajorMudGamePrompt();
}
