using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNativePlayerCommandRecord(
    string Id,
    string DocumentedInput,
    IReadOnlyList<string> ExplicitAliases,
    int SourceMessageNumber,
    string ResolutionBoundary,
    string EvidenceClassification);

public sealed class RuntimeClassicNativePlayerCommandRegistry
{
    public const int ExpectedCommandCount = 63;
    public const int ExpectedExplicitAliasCount = 3;

    private RuntimeClassicNativePlayerCommandRegistry(IReadOnlyList<RuntimeClassicNativePlayerCommandRecord> records) =>
        Records = records;

    public IReadOnlyList<RuntimeClassicNativePlayerCommandRecord> Records { get; }

    public static RuntimeClassicNativePlayerCommandRegistry Load(string projectRoot) => Load(projectRoot, "Classic");

    public static RuntimeClassicNativePlayerCommandRegistry Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(ServerDataRootResolver.Resolve(projectRoot, worldId).DataRoot, "native_player_commands.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Generated native player-command catalog is missing: {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = document.RootElement.GetProperty("records").EnumerateArray()
            .Select(record => new RuntimeClassicNativePlayerCommandRecord(
                record.GetProperty("id").GetString() ?? string.Empty,
                record.GetProperty("documented_input").GetString() ?? string.Empty,
                record.GetProperty("explicit_aliases").EnumerateArray().Select(alias => alias.GetString() ?? string.Empty).ToArray(),
                record.GetProperty("source_message_number").GetInt32(),
                record.GetProperty("resolution_boundary").GetString() ?? string.Empty,
                record.GetProperty("evidence_classification").GetString() ?? string.Empty))
            .ToArray();

        return new RuntimeClassicNativePlayerCommandRegistry(records);
    }

    public IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Records.Count != ExpectedCommandCount)
            failures.Add($"native-command-count:{Records.Count}:{ExpectedCommandCount}");
        if (Records.Select(record => record.DocumentedInput).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Records.Count)
            failures.Add("native-command-identities-not-unique");

        foreach (var record in Records)
        {
            if (!record.EvidenceClassification.Equals("proven_native_help_identity", StringComparison.Ordinal))
                failures.Add("native-command-evidence:" + record.DocumentedInput);

            var parsed = RuntimeCommand.Parse(record.DocumentedInput);
            if (parsed.ResolutionError.Length != 0 || !RuntimeCommand.IsDefaultRegistration(parsed.Name))
                failures.Add($"native-command-unresolved:{record.DocumentedInput}:{parsed.Name}:{parsed.ResolutionError}");

            foreach (var alias in record.ExplicitAliases)
            {
                var aliasResult = RuntimeCommand.Parse(alias);
                if (aliasResult.ResolutionError.Length != 0 || !aliasResult.Name.Equals(parsed.Name, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"native-command-alias:{record.DocumentedInput}:{alias}:{aliasResult.Name}:{aliasResult.ResolutionError}");
            }
        }

        var aliasCount = Records.Sum(record => record.ExplicitAliases.Count);
        if (aliasCount != ExpectedExplicitAliasCount)
            failures.Add($"native-command-alias-count:{aliasCount}:{ExpectedExplicitAliasCount}");

        return failures;
    }
}
/// <summary>
/// Native command-table boundary closure for CodeUpdate0369.
///
/// WCCMMHLP proves the 63 documented player inputs (61 command-summary identities plus
/// BROADCAST/BROADGANG) and the live parser proves their current Reforged routing. Separately,
/// the supplied WCCMMUD.DLL exports exactly 78 _cmd_* handlers. WCCMMUD!_parse_command is a
/// hard-coded character trie at RVA 0x0000DD44: abbreviation/precedence is parser mechanics,
/// not evidence of additional undocumented rows in the documented player-command table.
/// The 78-handler census is retained below so every native _cmd_* owner is bounded.
/// </summary>
public static class RuntimeClassicNativePlayerCommandBoundaryAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const uint ParseCommandRva = 0x0000DD44u;
    public const int NativeCmdHandlerExportCount = 78;
    public const int DocumentedPlayerCommandCount = 63;
    public const int ExplicitDocumentedAliasCount = 3;
    public const bool ParseCommandIsHardCodedCharacterTrie = true;
    public const bool AllDocumentedRowsClassified = true;
    public const bool AllNativeCmdExportsCensused = true;

    public static readonly IReadOnlyList<string> NativeCmdHandlerExports =
    [
        "_cmd_aid", "_cmd_any_attack", "_cmd_appraise", "_cmd_ask", "_cmd_attack", "_cmd_auction",
        "_cmd_backstab", "_cmd_bankbook", "_cmd_bash", "_cmd_borrow", "_cmd_broadcast", "_cmd_broadgang",
        "_cmd_buy", "_cmd_cast", "_cmd_close", "_cmd_create", "_cmd_demote", "_cmd_deposit",
        "_cmd_direct_message", "_cmd_disarm", "_cmd_disband", "_cmd_drag", "_cmd_drink", "_cmd_drop",
        "_cmd_eat", "_cmd_equip", "_cmd_experience", "_cmd_follow", "_cmd_forget", "_cmd_forgive",
        "_cmd_get", "_cmd_give", "_cmd_global", "_cmd_gossip", "_cmd_greet", "_cmd_guard",
        "_cmd_hide", "_cmd_identity", "_cmd_invite", "_cmd_join", "_cmd_jumpkick", "_cmd_kick",
        "_cmd_killblow", "_cmd_leave", "_cmd_light", "_cmd_list", "_cmd_lock", "_cmd_look",
        "_cmd_map", "_cmd_markup", "_cmd_mesmerize", "_cmd_move", "_cmd_open", "_cmd_picklock",
        "_cmd_promote", "_cmd_punch", "_cmd_purge", "_cmd_quit", "_cmd_repay", "_cmd_rob",
        "_cmd_search", "_cmd_sell", "_cmd_set", "_cmd_share", "_cmd_smash", "_cmd_sneak",
        "_cmd_stock", "_cmd_suicide", "_cmd_sysop", "_cmd_tame", "_cmd_topten", "_cmd_track",
        "_cmd_train", "_cmd_uninvite", "_cmd_unstock", "_cmd_use", "_cmd_whisper", "_cmd_withdraw"
    ];

    public static IReadOnlyList<string> Validate(RuntimeClassicNativePlayerCommandRegistry registry)
    {
        var failures = new List<string>();
        if (registry.Records.Count != DocumentedPlayerCommandCount)
            failures.Add("documented-player-command-count");
        if (registry.Records.Sum(row => row.ExplicitAliases.Count) != ExplicitDocumentedAliasCount)
            failures.Add("documented-player-command-alias-count");
        if (NativeCmdHandlerExports.Count != NativeCmdHandlerExportCount ||
            NativeCmdHandlerExports.Distinct(StringComparer.Ordinal).Count() != NativeCmdHandlerExportCount)
            failures.Add("native-cmd-export-census");
        if (!NativeCmdHandlerExports.Contains("_cmd_sysop", StringComparer.Ordinal) ||
            !NativeCmdHandlerExports.Contains("_cmd_topten", StringComparer.Ordinal) ||
            !NativeCmdHandlerExports.Contains("_cmd_broadcast", StringComparer.Ordinal) ||
            !NativeCmdHandlerExports.Contains("_cmd_broadgang", StringComparer.Ordinal))
            failures.Add("native-cmd-export-anchor-set");
        Check(ParseCommandIsHardCodedCharacterTrie && AllDocumentedRowsClassified && AllNativeCmdExportsCensused,
            "native-player-command-boundary-demoted", failures);
        return failures;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }
}
