using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

public sealed record BorinReadOnlyEvidenceItem(
    string RelativePath,
    int LineNumber,
    string Kind,
    string Snippet,
    double Score)
{
    public string Location => LineNumber > 0
        ? $"{RelativePath}:{LineNumber}"
        : RelativePath;

    public string PromptLine => $"- [{Kind}] {Location} — {Snippet}";
}

public sealed record BorinReadOnlyEvidenceSnapshot(
    IReadOnlyList<BorinReadOnlyEvidenceItem> Items,
    int FilesConsidered,
    int FilesRead,
    string RepositoryRoot,
    string Detail)
{
    public static BorinReadOnlyEvidenceSnapshot Empty(string repositoryRoot = "", string detail = "No read-only project evidence was gathered for this turn.") =>
        new([], 0, 0, repositoryRoot, detail);

    public string Summary => Items.Count == 0
        ? Detail
        : $"Read-only evidence: {Items.Count} matches from {FilesRead} files ({FilesConsidered} considered).";

    public string PromptText => Items.Count == 0
        ? Detail
        : string.Join("\n", Items.Select(item => item.PromptLine));
}

public interface IBorinReadOnlyAccess
{
    Task<BorinReadOnlyEvidenceSnapshot> GatherAsync(
        string repositoryRoot,
        string request,
        int maximumResults = 12,
        CancellationToken token = default);
}

