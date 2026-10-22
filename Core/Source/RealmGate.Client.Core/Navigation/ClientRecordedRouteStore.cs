using System.Collections.ObjectModel;
using System.Text.Json;

namespace RealmGate.Client.Navigation;

public sealed class ClientRecordedRouteStep
{
    public string FromRoomKey { get; set; } = string.Empty;
    public string ToRoomKey { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}

public sealed class ClientRecordedRoute
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Recorded Route";
    public string WorldId { get; set; } = "Classic";
    public string StartRoomKey { get; set; } = string.Empty;
    public string DestinationRoomKey { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ObservableCollection<ClientRecordedRouteStep> Steps { get; set; } = [];
    public string DisplayName => $"{Name} ({Steps.Count} steps, {StartRoomKey} to {DestinationRoomKey})";
}

public sealed class ClientRecordedRouteCatalog
{
    public int Version { get; set; } = 1;
    public ObservableCollection<ClientRecordedRoute> Routes { get; set; } = [];
}

public sealed class ClientRecordedRouteStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ClientRecordedRouteStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public static ClientRecordedRouteStore CreateForCurrentUser(bool publicRelease = false)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = publicRelease
            ? System.IO.Path.Combine(local, "RealmGate", "Public")
            : System.IO.Path.Combine(local, "RealmGate");
        return new(System.IO.Path.Combine(root, "Client", "Navigation", "recorded-routes.json"));
    }

    public async Task<ClientRecordedRouteCatalog> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path)) return new();
        try
        {
            var catalog = JsonSerializer.Deserialize<ClientRecordedRouteCatalog>(
                              await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false),
                              _json)
                          ?? new ClientRecordedRouteCatalog();
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

    public async Task SaveAsync(
        ClientRecordedRouteCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Normalize(catalog);
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temporary = Path + ".tmp";
            await File.WriteAllTextAsync(
                    temporary,
                    JsonSerializer.Serialize(catalog, _json),
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public static void Normalize(ClientRecordedRouteCatalog catalog)
    {
        catalog.Version = 1;
        catalog.Routes ??= [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in catalog.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Id) || !ids.Add(route.Id))
            {
                do route.Id = Guid.NewGuid().ToString("N");
                while (!ids.Add(route.Id));
            }
            route.Name = string.IsNullOrWhiteSpace(route.Name) ? "Recorded Route" : route.Name.Trim();
            route.WorldId = NormalizeWorldId(route.WorldId);
            route.StartRoomKey = ClientRouteGraph.NormalizeRoomKey(route.StartRoomKey);
            route.DestinationRoomKey = ClientRouteGraph.NormalizeRoomKey(route.DestinationRoomKey);
            route.Steps ??= [];
            foreach (var step in route.Steps)
            {
                step.FromRoomKey = ClientRouteGraph.NormalizeRoomKey(step.FromRoomKey);
                step.ToRoomKey = ClientRouteGraph.NormalizeRoomKey(step.ToRoomKey);
                step.Command = step.Command?.Trim() ?? string.Empty;
            }
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
            File.Move(
                Path,
                Path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}",
                overwrite: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
