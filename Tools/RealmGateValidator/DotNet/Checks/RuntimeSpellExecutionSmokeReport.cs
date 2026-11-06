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
    private sealed class FailingRollbackSink : IRuntimeSpellEventSink
    {
        private int _applies;
        public void Apply(RuntimeSpellExecutionEvent spellEvent)
        {
            if (++_applies == 2)
                throw new InvalidOperationException("SPELL_SMOKE_SECOND_APPLY");
        }
        public void Revert(RuntimeSpellExecutionEvent spellEvent) =>
            throw new InvalidOperationException("SPELL_SMOKE_UNABLE_TO_REVERT");
    }
    private sealed class FailingOperations : IRuntimeSpellOperationPolicy
    {
        public IReadOnlyList<RuntimeSpellExecutionEvent> ResolveOperations(RuntimeSpellDefinition spell,
            RuntimeSpellCastRequest request, RuntimeActiveSpellEffect effect)
            => throw new InvalidOperationException("SPELL_SMOKE_PLAN_FAILURE");
    }
    private sealed class FailingSecondEventSink : IRuntimeSpellEventSink
    {
        private int _attempts;
        public readonly List<RuntimeSpellExecutionEvent> Applied = new();
        public void Apply(RuntimeSpellExecutionEvent spellEvent)
        {
            if (++_attempts == 2) throw new InvalidOperationException("SPELL_SMOKE_SECOND_EVENT_FAILURE");
            Applied.Add(spellEvent);
        }
        public void Revert(RuntimeSpellExecutionEvent spellEvent) => Applied.Remove(spellEvent);
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
        Check("operation-plan-failure-restores-cast-cost-and-status", () =>
        {
            var isolated = new RuntimeSpellEngine(catalog);
            isolated.Learn(caster, spell.Id);
            isolated.SetResources(caster, new RuntimeSpellResources(spell.ManaCost + 20, spell.EnergyCost + 20));
            var before = isolated.Snapshot();
            var failing = new RuntimeSpellExecutionEngine(catalog, isolated, new FailingOperations(), new Sink());
            var failedAsExpected = false;
            try { failing.Cast(new(caster, spell.Id, target, DateTimeOffset.UtcNow)); }
            catch (InvalidOperationException ex) when (ex.Message == "SPELL_SMOKE_PLAN_FAILURE")
            {
                failedAsExpected = true;
            }
            var after = isolated.Snapshot();
            if (!failedAsExpected || after.Resources[caster] != before.Resources[caster]
                || after.ActiveEffects.Count != before.ActiveEffects.Count
                || after.KnownSpells.Count != before.KnownSpells.Count)
                throw new InvalidOperationException("Failed operation planning consumed resources or retained a cast.");
        });
        Check("periodic-later-event-failure-reverts-earlier-event", () =>
        {
            var isolated = new RuntimeSpellEngine(catalog);
            isolated.Learn(caster, spell.Id);
            isolated.SetResources(caster, new RuntimeSpellResources(spell.ManaCost + 20, spell.EnergyCost + 20));
            var initialCast = isolated.Cast(new(caster, spell.Id, target, DateTimeOffset.UtcNow));
            if (!initialCast.Succeeded) throw new InvalidOperationException("Periodic fixture cast failed.");
            var before = isolated.Snapshot();
            var failingSink = new FailingSecondEventSink();
            var executor = new RuntimeSpellExecutionEngine(catalog, isolated, new Operations(), failingSink);
            var now = DateTimeOffset.UtcNow;
            var failedAsExpected = false;
            try
            {
                executor.Tick(now, effect =>
                [
                    new RuntimeSpellExecutionEvent("periodic.1", effect.SpellId, effect.CasterId,
                        effect.Target, "generated-effect", 1, now, effect.EffectId),
                    new RuntimeSpellExecutionEvent("periodic.2", effect.SpellId, effect.CasterId,
                        effect.Target, "generated-effect", 1, now, effect.EffectId)
                ]);
            }
            catch (InvalidOperationException ex) when (ex.Message == "SPELL_SMOKE_SECOND_EVENT_FAILURE")
            {
                failedAsExpected = true;
            }
            var after = isolated.Snapshot();
            if (!failedAsExpected || failingSink.Applied.Count != 0
                || after.Resources[caster] != before.Resources[caster]
                || !after.ActiveEffects.Select(effect => effect.EffectId)
                    .SequenceEqual(before.ActiveEffects.Select(effect => effect.EffectId)))
                throw new InvalidOperationException("Periodic event failure left a partial mutation or changed spell state.");
        });
        Check("failed-sink-rollback-is-explicitly-uncertain", () =>
        {
            var isolated = new RuntimeSpellEngine(catalog);
            isolated.Learn(caster, spell.Id);
            isolated.SetResources(caster, new RuntimeSpellResources(spell.ManaCost + 20, spell.EnergyCost + 20));
            var cast = isolated.Cast(new(caster, spell.Id, target, DateTimeOffset.UtcNow));
            if (!cast.Succeeded || cast.Effect is null)
                throw new InvalidOperationException("Rollback failure fixture cast failed.");
            var before = isolated.Snapshot();
            var executor = new RuntimeSpellExecutionEngine(catalog, isolated, new Operations(), new FailingRollbackSink());
            var now = DateTimeOffset.UtcNow;
            var observed = false;
            try
            {
                executor.Tick(now, effect =>
                [
                    new RuntimeSpellExecutionEvent("rollback.1", effect.SpellId, effect.CasterId,
                        effect.Target, "generated-effect", 1, now, effect.EffectId),
                    new RuntimeSpellExecutionEvent("rollback.2", effect.SpellId, effect.CasterId,
                        effect.Target, "generated-effect", 1, now, effect.EffectId)
                ]);
            }
            catch (AggregateException ex) when (
                ex.Message.Contains("SPELL_EXECUTION_ROLLBACK_UNCERTAIN", StringComparison.Ordinal))
            {
                observed = true;
            }
            // No active tick effects means no sink dispatch; fixture one active
            // spell so an attempted mutation and rollback are guaranteed.
            if (!observed) throw new InvalidOperationException("Uncertain rollback was not surfaced.");
        });
        Check("validator", () => { if (!engine.Validate().Passed) throw new InvalidOperationException("Spell engine invalid after execution."); });
        return new RuntimeSpellExecutionSmokeResult(failures.Count == 0, checks, failures);
    }
}
