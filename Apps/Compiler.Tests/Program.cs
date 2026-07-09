using MajorMud.Reforged.Compiler;

static string FindRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "Librarium")) && Directory.Exists(Path.Combine(dir.FullName, "Data"))) return dir.FullName;
        dir = dir.Parent;
    }
    throw new Exception("Repository root not found.");
}

var root = FindRoot();
var result = new LibrariumDataWriter().Compile(new LibrariumCompilerOptions(root, "CompilerSmokeTest", Clean: true));
if (!result.Success) throw new Exception("Compiler smoke test failed: " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
if (result.ObjectFamiliesCompiled < 10) throw new Exception("Too few object families compiled.");
if (!File.Exists(Path.Combine(result.OutputRoot, "compile_manifest.json"))) throw new Exception("Manifest was not generated.");
Console.WriteLine($"PASS: compiled {result.ObjectsCompiled} records across {result.ObjectFamiliesCompiled} families.");
