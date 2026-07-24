using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcMerchantGoldSettlementRequest(
    string NpcInstanceId,
    string RequestId,
    string ActorId,
    string ServiceKind,
    long ExactAmount,
    DateTimeOffset SettledAtUtc,
    string Authority,
    string? Outcome = null);

public sealed record RuntimeNpcMerchantGoldSettlementResult(
    bool Settled,
    string NpcInstanceId,
    string RequestId,
    string ServiceKind,
    long Amount,
    long ActorCarriedBefore,
    long ActorCarriedAfter,
    long NpcGoldBefore,
    long NpcGoldAfter,
    string Reason);

/// <summary>
/// Exact merchant-currency settlement boundary. Pricing and authorization are owned upstream.
/// This service only settles an already-admitted exact buy, sell, or repair request between the
/// actor's carried-currency account and the living merchant NPC's persisted gold balance.
/// Every failure rolls both balances back and leaves the active service request available.
/// </summary>
public sealed class RuntimeNpcMerchantGoldSettlementService
{
    private readonly RuntimeLivingWorldService _living;

    public RuntimeNpcMerchantGoldSettlementService(RuntimeLivingWorldService living) => _living = living;

    public RuntimeNpcMerchantGoldSettlementResult SettleExact(RuntimeNpcMerchantGoldSettlementRequest request, RuntimeCurrencyAccount actor)
    {
        var actorBefore = actor.Capture();
        var npcBefore = TryInspect(request.NpcInstanceId);
        if (npcBefore is null) return Reject(request, actorBefore.Carried, actorBefore.Carried, 0, 0, "instance-not-found");
        if (string.IsNullOrWhiteSpace(request.Authority)) return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "authority-required");
        if (string.IsNullOrWhiteSpace(request.RequestId)) return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "request-id-required");
        if (!actor.AccountId.Equals(request.ActorId, StringComparison.OrdinalIgnoreCase)) return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "actor-account-mismatch");
        if (request.ExactAmount <= 0) return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "amount-must-be-positive");
        var kind = request.ServiceKind?.Trim().ToLowerInvariant() ?? string.Empty;
        if (kind is not ("buy" or "sell" or "repair")) return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "unsupported-service-kind");

        var npc = _living.InspectNpc(request.NpcInstanceId);
        if (npc is null || !npc.Alive) return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "npc-is-not-alive");
        if (!npc.Metadata.TryGetValue("npc_service_active_request_id", out var active) || !active.Equals(request.RequestId, StringComparison.Ordinal))
            return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "active-request-mismatch");
        if (!npc.Metadata.TryGetValue("npc_service_actor_id", out var actorId) || !actorId.Equals(request.ActorId, StringComparison.OrdinalIgnoreCase))
            return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "active-actor-mismatch");
        if (!npc.Metadata.TryGetValue("npc_service_kind", out var activeKind) || !activeKind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, "active-service-kind-mismatch");

        try
        {
            if (kind is "buy" or "repair")
            {
                actor.DebitCarried(request.ExactAmount);
                var mutation = _living.AdjustNpcGoldExact(request.NpcInstanceId, request.ExactAmount, request.Authority, request.SettledAtUtc);
                if (!mutation.Applied) throw new InvalidOperationException(mutation.Reason);
            }
            else
            {
                var mutation = _living.AdjustNpcGoldExact(request.NpcInstanceId, -request.ExactAmount, request.Authority, request.SettledAtUtc);
                if (!mutation.Applied) throw new InvalidOperationException(mutation.Reason);
                actor.CreditCarried(request.ExactAmount);
            }

            if (!_living.CompleteNpcService(request.NpcInstanceId, request.RequestId, true,
                    string.IsNullOrWhiteSpace(request.Outcome) ? "merchant-gold-settled" : request.Outcome, request.SettledAtUtc))
                throw new InvalidOperationException("service-completion-rejected");

            var actorAfter = actor.Capture();
            var npcAfter = _living.InspectNpcGold(request.NpcInstanceId);
            return new(true, request.NpcInstanceId, request.RequestId, kind, request.ExactAmount,
                actorBefore.Carried, actorAfter.Carried, npcBefore.Gold, npcAfter.Gold, "merchant-gold-settled");
        }
        catch (Exception ex)
        {
            actor.Restore(actorBefore);
            var current = _living.InspectNpcGold(request.NpcInstanceId);
            if (current.Gold != npcBefore.Gold)
                _living.SetNpcGoldExact(request.NpcInstanceId, npcBefore.Gold, request.Authority, request.SettledAtUtc);
            return Reject(request, actorBefore.Carried, actorBefore.Carried, npcBefore.Gold, npcBefore.Gold, ex.Message);
        }
    }

    private RuntimeNpcGoldSnapshot? TryInspect(string instanceId)
    {
        try { return _living.InspectNpcGold(instanceId); }
        catch (InvalidOperationException) { return null; }
    }

    private static RuntimeNpcMerchantGoldSettlementResult Reject(RuntimeNpcMerchantGoldSettlementRequest request,
        long actorBefore, long actorAfter, long npcBefore, long npcAfter, string reason) =>
        new(false, request.NpcInstanceId ?? string.Empty, request.RequestId ?? string.Empty,
            request.ServiceKind ?? string.Empty, request.ExactAmount, actorBefore, actorAfter, npcBefore, npcAfter, reason);
}

public sealed class RuntimeNpcMerchantGoldSettlementSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcMerchantGoldSettlementSmokeReport Run()
    {
        var report = new RuntimeNpcMerchantGoldSettlementSmokeReport();
        try
        {
            var type = typeof(RuntimeNpcMerchantGoldSettlementService);
            Require(type.GetMethod(nameof(RuntimeNpcMerchantGoldSettlementService.SettleExact)) is not null, "merchant-gold-settlement-exposed");
            var living = typeof(RuntimeLivingWorldService);
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.InspectNpcGold)) is not null, "npc-gold-inspection-integrated");
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.AdjustNpcGoldExact)) is not null, "npc-gold-mutation-integrated");
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcService)) is not null, "npc-service-completion-integrated");
            report.Checks.AddRange(["merchant-gold-settlement-exposed","npc-gold-inspection-integrated","npc-gold-mutation-integrated","npc-service-completion-integrated"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
