namespace RealmGate.Server;

/// <summary>
/// Read-only catalog of game modules the Server can discover before gameplay systems exist.
/// </summary>
public sealed class GameModuleCatalog
{
    private readonly List<GameModuleRegistration> modules = new();

    public IReadOnlyList<GameModuleRegistration> Modules => modules;

    public int ModuleCount => modules.Count;

    public int GameplayEnabledModuleCount => modules.Count(module => module.GameplayEnabled);

    public int GameplayDisabledModuleCount => modules.Count(module => !module.GameplayEnabled);

    public string Status => ModuleCount == 0 ? "no-modules-registered" : "modules-registered";

    public void Register(GameModuleRegistration module)
    {
        modules.Add(module);
    }
}

public sealed record GameModuleRegistration(
    string Id,
    string Name,
    string Role,
    string DataRoot,
    bool GameplayEnabled,
    IReadOnlyList<RuleProfileRegistration> RuleProfiles);

public sealed record RuleProfileRegistration(
    string Id,
    string Name,
    string Status,
    bool RuntimeEnabled);
