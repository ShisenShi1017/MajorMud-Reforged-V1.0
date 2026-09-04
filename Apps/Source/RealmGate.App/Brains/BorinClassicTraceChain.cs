using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RealmGate.App.Brains;

public static class BorinClassicTraceStageIds
{
    public const string DecodedSource = "decoded-source";
    public const string LibrariumAuthority = "librarium-authority";
    public const string GeneratedClassicData = "generated-classic-data";
    public const string RuntimeCatalog = "runtime-catalog";
    public const string LiveRuntimeConsumer = "live-runtime-consumer";
    public const string PersistenceResetCleanup = "persistence-reset-cleanup";
    public const string ParityValidation = "parity-validation";
}

public sealed record BorinClassicTraceEvidenceItem(
    string StageId,
    string StageName,
    string RelativePath,
    int LineNumber,
    string Snippet,
    double Score)
{
    public string Location => LineNumber > 0
        ? $"{RelativePath}:{LineNumber}"
        : RelativePath;

    public string PromptLine => $"- {Location} — {Snippet}";
}

public sealed record BorinClassicTraceStageResult(
    string StageId,
    string StageName,
    string Requirement,
    IReadOnlyList<BorinClassicTraceEvidenceItem> Evidence)
{
    public bool HasEvidence => Evidence.Count > 0;
    public string Status => HasEvidence ? "observed evidence" : "no matching evidence found";

    public string PromptText
    {
        get
        {
            if (!HasEvidence)
                return $"[{StageName}] MISSING — {Requirement}";

            return $"[{StageName}] OBSERVED — {Requirement}\n" +
                   string.Join("\n", Evidence.Select(item => item.PromptLine));
        }
    }
}

public sealed record BorinClassicTraceSnapshot(
    string Query,
    IReadOnlyList<BorinClassicTraceStageResult> Stages,
    IReadOnlyList<BorinClassicTraceEvidenceItem> ImportedReferenceEvidence,
    int FilesConsidered,
    int FilesRead,
    string RepositoryRoot,
    string Detail)
{
    public static BorinClassicTraceSnapshot Empty(
        string repositoryRoot = "",
        string query = "",
        string detail = "No Classic trace chain was gathered for this turn.") =>
        new(query, [], [], 0, 0, repositoryRoot, detail);

    public int CoveredStageCount => Stages.Count(stage => stage.HasEvidence);
    public int TotalStageCount => Stages.Count;

    public string Summary => TotalStageCount == 0
        ? Detail
        : $"Classic trace: {CoveredStageCount}/{TotalStageCount} stages have matching evidence • {FilesRead} files read.";

    public string PromptText
    {
        get
        {
            if (Stages.Count == 0)
                return Detail;

            var builder = new StringBuilder();
            builder.AppendLine($"Classic trace request: {Query}");
            builder.AppendLine("Evidence at one stage does not prove the next stage. Missing stages must remain explicit.");
            builder.AppendLine();
            foreach (var stage in Stages)
            {
                builder.AppendLine(stage.PromptText);
                builder.AppendLine();
            }

            if (ImportedReferenceEvidence.Count > 0)
            {
                builder.AppendLine("[Imported references — reference-only, never authoritative and never stage completion]");
                foreach (var item in ImportedReferenceEvidence)
                    builder.AppendLine(item.PromptLine);
            }

            return builder.ToString().Trim();
        }
    }
}

public interface IBorinClassicTraceAccess
{
    bool ShouldTrace(string request);

    Task<BorinClassicTraceSnapshot> TraceAsync(
        string repositoryRoot,
        string request,
        int maximumResultsPerStage = 4,
        CancellationToken token = default);
}

