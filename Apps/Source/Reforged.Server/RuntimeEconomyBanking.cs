using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeBankTransactionKind
{
    Deposit,
    Withdrawal,
    Transfer
}

public sealed record RuntimeBankPolicy(
    string PolicyId,
    string CurrencyId,
    long MinimumDepositBaseUnits,
    long MinimumWithdrawalBaseUnits,
    long? MaximumAccountBaseUnits,
    long? MaximumTransactionBaseUnits,
    int DepositFeeBasisPoints,
    int WithdrawalFeeBasisPoints,
    long MinimumDepositFeeBaseUnits,
    long MinimumWithdrawalFeeBaseUnits,
    RuntimeCommerceRoundingMode FeeRoundingMode);

public sealed record RuntimeBankAccountSnapshot(
    string AccountId,
    string BankId,
    string CurrencyId,
    IReadOnlyList<string> OwnerIds,
    long BalanceBaseUnits,
    long Version,
    IReadOnlyList<string> AppliedTransactionIds);

public sealed record RuntimeBankTransactionResult(
    string TransactionId,
    RuntimeBankTransactionKind Kind,
    string BankId,
    string SourceId,
    string DestinationId,
    long RequestedBaseUnits,
    long FeeBaseUnits,
    long NetBaseUnits,
    long SourceVersion,
    long DestinationVersion,
    bool Replayed);

public sealed class RuntimeBankAccount
{
    private readonly object _gate = new();
    private readonly RuntimeBankPolicy _policy;
    private readonly HashSet<string> _owners;
    private readonly HashSet<string> _appliedTransactionIds;
    private long _balanceBaseUnits;
    private long _version;

    public RuntimeBankAccount(string accountId, string bankId, RuntimeBankPolicy policy, IEnumerable<string> ownerIds, long initialBalanceBaseUnits = 0)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("bank-account-id-required", nameof(accountId));
        if (string.IsNullOrWhiteSpace(bankId)) throw new ArgumentException("bank-id-required", nameof(bankId));
        ValidatePolicy(policy);
        AccountId = accountId;
        BankId = bankId;
        _policy = policy;
        _owners = ownerIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_owners.Count == 0) throw new InvalidOperationException("bank-account-owner-required");
        _appliedTransactionIds = new(StringComparer.OrdinalIgnoreCase);
        EnsureBalanceAllowed(initialBalanceBaseUnits);
        _balanceBaseUnits = initialBalanceBaseUnits;
    }

    public string AccountId { get; }
    public string BankId { get; }
    public string CurrencyId => _policy.CurrencyId;
    public RuntimeBankPolicy Policy => _policy;
    internal object Gate => _gate;

    public RuntimeBankAccountSnapshot Capture()
    {
        lock (_gate) return CaptureUnsafe();
    }

    public bool CanAccess(string ownerId)
    {
        lock (_gate) return _owners.Contains(ownerId);
    }

    public void AddOwner(string actingOwnerId, string newOwnerId)
    {
        if (string.IsNullOrWhiteSpace(newOwnerId)) throw new InvalidOperationException("bank-owner-id-required");
        lock (_gate)
        {
            EnsureOwnerUnsafe(actingOwnerId);
            if (_owners.Add(newOwnerId)) _version = checked(_version + 1);
        }
    }

    public void RemoveOwner(string actingOwnerId, string ownerId)
    {
        lock (_gate)
        {
            EnsureOwnerUnsafe(actingOwnerId);
            if (!_owners.Contains(ownerId)) return;
            if (_owners.Count == 1) throw new InvalidOperationException("bank-account-must-retain-owner");
            _owners.Remove(ownerId);
            _version = checked(_version + 1);
        }
    }

    internal RuntimeBankAccountSnapshot CaptureUnsafe() => new(
        AccountId,
        BankId,
        CurrencyId,
        _owners.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
        _balanceBaseUnits,
        _version,
        _appliedTransactionIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());

    internal void RestoreUnsafe(RuntimeBankAccountSnapshot snapshot)
    {
        _owners.Clear();
        foreach (var owner in snapshot.OwnerIds) _owners.Add(owner);
        _appliedTransactionIds.Clear();
        foreach (var transactionId in snapshot.AppliedTransactionIds) _appliedTransactionIds.Add(transactionId);
        _balanceBaseUnits = snapshot.BalanceBaseUnits;
        _version = snapshot.Version;
    }

    internal void EnsureOwnerUnsafe(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !_owners.Contains(ownerId)) throw new UnauthorizedAccessException("bank-account-access-denied");
    }

    internal bool IsAppliedUnsafe(string transactionId) => _appliedTransactionIds.Contains(transactionId);
    internal void MarkAppliedUnsafe(string transactionId) => _appliedTransactionIds.Add(transactionId);
    internal long BalanceUnsafe => _balanceBaseUnits;
    internal long VersionUnsafe => _version;

    internal void SetBalanceUnsafe(long balanceBaseUnits)
    {
        EnsureBalanceAllowed(balanceBaseUnits);
        _balanceBaseUnits = balanceBaseUnits;
        _version = checked(_version + 1);
    }

    private void EnsureBalanceAllowed(long balanceBaseUnits)
    {
        if (balanceBaseUnits < 0) throw new InvalidOperationException("negative-bank-balance");
        if (_policy.MaximumAccountBaseUnits is { } maximum && balanceBaseUnits > maximum) throw new OverflowException("bank-account-limit-exceeded");
    }

    private static void ValidatePolicy(RuntimeBankPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.PolicyId)) throw new InvalidOperationException("bank-policy-id-required");
        if (string.IsNullOrWhiteSpace(policy.CurrencyId)) throw new InvalidOperationException("bank-policy-currency-required");
        if (policy.MinimumDepositBaseUnits < 0 || policy.MinimumWithdrawalBaseUnits < 0) throw new InvalidOperationException("bank-minimum-invalid");
        if (policy.MaximumAccountBaseUnits is < 0 || policy.MaximumTransactionBaseUnits is < 1) throw new InvalidOperationException("bank-limit-invalid");
        if (policy.DepositFeeBasisPoints is < 0 or > 10_000 || policy.WithdrawalFeeBasisPoints is < 0 or > 10_000) throw new InvalidOperationException("bank-fee-basis-points-invalid");
        if (policy.MinimumDepositFeeBaseUnits < 0 || policy.MinimumWithdrawalFeeBaseUnits < 0) throw new InvalidOperationException("bank-minimum-fee-invalid");
    }
}

