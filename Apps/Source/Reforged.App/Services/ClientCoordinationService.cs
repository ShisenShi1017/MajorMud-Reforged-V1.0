using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Reforged.App.Models;

namespace Reforged.App.Services;

public interface IClientCoordinationService
{
    ClientCoordinationProfile Profile { get; }
    event EventHandler<CoordinatedCommandRequest>? CommandRequested;
    event EventHandler<CoordinationResponse>? ResponseRequested;
    event EventHandler<CoordinationQueryRequest>? QueryRequested;
    Task LoadAsync();
    Task SaveAsync();
    CoordinationParseResult ParseChatCommand(string sender, string message);
    IReadOnlyList<CoordinatedCommandRequest> DispatchLocal(string target, string command, string source = "Local leader");
    IReadOnlyList<CoordinatedCommandRequest> DispatchChat(string sender, string message);
    void PauseAll();
    void ResumeAll();
    void StopAll();
    void MarkCompleted(string sessionId, string result);
    void ResyncAll();
    bool TryHandleControlCommand(string sourceSessionId, string sender, string message);
    void SendResponse(string sourceSessionId, string recipient, CoordinationResponseKind kind, string message);
    bool TryCreateQuery(string sourceSessionId, string sender, string message);
}

public sealed class ClientCoordinationService : IClientCoordinationService
{
    private readonly IApplicationSettingsService _settings;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastDispatch = new(StringComparer.OrdinalIgnoreCase);

    public ClientCoordinationProfile Profile { get; private set; } = CreateDefaultProfile();
    public event EventHandler<CoordinatedCommandRequest>? CommandRequested;
    public event EventHandler<CoordinationResponse>? ResponseRequested;
    public event EventHandler<CoordinationQueryRequest>? QueryRequested;

    public ClientCoordinationService(IApplicationSettingsService settings) => _settings = settings;

    public async Task LoadAsync()
    {
        var path = GetPath();
        if (!File.Exists(path)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<ClientCoordinationProfile>(await File.ReadAllTextAsync(path), _json);
            if (loaded is not null) Profile = loaded;
        }
        catch
        {
            Profile = CreateDefaultProfile();
        }
    }

