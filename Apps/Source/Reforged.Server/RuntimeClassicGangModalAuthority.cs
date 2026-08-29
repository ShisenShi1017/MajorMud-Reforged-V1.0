namespace Reforged.Server;

/// <summary>
/// Exact WCCMMUD/Worldgroup gang-disband modal boundary. _cmd_disband moves the session from
/// active gameplay state 0x82 to modal state 0x88; host handler 0x004083DC accepts only one
/// parsed token whose first character uppercases to 'Y'. Every other input cancels.
/// </summary>
public static class RuntimeClassicGangModalAuthority
{
    public const int ActiveGameplayState = 0x82;
    public const int DisbandConfirmationState = 0x88;
    public const int NativeDisbandCommandVa = 0x00458725;
    public const int NativeDisbandConfirmationHandlerVa = 0x004083DC;

    public static bool ConfirmsDisband(string? input)
    {
        var tokens = (input ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 1 && tokens[0].Length != 0 && char.ToUpperInvariant(tokens[0][0]) == 'Y';
    }
}
