using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeCommunicationChannel
{
    Say,
    Whisper,
    Tell,
    Shout,
    Yell,
    Emote,
    Pose,
    Party,
    Guild,
    Global,
    System,
    AdministrativeBroadcast
}

public sealed record RuntimeCommunicationActor(
    string ActorId,
    string DisplayName,
    string? RoomId,
    bool Connected,
    bool Deafened,
    bool Silenced,
    bool Hidden,
    bool Administrator,
    string? GuildId,
    IReadOnlySet<string>? AdjacentRoomIds = null);

/// <summary>
/// Reforged compatibility communication policy shell. MaximumMessageLength and the
/// FloodWindowSeconds/FloodMaximumMessages pair are extension policy, NOT recovered
/// Classic MajorMUD MAXPAGES/page/telegram semantics and receive zero Classic parity credit.
/// </summary>
public sealed record RuntimeCommunicationPolicy(
    int MaximumMessageLength,
    int FloodWindowSeconds,
    int FloodMaximumMessages,
    bool AllowGlobal,
    bool AllowGuild,
    bool AllowParty,
    bool HiddenSendersRevealToAdministrators)
{
    public static RuntimeCommunicationPolicy CompatibilityShellDefault { get; } = new(
        MaximumMessageLength: 512,
        FloodWindowSeconds: 10,
        FloodMaximumMessages: 6,
        AllowGlobal: false,
        AllowGuild: true,
        AllowParty: true,
        HiddenSendersRevealToAdministrators: true);
}

public sealed record RuntimeCommunicationRequest(
    RuntimeCommunicationChannel Channel,
    string SenderId,
    string Message,
    string? TargetActorId = null);

public sealed record RuntimeCommunicationDelivery(
    string RecipientActorId,
    string SenderPresentation,
    RuntimeCommunicationChannel Channel,
    string RenderedText);

public sealed record RuntimeCommunicationResult(
    bool Succeeded,
    string? FailureCode,
    IReadOnlyList<RuntimeCommunicationDelivery> Deliveries);

public sealed class RuntimeCommunicationService
{
    private readonly object _gate = new();
    private readonly RuntimeCommunicationPolicy _policy;
    private readonly RuntimePartyCoordinationService? _parties;
    private readonly RuntimeOrganizationService? _organizations;
    private readonly Dictionary<string, RuntimeCommunicationActor> _actors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _ignored = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<DateTimeOffset>> _flood = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCommunicationService(RuntimeCommunicationPolicy? policy = null, RuntimePartyCoordinationService? parties = null, RuntimeOrganizationService? organizations = null)
    {
        _policy = policy ?? RuntimeCommunicationPolicy.CompatibilityShellDefault;
        _parties = parties;
        _organizations = organizations;
        if (_policy.MaximumMessageLength < 1) throw new ArgumentOutOfRangeException(nameof(policy));
        if (_policy.FloodWindowSeconds < 1 || _policy.FloodMaximumMessages < 1) throw new ArgumentOutOfRangeException(nameof(policy));
    }

