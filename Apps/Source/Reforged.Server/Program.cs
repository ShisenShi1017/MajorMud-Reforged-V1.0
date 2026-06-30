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
Console.WriteLine($"Readiness: {report.Readiness.Status}; gameplay={report.Readiness.GameplayStatus}");
Console.WriteLine($"RequiredDirectories: {report.PresentRequiredDirectoryCount}/{report.RequiredDirectoryCount} present, {report.MissingRequiredDirectoryCount} missing");
Console.WriteLine($"Validation: {report.IssueCount} issue(s), {report.InfoCount} info, {report.WarningCount} warning(s), {report.ErrorCount} error(s), {report.FatalCount} fatal");

foreach (var directory in report.RequiredDirectories)
{
    Console.WriteLine($"Directory: {(directory.Exists ? "present" : "missing")} {directory.Name} -> {directory.Path}");
}

if (report.ServerDataCatalog is not null)
{
    Console.WriteLine($"ServerDataCatalog: {report.ServerDataCatalog.Status}; categories={report.ServerDataCatalog.PresentCategoryCount}/{report.ServerDataCatalog.CategoryCount}; runtimeReady={report.ServerDataCatalog.RuntimeReadyCategoryCount}; runtimeBlocked={report.ServerDataCatalog.RuntimeBlockedCategoryCount}; placeholders={report.ServerDataCatalog.EmptyPlaceholderCategoryCount}; records={report.ServerDataCatalog.LoadedRecordCount}");
    foreach (var category in report.ServerDataCatalog.Categories)
    {
        Console.WriteLine($"ServerDataCategory: {category.Category}; status={category.Status}; runtimeReady={category.RuntimeReady}; placeholder={category.IsPlaceholder}; records={category.RecordCount}; file={category.RelativePath}; bytes={category.FileSizeBytes}; note={category.ReadinessNote}");
    }
}

if (report.ClassicParityAudit is not null)
{
    Console.WriteLine($"ClassicParity: {report.ClassicParityAudit.Status}; canonicalFiles={report.ClassicParityAudit.CanonicalFileCount}; importedReferenceFiles={report.ClassicParityAudit.ImportedReferenceFileCount}; exactNameMatches={report.ClassicParityAudit.ExactNameMatchCount}");
    foreach (var category in report.ClassicParityAudit.Categories)
    {
        Console.WriteLine($"ClassicParityCategory: {category.Category}; canonical={category.CanonicalFileCount}; imported={category.ImportedReferenceFileCount}; matches={category.ExactNameMatchCount}; status={category.Status}");
    }
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
