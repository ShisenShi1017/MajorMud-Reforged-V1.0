using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeServiceDefinition(string Id, string ServiceKind, JsonElement GeneratedRecord);
public sealed record RuntimeEconomyRuleDefinition(string Id, JsonElement GeneratedRecord);

public sealed class RuntimeServiceCatalog
{
    private readonly Dictionary<string, RuntimeServiceDefinition> _services;
    private readonly Dictionary<string, RuntimeEconomyRuleDefinition> _rules;
    public RuntimeBundleCDecodedServiceAuthority BundleCAuthority { get; }

    private RuntimeServiceCatalog(IEnumerable<RuntimeServiceDefinition> services, IEnumerable<RuntimeEconomyRuleDefinition> rules, RuntimeBundleCDecodedServiceAuthority bundleCAuthority)
    {
        _services = services.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _rules = rules.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        BundleCAuthority = bundleCAuthority;
    }

    public int ServiceCount => _services.Count;
    public int EconomyRuleCount => _rules.Count;
    public IReadOnlyCollection<RuntimeServiceDefinition> Services => _services.Values;
    public bool TryGetService(string id, out RuntimeServiceDefinition definition) => _services.TryGetValue(id, out definition!);
    public bool TryGetRule(string id, out RuntimeEconomyRuleDefinition definition) => _rules.TryGetValue(id, out definition!);

    public static RuntimeServiceCatalog Load(string projectRoot, string worldId)
    {
        var resolved = ServerDataRootResolver.ResolveDefault(projectRoot);
        if (!resolved.WorldId.Equals(worldId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("requested-world-is-not-configured-default");

        var services = new List<RuntimeServiceDefinition>();
        services.AddRange(ReadFamily(Path.Combine(resolved.DataRoot, "shops.json"), "shop"));
        services.AddRange(ReadFamily(Path.Combine(resolved.DataRoot, "banks.json"), "bank"));
        var rules = ReadRules(Path.Combine(resolved.DataRoot, "economy_rules.json"));
        var bundleCAuthority = RuntimeBundleCDecodedServiceAuthority.Load(projectRoot, worldId);
        return new RuntimeServiceCatalog(services, rules, bundleCAuthority);
    }

    private static IEnumerable<RuntimeServiceDefinition> ReadFamily(string path, string kind)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = record.GetProperty("id").GetString();
            if (!string.IsNullOrWhiteSpace(id)) yield return new RuntimeServiceDefinition(id, kind, record.Clone());
        }
    }

    private static IEnumerable<RuntimeEconomyRuleDefinition> ReadRules(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var index = 0;
        foreach (var record in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = record.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
            yield return new RuntimeEconomyRuleDefinition(string.IsNullOrWhiteSpace(id) ? $"economy-rule.{index++}" : id, record.Clone());
        }
    }
}

public sealed record RuntimeCurrencyAccountSnapshot(string AccountId, long Carried, long Banked, IReadOnlyDictionary<string, string> Metadata);

public sealed class RuntimeCurrencyAccount
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCurrencyAccount(string accountId, long carried = 0, long banked = 0)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("account-id-required", nameof(accountId));
        if (carried < 0 || banked < 0) throw new ArgumentOutOfRangeException(nameof(carried));
        AccountId = accountId;
        Carried = carried;
        Banked = banked;
    }

    public string AccountId { get; }
    public long Carried { get; private set; }
    public long Banked { get; private set; }

    public RuntimeCurrencyAccountSnapshot Capture()
    {
        lock (_gate) return new(AccountId, Carried, Banked, new Dictionary<string, string>(_metadata, StringComparer.OrdinalIgnoreCase));
    }

    public void Restore(RuntimeCurrencyAccountSnapshot snapshot)
    {
        if (!AccountId.Equals(snapshot.AccountId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("currency-account-mismatch");
        if (snapshot.Carried < 0 || snapshot.Banked < 0) throw new InvalidOperationException("negative-currency-snapshot");
        lock (_gate)
        {
            Carried = snapshot.Carried;
            Banked = snapshot.Banked;
            _metadata.Clear();
            foreach (var pair in snapshot.Metadata) _metadata[pair.Key] = pair.Value;
        }
    }

    public void CreditCarried(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); lock (_gate) checked { Carried += amount; } }
    public void DebitCarried(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); lock (_gate) { if (Carried < amount) throw new InvalidOperationException("insufficient-carried-currency"); Carried -= amount; } }
    public void Deposit(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); lock (_gate) { if (Carried < amount) throw new InvalidOperationException("insufficient-carried-currency"); Carried -= amount; checked { Banked += amount; } } }
    public void Withdraw(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); lock (_gate) { if (Banked < amount) throw new InvalidOperationException("insufficient-banked-currency"); Banked -= amount; checked { Carried += amount; } } }
}

