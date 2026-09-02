using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeExtensionBoundariesSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public static RuntimeExtensionBoundariesSmokeReport Run()
    {
        var definitions = new[] {
            new RuntimeExtensionDefinition("classic-observability",RuntimeWorldMode.Classic,1,new HashSet<RuntimeExtensionSurface>{RuntimeExtensionSurface.Runtime,RuntimeExtensionSurface.Librarium},true,true),
            new RuntimeExtensionDefinition("custom-builder",RuntimeWorldMode.Custom,2,new HashSet<RuntimeExtensionSurface>{RuntimeExtensionSurface.Builder,RuntimeExtensionSurface.Save})
        };
        var service = new RuntimeExtensionBoundaryService(definitions);
        var checks = new List<string>();
        var classic = new RuntimeExtensionContext(RuntimeWorldMode.Classic,true,1,new HashSet<string>{"classic-observability","custom-builder"});
        if(service.Authorize("classic-observability",RuntimeExtensionSurface.Runtime,classic,false).Allowed) checks.Add("classic-compatible-allowed");
        if(!service.Authorize("custom-builder",RuntimeExtensionSurface.Builder,classic,true).Allowed) checks.Add("custom-blocked-in-classic");
        if(service.ValidateClassicIsolation(classic).SequenceEqual(new[]{"custom-builder"},StringComparer.OrdinalIgnoreCase)) checks.Add("classic-isolation-detected");
        var custom = new RuntimeExtensionContext(RuntimeWorldMode.Custom,true,1,new HashSet<string>{"custom-builder"});
        var migration = service.Authorize("custom-builder",RuntimeExtensionSurface.Save,custom,true);
        if(!migration.Allowed && migration.RequiresMigration && migration.RequiredVersion==2) checks.Add("migration-required");
        var current = custom with { SaveVersion=2 };
        if(service.Authorize("custom-builder",RuntimeExtensionSurface.Builder,current,true).Allowed) checks.Add("custom-authorized");
        if(!service.Authorize("custom-builder",RuntimeExtensionSurface.Builder,current with { HostAuthorized=false },true).Allowed) checks.Add("host-boundary-enforced");
        return new(checks.Count==6,checks);
    }
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}
