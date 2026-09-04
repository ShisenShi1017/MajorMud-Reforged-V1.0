namespace RealmGate.Server;

/// <summary>
/// Read-only readiness view over registered Server modules.
/// </summary>
public sealed class ServerModuleReadinessReport
{
    private readonly List<ModuleReadinessSummary> modules = new();

    public string? ProjectRoot { get; internal set; }

    public IReadOnlyList<ModuleReadinessSummary> Modules => modules;

    public int ModuleCount => modules.Count;

    public int HostReadyModuleCount => modules.Count(module => module.HostReady);

    public int GameplayEnabledModuleCount => modules.Count(module => module.GameplayEnabled);

    public int RuntimeRuleProfileEnabledCount => modules.Sum(module => module.RuntimeRuleProfileEnabledCount);

    public string Status
    {
        get
        {
            if (ModuleCount == 0)
            {
                return "no-modules";
            }

            if (GameplayEnabledModuleCount > 0 || RuntimeRuleProfileEnabledCount > 0)
            {
                return "runtime-enabled";
            }

            return HostReadyModuleCount == ModuleCount ? "host-ready-gameplay-disabled" : "host-readiness-has-gaps";
        }
    }

    internal void AddModule(ModuleReadinessSummary module) => modules.Add(module);
}

public sealed record ModuleReadinessSummary(
    string ModuleId,
    string Name,
    string Role,
    string DataRoot,
    string DataRootPath,
    bool DataRootExists,
    bool GameplayEnabled,
    int RuleProfileCount,
    int RuntimeRuleProfileEnabledCount,
    bool RuleProfilesDisabled,
    int DataCategoryCount,
    int RuntimeReadyCategoryCount,
    int RuntimeBlockedCategoryCount,
    int EmptyPlaceholderCategoryCount,
    bool WorldProjectionReady,
    int WorldRoomCount,
    int MissingWorldRoomCount,
    bool HostReady,
    string Status,
    string ReadinessNote);
