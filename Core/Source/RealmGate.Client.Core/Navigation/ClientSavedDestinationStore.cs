using System.Collections.ObjectModel;
using System.Text.Json;

namespace RealmGate.Client.Navigation;

public enum ClientSavedDestinationKind
{
    Favorite,
    Stash
}

public sealed class ClientSavedDestination
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Saved location";
    public string WorldId { get; set; } = "Classic";
    public string RoomKey { get; set; } = string.Empty;
    public ClientSavedDestinationKind Kind { get; set; }
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string DisplayName => $"{(Kind == ClientSavedDestinationKind.Stash ? "Stash" : "Favorite")} - {Name} [{RoomKey}]";
}

public sealed class ClientSavedDestinationCatalog
{
    public int Version { get; set; } = 1;
    public ObservableCollection<ClientSavedDestination> Destinations { get; set; } = [];
}

public sealed class ClientSavedDestinationStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ClientSavedDestinationStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public static ClientSavedDestinationStore CreateForCurrentUser(bool publicRelease = false)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = publicRelease
            ? System.IO.Path.Combine(local, "RealmGate", "Public")
            : System.IO.Path.Combine(local, "RealmGate");
        return new(System.IO.Path.Combine(root, "Client", "Navigation", "saved-destinations.json"));
    }

    public async Task<ClientSavedDestinationCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path)) return new();
        try
        {
            var catalog = JsonSerializer.Deserialize<ClientSavedDestinationCatalog>(
                              await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false),
                              _json)
                          ?? new ClientSavedDestinationCatalog();
            Normalize(catalog);
            return catalog;
        }
        catch (JsonException)
        {
            BackupCorrupt();
            return new();
        }
        catch (IOException)
        {
            return new();
        }
        catch (UnauthorizedAccessException)
        {
            return new();
        }
    }

    public async Task SaveAsync(ClientSavedDestinationCatalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Normalize(catalog);
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temporary = Path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(catalog, _json), cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public static void Normalize(ClientSavedDestinationCatalog catalog)
    {
        catalog.Version = 1;
        catalog.Destinations ??= [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var destination in catalog.Destinations)
        {
            if (string.IsNullOrWhiteSpace(destination.Id) || !ids.Add(destination.Id))
            {
                do destination.Id = Guid.NewGuid().ToString("N");
                while (!ids.Add(destination.Id));
            }
            destination.Name = string.IsNullOrWhiteSpace(destination.Name)
                ? "Saved location"
                : destination.Name.Trim();
            destination.WorldId = NormalizeWorldId(destination.WorldId);
            destination.RoomKey = ClientRouteGraph.NormalizeRoomKey(destination.RoomKey);
        }
    }

    private static string NormalizeWorldId(string? value)
    {
        var text = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        return text.Length == 0 ? "Classic" : text;
    }

    private void BackupCorrupt()
    {
        try
        {
            File.Move(Path, Path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}", overwrite: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
