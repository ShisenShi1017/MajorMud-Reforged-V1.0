using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Reforged.App.Models;

namespace Reforged.App.Services;

/// <summary>
/// Reforged-owned party coordinator. It consumes observed/server-authoritative party state,
/// returns at most one bounded action per evaluation, and never invents a party member,
/// target, spell, or online player. Remote @ commands remain owned by ClientCoordinationService
/// and its per-controller permission flags.
/// </summary>
public sealed class ReforgedPartyAutomationService
{
    private readonly string _profileRoot;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Dictionary<string, PartyRuntimeState> _runtime = new(StringComparer.OrdinalIgnoreCase);

    private sealed class PartyRuntimeState
    {
        public bool WaitingForLeader { get; set; }
        public bool FollowIssued { get; set; }
        public string LastFingerprint { get; set; } = string.Empty;
        public DateTimeOffset LastActionAt { get; set; }
        public DateTimeOffset LastReconnectAt { get; set; }
        public Dictionary<string, DateTimeOffset> LastBuffAt { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public ReforgedPartyAutomationService(string? rootPath = null)
    {
        var root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MajorMUD Reforged");
        _profileRoot = Path.Combine(root, "Client", "PartyAutomation");
    }

    public ReforgedPartyAutomationProfile LoadProfile(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var key = BuildProfileKey(session);
        var path = ProfilePath(key);
        if (!File.Exists(path)) return NewProfile(session, key);
        try
        {
            var profile = JsonSerializer.Deserialize<ReforgedPartyAutomationProfile>(File.ReadAllText(path), _json)
                          ?? NewProfile(session, key);
            Normalize(profile, session, key);
            return profile;
        }
        catch (JsonException)
        {
            BackupCorrupt(path);
            return NewProfile(session, key);
        }
        catch (IOException) { return NewProfile(session, key); }
        catch (UnauthorizedAccessException) { return NewProfile(session, key); }
    }

    public void SaveProfile(ClientSessionConnection session, ReforgedPartyAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var key = BuildProfileKey(session);
        Normalize(profile, session, key);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_profileRoot);
        var path = ProfilePath(key);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, _json), new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    public void MarkReconnect(ClientSessionConnection session, DateTimeOffset at)
    {
        var state = State(session.SessionId);
        state.LastReconnectAt = at;
        state.WaitingForLeader = false;
        state.FollowIssued = false;
        state.LastFingerprint = string.Empty;
        state.LastActionAt = DateTimeOffset.MinValue;
    }

    public void MarkExecuted(ClientSessionConnection session, ReforgedPartyAutomationPlan plan, DateTimeOffset at)
    {
        var state = State(session.SessionId);
        state.LastActionAt = at;
        switch (plan.Kind)
        {
            case ReforgedPartyActionKind.WaitForLeader:
                state.WaitingForLeader = true;
                state.FollowIssued = false;
                break;
            case ReforgedPartyActionKind.ResumeWithLeader:
            case ReforgedPartyActionKind.FollowLeader:
                state.WaitingForLeader = false;
                state.FollowIssued = true;
                break;
            case ReforgedPartyActionKind.BuffMember:
                if (!string.IsNullOrWhiteSpace(plan.Target))
                    state.LastBuffAt[$"buff:{plan.Target}"] = at;
                break;
        }
        state.LastFingerprint = Fingerprint(session);
    }

    public void ClearRuntime(string sessionId) => _runtime.Remove(sessionId);

