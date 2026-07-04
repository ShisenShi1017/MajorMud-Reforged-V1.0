namespace Reforged.Server;

/// <summary>
/// Derives the Server host lifecycle state from existing non-gameplay bootstrap reports.
/// </summary>
public sealed class ServerHostLifecycleAudit
{
    public ServerHostLifecycleReport Run(
        string projectRoot,
        ServerHostConfigurationReport hostConfiguration,
        ServerDataCatalogAuditReport dataCatalog,
        ServerWorldProjectionAuditReport worldProjection,
        GameModuleCatalog moduleCatalog,
        ServerModuleReadinessReport moduleReadiness)
    {
        var root = Path.GetFullPath(projectRoot);
        var initialized = Directory.Exists(root);
        var configured = hostConfiguration.Status == "configured-runtime-disabled" && hostConfiguration.RuntimeDisabled;
        var dataReady = dataCatalog.MissingCategoryCount == 0 && dataCatalog.LoadedRecordCount > 0;
        var worldProjectionReady = worldProjection.ProjectionReady;
        var moduleReady =
            moduleCatalog.ModuleCount > 0 &&
            moduleReadiness.ModuleCount == moduleCatalog.ModuleCount &&
            moduleReadiness.HostReadyModuleCount == moduleReadiness.ModuleCount &&
            moduleReadiness.Status == "host-ready-gameplay-disabled";
        var runtimeDisabled =
            hostConfiguration.RuntimeDisabled &&
            moduleCatalog.GameplayEnabledModuleCount == 0 &&
            moduleReadiness.GameplayEnabledModuleCount == 0 &&
            moduleReadiness.RuntimeRuleProfileEnabledCount == 0;

        var report = new ServerHostLifecycleReport
        {
            ProjectRoot = root,
            Initialized = initialized,
            Configured = configured,
            DataReady = dataReady,
            WorldProjectionReady = worldProjectionReady,
            ModuleReady = moduleReady,
            RuntimeDisabled = runtimeDisabled,
            GameplayEnabled = hostConfiguration.GameplayEnabled || moduleCatalog.GameplayEnabledModuleCount > 0 || moduleReadiness.GameplayEnabledModuleCount > 0,
            NetworkingEnabled = hostConfiguration.NetworkingEnabled,
            WorldSimulationEnabled = hostConfiguration.WorldSimulationEnabled
        };

        report.AddStage(new HostLifecycleStage(
            "initialized",
            initialized,
            initialized ? "ready" : "not-ready",
            initialized ? "Repository root is available for host planning." : "Repository root is not available."));

        report.AddStage(new HostLifecycleStage(
            "configured",
            configured,
            configured ? "runtime-disabled" : "configuration-has-gaps",
            configured ? "Host configuration loaded with runtime toggles disabled." : "Host configuration is missing, invalid, or runtime-enabled."));

        report.AddStage(new HostLifecycleStage(
            "data-ready",
            dataReady,
            dataReady ? "loaded" : "data-has-gaps",
            dataReady ? "Canonical Data files are present and load records." : "Canonical Data files are missing or did not load records."));

        report.AddStage(new HostLifecycleStage(
            "world-projection-ready",
            worldProjectionReady,
            worldProjectionReady ? "projection-ready" : "projection-has-gaps",
            worldProjectionReady ? "World room projection has no missing or duplicate room references." : "World projection has missing or duplicate room references."));

        report.AddStage(new HostLifecycleStage(
            "module-ready",
            moduleReady,
            moduleReady ? "host-ready-gameplay-disabled" : "module-readiness-has-gaps",
            moduleReady ? "Registered modules are ready for non-gameplay host planning." : "Registered modules are not ready for host planning."));

        report.AddStage(new HostLifecycleStage(
            "runtime-disabled",
            runtimeDisabled,
            runtimeDisabled ? "disabled" : "enabled",
            runtimeDisabled ? "Gameplay, networking, world simulation, and runtime rule profiles remain disabled." : "One or more runtime paths are enabled."));

        return report;
    }
}
