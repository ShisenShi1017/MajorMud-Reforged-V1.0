using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

/// <summary>
/// Runtime projection of the compiler-generated WCCMMHLP help family.
///
/// CodeUpdate0369 closes the retained native resolver boundary from WCCMMUD.DLL:
/// _help_text checks _ljnsame(query,"Topics") first; _ljnsame scans the whitespace
/// separated title words and WGSERVER!_sameto performs a case-insensitive prefix
/// comparison. The first title record that matches wins. HELP TOPICS walks native
/// title records in message-number order, emits the first word of each title, wraps
/// at the native 79-column threshold, and terminates on the "Help ?" sentinel.
/// Static WCCMMHLP topic iteration is not permission filtered.
/// </summary>
public sealed class RuntimeMajorMudNativeHelpCatalog
{
    private const string GeneratedRelativePath = "Data/Classic/native_help.json";
    private const int NativeTopicsWrapColumn = 79;
    private const string NativeTopicsHeader = "The following help topics are currently available: \r";
    private const string NativeNoHelp = "Sorry, no help is available for that topic!\r";
    private const string NativeTopicsSentinel = "Help ?";

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
            topics.Add(new RuntimeMajorMudHelpTopic(titleNumber, bodyNumber, keywords));
        }

        if (topics.Count != 131 || !bodies.ContainsKey(3) ||
            !topics.Any(topic => topic.NativeKeywords.Equals(NativeTopicsSentinel, StringComparison.Ordinal)))
            throw new InvalidDataException($"Generated MajorMUD help catalog expected 131 topics including COMMANDS and Help ?; found {topics.Count}.");

        return new RuntimeMajorMudNativeHelpCatalog(ReadString(main, "body"), bodies, topics);
    }

    public string Main() => _mainBody;
    public string NoHelpMessage() => NativeNoHelp;

    public bool TryResolve(string query, out string body)
    {
        body = string.Empty;
        query = NormalizeNativeQuery(query);
        if (query.Length == 0)
        {
            body = _mainBody;
            return true;
        }

        if (NativeSameTo(query, "Topics"))
        {
            body = RenderTopics();
            return true;
        }

        foreach (var topic in _topics)
        {
            if (!NativeLjnsame(query, topic.NativeKeywords)) continue;
            if (_bodies.TryGetValue(topic.BodyNumber, out var resolvedBody))
            {
                body = resolvedBody;
                return true;
            }
        }

        return false;
    }

    public string RenderTopics()
    {
        var builder = new StringBuilder(NativeTopicsHeader);
        var width = 0;

        foreach (var topic in _topics)
        {
            var title = topic.NativeKeywords.Replace('\0', ' ').Trim();
            if (title.Equals(NativeTopicsSentinel, StringComparison.Ordinal))
            {
                var terminal = title + ".\r";
                if (width + terminal.Length > NativeTopicsWrapColumn)
                {
                    builder.Append('\r');
                    width = 0;
                }
                builder.Append(terminal);
                break;
            }

            var firstWord = FirstNativeWord(title);
            if (firstWord.Length == 0) continue;
            var rendered = firstWord + ", ";
            if (width + rendered.Length > NativeTopicsWrapColumn)
            {
                builder.Append('\r');
                width = 0;
            }

            builder.Append(rendered);
            width += rendered.Length;
        }

        return builder.ToString();
    }

    public bool HasNativeMainAndCommands() => _mainBody.Length > 0 && _bodies.ContainsKey(3);
    public int TopicCount => _topics.Count;

    internal static bool NativeSameTo(string query, string candidate)
    {
        query = NormalizeNativeQuery(query);
        candidate = candidate.Replace('\0', ' ').Trim();
        return query.Length > 0
            && query.Length <= candidate.Length
            && candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool NativeLjnsame(string query, string nativeKeywords)
    {
        foreach (var word in NativeWords(nativeKeywords))
            if (NativeSameTo(query, word)) return true;
        return false;
    }

    private static IEnumerable<string> NativeWords(string value) =>
        value.Replace('\0', ' ')
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FirstNativeWord(string value) => NativeWords(value).FirstOrDefault() ?? string.Empty;

    private static string NormalizeNativeQuery(string value) =>
        (value ?? string.Empty).Replace('\0', ' ').Trim();

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

public sealed record RuntimeMajorMudHelpTopic(
    int TitleNumber,
    int BodyNumber,
    string NativeKeywords);

public sealed partial class RuntimeLiveGameplayFacade
{
    private RuntimeMajorMudNativeHelpCatalog? _majorMudNativeHelp;

    public bool TryRenderMajorMudNativeHelp(string query, out string output)
    {
        _majorMudNativeHelp ??= RuntimeMajorMudNativeHelpCatalog.Load(_characters.ProjectRoot);
        return _majorMudNativeHelp.TryResolve(query, out output);
    }

    public string RenderMajorMudNativeNoHelp()
    {
        _majorMudNativeHelp ??= RuntimeMajorMudNativeHelpCatalog.Load(_characters.ProjectRoot);
        return _majorMudNativeHelp.NoHelpMessage();
    }
}
