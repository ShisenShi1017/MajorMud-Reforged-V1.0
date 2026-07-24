using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.Server;

public sealed record RuntimeBundleHGangHouseAuditReport(
    bool Passed,
    bool RuntimeGameplayReady,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> RuntimeGaps,
    int NativeGangFileBytes,
    int DefaultGangFileBytes,
    int GangMapReferenceCount,
    int DeedItemCount,
    int GangShopCount,
    int GeneratedGangHouseEnvironmentRows)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleHGangHouseAudit
{
    private static readonly string[] ExpectedMapFiles =
    {
        "gang_house_red.html",
        "gang_house_orange.html",
        "gang_house_black.html",
        "gang_house_violet.html",
        "gang_house_yellow.html",
        "gang_house_green.html",
        "gang_house_silver.html",
        "gang_house_gold.html",
        "gang_house_white.html",
        "gang_house_blue.html"
    };

    private static readonly string[] ExpectedDeedNames =
    {
        "red parchment deed",
        "orange parchment deed",
        "yellow parchment deed",
        "green parchment deed",
        "blue parchment deed",
        "violet parchment deed",
        "black parchment deed",
        "silver parchment deed",
        "gold parchment deed",
        "white parchment deed"
    };

    public static RuntimeBundleHGangHouseAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id)
        {
            checks.Add(id);
            if (!condition) failures.Add(id);
        }

        var nativeGangPath = Path.Combine(projectRoot, "ImportedDatabases", "NativeRuntime", "WCCGANGS.DAT");
        var defaultGangPath = Path.Combine(projectRoot, "ImportedDatabases", "WCCDefaultInstall", "wcc default", "wccgang2.dat");
        var summaryPath = Path.Combine(projectRoot, "Data", "NativeDecodeReports", "CodeUpdate0139RoomTextAudit", "WCCGANGS_summary.json");
        var samplePath = Path.Combine(projectRoot, "Data", "NativeDecodeReports", "CodeUpdate0139RoomTextAudit", "WCCGANGS_block_samples_1024.json");
        var stringsPath = Path.Combine(projectRoot, "Data", "NativeDecodeReports", "CodeUpdate0139RoomTextAudit", "WCCGANGS_strings.tsv");
        var mapsRoot = Path.Combine(projectRoot, "ImportedDatabases", "OmniVerseReference", "CodeUpdate0046", "MudInfoMaps", "Extracted", "maps");
        var environmentPath = Path.Combine(projectRoot, "Data", "Classic", "environment_rooms.json");
        var gangHousesPath = Path.Combine(projectRoot, "Data", "Classic", "gang_houses.json");
        var librariumGangHousesPath = Path.Combine(projectRoot, "Librarium", "Objects", "gang_houses", "gang_house_records.json");
        var itemsPath = Path.Combine(projectRoot, "Data", "Classic", "items.json");
        var shopsPath = Path.Combine(projectRoot, "Data", "Classic", "shops.json");
        var protectionPath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeClassicGangHouseProtection.cs");
        var localCoreGraph = Path.Combine(projectRoot, "LibrariumLocal", "core_graph.json");
        var localRuntimeIndex = Path.Combine(projectRoot, "LibrariumLocal", "runtime_index.json");

        Check(File.Exists(nativeGangPath), "native-runtime-wccgangs-present");
        Check(File.Exists(defaultGangPath), "default-install-wccgang2-present");
        Check(File.Exists(summaryPath), "wccgangs-summary-present");
        Check(File.Exists(samplePath), "wccgangs-block-samples-present");
        Check(File.Exists(stringsPath), "wccgangs-strings-catalog-present");
        Check(ExpectedMapFiles.All(file => File.Exists(Path.Combine(mapsRoot, file))), "all-ten-gang-house-map-references-present");
        Check(File.Exists(itemsPath), "generated-items-present");
        Check(File.Exists(shopsPath), "generated-shops-present");
        Check(File.Exists(environmentPath), "generated-environment-rooms-present");
        Check(File.Exists(librariumGangHousesPath), "librarium-gang-house-catalog-present");
        Check(File.Exists(gangHousesPath), "generated-gang-house-catalog-present");
        Check(File.Exists(protectionPath), "runtime-gang-house-protection-service-present");
        Check(!File.Exists(localCoreGraph), "librariumlocal-core-graph-not-present-in-repo");
        Check(!File.Exists(localRuntimeIndex), "librariumlocal-runtime-index-not-present-in-repo");

        var nativeLength = File.Exists(nativeGangPath) ? (int)new FileInfo(nativeGangPath).Length : 0;
        var defaultLength = File.Exists(defaultGangPath) ? (int)new FileInfo(defaultGangPath).Length : 0;
        Check(nativeLength == 2048, "native-runtime-wccgangs-size-2048");
        Check(defaultLength == 28672, "default-install-wccgang2-size-28672");

        var mapReferenceCount = ExpectedMapFiles.Count(file =>
        {
            var path = Path.Combine(mapsRoot, file);
            return File.Exists(path) && File.ReadAllText(path).Contains("Gang House", StringComparison.OrdinalIgnoreCase);
        });
        Check(mapReferenceCount == ExpectedMapFiles.Length, "all-map-references-identify-gang-house");

        var deedItemCount = CountMatchingNames(itemsPath, ExpectedDeedNames);
        var gangShopCount = CountRecordsByNamePattern(shopsPath, "gang house|gang shop|deed office");
        var environmentGangRows = CountEnvironmentGangRows(environmentPath);
        var catalog = File.Exists(gangHousesPath)
            ? RuntimeClassicGangHouseCatalog.Load(gangHousesPath)
            : new RuntimeClassicGangHouseCatalog(Array.Empty<RuntimeClassicGangHouseRecord>());
        var options = ServerRuntimeOptions.Load(projectRoot);
        var definition = options.Worlds.First(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        Check(deedItemCount == ExpectedDeedNames.Length, "all-ten-gang-deed-items-present");
        Check(gangShopCount >= 21, "gang-house-vault-supply-and-deed-shops-present");
        Check(catalog.Records.Count == 10, "generated-gang-house-catalog-has-ten-houses");
        Check(catalog.Records.All(x => x.Locations.Count == 2), "generated-gang-house-catalog-has-vault-and-supply-locations");
        Check(catalog.Locations.Count == 20, "generated-gang-house-catalog-has-twenty-service-locations");
        Check(catalog.Locations.All(x => world.FindRoom(x.MapId, x.RoomNumber) is not null), "gang-house-service-locations-resolve-to-runtime-rooms");
        Check(catalog.Locations.Count(x => x.IsVault) == 10, "gang-house-catalog-has-ten-vault-locations");
        Check(catalog.Locations.Count(x => x.IsGangHouseShop) == 10, "gang-house-catalog-has-ten-supply-shop-locations");
        Check(environmentGangRows == 0, "environment-catalog-still-has-no-native-gang-house-flags");
        Check(Read(protectionPath).Contains("matching-deed-and-upkeep-current", StringComparison.Ordinal), "runtime-protection-requires-deed-and-upkeep");
        var protection = new RuntimeClassicGangHouseProtectionService(
            catalog.ToProtectionLocations(),
            new[]
            {
                new RuntimeClassicDeedState("character.audit", "item.1008", "gang_house.red", DateTimeOffset.MaxValue),
                new RuntimeClassicDeedState("character.expired", "item.1008", "gang_house.red", DateTimeOffset.MinValue)
            });
        var item = new RuntimeItemInstance("audit.item", "item.audit", null, RuntimeItemLocations.Room, "room.map15.908", null, 0, 1, null, null);
        Check(protection.Evaluate(item, "character.audit", DateTimeOffset.UtcNow).Protected, "runtime-protection-catalog-allows-current-deed");
        Check(!protection.Evaluate(item, "character.expired", DateTimeOffset.UtcNow).Protected, "runtime-protection-catalog-rejects-expired-deed");
        Check(!protection.Evaluate(item with { LocationId = "room.map1.2140" }, "character.audit", DateTimeOffset.UtcNow).Protected, "runtime-protection-catalog-rejects-non-gang-room");

        return new(
            failures.Count == 0,
            RuntimeGameplayReady: catalog.Records.Count == 10 && catalog.Locations.Count == 20,
            checks,
            failures,
            new[]
            {
                "Native WCCGANGS.DAT, default-install wccgang2.dat, decode summary/sample/string outputs, and all ten MudInfo gang-house reference maps are present.",
                "Generated Classic data contains the ten parchment deed items and the gang-house vault/supply/deed shop family.",
                "Librarium and generated Classic gang-house catalogs bind ten house records to twenty WCC shop API service locations.",
                "RuntimeClassicGangHouseProtectionService consumes the generated service-location catalog and protects cleanup only when a matching deed exists and upkeep is current."
            },
            new[]
            {
                "Generated Data/Classic/environment_rooms.json still contains zero native gang_house flags; active gang-house service locations are supplied by Data/Classic/gang_houses.json.",
                "Only WCC shop API service rooms are promoted in this pass; ordinary interior house rooms remain blocked until exact map-qualified ownership/location evidence is recovered.",
                "The listed LibrariumLocal/core_graph.json and LibrariumLocal/runtime_index.json handoff files are not present in the repository.",
                "Gang-house maps are reference evidence for layout/legend only; this audit does not promote room IDs from map art.",
                "Persistent deed acquisition/upkeep mutation is still not a complete live gameplay flow; this pass wires cleanup protection consumption of the generated catalog."
            },
            nativeLength,
            defaultLength,
            mapReferenceCount,
            deedItemCount,
            gangShopCount,
            environmentGangRows);
    }

    private static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    private static int CountMatchingNames(string path, IReadOnlyCollection<string> expectedNames)
    {
        if (!File.Exists(path)) return 0;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return 0;
        var names = records.EnumerateArray()
            .Select(record => JsonValueReader.ReadString(record, "name", string.Empty))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return expectedNames.Count(names.Contains);
    }

    private static int CountRecordsByNamePattern(string path, string pattern)
    {
        if (!File.Exists(path)) return 0;
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return 0;
        return records.EnumerateArray().Count(record => regex.IsMatch(JsonValueReader.ReadString(record, "name", string.Empty)));
    }

    private static int CountEnvironmentGangRows(string path)
    {
        if (!File.Exists(path)) return 0;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return 0;
        return records.EnumerateArray().Count(record => JsonValueReader.ReadInt(record, "gang_house", 0) != 0);
    }
}
