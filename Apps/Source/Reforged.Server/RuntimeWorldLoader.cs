using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeWorldLoader
{
    public RuntimeWorld Load(string projectRoot, WorldRuntimeDefinition definition)
    {
        var roomsPath = Path.Combine(projectRoot, definition.DataRoot, definition.RoomsFile);
        if (!File.Exists(roomsPath))
        {
            throw new FileNotFoundException($"World rooms file was not found for world '{definition.Id}'.", roomsPath);
        }

        var rooms = new Dictionary<string, RuntimeRoom>(StringComparer.OrdinalIgnoreCase);
        var roomsByNumber = new Dictionary<int, RuntimeRoom>();
        using var document = JsonDocument.Parse(File.ReadAllText(roomsPath));
        var roomElements = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.EnumerateArray(),
            JsonValueKind.Object when document.RootElement.TryGetProperty("records", out var recordsElement) && recordsElement.ValueKind == JsonValueKind.Array => recordsElement.EnumerateArray(),
            _ => throw new InvalidOperationException($"World rooms file must contain a JSON array or generated records array: {roomsPath}")
        };

        foreach (var roomElement in roomElements)
        {
            var room = RuntimeRoom.FromJson(roomElement);
            if (string.IsNullOrWhiteSpace(room.Id))
            {
                continue;
            }

            rooms[room.Id] = room;
            if (room.RoomNumber > 0)
            {
                roomsByNumber[room.RoomNumber] = room;
            }
        }

        if (!rooms.ContainsKey(definition.StartRoom))
        {
            throw new InvalidOperationException($"Configured start room '{definition.StartRoom}' was not found for world '{definition.Id}'.");
        }

        var catalog = GeneratedWorldDataCatalog.Validate(projectRoot, definition);
        if (!catalog.Passed)
        {
            throw new InvalidOperationException($"Generated world data validation failed for world '{definition.Id}': {string.Join("; ", catalog.Diagnostics)}");
        }

        return new RuntimeWorld(definition, rooms, roomsByNumber, roomsPath, catalog);
    }
}

public sealed class RuntimeWorld
{
    private readonly IReadOnlyDictionary<string, RuntimeRoom> _rooms;
    private readonly IReadOnlyDictionary<int, RuntimeRoom> _roomsByNumber;

    public RuntimeWorld(WorldRuntimeDefinition definition, IReadOnlyDictionary<string, RuntimeRoom> rooms, IReadOnlyDictionary<int, RuntimeRoom> roomsByNumber, string evidencePath, GeneratedWorldDataCatalog dataCatalog)
    {
        Definition = definition;
        _rooms = rooms;
        _roomsByNumber = roomsByNumber;
        EvidencePath = evidencePath;
        DataCatalog = dataCatalog;
    }

    public WorldRuntimeDefinition Definition { get; }
    public string EvidencePath { get; }
    public GeneratedWorldDataCatalog DataCatalog { get; }
    public int RoomCount => _rooms.Count;

    public RuntimeRoom GetStartRoom()
    {
        return _rooms[Definition.StartRoom];
    }

    public RuntimeRoom? FindRoomById(string id)
    {
        return _rooms.TryGetValue(id, out var room) ? room : null;
    }

    public RuntimeRoom? FindRoomByNumber(int roomNumber)
    {
        return _roomsByNumber.TryGetValue(roomNumber, out var room) ? room : null;
    }
}
