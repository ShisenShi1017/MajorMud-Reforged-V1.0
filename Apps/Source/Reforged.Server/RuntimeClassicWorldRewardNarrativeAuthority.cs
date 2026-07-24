using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicWorldRewardGate(int ClassId, string ClassName, int MinimumLevel);
public sealed record RuntimeClassicWorldRewardScript(string Id, int AbilityId, int RootTextblockId, int? TurninItemId, int? BaseMinimumLevel, int? ExperienceReward, IReadOnlyList<RuntimeClassicWorldRewardGate> Gates);
public sealed record RuntimeClassicFixedWorldReward(string Id, string ActivationKind, int MinimumLevel, int? RootTextblockId, int? RaceLegacyId, bool OneTime, IReadOnlyList<string> ActivationTerms, IReadOnlyList<RuntimeClassicWorldRewardOperation> Operations);
public sealed record RuntimeClassicWorldRewardOperation(string Opcode, IReadOnlyList<int> Arguments);
public sealed record RuntimeClassicWorldRewardNarrativeReport(bool Passed, int FixedRewardCount, int ClassScriptCount, int ClassGateCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{ public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }); }

/// <summary>
/// Bundle F Phase F9 activation authority. It consumes the decoded reward anchors and class gates already
/// owned by Bundle A and routes their activation through the existing narrative transaction coordinator.
/// It does not duplicate character, inventory, room, or NPC mutation ownership.
/// </summary>
public sealed class RuntimeClassicWorldRewardNarrativeAuthority
{
    public IReadOnlyList<RuntimeClassicFixedWorldReward> FixedRewards { get; }
    public IReadOnlyList<RuntimeClassicWorldRewardScript> ClassScripts { get; }
    private RuntimeClassicWorldRewardNarrativeAuthority(IReadOnlyList<RuntimeClassicFixedWorldReward> fixedRewards, IReadOnlyList<RuntimeClassicWorldRewardScript> scripts)
    { FixedRewards = fixedRewards; ClassScripts = scripts; }

    public static RuntimeClassicWorldRewardNarrativeAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "world_reward_narrative_authority.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Bundle F world-reward narrative catalog is required.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.GetProperty("disconnects").GetArrayLength() != 0 || root.GetProperty("conflicts").GetArrayLength() != 0)
            throw new InvalidDataException("Phase F9 catalog contains unresolved owned relationships.");
        var fixedRewards = root.GetProperty("fixed_rewards").EnumerateArray().Select(x => new RuntimeClassicFixedWorldReward(
            x.GetProperty("id").GetString() ?? string.Empty,
            x.GetProperty("activation_kind").GetString() ?? string.Empty,
            x.GetProperty("minimum_level").GetInt32(),
            x.TryGetProperty("root_textblock_id", out var tb) ? tb.GetInt32() : null,
            x.TryGetProperty("race_legacy_id", out var race) ? race.GetInt32() : null,
            x.GetProperty("one_time").GetBoolean(),
            x.TryGetProperty("activation_terms", out var terms) && terms.ValueKind == JsonValueKind.Array ? terms.EnumerateArray().Select(v => v.GetString() ?? string.Empty).ToArray() : Array.Empty<string>(),
            x.GetProperty("operations").EnumerateArray().Select(o => new RuntimeClassicWorldRewardOperation(o.GetProperty("opcode").GetString() ?? string.Empty, o.GetProperty("arguments").EnumerateArray().Select(v => v.GetInt32()).ToArray())).ToArray())).ToArray();
        var scripts = root.GetProperty("class_reward_scripts").EnumerateArray().Select(x => new RuntimeClassicWorldRewardScript(
            x.GetProperty("id").GetString() ?? string.Empty,
            x.GetProperty("ability_id").GetInt32(),
            x.GetProperty("root_textblock_id").GetInt32(),
            x.TryGetProperty("turnin_item_id", out var item) ? item.GetInt32() : null,
            x.TryGetProperty("base_minimum_level", out var level) ? level.GetInt32() : null,
            x.TryGetProperty("experience_reward", out var exp) ? exp.GetInt32() : null,
            x.GetProperty("gates").EnumerateArray().Select(g => new RuntimeClassicWorldRewardGate(g.GetProperty("class_id").GetInt32(), g.GetProperty("class_name").GetString() ?? string.Empty, g.GetProperty("minimum_level").GetInt32())).ToArray())).ToArray();
        if (fixedRewards.Length != 4 || scripts.Length != 3 || scripts.Sum(x => x.Gates.Count) != 23)
            throw new InvalidDataException("Complete Phase F9 fixed rewards and class gates are required.");
        return new(fixedRewards, scripts);
    }

    public bool TryResolveClassReward(int rootTextblockId, int classId, int level, out RuntimeClassicWorldRewardScript script, out RuntimeClassicWorldRewardGate gate)
    {
        script = ClassScripts.SingleOrDefault(x => x.RootTextblockId == rootTextblockId)!;
        gate = script?.Gates.SingleOrDefault(x => x.ClassId == classId)!;
        return script is not null && gate is not null && level >= gate.MinimumLevel;
    }
}

