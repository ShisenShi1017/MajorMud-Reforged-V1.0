using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeCurrencyDenomination(string Id, long BaseUnits, int DisplayOrder);

public sealed record RuntimeCurrencyDefinition(
    string Id,
    string BaseDenominationId,
    IReadOnlyList<RuntimeCurrencyDenomination> Denominations,
    long? MaximumAccountBaseUnits,
    bool WeightEnabled,
    decimal WeightPerBaseUnit);

public sealed record RuntimeCoinQuantity(string DenominationId, long Quantity);

public sealed record RuntimeCurrencyWalletSnapshot(
    string WalletId,
    string CurrencyId,
    IReadOnlyDictionary<string, long> Quantities,
    long TotalBaseUnits,
    long Version);

public sealed class RuntimeCurrencyMath
{
    private readonly RuntimeCurrencyDefinition _definition;
    private readonly Dictionary<string, RuntimeCurrencyDenomination> _denominations;

    public RuntimeCurrencyMath(RuntimeCurrencyDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id)) throw new ArgumentException("currency-id-required", nameof(definition));
        if (definition.Denominations.Count == 0) throw new InvalidOperationException("currency-denominations-required");
        if (definition.Denominations.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.BaseUnits <= 0)) throw new InvalidOperationException("invalid-currency-denomination");
        if (definition.Denominations.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definition.Denominations.Count) throw new InvalidOperationException("duplicate-currency-denomination");
        if (!definition.Denominations.Any(x => x.Id.Equals(definition.BaseDenominationId, StringComparison.OrdinalIgnoreCase) && x.BaseUnits == 1)) throw new InvalidOperationException("base-denomination-must-equal-one");
        if (definition.MaximumAccountBaseUnits is < 0) throw new InvalidOperationException("invalid-currency-limit");
        if (definition.WeightPerBaseUnit < 0) throw new InvalidOperationException("invalid-currency-weight");
        _definition = definition;
        _denominations = definition.Denominations.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeCurrencyDefinition Definition => _definition;

    public long ToBaseUnits(IEnumerable<RuntimeCoinQuantity> quantities)
    {
        long total = 0;
        foreach (var quantity in quantities)
        {
            if (quantity.Quantity < 0) throw new InvalidOperationException("negative-coin-quantity");
            if (!_denominations.TryGetValue(quantity.DenominationId, out var denomination)) throw new InvalidOperationException("unknown-currency-denomination");
            total = checked(total + checked(quantity.Quantity * denomination.BaseUnits));
        }
        EnsureWithinLimit(total);
        return total;
    }

    public IReadOnlyList<RuntimeCoinQuantity> FromBaseUnits(long baseUnits)
    {
        if (baseUnits < 0) throw new InvalidOperationException("negative-currency-balance");
        EnsureWithinLimit(baseUnits);
        var remaining = baseUnits;
        var result = new List<RuntimeCoinQuantity>();
        foreach (var denomination in _definition.Denominations.OrderByDescending(x => x.BaseUnits).ThenBy(x => x.DisplayOrder))
        {
            var quantity = remaining / denomination.BaseUnits;
            remaining %= denomination.BaseUnits;
            if (quantity > 0) result.Add(new(denomination.Id, quantity));
        }
        if (remaining != 0) throw new InvalidOperationException("currency-not-representable");
        return result;
    }

    public decimal CalculateWeight(long baseUnits)
    {
        if (baseUnits < 0) throw new InvalidOperationException("negative-currency-balance");
        return _definition.WeightEnabled ? checked(baseUnits * _definition.WeightPerBaseUnit) : 0m;
    }

    public void EnsureWithinLimit(long baseUnits)
    {
        if (baseUnits < 0) throw new InvalidOperationException("negative-currency-balance");
        if (_definition.MaximumAccountBaseUnits is { } maximum && baseUnits > maximum) throw new OverflowException("currency-account-limit-exceeded");
    }
}

public sealed class RuntimeCurrencyWallet
{
    private readonly object _gate = new();
    private readonly RuntimeCurrencyMath _math;
    private Dictionary<string, long> _quantities;
    private long _version;

    public RuntimeCurrencyWallet(string walletId, RuntimeCurrencyMath math, IEnumerable<RuntimeCoinQuantity>? initial = null)
    {
        if (string.IsNullOrWhiteSpace(walletId)) throw new ArgumentException("wallet-id-required", nameof(walletId));
        WalletId = walletId;
        _math = math;
        _quantities = Normalize(initial ?? Array.Empty<RuntimeCoinQuantity>());
        _math.EnsureWithinLimit(TotalBaseUnitsUnsafe());
    }

    public string WalletId { get; }
    public string CurrencyId => _math.Definition.Id;

    public RuntimeCurrencyWalletSnapshot Capture()
    {
        lock (_gate) return CaptureUnsafe();
    }

