namespace Reforged.Server;

public sealed record RuntimeEconomyBankingSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures);

public static class RuntimeEconomyBankingSmoke
{
    public static RuntimeEconomyBankingSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var currency = new RuntimeCurrencyDefinition("currency.test", "coin.base", new[] { new RuntimeCurrencyDenomination("coin.base", 1, 0) }, 100_000, false, 0m);
        var math = new RuntimeCurrencyMath(currency);
        var policy = new RuntimeBankPolicy("bank.policy.test", "currency.test", 10, 10, 10_000, 5_000, 100, 200, 1, 1, RuntimeCommerceRoundingMode.Ceiling);

        Try("deposit-fee-and-ownership", () =>
        {
            var wallet = new RuntimeCurrencyWallet("wallet.owner", math, new[] { new RuntimeCoinQuantity("coin.base", 1_000) });
            var account = new RuntimeBankAccount("account.owner", "bank.test", policy, new[] { "character.owner" });
            var result = RuntimeBankingEngine.Deposit("bank.tx.deposit", "character.owner", wallet, account, 500);
            if (result.FeeBaseUnits != 5 || account.Capture().BalanceBaseUnits != 495 || wallet.Capture().TotalBaseUnits != 500) throw new InvalidOperationException("deposit-state-mismatch");
        }, checks, failures);

        Try("withdrawal-fee-and-order", () =>
        {
            var wallet = new RuntimeCurrencyWallet("wallet.withdraw", math);
            var account = new RuntimeBankAccount("account.withdraw", "bank.test", policy, new[] { "character.owner" }, 1_000);
            var result = RuntimeBankingEngine.Withdraw("bank.tx.withdraw", "character.owner", account, wallet, 500);
            if (result.FeeBaseUnits != 10 || account.Capture().BalanceBaseUnits != 490 || wallet.Capture().TotalBaseUnits != 500) throw new InvalidOperationException("withdrawal-state-mismatch");
        }, checks, failures);

        Try("shared-account-access", () =>
        {
            var account = new RuntimeBankAccount("account.shared", "bank.test", policy, new[] { "character.owner", "character.partner" }, 100);
            if (!account.CanAccess("character.partner")) throw new InvalidOperationException("shared-owner-missing");
            account.RemoveOwner("character.owner", "character.partner");
            if (account.CanAccess("character.partner")) throw new InvalidOperationException("shared-owner-remove-failed");
        }, checks, failures);

        Try("overflow-rollback", () =>
        {
            var wallet = new RuntimeCurrencyWallet("wallet.rollback", math, new[] { new RuntimeCoinQuantity("coin.base", 100) });
            var account = new RuntimeBankAccount("account.rollback", "bank.test", policy, new[] { "character.owner" }, 9_950);
            var walletBefore = wallet.Capture();
            var accountBefore = account.Capture();
            try { RuntimeBankingEngine.Deposit("bank.tx.rollback", "character.owner", wallet, account, 100); throw new InvalidOperationException("expected-overflow"); }
            catch (OverflowException) { }
            if (wallet.Capture().TotalBaseUnits != walletBefore.TotalBaseUnits || account.Capture().BalanceBaseUnits != accountBefore.BalanceBaseUnits) throw new InvalidOperationException("bank-rollback-mismatch");
        }, checks, failures);

        Try("idempotent-replay", () =>
        {
            var wallet = new RuntimeCurrencyWallet("wallet.replay", math, new[] { new RuntimeCoinQuantity("coin.base", 1_000) });
            var account = new RuntimeBankAccount("account.replay", "bank.test", policy, new[] { "character.owner" });
            RuntimeBankingEngine.Deposit("bank.tx.replay", "character.owner", wallet, account, 100);
            var repeated = RuntimeBankingEngine.Deposit("bank.tx.replay", "character.owner", wallet, account, 100);
            if (!repeated.Replayed || wallet.Capture().TotalBaseUnits != 900 || account.Capture().BalanceBaseUnits != 99) throw new InvalidOperationException("bank-replay-mutated-state");
        }, checks, failures);

        Try("persistence-roundtrip", () =>
        {
            var account = new RuntimeBankAccount("account.persist", "bank.test", policy, new[] { "character.owner", "character.partner" }, 750);
            var json = RuntimeBankPersistence.Serialize(new[] { account });
            var restored = RuntimeBankPersistence.Deserialize(json);
            if (restored.Accounts.Count != 1 || restored.Accounts[0].BalanceBaseUnits != 750 || restored.Accounts[0].OwnerIds.Count != 2) throw new InvalidOperationException("bank-persistence-mismatch");
        }, checks, failures);

        return new(failures.Count == 0, checks, failures);
    }

    private static void Try(string name, Action action, List<string> checks, List<string> failures)
    {
        try { action(); checks.Add(name); }
        catch (Exception ex) { failures.Add($"{name}:{ex.Message}"); }
    }
}
