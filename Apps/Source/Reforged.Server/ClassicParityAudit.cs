namespace Reforged.Server;

/// <summary>
/// Builds a conservative Classic parity inventory from canonical Data/ and ImportedDatabases/.
/// It intentionally performs file/category inventory only. Field-level MajorMUD parity requires follow-up parsers.
/// </summary>
public sealed class ClassicParityAudit
{
    private static readonly ClassicParityCategoryDefinition[] Categories =
    {
        new("Rooms", "World rooms, locations, exits, and map-adjacent room records.", new[] { "room", "rooms", "rms" }, new[] { ".room", ".rooms" }),
        new("Monsters", "Monsters, mobs, NPC combat records, and spawn-adjacent source files.", new[] { "monster", "monsters", "mob", "mobs", "npc", "npcs" }, new[] { ".mon", ".mob", ".npc" }),
        new("Items", "Items, weapons, armor, equipment, consumables, and object records.", new[] { "item", "items", "weapon", "weapons", "armor", "armour", "object", "objects" }, new[] { ".itm", ".item", ".obj" }),
        new("Spells", "Spell, skill, ability, effect, and magic records.", new[] { "spell", "spells", "skill", "skills", "ability", "abilities", "effect", "effects" }, new[] { ".spl", ".spell" }),
        new("Shops", "Shop, store, vendor inventory, pricing, and restock reference files.", new[] { "shop", "shops", "store", "stores", "vendor", "vendors" }, new[] { ".sto", ".shop" }),
        new("Quests", "Quest chains, rewards, requirements, and quest state records.", new[] { "quest", "quests", "mission", "missions" }, new[] { ".qst", ".quest" }),
        new("ClassesRaces", "Class, race, profession, progression, and creation reference files.", new[] { "class", "classes", "race", "races", "profession", "professions" }, new[] { ".cls", ".race" }),
        new("Ganghouses", "Ganghouse, guild hall, house, and ownership reference files.", new[] { "gang", "gangs", "house", "houses", "guild", "guilds" }, new[] { ".hse", ".house" }),
        new("MapsPaths", "Maps, paths, routes, explorer/discovery-map inputs, and navigation reference files.", new[] { "map", "maps", "path", "paths", "route", "routes" }, new[] { ".mp", ".map" }),
        new("RawDatabases", "Unparsed legacy database files that may contain mixed Classic source data.", new[] { "wcc", "wbb", "wsg", "mud", "database", "dat" }, new[] { ".dat", ".mdb", ".db", ".xml", ".rar", ".zip" })
    };

    public ClassicParityAuditReport Run(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var canonicalDataPath = Path.Combine(root, "Data");
        var importedDatabasesPath = Path.Combine(root, "ImportedDatabases");
        var report = new ClassicParityAuditReport
        {
            ProjectRoot = root,
            CanonicalDataPath = canonicalDataPath,
            ImportedDatabasesPath = importedDatabasesPath
        };

        var canonicalFiles = EnumerateFiles(canonicalDataPath).ToList();
        var importedFiles = EnumerateFiles(importedDatabasesPath).ToList();

        report.CanonicalFileCount = canonicalFiles.Count;
        report.ImportedReferenceFileCount = importedFiles.Count;

        report.AddSource(BuildSourceSummary("Canonical Data", canonicalDataPath, "canonical-live-editable-data", canonicalFiles));
        report.AddSource(BuildSourceSummary("Imported Reference Data", importedDatabasesPath, "read-only-reference-import-material", importedFiles));

        var canonicalNames = new HashSet<string>(canonicalFiles.Select(file => Path.GetFileName(file)), StringComparer.OrdinalIgnoreCase);
        foreach (var name in importedFiles.Select(file => Path.GetFileName(file)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (canonicalNames.Contains(name))
            {
                report.AddExactNameMatch(name);
            }
        }

        foreach (var category in Categories)
        {
            var canonicalCategoryFiles = canonicalFiles.Where(category.Matches).ToList();
            var importedCategoryFiles = importedFiles.Where(category.Matches).ToList();
            var canonicalCategoryNames = new HashSet<string>(canonicalCategoryFiles.Select(GetFileName), StringComparer.OrdinalIgnoreCase);
            var matchCount = importedCategoryFiles.Select(GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count(canonicalCategoryNames.Contains);

            report.AddCategory(new ClassicParityCategorySummary(
                category.Name,
                category.Description,
                canonicalCategoryFiles.Count,
                importedCategoryFiles.Count,
                matchCount,
                DetermineCategoryStatus(canonicalCategoryFiles.Count, importedCategoryFiles.Count, matchCount)));
        }

        return report;
    }

    private static IEnumerable<string> EnumerateFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(file => !IsTransientFile(file))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTransientFile(string file)
    {
        var segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static ClassicParitySourceSummary BuildSourceSummary(string name, string path, string role, IReadOnlyList<string> files)
    {
        var extensionCounts = files
            .GroupBy(file => string.IsNullOrWhiteSpace(Path.GetExtension(file)) ? "<none>" : Path.GetExtension(file).ToLowerInvariant())
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new ClassicParitySourceSummary(name, path, role, files.Count, extensionCounts);
    }

    private static string GetFileName(string file) => Path.GetFileName(file) ?? string.Empty;

    private static string DetermineCategoryStatus(int canonicalCount, int importedCount, int matchCount)
    {
        if (canonicalCount == 0 && importedCount == 0)
        {
            return "no-files-detected";
        }

        if (canonicalCount == 0)
        {
            return "reference-only-needs-canonical-model";
        }

        if (importedCount == 0)
        {
            return "canonical-only-needs-reference-parser";
        }

        if (matchCount > 0)
        {
            return "name-overlap-detected-needs-field-comparison";
        }

        return "category-overlap-detected-needs-parser-mapping";
    }

    private sealed record ClassicParityCategoryDefinition(string Name, string Description, string[] Keywords, string[] Extensions)
    {
        public bool Matches(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var extension = Path.GetExtension(file);
            var relative = file.Replace('\\', '/');

            return Extensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase)) ||
                   Keywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || relative.Contains($"/{keyword}/", StringComparison.OrdinalIgnoreCase));
        }
    }
}
