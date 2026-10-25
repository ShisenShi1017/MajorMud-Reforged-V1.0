using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicAbilityAcquisitionResult(RuntimeCharacterRecord Character, IReadOnlyList<string> Lines, int AbilityId, bool Granted);

public sealed class RuntimeClassicAbilityAcquisitionAuthority
{
    private readonly IReadOnlyDictionary<(int NpcId, string Keyword), Definition> _definitions;
    private readonly RuntimeClassicNativeTextblockPayloadAuthority _payloads;

    private RuntimeClassicAbilityAcquisitionAuthority(
        IReadOnlyDictionary<(int NpcId, string Keyword), Definition> definitions,
        RuntimeClassicNativeTextblockPayloadAuthority payloads)
    {
        _definitions = definitions;
        _payloads = payloads;
    }

    public static RuntimeClassicAbilityAcquisitionAuthority Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "ability_acquisitions.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated ability acquisition catalog is required.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = document.RootElement.GetProperty("records");
        var definitions = records.EnumerateArray().Select(Parse).ToArray();
        if (definitions.Length != 2 || definitions.Select(x => x.AbilityId).Order().SequenceEqual(new[] { 186, 187 }) is false)
            throw new InvalidDataException("Classic ability acquisition catalog must contain exact decoded abilities 186 and 187.");
        if (definitions.Any(x => !x.HasVerifiedEmptySuccessOutput))
            throw new InvalidDataException("Classic ability acquisition success output must retain its decoded empty-inline-text proof.");
        return new(definitions.ToDictionary(x => (x.NpcId, x.Keyword), x => x), RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot, worldId));
    }

    public bool HasRoute(int npcId, string keyword) => _definitions.ContainsKey((npcId, Normalize(keyword)));
    public IReadOnlyList<string> GetKeywords(int npcId) => _definitions.Keys
        .Where(key => key.NpcId == npcId).Select(key => key.Keyword)
        .OrderBy(keyword => keyword, StringComparer.OrdinalIgnoreCase).ToArray();

    public RuntimeClassicAbilityAcquisitionResult Execute(int npcId, string keyword, RuntimeQuestDialogueContext context)
    {
        if (!_definitions.TryGetValue((npcId, Normalize(keyword)), out var definition))
            throw new InvalidOperationException("That NPC has no decoded response to that ASK keyword.");

        var snapshot = context.Capture();
        try
        {
            if (!definition.TransactionClasses.Contains(context.ClassLegacyId) || context.Level < definition.TransactionMinimumLevel)
                throw new InvalidOperationException("You do not meet the decoded class and level requirements.");
            if (!context.FailAbility(definition.AbilityId))
                throw new InvalidOperationException("You already possess that ability.");
            if (definition.RequiredItemId is int itemId) context.TakeItem(itemId);
            context.Experience = checked(context.Experience + definition.ExperienceReward);

            var granted = definition.MinimumLevelByClass.TryGetValue(context.ClassLegacyId, out var minimumLevel)
                && context.Level >= minimumLevel;
            if (granted) context.GiveAbility(definition.AbilityId, definition.AbilityValue);

            var lines = definition.MessageIds.Select(_payloads.RequireMessage).ToList();
            // Textblocks 9013 and 9026 are exact inline MDB empty strings (declared length 2).
            // Their silence is authoritative and must not fall back to guessed player-facing prose.
            return new(context.ToCharacterRecord(), lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(), definition.AbilityId, granted);
        }
        catch
        {
            context.Restore(snapshot);
            throw;
        }
    }

    private static Definition Parse(JsonElement row)
    {
        var gates = row.GetProperty("minimum_level_by_class").EnumerateObject()
            .ToDictionary(x => int.Parse(x.Name, CultureInfo.InvariantCulture), x => x.Value.GetInt32());
        var transactionClasses = row.TryGetProperty("transaction_classes", out var classes)
            ? classes.EnumerateArray().Select(x => x.GetInt32()).ToHashSet()
            : gates.Keys.ToHashSet();
        var requiredItem = row.TryGetProperty("required_item_id", out var item) && item.ValueKind == JsonValueKind.Number ? item.GetInt32() : (int?)null;
        return new(
            row.GetProperty("ability_id").GetInt32(), row.GetProperty("ability_value").GetInt32(),
            row.GetProperty("npc_id").GetInt32(), Normalize(row.GetProperty("command_keyword").GetString() ?? string.Empty),
            row.GetProperty("root_textblock_id").GetInt32(), row.GetProperty("action_textblock_id").GetInt32(),
            row.GetProperty("success_textblock_id").GetInt32(),
            row.GetProperty("success_output_disposition").GetString() == "verified_empty_inline_text"
                && row.GetProperty("success_output_declared_length").GetInt32() == 2,
            gates, transactionClasses,
            row.TryGetProperty("transaction_minimum_level", out var level) ? level.GetInt32() : gates.Values.Min(),
            requiredItem, row.GetProperty("experience_reward").GetInt32(),
            row.GetProperty("message_ids").EnumerateArray().Select(x => x.GetInt32()).ToArray());
    }

    private static string Normalize(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed record Definition(
        int AbilityId, int AbilityValue, int NpcId, string Keyword, int RootTextblockId, int ActionTextblockId,
        int SuccessTextblockId, bool HasVerifiedEmptySuccessOutput, IReadOnlyDictionary<int, int> MinimumLevelByClass, IReadOnlySet<int> TransactionClasses,
        int TransactionMinimumLevel, int? RequiredItemId, int ExperienceReward, IReadOnlyList<int> MessageIds);
}