public sealed record RuntimeEconomyEvent(string EventId, string EventType, string AccountId, string? CounterpartyId, string? ServiceId, string? SubjectId, long Amount, DateTimeOffset OccurredAtUtc, IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record RuntimeServiceRequest(string RequestId, string ServiceId, string ActorId, string Operation, string? SubjectId, long Quantity = 1, IReadOnlyDictionary<string, string>? Arguments = null);
public sealed record RuntimeServicePlan(bool Allowed, string? FailureCode, long CurrencyDelta, IReadOnlyList<RuntimeEconomyEvent> Events);
public sealed record RuntimeServiceResult(bool Succeeded, string? FailureCode, IReadOnlyList<RuntimeEconomyEvent> Events);

public interface IRuntimeServicePolicy
{
    RuntimeServicePlan Resolve(RuntimeServiceDefinition service, RuntimeCurrencyAccountSnapshot actor, RuntimeServiceRequest request, DateTimeOffset nowUtc);
}

public interface IRuntimeEconomyEventSink
{
    void Apply(RuntimeEconomyEvent economyEvent);
    void Revert(RuntimeEconomyEvent economyEvent);
}

public interface IRuntimeTrainerServicePolicy : IRuntimeServicePolicy { }
public interface IRuntimeShopServicePolicy : IRuntimeServicePolicy { }
public interface IRuntimeRepairServicePolicy : IRuntimeServicePolicy { }

public sealed record RuntimeEconomyLedgerEntry(string TransactionId, string RequestId, string ActorId, string ServiceId, long CurrencyDelta, DateTimeOffset CommittedAtUtc, IReadOnlyList<RuntimeEconomyEvent> Events);
public sealed record RuntimeEconomyLedgerSnapshot(IReadOnlyList<RuntimeEconomyLedgerEntry> Entries);

public sealed class RuntimeEconomyLedger
{
    private readonly object _gate = new();
    private readonly List<RuntimeEconomyLedgerEntry> _entries = new();
    public IReadOnlyList<RuntimeEconomyLedgerEntry> Entries { get { lock (_gate) return _entries.ToArray(); } }
    public void Append(RuntimeEconomyLedgerEntry entry) { lock (_gate) { if (_entries.Any(x => x.TransactionId.Equals(entry.TransactionId, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("duplicate-transaction-id"); _entries.Add(entry); } }
    public RuntimeEconomyLedgerSnapshot Capture() { lock (_gate) return new(_entries.ToArray()); }
    public void Restore(RuntimeEconomyLedgerSnapshot snapshot) { lock (_gate) { _entries.Clear(); _entries.AddRange(snapshot.Entries); } }
}

public sealed class RuntimeServiceTransactionEngine
{
    private readonly RuntimeServiceCatalog _catalog;
    private readonly IRuntimeServicePolicy _policy;
    private readonly IRuntimeEconomyEventSink _sink;
    private readonly RuntimeEconomyLedger _ledger;

    public RuntimeServiceTransactionEngine(RuntimeServiceCatalog catalog, IRuntimeServicePolicy policy, IRuntimeEconomyEventSink sink, RuntimeEconomyLedger ledger)
    {
        _catalog = catalog; _policy = policy; _sink = sink; _ledger = ledger;
    }

    public RuntimeServiceResult Execute(RuntimeCurrencyAccount account, RuntimeServiceRequest request, DateTimeOffset nowUtc)
    {
        if (!account.AccountId.Equals(request.ActorId, StringComparison.OrdinalIgnoreCase)) return new(false, "actor-mismatch", Array.Empty<RuntimeEconomyEvent>());
        if (!_catalog.TryGetService(request.ServiceId, out var service)) return new(false, "unknown-service", Array.Empty<RuntimeEconomyEvent>());
        if (request.Quantity <= 0) return new(false, "invalid-quantity", Array.Empty<RuntimeEconomyEvent>());

        var accountBefore = account.Capture();
        var ledgerBefore = _ledger.Capture();
        var applied = new List<RuntimeEconomyEvent>();
        try
        {
            var plan = _policy.Resolve(service, accountBefore, request, nowUtc);
            if (!plan.Allowed) return new(false, plan.FailureCode, Array.Empty<RuntimeEconomyEvent>());
            if (plan.CurrencyDelta < 0) account.DebitCarried(checked(-plan.CurrencyDelta));
            else if (plan.CurrencyDelta > 0) account.CreditCarried(plan.CurrencyDelta);
            foreach (var economyEvent in plan.Events) { _sink.Apply(economyEvent); applied.Add(economyEvent); }
            _ledger.Append(new(Guid.NewGuid().ToString("N"), request.RequestId, request.ActorId, request.ServiceId, plan.CurrencyDelta, nowUtc, applied.ToArray()));
            return new(true, null, applied);
        }
        catch
        {
            for (var index = applied.Count - 1; index >= 0; index--) try { _sink.Revert(applied[index]); } catch { }
            account.Restore(accountBefore);
            _ledger.Restore(ledgerBefore);
            throw;
        }
    }
}

public sealed record RuntimeTradeOffer(string ParticipantId, long Currency, IReadOnlyList<string> ItemInstanceIds, bool Confirmed);
public sealed record RuntimeTradeSnapshot(string TradeId, RuntimeTradeOffer Left, RuntimeTradeOffer Right, bool Completed, DateTimeOffset CreatedAtUtc);
public interface IRuntimeTradePolicy { IReadOnlyList<RuntimeEconomyEvent> Resolve(RuntimeTradeSnapshot trade, DateTimeOffset nowUtc); }

public sealed class RuntimeTradeSession
{
    private readonly object _gate = new();
    public RuntimeTradeSession(string tradeId, string leftParticipantId, string rightParticipantId, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tradeId) || string.IsNullOrWhiteSpace(leftParticipantId) || string.IsNullOrWhiteSpace(rightParticipantId)) throw new ArgumentException("trade-identifiers-required");
        if (leftParticipantId.Equals(rightParticipantId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("trade-participants-must-differ");
        TradeId = tradeId; CreatedAtUtc = createdAtUtc;
        Left = new(leftParticipantId, 0, Array.Empty<string>(), false);
        Right = new(rightParticipantId, 0, Array.Empty<string>(), false);
    }
    public string TradeId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public RuntimeTradeOffer Left { get; private set; }
    public RuntimeTradeOffer Right { get; private set; }
    public bool Completed { get; private set; }
    public void SetOffer(string participantId, long currency, IReadOnlyList<string> itemInstanceIds)
    {
        if (currency < 0 || itemInstanceIds.Count != itemInstanceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()) throw new InvalidOperationException("invalid-trade-offer");
        lock (_gate) { RequireOpen(); if (Left.ParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase)) Left = new(participantId, currency, itemInstanceIds.ToArray(), false); else if (Right.ParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase)) Right = new(participantId, currency, itemInstanceIds.ToArray(), false); else throw new InvalidOperationException("not-trade-participant"); ResetOther(participantId); }
    }
    public void Confirm(string participantId) { lock (_gate) { RequireOpen(); if (Left.ParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase)) Left = Left with { Confirmed = true }; else if (Right.ParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase)) Right = Right with { Confirmed = true }; else throw new InvalidOperationException("not-trade-participant"); } }
    public RuntimeTradeSnapshot Capture() { lock (_gate) return new(TradeId, Left, Right, Completed, CreatedAtUtc); }
    public void Restore(RuntimeTradeSnapshot snapshot) { if (!TradeId.Equals(snapshot.TradeId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("trade-snapshot-mismatch"); lock (_gate) { Left = snapshot.Left; Right = snapshot.Right; Completed = snapshot.Completed; } }
    public void MarkCompleted() { lock (_gate) { if (!Left.Confirmed || !Right.Confirmed) throw new InvalidOperationException("trade-not-confirmed"); Completed = true; } }
    private void RequireOpen() { if (Completed) throw new InvalidOperationException("trade-completed"); }
    private void ResetOther(string participantId) { if (!Left.ParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase)) Left = Left with { Confirmed = false }; if (!Right.ParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase)) Right = Right with { Confirmed = false }; }
}

