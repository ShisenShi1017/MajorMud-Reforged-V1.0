using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeMajorMudBossTimerEvidence(
    string Name,
    int MapId,
    int RoomNumber,
    int Hours,
    string EvidenceId);

public sealed record RuntimeMajorMudNpcTimerEvidence(
    string Name,
    int Hours,
    string EvidenceId);

public sealed record RuntimeMajorMudSpawnTimingResolution(
    string Mode,
    int EffectiveMinutes,
    int SourceValue,
    string SourceUnit,
    string Authority,
    string Confidence,
    bool NativeMinuteGatePending,
    bool NativeRoomEntryAdmissionPending,
    string Note)
{
    public TimeSpan Delay => TimeSpan.FromMinutes(Math.Max(0, EffectiveMinutes));
}

/// <summary>
/// Evidence-gated MajorMUD spawn timing projection.
///
/// It intentionally does not invent one universal timer formula. Fixed boss room/name
/// timers are applied only where retained external observations identify an hour value.
/// Ordinary generated room Delay follows native _generate_monster exactly: Delay 0
/// bypasses the Last-Killed timer gate; positive Delay blocks while elapsed whole minutes
/// are <= Delay and becomes eligible only when elapsed > Delay. Native room entry and the
/// five-second monster-create background owner both call the same generation routine.
/// </summary>
public sealed class RuntimeMajorMudSpawnTimingCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeMajorMudBossTimerEvidence> _roomBossTimers;
    private readonly IReadOnlyDictionary<string, RuntimeMajorMudNpcTimerEvidence> _npcNameTimers;

    private RuntimeMajorMudSpawnTimingCatalog(
        IReadOnlyDictionary<string, RuntimeMajorMudBossTimerEvidence> roomBossTimers,
        IReadOnlyDictionary<string, RuntimeMajorMudNpcTimerEvidence> npcNameTimers)
    {
        _roomBossTimers = roomBossTimers;
        _npcNameTimers = npcNameTimers;
    }

    public int RoomBossTimerCount => _roomBossTimers.Count;
    public int NpcNameTimerCount => _npcNameTimers.Count;

    public static RuntimeMajorMudSpawnTimingCatalog Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return new RuntimeMajorMudSpawnTimingCatalog(
            new Dictionary<string, RuntimeMajorMudBossTimerEvidence>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, RuntimeMajorMudNpcTimerEvidence>(StringComparer.OrdinalIgnoreCase));
    }

    public RuntimeMajorMudSpawnTimingResolution Resolve(RuntimeNpcSpawn spawn, RuntimeNpcDefinition npc)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(npc);

        // A fixed map+room boss observation has the highest available timer authority.
        // Only apply it to a direct/BossDefinition materialization so a lair sharing the
        // room does not accidentally inherit an hours-long boss timer.
        if (spawn.BossDefinition is not null
            && _roomBossTimers.TryGetValue(RoomKey(spawn.SourceMapId, spawn.SourceRoomNumber), out var roomBoss)
            && roomBoss.Hours > 0
            && npc.Name.Trim().Equals(roomBoss.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BossHours(
                "observed-boss-room-hours",
                roomBoss.Hours,
                $"external:{roomBoss.EvidenceId}",
                $"Observed boss timer for {roomBoss.Name} at map {roomBoss.MapId} room {roomBoss.RoomNumber}.");
        }

        // Some named bosses/trainers are absent from the retained fixed-room table.
        if (spawn.BossDefinition is not null
            && _npcNameTimers.TryGetValue(npc.Name.Trim(), out var namedBoss)
            && namedBoss.Hours > 0)
        {
            return BossHours(
                "observed-boss-name-hours",
                namedBoss.Hours,
                $"external:{namedBoss.EvidenceId}",
                $"Observed named NPC timer for {namedBoss.Name}.");
        }

        var roomDelay = Math.Max(0, spawn.RespawnMinutes);
        return new RuntimeMajorMudSpawnTimingResolution(
            roomDelay == 0 ? "generated-room-delay-zero-immediate"
                : spawn.BossDefinition is null ? "generated-lair-delay-native-minutes" : "direct-room-delay-native-minutes",
            roomDelay,
            roomDelay,
            "minutes",
            "WCCMMUD.DLL:_generate_monster:room.Delay/LastKilled",
            "PASS",
            false,
            false,
            roomDelay == 0
                ? "Native room Delay 0 bypasses the Last-Killed minute gate and is immediately eligible when generation is attempted."
                : "Native generation blocks while elapsed whole minutes <= Delay and is eligible when elapsed > Delay; room entry and background monster-create both attempt admission.");
    }

    public static DateTimeOffset ResolveNativeRoomDelayEligibilityUtc(DateTimeOffset lastKilledUtc, int delayMinutes)
    {
        if (delayMinutes <= 0) return lastKilledUtc;
        // Native stores/compares integer minute values and admits only when elapsed > Delay.
        var minuteFloor = new DateTimeOffset(lastKilledUtc.Year, lastKilledUtc.Month, lastKilledUtc.Day,
            lastKilledUtc.Hour, lastKilledUtc.Minute, 0, lastKilledUtc.Offset);
        return minuteFloor.AddMinutes(checked(delayMinutes + 1));
    }

    public bool TryGetRoomBossTimer(int mapId, int roomNumber, out RuntimeMajorMudBossTimerEvidence timer) =>
        _roomBossTimers.TryGetValue(RoomKey(mapId, roomNumber), out timer!);

    public bool TryGetNamedNpcTimer(string name, out RuntimeMajorMudNpcTimerEvidence timer) =>
        _npcNameTimers.TryGetValue(name.Trim(), out timer!);

    private static RuntimeMajorMudSpawnTimingResolution BossHours(string mode, int hours, string authority, string note)
    {
        var minutes = checked(Math.Max(0, hours) * 60);
        return new RuntimeMajorMudSpawnTimingResolution(
            mode,
            minutes,
            Math.Max(0, hours),
            "hours",
            authority,
            "PASS",
            false,
            false,
            note + " This observed fixed-hour owner remains separate from ordinary Room.Delay minute gating.");
    }

    private static string RoomKey(int mapId, int roomNumber) => $"{mapId}:{roomNumber}";

    private static string ReadText(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : 0;
    }
}

