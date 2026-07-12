using Reforged.Server;

var startDirectory = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
var bootstrap = new ServerBootstrap();
var report = bootstrap.Run(startDirectory ?? Directory.GetCurrentDirectory());

Console.WriteLine("Reforged Server Bootstrap");
Console.WriteLine($"ProjectRoot: {report.ProjectRoot ?? "<not found>"}");
Console.WriteLine($"Project: {report.ProjectName ?? "<unknown>"}");
Console.WriteLine($"CodeUpdate: {report.CodeUpdate ?? "<unknown>"}");
Console.WriteLine($"DatabaseVersion: {report.DatabaseVersion ?? "<unknown>"}");
Console.WriteLine($"Milestone: {report.Milestone ?? "<unknown>"}");
Console.WriteLine($"Checks: {string.Join(", ", report.Checks)}");
Console.WriteLine($"Readiness: {report.Readiness.Status}; gameplay={report.Readiness.GameplayStatus}");
Console.WriteLine($"RequiredDirectories: {report.PresentRequiredDirectoryCount}/{report.RequiredDirectoryCount} present, {report.MissingRequiredDirectoryCount} missing");
Console.WriteLine($"Validation: {report.IssueCount} issue(s), {report.InfoCount} info, {report.WarningCount} warning(s), {report.ErrorCount} error(s), {report.FatalCount} fatal");

foreach (var directory in report.RequiredDirectories)
{
    Console.WriteLine($"Directory: {(directory.Exists ? "present" : "missing")} {directory.Name} -> {directory.Path}");
}

if (report.HostConfiguration is not null)
{
    Console.WriteLine($"HostConfiguration: {report.HostConfiguration.Status}; name={report.HostConfiguration.HostName}; bind={report.HostConfiguration.BindAddress}; port={report.HostConfiguration.Port}; maxSessions={report.HostConfiguration.MaxSessions}; worldTickMs={report.HostConfiguration.WorldTickMilliseconds}; combatTickMs={report.HostConfiguration.CombatTickMilliseconds}; gameplayEnabled={report.HostConfiguration.GameplayEnabled}; networkingEnabled={report.HostConfiguration.NetworkingEnabled}; worldSimulationEnabled={report.HostConfiguration.WorldSimulationEnabled}; configFileExists={report.HostConfiguration.ConfigurationFileExists}");
}

if (report.ServerDataCatalog is not null)
{
    Console.WriteLine($"ServerDataCatalog: {report.ServerDataCatalog.Status}; categories={report.ServerDataCatalog.PresentCategoryCount}/{report.ServerDataCatalog.CategoryCount}; runtimeReady={report.ServerDataCatalog.RuntimeReadyCategoryCount}; runtimeBlocked={report.ServerDataCatalog.RuntimeBlockedCategoryCount}; placeholders={report.ServerDataCatalog.EmptyPlaceholderCategoryCount}; records={report.ServerDataCatalog.LoadedRecordCount}");
}

if (report.WorldProjection is not null)
{
    Console.WriteLine($"WorldProjection: {report.WorldProjection.Status}; worldRooms={report.WorldProjection.WorldRoomCount}; uniqueWorldRooms={report.WorldProjection.UniqueWorldRoomCount}; canonicalRooms={report.WorldProjection.CanonicalRoomCount}; missingRooms={report.WorldProjection.MissingRoomCount}; duplicateWorldRooms={report.WorldProjection.DuplicateWorldRoomCount}; startRoom={report.WorldProjection.StartRoom ?? "<unknown>"}; startRoomExists={report.WorldProjection.StartRoomExists}");
}

if (report.GameModuleCatalog is not null)
{
    Console.WriteLine($"GameModules: {report.GameModuleCatalog.Status}; modules={report.GameModuleCatalog.ModuleCount}; gameplayEnabled={report.GameModuleCatalog.GameplayEnabledModuleCount}; gameplayDisabled={report.GameModuleCatalog.GameplayDisabledModuleCount}");
}

if (report.ModuleReadiness is not null)
{
    Console.WriteLine($"ModuleReadiness: {report.ModuleReadiness.Status}; modules={report.ModuleReadiness.ModuleCount}; hostReady={report.ModuleReadiness.HostReadyModuleCount}; gameplayEnabled={report.ModuleReadiness.GameplayEnabledModuleCount}; runtimeRuleProfilesEnabled={report.ModuleReadiness.RuntimeRuleProfileEnabledCount}");
}

