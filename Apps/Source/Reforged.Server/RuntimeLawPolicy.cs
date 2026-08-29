using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeCrimeKind { Assault, Murder, Theft, Trespass, Fraud, Administrative }
public enum RuntimeLawActionKind { RecordCrime, SetWanted, ClearWanted, RequestGuardAlert, RequestFine, RequestJail, RequestConfiscation, RequestReputationChange, Pardon }
public sealed record RuntimeLawZoneRule(string RoomId, bool LawEnforced, string? GuardFactionId = null, int WitnessRadius = 0, TimeSpan? CrimeDecay = null, bool AllowCitizenReports = false);
public sealed record RuntimeCrimeRecord(string CrimeId, string OffenderId, RuntimeCrimeKind Kind, string RoomId, DateTimeOffset OccurredAtUtc, string? VictimId, IReadOnlyList<string> WitnessIds, int Severity, bool Active);
public sealed record RuntimeBountyState(string CharacterId, long Amount, bool Wanted, DateTimeOffset? ExpiresAtUtc, int Revision);
public sealed record RuntimeLawRequest(RuntimeLawActionKind Kind, string CharacterId, string? RelatedId, long Amount, string Reason);
public sealed record RuntimeLawResult(bool Accepted, string Code, IReadOnlyList<RuntimeLawRequest> Requests);

/// <summary>
/// Reforged compatibility / extension law-policy shell. Its crime kinds, witness radius,
/// severity-to-bounty arithmetic, expiration deadlines, fine/jail requests, and pardon model
/// are NOT evidence of native Classic MajorMUD law behavior. Classic parity is bounded by
/// RuntimeClassicLawBountyGuardAuthority and the narrower live Evil-Point/alignment owners.
/// </summary>
public sealed class RuntimeLawPolicyService
{
    private readonly Dictionary<string, RuntimeLawZoneRule> _zones;
    private readonly Dictionary<string, RuntimeCrimeRecord> _crimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeBountyState> _bounties = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeLawPolicyService(IEnumerable<RuntimeLawZoneRule> zones)
    {
        _zones = zones.ToDictionary(x => x.RoomId, StringComparer.OrdinalIgnoreCase);
        if (_zones.Keys.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("law-zone-room-required", nameof(zones));
    }

    public RuntimeLawResult RecordCrime(string crimeId, string offenderId, RuntimeCrimeKind kind, string roomId, DateTimeOffset nowUtc, int severity, string? victimId = null, IEnumerable<string>? witnessIds = null)
    {
        if (string.IsNullOrWhiteSpace(crimeId) || string.IsNullOrWhiteSpace(offenderId) || string.IsNullOrWhiteSpace(roomId) || severity < 1)
            return new(false, "invalid-crime", Array.Empty<RuntimeLawRequest>());
        if (_crimes.ContainsKey(crimeId)) return new(false, "duplicate-crime-id", Array.Empty<RuntimeLawRequest>());
        if (!_zones.TryGetValue(roomId, out var zone) || !zone.LawEnforced)
            return new(false, "law-not-enforced", Array.Empty<RuntimeLawRequest>());
        var witnesses = (witnessIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var crime = new RuntimeCrimeRecord(crimeId, offenderId, kind, roomId, nowUtc, victimId, witnesses, severity, true);
        _crimes.Add(crimeId, crime);
        var current = _bounties.TryGetValue(offenderId, out var existing) ? existing : new RuntimeBountyState(offenderId, 0, false, null, 0);
        var amount = checked(current.Amount + severity);
        var expires = zone.CrimeDecay is { } decay ? nowUtc + decay : current.ExpiresAtUtc;
        _bounties[offenderId] = current with { Amount = amount, Wanted = true, ExpiresAtUtc = expires, Revision = current.Revision + 1 };
        var requests = new List<RuntimeLawRequest>
        {
            new(RuntimeLawActionKind.SetWanted, offenderId, crimeId, amount, "crime-recorded"),
            new(RuntimeLawActionKind.RequestGuardAlert, offenderId, zone.GuardFactionId, severity, "law-zone-guard-alert")
        };
        return new(true, "crime-recorded", requests);
    }

    public RuntimeLawResult ApplyFine(string characterId, long amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(characterId) || amount <= 0) return new(false, "invalid-fine", Array.Empty<RuntimeLawRequest>());
        return new(true, "fine-requested", new[] { new RuntimeLawRequest(RuntimeLawActionKind.RequestFine, characterId, null, amount, reason) });
    }

    public RuntimeLawResult RequestJail(string characterId, string jailRoomId, string reason)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(jailRoomId)) return new(false, "invalid-jail-request", Array.Empty<RuntimeLawRequest>());
        return new(true, "jail-requested", new[] { new RuntimeLawRequest(RuntimeLawActionKind.RequestJail, characterId, jailRoomId, 0, reason) });
    }

    public RuntimeLawResult Pardon(string characterId, string authorityId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(authorityId)) return new(false, "invalid-pardon", Array.Empty<RuntimeLawRequest>());
        if (!_bounties.TryGetValue(characterId, out var state)) return new(false, "no-active-bounty", Array.Empty<RuntimeLawRequest>());
        _bounties[characterId] = state with { Amount = 0, Wanted = false, ExpiresAtUtc = null, Revision = state.Revision + 1 };
        foreach (var crime in _crimes.Values.Where(x => x.Active && string.Equals(x.OffenderId, characterId, StringComparison.OrdinalIgnoreCase)).ToArray())
            _crimes[crime.CrimeId] = crime with { Active = false };
        return new(true, "pardoned", new[] { new RuntimeLawRequest(RuntimeLawActionKind.Pardon, characterId, authorityId, 0, "authorized-pardon") });
    }

    public IReadOnlyList<RuntimeLawRequest> ProcessDecay(DateTimeOffset nowUtc)
    {
        var requests = new List<RuntimeLawRequest>();
        foreach (var state in _bounties.Values.Where(x => x.Wanted && x.ExpiresAtUtc is { } due && nowUtc >= due).OrderBy(x => x.CharacterId, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            _bounties[state.CharacterId] = state with { Amount = 0, Wanted = false, ExpiresAtUtc = null, Revision = state.Revision + 1 };
            requests.Add(new(RuntimeLawActionKind.ClearWanted, state.CharacterId, null, 0, "crime-decay-expired"));
        }
        return requests;
    }

    public RuntimeBountyState? InspectBounty(string characterId) => _bounties.TryGetValue(characterId, out var state) ? state : null;
    public IReadOnlyList<string> ValidateState()
    {
        var errors = new List<string>();
        foreach (var state in _bounties.Values)
        {
            if (state.Amount < 0) errors.Add($"negative-bounty:{state.CharacterId}");
            if (!state.Wanted && state.Amount != 0) errors.Add($"inactive-bounty-has-amount:{state.CharacterId}");
        }
        return errors;
    }
    public string ToJson() => JsonSerializer.Serialize(new { crimes = _crimes.Values, bounties = _bounties.Values }, new JsonSerializerOptions { WriteIndented = true });
}
