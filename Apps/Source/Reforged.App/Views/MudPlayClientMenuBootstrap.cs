namespace Reforged.App.Views;

/// <summary>
/// Compatibility shim retained during the RealmGate client simplification.
/// The former MudPlay-inspired dynamic menu injection is intentionally disabled.
/// Player-facing features now belong in the fixed MegaMUD-style client menus,
/// Character workspace, Librarium, Automation, Party, BBS Profiles, or Creator Studio.
/// </summary>
internal static class MudPlayClientMenuBootstrap
{
    internal static void ApplyCustomizations(ClientTerminalWindow terminal)
    {
        // Intentionally no-op. The mutable MudPlay-style toolbar/menu injection has been retired.
    }
}
