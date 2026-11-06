using System.Text.Json;

namespace RealmGate.Client.Gameplay;

public sealed class ClientGameplayAutomationRuntime
{
    private readonly Dictionary<string, DateTimeOffset> _lastActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _openingActions = new(StringComparer.OrdinalIgnoreCase);
    private string _roomId = string.Empty;

    public string AppliedEquipmentSetId { get; private set; } = string.Empty;

    public void ObserveRoom(string roomId)
    {
        var normalized = roomId?.Trim() ?? string.Empty;
        if (_roomId.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return;
        _roomId = normalized;
        AppliedEquipmentSetId = string.Empty;
        _lastActions.Clear();
        _openingActions.Clear();
    }

    public bool IsReady(string actionId, int cooldownSeconds, DateTimeOffset now) =>
        !_lastActions.TryGetValue(actionId, out var last) ||
        now - last >= TimeSpan.FromSeconds(Math.Clamp(cooldownSeconds, 1, 3600));

    public bool OpeningActionWasSent(string actionId) => _openingActions.Contains(actionId);

    public void MarkExecuted(ClientGameplayActionPlan plan, DateTimeOffset now, string roomId)
    {
        ObserveRoom(roomId);
        if (plan.Kind == ClientGameplayActionKind.EquipSet)
        {
            AppliedEquipmentSetId = plan.EquipmentSetId.Trim();
            return;
        }
        if (string.IsNullOrWhiteSpace(plan.ActionId)) return;
        _lastActions[plan.ActionId] = now;
        if (plan.Kind == ClientGameplayActionKind.CombatRule)
            _openingActions.Add(plan.ActionId);
    }

    public void Reset()
    {
        _roomId = string.Empty;
        AppliedEquipmentSetId = string.Empty;
        _lastActions.Clear();
        _openingActions.Clear();
    }
}

public static class ClientGameplayAutomationStateDecoder
{
    public const int MaximumPayloadCharacters = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ClientGameplayAutomationSnapshot Decode(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (payload.Length > MaximumPayloadCharacters)
            throw new InvalidDataException("Gameplay state payload exceeds the client safety limit.");
        ClientGameplayAutomationSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ClientGameplayAutomationSnapshot>(payload, Json)
                       ?? throw new InvalidDataException("Gameplay state payload was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Gameplay state payload is not valid JSON.", exception);
        }

        if (string.IsNullOrWhiteSpace(snapshot.WorldId) || string.IsNullOrWhiteSpace(snapshot.CharacterId))
            throw new InvalidDataException("Gameplay state has no world or character identity.");
        if (!IsQualifiedRoom(snapshot.RoomId))
            throw new InvalidDataException($"Gameplay state room '{snapshot.RoomId}' is not map-qualified.");
        if (snapshot.HealthMaximum <= 0 ||
            snapshot.ManaCurrent < 0 || snapshot.ManaMaximum < 0)
            throw new InvalidDataException("Gameplay state contains invalid resource bounds.");
        if (snapshot.Conditions is null || snapshot.LearnedSpells is null || snapshot.ActiveEffects is null ||
            snapshot.Npcs is null || snapshot.Players is null || snapshot.PartyMembers is null ||
            snapshot.ActiveEffects.Any(effect => effect is null || string.IsNullOrWhiteSpace(effect.Name)) ||
            snapshot.Npcs.Any(npc => npc is null || string.IsNullOrWhiteSpace(npc.InstanceId) ||
                string.IsNullOrWhiteSpace(npc.Name) || string.IsNullOrWhiteSpace(npc.Disposition)) ||
            snapshot.Players.Any(player => player is null || string.IsNullOrWhiteSpace(player.CharacterId)) ||
            snapshot.PartyMembers.Any(member => member is null || string.IsNullOrWhiteSpace(member.CharacterId) ||
                string.IsNullOrWhiteSpace(member.Name) || member.Conditions is null))
            throw new InvalidDataException("Gameplay state contains incomplete collections.");

