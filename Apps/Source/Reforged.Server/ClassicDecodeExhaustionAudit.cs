using System.Text;
using System.Text.Json;

namespace Reforged.Server;

public sealed record ClassicDecodeFieldCoverage(
    string Family,
    string JsonPath,
    string FieldName,
    int OccurrenceCount,
    int NonNullCount,
    IReadOnlyList<string> ValueKinds,
    int EvidenceReferenceCount,
    int RuntimeConsumerCount,
    string Status,
    IReadOnlyList<string> RuntimeConsumers,
    IReadOnlyList<string> Diagnostics);

public sealed record ClassicDecodeFamilyCoverage(
    string Family,
    string DataFile,
    int RecordCount,
    int FieldCount,
    int ConnectedFieldCount,
    int DisconnectedFieldCount,
    int EvidenceFileCount,
    int RuntimeConsumerCount,
    int PendingMarkerCount,
    int UnresolvedMarkerCount,
    string Status,
    IReadOnlyList<string> RuntimeConsumers,
    IReadOnlyList<string> Diagnostics);

public sealed record ClassicDecodeExhaustionReport(
    DateTimeOffset GeneratedAtUtc,
    string ProjectRoot,
    int FamilyCount,
    int ConnectedFamilyCount,
    int DisconnectedFamilyCount,
    int PendingFamilyCount,
    int TotalClassicRecords,
    int TotalFields,
    int ConnectedFieldCount,
    int DisconnectedFieldCount,
    int TotalEvidenceFiles,
    IReadOnlyList<ClassicDecodeFamilyCoverage> Families,
    IReadOnlyList<ClassicDecodeFieldCoverage> Fields,
    IReadOnlyList<string> Diagnostics)
{
    public bool Passed => Diagnostics.Count == 0 && DisconnectedFamilyCount == 0 && PendingFamilyCount == 0 && DisconnectedFieldCount == 0;
}

/// <summary>
/// Repository-wide completion gate for the Classic data-driven runtime.
/// A generated JSON file is not considered complete merely because it exists.
/// Every family and every generated scalar field must be traceable to evidence
/// and to a live runtime consumer, or be explicitly classified as structural
/// metadata/provenance.
/// </summary>
public static class ClassicDecodeExhaustionAudit
{
    private static readonly string[] PendingMarkers =
    {
        "pending_generator_mapping", "pending_runtime_mapping", "pending", "cataloged_for_export",
        "evidence_mined_requires_parser_confirmation", "probe", "unscored", "quarantined"
    };

    private static readonly string[] UnresolvedMarkers =
    {
        "unresolved", "unknown", "opaque", "requires_native", "native-dependent", "not_mapped"
    };

