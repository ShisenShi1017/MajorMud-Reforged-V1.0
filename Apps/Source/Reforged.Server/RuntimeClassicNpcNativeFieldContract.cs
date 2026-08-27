namespace Reforged.Server;

/// <summary>
/// Native MajorMUD monster-record width contract recovered from the Nightmare Redux field map.
/// MajorMUD is a 16-bit legacy target, but its monster record is mixed-width: many gameplay
/// fields are signed 16-bit integers, attack type/percent fields are bytes, and identifiers,
/// message pointers, experience and currency fields are signed 32-bit longs.
///
/// This class validates decoded Classic data at the native storage boundary. It intentionally
/// does not wrap arbitrary modern runtime arithmetic back into Int16; overflow/truncation is
/// emulated only where the original operation itself has been recovered.
/// </summary>
public static class RuntimeClassicNpcNativeFieldContract
{
    public const int SignedInt16Minimum = -32768;
    public const int SignedInt16Maximum = 32767;
    public const int UnsignedByteMinimum = 0;
    public const int UnsignedByteMaximum = 255;

    public static RuntimeNpcDefinition Validate(RuntimeNpcDefinition npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        Int16(npc.GroupId, nameof(npc.GroupId), npc);
        Int16(npc.HitPoints, nameof(npc.HitPoints), npc);
        Int16(npc.Energy, nameof(npc.Energy), npc);
        Int16(npc.ArmorClass, nameof(npc.ArmorClass), npc);
        Int16(npc.DamageResistance, nameof(npc.DamageResistance), npc);
        Int16(npc.MagicResistance, nameof(npc.MagicResistance), npc);
        Int16(npc.CharmResistance, nameof(npc.CharmResistance), npc);
        Int16(npc.CharmLevel, nameof(npc.CharmLevel), npc);
        Int16(npc.FollowValue, nameof(npc.FollowValue), npc);
        Int16(npc.RegenTime, nameof(npc.RegenTime), npc);
        Int16(npc.HitPointRegen, nameof(npc.HitPointRegen), npc);
        Int16(npc.GameLimit, nameof(npc.GameLimit), npc);
        Int16(npc.MonsterType, nameof(npc.MonsterType), npc);
        Int16(npc.BackstabDefense, nameof(npc.BackstabDefense), npc);

        // MME/NMR names this native Int16 field Follow%; all retained 1.11p rows are 0..100.
        if (npc.FollowValue is < 0 or > 100)
            throw new InvalidDataException($"NPC_NATIVE_FOLLOW_PERCENT_RANGE:{npc.LegacyId}:{npc.FollowValue}");

        Int32(npc.LegacyId, nameof(npc.LegacyId), npc);
        Int32(npc.ExperienceReward, nameof(npc.ExperienceReward), npc);
        Int32(npc.CopperReward, nameof(npc.CopperReward), npc);
        Int32(npc.SilverReward, nameof(npc.SilverReward), npc);
        Int32(npc.GoldReward, nameof(npc.GoldReward), npc);
        Int32(npc.PlatinumReward, nameof(npc.PlatinumReward), npc);
        Int32(npc.RunicReward, nameof(npc.RunicReward), npc);

        foreach (var attack in npc.Attacks)
        {
            Byte(attack.Type, $"attack[{attack.Slot}].Type", npc);
            // Att% is a native byte. Retained Classic rows include values above 100; with the
            // recovered inclusive d100 comparison those values intentionally saturate to always
            // true when their slot is reached, so do not clamp them to a modern 0..100 schema.
            Byte(attack.SelectionPercent, $"attack[{attack.Slot}].SelectionPercent", npc);
            Int16(attack.Accuracy, $"attack[{attack.Slot}].Accuracy", npc);
            Int16(attack.MinimumDamage, $"attack[{attack.Slot}].MinimumDamage", npc);
            Int16(attack.MaximumDamage, $"attack[{attack.Slot}].MaximumDamage", npc);
            Int16(attack.HitSpellLegacyId, $"attack[{attack.Slot}].HitSpellLegacyId", npc);
            Int16(attack.EnergyCost, $"attack[{attack.Slot}].EnergyCost", npc);
            Int32(attack.HitMessageLegacyId, $"attack[{attack.Slot}].HitMessageLegacyId", npc);
            Int32(attack.MissMessageLegacyId, $"attack[{attack.Slot}].MissMessageLegacyId", npc);
            Int32(attack.DodgeMessageLegacyId, $"attack[{attack.Slot}].DodgeMessageLegacyId", npc);
        }
        return npc;
    }

    private static void Int16(long value, string field, RuntimeNpcDefinition npc)
    {
        if (value is < SignedInt16Minimum or > SignedInt16Maximum)
            throw new InvalidDataException($"NPC_NATIVE_INT16_RANGE:{npc.LegacyId}:{field}:{value}");
    }

    private static void Byte(long value, string field, RuntimeNpcDefinition npc)
    {
        if (value is < UnsignedByteMinimum or > UnsignedByteMaximum)
            throw new InvalidDataException($"NPC_NATIVE_BYTE_RANGE:{npc.LegacyId}:{field}:{value}");
    }

    private static void Int32(long value, string field, RuntimeNpcDefinition npc)
    {
        if (value is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException($"NPC_NATIVE_INT32_RANGE:{npc.LegacyId}:{field}:{value}");
    }
}
