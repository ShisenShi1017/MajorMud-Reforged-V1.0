namespace RealmGate.Server;

public sealed record RuntimeEconomyCommerceSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures);

public static class RuntimeEconomyCommerceSmoke
{
    public static RuntimeEconomyCommerceSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var currency = new RuntimeCurrencyMath(new RuntimeCurrencyDefinition("currency.test", "coin", new[] { new RuntimeCurrencyDenomination("coin", 1, 0) }, 1_000_000, false, 0));
        var items = new[] { new RuntimeEconomyItemDefinition("item.sword", 100, true, 99, 2, true, false, false) };
        var merchant = new RuntimeMerchantDefinition("merchant.test", "shop.test", "wallet.merchant", new RuntimeMerchantPricePolicy(12_500, 5_000, RuntimeCommerceRoundingMode.Ceiling, 1, 1), new[] { new RuntimeMerchantStockDefinition("item.sword", 10, 20, false, true, true) });

        Try("buy-order-and-price", () =>
        {
            var stock = new RuntimeMerchantStock(merchant);
            var buyer = new RuntimeCurrencyWallet("wallet.buyer", currency, new[] { new RuntimeCoinQuantity("coin", 1_000) });
            var merchantWallet = new RuntimeCurrencyWallet("wallet.merchant", currency);
            var inventory = new RuntimeEconomyInventory("inventory.buyer", 100, items);
            var result = RuntimeCommerceEngine.Buy("tx.buy", stock, buyer, merchantWallet, inventory, "item.sword", 2);
            if (result.Quote.UnitPriceBaseUnits != 125 || buyer.Capture().TotalBaseUnits != 750 || merchantWallet.Capture().TotalBaseUnits != 250 || inventory.Quantity("item.sword") != 2 || stock.Capture().Quantities["item.sword"] != 8) throw new InvalidOperationException("buy-state-mismatch");
        }, checks, failures);

        Try("sell-order-and-price", () =>
        {
            var stock = new RuntimeMerchantStock(merchant);
            var seller = new RuntimeCurrencyWallet("wallet.seller", currency);
            var merchantWallet = new RuntimeCurrencyWallet("wallet.merchant", currency, new[] { new RuntimeCoinQuantity("coin", 1_000) });
            var inventory = new RuntimeEconomyInventory("inventory.seller", 100, items, new[] { new KeyValuePair<string, long>("item.sword", 3) });
            var result = RuntimeCommerceEngine.Sell("tx.sell", stock, seller, merchantWallet, inventory, "item.sword", 2);
            if (result.Quote.UnitPriceBaseUnits != 50 || seller.Capture().TotalBaseUnits != 100 || merchantWallet.Capture().TotalBaseUnits != 900 || inventory.Quantity("item.sword") != 1 || stock.Capture().Quantities["item.sword"] != 12) throw new InvalidOperationException("sell-state-mismatch");
        }, checks, failures);

        Try("capacity-rollback", () =>
        {
            var stock = new RuntimeMerchantStock(merchant);
            var buyer = new RuntimeCurrencyWallet("wallet.capacity", currency, new[] { new RuntimeCoinQuantity("coin", 1_000) });
            var merchantWallet = new RuntimeCurrencyWallet("wallet.merchant", currency);
            var inventory = new RuntimeEconomyInventory("inventory.capacity", 1, items);
            var beforeBuyer = buyer.Capture();
            var beforeMerchant = merchantWallet.Capture();
            var beforeStock = stock.Capture();
            try { RuntimeCommerceEngine.Buy("tx.rollback", stock, buyer, merchantWallet, inventory, "item.sword", 1); throw new InvalidOperationException("expected-capacity-failure"); }
            catch (InvalidOperationException ex) when (ex.Message == "inventory-capacity-exceeded") { }
            if (buyer.Capture().TotalBaseUnits != beforeBuyer.TotalBaseUnits || merchantWallet.Capture().TotalBaseUnits != beforeMerchant.TotalBaseUnits || stock.Capture().Quantities["item.sword"] != beforeStock.Quantities["item.sword"] || inventory.Quantity("item.sword") != 0) throw new InvalidOperationException("rollback-state-mismatch");
        }, checks, failures);

        return new(failures.Count == 0, checks, failures);
    }

    private static void Try(string name, Action action, List<string> checks, List<string> failures)
    {
        try { action(); checks.Add(name); }
        catch (Exception ex) { failures.Add($"{name}:{ex.Message}"); }
    }
}