    public ReforgedPartyAutomationPlan Plan(
        ClientSessionConnection session,
        ReforgedPartyAutomationProfile profile,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var p = profile.Preferences;
        if (!p.Enabled) return None("Advanced party automation is disabled.");

        var runtime = State(session.SessionId);
        if (runtime.LastActionAt != DateTimeOffset.MinValue &&
            now - runtime.LastActionAt < TimeSpan.FromSeconds(Math.Max(1, p.ActionHoldSeconds)))
            return None("Holding briefly after the last party action to avoid command thrash.");

        if (!IsFresh(session.PartyUpdatedAt, now, p.PartyStateMaximumAgeSeconds))
            return RefreshParty("Refreshing the party roster before making a coordination decision.");

        if (p.ResyncAfterReconnect && runtime.LastReconnectAt != DateTimeOffset.MinValue &&
            session.PartyUpdatedAt is { } partyAt && partyAt < runtime.LastReconnectAt)
            return RefreshParty("Refreshing party state after reconnect before resuming automation.");

        var support = PlanSupport(session, profile, runtime, now);
        if (support.HasAction) return support;

        var rank = PlanRank(session, profile);
        if (rank.HasAction) return rank;

        if (p.Role == ReforgedPartyRole.LeadParty)
        {
            var invite = PlanInvite(session, profile, now);
            if (invite.HasAction) return invite;
            return None("Leader mode: current observed members do not require a party action.");
        }

        if (p.Role == ReforgedPartyRole.SupportOnly)
            return None("Support-only mode: no healing, cure, rescue, or rank action is required.");

        var leader = p.LeaderName.Trim();
        if (leader.Length == 0)
            return None("Follower mode requires an explicit leader name; no leader was guessed.");

        var leaderMember = session.PartyMembers.FirstOrDefault(m =>
            m.Name.Equals(leader, StringComparison.OrdinalIgnoreCase));
        if (leaderMember is null)
        {
            runtime.FollowIssued = false;
            if (p.WaitWhenLeaderMissing && !runtime.WaitingForLeader)
                return Command(ReforgedPartyActionKind.WaitForLeader, p.WaitCommand, leader,
                    $"Configured leader {leader} is absent from the fresh party roster.", p.CommandCooldownSeconds,
                    pauseRoute: p.PauseRouteWhileLeaderMissing);
            if (p.RegroupWhenSeparated)
                return Command(ReforgedPartyActionKind.Regroup, p.RegroupCommand, leader,
                    $"Configured leader {leader} is absent; requesting regroup information.", p.CommandCooldownSeconds,
                    pauseRoute: p.PauseRouteWhileLeaderMissing);
            return None($"Configured leader {leader} is absent; no follow/assist target was guessed.");
        }

        if (runtime.WaitingForLeader && p.ResumeWhenLeaderReturns)
            return Command(ReforgedPartyActionKind.ResumeWithLeader,
                Apply(p.ResumeCommandTemplate, "leader", leader), leader,
                $"Configured leader {leader} returned to the observed party roster.", p.CommandCooldownSeconds,
                resumeRoute: p.ResumeRouteWhenLeaderReturns);

        if (p.AutoFollowLeader && !runtime.FollowIssued)
            return Command(ReforgedPartyActionKind.FollowLeader,
                Apply(p.FollowCommandTemplate, "leader", leader), leader,
                $"Following configured leader {leader}.", p.CommandCooldownSeconds);

        if (p.AutoAssistLeader && session.InCombat)
            return Command(ReforgedPartyActionKind.AssistLeader,
                Apply(p.AssistCommandTemplate, "leader", leader), leader,
                $"Assisting configured leader {leader} while combat is active.", p.CommandCooldownSeconds);

        runtime.LastFingerprint = Fingerprint(session);
        return None($"Leader {leader} is present; no party action is currently required.");
    }

