using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed class RuntimeAbilityDomainBoundary
{
    private readonly HashSet<int> _questAbilityIds;
    private readonly HashSet<string> _learnedSkillIds;

    private RuntimeAbilityDomainBoundary(HashSet<int> questAbilityIds, HashSet<string> learnedSkillIds)
    {
        _questAbilityIds = questAbilityIds;
        _learnedSkillIds = learnedSkillIds;
    }

    public static RuntimeAbilityDomainBoundary Load(string projectRoot, string worldId)
    {
        var resolved = ServerDataRootResolver.Resolve(projectRoot, worldId);
        var dataRoot = resolved.DataRoot;
        using var abilities = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "abilities.json")));
        using var skills = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "skills.json")));
        var questIds = abilities.RootElement.GetProperty("records").EnumerateArray()
            .Select(x => x.TryGetProperty("legacy_id", out var id) && int.TryParse(id.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1)
            .Where(x => x >= 0).ToHashSet();
        var skillIds = skills.RootElement.GetProperty("records").EnumerateArray()
            .Select(x => x.GetProperty("id").GetString() ?? string.Empty)
            .Where(x => x.StartsWith("skill.", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (questIds.Count == 0 || skillIds.Count == 0) throw new InvalidOperationException("Ability domain catalogs are empty.");
        if (resolved.WorldId.Equals("Classic", StringComparison.OrdinalIgnoreCase) && (questIds.Count != 188 || skillIds.Count != 13))
            throw new InvalidOperationException("Classic ability domain catalogs are incomplete.");
        return new RuntimeAbilityDomainBoundary(questIds, skillIds);
    }

    public bool IsQuestAbility(int legacyId) => _questAbilityIds.Contains(legacyId);
    public bool IsLearnedSkill(string canonicalId) => _learnedSkillIds.Contains(canonicalId);
    public void RequireQuestAbility(int legacyId)
    {
        if (!IsQuestAbility(legacyId)) throw new InvalidOperationException($"Unknown quest ability ability.{legacyId}.");
    }
    public void RequireLearnedSkill(string canonicalId)
    {
        if (!IsLearnedSkill(canonicalId)) throw new InvalidOperationException($"Unknown learned skill '{canonicalId}'.");
    }
}
