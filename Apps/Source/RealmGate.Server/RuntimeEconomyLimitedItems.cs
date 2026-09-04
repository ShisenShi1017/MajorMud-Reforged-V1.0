using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeLimitedItemState { Reserved, Active, Released, Destroyed, Recycled, RespawnPending }
public enum RuntimeLimitedItemSourceType { Character, Merchant, LootCorpse, Quest, Script, WorldEvent, System }
public enum RuntimeLimitedItemReleaseReason { Transfer, Sale, LootClaim, Consumption, Destruction, Expiry, Reset, Administrative }

public sealed record RuntimeLimitedItemPolicy(
    string ItemId,
    long WorldLimit,
    bool UniquePerOwner,
    bool RecycleOnDestroy,
    bool RespawnEnabled,
    TimeSpan RespawnDelay,
    bool MerchantEligible,
    bool LootEligible,
    string RuntimeOwner,
    string NativeDependency);

public sealed record RuntimeLimitedItemReservationRequest(
    string ReservationId,
    string ReplayKey,
    string ItemId,
    long Quantity,
    RuntimeLimitedItemSourceType SourceType,
    string SourceId,
    string IntendedOwnerId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc);

public sealed record RuntimeLimitedItemAllocation(
    string AllocationId,
    string ItemId,
    long Quantity,
    RuntimeLimitedItemState State,
    RuntimeLimitedItemSourceType OwnerType,
    string OwnerId,
    string ReservationId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    DateTimeOffset? RespawnDueUtc,
    long Version);

public sealed record RuntimeLimitedItemLedgerSnapshot(
    string LedgerId,
    long Version,
    IReadOnlyList<RuntimeLimitedItemAllocation> Allocations,
    IReadOnlySet<string> AppliedReplayKeys);

public sealed record RuntimeLimitedItemResult(
    string ItemId,
    long Quantity,
    string AllocationId,
    string ReplayKey,
    RuntimeLimitedItemState State,
    long LedgerVersion,
    string Status);

public sealed class RuntimeLimitedItemLedger
{
    private readonly object _gate=new();
    private readonly IReadOnlyDictionary<string,RuntimeLimitedItemPolicy> _policies;
    private Dictionary<string,RuntimeLimitedItemAllocation> _allocations;
    private HashSet<string> _appliedReplayKeys;
    private long _version;

    public RuntimeLimitedItemLedger(string ledgerId,IEnumerable<RuntimeLimitedItemPolicy> policies)
    {
        if(string.IsNullOrWhiteSpace(ledgerId)) throw new InvalidOperationException("limited-item-ledger-id-required");
        LedgerId=ledgerId;
        _policies=policies.ToDictionary(x=>x.ItemId,StringComparer.OrdinalIgnoreCase);
        if(_policies.Count==0||_policies.Values.Any(x=>string.IsNullOrWhiteSpace(x.ItemId)||x.WorldLimit<=0||x.RespawnDelay<TimeSpan.Zero||string.IsNullOrWhiteSpace(x.RuntimeOwner))) throw new InvalidOperationException("limited-item-policy-invalid");
        _allocations=new(StringComparer.OrdinalIgnoreCase);
        _appliedReplayKeys=new(StringComparer.OrdinalIgnoreCase);
    }

    private RuntimeLimitedItemLedger(string ledgerId,IEnumerable<RuntimeLimitedItemPolicy> policies,RuntimeLimitedItemLedgerSnapshot snapshot):this(ledgerId,policies)
    {
        RestoreUnsafe(snapshot);
    }

    public string LedgerId{get;}
    internal object Gate=>_gate;
    public RuntimeLimitedItemPolicy ResolvePolicy(string itemId)=>_policies.TryGetValue(itemId,out var p)?p:throw new InvalidOperationException("unknown-limited-item-id");
    public RuntimeLimitedItemLedgerSnapshot Capture(){lock(_gate)return CaptureUnsafe();}
    public static RuntimeLimitedItemLedger Restore(string ledgerId,IEnumerable<RuntimeLimitedItemPolicy> policies,RuntimeLimitedItemLedgerSnapshot snapshot)=>new(ledgerId,policies,snapshot);

