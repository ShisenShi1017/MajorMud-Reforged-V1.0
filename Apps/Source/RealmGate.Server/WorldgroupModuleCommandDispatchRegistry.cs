namespace RealmGate.Server;

public static class WorldgroupModuleCommandDispatchRegistry
{
    public const int GlobalCommandCapacity = 50;
    public const int ModuleStatisticsStride = 36;
    public const int State4HandlerOffset = 0x1C;
    public const int DefaultHandlerOffset = 0x20;
    public const int RequiredHostCallbackOffset = 0x24;
    public const int State5HandlerOffset = 0x2C;
    public const int SessionArgumentCallbackOffset = 0x38;

    public enum GlobalCommandResult
    {
        HostAction = -2,
        Handled = -3,
        Continue = -4
    }

    public static int SelectInputHandlerOffset(int sessionState) => sessionState switch
    {
        4 => State4HandlerOffset,
        5 => State5HandlerOffset,
        _ => DefaultHandlerOffset
    };
}