/// <summary>
/// Controlled read-only bridge between Borin's local model and the Reforged project.
/// It never creates, changes, deletes, executes, builds, launches, or restarts anything.
/// Only approved text files under the repository root are inspected.
/// </summary>
public sealed class BorinReadOnlyAccess : IBorinReadOnlyAccess
{
    private const int MaximumFilesConsidered = 6000;
    private const int MaximumLinesPerFile = 3000;
    private const long MaximumFileBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".json", ".jsonl", ".md", ".txt", ".log", ".xml",
        ".props", ".targets", ".sln", ".csproj", ".ps1", ".bat", ".cmd",
        ".yml", ".yaml", ".sql", ".csv", ".ini", ".config"
    };

    private static readonly string[] ExcludedDirectoryNames =
    [
        ".git", ".vs", "bin", "obj", "node_modules", "packages", "Backups",
        "AI\\Models", "AI/Models", "AI\\Workspaces", "AI/Workspaces", "Build\\Borin", "Build/Borin"
    ];

    private static readonly HashSet<string> CasualOnlyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "hi", "hello", "hey", "morning", "afternoon", "evening", "thanks", "thank",
        "aye", "yes", "no", "okay", "ok", "howdy"
    };

    public Task<BorinReadOnlyEvidenceSnapshot> GatherAsync(
        string repositoryRoot,
        string request,
        int maximumResults = 12,
        CancellationToken token = default)
    {
        maximumResults = Math.Clamp(maximumResults, 1, 30);
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
            return Task.FromResult(BorinReadOnlyEvidenceSnapshot.Empty(repositoryRoot, "The configured repository root was not found."));

        var terms = Tokenize(request);
        if (!ShouldInspect(request, terms))
            return Task.FromResult(BorinReadOnlyEvidenceSnapshot.Empty(repositoryRoot, "This conversational turn did not require repository evidence."));

        var fullRoot = Path.GetFullPath(repositoryRoot);
        return Task.Run(() => GatherCore(fullRoot, request, terms, maximumResults, token), token);
    }

    private static BorinReadOnlyEvidenceSnapshot GatherCore(
        string repositoryRoot,
        string request,
        IReadOnlyList<string> terms,
        int maximumResults,
        CancellationToken token)
    {
        var candidates = new List<BorinReadOnlyEvidenceItem>();
        var filesConsidered = 0;
        var filesRead = 0;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories);
        }
        catch (Exception exception)
        {
            return BorinReadOnlyEvidenceSnapshot.Empty(repositoryRoot, $"Read-only enumeration failed safely: {exception.Message}");
        }

        // Repository enumeration order is not evidence order. Prefer application/core/test
        // source and request-matching paths so large Archive/Librarium trees cannot consume
        // the bounded file budget before the relevant implementation is reached.
        files = files
            .Where(file => IsAllowedFile(repositoryRoot, file))
            .OrderByDescending(file => SourcePriority(Path.GetRelativePath(repositoryRoot, file)))
            .ThenByDescending(file => ScoreText(Path.GetRelativePath(repositoryRoot, file), terms))
            .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumFilesConsidered)
            .ToArray();

        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            if (filesConsidered >= MaximumFilesConsidered)
                break;
            filesConsidered++;
            FileInfo info;
            try
            {
                info = new FileInfo(file);
                if (!info.Exists || info.Length > MaximumFileBytes)
                    continue;
            }
            catch
            {
                continue;
            }

            var relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            var pathScore = ScoreText(relative, terms) * 2.0;
            var sourceBonus = SourcePriority(relative) * 0.35;
            var isLogRequest = terms.Any(term => term is "log" or "report" or "error" or "fail");
            var isLogFile = relative.Contains("/Reports/", StringComparison.OrdinalIgnoreCase) ||
                            relative.Contains("/Logs/", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetExtension(file).Equals(".log", StringComparison.OrdinalIgnoreCase);

            try
            {
                var lineNumber = 0;
                var matchedThisFile = false;
                foreach (var rawLine in File.ReadLines(file))
                {
                    token.ThrowIfCancellationRequested();
                    lineNumber++;
                    if (lineNumber > MaximumLinesPerFile)
                        break;

                    var line = NormalizeSnippet(rawLine);
                    if (line.Length == 0)
                        continue;

                    var lineScore = ScoreText(line, terms);
                    if (lineScore <= 0 && pathScore <= 0)
                        continue;

                    var score = lineScore * 4.0 + pathScore + sourceBonus;
                    if (isLogRequest && isLogFile)
                        score += 3.0;
                    if (line.Contains(request, StringComparison.OrdinalIgnoreCase))
                        score += 8.0;

                    candidates.Add(new BorinReadOnlyEvidenceItem(
                        relative,
                        lineNumber,
                        Classify(relative),
                        line,
                        score));
                    matchedThisFile = true;

                    if (candidates.Count > maximumResults * 20)
                        TrimCandidates(candidates, maximumResults * 10);
                }

                if (matchedThisFile)
                    filesRead++;
                else if (pathScore > 0)
                {
                    candidates.Add(new BorinReadOnlyEvidenceItem(
                        relative,
                        0,
                        Classify(relative),
                        "Path matched the request; no stronger text line was found within the read limit.",
                        pathScore));
                    filesRead++;
                }
            }
            catch (IOException)
            {
                // A changing log may be locked. Skip it without widening permissions.
            }
            catch (UnauthorizedAccessException)
            {
                // Read-only access remains bounded by the current Windows account.
            }
        }

        AddSourceReferences(repositoryRoot, files, candidates, maximumResults, token);

        var deduplicated = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LineNumber)
            .GroupBy(item => $"{item.RelativePath}|{item.LineNumber}|{item.Snippet}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Take(group.Any(item => item.Kind == "Source reference") ? 8 : 2))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LineNumber)
            .ToArray();
        var directLimit = Math.Max(1, maximumResults - Math.Min(4, maximumResults / 3));
        var reserved = deduplicated
            .Where(item => item.Kind != "Source reference" &&
                (item.RelativePath.Contains("/Views/", StringComparison.OrdinalIgnoreCase) || item.RelativePath.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.RelativePath.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase) ? "ViewModels" : "Views")
            .SelectMany(group => group.Take(3))
            .ToList();
        reserved.AddRange(deduplicated
            .Where(item => item.Kind != "Source reference" && !reserved.Contains(item))
            .Take(Math.Max(0, directLimit - reserved.Count)));
        var selected = reserved
            .Concat(deduplicated.Where(item => item.Kind == "Source reference").Take(maximumResults - reserved.Count))
            .Take(maximumResults)
            .Select(item => item with { Snippet = ExpandSourceWindow(repositoryRoot, item) })
            .ToArray();

        var detail = selected.Length == 0
            ? $"Read-only search found no matching text in {filesConsidered} approved files."
            : "Evidence was gathered read-only. No file was changed and no command was executed.";
        return new BorinReadOnlyEvidenceSnapshot(selected, filesConsidered, filesRead, repositoryRoot, detail);
    }

    private static string ExpandSourceWindow(string repositoryRoot, BorinReadOnlyEvidenceItem item)
    {
        if (item.LineNumber <= 0 || item.Kind is not ("Source" or "Source reference"))
            return item.Snippet;
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, item.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var fullRoot = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return item.Snippet;
        try
        {
            var lines = File.ReadLines(fullPath)
                .Take(Math.Min(MaximumLinesPerFile, item.LineNumber + 5))
                .ToArray();
            var start = Math.Max(1, item.LineNumber - 5);
            var end = Math.Min(lines.Length, item.LineNumber + 5);
            var builder = new StringBuilder();
            for (var line = start; line <= end; line++)
                builder.Append(line).Append(": ").AppendLine(lines[line - 1]);
            var value = builder.ToString().TrimEnd();
            return value.Length <= 1800 ? value : value[..1800];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return item.Snippet;
        }
    }

    private static void AddSourceReferences(
        string repositoryRoot,
        IEnumerable<string> files,
        List<BorinReadOnlyEvidenceItem> candidates,
        int maximumResults,
        CancellationToken token)
    {
        var symbolScores = candidates
            .Where(item => item.RelativePath.Contains("/Views/", StringComparison.OrdinalIgnoreCase) ||
                           item.RelativePath.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Score)
            .Select(item => new { Symbol = Path.GetFileNameWithoutExtension(item.RelativePath), item.Score })
            .Where(item => item.Symbol.Length >= 7)
            .GroupBy(item => item.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Symbol = group.Key, Relevance = group.Max(item => item.Score) })
            .OrderByDescending(item => item.Relevance)
            .Take(20)
            .ToDictionary(item => item.Symbol, item => item.Relevance, StringComparer.OrdinalIgnoreCase);
        if (symbolScores.Count == 0) return;

        var references = new List<BorinReadOnlyEvidenceItem>();
        foreach (var file in files.Where(file => Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".xaml", StringComparison.OrdinalIgnoreCase)))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            try
            {
                var lineNumber = 0;
                foreach (var rawLine in File.ReadLines(file))
                {
                    if (++lineNumber > MaximumLinesPerFile) break;
                    var symbol = symbolScores.Keys.FirstOrDefault(value => rawLine.Contains(value, StringComparison.Ordinal));
                    if (symbol is null) continue;
                    var score = 40 + symbol.Length + symbolScores[symbol];
                    if (rawLine.Contains("new " + symbol, StringComparison.Ordinal)) score += 30;
                    if (rawLine.Contains("DataContext", StringComparison.Ordinal)) score += 15;
                    if (relative.EndsWith("App.xaml.cs", StringComparison.OrdinalIgnoreCase)) score += 10;
                    references.Add(new BorinReadOnlyEvidenceItem(relative, lineNumber, "Source reference", NormalizeSnippet(rawLine), score));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        candidates.AddRange(references
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maximumResults));
    }

    private static bool ShouldInspect(string request, IReadOnlyList<string> terms)
    {
        var trimmed = request.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("/", StringComparison.Ordinal))
            return false;
        if (terms.Count == 0)
            return false;
        if (terms.Count <= 4 && terms.All(term => CasualOnlyWords.Contains(term)))
            return false;
        return true;
    }

    private static bool IsAllowedFile(string repositoryRoot, string file)
    {
        var extension = Path.GetExtension(file);
        if (!AllowedExtensions.Contains(extension))
            return false;

        var relative = Path.GetRelativePath(repositoryRoot, file).Replace('/', '\\');
        foreach (var excluded in ExcludedDirectoryNames)
        {
            var normalized = excluded.Replace('/', '\\');
            if (relative.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains("\\" + normalized + "\\", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static int SourcePriority(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        if (path.StartsWith("Apps/Source/Reforged.App/Views/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("Apps/Source/Reforged.App/ViewModels/", StringComparison.OrdinalIgnoreCase)) return 8;
        if (path.StartsWith("Apps/Source/Reforged.App/Services/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("Apps/Source/Reforged.App/Models/", StringComparison.OrdinalIgnoreCase)) return 7;
        if (path.StartsWith("Apps/Source/", StringComparison.OrdinalIgnoreCase)) return 6;
        if (path.StartsWith("Core/Source/", StringComparison.OrdinalIgnoreCase)) return 5;
        if (path.StartsWith("Tests/Source/", StringComparison.OrdinalIgnoreCase) || path.Contains(".Tests/", StringComparison.OrdinalIgnoreCase)) return 4;
        if (path.StartsWith("Apps/", StringComparison.OrdinalIgnoreCase)) return 3;
        if (path.StartsWith("AI/Knowledge/", StringComparison.OrdinalIgnoreCase)) return 2;
        return 1;
    }

    private static IReadOnlyList<string> Tokenize(string text)
    {
        var normalizedText = text.ToLowerInvariant();
        var words = Regex.Matches(normalizedText, "[a-z0-9_./\\-]+")
            .Select(match => match.Value.Trim('.', '/', '\\', '-'))
            .Where(word => word.Length >= 3)
            .Where(word => word is not "the" and not "and" and not "for" and not "that" and not "this" and not "with" and not "from" and not "what" and not "where" and not "when" and not "why" and not "how"
                and not "borin" and not "investigate" and not "create" and not "focused" and not "patch" and not "isolated" and not "workspace" and not "build" and not "test" and not "incorporate")
            .SelectMany(word => word.EndsWith('s') && word.Length > 4 ? new[] { word, word[..^1] } : new[] { word })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(18)
            .ToList();
        if (normalizedText.Contains("sys goto", StringComparison.Ordinal) && !words.Contains("sysgoto", StringComparer.OrdinalIgnoreCase))
            words.Insert(0, "sysgoto");
        return words.Take(18).ToArray();
    }

    private static double ScoreText(string text, IReadOnlyList<string> terms)
    {
        var score = 0.0;
        foreach (var term in terms)
        {
            var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;
            score += term.Length >= 8 ? 2.5 : term.Length >= 5 ? 1.6 : 1.0;
        }
        return score;
    }

    private static string NormalizeSnippet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var normalized = Regex.Replace(value.Trim(), "\\s+", " ");
        return normalized.Length <= 420 ? normalized : normalized[..420] + "…";
    }

    private static string Classify(string relativePath)
    {
        if (relativePath.Contains("Librarium", StringComparison.OrdinalIgnoreCase)) return "Librarium";
        if (relativePath.Contains("Decoded", StringComparison.OrdinalIgnoreCase)) return "Decoded";
        if (relativePath.Contains("Imported", StringComparison.OrdinalIgnoreCase)) return "Imported reference";
        if (relativePath.Contains("Reports", StringComparison.OrdinalIgnoreCase) || relativePath.Contains("Logs", StringComparison.OrdinalIgnoreCase)) return "Report/Log";
        if (relativePath.Contains("Data", StringComparison.OrdinalIgnoreCase)) return "Data";
        if (relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) return "Source";
        return "Project file";
    }

    private static void TrimCandidates(List<BorinReadOnlyEvidenceItem> candidates, int keep)
    {
        var trimmed = candidates.OrderByDescending(item => item.Score).Take(keep).ToArray();
        candidates.Clear();
        candidates.AddRange(trimmed);
    }
}