/// <summary>
/// Builds an evidence-only Classic provenance chain. It reads approved text files and
/// never executes, changes, creates, deletes, builds, restarts, installs, or deploys anything.
/// Imported databases are surfaced separately as reference-only evidence and cannot satisfy
/// an authoritative trace stage.
/// </summary>
public sealed class BorinClassicTraceAccess : IBorinClassicTraceAccess
{
    private const int MaximumFilesConsidered = 8000;
    private const int MaximumLinesPerFile = 4000;
    private const long MaximumFileBytes = 3 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".json", ".jsonl", ".md", ".txt", ".log", ".xml",
        ".props", ".targets", ".sln", ".csproj", ".ps1", ".bat", ".cmd",
        ".yml", ".yaml", ".sql", ".csv", ".ini", ".config"
    };

    private static readonly string[] ExcludedDirectoryNames =
    [
        ".git", ".vs", "bin", "obj", "node_modules", "packages", "Backups",
        "AI\\Models", "AI/Models", "Build\\Borin", "Build/Borin"
    ];

    private static readonly string[] TraceIntentPhrases =
    [
        "trace", "provenance", "source to runtime", "end to end", "decoded source",
        "librarium", "generated classic", "runtime catalog", "runtime consumer",
        "persistence", "reset", "cleanup", "parity", "implemented through",
        "fully implemented", "where is this consumed", "where does this come from"
    ];

    private static readonly TraceStageDefinition[] StageDefinitions =
    [
        new(
            BorinClassicTraceStageIds.DecodedSource,
            "1. Decoded source",
            "Locate decoded/native evidence before Librarium projection.",
            ["decoded", "worldgroupnative", "datadecode", "native", "wcc", "reverseengineering"],
            ["decoded", "native", "wcc", "worldgroup", "provenance", "source record"]),
        new(
            BorinClassicTraceStageIds.LibrariumAuthority,
            "2. Librarium authority",
            "Locate the authoritative Librarium record or compiler input.",
            ["librarium/"],
            ["librarium", "objects", "provenance", "confidence", "source"]),
        new(
            BorinClassicTraceStageIds.GeneratedClassicData,
            "3. Generated Classic data",
            "Locate generated Data/Classic output consumed by the runtime.",
            ["data/classic/"],
            ["classic", "generated", "compile_manifest", "world_manifest"]),
        new(
            BorinClassicTraceStageIds.RuntimeCatalog,
            "4. Classic runtime catalog",
            "Locate the loader/catalog/resolver that reads generated Classic data.",
            ["apps/source/realmgate.server/"],
            ["runtimeclassic", "catalog", "authority", "resolver", "load", "deserialize"]),
        new(
            BorinClassicTraceStageIds.LiveRuntimeConsumer,
            "5. Live runtime consumer",
            "Locate live command, gameplay, host, dispatch, or service execution.",
            ["apps/source/realmgate.server/"],
            ["execute", "dispatch", "handle", "apply", "consume", "runtime", "command", "service", "livingworld"]),
        new(
            BorinClassicTraceStageIds.PersistenceResetCleanup,
            "6. Persistence / reset / cleanup",
            "Locate durable state, reset, maintenance, timer, or cleanup behavior where applicable.",
            ["apps/source/realmgate.server/", "core/source/"],
            ["persist", "persistence", "save", "reset", "cleanup", "maintenance", "timer", "restore", "delete"]),
        new(
            BorinClassicTraceStageIds.ParityValidation,
            "7. Parity validation",
            "Locate tests, audits, smoke gates, validators, or reports proving the traced behavior.",
            ["reports/", "sim/", "simulator/", "tests/", "tools/"],
            ["parity", "validate", "validator", "audit", "smoke", "regression", "pass", "coverage", "gate"])
    ];

    public bool ShouldTrace(string request)
    {
        if (string.IsNullOrWhiteSpace(request))
            return false;

        var trimmed = request.Trim();
        if (trimmed.StartsWith("/trace", StringComparison.OrdinalIgnoreCase))
            return true;

        return TraceIntentPhrases.Any(phrase =>
            trimmed.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    public Task<BorinClassicTraceSnapshot> TraceAsync(
        string repositoryRoot,
        string request,
        int maximumResultsPerStage = 4,
        CancellationToken token = default)
    {
        maximumResultsPerStage = Math.Clamp(maximumResultsPerStage, 1, 10);
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
        {
            return Task.FromResult(BorinClassicTraceSnapshot.Empty(
                repositoryRoot,
                request,
                "The configured repository root was not found, so no Classic trace chain was gathered."));
        }

        var terms = Tokenize(request);
        if (terms.Count == 0)
        {
            return Task.FromResult(BorinClassicTraceSnapshot.Empty(
                repositoryRoot,
                request,
                "The trace request did not contain enough searchable terms."));
        }

        var fullRoot = Path.GetFullPath(repositoryRoot);
        return Task.Run(
            () => TraceCore(fullRoot, request, terms, maximumResultsPerStage, token),
            token);
    }

    private static BorinClassicTraceSnapshot TraceCore(
        string repositoryRoot,
        string request,
        IReadOnlyList<string> terms,
        int maximumResultsPerStage,
        CancellationToken token)
    {
        var candidates = StageDefinitions.ToDictionary(
            stage => stage.Id,
            _ => new List<BorinClassicTraceEvidenceItem>(),
            StringComparer.OrdinalIgnoreCase);
        var importedReferences = new List<BorinClassicTraceEvidenceItem>();
        var filesConsidered = 0;
        var filesRead = 0;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories);
        }
        catch (Exception exception)
        {
            return BorinClassicTraceSnapshot.Empty(
                repositoryRoot,
                request,
                $"Classic trace enumeration failed safely: {exception.Message}");
        }

        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            if (filesConsidered >= MaximumFilesConsidered)
                break;
            if (!IsAllowedFile(repositoryRoot, file))
                continue;

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
            var normalizedPath = relative.ToLowerInvariant();
            var queryPathScore = ScoreText(normalizedPath, terms);
            var isImportedReference = IsImportedReference(normalizedPath);
            var possibleStages = StageDefinitions
                .Where(stage => stage.PathHints.Any(hint => normalizedPath.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            if (possibleStages.Length == 0 && !isImportedReference && queryPathScore <= 0)
                continue;

            var matchedFile = false;
            try
            {
                var lineNumber = 0;
                foreach (var rawLine in File.ReadLines(file))
                {
                    token.ThrowIfCancellationRequested();
                    lineNumber++;
                    if (lineNumber > MaximumLinesPerFile)
                        break;

                    var snippet = NormalizeSnippet(rawLine);
                    if (snippet.Length == 0)
                        continue;

                    var queryLineScore = ScoreText(snippet, terms);
                    if (queryLineScore <= 0 && queryPathScore <= 0)
                        continue;

                    if (isImportedReference)
                    {
                        importedReferences.Add(new BorinClassicTraceEvidenceItem(
                            "imported-reference",
                            "Imported reference",
                            relative,
                            lineNumber,
                            snippet,
                            queryLineScore * 4.0 + queryPathScore * 2.0));
                        matchedFile = true;
                        Trim(importedReferences, maximumResultsPerStage * 8);
                        continue;
                    }

                    foreach (var stage in possibleStages)
                    {
                        var stageLineScore = ScoreText(snippet, stage.ContentHints);
                        var stagePathScore = ScoreText(normalizedPath, stage.PathHints);
                        if (stageLineScore <= 0 && stagePathScore <= 0)
                            continue;

                        var score = queryLineScore * 5.0 + queryPathScore * 2.5 + stageLineScore * 1.5 + stagePathScore;
                        if (snippet.Contains(request, StringComparison.OrdinalIgnoreCase))
                            score += 8.0;

                        candidates[stage.Id].Add(new BorinClassicTraceEvidenceItem(
                            stage.Id,
                            stage.Name,
                            relative,
                            lineNumber,
                            snippet,
                            score));
                        matchedFile = true;
                        Trim(candidates[stage.Id], maximumResultsPerStage * 12);
                    }
                }

                if (!matchedFile && queryPathScore > 0)
                {
                    if (isImportedReference)
                    {
                        importedReferences.Add(new BorinClassicTraceEvidenceItem(
                            "imported-reference",
                            "Imported reference",
                            relative,
                            0,
                            "The path matched the request; imported data remains reference-only.",
                            queryPathScore));
                        matchedFile = true;
                    }
                    else
                    {
                        foreach (var stage in possibleStages)
                        {
                            candidates[stage.Id].Add(new BorinClassicTraceEvidenceItem(
                                stage.Id,
                                stage.Name,
                                relative,
                                0,
                                "The path matched the request; no stronger matching line was found within the read limit.",
                                queryPathScore));
                            matchedFile = true;
                        }
                    }
                }

                if (matchedFile)
                    filesRead++;
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

        var stageResults = StageDefinitions
            .Select(stage => new BorinClassicTraceStageResult(
                stage.Id,
                stage.Name,
                stage.Requirement,
                SelectDistinct(candidates[stage.Id], maximumResultsPerStage)))
            .ToArray();
        var selectedReferences = SelectDistinct(importedReferences, maximumResultsPerStage);
        var covered = stageResults.Count(stage => stage.HasEvidence);
        var detail = covered == 0
            ? $"No matching Classic trace evidence was found in {filesConsidered} approved files."
            : $"Read-only Classic trace gathered evidence for {covered}/{stageResults.Length} stages. Missing stages remain unresolved; no file was changed and no command was executed.";

        return new BorinClassicTraceSnapshot(
            request,
            stageResults,
            selectedReferences,
            filesConsidered,
            filesRead,
            repositoryRoot,
            detail);
    }

    private static IReadOnlyList<BorinClassicTraceEvidenceItem> SelectDistinct(
        IEnumerable<BorinClassicTraceEvidenceItem> items,
        int maximumResults) =>
        items
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LineNumber)
            .GroupBy(
                item => $"{item.RelativePath}|{item.LineNumber}|{item.Snippet}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(maximumResults)
            .ToArray();

    private static bool IsAllowedFile(string repositoryRoot, string file)
    {
        if (!AllowedExtensions.Contains(Path.GetExtension(file)))
            return false;

        var relative = Path.GetRelativePath(repositoryRoot, file).Replace('/', '\\');
        foreach (var excluded in ExcludedDirectoryNames)
        {
            var normalized = excluded.Replace('/', '\\');
            if (relative.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains("\\" + normalized + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsImportedReference(string normalizedPath) =>
        normalizedPath.StartsWith("importeddatabases/", StringComparison.OrdinalIgnoreCase) ||
        normalizedPath.Contains("/importeddatabases/", StringComparison.OrdinalIgnoreCase) ||
        normalizedPath.Contains("/imported/", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Tokenize(string text)
    {
        var words = Regex.Matches(text.ToLowerInvariant(), "[a-z0-9_./\\-]+")
            .Select(match => match.Value.Trim('.', '/', '\\', '-'))
            .Where(word => word.Length >= 3)
            .Where(word => word is not "the" and not "and" and not "for" and not "that" and not "this" and not "with" and not "from" and not "what" and not "where" and not "when" and not "why" and not "how" and not "trace")
            .SelectMany(word => word.EndsWith('s') && word.Length > 4 ? new[] { word, word[..^1] } : new[] { word })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        return words;
    }

    private static double ScoreText(string text, IReadOnlyList<string> terms)
    {
        var score = 0.0;
        foreach (var term in terms)
        {
            if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            score += term.Length >= 9 ? 2.8 : term.Length >= 6 ? 1.8 : 1.0;
        }
        return score;
    }

    private static string NormalizeSnippet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var normalized = Regex.Replace(value.Trim(), "\\s+", " ");
        return normalized.Length <= 440 ? normalized : normalized[..440] + "…";
    }

    private static void Trim(List<BorinClassicTraceEvidenceItem> candidates, int keep)
    {
        if (candidates.Count <= keep * 2)
            return;
        var selected = candidates.OrderByDescending(item => item.Score).Take(keep).ToArray();
        candidates.Clear();
        candidates.AddRange(selected);
    }

    private sealed record TraceStageDefinition(
        string Id,
        string Name,
        string Requirement,
        IReadOnlyList<string> PathHints,
        IReadOnlyList<string> ContentHints);
}