    internal RuntimeLimitedItemLedgerSnapshot CaptureUnsafe()=>new(LedgerId,_version,_allocations.Values.OrderBy(x=>x.AllocationId,StringComparer.OrdinalIgnoreCase).ToArray(),new HashSet<string>(_appliedReplayKeys,StringComparer.OrdinalIgnoreCase));
    internal void RestoreUnsafe(RuntimeLimitedItemLedgerSnapshot snapshot)
    {
        if(!LedgerId.Equals(snapshot.LedgerId,StringComparison.OrdinalIgnoreCase)||snapshot.Version<0) throw new InvalidOperationException("limited-item-snapshot-mismatch");
        if(snapshot.Allocations.Select(x=>x.AllocationId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=snapshot.Allocations.Count) throw new InvalidOperationException("limited-item-allocation-duplicate");
        _allocations=snapshot.Allocations.ToDictionary(x=>x.AllocationId,StringComparer.OrdinalIgnoreCase);
        _appliedReplayKeys=new(snapshot.AppliedReplayKeys,StringComparer.OrdinalIgnoreCase);
        _version=snapshot.Version;
        ValidateWorldLimitsUnsafe();
    }

    internal bool HasReplayUnsafe(string replayKey)=>_appliedReplayKeys.Contains(replayKey);
    internal RuntimeLimitedItemAllocation ResolveAllocationUnsafe(string allocationId)=>_allocations.TryGetValue(allocationId,out var a)?a:throw new InvalidOperationException("limited-item-allocation-not-found");
    internal void PutUnsafe(RuntimeLimitedItemAllocation allocation){_allocations[allocation.AllocationId]=allocation;_version=checked(_version+1);}
    internal void CommitReplayUnsafe(string replayKey)=>_appliedReplayKeys.Add(replayKey);
    internal long VersionUnsafe=>_version;

    internal long CountCommittedUnsafe(string itemId)=>_allocations.Values.Where(x=>x.ItemId.Equals(itemId,StringComparison.OrdinalIgnoreCase)&&x.State is RuntimeLimitedItemState.Reserved or RuntimeLimitedItemState.Active or RuntimeLimitedItemState.RespawnPending).Aggregate(0L,(a,x)=>checked(a+x.Quantity));
    internal bool OwnerHasActiveUnsafe(string itemId,string ownerId)=>_allocations.Values.Any(x=>x.ItemId.Equals(itemId,StringComparison.OrdinalIgnoreCase)&&x.OwnerId.Equals(ownerId,StringComparison.OrdinalIgnoreCase)&&x.State==RuntimeLimitedItemState.Active);
    internal void ValidateWorldLimitsUnsafe()
    {
        foreach(var p in _policies.Values) if(CountCommittedUnsafe(p.ItemId)>p.WorldLimit) throw new InvalidOperationException("limited-item-world-limit-exceeded");
    }
}

public static class RuntimeLimitedItemEngine
{
    public static RuntimeLimitedItemResult Reserve(RuntimeLimitedItemLedger ledger,RuntimeLimitedItemReservationRequest request,IRuntimeLimitedItemPersistence persistence)
    {
        ValidateIdentity(request.ReservationId,request.ReplayKey,request.SourceId,request.IntendedOwnerId);
        if(request.Quantity<=0||request.ExpiresUtc<request.CreatedUtc) throw new InvalidOperationException("limited-item-reservation-invalid");
        lock(ledger.Gate)
        {
            if(ledger.HasReplayUnsafe(request.ReplayKey)) throw new InvalidOperationException("limited-item-replay-detected");
            var before=ledger.CaptureUnsafe();
            try
            {
                var policy=ledger.ResolvePolicy(request.ItemId);
                ValidateSourceEligibility(policy,request.SourceType);
                if(policy.UniquePerOwner&&ledger.OwnerHasActiveUnsafe(request.ItemId,request.IntendedOwnerId)) throw new InvalidOperationException("limited-item-owner-unique-limit");
                if(checked(ledger.CountCommittedUnsafe(request.ItemId)+request.Quantity)>policy.WorldLimit) throw new InvalidOperationException("limited-item-world-limit-exceeded");
                var allocationId=$"limited:{request.ItemId}:{request.ReservationId}";
                var allocation=new RuntimeLimitedItemAllocation(allocationId,request.ItemId,request.Quantity,RuntimeLimitedItemState.Reserved,request.SourceType,request.SourceId,request.ReservationId,request.CreatedUtc,request.CreatedUtc,null,0);
                ledger.PutUnsafe(allocation); ledger.CommitReplayUnsafe(request.ReplayKey); ledger.ValidateWorldLimitsUnsafe();
                persistence.Persist(ledger.CaptureUnsafe());
                return new(request.ItemId,request.Quantity,allocationId,request.ReplayKey,allocation.State,ledger.VersionUnsafe,"limited-item-reserved");
            }
            catch{ledger.RestoreUnsafe(before);throw;}
        }
    }

