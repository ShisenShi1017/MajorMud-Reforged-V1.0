namespace RealmGate.Server;

/// <summary>
/// Native MajorMUD BREAK presentation recovered from WCCMMUD.DLL.
/// The native visible-break path calls _display_autocombat_broken before
/// _kill_autocombat(user, 1). _kill_autocombat clears only entries owned by that
/// user; _kill_autocombat_against is a separate routine and is not called by BREAK.
/// </summary>
public static class RuntimeClassicBreakAutocombatAuthority
{
    public static readonly string PlayerMessage = "*Combat Off*";
    public static readonly string ObserverSuffix = " breaks off combat.";
    public static readonly string NativeDisplayExport = "_display_autocombat_broken";
    public static readonly string NativeKillExport = "_kill_autocombat";
    public static readonly string NativeKillAgainstExport = "_kill_autocombat_against";
    public static readonly int NativeDisplayRva = 0x4BD6E;
    public static readonly int NativeKillRva = 0x4C8C2;
    public static readonly int NativeKillAgainstRva = 0x4C914;
    public static readonly bool CharacterOnly = true;
    public static readonly RuntimeClassicRoomMessagePriority ObserverPriority = RuntimeClassicRoomMessagePriority.High;

    public static string ObserverMessage(string displayName) => displayName + ObserverSuffix;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (PlayerMessage != "*Combat Off*") failures.Add("break-player-message");
        if (ObserverMessage("Shisenshi") != "Shisenshi breaks off combat.") failures.Add("break-observer-message");
        if (ObserverPriority != RuntimeClassicRoomMessagePriority.High) failures.Add("break-observer-priority");
        if (NativeDisplayRva != 0x4BD6E || NativeKillRva != 0x4C8C2 || NativeKillAgainstRva != 0x4C914) failures.Add("break-native-rva");
        if (!CharacterOnly) failures.Add("break-character-only");
        return failures;
    }
}
