using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

public sealed record LibrariumRuntimeBundleResult(
    string OutputPath,
    int FamilyCount,
    int ObjectCount,
    long CompressedBytes,
    IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

/// <summary>
/// Compact public-release representation of the authoritative Librarium object projection.
/// The private repository keeps the full audit/evidence tree. Public installs carry this bundle
/// plus writable WorldOverrides and the promoted room-description records required by generation.
/// </summary>
public static class LibrariumRuntimeBundle
{
    public const string FileName = "RealmGate.librarium";
    private const string ManifestEntryName = "manifest.json";
    private const string FamilyPrefix = "Objects/";

    public static string ResolvePath(string librariumRoot) => Path.Combine(librariumRoot, FileName);

    public static LibrariumRuntimeBundleResult Create(string repositoryRoot, string outputPath)
    {
        var diagnostics = new List<CompilerDiagnostic>();
        var librariumRoot = Path.Combine(repositoryRoot, "Librarium");
        var families = new LibrariumCatalogReader()
            .ReadFamilies(librariumRoot, diagnostics)
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            return new(outputPath, 0, 0, 0, diagnostics);
        if (families.Length == 0)
        {
            diagnostics.Add(new(
                DiagnosticSeverity.Error,
                "LIBBUNDLE001",
                "No authoritative Librarium families were available to bundle.",
                librariumRoot));
            return new(outputPath, 0, 0, 0, diagnostics);
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        if (File.Exists(fullOutputPath)) File.Delete(fullOutputPath);

        using (var archive = ZipFile.Open(fullOutputPath, ZipArchiveMode.Create))
        {
            var manifest = new JsonObject
            {
                ["schema"] = "realmgate_runtime_librarium_bundle_v1",
                ["format_version"] = 1,
                ["family_count"] = families.Length,
                ["object_count"] = families.Sum(family => family.Records.Count),
                ["source"] = "authoritative_librarium_projection",
                ["data_policy"] = "Data is disposable output generated from this bundle"
            };
            WriteJsonEntry(archive, ManifestEntryName, manifest);

            foreach (var family in families)
            {
                var sourceFiles = family.SourceFiles is { Count: > 0 }
                    ? family.SourceFiles
                    : new[] { family.SourceFile };
                var root = new JsonObject
                {
                    ["schema"] = "realmgate_runtime_librarium_family_v1",
                    ["family"] = family.Name,
                    ["record_count"] = family.Records.Count,
                    ["source_files"] = new JsonArray(sourceFiles
                        .Select(path => JsonValue.Create(Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/')))
                        .ToArray()),
                    ["records"] = new JsonArray(family.Records
                        .Select(record => record.DeepClone())
                        .ToArray())
                };
                WriteJsonEntry(archive, $"{FamilyPrefix}{family.Name}.json", root);
            }
        }

        var fileInfo = new FileInfo(fullOutputPath);
        diagnostics.Add(new(
            DiagnosticSeverity.Info,
            "LIBBUNDLE100",
            $"Created compact runtime Librarium with {families.Length:N0} families and {families.Sum(family => family.Records.Count):N0} objects.",
            fullOutputPath));
        return new(
            fullOutputPath,
            families.Length,
            families.Sum(family => family.Records.Count),
            fileInfo.Length,
            diagnostics);
    }

    public static IReadOnlyList<LibrariumFamily>? TryRead(
        string librariumRoot,
        List<CompilerDiagnostic> diagnostics)
    {
        var bundlePath = ResolvePath(librariumRoot);
        if (!File.Exists(bundlePath)) return null;

        try
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            var manifestEntry = archive.GetEntry(ManifestEntryName)
                ?? throw new InvalidDataException("Runtime Librarium bundle manifest was not found.");
            using (var manifestStream = manifestEntry.Open())
            {
                var manifest = JsonNode.Parse(manifestStream) as JsonObject
                    ?? throw new InvalidDataException("Runtime Librarium bundle manifest is invalid.");
                if (!string.Equals(
                        manifest["schema"]?.GetValue<string>(),
                        "realmgate_runtime_librarium_bundle_v1",
                        StringComparison.Ordinal))
                    throw new InvalidDataException("Runtime Librarium bundle schema is not supported.");
            }

            var families = new List<LibrariumFamily>();
            foreach (var entry in archive.Entries
                         .Where(entry => entry.FullName.StartsWith(FamilyPrefix, StringComparison.OrdinalIgnoreCase) &&
                                         entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                                         entry.FullName.Count(character => character == '/') == 1)
                         .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase))
            {
                using var stream = entry.Open();
                var root = JsonNode.Parse(stream) as JsonObject
                    ?? throw new InvalidDataException($"Runtime Librarium family entry '{entry.FullName}' is invalid.");
                var familyName = root["family"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(familyName))
                    familyName = Path.GetFileNameWithoutExtension(entry.Name);
                if (root["records"] is not JsonArray recordsArray)
                    throw new InvalidDataException($"Runtime Librarium family '{familyName}' has no records array.");

                var records = recordsArray
                    .OfType<JsonObject>()
                    .Select(record => (JsonObject)record.DeepClone())
                    .ToArray();
                var source = $"{bundlePath}!/{entry.FullName}";
                families.Add(new LibrariumFamily(familyName, source, records, new[] { source }));
            }

            if (families.Count == 0)
                throw new InvalidDataException("Runtime Librarium bundle contains no object families.");

            diagnostics.Add(new(
                DiagnosticSeverity.Info,
                "LIBBUNDLE101",
                $"Loaded {families.Count:N0} authoritative families directly from compact runtime Librarium bundle.",
                bundlePath));
            return families;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            diagnostics.Add(new(
                DiagnosticSeverity.Error,
                "LIBBUNDLE002",
                $"Could not read compact runtime Librarium bundle: {ex.Message}",
                bundlePath));
            return Array.Empty<LibrariumFamily>();
        }
    }

    private static void WriteJsonEntry(ZipArchive archive, string entryName, JsonNode node)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
        using var stream = entry.Open();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        node.WriteTo(writer);
    }
}