public sealed class RuntimeMajorMudSpawnTimingSmokeReport
{
    public IReadOnlyList<string> Checks { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Failures { get; private set; } = Array.Empty<string>();
    public bool Passed => Failures.Count == 0;

    public static RuntimeMajorMudSpawnTimingSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var catalog = RuntimeMajorMudSpawnTimingCatalog.Load(projectRoot);

        static RuntimeNpcDefinition Npc(string name, int regen = 0, int gameLimit = 1) => new()
        {
            Id = "npc.smoke",
            Name = name,
            HitPoints = 100,
            RegenTime = regen,
            GameLimit = gameLimit
        };

        static RuntimeNpcSpawn Spawn(int mapId, int room, int minutes, RuntimeNpcDefinition? boss = null) => new()
        {
            Id = $"spawn.smoke.{mapId}.{room}",
            SourceMapId = mapId,
            SourceRoomNumber = room,
            RespawnMinutes = minutes,
            BossDefinition = boss
        };

        void Check(bool value, string name)
        {
            if (value) checks.Add(name); else failures.Add(name);
        }

        if (catalog.RoomBossTimerCount > 0 || catalog.NpcNameTimerCount > 0)
        {
            var amazon = Npc("amazon battle master", 1);
            var amazonTiming = catalog.Resolve(Spawn(16, 384, 5, amazon), amazon);
            Check(amazonTiming.EffectiveMinutes == 300 && amazonTiming.SourceUnit == "hours", "amazon-battle-master-5-hours");

            var healer = Npc("shadowy healer", 1);
            var healerTiming = catalog.Resolve(Spawn(1, 2325, 15, healer), healer);
            Check(healerTiming.EffectiveMinutes == 60 && healerTiming.SourceUnit == "hours", "shadowy-healer-1-hour");

            var titan = Npc("aged titan", 24);
            var titanTiming = catalog.Resolve(Spawn(10, 271, 0, titan), titan);
            Check(titanTiming.EffectiveMinutes == 1440 && titanTiming.SourceUnit == "hours", "aged-titan-24-hours");
        }
        else
        {
            Check(true, "optional-fixed-boss-timer-catalog-absent-fail-closed");
        }

        var mismatchedBoss = Npc("different direct npc", 1);
        var mismatchedTiming = catalog.Resolve(Spawn(16, 384, 7, mismatchedBoss), mismatchedBoss);
        Check(mismatchedTiming.EffectiveMinutes == 7 && mismatchedTiming.Mode == "direct-room-delay-native-minutes", "boss-room-timer-requires-name-match");

        var lairNpc = Npc("ordinary lair creature", 0, 0);
        var lairTiming = catalog.Resolve(Spawn(1, 999, 3), lairNpc);
        Check(lairTiming.EffectiveMinutes == 3 && !lairTiming.NativeMinuteGatePending && lairTiming.Confidence == "PASS", "ordinary-lair-delay-native-minutes");

        var zeroTiming = catalog.Resolve(Spawn(1, 998, 0), lairNpc);
        Check(zeroTiming.EffectiveMinutes == 0 && zeroTiming.Confidence == "PASS" && zeroTiming.Mode == "generated-room-delay-zero-immediate", "zero-delay-immediate-native");

        var minuteOrigin = new DateTimeOffset(2026, 8, 31, 12, 34, 47, TimeSpan.Zero);
        Check(RuntimeMajorMudSpawnTimingCatalog.ResolveNativeRoomDelayEligibilityUtc(minuteOrigin, 3) == new DateTimeOffset(2026, 8, 31, 12, 38, 0, TimeSpan.Zero), "native-delay-strict-minute-boundary");
        Check(RuntimeMajorMudSpawnTimingCatalog.ResolveNativeRoomDelayEligibilityUtc(minuteOrigin, 0) == minuteOrigin, "native-zero-delay-immediate-eligibility");

        return new RuntimeMajorMudSpawnTimingSmokeReport { Checks = checks, Failures = failures };
    }
}
