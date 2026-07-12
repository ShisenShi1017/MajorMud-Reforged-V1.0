namespace Reforged.Server;

/// <summary>
/// Evaluates registered modules for non-gameplay host readiness.
/// </summary>
public sealed class ServerModuleReadinessAudit
{
    public ServerModuleReadinessReport Run(
        string projectRoot,
        GameModuleCatalog moduleCatalog,
        ServerDataCatalogAuditReport dataCatalog,
        ServerWorldProjectionAuditReport worldProjection)
    {
        var root = Path.GetFullPath(projectRoot);
        var report = new ServerModuleReadinessReport
        {
            ProjectRoot = root
        };

        foreach (var module in moduleCatalog.Modules)
        {
            var dataRootPath = Path.Combine(root, module.DataRoot);
            var dataRootExists = Directory.Exists(dataRootPath);
            var ruleProfilesReady = module.RuleProfiles.Count > 0 && module.RuleProfiles.All(profile => profile.RuntimeEnabled);
            var hostReady = dataRootExists &&
                dataCatalog.MissingCategoryCount == 0 &&
                dataCatalog.LoadedRecordCount > 0 &&
                worldProjection.ProjectionReady &&
                module.GameplayEnabled &&
                ruleProfilesReady;

            report.AddModule(new ModuleReadinessSummary(
                module.Id,
                module.Name,
                module.Role,
                module.DataRoot,
                dataRootPath,
                dataRootExists,
                module.GameplayEnabled,
                module.RuleProfiles.Count,
                module.RuleProfiles.Count(profile => profile.RuntimeEnabled),
                !ruleProfilesReady,
                dataCatalog.CategoryCount,
                dataCatalog.RuntimeReadyCategoryCount,
                dataCatalog.RuntimeBlockedCategoryCount,
                dataCatalog.EmptyPlaceholderCategoryCount,
                worldProjection.ProjectionReady,
                worldProjection.WorldRoomCount,
                worldProjection.MissingRoomCount,
                hostReady,
                DetermineStatus(hostReady, dataRootExists, ruleProfilesReady, worldProjection.ProjectionReady),
                DetermineReadinessNote(hostReady, dataRootExists, ruleProfilesReady, dataCatalog, worldProjection)));
        }

        return report;
    }

    private static string DetermineStatus(bool hostReady, bool dataRootExists, bool ruleProfilesReady, bool worldProjectionReady)
    {
        if (hostReady)
        {
            return "host-ready-runtime-enabled";
        }

        if (!dataRootExists)
        {
            return "missing-data-root";
        }

        if (!worldProjectionReady)
        {
            return "world-projection-has-gaps";
        }

        if (!ruleProfilesReady)
        {
            return "runtime-rule-profile-enabled";
        }

        return "host-readiness-has-gaps";
    }

    private static string DetermineReadinessNote(
        bool hostReady,
        bool dataRootExists,
        bool ruleProfilesReady,
        ServerDataCatalogAuditReport dataCatalog,
        ServerWorldProjectionAuditReport worldProjection)
    {
        if (hostReady)
        {
            return "Module data root, catalog, world projection, gameplay, and runtime rule profiles are enabled.";
        }

        if (!dataRootExists)
        {
            return "Module data root is missing.";
        }

        if (!worldProjection.ProjectionReady)
        {
            return "World projection must be ready before host planning can use the module.";
        }

        if (!ruleProfilesReady)
        {
            return "Runtime rule profiles must be enabled for the production host.";
        }

        if (dataCatalog.RuntimeBlockedCategoryCount > 0)
        {
            return "Canonical data still has runtime-blocked categories; block live host startup.";
        }

        return "Module readiness has unresolved gaps.";
    }
}
