using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicGnomeMerchantRoom(string RoomRef, int MapId, int RoomNumber, string Name);

public sealed class RuntimeClassicGnomeMerchantNarrativeAuthority
{
    public const int NpcLegacyId = 716;
    public const int RootTextblockId = 2798;
    private readonly HashSet<string> _eligibleRooms;
    public IReadOnlyList<RuntimeClassicGnomeMerchantRoom> Rooms { get; }

    private RuntimeClassicGnomeMerchantNarrativeAuthority(IReadOnlyList<RuntimeClassicGnomeMerchantRoom> rooms)
    {
        Rooms = rooms;
        _eligibleRooms = rooms.Select(x => x.RoomRef).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static RuntimeClassicGnomeMerchantNarrativeAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "gnome_merchant_narrative_placement.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Gnome Merchant placement catalog is required.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.GetProperty("npc_legacy_id").GetInt32() != NpcLegacyId || root.GetProperty("root_textblock_id").GetInt32() != RootTextblockId)
            throw new InvalidDataException("Gnome Merchant identity or textblock root mismatch.");
        var rooms = root.GetProperty("rooms").EnumerateArray().Select(x => new RuntimeClassicGnomeMerchantRoom(
            x.GetProperty("room_ref").GetString() ?? string.Empty,
            x.GetProperty("map_id").GetInt32(),
            x.GetProperty("room_number").GetInt32(),
            x.GetProperty("name").GetString() ?? string.Empty)).ToArray();
        if (rooms.Length == 0 || rooms.Any(x => x.MapId != 16 || !x.Name.StartsWith("Shallow Jungle,", StringComparison.Ordinal)))
            throw new InvalidDataException("Gnome Merchant placement must resolve to decoded Shallow Jungle rooms.");
        if (rooms.Select(x => x.RoomRef).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rooms.Length)
            throw new InvalidDataException("Duplicate Gnome Merchant room reference.");
        return new(rooms);
    }

    public bool CanActivate(int npcLegacyId, string npcCurrentRoomRef, string playerCurrentRoomRef) =>
        npcLegacyId == NpcLegacyId &&
        npcCurrentRoomRef.Equals(playerCurrentRoomRef, StringComparison.OrdinalIgnoreCase) &&
        _eligibleRooms.Contains(npcCurrentRoomRef);
}

public sealed record RuntimeBundleFPhaseF7GnomeMerchantAuditReport(bool Passed, int RoomCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF7GnomeMerchantAudit
{
    public static RuntimeBundleFPhaseF7GnomeMerchantAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var authority = RuntimeClassicGnomeMerchantNarrativeAuthority.Load(projectRoot);
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        Check(graph.Require(RuntimeClassicGnomeMerchantNarrativeAuthority.RootTextblockId).TextblockId == 2798, "textblock-2798-authoritative-root");
        Check(authority.Rooms.Count == 171, "all-decoded-shallow-jungle-rooms-projected");
        Check(authority.Rooms.All(x => x.MapId == 16 && x.Name.StartsWith("Shallow Jungle,", StringComparison.Ordinal)), "only-decoded-shallow-jungle-rooms");
        var sample = authority.Rooms[0];
        Check(authority.CanActivate(716, sample.RoomRef, sample.RoomRef), "activation-requires-live-npc-and-player-colocation");
        Check(!authority.CanActivate(716, sample.RoomRef, "room.map16.1"), "reject-player-in-different-room");
        Check(!authority.CanActivate(715, sample.RoomRef, sample.RoomRef), "reject-wrong-npc");
        Check(!authority.CanActivate(716, "room.map16.1", "room.map16.1"), "reject-non-shallow-jungle-room");
        return new(failures.Count == 0, authority.Rooms.Count, failures.Count, checks, failures);
    }
}
