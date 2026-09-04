using MajorMud.Reforged.Compiler;

static string FindRepositoryRoot(string? start)
{
    var directory = new DirectoryInfo(start ?? Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "Librarium")) && Directory.Exists(Path.Combine(directory.FullName, "Data")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Could not locate repository root containing Librarium and Data.");
}

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
var world = "classic";
var clean = false;
string? rootArg = null;
string? outputArg = null;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--world" when i + 1 < args.Length:
            world = args[++i];
            break;
        case "--clean":
            clean = true;
            break;
        case "--root" when i + 1 < args.Length:
            rootArg = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            outputArg = args[++i];
            break;
    }
}

if (command is "help" or "--help" or "-h")
{
    Console.WriteLine("RealmGate Librarium Compiler v1.2 / CodeUpdate 0279");
    Console.WriteLine("Commands:");
    Console.WriteLine("  build --world Classic|Custom1|CustomTemplate|AIFromClassic [--clean] [--root <repo>] [--output <folder>]");
    Console.WriteLine("  validate [--world Classic|Custom1] [--root <repo>]");
    Console.WriteLine("  audit-room-text [--root <repo>]");
    Console.WriteLine("  export-room-text [--root <repo>] [--output <file>]");
    return 0;
}

try
{
    var root = FindRepositoryRoot(rootArg);

    if (command == "validate")
    {
        var validation = new GeneratedProjectionValidator().Validate(root, world);
        Console.WriteLine($"World: {validation.WorldId}");
        Console.WriteLine($"Output: {validation.OutputRoot}");
        Console.WriteLine($"Files validated: {validation.FilesValidated}");
        Console.WriteLine("Mode: read-only generated projection validation");

        foreach (var error in validation.Errors)
        {
            Console.Error.WriteLine($"ERROR: {error}");
        }

        return validation.Success ? 0 : 1;
    }

    if (command == "audit-room-text")
    {
        var diagnostics = new List<CompilerDiagnostic>();
        var families = new LibrariumCatalogReader().ReadFamilies(Path.Combine(root, "Librarium"), diagnostics);
        var rooms = families.FirstOrDefault(family => family.Name.Equals("rooms", StringComparison.OrdinalIgnoreCase));
        if (rooms is null)
        {
            Console.Error.WriteLine("ERROR ROOMTEXT000: rooms family was not found.");
            return 1;
        }

        var audit = new NativeMapRoomTextResolver().Audit(root, rooms);
        Console.WriteLine("========== CLASSIC ROOM TEXT COMPILER AUDIT ==========");
        Console.WriteLine($"Source rooms: {audit.SourceRooms:N0}");
        Console.WriteLine($"Inline descriptions: {audit.InlineDescriptions:N0}");
        Console.WriteLine($"Description ref IDs: {audit.DescReferenceCount:N0}");
        Console.WriteLine($"Long description ref IDs: {audit.LongDescReferenceCount:N0}");
        Console.WriteLine($"Native text records loaded: {audit.TextRecordsLoaded:N0}");
        Console.WriteLine($"References resolved: {audit.ResolvedReferences:N0}");
        Console.WriteLine($"References unresolved: {audit.UnresolvedReferences:N0}");
        Console.WriteLine($"Emitted non-empty descriptions: {audit.EmittedNonEmptyDescriptions:N0}");
        Console.WriteLine($"Emitted empty descriptions: {audit.EmittedEmptyDescriptions:N0}");
        Console.WriteLine($"Duplicate native title groups: {audit.DuplicateNativeTitles:N0}");
        Console.WriteLine($"Missing native map files: {(audit.MissingNativeMapFiles.Count == 0 ? "none" : string.Join(", ", audit.MissingNativeMapFiles))}");
        Console.WriteLine("First unresolved rooms:");
        foreach (var unresolved in audit.FirstUnresolvedRooms)
        {
            Console.WriteLine($"- {unresolved.RoomId} | {unresolved.Title} | map {unresolved.MapId} room {unresolved.RoomNumber} | ref {unresolved.Reference} | family {unresolved.Family} | {unresolved.Reason}");
        }
        Console.WriteLine("======================================================");

        foreach (var diagnostic in diagnostics)
            Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");

        return diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) ? 1 : 0;
    }

    if (command == "export-room-text")
    {
        var diagnostics = new List<CompilerDiagnostic>();
        var families = new LibrariumCatalogReader().ReadFamilies(Path.Combine(root, "Librarium"), diagnostics);
        var rooms = families.FirstOrDefault(family => family.Name.Equals("rooms", StringComparison.OrdinalIgnoreCase));
        if (rooms is null)
        {
            Console.Error.WriteLine("ERROR ROOMTEXT000: rooms family was not found.");
            return 1;
        }

        var outputPath = outputArg is null
            ? Path.Combine(root, "Librarium", "Objects", "room_descriptions", "native_map_room_description_records.json")
            : Path.GetFullPath(Path.Combine(root, outputArg));
        var count = new NativeMapRoomTextResolver().ExportPromotedDescriptions(root, rooms, outputPath);
        Console.WriteLine($"Exported {count:N0} room description records to {Path.GetRelativePath(root, outputPath)}");
        foreach (var diagnostic in diagnostics)
            Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");

        return diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) ? 1 : 0;
    }

    if (command != "build")
    {
        Console.Error.WriteLine($"Unknown compiler command: {command}");
        return 2;
    }

    var options = new LibrariumCompilerOptions(root, world, clean, OutputRootOverride: outputArg);
    var compiler = new LibrariumDataWriter();
    var result = compiler.Compile(options);

    Console.WriteLine($"World: {result.WorldId}");
    Console.WriteLine($"Output: {result.OutputRoot}");
    Console.WriteLine($"Families: {result.ObjectFamiliesCompiled}");
    Console.WriteLine($"Objects: {result.ObjectsCompiled}");
    Console.WriteLine($"Files written: {result.FilesWritten}");

    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
    }

    return result.Success ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
