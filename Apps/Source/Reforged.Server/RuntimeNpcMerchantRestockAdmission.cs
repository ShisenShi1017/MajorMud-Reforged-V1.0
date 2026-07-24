using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcMerchantRestockEntry(string CanonicalItemId, int Quantity, DateTimeOffset NextRestockUtc);
public sealed record RuntimeNpcMerchantRestockRequest(string RequestId, string NpcInstanceId, string ShopId, IReadOnlyList<RuntimeNpcMerchantRestockEntry> Entries, string Authority);
public sealed record RuntimeNpcMerchantRestockResult(bool Succeeded, bool Replay, string RequestId, string NpcInstanceId, string ShopId, int AppliedEntryCount, string Reason);

/// <summary>
/// Exact, replay-safe merchant restock boundary. The caller owns cadence, stock selection,
/// quantities, limits, and next-restock timestamps. This service validates the living merchant
/// relationship and atomically commits the exact canonical stock batch without deriving policy.
/// </summary>
public sealed class RuntimeNpcMerchantRestockAdmissionService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeGameplayCompletionStateStore _store;
    private readonly RuntimeItemCatalog _items;

    public RuntimeNpcMerchantRestockAdmissionService(RuntimeLivingWorldService living, RuntimeGameplayCompletionStateStore store, RuntimeItemCatalog items)
    {
        _living = living;
        _store = store;
        _items = items;
    }

    public RuntimeNpcMerchantRestockResult ApplyExact(RuntimeNpcMerchantRestockRequest request, RuntimeServiceDefinition shop)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) return Reject(request, "request-id-required");
        if (string.IsNullOrWhiteSpace(request.Authority)) return Reject(request, "authority-required");
        if (!shop.Id.Equals(request.ShopId, StringComparison.OrdinalIgnoreCase)) return Reject(request, "shop-id-mismatch");
        try { RequireMerchant(request.NpcInstanceId, shop); }
        catch (InvalidOperationException ex) { return Reject(request, Normalize(ex.Message)); }
        if (request.Entries is null || request.Entries.Count == 0) return Reject(request, "restock-entries-required");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in request.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.CanonicalItemId)) return Reject(request, "canonical-item-id-required");
            if (!seen.Add(entry.CanonicalItemId)) return Reject(request, "duplicate-canonical-item-id");
            try { _items.Require(entry.CanonicalItemId); }
            catch (InvalidOperationException) { return Reject(request, "canonical-item-not-found"); }
            if (entry.Quantity < 0) return Reject(request, "quantity-must-be-nonnegative");
        }

        return _store.ExecuteExactShopStateTransaction(shop, state =>
        {
            var applied = new HashSet<string>(state.AppliedRestockRequestIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (applied.Contains(request.RequestId))
                return (state, new RuntimeNpcMerchantRestockResult(true, true, request.RequestId, request.NpcInstanceId, shop.Id, 0, "restock-request-already-applied"));

            var stock = new Dictionary<string, RuntimeShopStockEntry>(state.Stock, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in request.Entries.OrderBy(x => x.CanonicalItemId, StringComparer.OrdinalIgnoreCase))
                stock[entry.CanonicalItemId] = new RuntimeShopStockEntry(entry.CanonicalItemId, entry.Quantity, entry.NextRestockUtc);
            applied.Add(request.RequestId);
            var updated = new RuntimeShopState(shop.Id, stock, DateTimeOffset.UtcNow, applied.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            return (updated, new RuntimeNpcMerchantRestockResult(true, false, request.RequestId, request.NpcInstanceId, shop.Id, request.Entries.Count, "exact-restock-applied"));
        });
    }

    private RuntimeNpcInspectionSnapshot RequireMerchant(string instanceId, RuntimeServiceDefinition shop)
    {
        var npc = _living.InspectNpc(instanceId) ?? throw new InvalidOperationException("instance-not-found");
        if (!npc.Alive) throw new InvalidOperationException("npc-is-not-alive");
        if (!(npc.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) || npc.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("npc-does-not-own-merchant-service");
        if (!shop.ServiceKind.Contains("shop", StringComparison.OrdinalIgnoreCase) && !shop.ServiceKind.Contains("merchant", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("service-is-not-a-shop");
        return npc;
    }

    private static RuntimeNpcMerchantRestockResult Reject(RuntimeNpcMerchantRestockRequest request, string reason) =>
        new(false, false, request.RequestId ?? string.Empty, request.NpcInstanceId ?? string.Empty, request.ShopId ?? string.Empty, 0, reason);
    private static string Normalize(string value) => string.Join('-', value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcMerchantRestockAdmissionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcMerchantRestockAdmissionSmokeReport Run()
    {
        var report = new RuntimeNpcMerchantRestockAdmissionSmokeReport();
        try
        {
            var type = typeof(RuntimeNpcMerchantRestockAdmissionService);
            Require(type.GetMethod(nameof(RuntimeNpcMerchantRestockAdmissionService.ApplyExact)) is not null, "merchant-restock-exact-admission-exposed");
            Require(typeof(RuntimeShopState).GetProperty(nameof(RuntimeShopState.AppliedRestockRequestIds)) is not null, "merchant-restock-replay-state-persisted");
            report.Checks.AddRange(["merchant-restock-exact-admission-exposed", "merchant-restock-replay-state-persisted"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
