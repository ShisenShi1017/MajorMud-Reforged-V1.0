namespace RealmGate.Server;

public enum RuntimeClassicNativeHelpEvidenceState
{
    Proven,
    Strong,
    Unknown
}

public sealed record RuntimeClassicNativeHelpEvidenceRow(
    string Id,
    RuntimeClassicNativeHelpEvidenceState State,
    string Claim,
    string Boundary);

public static class RuntimeClassicNativeHelpEvidenceRegistry
{
    public const int NativeRecordCount = 263;
    public const int NativeTopicCount = 131;
    public const int GeneratedRecordCount = 132;
    public const uint NativeHelpTextVa = 0x0044B880u;
    public const uint NativeLjnsameVa = 0x0041974Du;
    public const uint WorldgroupSametoVa = 0x0043DBFCu;
    public const int TopicsWrapColumn = 79;

    public static IReadOnlyList<RuntimeClassicNativeHelpEvidenceRow> Rows { get; } =
    [
        new("help.native-corpus", RuntimeClassicNativeHelpEvidenceState.Proven,
            "WCCMMHLP retains 263 nonempty native records: one main body and 131 complete title/body pairs.",
            "Librarium/Objects/native_help/native_help_records.json"),
        new("help.generated-projection", RuntimeClassicNativeHelpEvidenceState.Proven,
            "The compiler emits one main record and 131 topic records into generated Classic data.",
            "Data/Classic/native_help.json is generated and never edited manually."),
        new("help.runtime-payload", RuntimeClassicNativeHelpEvidenceState.Proven,
            "Runtime returns retained native bodies including ANSI without reading Librarium evidence directly.",
            "RuntimeMajorMudNativeHelpCatalog consumes generated Classic data."),
        new("help.native-main-aliases", RuntimeClassicNativeHelpEvidenceState.Proven,
            "Native main/help title words resolve through the same recovered _ljnsame/_sameto matcher.",
            "WCCMMUD!_help_text at 0x0044B880."),
        new("help.prefix-resolution", RuntimeClassicNativeHelpEvidenceState.Proven,
            "WGSERVER!_sameto performs a case-insensitive prefix comparison and WCCMMUD!_ljnsame applies it to each whitespace-delimited title word; first title match wins.",
            "WCCMMUD!_ljnsame 0x0041974D; WGSERVER!_sameto 0x0043DBFC."),
        new("help.topics-rendering", RuntimeClassicNativeHelpEvidenceState.Proven,
            "HELP TOPICS is a dedicated native branch that enumerates title messages in message-number order, renders each first word, wraps at column 79, and terminates with Help ?.",
            "WCCMMUD!_help_text 0x0044B880 and retained WCCMMHLP title records."),
        new("help.search-algorithm", RuntimeClassicNativeHelpEvidenceState.Proven,
            "The retained resolver is prefix/first-match, not a synthetic fuzzy or ambiguity-rejection algorithm.",
            "Recovered _help_text -> _ljnsame -> WGSERVER!_sameto call chain."),
        new("help.permission-linkage", RuntimeClassicNativeHelpEvidenceState.Proven,
            "Static WCCMMHLP title iteration has no permission-filter callback; failed static lookup emits the native no-help string after supplemental families are considered.",
            "Recovered _help_text control flow and direct message-file iteration.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        Check(NativeRecordCount == 1 + (NativeTopicCount * 2), "native-help-record-pair-count-mismatch", failures);
        Check(GeneratedRecordCount == NativeTopicCount + 1, "generated-help-record-count-mismatch", failures);
        if (Rows.Count != 8) failures.Add("expected-eight-native-help-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicNativeHelpEvidenceState.Proven) != 8)
            failures.Add("expected-eight-proven-native-help-rows");
        if (Rows.Any(row => row.State != RuntimeClassicNativeHelpEvidenceState.Proven))
            failures.Add("native-help-open-evidence-remains");
        Check(NativeHelpTextVa == 0x0044B880u && NativeLjnsameVa == 0x0041974Du &&
            WorldgroupSametoVa == 0x0043DBFCu && TopicsWrapColumn == 79,
            "native-help-address-or-wrap-contract-mismatch", failures);
        return failures;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }
}
