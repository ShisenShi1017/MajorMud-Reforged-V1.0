namespace Reforged.Server;

/// <summary>
/// WCCMMUD 0x0044FA7C/0x0044FB55 projection. Classic gang invitations are process-local
/// linked-list nodes keyed by target user + gang name. They are deliberately never written
/// into a character or gang save. A process restart therefore drops every outstanding invite.
/// </summary>
public static class RuntimeClassicGangInvitationRegistry
{
    private sealed record Invitation(string TargetCharacterId, string GangName);
    private static readonly object Gate = new();
    private static readonly Dictionary<string, List<Invitation>> ByScope = new(StringComparer.OrdinalIgnoreCase);

    public static bool Add(string scope, string targetCharacterId, string gangName)
    {
        lock (Gate)
        {
            var invitations = GetScope(scope);
            if (invitations.Any(value =>
                    value.TargetCharacterId.Equals(targetCharacterId, StringComparison.OrdinalIgnoreCase)
                    && value.GangName.Equals(gangName, StringComparison.OrdinalIgnoreCase)))
                return false;
            invitations.Add(new(targetCharacterId, gangName));
            return true;
        }
    }

    public static bool Contains(string scope, string targetCharacterId, string gangName)
    {
        lock (Gate)
        {
            return ByScope.TryGetValue(scope, out var invitations)
                && invitations.Any(value =>
                    value.TargetCharacterId.Equals(targetCharacterId, StringComparison.OrdinalIgnoreCase)
                    && value.GangName.Equals(gangName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Native _clear_gang_invitations(user, gangName). A null gang name clears every gang
    /// invitation for the target; a non-null gang name clears only that exact pair.
    /// </summary>
    public static int ClearForCharacter(string scope, string targetCharacterId, string? gangName = null)
    {
        lock (Gate)
        {
            if (!ByScope.TryGetValue(scope, out var invitations)) return 0;
            var before = invitations.Count;
            invitations.RemoveAll(value =>
                value.TargetCharacterId.Equals(targetCharacterId, StringComparison.OrdinalIgnoreCase)
                && (gangName is null || value.GangName.Equals(gangName, StringComparison.OrdinalIgnoreCase)));
            if (invitations.Count == 0) ByScope.Remove(scope);
            return before - invitations.Count;
        }
    }

    internal static IReadOnlyList<(string TargetCharacterId, string GangName)> Snapshot(string scope)
    {
        lock (Gate)
        {
            if (!ByScope.TryGetValue(scope, out var invitations)) return [];
            return invitations.Select(value => (value.TargetCharacterId, value.GangName)).ToArray();
        }
    }

    private static List<Invitation> GetScope(string scope)
    {
        if (!ByScope.TryGetValue(scope, out var invitations))
        {
            invitations = [];
            ByScope[scope] = invitations;
        }
        return invitations;
    }
}
