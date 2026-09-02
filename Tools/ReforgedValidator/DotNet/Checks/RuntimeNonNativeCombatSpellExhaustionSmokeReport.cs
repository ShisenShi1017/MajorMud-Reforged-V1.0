namespace Reforged.Server;
public static class RuntimeNonNativeCombatSpellExhaustionSmokeReport
{
    public static IReadOnlyDictionary<string,string> Describe()=>new Dictionary<string,string>
    {
        ["complete-canonical-classification"]="Every canonical spell, evidence-visible weapon, and ability receives exactly one exhaustion status.",
        ["explicit-execution-gate"]="Only promoted_test_execution records pass RequireExecutable.",
        ["definition-only-rejection"]="item.1622 remains definition-bound but execution-blocked until attack path and timing semantics are promoted.",
        ["native-blocked-rejection"]="spell.1304 remains blocked without invented target, resistance, timing, resource, or effect behavior.",
        ["unknown-id-rejection"]="Definitions outside the canonical matrix fail closed."
    };
}