    public static RuntimeLimitedItemResult Activate(RuntimeLimitedItemLedger ledger,string allocationId,RuntimeLimitedItemSourceType ownerType,string ownerId,string replayKey,DateTimeOffset now,IRuntimeLimitedItemPersistence persistence)
        =>Transition(ledger,allocationId,replayKey,now,persistence,a=>
        {
            if(a.State!=RuntimeLimitedItemState.Reserved&&a.State!=RuntimeLimitedItemState.RespawnPending) throw new InvalidOperationException("limited-item-not-activatable");
            var p=ledger.ResolvePolicy(a.ItemId);
            if(p.UniquePerOwner&&ledger.OwnerHasActiveUnsafe(a.ItemId,ownerId)) throw new InvalidOperationException("limited-item-owner-unique-limit");
            return a with{State=RuntimeLimitedItemState.Active,OwnerType=ownerType,OwnerId=ownerId,UpdatedUtc=now,RespawnDueUtc=null,Version=checked(a.Version+1)};
        },"limited-item-activated");

    public static RuntimeLimitedItemResult Release(RuntimeLimitedItemLedger ledger,string allocationId,RuntimeLimitedItemReleaseReason reason,string replayKey,DateTimeOffset now,IRuntimeLimitedItemPersistence persistence)
        =>Transition(ledger,allocationId,replayKey,now,persistence,a=>
        {
            if(a.State is RuntimeLimitedItemState.Released or RuntimeLimitedItemState.Destroyed or RuntimeLimitedItemState.Recycled) throw new InvalidOperationException("limited-item-already-released");
            return a with{State=RuntimeLimitedItemState.Released,OwnerType=RuntimeLimitedItemSourceType.System,OwnerId=$"release:{reason}",UpdatedUtc=now,RespawnDueUtc=null,Version=checked(a.Version+1)};
        },"limited-item-released");

    public static RuntimeLimitedItemResult Destroy(RuntimeLimitedItemLedger ledger,string allocationId,string replayKey,DateTimeOffset now,IRuntimeLimitedItemPersistence persistence)
        =>Transition(ledger,allocationId,replayKey,now,persistence,a=>
        {
            if(a.State!=RuntimeLimitedItemState.Active) throw new InvalidOperationException("limited-item-not-destroyable");
            var p=ledger.ResolvePolicy(a.ItemId);
            var state=p.RespawnEnabled?RuntimeLimitedItemState.RespawnPending:p.RecycleOnDestroy?RuntimeLimitedItemState.Recycled:RuntimeLimitedItemState.Destroyed;
            var due=p.RespawnEnabled?(DateTimeOffset?)(now+p.RespawnDelay):null;
            return a with{State=state,OwnerType=RuntimeLimitedItemSourceType.System,OwnerId="system",UpdatedUtc=now,RespawnDueUtc=due,Version=checked(a.Version+1)};
        },"limited-item-destroyed");

