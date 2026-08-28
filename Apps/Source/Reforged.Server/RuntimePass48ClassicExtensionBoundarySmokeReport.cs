using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass48ClassicExtensionBoundarySmokeReport(
    bool Passed,
    int BoundaryCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass48ClassicExtensionBoundarySmokeReport Run()
    {
        var failures = RuntimeClassicExtensionBoundaryAuthority.Validate().ToList();
        var checks = new List<string>();
        if (failures.Count == 0)
        {
            checks.Add("classic-trade-and-repair-extensions-denied");
            checks.Add("classic-give-stock-command-retained");
            checks.Add("custom-and-ai-extension-commands-retained");
            checks.Add("generic-infrastructure-receives-no-classic-parity-credit");
            checks.Add("native-gameplay-owners-remain-classified");
            checks.Add("native-save-catalog-compatibility-remains-unresolved");
        }

        return new RuntimePass48ClassicExtensionBoundarySmokeReport(
            failures.Count == 0,
            RuntimeClassicExtensionBoundaryAuthority.Rows.Count,
            checks,
            failures);
    }
}
