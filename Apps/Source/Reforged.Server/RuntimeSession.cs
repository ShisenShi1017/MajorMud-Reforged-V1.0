using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeSession
{
    public RuntimeSession(
        string id,
        RuntimeWorld world,
        RuntimeRoom initialRoom,
        IRuntimeSessionActor actor,
        string spawnSource)
    {
        Id = id;
        World = world;
        CurrentRoom = initialRoom;
        Actor = actor;
        SpawnSource = spawnSource;
        ConnectedAtUtc = DateTimeOffset.UtcNow;
        LastCommandAtUtc = ConnectedAtUtc;
    }

    public string Id { get; }
    public IRuntimeSessionActor Actor { get; private set; }
    public RuntimeWorld World { get; private set; }
    public RuntimeRoom CurrentRoom { get; private set; }
    public string SpawnSource { get; private set; }
    public DateTimeOffset ConnectedAtUtc { get; }
    public DateTimeOffset LastCommandAtUtc { get; private set; }
    private readonly ConcurrentQueue<string> _messages = new();
    private readonly ConcurrentQueue<string> _classicPendingCommands = new();
    private readonly object _classicPendingCommandGate = new();
    private readonly object _classicModalGate = new();
    private int _classicSessionState = RuntimeClassicGangModalAuthority.ActiveGameplayState;
    private string? _classicPendingGangDisbandName;
    private readonly List<RuntimeRoomKey> _classicMovementTrail = [];

    /// <summary>
    /// WCCMMUD's per-session 20-entry movement trail, newest room first. Native normal movement
    /// shifts the trail and writes the destination room; teleports/respawns use MoveToWorld and
    /// deliberately do not alter it.
    /// </summary>
    public IReadOnlyList<RuntimeRoomKey> ClassicMovementTrail
    {
        get { lock (_classicMovementTrail) return _classicMovementTrail.ToArray(); }
    }

    public void EnqueueMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message)) _messages.Enqueue(message);
    }

    public IReadOnlyList<string> DrainMessages()
    {
        var messages = new List<string>();
        while (_messages.TryDequeue(out var message)) messages.Add(message);
        return messages;
    }

    /// <summary>
    /// Reforged's synchronous dispatcher normally leaves this queue empty, but the native
    /// lifecycle owner explicitly destroys every delayed/pending command node on death/logoff.
    /// Any future native-style delayed command producer must register here so cleanup remains exact.
    /// </summary>
    public bool ClassicPageEnabled { get; private set; } = RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultPageEnabled;
    public bool ToggleClassicPageEnabled() => ClassicPageEnabled = !ClassicPageEnabled;
    public bool SetClassicPageEnabled(bool enabled) => ClassicPageEnabled = enabled;

    // WCCMMUD live-user byte +0x6BB. ADDDELAY performs an 8-bit ADD and the one-second
    // fast-character owner decrements it once per native fast callback. This state belongs to
    // the live session/user object and is not serialized into the character record.
    public byte ClassicCommandDelay { get; private set; }
    public void AddClassicCommandDelay(int amount)
    {
        if (amount is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(amount));
        ClassicCommandDelay = unchecked((byte)(ClassicCommandDelay + amount));
    }
    public bool TickClassicCommandDelay()
    {
        if (ClassicCommandDelay == 0) return false;
        ClassicCommandDelay--;
        return ClassicCommandDelay > 0;
    }

    public int ClassicPendingCommandCount
    {
        get { lock (_classicPendingCommandGate) return _classicPendingCommands.Count; }
    }

    /// <summary>
    /// Native _queue_pending_command keeps at most thirteen per-user command nodes. Adding the
    /// tenth queued command emits the slow-down warning; an attempted fourteenth command is
    /// ignored with the native typing-too-quickly message. The command text itself remains raw
    /// because the native node stores the pre-dispatch input buffer and reparses it when drained.
    /// </summary>
    public string QueueClassicPendingCommand(string command)
    {
        command ??= string.Empty;
        lock (_classicPendingCommandGate)
        {
            var before = _classicPendingCommands.Count;
            if (before >= 13) return "You are typing too quickly - command ignored";
            _classicPendingCommands.Enqueue(command);
            return before == 9 ? "Why don't you slow down for a few seconds?" : string.Empty;
        }
    }

    public bool TryDequeueClassicPendingCommand(out string command)
    {
        lock (_classicPendingCommandGate) return _classicPendingCommands.TryDequeue(out command!);
    }

    public int ClearClassicPendingCommands()
    {
        lock (_classicPendingCommandGate)
        {
            var count = 0;
            while (_classicPendingCommands.TryDequeue(out _)) count++;
            return count;
        }
    }

    public int ClassicSessionState
    {
        get { lock (_classicModalGate) return _classicSessionState; }
    }

    public bool HasClassicModalInput => ClassicSessionState != RuntimeClassicGangModalAuthority.ActiveGameplayState;

    public void BeginClassicGangDisbandConfirmation(string gangName)
    {
        lock (_classicModalGate)
        {
            _classicPendingGangDisbandName = gangName;
            _classicSessionState = RuntimeClassicGangModalAuthority.DisbandConfirmationState;
        }
    }

    public bool TryGetClassicGangDisbandConfirmation(out string gangName)
    {
        lock (_classicModalGate)
        {
            if (_classicSessionState != RuntimeClassicGangModalAuthority.DisbandConfirmationState
                || string.IsNullOrWhiteSpace(_classicPendingGangDisbandName))
            {
                gangName = string.Empty;
                return false;
            }
            gangName = _classicPendingGangDisbandName;
            return true;
        }
    }

    public void CompleteClassicModalInput()
    {
        lock (_classicModalGate)
        {
            _classicPendingGangDisbandName = null;
            _classicSessionState = RuntimeClassicGangModalAuthority.ActiveGameplayState;
        }
    }

    public bool ClearClassicModalState()
    {
        lock (_classicModalGate)
        {
            var cleared = _classicSessionState != RuntimeClassicGangModalAuthority.ActiveGameplayState
                || _classicPendingGangDisbandName is not null;
            _classicPendingGangDisbandName = null;
            _classicSessionState = RuntimeClassicGangModalAuthority.ActiveGameplayState;
            return cleared;
        }
    }

    public void Touch() => LastCommandAtUtc = DateTimeOffset.UtcNow;

    public void MoveToWorld(RuntimeWorld world, RuntimeRoom room, string source)
    {
        World = world;
        CurrentRoom = room;
        SpawnSource = source;
        Touch();
    }

    public void ReplaceCharacter(RuntimeWorld world, RuntimeRoom room, RuntimeCharacterActor actor, string source)
    {
        World = world;
        CurrentRoom = room;
        Actor = actor;
        SpawnSource = source;
        Touch();
    }

    public bool TryMove(string direction, out string message)
    {
        Touch();
        if (!World.TryResolveExit(CurrentRoom, direction, out _, out var target, out message) || target is null)
            return false;

        CurrentRoom = target;
        RecordClassicMovement(target);
        message = RoomRenderer.Render(World, CurrentRoom);
        return true;
    }

    private void RecordClassicMovement(RuntimeRoom room)
    {
        lock (_classicMovementTrail)
        {
            _classicMovementTrail.Insert(0, new RuntimeRoomKey(room.MapId, room.RoomNumber));
            if (_classicMovementTrail.Count > RuntimeClassicNativeCharacterDeathAuthority.MovementTrailLength)
                _classicMovementTrail.RemoveRange(RuntimeClassicNativeCharacterDeathAuthority.MovementTrailLength,
                    _classicMovementTrail.Count - RuntimeClassicNativeCharacterDeathAuthority.MovementTrailLength);
        }
    }

    private static string NormalizeDirection(string direction) => direction.Trim().ToUpperInvariant() switch
    {
        "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W",
        "NORTHEAST" => "NE", "NORTHWEST" => "NW", "SOUTHEAST" => "SE", "SOUTHWEST" => "SW",
        "UP" => "U", "DOWN" => "D", _ => direction.Trim().ToUpperInvariant()
    };
}

public static class RoomRenderer
{
    public static string Render(RuntimeRoom room) => Render(null, room);

    public static string Render(RuntimeWorld? world, RuntimeRoom room)
    {
        var exits = room.Exits.Count == 0 ? "none" : string.Join(", ", room.Exits.Select(exit => exit.Direction).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value));
        var occupants = world?.FindSpawnsByRoom(room.Id) ?? Array.Empty<RuntimeNpcSpawn>();
        var npcText = occupants.Count == 0
            ? string.Empty
            : $"\n\nYou see: {string.Join(", ", occupants.Select(spawn => spawn.DisplayName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}";
        return $"{room.Name}\n{room.Description}{npcText}\n\nExits: {exits}";
    }
}
