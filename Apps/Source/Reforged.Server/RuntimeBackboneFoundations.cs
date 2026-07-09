namespace Reforged.Server;

public sealed class SpawnSchedulingFoundation
{
    private long _ticks;

    public Task TickAsync(RuntimeEventDispatcher events, IReadOnlyDictionary<string, RuntimeWorld> worlds, CancellationToken cancellationToken)
    {
        var tick = Interlocked.Increment(ref _ticks);
        foreach (var world in worlds.Values)
        {
            events.Publish("spawn", "scheduler-foundation-tick", world.Definition.Id, null, null, $"tick={tick}; execution=placeholder; evidence-gated=true");
        }

        return Task.CompletedTask;
    }
}

public sealed class RuntimeEventSchedulingFoundation
{
    private long _ticks;

    public Task TickAsync(RuntimeEventDispatcher events, IReadOnlyDictionary<string, RuntimeWorld> worlds, CancellationToken cancellationToken)
    {
        var tick = Interlocked.Increment(ref _ticks);
        foreach (var world in worlds.Values)
        {
            events.Publish("event-scheduler", "foundation-tick", world.Definition.Id, null, null, $"tick={tick}; dispatch-ready=true; gameplay-effects=disabled");
        }

        return Task.CompletedTask;
    }
}

public sealed class NpcLifecycleFoundation
{
    public string RenderStatus() => "NPC lifecycle foundation registered; spawn/despawn and AI behavior remain evidence-gated placeholders.";

    public Task TickAsync(RuntimeEventDispatcher events, IReadOnlyDictionary<string, RuntimeWorld> worlds, RuntimeEntityRegistry entities, CancellationToken cancellationToken)
    {
        foreach (var world in worlds.Values)
        {
            events.Publish("npc", "lifecycle-foundation-tick", world.Definition.Id, null, null, $"entities={entities.Entities.Count(item => item.WorldId == world.Definition.Id && item.EntityType == "npc")}; ai=disabled");
        }

        return Task.CompletedTask;
    }
}

public sealed class CombatLoopFoundation
{
    public string RenderStatus() => "Combat loop foundation registered; no attacks, damage, initiative, or formula execution implemented in this milestone.";

    public Task TickAsync(RuntimeEventDispatcher events, IReadOnlyDictionary<string, RuntimeWorld> worlds, CancellationToken cancellationToken)
    {
        foreach (var world in worlds.Values)
        {
            events.Publish("combat", "loop-foundation-tick", world.Definition.Id, null, null, "combat execution disabled until combat runtime milestone");
        }

        return Task.CompletedTask;
    }
}

public sealed class MagicCastPipelineFoundation
{
    public string RenderStatus() => "Magic/cast pipeline foundation registered; spell effects and formulas remain disabled until magic runtime milestone.";

    public RuntimeCastRequest CreateRejectedRequest(RuntimeSession session, string rawInput)
    {
        return new RuntimeCastRequest(
            Guid.NewGuid().ToString("N"),
            session.World.Definition.Id,
            session.Id,
            session.CharacterId,
            rawInput,
            RuntimeCastRequestStatus.RejectedUnsupported,
            DateTimeOffset.UtcNow);
    }
}

public sealed record RuntimeCastRequest(
    string RequestId,
    string WorldId,
    string SessionId,
    string CasterEntityId,
    string RawInput,
    RuntimeCastRequestStatus Status,
    DateTimeOffset CreatedAtUtc);

public enum RuntimeCastRequestStatus
{
    Queued,
    RejectedUnsupported,
    Resolved
}
