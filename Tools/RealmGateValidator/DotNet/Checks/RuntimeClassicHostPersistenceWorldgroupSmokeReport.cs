using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicHostPersistenceWorldgroupSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicHostPersistenceWorldgroupSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var rngFailures = RuntimeClassicGlobalRngContractAuthority.Validate();
        var saveFailures = RuntimeClassicSaveCoordinationAuthority.Validate();
        var worldFailures = RuntimeClassicGlobalWorldStateAuthority.Validate();
        var wccFailures = RealmGate.Server.NativeWccArtifactExhaustionRegistry.Validate();

        checks.Add("rng-host-contract:" + (rngFailures.Count == 0));
        checks.Add("save-coordination:" + (saveFailures.Count == 0));
        checks.Add("global-world-state-host-boundary:" + (worldFailures.Count == 0));
        checks.Add("wcc-artifact-exhaustion:" + (wccFailures.Count == 0));
        checks.Add("exact-repository-inventory-closure:" + RuntimeClassicFinalNativeRepositoryClosureAuthority.CanClaimExactRepositoryInventoryClosure());
        checks.Add("player-save-interval-60:" + (RuntimeClassicSaveCoordinationAuthority.PlayerDataSaveIntervalSeconds == 60));
        checks.Add("buffer-save-interval-300:" + (RuntimeClassicSaveCoordinationAuthority.BufferDataSaveIntervalSeconds == 300));
        checks.Add("buffer-family-count-11:" + (RuntimeClassicSaveCoordinationAuthority.BufferFamilies.Count == 11));
        checks.Add("player-file-wccuser2:" + RuntimeClassicGlobalWorldStateAuthority.NativePersistentPlayerFiles.SequenceEqual(new[]{"WCCUSER2.DAT"}));
        checks.Add("no-wcc-selected-transaction-grouping:" + (RuntimeClassicSaveCoordinationAuthority.MajorMudDfaTransactionImports.Count == 0));
        checks.Add("crash-replay-excluded:" + (!RuntimeClassicSaveCoordinationAuthority.CrashReplayRecordFormatResolved));
        checks.Add("no-separate-majormud-replication:" + (!RuntimeClassicGlobalWorldStateAuthority.SeparateMajorMudReplicationProtocolExists));
        checks.Add("owner-local-world-state-remains-delegated:" + (!RuntimeClassicGlobalWorldStateAuthority.RemainingBitMaskCounterLatchMeaningsResolved));
        checks.Add("owner-local-repository-semantics-remain-delegated:" + (!RuntimeClassicFinalNativeRepositoryClosureAuthority.EveryOwnerLocalFormulaExact));

        try
        {
            var ledgerPath = Path.Combine(projectRoot, "Librarium", "Parity", "majormud_parity_assertions.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(ledgerPath));
            var assertions = doc.RootElement.GetProperty("assertions").EnumerateArray().ToArray();
            var host = assertions.Where(x => string.Equals(x.GetProperty("category").GetString(), "Host / Persistence / Worldgroup", StringComparison.Ordinal)).ToArray();
            var pass = host.Count(x => string.Equals(x.GetProperty("state").GetString(), "PASS", StringComparison.Ordinal));
            var strong = host.Count(x => string.Equals(x.GetProperty("state").GetString(), "STRONG", StringComparison.Ordinal));
            var unknown = host.Count(x => string.Equals(x.GetProperty("state").GetString(), "UNKNOWN", StringComparison.Ordinal));
            var allPass = assertions.Count(x => string.Equals(x.GetProperty("state").GetString(), "PASS", StringComparison.Ordinal));
            var allStrong = assertions.Count(x => string.Equals(x.GetProperty("state").GetString(), "STRONG", StringComparison.Ordinal));
            var allUnknown = assertions.Count(x => string.Equals(x.GetProperty("state").GetString(), "UNKNOWN", StringComparison.Ordinal));
            checks.Add("host-assertion-count-9:" + (host.Length == 9));
            checks.Add("host-pass-count-9:" + (pass == 9));
            checks.Add("host-strong-zero:" + (strong == 0));
            checks.Add("host-unknown-zero:" + (unknown == 0));
            checks.Add("overall-pass-200:" + (allPass == 200));
            checks.Add("overall-strong-zero:" + (allStrong == 0));
            checks.Add("overall-unknown-zero:" + (allUnknown == 0));
        }
        catch (Exception ex)
        {
            checks.Add("parity-ledger-readable:False:" + ex.GetType().Name);
        }

        foreach (var failure in rngFailures) checks.Add("rng-detail-" + failure + ":False");
        foreach (var failure in saveFailures) checks.Add("save-detail-" + failure + ":False");
        foreach (var failure in worldFailures) checks.Add("world-detail-" + failure + ":False");
        foreach (var failure in wccFailures) checks.Add("wcc-detail-" + failure + ":False");

        var passed = checks.All(x => x.EndsWith(":True", StringComparison.OrdinalIgnoreCase));
        return new RuntimeClassicHostPersistenceWorldgroupSmokeReport(passed, checks);
    }
}
