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
            var options = new HostLaunchOptions(Directory.GetCurrentDirectory());
            await host.StartAsync(options, CancellationToken.None);

            var duplicateRejected = false;
            try
            {
                await host.StartAsync(options, CancellationToken.None);
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
