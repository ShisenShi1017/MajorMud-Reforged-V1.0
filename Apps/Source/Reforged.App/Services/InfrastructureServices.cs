using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reforged.App.Models;

namespace Reforged.App.Services;

public sealed class AppLogger : IAppLogger
{
    private readonly List<string> _recent = [];
    private readonly string _path;

    public AppLogger(string root)
    {
        var logsDirectory = Path.Combine(root, "Logs");
        Directory.CreateDirectory(logsDirectory);
        _path = Path.Combine(logsDirectory, "application.log");
    }

    public IReadOnlyList<string> Recent => _recent;

    public void Info(string message) => Write("INF", message, null);

    public void Error(string message, Exception? exception = null) =>
        Write("ERR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line =
            $"{DateTimeOffset.Now:O} [{level}] {message}" +
            (exception is null ? string.Empty : Environment.NewLine + exception);

        lock (_recent)
        {
            _recent.Add(line);
            if (_recent.Count > 100)
            {
                _recent.RemoveAt(0);
            }

            try
            {
                using var stream = new FileStream(
                    _path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(line);
            }
            catch (IOException)
            {
                // Logging must never be the reason the desktop app fails to open.
            }
            catch (UnauthorizedAccessException)
            {
                // The app can still run even if the local log file is temporarily unavailable.
            }
        }
    }
}

public sealed class ErrorReporter(IAppLogger logger) : IErrorReporter
{
    public void Report(Exception exception, string context) =>
        logger.Error(context, exception);
}

public sealed class ApplicationSettingsService : IApplicationSettingsService
{
    private readonly JsonSerializerOptions _json = CreateJsonOptions();
    private readonly IReadOnlyList<string> _legacySettingsPaths;

    public ApplicationSettingsService(string? rootPath = null)
    {
        if (rootPath is not null)
        {
            RootPath = rootPath;
            _legacySettingsPaths = [];
        }
        else
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            RootPath = Path.Combine(local, "RealmGate");
            var legacyRoots = new[]
            {
                Path.Combine(local, "MajorMUD Reforged"),
                Path.Combine(local, "MajorMudReforged"),
            };
            _legacySettingsPaths = legacyRoots
                .Select(path => Path.Combine(path, "Settings", "application.json"))
                .ToArray();
            TryMigrateLegacyRoots(legacyRoots, RootPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
    }

    public string RootPath { get; }

    public ApplicationSettings Current { get; private set; } = new();

    private string SettingsPath =>
        Path.Combine(RootPath, "Settings", "application.json");

    public async Task LoadAsync()
    {
        var sourcePath = File.Exists(SettingsPath)
            ? SettingsPath
            : _legacySettingsPaths.FirstOrDefault(File.Exists);

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        Current = JsonSerializer.Deserialize<ApplicationSettings>(
            await File.ReadAllTextAsync(sourcePath),
            _json) ?? new ApplicationSettings();

        if (!string.Equals(sourcePath, SettingsPath, StringComparison.OrdinalIgnoreCase))
        {
            await SaveAsync();
        }
    }

    private static void TryMigrateLegacyRoots(IEnumerable<string> legacyRoots, string destinationRoot)
    {
        foreach (var legacyRoot in legacyRoots)
        {
            if (!Directory.Exists(legacyRoot))
            {
                continue;
            }

            try
            {
                CopyMissingDirectoryTree(legacyRoot, destinationRoot);
            }
            catch (IOException)
            {
                // Settings.LoadAsync still falls back to the legacy settings file.
            }
            catch (UnauthorizedAccessException)
            {
                // Do not prevent RealmGate startup when legacy data cannot be copied.
            }
        }
    }

    private static void CopyMissingDirectoryTree(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (!File.Exists(destination))
            {
                File.Copy(file, destination, overwrite: false);
            }
        }
    }

    public Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        if (!double.IsFinite(Current.FontSize) || Current.FontSize <= 0)
        {
            Current.FontSize = 14;
        }

        return File.WriteAllTextAsync(
            SettingsPath,
            JsonSerializer.Serialize(Current, _json));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        options.Converters.Add(new FiniteDoubleJsonConverter());
        options.Converters.Add(new NullableFiniteDoubleJsonConverter());
        return options;
    }
}

public sealed class LayoutPersistenceService(
    IApplicationSettingsService settings) : ILayoutPersistenceService
{
    private readonly JsonSerializerOptions _json = CreateJsonOptions();

    private string LayoutPath =>
        Path.Combine(settings.RootPath, "Layouts", "shell.json");

    public async Task<LayoutState> LoadAsync()
    {
        if (!File.Exists(LayoutPath))
        {
            return new LayoutState();
        }

        try
        {
            return JsonSerializer.Deserialize<LayoutState>(
                await File.ReadAllTextAsync(LayoutPath),
                _json) ?? new LayoutState();
        }
        catch (JsonException)
        {
            return new LayoutState();
        }
        catch (IOException)
        {
            return new LayoutState();
        }
    }

    public async Task SaveAsync(LayoutState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);

        var safeState = new LayoutState
        {
            ActiveWorkspaceId = string.IsNullOrWhiteSpace(state.ActiveWorkspaceId)
                ? "home"
                : state.ActiveWorkspaceId,
            Width = FiniteOrDefault(state.Width, 1440),
            Height = FiniteOrDefault(state.Height, 900),
            Left = FiniteOrNull(state.Left),
            Top = FiniteOrNull(state.Top),
        };

        await File.WriteAllTextAsync(
            LayoutPath,
            JsonSerializer.Serialize(safeState, _json));
    }

    private static double FiniteOrDefault(double value, double fallback)
    {
        return double.IsFinite(value) && value > 0 ? value : fallback;
    }

    private static double? FiniteOrNull(double? value)
    {
        return value is { } number && double.IsFinite(number)
            ? number
            : null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        options.Converters.Add(new FiniteDoubleJsonConverter());
        options.Converters.Add(new NullableFiniteDoubleJsonConverter());
        return options;
    }
}

internal sealed class FiniteDoubleJsonConverter : JsonConverter<double>
{
    public override double Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var value) && double.IsFinite(value)
            ? value
            : 0;
    }

    public override void Write(
        Utf8JsonWriter writer,
        double value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue(double.IsFinite(value) ? value : 0);
    }
}

internal sealed class NullableFiniteDoubleJsonConverter : JsonConverter<double?>
{
    public override double? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var value) && double.IsFinite(value)
            ? value
            : null;
    }

    public override void Write(
        Utf8JsonWriter writer,
        double? value,
        JsonSerializerOptions options)
    {
        if (value is { } number && double.IsFinite(number))
        {
            writer.WriteNumberValue(number);
            return;
        }

        writer.WriteNullValue();
    }
}

public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services = [];

    public ServiceRegistry Add<T>(T instance)
        where T : class
    {
        _services[typeof(T)] = instance;
        return this;
    }

    public T Get<T>()
        where T : class
    {
        return (T)_services[typeof(T)];
    }
}
