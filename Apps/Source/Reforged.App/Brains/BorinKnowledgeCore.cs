using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reforged.App.Brains;

public enum BorinKnowledgeConfidence
{
    Unknown,
    Hypothesis,
    Likely,
    StronglyCorrelated,
    Proven,
}

public static class BorinKnowledgeConfidenceExtensions
{
    public static string ToDisplayText(this BorinKnowledgeConfidence value) => value switch
    {
        BorinKnowledgeConfidence.StronglyCorrelated => "STRONGLY CORRELATED",
        _ => value.ToString().ToUpperInvariant(),
    };
}

public enum BorinKnowledgeSearchMode
{
    Hybrid,
    Exact,
    Semantic,
    Relationship,
}

public sealed record BorinKnowledgeProvenance(
    string Category,
    string RelativePath,
    int StartLine,
    int EndLine,
    string Version);

public sealed record BorinKnowledgeQuery(
    string Text,
    BorinKnowledgeSearchMode Mode = BorinKnowledgeSearchMode.Hybrid,
    int MaximumResults = 8);

public interface IBorinKnowledgeCore : IBorinKnowledgeIndex, IDisposable
{
    Task<BorinMajorMudKnowledgeStatus> GetMajorMudStatusAsync(
        string repositoryRoot,
        CancellationToken token = default);
    Task<BorinMasteryEvaluationReport> RunMajorMudMasteryEvaluationAsync(
        string repositoryRoot,
        CancellationToken token = default);
    Task<IReadOnlyList<BorinMajorMudEntity>> GetMajorMudSampleEntitiesAsync(
        string repositoryRoot,
        string family,
        int maximumResults = 5,
        CancellationToken token = default);
    Task<BorinKnowledgeIndexSnapshot> SearchAsync(
        string repositoryRoot,
        BorinKnowledgeQuery query,
        CancellationToken token = default);
}

/// <summary>
/// Shared, provider-neutral knowledge facade. The existing approved-text index remains the
/// document authority; this service adds Roslyn declarations/references and one retrieval
/// contract consumed identically by Local, Lightning, and Ultra.
/// </summary>
public sealed class BorinKnowledgeCore : IBorinKnowledgeCore
{
    private const int GraphVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
    private static readonly Regex Terms = new("[A-Za-z_][A-Za-z0-9_]{1,}", RegexOptions.Compiled);
    private static readonly Regex RoomReferencePattern = new(
        @"room\.map\d+\.\d+|(?:map)?\d+\s*[/.:,-]\s*(?:room)?\d+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<string, string[]> SemanticAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["checkbox"] = ["IsChecked", "CheckBox", "binding", "setting"],
            ["persist"] = ["save", "load", "store", "serialize", "settings"],
            ["permission"] = ["authorize", "authority", "capability", "role", "sysop"],
            ["command"] = ["handler", "execute", "dispatch", "register", "parser"],
            ["call"] = ["reference", "invoke", "caller", "usage"],
            ["test"] = ["assert", "fixture", "harness", "smoke", "validation"],
            ["network"] = ["session", "socket", "transport", "connection", "terminal"],
        };

    private readonly IBorinKnowledgeIndex _documents;
    private readonly IBorinMajorMudKnowledgeCatalog? _majorMud;
    private readonly IBorinVerifiedKnowledgeLearning? _verifiedLearning;
    private readonly SemaphoreSlim _graphGate = new(1, 1);
    private readonly object _watchSync = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _refreshDelay;
    private string _watchedRoot = string.Empty;