    public void UpsertActor(RuntimeCommunicationActor actor)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId)) throw new ArgumentException("actor-id-required");
        if (string.IsNullOrWhiteSpace(actor.DisplayName)) throw new ArgumentException("display-name-required");
        lock (_gate) _actors[actor.ActorId] = actor;
    }

    public void SetIgnored(string recipientActorId, string senderActorId, bool ignored)
    {
        lock (_gate)
        {
            if (!_ignored.TryGetValue(recipientActorId, out var set))
                _ignored[recipientActorId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ignored) set.Add(senderActorId); else set.Remove(senderActorId);
        }
    }

    public RuntimeCommunicationResult Send(RuntimeCommunicationRequest request, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_actors.TryGetValue(request.SenderId, out var sender) || !sender.Connected)
                return Fail("communication-sender-unavailable");
            var message = (request.Message ?? string.Empty).Trim();
            if (message.Length == 0) return Fail("communication-message-empty");
            if (message.Length > _policy.MaximumMessageLength) return Fail("communication-message-too-long");
            if (sender.Silenced && request.Channel is not RuntimeCommunicationChannel.System)
                return Fail("communication-sender-silenced");
            if (!CheckFlood(sender.ActorId, nowUtc)) return Fail("communication-flood-limited");
            if (request.Channel == RuntimeCommunicationChannel.Global && !_policy.AllowGlobal) return Fail("communication-global-disabled");
            if (request.Channel == RuntimeCommunicationChannel.Guild && !_policy.AllowGuild) return Fail("communication-guild-disabled");
            if (request.Channel == RuntimeCommunicationChannel.Party && !_policy.AllowParty) return Fail("communication-party-disabled");
            if (request.Channel == RuntimeCommunicationChannel.AdministrativeBroadcast && !sender.Administrator)
                return Fail("communication-administrator-required");

            var recipients = ResolveRecipients(request, sender);
            if (recipients.FailureCode is not null) return Fail(recipients.FailureCode);
            var deliveries = recipients.ActorIds
                .Where(id => _actors.TryGetValue(id, out var recipient) && recipient.Connected && !recipient.Deafened)
                .Where(id => request.Channel != RuntimeCommunicationChannel.Tell
                    || !_ignored.TryGetValue(id, out var set)
                    || !set.Contains(sender.ActorId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Select(id => BuildDelivery(request.Channel, sender, _actors[id], message))
                .ToArray();
            return new(true, null, deliveries);
        }
    }

    public IReadOnlyList<string> Validate()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var pair in _ignored)
            {
                if (!_actors.ContainsKey(pair.Key)) failures.Add("ignore-recipient-missing:" + pair.Key);
                foreach (var senderId in pair.Value)
                    if (!_actors.ContainsKey(senderId)) failures.Add("ignore-sender-missing:" + senderId);
            }
            return failures;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(_actors.Values.OrderBy(x => x.ActorId), new JsonSerializerOptions { WriteIndented = true });

    private (IReadOnlyList<string> ActorIds, string? FailureCode) ResolveRecipients(RuntimeCommunicationRequest request, RuntimeCommunicationActor sender)
    {
        switch (request.Channel)
        {
            case RuntimeCommunicationChannel.Say:
            case RuntimeCommunicationChannel.Emote:
            case RuntimeCommunicationChannel.Pose:
                return (_actors.Values.Where(x => string.Equals(x.RoomId, sender.RoomId, StringComparison.OrdinalIgnoreCase)).Select(x => x.ActorId).ToArray(), null);
            case RuntimeCommunicationChannel.Whisper:
                return (Array.Empty<string>(), "communication-whisper-native-unresolved");
            case RuntimeCommunicationChannel.Tell:
                return (Array.Empty<string>(), "communication-tell-native-unresolved");
            case RuntimeCommunicationChannel.Yell:
                var adjacent = sender.AdjacentRoomIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return (_actors.Values.Where(x => x.Connected
                    && (string.Equals(x.RoomId, sender.RoomId, StringComparison.OrdinalIgnoreCase)
                        || (x.RoomId is not null && adjacent.Contains(x.RoomId))))
                    .Select(x => x.ActorId).ToArray(), null);
            case RuntimeCommunicationChannel.Shout:
                return (Array.Empty<string>(), "communication-shout-native-range-unresolved");
            case RuntimeCommunicationChannel.Party:
                if (_parties is null) return (Array.Empty<string>(), "communication-party-router-unavailable");
                var route = _parties.RoutePartyMessage(sender.ActorId);
                return route.Succeeded ? (route.RecipientActorIds, null) : (Array.Empty<string>(), route.FailureCode);
            case RuntimeCommunicationChannel.Guild:
                if (_organizations is not null)
                {
                    var guildRoute = _organizations.RouteGuildMessage(sender.ActorId);
                    return guildRoute.Succeeded ? (guildRoute.RecipientActorIds, null) : (Array.Empty<string>(), guildRoute.FailureCode);
                }
                if (string.IsNullOrWhiteSpace(sender.GuildId)) return (Array.Empty<string>(), "communication-sender-not-in-guild");
                return (_actors.Values.Where(x => string.Equals(x.GuildId, sender.GuildId, StringComparison.OrdinalIgnoreCase)).Select(x => x.ActorId).ToArray(), null);
            case RuntimeCommunicationChannel.Global:
            case RuntimeCommunicationChannel.System:
                return (_actors.Values.Where(x => x.Connected).Select(x => x.ActorId).ToArray(), null);
            case RuntimeCommunicationChannel.AdministrativeBroadcast:
                return (_actors.Values.Where(x => x.Connected).Select(x => x.ActorId).ToArray(), null);
            default:
                return (Array.Empty<string>(), "communication-channel-unsupported");
        }
    }

    private RuntimeCommunicationDelivery BuildDelivery(RuntimeCommunicationChannel channel, RuntimeCommunicationActor sender, RuntimeCommunicationActor recipient, string message)
    {
        var presentation = sender.Hidden && !(recipient.Administrator && _policy.HiddenSendersRevealToAdministrators)
            ? "Someone"
            : sender.DisplayName;
        var text = channel switch
        {
            RuntimeCommunicationChannel.Emote => $"{presentation} {message}",
            RuntimeCommunicationChannel.Pose => message.Replace("{name}", presentation, StringComparison.OrdinalIgnoreCase),
            RuntimeCommunicationChannel.AdministrativeBroadcast => $"[ADMIN] {message}",
            RuntimeCommunicationChannel.System => $"[SYSTEM] {message}",
            _ => $"{presentation}: {message}"
        };
        return new(recipient.ActorId, presentation, channel, text);
    }

    private bool CheckFlood(string actorId, DateTimeOffset nowUtc)
    {
        if (!_flood.TryGetValue(actorId, out var queue)) _flood[actorId] = queue = new Queue<DateTimeOffset>();
        var threshold = nowUtc.AddSeconds(-_policy.FloodWindowSeconds);
        while (queue.Count > 0 && queue.Peek() <= threshold) queue.Dequeue();
        if (queue.Count >= _policy.FloodMaximumMessages) return false;
        queue.Enqueue(nowUtc);
        return true;
    }

    private static RuntimeCommunicationResult Fail(string code) => new(false, code, Array.Empty<RuntimeCommunicationDelivery>());
}
