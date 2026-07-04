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
            var ruleProfilesDisabled = module.RuleProfiles.Count > 0 && module.RuleProfiles.All(profile => !profile.RuntimeEnabled);
            var hostReady = dataRootExists &&
                dataCatalog.MissingCategoryCount == 0 &&
                dataCatalog.LoadedRecordCount > 0 &&
                worldProjection.ProjectionReady &&
                !module.GameplayEnabled &&
                ruleProfilesDisabled;

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
                ruleProfilesDisabled,
                dataCatalog.CategoryCount,
                dataCatalog.RuntimeReadyCategoryCount,
                dataCatalog.RuntimeBlockedCategoryCount,
                dataCatalog.EmptyPlaceholderCategoryCount,
                worldProjection.ProjectionReady,
                worldProjection.WorldRoomCount,
                worldProjection.MissingRoomCount,
                hostReady,
                DetermineStatus(hostReady, dataRootExists, ruleProfilesDisabled, worldProjection.ProjectionReady),
                DetermineReadinessNote(hostReady, dataRootExists, ruleProfilesDisabled, dataCatalog, worldProjection)));
        }

        return report;
    }

    private static string DetermineStatus(bool hostReady, bool dataRootExists, bool ruleProfilesDisabled, bool worldProjectionReady)
    {
        if (hostReady)
        {
            return "host-ready-gameplay-disabled";
        }

        if (!dataRootExists)
        {
            return "missing-data-root";
        }

        if (!worldProjectionReady)
        {
            return "world-projection-has-gaps";
        }

        if (!ruleProfilesDisabled)
        {
            return "runtime-rule-profile-enabled";
        }

        return "host-readiness-has-gaps";
    }

    private static string DetermineReadinessNote(
        bool hostReady,
        bool dataRootExists,
        bool ruleProfilesDisabled,
        ServerDataCatalogAuditReport dataCatalog,
        ServerWorldProjectionAuditReport worldProjection)
    {
        if (hostReady)
        {
            return "Module data root, catalog, and world projection are ready for non-gameplay host planning; gameplay remains disabled.";
        }

        if (!dataRootExists)
        {
            return "Module data root is missing.";
        }

        if (!worldProjection.ProjectionReady)
        {
            return "World projection must be ready before host planning can use the module.";
        }

        if (!ruleProfilesDisabled)
        {
            return "Runtime rule profiles must remain disabled until an approved gameplay/runtime slice.";
        }

        if (dataCatalog.RuntimeBlockedCategoryCount > 0)
        {
            return "Canonical data still has runtime-blocked categories; keep module in non-gameplay host planning.";
        }

        return "Module readiness has unresolved gaps.";
    }
}
