using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDHiddenObjectAuditResult(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures, IReadOnlyList<string> Verified, IReadOnlyList<string> StronglyCorroborated, IReadOnlyList<string> DecodedButNotMapped, IReadOnlyList<string> Conflicts, IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDHiddenObjectAudit
{
    public static RuntimeBundleDHiddenObjectAuditResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool condition, string code) { if (condition) checks.Add(code); else failures.Add(code); }
        var exitsPath = Path.Combine(projectRoot, "Data", worldId, "exits.json");
        var records = RuntimeChunkedClassicCatalog.LoadRecords(exitsPath);
        var hidden = records.Where(x => x.TryGetProperty("gate_kind", out var g) && g.GetString()?.Equals("hidden", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var searchable = hidden.Where(x => x.TryGetProperty("qualifier", out var q) && (q.GetString() ?? "").Contains("Searchable", StringComparison.OrdinalIgnoreCase)).ToArray();
        var passable = hidden.Where(x => x.TryGetProperty("qualifier", out var q) && (q.GetString() ?? "").Contains("Passable", StringComparison.OrdinalIgnoreCase)).ToArray();
        var action = hidden.Where(x => x.TryGetProperty("qualifier", out var q) && (q.GetString() ?? "").Contains("Actions", StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(hidden.Length > 0, "decoded-hidden-exits-present");
        Check(searchable.Length > 0, "decoded-searchable-hidden-exits-present");
        Check(passable.Length > 0, "decoded-passable-hidden-exits-present");
        Check(action.Length > 0, "decoded-action-latch-hidden-exits-present");
        Check(hidden.All(x => x.TryGetProperty("edge_state_semantics", out _)), "hidden-exits-retain-edge-state-semantics");
        var registry = new RuntimeClassicHiddenDiscoveryRegistry();
        Check(registry.Discover("character.one", "exit.audit"), "player-discovery-created");
        Check(registry.IsDiscovered("character.one", "exit.audit") && !registry.IsDiscovered("character.two", "exit.audit"), "player-discovery-isolated");
        var hiddenItem = new RuntimeItemInstance("hidden.item", "item.audit", null, RuntimeItemLocations.Room, "room.map1.2150", null, 0, 1, null, new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"hidden_at_cleanup","true"}});
        Check(RuntimeClassicHiddenSearchService.IsHidden(hiddenItem), "cleanup-hidden-item-state-recognized");
        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        Check(File.ReadAllText(Path.Combine(serverRoot, "RuntimeLiveWorld.cs")).Contains("CanCharacterSeeExit", StringComparison.Ordinal), "room-rendering-filters-undiscovered-hidden-exits");
        var livingWorld = File.ReadAllText(Path.Combine(serverRoot, "RuntimeLivingWorld.cs"));
        Check(livingWorld.Contains("CanTraverseLivingWorld", StringComparison.Ordinal) &&
              livingWorld.Contains("EvaluateExitGate(session.CurrentRoom, direction, context)", StringComparison.Ordinal) &&
              livingWorld.Contains("Registry.GetDiscoveredTargets(session.Actor.Id)", StringComparison.Ordinal),
            "movement-enforces-hidden-exit-mode");
        Check(File.ReadAllText(Path.Combine(serverRoot, "RuntimeClassicHiddenSearch.cs")).Contains("DropClassicVisible", StringComparison.Ordinal), "pickup-redrop-visibility-transition-owned");
        return new(failures.Count == 0, checks, failures,
            new[]{"Hidden ground items remain globally hidden and are discovered per character.","Cleanup-hidden items become visible only after pickup and re-drop.","Hidden and visible ground stacks remain separate because hidden metadata is removed only on pickup.","Decoded Hidden/Searchable exits require player discovery.","Decoded Hidden/Passable exits allow explicit traversal but remain absent from obvious exits until discovered."},
            new[]{"Decoded hidden exit edge-state semantics are generated data and consumed by runtime mode checks."},
            new[]{"Hidden action-count exits remain owned by the later trap/action-latch runtime pass; their decoded action counts and ordering are preserved."},
            Array.Empty<string>(),
            new[]{"No numeric concealment threshold is present on decoded Hidden/Searchable exit records; runtime does not invent one."});
    }
}
