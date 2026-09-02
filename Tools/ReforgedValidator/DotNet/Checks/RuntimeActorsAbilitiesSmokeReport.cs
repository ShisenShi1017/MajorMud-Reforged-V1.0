using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeActorsAbilitiesSmokeReport
{
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public int AbilityDefinitionCount { get; private set; }
    public bool Passed => Failures.Count == 0;
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeActorsAbilitiesSmokeReport Run(string projectRoot, string worldId)
    {
        var report = new RuntimeActorsAbilitiesSmokeReport();
        var catalog = RuntimeAbilityCatalog.Load(projectRoot, worldId);
        report.AbilityDefinitionCount = catalog.Count;
        Check(report, "ability-catalog", catalog.Count > 0);
        var ability = catalog.Definitions.First();
        var actor = new RuntimeActorAbilityState("character.1");
        Check(report, "learn", actor.Learn(ability.Id, DateTimeOffset.UtcNow));
        Check(report, "duplicate-prevention", !actor.Learn(ability.Id, DateTimeOffset.UtcNow));
        var sink = new Sink();
        var engine = new RuntimeAbilityEngine(catalog, new Policy(), sink);
        var now = DateTimeOffset.UtcNow;
        var result = engine.Execute(actor, new("request.1", actor.ActorId, ability.Id, "npc.instance.1"), now);
        Check(report, "execute", result.Succeeded && sink.Events.Count == 1);
        Check(report, "cooldown", !actor.IsReady(ability.Id, now));
        var saved = actor.Capture();
        actor.Forget(ability.Id);
        actor.Restore(saved);
        Check(report, "ability-persistence", actor.Knows(ability.Id));
        Check(report, "ability-validator", actor.Validate(catalog).Count == 0);

        var definition = new RuntimeNpcDefinition { Id = "npc.test", LegacyId = 1, Name = "Test NPC", HitPoints = 5, Energy = 3 };
        var npc = new RuntimeNpcInstance("npc.instance.1", definition, "room.1");
        var registry = new RuntimeNpcRegistry();
        Check(report, "npc-register", registry.Add(npc) && !registry.Add(npc));
        var lifecycle = new Lifecycle();
        npc.ApplyDamage(5, lifecycle, now);
        Check(report, "npc-death", npc.Capture().State == RuntimeNpcLifeState.WaitingToRespawn);
        Check(report, "npc-respawn", npc.Tick(lifecycle, now.AddMinutes(1)) && npc.Capture().CurrentHitPoints == 5);
        var npcSaved = npc.Capture();
        npc.ApplyDamage(1, lifecycle, now.AddMinutes(2));
        npc.Restore(npcSaved);
        Check(report, "npc-persistence", npc.Capture().CurrentHitPoints == npcSaved.CurrentHitPoints);
        Check(report, "npc-validator", registry.Validate().Count == 0);

        var failing = new Sink { Fail = true };
        var rollbackEngine = new RuntimeAbilityEngine(catalog, new Policy(), failing);
        var before = actor.Capture();
        try { rollbackEngine.Execute(actor, new("request.rollback", actor.ActorId, ability.Id, "npc.instance.1"), now.AddMinutes(2)); } catch { }
        Check(report, "ability-rollback", actor.Capture().Cooldowns.Count == before.Cooldowns.Count);
        Check(report, "trainer-policy-contract", typeof(IRuntimeTrainerPolicy).IsInterface);
        return report;
    }

    private static void Check(RuntimeActorsAbilitiesSmokeReport report, string name, bool passed) { report.Checks.Add(name); if (!passed) report.Failures.Add(name); }
    private sealed class Policy : IRuntimeAbilityPolicy
    {
        public RuntimeAbilityPlan Resolve(RuntimeAbilityDefinition definition, RuntimeActorAbilitySnapshot actor, RuntimeAbilityRequest request, DateTimeOffset nowUtc) =>
            new(true, null, nowUtc.AddMinutes(1), new[] { new RuntimeAbilityEvent(Guid.NewGuid().ToString("N"), "ability", actor.ActorId, definition.Id, request.TargetId, 0, nowUtc) });
    }
    private sealed class Sink : IRuntimeAbilityEventSink
    {
        public bool Fail { get; init; }
        public List<RuntimeAbilityEvent> Events { get; } = new();
        public void Apply(RuntimeAbilityEvent abilityEvent) { if (Fail) throw new InvalidOperationException("sink-failure"); Events.Add(abilityEvent); }
        public void Revert(RuntimeAbilityEvent abilityEvent) => Events.Remove(abilityEvent);
    }
    private sealed class Lifecycle : IRuntimeNpcLifecyclePolicy
    {
        public DateTimeOffset? ResolveRespawn(RuntimeNpcDefinition definition, RuntimeNpcInstanceSnapshot instance, DateTimeOffset nowUtc) => nowUtc.AddSeconds(1);
        public string? ResolveSpawnRoom(RuntimeNpcDefinition definition, RuntimeNpcInstanceSnapshot instance, DateTimeOffset nowUtc) => instance.RoomId;
    }
}
