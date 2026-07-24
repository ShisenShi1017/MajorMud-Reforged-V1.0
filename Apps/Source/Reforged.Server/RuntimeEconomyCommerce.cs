namespace Reforged.Server;

public enum RuntimeCommerceRoundingMode
{
    Floor,
    Ceiling,
    NearestAwayFromZero
}

public sealed record RuntimeEconomyItemDefinition(
    string ItemId,
    long BaseValueUnits,
    bool Stackable,
    long MaximumStack,
    long CapacityUnitsPerItem,
    bool Sellable,
    bool QuestItem,
    bool LimitedItem);

public sealed record RuntimeMerchantPricePolicy(
    int BuyBasisPoints,
    int SellBasisPoints,
    RuntimeCommerceRoundingMode RoundingMode,
    long MinimumBuyPriceUnits,
    long MinimumSellPriceUnits);

public sealed record RuntimeMerchantStockDefinition(
    string ItemId,
    long InitialQuantity,
    long? MaximumQuantity,
    bool Unlimited,
    bool Purchasable,
    bool MerchantAcceptsSales);

public sealed record RuntimeMerchantDefinition(
    string MerchantId,
    string ShopId,
    string WalletId,
    RuntimeMerchantPricePolicy PricePolicy,
    IReadOnlyList<RuntimeMerchantStockDefinition> Stock);

public sealed record RuntimeInventorySnapshot(
    string InventoryId,
    IReadOnlyDictionary<string, long> Quantities,
    long CapacityUsed,
    long Version);

public sealed class RuntimeEconomyInventory
{
    private readonly object _gate = new();
    private readonly IReadOnlyDictionary<string, RuntimeEconomyItemDefinition> _items;
    private Dictionary<string, long> _quantities;
    private long _version;

