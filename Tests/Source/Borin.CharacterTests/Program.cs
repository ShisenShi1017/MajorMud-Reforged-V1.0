using System.IO;
using System.Text.Json;
using Reforged.App.Brains;

var repositoryRoot = ReadArgument(args, "--repository") ?? FindRepositoryRoot(Environment.CurrentDirectory);
var reportPath = ReadArgument(args, "--report") ?? Path.Combine(
    repositoryRoot,
    "Reports",
    "Validation",
    $"Borin-Character-Tests-{DateTime.Now:yyyyMMdd-HHmmss}.json");

Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? repositoryRoot);
Console.WriteLine("Borin automated conversation and character tests");
Console.WriteLine($"Repository: {repositoryRoot}");
Console.WriteLine("The GGUF model, BBS connection, and real AI memory are not used by this harness.");
Console.WriteLine();

var harness = new BorinCharacterTestHarness();
var report = await harness.RunAsync();
foreach (var item in report.Cases)
{
    Console.WriteLine($"[{(item.Passed ? "PASS" : "FAIL")}] {item.Name} ({item.DurationMilliseconds:0} ms)");
    if (!item.Passed)
        Console.WriteLine($"       {item.Detail}");
}

await File.WriteAllTextAsync(
    reportPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine();
Console.WriteLine($"Result: {report.PassedCount} passed, {report.FailedCount} failed");
Console.WriteLine($"Full JSON report: {Path.GetFullPath(reportPath)}");
if (args.Contains("--build-majormud-knowledge", StringComparer.OrdinalIgnoreCase))
{
    var catalog = new BorinMajorMudKnowledgeCatalog();
    var status = await catalog.BuildAsync(repositoryRoot, changedOnly: true);
    Console.WriteLine($"MajorMUD knowledge: {status.State}; {status.Records:N0} records; {status.Relationships:N0} relationships");
    Console.WriteLine($"MajorMUD knowledge index: {status.StoragePath}");
    var mastery = await catalog.RunMasteryEvaluationAsync(repositoryRoot);
    Console.WriteLine($"MajorMUD mastery evaluation: {mastery.Summary}");
    Console.WriteLine($"MajorMUD mastery report: {mastery.StoragePath}");
}
return report.Passed ? 0 : 1;

static string? ReadArgument(IReadOnlyList<string> arguments, string name)
{
    for (var index = 0; index < arguments.Count - 1; index++)
    {
        if (arguments[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(arguments[index + 1]);
    }
    return null;
}

static string FindRepositoryRoot(string start)
{
    var current = new DirectoryInfo(Path.GetFullPath(start));
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "MajorMud.Reforged.sln")))
            return current.FullName;
        current = current.Parent;
    }
    return Path.GetFullPath(start);
}