    public void Restore(RuntimeCurrencyWalletSnapshot snapshot)
    {
        if (!WalletId.Equals(snapshot.WalletId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("currency-wallet-mismatch");
        if (!CurrencyId.Equals(snapshot.CurrencyId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("currency-definition-mismatch");
        lock (_gate)
        {
            var normalized = Normalize(snapshot.Quantities.Select(x => new RuntimeCoinQuantity(x.Key, x.Value)));
            var total = _math.ToBaseUnits(normalized.Select(x => new RuntimeCoinQuantity(x.Key, x.Value)));
            if (total != snapshot.TotalBaseUnits) throw new InvalidOperationException("currency-snapshot-total-mismatch");
            _quantities = normalized;
            _version = snapshot.Version;
        }
    }

    public void CreditBaseUnits(long baseUnits)
    {
        if (baseUnits < 0) throw new ArgumentOutOfRangeException(nameof(baseUnits));
        lock (_gate)
        {
            var total = checked(TotalBaseUnitsUnsafe() + baseUnits);
            _math.EnsureWithinLimit(total);
            _quantities = Normalize(_math.FromBaseUnits(total));
            _version = checked(_version + 1);
        }
    }

    public void DebitBaseUnits(long baseUnits)
    {
        if (baseUnits < 0) throw new ArgumentOutOfRangeException(nameof(baseUnits));
        lock (_gate)
        {
            var total = TotalBaseUnitsUnsafe();
            if (total < baseUnits) throw new InvalidOperationException("insufficient-currency");
            _quantities = Normalize(_math.FromBaseUnits(total - baseUnits));
            _version = checked(_version + 1);
        }
    }

    internal object Gate => _gate;
    internal RuntimeCurrencyWalletSnapshot CaptureUnsafe() => new(WalletId, CurrencyId, new Dictionary<string, long>(_quantities, StringComparer.OrdinalIgnoreCase), TotalBaseUnitsUnsafe(), _version);
    internal void RestoreUnsafe(RuntimeCurrencyWalletSnapshot snapshot)
    {
        _quantities = new Dictionary<string, long>(snapshot.Quantities, StringComparer.OrdinalIgnoreCase);
        _version = snapshot.Version;
    }
    internal long TotalBaseUnitsUnsafe() => _math.ToBaseUnits(_quantities.Select(x => new RuntimeCoinQuantity(x.Key, x.Value)));
    internal void SetTotalUnsafe(long baseUnits)
    {
        _math.EnsureWithinLimit(baseUnits);
        _quantities = Normalize(_math.FromBaseUnits(baseUnits));
        _version = checked(_version + 1);
    }

    private Dictionary<string, long> Normalize(IEnumerable<RuntimeCoinQuantity> quantities)
    {
        var total = _math.ToBaseUnits(quantities);
        return _math.FromBaseUnits(total).ToDictionary(x => x.DenominationId, x => x.Quantity, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record RuntimeCurrencyTransferResult(string TransferId, string SourceWalletId, string DestinationWalletId, long BaseUnits, long SourceVersion, long DestinationVersion);

public static class RuntimeCurrencyTransferEngine
{
    public static RuntimeCurrencyTransferResult Transfer(RuntimeCurrencyWallet source, RuntimeCurrencyWallet destination, long baseUnits, string? transferId = null)
    {
        if (ReferenceEquals(source, destination) || source.WalletId.Equals(destination.WalletId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("currency-transfer-wallets-must-differ");
        if (!source.CurrencyId.Equals(destination.CurrencyId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("currency-transfer-definition-mismatch");
        if (baseUnits <= 0) throw new InvalidOperationException("currency-transfer-amount-must-be-positive");

        var first = string.Compare(source.WalletId, destination.WalletId, StringComparison.OrdinalIgnoreCase) <= 0 ? source : destination;
        var second = ReferenceEquals(first, source) ? destination : source;
        lock (first.Gate)
        lock (second.Gate)
        {
            var sourceBefore = source.CaptureUnsafe();
            var destinationBefore = destination.CaptureUnsafe();
            try
            {
                if (sourceBefore.TotalBaseUnits < baseUnits) throw new InvalidOperationException("insufficient-currency");
                source.SetTotalUnsafe(sourceBefore.TotalBaseUnits - baseUnits);
                destination.SetTotalUnsafe(checked(destinationBefore.TotalBaseUnits + baseUnits));
                var sourceAfter = source.CaptureUnsafe();
                var destinationAfter = destination.CaptureUnsafe();
                return new(transferId ?? Guid.NewGuid().ToString("N"), source.WalletId, destination.WalletId, baseUnits, sourceAfter.Version, destinationAfter.Version);
            }
            catch
            {
                source.RestoreUnsafe(sourceBefore);
                destination.RestoreUnsafe(destinationBefore);
                throw;
            }
        }
    }
}

public sealed record RuntimeCurrencyStoreSnapshot(IReadOnlyList<RuntimeCurrencyWalletSnapshot> Wallets);

public static class RuntimeCurrencyPersistence
{
    public static string Serialize(RuntimeCurrencyStoreSnapshot snapshot) => JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeCurrencyStoreSnapshot Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<RuntimeCurrencyStoreSnapshot>(json) ?? throw new InvalidOperationException("currency-snapshot-invalid");
        var errors = Validate(snapshot);
        if (errors.Count != 0) throw new InvalidOperationException(string.Join(';', errors));
        return snapshot;
    }

    public static IReadOnlyList<string> Validate(RuntimeCurrencyStoreSnapshot snapshot)
    {
        var errors = new List<string>();
        if (snapshot.Wallets.Select(x => x.WalletId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Wallets.Count) errors.Add("duplicate-wallet-id");
        foreach (var wallet in snapshot.Wallets)
        {
            if (string.IsNullOrWhiteSpace(wallet.WalletId)) errors.Add("wallet-id-required");
            if (string.IsNullOrWhiteSpace(wallet.CurrencyId)) errors.Add($"wallet-currency-required:{wallet.WalletId}");
            if (wallet.TotalBaseUnits < 0 || wallet.Quantities.Values.Any(x => x < 0)) errors.Add($"negative-wallet-balance:{wallet.WalletId}");
            if (wallet.Version < 0) errors.Add($"negative-wallet-version:{wallet.WalletId}");
        }
        return errors;
    }
}