    public static IReadOnlyList<RuntimeLimitedItemResult> ProcessRespawns(RuntimeLimitedItemLedger ledger,DateTimeOffset now,string replayKey,IRuntimeLimitedItemPersistence persistence)
    {
        if(string.IsNullOrWhiteSpace(replayKey)) throw new InvalidOperationException("limited-item-replay-key-required");
        lock(ledger.Gate)
        {
            if(ledger.HasReplayUnsafe(replayKey)) throw new InvalidOperationException("limited-item-replay-detected");
            var before=ledger.CaptureUnsafe();
            try
            {
                var results=new List<RuntimeLimitedItemResult>();
                foreach(var a in before.Allocations.Where(x=>x.State==RuntimeLimitedItemState.RespawnPending&&x.RespawnDueUtc<=now))
                {
                    var next=a with{State=RuntimeLimitedItemState.Reserved,OwnerType=RuntimeLimitedItemSourceType.System,OwnerId="respawn-pool",UpdatedUtc=now,RespawnDueUtc=null,Version=checked(a.Version+1)};
                    ledger.PutUnsafe(next);
                    results.Add(new(next.ItemId,next.Quantity,next.AllocationId,replayKey,next.State,ledger.VersionUnsafe,"limited-item-respawn-ready"));
                }
                ledger.CommitReplayUnsafe(replayKey);ledger.ValidateWorldLimitsUnsafe();persistence.Persist(ledger.CaptureUnsafe());return results;
            }
            catch{ledger.RestoreUnsafe(before);throw;}
        }
    }

    public static void ValidateMerchantIntegration(RuntimeLimitedItemLedger ledger,string itemId)
    {
        if(!ledger.ResolvePolicy(itemId).MerchantEligible) throw new InvalidOperationException("limited-item-merchant-ineligible");
    }
    public static void ValidateLootIntegration(RuntimeLimitedItemLedger ledger,string itemId)
    {
        if(!ledger.ResolvePolicy(itemId).LootEligible) throw new InvalidOperationException("limited-item-loot-ineligible");
    }

    private static RuntimeLimitedItemResult Transition(RuntimeLimitedItemLedger ledger,string allocationId,string replayKey,DateTimeOffset now,IRuntimeLimitedItemPersistence persistence,Func<RuntimeLimitedItemAllocation,RuntimeLimitedItemAllocation> mutate,string status)
    {
        ValidateIdentity(allocationId,replayKey,"transition","transition");
        lock(ledger.Gate)
        {
            if(ledger.HasReplayUnsafe(replayKey)) throw new InvalidOperationException("limited-item-replay-detected");
            var before=ledger.CaptureUnsafe();
            try
            {
                var next=mutate(ledger.ResolveAllocationUnsafe(allocationId)); ledger.PutUnsafe(next);ledger.CommitReplayUnsafe(replayKey);ledger.ValidateWorldLimitsUnsafe();persistence.Persist(ledger.CaptureUnsafe());
                return new(next.ItemId,next.Quantity,next.AllocationId,replayKey,next.State,ledger.VersionUnsafe,status);
            }
            catch{ledger.RestoreUnsafe(before);throw;}
        }
    }
    private static void ValidateSourceEligibility(RuntimeLimitedItemPolicy p,RuntimeLimitedItemSourceType source)
    {
        if(source==RuntimeLimitedItemSourceType.Merchant&&!p.MerchantEligible) throw new InvalidOperationException("limited-item-merchant-ineligible");
        if(source==RuntimeLimitedItemSourceType.LootCorpse&&!p.LootEligible) throw new InvalidOperationException("limited-item-loot-ineligible");
    }
    private static void ValidateIdentity(params string[] values){if(values.Any(string.IsNullOrWhiteSpace))throw new InvalidOperationException("limited-item-identity-required");}
}

public interface IRuntimeLimitedItemPersistence { void Persist(RuntimeLimitedItemLedgerSnapshot snapshot); }
public sealed record RuntimeLimitedItemStore(int SchemaVersion,IReadOnlyList<RuntimeLimitedItemLedgerSnapshot> Ledgers);
public static class RuntimeLimitedItemPersistence
{
    public const int CurrentSchemaVersion=1;
    public static string Serialize(IEnumerable<RuntimeLimitedItemLedgerSnapshot> ledgers)=>JsonSerializer.Serialize(new RuntimeLimitedItemStore(CurrentSchemaVersion,ledgers.ToArray()));
    public static RuntimeLimitedItemStore Deserialize(string json)
    {
        var store=JsonSerializer.Deserialize<RuntimeLimitedItemStore>(json)??throw new InvalidOperationException("limited-item-store-invalid");
        if(store.SchemaVersion!=CurrentSchemaVersion) throw new InvalidOperationException("limited-item-store-schema-unsupported");
        return store;
    }
}