public static class RuntimeBankingEngine
{
    public static RuntimeBankTransactionResult Deposit(string transactionId, string ownerId, RuntimeCurrencyWallet wallet, RuntimeBankAccount account, long requestedBaseUnits)
    {
        ValidateCommon(transactionId, wallet, account, requestedBaseUnits);
        return LockBoth(wallet.Gate, account.Gate, () =>
        {
            account.EnsureOwnerUnsafe(ownerId);
            var replay = Replay(account, transactionId, RuntimeBankTransactionKind.Deposit, wallet.WalletId, account.AccountId, requestedBaseUnits);
            if (replay is not null) return replay;
            EnsureTransactionLimit(account.Policy, requestedBaseUnits);
            if (requestedBaseUnits < account.Policy.MinimumDepositBaseUnits) throw new InvalidOperationException("bank-deposit-below-minimum");
            var fee = CalculateFee(requestedBaseUnits, account.Policy.DepositFeeBasisPoints, account.Policy.MinimumDepositFeeBaseUnits, account.Policy.FeeRoundingMode);
            if (fee > requestedBaseUnits) throw new InvalidOperationException("bank-fee-exceeds-deposit");
            var net = requestedBaseUnits - fee;
            var walletBefore = wallet.CaptureUnsafe();
            var accountBefore = account.CaptureUnsafe();
            try
            {
                if (walletBefore.TotalBaseUnits < requestedBaseUnits) throw new InvalidOperationException("insufficient-currency");
                wallet.SetTotalUnsafe(walletBefore.TotalBaseUnits - requestedBaseUnits);
                account.SetBalanceUnsafe(checked(accountBefore.BalanceBaseUnits + net));
                account.MarkAppliedUnsafe(transactionId);
                return new(transactionId, RuntimeBankTransactionKind.Deposit, account.BankId, wallet.WalletId, account.AccountId, requestedBaseUnits, fee, net, wallet.CaptureUnsafe().Version, account.VersionUnsafe, false);
            }
            catch
            {
                wallet.RestoreUnsafe(walletBefore);
                account.RestoreUnsafe(accountBefore);
                throw;
            }
        });
    }

