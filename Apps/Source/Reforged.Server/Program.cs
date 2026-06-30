using Reforged.Server;

var bootstrap = new ServerBootstrap();
var startDirectory = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var report = bootstrap.Run(startDirectory);

Console.WriteLine("Reforged Server Bootstrap");
Console.WriteLine($"ProjectRoot: {report.ProjectRoot ?? "<not found>"}");
Console.WriteLine($"Project: {report.ProjectName ?? "<unknown>"}");
Console.WriteLine($"CodeUpdate: {report.CodeUpdate ?? "<unknown>"}");
Console.WriteLine($"DatabaseVersion: {report.DatabaseVersion ?? "<unknown>"}");
Console.WriteLine($"Milestone: {report.Milestone ?? "<unknown>"}");
Console.WriteLine($"Checks: {string.Join(", ", report.Checks)}");
Console.WriteLine($"RequiredDirectories: {report.PresentRequiredDirectoryCount}/{report.RequiredDirectoryCount} present, {report.MissingRequiredDirectoryCount} missing");
Console.WriteLine($"Validation: {report.IssueCount} issue(s), {report.InfoCount} info, {report.WarningCount} warning(s), {report.ErrorCount} error(s), {report.FatalCount} fatal");

foreach (var directory in report.RequiredDirectories)
{
    Console.WriteLine($"Directory: {(directory.Exists ? "present" : "missing")} {directory.Name} -> {directory.Path}");
}

foreach (var issue in report.Validation.Issues)
{
    Console.Error.WriteLine($"{issue.Severity}: {issue.Code}: {issue.Message}");
}

if (!report.Passed)
{
    Console.Error.WriteLine("Server bootstrap failed before gameplay startup.");
    return report.ExitCode;
}

Console.WriteLine("Server bootstrap completed successfully. Gameplay, networking, and world simulation remain intentionally disabled.");
return report.ExitCode;
