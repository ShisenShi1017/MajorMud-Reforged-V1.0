using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeMerchantRestockMode { None, FixedQuantity, FillToTarget, GeneratedInventory }
public enum RuntimeMerchantVisibilityMode { Public, Hidden, RequirementGated }
public enum RuntimeMerchantAvailabilityState { Open, Closed, Suspended, Disabled }

public sealed record RuntimeMerchantScheduleWindow(int StartMinuteInclusive, int EndMinuteExclusive, IReadOnlySet<int> DaysOfWeek)
{
    public bool Contains(DateTimeOffset instant)
    {
        var day=(int)instant.DayOfWeek;
        if(!DaysOfWeek.Contains(day)) return false;
        var minute=instant.Hour*60+instant.Minute;
        return StartMinuteInclusive<=EndMinuteExclusive
            ? minute>=StartMinuteInclusive&&minute<EndMinuteExclusive
            : minute>=StartMinuteInclusive||minute<EndMinuteExclusive;
    }
}

public sealed record RuntimeMerchantAccessPolicy(
    RuntimeMerchantVisibilityMode Visibility,
    IReadOnlySet<string> AllowedRaceIds,
    IReadOnlySet<string> AllowedClassIds,
    IReadOnlySet<string> RequiredFlags,
    string? RequiredLocationId,
    long? MinimumLevel,
    long? MaximumLevel,
    string? RequiredReputationId,
    long? MinimumReputation);

public sealed record RuntimeMerchantRestockEntry(
    string ItemId,
    RuntimeMerchantRestockMode Mode,
    long QuantityPerCycle,
    long TargetQuantity,
    long? MaximumQuantity,
    int Weight,
    bool Enabled);

public sealed record RuntimeMerchantLifecycleDefinition(
    string MerchantId,
    string ShopId,
    IReadOnlySet<string> CategoryIds,
    IReadOnlyList<RuntimeMerchantScheduleWindow> Schedule,
    RuntimeMerchantAccessPolicy Access,
    IReadOnlyList<RuntimeMerchantRestockEntry> RestockEntries,
    TimeSpan RestockInterval,
    int GeneratedSelectionCount,
    bool PersistStock,
    bool PersistScheduleState,
    string RuntimeOwner,
    string NativeDependency);

public sealed record RuntimeMerchantCustomerContext(
    string CharacterId,
    string RaceId,
    string ClassId,
    long Level,
    string LocationId,
    IReadOnlySet<string> Flags,
    IReadOnlyDictionary<string,long> Reputation);

public sealed record RuntimeMerchantLifecycleSnapshot(
    string MerchantId,
    RuntimeMerchantAvailabilityState Availability,
    DateTimeOffset LastRestockAt,
    DateTimeOffset NextRestockAt,
    long RestockVersion,
    long LifecycleVersion,
    long StockVersion,
    IReadOnlyDictionary<string,long> Quantities,
    IReadOnlySet<string> GeneratedItemIds,
    IReadOnlySet<string> AppliedReplayKeys);

public sealed record RuntimeMerchantAccessResult(bool Visible,bool Open,string Reason);
public sealed record RuntimeMerchantRestockResult(string MerchantId,string ReplayKey,int CyclesApplied,IReadOnlyDictionary<string,long> AddedQuantities,DateTimeOffset NextRestockAt,long Version,string Status);

public sealed class RuntimeMerchantLifecycleState
{
    private readonly object _gate=new();
    private RuntimeMerchantAvailabilityState _availability;
    private DateTimeOffset _lastRestockAt;
    private DateTimeOffset _nextRestockAt;
    private long _restockVersion;
    private long _lifecycleVersion;
    private HashSet<string> _generatedItemIds;
    private HashSet<string> _appliedReplayKeys;

