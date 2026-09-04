using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeCurrencyBundle(long Copper, long Silver, long Gold, long Platinum, long Runic)
{
    public static RuntimeCurrencyBundle Zero { get; } = new(0, 0, 0, 0, 0);
    public bool IsEmpty => Copper == 0 && Silver == 0 && Gold == 0 && Platinum == 0 && Runic == 0;
    public RuntimeCurrencyBundle Add(RuntimeCurrencyBundle other) => new(
        checked(Copper + other.Copper), checked(Silver + other.Silver), checked(Gold + other.Gold),
        checked(Platinum + other.Platinum), checked(Runic + other.Runic));
    public void Validate(string label)
    {
        if (Copper < 0 || Silver < 0 || Gold < 0 || Platinum < 0 || Runic < 0)
            throw new InvalidOperationException($"{label} currency cannot be negative.");
    }
}

public sealed record RuntimeCurrencyRange(RuntimeCurrencyBundle Minimum, RuntimeCurrencyBundle Maximum)
{
    public static RuntimeCurrencyRange Zero { get; } = new(RuntimeCurrencyBundle.Zero, RuntimeCurrencyBundle.Zero);
    public void Validate()
    {
        Minimum.Validate("Minimum NPC ground");
        Maximum.Validate("Maximum NPC ground");
        if (Minimum.Copper > Maximum.Copper || Minimum.Silver > Maximum.Silver || Minimum.Gold > Maximum.Gold ||
            Minimum.Platinum > Maximum.Platinum || Minimum.Runic > Maximum.Runic)
            throw new InvalidOperationException("NPC ground currency minimum cannot exceed maximum.");
    }
}

public sealed record RuntimeNpcGroundDropEntry(string CanonicalItemId, int Quantity, int DropPercent, IReadOnlyDictionary<string,string>? Metadata = null);
public sealed record RuntimeNpcGroundDropRequest(
    string RequestId,
    string NpcInstanceId,
    string RoomId,
    string Authority,
    int Seed,
    RuntimeCurrencyRange CurrencyRange,
    IReadOnlyList<RuntimeNpcGroundDropEntry> Items);
public sealed record RuntimeNpcGroundDropOutcome(string CanonicalItemId, int DropPercent, int RollPercent, bool Awarded, int Quantity, IReadOnlyList<string> InstanceIds);
public sealed record RuntimeNpcGroundDropResult(
    bool Succeeded,
    string RequestId,
    string NpcInstanceId,
    string RoomId,
    RuntimeCurrencyBundle CurrencyAdded,
    IReadOnlyList<RuntimeNpcGroundDropOutcome> ItemOutcomes,
    string Reason);

public sealed record RuntimeRoomCurrencyState(RuntimeCurrencyBundle Amount, bool HiddenAtCleanup, DateTimeOffset? HiddenAtUtc);

