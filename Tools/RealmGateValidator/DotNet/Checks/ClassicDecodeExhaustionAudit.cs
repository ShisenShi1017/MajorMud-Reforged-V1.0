using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

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
    public bool Passed => Diagnostics.Count == 0
        && DisconnectedFamilyCount == 0
        && PendingFamilyCount == 0
        && DisconnectedFieldCount == 0
        && ConnectedFamilyCount == FamilyCount
        && ConnectedFieldCount == TotalFields;
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
        "GeneratedWorldSmokeReport.cs", "RuntimeLivingWorldSmokeReport.cs",
        "RuntimeClassicDataReconstructionSmokeReport.cs"
    };

    private static readonly HashSet<string> GeneratedIndexFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "quest_narrative_roots.json",
        "alignment_quest_narrative_paths.json",
        "gnome_merchant_narrative_placement.json",
        "gnome_merchant_scroll_offerings.json",
        "witchunter_magebane_narrative.json",
        "world_reward_narrative_authority.json",
        "random_table_runtime_authority.json",
        "event_mission_runtime_authority.json",
        "reward_execution_authority.json",
        "narrative_persistence_authority.json",
        "combat_spells.json"
    };

    private static readonly HashSet<string> StructuralFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "legacy_id", "legacyId", "record_id", "recordId", "key", "name", "title", "type", "kind",
        "schema", "schema_version", "schemaVersion", "version", "family", "source", "source_file", "sourceFile",
        "source_path", "sourcePath", "source_offset", "sourceOffset", "provenance", "confidence", "status",
        "record_kind", "resolution_boundary", "canonical_status", "root_textblock_ref",
        "next_textblock_ref", "target_textblock_ref", "ability_ref", "selection_value",
        "notes", "diagnostics", "record_count", "recordCount", "generated_at", "generatedAt", "generated_at_utc",
        "generatedAtUtc", "generated_by", "generatedBy", "compiler_code_update", "compilerCodeUpdate", "object_type", "objectType",
        "manifest", "metadata", "description", "display_name", "displayName", "exports_to", "exportsTo", "generated_only",
        "generatedOnly", "runtime_reads_generated_data", "runtimeReadsGeneratedData", "promotion_code_update", "promotionCodeUpdate",
        "override_policy", "overridePolicy", "impl_status", "implStatus", "dll_notes", "dllNotes"
        ,"authority", "world_id", "worldId", "projection", "client_rule", "families_available",
        "code_update", "compiler", "librarium_authority", "object_families_compiled", "objects_compiled",
        "files_written", "source_files", "field_inventory", "severity", "message", "path",
        "output_root", "runtime_loader_contract", "source_of_truth", "generated_output",
        "base_world_id", "standalone_runtime", "runtime_data_is_complete", "live_classic_fallback_allowed",
        "record_parts", "chunked_records", "runtime_projection"
    };

    public static ClassicDecodeExhaustionReport Run(string projectRoot)
    {
        var diagnostics = new List<string>();
        var dataRoot = Path.Combine(projectRoot, "Data", "Classic");
        var librariumObjectsRoot = Path.Combine(projectRoot, "Librarium", "Objects");
        var serverSourceRoot = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server");
        var runtimeSourceRoot = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server.Runtime");
        var clientCoreSourceRoot = Path.Combine(projectRoot, "Core", "Source", "RealmGate.Client.Core");
        var desktopSourceRoot = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Desktop");
        var validationSourceRoot = Path.Combine(projectRoot, "Tools", "RealmGateValidator", "DotNet", "Checks");

        if (!Directory.Exists(dataRoot)) diagnostics.Add($"Missing Classic data directory: {dataRoot}");
        if (!Directory.Exists(serverSourceRoot)) diagnostics.Add($"Missing server source directory: {serverSourceRoot}");

        if (diagnostics.Count > 0)
        {
            return new ClassicDecodeExhaustionReport(
                DateTimeOffset.UtcNow, projectRoot, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                Array.Empty<ClassicDecodeFamilyCoverage>(), Array.Empty<ClassicDecodeFieldCoverage>(), diagnostics);
        }

        var sourceRoots = new[]
        {
            serverSourceRoot,
            runtimeSourceRoot,
            clientCoreSourceRoot,
            desktopSourceRoot,
            validationSourceRoot
        }.Where(Directory.Exists).ToArray();
        var sourceFiles = sourceRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !AuditOnlyFiles.Contains(Path.GetFileName(path)))
            .Select(path => new SourceText(path, File.ReadAllText(path)))
            .ToArray();

        var evidenceRoots = new[] { librariumObjectsRoot }
            .Where(Directory.Exists)
            .ToArray();
        var evidenceFiles = evidenceRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(path =>
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var evidenceTexts = evidenceFiles.Select(path => new SourceText(path, SafeReadAllText(path))).ToArray();
        var fieldConsumerCache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var evidenceReferenceCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var families = new List<ClassicDecodeFamilyCoverage>();
        var fields = new List<ClassicDecodeFieldCoverage>();

        foreach (var dataFile in Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.TopDirectoryOnly)
                     .Where(path => !GeneratedIndexFiles.Contains(Path.GetFileName(path)))
                     .OrderBy(Path.GetFileName))
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
                var generated = ReadGeneratedFamily(dataFile);
                recordCount = generated.RecordCount;
                pendingCount = generated.PendingMarkerCount;
                unresolvedCount = generated.UnresolvedMarkerCount;
                familyFields = generated.Fields;
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
                if (!fieldConsumerCache.TryGetValue(field.FieldName, out var consumerFiles))
                {
                    consumerFiles = FindFieldConsumers(projectRoot, sourceFiles, field.FieldName);
                    fieldConsumerCache[field.FieldName] = consumerFiles;
                }
                var explicitRuntimeProof = ExplicitRuntimeConsumers(family, field.JsonPath);
                if (explicitRuntimeProof.Count > 0)
                    consumerFiles = consumerFiles.Concat(explicitRuntimeProof).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (!evidenceReferenceCache.TryGetValue(field.FieldName, out var evidenceReferenceCount))
                {
                    evidenceReferenceCount = CountEvidenceReferences(evidenceTexts, field.FieldName);
                    evidenceReferenceCache[field.FieldName] = evidenceReferenceCount;
                }
                var fieldEvidenceAliases = ExplicitEvidenceAliases(family, field.JsonPath);
                if (evidenceReferenceCount == 0 && fieldEvidenceAliases.Count > 0)
                    evidenceReferenceCount = CountEvidenceReferences(evidenceTexts, fieldEvidenceAliases);
                var fieldDiagnostics = new List<string>();
                var structural = StructuralFields.Contains(field.FieldName) ||
                                 IsNumericObjectKey(field.FieldName) ||
                                 IsStructuralPath(field.JsonPath);

                if (field.NonNullCount == 0) fieldDiagnostics.Add("Field is present but null in all sampled/generated occurrences.");
                if (!structural && evidenceReferenceCount == 0 && explicitRuntimeProof.Count == 0) fieldDiagnostics.Add("No evidence reference was found for this generated field name.");
                if (!structural && consumerFiles.Count == 0) fieldDiagnostics.Add("No live runtime consumer was found for this generated field.");
                if (explicitRuntimeProof.Count > 0) fieldDiagnostics.Add("Exact runtime consumer proof overrides the generic field-name scanner.");
                if (fieldEvidenceAliases.Count > 0 && evidenceReferenceCount > 0) fieldDiagnostics.Add("Exact compiler-source evidence alias overrides the generated field-name scanner.");

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
            var connectedFields = familyCoverage.Count(item => IsConnectedFieldStatus(item.Status));
            var disconnectedFields = familyCoverage.Count(item => item.Status is "generated-unconsumed" or "consumed-without-field-evidence");
            var structuralOnly = familyCoverage.Length > 0 && familyCoverage.All(item => item.Status == "structural-metadata");

            if (recordCount == 0) localDiagnostics.Add("No generated records were detected.");
            if (matchingEvidence == 0) localDiagnostics.Add("No family-specific evidence file was located by name/path.");
            if (runtimeConsumers.Length == 0 && !structuralOnly) localDiagnostics.Add("No live runtime consumer was found.");
            if (structuralOnly) localDiagnostics.Add("Structural contract family; no gameplay runtime consumer is required.");
            if (disconnectedFields > 0) localDiagnostics.Add($"{disconnectedFields} generated fields are not fully connected.");

            var status = localDiagnostics.Any(item => item.Contains("parse failed", StringComparison.OrdinalIgnoreCase))
                ? "invalid"
                : structuralOnly
                    ? "connected-structural"
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
            families.Count(item => item.Status is "connected" or "connected-structural"),
            families.Count(item => item.Status is "disconnected" or "invalid"),
            families.Count(item => item.Status == "connected-with-unresolved-records"),
            families.Sum(item => item.RecordCount),
            fields.Count,
            fields.Count(item => IsConnectedFieldStatus(item.Status)),
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
        builder.AppendLine($"- Connected/structural/explicit-empty fields: {report.ConnectedFieldCount}");
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
        builder.AppendLine("## Explicit-empty connected fields");
        foreach (var field in report.Fields.Where(item => item.Status == "connected-empty")
                     .OrderBy(item => item.Family).ThenBy(item => item.JsonPath))
        {
            builder.AppendLine($"- `{field.Family}:{field.JsonPath}` — evidence-linked and runtime-consumed; occurrences={field.OccurrenceCount}; non-null={field.NonNullCount}; evidence={field.EvidenceReferenceCount}; consumers={field.RuntimeConsumerCount}");
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
        foreach (var family in report.Families.Where(item => item.Status is "disconnected" or "invalid" or "connected-with-unresolved-records"))
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

    private static GeneratedFamilyInventory ReadGeneratedFamily(string dataFile)
    {
        var map = new Dictionary<string, MutableFieldAccumulator>(StringComparer.OrdinalIgnoreCase);
        var pending = 0;
        var unresolved = 0;
        var recordCount = 0;
        var text = File.ReadAllText(dataFile);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        pending += CountMarkerValues(root, PendingMarkers);
        unresolved += CountMarkerValues(root, UnresolvedMarkers);
        Walk(root, "$", map);

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("record_parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(part.GetString()))
                    throw new InvalidDataException($"Chunk manifest '{dataFile}' contains an invalid record part.");
                var partPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dataFile)!, part.GetString()!.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(partPath)) throw new FileNotFoundException("Generated record part is missing.", partPath);
                var partText = File.ReadAllText(partPath);
                using var partDocument = JsonDocument.Parse(partText);
                pending += CountMarkerValues(partDocument.RootElement, PendingMarkers);
                unresolved += CountMarkerValues(partDocument.RootElement, UnresolvedMarkers);
                recordCount += CountRecords(partDocument.RootElement);
                Walk(partDocument.RootElement, "$", map);
            }
            if (root.TryGetProperty("record_count", out var declared)
                && declared.TryGetInt32(out var declaredCount)
                && declaredCount != recordCount)
                throw new InvalidDataException($"Chunk manifest '{dataFile}' declares {declaredCount} records but its parts contain {recordCount}.");
        }
        else
        {
            recordCount = CountRecords(root);
        }

        var fields = map.Values.Select(value => new FieldAccumulator(
            value.JsonPath, value.FieldName, value.OccurrenceCount, value.NonNullCount, value.ValueKinds))
            .OrderBy(value => value.JsonPath)
            .ToArray();
        return new(recordCount, pending, unresolved, fields);
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
                foreach (var item in element.EnumerateArray())
                {
                    var itemPath = path + "[*]";
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        Walk(item, itemPath, map);
                        continue;
                    }
                    var fieldName = ExtractArrayFieldName(path);
                    if (!map.TryGetValue(itemPath, out var accumulator))
                    {
                        accumulator = new MutableFieldAccumulator(itemPath, fieldName);
                        map[itemPath] = accumulator;
                    }
                    accumulator.OccurrenceCount++;
                    if (item.ValueKind != JsonValueKind.Null) accumulator.NonNullCount++;
                    accumulator.ValueKinds.Add(item.ValueKind.ToString());
                }
                break;
        }
    }

    private static string ExtractArrayFieldName(string path)
    {
        var end = path.EndsWith("[*]", StringComparison.Ordinal) ? path.Length - 3 : path.Length;
        var dot = path.LastIndexOf('.', Math.Max(0, end - 1));
        return path[(dot + 1)..end];
    }

    private static IReadOnlyList<string> FindFieldConsumers(string projectRoot, IEnumerable<SourceText> sources, string fieldName)
    {
        var quoted = "\"" + fieldName + "\"";
        if (fieldName.Length < 3)
        {
            return sources.Where(source => source.Text.Contains(quoted, StringComparison.OrdinalIgnoreCase))
                .Select(source => Path.GetRelativePath(projectRoot, source.Path).Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToArray();
        }
        var pascal = ToPascalCase(fieldName);
        return sources.Where(source =>
                source.Text.Contains(quoted, StringComparison.OrdinalIgnoreCase) ||
                (pascal.Length >= 3 && source.Text.Contains(pascal, StringComparison.Ordinal)))
            .Select(source => Path.GetRelativePath(projectRoot, source.Path).Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();
    }

    private static IReadOnlyList<string> ExplicitRuntimeConsumers(string family, string jsonPath)
    {
        if (family.Equals("npcs", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].combat.hp", StringComparison.OrdinalIgnoreCase))
            return [
                "Apps/Source/RealmGate.Server/RuntimeNpcModels.cs",
                "Apps/Source/RealmGate.Server/RuntimeCombatCompletion.cs"
            ];
        return Array.Empty<string>();
    }

    private static int CountEvidenceReferences(IEnumerable<SourceText> evidenceTexts, string fieldName)
    {
        if (fieldName.Length < 3)
        {
            var quoted = "\"" + fieldName + "\"";
            return evidenceTexts.Count(source => source.Text.Contains(quoted, StringComparison.OrdinalIgnoreCase));
        }
        var pascal = ToPascalCase(fieldName);
        return evidenceTexts.Count(source =>
            source.Text.Contains(fieldName, StringComparison.OrdinalIgnoreCase) ||
            (pascal.Length >= 3 && source.Text.Contains(pascal, StringComparison.OrdinalIgnoreCase)));
    }

    private static int CountEvidenceReferences(IEnumerable<SourceText> evidenceTexts, IReadOnlyList<string> fieldNames)
    {
        return evidenceTexts.Count(source => fieldNames.Any(fieldName =>
        {
            var quoted = "\"" + fieldName + "\"";
            var pascal = ToPascalCase(fieldName);
            return source.Text.Contains(quoted, StringComparison.OrdinalIgnoreCase) ||
                   (pascal.Length >= 3 && source.Text.Contains(pascal, StringComparison.OrdinalIgnoreCase));
        }));
    }

    private static IReadOnlyList<string> ExplicitEvidenceAliases(string family, string jsonPath)
    {
        if (family.Equals("native_help", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].body_message_number", StringComparison.OrdinalIgnoreCase))
            return ["message_number"];
        if (family.Equals("native_help", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].title_message_number", StringComparison.OrdinalIgnoreCase))
            return ["message_number"];
        if (family.Equals("native_help", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].native_keywords", StringComparison.OrdinalIgnoreCase))
            return ["decoded_cp437"];
        if (family.Equals("native_player_commands", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].documented_input", StringComparison.OrdinalIgnoreCase))
            return ["decoded_cp437"];
        if (family.Equals("native_player_commands", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].explicit_aliases[*]", StringComparison.OrdinalIgnoreCase))
            return ["decoded_cp437"];
        if (family.Equals("equipment", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].minimum_strength", StringComparison.OrdinalIgnoreCase))
            return ["str_requirement"];
        if (family.Equals("quests", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].root_textblock_id", StringComparison.OrdinalIgnoreCase))
            return ["root_textblock"];
        if (family.Equals("rooms", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].environment.room_delay", StringComparison.OrdinalIgnoreCase))
            return ["delay"];
        if (family.Equals("rooms", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].environment.room_spell_id", StringComparison.OrdinalIgnoreCase))
            return ["spell_id"];
        if (family.Equals("items", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].default_charges", StringComparison.OrdinalIgnoreCase))
            return ["charges"];
        if (family.Equals("items", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.records[*].stackable", StringComparison.OrdinalIgnoreCase))
            return ["item_type"];
        if (family.Equals("world_manifest", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.character_start_room", StringComparison.OrdinalIgnoreCase))
            return ["new_character_start", "room_id"];
        if (family.Equals("world_manifest", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.character_spawning.start_room", StringComparison.OrdinalIgnoreCase))
            return ["new_character_start", "room_id"];
        if (family.Equals("navigation_graph", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.edges[*].gateKind", StringComparison.OrdinalIgnoreCase))
            return ["gate_kind"];
        if (family.Equals("navigation_graph", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.edges[*].hasRestrictions", StringComparison.OrdinalIgnoreCase))
            return ["prerequisites"];
        if (family.Equals("navigation_graph", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.rooms[*].maximumNpcLevel", StringComparison.OrdinalIgnoreCase))
            return ["level", "room_number"];
        if (family.Equals("navigation_graph", StringComparison.OrdinalIgnoreCase) &&
            jsonPath.Equals("$.rooms[*].roomSpellId", StringComparison.OrdinalIgnoreCase))
            return ["room_spell_id"];
        return Array.Empty<string>();
    }

    private static bool IsConnectedFieldStatus(string status) =>
        status is "connected" or "structural-metadata" or "connected-empty";

    private static bool IsStructuralPath(string jsonPath) =>
        jsonPath.Contains(".metadata.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".provenance.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".description_evidence.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".semantic_provenance", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".manifest.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence_refs", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence_summary", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence_bucket", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence_total", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evidence[", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".correlation_report", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".generator_mapping", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".librarium_enrichment", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".relationship_summary", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".override_policy", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".runtime_mapping", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".runtimeMapping", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".source.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".source_", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".native_field_offset", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".native_record_offset", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".native_record_source", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".raw_mdb_descriptor_value", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".physical_record_size", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".record_header", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".record_marker", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".promotion_note_", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".projection_pass", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".state_impact.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".save_or_world_state_impact.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".save_world_state_impact", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".identity_semantics.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".execution_semantics.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".dispatch_semantics.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".evaluation_semantics.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".edge_state_semantics.action_binding.", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".room_ref_semantics", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".spawn_model.linked_", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".decoded_shop_type", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".inventory_projection_status", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".resolved_in_code_update", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".client_access", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".data_output", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".host_editable", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".lock_state", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".public_reference", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".mmexplorer_sql_reference", StringComparison.OrdinalIgnoreCase) ||
        jsonPath.Contains(".compiler_projection", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumericObjectKey(string fieldName) =>
        fieldName.Length > 0 && fieldName.All(char.IsDigit);

    private static int CountMarkerValues(JsonElement element, IReadOnlyCollection<string> markers)
    {
        var count = 0;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    count += CountMarkerValues(property.Value, markers);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    count += CountMarkerValues(item, markers);
                break;
            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                foreach (var marker in markers)
                {
                    var genericMarker = marker is "pending" or "unresolved" or "unknown" or "opaque" or "probe" or "unscored" or "quarantined";
                    if (genericMarker
                            ? value.Equals(marker, StringComparison.OrdinalIgnoreCase)
                            : value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                        count++;
                }
                break;
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
        if (family.Equals("navigation_graph", StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add("rooms");
            aliases.Add("exits");
            aliases.Add("spawns");
            aliases.Add("npcs");
        }
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
    private sealed record GeneratedFamilyInventory(
        int RecordCount,
        int PendingMarkerCount,
        int UnresolvedMarkerCount,
        IReadOnlyList<FieldAccumulator> Fields);
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