    public async Task SaveAsync()
    {
        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Profile, _json));
    }

    public CoordinationParseResult ParseChatCommand(string sender, string message)
    {
        if (!ClientMegaMudAutomationGate.IsEnabled)
            return new(false, sender, string.Empty, string.Empty, ClientMegaMudAutomationGate.DisabledReason);

        message = NormalizeCompatibilityMessage(message);
        if (!Profile.Enabled || !Profile.AcceptChatCommands)
            return new(false, sender, string.Empty, string.Empty, "Chat coordination is disabled.");

        var text = message.Trim();
        var prefix = Profile.CommandPrefix.Trim();
        if (prefix.Length == 0 || !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return new(false, sender, string.Empty, string.Empty, "Not a coordination command.");

        if (!IsTrusted(sender, RemoteControlPermission.ExecuteRawCommands) &&
            !IsTrustedForCommand(sender, text[prefix.Length..].Trim()))
            return new(true, sender, string.Empty, string.Empty, "Sender is not trusted for this command.");

        var body = text[prefix.Length..].Trim();
        if (body.Length == 0)
            return new(true, sender, string.Empty, string.Empty, "No command followed the coordination prefix.");

        var target = "followers";
        if (body.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
        {
            var separator = body.IndexOf(':');
            if (separator < 4)
                return new(true, sender, string.Empty, string.Empty, "Use '@do to target: command'.");
            target = body[3..separator].Trim();
            body = body[(separator + 1)..].Trim();
        }
        else
        {
            var separator = body.IndexOf(':');
            if (separator > 0)
            {
                target = body[..separator].Trim();
                body = body[(separator + 1)..].Trim();
            }
        }

        return body.Length == 0
            ? new(true, sender, target, string.Empty, "Command is empty.")
            : new(true, sender, target, body, string.Empty);
    }

    public IReadOnlyList<CoordinatedCommandRequest> DispatchLocal(string target, string command, string source = "Local leader")
        => ClientMegaMudAutomationGate.IsEnabled
            ? Dispatch(new CoordinationInstruction(source, target, command, true, DateTimeOffset.UtcNow))
            : [];

    public IReadOnlyList<CoordinatedCommandRequest> DispatchChat(string sender, string message)
    {
        var parsed = ParseChatCommand(sender, message);
        if (!parsed.Recognized || parsed.Error.Length > 0) return [];
        return Dispatch(new CoordinationInstruction(parsed.Source, parsed.Target, parsed.Command, false, DateTimeOffset.UtcNow));
    }

    public void PauseAll()
    {
        foreach (var session in Profile.Sessions) session.Paused = true;
    }

    public void ResumeAll()
    {
        if (!ClientMegaMudAutomationGate.IsEnabled)
        {
            StopAll();
            return;
        }

        foreach (var session in Profile.Sessions.Where(s => s.Enabled)) session.Paused = false;
    }

    public void StopAll()
    {
        foreach (var session in Profile.Sessions)
        {
            session.Paused = true;
            session.PendingCommands = 0;
            session.LastInstruction = string.Empty;
            session.LastResult = "Stopped by user";
        }
    }

    public void MarkCompleted(string sessionId, string result)
    {
        var session = Profile.Sessions.FirstOrDefault(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        if (session is null) return;
        session.PendingCommands = Math.Max(0, session.PendingCommands - 1);
        session.LastAcknowledgedSequence = session.LastSequence;
        session.LastAcknowledgedAt = DateTimeOffset.UtcNow;
        session.NeedsResync = false;
        session.LastResult = string.IsNullOrWhiteSpace(result) ? "Completed" : result.Trim();
        LogActivity(session.CharacterName, session.DisplayName, session.LastInstruction, session.LastResult);
    }

    public void ResyncAll()
    {
        foreach (var session in Profile.Sessions)
        {
            session.PendingCommands = 0;
            session.LastAcknowledgedSequence = session.LastSequence;
            session.LastAcknowledgedAt = DateTimeOffset.UtcNow;
            session.NeedsResync = false;
            session.LastResult = "Resynchronized";
        }
        LogActivity("Local leader", "all", "resync", "All local queues cleared and synchronized");
    }

    public bool TryHandleControlCommand(string sourceSessionId, string sender, string message)
    {
        if (!ClientMegaMudAutomationGate.IsEnabled) return false;

        var normalized = NormalizeCompatibilityMessage(message).Trim();
        var parsed = ParseChatCommand(sender, normalized);
        if (!parsed.Recognized || parsed.Error.Length > 0) return false;
        var action = parsed.Command.Trim().ToLowerInvariant();
        if (action is not ("pause" or "wait" or "resume" or "ok" or "stop" or "resync" or "sync")) return false;
        if (!IsTrusted(sender, RemoteControlPermission.Movement) && !IsTrusted(sender, RemoteControlPermission.ExecuteRawCommands))
        {
            SendResponse(sourceSessionId, sender, CoordinationResponseKind.Refusal, $"Control action '{action}' is not permitted.");
            return true;
        }
        var targets = ResolveTargets(parsed.Target).Where(s => s.Enabled).ToArray();
        foreach (var session in targets)
        {
            if (action is "pause" or "wait") { session.Paused = true; session.LastResult = $"Paused by {sender}"; }
            else if (action is "resume" or "ok") { session.Paused = false; session.LastResult = $"Resumed by {sender}"; }
            else if (action == "stop") { session.Paused = true; session.PendingCommands = 0; session.LastInstruction = string.Empty; session.LastResult = $"Stopped by {sender}"; }
            else { session.PendingCommands = 0; session.NeedsResync = false; session.LastAcknowledgedSequence = session.LastSequence; session.LastAcknowledgedAt = DateTimeOffset.UtcNow; session.LastResult = $"Resynchronized by {sender}"; }
        }
        LogActivity(sender, parsed.Target, action, $"Applied to {targets.Length} session(s)");
        if (Profile.SendAcknowledgements)
            SendResponse(sourceSessionId, sender, CoordinationResponseKind.Acknowledgement, $"{action.ToUpperInvariant()} applied to {targets.Length} session(s).");
        return true;
    }

    public void SendResponse(string sourceSessionId, string recipient, CoordinationResponseKind kind, string message)
    {
        if (!ClientMegaMudAutomationGate.IsEnabled) return;
        if (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrWhiteSpace(message)) return;
        ResponseRequested?.Invoke(this, new CoordinationResponse(sourceSessionId, recipient.Trim(), kind, message.Trim(), DateTimeOffset.UtcNow));
    }

    public bool TryCreateQuery(string sourceSessionId, string sender, string message)
    {
        if (!ClientMegaMudAutomationGate.IsEnabled) return false;

        var parsed = ParseChatCommand(sender, message);
        if (!parsed.Recognized || parsed.Error.Length > 0) return false;
        var query = parsed.Command.Trim().ToLowerInvariant();
        var permission = query switch
        {
            "status" or "health" => RemoteControlPermission.QueryStatus,
            "where" or "location" => RemoteControlPermission.QueryLocation,
            "inventory" or "inv" => RemoteControlPermission.QueryInventory,
            "experience" or "xp" => RemoteControlPermission.QueryExperience,
            _ => RemoteControlPermission.None
        };
        if (permission == RemoteControlPermission.None) return false;
        if (!IsTrusted(sender, permission))
        {
            SendResponse(sourceSessionId, sender, CoordinationResponseKind.Refusal, $"Query '{query}' is not permitted.");
            return true;
        }
        QueryRequested?.Invoke(this, new CoordinationQueryRequest(sourceSessionId, sender, permission, query, DateTimeOffset.UtcNow));
        return true;
    }

    private IReadOnlyList<CoordinatedCommandRequest> Dispatch(CoordinationInstruction instruction)
    {
        if (!ClientMegaMudAutomationGate.IsEnabled) return [];
        if (!Profile.Enabled || string.IsNullOrWhiteSpace(instruction.Command)) return [];

        var now = instruction.CreatedAt;
        var selected = ResolveTargets(instruction.Target).Where(s => s.Enabled && !s.Paused).ToArray();
        var requests = new List<CoordinatedCommandRequest>();

        foreach (var session in selected)
        {
            if (session.PendingCommands > 0 && _lastDispatch.TryGetValue(session.SessionId, out var pendingSince) &&
                (now - pendingSince).TotalSeconds >= Math.Max(1, Profile.AcknowledgementTimeoutSeconds))
            {
                session.NeedsResync = true;
                session.LastResult = "Acknowledgement timeout — resync required";
                LogActivity(instruction.Source, session.DisplayName, instruction.Command, session.LastResult);
                continue;
            }
            if (session.PendingCommands >= Math.Max(1, Profile.MaximumQueuedCommandsPerSession))
            {
                session.LastResult = "Queue limit reached";
                continue;
            }

            if (_lastDispatch.TryGetValue(session.SessionId, out var last) &&
                (now - last).TotalMilliseconds < Math.Max(0, Profile.MinimumDispatchDelayMs))
            {
                session.LastResult = "Waiting for command delay";
                continue;
            }

            var sequence = ++session.LastSequence;
            var request = new CoordinatedCommandRequest(
                session.SessionId,
                session.CharacterName,
                instruction.Command.Trim(),
                instruction.Source,
                sequence,
                now);

            session.PendingCommands++;
            session.LastController = instruction.Source;
            session.NeedsResync = false;
            session.LastInstruction = request.Command;
            session.LastResult = "Queued";
            _lastDispatch[session.SessionId] = now;
            requests.Add(request);
            LogActivity(instruction.Source, session.CharacterName.Length == 0 ? session.DisplayName : session.CharacterName, request.Command, $"Queued #{sequence}");
            CommandRequested?.Invoke(this, request);
        }

        return requests;
    }

    private IEnumerable<CoordinatedSession> ResolveTargets(string target)
    {
        var normalized = target.Trim();
        if (normalized.Length == 0 || normalized.Equals("followers", StringComparison.OrdinalIgnoreCase))
            return Profile.Sessions.Where(s => s.Role != CoordinationRole.Leader && s.Role != CoordinationRole.Unassigned);
        if (normalized.Equals("all", StringComparison.OrdinalIgnoreCase))
            return Profile.Sessions;
        if (Enum.TryParse<CoordinationRole>(normalized, true, out var role))
            return Profile.Sessions.Where(s => s.Role == role);
        return Profile.Sessions.Where(s =>
            s.DisplayName.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            s.CharacterName.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsTrusted(string playerName, RemoteControlPermission permission)
        => Profile.TrustedControllers.Any(t => t.Enabled &&
                                               t.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase) &&
                                               t.Permissions.HasFlag(permission));

    private bool IsTrustedForCommand(string sender, string command)
    {
        var first = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        var permission = first switch
        {
            "go" or "north" or "south" or "east" or "west" or "up" or "down" or "n" or "s" or "e" or "w" or "u" or "d" => RemoteControlPermission.Movement,
            "attack" or "kill" or "assist" or "flee" => RemoteControlPermission.Combat,
            "cast" or "prepare" => RemoteControlPermission.Spellcasting,
            "use" or "get" or "drop" or "put" or "give" or "wear" or "wield" or "remove" => RemoteControlPermission.ItemUse,
            "invite" => RemoteControlPermission.Invite,
            "status" or "health" => RemoteControlPermission.QueryStatus,
            "where" or "location" => RemoteControlPermission.QueryLocation,
            "inventory" or "inv" => RemoteControlPermission.QueryInventory,
            "experience" or "xp" => RemoteControlPermission.QueryExperience,
            _ => RemoteControlPermission.ExecuteApprovedMacros
        };
        return IsTrusted(sender, permission);
    }

    private static string NormalizeCompatibilityMessage(string message)
    {
        var text = message.Trim();
        return text.ToLowerInvariant() switch
        {
            "@status" or "@health" => "@do status",
            "@where" or "@location" => "@do location",
            "@inventory" or "@inv" => "@do inventory",
            "@experience" or "@xp" => "@do experience",
            "@wait" or "@pause" => "@do pause",
            "@ok" or "@resume" => "@do resume",
            "@stop" => "@do stop",
            "@sync" or "@resync" => "@do resync",
            _ => text
        };
    }

    private void LogActivity(string source, string target, string action, string result)
    {
        Profile.RecentActivity.Insert(0, new CoordinationActivity(DateTimeOffset.Now, source, target, action, result));
        while (Profile.RecentActivity.Count > 100) Profile.RecentActivity.RemoveAt(Profile.RecentActivity.Count - 1);
    }

    private string GetPath() => Path.Combine(_settings.RootPath, "Client", "Profiles", "coordination-default.json");

    private static ClientCoordinationProfile CreateDefaultProfile()
    {
        var profile = new ClientCoordinationProfile();
        profile.Sessions.Add(new CoordinatedSession { DisplayName = "Session 1", Role = CoordinationRole.Leader });
        profile.Sessions.Add(new CoordinatedSession { DisplayName = "Session 2", Role = CoordinationRole.Follower });
        profile.Sessions.Add(new CoordinatedSession { DisplayName = "Session 3", Role = CoordinationRole.Follower });
        return profile;
    }
}