    public static RuntimeBankTransactionResult Withdraw(string transactionId, string ownerId, RuntimeBankAccount account, RuntimeCurrencyWallet wallet, long requestedBaseUnits)
    {
        ValidateCommon(transactionId, wallet, account, requestedBaseUnits);
        return LockBoth(wallet.Gate, account.Gate, () =>
        {
            account.EnsureOwnerUnsafe(ownerId);
            var replay = Replay(account, transactionId, RuntimeBankTransactionKind.Withdrawal, account.AccountId, wallet.WalletId, requestedBaseUnits);
            if (replay is not null) return replay;
            EnsureTransactionLimit(account.Policy, requestedBaseUnits);
            if (requestedBaseUnits < account.Policy.MinimumWithdrawalBaseUnits) throw new InvalidOperationException("bank-withdrawal-below-minimum");
            var fee = CalculateFee(requestedBaseUnits, account.Policy.WithdrawalFeeBasisPoints, account.Policy.MinimumWithdrawalFeeBaseUnits, account.Policy.FeeRoundingMode);
            var debit = checked(requestedBaseUnits + fee);
            var walletBefore = wallet.CaptureUnsafe();
            var accountBefore = account.CaptureUnsafe();
            try
            {
                if (accountBefore.BalanceBaseUnits < debit) throw new InvalidOperationException("insufficient-bank-balance");
                account.SetBalanceUnsafe(accountBefore.BalanceBaseUnits - debit);
                wallet.SetTotalUnsafe(checked(walletBefore.TotalBaseUnits + requestedBaseUnits));
                account.MarkAppliedUnsafe(transactionId);
                return new(transactionId, RuntimeBankTransactionKind.Withdrawal, account.BankId, account.AccountId, wallet.WalletId, requestedBaseUnits, fee, requestedBaseUnits, account.VersionUnsafe, wallet.CaptureUnsafe().Version, false);
            }
            catch
            {
                wallet.RestoreUnsafe(walletBefore);
                account.RestoreUnsafe(accountBefore);
                throw;
            }
        });
    }

