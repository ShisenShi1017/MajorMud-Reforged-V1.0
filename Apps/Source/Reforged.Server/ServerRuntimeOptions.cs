using System.Net;
using System.Text.Json;

namespace Reforged.Server;

/// <summary>
/// Runtime host options are intentionally data/config driven. Runtime Milestone 1 remains
/// Classic-first while allowing later Custom and AI worlds to be added by configuration and
/// services rather than hardcoded host rewrites.
/// </summary>
public sealed class ServerRuntimeOptions
{
    public string HostName { get; init; } = "Reforged Realms Host";
    public string DefaultWorldId { get; init; } = "classic";
    public int WorldTickMilliseconds { get; init; } = 1000;
    public NetworkRuntimeOptions Network { get; init; } = new();
    public IReadOnlyList<WorldRuntimeDefinition> Worlds { get; init; } = Array.Empty<WorldRuntimeDefinition>();

    public static ServerRuntimeOptions Load(string projectRoot)
    {
        var configPath = Path.Combine(projectRoot, "Config", "server.worlds.json");
        if (!File.Exists(configPath))
        {
            return CreateClassicFallback();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = document.RootElement;
            var worlds = new List<WorldRuntimeDefinition>();
            if (root.TryGetProperty("worlds", out var worldsElement) && worldsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var world in worldsElement.EnumerateArray())
                {
                    worlds.Add(new WorldRuntimeDefinition
                    {
                        Id = ReadString(world, "id", "classic"),
                        Name = ReadString(world, "name", "Classic World"),
                        Description = ReadString(world, "description", "Original MajorMUD-compatible Classic world."),
                        Type = ReadString(world, "type", "Classic"),
                        RulesProfile = ReadString(world, "rulesProfile", "classic"),
                        DataRoot = ReadString(world, "dataRoot", "Data"),
                        RoomsFile = ReadString(world, "roomsFile", "rooms.json"),
                        StartRoom = ReadString(world, "startRoom", "wcc_map1_room1"),
                        AiMode = ReadString(world, "aiMode", "Off"),
                        BrandingLogo = ReadString(world, "brandingLogo", string.Empty),
                        LoginBanner = ReadString(world, "loginBanner", string.Empty),
                        Motd = ReadString(world, "motd", "Welcome to MajorMUD Reforged Classic."),
                        RuntimeEnabled = ReadBoolean(world, "runtimeEnabled", true),
                        TransfersAllowed = ReadBoolean(world, "transfersAllowed", false),
                        ClassicProtected = ReadBoolean(world, "classicProtected", true)
                    });
                }
            }

            if (worlds.Count == 0)
            {
                worlds.Add(CreateClassicFallback().Worlds[0]);
            }

            return new ServerRuntimeOptions
            {
                HostName = ReadString(root, "hostName", "Reforged Realms Host"),
                DefaultWorldId = ReadString(root, "defaultWorldId", worlds[0].Id),
                WorldTickMilliseconds = ReadInt(root, "worldTickMilliseconds", 1000),
                Network = ReadNetwork(root),
                Worlds = worlds
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Config/server.worlds.json could not be parsed: {ex.Message}", ex);
        }
    }

    private static ServerRuntimeOptions CreateClassicFallback()
    {
        return new ServerRuntimeOptions
        {
            HostName = "Reforged Realms Host",
            DefaultWorldId = "classic",
            WorldTickMilliseconds = 1000,
            Network = new NetworkRuntimeOptions(),
            Worlds = new[]
            {
                new WorldRuntimeDefinition
                {
                    Id = "classic",
                    Name = "Classic World",
                    Description = "Original MajorMUD-compatible Classic world.",
                    Type = "Classic",
                    RulesProfile = "classic",
                    DataRoot = "Data",
                    RoomsFile = "rooms.json",
                    StartRoom = "wcc_map1_room1",
                    AiMode = "Off",
                    RuntimeEnabled = true,
                    TransfersAllowed = false,
                    ClassicProtected = true,
                    Motd = "Welcome to MajorMUD Reforged Classic."
                }
            }
        };
    }

    private static NetworkRuntimeOptions ReadNetwork(JsonElement root)
    {
        if (!root.TryGetProperty("network", out var network) || network.ValueKind != JsonValueKind.Object)
        {
            return new NetworkRuntimeOptions();
        }

        return new NetworkRuntimeOptions
        {
            Enabled = ReadBoolean(network, "enabled", false),
            BindAddress = ReadString(network, "bindAddress", "127.0.0.1"),
            TelnetPort = ReadInt(network, "telnetPort", 4000),
            MaxSessions = ReadInt(network, "maxSessions", 32),
            LoginTimeoutSeconds = ReadInt(network, "loginTimeoutSeconds", 120),
            AllowWorldSelection = ReadBoolean(network, "allowWorldSelection", true)
        };
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName, bool fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }

    private static int ReadInt(JsonElement element, string propertyName, int fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : fallback;
    }
}

public sealed class NetworkRuntimeOptions
{
    public bool Enabled { get; init; }
    public string BindAddress { get; init; } = "127.0.0.1";
    public int TelnetPort { get; init; } = 4000;
    public int MaxSessions { get; init; } = 32;
    public int LoginTimeoutSeconds { get; init; } = 120;
    public bool AllowWorldSelection { get; init; } = true;

    public IPAddress ResolveBindAddress()
    {
        if (IPAddress.TryParse(BindAddress, out var address))
        {
            return address;
        }

        return IPAddress.Loopback;
    }
}

public sealed class WorldRuntimeDefinition
{
    public string Id { get; init; } = "classic";
    public string Name { get; init; } = "Classic World";
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = "Classic";
    public string RulesProfile { get; init; } = "classic";
    public string DataRoot { get; init; } = "Data";
    public string RoomsFile { get; init; } = "rooms.json";
    public string StartRoom { get; init; } = "wcc_map1_room1";
    public string AiMode { get; init; } = "Off";
    public string BrandingLogo { get; init; } = string.Empty;
    public string LoginBanner { get; init; } = string.Empty;
    public string Motd { get; init; } = string.Empty;
    public bool RuntimeEnabled { get; init; } = true;
    public bool TransfersAllowed { get; init; }
    public bool ClassicProtected { get; init; } = true;
}
