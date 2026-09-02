using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using JetDatabaseReader;

namespace Reforged.App.Services;

public sealed record NightmareMdbTableSummary(
    string TableName,
    int RowCount,
    int ColumnCount,
    string ReforgedFamily,
    string Status,
    int MappableRows,
    int Conflicts,
    string Notes);

public sealed record NightmareMdbAnalysis(
    string FilePath,
    string Provider,
    int TableCount,
    int TotalRows,
    int MappableRows,
    int Conflicts,
    int RawOnlyRows,
    IReadOnlyList<NightmareMdbTableSummary> Tables,
    string Summary);

public sealed record NightmareMdbImportResult(
    string FilePath,
    int ImportedRecords,
    int Conflicts,
    int RawOnlyRows,
    string ArchiveFolder,
    string Summary);

public sealed record NightmareMdbExportResult(
    string FilePath,
    int ExportedRows,
    int SkippedRows,
    int TableCount,
    string Summary);

public sealed record NightmareWccEvidenceFile(
    string FileName,
    string Family,
    long Bytes,
    string Sha256,
    string Status);

public sealed record NightmareWccEvidenceAnalysis(
    string Folder,
    int FileCount,
    int RecognizedFiles,
    long TotalBytes,
    IReadOnlyList<NightmareWccEvidenceFile> Files,
    string Summary);

public sealed record NightmareMdbWccPairValidation(
    bool CanImport,
    string MdbSha256,
    string WccSetFingerprint,
    string SourcePairFingerprint,
    int MappedFamilies,
    int CoveredFamilies,
    int UniqueAnchorRows,
    IReadOnlyList<string> MissingWccFamilies,
    IReadOnlyList<string> UnanchoredFamilies,
    string Summary);

public sealed record NightmareWccGapRow(
    string TableName,
    string ReforgedFamily,
    string WccFiles,
    int MdbColumns,
    int PromotedColumns,
    int PreservedColumns,
    int UniqueNameAnchors,
    int StrongCorrelations,
    int WccOnlyStringCandidates,
    string Status,
    string Notes);

public sealed record NightmareWccGapAnalysis(
    string MdbPath,
    string WccFolder,
    int TableCount,
    int TotalMdbColumns,
    int PromotedColumns,
    int PreservedColumns,
    int CapturedRecordWindows,
    int StrongCorrelations,
    int WccOnlyStringCandidates,
    string ReportFolder,
    IReadOnlyList<NightmareWccGapRow> Rows,
    string Summary);

