namespace Reforged.Server;

/// <summary>
/// Defines the initial non-gameplay module registry for the Server host.
/// </summary>
public sealed class ServerGameModuleCatalog
{
    public GameModuleCatalog Load()
    {
        var catalog = new GameModuleCatalog();
        catalog.Register(new GameModuleRegistration(
            "majormud-classic",
            "MajorMUD Classic",
            "First game module and data baseline for RealmGate server construction.",
            "Data",
            GameplayEnabled: true,
            new[]
            {
                new RuleProfileRegistration("classic", "Classic", "planned-data-driven-baseline", RuntimeEnabled: true)
            }));

        return catalog;
    }
}
