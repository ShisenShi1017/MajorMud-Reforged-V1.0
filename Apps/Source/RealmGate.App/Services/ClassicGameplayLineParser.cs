using System.Text.RegularExpressions;

namespace RealmGate.App.Services;

internal enum ClassicCombatEventKind
{
    None, Engaged, Disengaged, PlayerHit, PlayerMiss, IncomingHit,
    IncomingMiss, Dodge, Block, ExperienceReward
}

internal readonly record struct ClassicCombatObservation(
    ClassicCombatEventKind Kind,
    bool InCombat,
    int Amount = 0);

/// <summary>
/// Stateful recognition for MajorMUD's authoritative combat window. Outcomes
/// classify only between native engage/off markers, so unrelated prose cannot
/// mutate combat state.
/// </summary>
internal sealed partial class ClassicCombatStreamParser
{
    public bool InCombat { get; private set; }
    public bool AutocombatActive { get; private set; }

    public ClassicCombatObservation Observe(string? line)
    {
        var text = line?.Trim() ?? string.Empty;
        if (text.StartsWith("*Combat Engaged*", StringComparison.OrdinalIgnoreCase))
        {
            InCombat = true;
            AutocombatActive = true;
            return new(ClassicCombatEventKind.Engaged, true);
        }
        if (text.StartsWith("*Combat Off*", StringComparison.OrdinalIgnoreCase))
        {
            InCombat = false;
            AutocombatActive = false;
            return new(ClassicCombatEventKind.Disengaged, false);
        }

        // Native BREAK is character-only. A later incoming hit/miss proves that an
        // NPC or PvP opponent is still attacking even though local autocombat remains off.
        if (!InCombat)
        {
            var incomingDamage = Damage().Match(text);
            if (incomingDamage.Success && !text.StartsWith("You ", StringComparison.OrdinalIgnoreCase))
            {
                InCombat = true;
                return new(ClassicCombatEventKind.IncomingHit, true, ParseAmount(incomingDamage.Groups["amount"].Value));
            }
            if (Miss().IsMatch(text) && !text.StartsWith("You ", StringComparison.OrdinalIgnoreCase))
            {
                InCombat = true;
                return new(ClassicCombatEventKind.IncomingMiss, true);
            }
            return default;
        }

        var experience = ExperienceReward().Match(text);
        if (experience.Success)
            return new(ClassicCombatEventKind.ExperienceReward, true, ParseAmount(experience.Groups["amount"].Value));

        var damage = Damage().Match(text);
        if (damage.Success)
        {
            var amount = ParseAmount(damage.Groups["amount"].Value);
            return text.StartsWith("You ", StringComparison.OrdinalIgnoreCase)
                ? new(ClassicCombatEventKind.PlayerHit, true, amount)
                : new(ClassicCombatEventKind.IncomingHit, true, amount);
        }
        if (Dodge().IsMatch(text)) return new(ClassicCombatEventKind.Dodge, true);
        if (Block().IsMatch(text)) return new(ClassicCombatEventKind.Block, true);
        if (Miss().IsMatch(text))
            return text.StartsWith("You ", StringComparison.OrdinalIgnoreCase)
                ? new(ClassicCombatEventKind.PlayerMiss, true)
                : new(ClassicCombatEventKind.IncomingMiss, true);
        return new(ClassicCombatEventKind.None, true);
    }

    public void Reset()
    {
        InCombat = false;
        AutocombatActive = false;
    }

    private static int ParseAmount(string value) =>
        int.TryParse(value.Replace(",", string.Empty, StringComparison.Ordinal), out var amount)
            ? Math.Max(0, amount)
            : 0;

    [GeneratedRegex(@"\bfor\s+(?<amount>[0-9,]+)\s+damage\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Damage();
    [GeneratedRegex(@"^You gain\s+(?<amount>[0-9,]+)\s+experience\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExperienceReward();
    [GeneratedRegex(@"\b(?:miss(?:es)?|failed to hit)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Miss();
    [GeneratedRegex(@"\bdodg(?:e|es|ed)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Dodge();
    [GeneratedRegex(@"\b(?:armor|armour|shield)\b.*\b(?:blocks?|deflects?)\b|\b(?:blocks?|deflects?)\b.*\b(?:attack|blow|strike)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Block();
}
