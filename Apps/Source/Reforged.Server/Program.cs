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

var projectRoot = report.ProjectRoot!;

if (args.Any(arg => string.Equals(arg, "--audit-classic-native-messages", StringComparison.OrdinalIgnoreCase)))
{
    var nativeMessageReport = RuntimeClassicNativeMessageCoverageAudit.Run(projectRoot);
    Console.WriteLine("Classic Native Message Coverage Audit");
    Console.WriteLine(nativeMessageReport.ToJson());
    return nativeMessageReport.Passed ? 0 : 30;
}



if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-remaining", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run remaining Bundle D audit because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDRemainingExhaustionAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-final", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run final Bundle D exhaustion audit because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDFinalExhaustionAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_final_exhaustion_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.BundleDComplete ? 0 : 96;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-maps", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D maps because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDMapAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_map_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 95;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-traps", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D traps because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDTrapAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_trap_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 94;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-environment", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D environment because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDEnvironmentAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_environment_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 93;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-h-gang-houses", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle H gang-house sources because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleHGangHouseAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_h_gang_house_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 97;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-teleporters", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D teleporters because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDTeleporterAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_teleporter_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 92;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-hidden-objects", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D hidden objects because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDHiddenObjectAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_hidden_object_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 91;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-cleanup", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D cleanup because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDCleanupAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_cleanup_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 89;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-search", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D search because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDSearchAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_search_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 88;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-corrective", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D corrective exhaustion because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDCorrectiveExhaustionAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_corrective_exhaustion_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 90;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-doors", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D doors because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDDoorAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_door_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 87;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-exits", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D exits because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleDExitAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_exit_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 86;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-rooms", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D rooms because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var roomAudit = RuntimeBundleDRoomAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_room_audit.json"), roomAudit.ToJson());
    Console.WriteLine("Bundle D Room Audit");
    Console.WriteLine(roomAudit.ToJson());
    return roomAudit.Passed ? 0 : 82;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-d-world-authority", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle D world authority because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var authorityAudit = RuntimeBundleDWorldAuthorityAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_d_world_authority_audit.json"), authorityAudit.ToJson());
    Console.WriteLine("Bundle D World Authority Audit");
    Console.WriteLine(authorityAudit.ToJson());
    return authorityAudit.Passed ? 0 : 81;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-completion", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C completion because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var completionAudit = RuntimeBundleCCompletionAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_completion_audit.json"), completionAudit.ToJson());
    Console.WriteLine("Bundle C Completion Audit");
    Console.WriteLine(completionAudit.ToJson());
    return completionAudit.Passed ? 0 : 80;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-persistence", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C persistence because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var persistenceAudit = RuntimeBundleCPersistenceAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_persistence_audit.json"), persistenceAudit.ToJson());
    Console.WriteLine("Bundle C Persistence Audit");
    Console.WriteLine(persistenceAudit.ToJson());
    return persistenceAudit.Passed ? 0 : 79;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-loot", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C loot because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var lootAudit = RuntimeBundleCLootAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_loot_audit.json"), lootAudit.ToJson());
    Console.WriteLine("Bundle C Loot Audit");
    Console.WriteLine(lootAudit.ToJson());
    return lootAudit.Passed ? 0 : 78;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-economy", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C economy because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var economyAudit = RuntimeBundleCEconomyAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_economy_audit.json"), economyAudit.ToJson());
    Console.WriteLine("Bundle C Economy Audit");
    Console.WriteLine(economyAudit.ToJson());
    return economyAudit.Passed ? 0 : 77;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-merchants", StringComparison.OrdinalIgnoreCase)))
{
    var merchantAudit = RuntimeBundleCMerchantAudit.Run(report.ProjectRoot!, "Classic");
    Console.WriteLine("Bundle C Merchant Audit");
    Console.WriteLine(merchantAudit.ToJson());
    return merchantAudit.Passed ? 0 : 76;
}

