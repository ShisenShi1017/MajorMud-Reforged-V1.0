namespace RealmGate.Server;

internal static class RuntimeClassicCombatAnsi
{
    internal const string Reset = "\u001b[0m";
    internal const string Red = "\u001b[0;31m";
    internal const string Green = "\u001b[0;32m";
    internal const string Yellow = "\u001b[0;33m";
    internal const string Cyan = "\u001b[0;36m";
    internal const string BrightRed = "\u001b[1;31m";
    internal const string BrightGreen = "\u001b[1;32m";
    internal const string BrightYellow = "\u001b[1;33m";
    internal const string BrightBlue = "\u001b[1;34m";
    internal const string BrightMagenta = "\u001b[1;35m";
    internal const string BrightCyan = "\u001b[1;36m";
    internal const string White = "\u001b[0;37m";

    // Original WCC emits ESC[0;33m immediately before *Combat Engaged*.
    internal static string Engagement(string npcName) =>
        $"{Yellow}*Combat Engaged*{Reset}";

    // Observed MajorMUD/MegaMUD presentation keeps the player's successful attack
    // family red and uses cyan for misses/dodges. Damage severity changes wording,
    // not the semantic foreground color.
    internal static string PlayerAttack(string text, ClassicHitOutcome outcome) =>
        Color(text, outcome is ClassicHitOutcome.Miss or ClassicHitOutcome.Dodged
            ? BrightCyan
            : BrightRed);

    // Incoming NPC attack prose is cyan in the captured native presentation,
    // including hit/miss variants. Do not reuse player-damage red here.
    internal static string NpcAttack(string text, ClassicHitOutcome outcome) =>
        Color(text, BrightCyan);

    internal static string Warning(string text) => Color(text, BrightYellow);
    internal static string Defeat(string text) => Color(text, BrightRed);
    internal static string Victory(string text) => Color(text, BrightGreen);
    internal static string Effect(string text) => Color(text, BrightMagenta);

    internal static string RoundSummary(int hitPoints, int maximumHitPoints, string npcName, int npcHitPoints, int npcMaximumHitPoints)
    {
        var playerColor = HealthColor(hitPoints, maximumHitPoints);
        var npcColor = HealthColor(npcHitPoints, npcMaximumHitPoints);
        return $"{BrightCyan}HP:{Reset} {playerColor}{hitPoints}/{maximumHitPoints}{Reset}; " +
               $"{BrightCyan}{npcName}:{Reset} {npcColor}{npcHitPoints}/{npcMaximumHitPoints}{Reset}";
    }

    internal static string Prompt(
        int hitPoints, int maximumHitPoints,
        int resource, int maximumResource,
        bool usesKai, string position)
    {
        var label = usesKai ? "KAI" : "MA";
        var suffix = string.IsNullOrWhiteSpace(position) ? string.Empty : " (" + position.Trim() + ")";
        return $"{BrightCyan}[HP={Reset}{HealthColor(hitPoints, maximumHitPoints)}{hitPoints}{Reset}" +
               $"{BrightCyan}/{label}={Reset}{HealthColor(resource, maximumResource)}{resource}{Reset}{BrightCyan}]:{Reset}{suffix}";
    }

    private static string HealthColor(int current, int maximum)
    {
        if (maximum <= 0 || current * 100 <= maximum * 25) return BrightRed;
        if (current * 100 <= maximum * 60) return BrightYellow;
        return BrightGreen;
    }

    private static string Color(string text, string color) => $"{color}{text}{Reset}";
}