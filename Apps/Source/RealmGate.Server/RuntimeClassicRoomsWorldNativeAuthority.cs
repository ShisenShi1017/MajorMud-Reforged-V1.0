using System.Collections.Concurrent;
using System.Text.Json;

namespace RealmGate.Server;

/// <summary>
/// Direct RoomRec authority recovered from the stock WGSERV MajorMUD 1.11p
/// WCCMP002.dat and WCCMMUD.DLL.  This is deliberately separate from the MMUD-Explorer
/// display projection because the latter loses native Para fields needed by SEARCH,
/// PICKLOCK, BASH, trap reset, and hidden-exit state transitions.
/// </summary>
public sealed record RuntimeClassicNativeExitRecord(
    string ExitId,
    int FromMapNumber,
    int FromRoomNumber,
    string Direction,
    int ToMapNumber,
    int CanonicalToRoomNumber,
    string CanonicalGateKind,
    long NativeRecordOffset,
    int NativeDirectionIndex,
    int RoomExit,
    int RoomType,
    int Para1,
    int Para2,
    int Para3,
    int Para4);

public sealed class RuntimeClassicRoomsWorldNativeAuthority
{
    public const int ExpectedRecordCount = 3490;
    public const int ExpectedTrapCount = 295;
    public const int ExitTimerQuantumSeconds = 300;
    public const string RugExitId = "exit.map12.2248.d";

    private static readonly ConcurrentDictionary<string, RuntimeClassicRoomsWorldNativeAuthority> Cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, RuntimeClassicNativeExitRecord> _records;

    private RuntimeClassicRoomsWorldNativeAuthority(IReadOnlyDictionary<string, RuntimeClassicNativeExitRecord> records) => _records = records;

    public int Count => _records.Count;
    public IReadOnlyCollection<RuntimeClassicNativeExitRecord> Records => _records.Values.ToArray();

    public bool TryGet(string exitId, out RuntimeClassicNativeExitRecord record) =>
        _records.TryGetValue(exitId ?? string.Empty, out record!);

    public RuntimeClassicNativeExitRecord Require(string exitId) =>
        TryGet(exitId, out var record) ? record : throw new KeyNotFoundException($"No native exit authority row exists for {exitId}.");

    public static RuntimeClassicRoomsWorldNativeAuthority Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        return Cache.GetOrAdd(root, static key => LoadUncached(key));
    }

    private static RuntimeClassicRoomsWorldNativeAuthority LoadUncached(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "native_room_exits.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Classic native exit authority is required.", path);
        var recordsElement = RuntimeChunkedClassicCatalog.LoadRecords(path);

        var records = new Dictionary<string, RuntimeClassicNativeExitRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in recordsElement)
        {
            var record = new RuntimeClassicNativeExitRecord(
                JsonValueReader.ReadString(value, "exit_id", string.Empty),
                JsonValueReader.ReadInt(value, "from_map_number", 0),
                JsonValueReader.ReadInt(value, "from_room_number", 0),
                JsonValueReader.ReadString(value, "direction", string.Empty).ToUpperInvariant(),
                JsonValueReader.ReadInt(value, "to_map_number", 0),
                JsonValueReader.ReadInt(value, "canonical_to_room_number", 0),
                JsonValueReader.ReadString(value, "canonical_gate_kind", string.Empty),
                value.TryGetProperty("native_record_offset", out var offset) && offset.TryGetInt64(out var parsedOffset) ? parsedOffset : 0,
                JsonValueReader.ReadInt(value, "native_direction_index", -1),
                JsonValueReader.ReadInt(value, "room_exit", 0),
                JsonValueReader.ReadInt(value, "room_type", -1),
                JsonValueReader.ReadInt(value, "para1", 0),
                JsonValueReader.ReadInt(value, "para2", 0),
                JsonValueReader.ReadInt(value, "para3", 0),
                JsonValueReader.ReadInt(value, "para4", 0));
            if (string.IsNullOrWhiteSpace(record.ExitId)) throw new InvalidOperationException("Native exit authority row has no exit ID.");
            if (!records.TryAdd(record.ExitId, record)) throw new InvalidOperationException($"Duplicate native exit authority row: {record.ExitId}");
        }

        var authority = new RuntimeClassicRoomsWorldNativeAuthority(records);
        authority.Validate();
        return authority;
    }

    private void Validate()
    {
        if (_records.Count != ExpectedRecordCount)
            throw new InvalidOperationException($"Native exit authority expected {ExpectedRecordCount} rows but found {_records.Count}.");
        if (_records.Values.Count(record => record.RoomType is 9 or 24) != ExpectedTrapCount)
            throw new InvalidOperationException("Native exit authority trap count mismatch.");
        var rug = Require(RugExitId);
        if (rug.RoomType != 6 || rug.Para1 != 16 || rug.Para2 != 0 || rug.Para3 != 2687 || rug.Para4 != 2688)
            throw new InvalidOperationException("Native rug RoomRec does not match WGSERV evidence.");
    }

    public static int TimerSeconds(int count) => checked(ExitTimerQuantumSeconds * Math.Max(1, count));

    public static bool HiddenStateAllowsTraversal(int state) => (state & (1 | 4 | 8)) != 0;

    public static int ResetDoorState(RuntimeClassicNativeExitRecord record, int currentState)
    {
        // WCCMMUD.DLL background exit dispatcher:
        // type 2 always returns its signed Para2 state to 2; types 7/11 only return Para1
        // to locked state 2 when their signed Para2 field is non-positive.  Positive Para2
        // causes the native timer callback to leave the current door/gate state unchanged.
        return record.RoomType switch
        {
            2 => 2,
            7 or 11 when record.Para2 <= 0 => 2,
            7 or 11 => currentState,
            _ => currentState
        };
    }

    public static int ResetHiddenState(int currentState, int para2)
    {
        // WCCMMUD.DLL 0x0044F6C9: searched state bit 4 returns to 2.  Action-open bit 8
        // restores an authored action mask only when Para2 is non-zero; Para2=0 leaves state 8.
        if ((currentState & 4) != 0) return 2;
        if ((currentState & 8) == 0 || para2 == 0) return currentState;
        var actionCount = Math.Abs(para2);
        if (actionCount is < 1 or > 10) return currentState;
        return checked(16 * ((1 << actionCount) - 1));
    }
}
