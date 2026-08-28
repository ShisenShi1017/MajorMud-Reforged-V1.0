using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.Server;

/// <summary>
/// Runtime projection of the compiler-generated WCCMMHLP help family.
/// Bodies are returned verbatim (including ANSI) so the wire presentation remains
/// MajorMUD-compatible. Topic matching outside the explicit native main-menu aliases
/// is deliberately conservative and is not treated as proof of the original resolver.
/// </summary>
public sealed class RuntimeMajorMudNativeHelpCatalog
{
    private const string GeneratedRelativePath = "Data/Classic/native_help.json";
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _mainBody;
    private readonly IReadOnlyDictionary<int, string> _bodies;
    private readonly IReadOnlyList<RuntimeMajorMudHelpTopic> _topics;

    private RuntimeMajorMudNativeHelpCatalog(
        string mainBody,
        IReadOnlyDictionary<int, string> bodies,
        IReadOnlyList<RuntimeMajorMudHelpTopic> topics)
    {
        _mainBody = mainBody;
        _bodies = bodies;
        _topics = topics;
    }

    public static RuntimeMajorMudNativeHelpCatalog Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, GeneratedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) throw new FileNotFoundException("Generated MajorMUD native help catalog is missing.", path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = document.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var main = records.SingleOrDefault(record =>
            string.Equals(ReadString(record, "record_kind"), "main", StringComparison.OrdinalIgnoreCase));
        if (main.ValueKind == JsonValueKind.Undefined || ReadString(main, "body").Length == 0)
            throw new InvalidDataException("Generated MajorMUD help main body is missing.");

        var bodies = new Dictionary<int, string>();
        var topics = new List<RuntimeMajorMudHelpTopic>();
        foreach (var topic in records.Where(record =>
                     string.Equals(ReadString(record, "record_kind"), "topic", StringComparison.OrdinalIgnoreCase))
                 .OrderBy(record => record.GetProperty("title_message_number").GetInt32()))
        {
            var titleNumber = topic.GetProperty("title_message_number").GetInt32();
            var bodyNumber = topic.GetProperty("body_message_number").GetInt32();
            var keywords = ReadString(topic, "native_keywords");
            var body = ReadString(topic, "body");
            if (keywords.Length == 0 || body.Length == 0) continue;
            bodies[bodyNumber] = body;
            topics.Add(new RuntimeMajorMudHelpTopic(titleNumber, bodyNumber, keywords, Tokenize(keywords)));
        }

        if (topics.Count != 131 || !bodies.ContainsKey(3))
            throw new InvalidDataException($"Generated MajorMUD help catalog expected 131 topics including COMMANDS; found {topics.Count}.");
        return new RuntimeMajorMudNativeHelpCatalog(ReadString(main, "body"), bodies, topics);
    }

    public string Main() => _mainBody;

    public bool TryResolve(string query, out string body)
    {
        body = string.Empty;
        var canonical = Canonicalize(query);
        if (canonical.Length == 0)
        {
            body = _mainBody;
            return true;
        }

        // Labels printed by the native HELP main screen. These are direct evidence,
        // not inferred aliases.
        var nativeMainAliasBody = canonical switch
        {
            "commands" => 3,
            "commun" or "communications" => 5,
            "combat" => 7,
            "races" => 9,
            "classes" => 11,
            "info" => 19,
            "spells" => 21,
            "shops" => 23,
            "laws" => 31,
            "movement" => 25,
            "party" => 27,
            "tips" => 33,
            "items" => 35,
            "profile" => 37,
            "misc" => 111,
            "set" => 117,
            "stats1" => 45,
            "stats2" => 47,
            "help" => 263,
            _ => 0
        };
        if (nativeMainAliasBody > 0 && _bodies.TryGetValue(nativeMainAliasBody, out var aliasBody))
        {
            body = aliasBody;
            return true;
        }

        // The decoded title records contain MajorMUD's search words. Exact token hits
        // are useful runtime behavior, but the original ambiguity/prefix resolver is
        // still evidence-gated and therefore not scored as strict parity.
        var exact = _topics.Where(topic => topic.Tokens.Contains(canonical, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (exact.Length == 1 && _bodies.TryGetValue(exact[0].BodyNumber, out var exactBody))
        {
            body = exactBody;
            return true;
        }

        return false;
    }

    public bool HasNativeMainAndCommands() => _mainBody.Length > 0 && _bodies.ContainsKey(3);

    public int TopicCount => _topics.Count;

    private static IReadOnlyList<string> Tokenize(string value) =>
        Whitespace.Replace(value.Replace('\0', ' '), " ").Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Canonicalize)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Canonicalize(string value)
    {
        var normalized = Whitespace.Replace(value.Trim().ToLowerInvariant(), " ");
        return new string(normalized.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '.').ToArray()).Trim();
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

public sealed record RuntimeMajorMudHelpTopic(
    int TitleNumber,
    int BodyNumber,
    string NativeKeywords,
    IReadOnlyList<string> Tokens);

public sealed partial class RuntimeLiveGameplayFacade
{
    private RuntimeMajorMudNativeHelpCatalog? _majorMudNativeHelp;

    public bool TryRenderMajorMudNativeHelp(string query, out string output)
    {
        _majorMudNativeHelp ??= RuntimeMajorMudNativeHelpCatalog.Load(_characters.ProjectRoot);
        return _majorMudNativeHelp.TryResolve(query, out output);
    }
}