/// <summary>
/// Bridge between custom MajorMUD/Nightmare Access/MDB exports and Reforged Custom1.
/// MDB is the structured transport, but import is intentionally locked until the matching original
/// WCC/WGS DAT/BIN set is present and source-pair verification succeeds. Both sources remain read-only;
/// Reforged writes only Custom1 overlay/provenance artifacts.
/// </summary>
public sealed class NightmareMdbBridgeService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly CustomWorldEditorService _custom;
    private readonly string _projectRoot;

    public NightmareMdbBridgeService(CustomWorldEditorService custom)
    {
        _custom = custom;
        _projectRoot = custom.ProjectRoot;
    }

    public NightmareMdbAnalysis AnalyzeMdb(string path)
    {
        path = RequireMdb(path);
        using var dump = ReadMdbDump(path);
        var tables = ReadTables(dump.Root);
        var overrideKeys = _custom.Load().Overrides
            .Select(row => MakeKey(row.Family, row.TargetId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var summaries = new List<NightmareMdbTableSummary>();
        var totalRows = 0;
        var mappableRows = 0;
        var conflicts = 0;
        var rawOnlyRows = 0;
        foreach (var table in tables)
        {
            var profile = ResolveProfile(table.Name);
            var rows = table.Rows.Count;
            totalRows += rows;
            var mapped = 0;
            var tableConflicts = 0;
            if (profile is not null)
            {
                foreach (var row in table.Rows)
                {
                    if (!TryMapRow(profile, row, out var record)) continue;
                    mapped++;
                    if (overrideKeys.Contains(MakeKey(record.Family, record.TargetId))) tableConflicts++;
                }
            }
            var rawOnly = Math.Max(0, rows - mapped);
            mappableRows += mapped;
            conflicts += tableConflicts;
            rawOnlyRows += rawOnly;
            summaries.Add(new NightmareMdbTableSummary(
                table.Name,
                rows,
                table.Columns.Count,
                profile?.Family ?? string.Empty,
                profile is null ? "RAW ONLY" : mapped == rows ? "MAPPED" : "PARTIAL",
                mapped,
                tableConflicts,
                profile is null
                    ? "Table is preserved in the raw import archive but is not promoted into Custom1 yet."
                    : rawOnly == 0
                        ? $"Maps to {CustomWorldSimpleFormCatalog.LabelForFamily(profile.Family)}. Unmapped columns are retained on imported Custom1 records under legacy_sources.nightmare_mdb.unmapped_fields and in the raw MDB archive."
                        : $"{rawOnly} row(s) lack a usable Nightmare record key and will remain raw-only."));
        }

        var provider = dump.Root["provider"]?.GetValue<string>() ?? "unknown";
        var summary = $"{tables.Count} table(s), {totalRows} row(s): {mappableRows} can map into Custom1, {conflicts} conflict with existing Custom1 overrides, {rawOnlyRows} remain raw-only. Provider: {provider}.";
        return new NightmareMdbAnalysis(path, provider, tables.Count, totalRows, mappableRows, conflicts, rawOnlyRows, summaries, summary);
    }

    public NightmareMdbImportResult ImportMdb(string path, string wccEvidenceFolder)
    {
        path = RequireMdb(path);
        var pairValidation = ValidateMdbWccPair(path, wccEvidenceFolder);
        if (!pairValidation.CanImport)
            throw new InvalidOperationException("MDB import blocked because the required WCC/WGS source set could not be matched. " + pairValidation.Summary);
        wccEvidenceFolder = Path.GetFullPath(wccEvidenceFolder);
        using var dump = ReadMdbDump(path);
        var tables = ReadTables(dump.Root);
        var entries = new JsonArray();
        var mappedRows = 0;
        var rawOnly = 0;
        var mappedRecords = new Dictionary<string, MappedRecord>(StringComparer.OrdinalIgnoreCase);
        var wccSourceCache = new Dictionary<TableKind, IReadOnlyList<WccFileData>>();
        var wccAnchoredRows = 0;
        foreach (var table in tables)
        {
            var profile = ResolveProfile(table.Name);
            if (profile is null)
            {
                rawOnly += table.Rows.Count;
                continue;
            }
            foreach (var row in table.Rows)
            {
                if (!TryMapRow(profile, row, out var record))
                {
                    rawOnly++;
                    continue;
                }

                if (AttachWccAnchorEvidence(profile, row, record.Fields, wccEvidenceFolder, wccSourceCache))
                    wccAnchoredRows++;
                mappedRows++;
                var key = MakeKey(record.Family, record.TargetId);
                if (mappedRecords.TryGetValue(key, out var existing))
                    mappedRecords[key] = MergeMappedRows(existing, record);
                else
                    mappedRecords[key] = record;

                if (profile.Kind == TableKind.Rooms)
                {
                    foreach (var exitRecord in ImportRoomExits(row))
                    {
                        var exitKey = MakeKey(exitRecord.Family, exitRecord.TargetId);
                        mappedRecords[exitKey] = exitRecord;
                    }
                }
            }
        }

        foreach (var record in mappedRecords.Values)
        {
            var operation = "upsert";
            try
            {
                var state = _custom.LoadSimpleRecord(record.Family, record.TargetId);
                operation = state.ExistsInBase ? "patch" : "upsert";
            }
            catch
            {
                operation = "upsert";
            }

            entries.Add(new JsonObject
            {
                ["family"] = record.Family,
                ["target_id"] = record.TargetId,
                ["operation"] = operation,
                ["fields"] = record.Fields.DeepClone()
            });
        }

        var mapped = mappedRecords.Count;
        if (mapped == 0)
            throw new InvalidOperationException("No Nightmare MDB rows could be mapped into Custom1. The MDB raw data was not applied.");

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var archiveFolder = Path.Combine(_projectRoot, "Librarium", "WorldOverrides", CustomWorldEditorService.CustomWorldId, "Imports", "NightmareMDB-" + stamp);
        Directory.CreateDirectory(archiveFolder);
        var rawArchive = Path.Combine(archiveFolder, "nightmare-mdb-raw.json");
        File.WriteAllText(rawArchive, dump.Root.ToJsonString(JsonOptions) + Environment.NewLine);

        var evidence = AnalyzeWccEvidenceFolder(wccEvidenceFolder);
        File.WriteAllText(Path.Combine(archiveFolder, "wcc-evidence-manifest.json"), JsonSerializer.Serialize(evidence, JsonOptions) + Environment.NewLine);
        File.WriteAllText(Path.Combine(archiveFolder, "source-pair-validation.json"), JsonSerializer.Serialize(pairValidation, JsonOptions) + Environment.NewLine);

        var packagePath = Path.Combine(archiveFolder, "reforged-custom1-import-package.json");
        var package = new JsonObject
        {
            ["schema"] = "reforged_custom1_export_v1",
            ["world_id"] = CustomWorldEditorService.CustomWorldId,
            ["source"] = "Reforged Custom World MDB + WCC Import",
            ["source_mdb"] = Path.GetFileName(path),
            ["source_mdb_sha256"] = pairValidation.MdbSha256,
            ["source_wcc_set_fingerprint"] = pairValidation.WccSetFingerprint,
            ["source_pair_fingerprint"] = pairValidation.SourcePairFingerprint,
            ["imported_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["overrides"] = entries
        };
        File.WriteAllText(packagePath, package.ToJsonString(JsonOptions) + Environment.NewLine);

        var preview = _custom.ApplyImportPackage(packagePath);
        var manifest = new JsonObject
        {
            ["schema"] = "reforged_custom_world_source_manifest_v2",
            ["source_mdb"] = Path.GetFullPath(path),
            ["source_mdb_sha256"] = pairValidation.MdbSha256,
            ["source_wcc_set_fingerprint"] = pairValidation.WccSetFingerprint,
            ["source_pair_fingerprint"] = pairValidation.SourcePairFingerprint,
            ["source_pair_verified"] = pairValidation.CanImport,
            ["source_pair_covered_families"] = pairValidation.CoveredFamilies,
            ["source_pair_mapped_families"] = pairValidation.MappedFamilies,
            ["mapped_rows"] = mappedRows,
            ["mapped_records"] = mapped,
            ["raw_only_rows"] = rawOnly,
            ["custom1_conflicts_replaced"] = preview.Conflicts,
            ["wcc_evidence_folder"] = evidence.Folder,
            ["wcc_recognized_files"] = evidence.RecognizedFiles,
            ["wcc_unique_anchor_rows"] = wccAnchoredRows,
            ["raw_archive"] = rawArchive,
            ["package_archive"] = packagePath
        };
        File.WriteAllText(Path.Combine(archiveFolder, "bridge-manifest.json"), manifest.ToJsonString(JsonOptions) + Environment.NewLine);

        var provenancePath = Path.Combine(_projectRoot, "Librarium", "WorldOverrides", CustomWorldEditorService.CustomWorldId, "source-provenance.json");
        var provenance = new JsonObject
        {
            ["schema"] = "reforged_custom_world_source_provenance_v1",
            ["world_id"] = CustomWorldEditorService.CustomWorldId,
            ["source_pair_fingerprint"] = pairValidation.SourcePairFingerprint,
            ["source_mdb_name"] = Path.GetFileName(path),
            ["source_mdb_sha256"] = pairValidation.MdbSha256,
            ["wcc_set_fingerprint"] = pairValidation.WccSetFingerprint,
            ["wcc_file_count"] = evidence.FileCount,
            ["wcc_recognized_files"] = evidence.RecognizedFiles,
            ["unique_wcc_anchor_rows"] = wccAnchoredRows,
            ["last_imported_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["latest_archive"] = archiveFolder
        };
        File.WriteAllText(provenancePath, provenance.ToJsonString(JsonOptions) + Environment.NewLine);

        return new NightmareMdbImportResult(
            path,
            mapped,
            preview.Conflicts,
            rawOnly,
            archiveFolder,
            $"Imported {mapped} Custom1 overlay record(s) from {mappedRows} mapped MDB row(s) after required WCC/WGS source verification; {preview.Conflicts} existing Custom1 override(s) were replaced. {rawOnly} row(s) remain preserved raw-only. {wccAnchoredRows} mapped row(s) received exact unique native anchors. Provenance fingerprint: {pairValidation.SourcePairFingerprint[..Math.Min(16, pairValidation.SourcePairFingerprint.Length)]}…. Archive: {archiveFolder}");
    }

    public NightmareMdbExportResult ExportMdb(string path, bool changedOnly)
    {
        path = RequireMdbOutput(path);
        var manifest = BuildNightmareExportManifest(changedOnly, out var exported, out var skipped, out var tableCount);
        var work = Path.Combine(Path.GetTempPath(), "Reforged-NightmareMDB-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var manifestPath = Path.Combine(work, "export-manifest.json");
            File.WriteAllText(manifestPath, manifest.ToJsonString(JsonOptions) + Environment.NewLine);
            RunBridgeScript("Write-NightmareMdb.ps1", ["-ManifestJson", manifestPath, "-MdbPath", Path.GetFullPath(path)]);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { }
        }
        return new NightmareMdbExportResult(
            path,
            exported,
            skipped,
            tableCount,
            $"Exported {exported} Nightmare-compatible row(s) across {tableCount} table(s) to {path}. {skipped} Reforged-only record(s) without a usable numeric WCC/Nightmare key were skipped.");
    }

    public NightmareWccEvidenceAnalysis AnalyzeWccEvidenceFolder(string folder)
    {
        folder = Path.GetFullPath(string.IsNullOrWhiteSpace(folder) ? throw new InvalidOperationException("Choose a WCC/WGS DAT folder first.") : folder.Trim());
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path).Equals(".dat", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".bin", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var name = Path.GetFileName(path);
                var family = ClassifyWccFile(name);
                var info = new FileInfo(path);
                return new NightmareWccEvidenceFile(name, family, info.Length, Sha256(path), family.Length == 0 ? "UNCLASSIFIED" : "RECOGNIZED");
            })
            .ToArray();
        var recognized = files.Count(file => file.Family.Length > 0);
        var bytes = files.Sum(file => file.Bytes);
        return new NightmareWccEvidenceAnalysis(
            folder,
            files.Length,
            recognized,
            bytes,
            files,
            $"Found {files.Length} DAT/BIN file(s); {recognized} recognized as MajorMUD/Worldgroup evidence. These files are retained read-only and are never modified by the bridge.");
    }


    public NightmareMdbWccPairValidation ValidateMdbWccPair(string mdbPath, string wccFolder)
    {
        mdbPath = RequireMdb(mdbPath);
        wccFolder = Path.GetFullPath(string.IsNullOrWhiteSpace(wccFolder)
            ? throw new InvalidOperationException("Choose the matching original WCC/WGS folder first. Custom-world imports require the MDB and its native WCC/WGS source set together.")
            : wccFolder.Trim());
        if (!Directory.Exists(wccFolder)) throw new DirectoryNotFoundException(wccFolder);

        using var dump = ReadMdbDump(mdbPath);
        var tables = ReadTables(dump.Root);
        var mappedKinds = tables
            .Where(table => table.Rows.Count > 0)
            .Select(table => ResolveProfile(table.Name))
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .GroupBy(profile => profile.Kind)
            .Select(group => group.First())
            .ToArray();
        if (mappedKinds.Length == 0)
            throw new InvalidOperationException("The MDB has no supported MajorMUD tables to pair with WCC/WGS source files.");

        var evidence = AnalyzeWccEvidenceFolder(wccFolder);
        if (evidence.RecognizedFiles == 0)
            return new NightmareMdbWccPairValidation(false, Sha256(mdbPath), string.Empty, string.Empty,
                mappedKinds.Length, 0, 0, mappedKinds.Select(profile => profile.Family).OrderBy(value => value).ToArray(),
                Array.Empty<string>(), "No recognized MajorMUD WCC/WGS DAT/BIN files were found. Import is blocked.");

        var wccFingerprint = FingerprintWccSet(evidence.Files);
        var mdbSha = Sha256(mdbPath);
        var pairFingerprint = Sha256Text(mdbSha + "\n" + wccFingerprint);
        var missing = new List<string>();
        var unanchored = new List<string>();
        var covered = 0;
        var uniqueAnchors = 0;
        var cache = new Dictionary<TableKind, IReadOnlyList<WccFileData>>();

        foreach (var profile in mappedKinds)
        {
            var files = LoadCandidateWccFiles(wccFolder, profile.Kind);
            cache[profile.Kind] = files;
            if (files.Count == 0)
            {
                missing.Add(profile.Family);
                continue;
            }
            covered++;

            var anchorableRows = tables
                .Where(table => ResolveProfile(table.Name)?.Kind == profile.Kind)
                .SelectMany(table => table.Rows)
                .Select(row => (Row: row, Anchor: GapAnchor(profile.Kind, row)))
                .Where(item => item.Anchor.Length >= 4)
                .ToArray();
            if (anchorableRows.Length == 0) continue;

            var familyAnchors = 0;
            foreach (var item in anchorableRows)
            {
                if (TryFindUniqueAnchor(files, item.Anchor, out _, out _))
                {
                    familyAnchors++;
                    uniqueAnchors++;
                }
            }
            if (familyAnchors == 0) unanchored.Add(profile.Family);
        }

        // A valid pair must provide the native file family for every supported MDB family.
        // At least one exact unique text anchor somewhere in the set is also required; this
        // prevents an unrelated WCC folder that merely happens to contain familiar filenames
        // from being accepted as the source for the MDB.
        var canImport = missing.Count == 0 && uniqueAnchors > 0;
        var summary = canImport
            ? $"Source pair verified: {covered}/{mappedKinds.Length} mapped MDB families have matching WCC/WGS files and {uniqueAnchors:N0} MDB row(s) have exact unique native-text anchors. Source fingerprint {pairFingerprint[..Math.Min(16, pairFingerprint.Length)]}…."
            : $"Source pair rejected: {covered}/{mappedKinds.Length} mapped MDB families have WCC/WGS coverage, {uniqueAnchors:N0} exact unique anchor(s), missing families: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}. Import remains blocked.";

        return new NightmareMdbWccPairValidation(
            canImport,
            mdbSha,
            wccFingerprint,
            pairFingerprint,
            mappedKinds.Length,
            covered,
            uniqueAnchors,
            missing.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            unanchored.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            summary);
    }

    public NightmareWccGapAnalysis AnalyzeMdbWccGaps(string mdbPath, string wccFolder)
    {
        mdbPath = RequireMdb(mdbPath);
        wccFolder = Path.GetFullPath(string.IsNullOrWhiteSpace(wccFolder)
            ? throw new InvalidOperationException("Choose the matching original WCC/WGS folder first.")
            : wccFolder.Trim());
        if (!Directory.Exists(wccFolder)) throw new DirectoryNotFoundException(wccFolder);

        using var dump = ReadMdbDump(mdbPath);
        var tables = ReadTables(dump.Root);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var reportFolder = Path.Combine(_projectRoot, "Librarium", "WorldOverrides", CustomWorldEditorService.CustomWorldId, "Imports", "WccGapAnalysis-" + stamp);
        Directory.CreateDirectory(reportFolder);

        var rows = new List<NightmareWccGapRow>();
        var reportRows = new JsonArray();
        var totalColumns = 0;
        var totalPromoted = 0;
        var totalPreserved = 0;
        var capturedWindows = 0;
        var totalStrong = 0;
        var totalWccOnlyStrings = 0;
        var windowPath = Path.Combine(reportFolder, "wcc-record-windows.jsonl");

        using (var windowWriter = new StreamWriter(windowPath, false, new UTF8Encoding(false)))
        {
            foreach (var table in tables)
            {
                var profile = ResolveProfile(table.Name);
                if (profile is null) continue;

                var columns = table.Columns.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var promoted = columns.Where(column => IsPromotedColumn(profile.Kind, column)).ToArray();
                var preserved = columns.Where(column => !IsPromotedColumn(profile.Kind, column)).ToArray();
                totalColumns += columns.Length;
                totalPromoted += promoted.Length;
                totalPreserved += preserved.Length;

                var wccFiles = LoadCandidateWccFiles(wccFolder, profile.Kind);
                var correlations = AnalyzeGapCorrelations(profile, table, wccFiles, preserved);
                var strong = correlations.Count(row => row.Classification == "STRONGLY_CORRELATED");
                totalStrong += strong;

                var uniqueAnchors = 0;
                var tableWccOnlyStrings = 0;
                foreach (var sourceRow in table.Rows)
                {
                    var anchor = GapAnchor(profile.Kind, sourceRow);
                    if (anchor.Length < 4 || !TryFindUniqueAnchor(wccFiles, anchor, out var matchedFile, out var anchorOffset)) continue;
                    if (!TryMapRow(profile, sourceRow, out var mapped)) continue;

                    uniqueAnchors++;
                    var start = Math.Max(0, anchorOffset - 128);
                    var length = Math.Min(384, matchedFile.Data.Length - start);
                    if (length <= 0) continue;
                    var bytes = matchedFile.Data.AsSpan(start, length).ToArray();
                    var rowStrings = RowStringValues(sourceRow);
                    var windowStrings = ExtractAsciiStrings(bytes, minimumLength: 4)
                        .Where(value => !rowStrings.Contains(value, StringComparer.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(24)
                        .ToArray();
                    tableWccOnlyStrings += windowStrings.Length;
                    capturedWindows++;

                    var recordEvidence = new JsonObject
                    {
                        ["table"] = table.Name,
                        ["reforged_family"] = profile.Family,
                        ["target_id"] = mapped.TargetId,
                        ["anchor"] = anchor,
                        ["wcc_file"] = matchedFile.Name,
                        ["wcc_sha256"] = matchedFile.Sha256,
                        ["anchor_offset"] = anchorOffset,
                        ["window_start"] = start,
                        ["window_length"] = length,
                        ["window_hex"] = Convert.ToHexString(bytes),
                        ["wcc_only_strings"] = ToJsonArray(windowStrings)
                    };
                    windowWriter.WriteLine(recordEvidence.ToJsonString());
                }
                totalWccOnlyStrings += tableWccOnlyStrings;

                var correlationNotes = correlations
                    .Where(row => row.Classification is "STRONGLY_CORRELATED" or "CANDIDATE")
                    .OrderByDescending(row => row.Confidence)
                    .Take(5)
                    .Select(row => $"{row.ColumnName}@{row.RelativeOffset:+#;-#;0}/{row.WidthBytes}B {row.Confidence:P0}")
                    .ToArray();
                var status = wccFiles.Count == 0
                    ? "NO WCC FILE"
                    : uniqueAnchors == 0
                        ? "NO UNIQUE ANCHORS"
                        : preserved.Length == 0 && tableWccOnlyStrings == 0
                            ? "MDB COVERED"
                            : strong > 0 ? "CORRELATED" : "CAPTURED";
                var notes = new List<string>();
                if (preserved.Length > 0) notes.Add("Preserved MDB columns: " + string.Join(", ", preserved.Take(12)) + (preserved.Length > 12 ? " ..." : string.Empty));
                if (correlationNotes.Length > 0) notes.Add("Best WCC correlations: " + string.Join("; ", correlationNotes));
                if (tableWccOnlyStrings > 0) notes.Add($"Captured {tableWccOnlyStrings} printable WCC-only string candidate(s) in record windows.");
                if (notes.Count == 0) notes.Add("All known MDB columns for this table are promoted into Reforged fields.");

                rows.Add(new NightmareWccGapRow(
                    table.Name,
                    profile.Family,
                    string.Join(", ", wccFiles.Select(file => file.Name)),
                    columns.Length,
                    promoted.Length,
                    preserved.Length,
                    uniqueAnchors,
                    strong,
                    tableWccOnlyStrings,
                    status,
                    string.Join(" ", notes)));

                var correlationJson = new JsonArray();
                foreach (var correlation in correlations)
                {
                    correlationJson.Add(new JsonObject
                    {
                        ["column"] = correlation.ColumnName,
                        ["wcc_file"] = correlation.WccFile,
                        ["relative_offset"] = correlation.RelativeOffset,
                        ["width_bytes"] = correlation.WidthBytes,
                        ["matching_rows"] = correlation.MatchingRows,
                        ["eligible_rows"] = correlation.EligibleRows,
                        ["confidence"] = correlation.Confidence,
                        ["classification"] = correlation.Classification
                    });
                }
                reportRows.Add(new JsonObject
                {
                    ["table"] = table.Name,
                    ["reforged_family"] = profile.Family,
                    ["wcc_files"] = ToJsonArray(wccFiles.Select(file => file.Name)),
                    ["mdb_columns"] = ToJsonArray(columns),
                    ["promoted_columns"] = ToJsonArray(promoted),
                    ["preserved_columns"] = ToJsonArray(preserved),
                    ["unique_name_anchors"] = uniqueAnchors,
                    ["wcc_only_string_candidates"] = tableWccOnlyStrings,
                    ["correlations"] = correlationJson
                });
            }
        }

        var evidence = AnalyzeWccEvidenceFolder(wccFolder);
        var report = new JsonObject
        {
            ["schema"] = "reforged_mdb_wcc_gap_analysis_v1",
            ["created_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["source_mdb"] = Path.GetFullPath(mdbPath),
            ["source_mdb_sha256"] = Sha256(mdbPath),
            ["source_wcc_folder"] = wccFolder,
            ["wcc_manifest"] = JsonSerializer.SerializeToNode(evidence, JsonOptions),
            ["tables"] = reportRows,
            ["record_windows_jsonl"] = windowPath,
            ["interpretation"] = "STRONGLY_CORRELATED identifies repeated byte offsets that reproduce an MDB numeric value across uniquely name-anchored WCC records. It is evidence, not automatic runtime semantics. WCC-only strings are preserved candidates and are not guessed into gameplay fields."
        };
        File.WriteAllText(Path.Combine(reportFolder, "wcc-gap-analysis.json"), report.ToJsonString(JsonOptions) + Environment.NewLine);

        var summary = $"Gap analysis complete: {rows.Count} mapped table(s), {totalPromoted}/{totalColumns} MDB column(s) promoted, {totalPreserved} preserved legacy column(s), {capturedWindows} uniquely anchored WCC record window(s), {totalStrong} strong byte correlation(s), and {totalWccOnlyStrings} printable WCC-only string candidate(s). Report: {reportFolder}";
        return new NightmareWccGapAnalysis(
            mdbPath,
            wccFolder,
            rows.Count,
            totalColumns,
            totalPromoted,
            totalPreserved,
            capturedWindows,
            totalStrong,
            totalWccOnlyStrings,
            reportFolder,
            rows,
            summary);
    }

    private JsonObject BuildNightmareExportManifest(bool changedOnly, out int exported, out int skipped, out int tableCount)
    {
        var changed = _custom.Load().Overrides
            .GroupBy(row => row.Family, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(row => row.TargetId).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var tables = new JsonArray();
        exported = 0;
        skipped = 0;

        AddExportTable(tables, "Rooms", "rooms", RoomColumns(), changedOnly, changed, ExportRoom, ref exported, ref skipped);
        AddExportTable(tables, "Items", "items", ItemColumns(), changedOnly, changed, ExportItem, ref exported, ref skipped);
        AddExportTable(tables, "Monsters", "npcs", MonsterColumns(), changedOnly, changed, ExportMonster, ref exported, ref skipped);
        AddExportTable(tables, "Spells", "spells", SpellColumns(), changedOnly, changed, ExportSpell, ref exported, ref skipped);
        AddExportTable(tables, "Shops", "shops", ShopColumns(), changedOnly, changed, ExportShop, ref exported, ref skipped);
        AddExportTable(tables, "Classes", "classes", ClassColumns(), changedOnly, changed, ExportClass, ref exported, ref skipped);
        AddExportTable(tables, "Races", "races", RaceColumns(), changedOnly, changed, ExportRace, ref exported, ref skipped);
        AddExportTable(tables, "Messages", "native_message_payloads", MessageColumns(), changedOnly, changed, ExportMessage, ref exported, ref skipped);
        AddExportTable(tables, "Textblocks", "native_textblock_payloads", TextblockColumns(), changedOnly, changed, ExportTextblock, ref exported, ref skipped);
        AddExportTable(tables, "Actions", "room_interactions", ActionColumns(), changedOnly, changed, ExportAction, ref exported, ref skipped);
        AddExportTable(tables, "Data", "abilities", AbilityColumns(), changedOnly, changed, ExportAbility, ref exported, ref skipped);

        tables.Insert(0, new JsonObject
        {
            ["name"] = "VersionInfo",
            ["columns"] = new JsonArray(Column("NMR Version", "TEXT"), Column("Dat File Version", "TEXT"), Column("Source", "TEXT")),
            ["rows"] = new JsonArray(new JsonObject
            {
                ["NMR Version"] = "Reforged MDB Bridge",
                ["Dat File Version"] = "1.11p",
                ["Source"] = changedOnly ? "Reforged Custom1 changed records" : "Reforged Custom1 effective world"
            })
        });
        tableCount = tables.Count;
        return new JsonObject
        {
            ["schema"] = "reforged_nightmare_mdb_export_v1",
            ["created_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["world_id"] = CustomWorldEditorService.CustomWorldId,
            ["scope"] = changedOnly ? "changed_only" : "full_effective_custom1",
            ["tables"] = tables
        };
    }

    private void AddExportTable(
        JsonArray tables,
        string tableName,
        string family,
        JsonArray columns,
        bool changedOnly,
        IReadOnlyDictionary<string, HashSet<string>> changed,
        Func<JsonObject, JsonObject?> projector,
        ref int exported,
        ref int skipped)
    {
        IEnumerable<CustomWorldObjectSummary> objects = _custom.BrowseObjects(family, null, 20000);
        if (changedOnly)
        {
            if (!changed.TryGetValue(family, out var ids) || ids.Count == 0) return;
            objects = objects.Where(row => ids.Contains(row.Id));
        }

        var rows = new JsonArray();
        foreach (var summary in objects)
        {
            try
            {
                var record = JsonNode.Parse(_custom.ReadBaseRecordJson(family, summary.Id)) as JsonObject;
                var row = record is null ? null : projector(record);
                if (row is null) { skipped++; continue; }
                rows.Add(row);
                exported++;
            }
            catch { skipped++; }
        }
        if (rows.Count == 0) return;
        tables.Add(new JsonObject { ["name"] = tableName, ["columns"] = columns, ["rows"] = rows });
    }

    private JsonObject? ExportRoom(JsonObject record)
    {
        var map = FirstInt(record, "map_number", "map_id");
        var room = FirstInt(record, "room_number", "legacy_id");
        if ((map <= 0 || room <= 0) && TryRoomId(record["id"]?.GetValue<string>(), out var idMap, out var idRoom)) { map = idMap; room = idRoom; }
        if (map <= 0 || room <= 0) return null;
        var row = new JsonObject
        {
            ["Map Number"] = map,
            ["Room Number"] = room,
            ["Name"] = TextAt(record, "name"),
            ["Light"] = FirstInt(record, "environment.light_level", "light"),
            ["Shop Number"] = NumericRef(record, "shop_ref", "relationships.shop", "services.0"),
            ["Control Room"] = FirstInt(record, "control_room", "environment.control_room"),
            ["Runic"] = FirstInt(record, "currency.runic", "currency.visible.runic"),
            ["Platinum"] = FirstInt(record, "currency.platinum", "currency.visible.platinum"),
            ["Gold"] = FirstInt(record, "currency.gold", "currency.visible.gold"),
            ["Silver"] = FirstInt(record, "currency.silver", "currency.visible.silver"),
            ["Copper"] = FirstInt(record, "currency.copper", "currency.visible.copper"),
            ["InvisRunic"] = FirstInt(record, "currency.hidden.runic"),
            ["InvisPlatinum"] = FirstInt(record, "currency.hidden.platinum"),
            ["InvisGold"] = FirstInt(record, "currency.hidden.gold"),
            ["InvisSilver"] = FirstInt(record, "currency.hidden.silver"),
            ["InvisCopper"] = FirstInt(record, "currency.hidden.copper")
        };
        ExportRoomExits(record, map, room, row);
        return row;
    }

    private void ExportRoomExits(JsonObject roomRecord, int map, int room, JsonObject row)
    {
        var exitIds = new List<string>();
        if (At(roomRecord, "relationships.exits") is JsonArray relationships)
            exitIds.AddRange(relationships.Select(NodeText).Where(value => value.Length > 0));

        // Custom/imported rooms always carry relationships.exits. If a creator added an exit
        // directly, also probe the ten canonical IDs without scanning the entire exit database.
        foreach (var (_, direction) in NightmareRoomDirections)
        {
            var canonical = $"exit.map{map}.{room}.{direction.ToLowerInvariant()}";
            if (!exitIds.Contains(canonical, StringComparer.OrdinalIgnoreCase)) exitIds.Add(canonical);
        }

        foreach (var exitId in exitIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            JsonObject exit;
            try { exit = JsonNode.Parse(_custom.ReadBaseRecordJson("exits", exitId)) as JsonObject ?? new JsonObject(); }
            catch { continue; }
            var direction = TextAt(exit, "direction").ToUpperInvariant();
            var slot = NightmareRoomDirections.FirstOrDefault(value => value.Direction.Equals(direction, StringComparison.OrdinalIgnoreCase)).Slot;
            if (!NightmareRoomDirections.Any(value => value.Direction.Equals(direction, StringComparison.OrdinalIgnoreCase))) continue;
            var toMap = FirstInt(exit, "to_map_number");
            var toRoom = FirstInt(exit, "to_room_number");
            if ((toMap <= 0 || toRoom <= 0) && TryRoomId(TextAt(exit, "relationships.to_room"), out var parsedMap, out var parsedRoom))
            {
                toMap = parsedMap; toRoom = parsedRoom;
            }
            if (toRoom <= 0) continue;
            row[$"Exit Room {slot}"] = toMap > 0 && toMap != map ? $"{toMap}/{toRoom}" : toRoom.ToString(System.Globalization.CultureInfo.InvariantCulture);
            row[$"Attributes {slot}"] = TextAt(exit, "nightmare_mdb.attributes");
            row[$"Desc {slot}"] = TextAt(exit, "nightmare_mdb.description");
            row[$"Exit {slot}"] = TextAt(exit, "nightmare_mdb.exit");
            row[$"Type {slot}"] = TextAt(exit, "nightmare_mdb.type");
            row[$"Para1 {slot}"] = TextAt(exit, "nightmare_mdb.para1");
            row[$"Para2 {slot}"] = TextAt(exit, "nightmare_mdb.para2");
            row[$"Para3 {slot}"] = TextAt(exit, "nightmare_mdb.para3");
            row[$"Para4 {slot}"] = TextAt(exit, "nightmare_mdb.para4");
        }
    }

    private static JsonObject? ExportItem(JsonObject record)
    {
        var number = LegacyNumber(record);
        if (number <= 0) return null;
        var row = new JsonObject
        {
            ["Item #"] = number,
            ["Name"] = TextAt(record, "name"),
            ["Game Limit"] = FirstInt(record, "public_reference_0139.fields.game_limit", "game_limit"),
            ["Weight"] = FirstInt(record, "economy.weight", "weight"),
            ["Uses"] = FirstInt(record, "public_reference_0139.fields.max_uses", "uses"),
            ["Price"] = FirstInt(record, "economy.cost", "price"),
            ["Worn On"] = FirstInt(record, "equipment.worn_on"),
            ["Gettable"] = BoolAt(record, "flags.gettable"),
            ["Req Str"] = FirstInt(record, "equipment.str_requirement"),
            ["Speed"] = FirstInt(record, "combat.speed"),
            ["Miss Msg"] = FirstInt(record, "messages.miss", "public_reference_0139.fields.miss_message"),
            ["Hit Msg"] = FirstInt(record, "messages.hit", "public_reference_0139.fields.hit_message"),
            ["Read Msg"] = FirstInt(record, "messages.read", "public_reference_0139.fields.read_message"),
            ["Not Droppable"] = BoolAt(record, "public_reference_0139.fields.not_droppable"),
            ["Destroy On Death"] = BoolAt(record, "public_reference_0139.fields.destroy_on_death"),
            ["Retain After Uses"] = BoolAt(record, "public_reference_0139.fields.retain_after_uses")
        };
        var descriptions = StringArrayAt(record, "public_reference_0139.fields.descriptions");
        for (var i = 0; i < 6; i++) row[$"Desc{i + 1}"] = i < descriptions.Count ? descriptions[i] : string.Empty;
        return row;
    }

    private static JsonObject? ExportMonster(JsonObject record)
    {
        var number = LegacyNumber(record);
        if (number <= 0) return null;
        var expMultiplier = FirstInt(record, "rewards.exp_multiplier", "exp_multiplier");
        var row = new JsonObject
        {
            ["Monster #"] = number,
            ["Name"] = TextAt(record, "name"),
            ["Active"] = 0,
            ["Undead"] = BoolAt(record, "behavior.undead"),
            ["Alignment"] = TextAt(record, "behavior.alignment"),
            ["Regen Time"] = FirstInt(record, "behavior.regen_time"),
            ["Exp Multiplier"] = expMultiplier > 0 ? expMultiplier : 1,
            ["BS Defense"] = FirstInt(record, "public_reference_0139.fields.backstab_defense"),
            ["Index"] = FirstInt(record, "behavior.group_id"),
            ["Follow"] = FirstInt(record, "behavior.follow"),
            ["Experience"] = FirstInt(record, "rewards.xp_base", "experience"),
            ["Hit Points"] = FirstInt(record, "combat.hp"),
            ["HP Regen"] = FirstInt(record, "public_reference_0139.fields.hp_regen"),
            ["Charm LvL"] = FirstInt(record, "behavior.charm_level"),
            ["Charm RES"] = FirstInt(record, "behavior.charm_resistance"),
            ["Move Msg"] = FirstInt(record, "messages.move"),
            ["Death Msg"] = FirstInt(record, "messages.death"),
            ["Greet Txt"] = FirstInt(record, "messages.greet_textblock", "public_reference_0139.fields.greet_textblock"),
            ["Desc Txt"] = FirstInt(record, "messages.description_textblock", "public_reference_0139.fields.description_textblock"),
            ["Talk Txt"] = FirstInt(record, "messages.talk_textblock", "public_reference_0139.fields.talk_textblock"),
            ["Death Spell"] = FirstInt(record, "death_spell_id", "public_reference_0139.fields.death_spell"),
            ["Create Spell"] = FirstInt(record, "create_spell_id", "public_reference_0139.fields.create_spell")
        };
        if (At(record, "attacks") is JsonArray attacks)
        {
            var slot = 1;
            foreach (var attack in attacks.OfType<JsonObject>().Take(10))
            {
                row[$"Attack Type {slot}"] = FirstInt(attack, "type", "attack_type");
                row[$"Attack Accu/Spell {slot}"] = FirstInt(attack, "accuracy", "spell_id");
                row[$"Attack % {slot}"] = FirstInt(attack, "selection_percent", "percent");
                row[$"Attack Min Hit/Cast % {slot}"] = FirstInt(attack, "minimum_damage", "minimum", "cast_percent");
                row[$"Attack Max Hit/Cast LVL {slot}"] = FirstInt(attack, "maximum_damage", "maximum", "cast_level");
                row[$"Attack Energy {slot}"] = FirstInt(attack, "energy_cost", "energy");
                row[$"Attack Hit Spell {slot}"] = FirstInt(attack, "hit_spell_id", "hit_spell");
                slot++;
            }
        }
        return row;
    }

    private static JsonObject? ExportSpell(JsonObject record)
    {
        var number = LegacyNumber(record);
        if (number <= 0) return null;
        var descriptions = StringArrayAt(record, "public_reference_0139.fields.descriptions");
        return new JsonObject
        {
            ["Spell #"] = number,
            ["Name"] = TextAt(record, "name"),
            ["Short Name"] = TextAt(record, "short_name"),
            ["Desc 1"] = descriptions.Count > 0 ? descriptions[0] : string.Empty,
            ["Desc 2"] = descriptions.Count > 1 ? descriptions[1] : string.Empty,
            ["Cast MSG A"] = FirstInt(record, "messages.cast_a", "public_reference_0139.fields.cast_message_a"),
            ["Cast MSG B"] = FirstInt(record, "messages.cast_b", "public_reference_0139.fields.cast_message_b"),
            ["MSG Style"] = TextAt(record, "public_reference_0139.fields.message_style"),
            ["Energy"] = FirstInt(record, "costs.energy_cost"),
            ["Mana"] = FirstInt(record, "costs.mana_cost"),
            ["Spell Type"] = TextAt(record, "classification.spell_type"),
            ["Type of Resists"] = FirstInt(record, "public_reference_0139.fields.resist_type"),
            ["Difficulty"] = FirstInt(record, "public_reference_0139.fields.difficulty"),
            ["Target"] = TextAt(record, "classification.target_type"),
            ["Duration"] = FirstInt(record, "effect.duration"),
            ["Attack Type"] = TextAt(record, "classification.attack_type"),
            ["Resist Ability"] = FirstInt(record, "public_reference_0139.fields.resist_ability"),
            ["Magery A"] = FirstInt(record, "public_reference_0139.fields.magery_requirement_a"),
            ["Magery B"] = FirstInt(record, "public_reference_0139.fields.magery_requirement_b"),
            ["Level Cap"] = FirstInt(record, "public_reference_0139.fields.level_cap"),
            ["LVLS Max Increase"] = FirstInt(record, "public_reference_0139.fields.levels_for_max_increase"),
            ["Max Increase"] = FirstInt(record, "public_reference_0139.fields.max_increase"),
            ["LVLS Min Increase"] = FirstInt(record, "public_reference_0139.fields.levels_for_min_increase"),
            ["Min Increase"] = FirstInt(record, "public_reference_0139.fields.min_increase"),
            ["LVLS Dur Increase"] = FirstInt(record, "public_reference_0139.fields.levels_for_duration_increase"),
            ["Dur Increase"] = FirstInt(record, "public_reference_0139.fields.duration_increase")
        };
    }

    private static JsonObject? ExportShop(JsonObject record)
    {
        var number = LegacyNumber(record);
        if (number <= 0) return null;
        var row = new JsonObject
        {
            ["Shop #"] = number,
            ["Name"] = TextAt(record, "name"),
            ["ShopType"] = FirstInt(record, "service_type"),
            ["Desc A"] = TextAt(record, "public_reference_0139.fields.descriptions.description_a"),
            ["Desc B"] = TextAt(record, "public_reference_0139.fields.descriptions.description_b"),
            ["Desc C"] = TextAt(record, "public_reference_0139.fields.descriptions.description_c"),
            ["Min Lvl"] = FirstInt(record, "level_range.min"),
            ["Max Lvl"] = FirstInt(record, "level_range.max"),
            ["MarkUp"] = FirstInt(record, "markup_percent"),
            ["Class Limit"] = FirstInt(record, "class_limit")
        };
        if (At(record, "inventory") is JsonArray inventory)
        {
            var slot = 1;
            foreach (var item in inventory.OfType<JsonObject>().Take(30))
            {
                row[$"Item {slot}"] = NumericNode(item["item_ref"]);
                row[$"Max {slot}"] = FirstInt(item, "max_qty");
                row[$"Normal {slot}"] = FirstInt(item, "normal_qty");
                row[$"Regen Time {slot}"] = FirstInt(item, "regen_time");
                row[$"Regen Number {slot}"] = FirstInt(item, "regen_qty");
                row[$"Regen % {slot}"] = FirstInt(item, "regen_percent");
                slot++;
            }
        }
        return row;
    }

    private static JsonObject? ExportClass(JsonObject record)
    {
        var number = LegacyNumber(record);
        if (number <= 0) return null;
        return new JsonObject
        {
            ["Class #"] = number,
            ["Name"] = TextAt(record, "name"),
            ["Min HP"] = FirstInt(record, "min_hp", "stats.min_hp"),
            ["Max HP"] = FirstInt(record, "max_hp", "stats.max_hp"),
            ["ExpTable"] = FirstInt(record, "exp_table", "stats.exp_table"),
            ["Magic Type"] = FirstInt(record, "magic_type", "magic.type"),
            ["Magic LVL"] = FirstInt(record, "magic_level", "magic.level"),
            ["Combat"] = FirstInt(record, "combat_level", "combat.level"),
            ["Title Text"] = FirstInt(record, "title_text", "title_textblock")
        };
    }

    private static JsonObject? ExportRace(JsonObject record)
    {
        var number = LegacyNumber(record);
        if (number <= 0) return null;
        return new JsonObject
        {
            ["Race #"] = number,
            ["Name"] = TextAt(record, "name"),
            ["Min INT"] = FirstInt(record, "stats.min_int", "min_int"),
            ["Min WIL"] = FirstInt(record, "stats.min_wil", "min_wil"),
            ["Min STR"] = FirstInt(record, "stats.min_str", "min_str"),
            ["Min HEA"] = FirstInt(record, "stats.min_hea", "min_hea"),
            ["Min AGL"] = FirstInt(record, "stats.min_agl", "min_agl"),
            ["Min CHM"] = FirstInt(record, "stats.min_chm", "min_chm"),
            ["Max INT"] = FirstInt(record, "stats.max_int", "max_int"),
            ["Max WIL"] = FirstInt(record, "stats.max_wil", "max_wil"),
            ["Max STR"] = FirstInt(record, "stats.max_str", "max_str"),
            ["Max HEA"] = FirstInt(record, "stats.max_hea", "max_hea"),
            ["Max AGL"] = FirstInt(record, "stats.max_agl", "max_agl"),
            ["Max CHM"] = FirstInt(record, "stats.max_chm", "max_chm"),
            ["HP Bonus"] = FirstInt(record, "hp_bonus", "stats.hp_bonus")
        };
    }

    private static JsonObject? ExportMessage(JsonObject record)
    {
        var number = FirstInt(record, "message_id", "legacy_id");
        if (number <= 0) return null;
        return new JsonObject
        {
            ["Message #"] = number,
            ["Line 1"] = TextAt(record, "player_payload"),
            ["Line 2"] = TextAt(record, "secondary_payload"),
            ["Line 3"] = TextAt(record, "tertiary_payload")
        };
    }

    private static JsonObject? ExportTextblock(JsonObject record)
    {
        var number = FirstInt(record, "textblock_id", "legacy_id");
        if (number <= 0) return null;
        return new JsonObject
        {
            ["Textblock #"] = number,
            ["Part #"] = Math.Max(1, FirstInt(record, "part_number", "part")),
            ["Link To"] = FirstInt(record, "link_to", "link_to_textblock"),
            ["Data"] = TextAt(record, "payload", "text")
        };
    }

    private static JsonObject? ExportAction(JsonObject record)
    {
        var number = FirstInt(record, "action_number", "legacy_id");
        if (number <= 0) return null;
        return new JsonObject
        {
            ["Action #"] = number,
            ["Single to User"] = FirstInt(record, "messages.single_to_user"),
            ["Single to Room"] = FirstInt(record, "messages.single_to_room"),
            ["User to User"] = FirstInt(record, "messages.user_to_user"),
            ["User to Other User"] = FirstInt(record, "messages.user_to_other_user"),
            ["User to Room"] = FirstInt(record, "messages.user_to_room"),
            ["Monster to User"] = FirstInt(record, "messages.monster_to_user"),
            ["Monster to Room"] = FirstInt(record, "messages.monster_to_room"),
            ["Inventory to User"] = FirstInt(record, "messages.inventory_to_user"),
            ["Inventory to Room"] = FirstInt(record, "messages.inventory_to_room"),
            ["Floor Item to User"] = FirstInt(record, "messages.floor_item_to_user"),
            ["Floor Item to Room"] = FirstInt(record, "messages.floor_item_to_room")
        };
    }

    private static JsonObject? ExportAbility(JsonObject record)
    {
        var number = LegacyNumber(record);
        if (number <= 0) return null;
        return new JsonObject
        {
            ["Number"] = number,
            ["Name"] = TextAt(record, "name"),
            ["Description"] = TextAt(record, "public_reference_0139.fields.description", "description"),
            ["RED"] = FirstInt(record, "public_reference_0139.fields.color_red"),
            ["GREEN"] = FirstInt(record, "public_reference_0139.fields.color_green"),
            ["BLUE"] = FirstInt(record, "public_reference_0139.fields.color_blue")
        };
    }

    private bool TryMapRow(TableProfile profile, JsonObject row, out MappedRecord mapped)
    {
        mapped = default!;
        var fields = new JsonObject();
        string targetId;
        switch (profile.Kind)
        {
            case TableKind.Rooms:
            {
                var map = RowInt(row, "Map Number", "Map", "MapNumber");
                var room = RowInt(row, "Room Number", "Room", "RoomNumber");
                if (map <= 0 || room <= 0) return false;
                targetId = $"room.map{map}.{room}";
                fields["id"] = targetId;
                fields["object_type"] = "room";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                fields["map_number"] = map;
                fields["room_number"] = room;
                SetIfText(fields, "name", RowText(row, "Name", "Room Name"));
                SetPath(fields, "environment.light_level", JsonValue.Create(RowInt(row, "Light")));
                SetPath(fields, "environment.control_room", JsonValue.Create(RowInt(row, "Control Room", "ControlRoom")));
                var shop = RowInt(row, "Shop Number", "Shop#", "Shop");
                if (shop > 0) fields["services"] = new JsonArray(JsonValue.Create($"shop.{shop}"));
                SetRoomCurrency(fields, row);
                var exitIds = ImportedRoomExitIds(row, map, room);
                if (exitIds.Count > 0) SetPath(fields, "relationships.exits", ToJsonArray(exitIds));
                break;
            }
            case TableKind.Items:
            {
                var number = RowInt(row, "Item #", "Item Number", "Number");
                if (number <= 0) return false;
                targetId = $"item.{number}";
                fields["id"] = targetId;
                fields["legacy_id"] = number.ToString();
                fields["object_type"] = "item";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                SetIfText(fields, "name", RowText(row, "Name"));
                SetPath(fields, "economy.weight", JsonValue.Create(RowInt(row, "Weight")));
                SetPath(fields, "economy.cost", JsonValue.Create(RowInt(row, "Price", "Cost")));
                SetPath(fields, "equipment.worn_on", JsonValue.Create(RowInt(row, "Worn On", "WornOn")));
                SetPath(fields, "equipment.str_requirement", JsonValue.Create(RowInt(row, "Req Str", "Required Strength", "StrReq")));
                SetPath(fields, "combat.speed", JsonValue.Create(RowInt(row, "Speed")));
                SetPath(fields, "messages.miss", JsonValue.Create(RowInt(row, "Miss Msg", "Miss Message")));
                SetPath(fields, "messages.hit", JsonValue.Create(RowInt(row, "Hit Msg", "Hit Message")));
                SetPath(fields, "messages.read", JsonValue.Create(RowInt(row, "Read Msg", "Read Message")));
                SetPath(fields, "flags.gettable", JsonValue.Create(RowBool(row, "Gettable")));
                SetPath(fields, "public_reference_0139.fields.game_limit", JsonValue.Create(RowInt(row, "Game Limit")));
                SetPath(fields, "public_reference_0139.fields.max_uses", JsonValue.Create(RowInt(row, "Uses", "UseCount")));
                SetPath(fields, "public_reference_0139.fields.not_droppable", JsonValue.Create(RowBool(row, "Not Droppable")));
                SetPath(fields, "public_reference_0139.fields.destroy_on_death", JsonValue.Create(RowBool(row, "Destroy On Death")));
                SetPath(fields, "public_reference_0139.fields.retain_after_uses", JsonValue.Create(RowBool(row, "Retain After Uses", "RetainAfterUser")));
                var descriptions = CollectNumberedStrings(row, "Desc", 6);
                if (descriptions.Count > 0) SetPath(fields, "public_reference_0139.fields.descriptions", ToJsonArray(descriptions));
                break;
            }
            case TableKind.Monsters:
            {
                var number = RowInt(row, "Monster #", "Monster Number", "Number");
                if (number <= 0) return false;
                targetId = $"npc.{number}";
                fields["id"] = targetId;
                fields["legacy_id"] = number.ToString();
                fields["object_type"] = "npc";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                SetIfText(fields, "name", RowText(row, "Name"));
                SetPath(fields, "combat.hp", JsonValue.Create(RowInt(row, "Hit Points", "HP")));
                SetPath(fields, "behavior.follow", JsonValue.Create(RowInt(row, "Follow", "Follow%")));
                SetPath(fields, "behavior.group_id", JsonValue.Create(RowInt(row, "Index", "Group")));
                SetPath(fields, "behavior.charm_level", JsonValue.Create(RowInt(row, "Charm LvL", "CharmLVL")));
                SetPath(fields, "behavior.charm_resistance", JsonValue.Create(RowInt(row, "Charm RES")));
                SetPath(fields, "behavior.undead", JsonValue.Create(RowBool(row, "Undead")));
                SetPath(fields, "behavior.regen_time", JsonValue.Create(RowInt(row, "Regen Time", "RegenTime")));
                SetIfText(fields, "behavior.alignment", RowText(row, "Alignment"));
                SetPath(fields, "rewards.xp_base", JsonValue.Create(RowInt(row, "Experience")));
                SetPath(fields, "rewards.exp_multiplier", JsonValue.Create(RowInt(row, "Exp Multiplier", "Experience Multiplier")));
                SetPath(fields, "messages.move", JsonValue.Create(RowInt(row, "Move Msg", "Move Message")));
                SetPath(fields, "messages.death", JsonValue.Create(RowInt(row, "Death Msg", "Death Message")));
                SetPath(fields, "messages.greet_textblock", JsonValue.Create(RowInt(row, "Greet Txt", "Greet Text")));
                SetPath(fields, "messages.description_textblock", JsonValue.Create(RowInt(row, "Desc Txt", "Description Text")));
                SetPath(fields, "messages.talk_textblock", JsonValue.Create(RowInt(row, "Talk Txt", "Talk Text")));
                fields["death_spell_id"] = RowInt(row, "Death Spell");
                fields["create_spell_id"] = RowInt(row, "Create Spell");
                SetPath(fields, "public_reference_0139.fields.hp_regen", JsonValue.Create(RowInt(row, "HP Regen", "HPRegen")));
                SetPath(fields, "public_reference_0139.fields.backstab_defense", JsonValue.Create(RowInt(row, "BS Defense")));
                var attacks = ImportMonsterAttacks(row);
                if (attacks.Count > 0) fields["attacks"] = attacks;
                break;
            }
            case TableKind.Spells:
            {
                var number = RowInt(row, "Spell #", "Spell Number", "Number");
                if (number <= 0) return false;
                targetId = $"spell.{number}";
                fields["id"] = targetId;
                fields["legacy_id"] = number.ToString();
                fields["object_type"] = "spell";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                SetIfText(fields, "name", RowText(row, "Name"));
                SetIfText(fields, "short_name", RowText(row, "Short Name"));
                SetPath(fields, "costs.energy_cost", JsonValue.Create(RowInt(row, "Energy", "EnergyCost")));
                SetPath(fields, "costs.mana_cost", JsonValue.Create(RowInt(row, "Mana", "ManaCost")));
                SetPath(fields, "classification.spell_type", JsonValue.Create(RowText(row, "Spell Type")));
                SetPath(fields, "classification.target_type", JsonValue.Create(RowText(row, "Target", "Targets")));
                SetPath(fields, "classification.attack_type", JsonValue.Create(RowText(row, "Attack Type", "AttType")));
                SetPath(fields, "effect.duration", JsonValue.Create(RowInt(row, "Duration", "Length")));
                SetPath(fields, "messages.cast_a", JsonValue.Create(RowInt(row, "Cast MSG A", "Cast Message A")));
                SetPath(fields, "messages.cast_b", JsonValue.Create(RowInt(row, "Cast MSG B", "Cast Message B")));
                SetIfText(fields, "public_reference_0139.fields.message_style", RowText(row, "MSG Style", "Message Style"));
                SetPath(fields, "public_reference_0139.fields.resist_type", JsonValue.Create(RowInt(row, "Type of Resists", "Type of Resist")));
                SetPath(fields, "public_reference_0139.fields.resist_ability", JsonValue.Create(RowInt(row, "Resist Ability")));
                SetPath(fields, "public_reference_0139.fields.difficulty", JsonValue.Create(RowInt(row, "Difficulty")));
                SetPath(fields, "public_reference_0139.fields.magery_requirement_a", JsonValue.Create(RowInt(row, "Magery A", "Magery Type")));
                SetPath(fields, "public_reference_0139.fields.magery_requirement_b", JsonValue.Create(RowInt(row, "Magery B", "Magery Level")));
                SetPath(fields, "public_reference_0139.fields.level_cap", JsonValue.Create(RowInt(row, "Level Cap")));
                SetPath(fields, "public_reference_0139.fields.levels_for_max_increase", JsonValue.Create(RowInt(row, "LVLS Max Increase", "MaxIncLVLs")));
                SetPath(fields, "public_reference_0139.fields.max_increase", JsonValue.Create(RowInt(row, "Max Increase", "MaxInc")));
                SetPath(fields, "public_reference_0139.fields.levels_for_min_increase", JsonValue.Create(RowInt(row, "LVLS Min Increase", "MinIncLVLs")));
                SetPath(fields, "public_reference_0139.fields.min_increase", JsonValue.Create(RowInt(row, "Min Increase", "MinInc")));
                SetPath(fields, "public_reference_0139.fields.levels_for_duration_increase", JsonValue.Create(RowInt(row, "LVLS Dur Increase", "DurIncLVLs")));
                SetPath(fields, "public_reference_0139.fields.duration_increase", JsonValue.Create(RowInt(row, "Dur Increase", "DurInc")));
                var descriptions = new[] { RowText(row, "Desc 1", "Desc1"), RowText(row, "Desc 2", "Desc2") }.Where(value => value.Length > 0).ToArray();
                if (descriptions.Length > 0) SetPath(fields, "public_reference_0139.fields.descriptions", ToJsonArray(descriptions));
                break;
            }
            case TableKind.Shops:
            {
                var number = RowInt(row, "Shop #", "Shop Number", "Number");
                if (number <= 0) return false;
                targetId = $"shop.{number}";
                fields["id"] = targetId;
                fields["legacy_id"] = number.ToString();
                fields["object_type"] = "shop";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                SetIfText(fields, "name", RowText(row, "Name"));
                SetPath(fields, "service_type", JsonValue.Create(RowInt(row, "ShopType", "Shop Type")));
                SetPath(fields, "level_range.min", JsonValue.Create(RowInt(row, "Min Lvl", "MinLVL")));
                SetPath(fields, "level_range.max", JsonValue.Create(RowInt(row, "Max Lvl", "MaxLVL")));
                SetPath(fields, "class_limit", JsonValue.Create(RowInt(row, "Class Limit", "ClassRest")));
                SetPath(fields, "markup_percent", JsonValue.Create(RowInt(row, "MarkUp", "Markup%", "Markup")));
                var inventory = ImportShopInventory(row);
                if (inventory.Count > 0) fields["inventory"] = inventory;
                SetPath(fields, "public_reference_0139.fields.descriptions.description_a", JsonValue.Create(RowText(row, "Desc A")));
                SetPath(fields, "public_reference_0139.fields.descriptions.description_b", JsonValue.Create(RowText(row, "Desc B")));
                SetPath(fields, "public_reference_0139.fields.descriptions.description_c", JsonValue.Create(RowText(row, "Desc C")));
                break;
            }
            case TableKind.Classes:
            {
                var number = RowInt(row, "Class #", "Number");
                if (number <= 0) return false;
                var name = RowText(row, "Name");
                targetId = name.Length > 0 ? "class." + Slug(name) : $"class.nmr.{number}";
                fields["id"] = targetId;
                fields["legacy_id"] = number.ToString();
                fields["object_type"] = "class";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                SetIfText(fields, "name", name);
                fields["min_hp"] = RowInt(row, "Min HP", "MinHits");
                fields["max_hp"] = RowInt(row, "Max HP", "MaxHits");
                fields["exp_table"] = RowInt(row, "ExpTable");
                fields["magic_type"] = RowInt(row, "Magic Type", "MageryType");
                fields["magic_level"] = RowInt(row, "Magic LVL", "MageryLVL", "Magery Level");
                fields["combat_level"] = RowInt(row, "Combat", "Combat Level", "CombatLVL");
                fields["title_text"] = RowInt(row, "Title Text");
                break;
            }
            case TableKind.Races:
            {
                var number = RowInt(row, "Race #", "Number");
                if (number <= 0) return false;
                var name = RowText(row, "Name");
                targetId = name.Length > 0 ? "race." + Slug(name) : $"race.nmr.{number}";
                fields["id"] = targetId;
                fields["legacy_id"] = number.ToString();
                fields["object_type"] = "race";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                SetIfText(fields, "name", name);
                foreach (var stat in new[] { "INT", "WIL", "STR", "HEA", "AGL", "CHM" })
                {
                    SetPath(fields, "stats.min_" + stat.ToLowerInvariant(), JsonValue.Create(RowInt(row, "Min " + stat, "m" + stat)));
                    SetPath(fields, "stats.max_" + stat.ToLowerInvariant(), JsonValue.Create(RowInt(row, "Max " + stat, "x" + stat)));
                }
                SetPath(fields, "stats.hp_bonus", JsonValue.Create(RowInt(row, "HP Bonus", "HPPerLVL")));
                break;
            }
            case TableKind.Messages:
            {
                var number = RowInt(row, "Message #", "Number");
                if (number <= 0) return false;
                targetId = $"native_message_payload.{number}";
                fields["id"] = targetId;
                fields["object_type"] = "native_message_payload";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                fields["message_id"] = number;
                fields["player_payload"] = RowText(row, "Line 1", "Line1");
                fields["secondary_payload"] = RowText(row, "Line 2", "Line2");
                fields["tertiary_payload"] = RowText(row, "Line 3", "Line3");
                break;
            }
            case TableKind.Textblocks:
            {
                var number = RowInt(row, "Textblock #", "Number");
                if (number <= 0) return false;
                var part = Math.Max(1, RowInt(row, "Part #", "Part#", "Part"));
                targetId = $"native_textblock_payload.{number}";
                fields["id"] = targetId;
                fields["object_type"] = "native_textblock_payload";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                fields["textblock_id"] = number;
                fields["part_number"] = part;
                fields["link_to"] = RowInt(row, "Link To", "LinkTo");
                fields["payload"] = RowText(row, "Data", "Text");
                break;
            }
            case TableKind.Actions:
            {
                var number = RowInt(row, "Action #", "Action", "Number");
                if (number <= 0) return false;
                targetId = $"interaction.nmr.action.{number}";
                fields["id"] = targetId;
                fields["object_type"] = "room_interaction";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                fields["interaction_kind"] = "nightmare_action";
                fields["action_number"] = number;
                var messageMap = new Dictionary<string, string>
                {
                    ["single_to_user"] = "Single to User", ["single_to_room"] = "Single to Room",
                    ["user_to_user"] = "User to User", ["user_to_other_user"] = "User to Other User", ["user_to_room"] = "User to Room",
                    ["monster_to_user"] = "Monster to User", ["monster_to_room"] = "Monster to Room",
                    ["inventory_to_user"] = "Inventory to User", ["inventory_to_room"] = "Inventory to Room",
                    ["floor_item_to_user"] = "Floor Item to User", ["floor_item_to_room"] = "Floor Item to Room"
                };
                foreach (var pair in messageMap) SetPath(fields, "messages." + pair.Key, JsonValue.Create(RowInt(row, pair.Value)));
                break;
            }
            case TableKind.Abilities:
            {
                var number = RowInt(row, "Number", "Ability #", "AOIndex");
                if (number <= 0) return false;
                targetId = $"ability.{number}";
                fields["id"] = targetId;
                fields["legacy_id"] = number.ToString();
                fields["object_type"] = "ability";
                fields["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId));
                SetIfText(fields, "name", RowText(row, "Name"));
                SetPath(fields, "public_reference_0139.fields.description", JsonValue.Create(RowText(row, "Description")));
                SetPath(fields, "public_reference_0139.fields.color_red", JsonValue.Create(RowInt(row, "RED")));
                SetPath(fields, "public_reference_0139.fields.color_green", JsonValue.Create(RowInt(row, "GREEN")));
                SetPath(fields, "public_reference_0139.fields.color_blue", JsonValue.Create(RowInt(row, "BLUE")));
                break;
            }
            default:
                return false;
        }
        AttachUnmappedMdbFields(profile.Kind, row, fields);
        mapped = new MappedRecord(profile.Family, targetId, fields);
        return true;
    }

    private static readonly (int Slot, string Direction)[] NightmareRoomDirections =
    [
        (0, "N"), (1, "NE"), (2, "E"), (3, "SE"), (4, "S"),
        (5, "SW"), (6, "W"), (7, "NW"), (8, "U"), (9, "D")
    ];

    private static IReadOnlyList<string> ImportedRoomExitIds(JsonObject row, int map, int room)
    {
        var result = new List<string>();
        foreach (var (slot, direction) in NightmareRoomDirections)
        {
            if (!TryNightmareExitDestination(row, slot, map, out _, out _)) continue;
            result.Add($"exit.map{map}.{room}.{direction.ToLowerInvariant()}");
        }
        return result;
    }

    private static IEnumerable<MappedRecord> ImportRoomExits(JsonObject row)
    {
        var map = RowInt(row, "Map Number", "Map", "MapNumber");
        var room = RowInt(row, "Room Number", "Room", "RoomNumber");
        if (map <= 0 || room <= 0) yield break;
        foreach (var (slot, direction) in NightmareRoomDirections)
        {
            if (!TryNightmareExitDestination(row, slot, map, out var toMap, out var toRoom)) continue;
            var id = $"exit.map{map}.{room}.{direction.ToLowerInvariant()}";
            var fields = new JsonObject
            {
                ["id"] = id,
                ["object_type"] = "exit",
                ["world_scope"] = new JsonArray(JsonValue.Create(CustomWorldEditorService.CustomWorldId)),
                ["direction"] = direction,
                ["from_map_number"] = map,
                ["from_room_number"] = room,
                ["to_map_number"] = toMap,
                ["to_room_number"] = toRoom,
                ["gate_kind"] = "open",
                ["relationships"] = new JsonObject
                {
                    ["from_room"] = $"room.map{map}.{room}",
                    ["to_room"] = $"room.map{toMap}.{toRoom}"
                },
                ["nightmare_mdb"] = new JsonObject
                {
                    ["slot"] = slot,
                    ["attributes"] = RowText(row, $"Attributes {slot}"),
                    ["description"] = RowText(row, $"Desc {slot}"),
                    ["exit"] = RowText(row, $"Exit {slot}"),
                    ["type"] = RowText(row, $"Type {slot}"),
                    ["para1"] = RowText(row, $"Para1 {slot}"),
                    ["para2"] = RowText(row, $"Para2 {slot}"),
                    ["para3"] = RowText(row, $"Para3 {slot}"),
                    ["para4"] = RowText(row, $"Para4 {slot}")
                }
            };
            yield return new MappedRecord("exits", id, fields);
        }
    }

    private static bool TryNightmareExitDestination(JsonObject row, int slot, int defaultMap, out int map, out int room)
    {
        map = defaultMap;
        room = 0;
        var node = RowNode(row, $"Exit Room {slot}", $"ExitRoom {slot}", $"ExitRoom{slot}");
        var text = NodeText(node).Trim();
        if (text.Length == 0) return false;
        var match = Regex.Match(text, @"^(?<map>\d+)\s*[/,:]\s*(?<room>\d+)$");
        if (match.Success)
        {
            map = int.Parse(match.Groups["map"].Value, System.Globalization.CultureInfo.InvariantCulture);
            room = int.Parse(match.Groups["room"].Value, System.Globalization.CultureInfo.InvariantCulture);
            return map > 0 && room > 0;
        }
        room = NodeInt(node);
        return room > 0;
    }

    private static MappedRecord MergeMappedRows(MappedRecord existing, MappedRecord incoming)
    {
        // Nightmare Textblocks may contain multiple Part # rows for one logical textblock.
        // Reforged stores the effective textblock as one payload, so preserve row order by joining parts.
        if (existing.Family.Equals("native_textblock_payloads", StringComparison.OrdinalIgnoreCase))
        {
            var left = TextAt(existing.Fields, "payload");
            var right = TextAt(incoming.Fields, "payload");
            if (right.Length > 0)
            {
                existing.Fields["payload"] = left.Length == 0 ? right : left + Environment.NewLine + right;
            }
            var linkTo = FirstInt(incoming.Fields, "link_to");
            if (linkTo > 0) existing.Fields["link_to"] = linkTo;
            existing.Fields["part_number"] = 1;
            return existing;
        }

        MergeJson(existing.Fields, incoming.Fields);
        return existing;
    }

    private static void MergeJson(JsonObject target, JsonObject source)
    {
        foreach (var pair in source)
        {
            if (pair.Value is JsonObject sourceObject && target[pair.Key] is JsonObject targetObject)
            {
                MergeJson(targetObject, sourceObject);
                continue;
            }
            target[pair.Key] = pair.Value?.DeepClone();
        }
    }


    private static void AttachUnmappedMdbFields(TableKind kind, JsonObject row, JsonObject fields)
    {
        var preserved = new JsonObject();
        foreach (var pair in row)
        {
            if (pair.Value is null || IsPromotedColumn(kind, pair.Key)) continue;
            preserved[pair.Key] = pair.Value.DeepClone();
        }
        if (preserved.Count == 0) return;
        SetPath(fields, "legacy_sources.nightmare_mdb.unmapped_fields", preserved);
        SetPath(fields, "legacy_sources.nightmare_mdb.unmapped_field_count", JsonValue.Create(preserved.Count));
    }

    private static bool IsPromotedColumn(TableKind kind, string column)
    {
        var key = Normalize(column);
        if (key.Length == 0) return false;
        return kind switch
        {
            TableKind.Rooms => key is "mapnumber" or "map" or "roomnumber" or "room" or "name" or "roomname" or "light" or "shopnumber" or "shop" or "controlroom"
                or "runic" or "platinum" or "gold" or "silver" or "copper" or "invisrunic" or "invisplatinum" or "invisgold" or "invissilver" or "inviscopper"
                || Regex.IsMatch(key, @"^(exitroom|attributes|desc|exit|type|para[1-4])\d+$"),
            TableKind.Items => key is "item" or "itemnumber" or "number" or "name" or "weight" or "price" or "cost" or "wornon" or "reqstr" or "requiredstrength" or "strreq"
                or "speed" or "gettable" or "gamelimit" or "uses" or "usecount" or "notdroppable" or "destroyondeath" or "retainafteruses" or "retainafteruser"
                or "missmsg" or "missmessage" or "hitmsg" or "hitmessage" or "readmsg" or "readmessage"
                || Regex.IsMatch(key, @"^desc[1-6]$"),
            TableKind.Monsters => key is "monster" or "monsternumber" or "number" or "name" or "hitpoints" or "hp" or "follow" or "index" or "group" or "charmlvl" or "charmlevel"
                or "charmres" or "undead" or "regentime" or "experience" or "hpregen" or "bsdefense" or "alignment" or "expmultiplier" or "experiencemultiplier"
                or "movemsg" or "movemessage" or "deathmsg" or "deathmessage" or "greettxt" or "greettext" or "desctxt" or "descriptiontext" or "talktxt" or "talktext"
                or "deathspell" or "createspell"
                || Regex.IsMatch(key, @"^attack(type|accuspell|minhitcast|maxhitcastlvl|energy|hitspell)?\d+$"),
            TableKind.Spells => key is "spell" or "spellnumber" or "number" or "name" or "shortname" or "energy" or "energycost" or "mana" or "manacost" or "spelltype"
                or "target" or "targets" or "attacktype" or "atttype" or "duration" or "length" or "typeofresists" or "typeofresist" or "resistability" or "difficulty"
                or "magerya" or "magerytype" or "mageryb" or "magerylevel" or "levelcap" or "lvlsmaxincrease" or "maxinclvls" or "maxincrease" or "maxinc"
                or "lvlsminincrease" or "mininclvls" or "minincrease" or "mininc" or "lvlsdurincrease" or "durinclvls" or "durincrease" or "durinc"
                or "castmsga" or "castmessagea" or "castmsgb" or "castmessageb" or "msgstyle" or "messagestyle" or "desc1" or "desc2",
            TableKind.Shops => key is "shop" or "shopnumber" or "number" or "name" or "shoptype" or "minlvl" or "maxlvl" or "classlimit" or "classrest" or "markup" or "desca" or "descb" or "descc"
                || Regex.IsMatch(key, @"^(item|max|normal|regentime|regennumber|regen|regenpercent)\d+$"),
            TableKind.Classes => key is "class" or "number" or "name" or "minhp" or "minhits" or "maxhp" or "maxhits" or "exptable" or "magictype" or "magerytype"
                or "magiclvl" or "magerylvl" or "magerylevel" or "combat" or "combatlevel" or "combatlvl" or "titletext",
            TableKind.Races => key is "race" or "number" or "name" or "hpbonus" or "hpperlvl"
                || Regex.IsMatch(key, @"^(min|max)(int|wil|str|hea|agl|chm)$") || Regex.IsMatch(key, @"^[mx](int|wil|str|hea|agl|chm)$"),
            TableKind.Messages => key is "message" or "number" or "line1" or "line2" or "line3",
            TableKind.Textblocks => key is "textblock" or "number" or "part" or "linkto" or "data" or "text",
            TableKind.Actions => key is "action" or "number" or "singletouser" or "singletoroom" or "usertouser" or "usertootheruser" or "usertoroom"
                or "monstertouser" or "monstertoroom" or "inventorytouser" or "inventorytoroom" or "flooritemtouser" or "flooritemtoroom",
            TableKind.Abilities => key is "number" or "ability" or "aoindex" or "name" or "description" or "red" or "green" or "blue",
            _ => false
        };
    }

    private static bool AttachWccAnchorEvidence(
        TableProfile profile,
        JsonObject row,
        JsonObject fields,
        string folder,
        IDictionary<TableKind, IReadOnlyList<WccFileData>> cache)
    {
        if (!cache.TryGetValue(profile.Kind, out var files))
        {
            files = LoadCandidateWccFiles(folder, profile.Kind);
            cache[profile.Kind] = files;
        }
        var anchor = GapAnchor(profile.Kind, row);
        if (anchor.Length < 4 || !TryFindUniqueAnchor(files, anchor, out var file, out var offset)) return false;
        SetPath(fields, "legacy_sources.wcc.file", JsonValue.Create(file.Name));
        SetPath(fields, "legacy_sources.wcc.sha256", JsonValue.Create(file.Sha256));
        SetPath(fields, "legacy_sources.wcc.anchor", JsonValue.Create(anchor));
        SetPath(fields, "legacy_sources.wcc.anchor_offset", JsonValue.Create(offset));
        SetPath(fields, "legacy_sources.wcc.classification", JsonValue.Create("EXACT_UNIQUE_TEXT_ANCHOR"));
        return true;
    }

    private static IReadOnlyList<WccFileData> LoadCandidateWccFiles(string folder, TableKind kind)
    {
        var paths = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path).Equals(".dat", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".bin", StringComparison.OrdinalIgnoreCase))
            .Where(path => IsCandidateWccFile(kind, Path.GetFileName(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var result = new List<WccFileData>(paths.Length);
        foreach (var path in paths)
        {
            try
            {
                var data = File.ReadAllBytes(path);
                result.Add(new WccFileData(Path.GetFileName(path), path, Sha256(path), data, BuildAsciiAnchorIndex(data)));
            }
            catch { }
        }
        return result;
    }

    private static bool IsCandidateWccFile(TableKind kind, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        static bool Prefix(string name, params string[] prefixes) => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
        return kind switch
        {
            TableKind.Rooms => Prefix(name, "WCCMP", "WBBMP"),
            TableKind.Items => Prefix(name, "WCCITEM", "WBBITEM"),
            TableKind.Monsters => Prefix(name, "WCCKNMS", "WBBKNMS", "WCCACMS", "WBBACMS", "WCCMON", "WBBMON"),
            TableKind.Spells => Prefix(name, "WCCSPEL", "WBBSPEL"),
            TableKind.Shops => Prefix(name, "WCCSHOP", "WBBSHOP"),
            TableKind.Classes => Prefix(name, "WCCCLAS", "WBBCLAS"),
            TableKind.Races => Prefix(name, "WCCRACE", "WBBRACE"),
            TableKind.Messages => Prefix(name, "WCCMSG", "WBBMSG"),
            TableKind.Textblocks => Prefix(name, "WCCTEXT", "WBBTEXT"),
            TableKind.Actions => Prefix(name, "WCCACT", "WBBACT"),
            TableKind.Abilities => Prefix(name, "WCCABIL", "WBBABIL", "WCCDATA", "WBBDATA"),
            _ => false
        };
    }

    private static string GapAnchor(TableKind kind, JsonObject row)
    {
        var value = kind switch
        {
            TableKind.Rooms => RowText(row, "Name", "Room Name"),
            TableKind.Items => RowText(row, "Name"),
            TableKind.Monsters => RowText(row, "Name"),
            TableKind.Spells => RowText(row, "Name"),
            TableKind.Shops => RowText(row, "Name"),
            TableKind.Classes => RowText(row, "Name"),
            TableKind.Races => RowText(row, "Name"),
            TableKind.Messages => RowText(row, "Line 1", "Line1"),
            TableKind.Textblocks => RowText(row, "Data", "Text"),
            TableKind.Abilities => RowText(row, "Name"),
            _ => string.Empty
        };
        value = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        if (value.Length > 80) value = value[..80];
        if (value.Any(ch => ch < 32 || ch > 255)) return string.Empty;
        return value;
    }

    private static bool TryFindUniqueAnchor(IReadOnlyList<WccFileData> files, string anchor, out WccFileData file, out int offset)
    {
        file = default!;
        offset = -1;
        var key = NormalizeEvidenceString(anchor);
        if (key.Length < 4) return false;
        var total = 0;
        foreach (var candidate in files)
        {
            if (!candidate.AnchorOffsets.TryGetValue(key, out var hits)) continue;
            foreach (var hit in hits)
            {
                total++;
                if (total == 1) { file = candidate; offset = hit; }
                if (total > 1) return false;
            }
        }
        return total == 1;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<int>> BuildAsciiAnchorIndex(byte[] data)
    {
        var mutable = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var start = -1;
        for (var i = 0; i <= data.Length; i++)
        {
            var printable = i < data.Length && data[i] is >= 32 and <= 126;
            if (printable)
            {
                if (start < 0) start = i;
                continue;
            }
            if (start >= 0 && i - start >= 4)
            {
                var text = Encoding.Latin1.GetString(data, start, i - start);
                var key = NormalizeEvidenceString(text);
                if (key.Length >= 4)
                {
                    if (!mutable.TryGetValue(key, out var offsets)) { offsets = []; mutable[key] = offsets; }
                    if (offsets.Count < 3) offsets.Add(start);
                }
            }
            start = -1;
        }
        return mutable.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeEvidenceString(string? value) => Regex.Replace(value?.Trim().ToLowerInvariant() ?? string.Empty, @"\s+", " ");

    private static IReadOnlyList<string> RowStringValues(JsonObject row)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in row)
        {
            if (pair.Value is null) continue;
            var text = NodeText(pair.Value).Trim();
            if (text.Length >= 4) values.Add(Regex.Replace(text, @"\s+", " "));
        }
        return values.ToArray();
    }

    private static IReadOnlyList<string> ExtractAsciiStrings(byte[] bytes, int minimumLength)
    {
        var result = new List<string>();
        var buffer = new List<byte>();
        void Flush()
        {
            if (buffer.Count >= minimumLength)
            {
                var text = Encoding.Latin1.GetString(buffer.ToArray()).Trim();
                if (text.Length >= minimumLength) result.Add(text);
            }
            buffer.Clear();
        }
        foreach (var value in bytes)
        {
            if (value is >= 32 and <= 126) buffer.Add(value); else Flush();
        }
        Flush();
        return result;
    }

    private static IReadOnlyList<WccGapCorrelation> AnalyzeGapCorrelations(TableProfile profile, MdbTable table, IReadOnlyList<WccFileData> files, IReadOnlyList<string> preservedColumns)
    {
        if (files.Count == 0 || preservedColumns.Count == 0) return [];
        var results = new List<WccGapCorrelation>();
        foreach (var column in preservedColumns)
        {
            var votes = new Dictionary<(string File, int Width, int RelativeOffset), HashSet<int>>();
            var eligible = 0;
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                if (!TryNumericValue(row[column], out var value) || value == 0) continue;
                var anchor = GapAnchor(profile.Kind, row);
                if (anchor.Length < 4 || !TryFindUniqueAnchor(files, anchor, out var matchedFile, out var anchorOffset)) continue;
                eligible++;
                var start = Math.Max(0, anchorOffset - 512);
                var end = Math.Min(matchedFile.Data.Length, anchorOffset + 512);
                foreach (var width in new[] { 1, 2, 4 })
                {
                    var pattern = NumericPattern(value, width);
                    if (pattern is null) continue;
                    for (var position = start; position <= end - pattern.Length; position++)
                    {
                        var same = true;
                        for (var j = 0; j < pattern.Length; j++)
                        {
                            if (matchedFile.Data[position + j] == pattern[j]) continue;
                            same = false;
                            break;
                        }
                        if (!same) continue;
                        var key = (matchedFile.Name, width, position - anchorOffset);
                        if (!votes.TryGetValue(key, out var rowSet)) { rowSet = []; votes[key] = rowSet; }
                        rowSet.Add(rowIndex);
                    }
                }
            }
            if (eligible < 3 || votes.Count == 0) continue;
            var best = votes.OrderByDescending(pair => pair.Value.Count).ThenBy(pair => Math.Abs(pair.Key.RelativeOffset)).First();
            var matched = best.Value.Count;
            var confidence = matched / (double)eligible;
            var classification = confidence >= 0.80 && matched >= 4 ? "STRONGLY_CORRELATED" : confidence >= 0.60 && matched >= 3 ? "CANDIDATE" : "WEAK";
            if (classification == "WEAK") continue;
            results.Add(new WccGapCorrelation(column, best.Key.File, best.Key.RelativeOffset, best.Key.Width, matched, eligible, confidence, classification));
        }
        return results;
    }

    private static bool TryNumericValue(JsonNode? node, out long value)
    {
        value = 0;
        if (node is null) return false;
        try { value = node.GetValue<long>(); return true; } catch { }
        try { value = node.GetValue<int>(); return true; } catch { }
        try { value = node.GetValue<bool>() ? 1 : 0; return true; } catch { }
        return long.TryParse(NodeText(node), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static byte[]? NumericPattern(long value, int width)
    {
        return width switch
        {
            1 when value is >= byte.MinValue and <= byte.MaxValue => [(byte)value],
            2 when value is >= short.MinValue and <= ushort.MaxValue => BitConverter.GetBytes(unchecked((short)value)),
            4 when value is >= int.MinValue and <= uint.MaxValue => BitConverter.GetBytes(unchecked((int)value)),
            _ => null
        };
    }

    private static JsonArray ImportMonsterAttacks(JsonObject row)
    {
        var attacks = new JsonArray();
        for (var slot = 1; slot <= 20; slot++)
        {
            var type = RowInt(row, $"Attack Type {slot}", $"AttType-{slot}");
            var accuracy = RowInt(row, $"Attack Accu/Spell {slot}", $"AttAcc-{slot}");
            var percent = RowInt(row, $"Attack % {slot}", $"Att%-{slot}");
            var minimum = RowInt(row, $"Attack Min Hit/Cast % {slot}", $"AttMin-{slot}");
            var maximum = RowInt(row, $"Attack Max Hit/Cast LVL {slot}", $"AttMax-{slot}");
            var energy = RowInt(row, $"Attack Energy {slot}", $"AttEnergy-{slot}");
            var hitSpell = RowInt(row, $"Attack Hit Spell {slot}", $"AttHitSpell-{slot}");
            if (type == 0 && accuracy == 0 && percent == 0 && minimum == 0 && maximum == 0 && energy == 0 && hitSpell == 0) continue;
            attacks.Add(new JsonObject
            {
                ["slot"] = slot,
                ["type"] = type,
                ["accuracy"] = accuracy,
                ["selection_percent"] = percent,
                ["minimum_damage"] = minimum,
                ["maximum_damage"] = maximum,
                ["energy_cost"] = energy,
                ["hit_spell_id"] = hitSpell
            });
        }
        return attacks;
    }

    private static JsonArray ImportShopInventory(JsonObject row)
    {
        var inventory = new JsonArray();
        for (var slot = 1; slot <= 200; slot++)
        {
            var item = RowInt(row, $"Item {slot}", $"Item-{slot}");
            if (item <= 0) continue;
            inventory.Add(new JsonObject
            {
                ["item_ref"] = $"item.{item}",
                ["max_qty"] = RowInt(row, $"Max {slot}", $"Max-{slot}"),
                ["normal_qty"] = RowInt(row, $"Normal {slot}"),
                ["regen_time"] = RowInt(row, $"Regen Time {slot}", $"Time-{slot}"),
                ["regen_qty"] = RowInt(row, $"Regen Number {slot}", $"Amount-{slot}"),
                ["regen_percent"] = RowInt(row, $"Regen % {slot}")
            });
        }
        return inventory;
    }

    private static void SetRoomCurrency(JsonObject fields, JsonObject row)
    {
        foreach (var coin in new[] { "runic", "platinum", "gold", "silver", "copper" })
        {
            var title = char.ToUpperInvariant(coin[0]) + coin[1..];
            SetPath(fields, "currency.visible." + coin, JsonValue.Create(RowInt(row, title)));
            SetPath(fields, "currency.hidden." + coin, JsonValue.Create(RowInt(row, "Invis" + title)));
        }
    }

    private MdbDump ReadMdbDump(string path)
    {
        path = RequireMdb(path);
        using AccessReader reader = AccessReader.Open(path);
        var tables = new JsonArray();
        foreach (var tableName in reader.ListTables()
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Where(name => !name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                     .Where(name => !name.StartsWith('~'))
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            DataTable table;
            try
            {
                table = reader.ReadTable(tableName);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"JetDatabaseReader could not read MDB table '{tableName}': {exception.Message}", exception);
            }

            var columns = new JsonArray();
            foreach (DataColumn column in table.Columns)
                columns.Add(column.ColumnName);

            var rows = new JsonArray();
            foreach (DataRow dataRow in table.Rows)
            {
                var row = new JsonObject();
                foreach (DataColumn column in table.Columns)
                    row[column.ColumnName] = ToJsonCell(dataRow[column]);
                rows.Add(row);
            }

            tables.Add(new JsonObject
            {
                ["name"] = tableName,
                ["columns"] = columns,
                ["rows"] = rows
            });
        }

        return new MdbDump(new JsonObject
        {
            ["schema"] = "nightmare_mdb_dump_v2",
            ["provider"] = "JetDatabaseReader 2.2.0 (pure managed)",
            ["source"] = Path.GetFullPath(path),
            ["tables"] = tables
        });
    }

    private static JsonNode? ToJsonCell(object? value)
    {
        if (value is null || value is DBNull) return null;
        return value switch
        {
            byte[] bytes => JsonValue.Create(Convert.ToBase64String(bytes)),
            DateTime dateTime => JsonValue.Create(dateTime.ToString("O")),
            DateTimeOffset dateTimeOffset => JsonValue.Create(dateTimeOffset.ToString("O")),
            Guid guid => JsonValue.Create(guid.ToString()),
            bool boolean => JsonValue.Create(boolean),
            byte number => JsonValue.Create(number),
            sbyte number => JsonValue.Create(number),
            short number => JsonValue.Create(number),
            ushort number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            uint number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            ulong number => JsonValue.Create(number),
            float number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            string text => JsonValue.Create(text),
            _ => JsonValue.Create(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))
        };
    }

    private void RunBridgeScript(string scriptName, IReadOnlyList<string> args)
    {
        var script = Path.Combine(_projectRoot, "Tools", "NightmareBridge", scriptName);
        if (!File.Exists(script)) throw new FileNotFoundException($"Nightmare bridge helper is missing: {script}", script);
        var powershell = ResolveBridgePowerShell();
        var start = new ProcessStartInfo
        {
            FileName = powershell,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = _projectRoot
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start the Nightmare MDB bridge helper.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"Nightmare MDB bridge helper failed (exit {process.ExitCode}): {message.Trim()}");
        }
    }

    private static string ResolveBridgePowerShell()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windows))
        {
            var wow64 = Path.Combine(windows, "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(wow64)) return wow64;
            var system = Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(system)) return system;
        }
        return "powershell.exe";
    }

    private static IReadOnlyList<MdbTable> ReadTables(JsonObject root)
    {
        var result = new List<MdbTable>();
        foreach (var table in (root["tables"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            var name = table["name"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (name.Length == 0) continue;
            var columns = (table["columns"] as JsonArray)?.Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => value.Length > 0).ToArray() ?? [];
            var rows = (table["rows"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
            result.Add(new MdbTable(name, columns, rows));
        }
        return result;
    }

    private static TableProfile? ResolveProfile(string tableName)
    {
        var key = Normalize(tableName);
        return key switch
        {
            "rooms" or "room" => new(TableKind.Rooms, "rooms"),
            "items" or "item" => new(TableKind.Items, "items"),
            "monsters" or "monster" or "npcs" or "npc" => new(TableKind.Monsters, "npcs"),
            "spells" or "spell" => new(TableKind.Spells, "spells"),
            "shops" or "shop" => new(TableKind.Shops, "shops"),
            "classes" or "class" => new(TableKind.Classes, "classes"),
            "races" or "race" => new(TableKind.Races, "races"),
            "messages" or "message" => new(TableKind.Messages, "native_message_payloads"),
            "textblocks" or "textblock" => new(TableKind.Textblocks, "native_textblock_payloads"),
            "actions" or "action" => new(TableKind.Actions, "room_interactions"),
            "data" or "abilities" or "ability" => new(TableKind.Abilities, "abilities"),
            _ => null
        };
    }

    private static string ClassifyWccFile(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        static bool Prefix(string value, params string[] prefixes) => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
        if (Prefix(name, "WCCMP", "WBBMP")) return "Rooms / Map";
        if (Prefix(name, "WCCITEM", "WBBITEM")) return "Items";
        if (Prefix(name, "WCCSPEL", "WBBSPEL")) return "Spells";
        if (Prefix(name, "WCCSHOP", "WBBSHOP")) return "Shops";
        if (Prefix(name, "WCCRACE", "WBBRACE")) return "Races";
        if (Prefix(name, "WCCCLAS", "WBBCLAS")) return "Classes";
        if (Prefix(name, "WCCMSG", "WBBMSG")) return "Messages";
        if (Prefix(name, "WCCTEXT", "WBBTEXT")) return "Textblocks";
        if (Prefix(name, "WCCACT", "WBBACT")) return "Actions";
        if (Prefix(name, "WCCABIL", "WBBABIL", "WCCDATA", "WBBDATA")) return "Abilities / Data";
        if (Prefix(name, "WCCACMS", "WBBACMS", "WCCKNMS", "WBBKNMS", "WCCMON", "WBBMON")) return "Monsters / Monster Support";
        if (Prefix(name, "WCCITOW", "WBBITOW")) return "Item Ownership / Item Support";
        if (Prefix(name, "WCCUPDA", "WBBUPDA")) return "Update / Runtime Support";
        if (Prefix(name, "WCCBANK", "WBBBANK")) return "Bank / Economy Support";
        if (Prefix(name, "WCCGANG", "WBBGANG")) return "Gang / Guild Support";
        if (Prefix(name, "WCCUSER", "WBBUSER")) return "World User State";
        if (name.StartsWith("WGS", StringComparison.Ordinal) || name.StartsWith("WOR", StringComparison.Ordinal)) return "Worldgroup Host Support";
        return string.Empty;
    }

    private static string RequireMdb(string path)
    {
        path = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? throw new InvalidOperationException("Choose a Nightmare MDB file first.") : path.Trim());
        if (!File.Exists(path)) throw new FileNotFoundException("Nightmare MDB file was not found.", path);
        if (!Path.GetExtension(path).Equals(".mdb", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Nightmare bridge input must be an .mdb file.");
        return path;
    }

    private static string RequireMdbOutput(string path)
    {
        path = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? throw new InvalidOperationException("Choose an MDB output path first.") : path.Trim());
        if (!Path.GetExtension(path).Equals(".mdb", StringComparison.OrdinalIgnoreCase)) path += ".mdb";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sha256Text(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FingerprintWccSet(IEnumerable<NightmareWccEvidenceFile> files)
    {
        var canonical = string.Join("\n", files
            .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(file => $"{file.FileName.ToLowerInvariant()}|{file.Bytes}|{file.Sha256.ToLowerInvariant()}"));
        return Sha256Text(canonical);
    }

    private static string MakeKey(string family, string targetId) => family.Trim().ToLowerInvariant() + "|" + targetId.Trim().ToLowerInvariant();
    private static string Normalize(string? value) => Regex.Replace(value?.Trim().ToLowerInvariant() ?? string.Empty, "[^a-z0-9]+", string.Empty);
    private static string Slug(string value) => Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

    private static JsonNode? RowNode(JsonObject row, params string[] names)
    {
        if (row.Count == 0) return null;
        var wanted = names.Select(Normalize).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in row)
            if (wanted.Contains(Normalize(pair.Key))) return pair.Value;
        return null;
    }

    private static string RowText(JsonObject row, params string[] names) => NodeText(RowNode(row, names));
    private static int RowInt(JsonObject row, params string[] names) => NodeInt(RowNode(row, names));
    private static bool RowBool(JsonObject row, params string[] names)
    {
        var node = RowNode(row, names);
        if (node is null) return false;
        try { return node.GetValue<bool>(); } catch { }
        var text = NodeText(node).Trim();
        if (bool.TryParse(text, out var value)) return value;
        if (int.TryParse(text, out var number)) return number != 0;
        return text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text.Equals("y", StringComparison.OrdinalIgnoreCase) || text.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string NodeText(JsonNode? node)
    {
        if (node is null) return string.Empty;
        try { return node.GetValue<string>()?.Trim() ?? string.Empty; } catch { }
        try { return node.GetValue<int>().ToString(); } catch { }
        try { return node.GetValue<long>().ToString(); } catch { }
        try { return node.GetValue<double>().ToString(System.Globalization.CultureInfo.InvariantCulture); } catch { }
        try { return node.GetValue<bool>() ? "true" : "false"; } catch { }
        return node.ToJsonString().Trim('"');
    }

    private static int NodeInt(JsonNode? node)
    {
        if (node is null) return 0;
        try { return node.GetValue<int>(); } catch { }
        try { return checked((int)node.GetValue<long>()); } catch { }
        try { return (int)Math.Round(node.GetValue<double>()); } catch { }
        var text = NodeText(node);
        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)) return value;
        var match = Regex.Match(text, @"-?\d+");
        return match.Success && int.TryParse(match.Value, out value) ? value : 0;
    }

    private static JsonNode? At(JsonObject root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is JsonObject obj)
            {
                var pair = obj.FirstOrDefault(item => item.Key.Equals(segment, StringComparison.OrdinalIgnoreCase));
                current = pair.Value;
                continue;
            }
            if (current is JsonArray array && int.TryParse(segment, out var index) && index >= 0 && index < array.Count)
            {
                current = array[index];
                continue;
            }
            return null;
        }
        return current;
    }

    private static string TextAt(JsonObject root, params string[] paths)
    {
        foreach (var path in paths)
        {
            var text = NodeText(At(root, path));
            if (text.Length > 0) return text;
        }
        return string.Empty;
    }

    private static int FirstInt(JsonObject root, params string[] paths)
    {
        foreach (var path in paths)
        {
            var node = At(root, path);
            if (node is null) continue;
            var value = NodeInt(node);
            if (value != 0 || NodeText(node) is "0" or "0.0") return value;
        }
        return 0;
    }

    private static bool BoolAt(JsonObject root, string path)
    {
        var node = At(root, path);
        if (node is null) return false;
        try { return node.GetValue<bool>(); } catch { }
        var text = NodeText(node);
        return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text == "1";
    }

    private static int LegacyNumber(JsonObject record)
    {
        var value = FirstInt(record, "legacy_id", "legacy_number", "sourceNumbers.number", "source_numbers.number", "number");
        if (value > 0) return value;
        var id = TextAt(record, "id");
        var match = Regex.Match(id, @"(?:^|\.)(\d+)$");
        return match.Success && int.TryParse(match.Groups[1].Value, out value) ? value : 0;
    }

    private static int NumericRef(JsonObject record, params string[] paths)
    {
        foreach (var path in paths)
        {
            var node = At(record, path);
            if (node is null) continue;
            var value = NumericNode(node);
            if (value > 0) return value;
        }
        return 0;
    }

    private static int NumericNode(JsonNode? node)
    {
        var direct = NodeInt(node);
        if (direct > 0) return direct;
        var match = Regex.Match(NodeText(node), @"(\d+)(?!.*\d)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }

    private static IReadOnlyList<string> StringArrayAt(JsonObject record, string path)
    {
        if (At(record, path) is not JsonArray array) return [];
        return array.Select(NodeText).Where(value => value.Length > 0).ToArray();
    }

    private static IReadOnlyList<string> CollectNumberedStrings(JsonObject row, string stem, int max)
    {
        var values = new List<string>();
        for (var i = 1; i <= max; i++)
        {
            var value = RowText(row, $"{stem}{i}", $"{stem} {i}");
            if (value.Length > 0) values.Add(value);
        }
        return values;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var result = new JsonArray();
        foreach (var value in values) result.Add(value);
        return result;
    }

    private static void SetIfText(JsonObject target, string path, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        SetPath(target, path, JsonValue.Create(value.Trim()));
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current[parts[i]] is not JsonObject next)
            {
                next = new JsonObject();
                current[parts[i]] = next;
            }
            current = next;
        }
        current[parts[^1]] = value;
    }

    private static bool TryRoomId(string? id, out int map, out int room)
    {
        map = 0; room = 0;
        var match = Regex.Match(id ?? string.Empty, @"room\.map(?<map>\d+)\.(?<room>\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["map"].Value, out map) && int.TryParse(match.Groups["room"].Value, out room);
    }

    private static JsonObject Column(string name, string type, int size = 0) => new() { ["name"] = name, ["type"] = type, ["size"] = size };
    private static JsonArray Columns(params JsonObject[] columns) => new(columns.Select(column => (JsonNode)column).ToArray());

    private static JsonArray RoomColumns()
    {
        var list = new List<JsonObject>
        {
            Column("Map Number", "LONG"), Column("Room Number", "LONG"), Column("Name", "TEXT", 255), Column("Light", "LONG"),
            Column("Shop Number", "LONG"), Column("Control Room", "LONG"), Column("Runic", "LONG"), Column("Platinum", "LONG"), Column("Gold", "LONG"), Column("Silver", "LONG"), Column("Copper", "LONG"),
            Column("InvisRunic", "LONG"), Column("InvisPlatinum", "LONG"), Column("InvisGold", "LONG"), Column("InvisSilver", "LONG"), Column("InvisCopper", "LONG")
        };
        for (var slot = 0; slot <= 9; slot++)
        {
            list.Add(Column($"Exit Room {slot}", "TEXT", 64));
            list.Add(Column($"Attributes {slot}", "TEXT", 64));
            list.Add(Column($"Desc {slot}", "TEXT", 255));
            list.Add(Column($"Exit {slot}", "TEXT", 255));
            list.Add(Column($"Type {slot}", "TEXT", 64));
            list.Add(Column($"Para1 {slot}", "TEXT", 255));
            list.Add(Column($"Para2 {slot}", "TEXT", 255));
            list.Add(Column($"Para3 {slot}", "TEXT", 255));
            list.Add(Column($"Para4 {slot}", "TEXT", 255));
        }
        return Columns(list.ToArray());
    }

    private static JsonArray ItemColumns() => Columns(
        Column("Item #", "LONG"), Column("Name", "TEXT", 255), Column("Desc1", "MEMO"), Column("Desc2", "MEMO"), Column("Desc3", "MEMO"), Column("Desc4", "MEMO"), Column("Desc5", "MEMO"), Column("Desc6", "MEMO"),
        Column("Game Limit", "LONG"), Column("Weight", "LONG"), Column("Uses", "LONG"), Column("Price", "LONG"), Column("Worn On", "LONG"), Column("Gettable", "YESNO"), Column("Req Str", "LONG"), Column("Speed", "LONG"),
        Column("Miss Msg", "LONG"), Column("Hit Msg", "LONG"), Column("Read Msg", "LONG"), Column("Not Droppable", "YESNO"), Column("Destroy On Death", "YESNO"), Column("Retain After Uses", "YESNO"));

    private static JsonArray MonsterColumns()
    {
        var list = new List<JsonObject>
        {
            Column("Monster #", "LONG"), Column("Name", "TEXT", 255), Column("Active", "LONG"), Column("Undead", "YESNO"), Column("Alignment", "TEXT", 64), Column("Regen Time", "LONG"),
            Column("Exp Multiplier", "LONG"), Column("BS Defense", "LONG"), Column("Index", "LONG"), Column("Follow", "LONG"), Column("Experience", "LONG"), Column("Hit Points", "LONG"), Column("HP Regen", "LONG"),
            Column("Charm LvL", "LONG"), Column("Charm RES", "LONG"), Column("Move Msg", "LONG"), Column("Death Msg", "LONG"), Column("Greet Txt", "LONG"), Column("Desc Txt", "LONG"), Column("Talk Txt", "LONG"), Column("Death Spell", "LONG"), Column("Create Spell", "LONG")
        };
        for (var i = 1; i <= 10; i++)
        {
            list.Add(Column($"Attack Type {i}", "LONG")); list.Add(Column($"Attack Accu/Spell {i}", "LONG")); list.Add(Column($"Attack % {i}", "LONG"));
            list.Add(Column($"Attack Min Hit/Cast % {i}", "LONG")); list.Add(Column($"Attack Max Hit/Cast LVL {i}", "LONG")); list.Add(Column($"Attack Energy {i}", "LONG")); list.Add(Column($"Attack Hit Spell {i}", "LONG"));
        }
        return Columns(list.ToArray());
    }

    private static JsonArray SpellColumns() => Columns(
        Column("Spell #", "LONG"), Column("Name", "TEXT", 255), Column("Short Name", "TEXT", 64), Column("Desc 1", "MEMO"), Column("Desc 2", "MEMO"),
        Column("Cast MSG A", "LONG"), Column("Cast MSG B", "LONG"), Column("MSG Style", "TEXT", 64), Column("Energy", "LONG"), Column("Mana", "LONG"), Column("Spell Type", "TEXT", 64),
        Column("Type of Resists", "LONG"), Column("Difficulty", "LONG"), Column("Target", "TEXT", 64), Column("Duration", "LONG"), Column("Attack Type", "TEXT", 64), Column("Resist Ability", "LONG"),
        Column("Magery A", "LONG"), Column("Magery B", "LONG"), Column("Level Cap", "LONG"), Column("LVLS Max Increase", "LONG"), Column("Max Increase", "LONG"), Column("LVLS Min Increase", "LONG"), Column("Min Increase", "LONG"), Column("LVLS Dur Increase", "LONG"), Column("Dur Increase", "LONG"));

    private static JsonArray ShopColumns()
    {
        var list = new List<JsonObject>
        {
            Column("Shop #", "LONG"), Column("Name", "TEXT", 255), Column("ShopType", "LONG"), Column("Desc A", "MEMO"), Column("Desc B", "MEMO"), Column("Desc C", "MEMO"),
            Column("Min Lvl", "LONG"), Column("Max Lvl", "LONG"), Column("MarkUp", "LONG"), Column("Class Limit", "LONG")
        };
        for (var i = 1; i <= 30; i++)
        {
            list.Add(Column($"Item {i}", "LONG")); list.Add(Column($"Max {i}", "LONG")); list.Add(Column($"Normal {i}", "LONG"));
            list.Add(Column($"Regen Time {i}", "LONG")); list.Add(Column($"Regen Number {i}", "LONG")); list.Add(Column($"Regen % {i}", "LONG"));
        }
        return Columns(list.ToArray());
    }

    private static JsonArray ClassColumns() => Columns(Column("Class #", "LONG"), Column("Name", "TEXT", 255), Column("Min HP", "LONG"), Column("Max HP", "LONG"), Column("ExpTable", "LONG"), Column("Magic Type", "LONG"), Column("Magic LVL", "LONG"), Column("Combat", "LONG"), Column("Title Text", "LONG"));
    private static JsonArray RaceColumns() => Columns(Column("Race #", "LONG"), Column("Name", "TEXT", 255), Column("Min INT", "LONG"), Column("Min WIL", "LONG"), Column("Min STR", "LONG"), Column("Min HEA", "LONG"), Column("Min AGL", "LONG"), Column("Min CHM", "LONG"), Column("Max INT", "LONG"), Column("Max WIL", "LONG"), Column("Max STR", "LONG"), Column("Max HEA", "LONG"), Column("Max AGL", "LONG"), Column("Max CHM", "LONG"), Column("HP Bonus", "LONG"));
    private static JsonArray MessageColumns() => Columns(Column("Message #", "LONG"), Column("Line 1", "MEMO"), Column("Line 2", "MEMO"), Column("Line 3", "MEMO"));
    private static JsonArray TextblockColumns() => Columns(Column("Textblock #", "LONG"), Column("Part #", "LONG"), Column("Link To", "LONG"), Column("Data", "MEMO"));
    private static JsonArray ActionColumns() => Columns(Column("Action #", "LONG"), Column("Single to User", "LONG"), Column("Single to Room", "LONG"), Column("User to User", "LONG"), Column("User to Other User", "LONG"), Column("User to Room", "LONG"), Column("Monster to User", "LONG"), Column("Monster to Room", "LONG"), Column("Inventory to User", "LONG"), Column("Inventory to Room", "LONG"), Column("Floor Item to User", "LONG"), Column("Floor Item to Room", "LONG"));
    private static JsonArray AbilityColumns() => Columns(Column("Number", "LONG"), Column("Name", "TEXT", 255), Column("Description", "MEMO"), Column("RED", "LONG"), Column("GREEN", "LONG"), Column("BLUE", "LONG"));

    private sealed record WccFileData(string Name, string Path, string Sha256, byte[] Data, IReadOnlyDictionary<string, IReadOnlyList<int>> AnchorOffsets);
    private sealed record WccGapCorrelation(string ColumnName, string WccFile, int RelativeOffset, int WidthBytes, int MatchingRows, int EligibleRows, double Confidence, string Classification);

    private sealed class MdbDump : IDisposable
    {
        public MdbDump(JsonObject root) { Root = root; }
        public JsonObject Root { get; }
        public void Dispose() { }
    }

    private sealed record MdbTable(string Name, IReadOnlyList<string> Columns, IReadOnlyList<JsonObject> Rows);
    private sealed record TableProfile(TableKind Kind, string Family);
    private sealed record MappedRecord(string Family, string TargetId, JsonObject Fields);
    private enum TableKind { Rooms, Items, Monsters, Spells, Shops, Classes, Races, Messages, Textblocks, Actions, Abilities }
}