    private ReforgedPartyAutomationPlan PlanSupport(
        ClientSessionConnection session,
        ReforgedPartyAutomationProfile profile,
        PartyRuntimeState runtime,
        DateTimeOffset now)
    {
        var p = profile.Preferences;
        var members = session.PartyMembers
            .Where(m => !m.Name.Equals(session.CharacterName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (p.CurePoisonedMembers)
        {
            var poisoned = members.OrderBy(m => m.HealthPercent).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(m => m.Poisoned);
            if (poisoned is not null)
            {
                var command = Apply(p.CurePoisonCommandTemplate, "target", poisoned.Name);
                if (CanUseCommand(session, profile, command))
                    return Command(ReforgedPartyActionKind.CureMember, command, poisoned.Name,
                        $"Party roster marks {poisoned.Name} poisoned; applying configured cure.", p.CommandCooldownSeconds,
                        isSpell: IsCast(command));
            }
        }

        var wounded = members.OrderBy(m => m.HealthPercent).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        if (wounded is not null && p.AutoRescueMembers && session.InCombat && wounded.HealthPercent <= p.RescueBelowPercent)
            return Command(ReforgedPartyActionKind.RescueMember,
                Apply(p.RescueCommandTemplate, "target", wounded.Name), wounded.Name,
                $"{wounded.Name} is at {wounded.HealthPercent}% HP; configured rescue threshold reached.", p.CommandCooldownSeconds);

        if (wounded is not null && p.AutoHealMembers && wounded.HealthPercent <= p.HealBelowPercent)
        {
            var command = Apply(p.HealCommandTemplate, "target", wounded.Name);
            if (CanUseCommand(session, profile, command))
                return Command(ReforgedPartyActionKind.HealMember, command, wounded.Name,
                    $"Healing {wounded.Name} at {wounded.HealthPercent}% HP.", p.CommandCooldownSeconds,
                    isSpell: IsCast(command));
        }

        if (p.AutoBuffMembers && (!p.PartyBuffOnlyOutOfCombat || !session.InCombat) &&
            session.ManaPercent >= p.PartyBuffMinimumManaPercent && !string.IsNullOrWhiteSpace(p.PartyBuffCommandTemplate))
        {
            var allowedTargets = SplitNames(p.PartyBuffTargetNames);
            var candidates = members
                .Where(member => allowedTargets.Count == 0 || allowedTargets.Contains(member.Name, StringComparer.OrdinalIgnoreCase))
                .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var member in candidates)
            {
                var key = $"buff:{member.Name}";
                if (runtime.LastBuffAt.TryGetValue(key, out var last) &&
                    now - last < TimeSpan.FromMinutes(Math.Clamp(p.PartyBuffIntervalMinutes, 1, 1440))) continue;
                var command = Apply(p.PartyBuffCommandTemplate, "target", member.Name);
                if (!CanUseCommand(session, profile, command)) continue;
                return Command(ReforgedPartyActionKind.BuffMember, command, member.Name,
                    $"Applying configured timed party buff to {member.Name}; per-member buff state is not guessed.",
                    p.CommandCooldownSeconds, isSpell: IsCast(command));
            }
        }
        return None(string.Empty);
    }

    private ReforgedPartyAutomationPlan PlanRank(
        ClientSessionConnection session,
        ReforgedPartyAutomationProfile profile)
    {
        var p = profile.Preferences;
        if (!p.MaintainPreferredRank || string.IsNullOrWhiteSpace(session.CharacterName)) return None(string.Empty);
        var self = session.PartyMembers.FirstOrDefault(m =>
            m.Name.Equals(session.CharacterName, StringComparison.OrdinalIgnoreCase));
        if (self is null || string.IsNullOrWhiteSpace(self.Rank)) return None(string.Empty);
        var preferred = p.PreferredRank.Trim();
        if (preferred.Length == 0 || self.Rank.Contains(preferred, StringComparison.OrdinalIgnoreCase)) return None(string.Empty);
        var back = preferred.Contains("back", StringComparison.OrdinalIgnoreCase);
        var command = back ? p.BackRankCommand : p.FrontRankCommand;
        return Command(ReforgedPartyActionKind.SetRank, command, session.CharacterName,
            $"Observed rank '{self.Rank}' differs from configured '{preferred}'.", p.CommandCooldownSeconds);
    }

    private ReforgedPartyAutomationPlan PlanInvite(
        ClientSessionConnection session,
        ReforgedPartyAutomationProfile profile,
        DateTimeOffset now)
    {
        var p = profile.Preferences;
        if (!p.AutoInviteNamedMembers) return None(string.Empty);
        var names = SplitNames(p.InviteNames);
        if (names.Count == 0) return None(string.Empty);
        if (!IsFresh(session.OnlinePlayersUpdatedAt, now, p.OnlineStateMaximumAgeSeconds))
            return new ReforgedPartyAutomationPlan
            {
                Kind = ReforgedPartyActionKind.RefreshOnlinePlayers,
                RefreshOnlinePlayers = true,
                Reason = "Refreshing the online-player roster before inviting configured members.",
                CooldownSeconds = 2
            };

        var present = session.PartyMembers.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var online = session.OnlinePlayers.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var target = names.FirstOrDefault(name => !present.Contains(name) && online.Contains(name));
        if (string.IsNullOrWhiteSpace(target)) return None("Configured invite members are already grouped or are not in the fresh online roster.");
        var command = Apply(p.InviteCommandTemplate, "target", target);
        return Command(ReforgedPartyActionKind.InviteMember, command, target,
            $"Inviting configured online member {target}; identity came from the fresh WHO roster.", p.CommandCooldownSeconds);
    }

    public bool CanUseCommand(
        ClientSessionConnection session,
        ReforgedPartyAutomationProfile profile,
        string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (!IsCast(command)) return true;
        var spell = session.LearnedSpellDetails
            .Where(s => command.Contains(s.Name, StringComparison.OrdinalIgnoreCase) ||
                        command.Contains(s.CanonicalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Name.Length)
            .FirstOrDefault();
        if (spell is null) return false;
        var reserve = session.ManaMaximum <= 0 ? 0 :
            (int)Math.Ceiling(session.ManaMaximum * Math.Clamp(profile.Preferences.ReserveManaPercent, 0, 100) / 100d);
        return session.ManaCurrent >= spell.ManaCost && session.ManaCurrent - spell.ManaCost >= reserve;
    }

    private static ReforgedPartyAutomationPlan RefreshParty(string reason) => new()
    {
        Kind = ReforgedPartyActionKind.RefreshParty,
        RefreshParty = true,
        Reason = reason,
        CooldownSeconds = 2
    };

    private static ReforgedPartyAutomationPlan Command(
        ReforgedPartyActionKind kind,
        string command,
        string target,
        string reason,
        int cooldown,
        bool pauseRoute = false,
        bool resumeRoute = false,
        bool isSpell = false) => new()
    {
        Kind = kind,
        Command = command?.Trim() ?? string.Empty,
        Target = target,
        Reason = reason,
        CooldownSeconds = Math.Clamp(cooldown, 1, 60),
        PauseRoute = pauseRoute,
        ResumeRoute = resumeRoute,
        IsSpellCommand = isSpell
    };

    private static ReforgedPartyAutomationPlan None(string reason) => new()
    {
        Kind = ReforgedPartyActionKind.None,
        Reason = reason
    };

    private PartyRuntimeState State(string sessionId)
    {
        if (!_runtime.TryGetValue(sessionId, out var state))
            _runtime[sessionId] = state = new PartyRuntimeState();
        return state;
    }

    private static bool IsFresh(DateTimeOffset? at, DateTimeOffset now, int seconds) =>
        at is { } value && now - value <= TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 3600));

