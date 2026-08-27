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
/// Ordinary generated room Delay remains minute-based. Room-entry admission is active
/// from retained Last Killed/regen evidence; the original wall-clock minute phase,
/// zero-delay edge semantics, restart ordering, and global reset interaction remain
/// explicit native dependencies rather than hidden guesses.
/// </summary>
public sealed class RuntimeMajorMudSpawnTimingCatalog
{
    public const string EvidenceRelativePath = "Librarium/Parity/majormud_spawn_timer_evidence_0277.json";

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
        var path = Path.Combine(Path.GetFullPath(projectRoot), EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return new RuntimeMajorMudSpawnTimingCatalog(
                new Dictionary<string, RuntimeMajorMudBossTimerEvidence>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, RuntimeMajorMudNpcTimerEvidence>(StringComparer.OrdinalIgnoreCase));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var roomTimers = new Dictionary<string, RuntimeMajorMudBossTimerEvidence>(StringComparer.OrdinalIgnoreCase);
        var nameTimers = new Dictionary<string, RuntimeMajorMudNpcTimerEvidence>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("room_boss_timers", out var roomArray) && roomArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in roomArray.EnumerateArray())
            {
                var mapId = ReadInt(item, "map_id");
                var roomNumber = ReadInt(item, "room_number");
                var hours = ReadInt(item, "hours");
                var name = ReadText(item, "name");
                var evidenceId = ReadText(item, "evidence_id");
                if (mapId <= 0 || roomNumber <= 0 || hours < 0 || string.IsNullOrWhiteSpace(name)) continue;
                roomTimers[RoomKey(mapId, roomNumber)] = new(name, mapId, roomNumber, hours, evidenceId);
            }
        }

        if (root.TryGetProperty("npc_name_timers", out var nameArray) && nameArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in nameArray.EnumerateArray())
            {
                var name = ReadText(item, "name");
                var hours = ReadInt(item, "hours");
                var evidenceId = ReadText(item, "evidence_id");
                if (string.IsNullOrWhiteSpace(name) || hours < 0) continue;
                nameTimers[name.Trim()] = new(name.Trim(), hours, evidenceId);
            }
        }

        return new RuntimeMajorMudSpawnTimingCatalog(roomTimers, nameTimers);
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
        if (roomDelay == 0)
        {
            return new RuntimeMajorMudSpawnTimingResolution(
                "generated-room-delay-zero",
                0,
                0,
                "minutes",
                "generated:spawns.json:spawn_model.respawn_minutes",
                "UNKNOWN",
                false,
                false,
                "Zero room Delay is version/evidence disputed. Room-entry admission is active, but whether zero means immediately eligible on the next entry remains UNKNOWN.");
        }

        return new RuntimeMajorMudSpawnTimingResolution(
            spawn.BossDefinition is null ? "generated-lair-delay-minutes" : "unverified-direct-room-delay-minutes",
            roomDelay,
            roomDelay,
            "minutes",
            "generated:spawns.json:spawn_model.respawn_minutes",
            "STRONG",
            true,
            false,
            spawn.BossDefinition is null
                ? "Room Delay is projected in minutes and due spawns are admitted on room entry; exact native minute-boundary phase remains unresolved."
                : "No fixed boss timer evidence matched this direct NPC. Generated room Delay is retained and due materialization is room-entry admitted without pretending the direct-NPC timer precedence is proven.");
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
            "STRONG",
            false,
            false,
            note + " Due materialization is admitted on room entry; restart ordering remains unresolved.");
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

        var amazon = Npc("amazon battle master", 1);
        var amazonTiming = catalog.Resolve(Spawn(16, 384, 5, amazon), amazon);
        Check(amazonTiming.EffectiveMinutes == 300 && amazonTiming.SourceUnit == "hours", "amazon-battle-master-5-hours");

        var healer = Npc("shadowy healer", 1);
        var healerTiming = catalog.Resolve(Spawn(1, 2325, 15, healer), healer);
        Check(healerTiming.EffectiveMinutes == 60 && healerTiming.SourceUnit == "hours", "shadowy-healer-1-hour");

        var titan = Npc("aged titan", 24);
        var titanTiming = catalog.Resolve(Spawn(10, 271, 0, titan), titan);
        Check(titanTiming.EffectiveMinutes == 1440 && titanTiming.SourceUnit == "hours", "aged-titan-24-hours");

        var mismatchedBoss = Npc("different direct npc", 1);
        var mismatchedTiming = catalog.Resolve(Spawn(16, 384, 7, mismatchedBoss), mismatchedBoss);
        Check(mismatchedTiming.EffectiveMinutes == 7 && mismatchedTiming.Mode == "unverified-direct-room-delay-minutes", "boss-room-timer-requires-name-match");

        var lairNpc = Npc("ordinary lair creature", 0, 0);
        var lairTiming = catalog.Resolve(Spawn(1, 999, 3), lairNpc);
        Check(lairTiming.EffectiveMinutes == 3 && lairTiming.NativeMinuteGatePending, "ordinary-lair-delay-kept-minutes");

        var zeroTiming = catalog.Resolve(Spawn(1, 998, 0), lairNpc);
        Check(zeroTiming.EffectiveMinutes == 0 && zeroTiming.Confidence == "UNKNOWN", "zero-delay-remains-evidence-gated");

        return new RuntimeMajorMudSpawnTimingSmokeReport { Checks = checks, Failures = failures };
    }
}
