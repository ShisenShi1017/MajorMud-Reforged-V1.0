using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcHitSpellScriptOperation(string Kind, int FirstArgument, int SecondArgument = 0);
public sealed record RuntimeNpcHitSpellScriptBranch(int Threshold, int TextblockId, IReadOnlyList<RuntimeNpcHitSpellScriptOperation> Operations);
public sealed record RuntimeNpcHitSpellScript(int SpellId, int RootTextblockId, int RollMinimum, int RollMaximum, IReadOnlyList<RuntimeNpcHitSpellScriptBranch> Branches);
public sealed record RuntimeNpcHitSpellScriptPlan(RuntimeNpcHitSpellScriptBranch Branch, int TotalDamage);

public static class RuntimeNpcHitSpellScriptCatalog
{
    private static readonly string[] ExpectedKinds = ["message", "teleport", "message", "message", "teleport", "message", "cast", "cast"];

    public static RuntimeNpcHitSpellScript? Load(string projectRoot, int spellId) =>
        Load(projectRoot, ServerRuntimeOptions.Load(projectRoot).DefaultWorldId, spellId);

    public static RuntimeNpcHitSpellScript? Load(string projectRoot, string worldId, int spellId)
    {
        var dataRoot = ServerDataRootResolver.Resolve(projectRoot, worldId).DataRoot;
        var path = Path.Combine(dataRoot, "scripts.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var matches = document.RootElement.GetProperty("records").EnumerateArray()
            .Where(row => ReadString(row, "script_type").Equals("npc_hit_spell_textblock_chain", StringComparison.OrdinalIgnoreCase))
            .Where(row => ReadInt(row, "spell_id") == spellId)
            .ToArray();
        if (matches.Length == 0) return null;
        if (matches.Length != 1) throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_DUPLICATE:{spellId}");

        var row = matches[0];
        var table = row.GetProperty("random_table");
        var rollMinimum = ReadInt(table, "roll_minimum");
        var rollMaximum = ReadInt(table, "roll_maximum");
        if (rollMinimum != 1 || rollMaximum != 100)
            throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_ROLL_RANGE:{spellId}");

        var branches = table.GetProperty("branches").EnumerateArray().Select(ParseBranch).ToArray();
        if (branches.Length == 0 || branches[^1].Threshold != rollMaximum ||
            branches.Any(branch => branch.Threshold < rollMinimum || branch.Threshold > rollMaximum) ||
            branches.Zip(branches.Skip(1), (left, right) => left.Threshold >= right.Threshold).Any(invalid => invalid))
            throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_THRESHOLDS:{spellId}");

        return new(spellId, ReadInt(row, "root_textblock_id"), rollMinimum, rollMaximum, branches);
    }

    public static RuntimeNpcHitSpellScriptBranch SelectBranch(RuntimeNpcHitSpellScript script, int roll)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (roll < script.RollMinimum || roll > script.RollMaximum) throw new ArgumentOutOfRangeException(nameof(roll));
        return script.Branches.First(branch => roll <= branch.Threshold);
    }

    public static RuntimeNpcHitSpellScriptPlan ValidateAndPlan(
        string projectRoot,
        RuntimeWorld world,
        RuntimeNpcHitSpellScript script,
        int roll,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(random);
        var branch = SelectBranch(script, roll);
        var messages = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot, world.Definition.Id);
        var totalDamage = 0;

        foreach (var operation in branch.Operations)
        {
            switch (operation.Kind)
            {
                case "message":
                    if (!messages.TryResolveMessage(operation.FirstArgument, out _) ||
                        !messages.TryResolveSecondaryMessage(operation.FirstArgument, out _))
                        throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_MESSAGE_UNRESOLVED:{operation.FirstArgument}");
                    break;
                case "teleport":
                    if (world.FindRoom(operation.SecondArgument, operation.FirstArgument) is null)
                        throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_ROOM_UNRESOLVED:{operation.SecondArgument}:{operation.FirstArgument}");
                    break;
                case "cast":
                    var range = LoadDamageSpellRange(projectRoot, world.Definition.Id, operation.FirstArgument);
                    totalDamage = checked(totalDamage + random.Next(range.Minimum, checked(range.Maximum + 1)));
                    break;
                default:
                    throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_OPERATION_UNSUPPORTED:{operation.Kind}");
            }
        }
        return new(branch, totalDamage);
    }

    private static RuntimeNpcHitSpellScriptBranch ParseBranch(JsonElement row)
    {
        var operations = row.GetProperty("operations").EnumerateArray().Select(value => ParseOperation(value.GetString() ?? string.Empty)).ToArray();
        if (operations.Length != ExpectedKinds.Length || operations.Where((operation, index) => !operation.Kind.Equals(ExpectedKinds[index], StringComparison.Ordinal)).Any())
            throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_OPERATION_ORDER:{ReadInt(row, "textblock_id")}");
        return new(ReadInt(row, "threshold"), ReadInt(row, "textblock_id"), operations);
    }

    private static RuntimeNpcHitSpellScriptOperation ParseOperation(string source)
    {
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Length > 3 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var first) ||
            (parts.Length == 3 && !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_OPERATION_MALFORMED:{source}");
        var second = parts.Length == 3 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
        return new(parts[0].ToLowerInvariant(), first, second);
    }

    private static (int Minimum, int Maximum) LoadDamageSpellRange(string projectRoot, string worldId, int spellId)
    {
        var dataRoot = ServerDataRootResolver.Resolve(projectRoot, worldId).DataRoot;
        var path = Path.Combine(dataRoot, "spells.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var matches = document.RootElement.GetProperty("records").EnumerateArray()
            .Where(row => ReadInt(row, "legacy_id") == spellId).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_CAST_UNRESOLVED:{spellId}");
        var row = matches[0];
        var classification = row.GetProperty("classification");
        var effect = row.GetProperty("effect");
        var minimum = ReadInt(effect, "min_effect");
        var maximum = ReadInt(effect, "max_effect");
        if (ReadInt(classification, "spell_type") != 3 || ReadInt(classification, "target_type") != 1 || minimum < 0 || maximum < minimum)
            throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_CAST_NOT_DAMAGE:{spellId}");
        return (minimum, maximum);
    }

    private static int ReadInt(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out var value)) throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_FIELD_MISSING:{property}");
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return number;
        throw new InvalidDataException($"NPC_HIT_SPELL_SCRIPT_FIELD_INVALID:{property}");
    }

    private static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
