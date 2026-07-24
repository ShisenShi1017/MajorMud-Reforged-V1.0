using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeEconomicRoundingMode { Floor, Ceiling, NearestAwayFromZero }
public enum RuntimeEconomicServiceType { Training, SpellLearning, AbilityLearning, ItemRepair, Resurrection, Inn, Teleport, GuildJoin, GuildDues, GuildRank, QuestPayment, Scripted, TextblockPayment, CommandReward, WorldEventPayment }
public enum RuntimeEconomicPaymentDirection { ToCharacter, FromCharacter }

public sealed record RuntimeItemValuePolicy(
    int BuyBasisPoints, int SellBasisPoints, int AppraisalBasisPoints, int RepairBasisPoints,
    int DurabilityValueBasisPoints, int ChargeValueBasisPoints,
    RuntimeEconomicRoundingMode RoundingMode, long MinimumBuyUnits, long MinimumSellUnits,
    long MinimumAppraisalUnits, long MinimumRepairUnits,
    bool AllowLimitedItems, bool AllowUniqueItems, bool AllowCursedItems, bool AllowQuestItems);

public sealed record RuntimeItemValueDefinition(
    string ItemId, long BaseValueUnits, bool Stackable, bool Sellable, bool Repairable,
    bool Cursed, bool QuestItem, bool LimitedItem, bool UniqueItem,
    long MaximumDurability, long MaximumCharges, string NativeFormulaStatus);

public sealed record RuntimeItemInstanceValueState(
    string InstanceId, string ItemId, long Quantity, long CurrentDurability,
    long CurrentCharges, long Version);

public sealed record RuntimeItemValueQuote(
    string ItemId, long Quantity, long CanonicalBaseValueUnits, long StackValueUnits,
    long BuyValueUnits, long SellValueUnits, long AppraisalValueUnits,
    long RepairValueUnits, int ConditionBasisPoints, int ChargeBasisPoints,
    string RoundingPolicy);

