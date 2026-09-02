using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicExtensionBoundarySmokeReport(
    bool Passed,
    int BoundaryCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicExtensionBoundarySmokeReport Run()
    {
        var failures = RuntimeClassicExtensionBoundaryAuthority.Validate().ToList();
        var checks = new List<string>();
        if (failures.Count == 0)
        {
            checks.Add("native-78-command-export-census-trade-repair-absent");
            checks.Add("decoded-204-service-census-repair-absent");
            checks.Add("classic-trade-and-repair-extensions-denied");
            checks.Add("classic-give-stock-command-retained");
            checks.Add("custom-and-ai-extension-commands-retained");
            checks.Add("generic-infrastructure-receives-no-classic-parity-credit");
            checks.Add("native-gameplay-owners-remain-classified");
            checks.Add("native-save-catalog-byte-compatibility-is-separately-owned-not-an-extension-gap");
        }

        return new RuntimeClassicExtensionBoundarySmokeReport(
            failures.Count == 0,
            RuntimeClassicExtensionBoundaryAuthority.Rows.Count,
            checks,
            failures);
    }
}
