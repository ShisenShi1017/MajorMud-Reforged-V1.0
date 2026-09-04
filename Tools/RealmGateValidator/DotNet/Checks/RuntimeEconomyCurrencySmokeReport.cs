namespace RealmGate.Server;

public sealed record RuntimeEconomyCurrencySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures);

public static class RuntimeEconomyCurrencySmoke
{
    public static RuntimeEconomyCurrencySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var definition = new RuntimeCurrencyDefinition(
            "currency.test",
            "coin.base",
            new[]
            {
                new RuntimeCurrencyDenomination("coin.high", 100, 0),
                new RuntimeCurrencyDenomination("coin.mid", 10, 1),
                new RuntimeCurrencyDenomination("coin.base", 1, 2)
            },
            10_000,
            true,
            0.01m);
        var math = new RuntimeCurrencyMath(definition);

        Try("checked-conversion", () =>
        {
            var total = math.ToBaseUnits(new[] { new RuntimeCoinQuantity("coin.high", 2), new RuntimeCoinQuantity("coin.mid", 3), new RuntimeCoinQuantity("coin.base", 4) });
            if (total != 234) throw new InvalidOperationException("unexpected-total");
        }, checks, failures);

        Try("canonical-change", () =>
        {
            var change = math.FromBaseUnits(234).ToDictionary(x => x.DenominationId, x => x.Quantity, StringComparer.OrdinalIgnoreCase);
            if (change["coin.high"] != 2 || change["coin.mid"] != 3 || change["coin.base"] != 4) throw new InvalidOperationException("unexpected-change");
        }, checks, failures);

        Try("atomic-transfer", () =>
        {
            var source = new RuntimeCurrencyWallet("wallet.source", math, new[] { new RuntimeCoinQuantity("coin.high", 5) });
            var destination = new RuntimeCurrencyWallet("wallet.destination", math);
            RuntimeCurrencyTransferEngine.Transfer(source, destination, 125, "transfer.test");
            if (source.Capture().TotalBaseUnits != 375 || destination.Capture().TotalBaseUnits != 125) throw new InvalidOperationException("transfer-total-mismatch");
        }, checks, failures);

        Try("overflow-rollback", () =>
        {
            var source = new RuntimeCurrencyWallet("wallet.rollback.source", math, new[] { new RuntimeCoinQuantity("coin.high", 1) });
            var destination = new RuntimeCurrencyWallet("wallet.rollback.destination", math, new[] { new RuntimeCoinQuantity("coin.high", 100) });
            var beforeSource = source.Capture();
            var beforeDestination = destination.Capture();
            try { RuntimeCurrencyTransferEngine.Transfer(source, destination, 1); throw new InvalidOperationException("expected-overflow"); }
            catch (OverflowException) { }
            if (source.Capture().TotalBaseUnits != beforeSource.TotalBaseUnits || destination.Capture().TotalBaseUnits != beforeDestination.TotalBaseUnits) throw new InvalidOperationException("rollback-mismatch");
        }, checks, failures);

        Try("persistence-roundtrip", () =>
        {
            var wallet = new RuntimeCurrencyWallet("wallet.persist", math, new[] { new RuntimeCoinQuantity("coin.mid", 7) });
            var json = RuntimeCurrencyPersistence.Serialize(new RuntimeCurrencyStoreSnapshot(new[] { wallet.Capture() }));
            var restored = RuntimeCurrencyPersistence.Deserialize(json);
            if (restored.Wallets.Count != 1 || restored.Wallets[0].TotalBaseUnits != 70) throw new InvalidOperationException("persistence-mismatch");
        }, checks, failures);

        return new(failures.Count == 0, checks, failures);
    }

    private static void Try(string name, Action action, List<string> checks, List<string> failures)
    {
        try { action(); checks.Add(name); }
        catch (Exception ex) { failures.Add($"{name}:{ex.Message}"); }
    }
}
