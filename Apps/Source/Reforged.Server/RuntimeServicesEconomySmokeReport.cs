using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeServicesEconomySmokeResult(bool Passed, int ServiceCount, int EconomyRuleCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeServicesEconomySmokeReport
{
    public static RuntimeServicesEconomySmokeResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>(); var failures = new List<string>();
        var catalog = RuntimeServiceCatalog.Load(projectRoot, worldId);
        Try("catalog", () => { if (catalog.ServiceCount == 0 || catalog.EconomyRuleCount == 0) throw new InvalidOperationException(); }, checks, failures);
        var shop = catalog.Services.First(x => x.ServiceKind == "shop");
        var bank = catalog.Services.First(x => x.ServiceKind == "bank");
        var account = new RuntimeCurrencyAccount("actor.1", 1000, 500);
        var ledger = new RuntimeEconomyLedger(); var sink = new MemorySink();
        var engine = new RuntimeServiceTransactionEngine(catalog, new Policy(), sink, ledger);
        Try("shop-buy", () => { var result = engine.Execute(account, new("request.buy", shop.Id, account.AccountId, "buy", "item.test"), DateTimeOffset.UtcNow); if (!result.Succeeded || account.Carried != 900) throw new InvalidOperationException(); }, checks, failures);
        Try("shop-sell", () => { var result = engine.Execute(account, new("request.sell", shop.Id, account.AccountId, "sell", "item.test"), DateTimeOffset.UtcNow); if (!result.Succeeded || account.Carried != 950) throw new InvalidOperationException(); }, checks, failures);
        Try("trainer-contract", () => { var result = engine.Execute(account, new("request.train", shop.Id, account.AccountId, "train", "skill.test"), DateTimeOffset.UtcNow); if (!result.Succeeded) throw new InvalidOperationException(); }, checks, failures);
        Try("bank-deposit-withdraw", () => { account.Deposit(200); account.Withdraw(100); if (account.Carried != 850 || account.Banked != 600) throw new InvalidOperationException(); }, checks, failures);
        Try("ledger-persistence", () => { var snapshot = ledger.Capture(); var restored = new RuntimeEconomyLedger(); restored.Restore(snapshot); if (restored.Entries.Count != ledger.Entries.Count) throw new InvalidOperationException(); }, checks, failures);
        var trade = new RuntimeTradeSession("trade.1", "actor.1", "actor.2", DateTimeOffset.UtcNow);
        Try("trade-confirmation", () => { trade.SetOffer("actor.1", 10, new[] { "instance.1" }); trade.SetOffer("actor.2", 20, new[] { "instance.2" }); trade.Confirm("actor.1"); trade.Confirm("actor.2"); trade.MarkCompleted(); if (!trade.Capture().Completed) throw new InvalidOperationException(); }, checks, failures);
        Try("rollback", () => { var before = account.Capture(); var failing = new RuntimeServiceTransactionEngine(catalog, new Policy(), new FailingSink(), ledger); try { failing.Execute(account, new("request.fail", shop.Id, account.AccountId, "buy", "item.fail"), DateTimeOffset.UtcNow); throw new InvalidOperationException("expected-failure"); } catch (InvalidOperationException ex) when (ex.Message == "sink-failure") { } var after = account.Capture(); if (before.Carried != after.Carried || before.Banked != after.Banked) throw new InvalidOperationException("rollback-failed"); }, checks, failures);
        Try("validator", () => { var errors = RuntimeServicesEconomyValidator.Validate(catalog, new[] { account }, ledger, new[] { trade }); if (errors.Count != 0) throw new InvalidOperationException(string.Join(',', errors)); }, checks, failures);
        return new(failures.Count == 0, catalog.ServiceCount, catalog.EconomyRuleCount, checks, failures);
    }

    private static void Try(string name, Action action, List<string> checks, List<string> failures) { try { action(); checks.Add(name); } catch (Exception ex) { failures.Add($"{name}:{ex.Message}"); } }
    private sealed class Policy : IRuntimeShopServicePolicy, IRuntimeTrainerServicePolicy
    {
        public RuntimeServicePlan Resolve(RuntimeServiceDefinition service, RuntimeCurrencyAccountSnapshot actor, RuntimeServiceRequest request, DateTimeOffset nowUtc)
        {
            var delta = request.Operation.Equals("buy", StringComparison.OrdinalIgnoreCase) ? -100L : request.Operation.Equals("sell", StringComparison.OrdinalIgnoreCase) ? 50L : 0L;
            return new(true, null, delta, new[] { new RuntimeEconomyEvent(Guid.NewGuid().ToString("N"), request.Operation, actor.AccountId, null, service.Id, request.SubjectId, Math.Abs(delta), nowUtc) });
        }
    }
    private sealed class MemorySink : IRuntimeEconomyEventSink { public void Apply(RuntimeEconomyEvent economyEvent) { } public void Revert(RuntimeEconomyEvent economyEvent) { } }
    private sealed class FailingSink : IRuntimeEconomyEventSink { public void Apply(RuntimeEconomyEvent economyEvent) => throw new InvalidOperationException("sink-failure"); public void Revert(RuntimeEconomyEvent economyEvent) { } }
}