    private static bool IsCast(string command) => command.TrimStart().StartsWith("cast ", StringComparison.OrdinalIgnoreCase);

    private static string Apply(string template, string token, string value)
    {
        var text = (template ?? string.Empty).Trim();
        if (text.Length == 0) return string.Empty;
        var marker = "{" + token + "}";
        if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            return text.Replace(marker, value, StringComparison.OrdinalIgnoreCase);
        return $"{text} {value}".Trim();
    }

    private static IReadOnlyList<string> SplitNames(string raw) => (raw ?? string.Empty)
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(s => s.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string Fingerprint(ClientSessionConnection session) => string.Join("|", session.PartyMembers
        .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
        .Select(m => $"{m.Name}:{m.HealthPercent}:{m.Rank}:{m.State}:{m.Poisoned}"));

    private static ReforgedPartyAutomationProfile NewProfile(ClientSessionConnection session, string key) => new()
    {
        ProfileKey = key,
        CharacterName = session.CharacterName,
        ServerProfileName = session.ServerProfileName,
        WorldName = session.WorldName,
        HostName = session.HostName,
        Port = session.Port,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static void Normalize(ReforgedPartyAutomationProfile profile, ClientSessionConnection session, string key)
    {
        profile.SchemaVersion = 1;
        profile.ProfileKey = key;
        profile.CharacterName = session.CharacterName;
        profile.ServerProfileName = session.ServerProfileName;
        profile.WorldName = session.WorldName;
        profile.HostName = session.HostName;
        profile.Port = session.Port;
        profile.Preferences ??= new ReforgedPartyAutomationPreferences();
        profile.Notes ??= [];
        profile.Preferences.PartyStateMaximumAgeSeconds = Math.Clamp(profile.Preferences.PartyStateMaximumAgeSeconds, 2, 300);
        profile.Preferences.OnlineStateMaximumAgeSeconds = Math.Clamp(profile.Preferences.OnlineStateMaximumAgeSeconds, 2, 300);
        profile.Preferences.CommandCooldownSeconds = Math.Clamp(profile.Preferences.CommandCooldownSeconds, 1, 60);
        profile.Preferences.ActionHoldSeconds = Math.Clamp(profile.Preferences.ActionHoldSeconds, 1, 60);
        profile.Preferences.HealBelowPercent = Math.Clamp(profile.Preferences.HealBelowPercent, 1, 100);
        profile.Preferences.RescueBelowPercent = Math.Clamp(profile.Preferences.RescueBelowPercent, 1, 100);
        profile.Preferences.ReserveManaPercent = Math.Clamp(profile.Preferences.ReserveManaPercent, 0, 100);
        profile.Preferences.PartyBuffIntervalMinutes = Math.Clamp(profile.Preferences.PartyBuffIntervalMinutes, 1, 1440);
        profile.Preferences.PartyBuffMinimumManaPercent = Math.Clamp(profile.Preferences.PartyBuffMinimumManaPercent, 0, 100);
    }

    private string ProfilePath(string key) => Path.Combine(_profileRoot, key + ".json");

    private static string BuildProfileKey(ClientSessionConnection session)
    {
        var raw = string.Join("|", session.ServerProfileName.Trim(), session.WorldName.Trim(),
            session.HostName.Trim().ToLowerInvariant(), session.Port, session.CharacterName.Trim().ToLowerInvariant());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant()[..24];
    }

    private static void BackupCorrupt(string path)
    {
        try
        {
            if (File.Exists(path)) File.Move(path, path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}", true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
