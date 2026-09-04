using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Reforged.App.Models;

namespace Reforged.App.Services;

/// <summary>
/// Reforged-native combat planner. It consumes only observed/session-authoritative state,
/// keeps target/action runtime state per session, and emits one bounded action at a time.
/// The planner never invents a target, spell, item, or server capability.
/// </summary>
public sealed class ReforgedCombatAutomationService
{
    private readonly string _profileRoot;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Dictionary<string, RuntimeState> _runtime = new(StringComparer.OrdinalIgnoreCase);

    private sealed class RuntimeState
    {
        public string RoomKey { get; set; } = string.Empty;
        public string TargetInstanceId { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public bool PreAttackSent { get; set; }
        public bool EngageSent { get; set; }
        public string AppliedEquipmentSetId { get; set; } = string.Empty;
        public HashSet<string> OpeningActionsSent { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DateTimeOffset> Cooldowns { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DateTimeOffset> FailedActions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string LastActionId { get; set; } = string.Empty;
        public string LastActionName { get; set; } = string.Empty;
        public string LastTargetName { get; set; } = string.Empty;
        public DateTimeOffset? SessionHoldUntil { get; set; }
        public DateTimeOffset? LastRoundAt { get; set; }
    }

    public ReforgedCombatAutomationService(string? rootPath = null)
    {
        var root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate");
        _profileRoot = Path.Combine(root, "Client", "CombatAutomation");
    }

    public ReforgedCombatAutomationProfile LoadProfile(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var key = BuildProfileKey(session);
        var path = ProfilePath(key);
        if (!File.Exists(path)) return NewProfile(session, key);
        try
        {
            var profile = JsonSerializer.Deserialize<ReforgedCombatAutomationProfile>(File.ReadAllText(path), _json)
                          ?? NewProfile(session, key);
            NormalizeProfile(profile, session, key);
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

    public void SaveProfile(ClientSessionConnection session, ReforgedCombatAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var key = BuildProfileKey(session);
        NormalizeProfile(profile, session, key);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_profileRoot);
        var path = ProfilePath(key);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, _json), new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    public ReforgedCombatAutomationPlan Plan(
        ClientSessionConnection session,
        ReforgedCombatAutomationProfile profile,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var p = profile.Preferences;
        if (!p.Enabled) return None("Advanced RealmGate combat automation is disabled.");

        var state = State(session.SessionId);
        if (state.SessionHoldUntil is { } hold && now < hold)
            return None($"Combat planner is holding after a failed action until {hold:HH:mm:ss}.");

        if (!string.Equals(state.RoomKey, session.CurrentRoomKey, StringComparison.OrdinalIgnoreCase))
            ResetForRoom(state, session.CurrentRoomKey);

        if (!IsFresh(session.CurrentRoomNpcDetailsUpdatedAt, now, p.StateMaximumAgeSeconds))
            return new ReforgedCombatAutomationPlan
            {
                Kind = ReforgedCombatActionKind.RefreshRoom,
                RefreshRoom = true,
                Reason = "Refreshing hostile-room state before combat decisions.",
                CooldownSeconds = 2
            };

        if (session.HealthMaximum > 0 && session.HealthCurrent <= 0)
            return None("Stopped: character is incapacitated.");
        if (session.IsMoving)
            return None("Waiting for movement to finish before combat automation acts.");
        if (!session.InCombat && session.IsResting)
            return None("Waiting while resting.");

        var effects = string.Join(' ', session.ActiveEffects);
        if (effects.Contains("incapacitat", StringComparison.OrdinalIgnoreCase) ||
            effects.Contains("unconscious", StringComparison.OrdinalIgnoreCase))
            return None("Stopped: character is incapacitated.");
        if (effects.Contains("blind", StringComparison.OrdinalIgnoreCase))
            return None("Stopped: blindness prevents safe automatic targeting.");

        var hostiles = session.CurrentRoomNpcDetails
            .Where(npc => npc.Alive && npc.Disposition.Equals("hostile", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!session.InCombat && p.StopWhenPlayersPresent && session.CurrentRoomPlayers.Count > 0)
            return None("Engagement paused because another player is present in the room.");
        if (hostiles.Length == 0)
        {
            ClearTarget(state);
            return None(session.InCombat
                ? "Combat is active but no observed living hostile NPC is available for an automatic command."
                : "No living hostile NPCs are present.");
        }
        if (hostiles.Length > p.MaximumHostiles)
            return None($"Stopped: {hostiles.Length} hostiles exceed the configured maximum of {p.MaximumHostiles}.");
        var totalXp = hostiles.Sum(npc => Math.Max(0L, npc.ExperienceReward));
        if (totalXp > p.MaximumTotalExperience)
            return None($"Stopped: room experience {totalXp:N0} exceeds the configured safety limit.");
        if (session.CharacterLevel > 0 && hostiles.Any(npc => npc.Level > session.CharacterLevel + p.MaximumTargetLevelAboveCharacter))
            return None("Stopped: at least one hostile exceeds the configured level-difference limit.");

        if (session.InCombat)
        {
            var escape = PlanEscape(session, profile, hostiles.Length, now);
            if (escape.HasAction) return escape;

            var defensiveSet = p.DefensiveEquipmentSetId;
            if (!string.IsNullOrWhiteSpace(defensiveSet) &&
                session.HealthPercent <= p.DefensiveEquipmentSetAtHealthPercent &&
                !state.AppliedEquipmentSetId.Equals(defensiveSet, StringComparison.OrdinalIgnoreCase))
                return Equip(defensiveSet, "Switching to the configured defensive equipment set.");

            if (p.AutoDefensiveAction && session.HealthPercent <= p.DefensiveActionAtHealthPercent &&
                CooldownReady(state, "__defensive", now) && !string.IsNullOrWhiteSpace(p.DefensiveActionCommand))
            {
                return new ReforgedCombatAutomationPlan
                {
                    Kind = ReforgedCombatActionKind.DefensiveAction,
                    Command = p.DefensiveActionCommand.Trim(),
                    ActionId = "__defensive",
                    ActionName = "Defensive action",
                    Reason = $"Using configured defensive action at {session.HealthPercent}% HP.",
                    CooldownSeconds = p.DefensiveActionCooldownSeconds
                };
            }

            if (state.LastRoundAt is { } lastRound && now - lastRound < TimeSpan.FromSeconds(p.RoundMinimumDelaySeconds))
                return None("Waiting for the configured round-action delay.");

            var target = ResolveLockedOrSelectTarget(session, profile, hostiles, state);
            if (target is null) return None("No safe target satisfied the target filters.");
            var action = SelectAction(profile.Actions.Where(a => !a.OpeningOnly), profile, session, target, hostiles.Length, state, now, inCombat: true);
            if (action is null)
                return None("Combat active; no eligible configured round action is ready.");
            return ActionPlan(ReforgedCombatActionKind.RoundAction, action, target.Name, $"Round action: {action.Name} against {target.Name}.");
        }

        var selected = ResolveLockedOrSelectTarget(session, profile, hostiles, state);
        if (selected is null)
            return None("No safe target satisfied the target filters.");

        var equipmentSetId = hostiles.Length >= p.MultiTargetEquipmentSetAtCount && !string.IsNullOrWhiteSpace(p.MultiTargetEquipmentSetId)
            ? p.MultiTargetEquipmentSetId
            : p.DefaultEquipmentSetId;
        if (!string.IsNullOrWhiteSpace(equipmentSetId) &&
            !state.AppliedEquipmentSetId.Equals(equipmentSetId, StringComparison.OrdinalIgnoreCase))
            return Equip(equipmentSetId, hostiles.Length >= p.MultiTargetEquipmentSetAtCount
                ? "Preparing the configured multi-target equipment set."
                : "Preparing the configured default combat equipment set.");

        var opening = SelectAction(profile.Actions.Where(a => a.OpeningOnly), profile, session, selected, hostiles.Length, state, now, inCombat: false);
        if (opening is not null && !state.OpeningActionsSent.Contains(opening.Id))
            return ActionPlan(ReforgedCombatActionKind.OpeningAction, opening, selected.Name, $"Opening action: {opening.Name} against {selected.Name}.");

        if (p.AutoPreAttack && !state.PreAttackSent && !string.IsNullOrWhiteSpace(p.PreAttackCommand) &&
            CanUseCommand(session, p.PreAttackCommand, p.ManaReservePercent, p.RequireLearnedSpellForCastCommands))
        {
            return new ReforgedCombatAutomationPlan
            {
                Kind = ReforgedCombatActionKind.PreAttack,
                Command = ApplyTarget(p.PreAttackCommand, selected.Name),
                Target = selected.Name,
                ActionId = "__preattack",
                ActionName = "Pre-attack",
                Reason = $"Running the configured pre-attack action for {selected.Name}.",
                CooldownSeconds = p.EngagementCooldownSeconds,
                IsSpellCommand = IsCast(p.PreAttackCommand)
            };
        }

        if (!state.EngageSent && !string.IsNullOrWhiteSpace(p.EngageCommand))
        {
            return new ReforgedCombatAutomationPlan
            {
                Kind = ReforgedCombatActionKind.Engage,
                Command = ApplyTarget(p.EngageCommand, selected.Name),
                Target = selected.Name,
                ActionId = "__engage",
                ActionName = "Engage",
                Reason = $"Engaging selected hostile target {selected.Name}.",
                CooldownSeconds = p.EngagementCooldownSeconds
            };
        }

        if (!p.AutoRetarget)
            return None("Target engagement has already been attempted; automatic retargeting is disabled.");
        return None("Waiting for combat-state confirmation or target-state change after engagement.");
    }

    public void MarkExecuted(
        ClientSessionConnection session,
        ReforgedCombatAutomationProfile profile,
        ReforgedCombatAutomationPlan plan,
        DateTimeOffset now)
    {
        var state = State(session.SessionId);
        if (!string.IsNullOrWhiteSpace(plan.EquipmentSetId))
            state.AppliedEquipmentSetId = plan.EquipmentSetId;
        if (plan.Kind == ReforgedCombatActionKind.PreAttack)
            state.PreAttackSent = true;
        if (plan.Kind == ReforgedCombatActionKind.Engage)
            state.EngageSent = true;
        if (plan.Kind == ReforgedCombatActionKind.OpeningAction && !string.IsNullOrWhiteSpace(plan.ActionId))
            state.OpeningActionsSent.Add(plan.ActionId);
        if (plan.Kind == ReforgedCombatActionKind.RoundAction)
            state.LastRoundAt = now;
        if (!string.IsNullOrWhiteSpace(plan.ActionId))
        {
            state.Cooldowns[plan.ActionId] = now.AddSeconds(Math.Max(1, plan.CooldownSeconds));
            state.LastActionId = plan.ActionId;
            state.LastActionName = plan.ActionName;
            state.LastTargetName = plan.Target;
        }
        if (plan.Kind == ReforgedCombatActionKind.Escape)
            ClearTarget(state);
    }

    public void MarkEquipmentSetApplied(string sessionId, string setId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(setId)) return;
        State(sessionId).AppliedEquipmentSetId = setId;
    }

    public void ObserveLine(
        ClientSessionConnection session,
        ReforgedCombatAutomationProfile profile,
        string line,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var state = State(session.SessionId);
        var text = line.Trim();
        if (ContainsAny(text,
            "isn't here", "is not here", "already dead", "is dead", "no such target", "cannot see your target"))
        {
            ClearTarget(state);
            return;
        }

        if (string.IsNullOrWhiteSpace(state.LastActionId)) return;
        if (!ContainsAny(text,
            "failed to cast", "do not know", "not enough mana", "cannot cast", "can't cast",
            "cannot use", "can't use", "may not use", "does no damage", "no effect", "immune"))
            return;

        var holdSeconds = Math.Max(1, profile.Preferences.FailedActionHoldSeconds);
        state.FailedActions[state.LastActionId] = now.AddSeconds(holdSeconds);
        if (profile.Preferences.StopAfterActionFailure)
            state.SessionHoldUntil = now.AddSeconds(holdSeconds);
    }

    public bool CanUseConfiguredCommand(
        ClientSessionConnection session,
        ReforgedCombatAutomationProfile profile,
        string command) => CanUseCommand(
            session, command, profile.Preferences.ManaReservePercent,
            profile.Preferences.RequireLearnedSpellForCastCommands);

    public void ClearRuntime(string sessionId) => _runtime.Remove(sessionId);

    private ReforgedCombatAutomationPlan PlanEscape(
        ClientSessionConnection session,
        ReforgedCombatAutomationProfile profile,
        int hostileCount,
        DateTimeOffset now)
    {
        var p = profile.Preferences;
        if (!p.AutoEscape || string.IsNullOrWhiteSpace(p.EscapeCommand)) return None(string.Empty);
        var reason = session.HealthPercent <= p.EscapeAtHealthPercent
            ? $"Escaping at {session.HealthPercent}% HP."
            : p.EscapeAtLowMana && session.ManaPercent <= p.EscapeAtManaPercent
                ? $"Escaping at {session.ManaPercent}% mana/Kai."
                : p.EscapeWhenOverwhelmed && hostileCount >= p.EscapeAtHostileCount
                    ? $"Escaping because {hostileCount} hostiles meet the overwhelmed threshold."
                    : string.Empty;
        if (reason.Length == 0) return None(string.Empty);
        var state = State(session.SessionId);
        if (!CooldownReady(state, "__escape", now)) return None("Escape command is cooling down.");
        return new ReforgedCombatAutomationPlan
        {
            Kind = ReforgedCombatActionKind.Escape,
            Command = p.EscapeCommand.Trim(),
            ActionId = "__escape",
            ActionName = "Escape",
            Reason = reason,
            CooldownSeconds = p.EscapeCooldownSeconds
        };
    }

    private ClientRoomInspectionNpc? ResolveLockedOrSelectTarget(
        ClientSessionConnection session,
        ReforgedCombatAutomationProfile profile,
        IReadOnlyList<ClientRoomInspectionNpc> hostiles,
        RuntimeState state)
    {
        if (!string.IsNullOrWhiteSpace(state.TargetInstanceId))
        {
            var locked = hostiles.FirstOrDefault(npc => npc.InstanceId.Equals(state.TargetInstanceId, StringComparison.OrdinalIgnoreCase));
            if (locked is not null && IsTargetAllowed(locked, profile.Preferences)) return locked;
            ClearTarget(state);
        }

        var allowed = hostiles.Where(npc => IsTargetAllowed(npc, profile.Preferences)).ToArray();
        if (allowed.Length == 0) return null;
        var priorityWords = SplitWords(profile.Preferences.PriorityTargetWords);
        var prioritized = priorityWords.Length == 0
            ? allowed
            : allowed.Where(npc => priorityWords.Any(word => npc.Name.Contains(word, StringComparison.OrdinalIgnoreCase))).ToArray();
        var source = prioritized.Length > 0 ? prioritized : allowed;
        var selected = profile.Preferences.TargetMode switch
        {
            ReforgedCombatTargetMode.LowestHealthPercent => source.OrderBy(HealthPercent).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).First(),
            ReforgedCombatTargetMode.LowestHealth => source.OrderBy(n => n.CurrentHitPoints).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).First(),
            ReforgedCombatTargetMode.LowestLevel => source.OrderBy(n => n.Level).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).First(),
            ReforgedCombatTargetMode.HighestLevel => source.OrderByDescending(n => n.Level).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).First(),
            ReforgedCombatTargetMode.HighestExperience => source.OrderByDescending(n => n.ExperienceReward).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).First(),
            _ => source[0]
        };
        state.TargetInstanceId = selected.InstanceId;
        state.TargetName = selected.Name;
        state.PreAttackSent = false;
        state.EngageSent = false;
        state.AppliedEquipmentSetId = string.Empty;
        state.OpeningActionsSent.Clear();
        return selected;
    }

    private ReforgedCombatActionRule? SelectAction(
        IEnumerable<ReforgedCombatActionRule> source,
        ReforgedCombatAutomationProfile profile,
        ClientSessionConnection session,
        ClientRoomInspectionNpc target,
        int targetCount,
        RuntimeState state,
        DateTimeOffset now,
        bool inCombat)
    {
        return source
            .Where(action => action.Enabled && !string.IsNullOrWhiteSpace(action.Command))
            .Where(action => !action.InCombatOnly || inCombat)
            .Where(action => action.MinimumTargets <= targetCount && targetCount <= action.MaximumTargets)
            .Where(action => !action.IsAreaAction || targetCount >= Math.Max(2, action.MinimumTargets))
            .Where(action => session.HealthPercent >= action.MinimumHealthPercent && session.ManaPercent >= action.MinimumManaPercent)
            .Where(action => string.IsNullOrWhiteSpace(action.TargetNameContains) || target.Name.Contains(action.TargetNameContains, StringComparison.OrdinalIgnoreCase))
            .Where(action => CooldownReady(state, action.Id, now))
            .Where(action => !state.FailedActions.TryGetValue(action.Id, out var failedUntil) || now >= failedUntil)
            .Where(action => CanUseCommand(session, action.Command, profile.Preferences.ManaReservePercent, profile.Preferences.RequireLearnedSpellForCastCommands))
            .OrderBy(action => action.Priority)
            .ThenBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool CanUseCommand(
        ClientSessionConnection session, string command, int reservePercent, bool requireLearnedSpell)
    {
        if (!IsCast(command)) return true;
        var effects = string.Join(' ', session.ActiveEffects);
        if (effects.Contains("silenc", StringComparison.OrdinalIgnoreCase)) return false;
        if (!requireLearnedSpell) return true;
        var spell = session.LearnedSpellDetails
            .Where(candidate => command.Contains(candidate.Name, StringComparison.OrdinalIgnoreCase) ||
                                command.Contains(candidate.CanonicalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Name.Length)
            .FirstOrDefault();
        if (spell is null) return false;
        var reserve = session.ManaMaximum > 0
            ? (int)Math.Ceiling(session.ManaMaximum * Math.Clamp(reservePercent, 0, 100) / 100d)
            : 0;
        return session.ManaCurrent >= spell.ManaCost && session.ManaCurrent - spell.ManaCost >= reserve;
    }

    private ReforgedCombatAutomationPlan ActionPlan(
        ReforgedCombatActionKind kind,
        ReforgedCombatActionRule action,
        string target,
        string reason) => new()
    {
        Kind = kind,
        Command = action.AppendTarget ? ApplyTarget(action.Command, target) : action.Command.Trim(),
        Target = target,
        ActionId = action.Id,
        ActionName = action.Name,
        EquipmentSetId = action.EquipmentSetId,
        Reason = reason,
        FallbackCommand = action.FallbackCommand,
        CooldownSeconds = action.CooldownSeconds,
        IsSpellCommand = IsCast(action.Command)
    };

    private static ReforgedCombatAutomationPlan Equip(string setId, string reason) => new()
    {
        Kind = ReforgedCombatActionKind.EquipSet,
        EquipmentSetId = setId,
        Reason = reason,
        CooldownSeconds = 2
    };

    private static bool IsTargetAllowed(ClientRoomInspectionNpc npc, ReforgedCombatAutomationPreferences preferences)
    {
        if (!npc.Alive || !npc.Disposition.Equals("hostile", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(npc.Name)) return false;
        var avoid = SplitWords(preferences.AvoidTargetWords);
        return !avoid.Any(word => npc.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static double HealthPercent(ClientRoomInspectionNpc npc) => npc.MaximumHitPoints <= 0
        ? 100d
        : npc.CurrentHitPoints * 100d / npc.MaximumHitPoints;

    private static bool CooldownReady(RuntimeState state, string key, DateTimeOffset now) =>
        !state.Cooldowns.TryGetValue(key, out var readyAt) || now >= readyAt;

    private RuntimeState State(string sessionId)
    {
        if (!_runtime.TryGetValue(sessionId, out var state))
        {
            state = new RuntimeState();
            _runtime[sessionId] = state;
        }
        return state;
    }

    private static void ResetForRoom(RuntimeState state, string roomKey)
    {
        state.RoomKey = roomKey ?? string.Empty;
        ClearTarget(state);
        state.AppliedEquipmentSetId = string.Empty;
    }

    private static void ClearTarget(RuntimeState state)
    {
        state.TargetInstanceId = string.Empty;
        state.TargetName = string.Empty;
        state.PreAttackSent = false;
        state.EngageSent = false;
        state.AppliedEquipmentSetId = string.Empty;
        state.OpeningActionsSent.Clear();
    }

    private static string ApplyTarget(string command, string target)
    {
        var normalized = command?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return string.Empty;
        return normalized.Contains("{target}", StringComparison.OrdinalIgnoreCase)
            ? normalized.Replace("{target}", target, StringComparison.OrdinalIgnoreCase)
            : $"{normalized} {target}".Trim();
    }

    private static string[] SplitWords(string value) => (value ?? string.Empty)
        .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool IsCast(string? command) =>
        !string.IsNullOrWhiteSpace(command) && command.TrimStart().StartsWith("cast ", StringComparison.OrdinalIgnoreCase);

    private static bool IsFresh(DateTimeOffset? capturedAt, DateTimeOffset now, int seconds) =>
        capturedAt is not null && now - capturedAt.Value <= TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 120));

    private static ReforgedCombatAutomationPlan None(string reason) => new() { Kind = ReforgedCombatActionKind.None, Reason = reason };

    private static string BuildProfileKey(ClientSessionConnection session)
    {
        var raw = string.Join('|',
            session.ServerProfileName?.Trim().ToLowerInvariant(),
            session.WorldName?.Trim().ToLowerInvariant(),
            session.HostName?.Trim().ToLowerInvariant(),
            session.Port.ToString(),
            session.CharacterName?.Trim().ToLowerInvariant());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..24];
    }

    private string ProfilePath(string key) => Path.Combine(_profileRoot, key + ".json");

    private static ReforgedCombatAutomationProfile NewProfile(ClientSessionConnection session, string key)
    {
        var profile = new ReforgedCombatAutomationProfile
        {
            ProfileKey = key,
            CharacterName = session.CharacterName,
            ServerProfileName = session.ServerProfileName,
            WorldName = session.WorldName
        };
        profile.Actions.Add(new ReforgedCombatActionRule
        {
            Priority = 100,
            Name = "Primary combat action",
            Command = "attack",
            AppendTarget = true,
            InCombatOnly = true,
            OpeningOnly = false,
            CooldownSeconds = 3
        });
        return profile;
    }

    private static void NormalizeProfile(ReforgedCombatAutomationProfile profile, ClientSessionConnection session, string key)
    {
        profile.Version = 1;
        profile.ProfileKey = key;
        profile.CharacterName = session.CharacterName;
        profile.ServerProfileName = session.ServerProfileName;
        profile.WorldName = session.WorldName;
        profile.Preferences ??= new ReforgedCombatAutomationPreferences();
        profile.Actions ??= [];
        var p = profile.Preferences;
        p.StateMaximumAgeSeconds = Math.Clamp(p.StateMaximumAgeSeconds, 1, 120);
        p.EngagementCooldownSeconds = Math.Clamp(p.EngagementCooldownSeconds, 1, 60);
        p.RoundMinimumDelaySeconds = Math.Clamp(p.RoundMinimumDelaySeconds, 1, 30);
        p.ManaReservePercent = Math.Clamp(p.ManaReservePercent, 0, 100);
        p.MaximumHostiles = Math.Clamp(p.MaximumHostiles, 1, 99);
        p.MaximumTotalExperience = Math.Max(0, p.MaximumTotalExperience);
        p.MaximumTargetLevelAboveCharacter = Math.Clamp(p.MaximumTargetLevelAboveCharacter, 0, 999);
        p.MultiTargetEquipmentSetAtCount = Math.Clamp(p.MultiTargetEquipmentSetAtCount, 2, 99);
        p.DefensiveEquipmentSetAtHealthPercent = Math.Clamp(p.DefensiveEquipmentSetAtHealthPercent, 0, 100);
        p.DefensiveActionAtHealthPercent = Math.Clamp(p.DefensiveActionAtHealthPercent, 0, 100);
        p.DefensiveActionCooldownSeconds = Math.Clamp(p.DefensiveActionCooldownSeconds, 1, 300);
        p.EscapeAtHealthPercent = Math.Clamp(p.EscapeAtHealthPercent, 0, 100);
        p.EscapeAtManaPercent = Math.Clamp(p.EscapeAtManaPercent, 0, 100);
        p.EscapeAtHostileCount = Math.Clamp(p.EscapeAtHostileCount, 1, 99);
        p.EscapeCooldownSeconds = Math.Clamp(p.EscapeCooldownSeconds, 1, 300);
        p.FailedActionHoldSeconds = Math.Clamp(p.FailedActionHoldSeconds, 1, 300);
        foreach (var action in profile.Actions)
        {
            action.Id = string.IsNullOrWhiteSpace(action.Id) ? Guid.NewGuid().ToString("N") : action.Id;
            action.Priority = Math.Clamp(action.Priority, 0, 100000);
            action.MinimumTargets = Math.Clamp(action.MinimumTargets, 1, 99);
            action.MaximumTargets = Math.Clamp(action.MaximumTargets, action.MinimumTargets, 99);
            action.MinimumHealthPercent = Math.Clamp(action.MinimumHealthPercent, 0, 100);
            action.MinimumManaPercent = Math.Clamp(action.MinimumManaPercent, 0, 100);
            action.CooldownSeconds = Math.Clamp(action.CooldownSeconds, 1, 300);
        }
    }

    private static void BackupCorrupt(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Move(path, path + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}", true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
