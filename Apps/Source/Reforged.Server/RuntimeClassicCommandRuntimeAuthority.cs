using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleFCommandAuthorityRecord(
    string Name,
    IReadOnlyList<string> Aliases,
    int MinimumPrefixLength,
    bool Movement,
    string Family,
    string RuntimeOwner,
    string LiveConsumer,
    string ContextRestriction,
    string MutationPermission,
    string Evidence);

public sealed record RuntimeBundleFPhaseF11CommandAuditResult(
    bool Passed,
    int CommandCount,
    int AliasCount,
    int DispatchFamilyCount,
    int GeneratedEvidenceCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class RuntimeClassicCommandRuntimeAuthority
{
    private readonly IReadOnlyDictionary<string, RuntimeBundleFCommandAuthorityRecord> _records;

    private RuntimeClassicCommandRuntimeAuthority(IReadOnlyDictionary<string, RuntimeBundleFCommandAuthorityRecord> records) =>
        _records = records;

    public int Count => _records.Count;
    public IReadOnlyCollection<RuntimeBundleFCommandAuthorityRecord> Records => _records.Values.ToArray();

    public RuntimeBundleFCommandAuthorityRecord Require(string canonical) =>
        _records.TryGetValue(canonical, out var record)
            ? record
            : throw new InvalidOperationException($"Command authority is missing '{canonical}'.");

    public static RuntimeClassicCommandRuntimeAuthority Load(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("A project root is required.", nameof(projectRoot));

        var records = new Dictionary<string, RuntimeBundleFCommandAuthorityRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in RuntimeCommand.DefaultRegistrations)
        {
            var route = RuntimeCommandRuntimeRouteCatalog.Require(registration.Canonical);
            var record = new RuntimeBundleFCommandAuthorityRecord(
                registration.Canonical,
                registration.Aliases,
                registration.MinimumAbbreviationLength,
                registration.IsMovement,
                route.Kind.ToString(),
                route.RuntimeOwner,
                route.LiveConsumer,
                ContextRestriction(route.Kind),
                MutationPermission(route.Kind),
                "RuntimeCommandSystem.cs; RuntimeCommandRouting.cs; generated Classic command catalog");
            if (!records.TryAdd(registration.Canonical, record))
                throw new InvalidOperationException($"Duplicate command authority record '{registration.Canonical}'.");
        }

        return new RuntimeClassicCommandRuntimeAuthority(records);
    }

    private static string ContextRestriction(RuntimeCommandRouteKind kind) => kind switch
    {
        RuntimeCommandRouteKind.Movement => "current room and decoded exit gate",
        RuntimeCommandRouteKind.ItemRuntime => "persistent character and item location",
        RuntimeCommandRouteKind.CombatRuntime => "live target and combat admission",
        RuntimeCommandRouteKind.PowerRuntime => "known spell/ability and live target",
        RuntimeCommandRouteKind.NarrativeRuntime => "decoded NPC dialogue or quest journal",
        RuntimeCommandRouteKind.ServiceRuntime => "generated service in current room",
        RuntimeCommandRouteKind.BankRuntime => "generated bank service in current room",
        RuntimeCommandRouteKind.LivingWorldRuntime => "decoded object or exit in current room",
        RuntimeCommandRouteKind.SysopRuntime => "authenticated SYSOP/GAMEOP capability",
        RuntimeCommandRouteKind.SessionExit => "connected runtime session",
        _ => "connected runtime session"
    };

    private static string MutationPermission(RuntimeCommandRouteKind kind) => kind switch
    {
        RuntimeCommandRouteKind.RoomPresentation or RuntimeCommandRouteKind.CharacterState or
        RuntimeCommandRouteKind.RuntimeDiagnostics or RuntimeCommandRouteKind.Help => "read or presentation state only",
        RuntimeCommandRouteKind.CommunicationRuntime => "message queues only",
        RuntimeCommandRouteKind.SessionExit => "session lifecycle only",
        _ => "validated runtime mutation with subsystem rollback/fail-closed behavior"
    };
}