if (report.HostLifecycle is not null)
{
    Console.WriteLine($"HostLifecycle: {report.HostLifecycle.Status}; initialized={report.HostLifecycle.Initialized}; configured={report.HostLifecycle.Configured}; dataReady={report.HostLifecycle.DataReady}; worldProjectionReady={report.HostLifecycle.WorldProjectionReady}; moduleReady={report.HostLifecycle.ModuleReady}; runtimeDisabled={report.HostLifecycle.RuntimeDisabled}; gameplayEnabled={report.HostLifecycle.GameplayEnabled}; networkingEnabled={report.HostLifecycle.NetworkingEnabled}; worldSimulationEnabled={report.HostLifecycle.WorldSimulationEnabled}");
}

if (report.SchedulerBootstrap is not null)
{
    Console.WriteLine($"SchedulerBootstrap: {report.SchedulerBootstrap.Status}; channels={report.SchedulerBootstrap.ChannelCount}; runtimeEnabled={report.SchedulerBootstrap.RuntimeEnabledChannelCount}; runtimeDisabled={report.SchedulerBootstrap.RuntimeDisabledChannelCount}; hostLifecycleReady={report.SchedulerBootstrap.HostLifecycleReady}; diagnosticOnly={report.SchedulerBootstrap.DiagnosticOnly}");
}

if (report.SchedulerStatusCommands is not null)
{
    Console.WriteLine($"SchedulerStatusCommands: {report.SchedulerStatusCommands.Status}; commands={report.SchedulerStatusCommands.CommandCount}; runtimeEnabled={report.SchedulerStatusCommands.RuntimeEnabledCommandCount}; runtimeDisabled={report.SchedulerStatusCommands.RuntimeDisabledCommandCount}; guarded={report.SchedulerStatusCommands.GuardedCommandCount}; mutating={report.SchedulerStatusCommands.StateMutatingCommandCount}; schedulerReady={report.SchedulerStatusCommands.SchedulerBootstrapReady}; diagnosticOnly={report.SchedulerStatusCommands.DiagnosticOnly}");
}

if (report.ClassicParityAudit is not null)
{
    Console.WriteLine($"ClassicParity: {report.ClassicParityAudit.Status}; canonicalFiles={report.ClassicParityAudit.CanonicalFileCount}; importedReferenceFiles={report.ClassicParityAudit.ImportedReferenceFileCount}; exactNameMatches={report.ClassicParityAudit.ExactNameMatchCount}");
}

foreach (var issue in report.Validation.Issues)
{
    Console.Error.WriteLine($"{issue.Severity}: {issue.Code}: {issue.Message}");
}

if (!report.Passed)
{
    Console.Error.WriteLine("Server bootstrap failed before gameplay startup.");
    return report.ExitCode;
}