    private static readonly HashSet<string> AuditOnlyFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClassicDecodeExhaustionAudit.cs", "ClassicRuntimeAudit.cs", "ClassicParityAudit.cs",
        "GeneratedWorldSmokeReport.cs", "RuntimeLivingWorldSmokeReport.cs"
    };

    private static readonly HashSet<string> StructuralFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "legacy_id", "legacyId", "record_id", "recordId", "key", "name", "title", "type", "kind",
        "schema", "schema_version", "schemaVersion", "version", "family", "source", "source_file", "sourceFile",
        "source_path", "sourcePath", "source_offset", "sourceOffset", "provenance", "confidence", "status",
        "notes", "diagnostics", "record_count", "recordCount", "generated_at", "generatedAt", "generated_at_utc",
        "generatedAtUtc", "generated_by", "generatedBy", "compiler_code_update", "compilerCodeUpdate", "object_type", "objectType",
        "manifest", "metadata", "description", "display_name", "displayName", "exports_to", "exportsTo", "generated_only",
        "generatedOnly", "runtime_reads_generated_data", "runtimeReadsGeneratedData", "promotion_code_update", "promotionCodeUpdate",
        "override_policy", "overridePolicy", "impl_status", "implStatus", "dll_notes", "dllNotes"
    };

    public static ClassicDecodeExhaustionReport Run(string projectRoot)
    {
        var diagnostics = new List<string>();
        var dataRoot = Path.Combine(projectRoot, "Data", "Classic");
        var evidenceRoot = Path.Combine(projectRoot, "Evidence");
        var serverSourceRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        var runtimeSourceRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server.Runtime");

        if (!Directory.Exists(dataRoot)) diagnostics.Add($"Missing Classic data directory: {dataRoot}");
        if (!Directory.Exists(evidenceRoot)) diagnostics.Add($"Missing evidence directory: {evidenceRoot}");
        if (!Directory.Exists(serverSourceRoot)) diagnostics.Add($"Missing server source directory: {serverSourceRoot}");

        if (diagnostics.Count > 0)
        {
            return new ClassicDecodeExhaustionReport(
                DateTimeOffset.UtcNow, projectRoot, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                Array.Empty<ClassicDecodeFamilyCoverage>(), Array.Empty<ClassicDecodeFieldCoverage>(), diagnostics);
        }

        var sourceRoots = new[] { serverSourceRoot, runtimeSourceRoot }.Where(Directory.Exists).ToArray();
        var sourceFiles = sourceRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !AuditOnlyFiles.Contains(Path.GetFileName(path)))
            .Select(path => new SourceText(path, File.ReadAllText(path)))
            .ToArray();

        var evidenceFiles = Directory.EnumerateFiles(evidenceRoot, "*.json", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(evidenceRoot, "*.md", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(evidenceRoot, "*.txt", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var evidenceTexts = evidenceFiles.Select(path => new SourceText(path, SafeReadAllText(path))).ToArray();

        var families = new List<ClassicDecodeFamilyCoverage>();
        var fields = new List<ClassicDecodeFieldCoverage>();

        foreach (var dataFile in Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName))
        {
            var family = Path.GetFileNameWithoutExtension(dataFile);
            var fileName = Path.GetFileName(dataFile);
            var localDiagnostics = new List<string>();
            int recordCount;
            int pendingCount;
            int unresolvedCount;
            IReadOnlyList<FieldAccumulator> familyFields;

            try
            {
                var text = File.ReadAllText(dataFile);
                using var document = JsonDocument.Parse(text);
                recordCount = CountRecords(document.RootElement);
                pendingCount = CountMarkers(text, PendingMarkers);
                unresolvedCount = CountMarkers(text, UnresolvedMarkers);
                familyFields = EnumerateFields(document.RootElement);
            }
            catch (Exception exception)
            {
                recordCount = 0;
                pendingCount = 0;
                unresolvedCount = 0;
                familyFields = Array.Empty<FieldAccumulator>();
                localDiagnostics.Add($"JSON parse failed: {exception.Message}");
            }

            var runtimeConsumers = sourceFiles
                .Where(source => MentionsFamily(source.Text, family, fileName))
                .Select(source => Path.GetRelativePath(projectRoot, source.Path).Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToArray();

            var evidenceAliases = BuildAliases(family);
            var matchingEvidence = evidenceFiles.Count(path => evidenceAliases.Any(alias =>
                Path.GetFileName(path).Contains(alias, StringComparison.OrdinalIgnoreCase) ||
                Path.GetDirectoryName(path)?.Contains(alias, StringComparison.OrdinalIgnoreCase) == true));

            foreach (var field in familyFields)
            {
                var consumerFiles = FindFieldConsumers(projectRoot, sourceFiles, field.FieldName);
                var evidenceReferenceCount = CountEvidenceReferences(evidenceTexts, field.FieldName);
                var fieldDiagnostics = new List<string>();
                var structural = StructuralFields.Contains(field.FieldName) || IsStructuralPath(field.JsonPath);

                if (field.NonNullCount == 0) fieldDiagnostics.Add("Field is present but null in all sampled/generated occurrences.");
                if (!structural && evidenceReferenceCount == 0) fieldDiagnostics.Add("No evidence reference was found for this generated field name.");
                if (!structural && consumerFiles.Count == 0) fieldDiagnostics.Add("No live runtime consumer was found for this generated field.");

                var fieldStatus = structural
                    ? "structural-metadata"
                    : consumerFiles.Count == 0
                        ? "generated-unconsumed"
                        : evidenceReferenceCount == 0
                            ? "consumed-without-field-evidence"
                            : field.NonNullCount == 0
                                ? "connected-empty"
                                : "connected";

                fields.Add(new ClassicDecodeFieldCoverage(
                    family,
                    field.JsonPath,
                    field.FieldName,
                    field.OccurrenceCount,
                    field.NonNullCount,
                    field.ValueKinds.OrderBy(value => value).ToArray(),
                    evidenceReferenceCount,
                    consumerFiles.Count,
                    fieldStatus,
                    consumerFiles,
                    fieldDiagnostics));
            }

            var familyCoverage = fields.Where(item => string.Equals(item.Family, family, StringComparison.OrdinalIgnoreCase)).ToArray();
            var connectedFields = familyCoverage.Count(item => item.Status is "connected" or "structural-metadata");
            var disconnectedFields = familyCoverage.Count(item => item.Status is "generated-unconsumed" or "consumed-without-field-evidence");

            if (recordCount == 0) localDiagnostics.Add("No generated records were detected.");
            if (matchingEvidence == 0) localDiagnostics.Add("No family-specific evidence file was located by name/path.");
            if (runtimeConsumers.Length == 0) localDiagnostics.Add("No live runtime consumer was found.");
            if (disconnectedFields > 0) localDiagnostics.Add($"{disconnectedFields} generated fields are not fully connected.");

            var status = localDiagnostics.Any(item => item.Contains("parse failed", StringComparison.OrdinalIgnoreCase))
                ? "invalid"
                : runtimeConsumers.Length == 0 || disconnectedFields > 0
                    ? "disconnected"
                    : pendingCount > 0 || unresolvedCount > 0
                        ? "connected-with-unresolved-records"
                        : "connected";

            families.Add(new ClassicDecodeFamilyCoverage(
                family,
                Path.GetRelativePath(projectRoot, dataFile).Replace('\\', '/'),
                recordCount,
                familyCoverage.Length,
                connectedFields,
                disconnectedFields,
                matchingEvidence,
                runtimeConsumers.Length,
                pendingCount,
                unresolvedCount,
                status,
                runtimeConsumers,
                localDiagnostics));
        }

        return new ClassicDecodeExhaustionReport(
            DateTimeOffset.UtcNow,
            projectRoot,
            families.Count,
            families.Count(item => item.Status == "connected"),
            families.Count(item => item.Status is "disconnected" or "invalid"),
            families.Count(item => item.Status == "connected-with-unresolved-records"),
            families.Sum(item => item.RecordCount),
            fields.Count,
            fields.Count(item => item.Status is "connected" or "structural-metadata"),
            fields.Count(item => item.Status is "generated-unconsumed" or "consumed-without-field-evidence"),
            evidenceFiles.Length,
            families,
            fields,
            diagnostics);
    }

    public static string RenderMarkdown(ClassicDecodeExhaustionReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Classic Decode Exhaustion Audit");
        builder.AppendLine();
        builder.AppendLine($"Generated: {report.GeneratedAtUtc:O}");
        builder.AppendLine($"Project: `{report.ProjectRoot}`");
        builder.AppendLine();
        builder.AppendLine($"- Families: {report.FamilyCount}");
        builder.AppendLine($"- Fully connected families: {report.ConnectedFamilyCount}");
        builder.AppendLine($"- Disconnected/invalid families: {report.DisconnectedFamilyCount}");
        builder.AppendLine($"- Families with unresolved records: {report.PendingFamilyCount}");
        builder.AppendLine($"- Generated records: {report.TotalClassicRecords}");
        builder.AppendLine($"- Generated scalar fields: {report.TotalFields}");
        builder.AppendLine($"- Connected/structural fields: {report.ConnectedFieldCount}");
        builder.AppendLine($"- Disconnected fields: {report.DisconnectedFieldCount}");
        builder.AppendLine($"- Evidence files scanned: {report.TotalEvidenceFiles}");
        builder.AppendLine();
        builder.AppendLine("| Family | Records | Fields | Connected fields | Disconnected fields | Evidence | Runtime consumers | Pending | Unresolved | Status |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var family in report.Families)
        {
            builder.AppendLine($"| {family.Family} | {family.RecordCount} | {family.FieldCount} | {family.ConnectedFieldCount} | {family.DisconnectedFieldCount} | {family.EvidenceFileCount} | {family.RuntimeConsumerCount} | {family.PendingMarkerCount} | {family.UnresolvedMarkerCount} | {family.Status} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Disconnected fields");
        foreach (var field in report.Fields.Where(item => item.Status is "generated-unconsumed" or "consumed-without-field-evidence")
                     .OrderBy(item => item.Family).ThenBy(item => item.JsonPath))
        {
            builder.AppendLine($"- `{field.Family}:{field.JsonPath}` — {field.Status}; occurrences={field.OccurrenceCount}; non-null={field.NonNullCount}; evidence={field.EvidenceReferenceCount}; consumers={field.RuntimeConsumerCount}");
        }

        builder.AppendLine();
        builder.AppendLine("## Disconnected or unresolved families");
        foreach (var family in report.Families.Where(item => item.Status != "connected"))
        {
            builder.AppendLine();
            builder.AppendLine($"### {family.Family}");
            builder.AppendLine($"Data: `{family.DataFile}`");
            if (family.RuntimeConsumers.Count > 0)
            {
                builder.AppendLine("Runtime consumers:");
                foreach (var consumer in family.RuntimeConsumers) builder.AppendLine($"- `{consumer}`");
            }
            foreach (var diagnostic in family.Diagnostics) builder.AppendLine($"- {diagnostic}");
        }

        if (report.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Audit diagnostics");
            foreach (var diagnostic in report.Diagnostics) builder.AppendLine($"- {diagnostic}");
        }

        return builder.ToString();
    }

    public static string ToJson(ClassicDecodeExhaustionReport report) => JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

    private static int CountRecords(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.GetArrayLength();
        if (root.ValueKind != JsonValueKind.Object) return 0;

        var preferred = new[] { "records", "items", "rooms", "exits", "npcs", "spawns", "commands", "formulas", "relationships", "definitions", "entries" };
        foreach (var name in preferred)
        {
            if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Array) return property.GetArrayLength();
        }

        var arrays = root.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.Array).ToArray();
        return arrays.Length == 0 ? 1 : arrays.Sum(property => property.Value.GetArrayLength());
    }

    private static IReadOnlyList<FieldAccumulator> EnumerateFields(JsonElement root)
    {
        var map = new Dictionary<string, MutableFieldAccumulator>(StringComparer.OrdinalIgnoreCase);
        Walk(root, "$", map);
        return map.Values.Select(value => new FieldAccumulator(
            value.JsonPath,
            value.FieldName,
            value.OccurrenceCount,
            value.NonNullCount,
            value.ValueKinds)).OrderBy(value => value.JsonPath).ToArray();
    }

    private static void Walk(JsonElement element, string path, IDictionary<string, MutableFieldAccumulator> map)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        Walk(property.Value, propertyPath, map);
                    }
                    else
                    {
                        if (!map.TryGetValue(propertyPath, out var accumulator))
                        {
                            accumulator = new MutableFieldAccumulator(propertyPath, property.Name);
                            map[propertyPath] = accumulator;
                        }
                        accumulator.OccurrenceCount++;
                        if (property.Value.ValueKind != JsonValueKind.Null) accumulator.NonNullCount++;
                        accumulator.ValueKinds.Add(property.Value.ValueKind.ToString());
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Walk(item, path + "[*]", map);
                break;
        }
    }

    private static IReadOnlyList<string> FindFieldConsumers(string projectRoot, IEnumerable<SourceText> sources, string fieldName)
    {
        if (fieldName.Length < 3) return Array.Empty<string>();
        var pascal = ToPascalCase(fieldName);
        var quoted = "\"" + fieldName + "\"";
        return sources.Where(source =>
                source.Text.Contains(quoted, StringComparison.OrdinalIgnoreCase) ||
                (pascal.Length >= 3 && source.Text.Contains(pascal, StringComparison.Ordinal)))
            .Select(source => Path.GetRelativePath(projectRoot, source.Path).Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();
    }

    private static int CountEvidenceReferences(IEnumerable<SourceText> evidenceTexts, string fieldName)
    {
        if (fieldName.Length < 3) return 0;
        var pascal = ToPascalCase(fieldName);
        return evidenceTexts.Count(source =>
            source.Text.Contains(fieldName, StringComparison.OrdinalIgnoreCase) ||
            (pascal.Length >= 3 && source.Text.Contains(pascal, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsStructuralPath(string jsonPath) =>
        jsonPath.Contains(".metadata.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".provenance.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".manifest.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence_refs", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence_summary", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence[", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".generator_mapping", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".librarium_enrichment", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".relationship_summary", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".override_policy", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".runtime_mapping", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".runtimeMapping", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".source.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".source_", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".public_reference", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".mmexplorer_sql_reference", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".compiler_projection", StringComparison.OrdinalIgnoreCase);

    private static int CountMarkers(string text, IEnumerable<string> markers) => markers.Sum(marker => CountOccurrences(text, marker));

    private static int CountOccurrences(string text, string marker)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += marker.Length;
        }
        return count;
    }

    private static bool MentionsFamily(string source, string family, string fileName)
    {
        if (source.Contains(fileName, StringComparison.OrdinalIgnoreCase)) return true;
        var pascal = ToPascalCase(family);
        return pascal.Length >= 5 && source.Contains(pascal, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] BuildAliases(string family)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { family, family.Replace("_", string.Empty), family.Replace("_", "-") };
        foreach (var part in family.Split('_', StringSplitOptions.RemoveEmptyEntries)) if (part.Length >= 4) aliases.Add(part);
        return aliases.ToArray();
    }

    private static string ToPascalCase(string value) => string.Concat(value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .Where(part => part.Length > 0)
        .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private static string SafeReadAllText(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return string.Empty; }
    }

    private sealed record SourceText(string Path, string Text);
    private sealed record FieldAccumulator(string JsonPath, string FieldName, int OccurrenceCount, int NonNullCount, IReadOnlyCollection<string> ValueKinds);
    private sealed class MutableFieldAccumulator
    {
        public MutableFieldAccumulator(string jsonPath, string fieldName) { JsonPath = jsonPath; FieldName = fieldName; }
        public string JsonPath { get; }
        public string FieldName { get; }
        public int OccurrenceCount { get; set; }
        public int NonNullCount { get; set; }
        public HashSet<string> ValueKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
