using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeSpellSmokeResult(bool Passed, int CatalogSpellCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeSpellSmokeReport
{
    public static RuntimeSpellSmokeResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, Action action)
        {
            try { action(); checks.Add(name); }
            catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
        }
        var catalog = RuntimeSpellCatalog.Load(projectRoot, worldId);
        var engine = new RuntimeSpellEngine(catalog);
        var spell = catalog.Spells.First();
        var caster = "smoke:caster";
        var target = new RuntimeSpellTarget("smoke", "smoke:target");
        RuntimeSpellCastResult? result = null;
        Check("catalog-load", () => { if (catalog.Spells.Count == 0) throw new InvalidOperationException("No generated spells loaded."); });
        Check("learn", () => engine.Learn(caster, spell.Id));
        Check("duplicate-learn-prevention", () =>
        {
            try { engine.Learn(caster, spell.Id); throw new InvalidOperationException("Duplicate spell accepted."); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already knows", StringComparison.OrdinalIgnoreCase)) { }
        });
        Check("resources", () => engine.SetResources(caster, new RuntimeSpellResources(spell.ManaCost + 100, spell.EnergyCost + 100)));
        Check("cast", () =>
        {
            result = engine.Cast(new RuntimeSpellCastRequest(caster, spell.Id, target, DateTimeOffset.UtcNow));
            if (!result.Succeeded || result.Effect is null) throw new InvalidOperationException(result.FailureMessage ?? "Cast failed.");
        });
        Check("generated-cost-deduction", () =>
        {
            if (result!.Resources.Mana != 100 || result.Resources.Energy != 100) throw new InvalidOperationException("Generated costs were not deducted atomically.");
        });
        Check("active-effect", () =>
        {
            var effect = engine.EffectsFor(target.Id).Single();
            if (effect.MinimumEffect != spell.MinimumEffect || effect.MaximumEffect != spell.MaximumEffect || effect.Duration != spell.Duration)
                throw new InvalidOperationException("Generated effect metadata mismatch.");
        });
        Check("unknown-spell-prevention", () =>
        {
            try { engine.Cast(new RuntimeSpellCastRequest(caster, "spell.missing", target, DateTimeOffset.UtcNow)); throw new InvalidOperationException("Unknown spell accepted."); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Unknown generated spell", StringComparison.OrdinalIgnoreCase)) { }
        });
        Check("save-load", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "ReforgedSpellSmoke", Guid.NewGuid().ToString("N"));
            var repository = new RuntimeSpellStateRepository(path, worldId);
            repository.Save(engine.Snapshot());
            var restored = new RuntimeSpellEngine(catalog, initial: repository.Load());
            if (!restored.Validate().Passed || restored.Spellbook(caster).Count != 1 || restored.EffectsFor(target.Id).Count != 1)
                throw new InvalidOperationException("Spell state persistence mismatch.");
        });
        Check("forget", () => engine.Forget(caster, spell.Id));
        Check("validator", () => { var report = engine.Validate(); if (!report.Passed) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(x => x.Message))); });
        return new RuntimeSpellSmokeResult(failures.Count == 0, catalog.Spells.Count, checks, failures);
    }
}
