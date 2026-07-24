namespace Reforged.Server;

/// <summary>
/// Evidence-backed classification of WCCMMUD callbacks selected by Worldgroup session state.
/// Corrected in Worldgroup Native Closure Update0009 from the executable import table and
/// the instruction stream at 0x00405104.
/// </summary>
public static class WorldgroupMajorMudSessionCallbackRegistry
{
    public const uint State4Entry = 0x004031D4;
    public const uint DefaultEntry = 0x00405104;
    public const uint State5Entry = 0x00408D3C;

    // WCCMMUD import thunks / IAT slots, not MajorMUD-owned parser storage.
    public const uint InjectCarriageReturnThunk = 0x004789CA; // WGSERVER!_injacr
    public const uint GlobalCommandVectorIat = 0x0049D4D8;    // WGSERVER!_globs
    public const uint GlobalCommandCountIat = 0x0049D48C;     // WGSERVER!_nglobs

    public enum CallbackRole
    {
        SessionInitialization,
        ActiveSessionDispatcher,
        SessionTransitionCleanup
    }

    public enum GlobalCommandResult
    {
        HostActionAndHandled = -2,
        Handled = -3,
        Continue = -4
    }

    public static CallbackRole Classify(int sessionState) => sessionState switch
    {
        4 => CallbackRole.SessionInitialization,
        5 => CallbackRole.SessionTransitionCleanup,
        _ => CallbackRole.ActiveSessionDispatcher
    };

    public static bool IsTerminalGlobalCommandResult(int result) => result is -2 or -3;
    public static bool ContinuesGlobalCommandChain(int result) => result == -4;
}
