using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using JetDatabaseReader;
using Reforged.App.Models;

namespace Reforged.App.Services;

public sealed record MudPlayMdbImportProgress(
    string Phase,
    string Table,
    int TablesDone,
    int TablesTotal,
    int RowsDone,
    int RowsTotal,
    string Message);

public sealed record MudPlayMdbImportResult(
    bool Success,
    string FileName,
    int TablesFound,
    int TablesCompared,
    int RowsCompared,
    int ExistingRecordsChanged,
    int NewRecordsAdded,
    int FieldOverridesWritten,
    int EqualFieldsSkipped,
    int UnmappedColumns,
    int RowsWithoutIdentity,
    int RowsWithoutClassicMatch,
    int ClassicRowsNotPresentInMdb,
    string Message)
{
    public static MudPlayMdbImportResult Failure(string message, string fileName = "") =>
        new(false, fileName, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, message);
}

/// <summary>
/// Reads an external MajorMUD MDB/ACCDB and builds only the difference layer
/// required above Reforged Classic data. The imported database is never copied
/// into the client data store. Re-import replaces only MDB-derived fields while
/// preserving manual client overrides.
/// </summary>
public sealed class MudPlayMdbOverlayImporter
{
    private readonly MudPlayClientGameDataOverlayService _overlayService;