/// <summary>
/// Classic NPC death materialization. NPC deaths never create corpses: selected items and
/// denomination balances are placed directly in the room. Currency is rolled from the monster's
/// decoded denomination ranges. Stackable item definitions merge with compatible room stacks.
/// </summary>
public sealed class RuntimeNpcGroundDropService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;
    private readonly RuntimeItemCatalog _catalog;
    private readonly Dictionary<string, RuntimeRoomCurrencyState> _roomCurrency = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeNpcGroundDropResult> _completed = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public RuntimeNpcGroundDropService(RuntimeLivingWorldService living, RuntimeItemEngine items, RuntimeItemCatalog catalog)
    {
        _living = living ?? throw new ArgumentNullException(nameof(living));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public RuntimeRoomCurrencyState InspectRoomCurrencyState(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new InvalidOperationException("Room identity is required.");
        lock (_gate) return _roomCurrency.GetValueOrDefault(roomId, new(RuntimeCurrencyBundle.Zero, false, null));
    }

    public RuntimeCurrencyBundle InspectRoomCurrency(string roomId) => InspectRoomCurrencyState(roomId).Amount;

    public RuntimeCurrencyBundle TakeRoomCurrency(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new InvalidOperationException("Room identity is required.");
        lock (_gate)
        {
            var state = _roomCurrency.GetValueOrDefault(roomId, new(RuntimeCurrencyBundle.Zero, false, null));
            if (state.HiddenAtCleanup) return RuntimeCurrencyBundle.Zero;
            _roomCurrency.Remove(roomId);
            return state.Amount;
        }
    }

    public int HideRoomCurrencyAtCleanup(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var changed = 0;
            foreach (var roomId in _roomCurrency.Keys.ToArray())
            {
                var state = _roomCurrency[roomId];
                if (state.Amount.IsEmpty || state.HiddenAtCleanup) continue;
                _roomCurrency[roomId] = state with { HiddenAtCleanup = true, HiddenAtUtc = nowUtc };
                changed++;
            }
            return changed;
        }
    }

    public RuntimeNpcGroundDropResult Admit(RuntimeNpcGroundDropRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (_completed.TryGetValue(request.RequestId, out var replay)) return replay with { Reason = "already-applied" };
            if (string.IsNullOrWhiteSpace(request.RequestId)) return Reject(request, "request-id-required");
            if (string.IsNullOrWhiteSpace(request.Authority)) return Reject(request, "authority-required");
            if (string.IsNullOrWhiteSpace(request.RoomId)) return Reject(request, "room-id-required");
            try { request.CurrencyRange.Validate(); }
            catch { return Reject(request, "currency-range-invalid"); }
            var npc = _living.InspectNpc(request.NpcInstanceId);
            if (npc is null) return Reject(request, "instance-not-found");
            if (npc.Alive) return Reject(request, "npc-is-still-alive");
            if (!npc.RoomId.Equals(request.RoomId, StringComparison.OrdinalIgnoreCase)) return Reject(request, "npc-room-mismatch");
            foreach (var entry in request.Items ?? Array.Empty<RuntimeNpcGroundDropEntry>())
            {
                if (entry.Quantity < 1) return Reject(request, "item-quantity-must-be-positive");
                if (entry.DropPercent is < 0 or > 100) return Reject(request, "item-drop-percent-out-of-range");
                try { _catalog.Require(entry.CanonicalItemId); }
                catch { return Reject(request, "canonical-item-not-found"); }
            }

            var currencyAdded = RollCurrency(request.RequestId, request.Seed, request.CurrencyRange);
            if (!currencyAdded.IsEmpty)
            {
                var current = _roomCurrency.GetValueOrDefault(request.RoomId, new(RuntimeCurrencyBundle.Zero, false, null));
                _roomCurrency[request.RoomId] = new(current.Amount.Add(currencyAdded), false, null);
            }

            var outcomes = new List<RuntimeNpcGroundDropOutcome>();
            var index = 0;
            foreach (var entry in (request.Items ?? Array.Empty<RuntimeNpcGroundDropEntry>()).OrderBy(x => x.CanonicalItemId, StringComparer.OrdinalIgnoreCase))
            {
                var roll = RollPercent(request.RequestId, request.Seed + index++, "item:" + entry.CanonicalItemId);
                var awarded = roll <= entry.DropPercent;
                var ids = new List<string>();
                if (awarded)
                {
                    var metadata = entry.Metadata is null
                        ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                        : entry.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                    metadata["npc_ground_drop_request_id"] = request.RequestId;
                    metadata["npc_ground_drop_authority"] = request.Authority;
                    metadata["npc_source_instance_id"] = npc.InstanceId;
                    metadata["hidden_at_cleanup"] = "false";
                    var created = _items.Create(entry.CanonicalItemId, RuntimeItemLocations.Room, request.RoomId, entry.Quantity, null, metadata);
                    ids.Add(created.InstanceId);
                }
                outcomes.Add(new(entry.CanonicalItemId, entry.DropPercent, roll, awarded, awarded ? entry.Quantity : 0, ids));
            }

            var result = new RuntimeNpcGroundDropResult(true, request.RequestId, npc.InstanceId, request.RoomId,
                currencyAdded, outcomes, "materialized-on-room-ground");
            _completed.Add(request.RequestId, result);
            return result;
        }
    }

    private static RuntimeCurrencyBundle RollCurrency(string requestId, int seed, RuntimeCurrencyRange range) => new(
        RollInclusive(requestId, seed, "currency:copper", range.Minimum.Copper, range.Maximum.Copper),
        RollInclusive(requestId, seed, "currency:silver", range.Minimum.Silver, range.Maximum.Silver),
        RollInclusive(requestId, seed, "currency:gold", range.Minimum.Gold, range.Maximum.Gold),
        RollInclusive(requestId, seed, "currency:platinum", range.Minimum.Platinum, range.Maximum.Platinum),
        RollInclusive(requestId, seed, "currency:runic", range.Minimum.Runic, range.Maximum.Runic));

    private static long RollInclusive(string requestId, int seed, string purpose, long minimum, long maximum)
    {
        if (minimum == maximum) return minimum;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{requestId}|{seed}|{purpose}"));
        var span = checked((ulong)(maximum - minimum) + 1UL);
        return checked(minimum + (long)(BitConverter.ToUInt64(bytes, 0) % span));
    }

    private static int RollPercent(string requestId, int seed, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{requestId}|{seed}|{purpose}"));
        return 1 + (int)(BitConverter.ToUInt32(bytes, 0) % 100);
    }

    private static RuntimeNpcGroundDropResult Reject(RuntimeNpcGroundDropRequest request, string reason) =>
        new(false, request.RequestId ?? string.Empty, request.NpcInstanceId ?? string.Empty, request.RoomId ?? string.Empty,
            RuntimeCurrencyBundle.Zero, Array.Empty<RuntimeNpcGroundDropOutcome>(), reason);
}

public sealed class RuntimeNpcGroundDropSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
