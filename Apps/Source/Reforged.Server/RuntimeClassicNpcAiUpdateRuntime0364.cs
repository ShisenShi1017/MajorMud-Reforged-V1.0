namespace Reforged.Server;

public sealed record RuntimeClassicNpcAiLanePumpResult(
    int HostCycles,
    int SlowSlotsVisited,
    int MediumSlotsVisited,
    int FastSlotsVisited,
    int HitPointRegenerations,
    int MediumMoveAttempts,
    int MediumMoves,
    bool SlowPending,
    bool MediumPending,
    bool FastPending);

/// <summary>
/// Executable adapter for the native WCC monster generation/cursor state machines.
///
/// WCC timer callbacks start at most one pending fast/medium/slow generation. The host idle
/// loop then calls slow-next, fast-next, medium-next in that order, one active-monster slot
/// per lane per host cycle. Reforged does not expose the Worldgroup idle callback itself, so
/// one world-simulation callback pumps enough internal host cycles to service the current
/// active-slot table while preserving the native per-slot cursor/order and pending cap.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    private bool _classicSlowNpcPending;
    private bool _classicMediumNpcPending;
    private bool _classicFastNpcPending;
    private int _classicMediumMoveAttempts;
    private readonly RuntimeClassicWorldgroupPrng _classicNpcAiRandom = new(unchecked((uint)Environment.TickCount64));

    private static readonly string[] NativeMonsterDirections = ["N", "S", "E", "W", "NE", "NW", "SE", "SW", "U", "D"];
    private static readonly HashSet<int> NativeRandomRoamExitTypes = [0, 2, 5, 7, 11, 19, 24];

    public RuntimeClassicNpcAiLanePumpResult PumpClassicNpcAiUpdateLanes(RuntimeClassicSchedulerStep schedulerStep, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(schedulerStep);
        lock (_gate)
        {
            if (!_world.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase))
                return new(0, 0, 0, 0, 0, 0, 0, false, false, false);
            if (schedulerStep.SlowDue) _classicSlowNpcPending = true; // native pending count capped at one
            if (schedulerStep.FastDue) _classicFastNpcPending = true;
            if (schedulerStep.MediumDue && !_classicMediumNpcPending)
            {
                _classicMediumNpcPending = true;
                _classicMediumMoveAttempts = 0; // native _medium_update_monsters resets this only when a new generation is admitted
            }

            var slotTable = _npcs.Values.OrderBy(ReadClassicActiveSlot).ThenBy(npc => npc.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
            if (slotTable.Length == 0)
            {
                _classicSlowNpcPending = _classicMediumNpcPending = _classicFastNpcPending = false;
                _classicSlowNpcCursor = _classicMediumNpcCursor = _classicFastNpcCursor = 0;
                return new(0, 0, 0, 0, 0, 0, 0, false, false, false);
            }

            var hostCycles = 0;
            var slowVisited = 0;
            var mediumVisited = 0;
            var fastVisited = 0;
            var regenerations = 0;
            var moveAttempts = 0;
            var moves = 0;
            var changed = false;

            // Drain one currently pending generation per lane. WCC would do the same over many
            // idle callbacks; the outer Reforged callback is coarser, so this is an adapter only
            // for host-cycle delivery, not a claim about Worldgroup wall-clock idle frequency.
            var maxCycles = slotTable.Length;
            while (hostCycles < maxCycles && (_classicSlowNpcPending || _classicFastNpcPending || _classicMediumNpcPending))
            {
                hostCycles++;
                if (_classicSlowNpcPending)
                {
                    slowVisited++;
                    var npc = slotTable[_classicSlowNpcCursor];
                    if (ApplyNativeSlowNpcSlotLocked(npc, nowUtc)) { regenerations++; changed = true; }
                    AdvanceCursor(ref _classicSlowNpcCursor, ref _classicSlowNpcPending, slotTable.Length);
                }

                if (_classicFastNpcPending)
                {
                    fastVisited++;
                    // Native fast behavior is a followed-user tracker. Static Classic spawns do
                    // not carry the active +0x1a followed-user runtime field. Reforged therefore
                    // advances the exact slot cursor but does not synthesize a follow target from
                    // combat threat, disposition, or perception metadata.
                    AdvanceCursor(ref _classicFastNpcCursor, ref _classicFastNpcPending, slotTable.Length);
                }

                if (_classicMediumNpcPending)
                {
                    mediumVisited++;
                    var npc = slotTable[_classicMediumNpcCursor];
                    var movement = ApplyNativeMediumNpcSlotLocked(npc, nowUtc);
                    moveAttempts += movement.Attempted ? 1 : 0;
                    moves += movement.Moved ? 1 : 0;
                    changed |= movement.Moved;
                    AdvanceCursor(ref _classicMediumNpcCursor, ref _classicMediumNpcPending, slotTable.Length);
                }
            }

            if (changed) Save();
            return new(hostCycles, slowVisited, mediumVisited, fastVisited, regenerations, moveAttempts, moves,
                _classicSlowNpcPending, _classicMediumNpcPending, _classicFastNpcPending);
        }
    }

    private bool ApplyNativeSlowNpcSlotLocked(RuntimeLivingNpcState npc, DateTimeOffset nowUtc)
    {
        if (!npc.Alive || npc.CurrentHitPoints >= npc.MaximumHitPoints) return false;
        if (!npc.Metadata.TryGetValue("hp_regen", out var raw) || !int.TryParse(raw, out var amount) || amount <= 0) return false;
        var next = Math.Min(npc.MaximumHitPoints, checked(npc.CurrentHitPoints + amount));
        if (next == npc.CurrentHitPoints) return false;
        _npcs[npc.InstanceId] = npc with { CurrentHitPoints = next };
        PublishLivingWorldEventLocked("npc-native-slow-regeneration", npc.InstanceId, npc.RoomId, nowUtc);
        return true;
    }

    private (bool Attempted, bool Moved) ApplyNativeMediumNpcSlotLocked(RuntimeLivingNpcState npc, DateTimeOffset nowUtc)
    {
        if (!npc.Alive) return (false, false);
        if (!npc.Metadata.TryGetValue("monster_type", out var typeRaw) || !int.TryParse(typeRaw, out var monsterType)) monsterType = 0;

        // Stock Classic contains only types 0..3, but the native jump table is fully recovered:
        // 0 and 2 hold position; 1, 3, 4 and >5 use probabilistic random roam; type 5
        // uses the unconditional roam branch.
        var unconditional = monsterType == 5;
        var probabilistic = monsterType is 1 or 3 or 4 || monsterType > 5;
        if (!unconditional && !probabilistic) return (false, false);
        if (_classicMediumMoveAttempts >= 3) return (false, false);

        var moveFlags = npc.Metadata.TryGetValue("classic_monster_move_flags", out var flagsRaw) && int.TryParse(flagsRaw, out var flags) ? flags : 0;
        if ((moveFlags & 0x3) != 0) return (false, false);

        if (probabilistic)
        {
            var follow = npc.Metadata.TryGetValue("follow_value", out var followRaw) && int.TryParse(followRaw, out var parsedFollow)
                ? parsedFollow : 0;
            var threshold = (100 - follow) / 2; // signed native truncation; stock values are 0..100
            if (_classicNpcAiRandom.Genrdn(0, 100) >= threshold) return (false, false);
        }

        if (ClassicMonsterConfusionFumblesLocked(npc, nowUtc)) return (false, false);
        _classicMediumMoveAttempts++;

        var room = _world.FindRoomById(npc.RoomId);
        if (room is null) return (true, false);
        var directionIndex = PickNativeRandomRoamDirectionLocked(room);
        if (directionIndex < 0) return (true, false);
        var priorDirection = npc.Metadata.TryGetValue("classic_last_direction_index", out var priorRaw) && int.TryParse(priorRaw, out var parsedPrior)
            ? parsedPrior : -1;
        if (directionIndex == priorDirection) return (true, false);

        var direction = NativeMonsterDirections[directionIndex];
        var exit = room.Exits.FirstOrDefault(candidate => candidate.Direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
        if (exit is null || exit.HasBlockedDestination || string.IsNullOrWhiteSpace(exit.TargetRoomId)) return (true, false);
        if (!CanTraverse(room, direction, out _)) return (true, false);

        var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["classic_last_direction_index"] = directionIndex.ToString(),
            ["classic_last_medium_move_at_utc"] = nowUtc.ToString("O")
        };
        _npcs[npc.InstanceId] = npc with { RoomId = exit.TargetRoomId, Metadata = metadata };
        PublishLivingWorldEventLocked("npc-native-medium-moved", npc.InstanceId, exit.TargetRoomId, nowUtc,
            new Dictionary<string,string> { ["direction"] = direction });
        return (true, true);
    }

    private bool ClassicMonsterConfusionFumblesLocked(RuntimeLivingNpcState npc, DateTimeOffset nowUtc)
    {
        var confusion = ResolveNpcTimedAbilityModifier(npc.InstanceId, 71, nowUtc);
        return confusion > 0 && _classicNpcAiRandom.Genrdn(0, 100) < confusion;
    }

    private int PickNativeRandomRoamDirectionLocked(RuntimeRoom room)
    {
        var selected = -1;
        for (var directionIndex = 0; directionIndex < NativeMonsterDirections.Length; directionIndex++)
        {
            var direction = NativeMonsterDirections[directionIndex];
            var exit = room.Exits.FirstOrDefault(candidate => candidate.Direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
            if (exit is null || exit.HasBlockedDestination || string.IsNullOrWhiteSpace(exit.TargetRoomId)) continue;
            var exitType = exit.ExitType ?? -1;
            if (!NativeRandomRoamExitTypes.Contains(exitType)) continue;
            if (selected < 0) selected = directionIndex;
            else if (_classicNpcAiRandom.Genrdn(0, 100) < 40) selected = directionIndex;
        }
        return selected;
    }

    private static void AdvanceCursor(ref int cursor, ref bool pending, int slotCount)
    {
        cursor++;
        if (cursor < slotCount) return;
        cursor = 0;
        pending = false;
    }
}
