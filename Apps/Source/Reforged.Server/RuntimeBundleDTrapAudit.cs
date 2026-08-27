using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDTrapAuditResult(bool Passed, int TrapCount, int DamageTrapCount, int SpellTrapCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDTrapAudit
{
    public static RuntimeBundleDTrapAuditResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name) { if (condition) checks.Add(name); else failures.Add(name); }

        var catalog = RuntimeClassicExitTrapCatalog.Load(projectRoot);
        var definitions = catalog.Definitions.ToArray();
        var damage = definitions.Where(x => x.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase)).ToArray();
        var spell = definitions.Where(x => x.TrapKind.Equals("spell_trap", StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(definitions.Length == 295, "decoded-trap-total-295");
        Check(damage.Length == 273, "decoded-damage-traps-273");
        Check(spell.Length == 22, "decoded-spell-traps-22");
        Check(damage.All(x => x.Damage > 0 && x.SpellId == 0), "damage-values-preserved");
        Check(spell.All(x => x.SpellId > 0 && x.Damage == 0), "spell-identifiers-preserved");
        Check(definitions.Select(x => x.ExitId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == definitions.Length, "one-trap-per-canonical-exit");
        Check(definitions.All(x => x.SourceExitRecord.Equals(x.ExitId, StringComparison.OrdinalIgnoreCase)), "source-exit-provenance-preserved");
        Check(definitions.All(x => !x.NativeActivationStateProjected && x.NativeActivationState is null), "mme-projection-does-not-invent-trap-para2-state");

        var options = ServerRuntimeOptions.Load(projectRoot);
        var definition = options.Worlds.First(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        Check(definitions.All(x => world.FindRoomById(x.FromRoomId) is not null && world.FindRoomById(x.ToRoomId) is not null), "trap-endpoints-resolve");
        Check(definitions.All(x => world.FindRoomById(x.FromRoomId)?.Exits.Any(e => e.Id.Equals(x.ExitId, StringComparison.OrdinalIgnoreCase)) == true), "trap-exits-owned-by-runtime-room-graph");

        var dispatcherPath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeCommandSystem.cs");
        var runtimePath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeClassicExitTraps.cs");
        Check(File.ReadAllText(dispatcherPath).Contains("ApplyExitTrap(session, previousRoom, command.Name)", StringComparison.Ordinal), "normal-movement-invokes-live-trap-consumer");
        var runtimeText = File.ReadAllText(runtimePath);
        Check(runtimeText.Contains("CompletePlayerDefeat(session, character)", StringComparison.Ordinal), "lethal-damage-uses-authoritative-death-consumer");
        Check(runtimeText.Contains("RuntimeSpellExecutionEngine", StringComparison.Ordinal), "spell-traps-retain-authoritative-spell-engine-consumer");
        Check(runtimeText.Contains("if (!trap.NativeActivationStateProjected) return string.Empty;", StringComparison.Ordinal), "unknown-native-trap-activation-fails-closed");
        Check(!runtimeText.Contains("DetectionDifficulty", StringComparison.Ordinal) && !runtimeText.Contains("DisarmDifficulty", StringComparison.Ordinal), "no-unsupported-detection-or-disarm-values-invented");
        Check(Directory.GetFiles(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server"), "RuntimeClassicExitTraps.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-live-exit-trap-consumer");

        return new RuntimeBundleDTrapAuditResult(failures.Count == 0, definitions.Length, damage.Length, spell.Length, checks, failures);
    }
}
