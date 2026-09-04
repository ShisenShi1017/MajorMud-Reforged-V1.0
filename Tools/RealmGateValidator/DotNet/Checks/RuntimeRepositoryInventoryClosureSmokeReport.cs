using System.Collections.Generic;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeRepositoryInventoryClosureSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeRepositoryInventoryClosureSmokeReport Run()
    {
        var service = new RuntimeRepositoryInventoryClosureService();
        var systems = new HashSet<string> { "inventory-closure", "party-coordination" };
        var valid = service.Validate(new[] { new RuntimeInventoryDeclaration("commands.party", "party-coordination", "RuntimePartyCoordinationService", "world-session coordination") }, systems);
        var invalid = service.Validate(new[] { new RuntimeInventoryDeclaration("bad", "unknown", "", "") }, systems);
        var checks = new List<string> { "valid-ownership:" + valid.Passed, "unknown-owner-fails-closed:" + (!invalid.Passed) };
        return new RuntimeRepositoryInventoryClosureSmokeReport(valid.Passed && !invalid.Passed, checks);
    }
}