    public RuntimeMerchantLifecycleState(RuntimeMerchantLifecycleDefinition definition,DateTimeOffset initializedAt,RuntimeMerchantAvailabilityState availability=RuntimeMerchantAvailabilityState.Open)
    {
        Definition=definition;
        ValidateDefinition(definition);
        _availability=availability;
        _lastRestockAt=initializedAt;
        _nextRestockAt=definition.RestockInterval>TimeSpan.Zero?initializedAt+definition.RestockInterval:DateTimeOffset.MaxValue;
        _generatedItemIds=new(StringComparer.OrdinalIgnoreCase);
        _appliedReplayKeys=new(StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeMerchantLifecycleDefinition Definition{get;}
    internal object Gate=>_gate;

    public RuntimeMerchantLifecycleSnapshot Capture(RuntimeMerchantStock stock)
    {
        lock(_gate) lock(stock.Gate) return CaptureUnsafe(stock);
    }

    internal RuntimeMerchantLifecycleSnapshot CaptureUnsafe(RuntimeMerchantStock stock)
    {
        var quantities=stock.CaptureUnsafe().Quantities;
        return new(Definition.MerchantId,_availability,_lastRestockAt,_nextRestockAt,_restockVersion,_lifecycleVersion,stock.CaptureUnsafe().Version,new Dictionary<string,long>(quantities,StringComparer.OrdinalIgnoreCase),new HashSet<string>(_generatedItemIds,StringComparer.OrdinalIgnoreCase),new HashSet<string>(_appliedReplayKeys,StringComparer.OrdinalIgnoreCase));
    }

    internal void RestoreUnsafe(RuntimeMerchantStock stock,RuntimeMerchantLifecycleSnapshot snapshot)
    {
        if(!Definition.MerchantId.Equals(snapshot.MerchantId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("merchant-lifecycle-snapshot-mismatch");
        stock.RestoreUnsafe(new(snapshot.MerchantId,new Dictionary<string,long>(snapshot.Quantities,StringComparer.OrdinalIgnoreCase),snapshot.StockVersion));
        _availability=snapshot.Availability;
        _lastRestockAt=snapshot.LastRestockAt;
        _nextRestockAt=snapshot.NextRestockAt;
        _restockVersion=snapshot.RestockVersion;
        _lifecycleVersion=snapshot.LifecycleVersion;
        _generatedItemIds=new(snapshot.GeneratedItemIds,StringComparer.OrdinalIgnoreCase);
        _appliedReplayKeys=new(snapshot.AppliedReplayKeys,StringComparer.OrdinalIgnoreCase);
    }

    internal RuntimeMerchantAvailabilityState AvailabilityUnsafe=>_availability;
    internal DateTimeOffset NextRestockAtUnsafe=>_nextRestockAt;
    internal bool HasReplayKeyUnsafe(string replayKey)=>_appliedReplayKeys.Contains(replayKey);
    internal void AddGeneratedUnsafe(string itemId)=>_generatedItemIds.Add(itemId);
    internal void CommitRestockUnsafe(DateTimeOffset now,int cycles,string replayKey)
    {
        _lastRestockAt=now;
        _nextRestockAt=_nextRestockAt.AddTicks(checked(Definition.RestockInterval.Ticks*cycles));
        _restockVersion=checked(_restockVersion+cycles);
        _lifecycleVersion=checked(_lifecycleVersion+1);
        _appliedReplayKeys.Add(replayKey);
    }
    internal void SetAvailabilityUnsafe(RuntimeMerchantAvailabilityState value){_availability=value;_lifecycleVersion=checked(_lifecycleVersion+1);}

    private static void ValidateDefinition(RuntimeMerchantLifecycleDefinition d)
    {
        if(string.IsNullOrWhiteSpace(d.MerchantId)||string.IsNullOrWhiteSpace(d.ShopId)||string.IsNullOrWhiteSpace(d.RuntimeOwner)) throw new InvalidOperationException("merchant-lifecycle-definition-invalid");
        if(d.RestockInterval<TimeSpan.Zero||d.GeneratedSelectionCount<0) throw new InvalidOperationException("merchant-restock-policy-invalid");
        foreach(var w in d.Schedule) if(w.StartMinuteInclusive<0||w.StartMinuteInclusive>=1440||w.EndMinuteExclusive<0||w.EndMinuteExclusive>1440||w.DaysOfWeek.Any(x=>x<0||x>6)) throw new InvalidOperationException("merchant-schedule-invalid");
        var duplicate=d.RestockEntries.GroupBy(x=>x.ItemId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1);
        if(duplicate||d.RestockEntries.Any(x=>string.IsNullOrWhiteSpace(x.ItemId)||x.QuantityPerCycle<0||x.TargetQuantity<0||x.MaximumQuantity is <0||x.Weight<0)) throw new InvalidOperationException("merchant-restock-entry-invalid");
    }
}

public static class RuntimeMerchantLifecycleEngine
{
    public static RuntimeMerchantAccessResult EvaluateAccess(RuntimeMerchantLifecycleState lifecycle,RuntimeMerchantCustomerContext customer,DateTimeOffset now)
    {
        lock(lifecycle.Gate)
        {
            var d=lifecycle.Definition;
            var availability=lifecycle.AvailabilityUnsafe;
            if(availability is RuntimeMerchantAvailabilityState.Disabled or RuntimeMerchantAvailabilityState.Suspended) return new(false,false,"merchant-unavailable");
            var a=d.Access;
            if(a.RequiredLocationId is { } location&&!location.Equals(customer.LocationId,StringComparison.OrdinalIgnoreCase)) return new(false,false,"merchant-location-restricted");
            if(a.MinimumLevel is { } min&&customer.Level<min||a.MaximumLevel is { } max&&customer.Level>max) return new(false,false,"merchant-level-restricted");
            if(a.AllowedRaceIds.Count>0&&!a.AllowedRaceIds.Contains(customer.RaceId)) return new(false,false,"merchant-race-restricted");
            if(a.AllowedClassIds.Count>0&&!a.AllowedClassIds.Contains(customer.ClassId)) return new(false,false,"merchant-class-restricted");
            if(a.RequiredFlags.Any(x=>!customer.Flags.Contains(x))) return new(false,false,"merchant-flag-restricted");
            if(a.RequiredReputationId is { } rep&&(!customer.Reputation.TryGetValue(rep,out var value)||value<(a.MinimumReputation??0))) return new(false,false,"merchant-reputation-restricted");
            var scheduled=d.Schedule.Count==0||d.Schedule.Any(x=>x.Contains(now));
            var open=availability==RuntimeMerchantAvailabilityState.Open&&scheduled;
            var visible=a.Visibility==RuntimeMerchantVisibilityMode.Public||open;
            return new(visible,open,open?"merchant-open":"merchant-closed");
        }
    }

    public static RuntimeMerchantRestockResult Restock(RuntimeMerchantLifecycleState lifecycle,RuntimeMerchantStock stock,DateTimeOffset now,string replayKey,IRuntimeMerchantLifecyclePersistence persistence)
    {
        if(string.IsNullOrWhiteSpace(replayKey)) throw new InvalidOperationException("merchant-restock-replay-key-required");
        if(!stock.Merchant.MerchantId.Equals(lifecycle.Definition.MerchantId,StringComparison.OrdinalIgnoreCase)||!stock.Merchant.ShopId.Equals(lifecycle.Definition.ShopId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("merchant-lifecycle-ownership-mismatch");
        lock(lifecycle.Gate) lock(stock.Gate)
        {
            var before=lifecycle.CaptureUnsafe(stock);
            if(before.AppliedReplayKeys.Contains(replayKey)) throw new InvalidOperationException("merchant-restock-replay-detected");
            try
            {
                if(lifecycle.Definition.RestockInterval<=TimeSpan.Zero||now<before.NextRestockAt) return new(before.MerchantId,replayKey,0,new Dictionary<string,long>(),before.NextRestockAt,before.LifecycleVersion,"not-due");
                var cycles=checked((int)Math.Min(int.MaxValue,1+(now-before.NextRestockAt).Ticks/lifecycle.Definition.RestockInterval.Ticks));
                var added=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
                for(var cycle=0;cycle<cycles;cycle++) ApplyCycle(lifecycle,stock,replayKey,cycle,added);
                lifecycle.CommitRestockUnsafe(now,cycles,replayKey);
                var after=lifecycle.CaptureUnsafe(stock);
                persistence.Persist(after);
                return new(after.MerchantId,replayKey,cycles,added,after.NextRestockAt,after.LifecycleVersion,"committed");
            }
            catch{lifecycle.RestoreUnsafe(stock,before);throw;}
        }
    }

    private static void ApplyCycle(RuntimeMerchantLifecycleState lifecycle,RuntimeMerchantStock stock,string replayKey,int cycle,Dictionary<string,long> added)
    {
        var entries=lifecycle.Definition.RestockEntries.Where(x=>x.Enabled&&x.Mode!=RuntimeMerchantRestockMode.None).ToArray();
        var selected=SelectGenerated(entries,lifecycle.Definition.GeneratedSelectionCount,replayKey,cycle);
        foreach(var entry in entries)
        {
            if(entry.Mode==RuntimeMerchantRestockMode.GeneratedInventory&&!selected.Contains(entry.ItemId)) continue;
            var current=stock.AvailableUnsafe(entry.ItemId);
            if(current==long.MaxValue) continue;
            long add=entry.Mode switch
            {
                RuntimeMerchantRestockMode.FixedQuantity=>entry.QuantityPerCycle,
                RuntimeMerchantRestockMode.FillToTarget=>Math.Max(0,entry.TargetQuantity-current),
                RuntimeMerchantRestockMode.GeneratedInventory=>entry.QuantityPerCycle,
                _=>0
            };
            var cap=entry.MaximumQuantity??stock.Resolve(entry.ItemId).MaximumQuantity;
            if(cap is { } maximum) add=Math.Min(add,Math.Max(0,maximum-current));
            if(add<=0) continue;
            stock.AddRestockUnsafe(entry.ItemId,add);
            added[entry.ItemId]=checked((added.TryGetValue(entry.ItemId,out var prior)?prior:0)+add);
            if(entry.Mode==RuntimeMerchantRestockMode.GeneratedInventory) lifecycle.AddGeneratedUnsafe(entry.ItemId);
        }
    }

    private static HashSet<string> SelectGenerated(RuntimeMerchantRestockEntry[] entries,int count,string replayKey,int cycle)
    {
        var pool=entries.Where(x=>x.Mode==RuntimeMerchantRestockMode.GeneratedInventory&&x.Weight>0).ToList();
        var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(var draw=0;draw<count&&pool.Count>0;draw++)
        {
            long total=pool.Sum(x=>(long)x.Weight);
            var roll=(long)(StableHash($"{replayKey}:{cycle}:{draw}")%(ulong)total);
            long cursor=0; RuntimeMerchantRestockEntry? chosen=null;
            foreach(var e in pool){cursor+=e.Weight;if(roll<cursor){chosen=e;break;}}
            chosen??=pool[^1]; result.Add(chosen.ItemId); pool.Remove(chosen);
        }
        return result;
    }

    private static ulong StableHash(string value){const ulong offset=14695981039346656037UL,prime=1099511628211UL;var h=offset;foreach(var c in value){h^=c;h*=prime;}return h;}
}

public interface IRuntimeMerchantLifecyclePersistence { void Persist(RuntimeMerchantLifecycleSnapshot snapshot); }

public static class RuntimeMerchantStockLifecycleExtensions
{
    public static void AddRestockUnsafe(this RuntimeMerchantStock stock,string itemId,long quantity)
    {
        var definition=stock.Resolve(itemId);
        if(definition.Unlimited)return;
        var snap=stock.CaptureUnsafe();
        var current=snap.Quantities[itemId];
        var next=checked(current+quantity);
        if(definition.MaximumQuantity is { } max&&next>max) throw new InvalidOperationException("merchant-stock-limit-exceeded");
        var quantities=new Dictionary<string,long>(snap.Quantities,StringComparer.OrdinalIgnoreCase){[itemId]=next};
        stock.RestoreUnsafe(new(snap.MerchantId,quantities,checked(snap.Version+1)));
    }
}

public sealed record RuntimeMerchantLifecycleStore(int SchemaVersion,IReadOnlyList<RuntimeMerchantLifecycleSnapshot> Merchants);
public static class RuntimeMerchantLifecyclePersistence
{
    public const int CurrentSchemaVersion=1;
    public static string Serialize(RuntimeMerchantLifecycleStore store)=>JsonSerializer.Serialize(store,new JsonSerializerOptions{WriteIndented=true});
    public static RuntimeMerchantLifecycleStore Deserialize(string json){var store=JsonSerializer.Deserialize<RuntimeMerchantLifecycleStore>(json)??throw new InvalidOperationException("merchant-lifecycle-persistence-empty");if(store.SchemaVersion!=CurrentSchemaVersion)throw new InvalidOperationException("merchant-lifecycle-persistence-version-unsupported");return store;}
}
