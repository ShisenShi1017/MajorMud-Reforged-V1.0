using Reforged.App.Models;

namespace Reforged.App.Services;

/// <summary>
/// Temporary client-window-only freeze for command-generating MegaMUD automation.
/// Manual terminal entry and non-client systems are intentionally outside this gate.
/// </summary>
public static class ClientMegaMudAutomationGate
{
    public static bool IsEnabled => false;

    public const string DisabledReason =
        "MegaMUD client automation is disabled until the full client implementation is complete. Manual terminal play remains available.";

    public static void ForceDisabled(ClientAutomationProfile profile, ClientAutomationPolicy policy)
    {
        profile.Enabled = false;
        profile.PauseAll = true;
        policy.AutomationAllowed = false;
    }
}
