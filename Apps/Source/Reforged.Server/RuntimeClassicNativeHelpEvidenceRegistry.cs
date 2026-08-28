namespace Reforged.Server;

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

    public static IReadOnlyList<RuntimeClassicNativeHelpEvidenceRow> Rows { get; } =
    [
        new("help.native-corpus", RuntimeClassicNativeHelpEvidenceState.Proven,
            "WCCMMHLP retains 263 nonempty native records: one main body and 131 complete title/body pairs.",
            "Librarium/Evidence/native_message_catalog_correlation_0022.json"),
        new("help.generated-projection", RuntimeClassicNativeHelpEvidenceState.Proven,
            "The compiler emits one main record and 131 topic records into generated Classic data.",
            "Data/Classic/native_help.json is generated and never edited manually."),
        new("help.runtime-payload", RuntimeClassicNativeHelpEvidenceState.Proven,
            "Runtime returns retained native bodies including ANSI without reading Librarium evidence directly.",
            "RuntimeMajorMudNativeHelpCatalog consumes generated Classic data."),
        new("help.native-main-aliases", RuntimeClassicNativeHelpEvidenceState.Proven,
            "The exact aliases printed on the native main help screen remain directly mapped.",
            "Pass 31 alias boundary remains unchanged."),
        new("help.unique-token-resolution", RuntimeClassicNativeHelpEvidenceState.Strong,
            "A unique exact keyword token resolves to its paired native help body.",
            "Useful runtime behavior; the original resolver implementation is not recovered."),
        new("help.topics-rendering", RuntimeClassicNativeHelpEvidenceState.Unknown,
            "Exact HELP TOPICS list formatting, grouping, ordering, and pagination.",
            "No native rendered topic-list payload is retained."),
        new("help.search-algorithm", RuntimeClassicNativeHelpEvidenceState.Unknown,
            "Exact prefix, fuzzy, ambiguity, and precedence behavior.",
            "Only direct main aliases and unique exact tokens are admitted."),
        new("help.permission-linkage", RuntimeClassicNativeHelpEvidenceState.Unknown,
            "Exact permission filtering and textblock linkage outside the retained WCCMMHLP pairs.",
            "No permission dispatcher callsite is recovered.")
    ];

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        ValidateCounts(failures, NativeRecordCount, NativeTopicCount, GeneratedRecordCount);
        if (Rows.Count != 8) failures.Add("expected-eight-native-help-evidence-rows");
        if (Rows.Count(row => row.State == RuntimeClassicNativeHelpEvidenceState.Proven) != 4)
            failures.Add("expected-four-proven-native-help-rows");
        if (Rows.Count(row => row.State == RuntimeClassicNativeHelpEvidenceState.Strong) != 1)
            failures.Add("expected-one-strong-native-help-row");
        if (Rows.Count(row => row.State == RuntimeClassicNativeHelpEvidenceState.Unknown) != 3)
            failures.Add("expected-three-open-native-help-rows");
        return failures;
    }

    private static void ValidateCounts(List<string> failures, int nativeRecords, int nativeTopics, int generatedRecords)
    {
        if (nativeRecords != 1 + (nativeTopics * 2)) failures.Add("native-help-record-pair-count-mismatch");
        if (generatedRecords != nativeTopics + 1) failures.Add("generated-help-record-count-mismatch");
    }
}
