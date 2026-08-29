using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNativePlayerCommandSmokeReport(
    bool Passed,
    int DocumentedCommandCount,
    int ExplicitAliasCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicNativePlayerCommandSmokeReport Run(string projectRoot)
    {
        var registry = RuntimeClassicNativePlayerCommandRegistry.Load(projectRoot);
        var failures = registry.Validate().ToList();
        var checks = new List<string>();

        if (RuntimeCommandRuntimeRouteCatalog.Require("health").Kind != RuntimeCommandRouteKind.CharacterState)
            failures.Add("HEALTH must execute through the live character-state runtime, not the native evidence placeholder.");
        else
            checks.Add("health-routes-to-live-character-state-runtime");

        if (RuntimeCommandRuntimeRouteCatalog.Require("invoke").Kind != RuntimeCommandRouteKind.PowerRuntime)
            failures.Add("INVOKE must execute through the live power runtime, not the native evidence placeholder.");
        else
            checks.Add("invoke-routes-to-live-kai-power-runtime");

        if (RuntimeCommandRuntimeRouteCatalog.Require("exits").Kind != RuntimeCommandRouteKind.RoomPresentation)
            failures.Add("EXITS must execute through the live room-presentation runtime, not the native evidence placeholder.");
        else
            checks.Add("exits-routes-to-live-obvious-exit-presentation");

        if (RuntimeCommandRuntimeRouteCatalog.Require("ready").Kind != RuntimeCommandRouteKind.ItemRuntime)
            failures.Add("READY must execute through the live item/equipment runtime, not the native evidence placeholder.");
        else
            checks.Add("ready-routes-to-live-equipment-runtime");

        if (RuntimeCommandRuntimeRouteCatalog.Require("profile").Kind != RuntimeCommandRouteKind.CharacterState)
            failures.Add("PROFILE must execute through the live character/profile runtime; native help message 37 proves it displays current profile settings.");
        else
            checks.Add("profile-routes-to-live-current-profile-renderer");

        if (RuntimeCommandRuntimeRouteCatalog.Require("scan").Kind != RuntimeCommandRouteKind.CharacterState)
            failures.Add("SCAN must execute through the live character-state runtime; WCCMMUD.DLL command 0x3A calls display_scan_of_users(0).");
        else
            checks.Add("scan-routes-to-live-current-adventurer-scan");

        if (RuntimeCommandRuntimeRouteCatalog.Require("reroll").Kind != RuntimeCommandRouteKind.SessionLifecycle)
            failures.Add("REROLL must execute through the interactive session lifecycle so the departed character is replaced rather than mutated in place.");
        else
            checks.Add("reroll-routes-to-interactive-character-replacement-lifecycle");

        if (failures.Count == 0)
        {
            checks.Add("63-native-help-command-identities-resolve-through-live-parser");
            checks.Add("explicit-native-aliases-bs-br-bg-resolve-to-documented-command");
            checks.Add("undocumented-prefix-and-precedence-rules-remain-unclaimed");
        }

        return new RuntimeClassicNativePlayerCommandSmokeReport(
            failures.Count == 0,
            registry.Records.Count,
            registry.Records.Sum(record => record.ExplicitAliases.Count),
            checks,
            failures);
    }
}