if (args.Any(arg => string.Equals(arg, "--smoke-world-systems-bundle", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate world systems because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var worldSystemsReport = RuntimeWorldSystemsBundleSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime World Systems Bundle Smoke Report");
    Console.WriteLine(worldSystemsReport.ToJson());
    return worldSystemsReport.Passed ? 0 : 29;
}

if (args.Any(arg => string.Equals(arg, "--smoke-living-world", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate living world because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var livingReport = RuntimeLivingWorldSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Living World Smoke Report");
    Console.WriteLine(livingReport.ToJson());
    return livingReport.Passed ? 0 : 18;
}

if (args.Any(arg => string.Equals(arg, "--smoke-live-powers", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate live powers because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var powersReport = RuntimeLivePowersSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Live Powers Smoke Report");
    Console.WriteLine(powersReport.ToJson());
    return powersReport.Passed ? 0 : 28;
}
if (args.Any(arg => string.Equals(arg, "--smoke-combat-completion", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate combat completion because project root was not resolved."); return 2; }
    var combatCompletionReport = RuntimeCombatCompletionSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime Combat Completion Smoke Report");
    Console.WriteLine(combatCompletionReport.ToJson());
    return combatCompletionReport.Passed ? 0 : 27;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-combat-round", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate live combat rounds because project root was not resolved."); return 2; }
    var liveCombatRoundReport = RuntimeLiveCombatRoundSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime Live Combat Round Smoke Report");
    Console.WriteLine(liveCombatRoundReport.ToJson());
    return liveCombatRoundReport.Passed ? 0 : 26;
}
if (args.Any(arg => string.Equals(arg, "--smoke-npc-defensive-profile", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC defensive profiles because project root was not resolved."); return 2; }
    var npcDefensiveProfileReport = RuntimeNpcDefensiveProfileSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Defensive Profile Smoke Report");
    Console.WriteLine(npcDefensiveProfileReport.ToJson());
    return npcDefensiveProfileReport.Passed ? 0 : 25;
}
if (args.Any(arg => string.Equals(arg, "--smoke-player-combat-profile", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate player combat profile because project root was not resolved."); return 2; }
    var playerCombatProfileReport = RuntimePlayerCombatProfileSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime Player Combat Profile Smoke Report");
    Console.WriteLine(playerCombatProfileReport.ToJson());
    return playerCombatProfileReport.Passed ? 0 : 24;
}
if (args.Any(arg => string.Equals(arg, "--smoke-combat-profile-promotion", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate combat profile promotion because project root was not resolved."); return 2; }
    var combatProfilePromotionReport = RuntimeCombatProfilePromotionSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime Combat Profile Promotion Smoke Report");
    Console.WriteLine(combatProfilePromotionReport.ToJson());
    return combatProfilePromotionReport.Passed ? 0 : 23;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-combat-admission", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate live combat admission because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var liveCombatReport = RuntimeLiveCombatSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Live Combat Admission Smoke Report");
    Console.WriteLine(liveCombatReport.ToJson());
    return liveCombatReport.Passed ? 0 : 22;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-gameplay-part6", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate live services because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var liveGameplayPart6Report = RuntimeLiveGameplayPart6SmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Live Gameplay Integration Part 6 Smoke Report");
    Console.WriteLine(liveGameplayPart6Report.ToJson());
    return liveGameplayPart6Report.Passed ? 0 : 21;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-gameplay-part5", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate live banking because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var liveGameplayPart5Report = RuntimeLiveGameplayPart5SmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Live Gameplay Integration Part 5 Smoke Report");
    Console.WriteLine(liveGameplayPart5Report.ToJson());
    return liveGameplayPart5Report.Passed ? 0 : 20;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-gameplay-part4", StringComparison.OrdinalIgnoreCase)))
{
    var liveGameplayPart4Report = RuntimeLiveGameplayPart4SmokeReport.Run();
    Console.WriteLine("Runtime Live Gameplay Integration Part 4 Smoke Report");
    Console.WriteLine(liveGameplayPart4Report.ToJson());
    return liveGameplayPart4Report.Passed ? 0 : 19;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-gameplay-part3", StringComparison.OrdinalIgnoreCase)))
{
    var liveGameplayPart3Report = RuntimeLiveGameplayPart3SmokeReport.Run();
    Console.WriteLine("Runtime Live Gameplay Integration Part 3 Smoke Report");
    Console.WriteLine(liveGameplayPart3Report.ToJson());
    return liveGameplayPart3Report.Passed ? 0 : 18;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-gameplay-part2", StringComparison.OrdinalIgnoreCase)))
{
    var liveGameplayPart2Report = RuntimeLiveGameplayPart2SmokeReport.Run();
    Console.WriteLine("Runtime Live Gameplay Integration Part 2 Smoke Report");
    Console.WriteLine(liveGameplayPart2Report.ToJson());
    return liveGameplayPart2Report.Passed ? 0 : 1;
}
if (args.Any(arg => string.Equals(arg, "--smoke-live-gameplay", StringComparison.OrdinalIgnoreCase)))
{
    var liveGameplayReport = RuntimeLiveGameplaySmokeReport.Run();
    Console.WriteLine("Runtime Live Gameplay Integration Smoke Report");
    Console.WriteLine(liveGameplayReport.ToJson());
    return liveGameplayReport.Passed ? 0 : 16;
}

if (args.Any(arg => string.Equals(arg, "--smoke-multiplayer-completion", StringComparison.OrdinalIgnoreCase)))
{
    var multiplayerCompletionReport = RuntimeMultiplayerCompletionSmokeReport.Run();
    Console.WriteLine("Runtime Multiplayer Completion Smoke Report");
    Console.WriteLine(multiplayerCompletionReport.ToJson());
    return multiplayerCompletionReport.Passed ? 0 : 24;
}

if (args.Any(arg => string.Equals(arg, "--smoke-world-multiplayer", StringComparison.OrdinalIgnoreCase)))
{
    var worldMultiplayerReport = RuntimeWorldMultiplayerSmokeReport.Run();
    Console.WriteLine("Runtime World and Multiplayer Smoke Report");
    Console.WriteLine(worldMultiplayerReport.ToJson());
    return worldMultiplayerReport.Passed ? 0 : 15;
}

if (args.Any(arg => string.Equals(arg, "--smoke-services-economy", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate services and economy because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var economyReport = RuntimeServicesEconomySmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Services and Economy Smoke Report");
    Console.WriteLine(economyReport.ToJson());
    return economyReport.Passed ? 0 : 14;
}

if (args.Any(arg => string.Equals(arg, "--smoke-interaction-narrative", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate interaction and narrative because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var narrativeReport = RuntimeInteractionNarrativeSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Interaction and Narrative Smoke Report");
    Console.WriteLine(narrativeReport.ToJson());
    return narrativeReport.Passed ? 0 : 13;
}

if (args.Any(arg => string.Equals(arg, "--smoke-actors-abilities", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate actors and abilities because project root was not resolved.");
        return 2;
    }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var actorsAbilitiesReport = RuntimeActorsAbilitiesSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Actors and Abilities Smoke Report");
    Console.WriteLine(actorsAbilitiesReport.ToJson());
    return actorsAbilitiesReport.Passed ? 0 : 12;
}

if (args.Any(arg => string.Equals(arg, "--smoke-combat", StringComparison.OrdinalIgnoreCase)))
{
    var combatSmoke = ClassicCombatSmokeReport.Run();
    Console.WriteLine("Classic Combat Smoke Report");
    Console.WriteLine(combatSmoke.ToJson());
    return combatSmoke.Passed ? 0 : 6;
}


if (args.Any(arg => string.Equals(arg, "--smoke-combat-runtime-part2", StringComparison.OrdinalIgnoreCase)))
{
    var combatPart2Report = RuntimeCombatPart2SmokeReport.Run();
    Console.WriteLine("Runtime Combat Part 2 Smoke Report");
    Console.WriteLine(combatPart2Report.ToJson());
    return combatPart2Report.Passed ? 0 : 1;
}
if (args.Any(arg => string.Equals(arg, "--smoke-combat-runtime", StringComparison.OrdinalIgnoreCase)))
{
    var combatRuntimeSmoke = RuntimeCombatSmokeReport.Run();
    Console.WriteLine("Runtime Combat Smoke Report");
    Console.WriteLine(combatRuntimeSmoke.ToJson());
    return combatRuntimeSmoke.Passed ? 0 : 11;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npcs", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate NPC loading because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var npcSmoke = host.CreateNpcSmokeReport();
    Console.WriteLine("Runtime NPC Smoke Report");
    Console.WriteLine(npcSmoke.ToJson());
    return npcSmoke.Passed ? 0 : 5;
}

if (args.Any(arg => string.Equals(arg, "--smoke-character-foundation", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate character foundation because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var selectedWorld)
        ? selectedWorld
        : host.Worlds.Values.First();
    var characterSmoke = RuntimeCharacterFoundationSmoke.Run(report.ProjectRoot, world);
    Console.WriteLine("Runtime Character Foundation Smoke Report");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(characterSmoke, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return characterSmoke.Passed ? 0 : 7;
}

if (args.Any(arg => string.Equals(arg, "--smoke-items", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--smoke-item-runtime", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate item runtime because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var smoke = RuntimeItemSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Item Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 8;
}

if (args.Any(arg => string.Equals(arg, "--smoke-spells", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--smoke-spell-runtime", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate spell runtime because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var smoke = RuntimeSpellSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Spell Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 9;
}

if (args.Any(arg => string.Equals(arg, "--smoke-spell-execution", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate spell execution because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var smoke = RuntimeSpellExecutionSmokeReport.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine("Runtime Spell Execution Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 10;
}

if (args.Any(arg => string.Equals(arg, "--smoke-room-exits", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate room exits because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var roomExitSmoke = host.CreateRoomExitSmokeReport();
    Console.WriteLine("Runtime Room/Exit Smoke Report");
    Console.WriteLine(roomExitSmoke.ToJson());
    return roomExitSmoke.Passed ? 0 : 4;
}

if (args.Any(arg => string.Equals(arg, "--smoke-generated-data", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--validate-generated-data", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot validate generated data because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    var smoke = host.CreateSmokeReport();
    Console.WriteLine("Generated World Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 3;
}

if (args.Any(arg => string.Equals(arg, "--smoke-host-enablement", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate host enablement because project root was not resolved."); return 2; }
    var smoke = RuntimeHostEnablementSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime Host Enablement Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 31;
}

if (args.Any(arg => string.Equals(arg, "--run-local", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot start runtime because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    await host.RunLocalConsoleAsync(CancellationToken.None).ConfigureAwait(false);
    return 0;
}

if (args.Any(arg => string.Equals(arg, "--run-network", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--run-telnet", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null)
    {
        Console.Error.WriteLine("Cannot start runtime because project root was not resolved.");
        return 2;
    }

    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var host = new ServerRuntimeHost(report.ProjectRoot, options);
    host.Bootstrap();
    await host.RunNetworkAsync(CancellationToken.None).ConfigureAwait(false);
    return 0;
}

Console.WriteLine("Server bootstrap completed successfully. Host runtime is enabled; pass --run-local for console or --run-network for telnet hosting.");
return report.ExitCode;
