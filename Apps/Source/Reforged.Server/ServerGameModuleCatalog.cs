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
            "First game module and data baseline for Reforged server construction.",
            "Data",
            GameplayEnabled: false,
            new[]
            {
                new RuleProfileRegistration("classic", "Classic", "planned-data-driven-baseline", RuntimeEnabled: false)
            }));

        return catalog;
    }
}
