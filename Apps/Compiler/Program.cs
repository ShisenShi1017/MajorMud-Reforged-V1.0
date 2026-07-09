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
    }
}

if (command is "help" or "--help" or "-h")
{
    Console.WriteLine("MajorMUD Reforged Librarium Compiler v1.1 / CodeUpdate 0127");
    Console.WriteLine("Commands:");
    Console.WriteLine("  build --world Classic|CustomTemplate|AIFromClassic [--clean] [--root <repo>]");
    Console.WriteLine("  validate [--root <repo>]");
    return 0;
}

try
{
    var root = FindRepositoryRoot(rootArg);
    var options = new LibrariumCompilerOptions(root, world, clean);
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