        return snapshot with
        {
            WorldId = NormalizeIdentity(snapshot.WorldId),
            CharacterId = snapshot.CharacterId.Trim(),
            CharacterName = snapshot.CharacterName?.Trim() ?? string.Empty,
            RoomId = snapshot.RoomId.Trim(),
            Conditions = Clean(snapshot.Conditions),
            LearnedSpells = Clean(snapshot.LearnedSpells),
            ActiveEffects = snapshot.ActiveEffects ?? [],
            Npcs = snapshot.Npcs ?? [],
            Players = snapshot.Players ?? [],
            PartyMembers = snapshot.PartyMembers ?? []
        };
    }

    public static string NormalizeIdentity(string? value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        return normalized.Length == 0 ? "Classic" : normalized;
    }

    private static bool IsQualifiedRoom(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return false;
        var value = roomId.Trim();
        if (!value.StartsWith("room.map", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = value[8..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var map) && map > 0 &&
               int.TryParse(parts[1], out var room) && room > 0;
    }

    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? values) =>
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

public static class ClientGameplayAutomationPlanner
{
    public static ClientGameplayActionPlan PlanRecovery(
        ClientGameplayAutomationSnapshot state,
        ClientGameplayAutomationProfile profile,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Recovery.Enabled) return ClientGameplayActionPlan.None("Recovery automation is disabled.");
        if (!IdentityMatches(state, profile)) return ClientGameplayActionPlan.None("World or character identity does not match the profile.");
        if (!Fresh(state, profile, now)) return ClientGameplayActionPlan.Refresh("Recovery state is stale.");
        if (state.HealthCurrent <= 0) return ClientGameplayActionPlan.None("Character is incapacitated.");
        var options = profile.Recovery;
        if (state.HealthPercent <= options.EmergencyHealthPercent && !string.IsNullOrWhiteSpace(options.EmergencyCommand))
            return Action(ClientGameplayActionKind.Emergency, options.EmergencyCommand,
                $"Emergency threshold reached at {state.HealthPercent}% HP.", state, options.RequireLearnedSpellForCastCommands);
        if (HasCondition(state, "poisoned") && options.CurePoison && !string.IsNullOrWhiteSpace(options.CurePoisonCommand))
            return Action(ClientGameplayActionKind.Cure, options.CurePoisonCommand,
                "Poison is present in authoritative character state.", state, options.RequireLearnedSpellForCastCommands);
        if (state.HealthPercent <= options.HealBelowPercent && !string.IsNullOrWhiteSpace(options.HealCommand))
            return Action(ClientGameplayActionKind.Heal, options.HealCommand,
                $"Health is {state.HealthPercent}%.", state, options.RequireLearnedSpellForCastCommands);
        if (options.MaintainBuffs)
        {
            foreach (var buff in profile.Buffs.Where(rule => rule.Enabled)
                         .OrderBy(rule => rule.Priority).ThenBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (buff.OnlyOutOfCombat && state.InCombat || state.ManaPercent < buff.MinimumManaPercent) continue;
                var match = string.IsNullOrWhiteSpace(buff.EffectName) ? buff.Name : buff.EffectName;
                if (string.IsNullOrWhiteSpace(match)) continue;
                var refreshLead = TimeSpan.FromSeconds(Math.Clamp(buff.RefreshBeforeExpirySeconds, 0, 3600));
                var active = state.ActiveEffects.FirstOrDefault(effect =>
                    effect.Name.Equals(match, StringComparison.OrdinalIgnoreCase) &&
                    (effect.ExpiresAtUtc is null || effect.ExpiresAtUtc > now));
                if (active is not null &&
                    (active.ExpiresAtUtc is null || active.ExpiresAtUtc - now > refreshLead)) continue;
                var reason = active?.ExpiresAtUtc is { } expiry
                    ? $"Configured effect '{match}' expires in {Math.Max(0, (int)(expiry - now).TotalSeconds)} second(s)."
                    : $"Configured effect '{match}' is not active.";
                var plan = Action(ClientGameplayActionKind.MaintainBuff, buff.Command,
                    reason, state, options.RequireLearnedSpellForCastCommands);
                if (plan.HasAction) return plan;
            }
        }
        if (state.IsResting) return ClientGameplayActionPlan.None("Recovery is already active.");
        if (HasCondition(state, "poisoned")) return ClientGameplayActionPlan.None("Rest is blocked while poisoned.");
        if (!state.InCombat && state.CanMeditate && state.ManaMaximum > 0 && state.ManaPercent <= options.MeditateBelowManaPercent &&
            !string.IsNullOrWhiteSpace(options.MeditateCommand))
            return Action(ClientGameplayActionKind.RestoreMana, options.MeditateCommand,
                $"Mana is {state.ManaPercent}%.", state, options.RequireLearnedSpellForCastCommands);
        if (!state.InCombat && state.HealthPercent <= options.RestBelowHealthPercent &&
            !string.IsNullOrWhiteSpace(options.RestCommand))
            return Action(ClientGameplayActionKind.Rest, options.RestCommand,
                $"Health is {state.HealthPercent}% and combat is inactive.", state, options.RequireLearnedSpellForCastCommands);
        return ClientGameplayActionPlan.None("No recovery action is required.");
    }

    public static ClientGameplayActionPlan PlanCombat(
        ClientGameplayAutomationSnapshot state,
        ClientGameplayAutomationProfile profile,
        DateTimeOffset now,
        ClientGameplayAutomationRuntime? runtime = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Combat.Enabled) return ClientGameplayActionPlan.None("Combat automation is disabled.");
        if (!IdentityMatches(state, profile)) return ClientGameplayActionPlan.None("World or character identity does not match the profile.");
        if (!Fresh(state, profile, now)) return ClientGameplayActionPlan.Refresh("Combat state is stale.");
        if (state.HealthCurrent <= 0) return ClientGameplayActionPlan.None("Character is incapacitated.");
        var options = profile.Combat;
        runtime?.ObserveRoom(state.RoomId);
        if (state.InCombat && state.HealthPercent <= options.EscapeBelowHealthPercent)
            return Action(ClientGameplayActionKind.Emergency, options.EscapeCommand,
                $"Escape threshold reached at {state.HealthPercent}% HP.", state, options.RequireLearnedSpellForCastCommands);
        if (state.IsResting) return ClientGameplayActionPlan.None("Engagement paused during recovery.");
        if (state.InPlayerCombat) return ClientGameplayActionPlan.None("NPC automation is paused during player combat.");
        if (HasCondition(state, "blinded") || HasCondition(state, "paralyzed"))
            return ClientGameplayActionPlan.None("Character status prevents safe automatic targeting.");
        if (!state.InCombat && options.StopWhenOtherPlayersPresent && state.Players.Count > 0)
            return ClientGameplayActionPlan.None("Engagement paused because another player is present.");
        var avoided = SplitNames(options.AvoidNames);
        var hostiles = state.Npcs.Where(npc => npc.Alive &&
                npc.Disposition.Equals("hostile", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (hostiles.Length == 0) return ClientGameplayActionPlan.None("No observed hostile NPC is available.");
        if (hostiles.Length > Math.Max(1, options.MaximumHostiles))
            return ClientGameplayActionPlan.None($"Observed hostiles exceed the configured maximum of {options.MaximumHostiles}.");
        if (hostiles.Any(npc => npc.Level > state.Level + options.MaximumTargetLevelAboveCharacter))
            return ClientGameplayActionPlan.None("A hostile exceeds the configured level difference.");
        var candidates = hostiles.Where(npc => !avoided.Any(name =>
            npc.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (candidates.Length == 0) return ClientGameplayActionPlan.None("All observed hostiles match the avoid list.");
        var target = SelectTarget(candidates, options.TargetMode);

        var equipmentSetId = state.InCombat && state.HealthPercent <= options.DefensiveEquipmentSetAtHealthPercent &&
                             !string.IsNullOrWhiteSpace(options.DefensiveEquipmentSetId)
            ? options.DefensiveEquipmentSetId
            : hostiles.Length >= options.MultiTargetEquipmentSetAtCount &&
              !string.IsNullOrWhiteSpace(options.MultiTargetEquipmentSetId)
                ? options.MultiTargetEquipmentSetId
                : options.DefaultEquipmentSetId;
        if (!string.IsNullOrWhiteSpace(equipmentSetId) &&
            !(runtime?.AppliedEquipmentSetId.Equals(equipmentSetId, StringComparison.OrdinalIgnoreCase) ?? false))
            return new ClientGameplayActionPlan(
                ClientGameplayActionKind.EquipSet,
                string.Empty,
                $"Applying the selected combat equipment set before acting on {target.Name}.",
                target.Name,
                equipmentSetId.Trim());

        const string defensiveActionId = "combat:defensive";
        if (state.InCombat && options.AutoDefensiveAction &&
            state.HealthPercent <= options.DefensiveActionAtHealthPercent &&
            (runtime?.IsReady(defensiveActionId, options.DefensiveActionCooldownSeconds, now) ?? true))
        {
            var defensive = Action(ClientGameplayActionKind.DefensiveAction, options.DefensiveActionCommand,
                $"Defensive threshold reached at {state.HealthPercent}% HP.", state,
                options.RequireLearnedSpellForCastCommands, target.Name, options.MinimumManaPercent);
            if (defensive.HasAction)
                return defensive with
                {
                    ActionId = defensiveActionId,
                    CooldownSeconds = options.DefensiveActionCooldownSeconds
                };
        }

        foreach (var rule in profile.CombatActions.Where(rule => rule.Enabled)
                     .OrderBy(rule => rule.Priority).ThenBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (rule.OpeningOnly && (state.InCombat || (runtime?.OpeningActionWasSent(rule.Id) ?? false))) continue;
            if (rule.InCombatOnly && !state.InCombat) continue;
            if (hostiles.Length < rule.MinimumTargets || hostiles.Length > rule.MaximumTargets) continue;
            if (state.HealthPercent < rule.MinimumHealthPercent || state.ManaPercent < rule.MinimumManaPercent) continue;
            if (rule.TargetNameContains.Length > 0 &&
                !target.Name.Contains(rule.TargetNameContains, StringComparison.OrdinalIgnoreCase)) continue;
            if (!(runtime?.IsReady(rule.Id, rule.CooldownSeconds, now) ?? true)) continue;
            if (!string.IsNullOrWhiteSpace(rule.EquipmentSetId) &&
                !(runtime?.AppliedEquipmentSetId.Equals(rule.EquipmentSetId, StringComparison.OrdinalIgnoreCase) ?? false))
                return new ClientGameplayActionPlan(
                    ClientGameplayActionKind.EquipSet,
                    string.Empty,
                    $"Applying equipment set for combat action '{rule.Name}'.",
                    target.Name,
                    rule.EquipmentSetId.Trim());

            var ruleCommand = ApplyCombatRuleCommand(rule, target.InstanceId);
            var plan = Action(ClientGameplayActionKind.CombatRule, ruleCommand,
                $"Combat rule '{rule.Name}' selected for {target.Name}.", state,
                options.RequireLearnedSpellForCastCommands, target.Name, rule.MinimumManaPercent);
            if (plan.HasAction)
                return plan with
                {
                    ActionId = rule.Id,
                    CooldownSeconds = rule.CooldownSeconds
                };
        }

        if (state.InCombat && string.IsNullOrWhiteSpace(options.RoundCommand))
            return ClientGameplayActionPlan.None("Combat is active; the server is handling normal attacks.");
        // Instance IDs avoid redirecting an attack to a same-named neutral NPC or player.
        var template = state.InCombat ? options.RoundCommand : options.EngageCommand;
        var kind = state.InCombat ? ClientGameplayActionKind.CombatAction : ClientGameplayActionKind.Engage;
        var command = ApplyTarget(template, target.InstanceId);
        return Action(kind, command,
            $"{(state.InCombat ? "Combat action" : "Engaging")} {target.Name}.", state,
            options.RequireLearnedSpellForCastCommands, target.Name, options.MinimumManaPercent);
    }

    public static ClientGameplayActionPlan PlanParty(
        ClientGameplayAutomationSnapshot state,
        ClientGameplayAutomationProfile profile,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Party.Enabled) return ClientGameplayActionPlan.None("Party automation is disabled.");
        if (!IdentityMatches(state, profile)) return ClientGameplayActionPlan.None("World or character identity does not match the profile.");
        if (!Fresh(state, profile, now)) return ClientGameplayActionPlan.Refresh("Party state is stale.");
        if (state.HealthCurrent <= 0) return ClientGameplayActionPlan.None("Character is incapacitated.");
        var options = profile.Party;
        var sameRoomMembers = state.PartyMembers.Where(member => member.Online && !member.Invited &&
                member.RoomId.Equals(state.RoomId, StringComparison.OrdinalIgnoreCase) &&
                member.HealthPercent > 0 &&
                !member.CharacterId.Equals(state.CharacterId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(member => member.HealthPercent).ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var poisoned = sameRoomMembers.FirstOrDefault(member =>
            member.Conditions.Any(condition => condition.Equals("poisoned", StringComparison.OrdinalIgnoreCase)));
        if (poisoned is not null && options.CurePoisonedMembers)
        {
            var cure = Action(ClientGameplayActionKind.PartyCure,
                ApplyTarget(options.CurePoisonCommand, poisoned.Name),
                $"Party member {poisoned.Name} is poisoned in the current room.", state,
                options.RequireLearnedSpellForCastCommands, poisoned.Name);
            if (cure.HasAction) return cure;
        }
        var wounded = sameRoomMembers.FirstOrDefault();
        if (wounded is not null && options.AutoRescueMembers && state.InCombat &&
            wounded.HealthPercent <= options.RescueBelowPercent)
            return Action(ClientGameplayActionKind.PartyRescue,
                ApplyTarget(options.RescueCommand, wounded.Name),
                $"Party member {wounded.Name} reached the {options.RescueBelowPercent}% rescue threshold.", state,
                options.RequireLearnedSpellForCastCommands, wounded.Name);
        if (wounded is not null && options.AutoHealMembers &&
            wounded.HealthPercent <= options.HealBelowPercent)
            return Action(ClientGameplayActionKind.PartyHeal,
                ApplyTarget(options.HealCommand, wounded.Name),
                $"Party member {wounded.Name} is at {wounded.HealthPercent}% HP.", state,
                options.RequireLearnedSpellForCastCommands, wounded.Name);

        if (options.MaintainPreferredRank)
        {
            var self = state.PartyMembers.FirstOrDefault(member =>
                member.CharacterId.Equals(state.CharacterId, StringComparison.OrdinalIgnoreCase));
            var preferred = options.PreferredRank.Trim();
            if (self is not null && preferred.Length > 0 &&
                !self.Rank.Contains(preferred, StringComparison.OrdinalIgnoreCase))
            {
                var command = preferred.Contains("back", StringComparison.OrdinalIgnoreCase)
                    ? options.BackRankCommand
                    : options.FrontRankCommand;
                return Action(ClientGameplayActionKind.PartyRank, command,
                    $"Observed rank '{self.Rank}' differs from preferred rank '{preferred}'.", state,
                    options.RequireLearnedSpellForCastCommands, state.CharacterName);
            }
        }

        if (options.AutoInviteNamedMembers)
        {
            var configured = SplitNames(options.InviteNames);
            var grouped = state.PartyMembers.Select(member => member.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var target = state.Players
                .Where(player => !grouped.Contains(player.Name) &&
                                 player.RoomId.Equals(state.RoomId, StringComparison.OrdinalIgnoreCase))
                .Select(player => player.Name)
                .FirstOrDefault(name => configured.Contains(name, StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(target))
                return Action(ClientGameplayActionKind.PartyInvite,
                    ApplyTarget(options.InviteCommand, target),
                    $"Inviting configured player {target}, who is visible in the current room.", state,
                    options.RequireLearnedSpellForCastCommands, target);
        }

        var leader = options.LeaderName.Trim();
        if (leader.Length == 0) return ClientGameplayActionPlan.None("No party leader is configured.");
        var member = state.PartyMembers.FirstOrDefault(candidate =>
            candidate.Name.Equals(leader, StringComparison.OrdinalIgnoreCase) && candidate.Online && !candidate.Invited);
        if (member is null || !member.RoomId.Equals(state.RoomId, StringComparison.OrdinalIgnoreCase))
        {
            if (options.RegroupWhenSeparated)
                return Action(ClientGameplayActionKind.PartyRegroup, options.RegroupCommand,
                    $"Configured leader {leader} is absent or in another room; requesting regroup information.", state,
                    options.RequireLearnedSpellForCastCommands, leader);
            return ClientGameplayActionPlan.None($"Configured leader {leader} is not present in the current room.");
        }
        if (options.AutoAssistLeader && !state.InCombat && member.InCombat &&
            member.RoomId.Equals(state.RoomId, StringComparison.OrdinalIgnoreCase))
            return Action(ClientGameplayActionKind.Assist, ApplyTarget(options.AssistCommand, leader),
                $"Assisting configured leader {leader}.", state, options.RequireLearnedSpellForCastCommands, leader);
        if (options.AutoFollowLeader && !member.IsLeader && !state.InCombat &&
            member.RoomId.Equals(state.RoomId, StringComparison.OrdinalIgnoreCase))
            return Action(ClientGameplayActionKind.Follow, ApplyTarget(options.FollowCommand, leader),
                $"Following configured leader {leader}.", state, options.RequireLearnedSpellForCastCommands, leader);
        return ClientGameplayActionPlan.None($"Leader {leader} is present; no party action is required.");
    }

    private static ClientGameplayActionPlan Action(
        ClientGameplayActionKind kind, string command, string reason,
        ClientGameplayAutomationSnapshot state, bool requireLearnedSpell,
        string target = "", int minimumManaPercent = 0)
    {
        command = command?.Trim() ?? string.Empty;
        if (command.Length == 0) return ClientGameplayActionPlan.None("Configured command is blank.");
        if (!IsSingleCommand(command)) return ClientGameplayActionPlan.None("Only a single plain command is allowed per action.");
        if (state.ManaPercent < minimumManaPercent)
            return ClientGameplayActionPlan.None($"Mana is below the configured {minimumManaPercent}% minimum.");
        if (requireLearnedSpell && IsCast(command) && !KnowsCommandSpell(state, command))
            return ClientGameplayActionPlan.None("The configured cast command does not match an observed learned spell.");
        return new(kind, command, reason, target);
    }

    private static bool Fresh(ClientGameplayAutomationSnapshot state, ClientGameplayAutomationProfile profile, DateTimeOffset now) =>
        state.CapturedAtUtc <= now.AddSeconds(1) && now - state.CapturedAtUtc <=
        TimeSpan.FromSeconds(Math.Clamp(profile.StateMaximumAgeSeconds, 2, 60));

    private static bool HasCondition(ClientGameplayAutomationSnapshot state, string value) =>
        state.Conditions.Any(condition => condition.Equals(value, StringComparison.OrdinalIgnoreCase));

    public static bool IsSingleCommand(string command) =>
        !string.IsNullOrWhiteSpace(command) && command.Length <= 512 &&
        !command.Any(char.IsControl) && !command.Contains(';') && !command.Contains('{') && !command.Contains('}');

    private static bool IdentityMatches(ClientGameplayAutomationSnapshot state, ClientGameplayAutomationProfile profile) =>
        state.WorldId.Equals(profile.WorldId, StringComparison.OrdinalIgnoreCase) &&
        state.CharacterId.Equals(profile.CharacterId, StringComparison.OrdinalIgnoreCase);

    private static bool IsCast(string command) =>
        command.StartsWith("cast ", StringComparison.OrdinalIgnoreCase) ||
        command.StartsWith("invoke ", StringComparison.OrdinalIgnoreCase);

    private static bool KnowsCommandSpell(ClientGameplayAutomationSnapshot state, string command)
    {
        var arguments = command[(command.IndexOf(' ') + 1)..].Trim();
        return state.LearnedSpells.Any(known =>
        {
            var separator = known.IndexOf(':');
            var name = (separator >= 0 ? known[(separator + 1)..] : known).Trim();
            return name.Length > 0 &&
                   (arguments.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    arguments.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase) ||
                    arguments.StartsWith('"' + name + '"', StringComparison.OrdinalIgnoreCase));
        });
    }

    private static ClientGameplayNpcState SelectTarget(
        IReadOnlyList<ClientGameplayNpcState> hostiles, ClientCombatTargetMode mode) => mode switch
    {
        ClientCombatTargetMode.LowestHealthPercent => hostiles.OrderBy(npc => npc.HealthPercent).ThenBy(npc => npc.Name).First(),
        ClientCombatTargetMode.LowestHealth => hostiles.OrderBy(npc => npc.CurrentHitPoints).ThenBy(npc => npc.Name).First(),
        ClientCombatTargetMode.LowestLevel => hostiles.OrderBy(npc => npc.Level).ThenBy(npc => npc.Name).First(),
        ClientCombatTargetMode.HighestLevel => hostiles.OrderByDescending(npc => npc.Level).ThenBy(npc => npc.Name).First(),
        ClientCombatTargetMode.HighestExperience => hostiles.OrderByDescending(npc => npc.ExperienceReward).ThenBy(npc => npc.Name).First(),
        _ => hostiles[0]
    };

    private static string ApplyTarget(string template, string target) =>
        (template ?? string.Empty).Replace("{target}", target, StringComparison.OrdinalIgnoreCase).Trim();

    private static string ApplyCombatRuleCommand(ClientCombatActionRule rule, string target)
    {
        var command = rule.Command?.Trim() ?? string.Empty;
        if (command.Length == 0) return string.Empty;
        if (command.Contains("{target}", StringComparison.OrdinalIgnoreCase))
            return ApplyTarget(command, target);
        return rule.AppendTarget && !rule.IsAreaAction ? $"{command} {target}" : command;
    }

    private static IReadOnlyList<string> SplitNames(string value) =>
        (value ?? string.Empty).Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public static class ClientGameplayDiagnostics
{
    public static IReadOnlyList<ClientDiagnosticCheck> Evaluate(
        ClientGameplayAutomationSnapshot? state,
        string selectedWorld,
        bool connected,
        bool gameplayReady,
        bool emergencyStop,
        bool assistantRunning,
        DateTimeOffset now)
    {
        var checks = new List<ClientDiagnosticCheck>
        {
            new("Connection", connected, connected ? "Connected" : "Disconnected"),
            new("Gameplay prompt", gameplayReady, gameplayReady ? "Ready" : "Not ready"),
            new("Emergency Stop", !emergencyStop, emergencyStop ? "Automation is stopped" : "Clear"),
            new("Assistant", true, assistantRunning ? "Running" : "Stopped")
        };
        if (state is null)
        {
            checks.Add(new("Authoritative state", false, "No server snapshot received"));
            return checks;
        }
        var expected = ClientGameplayAutomationStateDecoder.NormalizeIdentity(selectedWorld);
        checks.Add(new("World identity", state.WorldId.Equals(expected, StringComparison.OrdinalIgnoreCase),
            $"Selected {expected}; received {state.WorldId}"));
        checks.Add(new("Character identity", !string.IsNullOrWhiteSpace(state.CharacterId), state.CharacterName));
        checks.Add(new("Map-qualified room", state.RoomId.StartsWith("room.map", StringComparison.OrdinalIgnoreCase), state.RoomId));
        var age = now - state.CapturedAtUtc;
        checks.Add(new("Snapshot freshness", age >= TimeSpan.Zero && age <= TimeSpan.FromSeconds(10),
            $"{Math.Max(0, age.TotalSeconds):0.0} seconds old"));
        checks.Add(new("Resource bounds", state.HealthCurrent <= state.HealthMaximum && state.ManaCurrent <= state.ManaMaximum,
            $"HP {state.HealthCurrent}/{state.HealthMaximum}; resource {state.ManaCurrent}/{state.ManaMaximum}"));
        return checks;
    }
}
