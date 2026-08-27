namespace Reforged.App.Brains;

internal sealed record BorinAutonomousSafetyDecision(bool Allowed, string Reason)
{
    public static BorinAutonomousSafetyDecision Permit(string reason) => new(true, reason);
    public static BorinAutonomousSafetyDecision Reject(string reason) => new(false, reason);
}

/// <summary>
/// Deterministic final safety boundary for Borin's self-directed player commands.
/// This does not restrict ordinary gameplay strategy: combat, travel, spells, shops,
/// quests, communication, inventory, and equipment remain autonomous. It blocks
/// account/session destruction, privilege-control aliases, command batching, and
/// irreversible inventory actions attempted without fresh ownership evidence.
/// </summary>
internal static class BorinAutonomousSafetyPolicy
{
    private static readonly HashSet<string> AutonomousControlRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "QUIT", "LOGOFF", "LOGOUT", "DISCONNECT", "HANGUP",
        "DELETE", "ERASE", "REROLL", "SUICIDE",
        "SYS", "SYSTEM", "SYSOP", "GAMEOP", "GOP", "ADMIN", "ADM", "WIZ", "WIZARD", "GOTO",
        "ACCOUNT", "PASSWORD", "PASSWD", "USER-ID", "USERID",
        "SCRIPT", "MACRO", "EXEC", "SHELL", "POWERSHELL", "CMD",
    };

    private static readonly HashSet<string> ArgumentRequiredRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "BUY", "SELL", "DROP", "GIVE", "PUT", "WEAR", "EQUIP", "REMOVE",
        "USE", "DRINK", "EAT", "ATTACK", "KILL", "CAST", "OPEN", "CLOSE",
        "UNLOCK", "LOCK", "PUSH", "PULL", "TALK", "ASK", "READ", "EXAMINE",
    };

    private static readonly HashSet<string> InventoryEvidenceRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELL", "DROP", "GIVE", "PUT", "WEAR", "EQUIP", "DRINK", "EAT",
    };

    private static readonly HashSet<string> EquipmentEvidenceRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "REMOVE",
    };

    public static BorinAutonomousSafetyDecision Validate(string? command, BorinGameStateSnapshot state)
    {
        var normalized = Normalize(command);
        if (normalized.Length == 0)
            return BorinAutonomousSafetyDecision.Reject("Autonomous gameplay cannot send an empty command.");

        if (ContainsCommandSeparator(normalized))
            return BorinAutonomousSafetyDecision.Reject("Autonomous gameplay sends exactly one MajorMUD command per decision; command batching/separators are blocked.");

        var parts = normalized.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var root = parts[0];
        var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        if (AutonomousControlRoots.Contains(root))
            return BorinAutonomousSafetyDecision.Reject($"'{root}' is account/session, character-destruction, privilege-control, script, or operating-system control and is not autonomous player gameplay.");

        if (ArgumentRequiredRoots.Contains(root) && argument.Length == 0)
            return BorinAutonomousSafetyDecision.Reject($"'{root}' needs a concrete player-game target or argument; Borin may not send an underspecified state-changing command.");

        if (InventoryEvidenceRoots.Contains(root) && !HasFreshCapture(state.InventoryFreshness))
            return BorinAutonomousSafetyDecision.Reject($"'{root}' can change or dispose of possessions, so Borin must refresh INVENTORY before acting autonomously.");

        if (EquipmentEvidenceRoots.Contains(root) && !HasFreshCapture(state.EquipmentFreshness))
            return BorinAutonomousSafetyDecision.Reject($"'{root}' changes worn equipment, so Borin must refresh EQUIPMENT before acting autonomously.");

        return BorinAutonomousSafetyDecision.Permit("The command stays inside the autonomous ordinary-player safety boundary.");
    }

    internal static bool IsPerceptionRecoveryCommand(string? command)
    {
        var normalized = Normalize(command);
        return normalized is "LOOK" or "L" or "ROOM INFORMATION" or "RI";
    }

    private static bool HasFreshCapture(string? freshness)
    {
        var value = freshness?.Trim() ?? string.Empty;
        return value.Contains("captured within", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("older", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("unknown", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("not live", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCommandSeparator(string command) =>
        command.Contains(';') ||
        command.Contains("&&", StringComparison.Ordinal) ||
        command.Contains("||", StringComparison.Ordinal);

    private static string Normalize(string? command) =>
        System.Text.RegularExpressions.Regex.Replace(command?.Trim() ?? string.Empty, @"\s+", " ").ToUpperInvariant();
}
