using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicNativeMessageCoverageReport(
    bool Passed, int NativeMessageCount, int VerifiedPlayerNoOutputCount,
    int InteractionSuccessReferenceCount, int InteractionFailureReferenceCount,
    int DistinctInteractionMessageCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeClassicNativeMessageCoverageAudit
{
    public static RuntimeClassicNativeMessageCoverageReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string name) { if (condition) checks.Add(name); else failures.Add(name); }
        var authority = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
        using var interactions = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, "Data", "Classic", "room_interactions.json")));
        var successReferences = new List<int>();
        var failureReferences = new List<int>();
        var orderedSuccess = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in interactions.RootElement.GetProperty("records").EnumerateArray())
        {
            if (!row.TryGetProperty("command_semantics", out var semantics) || semantics.ValueKind != JsonValueKind.Object) continue;
            if (semantics.TryGetProperty("success_message_ids", out var success) && success.ValueKind == JsonValueKind.Array)
            {
                var ids = success.EnumerateArray().Select(x => x.GetInt32()).ToArray();
                successReferences.AddRange(ids);
                orderedSuccess[row.GetProperty("id").GetString() ?? string.Empty] = ids;
            }
            foreach (var property in new[] { "failure_textblock_id", "room_item_failure_textblock_id", "inventory_item_failure_textblock_id" })
                if (semantics.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number)
                    failureReferences.Add(value.GetInt32());
        }

        foreach (var id in successReferences)
            if (!authority.TryResolveMessage(id, out _)) failures.Add($"interaction-success-message-unresolved:{id}");
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        foreach (var id in failureReferences)
        {
            if (authority.TryResolveTextblock(id, out _)) continue;
            if (!graph.TryGet(id, out var node))
            {
                failures.Add($"interaction-failure-textblock-unresolved:{id}");
                continue;
            }
            foreach (var operation in node.Operations.Where(x => x.Opcode is "message" or "text"))
            {
                var target = operation.NumericArguments.FirstOrDefault();
                var resolved = operation.Opcode == "message"
                    ? authority.TryResolveMessage(target, out _)
                    : authority.TryResolveTextblock(target, out _);
                if (!resolved) failures.Add($"interaction-failure-presentation-unresolved:{id}:{operation.Opcode}:{target}");
            }
        }

        var expectedPedestals = new Dictionary<string, (int First, string Payload)>(StringComparer.OrdinalIgnoreCase)
        {
            ["interaction.decoded.map17.3037.diamond-pedestal"] = (3318, "You place the diamond on the pedestal..."),
            ["interaction.decoded.map17.3039.pearl-pedestal"] = (3319, "You place the pearl on the pedestal..."),
            ["interaction.decoded.map17.3041.moonstone-pedestal"] = (3320, "You place the moonstone on the pedestal..."),
            ["interaction.decoded.map17.3043.marble-pedestal"] = (3321, "You place the marble on the pedestal...")
        };
        foreach (var expected in expectedPedestals)
        {
            Check(orderedSuccess.TryGetValue(expected.Key, out var ids) && ids.SequenceEqual(new[] { expected.Value.First, 3322 }),
                $"ordered-pedestal-message-ids:{expected.Key}");
            Check(authority.RequireMessage(expected.Value.First) == expected.Value.Payload,
                $"exact-pedestal-item-payload:{expected.Value.First}");
        }
        Check(authority.RequireMessage(3322) == "The pedestal swallows up the item, and then glows brightly!",
            "exact-pedestal-completion-payload:3322");
        foreach (var id in new[] { 1479, 1685, 1686 })
            Check(authority.TryResolveMessage(id, out var payload) && payload.Length == 0 && authority.IsVerifiedNoOutputMessage(id),
                $"observer-only-message-player-no-output:{id}");

        var failureCharacter = new RuntimeCharacterRecord(
            "character.audit.boulder", "account.audit", "Boulder Audit", "Classic",
            "race.audit", "Audit", "class.audit", "Audit", 10, 0, 1000, 1000, 0, 0,
            "room.map17.2946", 2946,
            new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(),
            Array.Empty<string>(), new Dictionary<string, string?>(), new Dictionary<string, string>(),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var failureBranch = RuntimeClassicDecodedFailureBranch.Execute(
            graph.Require(4235), failureCharacter, authority,
            RuntimeSpellCatalog.Load(projectRoot, "Classic"), new Random(1324));
        Check(failureBranch.MessageIds.SequenceEqual(new[] { 3309 }), "boulder-failure-message-3309-executed");
        Check(failureBranch.SpellIds.SequenceEqual(new[] { "spell.1324" }), "boulder-failure-cast-1324-executed");
        Check(failureBranch.Output.SequenceEqual(new[] { "The boulder is suddenly surrounded by an aura of flame!" }),
            "boulder-failure-native-output-exact");
        Check(failureBranch.DamageApplied is >= 50 and <= 70
            && failureBranch.Character.HitPoints == failureCharacter.HitPoints - failureBranch.DamageApplied,
            "boulder-failure-level-scaled-fire-damage-applied");

        failures.AddRange(RuntimeBundleFPhaseF10NativePayloadAudit.Validate(projectRoot));
        Check(authority.NativeMessageCount == 3869, "all-3869-logical-wccmsg2-records-loaded");
        Check(successReferences.Count > 0 && successReferences.All(id=>authority.TryResolveMessage(id,out _)),
            $"all-{successReferences.Count}-generated-interaction-success-message-references-resolved");
        Check(failureReferences.Count == 19, "all-19-interaction-failure-textblock-references-audited");
        Check(successReferences.Distinct().All(id=>authority.TryResolveMessage(id,out _)),
            $"all-{successReferences.Distinct().Count()}-distinct-interaction-success-message-payloads-resolved");
        Check(failures.Count == 0, "zero-native-message-output-disconnects");

        var noOutputCount = Enumerable.Range(1, 10000).Count(authority.IsVerifiedNoOutputMessage);
        var report = new RuntimeClassicNativeMessageCoverageReport(failures.Count == 0, authority.NativeMessageCount, noOutputCount,
            successReferences.Count, failureReferences.Count,
            successReferences.Concat(failureReferences).Distinct().Count(),
            checks, failures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        var reportPath = Path.Combine(projectRoot, "Reports", "RuntimeVerification", "classic_native_message_coverage.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, report.ToJson() + Environment.NewLine);
        return report;
    }
}