public static class RuntimeServicesEconomyValidator
{
    public static IReadOnlyList<string> Validate(RuntimeServiceCatalog catalog, IEnumerable<RuntimeCurrencyAccount> accounts, RuntimeEconomyLedger ledger, IEnumerable<RuntimeTradeSession> trades)
    {
        var errors = new List<string>();
        if (catalog.ServiceCount == 0) errors.Add("service-catalog-empty");
        if (catalog.EconomyRuleCount == 0) errors.Add("economy-rule-catalog-empty");
        var accountSnapshots = accounts.Select(x => x.Capture()).ToArray();
        if (accountSnapshots.Any(x => x.Carried < 0 || x.Banked < 0)) errors.Add("negative-account-balance");
        if (accountSnapshots.Select(x => x.AccountId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != accountSnapshots.Length) errors.Add("duplicate-account-id");
        var entries = ledger.Entries;
        if (entries.Select(x => x.TransactionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count) errors.Add("duplicate-transaction-id");
        foreach (var trade in trades.Select(x => x.Capture()))
        {
            if (trade.Left.ParticipantId.Equals(trade.Right.ParticipantId, StringComparison.OrdinalIgnoreCase)) errors.Add("invalid-trade-participants");
            if (trade.Left.Currency < 0 || trade.Right.Currency < 0) errors.Add("negative-trade-currency");
        }
        return errors;
    }
}