public static class RuntimeBundleFPhaseF11CommandAudit
{
    public static RuntimeBundleFPhaseF11CommandAuditResult Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var runtime = RuntimeCommand.DefaultRegistrations;
        var runtimeNames = runtime.Select(x => x.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (runtime.Count == 0 || runtimeNames.Count != runtime.Count)
            failures.Add("runtime-registration-catalog-invalid");
        else
            checks.Add($"runtime-registrations:{runtime.Count}");

        var authority = RuntimeClassicCommandRuntimeAuthority.Load(projectRoot);
        if (authority.Count != runtime.Count)
            failures.Add($"runtime-authority-count:{authority.Count}:{runtime.Count}");
        else
            checks.Add($"runtime-authority-covers-all:{authority.Count}");

        var routeNames = RuntimeCommandRuntimeRouteCatalog.Definitions
            .Select(definition => definition.Canonical)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var missing in runtimeNames.Except(routeNames, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            failures.Add("registered-command-route-missing:" + missing);
        foreach (var extra in routeNames.Except(runtimeNames, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            failures.Add("orphan-command-route:" + extra);
        if (!failures.Any(value => value.Contains("command-route", StringComparison.Ordinal)))
            checks.Add("all-registered-commands-have-runtime-routes");

        var policyKinds = new[]
        {
            RuntimeCommandRouteKind.CombatRuntime,
            RuntimeCommandRouteKind.PowerRuntime,
            RuntimeCommandRouteKind.NarrativeRuntime,
            RuntimeCommandRouteKind.ServiceRuntime,
            RuntimeCommandRouteKind.BankRuntime,
            RuntimeCommandRouteKind.RecoveryRuntime
        };
        foreach (var definition in RuntimeCommandRuntimeRouteCatalog.Definitions.Where(definition => policyKinds.Contains(definition.Kind)))
        {
            if (!RuntimeCommandDispatcher.RoutesToPolicy(definition.Canonical))
                failures.Add("policy-route-missing:" + definition.Canonical);
        }
        if (!failures.Any(value => value.StartsWith("policy-route-missing:", StringComparison.Ordinal)))
            checks.Add("all-policy-families-connected");

        var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in runtime)
        {
            foreach (var token in registration.Tokens)
            {
                if (!exact.TryAdd(token, registration.Canonical) &&
                    !exact[token].Equals(registration.Canonical, StringComparison.OrdinalIgnoreCase))
                    failures.Add("exact-token-conflict:" + token);

                var parsed = RuntimeCommand.Parse(token);
                if (!parsed.Name.Equals(registration.Canonical, StringComparison.OrdinalIgnoreCase) || parsed.ResolutionError.Length != 0)
                    failures.Add($"exact-token-route:{token}:{parsed.Name}:{parsed.ResolutionError}");
            }

            var canonical = RuntimeCommand.Parse(registration.Canonical);
            if (!canonical.Name.Equals(registration.Canonical, StringComparison.OrdinalIgnoreCase) || canonical.ResolutionError.Length != 0)
                failures.Add($"canonical-route:{registration.Canonical}:{canonical.Name}:{canonical.ResolutionError}");
        }
        if (!failures.Any(value => value.StartsWith("exact-token-", StringComparison.Ordinal) || value.StartsWith("canonical-route:", StringComparison.Ordinal)))
            checks.Add("all-canonical-and-alias-tokens-resolve");

        if (!RuntimeCommand.SupportedCommands.SetEquals(runtimeNames))
            failures.Add("supported-command-catalog-drift");
        else
            checks.Add("supported-command-catalog-is-registration-derived");

        var missingHelp = runtimeNames.Where(name => !RuntimeCommandDispatcher.HasHelpUsage(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missingHelp.Length > 0)
            failures.Add("command-help-usage-missing:" + string.Join(",", missingHelp));
        else if (RuntimeCommandDispatcher.HelpUsageCount != runtime.Count)
            failures.Add($"command-help-usage-count:{RuntimeCommandDispatcher.HelpUsageCount}:{runtime.Count}");
        else
            checks.Add("all-built-in-commands-have-help-usage");

        var generatedPath = Path.Combine(projectRoot, "Data", "Classic", "commands.json");
        var generated = RuntimeClassicCommandCatalog.Load(generatedPath);
        if (generated.Diagnostics.Count != 0)
            failures.AddRange(generated.Diagnostics.Select(value => "generated-command-catalog:" + value));
        else
            checks.Add("generated-command-catalog-loaded");

        if (generated.GeneratedEvidenceCount != 25 || generated.ActivatedGeneratedCommandCount != 25 || generated.QuarantinedGeneratedCommandCount != 0)
            failures.Add($"generated-command-activation:{generated.GeneratedEvidenceCount}:{generated.ActivatedGeneratedCommandCount}:{generated.QuarantinedGeneratedCommandCount}");
        else
            checks.Add("25-user-confirmed-contextual-room-action-verbs-active");

        var contextualNames = generated.Registrations
            .Where(registration => !runtimeNames.Contains(registration.Canonical))
            .Select(registration => registration.Canonical)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (generated.ActiveCommandCount != runtime.Count + contextualNames.Length)
            failures.Add($"active-command-count:{generated.ActiveCommandCount}:{runtime.Count + contextualNames.Length}");
        else
            checks.Add($"active-command-catalog:{generated.ActiveCommandCount}");

        foreach (var contextual in contextualNames)
        {
            var parsed = RuntimeCommand.Parse(contextual, generated);
            if (!parsed.Name.Equals(contextual, StringComparison.OrdinalIgnoreCase) || parsed.ResolutionError.Length != 0)
                failures.Add($"contextual-command-route:{contextual}:{parsed.Name}:{parsed.ResolutionError}");
        }
        if (!failures.Any(value => value.StartsWith("contextual-command-route:", StringComparison.Ordinal)))
            checks.Add($"contextual-commands-route-to-decoded-world-runtime:{contextualNames.Length}");

        if (typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TryExecuteDecodedWorldCommand)) is null)
            failures.Add("decoded-world-command-consumer-missing");
        else
            checks.Add("decoded-world-command-consumer-connected");

        if (RuntimeCommand.Parse("g", generated).Name != "get" || RuntimeCommand.Parse("go", generated).Name != "go")
            failures.Add("g-get-go-context-routing");
        else
            checks.Add("g-is-get-and-go-remains-contextual");

        foreach (var exitToken in new[] { "quit", "qui", "logout", "exit" })
        {
            if (RuntimeCommand.Parse(exitToken, generated).Name != "quit")
                failures.Add("session-exit-token:" + exitToken);
        }
        if (!failures.Any(value => value.StartsWith("session-exit-token:", StringComparison.Ordinal)))
            checks.Add("session-exit-aliases-use-command-parser");

        if (RuntimeCommand.Parse("eq longsword", generated).Name != "equipment" ||
            RuntimeCommand.Parse("ba target", generated).Name != "bash" ||
            RuntimeCommand.Parse("sys goto newhaven", generated).Name != "sysop")
            failures.Add("contextual-alias-routing");
        else
            checks.Add("contextual-alias-routing");

        var families = RuntimeCommandRuntimeRouteCatalog.Definitions
            .Select(definition => definition.Kind)
            .Distinct()
            .Count();
        checks.Add($"dispatch-families:{families}");

        return new RuntimeBundleFPhaseF11CommandAuditResult(
            failures.Count == 0,
            runtime.Count,
            exact.Count - runtime.Count,
            families,
            generated.GeneratedEvidenceCount,
            checks,
            failures);
    }
}
