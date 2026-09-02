namespace Reforged.Server;

public static class RuntimeCombatStatusModifiersSmokeReport
{
    public static IReadOnlyList<string> CoveredBehaviors => new[]
    {
        "deterministic active-effect ordering",
        "stack-count-aware modifier scaling",
        "attack and defense modifiers",
        "initiative and speed modifiers",
        "resistance and visibility modifiers",
        "physical-attack restriction",
        "spell-cast restriction",
        "movement restriction",
        "hidden-state break signal",
        "fail-closed missing or weak status policy"
    };
}