public static class RuntimeItemValuation
{
    public static RuntimeItemValueQuote Quote(RuntimeItemValueDefinition item, RuntimeItemInstanceValueState state, RuntimeItemValuePolicy policy)
    {
        if (!item.ItemId.Equals(state.ItemId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("item-value-instance-mismatch");
        if (state.Quantity <= 0) throw new InvalidOperationException("item-value-quantity-invalid");
        if (!item.Stackable && state.Quantity != 1) throw new InvalidOperationException("item-value-nonstack-quantity-invalid");
        if (item.BaseValueUnits < 0) throw new InvalidOperationException("item-value-base-invalid");
        if (item.MaximumDurability < 0 || state.CurrentDurability < 0 || state.CurrentDurability > item.MaximumDurability) throw new InvalidOperationException("item-value-durability-invalid");
        if (item.MaximumCharges < 0 || state.CurrentCharges < 0 || state.CurrentCharges > item.MaximumCharges) throw new InvalidOperationException("item-value-charges-invalid");
        ValidatePolicy(policy);
        if (item.LimitedItem && !policy.AllowLimitedItems) throw new InvalidOperationException("item-value-limited-policy-denied");
        if (item.UniqueItem && !policy.AllowUniqueItems) throw new InvalidOperationException("item-value-unique-policy-denied");
        if (item.Cursed && !policy.AllowCursedItems) throw new InvalidOperationException("item-value-cursed-policy-denied");
        if (item.QuestItem && !policy.AllowQuestItems) throw new InvalidOperationException("item-value-quest-policy-denied");

        var stack = checked(item.BaseValueUnits * state.Quantity);
        var conditionBp = item.MaximumDurability == 0 ? 10_000 : checked((int)((state.CurrentDurability * 10_000L) / item.MaximumDurability));
        var chargeBp = item.MaximumCharges == 0 ? 10_000 : checked((int)((state.CurrentCharges * 10_000L) / item.MaximumCharges));
        var adjusted = Scale(stack, Blend(conditionBp, policy.DurabilityValueBasisPoints), policy.RoundingMode);
        adjusted = Scale(adjusted, Blend(chargeBp, policy.ChargeValueBasisPoints), policy.RoundingMode);
        var buy = Math.Max(policy.MinimumBuyUnits, Scale(adjusted, policy.BuyBasisPoints, policy.RoundingMode));
        var sell = item.Sellable ? Math.Max(policy.MinimumSellUnits, Scale(adjusted, policy.SellBasisPoints, policy.RoundingMode)) : 0;
        var appraisal = Math.Max(policy.MinimumAppraisalUnits, Scale(adjusted, policy.AppraisalBasisPoints, policy.RoundingMode));
        var missing = item.MaximumDurability == 0 ? 0 : item.MaximumDurability - state.CurrentDurability;
        var repairBase = item.MaximumDurability == 0 ? 0 : Divide(checked(stack * missing), item.MaximumDurability, policy.RoundingMode);
        var repair = item.Repairable && missing > 0 ? Math.Max(policy.MinimumRepairUnits, Scale(repairBase, policy.RepairBasisPoints, policy.RoundingMode)) : 0;
        return new(item.ItemId, state.Quantity, item.BaseValueUnits, stack, buy, sell, appraisal, repair, conditionBp, chargeBp, policy.RoundingMode.ToString());
    }

    public static long Scale(long value, int basisPoints, RuntimeEconomicRoundingMode mode)
    {
        if (value < 0 || basisPoints < 0) throw new InvalidOperationException("item-value-scale-invalid");
        return Divide(checked(value * basisPoints), 10_000, mode);
    }

    private static int Blend(int remainingBp, int influenceBp)
    {
        if (remainingBp < 0 || remainingBp > 10_000 || influenceBp < 0 || influenceBp > 10_000) throw new InvalidOperationException("item-value-modifier-invalid");
        return checked(10_000 - influenceBp + (int)((remainingBp * (long)influenceBp) / 10_000));
    }

    private static long Divide(long numerator, long denominator, RuntimeEconomicRoundingMode mode)
    {
        if (denominator <= 0) throw new InvalidOperationException("item-value-divisor-invalid");
        var q = numerator / denominator; var r = numerator % denominator;
        if (r == 0 || mode == RuntimeEconomicRoundingMode.Floor) return q;
        if (mode == RuntimeEconomicRoundingMode.Ceiling) return checked(q + 1);
        return checked(q + (checked(r * 2) >= denominator ? 1 : 0));
    }

    private static void ValidatePolicy(RuntimeItemValuePolicy p)
    {
        if (new[]{p.BuyBasisPoints,p.SellBasisPoints,p.AppraisalBasisPoints,p.RepairBasisPoints}.Any(x=>x<0)) throw new InvalidOperationException("item-value-policy-invalid");
        if (p.DurabilityValueBasisPoints is <0 or >10000 || p.ChargeValueBasisPoints is <0 or >10000) throw new InvalidOperationException("item-value-policy-invalid");
        if (new[]{p.MinimumBuyUnits,p.MinimumSellUnits,p.MinimumAppraisalUnits,p.MinimumRepairUnits}.Any(x=>x<0)) throw new InvalidOperationException("item-value-policy-invalid");
    }
}

public sealed record RuntimeEconomicRequirement(
    long? MinimumLevel=null, long? MaximumLevel=null, string? RaceId=null, string? ClassId=null,
    string? AlignmentId=null, string? ReputationId=null, long? MinimumReputation=null,
    string? PrerequisiteSpellId=null, string? PrerequisiteAbilityId=null,
    string? RequiredItemId=null, string? RequiredFlagId=null, string? LocationId=null);

public sealed record RuntimeEconomicRefundPolicy(bool RefundOnEffectFailure, bool RefundOnPersistenceFailure, int CancellationRefundBasisPoints);

public sealed record RuntimeEconomicServiceDefinition(
    string ServiceId, RuntimeEconomicServiceType ServiceType, string ProviderNpcId,
    string? MerchantId, string? ShopId, string? TrainerId, string? SpellId,
    string? AbilityId, string? ItemId, string CurrencyId, long BaseCostUnits,
    int CostBasisPoints, long Quantity, RuntimeEconomicRequirement Requirements,
    RuntimeEconomicRefundPolicy RefundPolicy, bool Available, bool PersistsCharacter,
    bool PersistsProvider, bool PersistsWorld, string NativeFormulaStatus);

public sealed record RuntimeEconomicCharacterSnapshot(
    string CharacterId, long Level, string RaceId, string ClassId, string AlignmentId,
    IReadOnlyDictionary<string,long> Reputation, IReadOnlySet<string> LearnedSpells,
    IReadOnlyDictionary<string,long> AbilityRanks, IReadOnlySet<string> Flags,
    IReadOnlyDictionary<string,long> Items, string LocationId, bool Dead, long Version);

public sealed class RuntimeEconomicCharacterState
{
    private RuntimeEconomicCharacterSnapshot _state;
    public RuntimeEconomicCharacterState(RuntimeEconomicCharacterSnapshot state) { Validate(state); _state=state; }
    internal object Gate { get; } = new();
    public RuntimeEconomicCharacterSnapshot Capture(){ lock(Gate) return _state; }
    internal RuntimeEconomicCharacterSnapshot CaptureUnsafe()=>_state;
    internal void RestoreUnsafe(RuntimeEconomicCharacterSnapshot state)=>_state=state;
    internal void SetUnsafe(RuntimeEconomicCharacterSnapshot state){ Validate(state); _state=state with { Version=checked(_state.Version+1) }; }
    private static void Validate(RuntimeEconomicCharacterSnapshot s){ if(string.IsNullOrWhiteSpace(s.CharacterId)||s.Level<0||s.Version<0) throw new InvalidOperationException("economic-character-state-invalid"); }
}

public sealed record RuntimeEconomicProviderSnapshot(string ProviderNpcId, bool Available, IReadOnlySet<string> ServiceIds, long Version);
public sealed class RuntimeEconomicProviderState
{
    private RuntimeEconomicProviderSnapshot _state;
    public RuntimeEconomicProviderState(RuntimeEconomicProviderSnapshot state){_state=state;}
    internal object Gate {get;}=new();
    internal RuntimeEconomicProviderSnapshot CaptureUnsafe()=>_state;
    internal void RestoreUnsafe(RuntimeEconomicProviderSnapshot s)=>_state=s;
}

public sealed record RuntimeEconomicWorldSnapshot(IReadOnlyDictionary<string,string> Values, long Version);
public sealed class RuntimeEconomicWorldState
{
    private RuntimeEconomicWorldSnapshot _state;
    public RuntimeEconomicWorldState(RuntimeEconomicWorldSnapshot state){_state=state;}
    internal object Gate {get;}=new();
    internal RuntimeEconomicWorldSnapshot CaptureUnsafe()=>_state;
    internal void RestoreUnsafe(RuntimeEconomicWorldSnapshot s)=>_state=s;
    internal void SetUnsafe(RuntimeEconomicWorldSnapshot s)=>_state=s with {Version=checked(_state.Version+1)};
}

public sealed record RuntimeEconomicEffectContext(RuntimeEconomicServiceDefinition Service, RuntimeEconomicCharacterState Character, RuntimeEconomicProviderState Provider, RuntimeEconomicWorldState World, RuntimeItemInstanceValueState? Item);
public interface IRuntimeEconomicServiceEffect { void Apply(RuntimeEconomicEffectContext context); }
public interface IRuntimeEconomicPersistenceCoordinator { void Persist(RuntimeEconomicServiceDefinition service, RuntimeEconomicCharacterSnapshot character, RuntimeEconomicProviderSnapshot provider, RuntimeEconomicWorldSnapshot world); }

public sealed record RuntimeEconomicServiceResult(string TransactionId,string ServiceId,string CharacterId,long ChargedUnits,string CurrencyId,string Status,long CharacterVersion,long ProviderVersion,long WorldVersion);

public sealed class RuntimeEconomicServiceEngine
{
    private readonly Dictionary<string,RuntimeEconomicServiceDefinition> _services;
    private readonly HashSet<string> _appliedReplayKeys=new(StringComparer.OrdinalIgnoreCase);
    private readonly object _replayGate=new();
    public RuntimeEconomicServiceEngine(IEnumerable<RuntimeEconomicServiceDefinition> services)
    {
        _services=services.ToDictionary(x=>x.ServiceId,StringComparer.OrdinalIgnoreCase);
        if(_services.Count==0 || _services.Values.Any(x=>string.IsNullOrWhiteSpace(x.ServiceId)||string.IsNullOrWhiteSpace(x.ProviderNpcId)||string.IsNullOrWhiteSpace(x.CurrencyId)||x.BaseCostUnits<0||x.CostBasisPoints<0||x.Quantity<=0)) throw new InvalidOperationException("economic-service-definition-invalid");
    }

    public RuntimeEconomicServiceResult Execute(string serviceId,string transactionId,RuntimeCurrencyWallet wallet,RuntimeEconomicCharacterState character,RuntimeEconomicProviderState provider,RuntimeEconomicWorldState world,IRuntimeEconomicServiceEffect effect,IRuntimeEconomicPersistenceCoordinator persistence,RuntimeItemInstanceValueState? item=null,RuntimeItemValueDefinition? itemDefinition=null,RuntimeItemValuePolicy? itemPolicy=null)
    {
        if(string.IsNullOrWhiteSpace(transactionId)) throw new InvalidOperationException("economic-service-replay-key-required");
        lock(_replayGate) if(_appliedReplayKeys.Contains(transactionId)) throw new InvalidOperationException("economic-service-replay-detected");
        if(!_services.TryGetValue(serviceId,out var service)) throw new InvalidOperationException("unknown-economic-service-id");
        if(!wallet.CurrencyId.Equals(service.CurrencyId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("economic-service-currency-mismatch");
        lock(wallet.Gate) lock(character.Gate) lock(provider.Gate) lock(world.Gate)
        {
            var wb=wallet.CaptureUnsafe(); var cb=character.CaptureUnsafe(); var pb=provider.CaptureUnsafe(); var worldb=world.CaptureUnsafe();
            try
            {
                ValidateOwnershipAndEligibility(service,cb,pb,item);
                var cost=CalculateCost(service,item,itemDefinition,itemPolicy);
                if(wb.TotalBaseUnits<cost) throw new InvalidOperationException("economic-service-insufficient-funds");
                wallet.SetTotalUnsafe(wb.TotalBaseUnits-cost);
                effect.Apply(new(service,character,provider,world,item));
                persistence.Persist(service,character.CaptureUnsafe(),provider.CaptureUnsafe(),world.CaptureUnsafe());
                lock(_replayGate) _appliedReplayKeys.Add(transactionId);
                var ca=character.CaptureUnsafe(); var pa=provider.CaptureUnsafe(); var wa=world.CaptureUnsafe();
                return new(transactionId,service.ServiceId,ca.CharacterId,cost,service.CurrencyId,"committed",ca.Version,pa.Version,wa.Version);
            }
            catch
            {
                wallet.RestoreUnsafe(wb); character.RestoreUnsafe(cb); provider.RestoreUnsafe(pb); world.RestoreUnsafe(worldb); throw;
            }
        }
    }

    private static long CalculateCost(RuntimeEconomicServiceDefinition s,RuntimeItemInstanceValueState? item,RuntimeItemValueDefinition? def,RuntimeItemValuePolicy? policy)
    {
        if(s.ServiceType==RuntimeEconomicServiceType.ItemRepair)
        {
            if(item is null||def is null||policy is null) throw new InvalidOperationException("repair-service-item-context-required");
            if(!s.ItemId!.Equals(item.ItemId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("repair-service-item-mismatch");
            return RuntimeItemValuation.Quote(def,item,policy).RepairValueUnits;
        }
        return RuntimeItemValuation.Scale(checked(s.BaseCostUnits*s.Quantity),s.CostBasisPoints,s.Requirements is null?RuntimeEconomicRoundingMode.Floor:RuntimeEconomicRoundingMode.NearestAwayFromZero);
    }

    private static void ValidateOwnershipAndEligibility(RuntimeEconomicServiceDefinition s,RuntimeEconomicCharacterSnapshot c,RuntimeEconomicProviderSnapshot p,RuntimeItemInstanceValueState? item)
    {
        if(!s.Available||!p.Available) throw new InvalidOperationException("economic-service-unavailable");
        if(!p.ProviderNpcId.Equals(s.ProviderNpcId,StringComparison.OrdinalIgnoreCase)||!p.ServiceIds.Contains(s.ServiceId)) throw new InvalidOperationException("economic-service-provider-ownership-mismatch");
        var r=s.Requirements;
        if(r.MinimumLevel is { } min&&c.Level<min||r.MaximumLevel is { } max&&c.Level>max) throw new InvalidOperationException("economic-service-level-restricted");
        if(r.RaceId is { } race&&!race.Equals(c.RaceId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("economic-service-race-restricted");
        if(r.ClassId is { } cls&&!cls.Equals(c.ClassId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("economic-service-class-restricted");
        if(r.AlignmentId is { } a&&!a.Equals(c.AlignmentId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("economic-service-alignment-restricted");
        if(r.ReputationId is { } rep&&( !c.Reputation.TryGetValue(rep,out var value)||value<(r.MinimumReputation??0))) throw new InvalidOperationException("economic-service-reputation-restricted");
        if(r.PrerequisiteSpellId is { } spell&&!c.LearnedSpells.Contains(spell)) throw new InvalidOperationException("economic-service-prerequisite-spell-missing");
        if(r.PrerequisiteAbilityId is { } ability&&!c.AbilityRanks.ContainsKey(ability)) throw new InvalidOperationException("economic-service-prerequisite-ability-missing");
        if(r.RequiredItemId is { } req&&(!c.Items.TryGetValue(req,out var q)||q<=0)) throw new InvalidOperationException("economic-service-required-item-missing");
        if(r.RequiredFlagId is { } flag&&!c.Flags.Contains(flag)) throw new InvalidOperationException("economic-service-required-flag-missing");
        if(r.LocationId is { } loc&&!loc.Equals(c.LocationId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("economic-service-location-restricted");
        if(s.ServiceType==RuntimeEconomicServiceType.SpellLearning&&s.SpellId is { } sid&&c.LearnedSpells.Contains(sid)) throw new InvalidOperationException("economic-service-spell-already-known");
        if(s.ServiceType==RuntimeEconomicServiceType.AbilityLearning&&s.AbilityId is { } aid&&c.AbilityRanks.ContainsKey(aid)) throw new InvalidOperationException("economic-service-ability-already-known");
        if(s.ServiceType==RuntimeEconomicServiceType.Resurrection&&!c.Dead) throw new InvalidOperationException("economic-service-character-not-dead");
        if(s.ServiceType==RuntimeEconomicServiceType.ItemRepair&&item is null) throw new InvalidOperationException("repair-service-item-context-required");
    }
}

public sealed record RuntimeEconomicPaymentDefinition(string PaymentId,RuntimeEconomicPaymentDirection Direction,string SourceOwnerId,string DestinationOwnerId,string CurrencyId,long AmountUnits,string RuntimeOwner,bool PersistsWorld);
public sealed record RuntimeEconomicPaymentResult(string ReplayKey,string PaymentId,long AmountUnits,string CurrencyId,string Status);
public sealed class RuntimeEconomicPaymentEngine
{
    private readonly HashSet<string> _applied=new(StringComparer.OrdinalIgnoreCase); private readonly object _gate=new();
    public RuntimeEconomicPaymentResult Execute(RuntimeEconomicPaymentDefinition p,string replayKey,RuntimeCurrencyWallet source,RuntimeCurrencyWallet destination)
    {
        if(string.IsNullOrWhiteSpace(replayKey)) throw new InvalidOperationException("economic-payment-replay-key-required");
        if(p.AmountUnits<=0) throw new InvalidOperationException("economic-payment-amount-invalid");
        if(!source.WalletId.Equals(p.SourceOwnerId,StringComparison.OrdinalIgnoreCase)||!destination.WalletId.Equals(p.DestinationOwnerId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("economic-payment-ownership-mismatch");
        if(!source.CurrencyId.Equals(p.CurrencyId,StringComparison.OrdinalIgnoreCase)||!destination.CurrencyId.Equals(p.CurrencyId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("economic-payment-currency-mismatch");
        lock(_gate){if(_applied.Contains(replayKey)) throw new InvalidOperationException("economic-payment-replay-detected"); RuntimeCurrencyTransferEngine.Transfer(source,destination,p.AmountUnits,replayKey); _applied.Add(replayKey);}
        return new(replayKey,p.PaymentId,p.AmountUnits,p.CurrencyId,"committed");
    }
}

public sealed record RuntimeEconomicServiceStoreSnapshot(int SchemaVersion,IReadOnlyList<string> AppliedReplayKeys,IReadOnlyList<RuntimeItemInstanceValueState> ItemStates);
public static class RuntimeEconomicServicePersistence
{
    public const int CurrentSchemaVersion=1;
    public static string Serialize(RuntimeEconomicServiceStoreSnapshot s)=>JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true});
    public static RuntimeEconomicServiceStoreSnapshot Deserialize(string json){var s=JsonSerializer.Deserialize<RuntimeEconomicServiceStoreSnapshot>(json)??throw new InvalidOperationException("economic-service-persistence-empty"); if(s.SchemaVersion!=CurrentSchemaVersion) throw new InvalidOperationException("economic-service-persistence-version-unsupported"); return s;}
}