public static class RuntimeBundleFPhaseF9WorldRewardAudit
{
    public static RuntimeClassicWorldRewardNarrativeReport Run(string projectRoot)
    {
        var authority = RuntimeClassicWorldRewardNarrativeAuthority.Load(projectRoot);
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        Check(authority.FixedRewards.Select(x => x.Id).Order().SequenceEqual(new[] { "classic.reward.adult_red_dragon", "classic.reward.gaunt_one", "classic.reward.high_druid_gem", "classic.reward.ice_sorceress_throne" }), "all-fixed-world-reward-anchors-live");
        Check(authority.FixedRewards.All(x => x.OneTime), "fixed-rewards-one-time-duplicate-protected");
        Check(authority.FixedRewards.Single(x => x.Id == "classic.reward.high_druid_gem").Operations.Any(o => o.Opcode == "giveability" && o.Arguments.SequenceEqual(new[] { 129, 2 })), "high-druid-stat-and-completion-operations-live");
        Check(authority.FixedRewards.Single(x => x.Id == "classic.reward.ice_sorceress_throne").Operations.Any(o => o.Opcode == "giveability" && o.Arguments.SequenceEqual(new[] { 125, 2 })), "ice-throne-stat-and-completion-operations-live");
        Check(authority.FixedRewards.Single(x => x.Id == "classic.reward.adult_red_dragon").Operations.Count(o => o.Opcode == "addability") == 2, "adult-red-dragon-dual-stat-operations-live");
        var gaunt = authority.FixedRewards.Single(x => x.Id == "classic.reward.gaunt_one");
        Check(gaunt.RootTextblockId == 2698 && gaunt.RaceLegacyId == 13 && gaunt.MinimumLevel == 15, "gaunt-one-race-level-textblock-gates-live");
        Check(authority.ClassScripts.Single(x => x.AbilityId == 32).Gates.Count == 6, "smash-six-class-gates-live");
        Check(authority.ClassScripts.Single(x => x.AbilityId == 186).Gates.Count == 7, "perfect-stealth-seven-class-gates-live");
        Check(authority.ClassScripts.Single(x => x.AbilityId == 187).Gates.Count == 10, "meditate-ten-class-gates-live");
        Check(authority.TryResolveClassReward(2942, 1, 22, out _, out _) && !authority.TryResolveClassReward(2942, 1, 21, out _, out _), "class-specific-thresholds-enforced");
        Check(authority.ClassScripts.Single(x => x.AbilityId == 32).TurninItemId == 965 && authority.ClassScripts.Single(x => x.AbilityId == 187).TurninItemId == 1351, "decoded-item-turnins-owned-by-bundle-c");
        Check(authority.ClassScripts.Select(x => x.RootTextblockId).Order().SequenceEqual(new[] { 2698, 2942, 9029, 9046 }.Where(x => x != 2698)), "class-script-roots-authoritative");
        return new(failures.Count == 0, authority.FixedRewards.Count, authority.ClassScripts.Count, authority.ClassScripts.Sum(x => x.Gates.Count), failures.Count, checks, failures);
    }
}
