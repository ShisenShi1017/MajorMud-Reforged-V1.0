using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

public interface IClientLibrariumBridgeService
{
    Task<ClientLibrariumSnapshot> BuildSnapshotAsync(ClientSessionConnection? session, CancellationToken token = default);
    Task<IReadOnlyList<ClientSavedBuildSnapshot>> LoadSavedBuildsAsync(CancellationToken token = default);
    Task<ClientSavedBuildSnapshot> SaveBuildAsync(string name, ClientLibrariumSnapshot snapshot, CancellationToken token = default);
    Task DeleteBuildAsync(Guid id, CancellationToken token = default);
    Task ExportBuildAsync(ClientSavedBuildSnapshot build, string filePath, CancellationToken token = default);
    Task<ClientSavedBuildSnapshot> ImportBuildAsync(string filePath, CancellationToken token = default);
    IReadOnlyList<ClientBuildComparisonRow> CompareBuilds(ClientSavedBuildSnapshot? buildA, ClientSavedBuildSnapshot? buildB);
}

public sealed class ClientLibrariumBridgeService : IClientLibrariumBridgeService
{
    private readonly List<LibrariumItemIndexEntry> _items = [];
    private readonly List<LibrariumSpellIndexEntry> _spells = [];
    private string _loadedWorld = string.Empty;
    private string _loadedSpellWorld = string.Empty;
    private readonly Dictionary<string, RateBaseline> _rateBaselines = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _savedBuildJsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };



    public async Task<IReadOnlyList<ClientSavedBuildSnapshot>> LoadSavedBuildsAsync(CancellationToken token = default)
    {
        var path = GetSavedBuildPath();
        TryMigrateLegacySavedBuilds(path);
        if (!File.Exists(path))
        {
            var legacyPath = GetLegacySavedBuildPath();
            if (!File.Exists(legacyPath))
                return [];
            path = legacyPath;
        }
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<ClientSavedBuildSnapshot>>(stream, _savedBuildJsonOptions, token) ?? [];
    }

    public async Task<ClientSavedBuildSnapshot> SaveBuildAsync(string name, ClientLibrariumSnapshot snapshot, CancellationToken token = default)
    {
        var saved = new ClientSavedBuildSnapshot
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{snapshot.CharacterName} Build" : name.Trim(),
            CharacterName = snapshot.CharacterName,
            WorldName = snapshot.WorldName,
            CapturedAt = snapshot.CapturedAt ?? DateTimeOffset.Now,
            Metrics = snapshot.Metrics.ToList(),
            EquipmentAndInventory = snapshot.EquipmentAndInventory.Select(item => new ClientLibrariumItemMatch
            {
                CapturedArea = item.CapturedArea,
                CapturedText = item.CapturedText,
                LibrariumName = item.LibrariumName,
                MatchStatus = item.MatchStatus,
                ItemType = item.ItemType,
                Weight = item.Weight,
                Cost = item.Cost,
                Notes = item.Notes
            }).ToList(),
            BuildMetrics = snapshot.BuildMetrics.ToList(),
            MagicMetrics = snapshot.MagicMetrics.ToList(),
            ProgressionPlans = snapshot.ProgressionPlans.ToList()
        };
        var builds = (await LoadSavedBuildsAsync(token)).ToList();
        builds.Add(saved);
        await WriteSavedBuildsAsync(builds, token);
        return saved;
    }

    public async Task DeleteBuildAsync(Guid id, CancellationToken token = default)
    {
        var builds = (await LoadSavedBuildsAsync(token)).Where(build => build.Id != id).ToList();
        await WriteSavedBuildsAsync(builds, token);
    }

    public async Task ExportBuildAsync(ClientSavedBuildSnapshot build, string filePath, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("An export file is required.", nameof(filePath));
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var package = new ClientPortableBuildPackage
        {
            ExportedAt = DateTimeOffset.Now,
            Build = CloneBuild(build, build.Id)
        };
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, package, _savedBuildJsonOptions, token);
    }

    public async Task<ClientSavedBuildSnapshot> ImportBuildAsync(string filePath, CancellationToken token = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("The selected Client build file was not found.", filePath);
        await using var stream = File.OpenRead(filePath);
        var package = await JsonSerializer.DeserializeAsync<ClientPortableBuildPackage>(stream, _savedBuildJsonOptions, token)
            ?? throw new InvalidDataException("The selected file does not contain a Client build package.");
        var supportedFormat =
            package.Format.Equals("RealmGate.ClientBuild", StringComparison.Ordinal) ||
            package.Format.Equals("MajorMudReforged.ClientBuild", StringComparison.Ordinal);
        if (!supportedFormat || package.Version != 1)
            throw new InvalidDataException("This Client build format or version is not supported.");
        if (string.IsNullOrWhiteSpace(package.Build.Name) || string.IsNullOrWhiteSpace(package.Build.CharacterName))
            throw new InvalidDataException("The imported build is missing its name or character identity.");

        var builds = (await LoadSavedBuildsAsync(token)).ToList();
        var importedName = MakeUniqueBuildName(package.Build.Name.Trim(), builds);
        var imported = CloneBuild(package.Build, Guid.NewGuid(), importedName);
        builds.Add(imported);
        await WriteSavedBuildsAsync(builds, token);
        return imported;
    }

    private static ClientSavedBuildSnapshot CloneBuild(ClientSavedBuildSnapshot source, Guid id, string? name = null)
        => new()
        {
            Id = id,
            Name = name ?? source.Name,
            CharacterName = source.CharacterName,
            WorldName = source.WorldName,
            CapturedAt = source.CapturedAt,
            Metrics = source.Metrics.ToList(),
            EquipmentAndInventory = source.EquipmentAndInventory.Select(item => new ClientLibrariumItemMatch
            {
                CapturedArea = item.CapturedArea,
                CapturedText = item.CapturedText,
                LibrariumName = item.LibrariumName,
                MatchStatus = item.MatchStatus,
                ItemType = item.ItemType,
                Weight = item.Weight,
                Cost = item.Cost,
                Notes = item.Notes
            }).ToList(),
            BuildMetrics = source.BuildMetrics.ToList(),
            MagicMetrics = source.MagicMetrics.ToList(),
            ProgressionPlans = source.ProgressionPlans.ToList()
        };

    private static string MakeUniqueBuildName(string requestedName, IReadOnlyCollection<ClientSavedBuildSnapshot> builds)
    {
        if (!builds.Any(build => build.Name.Equals(requestedName, StringComparison.OrdinalIgnoreCase))) return requestedName;
        for (var suffix = 2; suffix < 10000; suffix++)
        {
            var candidate = $"{requestedName} ({suffix})";
            if (!builds.Any(build => build.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase))) return candidate;
        }
        return $"{requestedName} ({Guid.NewGuid():N})";
    }

    public IReadOnlyList<ClientBuildComparisonRow> CompareBuilds(ClientSavedBuildSnapshot? buildA, ClientSavedBuildSnapshot? buildB)
    {
        if (buildA is null || buildB is null) return [];
        var rows = new List<ClientBuildComparisonRow>();
        var areas = buildA.BuildMetrics.Select(metric => metric.Area)
            .Concat(buildB.BuildMetrics.Select(metric => metric.Area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(area => area, StringComparer.OrdinalIgnoreCase);
        foreach (var area in areas)
        {
            var a = buildA.BuildMetrics.FirstOrDefault(metric => metric.Area.Equals(area, StringComparison.OrdinalIgnoreCase));
            var b = buildB.BuildMetrics.FirstOrDefault(metric => metric.Area.Equals(area, StringComparison.OrdinalIgnoreCase));
            var aValue = a?.Value ?? "Not captured";
            var bValue = b?.Value ?? "Not captured";
            rows.Add(new ClientBuildComparisonRow(area, aValue, bValue, DescribeDifference(aValue, bValue), b?.Detail ?? a?.Detail ?? "Read-only saved Client snapshot comparison."));
        }
        rows.Add(new ClientBuildComparisonRow("Equipped records", buildA.EquipmentAndInventory.Count(item => item.CapturedArea == "Equipped").ToString(), buildB.EquipmentAndInventory.Count(item => item.CapturedArea == "Equipped").ToString(), CountDifference(buildA.EquipmentAndInventory.Count(item => item.CapturedArea == "Equipped"), buildB.EquipmentAndInventory.Count(item => item.CapturedArea == "Equipped")), "Count only; Host equipment rules remain authoritative."));
        rows.Add(new ClientBuildComparisonRow("Matched Librarium records", buildA.EquipmentAndInventory.Count(item => item.MatchStatus == "Matched").ToString(), buildB.EquipmentAndInventory.Count(item => item.MatchStatus == "Matched").ToString(), CountDifference(buildA.EquipmentAndInventory.Count(item => item.MatchStatus == "Matched"), buildB.EquipmentAndInventory.Count(item => item.MatchStatus == "Matched")), "Higher match coverage improves planning confidence."));
        return rows;
    }

    private async Task WriteSavedBuildsAsync(IReadOnlyCollection<ClientSavedBuildSnapshot> builds, CancellationToken token)
    {
        var path = GetSavedBuildPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, builds, _savedBuildJsonOptions, token);
    }

    private static string GetSavedBuildPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RealmGate", "Client", "saved-builds.json");

    private static string GetLegacySavedBuildPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MajorMudReforged", "Client", "saved-builds.json");

    private static void TryMigrateLegacySavedBuilds(string destinationPath)
    {
        if (File.Exists(destinationPath))
            return;

        var legacyPath = GetLegacySavedBuildPath();
        if (!File.Exists(legacyPath))
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(legacyPath, destinationPath, overwrite: false);
        }
        catch (IOException)
        {
            // Keep the legacy file untouched; the caller can continue with an empty RealmGate catalog.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not prevent client startup when legacy data cannot be copied.
        }
    }

    private static string CountDifference(int a, int b)
    {
        var delta = b - a;
        return delta == 0 ? "No change" : delta > 0 ? $"+{delta}" : delta.ToString();
    }

    private static string DescribeDifference(string a, string b)
    {
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return "No displayed change";
        var aNumber = FirstNumber(a);
        var bNumber = FirstNumber(b);
        if (aNumber is not null && bNumber is not null)
        {
            var delta = bNumber.Value - aNumber.Value;
            return delta == 0 ? "Same first numeric value" : delta > 0 ? $"+{delta:0.##}" : $"{delta:0.##}";
        }
        return "Changed";
    }

    private static double? FirstNumber(string value)
    {
        var match = Regex.Match(value, @"-?\d+(?:\.\d+)?");
        return match.Success && double.TryParse(match.Value, out var number) ? number : null;
    }
    public async Task<ClientLibrariumSnapshot> BuildSnapshotAsync(ClientSessionConnection? session, CancellationToken token = default)
    {
        var snapshot = new ClientLibrariumSnapshot();
        if (session is null)
        {
            snapshot.Summary = "Select a Client session before importing a character snapshot into the Librarium bridge.";
            return snapshot;
        }

        snapshot.CharacterName = string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;
        snapshot.WorldName = string.IsNullOrWhiteSpace(session.WorldName) ? "Classic" : session.WorldName;
        snapshot.CapturedAt = DateTimeOffset.Now;
        snapshot.Metrics.Add(new("Level", session.CharacterLevel > 0 ? session.CharacterLevel.ToString() : "Not captured", "Client score snapshot"));
        snapshot.Metrics.Add(new("Health", $"{session.HealthCurrent}/{session.HealthMaximum} ({session.HealthPercent}%)", "Live Client status"));
        snapshot.Metrics.Add(new("Mana", $"{session.ManaCurrent}/{session.ManaMaximum} ({session.ManaPercent}%)", "Live Client status"));
        snapshot.Metrics.Add(new("Experience", session.ExperienceSummary, "Client score snapshot"));
        snapshot.Metrics.Add(new("Carried currency", session.CarriedCurrency.ToString("N0"), "Live Client status"));
        snapshot.Metrics.Add(new("Current room", string.IsNullOrWhiteSpace(session.CurrentRoomName) ? "Not recognized" : session.CurrentRoomName, "Client room recognition"));

        await EnsureItemIndexAsync(snapshot.WorldName, token);
        await EnsureSpellIndexAsync(snapshot.WorldName, token);
        foreach (var captured in session.EquippedItems)
        {
            token.ThrowIfCancellationRequested();
            snapshot.EquipmentAndInventory.Add(MatchItem(captured, "Equipped"));
        }
        foreach (var captured in session.InventoryItems)
        {
            token.ThrowIfCancellationRequested();
            snapshot.EquipmentAndInventory.Add(MatchItem(captured, "Carried"));
        }

        var matched = snapshot.EquipmentAndInventory.Count(item => item.MatchStatus == "Matched");
        var matchedEntries = snapshot.EquipmentAndInventory
            .Where(item => item.MatchStatus == "Matched")
            .Select(item => _items.FirstOrDefault(index => index.Name.Equals(item.LibrariumName, StringComparison.OrdinalIgnoreCase)))
            .Where(item => item is not null)
            .Cast<LibrariumItemIndexEntry>()
            .ToList();

        var minimumDamage = matchedEntries.Sum(item => item.MinimumDamage);
        var maximumDamage = matchedEntries.Sum(item => item.MaximumDamage);
        var averageDamage = matchedEntries.Sum(item => (item.MinimumDamage + item.MaximumDamage) / 2.0);
        var armorClass = matchedEntries.Sum(item => item.ArmorClassBonus);
        var damageResistance = matchedEntries.Sum(item => item.DamageResistanceBonus);
        var accuracy = matchedEntries.Sum(item => item.Accuracy);
        var totalWeight = matchedEntries.Sum(item => item.NumericWeight);
        var totalValue = matchedEntries.Sum(item => item.NumericCost);
        var xpRemaining = session.ExperienceNeeded > 0 ? Math.Max(0, session.ExperienceNeeded - session.ExperienceCurrent) : 0;
        var progressionPercent = session.ExperienceNeeded > 0
            ? Math.Clamp((double)session.ExperienceCurrent / session.ExperienceNeeded * 100.0, 0.0, 100.0)
            : 0.0;

        snapshot.BuildMetrics.Add(new("Damage", matchedEntries.Count > 0 ? $"{minimumDamage}-{maximumDamage} combined" : "Not available", matchedEntries.Count > 0 ? $"Estimated {averageDamage:0.0} average from matched carried equipment; Host combat rules remain authoritative." : "Capture inventory/equipment output and improve item matching.", matchedEntries.Count > 0 ? "Librarium item fields" : "Insufficient data"));
        snapshot.BuildMetrics.Add(new("Defense", $"AC +{armorClass}; DR +{damageResistance}", "Sum of matched item armor-class and damage-resistance bonuses. This is planning data, not live combat state.", matchedEntries.Count > 0 ? "Librarium item fields" : "Insufficient data"));
        snapshot.BuildMetrics.Add(new("Accuracy", accuracy == 0 ? "No matched bonus" : $"+{accuracy}", "Combined accuracy field from matched items.", matchedEntries.Count > 0 ? "Librarium item fields" : "Insufficient data"));
        snapshot.BuildMetrics.Add(new("Encumbrance", matchedEntries.Count > 0 ? $"{totalWeight:N0} weight" : "Not available", "Known weight from matched entries only; containers and unmatched text may make the actual carried weight different.", matchedEntries.Count > 0 ? "Partial estimate" : "Insufficient data"));
        snapshot.BuildMetrics.Add(new("Known value", matchedEntries.Count > 0 ? $"{totalValue:N0}" : "Not available", "Combined Librarium cost for matched entries; world economy and merchant prices may differ.", matchedEntries.Count > 0 ? "Reference value" : "Insufficient data"));
        snapshot.BuildMetrics.Add(new("XP progression", session.ExperienceNeeded > 0 ? $"{progressionPercent:0.0}%" : "Not captured", session.ExperienceNeeded > 0 ? $"{xpRemaining:N0} XP remain to the captured target." : "Run score to capture current and next-level experience.", session.ExperienceNeeded > 0 ? "Live Client snapshot" : "Insufficient data"));
        snapshot.BuildMetrics.Add(new("Equipment capture", session.EquippedItems.Count > 0 ? $"{session.EquippedItems.Count} equipped; {session.InventoryItems.Count} carried" : "Equipment not captured", "Equipped and carried records are kept separate so comparisons do not treat every carried item as active gear.", session.EquippedItems.Count > 0 ? "Live Client output" : "Inventory-only estimate"));

        AddTimedRateSample(snapshot, session);
        AddMagicPlanning(snapshot, session);
        AddProgressionPlanning(snapshot, session, matchedEntries, xpRemaining, progressionPercent, totalValue);
        AddSimulationScenarios(snapshot, session, averageDamage, armorClass, damageResistance, totalWeight, progressionPercent, matchedEntries.Count);
        AddUpgradeCandidates(snapshot, matchedEntries);
        snapshot.Comparisons.Add(new("Survivability", $"HP {session.HealthPercent}% | AC +{armorClass} | DR +{damageResistance}", session.HealthMaximum > 0 ? "Compare defensive candidates while preserving the Host-authoritative character state." : "Capture score/status output to enable build comparisons."));
        snapshot.Comparisons.Add(new("Magic reserve", $"MP {session.ManaPercent}%", session.ManaMaximum > 0 ? "Use spell-cost and mana-supporting item references for planning; the Client does not cast or equip from this view." : "Capture mana status to enable magic planning."));
        snapshot.Comparisons.Add(new("Progression", session.ExperienceSummary, session.ExperienceNeeded > 0 ? $"{xpRemaining:N0} XP remain to the captured target ({progressionPercent:0.0}% captured progress)." : "Run score to capture the next progression target."));
        snapshot.Comparisons.Add(new("Equipment knowledge", $"{matched}/{snapshot.EquipmentAndInventory.Count} inventory entries matched", "Analysis includes only matched records. Unmatched entries remain Client-local and are never written into Librarium."));
        snapshot.Summary = $"Read-only build snapshot for {snapshot.CharacterName}. {matched}/{snapshot.EquipmentAndInventory.Count} entries matched; {snapshot.UpgradeCandidates.Count} comparison candidates found. No character, Host, or Librarium data was changed.";
        return snapshot;
    }

    private async Task EnsureItemIndexAsync(string worldName, CancellationToken token)
    {
        if (_items.Count > 0 && _loadedWorld.Equals(worldName, StringComparison.OrdinalIgnoreCase)) return;
        _items.Clear();
        _loadedWorld = worldName;
        var root = FindRepositoryRoot();
        var worldRoot = Path.Combine(root, "Data", worldName);
        if (!Directory.Exists(worldRoot)) worldRoot = Path.Combine(root, "Data", "Classic");
        var path = Path.Combine(worldRoot, "items.json");
        if (!File.Exists(path)) return;
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var records = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("records", out var recordArray) ? recordArray : document.RootElement;
        if (records.ValueKind != JsonValueKind.Array) return;
        foreach (var record in records.EnumerateArray())
        {
            var name = Read(record, "name") ?? Read(record, "title");
            if (string.IsNullOrWhiteSpace(name)) continue;
            _items.Add(new(
                name,
                ReadNested(record, "classification", "type") ?? Read(record, "item_type") ?? string.Empty,
                ReadNested(record, "economy", "weight") ?? string.Empty,
                ReadNested(record, "economy", "cost") ?? string.Empty,
                ReadIntNested(record, "combat", "minimum_damage"),
                ReadIntNested(record, "combat", "maximum_damage"),
                ReadIntNested(record, "combat", "armor_class_bonus"),
                ReadIntNested(record, "combat", "damage_resistance_bonus"),
                ReadIntNested(record, "combat", "accuracy")));

        }
    }

    private async Task EnsureSpellIndexAsync(string worldName, CancellationToken token)
    {
        if (_spells.Count > 0 && _loadedSpellWorld.Equals(worldName, StringComparison.OrdinalIgnoreCase)) return;
        _spells.Clear();
        _loadedSpellWorld = worldName;
        var root = FindRepositoryRoot();
        var worldRoot = Path.Combine(root, "Data", worldName);
        if (!Directory.Exists(worldRoot)) worldRoot = Path.Combine(root, "Data", "Classic");
        var path = Path.Combine(worldRoot, "spells.json");
        if (!File.Exists(path)) return;
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var records = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("records", out var recordArray) ? recordArray : document.RootElement;
        if (records.ValueKind != JsonValueKind.Array) return;
        foreach (var record in records.EnumerateArray())
        {
            var name = Read(record, "name") ?? Read(record, "title");
            if (string.IsNullOrWhiteSpace(name)) continue;
            _spells.Add(new(
                name,
                ReadIntNested(record, "classification", "spell_level"),
                ReadIntNested(record, "costs", "mana_cost"),
                ReadIntNested(record, "costs", "energy_cost"),
                ReadIntNested(record, "effect", "min_effect"),
                ReadIntNested(record, "effect", "max_effect"),
                ReadIntNested(record, "effect", "duration")));
        }
    }

    private void AddTimedRateSample(ClientLibrariumSnapshot snapshot, ClientSessionConnection session)
    {
        var now = DateTimeOffset.Now;
        if (_rateBaselines.TryGetValue(session.SessionId, out var baseline))
        {
            var elapsed = now - baseline.CapturedAt;
            if (elapsed.TotalMinutes >= 1)
            {
                var xpDelta = Math.Max(0, session.ExperienceCurrent - baseline.Experience);
                var currencyDelta = session.CarriedCurrency - baseline.Currency;
                var hours = elapsed.TotalHours;
                snapshot.RateSamples.Add(new(
                    $"{elapsed.TotalMinutes:0.0} minutes",
                    $"{xpDelta:N0}",
                    hours > 0 ? $"{xpDelta / hours:N0}" : "Not available",
                    $"{currencyDelta:+#,0;-#,0;0}",
                    hours > 0 ? $"{currencyDelta / hours:+#,0;-#,0;0}" : "Not available",
                    elapsed.TotalMinutes >= 10 ? "Timed Client sample" : "Early sample; collect 10+ minutes"));
            }
            else
            {
                snapshot.RateSamples.Add(new("Less than one minute", "Awaiting sample", "Not available", "Awaiting sample", "Not available", "Refresh after at least one minute"));
            }
        }
        else
        {
            snapshot.RateSamples.Add(new("Baseline captured", "0", "Awaiting second snapshot", "0", "Awaiting second snapshot", "Refresh later to calculate rates"));
        }
        _rateBaselines[session.SessionId] = new(now, session.ExperienceCurrent, session.CarriedCurrency);
    }

    private void AddMagicPlanning(ClientLibrariumSnapshot snapshot, ClientSessionConnection session)
    {
        var available = _spells.Where(spell => session.CharacterLevel <= 0 || spell.Level <= session.CharacterLevel).ToList();
        var affordable = available.Where(spell => spell.ManaCost <= session.ManaCurrent).ToList();
        var highestEffect = affordable.OrderByDescending(spell => spell.MaximumEffect).ThenBy(spell => spell.ManaCost).FirstOrDefault();
        var efficient = affordable.Where(spell => spell.ManaCost > 0).OrderByDescending(spell => spell.AverageEffect / spell.ManaCost).FirstOrDefault();
        snapshot.MagicMetrics.Add(new("Mana reserve", session.ManaMaximum > 0 ? $"{session.ManaCurrent}/{session.ManaMaximum} ({session.ManaPercent}%)" : "Not captured", "Live Client status only; the Librarium bridge never casts a spell.", session.ManaMaximum > 0 ? "Live snapshot" : "Insufficient data"));
        snapshot.MagicMetrics.Add(new("Known spell reference", $"{available.Count:N0} level-appropriate records", $"{affordable.Count:N0} records have a listed mana cost within the current captured reserve.", _spells.Count > 0 ? "Read-only Librarium spell records" : "Spell data unavailable"));
        snapshot.MagicMetrics.Add(new("Largest listed effect", highestEffect is null ? "Not available" : $"{highestEffect.Name}: {highestEffect.MinimumEffect}-{highestEffect.MaximumEffect}", highestEffect is null ? "No affordable spell reference was found." : $"Listed cost {highestEffect.ManaCost} mana and {highestEffect.EnergyCost} energy; actual outcome is Host-authoritative.", highestEffect is null ? "Insufficient data" : "Reference estimate"));
        snapshot.MagicMetrics.Add(new("Effect efficiency", efficient is null ? "Not available" : efficient.Name, efficient is null ? "No non-zero mana-cost spell could be compared." : $"Approximately {efficient.AverageEffect / efficient.ManaCost:0.00} listed effect per mana before Host rules, resistance, scaling, and targeting.", efficient is null ? "Insufficient data" : "Planning estimate"));
    }

    private static void AddSimulationScenarios(
        ClientLibrariumSnapshot snapshot,
        ClientSessionConnection session,
        double averageDamage,
        int armorClass,
        int damageResistance,
        long totalWeight,
        double progressionPercent,
        int matchedItemCount)
    {
        var hpReady = session.HealthMaximum > 0 ? session.HealthPercent : 0;
        var manaReady = session.ManaMaximum > 0 ? session.ManaPercent : 0;
        var defensiveScore = Math.Clamp(hpReady + armorClass * 2 + damageResistance * 3, 0, 200);
        var offenseScore = Math.Clamp(averageDamage * 4 + Math.Max(0, manaReady - 25), 0, 200);
        var travelScore = Math.Clamp((hpReady + manaReady) / 2.0 - Math.Min(35, totalWeight / 20.0), 0, 100);

        snapshot.SimulationScenarios.Add(new(
            "Balanced encounter",
            ReadinessLabel((defensiveScore + offenseScore) / 4.0),
            averageDamage > 0 ? $"Estimated matched-item damage reference: {averageDamage:0.0} average; defensive reference AC +{armorClass}, DR +{damageResistance}." : "Capture and match equipped items before estimating combat readiness.",
            matchedItemCount == 0 ? "No matched equipment" : hpReady < 50 ? "Current health reserve" : manaReady < 25 ? "Current mana reserve" : "Host combat rules and target defenses",
            "Uses the selected Client snapshot and Librarium item fields only. It does not simulate exact Host combat rounds.",
            matchedItemCount > 0 && session.HealthMaximum > 0 ? "Planning estimate" : "Low confidence"));

        snapshot.SimulationScenarios.Add(new(
            "Defensive endurance",
            ReadinessLabel(defensiveScore / 2.0),
            $"Current HP reserve {hpReady}% with AC +{armorClass} and DR +{damageResistance} from matched records.",
            hpReady < 60 ? "Health reserve" : matchedItemCount == 0 ? "Equipment match coverage" : "Unknown enemy damage and status effects",
            "Assumes matched equipped records contribute their listed defensive fields. Host stacking and caps remain authoritative.",
            matchedItemCount > 0 ? "Moderate" : "Low"));

        snapshot.SimulationScenarios.Add(new(
            "Magic sustained use",
            ReadinessLabel(manaReady),
            session.ManaMaximum > 0 ? $"Mana reserve is {session.ManaCurrent:N0}/{session.ManaMaximum:N0} ({manaReady}%). Use the Magic & Spells tab for affordable spell references." : "Mana status has not been captured.",
            manaReady < 35 ? "Mana reserve" : "Spell costs, cooldowns, resistances, and Host scaling",
            "Read-only planning; no spell is prepared or cast and no exact success chance is inferred.",
            session.ManaMaximum > 0 ? "Live snapshot" : "Low confidence"));

        snapshot.SimulationScenarios.Add(new(
            "Travel and route readiness",
            ReadinessLabel(travelScore),
            $"Health {hpReady}%, mana {manaReady}%, known matched weight {totalWeight:N0}. Current room: {(string.IsNullOrWhiteSpace(session.CurrentRoomName) ? "not recognized" : session.CurrentRoomName)}.",
            string.IsNullOrWhiteSpace(session.CurrentRoomName) ? "Current room recognition" : totalWeight > 500 ? "Known carried weight" : hpReady < 50 ? "Health reserve" : "Route hazards and Host movement restrictions",
            "This scenario never discovers rooms or starts navigation. The Client map and Host remain responsible for route execution and validation.",
            session.HealthMaximum > 0 && !string.IsNullOrWhiteSpace(session.CurrentRoomName) ? "Live snapshot" : "Partial"));

        snapshot.SimulationScenarios.Add(new(
            "Progression session",
            session.ExperienceNeeded > 0 ? ReadinessLabel(Math.Max(20, 100 - progressionPercent / 2.0)) : "Needs score capture",
            session.ExperienceNeeded > 0 ? $"Captured next-level progress is {progressionPercent:0.0}%. Timed XP/hour samples improve this projection after repeated refreshes." : "Run score to capture current and next-level experience.",
            snapshot.RateSamples.Count == 0 ? "No timed XP-rate sample yet" : "Encounter availability and Host XP awards",
            "No XP is awarded or changed. Estimates use only captured Client values and elapsed time.",
            snapshot.RateSamples.Count > 0 ? "Timed Client sample" : "Partial"));
    }

    private static string ReadinessLabel(double score)
        => score >= 80 ? "Strong" : score >= 60 ? "Ready" : score >= 40 ? "Caution" : score > 0 ? "Not ready" : "Unknown";

    private static void AddProgressionPlanning(ClientLibrariumSnapshot snapshot, ClientSessionConnection session, IReadOnlyCollection<LibrariumItemIndexEntry> matchedEntries, long xpRemaining, double progressionPercent, long knownValue)
    {
        snapshot.ProgressionPlans.Add(new("Next-level progress", session.ExperienceNeeded > 0 ? $"{session.ExperienceCurrent:N0} / {session.ExperienceNeeded:N0} XP" : "Not captured", session.ExperienceNeeded > 0 ? $"{progressionPercent:0.0}% complete; {xpRemaining:N0} XP remaining" : "Run score to capture a target", "No XP-per-hour claim is made until the Client captures a timed play sample."));
        snapshot.ProgressionPlans.Add(new("Known carried value", matchedEntries.Count > 0 ? $"{knownValue:N0} reference value" : "No matched items", matchedEntries.Count > 0 ? $"Based on {matchedEntries.Count} matched inventory/equipment records" : "Capture inventory and improve matching", "Merchant prices, scarcity, condition, and server economy may differ."));
        snapshot.ProgressionPlans.Add(new("Loot knowledge", $"{matchedEntries.Count} matched records", matchedEntries.Count > 0 ? "Use matched item values to compare routes and loot profiles after timed samples are captured." : "No loot estimate is available yet.", "This view does not pick up, sell, bank, or alter any item."));
        snapshot.ProgressionPlans.Add(new("Timed-rate readiness", "Awaiting sample", "A future live sample can calculate XP/hour and reference loot/hour from two snapshots.", "Requires elapsed time plus before/after XP and inventory/currency snapshots."));
    }


    private void AddUpgradeCandidates(ClientLibrariumSnapshot snapshot, IReadOnlyCollection<LibrariumItemIndexEntry> matchedEntries)
    {
        foreach (var current in matchedEntries.Where(item => item.MaximumDamage > 0 || item.ArmorClassBonus > 0 || item.DamageResistanceBonus > 0).Take(12))
        {
            var candidate = _items
                .Where(item => item.Type.Equals(current.Type, StringComparison.OrdinalIgnoreCase) && !item.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase))
                .Select(item => new
                {
                    Item = item,
                    Gain = (item.AverageDamage - current.AverageDamage) + (item.ArmorClassBonus - current.ArmorClassBonus) + (item.DamageResistanceBonus - current.DamageResistanceBonus)
                })
                .Where(entry => entry.Gain > 0)
                .OrderByDescending(entry => entry.Gain)
                .ThenBy(entry => entry.Item.NumericWeight)
                .FirstOrDefault();
            if (candidate is null) continue;
            var improvement = current.MaximumDamage > 0
                ? $"Damage {current.MinimumDamage}-{current.MaximumDamage} → {candidate.Item.MinimumDamage}-{candidate.Item.MaximumDamage}"
                : $"AC/DR +{current.ArmorClassBonus}/+{current.DamageResistanceBonus} → +{candidate.Item.ArmorClassBonus}/+{candidate.Item.DamageResistanceBonus}";
            var weightDelta = candidate.Item.NumericWeight - current.NumericWeight;
            snapshot.UpgradeCandidates.Add(new(current.Name, candidate.Item.Name, current.Type, improvement, weightDelta == 0 ? "Same known weight" : $"Weight {weightDelta:+#;-#;0}", "Read-only Librarium comparison"));
        }
    }

    private static int ReadIntNested(JsonElement element, params string[] path)
        => int.TryParse(ReadNested(element, path), out var value) ? value : 0;

    private ClientLibrariumItemMatch MatchItem(string captured, string capturedArea)
    {
        var normalized = Normalize(captured);
        var exact = _items.FirstOrDefault(item => Normalize(item.Name).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        var match = exact ?? _items.FirstOrDefault(item => normalized.Contains(Normalize(item.Name), StringComparison.OrdinalIgnoreCase) || Normalize(item.Name).Contains(normalized, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? new ClientLibrariumItemMatch { CapturedArea = capturedArea, CapturedText = captured, LibrariumName = "No Librarium match", MatchStatus = "Not matched", Notes = "The captured Host text remains available to the Client and was not added to Librarium." }
            : new ClientLibrariumItemMatch { CapturedArea = capturedArea, CapturedText = captured, LibrariumName = match.Name, MatchStatus = "Matched", ItemType = match.Type, Weight = match.Weight, Cost = match.Cost, Notes = "Read-only Librarium reference" };
    }

    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();
    private static string? Read(JsonElement element, string property) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? value.ToString() : null;
    private static string? ReadNested(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path) if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current)) return null;
        return current.ToString();
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Data")) && Directory.Exists(Path.Combine(directory.FullName, "Librarium"))) return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not find the repository Data and Librarium folders.");
    }

    private sealed record RateBaseline(DateTimeOffset CapturedAt, long Experience, long Currency);

    private sealed record LibrariumSpellIndexEntry(
        string Name, int Level, int ManaCost, int EnergyCost, int MinimumEffect, int MaximumEffect, int Duration)
    {
        public double AverageEffect => (MinimumEffect + MaximumEffect) / 2.0;
    }

    private sealed record LibrariumItemIndexEntry(
        string Name, string Type, string Weight, string Cost,
        int MinimumDamage, int MaximumDamage, int ArmorClassBonus, int DamageResistanceBonus, int Accuracy)
    {
        public int NumericWeight => int.TryParse(Weight, out var value) ? value : 0;
        public int NumericCost => int.TryParse(Cost, out var value) ? value : 0;
        public double AverageDamage => (MinimumDamage + MaximumDamage) / 2.0;
    }
}