    public RuntimeEconomyInventory(string inventoryId, long maximumCapacityUnits, IEnumerable<RuntimeEconomyItemDefinition> items, IEnumerable<KeyValuePair<string, long>>? initial = null)
    {
        if (string.IsNullOrWhiteSpace(inventoryId)) throw new ArgumentException("inventory-id-required", nameof(inventoryId));
        if (maximumCapacityUnits < 0) throw new InvalidOperationException("inventory-capacity-invalid");
        InventoryId = inventoryId;
        MaximumCapacityUnits = maximumCapacityUnits;
        _items = items.ToDictionary(x => x.ItemId, StringComparer.OrdinalIgnoreCase);
        if (_items.Count == 0) throw new InvalidOperationException("item-catalog-required");
        if (_items.Values.Any(x => string.IsNullOrWhiteSpace(x.ItemId) || x.BaseValueUnits < 0 || x.MaximumStack <= 0 || x.CapacityUnitsPerItem < 0)) throw new InvalidOperationException("item-definition-invalid");
        _quantities = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in initial ?? Array.Empty<KeyValuePair<string, long>>()) AddUnsafe(pair.Key, pair.Value);
        EnsureCapacityUnsafe();
    }

    public string InventoryId { get; }
    public long MaximumCapacityUnits { get; }
    internal object Gate => _gate;

    public RuntimeInventorySnapshot Capture()
    {
        lock (_gate) return CaptureUnsafe();
    }

    public long Quantity(string itemId)
    {
        lock (_gate) return _quantities.TryGetValue(itemId, out var value) ? value : 0;
    }

    internal RuntimeInventorySnapshot CaptureUnsafe() => new(InventoryId, new Dictionary<string, long>(_quantities, StringComparer.OrdinalIgnoreCase), CapacityUsedUnsafe(), _version);

    internal void RestoreUnsafe(RuntimeInventorySnapshot snapshot)
    {
        if (!InventoryId.Equals(snapshot.InventoryId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("inventory-snapshot-mismatch");
        _quantities = new Dictionary<string, long>(snapshot.Quantities, StringComparer.OrdinalIgnoreCase);
        _version = snapshot.Version;
    }

    internal void AddUnsafe(string itemId, long quantity)
    {
        if (quantity <= 0) throw new InvalidOperationException("item-quantity-must-be-positive");
        var item = Resolve(itemId);
        var current = _quantities.TryGetValue(itemId, out var found) ? found : 0;
        var next = checked(current + quantity);
        if (!item.Stackable && next > 1) throw new InvalidOperationException("item-not-stackable");
        if (next > item.MaximumStack) throw new InvalidOperationException("item-stack-limit-exceeded");
        _quantities[itemId] = next;
        try { EnsureCapacityUnsafe(); }
        catch { if (current == 0) _quantities.Remove(itemId); else _quantities[itemId] = current; throw; }
        _version = checked(_version + 1);
    }

    internal void RemoveUnsafe(string itemId, long quantity)
    {
        if (quantity <= 0) throw new InvalidOperationException("item-quantity-must-be-positive");
        var current = _quantities.TryGetValue(itemId, out var found) ? found : 0;
        if (current < quantity) throw new InvalidOperationException("insufficient-item-quantity");
        var next = current - quantity;
        if (next == 0) _quantities.Remove(itemId); else _quantities[itemId] = next;
        _version = checked(_version + 1);
    }

    internal RuntimeEconomyItemDefinition Resolve(string itemId) => _items.TryGetValue(itemId, out var item) ? item : throw new InvalidOperationException("unknown-item-id");

    private long CapacityUsedUnsafe()
    {
        long total = 0;
        foreach (var pair in _quantities)
        {
            var item = Resolve(pair.Key);
            total = checked(total + checked(pair.Value * item.CapacityUnitsPerItem));
        }
        return total;
    }

    private void EnsureCapacityUnsafe()
    {
        if (CapacityUsedUnsafe() > MaximumCapacityUnits) throw new InvalidOperationException("inventory-capacity-exceeded");
    }
}

public sealed record RuntimeMerchantStockSnapshot(string MerchantId, IReadOnlyDictionary<string, long> Quantities, long Version);

public sealed class RuntimeMerchantStock
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeMerchantStockDefinition> _definitions;
    private Dictionary<string, long> _quantities;
    private long _version;

    public RuntimeMerchantStock(RuntimeMerchantDefinition merchant)
    {
        if (string.IsNullOrWhiteSpace(merchant.MerchantId) || string.IsNullOrWhiteSpace(merchant.ShopId) || string.IsNullOrWhiteSpace(merchant.WalletId)) throw new InvalidOperationException("merchant-definition-invalid");
        Merchant = merchant;
        _definitions = merchant.Stock.ToDictionary(x => x.ItemId, StringComparer.OrdinalIgnoreCase);
        if (_definitions.Count != merchant.Stock.Count) throw new InvalidOperationException("duplicate-merchant-stock-item");
        if (_definitions.Values.Any(x => x.InitialQuantity < 0 || x.MaximumQuantity is < 0 || (x.MaximumQuantity is { } max && x.InitialQuantity > max))) throw new InvalidOperationException("merchant-stock-invalid");
        _quantities = _definitions.ToDictionary(x => x.Key, x => x.Value.InitialQuantity, StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeMerchantDefinition Merchant { get; }
    internal object Gate => _gate;

    public RuntimeMerchantStockSnapshot Capture()
    {
        lock (_gate) return CaptureUnsafe();
    }

    internal RuntimeMerchantStockSnapshot CaptureUnsafe() => new(Merchant.MerchantId, new Dictionary<string, long>(_quantities, StringComparer.OrdinalIgnoreCase), _version);

    internal void RestoreUnsafe(RuntimeMerchantStockSnapshot snapshot)
    {
        if (!Merchant.MerchantId.Equals(snapshot.MerchantId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("merchant-stock-snapshot-mismatch");
        _quantities = new Dictionary<string, long>(snapshot.Quantities, StringComparer.OrdinalIgnoreCase);
        _version = snapshot.Version;
    }

    internal RuntimeMerchantStockDefinition Resolve(string itemId) => _definitions.TryGetValue(itemId, out var stock) ? stock : throw new InvalidOperationException("merchant-does-not-carry-item");

    internal long AvailableUnsafe(string itemId)
    {
        var definition = Resolve(itemId);
        return definition.Unlimited ? long.MaxValue : _quantities[itemId];
    }

    internal void RemoveForSaleUnsafe(string itemId, long quantity)
    {
        var definition = Resolve(itemId);
        if (!definition.Purchasable) throw new InvalidOperationException("merchant-item-not-purchasable");
        if (definition.Unlimited) return;
        var current = _quantities[itemId];
        if (current < quantity) throw new InvalidOperationException("merchant-stock-insufficient");
        _quantities[itemId] = current - quantity;
        _version = checked(_version + 1);
    }

    internal void AddFromPurchaseUnsafe(string itemId, long quantity)
    {
        var definition = Resolve(itemId);
        if (!definition.MerchantAcceptsSales) throw new InvalidOperationException("merchant-refuses-item");
        if (definition.Unlimited) return;
        var current = _quantities[itemId];
        var next = checked(current + quantity);
        if (definition.MaximumQuantity is { } maximum && next > maximum) throw new InvalidOperationException("merchant-stock-limit-exceeded");
        _quantities[itemId] = next;
        _version = checked(_version + 1);
    }
}

public sealed record RuntimeCommerceQuote(string MerchantId, string ShopId, string ItemId, long Quantity, long UnitPriceBaseUnits, long TotalPriceBaseUnits, string Direction);

public static class RuntimeCommercePricing
{
    public static RuntimeCommerceQuote QuoteBuy(RuntimeMerchantDefinition merchant, RuntimeEconomyItemDefinition item, long quantity) => Quote(merchant, item, quantity, merchant.PricePolicy.BuyBasisPoints, merchant.PricePolicy.MinimumBuyPriceUnits, "buy");
    public static RuntimeCommerceQuote QuoteSell(RuntimeMerchantDefinition merchant, RuntimeEconomyItemDefinition item, long quantity) => Quote(merchant, item, quantity, merchant.PricePolicy.SellBasisPoints, merchant.PricePolicy.MinimumSellPriceUnits, "sell");

    private static RuntimeCommerceQuote Quote(RuntimeMerchantDefinition merchant, RuntimeEconomyItemDefinition item, long quantity, int basisPoints, long minimum, string direction)
    {
        if (quantity <= 0) throw new InvalidOperationException("commerce-quantity-must-be-positive");
        if (basisPoints < 0) throw new InvalidOperationException("price-basis-points-invalid");
        if (minimum < 0) throw new InvalidOperationException("minimum-price-invalid");
        var numerator = checked(item.BaseValueUnits * (long)basisPoints);
        var unit = ApplyRounding(numerator, 10_000, merchant.PricePolicy.RoundingMode);
        unit = Math.Max(unit, minimum);
        var total = checked(unit * quantity);
        return new(merchant.MerchantId, merchant.ShopId, item.ItemId, quantity, unit, total, direction);
    }

    private static long ApplyRounding(long numerator, long denominator, RuntimeCommerceRoundingMode mode)
    {
        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        return mode switch
        {
            RuntimeCommerceRoundingMode.Floor => quotient,
            RuntimeCommerceRoundingMode.Ceiling => remainder == 0 ? quotient : checked(quotient + 1),
            RuntimeCommerceRoundingMode.NearestAwayFromZero => checked(quotient + (checked(remainder * 2) >= denominator ? 1 : 0)),
            _ => throw new InvalidOperationException("price-rounding-mode-invalid")
        };
    }
}

public sealed record RuntimeCommerceTransactionResult(string TransactionId, RuntimeCommerceQuote Quote, long BuyerWalletVersion, long SellerWalletVersion, long InventoryVersion, long MerchantStockVersion);

public static class RuntimeCommerceEngine
{
    public static RuntimeCommerceTransactionResult Buy(string transactionId, RuntimeMerchantStock merchantStock, RuntimeCurrencyWallet buyerWallet, RuntimeCurrencyWallet merchantWallet, RuntimeEconomyInventory buyerInventory, string itemId, long quantity)
    {
        var item = buyerInventory.Resolve(itemId);
        var quote = RuntimeCommercePricing.QuoteBuy(merchantStock.Merchant, item, quantity);
        return Execute(transactionId, quote, merchantStock, buyerWallet, merchantWallet, buyerInventory, buying: true);
    }

    public static RuntimeCommerceTransactionResult Sell(string transactionId, RuntimeMerchantStock merchantStock, RuntimeCurrencyWallet sellerWallet, RuntimeCurrencyWallet merchantWallet, RuntimeEconomyInventory sellerInventory, string itemId, long quantity)
    {
        var item = sellerInventory.Resolve(itemId);
        if (!item.Sellable || item.QuestItem) throw new InvalidOperationException("item-not-sellable");
        var quote = RuntimeCommercePricing.QuoteSell(merchantStock.Merchant, item, quantity);
        return Execute(transactionId, quote, merchantStock, sellerWallet, merchantWallet, sellerInventory, buying: false);
    }

    private static RuntimeCommerceTransactionResult Execute(string transactionId, RuntimeCommerceQuote quote, RuntimeMerchantStock stock, RuntimeCurrencyWallet characterWallet, RuntimeCurrencyWallet merchantWallet, RuntimeEconomyInventory inventory, bool buying)
    {
        if (string.IsNullOrWhiteSpace(transactionId)) throw new InvalidOperationException("transaction-id-required");
        if (!stock.Merchant.WalletId.Equals(merchantWallet.WalletId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("merchant-wallet-ownership-mismatch");
        var walletGates = string.Compare(characterWallet.WalletId, merchantWallet.WalletId, StringComparison.OrdinalIgnoreCase) <= 0
            ? new[] { characterWallet.Gate, merchantWallet.Gate }
            : new[] { merchantWallet.Gate, characterWallet.Gate };
        var gates = walletGates.Concat(new[] { inventory.Gate, stock.Gate }).Distinct().ToArray();
        return LockAll<RuntimeCommerceTransactionResult>(gates, 0, () =>
        {
            var characterBefore = characterWallet.CaptureUnsafe();
            var merchantBefore = merchantWallet.CaptureUnsafe();
            var inventoryBefore = inventory.CaptureUnsafe();
            var stockBefore = stock.CaptureUnsafe();
            try
            {
                if (buying)
                {
                    if (stock.AvailableUnsafe(quote.ItemId) < quote.Quantity) throw new InvalidOperationException("merchant-stock-insufficient");
                    if (characterBefore.TotalBaseUnits < quote.TotalPriceBaseUnits) throw new InvalidOperationException("insufficient-currency");
                    stock.RemoveForSaleUnsafe(quote.ItemId, quote.Quantity);
                    inventory.AddUnsafe(quote.ItemId, quote.Quantity);
                    characterWallet.SetTotalUnsafe(characterBefore.TotalBaseUnits - quote.TotalPriceBaseUnits);
                    merchantWallet.SetTotalUnsafe(checked(merchantBefore.TotalBaseUnits + quote.TotalPriceBaseUnits));
                }
                else
                {
                    if (inventoryBefore.Quantities.GetValueOrDefault(quote.ItemId) < quote.Quantity) throw new InvalidOperationException("insufficient-item-quantity");
                    if (merchantBefore.TotalBaseUnits < quote.TotalPriceBaseUnits) throw new InvalidOperationException("merchant-insufficient-currency");
                    stock.AddFromPurchaseUnsafe(quote.ItemId, quote.Quantity);
                    inventory.RemoveUnsafe(quote.ItemId, quote.Quantity);
                    merchantWallet.SetTotalUnsafe(merchantBefore.TotalBaseUnits - quote.TotalPriceBaseUnits);
                    characterWallet.SetTotalUnsafe(checked(characterBefore.TotalBaseUnits + quote.TotalPriceBaseUnits));
                }
                return new(transactionId, quote, characterWallet.CaptureUnsafe().Version, merchantWallet.CaptureUnsafe().Version, inventory.CaptureUnsafe().Version, stock.CaptureUnsafe().Version);
            }
            catch
            {
                characterWallet.RestoreUnsafe(characterBefore);
                merchantWallet.RestoreUnsafe(merchantBefore);
                inventory.RestoreUnsafe(inventoryBefore);
                stock.RestoreUnsafe(stockBefore);
                throw;
            }
        });
    }

    private static T LockAll<T>(object[] gates, int index, Func<T> action)
    {
        if (index == gates.Length) return action();
        lock (gates[index]) return LockAll(gates, index + 1, action);
    }
}
