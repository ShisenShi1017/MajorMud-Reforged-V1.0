using Reforged.App.Forge;

namespace Reforged.App.Brains;

public sealed record BorinCapabilityDescriptor(
    string ToolId,
    BorinAiIntent Intent,
    BorinRole Role,
    ForgePermissionMode MinimumPermission,
    string LocalAuthority,
    bool TestRealmOnly = false,
    bool RequiresApproval = false,
    bool ProductionMutation = false,
    bool HumanOnly = false);

public sealed record BorinCapabilityAvailability(
    BorinCapabilityDescriptor Capability,
    bool Available,
    string Reason);

public interface IBorinCapabilityCatalog
{
    IReadOnlyList<BorinCapabilityAvailability> Resolve(BorinRole roles, ForgeConfiguration configuration);
}

/// <summary>
/// Complete capability inventory for the three Borin roles. Entries name existing or
/// planned local authorities; this catalog grants no direct model access to any service.
/// </summary>
public sealed class BorinCapabilityCatalog : IBorinCapabilityCatalog
{
    private static readonly IReadOnlyList<BorinCapabilityDescriptor> All =
    [
        new("game.connect_login", BorinAiIntent.ConnectAndLogin, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch.ConnectAsync"),
        new("game.disconnect", BorinAiIntent.Disconnect, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch.DisconnectAsync"),
        new("game.look", BorinAiIntent.LookRoom, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch autonomous gateway"),
        new("game.move", BorinAiIntent.Move, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch autonomous gateway"),
        new("game.inventory", BorinAiIntent.Inventory, BorinRole.Player, ForgePermissionMode.Observe, "ClientSessionConnection parsed inventory"),
        new("game.equipment", BorinAiIntent.Equipment, BorinRole.Player, ForgePermissionMode.Observe, "ClientSessionConnection parsed equipment"),
        new("game.attack", BorinAiIntent.Attack, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch autonomous gateway"),
        new("game.cast", BorinAiIntent.Cast, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch autonomous gateway"),
        new("game.interact", BorinAiIntent.Interact, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch autonomous gateway"),
        new("game.shop", BorinAiIntent.Shop, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch autonomous gateway"),
        new("game.quest", BorinAiIntent.Quest, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch and MajorMUD mastery"),
        new("game.pathfind", BorinAiIntent.Pathfind, BorinRole.Player, ForgePermissionMode.Observe, "Client route and map services"),
        new("game.command", BorinAiIntent.GameCommand, BorinRole.Player, ForgePermissionMode.Observe, "IBorinPlayerResearch autonomous normal-game command gateway"),

        new("world.monitor", BorinAiIntent.MonitorWorld, BorinRole.Sysop, ForgePermissionMode.Observe, "Borin capability coordinator and runtime diagnostics"),
        new("world.get_room", BorinAiIntent.InspectRoom, BorinRole.Sysop, ForgePermissionMode.Observe, "ClassicAdministrationEditorService/runtime room inspection"),
        new("world.inspect_exit", BorinAiIntent.InspectExit, BorinRole.Sysop, ForgePermissionMode.Observe, "runtime room inspection"),
        new("world.inspect_monster", BorinAiIntent.InspectMonster, BorinRole.Sysop, ForgePermissionMode.Observe, "runtime NPC inspection"),
        new("world.inspect_item", BorinAiIntent.InspectItem, BorinRole.Sysop, ForgePermissionMode.Observe, "runtime item administration"),
        new("world.inspect_script", BorinAiIntent.InspectScript, BorinRole.Sysop, ForgePermissionMode.Observe, "Librarium/editor read services"),
        new("world.inspect_trigger", BorinAiIntent.InspectTrigger, BorinRole.Sysop, ForgePermissionMode.Observe, "Librarium/editor read services"),
        new("world.inspect_runtime", BorinAiIntent.InspectRuntime, BorinRole.Sysop, ForgePermissionMode.Observe, "Borin capability coordinator/runtime diagnostics"),
        new("world.report_issue", BorinAiIntent.ReportIssue, BorinRole.Sysop, ForgePermissionMode.Recommend, "Borin issue ledger"),
        new("editor.propose_change", BorinAiIntent.ProposeWorldChange, BorinRole.Sysop, ForgePermissionMode.Draft, "Worldsmith proposal service", TestRealmOnly: true),
        new("editor.apply_test_change", BorinAiIntent.ApplyTestWorldChange, BorinRole.Sysop, ForgePermissionMode.ApplyWithApproval, "Worldsmith approval/apply service", TestRealmOnly: true, RequiresApproval: true),
        new("world.reset_test_realm", BorinAiIntent.ResetTestRealm, BorinRole.Sysop, ForgePermissionMode.ApplyWithApproval, "RuntimeBuilderContentPolicy", TestRealmOnly: true, RequiresApproval: true),

        new("dev.search_code", BorinAiIntent.SearchCode, BorinRole.Developer, ForgePermissionMode.Observe, "IForgeRepositoryService.SearchAsync"),
        new("dev.read_file", BorinAiIntent.ReadFile, BorinRole.Developer, ForgePermissionMode.Observe, "IForgeRepositoryService bounded read context"),
        new("dev.read_directory", BorinAiIntent.ReadDirectory, BorinRole.Developer, ForgePermissionMode.Observe, "IForgeRepositoryService.ScanAsync"),
        new("dev.search_references", BorinAiIntent.SearchReferences, BorinRole.Developer, ForgePermissionMode.Observe, "IForgeRepositoryService.SearchAsync"),
        new("dev.read_build_errors", BorinAiIntent.ReadBuildErrors, BorinRole.Developer, ForgePermissionMode.Observe, "engineering reports/log service"),
        new("dev.read_runtime_logs", BorinAiIntent.ReadRuntimeLogs, BorinRole.Developer, ForgePermissionMode.Observe, "IForgeRepositoryService.ReadLatestLogsAsync"),
        new("dev.read_crash_logs", BorinAiIntent.ReadCrashLogs, BorinRole.Developer, ForgePermissionMode.Observe, "diagnostics/error reports"),
        new("dev.read_test_results", BorinAiIntent.ReadTestResults, BorinRole.Developer, ForgePermissionMode.Observe, "engineering reports"),
        new("dev.propose_patch", BorinAiIntent.ProposePatch, BorinRole.Developer, ForgePermissionMode.Draft, "Borin engineering workspace"),
        new("dev.preview_patch", BorinAiIntent.PreviewPatch, BorinRole.Developer, ForgePermissionMode.Draft, "IForgeDeveloperTools workspace diff"),
        new("dev.apply_patch", BorinAiIntent.ApplyPatch, BorinRole.Developer, ForgePermissionMode.Draft, "IForgeDeveloperTools isolated worktree"),
        new("dev.incorporate_patch", BorinAiIntent.IncorporatePatch, BorinRole.Developer, ForgePermissionMode.ApplyWithApproval, "IForgeDeveloperTools main apply UI", RequiresApproval: true, HumanOnly: true),
        new("dev.revert_patch", BorinAiIntent.RevertPatch, BorinRole.Developer, ForgePermissionMode.ApplyWithApproval, "IForgeDeveloperTools rollback", RequiresApproval: true, HumanOnly: true),
        new("dev.build", BorinAiIntent.Build, BorinRole.Developer, ForgePermissionMode.Validate, "IForgeDeveloperTools.BuildWorkspaceAsync"),
        new("dev.test", BorinAiIntent.Test, BorinRole.Developer, ForgePermissionMode.Validate, "IForgeDeveloperTools.TestWorkspaceAsync"),
        new("dev.run_specific_test", BorinAiIntent.RunSpecificTest, BorinRole.Developer, ForgePermissionMode.Validate, "guarded engineering command catalog"),
        new("dev.launch_test_server", BorinAiIntent.LaunchTestServer, BorinRole.Developer, ForgePermissionMode.Validate, "controlled Reforged host controller", TestRealmOnly: true),
        new("dev.stop_test_server", BorinAiIntent.StopTestServer, BorinRole.Developer, ForgePermissionMode.Validate, "controlled Reforged host controller", TestRealmOnly: true),
        new("dev.inspect_runtime", BorinAiIntent.InspectRuntime, BorinRole.Developer, ForgePermissionMode.Observe, "runtime diagnostics"),
        new("dev.compare_before_after", BorinAiIntent.CompareBeforeAfter, BorinRole.Developer, ForgePermissionMode.Validate, "engineering verification reports"),
        new("issue.create", BorinAiIntent.CreateIssue, BorinRole.Developer | BorinRole.Sysop, ForgePermissionMode.Recommend, "Borin issue ledger"),
        new("issue.update", BorinAiIntent.UpdateIssue, BorinRole.Developer | BorinRole.Sysop, ForgePermissionMode.Recommend, "Borin issue ledger"),
        new("report.repair", BorinAiIntent.GenerateRepairReport, BorinRole.Developer, ForgePermissionMode.Recommend, "engineering reports")
    ];

    public IReadOnlyList<BorinCapabilityAvailability> Resolve(BorinRole roles, ForgeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return All.Where(capability => (capability.Role & roles) != 0)
            .Select(capability => ResolveOne(capability, configuration))
            .ToArray();
    }

    private static BorinCapabilityAvailability ResolveOne(BorinCapabilityDescriptor capability, ForgeConfiguration configuration)
    {
        if (capability.Intent is BorinAiIntent.ProposeWorldChange or BorinAiIntent.ApplyTestWorldChange or BorinAiIntent.ResetTestRealm)
            return new(capability, false, "Worldsmith is installed but currently withheld by William.");
        if (configuration.PermissionMode < capability.MinimumPermission)
            return new(capability, false, $"Requires {capability.MinimumPermission} permission.");
        if (capability.HumanOnly)
            return new(capability, false, "Reserved for William's explicit local approval action.");
        if (capability.Intent == BorinAiIntent.ApplyPatch && !configuration.AllowWorkspaceEdits)
            return new(capability, false, "Workspace editing is disabled.");
        if (capability.Intent == BorinAiIntent.Build && !configuration.AllowBuildCommands)
            return new(capability, false, "Build commands are disabled.");
        if (capability.Intent is (BorinAiIntent.Test or BorinAiIntent.RunSpecificTest) && !configuration.AllowTestCommands)
            return new(capability, false, "Test commands are disabled.");
        if (capability.Intent is (BorinAiIntent.LaunchTestServer or BorinAiIntent.StopTestServer) && !configuration.AllowLocalProgramLaunch)
            return new(capability, false, "Local test-program launch is disabled.");
        if (capability.ProductionMutation)
            return new(capability, false, "Production mutation requires a separate explicit authorization path.");
        return new(capability, true, capability.RequiresApproval ? "Available through a local approval-gated authority." : "Available through the named local authority.");
    }
}
