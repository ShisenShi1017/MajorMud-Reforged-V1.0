namespace Reforged.Server;
public static class RuntimeCombatControlPerceptionSmokeReport
{
    public static IReadOnlyList<string> CoveredBehaviors => new[]
    {
        "fear requests forced movement without selecting destination",
        "charm creates explicit controller-target ownership decision",
        "existing control replacement requires promoted policy",
        "dead or separated control participants rejected",
        "hidden and invisible effects contribute concealment",
        "detection effects contribute observer detection",
        "visibility resolves deterministically by detection versus concealment",
        "weak or missing production policies fail closed"
    };
}