    public static RuntimeBankTransactionResult Transfer(string transactionId, string ownerId, RuntimeBankAccount source, RuntimeBankAccount destination, long requestedBaseUnits)
    {
        if (ReferenceEquals(source, destination) || source.AccountId.Equals(destination.AccountId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("bank-transfer-accounts-must-differ");
        if (!source.CurrencyId.Equals(destination.CurrencyId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("bank-currency-mismatch");
        if (requestedBaseUnits <= 0) throw new InvalidOperationException("bank-amount-must-be-positive");
        var first = string.Compare(source.AccountId, destination.AccountId, StringComparison.OrdinalIgnoreCase) <= 0 ? source : destination;
        var second = ReferenceEquals(first, source) ? destination : source;
        return LockBoth(first.Gate, second.Gate, () =>
        {
            source.EnsureOwnerUnsafe(ownerId);
            var replay = Replay(source, transactionId, RuntimeBankTransactionKind.Transfer, source.AccountId, destination.AccountId, requestedBaseUnits);
            if (replay is not null) return replay;
            EnsureTransactionLimit(source.Policy, requestedBaseUnits);
            var sourceBefore = source.CaptureUnsafe();
            var destinationBefore = destination.CaptureUnsafe();
            try
            {
                if (sourceBefore.BalanceBaseUnits < requestedBaseUnits) throw new InvalidOperationException("insufficient-bank-balance");
                source.SetBalanceUnsafe(sourceBefore.BalanceBaseUnits - requestedBaseUnits);
                destination.SetBalanceUnsafe(checked(destinationBefore.BalanceBaseUnits + requestedBaseUnits));
                source.MarkAppliedUnsafe(transactionId);
                return new(transactionId, RuntimeBankTransactionKind.Transfer, source.BankId, source.AccountId, destination.AccountId, requestedBaseUnits, 0, requestedBaseUnits, source.VersionUnsafe, destination.VersionUnsafe, false);
            }
            catch
            {
                source.RestoreUnsafe(sourceBefore);
                destination.RestoreUnsafe(destinationBefore);
                throw;
            }
        });
    }

    private static RuntimeBankTransactionResult? Replay(RuntimeBankAccount account, string transactionId, RuntimeBankTransactionKind kind, string sourceId, string destinationId, long requestedBaseUnits)
    {
        if (!account.IsAppliedUnsafe(transactionId)) return null;
        return new(transactionId, kind, account.BankId, sourceId, destinationId, requestedBaseUnits, 0, requestedBaseUnits, account.VersionUnsafe, account.VersionUnsafe, true);
    }

    private static void ValidateCommon(string transactionId, RuntimeCurrencyWallet wallet, RuntimeBankAccount account, long requestedBaseUnits)
    {
        if (string.IsNullOrWhiteSpace(transactionId)) throw new InvalidOperationException("bank-transaction-id-required");
        if (requestedBaseUnits <= 0) throw new InvalidOperationException("bank-amount-must-be-positive");
        if (!wallet.CurrencyId.Equals(account.CurrencyId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("bank-currency-mismatch");
    }

    private static void EnsureTransactionLimit(RuntimeBankPolicy policy, long requestedBaseUnits)
    {
        if (policy.MaximumTransactionBaseUnits is { } maximum && requestedBaseUnits > maximum) throw new InvalidOperationException("bank-transaction-limit-exceeded");
    }

    private static long CalculateFee(long amount, int basisPoints, long minimum, RuntimeCommerceRoundingMode rounding)
    {
        if (basisPoints == 0) return minimum;
        var numerator = checked(amount * (long)basisPoints);
        var quotient = numerator / 10_000;
        var remainder = numerator % 10_000;
        var calculated = rounding switch
        {
            RuntimeCommerceRoundingMode.Floor => quotient,
            RuntimeCommerceRoundingMode.Ceiling => remainder == 0 ? quotient : checked(quotient + 1),
            RuntimeCommerceRoundingMode.NearestAwayFromZero => checked(quotient + (checked(remainder * 2) >= 10_000 ? 1 : 0)),
            _ => throw new InvalidOperationException("bank-fee-rounding-invalid")
        };
        return Math.Max(calculated, minimum);
    }

    private static T LockBoth<T>(object firstGate, object secondGate, Func<T> action)
    {
        lock (firstGate)
        lock (secondGate)
            return action();
    }
}

public sealed record RuntimeBankStoreSnapshot(int SchemaVersion, IReadOnlyList<RuntimeBankAccountSnapshot> Accounts);

public static class RuntimeBankPersistence
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(IEnumerable<RuntimeBankAccount> accounts) => JsonSerializer.Serialize(
        new RuntimeBankStoreSnapshot(CurrentSchemaVersion, accounts.Select(x => x.Capture()).OrderBy(x => x.AccountId, StringComparer.OrdinalIgnoreCase).ToArray()), Options);

    public static RuntimeBankStoreSnapshot Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<RuntimeBankStoreSnapshot>(json, Options) ?? throw new InvalidOperationException("bank-persistence-empty");
        if (snapshot.SchemaVersion != CurrentSchemaVersion) throw new InvalidOperationException("bank-persistence-schema-unsupported");
        var accountIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in snapshot.Accounts)
        {
            if (string.IsNullOrWhiteSpace(account.AccountId) || !accountIds.Add(account.AccountId)) throw new InvalidOperationException("bank-persistence-account-id-invalid");
            if (string.IsNullOrWhiteSpace(account.BankId) || string.IsNullOrWhiteSpace(account.CurrencyId)) throw new InvalidOperationException("bank-persistence-reference-invalid");
            if (account.OwnerIds.Count == 0 || account.OwnerIds.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("bank-persistence-owner-invalid");
            if (account.OwnerIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != account.OwnerIds.Count) throw new InvalidOperationException("bank-persistence-owner-duplicate");
            if (account.BalanceBaseUnits < 0 || account.Version < 0) throw new InvalidOperationException("bank-persistence-state-invalid");
            if (account.AppliedTransactionIds.Any(string.IsNullOrWhiteSpace) || account.AppliedTransactionIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != account.AppliedTransactionIds.Count) throw new InvalidOperationException("bank-persistence-transaction-invalid");
        }
        return snapshot;
    }
}
