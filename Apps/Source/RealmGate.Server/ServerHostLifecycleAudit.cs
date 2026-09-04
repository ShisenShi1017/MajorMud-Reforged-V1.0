namespace RealmGate.Server;

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
        var configured = hostConfiguration.Status == "runtime-enabled" && hostConfiguration.GameplayEnabled && hostConfiguration.NetworkingEnabled && hostConfiguration.WorldSimulationEnabled;
        var dataReady = dataCatalog.MissingCategoryCount == 0 && dataCatalog.LoadedRecordCount > 0;
        var worldProjectionReady = worldProjection.ProjectionReady;
        var moduleReady =
            moduleCatalog.ModuleCount > 0 &&
            moduleReadiness.ModuleCount == moduleCatalog.ModuleCount &&
            moduleReadiness.HostReadyModuleCount == moduleReadiness.ModuleCount &&
            moduleReadiness.Status == "runtime-enabled";
        var runtimeDisabled = false;

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
            configured ? "runtime-enabled" : "configuration-has-gaps",
            configured ? "Host configuration loaded with gameplay, networking, and world simulation enabled." : "Host configuration is missing, invalid, or only partially enabled."));

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
            moduleReady ? "host-ready-runtime-enabled" : "module-readiness-has-gaps",
            moduleReady ? "Registered modules and runtime rule profiles are enabled for live hosting." : "Registered modules are not ready for live hosting."));

        report.AddStage(new HostLifecycleStage(
            "runtime-enabled",
            !runtimeDisabled,
            !runtimeDisabled ? "enabled" : "disabled",
            !runtimeDisabled ? "Gameplay, networking, world simulation, and runtime rule profiles are enabled." : "Runtime paths remain disabled."));

        return report;
    }
}
