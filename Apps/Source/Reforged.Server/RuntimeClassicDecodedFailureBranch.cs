namespace Reforged.Server;

public sealed record RuntimeClassicDecodedFailureBranchResult(
    RuntimeCharacterRecord Character,
    IReadOnlyList<string> Output,
    IReadOnlyList<int> MessageIds,
    IReadOnlyList<string> SpellIds,
    int DamageApplied);

public static class RuntimeClassicDecodedFailureBranch
{
    public static RuntimeClassicDecodedFailureBranchResult Execute(
        RuntimeClassicTextblockNode node,
        RuntimeCharacterRecord character,
        RuntimeClassicNativeTextblockPayloadAuthority payloads,
        RuntimeSpellCatalog spells,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(payloads);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(random);

        var output = new List<string>();
        var messageIds = new List<int>();
        var spellIds = new List<string>();
        var updated = character;
        var damageApplied = 0;

        foreach (var operation in node.Operations.OrderBy(x => x.Sequence).ThenBy(x => x.Line))
        {
            var target = operation.NumericArguments.FirstOrDefault();
            switch (operation.Opcode)
            {
                case "message":
                    messageIds.Add(target);
                    AddOutput(output, payloads.RequireMessage(target));
                    break;
                case "text":
                    AddOutput(output, payloads.RequireTextblock(target));
                    break;
                case "cast":
                {
                    var spell = spells.Require($"spell.{target}");
                    spellIds.Add(spell.Id);
                    var resolution = RuntimeClassicPlayerSpellCombat.Resolve(
                        spell, ReadMagicResistance(updated), false, false, false, random, updated.Level);
                    var decodedDamage = spell.DecodedAbilities.Any(x => x.AbilityId is 1 or 17);
                    if (!decodedDamage && !resolution.Operation.Equals("damage", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Decoded failure spell {spell.Id} did not resolve as damage.");
                    var damage = Math.Min(updated.HitPoints, resolution.Magnitude);
                    updated = updated with
                    {
                        HitPoints = Math.Max(0, updated.HitPoints - damage),
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    };
                    damageApplied = checked(damageApplied + damage);
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"Decoded failure textblock {node.TextblockId} contains unsupported operation '{operation.Opcode}'.");
            }
        }

        return new(updated, output, messageIds, spellIds, damageApplied);
    }

    private static int ReadMagicResistance(RuntimeCharacterRecord character)
    {
        var equipmentBonus = character.SemanticState?.DerivedStats.TryGetValue("magic_resistance_bonus", out var projected) == true ? projected : 0;
        foreach (var key in new[] { "magic_resistance", "magicresistance", "magic resist", "mr" })
        {
            if (character.Skills.TryGetValue(key, out var skill)) return Math.Max(0, checked(skill + equipmentBonus));
            if (character.BaseStats.TryGetValue(key, out var stat)) return Math.Max(0, checked(stat + equipmentBonus));
        }
        return Math.Max(0, equipmentBonus);
    }

    private static void AddOutput(List<string> output, string payload)
    {
        if (!string.IsNullOrEmpty(payload)) output.Add(payload);
    }
}
