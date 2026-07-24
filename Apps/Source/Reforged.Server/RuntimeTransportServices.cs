using System.Text.Json;
namespace Reforged.Server;
public enum RuntimeTransportState { Waiting, Boarding, InTransit, Arrived, Cancelled, Failed }
public enum RuntimeTransportRequestKind { ChargeFare, MoveActor, Announce, StartCombatRestriction, EndCombatRestriction }
public sealed record RuntimeTransportStop(string StopId, string RoomId, TimeSpan DwellTime);
public sealed record RuntimeTransportDefinition(string TransportId, IReadOnlyList<RuntimeTransportStop> Stops, long Fare, int Capacity, bool AllowInCombat, bool Persistent, TimeSpan TravelTime);
public sealed record RuntimeTransportActor(string CharacterId, string RoomId, bool Connected, bool InCombat, int Level, IReadOnlySet<string> Flags);
public sealed record RuntimeTransportRequest(RuntimeTransportRequestKind Kind, string CharacterId, string? RelatedId, long Amount, string Reason);
public sealed record RuntimeTransportResult(bool Accepted, string Code, RuntimeTransportState State, IReadOnlyList<RuntimeTransportRequest> Requests);
public sealed record RuntimeTransportTrip(string TransportId, string OriginStopId, string DestinationStopId, DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, RuntimeTransportState State, IReadOnlyList<string> Passengers, long Revision);
public sealed class RuntimeTransportService
{
    private readonly Dictionary<string,RuntimeTransportDefinition> _definitions;
    private readonly Dictionary<string,RuntimeTransportTrip> _trips=new(StringComparer.OrdinalIgnoreCase);
    public RuntimeTransportService(IEnumerable<RuntimeTransportDefinition> definitions)
    {
        _definitions=definitions.ToDictionary(x=>x.TransportId,StringComparer.OrdinalIgnoreCase);
        if(_definitions.Keys.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("transport-id-required",nameof(definitions));
        if(_definitions.Values.Any(x=>x.Capacity<=0||x.Fare<0||x.TravelTime<TimeSpan.Zero||x.Stops.Count<2)) throw new ArgumentException("invalid-transport-definition",nameof(definitions));
        if(_definitions.Values.SelectMany(x=>x.Stops).Any(x=>string.IsNullOrWhiteSpace(x.StopId)||string.IsNullOrWhiteSpace(x.RoomId))) throw new ArgumentException("invalid-transport-stop",nameof(definitions));
    }
    public RuntimeTransportResult Board(string transportId,string destinationStopId,RuntimeTransportActor actor,DateTimeOffset now)
    {
        if(!_definitions.TryGetValue(transportId,out var def)) return Reject("unknown-transport");
        if(string.IsNullOrWhiteSpace(actor.CharacterId)||!actor.Connected) return Reject("invalid-or-disconnected-actor");
        if(actor.InCombat&&!def.AllowInCombat) return Reject("combat-blocks-transport");
        var origin=def.Stops.FirstOrDefault(x=>string.Equals(x.RoomId,actor.RoomId,StringComparison.OrdinalIgnoreCase));
        var destination=def.Stops.FirstOrDefault(x=>string.Equals(x.StopId,destinationStopId,StringComparison.OrdinalIgnoreCase));
        if(origin is null) return Reject("not-at-transport-stop");
        if(destination is null||string.Equals(origin.StopId,destination.StopId,StringComparison.OrdinalIgnoreCase)) return Reject("invalid-destination");
        if(_trips.TryGetValue(transportId,out var existing)&&existing.State==RuntimeTransportState.InTransit) return Reject("transport-in-transit");
        var passengers=existing?.Passengers.ToList()??new List<string>();
        if(passengers.Contains(actor.CharacterId,StringComparer.OrdinalIgnoreCase)) return Reject("already-boarded");
        if(passengers.Count>=def.Capacity) return Reject("transport-full");
        passengers.Add(actor.CharacterId);
        var trip=new RuntimeTransportTrip(transportId,origin.StopId,destination.StopId,now,now+def.TravelTime,RuntimeTransportState.Boarding,passengers, (existing?.Revision??0)+1);
        _trips[transportId]=trip;
        var requests=new List<RuntimeTransportRequest>();
        if(def.Fare>0) requests.Add(new(RuntimeTransportRequestKind.ChargeFare,actor.CharacterId,transportId,def.Fare,"transport-fare"));
        requests.Add(new(RuntimeTransportRequestKind.Announce,actor.CharacterId,destination.StopId,0,"transport-boarded"));
        return new(true,"boarded",trip.State,requests);
    }
    public RuntimeTransportResult Depart(string transportId,DateTimeOffset now)
    {
        if(!_definitions.TryGetValue(transportId,out var def)||!_trips.TryGetValue(transportId,out var trip)) return Reject("transport-not-ready");
        if(trip.Passengers.Count==0||trip.State!=RuntimeTransportState.Boarding) return Reject("transport-not-ready");
        trip=trip with { DepartureUtc=now, ArrivalUtc=now+def.TravelTime, State=RuntimeTransportState.InTransit, Revision=trip.Revision+1}; _trips[transportId]=trip;
        return new(true,"departed",trip.State,trip.Passengers.Select(x=>new RuntimeTransportRequest(RuntimeTransportRequestKind.StartCombatRestriction,x,transportId,0,"transport-in-transit")).ToArray());
    }
    public RuntimeTransportResult Advance(string transportId,DateTimeOffset now)
    {
        if(!_definitions.TryGetValue(transportId,out var def)||!_trips.TryGetValue(transportId,out var trip)) return Reject("unknown-trip");
        if(trip.State!=RuntimeTransportState.InTransit) return Reject("transport-not-in-transit");
        if(now<trip.ArrivalUtc) return Reject("transport-not-arrived");
        var destination=def.Stops.First(x=>string.Equals(x.StopId,trip.DestinationStopId,StringComparison.OrdinalIgnoreCase));
        var requests=new List<RuntimeTransportRequest>();
        foreach(var passenger in trip.Passengers){requests.Add(new(RuntimeTransportRequestKind.MoveActor,passenger,destination.RoomId,0,"transport-arrival"));requests.Add(new(RuntimeTransportRequestKind.EndCombatRestriction,passenger,transportId,0,"transport-arrived"));}
        _trips[transportId]=trip with {State=RuntimeTransportState.Arrived,Revision=trip.Revision+1};
        return new(true,"arrived",RuntimeTransportState.Arrived,requests);
    }
    public RuntimeTransportResult Disconnect(string transportId,string characterId)
    {
        if(!_trips.TryGetValue(transportId,out var trip)) return Reject("unknown-trip");
        if(trip.State==RuntimeTransportState.InTransit) return Reject("disconnect-preserves-passenger-until-arrival");
        var passengers=trip.Passengers.Where(x=>!string.Equals(x,characterId,StringComparison.OrdinalIgnoreCase)).ToArray();
        _trips[transportId]=trip with {Passengers=passengers,Revision=trip.Revision+1};
        return new(true,"passenger-removed",trip.State,Array.Empty<RuntimeTransportRequest>());
    }
    public RuntimeTransportTrip? Inspect(string id)=>_trips.TryGetValue(id,out var x)?x:null;
    public IReadOnlyList<string> ValidateState()=>_trips.Values.Where(x=>x.Passengers.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=x.Passengers.Count).Select(x=>"duplicate-passenger:"+x.TransportId).ToArray();
    public string ToJson()=>JsonSerializer.Serialize(new{trips=_trips.Values},new JsonSerializerOptions{WriteIndented=true});
    private static RuntimeTransportResult Reject(string code)=>new(false,code,RuntimeTransportState.Failed,Array.Empty<RuntimeTransportRequest>());
}
