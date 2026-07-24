using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeEnvironmentalHazardSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeEnvironmentalHazardSmokeReport Run()
    {
        var now = DateTimeOffset.UtcNow;
        var hazards = new[]
        {
            new RuntimeHazardDefinition("lava-1", "room-lava", RuntimeHazardKind.Lava, new HashSet<RuntimeHazardPhase>{ RuntimeHazardPhase.Enter, RuntimeHazardPhase.Periodic }, TimeSpan.FromSeconds(10), "fire-resistance", "lava-protection", "damage:lava", 8, true, true, RuntimeHazardPersistence.Ephemeral),
            new RuntimeHazardDefinition("dark-1", "room-dark", RuntimeHazardKind.Darkness, new HashSet<RuntimeHazardPhase>{ RuntimeHazardPhase.Enter }, TimeSpan.Zero, null, null, "visibility:darkness", 1, true, true, RuntimeHazardPersistence.Ephemeral)
        };
        var service = new RuntimeEnvironmentalHazardService(hazards);
        var checks = new List<string>(); var failures = new List<string>();
        var actor = new RuntimeHazardActorContext("char-1", false, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), now);
        Check(service.Enter("lava-1", actor).Requests.Count == 1, "entry-request");
        var protectedActor = actor with { ActorId = "char-2", EquipmentProtections = new HashSet<string>{ "lava-protection" } };
        Check(service.Enter("lava-1", protectedActor).Protected, "equipment-protection");
        var ticks = service.Tick(now.AddSeconds(11), id => id == "char-1" ? actor : protectedActor);
        Check(ticks.Count == 1 && ticks[0].ActorId == "char-1", "periodic-eligibility-and-protection");
        Check(service.Enter("dark-1", actor).Requests.Single().Kind == RuntimeHazardRequestKind.VisibilityModifier, "visibility-routing");
        Check(service.SetEnabled("lava-1", false, now).Code == "hazard-disabled", "disable");
        Check(service.ValidateState().Count == 0, "state-invariants");
        return new(failures.Count == 0, checks, failures);
        void Check(bool condition, string name) { checks.Add(name); if (!condition) failures.Add(name); }
    }
}