    private static readonly IReadOnlyDictionary<string, string> TableFamilies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = "Items",
            ["item"] = "Items",
            ["spells"] = "Spells",
            ["spell"] = "Spells",
            ["monsters"] = "Monsters / NPCs",
            ["monster"] = "Monsters / NPCs",
            ["npcs"] = "Monsters / NPCs",
            ["npc"] = "Monsters / NPCs",
            ["shops"] = "Shops",
            ["shop"] = "Shops",
            ["rooms"] = "Rooms",
            ["room"] = "Rooms",
            ["classes"] = "Classes",
            ["class"] = "Classes",
            ["races"] = "Races",
            ["race"] = "Races"
        };

    private static readonly IReadOnlyDictionary<string, string> DirectionColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["n"] = "N", ["north"] = "N",
            ["ne"] = "NE", ["northeast"] = "NE",
            ["e"] = "E", ["east"] = "E",
            ["se"] = "SE", ["southeast"] = "SE",
            ["s"] = "S", ["south"] = "S",
            ["sw"] = "SW", ["southwest"] = "SW",
            ["w"] = "W", ["west"] = "W",
            ["nw"] = "NW", ["northwest"] = "NW",
            ["u"] = "U", ["up"] = "U",
            ["d"] = "D", ["down"] = "D"
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FieldMaps =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Items"] = Map(
                ("name", "name"),
                ("itemname", "name"),
                ("itemtype", "item_type"),
                ("type", "item_type"),
                ("minhit", "combat.minimum_damage"),
                ("minimumhit", "combat.minimum_damage"),
                ("mindamage", "combat.minimum_damage"),
                ("minimumdamage", "combat.minimum_damage"),
                ("maxhit", "combat.maximum_damage"),
                ("maximumhit", "combat.maximum_damage"),
                ("maxdamage", "combat.maximum_damage"),
                ("maximumdamage", "combat.maximum_damage"),
                ("armorclass", "combat.armor_class_bonus"),
                ("armourclass", "combat.armor_class_bonus"),
                ("ac", "combat.armor_class_bonus"),
                ("damageresist", "combat.damage_resistance_bonus"),
                ("damageresistance", "combat.damage_resistance_bonus"),
                ("dr", "combat.damage_resistance_bonus"),
                ("accy", "combat.accuracy"),
                ("accuracy", "combat.accuracy"),
                ("speed", "combat.speed"),
                ("weapontype", "combat.weapon_type"),
                ("armortype", "combat.armor_type"),
                ("armourtype", "combat.armor_type"),
                ("encum", "economy.weight"),
                ("encumbrance", "economy.weight"),
                ("weight", "economy.weight"),
                ("cost", "economy.cost"),
                ("costtype", "economy.cost_type"),
                ("gettable", "flags.gettable"),
                ("gamelimit", "public_reference_0139.fields.game_limit"),
                ("currentinstances", "public_reference_0139.fields.current_instances"),
                ("worn", "public_reference_0139.fields.properties.worn_on"),
                ("wornon", "public_reference_0139.fields.properties.worn_on"),
                ("strreq", "public_reference_0139.fields.stat_requirements.str"),
                ("strengthreq", "public_reference_0139.fields.stat_requirements.str"),
                ("strengthrequirement", "public_reference_0139.fields.stat_requirements.str"),
                ("maxuses", "public_reference_0139.fields.max_uses"),
                ("usesconsumedonuse", "public_reference_0139.fields.uses_consumed_on_use"),
                ("retainafteruses", "public_reference_0139.fields.retain_after_uses"),
                ("destroyondeath", "public_reference_0139.fields.destroy_on_death"),
                ("notdroppable", "public_reference_0139.fields.not_droppable")),

            ["Spells"] = Map(
                ("name", "name"),
                ("spellname", "name"),
                ("shortname", "short_name"),
                ("level", "classification.spell_level"),
                ("spelllevel", "classification.spell_level"),
                ("type", "classification.spell_type"),
                ("spelltype", "classification.spell_type"),
                ("target", "classification.target_type"),
                ("targettype", "classification.target_type"),
                ("attacktype", "classification.attack_type"),
                ("mana", "costs.mana_cost"),
                ("manacost", "costs.mana_cost"),
                ("energy", "costs.energy_cost"),
                ("energycost", "costs.energy_cost"),
                ("min", "effect.min_effect"),
                ("mineffect", "effect.min_effect"),
                ("max", "effect.max_effect"),
                ("maxeffect", "effect.max_effect"),
                ("duration", "effect.duration"),
                ("resisttype", "public_reference_0139.fields.resist_type"),
                ("resistability", "public_reference_0139.fields.resist_ability"),
                ("difficulty", "public_reference_0139.fields.difficulty"),
                ("mageryrequirementa", "public_reference_0139.fields.magery_requirement_a"),
                ("mageryrequirementb", "public_reference_0139.fields.magery_requirement_b"),
                ("levelcap", "public_reference_0139.fields.level_cap"),
                ("levelsformaxincrease", "public_reference_0139.fields.levels_for_max_increase"),
                ("maxincrease", "public_reference_0139.fields.max_increase"),
                ("levelsforminincrease", "public_reference_0139.fields.levels_for_min_increase"),
                ("minincrease", "public_reference_0139.fields.min_increase"),
                ("levelsfordurationincrease", "public_reference_0139.fields.levels_for_duration_increase"),
                ("durationincrease", "public_reference_0139.fields.duration_increase"),
                ("messagestyle", "public_reference_0139.fields.message_style")),

            ["Monsters / NPCs"] = Map(
                ("name", "name"),
                ("monstername", "name"),
                ("npcname", "name"),
                ("level", "combat.level"),
                ("hitpoints", "combat.hp"),
                ("hits", "combat.hp"),
                ("hp", "combat.hp"),
                ("energy", "combat.energy"),
                ("armorclass", "combat.armor_class"),
                ("armourclass", "combat.armor_class"),
                ("ac", "combat.armor_class"),
                ("damageresist", "combat.damage_resistance"),
                ("damageresistance", "combat.damage_resistance"),
                ("dr", "combat.damage_resistance"),
                ("magicresist", "combat.magic_resistance"),
                ("magicresistance", "combat.magic_resistance"),
                ("mr", "combat.magic_resistance"),
                ("experience", "rewards.xp_base"),
                ("baseexperience", "rewards.xp_base"),
                ("exp", "rewards.xp_base"),
                ("group", "behavior.group_id"),
                ("groupid", "behavior.group_id"),
                ("follow", "behavior.follow"),
                ("charmlevel", "behavior.charm_level"),
                ("charmresistance", "behavior.charm_resistance"),
                ("regentime", "behavior.regen_time"),
                ("respawnhours", "behavior.respawn_hours"),
                ("undead", "behavior.undead"),
                ("gold", "rewards.gold"),
                ("silver", "rewards.silver"),
                ("copper", "rewards.copper"),
                ("platinum", "rewards.platinum"),
                ("runic", "rewards.runic"),
                ("alignment", "public_reference_0139.fields.alignment"),
                ("monstertype", "public_reference_0139.fields.monster_type"),
                ("indexnumber", "public_reference_0139.fields.index_number"),
                ("experiencemultiplier", "public_reference_0139.fields.experience_multiplier"),
                ("backstabdefense", "public_reference_0139.fields.backstab_defense"),
                ("gamelimit", "public_reference_0139.fields.game_limit"),
                ("hpregen", "public_reference_0139.fields.hp_regen"),
                ("gender", "public_reference_0139.fields.gender")),

            ["Shops"] = Map(
                ("name", "name"),
                ("shopname", "name"),
                ("type", "service_type"),
                ("shoptype", "service_type"),
                ("minlevel", "level_range.min"),
                ("maxlevel", "level_range.max"),
                ("classlimit", "class_limit"),
                ("markup", "markup_percent"),
                ("markuppercent", "markup_percent"),
                ("map", "public_reference_0139.fields.map_id"),
                ("mapid", "public_reference_0139.fields.map_id"),
                ("room", "public_reference_0139.fields.room_number"),
                ("roomnumber", "public_reference_0139.fields.room_number")),

            ["Rooms"] = Map(
                ("name", "name"),
                ("roomname", "name"),
                ("description", "description"),
                ("desc", "description"),
                ("light", "environment.light_level"),
                ("lightlevel", "environment.light_level"),
                ("roomtype", "environment.room_type"),
                ("attributes", "environment.attributes"),
                ("shop", "environment.shop_id"),
                ("shopid", "environment.shop_id"),
                ("roomspell", "environment.room_spell_id"),
                ("roomspellid", "environment.room_spell_id"),
                ("lair", "environment.room_lair_id"),
                ("roomlair", "environment.room_lair_id"),
                ("roomlairid", "environment.room_lair_id"),
                ("delay", "environment.room_delay"),
                ("roomdelay", "environment.room_delay"),
                ("ganghouse", "environment.gang_house"),
                ("respawn", "environment.respawn_minutes"),
                ("respawnminutes", "environment.respawn_minutes"),
                ("maxarea", "environment.max_area_cap"),
                ("maxareacap", "environment.max_area_cap"),
                ("deathroom", "environment.death_room_id"),
                ("deathroomid", "environment.death_room_id"),
                ("linkedspawnroom", "environment.linked_spawn_room")),

            ["Classes"] = Map(
                ("name", "name"),
                ("classname", "name")),

            ["Races"] = Map(
                ("name", "name"),
                ("racename", "name"))
        };

    public MudPlayMdbOverlayImporter(MudPlayClientGameDataOverlayService overlayService)
    {
        _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
    }

    public Task<MudPlayMdbImportResult> ImportAsync(
        ClientSessionConnection session,
        string mdbPath,
        IProgress<MudPlayMdbImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(mdbPath))
            return Task.FromResult(MudPlayMdbImportResult.Failure("Choose an MDB or ACCDB file first."));

        var fullPath = Path.GetFullPath(mdbPath);
        return Task.Run(() => ImportCore(session, fullPath, progress, cancellationToken), cancellationToken);
    }

    private MudPlayMdbImportResult ImportCore(
        ClientSessionConnection session,
        string mdbPath,
        IProgress<MudPlayMdbImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(mdbPath);
        if (!File.Exists(mdbPath))
            return MudPlayMdbImportResult.Failure($"Database file not found: {mdbPath}", fileName);

        var extension = Path.GetExtension(mdbPath);
        if (!extension.Equals(".mdb", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".accdb", StringComparison.OrdinalIgnoreCase))
        {
            return MudPlayMdbImportResult.Failure("Only Microsoft Access .mdb and .accdb files are supported.", fileName);
        }

        var context = _overlayService.ResolveContext(session);
        if (!context.IsEditable || context.Mode != MudPlayGameDataAuthorityMode.ExternalBbs)
        {
            return MudPlayMdbImportResult.Failure(
                "MDB overlays are only for external BBS connections. RealmGate servers use their server-selected RealmGate world projection.",
                fileName);
        }

        try
        {
            progress?.Report(new MudPlayMdbImportProgress("Open", string.Empty, 0, 0, 0, 0, $"Opening {fileName}..."));
            var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mdbPath))).ToLowerInvariant();
            using AccessReader reader = AccessReader.Open(mdbPath);

            var userTables = reader.ListTables()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(name => !name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                .Where(name => !name.StartsWith('~'))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var mappedTables = userTables
                .Select(name => (Name: name, Family: ResolveFamily(name)))
                .Where(item => item.Family is not null)
                .Select(item => (item.Name, Family: item.Family!))
                .ToArray();

            var deltas = new List<MudPlayMdbImportRecordDelta>();
            var rowsCompared = 0;
            var existingRecordsChanged = 0;
            var newRecordsAdded = 0;
            var fieldOverrides = 0;
            var equalFields = 0;
            var unmappedColumns = 0;
            var rowsWithoutIdentity = 0;
            var rowsWithoutClassicMatch = 0;
            var classicRowsNotInMdb = 0;
            var tablesDone = 0;

            foreach (var (tableName, family) in mappedTables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new MudPlayMdbImportProgress(
                    "Read", tableName, tablesDone, mappedTables.Length, 0, 0,
                    $"Reading {tableName}..."));

                DataTable table = reader.ReadTable(tableName);
                var baseRecords = _overlayService.LoadBaseFamily(session, family);
                var baseIndex = BuildBaseIndex(family, baseRecords);
                var seenBaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var normalizedColumns = BuildColumnMap(table.Columns);
                var fieldMap = FieldMaps[family];
                var tableUnmapped = normalizedColumns.Keys
                    .Where(column => !IsIdentityColumn(family, column) &&
                                     !fieldMap.ContainsKey(column) &&
                                     !(family.Equals("Rooms", StringComparison.OrdinalIgnoreCase) && DirectionColumns.ContainsKey(column)))
                    .ToArray();
                unmappedColumns += tableUnmapped.Length;
                var missingMappedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var exitBaseIndex = family.Equals("Rooms", StringComparison.OrdinalIgnoreCase)
                    ? BuildBaseIndex("Exits / Paths", _overlayService.LoadBaseFamily(session, "Exits / Paths"))
                    : null;

                var rowNumber = 0;
                foreach (DataRow row in table.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rowNumber++;
                    rowsCompared++;
                    if (rowNumber == 1 || rowNumber % 250 == 0 || rowNumber == table.Rows.Count)
                    {
                        progress?.Report(new MudPlayMdbImportProgress(
                            "Compare", tableName, tablesDone, mappedTables.Length,
                            rowNumber, table.Rows.Count,
                            $"Comparing {tableName}: {rowNumber:N0}/{table.Rows.Count:N0}"));
                    }

                    if (!TryResolveIncomingIdentity(family, row, normalizedColumns, out var identity))
                    {
                        rowsWithoutIdentity++;
                        continue;
                    }

                    baseIndex.TryGetValue(identity, out var baseRecord);
                    if (baseRecord is not null)
                        seenBaseIds.Add(baseRecord.Id);
                    else
                        rowsWithoutClassicMatch++;

                    var recordId = baseRecord?.Id ?? BuildAddedRecordId(family, identity);
                    if (recordId.Length == 0)
                    {
                        rowsWithoutIdentity++;
                        continue;
                    }

                    var changedFields = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
                    JsonObject? addedRecord = baseRecord is null ? CreateAddedRecord(family, recordId, identity) : null;

                    foreach (var columnPair in normalizedColumns)
                    {
                        if (!fieldMap.TryGetValue(columnPair.Key, out var targetPath))
                            continue;

                        var rawValue = row[columnPair.Value];
                        if (rawValue is DBNull)
                            continue;

                        var incoming = ToJsonNode(rawValue);
                        if (baseRecord is null)
                        {
                            SetPath(addedRecord!, targetPath, incoming?.DeepClone());
                            continue;
                        }

                        if (!TryGetPath(baseRecord.EffectiveRecord, targetPath, out var baseValue))
                        {
                            // A known mapping without a corresponding Classic field is not
                            // safe to invent into an existing canonical record. It is counted
                            // as unmapped for this table shape and skipped.
                            missingMappedColumns.Add(columnPair.Key);
                            continue;
                        }

                        var coerced = CoerceToBaseType(incoming, baseValue);
                        if (Equivalent(baseValue, coerced))
                        {
                            equalFields++;
                            continue;
                        }

                        changedFields[targetPath] = coerced?.DeepClone();
                    }

                    if (baseRecord is null)
                    {
                        var name = ReadName(row, normalizedColumns);
                        if (!string.IsNullOrWhiteSpace(name))
                            addedRecord!["name"] = name.Trim();
                        deltas.Add(new MudPlayMdbImportRecordDelta(family, recordId, addedRecord, changedFields));
                        newRecordsAdded++;
                        fieldOverrides += CountLeafValues(addedRecord!);
                    }
                    else if (changedFields.Count > 0)
                    {
                        deltas.Add(new MudPlayMdbImportRecordDelta(family, recordId, null, changedFields));
                        existingRecordsChanged++;
                        fieldOverrides += changedFields.Count;
                    }

                    if (family.Equals("Rooms", StringComparison.OrdinalIgnoreCase) && exitBaseIndex is not null)
                    {
                        foreach (var exitDelta in BuildRoomExitDeltas(row, normalizedColumns, identity, exitBaseIndex))
                        {
                            deltas.Add(exitDelta);
                            if (exitDelta.AddedRecord is null) existingRecordsChanged++; else newRecordsAdded++;
                            fieldOverrides += exitDelta.AddedRecord is null
                                ? exitDelta.Fields.Count
                                : CountLeafValues(exitDelta.AddedRecord);
                        }
                    }
                }

                unmappedColumns += missingMappedColumns.Count;
                classicRowsNotInMdb += baseRecords.Count(record => !seenBaseIds.Contains(record.Id));
                tablesDone++;
                progress?.Report(new MudPlayMdbImportProgress(
                    "Compared", tableName, tablesDone, mappedTables.Length,
                    table.Rows.Count, table.Rows.Count,
                    $"Compared {tableName}."));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var importedAt = DateTimeOffset.UtcNow;
            var delta = new MudPlayMdbImportDelta(fileName, sha256, importedAt, deltas);
            _overlayService.ReplaceImportedMdbDelta(session, delta);

            var ignoredTableCount = userTables.Length - mappedTables.Length;
            var message =
                $"MDB comparison complete. Classic remains the base.\n\n" +
                $"Mapped tables: {mappedTables.Length:N0} of {userTables.Length:N0}" +
                (ignoredTableCount > 0 ? $" ({ignoredTableCount:N0} unsupported table(s) ignored)" : string.Empty) + "\n" +
                $"Rows compared: {rowsCompared:N0}\n" +
                $"Classic records changed: {existingRecordsChanged:N0}\n" +
                $"New MDB-only records added to overlay: {newRecordsAdded:N0}\n" +
                $"Overlay field values written: {fieldOverrides:N0}\n" +
                $"Equal fields skipped: {equalFields:N0}\n" +
                $"Rows without usable identity: {rowsWithoutIdentity:N0}\n" +
                $"MDB rows without a Classic match: {rowsWithoutClassicMatch:N0}\n" +
                $"Classic records absent from the MDB: {classicRowsNotInMdb:N0} (not deleted or hidden)\n" +
                $"Unmapped/unsupported columns encountered: {unmappedColumns:N0}\n\n" +
                "The MDB itself was not copied. Re-importing refreshes only MDB-derived overlay values; manual overrides are preserved.";

            progress?.Report(new MudPlayMdbImportProgress(
                "Complete", string.Empty, mappedTables.Length, mappedTables.Length, 0, 0,
                "MDB overlay updated."));

            return new MudPlayMdbImportResult(
                true, fileName, userTables.Length, mappedTables.Length, rowsCompared,
                existingRecordsChanged, newRecordsAdded, fieldOverrides, equalFields,
                unmappedColumns, rowsWithoutIdentity, rowsWithoutClassicMatch,
                classicRowsNotInMdb, message);
        }
        catch (OperationCanceledException)
        {
            return MudPlayMdbImportResult.Failure("MDB comparison was cancelled. The existing overlay was left unchanged.", fileName);
        }
        catch (UnauthorizedAccessException exception)
        {
            return MudPlayMdbImportResult.Failure($"Permission denied reading '{mdbPath}'. {exception.Message}", fileName);
        }
        catch (IOException exception)
        {
            return MudPlayMdbImportResult.Failure($"Could not read '{mdbPath}'. {exception.Message}", fileName);
        }
        catch (Exception exception)
        {
            return MudPlayMdbImportResult.Failure($"MDB comparison failed: {exception.Message}", fileName);
        }
    }

    private static string? ResolveFamily(string tableName)
    {
        var key = NormalizeToken(tableName);
        return TableFamilies.TryGetValue(key, out var family) ? family : null;
    }

    private static Dictionary<string, MudPlayGameDataRecord> BuildBaseIndex(
        string family,
        IReadOnlyList<MudPlayGameDataRecord> records)
    {
        var result = new Dictionary<string, MudPlayGameDataRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (TryResolveBaseIdentity(family, record.EffectiveRecord, out var identity) &&
                !result.ContainsKey(identity))
            {
                result[identity] = record;
            }
        }
        return result;
    }

    private static bool TryResolveBaseIdentity(string family, JsonObject record, out string identity)
    {
        if (family.Equals("Exits / Paths", StringComparison.OrdinalIgnoreCase))
        {
            if (TryReadInt(record["from_map_number"], out var fromMap) &&
                TryReadInt(record["from_room_number"], out var fromRoom) &&
                record["direction"] is JsonValue directionValue)
            {
                var direction = ScalarDisplay(directionValue).Trim().ToUpperInvariant();
                if (direction.Length > 0)
                {
                    identity = $"{fromMap.ToString(CultureInfo.InvariantCulture)}/{fromRoom.ToString(CultureInfo.InvariantCulture)}/{direction}";
                    return true;
                }
            }
        }
        else if (family.Equals("Rooms", StringComparison.OrdinalIgnoreCase))
        {
            if (TryReadInt(record["map_number"], out var map) &&
                TryReadInt(record["room_number"], out var room))
            {
                identity = $"{map.ToString(CultureInfo.InvariantCulture)}/{room.ToString(CultureInfo.InvariantCulture)}";
                return true;
            }

            if (record["sourceNumbers"] is JsonObject sourceNumbers &&
                TryReadInt(sourceNumbers["map"], out map) &&
                TryReadInt(sourceNumbers["room"], out room))
            {
                identity = $"{map.ToString(CultureInfo.InvariantCulture)}/{room.ToString(CultureInfo.InvariantCulture)}";
                return true;
            }
        }
        else
        {
            if (TryReadInt(record["legacy_id"], out var number))
            {
                identity = number.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            if (record["sourceNumbers"] is JsonObject sourceNumbers &&
                TryReadInt(sourceNumbers["number"], out number))
            {
                identity = number.ToString(CultureInfo.InvariantCulture);
                return true;
            }
        }

        identity = string.Empty;
        return false;
    }

    private static bool TryResolveIncomingIdentity(
        string family,
        DataRow row,
        IReadOnlyDictionary<string, DataColumn> columns,
        out string identity)
    {
        if (family.Equals("Rooms", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadColumnInt(row, columns, out var map, "map", "mapnumber", "mapno", "mapid") ||
                !TryReadColumnInt(row, columns, out var room, "number", "room", "roomnumber", "roomno", "recordnumber", "recordno"))
            {
                identity = string.Empty;
                return false;
            }
            identity = $"{map.ToString(CultureInfo.InvariantCulture)}/{room.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }

        if (!TryReadColumnInt(row, columns, out var number, "number", "recordnumber", "recordno", "num", "id"))
        {
            identity = string.Empty;
            return false;
        }
        identity = number.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryReadColumnInt(
        DataRow row,
        IReadOnlyDictionary<string, DataColumn> columns,
        out int value,
        params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!columns.TryGetValue(candidate, out var column))
                continue;
            var cell = row[column];
            if (cell is DBNull)
                continue;
            if (cell is int exact)
            {
                value = exact;
                return true;
            }
            if (int.TryParse(Convert.ToString(cell, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;
        }
        value = 0;
        return false;
    }

    private static string ReadName(DataRow row, IReadOnlyDictionary<string, DataColumn> columns)
    {
        foreach (var key in new[] { "name", "itemname", "spellname", "monstername", "npcname", "shopname", "roomname", "classname", "racename" })
        {
            if (!columns.TryGetValue(key, out var column))
                continue;
            var value = row[column];
            if (value is DBNull)
                continue;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        return string.Empty;
    }

    private static string BuildAddedRecordId(string family, string identity)
    {
        if (family.Equals("Rooms", StringComparison.OrdinalIgnoreCase))
        {
            var parts = identity.Split('/');
            return parts.Length == 2 ? $"room.map{parts[0]}.{parts[1]}" : string.Empty;
        }

        var prefix = family switch
        {
            "Items" => "item.",
            "Spells" => "spell.",
            "Monsters / NPCs" => "npc.",
            "Shops" => "shop.",
            "Classes" => "class.",
            "Races" => "race.",
            _ => string.Empty
        };
        return prefix.Length == 0 ? string.Empty : prefix + identity;
    }

    private static JsonObject CreateAddedRecord(string family, string id, string identity)
    {
        var singular = family switch
        {
            "Items" => "item",
            "Spells" => "spell",
            "Monsters / NPCs" => "npc",
            "Shops" => "shop",
            "Rooms" => "room",
            "Classes" => "class",
            "Races" => "race",
            _ => "record"
        };

        var record = new JsonObject
        {
            ["id"] = id,
            ["object_type"] = singular,
            ["authority"] = "ExternalBbsMdbOverlay",
            ["world_scope"] = new JsonArray("external_bbs")
        };

        if (family.Equals("Rooms", StringComparison.OrdinalIgnoreCase))
        {
            var parts = identity.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], out var map) && int.TryParse(parts[1], out var room))
            {
                record["map_number"] = map;
                record["room_number"] = room;
                record["legacy_id"] = room.ToString(CultureInfo.InvariantCulture);
            }
        }
        else if (int.TryParse(identity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            record["legacy_id"] = number.ToString(CultureInfo.InvariantCulture);
        }

        return record;
    }

    private static IReadOnlyList<MudPlayMdbImportRecordDelta> BuildRoomExitDeltas(
        DataRow row,
        IReadOnlyDictionary<string, DataColumn> columns,
        string roomIdentity,
        IReadOnlyDictionary<string, MudPlayGameDataRecord> baseIndex)
    {
        var roomParts = roomIdentity.Split('/');
        if (roomParts.Length != 2 ||
            !int.TryParse(roomParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromMap) ||
            !int.TryParse(roomParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromRoom))
            return [];

        var results = new List<MudPlayMdbImportRecordDelta>();
        var directionsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in columns)
        {
            if (!DirectionColumns.TryGetValue(pair.Key, out var direction) || !directionsSeen.Add(direction))
                continue;
            var cell = row[pair.Value];
            if (cell is DBNull)
                continue;
            var raw = NormalizeText(Convert.ToString(cell, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
            if (raw.Length == 0 || raw.Equals("none", StringComparison.OrdinalIgnoreCase) || raw == "0")
                continue;
            if (!TryParseExitDestination(raw, out var toMap, out var toRoom, out var qualifier))
                continue;

            var identity = $"{fromMap.ToString(CultureInfo.InvariantCulture)}/{fromRoom.ToString(CultureInfo.InvariantCulture)}/{direction}";
            var recordId = $"exit.map{fromMap.ToString(CultureInfo.InvariantCulture)}.{fromRoom.ToString(CultureInfo.InvariantCulture)}.{direction.ToLowerInvariant()}";
            var imported = new JsonObject
            {
                ["id"] = recordId,
                ["object_type"] = "exit",
                ["authority"] = "ExternalBbsMdbOverlay",
                ["world_scope"] = new JsonArray(JsonValue.Create("external_bbs")),
                ["direction"] = direction,
                ["from_map_number"] = fromMap,
                ["from_room_number"] = fromRoom,
                ["to_map_number"] = toMap,
                ["to_room_number"] = toRoom,
                ["gate_kind"] = GateKindFromQualifier(qualifier),
                ["qualifier"] = qualifier,
                ["raw_value"] = raw,
                ["relationships"] = new JsonObject
                {
                    ["from_room"] = $"room.map{fromMap.ToString(CultureInfo.InvariantCulture)}.{fromRoom.ToString(CultureInfo.InvariantCulture)}",
                    ["to_room"] = $"room.map{toMap.ToString(CultureInfo.InvariantCulture)}.{toRoom.ToString(CultureInfo.InvariantCulture)}"
                }
            };

            if (!baseIndex.TryGetValue(identity, out var baseRecord))
            {
                results.Add(new MudPlayMdbImportRecordDelta("Exits / Paths", recordId, imported,
                    new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase)));
                continue;
            }

            var fields = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in new[]
                     {
                         "relationships.to_room", "to_map_number", "to_room_number",
                         "gate_kind", "qualifier", "raw_value"
                     })
            {
                if (!TryGetPath(imported, target, out var incoming) ||
                    !TryGetPath(baseRecord.EffectiveRecord, target, out var current))
                    continue;
                var coerced = CoerceToBaseType(incoming, current);
                if (!Equivalent(current, coerced))
                    fields[target] = coerced?.DeepClone();
            }

            if (fields.Count > 0)
                results.Add(new MudPlayMdbImportRecordDelta("Exits / Paths", baseRecord.Id, null, fields));
        }

        return results;
    }

    private static bool TryParseExitDestination(string raw, out int map, out int room, out string qualifier)
    {
        var text = raw.Trim();
        qualifier = string.Empty;
        var open = text.IndexOf('(');
        if (open >= 0)
        {
            var close = text.LastIndexOf(')');
            if (close > open)
                qualifier = text[(open + 1)..close].Trim();
            text = text[..open].Trim();
        }

        var slash = text.IndexOf('/');
        if (slash <= 0 || slash >= text.Length - 1 ||
            !int.TryParse(text[..slash].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out map) ||
            !int.TryParse(text[(slash + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out room))
        {
            map = room = 0;
            qualifier = string.Empty;
            return false;
        }
        return map > 0 && room > 0;
    }

    private static string GateKindFromQualifier(string qualifier)
    {
        if (string.IsNullOrWhiteSpace(qualifier)) return "open";
        if (qualifier.Contains("door", StringComparison.OrdinalIgnoreCase)) return "door";
        if (qualifier.Contains("hidden", StringComparison.OrdinalIgnoreCase) ||
            qualifier.Contains("secret", StringComparison.OrdinalIgnoreCase)) return "hidden";
        return "unknown";
    }

    private static IReadOnlyDictionary<string, DataColumn> BuildColumnMap(DataColumnCollection columns)
    {
        var result = new Dictionary<string, DataColumn>(StringComparer.OrdinalIgnoreCase);
        foreach (DataColumn column in columns)
        {
            var key = NormalizeToken(column.ColumnName);
            if (key.Length > 0 && !result.ContainsKey(key))
                result[key] = column;
        }
        return result;
    }

    private static bool IsIdentityColumn(string family, string normalizedColumn)
    {
        if (family.Equals("Rooms", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedColumn is "map" or "mapnumber" or "mapno" or "mapid" or
                   "number" or "room" or "roomnumber" or "roomno" or "recordnumber" or "recordno";
        }
        return normalizedColumn is "number" or "recordnumber" or "recordno" or "num" or "id";
    }

    private static JsonNode? ToJsonNode(object value) => value switch
    {
        DBNull => null,
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString(),
        string text => NormalizeText(text),
        bool boolean => boolean,
        byte number => number,
        sbyte number => number,
        short number => number,
        ushort number => number,
        int number => number,
        uint number => number,
        long number => number,
        ulong number => number.ToString(CultureInfo.InvariantCulture),
        float number => number,
        double number => number,
        decimal number => number,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    private static JsonNode? CoerceToBaseType(JsonNode? incoming, JsonNode? baseValue)
    {
        if (incoming is null || baseValue is null)
            return incoming?.DeepClone();

        if (baseValue is not JsonValue baseScalar || incoming is not JsonValue incomingScalar)
            return incoming.DeepClone();

        if (baseScalar.TryGetValue<string>(out _))
        {
            return ScalarDisplay(incomingScalar);
        }
        if (baseScalar.TryGetValue<bool>(out _))
        {
            if (TryAsBool(incomingScalar, out var boolean))
                return boolean;
            return incoming.DeepClone();
        }
        if (TryAsDecimal(baseScalar, out _))
        {
            if (TryAsDecimal(incomingScalar, out var number))
            {
                if (baseScalar.TryGetValue<int>(out _)) return (int)number;
                if (baseScalar.TryGetValue<long>(out _)) return (long)number;
                if (baseScalar.TryGetValue<double>(out _)) return (double)number;
                if (baseScalar.TryGetValue<float>(out _)) return (float)number;
                if (baseScalar.TryGetValue<decimal>(out _)) return number;
            }
        }
        return incoming.DeepClone();
    }

    private static bool Equivalent(JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
        {
            if (left is null && right is null)
                return true;
            var other = left ?? right;
            return other is JsonValue scalar && string.IsNullOrWhiteSpace(ScalarDisplay(scalar));
        }

        if (left is JsonValue leftScalar && right is JsonValue rightScalar)
        {
            if (TryAsDecimal(leftScalar, out var leftNumber) && TryAsDecimal(rightScalar, out var rightNumber))
                return leftNumber == rightNumber;
            if (TryAsBool(leftScalar, out var leftBool) && TryAsBool(rightScalar, out var rightBool))
                return leftBool == rightBool;
            return string.Equals(NormalizeText(ScalarDisplay(leftScalar)), NormalizeText(ScalarDisplay(rightScalar)), StringComparison.Ordinal);
        }

        return JsonNode.DeepEquals(left, right);
    }

    private static bool TryAsDecimal(JsonValue value, out decimal number)
    {
        if (value.TryGetValue<decimal>(out number)) return true;
        if (value.TryGetValue<int>(out var i)) { number = i; return true; }
        if (value.TryGetValue<long>(out var l)) { number = l; return true; }
        if (value.TryGetValue<double>(out var d)) { number = (decimal)d; return true; }
        if (value.TryGetValue<float>(out var f)) { number = (decimal)f; return true; }
        if (value.TryGetValue<string>(out var text) &&
            decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number))
            return true;
        number = 0;
        return false;
    }

    private static bool TryAsBool(JsonValue value, out bool result)
    {
        if (value.TryGetValue<bool>(out result)) return true;
        if (value.TryGetValue<string>(out var text))
        {
            if (bool.TryParse(text, out result)) return true;
            if (text == "1") { result = true; return true; }
            if (text == "0") { result = false; return true; }
        }
        if (TryAsDecimal(value, out var number))
        {
            result = number != 0;
            return true;
        }
        result = false;
        return false;
    }

    private static string ScalarDisplay(JsonValue value)
    {
        if (value.TryGetValue<string>(out var text)) return text ?? string.Empty;
        if (value.TryGetValue<bool>(out var boolean)) return boolean ? "true" : "false";
        if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue.ToString(CultureInfo.InvariantCulture);
        if (value.TryGetValue<long>(out var longValue)) return longValue.ToString(CultureInfo.InvariantCulture);
        if (value.TryGetValue<double>(out var doubleValue)) return doubleValue.ToString("R", CultureInfo.InvariantCulture);
        return value.ToJsonString().Trim('"');
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        if (node is JsonValue scalar)
        {
            if (scalar.TryGetValue<int>(out value)) return true;
            if (scalar.TryGetValue<long>(out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
            {
                value = (int)longValue;
                return true;
            }
            if (scalar.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetPath(JsonObject root, string path, out JsonNode? value)
    {
        JsonNode? current = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(part, out current))
            {
                value = null;
                return false;
            }
        }
        value = current;
        return true;
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return;
        var current = root;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (current[parts[index]] is not JsonObject next)
            {
                next = new JsonObject();
                current[parts[index]] = next;
            }
            current = next;
        }
        current[parts[^1]] = value;
    }

    private static int CountLeafValues(JsonNode? node)
    {
        return node switch
        {
            JsonObject obj => obj.Sum(pair => CountLeafValues(pair.Value)),
            JsonArray array => array.Count,
            null => 0,
            _ => 1
        };
    }

    private static string NormalizeToken(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value ?? string.Empty)
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
        return builder.ToString();
    }

    private static string NormalizeText(string value) =>
        (value ?? string.Empty).TrimEnd('\0', ' ', '\t', '\r', '\n');

    private static IReadOnlyDictionary<string, string> Map(params (string Source, string Target)[] pairs) =>
        pairs.ToDictionary(pair => NormalizeToken(pair.Source), pair => pair.Target, StringComparer.OrdinalIgnoreCase);
}
