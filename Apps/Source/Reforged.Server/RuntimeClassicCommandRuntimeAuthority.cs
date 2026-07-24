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
    int TextblockDirectInvocationCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class RuntimeClassicCommandRuntimeAuthority
{
    private readonly IReadOnlyDictionary<string, RuntimeBundleFCommandAuthorityRecord> _records;
    private RuntimeClassicCommandRuntimeAuthority(IReadOnlyDictionary<string, RuntimeBundleFCommandAuthorityRecord> records) => _records = records;
    public int Count => _records.Count;
    public IReadOnlyCollection<RuntimeBundleFCommandAuthorityRecord> Records => _records.Values.ToArray();
    public RuntimeBundleFCommandAuthorityRecord Require(string canonical) => _records.TryGetValue(canonical, out var record) ? record : throw new InvalidOperationException($"Command authority is missing '{canonical}'.");

    public static RuntimeClassicCommandRuntimeAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "command_runtime_authority.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Bundle F command runtime authority was not found.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "bundle_f_phase_f11_command_runtime_authority_v1") throw new InvalidOperationException("Unexpected Bundle F command authority schema.");
        var records = new Dictionary<string, RuntimeBundleFCommandAuthorityRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.GetProperty("records").EnumerateArray())
        {
            var name = element.GetProperty("name").GetString() ?? string.Empty;
            var aliases = element.GetProperty("aliases").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
            var record = new RuntimeBundleFCommandAuthorityRecord(name, aliases, element.GetProperty("minimum_prefix_length").GetInt32(), element.GetProperty("movement").GetBoolean(), element.GetProperty("family").GetString() ?? string.Empty, element.GetProperty("runtime_owner").GetString() ?? string.Empty, element.GetProperty("live_consumer").GetString() ?? string.Empty, element.GetProperty("context_restriction").GetString() ?? string.Empty, element.GetProperty("mutation_permission").GetString() ?? string.Empty, element.GetProperty("evidence").GetString() ?? string.Empty);
            if (!records.TryAdd(name, record)) throw new InvalidOperationException($"Duplicate command authority record '{name}'.");
        }
        if (records.Count != 83) throw new InvalidOperationException($"Expected 83 authoritative runtime commands, found {records.Count}.");
        return new RuntimeClassicCommandRuntimeAuthority(records);
    }
}

public static class RuntimeBundleFPhaseF11CommandAudit
{
    public static RuntimeBundleFPhaseF11CommandAuditResult Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        RuntimeClassicCommandRuntimeAuthority authority;
        try { authority = RuntimeClassicCommandRuntimeAuthority.Load(projectRoot); checks.Add("authority-loaded"); }
        catch (Exception ex) { return new(false,0,0,0,0,checks,new[]{ex.Message}); }
        var runtime = RuntimeCommand.DefaultRegistrations;
        if (runtime.Count != 83) failures.Add($"runtime-registration-count:{runtime.Count}"); else checks.Add("83-runtime-registrations");
        var runtimeNames = runtime.Select(x => x.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var authorityNames = authority.Records.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var missing in runtimeNames.Except(authorityNames, StringComparer.OrdinalIgnoreCase)) failures.Add("authority-missing:"+missing);
        foreach (var extra in authorityNames.Except(runtimeNames, StringComparer.OrdinalIgnoreCase)) failures.Add("authority-extra:"+extra);
        if (!failures.Any(x=>x.StartsWith("authority-",StringComparison.Ordinal))) checks.Add("runtime-authority-name-parity");
        var exact = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in runtime)
        {
            var record=authority.Require(registration.Canonical);
            if (record.RuntimeOwner.Length==0 || record.LiveConsumer.Length==0 || record.ContextRestriction.Length==0 || record.MutationPermission.Length==0) failures.Add("unowned:"+record.Name);
            if (record.MinimumPrefixLength!=registration.MinimumAbbreviationLength || record.Movement!=registration.IsMovement) failures.Add("registration-mismatch:"+record.Name);
            foreach(var token in registration.Tokens) if(!exact.TryAdd(token,registration.Canonical) && !exact[token].Equals(registration.Canonical,StringComparison.OrdinalIgnoreCase)) failures.Add("exact-token-conflict:"+token);
        }
        if (!failures.Any(x=>x.StartsWith("unowned:",StringComparison.Ordinal))) checks.Add("all-commands-have-live-consumers");
        if (!failures.Any(x=>x.StartsWith("registration-mismatch:",StringComparison.Ordinal))) checks.Add("aliases-prefixes-movement-match");
        if (!failures.Any(x=>x.StartsWith("exact-token-conflict:",StringComparison.Ordinal))) checks.Add("no-exact-alias-conflicts");
        foreach(var required in new[]{"search","experience","smash","backstab","hide","sneak"}) if(!authorityNames.Contains(required)) failures.Add("corrected-command-missing:"+required);
        if (!failures.Any(x=>x.StartsWith("corrected-command-missing:",StringComparison.Ordinal))) checks.Add("stale-77-command-gap-closed");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot,"Data","Classic","command_runtime_authority.json")));
        var invocations=doc.RootElement.GetProperty("textblock_direct_command_invocations").EnumerateArray().ToArray();
        foreach(var invocation in invocations)
        {
            var command=(invocation.GetProperty("command").GetString()??string.Empty).Replace("command.",string.Empty,StringComparison.OrdinalIgnoreCase);
            if(!runtimeNames.Contains(command)) failures.Add("textblock-command-unresolved:"+command);
        }
        if(!failures.Any(x=>x.StartsWith("textblock-command-unresolved:",StringComparison.Ordinal))) checks.Add("textblock-direct-commands-resolve");
        var families=authority.Records.Select(x=>x.Family).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if(families!=11) failures.Add($"dispatch-family-count:{families}"); else checks.Add("11-dispatch-families");
        return new(failures.Count==0,authority.Count,exact.Count-runtime.Count,families,invocations.Length,checks,failures);
    }
}
