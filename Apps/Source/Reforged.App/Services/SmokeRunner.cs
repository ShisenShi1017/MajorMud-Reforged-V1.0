using System.IO;
using Reforged.App.Models;

namespace Reforged.App.Services;

public static class SmokeRunner
{
    public static async Task<int?> TryRunAsync(
        string[] args,
        ServiceRegistry services)
    {
        if (HasArgument(args, "--smoke-shell"))
        {
            return await RunShellSmokeAsync(services);
        }

        if (HasArgument(args, "--smoke-settings-layout"))
        {
            return await RunSettingsAndLayoutSmokeAsync(services);
        }

        if (HasArgument(args, "--smoke-cleanup-schedule"))
        {
            return await RunCleanupScheduleSmokeAsync(services);
        }

        if (HasArgument(args, "--smoke-embedded-host"))
        {
            return await RunEmbeddedHostSmokeAsync(services);
        }

        if (HasArgument(args, "--smoke-client-automation"))
        {
            return await ClientAutomationRegressionSmoke.RunAsync(services.Get<IApplicationSettingsService>());
        }

        if (HasArgument(args, "--smoke-client-map-librarium"))
        {
            return await ClientMapLibrariumRegressionSmoke.RunAsync(services.Get<IApplicationSettingsService>());
        }

        if (HasArgument(args, "--smoke-client-batch3"))
        {
            return await ClientBatch3RegressionSmoke.RunAsync(services.Get<IApplicationSettingsService>());
        }

        if (HasArgument(args, "--smoke-client-inventory-automation"))
        {
            return await RealmGateInventoryAutomationRegressionSmoke.RunAsync(services.Get<IApplicationSettingsService>());
        }

        if (HasArgument(args, "--smoke-client-recovery-automation"))
        {
            return await ReforgedRecoveryAutomationRegressionSmoke.RunAsync(services.Get<IApplicationSettingsService>());
        }

        if (HasArgument(args, "--smoke-client-combat-automation"))
        {
            return await ReforgedCombatAutomationRegressionSmoke.RunAsync(services.Get<IApplicationSettingsService>());
        }

        if (HasArgument(args, "--smoke-client-party-automation"))
        {
            return ReforgedPartyAutomationRegressionSmoke.Run();
        }

        if (HasArgument(args, "--smoke-client-diagnostics"))
        {
            return ReforgedClientDiagnosticsRegressionSmoke.Run();
        }

        if (HasArgument(args, "--smoke-client-parity-completion"))
        {
            return await ReforgedParityCompletionRegressionSmoke.RunAsync();
        }

        if (HasArgument(args, "--smoke-client-help"))
        {
            return ClientHelpRegressionSmoke.Run();
        }

        return null;
    }

    private static bool HasArgument(string[] args, string expected)
    {
        return args.Contains(expected, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<int> RunShellSmokeAsync(ServiceRegistry services)
    {
        var manager = services.Get<IWorkspaceManager>();

        foreach (var id in new[]
                 {
                     "home",
                     "play",
                     "host",
                     "editor",
                     "librarium",
                     "characters",
                     "settings",
                 })
        {
            await manager.ActivateAsync(id);
        }

        return manager.ActiveWorkspace?.Id == "settings" ? 0 : 31;
    }

    private static async Task<int> RunSettingsAndLayoutSmokeAsync(
        ServiceRegistry services)
    {
        var settings = services.Get<IApplicationSettingsService>();
        var originalFontSize = settings.Current.FontSize;

        try
        {
            settings.Current.FontSize = originalFontSize + 1;
            await settings.SaveAsync();

            await services.Get<ILayoutPersistenceService>().SaveAsync(
                new LayoutState
                {
                    ActiveWorkspaceId = "home",
                    Width = 1200,
                    Height = 800,
                    Left = 100,
                    Top = 100,
                });

            var loaded = await services
                .Get<ILayoutPersistenceService>()
                .LoadAsync();

            return loaded.ActiveWorkspaceId == "home" && loaded.Width == 1200
                ? 0
                : 32;
        }
        finally
        {
            settings.Current.FontSize = originalFontSize;
            await settings.SaveAsync();
        }
    }

    private static async Task<int> RunCleanupScheduleSmokeAsync(
        ServiceRegistry services)
    {
        var cleanup = services.Get<IHostCleanupScheduler>();
        var settings = services.Get<IApplicationSettingsService>();
        var originalSchedule = settings.Current.HostCleanup;

        try
        {
            var probe = new HostCleanupSchedule
            {
                Enabled = true,
                CleanupTime = "23:59",
                TimeZoneId = TimeZoneInfo.Local.Id,
                RestartHostAfterCleanup = true,
            };

            await cleanup.UpdateScheduleAsync(probe, CancellationToken.None);

            return cleanup.NextCleanupAt is not null &&
                   cleanup.State == HostCleanupSchedulerState.Waiting
                ? 0
                : 35;
        }
        finally
        {
            settings.Current.HostCleanup = originalSchedule;
            await settings.SaveAsync();
        }
    }

    private static async Task<int> RunEmbeddedHostSmokeAsync(
        ServiceRegistry services)
    {
        var host = services.Get<IReforgedHostController>();

        try
        {
            // First verify the normal developer/repository launch path.
            var repositoryOptions = new HostLaunchOptions(Directory.GetCurrentDirectory());
            await host.StartAsync(repositoryOptions, CancellationToken.None);
            await host.StopAsync(CancellationToken.None);

            // Then verify the same root shape used by the actual WPF Host view.
            // AppContext.BaseDirectory points at bin/.../win-x64 during development,
            // so the controller must discover the real Reforged repository root.
            var appBaseOptions = new HostLaunchOptions(AppContext.BaseDirectory);
            await host.StartAsync(appBaseOptions, CancellationToken.None);

            var duplicateRejected = false;
            try
            {
                await host.StartAsync(appBaseOptions, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                duplicateRejected = true;
            }

            await host.StopAsync(CancellationToken.None);

            return duplicateRejected && host.State == HostControllerState.Stopped
                ? 0
                : 33;
        }
        catch (Exception exception)
        {
            services.Get<IAppLogger>().Error(
                "Embedded Host smoke failed",
                exception);

            return 34;
        }
        finally
        {
            if (host.State is HostControllerState.Running or
                HostControllerState.Starting or
                HostControllerState.Failed)
            {
                try
                {
                    await host.StopAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    services.Get<IAppLogger>().Error(
                        "Embedded Host smoke cleanup failed",
                        exception);
                }
            }
        }
    }
}
