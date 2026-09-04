using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeSpellExecutionSmokeResult(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeSpellExecutionSmokeReport
{
    private sealed class Operations : IRuntimeSpellOperationPolicy
    {
        public IReadOnlyList<RuntimeSpellExecutionEvent> ResolveOperations(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request, RuntimeActiveSpellEffect effect)
            => new[] { new RuntimeSpellExecutionEvent(Guid.NewGuid().ToString("N"), spell.Id, request.CasterId, request.Target, "generated-effect", spell.MinimumEffect, request.RequestedAtUtc, effect.EffectId) };
    }
    private sealed class Sink : IRuntimeSpellEventSink
    {
        public readonly List<RuntimeSpellExecutionEvent> Applied = new();
        public void Apply(RuntimeSpellExecutionEvent spellEvent) => Applied.Add(spellEvent);
        public void Revert(RuntimeSpellExecutionEvent spellEvent) => Applied.Remove(spellEvent);
    }
    private sealed class TargetPolicy : IRuntimeSpellTargetPolicy
    {
        public RuntimeSpellValidationIssue? ValidateTarget(RuntimeSpellDefinition spell, RuntimeSpellCastRequest request)
            => string.IsNullOrWhiteSpace(request.Target.Id) ? new("INVALID_TARGET", "Target id is required.") : null;
    }

    public static RuntimeSpellExecutionSmokeResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(string name, Action action) { try { action(); checks.Add(name); } catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); } }
        var catalog = RuntimeSpellCatalog.Load(projectRoot, worldId);
        var engine = new RuntimeSpellEngine(catalog);
        var spell = catalog.Spells.First(); var caster = "execution:caster"; var target = new RuntimeSpellTarget("generated", "execution:target");
        engine.Learn(caster, spell.Id); engine.SetResources(caster, new RuntimeSpellResources(spell.ManaCost + 20, spell.EnergyCost + 20));
        var sink = new Sink(); var execution = new RuntimeSpellExecutionEngine(catalog, engine, new Operations(), sink, new[] { new TargetPolicy() });
        RuntimeSpellExecutionResult? result = null;
        Check("target-policy", () => { var rejected = execution.Cast(new(caster, spell.Id, new RuntimeSpellTarget("generated", ""), DateTimeOffset.UtcNow)); if (rejected.Succeeded || rejected.FailureCode != "INVALID_TARGET") throw new InvalidOperationException("Invalid target was accepted."); });
        Check("execute-cast", () => { result = execution.Cast(new(caster, spell.Id, target, DateTimeOffset.UtcNow)); if (!result.Succeeded) throw new InvalidOperationException(result.FailureMessage); });
        Check("event-sink", () => { if (sink.Applied.Count != 1 || result!.Events.Count != 1) throw new InvalidOperationException("Resolved spell event was not applied exactly once."); });
        Check("dispel", () => { execution.Dispel(result!.Cast.Effect!.EffectId); if (engine.EffectsFor(target.Id).Count != 0) throw new InvalidOperationException("Effect was not dispelled."); });
        Check("validator", () => { if (!engine.Validate().Passed) throw new InvalidOperationException("Spell engine invalid after execution."); });
        return new RuntimeSpellExecutionSmokeResult(failures.Count == 0, checks, failures);
    }
}