    public BorinKnowledgeCore(
        IBorinKnowledgeIndex documents,
        IBorinMajorMudKnowledgeCatalog? majorMud = null,
        IBorinVerifiedKnowledgeLearning? verifiedLearning = null)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _majorMud = majorMud;
        _verifiedLearning = verifiedLearning;
        _documents.StatusChanged += status => StatusChanged?.Invoke(status);
    }

    public event Action<BorinKnowledgeIndexStatus>? StatusChanged;

    public Task<BorinKnowledgeIndexStatus> GetStatusAsync(string repositoryRoot, CancellationToken token = default) =>
        _documents.GetStatusAsync(repositoryRoot, token);

    public Task<BorinMajorMudKnowledgeStatus> GetMajorMudStatusAsync(
        string repositoryRoot,
        CancellationToken token = default) =>
        _majorMud?.GetStatusAsync(repositoryRoot, token)
        ?? Task.FromResult(new BorinMajorMudKnowledgeStatus("Unavailable", 0, 0, string.Empty, null, "MajorMUD catalog is not configured."));

    public Task<BorinMasteryEvaluationReport> RunMajorMudMasteryEvaluationAsync(
        string repositoryRoot,
        CancellationToken token = default) =>
        _majorMud?.RunMasteryEvaluationAsync(repositoryRoot, token)
        ?? throw new InvalidOperationException("MajorMUD catalog is not configured.");

    public Task<IReadOnlyList<BorinMajorMudEntity>> GetMajorMudSampleEntitiesAsync(
        string repositoryRoot, string family, int maximumResults = 5, CancellationToken token = default) =>
        _majorMud?.GetSampleEntitiesAsync(repositoryRoot, family, maximumResults, token)
        ?? Task.FromResult<IReadOnlyList<BorinMajorMudEntity>>([]);

    public async Task<BorinKnowledgeIndexStatus> BuildAsync(
        string repositoryRoot,
        bool changedOnly,
        CancellationToken token = default)
    {
        var status = await _documents.BuildAsync(repositoryRoot, changedOnly, token);
        await BuildSymbolGraphAsync(repositoryRoot, changedOnly, token);
        if (_majorMud is not null)
            await _majorMud.BuildAsync(repositoryRoot, changedOnly, token);
        StartWatching(repositoryRoot);
        return status;
    }

    public Task<BorinKnowledgeIndexSnapshot> SearchAsync(
        string repositoryRoot,
        string query,
        int maximumResults = 8,
        CancellationToken token = default) =>
        SearchAsync(repositoryRoot, new BorinKnowledgeQuery(query, BorinKnowledgeSearchMode.Hybrid, maximumResults), token);

    public async Task<BorinKnowledgeIndexSnapshot> SearchAsync(
        string repositoryRoot,
        BorinKnowledgeQuery query,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var maximum = Math.Clamp(query.MaximumResults, 1, 20);
        var documentTask = query.Mode == BorinKnowledgeSearchMode.Relationship
            ? Task.FromResult(BorinKnowledgeIndexSnapshot.Empty(repositoryRoot, query.Text))
            : _documents.SearchAsync(repositoryRoot, ExpandSemanticQuery(query), maximum, token);
        var graphTask = SearchGraphAsync(repositoryRoot, query, maximum, token);
        var majorMudTask = _majorMud?.SearchAsync(repositoryRoot, query, maximum, token)
            ?? Task.FromResult<IReadOnlyList<BorinKnowledgeEvidenceItem>>([]);
        var routeTask = BuildRouteEvidenceAsync(repositoryRoot, query.Text, token);
        var specialistTask = BuildMajorMudSpecialistEvidenceAsync(repositoryRoot, query.Text, token);
        var learnedTask = _verifiedLearning?.SearchAsync(query.Text, maximum, token)
            ?? Task.FromResult<IReadOnlyList<BorinKnowledgeEvidenceItem>>([]);
        await Task.WhenAll(documentTask, graphTask, majorMudTask, routeTask, specialistTask, learnedTask);

        var documents = await documentTask;
        var graph = await graphTask;
        var merged = documents.Matches
            .Concat(graph)
            .Concat(await majorMudTask)
            .Concat(await routeTask)
            .Concat(await specialistTask)
            .Concat(await learnedTask)
            .GroupBy(item => $"{item.RelativePath}|{item.StartLine}|{item.EndLine}|{item.Relationship}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maximum)
            .ToArray();
        return new BorinKnowledgeIndexSnapshot(
            repositoryRoot,
            query.Text,
            merged,
            merged.Length == 0
                ? "The shared Knowledge Core found no current authoritative evidence. Confidence is UNKNOWN."
                : $"The shared Knowledge Core retrieved {merged.Length} current source evidence records using {query.Mode} retrieval.");
    }

    private async Task<IReadOnlyList<BorinKnowledgeEvidenceItem>> BuildMajorMudSpecialistEvidenceAsync(
        string repositoryRoot,
        string question,
        CancellationToken token)
    {
        if (_majorMud is null) return [];
        if (question.Contains("depend", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("dependency", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("trace quest", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("related to", StringComparison.OrdinalIgnoreCase))
        {
            var dependencies = await _majorMud.TraceDependenciesAsync(repositoryRoot, question, 3, token);
            if (dependencies.Count > 0)
            {
                return dependencies.Take(20).Select(entity => new BorinKnowledgeEvidenceItem(
                    entity.Provenance.RelativePath,
                    1,
                    1,
                    BorinKnowledgeAuthority.GeneratedClassic,
                    $"Dependency trace: {entity.Summary}",
                    175,
                    entity.Confidence,
                    [entity.Provenance],
                    string.Join("; ", entity.Relationships.Take(8)))).ToArray();
            }
        }
        if (question.Contains("require", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("restriction", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("who can", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("can i equip", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("can use", StringComparison.OrdinalIgnoreCase))
        {
            var requirements = await _majorMud.GetItemRequirementsAsync(repositoryRoot, question, token);
            if (requirements is not null)
            {
                var classes = requirements.AllowedClasses.Count == 0 ? "no decoded class list" : string.Join(", ", requirements.AllowedClasses);
                var races = requirements.AllowedRaces.Count == 0 ? "no decoded race list" : string.Join(", ", requirements.AllowedRaces);
                var stats = requirements.MinimumStats.Count == 0 ? "none decoded" : string.Join(", ", requirements.MinimumStats.Select(pair => $"{pair.Key} {pair.Value}"));
                return
                [
                    new BorinKnowledgeEvidenceItem(
                        requirements.Provenance.RelativePath,
                        1,
                        1,
                        BorinKnowledgeAuthority.GeneratedClassic,
                        $"Item eligibility: {requirements.ItemName} ({requirements.ItemId}); minimum level={requirements.MinimumLevel}; maximum level={(requirements.MaximumLevel == 0 ? "none decoded" : requirements.MaximumLevel)}; allowed classes={classes}; allowed races={races}; minimum stats={stats}. Boundary: {requirements.Boundary}",
                        190,
                        requirements.Confidence,
                        [requirements.Provenance],
                        "item -> level/class/race/stat eligibility")
                ];
            }
        }
        IReadOnlyList<BorinMajorMudEntity> entities;
        string traceKind;
        if (question.Contains("where", StringComparison.OrdinalIgnoreCase) &&
            (question.Contains("get", StringComparison.OrdinalIgnoreCase) ||
             question.Contains("buy", StringComparison.OrdinalIgnoreCase) ||
             question.Contains("drop", StringComparison.OrdinalIgnoreCase) ||
             question.Contains("obtain", StringComparison.OrdinalIgnoreCase)))
        {
            entities = await _majorMud.FindAcquisitionSourcesAsync(repositoryRoot, question, token);
            traceKind = "item acquisition";
        }
        else if (question.Contains("spawn", StringComparison.OrdinalIgnoreCase) ||
                 question.Contains("where is", StringComparison.OrdinalIgnoreCase) ||
                 question.Contains("where can i find", StringComparison.OrdinalIgnoreCase))
        {
            entities = await _majorMud.FindNpcSpawnsAsync(repositoryRoot, question, token);
            traceKind = "NPC spawn";
        }
        else
        {
            return [];
        }
        if (entities.Count == 0) return [];
        return entities.Take(12).Select(entity => new BorinKnowledgeEvidenceItem(
            entity.Provenance.RelativePath,
            entity.Provenance.StartLine,
            entity.Provenance.EndLine,
            BorinKnowledgeAuthority.GeneratedClassic,
            $"{traceKind}: {entity.Summary}",
            160,
            entity.Confidence,
            [entity.Provenance],
            string.Join("; ", entity.Relationships.Take(10)))).ToArray();
    }

    private async Task<IReadOnlyList<BorinKnowledgeEvidenceItem>> BuildRouteEvidenceAsync(
        string repositoryRoot,
        string question,
        CancellationToken token)
    {
        if (_majorMud is null ||
            !(question.Contains("path", StringComparison.OrdinalIgnoreCase) ||
              question.Contains("route", StringComparison.OrdinalIgnoreCase) ||
              question.Contains("travel", StringComparison.OrdinalIgnoreCase) ||
              question.Contains("get from", StringComparison.OrdinalIgnoreCase))) return [];
        var rooms = RoomReferencePattern.Matches(question).Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        if (rooms.Length != 2) return [];
        var route = await _majorMud.FindPathAsync(repositoryRoot, rooms[0], rooms[1], 150, token);
        if (!route.Found) return [];
        var steps = string.Join(Environment.NewLine, route.Steps.Select((step, index) =>
            $"{index + 1}. {step.Direction}: {step.FromRoom} -> {step.ToRoom} ({step.ExitId}, {step.Confidence.ToDisplayText()})"));
        return
        [
            new BorinKnowledgeEvidenceItem(
                "Data/Classic/exits.json",
                1,
                1,
                BorinKnowledgeAuthority.GeneratedClassic,
                $"Authoritative topology route {route.StartRoom} -> {route.DestinationRoom}: {route.Directions}\n{steps}\nBoundary: {route.Detail}",
                180,
                BorinKnowledgeConfidence.Proven,
                [new BorinKnowledgeProvenance("Generated Classic topology", "Data/Classic/exits.json", 1, 1, "current generated catalog")],
                $"{route.Steps.Count} directed exit relationships")
        ];
    }

    private static string ExpandSemanticQuery(BorinKnowledgeQuery query)
    {
        if (query.Mode == BorinKnowledgeSearchMode.Exact)
            return query.Text;
        var values = new HashSet<string>(Tokenize(query.Text), StringComparer.OrdinalIgnoreCase);
        foreach (var term in values.ToArray())
        {
            if (SemanticAliases.TryGetValue(term, out var aliases))
                values.UnionWith(aliases);
        }
        return string.Join(' ', values);
    }

    private async Task<IReadOnlyList<BorinKnowledgeEvidenceItem>> SearchGraphAsync(
        string repositoryRoot,
        BorinKnowledgeQuery query,
        int maximum,
        CancellationToken token)
    {
        var graph = await LoadGraphAsync(repositoryRoot, token);
        var rawTerms = Tokenize(query.Text);
        var searchTerms = new HashSet<string>(rawTerms, StringComparer.OrdinalIgnoreCase);
        if (query.Mode is BorinKnowledgeSearchMode.Hybrid or BorinKnowledgeSearchMode.Semantic)
        {
            foreach (var term in rawTerms)
                if (SemanticAliases.TryGetValue(term, out var aliases)) searchTerms.UnionWith(aliases);
        }
        if (searchTerms.Count == 0) return [];

        var results = new List<BorinKnowledgeEvidenceItem>();
        foreach (var symbol in graph.Symbols)
        {
            token.ThrowIfCancellationRequested();
            var exact = rawTerms.Any(term => symbol.Name.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                                             symbol.QualifiedName.Equals(term, StringComparison.OrdinalIgnoreCase));
            var nameHits = searchTerms.Count(term => symbol.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase));
            var related = symbol.References.Count(reference => searchTerms.Contains(reference));
            if (query.Mode == BorinKnowledgeSearchMode.Exact && !exact) continue;
            if (query.Mode == BorinKnowledgeSearchMode.Relationship && related == 0 && nameHits == 0) continue;
            if (!exact && nameHits == 0 && related == 0) continue;

            var score = (exact ? 80d : 0d) + nameHits * 9d + related * 5d;
            var relationship = query.Mode == BorinKnowledgeSearchMode.Relationship || related > 0
                ? $"{symbol.Kind} references: {string.Join(", ", symbol.References.Where(searchTerms.Contains).Take(8))}"
                : $"Roslyn {symbol.Kind} declaration";
            var provenance = new BorinKnowledgeProvenance(
                "RealmGate source",
                symbol.RelativePath,
                symbol.StartLine,
                symbol.EndLine,
                graph.RepositoryVersion);
            results.Add(new BorinKnowledgeEvidenceItem(
                symbol.RelativePath,
                symbol.StartLine,
                symbol.EndLine,
                BorinKnowledgeAuthority.Runtime,
                symbol.Signature,
                score,
                BorinKnowledgeConfidence.Proven,
                [provenance],
                relationship));
        }
        return results.OrderByDescending(result => result.Score).Take(maximum).ToArray();
    }

    private async Task BuildSymbolGraphAsync(string repositoryRoot, bool changedOnly, CancellationToken token)
    {
        var root = Path.GetFullPath(repositoryRoot);
        await _graphGate.WaitAsync(token);
        try
        {
            var path = GetGraphPath(root);
            var graph = changedOnly ? await LoadGraphAsync(root, token) : new SymbolGraphDocument();
            graph.Version = GraphVersion;
            graph.RepositoryRoot = root;
            var files = EnumerateSourceFiles(root).ToArray();
            var active = files.Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            graph.Files.RemoveAll(file => !active.Contains(file.RelativePath));
            graph.Symbols.RemoveAll(symbol => !active.Contains(symbol.RelativePath));

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                var info = new FileInfo(file);
                var existing = graph.Files.FirstOrDefault(item => item.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));
                if (existing is not null && existing.Length == info.Length && existing.LastWriteUtcTicks == info.LastWriteTimeUtc.Ticks)
                    continue;
                var text = await File.ReadAllTextAsync(file, token);
                var tree = CSharpSyntaxTree.ParseText(text, path: file, cancellationToken: token);
                var walker = new KnowledgeSyntaxWalker(relative, tree);
                walker.Visit(await tree.GetRootAsync(token));
                graph.Symbols.RemoveAll(symbol => symbol.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));
                graph.Symbols.AddRange(walker.Symbols);
                if (existing is not null) graph.Files.Remove(existing);
                graph.Files.Add(new SourceFileStamp(relative, info.Length, info.LastWriteTimeUtc.Ticks));
            }
            graph.RepositoryVersion = DateTimeOffset.UtcNow.ToString("O");
            graph.UpdatedUtc = DateTimeOffset.UtcNow;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(graph, JsonOptions), token);
            File.Move(temporary, path, true);
        }
        finally
        {
            _graphGate.Release();
        }
    }

    private void StartWatching(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        lock (_watchSync)
        {
            if (_watcher is not null && _watchedRoot.Equals(root, StringComparison.OrdinalIgnoreCase)) return;
            _watcher?.Dispose();
            _watchedRoot = root;
            _watcher = new FileSystemWatcher(root, "*.cs")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnRepositoryChanged;
            _watcher.Created += OnRepositoryChanged;
            _watcher.Deleted += OnRepositoryChanged;
            _watcher.Renamed += OnRepositoryChanged;
        }
    }

    private void OnRepositoryChanged(object sender, FileSystemEventArgs args)
    {
        var normalized = args.FullPath.Replace('\\', '/');
        if (normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Release/", StringComparison.OrdinalIgnoreCase)) return;
        lock (_watchSync)
        {
            _refreshDelay?.Cancel();
            _refreshDelay?.Dispose();
            _refreshDelay = new CancellationTokenSource();
            var token = _refreshDelay.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                    await BuildAsync(_watchedRoot, changedOnly: true, token);
                }
                catch (OperationCanceledException) { }
                catch { /* A watcher refresh is advisory; the explicit UI command remains available. */ }
            }, token);
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var value = path.Replace('\\', '/');
                return !value.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
                       !value.Contains("/obj/", StringComparison.OrdinalIgnoreCase) &&
                       !value.Contains("/.git/", StringComparison.OrdinalIgnoreCase) &&
                       !value.Contains("/Release/", StringComparison.OrdinalIgnoreCase) &&
                       !value.Contains("/Backups/", StringComparison.OrdinalIgnoreCase);
            });

    private static HashSet<string> Tokenize(string value) =>
        Terms.Matches(value ?? string.Empty).Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string GetGraphPath(string root) =>
        Path.Combine(root, "AI", "Memory", "Knowledge", "borin-reforged-symbols.json");

    private static async Task<SymbolGraphDocument> LoadGraphAsync(string root, CancellationToken token)
    {
        var path = GetGraphPath(Path.GetFullPath(root));
        if (!File.Exists(path)) return new SymbolGraphDocument { RepositoryRoot = root };
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<SymbolGraphDocument>(stream, JsonOptions, token)
                   ?? new SymbolGraphDocument { RepositoryRoot = root };
        }
        catch (JsonException)
        {
            return new SymbolGraphDocument { RepositoryRoot = root };
        }
    }

    public void Dispose()
    {
        lock (_watchSync)
        {
            _refreshDelay?.Cancel();
            _refreshDelay?.Dispose();
            _watcher?.Dispose();
            _refreshDelay = null;
            _watcher = null;
        }
        _graphGate.Dispose();
    }

    private sealed class KnowledgeSyntaxWalker(string relativePath, SyntaxTree tree) : CSharpSyntaxWalker
    {
        public List<SourceSymbol> Symbols { get; } = [];

        public override void VisitClassDeclaration(ClassDeclarationSyntax node) { Add(node, node.Identifier.Text, "class"); base.VisitClassDeclaration(node); }
        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) { Add(node, node.Identifier.Text, "interface"); base.VisitInterfaceDeclaration(node); }
        public override void VisitRecordDeclaration(RecordDeclarationSyntax node) { Add(node, node.Identifier.Text, "record"); base.VisitRecordDeclaration(node); }
        public override void VisitStructDeclaration(StructDeclarationSyntax node) { Add(node, node.Identifier.Text, "struct"); base.VisitStructDeclaration(node); }
        public override void VisitEnumDeclaration(EnumDeclarationSyntax node) { Add(node, node.Identifier.Text, "enum"); base.VisitEnumDeclaration(node); }
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node) { Add(node, node.Identifier.Text, "method"); base.VisitMethodDeclaration(node); }
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node) { Add(node, node.Identifier.Text, "property"); base.VisitPropertyDeclaration(node); }
        public override void VisitEventDeclaration(EventDeclarationSyntax node) { Add(node, node.Identifier.Text, "event"); base.VisitEventDeclaration(node); }

        private void Add(MemberDeclarationSyntax node, string name, string kind)
        {
            var span = tree.GetLineSpan(node.Span);
            var owner = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;
            var space = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                        ?? node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                        ?? string.Empty;
            var qualified = string.Join('.', new[] { space, owner, name }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var references = node.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Select(identifier => identifier.Identifier.Text)
                .Where(identifier => !identifier.Equals(name, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Take(160)
                .ToList();
            var signature = node switch
            {
                MethodDeclarationSyntax method => method.WithBody(null).WithExpressionBody(null).WithSemicolonToken(default).NormalizeWhitespace().ToFullString(),
                PropertyDeclarationSyntax property => $"{property.Modifiers} {property.Type} {property.Identifier}",
                _ => node.GetFirstToken().Parent?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault()?.ToString().Split('{')[0].Trim() ?? $"{kind} {name}",
            };
            Symbols.Add(new SourceSymbol(relativePath, span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1,
                kind, name, qualified, signature, $"{qualified} {kind} {string.Join(' ', references)}", references));
        }
    }

    private sealed class SymbolGraphDocument
    {
        public int Version { get; set; } = GraphVersion;
        public string RepositoryRoot { get; set; } = string.Empty;
        public string RepositoryVersion { get; set; } = string.Empty;
        public DateTimeOffset UpdatedUtc { get; set; }
        public List<SourceFileStamp> Files { get; set; } = [];
        public List<SourceSymbol> Symbols { get; set; } = [];
    }

    private sealed record SourceFileStamp(string RelativePath, long Length, long LastWriteUtcTicks);
    private sealed record SourceSymbol(string RelativePath, int StartLine, int EndLine, string Kind, string Name,
        string QualifiedName, string Signature, string SearchText, List<string> References);
}
