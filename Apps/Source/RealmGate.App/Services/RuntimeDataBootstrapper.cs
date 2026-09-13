using RealmGate.Compiler;
using RealmGate.Server;

namespace RealmGate.App.Services;

/// <summary>
/// Treats Librarium as the authoritative installed game database and Data/ as disposable runtime output.
/// A clean public install ships Librarium, configuration, and binaries; missing runtime world data is
/// generated locally before the embedded host is allowed to bootstrap.
/// </summary>
internal static class RuntimeDataBootstrapper
{
    private static readonly string[] RuntimeDirectories =
    [
        "Apps",
        "Core",
        "Data",
        "Docs",
        "Reports",
        "Bin"
    ];

    public static ServerRuntimeOptions Prepare(
        string projectRoot,
        IAppLogger logger,
        Action<string>? status = null)
    {
        if (!File.Exists(Path.Combine(projectRoot, "ProjectManifest.json")))
            throw new InvalidOperationException($"RealmGate project manifest was not found at '{projectRoot}'.");

        var librariumRoot = Path.GetFullPath(Path.Combine(projectRoot, "Librarium"));
        if (!Directory.Exists(librariumRoot))
            throw new InvalidOperationException($"RealmGate Librarium was not found at '{librariumRoot}'.");

        EnsureRuntimeLayout(projectRoot);
        var options = ServerRuntimeOptions.Load(projectRoot);

        foreach (var world in options.Worlds.Where(world => world.RuntimeEnabled))
        {
            var outputRoot = ResolveRuntimeOutputRoot(projectRoot, librariumRoot, world);
            var roomsPath = Path.Combine(outputRoot, world.RoomsFile);
            var manifestPath = Path.Combine(outputRoot, "world_manifest.json");
            if (File.Exists(roomsPath) && File.Exists(manifestPath))
                continue;

            status?.Invoke($"Building {world.Name} from Librarium");
            logger.Info($"Runtime data for world '{world.Id}' is missing. Generating it from Librarium into '{outputRoot}'.");

            CompileResult result;
            try
            {
                result = new LibrariumDataWriter().Compile(
                    new LibrariumCompilerOptions(
                        projectRoot,
                        world.Id,
                        Clean: true,
                        OutputRootOverride: outputRoot));
            }
            catch (Exception exception)
            {
                HandleGenerationFailure(projectRoot, options, world, logger, exception.Message, exception);
                continue;
            }

            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Take(5)
                    .ToArray();
                HandleGenerationFailure(
                    projectRoot,
                    options,
                    world,
                    logger,
                    errors.Length == 0 ? "The Librarium compiler reported an unsuccessful result." : string.Join(" | ", errors));
                continue;
            }

            if (!PathsEqual(result.OutputRoot, outputRoot))
            {
                HandleGenerationFailure(
                    projectRoot,
                    options,
                    world,
                    logger,
                    $"The compiler reported an unexpected output root '{result.OutputRoot}'. Expected '{outputRoot}'.");
                continue;
            }

            if (!File.Exists(roomsPath) || !File.Exists(manifestPath))
            {
                HandleGenerationFailure(
                    projectRoot,
                    options,
                    world,
                    logger,
                    "The compiler completed but did not create the required rooms/world manifest output in the configured Data root.");
                continue;
            }

            logger.Info($"Generated world '{world.Id}' into '{outputRoot}': {result.ObjectsCompiled:N0} objects in {result.FilesWritten:N0} files.");
        }

        status?.Invoke("Starting");
        return options;
    }

    private static void EnsureRuntimeLayout(string projectRoot)
    {
        foreach (var relative in RuntimeDirectories)
            Directory.CreateDirectory(Path.Combine(projectRoot, relative));
    }

    private static string ResolveRuntimeOutputRoot(
        string projectRoot,
        string librariumRoot,
        WorldRuntimeDefinition world)
    {
        var dataRoot = Path.GetFullPath(Path.Combine(projectRoot, "Data"));
        var configuredOutputRoot = Path.GetFullPath(Path.Combine(projectRoot, world.DataRoot));

        if (!IsSameOrDescendant(configuredOutputRoot, dataRoot))
        {
            throw new InvalidOperationException(
                $"World '{world.Id}' has an invalid dataRoot '{world.DataRoot}'. Runtime worlds may only generate under '{dataRoot}'.");
        }

        if (IsSameOrDescendant(configuredOutputRoot, librariumRoot))
        {
            throw new InvalidOperationException(
                $"World '{world.Id}' resolves inside Librarium. Generated runtime Data must never be written into Librarium.");
        }

        return configuredOutputRoot;
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }

    private static bool IsSameOrDescendant(string candidate, string parent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidate, parent, comparison)) return true;

        var parentWithSeparator = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(parentWithSeparator, comparison);
    }

    private static void HandleGenerationFailure(
        string projectRoot,
        ServerRuntimeOptions options,
        WorldRuntimeDefinition world,
        IAppLogger logger,
        string details,
        Exception? exception = null)
    {
        var message = $"World '{world.Id}' could not be generated from Librarium. {details}";
        var isRequired = world.Id.Equals(options.DefaultWorldId, StringComparison.OrdinalIgnoreCase) ||
                         world.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase);

        if (isRequired)
            throw exception is null ? new InvalidOperationException(message) : new InvalidOperationException(message, exception);

        if (exception is null)
            logger.Error(message);
        else
            logger.Error(message, exception);
    }
}
