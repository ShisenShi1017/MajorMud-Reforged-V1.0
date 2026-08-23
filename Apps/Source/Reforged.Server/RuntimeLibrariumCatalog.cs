namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderLibrarium(RuntimeSession session, RuntimeCommand command)
    {
        return ReforgedClientProtocol.Encode("LIBRARIUM", RenderLibrariumPayload(session, command));
    }

    private static string RenderLibrariumPayload(RuntimeSession session, RuntimeCommand command)
    {
        if (command.Arguments.Count == 0) return RenderLibrariumCatalog(session);
        if (!command.Arguments[0].Equals("page", StringComparison.OrdinalIgnoreCase))
            return "Usage: LIBRARIUM [PAGE <family> <offset> [search text]]";
        if (command.Arguments.Count < 3 || !int.TryParse(command.Arguments[2], out var offset) || offset < 0)
            return "Usage: LIBRARIUM PAGE <family> <offset> [search text]";
        var familyName = command.Arguments[1];
        var query = string.Join(" ", command.Arguments.Skip(3)).Trim();
        if (query.Length > 80) return "Librarium search text is limited to 80 characters.";
        if (!session.World.DataCatalog.Families.TryGetValue(familyName, out var family) || !family.Exists)
            return $"Unknown Librarium family '{familyName}'.";
        return RenderLibrariumPage(family, offset, query);
    }

    private static string RenderLibrariumCatalog(RuntimeSession session)
    {
        var catalog = session.World.DataCatalog;
        var rows = catalog.Families.Values.OrderBy(family => family.Family, StringComparer.OrdinalIgnoreCase).ToArray();
        var lines = new List<string>
        {
            "Librarium Server Catalog",
            $"World | {catalog.WorldId} | {catalog.CompileManifestHash}",
            "Family | Records | SHA256"
        };
        lines.AddRange(rows.Select(family => $"{family.Family} | {family.RecordCount} | {family.Sha256}"));
        lines.Add($"Total families: {rows.Length}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderLibrariumPage(GeneratedWorldFamilyStatus family, int offset, string query)
    {
        const int pageSize = 25;
        var all = EnumerateRecords(family.Path)
            .Where(record => query.Length == 0 || record.GetRawText().Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select((record, index) => ProjectRecord(record, index))
            .ToArray();
        var page = all.Skip(offset).Take(pageSize).ToArray();
        var lines = new List<string>
        {
            "Librarium Server Records",
            $"Page | {Encode(family.Family)} | {offset} | {pageSize} | {all.Length} | {Encode(query)}",
            "Record | ID | Name | Summary"
        };
        lines.AddRange(page.Select(record => $"Record | {Encode(record.Id)} | {Encode(record.Name)} | {Encode(record.Summary)}"));
        lines.Add($"Page records: {page.Length}");
        return string.Join(Environment.NewLine, lines);
    }

    private static (string Id, string Name, string Summary) ProjectRecord(System.Text.Json.JsonElement record, int index)
    {
        static string Read(System.Text.Json.JsonElement value, string property) =>
            value.ValueKind == System.Text.Json.JsonValueKind.Object && value.TryGetProperty(property, out var found) ? found.ToString() : string.Empty;
        var id = Read(record, "id");
        if (id.Length == 0) id = $"record.{index}";
        var name = Read(record, "name");
        if (name.Length == 0) name = Read(record, "title");
        if (name.Length == 0) name = id;
        var summary = record.GetRawText();
        return (id, name, summary);
    }

    private static IEnumerable<System.Text.Json.JsonElement> EnumerateRecords(string path)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("record_parts", out var parts) && parts.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var child = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, relative.Replace('/', Path.DirectorySeparatorChar)));
                foreach (var record in EnumerateRecords(child)) yield return record;
            }
            yield break;
        }
        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var record in root.EnumerateArray()) yield return record.Clone();
            yield break;
        }
        if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("records", out var records) && records.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var record in records.EnumerateArray()) yield return record.Clone();
            yield break;
        }
        yield return root.Clone();
    }

    private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
}
