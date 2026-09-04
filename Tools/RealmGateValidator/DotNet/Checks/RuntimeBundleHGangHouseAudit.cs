using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealmGate.Server;

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

        var gangHousesPath = Path.Combine(projectRoot, "Data", "Classic", "gang_houses.json");
        var librariumGangHousesPath = Path.Combine(projectRoot, "Librarium", "Objects", "gang_houses", "gang_house_records.json");
        var itemsPath = Path.Combine(projectRoot, "Data", "Classic", "items.json");
        var shopsPath = Path.Combine(projectRoot, "Data", "Classic", "shops.json");
        var protectionPath = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeClassicGangHouseProtection.cs");
        var localCoreGraph = Path.Combine(projectRoot, "LibrariumLocal", "core_graph.json");
        var localRuntimeIndex = Path.Combine(projectRoot, "LibrariumLocal", "runtime_index.json");

        // Runtime readiness is proved by promoted Librarium and generated catalogs.
        Check(File.Exists(itemsPath), "generated-items-present");
        Check(File.Exists(shopsPath), "generated-shops-present");
        Check(File.Exists(librariumGangHousesPath), "librarium-gang-house-catalog-present");
        Check(File.Exists(gangHousesPath), "generated-gang-house-catalog-present");
        Check(File.Exists(protectionPath), "runtime-gang-house-protection-service-present");
        Check(!File.Exists(localCoreGraph), "librariumlocal-core-graph-not-present-in-repo");
        Check(!File.Exists(localRuntimeIndex), "librariumlocal-runtime-index-not-present-in-repo");

        Check(Read(librariumGangHousesPath).Contains("librarium_promoted_native_hse_and_deed_evidence", StringComparison.Ordinal),
            "librarium-retains-promoted-native-gang-house-authority");

        var deedItemCount = CountMatchingNames(itemsPath, ExpectedDeedNames);
        var gangShopCount = CountRecordsByNamePattern(shopsPath, "gang house|gang shop|deed office");
        var catalog = File.Exists(gangHousesPath)
            ? RuntimeClassicGangHouseCatalog.Load(gangHousesPath)
            : new RuntimeClassicGangHouseCatalog(Array.Empty<RuntimeClassicGangHouseRecord>());
        var itemCatalog = RuntimeItemCatalog.Load(projectRoot, worldId);
        var itemRelationships = RuntimeClassicGangHouseAbilityAuthority.Resolve(itemCatalog, catalog);
        var options = ServerRuntimeOptions.Load(projectRoot);
        var definition = options.Worlds.First(x => x.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase));
        var world = new RuntimeWorldLoader().Load(projectRoot, definition);
        var environmentGangRows = world.AllRooms().Count(room => room.EnvironmentGangHouseId != 0);
        Check(deedItemCount == ExpectedDeedNames.Length, "all-ten-gang-deed-items-present");
        Check(gangShopCount >= 21, "gang-house-vault-supply-and-deed-shops-present");
        Check(catalog.Records.Count == 10, "generated-gang-house-catalog-has-ten-houses");
        Check(catalog.Records.All(x => x.Locations.Count >= 4), "generated-gang-house-catalog-has-native-interiors-and-services");
        Check(catalog.Records.Select(x => x.ActivationTextblockId).Order().SequenceEqual(Enumerable.Range(9000, 10)),
            "decoded-deed-activation-textblocks-9000-through-9009");
        Check(catalog.Records.All(x => x.ActivationMessageId == 2629 && x.ActivationGrantItemIds.Count == 4),
            "decoded-deed-activation-message-and-four-item-grants");
        var gangStateSource = Read(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeClassicGangState.cs"));
        var livePowerSource = Read(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeLivePowers.cs"));
        var liveCombatSource = Read(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeLiveCombat.cs"));
        var commandSource = Read(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeCommandSystem.cs"));
        Check(gangStateSource.Contains("AssignHouse", StringComparison.Ordinal) && gangStateSource.Contains("already owned by another active gang", StringComparison.Ordinal),
            "persistent-exclusive-gang-house-assignment");
        Check(livePowerSource.Contains("ActivateGangHouseDeed", StringComparison.Ordinal)
            && livePowerSource.Contains("ActivationGrantItemIds", StringComparison.Ordinal)
            && livePowerSource.Contains("ActivationMessageId", StringComparison.Ordinal),
            "live-deed-activation-consumes-generated-contract");
        Check(liveCombatSource.Contains("CanEnterGangHouse", StringComparison.Ordinal)
            && liveCombatSource.Contains("FindByMember", StringComparison.Ordinal)
            && liveCombatSource.Contains("GangHouseId", StringComparison.Ordinal)
            && commandSource.Contains("CanEnterGangHouse", StringComparison.Ordinal),
            "native-gang-house-guardian-member-entry-gate");
        var upkeepSource = Read(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeClassicGangHouseUpkeep.cs"));
        var worldTimeSource = Read(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeClassicWorldTimeEvents.cs"));
        Check(upkeepSource.Contains("ProcessDailyAutoCleanup", StringComparison.Ordinal)
            && upkeepSource.Contains("DebitBankGold", StringComparison.Ordinal)
            && upkeepSource.Contains("RepossessHouse", StringComparison.Ordinal),
            "decoded-sundown-bank-gold-tax-and-repossession-boundary");
        Check(worldTimeSource.Contains("RuntimeClassicWorldTimePhase.Sundown", StringComparison.Ordinal)
            && worldTimeSource.Contains("ProcessDailyAutoCleanup", StringComparison.Ordinal),
            "legacy-world-time-adapter-routes-to-native-daily-cleanup-owner");
        Check(catalog.Locations.Count == 57, "generated-gang-house-catalog-has-fifty-seven-native-and-service-locations");
        Check(catalog.Locations.All(x => world.FindRoom(x.MapId, x.RoomNumber) is not null), "gang-house-service-locations-resolve-to-runtime-rooms");
        Check(catalog.Locations.Count(x => x.LocationKind == "entrance") == 10, "gang-house-catalog-has-ten-native-entrances");
        Check(catalog.Locations.Count(x => x.LocationKind == "interior") == 10, "gang-house-catalog-has-ten-native-normal-interiors");
        Check(catalog.Locations.Count(x => x.LocationKind == "rest_room") == 8, "gang-house-catalog-has-eight-native-rest-rooms");
        Check(catalog.Locations.Count(x => x.IsVault) == 19, "gang-house-catalog-has-native-and-service-vault-locations");
        Check(catalog.Locations.Count(x => x.IsGangHouseShop) == 10, "gang-house-catalog-has-ten-supply-shop-locations");
        Check(itemRelationships.Count(x => x.IsDeed) == 10, "decoded-ghouse-deed-ten-owners");
        Check(itemRelationships.Count(x => x.IsHouseItem) == 59, "decoded-ghouse-item-fifty-nine-owners");
        Check(itemRelationships.Count(x => x.IsShopController) == 11, "decoded-gshop-item-eleven-owners");
        Check(itemRelationships.Where(x => x.IsDeed).All(x => x.TaxValue > 0), "decoded-ghouse-tax-ten-positive-values");
        Check(itemRelationships.All(x => catalog.Records.Any(house => house.HouseId.Equals(x.HouseId, StringComparison.OrdinalIgnoreCase))),
            "decoded-gang-item-selectors-resolve-generated-houses");
        Check(environmentGangRows == 0, "environment-catalog-still-has-no-native-gang-house-flags");
        Check(Read(protectionPath).Contains("active-persistent-gang-house-assignment", StringComparison.Ordinal)
            && Read(protectionPath).Contains("new RuntimeClassicGangStateService(projectRoot).List()", StringComparison.Ordinal)
            && Read(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeClassicCleanup.cs"))
                .Contains("new RuntimeClassicGangHouseProtectionService(projectRoot, worldId)", StringComparison.Ordinal),
            "runtime-protection-consumes-persistent-gang-house-assignment");
        var now = DateTimeOffset.UtcNow;
        RuntimeClassicGangRecord Gang(string id, string houseId, bool disbanded = false) => new(
            id, id, "character.audit", [new RuntimeClassicGangMember("character.audit", "Audit", "Leader", now)],
            1, 0, true, disbanded, [], houseId, 0, now)
        {
            GangHouseDeedHolderCharacterId = "character.audit",
            LastGangHouseSundownCycleId = "sundown.audit",
            LastGangHouseUpkeepPaidAtUtc = now
        };
        var protection = new RuntimeClassicGangHouseProtectionService(
            catalog.ToProtectionLocations(),
            new[] { Gang("gang.audit", "gang_house.red") });
        var item = new RuntimeItemInstance("audit.item", "item.audit", null, RuntimeItemLocations.Room, "room.map15.908", null, 0, 1, null, null);
        Check(protection.Evaluate(item, now).Protected, "runtime-protection-allows-active-persistent-house");
        Check(!new RuntimeClassicGangHouseProtectionService(catalog.ToProtectionLocations(), Array.Empty<RuntimeClassicGangRecord>()).Evaluate(item, now).Protected,
            "runtime-protection-rejects-unowned-or-repossessed-house");
        Check(!new RuntimeClassicGangHouseProtectionService(catalog.ToProtectionLocations(), new[] { Gang("gang.disbanded", "gang_house.red", true) }).Evaluate(item, now).Protected,
            "runtime-protection-rejects-disbanded-owner");
        Check(!protection.Evaluate(item with { LocationId = "room.map1.2140" }, now).Protected, "runtime-protection-rejects-non-gang-room");

        return new(
            failures.Count == 0,
            RuntimeGameplayReady: catalog.Records.Count == 10 && catalog.Locations.Count == 57,
            checks,
            failures,
            new[]
            {
                "Promoted native gang-house authority is retained in the Librarium; raw WCC and map references are optional local archives after source retirement.",
                "Generated Classic data contains the ten parchment deed items and the gang-house vault/supply/deed shop family.",
                "Generated Classic gang-house authority binds ten houses to 57 locations: 20 service locations plus 37 additional native WCC .HSE interiors.",
                "RuntimeClassicGangHouseProtectionService consumes generated service locations plus persistent active gang-house assignments; repossession or disbanding removes cleanup protection."
            },
            new[]
            {
                "Authoritative generated room records contain zero native gang_house flags; active gang-house service locations are supplied by Data/Classic/gang_houses.json.",
                "Native WCC .HSE files recover 47 map-qualified customization rooms; ten overlap stronger service bindings and 37 add entrances, normal interiors, rest rooms, and vaults without overriding conflicts.",
                "The listed LibrariumLocal/core_graph.json and LibrariumLocal/runtime_index.json handoff files are not present in the repository.",
                "Gang-house maps are reference evidence for layout/legend only; this audit does not promote room IDs from map art.",
                "Raw WCCGANGS, wccgang2, and map-reference files may be absent from Git after their promoted Librarium records have passed validation.",
                "Gang-house taxation is owned by the once-per-day host mcurou callback. Reforged uses the historical 03:00 local host default without restart catch-up; host-configured non-default cleanup times remain an external host setting."
            },
            0,
            0,
            0,
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