if (args.Any(arg => string.Equals(arg, "--audit-classic-data", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Classic data because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var definition = options.Worlds.First(world => world.Id.Equals(options.DefaultWorldId, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine(ClassicRuntimeAudit.RenderCompilerOutputAudit(report.ProjectRoot, definition));
    return 0;
}

if (args.Any(arg => string.Equals(arg, "--audit-decode-exhaustion", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit decode exhaustion because project root was not resolved."); return 2; }
    var exhaustionReport = ClassicDecodeExhaustionAudit.Run(report.ProjectRoot);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "classic_decode_exhaustion_report.json"), ClassicDecodeExhaustionAudit.ToJson(exhaustionReport));
    File.WriteAllText(Path.Combine(outputRoot, "CLASSIC_DECODE_EXHAUSTION_REPORT.md"), ClassicDecodeExhaustionAudit.RenderMarkdown(exhaustionReport));
    Console.WriteLine(ClassicDecodeExhaustionAudit.RenderMarkdown(exhaustionReport));
    return exhaustionReport.Diagnostics.Count == 0 ? 0 : 31;
}

if (args.Any(arg => string.Equals(arg, "--audit-combat-bundle", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit combat bundle because project root was not resolved."); return 2; }
    var result = RuntimeCombatDecodeBundleAudit.Run(report.ProjectRoot);
    Console.WriteLine("Decode Exhaustion Bundle B Combat Audit");
    Console.WriteLine(RuntimeCombatDecodeBundleAudit.ToJson(result));
    return result.Status == "PASS" ? 0 : 41;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-equipment-authority", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C equipment authority because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleCEquipmentAuthorityAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_equipment_authority_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 42;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-equipment-restrictions", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C equipment restrictions because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleCEquipmentRestrictionAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_equipment_restriction_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 43;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-equipment-runtime", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C equipment runtime because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleCEquipmentRuntimeAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_equipment_runtime_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 44;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-items", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C items because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleCItemAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_item_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 45;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-c-inventory", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle C inventory because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeBundleCInventoryAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_c_inventory_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 46;
}

if (args.Any(arg => string.Equals(arg, "--audit-character-bundle", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit character bundle because project root was not resolved."); return 2; }
    var options = ServerRuntimeOptions.Load(report.ProjectRoot);
    var result = RuntimeCharacterDecodeBundleAudit.Run(report.ProjectRoot, options.DefaultWorldId);
    var outputRoot = Path.Combine(report.ProjectRoot, "Build", "CombinedUpdate");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "decode_exhaustion_bundle_a_revision_1.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 32;
}

if (args.Any(arg => string.Equals(arg, "--audit-classic-runtime", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Classic runtime because project root was not resolved."); return 2; }
    var (host, world, living) = CreateAuditRuntime(report.ProjectRoot);
    Console.WriteLine(ClassicRuntimeAudit.RenderRuntimeAudit(report.ProjectRoot, world, living));
    return 0;
}

if (TryReadArgumentValue(args, "--audit-room", out var auditRoom))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit a Classic room because project root was not resolved."); return 2; }
    var (host, world, living) = CreateAuditRuntime(report.ProjectRoot);
    Console.WriteLine(ClassicRuntimeAudit.RenderRoomInspection(report.ProjectRoot, world, living, auditRoom));
    return 0;
}

if (args.Any(arg => string.Equals(arg, "--audit-spawns", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Classic spawns because project root was not resolved."); return 2; }
    var (host, world, living) = CreateAuditRuntime(report.ProjectRoot);
    Console.WriteLine(ClassicRuntimeAudit.RenderSpawnVerification(world, living));
    return 0;
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









if (args.Any(arg => string.Equals(arg, "--smoke-npc-merchant-gold-settlement", StringComparison.OrdinalIgnoreCase)))
{
    var settlementReport = RuntimeNpcMerchantGoldSettlementSmokeReport.Run();
    Console.WriteLine("Runtime NPC Merchant Gold Settlement Smoke Report");
    Console.WriteLine(settlementReport.ToJson());
    return settlementReport.Passed ? 0 : 71;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-persistence-validation", StringComparison.OrdinalIgnoreCase)))
{
    var persistenceReport = RuntimeNpcPersistenceValidationSmokeReport.Run();
    Console.WriteLine("Runtime NPC Persistence Validation Smoke Report");
    Console.WriteLine(persistenceReport.ToJson());
    return persistenceReport.Passed ? 0 : 69;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-explicit-route", StringComparison.OrdinalIgnoreCase)))
{
    var routeReport = RuntimeNpcExplicitRouteSmokeReport.Run();
    Console.WriteLine("Runtime NPC Explicit Route Smoke Report");
    Console.WriteLine(routeReport.ToJson());
    return routeReport.Passed ? 0 : 68;
}





if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f15-persistence", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Bundle F Phase F15 persistence audit because project root was not resolved."); return 2; }
    var audit = RuntimeBundleFPhaseF15Audit.Run(report.ProjectRoot);
    Console.WriteLine("Bundle F Phase F15 Persistence and Duplicate Prevention Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 76;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f14-rewards", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Bundle F Phase F14 reward audit because project root was not resolved."); return 2; }
    var audit = RuntimeBundleFPhaseF14Audit.Run(report.ProjectRoot);
    Console.WriteLine("Bundle F Phase F14 Reward Execution Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 75;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f13-events-missions", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Bundle F Phase F13 event/mission audit because project root was not resolved."); return 2; }
    var audit = RuntimeBundleFPhaseF13Audit.Run(report.ProjectRoot);
    Console.WriteLine("Bundle F Phase F13 Event and Mission Runtime Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 74;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f12-random-tables", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Bundle F Phase F12 random-table audit because project root was not resolved."); return 2; }
    var audit = RuntimeBundleFPhaseF12Audit.Run(report.ProjectRoot);
    Console.WriteLine("Bundle F Phase F12 Random Table Runtime Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 73;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f10-complete", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run complete Bundle F Phase F10 audit because project root was not resolved."); return 2; }
    var audit = RuntimeBundleFPhaseF10CompleteAudit.Run(report.ProjectRoot);
    Console.WriteLine("Bundle F Phase F10 Complete NPC Dialogue Runtime Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 72;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f11-commands", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Bundle F Phase F11 command audit because project root was not resolved."); return 2; }
    var audit = RuntimeBundleFPhaseF11CommandAudit.Run(report.ProjectRoot);
    Console.WriteLine("Bundle F Phase F11 Command Runtime Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 71;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f10-native-payloads", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot audit Bundle F native textblock payloads because project root was not resolved."); return 2; }
    var failures = RuntimeBundleFPhaseF10NativePayloadAudit.Validate(report.ProjectRoot);
    var result = new { Passed = failures.Count == 0, PayloadCount = RuntimeClassicNativeTextblockPayloadAuthority.Load(report.ProjectRoot).Count, DisconnectCount = failures.Count, Failures = failures };
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return failures.Count == 0 ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f10-npc-dialogue-execution", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF10NpcDialogueExecutionAudit.Run(projectRoot);
    Console.WriteLine("Bundle F Phase F10 NPC Dialogue Conditional Execution Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 51;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f10-npc-dialogue", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF10NpcDialogueAudit.Run(projectRoot);
    Console.WriteLine("Bundle F Phase F10 NPC Dialogue Ownership and Routing Audit");
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 50;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f9-world-rewards", StringComparison.OrdinalIgnoreCase)))
{
    var phaseF9Report = RuntimeBundleFPhaseF9WorldRewardAudit.Run(projectRoot);
    Console.WriteLine("Bundle F Phase F9 World Reward Narrative Audit");
    Console.WriteLine(phaseF9Report.ToJson());
    return phaseF9Report.Passed ? 0 : 49;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f8-witchunter", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF8WitchunterAudit.Run(projectRoot);
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f7-scroll-turnins", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF7ScrollAudit.Run(projectRoot);
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f7-gnome-merchant", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF7GnomeMerchantAudit.Run(projectRoot);
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f6-alignment", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF6AlignmentAudit.Run(projectRoot);
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f5-condition-transactions", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF5ConditionTransactionAudit.Run();
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f5-eligibility", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF5EligibilityAudit.Run(projectRoot);
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f5-quest-roots", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF5QuestRootAudit.Run(projectRoot);
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f4-semantics", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF4SemanticAudit.Run(projectRoot);
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f4-live-bindings", StringComparison.OrdinalIgnoreCase)))
{
    var audit = RuntimeBundleFPhaseF4LiveBindingAudit.Run();
    Console.WriteLine(audit.ToJson());
    return audit.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f4-execution", StringComparison.OrdinalIgnoreCase)))
{
    var result = RuntimeBundleFPhaseF4ExecutionAudit.Run(projectRoot);
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f3-opcodes", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Bundle F Phase F3 opcode dispatch audit because project root was not resolved."); return 2; }
    var result = RuntimeBundleFPhaseF3OpcodeDispatchAudit.Run(report.ProjectRoot);
    var outputRoot = Path.Combine(report.ProjectRoot, "Reports", "BundleF");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_f_phase_f3_opcode_dispatch_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 83;
}

if (args.Any(arg => string.Equals(arg, "--audit-native-remoteactions", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run native remoteaction audit because project root was not resolved."); return 2; }
    var result = RuntimeNativeRemoteActionAudit.Run(report.ProjectRoot);
    var outputRoot = Path.Combine(report.ProjectRoot, "Reports", "RuntimeVerification");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "native_remoteaction_audit_0139.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 86;
}

if (args.Any(arg => string.Equals(arg, "--audit-classic-spell-effects", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Classic spell-effect audit because project root was not resolved."); return 2; }
    var spellEffectReport = RuntimeClassicSpellEffectCoverageAudit.Run(report.ProjectRoot);
    var spellEffectReportPath = Path.Combine(report.ProjectRoot, "Reports", "RuntimeVerification", "classic_spell_effect_coverage_0139.json");
    Directory.CreateDirectory(Path.GetDirectoryName(spellEffectReportPath)!);
    File.WriteAllText(spellEffectReportPath, spellEffectReport.ToJson());
    Console.WriteLine(spellEffectReport.ToJson());
    return spellEffectReport.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-classic-economy-services", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Classic economy/service audit because project root was not resolved."); return 2; }
    var economyReport=RuntimeClassicEconomyServiceCoverageAudit.Run(report.ProjectRoot);
    var economyReportPath=Path.Combine(report.ProjectRoot,"Reports","RuntimeVerification","classic_economy_service_coverage_0139.json");
    Directory.CreateDirectory(Path.GetDirectoryName(economyReportPath)!);
    File.WriteAllText(economyReportPath,economyReport.ToJson());
    Console.WriteLine(economyReport.ToJson());
    return economyReport.Passed?0:1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f2-textblocks", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot run Bundle F Phase F2 textblock audit because project root was not resolved."); return 2; }
    var result = RuntimeBundleFPhaseF2TextblockAudit.Run(report.ProjectRoot);
    var outputRoot = Path.Combine(report.ProjectRoot, "Reports", "BundleF");
    Directory.CreateDirectory(outputRoot);
    File.WriteAllText(Path.Combine(outputRoot, "bundle_f_phase_f2_textblock_audit.json"), result.ToJson());
    Console.WriteLine(result.ToJson());
    return result.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f2a-dynamic", StringComparison.OrdinalIgnoreCase)))
{
    var worldId = "Classic";
    var dynamicReport = RuntimeBundleFPhaseF2ADynamicAudit.Run(projectRoot, worldId);
    Console.WriteLine(dynamicReport.ToJson());
    return dynamicReport.Passed ? 0 : 84;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f2a-static", StringComparison.OrdinalIgnoreCase)))
{
    var worldId = "Classic";
    var staticDescriptionReport = RuntimeBundleFPhaseF2AStaticDescriptionAudit.Run(projectRoot, worldId);
    Console.WriteLine(staticDescriptionReport.ToJson());
    return staticDescriptionReport.Passed ? 0 : 82;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-f-phase-f1", StringComparison.OrdinalIgnoreCase)))
{
    var worldId = args.FirstOrDefault(arg => arg.StartsWith("--world=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1] ?? "Classic";
    var bundleF = RuntimeBundleFPhaseF1AuthorityAudit.Run(projectRoot, worldId);
    Console.WriteLine("Runtime Bundle F Phase F1 Authority Audit");
    Console.WriteLine(bundleF.ToJson());
    return bundleF.Passed ? 0 : 81;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-e-zero-disconnects", StringComparison.OrdinalIgnoreCase)))
{
    var worldId = args.FirstOrDefault(arg => arg.StartsWith("--world=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1] ?? "classic";
    var zeroDisconnect = RuntimeBundleEExhaustionAudit.Run(projectRoot, worldId);
    Console.WriteLine(zeroDisconnect.ToJson());
    return zeroDisconnect.BundleEComplete ? 0 : 1;
}
if (args.Any(arg => string.Equals(arg, "--audit-bundle-e-npc-item-control-persistence", StringComparison.OrdinalIgnoreCase)))
{
    var closure = RuntimeBundleENpcItemControlPersistenceClosureAudit.Run(projectRoot);
    Console.WriteLine("Runtime Bundle E NPC Item/Control/Persistence Closure Audit");
    Console.WriteLine(closure.ToJson());
    return closure.Passed ? 0 : 76;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-e-relationship-service", StringComparison.OrdinalIgnoreCase)))
{
    var relationshipService = RuntimeBundleERelationshipServiceClosureAudit.Run(projectRoot);
    Console.WriteLine("Runtime Bundle E Relationship/Service Closure Audit");
    Console.WriteLine(relationshipService.ToJson());
    return relationshipService.Passed ? 0 : 75;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-e-boss-death-respawn", StringComparison.OrdinalIgnoreCase)))
{
    var bossDeathRespawn = RuntimeBundleEBossDeathRespawnClosureAudit.Run(projectRoot);
    Console.WriteLine("Runtime Bundle E Boss/Death/Respawn Closure Audit");
    Console.WriteLine(bossDeathRespawn.ToJson());
    return bossDeathRespawn.Passed ? 0 : 74;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-e-merchant-trainer", StringComparison.OrdinalIgnoreCase)))
{
    var merchantTrainerClosure = RuntimeBundleEMerchantTrainerClosureAudit.Run(projectRoot);
    Console.WriteLine("Runtime Bundle E Merchant/Trainer Closure Audit");
    Console.WriteLine(merchantTrainerClosure.ToJson());
    return merchantTrainerClosure.Passed ? 0 : 73;
}

if (args.Any(arg => string.Equals(arg, "--audit-bundle-e-companion-lifecycle", StringComparison.OrdinalIgnoreCase)))
{
    var companionClosure = RuntimeBundleECompanionLifecycleClosureAudit.Run();
    Console.WriteLine("Runtime Bundle E Companion Lifecycle Closure Audit");
    Console.WriteLine(companionClosure.ToJson());
    return companionClosure.Passed ? 0 : 72;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-companion-command", StringComparison.OrdinalIgnoreCase)))
{
    var commandReport = RuntimeNpcCompanionCommandSmokeReport.Run();
    Console.WriteLine("Runtime NPC Companion Command Smoke Report");
    Console.WriteLine(commandReport.ToJson());
    return commandReport.Passed ? 0 : 67;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-companion-lifecycle-cleanup", StringComparison.OrdinalIgnoreCase)))
{
    var cleanupReport = RuntimeNpcCompanionLifecycleCleanupSmokeReport.Run();
    Console.WriteLine("Runtime NPC Companion Lifecycle Cleanup Smoke Report");
    Console.WriteLine(cleanupReport.ToJson());
    return cleanupReport.Passed ? 0 : 66;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-companion-combat-assist", StringComparison.OrdinalIgnoreCase)))
{
    var companionCombatAssistReport = RuntimeNpcCompanionCombatAssistSmokeReport.Run();
    Console.WriteLine(companionCombatAssistReport.ToJson());
    return companionCombatAssistReport.Passed ? 0 : 1;
}
if (args.Any(arg => string.Equals(arg, "--smoke-npc-companion-follow", StringComparison.OrdinalIgnoreCase)))
{
    var companionFollowReport = RuntimeNpcCompanionFollowSmokeReport.Run();
    Console.WriteLine("Runtime NPC Companion Follow Smoke Report");
    Console.WriteLine(companionFollowReport.ToJson());
    return companionFollowReport.Passed ? 0 : 64;
}


if (args.Any(arg => string.Equals(arg, "--smoke-npc-service-refusal-dialogue", StringComparison.OrdinalIgnoreCase)))
{
    var refusalReport = RuntimeNpcServiceRefusalDialogueSmokeReport.Run();
    Console.WriteLine("Runtime NPC Service Refusal Dialogue Smoke Report");
    Console.WriteLine(refusalReport.ToJson());
    return refusalReport.Passed ? 0 : 67;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-companion-ownership", StringComparison.OrdinalIgnoreCase)))
{
    var companionReport = RuntimeNpcCompanionOwnershipSmokeReport.Run();
    Console.WriteLine("Runtime NPC Companion Ownership Smoke Report");
    Console.WriteLine(companionReport.ToJson());
    return companionReport.Passed ? 0 : 63;
}


if (args.Any(arg => string.Equals(arg, "--smoke-npc-trainer-eligibility-admission", StringComparison.OrdinalIgnoreCase)))
{
    var trainerEligibilityReport = RuntimeNpcTrainerEligibilityAdmissionSmokeReport.Run();
    Console.WriteLine("Runtime NPC Trainer Eligibility Admission Smoke Report");
    Console.WriteLine(trainerEligibilityReport.ToJson());
    return trainerEligibilityReport.Passed ? 0 : 62;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-merchant-restock-admission", StringComparison.OrdinalIgnoreCase)))
{
    var merchantRestockReport = RuntimeNpcMerchantRestockAdmissionSmokeReport.Run();
    Console.WriteLine("Runtime NPC Merchant Restock Admission Smoke Report");
    Console.WriteLine(merchantRestockReport.ToJson());
    return merchantRestockReport.Passed ? 0 : 61;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-merchant-access-admission", StringComparison.OrdinalIgnoreCase)))
{
    var merchantAccessReport = RuntimeNpcMerchantAccessAdmissionSmokeReport.Run();
    Console.WriteLine("Runtime NPC Merchant Access Admission Smoke Report");
    Console.WriteLine(merchantAccessReport.ToJson());
    return merchantAccessReport.Passed ? 0 : 60;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-trainer-offering-admission", StringComparison.OrdinalIgnoreCase)))
{
    var trainerAdmissionReport = RuntimeNpcTrainerOfferingAdmissionSmokeReport.Run();
    Console.WriteLine("Runtime NPC Trainer Offering Admission Smoke Report");
    Console.WriteLine(trainerAdmissionReport.ToJson());
    return trainerAdmissionReport.Passed ? 0 : 59;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-trainer-offerings", StringComparison.OrdinalIgnoreCase)))
{
    var trainerOfferingReport = RuntimeNpcTrainerOfferingOwnershipSmokeReport.Run();
    Console.WriteLine("Runtime NPC Trainer Offering Ownership Smoke Report");
    Console.WriteLine(trainerOfferingReport.ToJson());
    return trainerOfferingReport.Passed ? 0 : 58;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-merchant-stock", StringComparison.OrdinalIgnoreCase)))
{
    var merchantStockReport = RuntimeNpcMerchantStockOwnershipSmokeReport.Run();
    Console.WriteLine("Runtime NPC Merchant Stock Ownership Smoke Report");
    Console.WriteLine(merchantStockReport.ToJson());
    return merchantStockReport.Passed ? 0 : 57;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-loot-materialization", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC loot materialization because project root was not resolved."); return 2; }
    var lootReport = RuntimeNpcLootMaterializationSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Loot Materialization Smoke Report");
    Console.WriteLine(lootReport.ToJson());
    return lootReport.Passed ? 0 : 56;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-equipment-selection", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC equipment selection because project root was not resolved."); return 2; }
    var selectionReport = RuntimeNpcEquipmentSelectionSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Equipment Selection Smoke Report");
    Console.WriteLine(selectionReport.ToJson());
    return selectionReport.Passed ? 0 : 55;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-item-effect", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC item-effect ownership because project root was not resolved."); return 2; }
    var itemEffectReport = RuntimeNpcItemEffectSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Item Effect Smoke Report");
    Console.WriteLine(itemEffectReport.ToJson());
    return itemEffectReport.Passed ? 0 : 54;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-ammunition", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC ammunition because project root was not resolved."); return 2; }
    var ammunitionReport = RuntimeNpcAmmunitionSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Ammunition Smoke Report");
    Console.WriteLine(ammunitionReport.ToJson());
    return ammunitionReport.Passed ? 0 : 53;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-item-use", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC item use because project root was not resolved."); return 2; }
    var itemUseReport = RuntimeNpcItemUseSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Item Use Smoke Report");
    Console.WriteLine(itemUseReport.ToJson());
    return itemUseReport.Passed ? 0 : 52;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-equipment-ownership", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC equipment ownership because project root was not resolved."); return 2; }
    var equipmentReport = RuntimeNpcEquipmentOwnershipSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Equipment Ownership Smoke Report");
    Console.WriteLine(equipmentReport.ToJson());
    return equipmentReport.Passed ? 0 : 51;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-spawn-inventory", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC spawn inventory because project root was not resolved."); return 2; }
    var spawnInventoryReport = RuntimeNpcSpawnInventorySmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Spawn Inventory Smoke Report");
    Console.WriteLine(spawnInventoryReport.ToJson());
    return spawnInventoryReport.Passed ? 0 : 50;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-inventory-ownership", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC inventory ownership because project root was not resolved."); return 2; }
    var inventoryReport = RuntimeNpcInventoryOwnershipSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Inventory Ownership Smoke Report");
    Console.WriteLine(inventoryReport.ToJson());
    return inventoryReport.Passed ? 0 : 47;
}


if (args.Any(arg => string.Equals(arg, "--smoke-npc-corpse-access", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC corpse access because project root was not resolved."); return 2; }
    var corpseAccessReport = RuntimeNpcCorpseAccessSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Corpse Access Smoke Report");
    Console.WriteLine(corpseAccessReport.ToJson());
    return corpseAccessReport.Passed ? 0 : 49;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-corpse-ownership", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC corpse ownership because project root was not resolved."); return 2; }
    var corpseReport = RuntimeNpcCorpseOwnershipSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Corpse Ownership Smoke Report");
    Console.WriteLine(corpseReport.ToJson());
    return corpseReport.Passed ? 0 : 48;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-administration", StringComparison.OrdinalIgnoreCase)))
{
    var administrationReport = RuntimeNpcAdministrationSmokeReport.Run();
    Console.WriteLine("Runtime NPC Administration Smoke Report");
    Console.WriteLine(administrationReport.ToJson());
    return administrationReport.Passed ? 0 : 45;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-inspection", StringComparison.OrdinalIgnoreCase)))
{
    if (report.ProjectRoot is null) { Console.Error.WriteLine("Cannot validate NPC inspection because project root was not resolved."); return 2; }
    var inspectionReport = RuntimeNpcInspectionSmokeReport.Run(report.ProjectRoot);
    Console.WriteLine("Runtime NPC Inspection Smoke Report");
    Console.WriteLine(inspectionReport.ToJson());
    return inspectionReport.Passed ? 0 : 44;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-return-home", StringComparison.OrdinalIgnoreCase)))
{
    var returnHomeReport = RuntimeNpcReturnHomeSmokeReport.Run();
    Console.WriteLine("Runtime NPC Return Home Smoke Report");
    Console.WriteLine(returnHomeReport.ToJson());
    return returnHomeReport.Passed ? 0 : 38;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-flee-movement", StringComparison.OrdinalIgnoreCase)))
{
    var fleeMovementReport = RuntimeNpcFleeMovementSmokeReport.Run();
    Console.WriteLine("Runtime NPC Flee Movement Smoke Report");
    Console.WriteLine(fleeMovementReport.ToJson());
    return fleeMovementReport.Passed ? 0 : 37;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-flee", StringComparison.OrdinalIgnoreCase)))
{
    var fleeReport = RuntimeNpcFleeSmokeReport.Run();
    Console.WriteLine("Runtime NPC Flee Smoke Report");
    Console.WriteLine(fleeReport.ToJson());
    return fleeReport.Passed ? 0 : 36;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-assist", StringComparison.OrdinalIgnoreCase)))
{
    var assistReport = RuntimeNpcAssistSmokeReport.Run();
    Console.WriteLine("Runtime NPC Assist Smoke Report");
    Console.WriteLine(assistReport.ToJson());
    return assistReport.Passed ? 0 : 35;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-threat-taunt", StringComparison.OrdinalIgnoreCase)))
{
    var threatTauntReport = RuntimeNpcThreatTauntSmokeReport.Run();
    Console.WriteLine("Runtime NPC Threat Taunt Smoke Report");
    Console.WriteLine(threatTauntReport.ToJson());
    return threatTauntReport.Passed ? 0 : 42;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-threat-memory", StringComparison.OrdinalIgnoreCase)))
{
    var threatMemoryReport = RuntimeNpcThreatMemorySmokeReport.Run();
    Console.WriteLine("Runtime NPC Threat Memory Smoke Report");
    Console.WriteLine(threatMemoryReport.ToJson());
    return threatMemoryReport.Passed ? 0 : 41;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-threat-decay", StringComparison.OrdinalIgnoreCase)))
{
    var threatDecayReport = RuntimeNpcThreatDecaySmokeReport.Run();
    Console.WriteLine("Runtime NPC Threat Decay Smoke Report");
    Console.WriteLine(threatDecayReport.ToJson());
    return threatDecayReport.Passed ? 0 : 40;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-threat-generation", StringComparison.OrdinalIgnoreCase)))
{
    var threatGenerationReport = RuntimeNpcThreatGenerationSmokeReport.Run();
    Console.WriteLine("Runtime NPC Threat Generation Smoke Report");
    Console.WriteLine(threatGenerationReport.ToJson());
    return threatGenerationReport.Passed ? 0 : 39;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-threat", StringComparison.OrdinalIgnoreCase)))
{
    var threatReport = RuntimeNpcThreatSmokeReport.Run();
    Console.WriteLine("Runtime NPC Threat Smoke Report");
    Console.WriteLine(threatReport.ToJson());
    return threatReport.Passed ? 0 : 34;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-perception", StringComparison.OrdinalIgnoreCase)))
{
    var perceptionReport = RuntimeNpcPerceptionSmokeReport.Run();
    Console.WriteLine("Runtime NPC Perception Smoke Report");
    Console.WriteLine(perceptionReport.ToJson());
    return perceptionReport.Passed ? 0 : 33;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-spawn-lifecycle", StringComparison.OrdinalIgnoreCase)))
{
    var lifecycleReport = RuntimeNpcSpawnLifecycleSmokeReport.InspectContract();
    Console.WriteLine("Runtime NPC Spawn Lifecycle Smoke Report");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(lifecycleReport, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return lifecycleReport.Passed ? 0 : 32;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-ai-scheduler", StringComparison.OrdinalIgnoreCase)))
{
    var npcAiSchedulerReport = RuntimeNpcAiSchedulerSmokeReport.Run();
    Console.WriteLine("Runtime NPC AI Scheduler Smoke Report");
    Console.WriteLine(npcAiSchedulerReport.ToJson());
    return npcAiSchedulerReport.Passed ? 0 : 31;
}

if (args.Any(arg => string.Equals(arg, "--smoke-npc-ai-decision", StringComparison.OrdinalIgnoreCase)))
{
    var npcAiReport = RuntimeNpcAiDecisionSmokeReport.Run();
    Console.WriteLine("Runtime NPC AI Decision Smoke Report");
    Console.WriteLine(npcAiReport.ToJson());
    return npcAiReport.Passed ? 0 : 30;
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
    Console.WriteLine(RenderStartupAudit(report.ProjectRoot, host, options));
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
    Console.WriteLine(RenderStartupAudit(report.ProjectRoot, host, options));
    await host.RunNetworkAsync(CancellationToken.None).ConfigureAwait(false);
    return 0;
}

// Combined cumulative integration: retained command surfaces from other completed subsystems.
if (args.Any(arg => string.Equals(arg, "--smoke-inventory-closure", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeRepositoryInventoryClosureSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-extension-boundaries", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeExtensionBoundariesSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-optional-activities", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeOptionalActivitiesSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-error-recovery", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeErrorRecoverySmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-capacity-limits", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeCapacityLimitsSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-character-identity-policy", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeCharacterIdentityPolicySmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-rankings-statistics", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeRankingsStatisticsSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-help-reference", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeHelpReferenceSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-import-export-compatibility", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeImportExportCompatibilitySmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-builder-content-policy", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeBuilderContentPolicySmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-weather-day-night", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeWeatherDayNightSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-global-world-state", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeGlobalWorldStateSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-global-random", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeGlobalRandomSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-worldgroup-integration", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeWorldgroupIntegrationSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-startup-shutdown", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeStartupShutdownSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-save-coordination", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeSaveCoordinationSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-logging-diagnostics", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeLoggingDiagnosticsSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-security-policy", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeSecurityPolicySmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-administrative-policy", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeAdministrativePolicySmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-majormud-plus", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeMajorMudPlusSmokeReport.Run(report.ProjectRoot!);
    Console.WriteLine("Runtime MajorMUD Plus Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 91;
}

if (args.Any(arg => string.Equals(arg, "--smoke-wcc-sysop", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeWccSysopSmokeReport.Run(report.ProjectRoot!);
    Console.WriteLine("Runtime WCCMMUD SYSOP Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 92;
}

if (args.Any(arg => string.Equals(arg, "--smoke-classic-administration", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeClassicAdministrationSmokeReport.Run();
    Console.WriteLine("Runtime Classic Administration Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 93;
}

if (args.Any(arg => string.Equals(arg, "--smoke-world-rules-configuration", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeWorldRulesConfigurationSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-session-ownership", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeSessionOwnershipSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-offline-messaging", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeOfflineMessagingSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-player-transactions", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimePlayerTransactionSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-transport-services", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeTransportServiceSmokeReport.Run();
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 1;
}

if (args.Any(arg => string.Equals(arg, "--smoke-rest-recovery-policy", StringComparison.OrdinalIgnoreCase)))
{
    var restReport = RuntimeRestRecoveryPolicySmokeReport.Run();
    Console.WriteLine("Runtime Rest Recovery Policy Smoke Report");
    Console.WriteLine(restReport.ToJson());
    return restReport.Passed ? 0 : 40;
}

if (args.Any(arg => string.Equals(arg, "--smoke-law-policy", StringComparison.OrdinalIgnoreCase)))
{
    var lawReport = RuntimeLawPolicySmokeReport.Run();
    Console.WriteLine("Runtime Law Policy Smoke Report");
    Console.WriteLine(lawReport.ToJson());
    return lawReport.Passed ? 0 : 39;
}

if (args.Any(arg => string.Equals(arg, "--smoke-pvp-policy", StringComparison.OrdinalIgnoreCase)))
{
    var pvpReport = RuntimePvpPolicySmokeReport.Run();
    Console.WriteLine("Runtime PvP Policy Smoke Report");
    Console.WriteLine(pvpReport.ToJson());
    return pvpReport.Passed ? 0 : 38;
}

if (args.Any(arg => string.Equals(arg, "--smoke-world-events", StringComparison.OrdinalIgnoreCase)))
{
    var eventReport = RuntimeWorldEventSmokeReport.Run();
    Console.WriteLine("Runtime World Event Smoke Report");
    Console.WriteLine(eventReport.ToJson());
    return eventReport.Passed ? 0 : 37;
}

if (args.Any(arg => string.Equals(arg, "--smoke-world-reset-cleanup", StringComparison.OrdinalIgnoreCase)))
{
    var resetReport = RuntimeWorldResetCleanupSmokeReport.Run();
    Console.WriteLine("Runtime World Reset Cleanup Smoke Report");
    Console.WriteLine(resetReport.ToJson());
    return resetReport.Passed ? 0 : 36;
}

if (args.Any(arg => string.Equals(arg, "--smoke-clock-scheduling", StringComparison.OrdinalIgnoreCase)))
{
    var schedulingReport = RuntimeClockSchedulingSmokeReport.Run();
    Console.WriteLine("Runtime Clock Scheduling Smoke Report");
    Console.WriteLine(schedulingReport.ToJson());
    return schedulingReport.Passed ? 0 : 35;
}

if (args.Any(arg => string.Equals(arg, "--smoke-environmental-hazards", StringComparison.OrdinalIgnoreCase)))
{
    var hazardReport = RuntimeEnvironmentalHazardSmokeReport.Run();
    Console.WriteLine("Runtime Environmental Hazard Smoke Report");
    Console.WriteLine(hazardReport.ToJson());
    return hazardReport.Passed ? 0 : 28;
}

if (args.Any(arg => string.Equals(arg, "--smoke-traps", StringComparison.OrdinalIgnoreCase)))
{
    var trapReport = RuntimeTrapSmokeReport.Run();
    Console.WriteLine("Runtime Trap Smoke Report");
    Console.WriteLine(trapReport.ToJson());
    return trapReport.Passed ? 0 : 27;
}

if (args.Any(arg => string.Equals(arg, "--smoke-door-access", StringComparison.OrdinalIgnoreCase)))
{
    var doorReport = RuntimeDoorAccessSmokeReport.Run();
    Console.WriteLine("Runtime Door Access Smoke Report");
    Console.WriteLine(doorReport.ToJson());
    return doorReport.Passed ? 0 : 32;
}

if (args.Any(arg => string.Equals(arg, "--smoke-organizations", StringComparison.OrdinalIgnoreCase)))
{
    var organizationReport = RuntimeOrganizationSmokeReport.Run();
    Console.WriteLine("Runtime Organization Smoke Report");
    Console.WriteLine(organizationReport.ToJson());
    return organizationReport.Passed ? 0 : 31;
}

if (args.Any(arg => string.Equals(arg, "--smoke-world-containers", StringComparison.OrdinalIgnoreCase)))
{
    var smoke = RuntimeWorldContainerSmokeReport.Run();
    Console.WriteLine("Runtime World Container Smoke Report");
    Console.WriteLine(smoke.ToJson());
    return smoke.Passed ? 0 : 36;
}

if (args.Any(arg => string.Equals(arg, "--smoke-communications", StringComparison.OrdinalIgnoreCase)))
{
    var communicationReport = RuntimeCommunicationSmokeReport.Run();
    Console.WriteLine("Runtime Communication Smoke Report");
    Console.WriteLine(communicationReport.ToJson());
    return communicationReport.Passed ? 0 : 33;
}

if (args.Any(arg => string.Equals(arg, "--smoke-party-coordination", StringComparison.OrdinalIgnoreCase)))
{
    var partyReport = RuntimePartyCoordinationSmokeReport.Run();
    Console.WriteLine("Runtime Party Coordination Smoke Report");
    Console.WriteLine(partyReport.ToJson());
    return partyReport.Passed ? 0 : 32;
}

Console.WriteLine("Server bootstrap completed successfully. Host runtime is enabled; pass --run-local for console or --run-network for telnet hosting.");
return report.ExitCode;

static (ServerRuntimeHost Host, RuntimeWorld World, RuntimeLivingWorldService Living) CreateAuditRuntime(string projectRoot)
{
    var options = ServerRuntimeOptions.Load(projectRoot);
    var host = new ServerRuntimeHost(projectRoot, options);
    host.Bootstrap();
    var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured)
        ? configured
        : host.Worlds.Values.First();
    var auditRoot = Path.Combine(Path.GetTempPath(), "reforged-classic-runtime-audit-" + Guid.NewGuid().ToString("N"));
    var living = new RuntimeLivingWorldService(projectRoot, world, auditRoot);
    return (host, world, living);
}

static string RenderStartupAudit(string projectRoot, ServerRuntimeHost host, ServerRuntimeOptions options)
{
    var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured)
        ? configured
        : host.Worlds.Values.First();
    var auditRoot = Path.Combine(Path.GetTempPath(), "reforged-classic-startup-audit-" + Guid.NewGuid().ToString("N"));
    var living = new RuntimeLivingWorldService(projectRoot, world, auditRoot);
    return ClassicRuntimeAudit.RenderRuntimeAudit(projectRoot, world, living);
}

static bool TryReadArgumentValue(string[] values, string name, out string value)
{
    value = string.Empty;
    for (var index = 0; index < values.Length; index++)
    {
        var current = values[index];
        if (current.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = current[(name.Length + 1)..].Trim().Trim('"');
            return !string.IsNullOrWhiteSpace(value);
        }

        if (current.Equals(name, StringComparison.OrdinalIgnoreCase) && index + 1 < values.Length)
        {
            value = values[index + 1].Trim().Trim('"');
            return !string.IsNullOrWhiteSpace(value);
        }
    }

    return false;
}
