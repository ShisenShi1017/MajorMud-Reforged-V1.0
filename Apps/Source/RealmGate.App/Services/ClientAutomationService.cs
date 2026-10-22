using System.IO;
using System.Text.Json;
using RealmGate.App.Models;
using RealmGate.Client.Automation;

namespace RealmGate.App.Services;

public interface IClientAutomationService
{
    ClientAutomationProfile Profile { get; }
    ClientAutomationPolicy Policy { get; }
    event EventHandler<ClientAutomationCommandRequest>? CommandRequested;
    Task LoadAsync();
    Task SaveAsync();
    IReadOnlyList<ClientAutomationCommandRequest> Evaluate(ClientAutomationContext context);
    IReadOnlyList<ClientAutomationCommandRequest> EvaluateTimedEvents(ClientAutomationContext context, bool connected);
    bool TryResolveHumanInput(string input, out IReadOnlyList<string> commands);
    bool CanContinueInputAutomation { get; }
    void SetMasterEnabled(bool enabled);
    void StopAll();
}

public sealed class ClientAutomationService : IClientAutomationService
{
    internal const int CurrentProfileSchemaVersion = 3;
    private readonly IApplicationSettingsService _settings;
    private readonly Dictionary<string, DateTimeOffset> _lastCommandAtBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Dictionary<string, DateTimeOffset> _ruleLastRun = new(StringComparer.OrdinalIgnoreCase);
    private readonly ClientTimedEventScheduler _timedEvents = new();

    public ClientAutomationProfile Profile { get; private set; } = CreateDefaultProfile();
    public ClientAutomationPolicy Policy { get; } = new();
    public event EventHandler<ClientAutomationCommandRequest>? CommandRequested;
    public bool CanContinueInputAutomation => !Profile.Capabilities.EmergencyStop
        && Policy.AutomationAllowed;

    public ClientAutomationService(IApplicationSettingsService settings) => _settings = settings;

    public async Task LoadAsync()
    {
        var path = GetPath();
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<ClientAutomationProfile>(await File.ReadAllTextAsync(path), _json);
                if (loaded is not null)
                {
                    Profile = loaded;
                    EnsureProfileDefaults(Profile);
                    MigrateLegacyProfile(Profile);
                }
            }
            catch
            {
                Profile = CreateDefaultProfile();
            }
        }

        _ruleLastRun.Clear();
        _lastCommandAtBySession.Clear();
        _timedEvents.Reset();
    }

    public async Task SaveAsync()
    {
        Profile.SchemaVersion = CurrentProfileSchemaVersion;
        SynchronizeRulesFromOptions(Profile);
        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Profile, _json));
    }

    public IReadOnlyList<ClientAutomationCommandRequest> Evaluate(ClientAutomationContext context)
    {
        if (Profile.Capabilities.EmergencyStop || !Policy.AutomationAllowed) return [];
        SynchronizeRulesFromOptions(Profile);
        var delay = Math.Max(Profile.MinimumCommandDelayMs, Policy.MinimumCommandDelayMs);
        if (_lastCommandAtBySession.TryGetValue(context.SessionId, out var lastCommandAt)
            && (context.Timestamp - lastCommandAt).TotalMilliseconds < delay) return [];

        var candidates = new List<(int Priority, ClientAutomationCommandRequest Request)>();
        if (Profile.Capabilities.TextTriggersEnabled)
        {
            foreach (var trigger in Profile.Capabilities.TextTriggers
                         .Where(trigger => trigger.Enabled)
                         .OrderBy(trigger => trigger.Priority)
                         .ThenBy(trigger => trigger.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(trigger.MatchText) || string.IsNullOrWhiteSpace(trigger.Command)) continue;
                if (trigger.OnlyOutOfCombat && context.InCombat) continue;
                if (trigger.OnlyInCombat && !context.InCombat) continue;
                if (_ruleLastRun.TryGetValue(RunKey(context.SessionId, trigger.Id), out var last) &&
                    (context.Timestamp - last).TotalSeconds < Math.Max(0, trigger.CooldownSeconds)) continue;
                if (!ClientAutomationExpansionEngine.TryMatchTrigger(trigger, context.LastLine, out var captures, out _)) continue;
                if (!ClientAutomationExpansionEngine.TryExpandTriggerResponse(trigger.Command, captures, out var commands)) continue;

                var name = string.IsNullOrWhiteSpace(trigger.Name)
                    ? $"Text trigger: {trigger.MatchText.Trim()}"
                    : trigger.Name.Trim();
                candidates.Add((trigger.Priority, new ClientAutomationCommandRequest(
                    trigger.Id, name, commands[0], AutomationArea.Utility,
                    context.Timestamp, context.SessionId, commands)));
            }
        }

        foreach (var rule in Profile.Rules
                     .Where(r => Profile.Capabilities.TextTriggersEnabled && r.Enabled && IsPermitted(r) &&
                                 ContextAllowed(r, context) && CooldownReady(r, context.Timestamp, context.SessionId) &&
                                 Matches(r, context) && !string.IsNullOrWhiteSpace(r.Command))
                     .OrderBy(r => r.Priority)
                     .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
        {
            var commands = ClientAutomationExpansionEngine.SplitCommandSteps(rule.Command);
            if (commands.Count == 0) continue;
            candidates.Add((rule.Priority, new ClientAutomationCommandRequest(
                rule.Id, rule.Name, commands[0], rule.Area, context.Timestamp, context.SessionId, commands)));
        }

        var request = candidates
            .OrderBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Request.RuleId, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Request)
            .FirstOrDefault();
        if (request is null) return [];

        _lastCommandAtBySession[context.SessionId] = context.Timestamp;
        _ruleLastRun[RunKey(context.SessionId, request.RuleId)] = context.Timestamp;
        CommandRequested?.Invoke(this, request);
        return [request];
    }

    public IReadOnlyList<ClientAutomationCommandRequest> EvaluateTimedEvents(ClientAutomationContext context, bool connected)
    {
        var delay = Math.Max(Profile.MinimumCommandDelayMs, Policy.MinimumCommandDelayMs);
        var match = _timedEvents.Evaluate(Profile.Options.Events, new(
            context.SessionId, context.Timestamp, connected, connected, context.InCombat,
            CanContinueInputAutomation && Profile.Capabilities.TimedEventsEnabled, delay,
            _lastCommandAtBySession.TryGetValue(context.SessionId, out var lastCommandAt) ? lastCommandAt : null));
        if (match is null) return [];
        var request = new ClientAutomationCommandRequest(
            match.RuleId, match.RuleName, match.Commands[0], AutomationArea.Events,
            context.Timestamp, context.SessionId, match.Commands);
        _lastCommandAtBySession[context.SessionId] = context.Timestamp;
        CommandRequested?.Invoke(this, request);
        return [request];
    }

    public bool TryResolveHumanInput(string input, out IReadOnlyList<string> commands)
    {
        var capabilities = Profile.Capabilities;
        var runtime = new ClientInputAutomationRuntime(new ClientInputAutomationProfile
        {
            AliasesEnabled = capabilities.AliasesEnabled,
            MacrosEnabled = capabilities.MacrosEnabled,
            TextTriggersEnabled = capabilities.TextTriggersEnabled,
            EmergencyStop = !CanContinueInputAutomation,
            Aliases = capabilities.Aliases,
            Macros = capabilities.Macros,
            TextTriggers = capabilities.TextTriggers
        });
        return runtime.TryResolveHumanInput(input, out commands);
    }

    public void SetMasterEnabled(bool enabled)
    {
        Profile.Enabled = enabled;
        Profile.PauseAll = !enabled;
        Profile.Capabilities.EmergencyStop = !enabled;
        Policy.AutomationAllowed = enabled;
    }

    public void StopAll()
    {
        Profile.Capabilities.EmergencyStop = true;
        Profile.Enabled = false;
        Profile.PauseAll = true;
        _ruleLastRun.Clear();
        _lastCommandAtBySession.Clear();
        _timedEvents.Reset();
    }

    private bool IsPermitted(ClientAutomationRule rule) => rule.Area switch
    {
        AutomationArea.Roaming => ClientAutomationCapabilityPolicy.Allows(Profile.Capabilities, rule.Area) && Policy.MovementAllowed,
        AutomationArea.Combat or AutomationArea.PvP => ClientAutomationCapabilityPolicy.Allows(Profile.Capabilities, rule.Area) && Policy.CombatAllowed,
        AutomationArea.Spells or AutomationArea.Health => ClientAutomationCapabilityPolicy.Allows(Profile.Capabilities, rule.Area) && Policy.SpellcastingAllowed,
        AutomationArea.LootAndCash or AutomationArea.Utility => ClientAutomationCapabilityPolicy.Allows(Profile.Capabilities, rule.Area) && Policy.ItemUseAllowed,
        AutomationArea.Party => ClientAutomationCapabilityPolicy.Allows(Profile.Capabilities, rule.Area),
        _ => true
    };

    private bool CooldownReady(ClientAutomationRule rule, DateTimeOffset now, string sessionId)
        => !_ruleLastRun.TryGetValue(RunKey(sessionId, rule.Id), out var last) || (now - last).TotalSeconds >= Math.Max(0, rule.CooldownSeconds);

    private static string RunKey(string sessionId, string ruleId) => $"{sessionId}\u001f{ruleId}";

    private static bool ContextAllowed(ClientAutomationRule rule, ClientAutomationContext context)
        => (!rule.OnlyOutOfCombat || !context.InCombat)
           && (!rule.OnlyInCombat || context.InCombat)
           && (rule.MinimumHealthPercent <= 0 || context.HealthPercent >= rule.MinimumHealthPercent)
           && (rule.MinimumManaPercent <= 0 || context.ManaPercent >= rule.MinimumManaPercent);

    private static bool Matches(ClientAutomationRule rule, ClientAutomationContext c)
    {
        var trigger = rule.Trigger.Trim().ToLowerInvariant();
        return trigger switch
        {
            "health-below" => c.HealthPercent <= rule.Threshold,
            "mana-below" => c.ManaPercent <= rule.Threshold,
            "in-combat" => c.InCombat,
            "out-of-combat" => !c.InCombat,
            "resting" => c.IsResting,
            "moving" => c.IsMoving,
            "text-contains" => rule.Description.Length > 0 && c.LastLine.Contains(rule.Description, StringComparison.OrdinalIgnoreCase),
            "health-above" => c.HealthPercent >= rule.Threshold,
            "mana-above" => c.ManaPercent >= rule.Threshold,
            "combat-started" => c.InCombat && c.LastLine.Contains("attack", StringComparison.OrdinalIgnoreCase),
            "combat-ended" => !c.InCombat && (c.LastLine.Contains("combat", StringComparison.OrdinalIgnoreCase) || c.LastLine.Contains("dead", StringComparison.OrdinalIgnoreCase)),
            "room-entered" => c.IsMoving,
            "always" => true,
            _ => false
        };
    }

    private string GetPath() => Path.Combine(_settings.RootPath, "Client", "Profiles", "automation-default.json");

    private static ClientAutomationProfile CreateDefaultProfile()
    {
        var profile = new ClientAutomationProfile { SchemaVersion = CurrentProfileSchemaVersion };
        EnsureProfileDefaults(profile);
        Add(profile, AutomationArea.Health, "Auto Minor Heal", "Use the configured minor healing command at the first health threshold.", "health-below", "cast minor heal", 80, 10, 8);
        Add(profile, AutomationArea.Health, "Auto Major Heal", "Use the configured major healing command at the emergency threshold.", "health-below", "cast major heal", 55, 1, 5);
        Add(profile, AutomationArea.Health, "Auto Cure Poison", "React when the Host reports poison.", "text-contains", "cast cure poison", 0, 4, 15, "poison");
        Add(profile, AutomationArea.Health, "Auto Cure Disease", "React when the Host reports disease.", "text-contains", "cast cure disease", 0, 5, 15, "disease");
        Add(profile, AutomationArea.Health, "Auto Cure Blindness", "React when the Host reports blindness.", "text-contains", "cast cure blindness", 0, 6, 15, "blind");
        Add(profile, AutomationArea.Health, "Emergency Healing Item", "Use the configured healing item at the emergency threshold.", "health-below", "use healing potion", 35, 2, 10);
        Add(profile, AutomationArea.Health, "Stop Resting When Recovered", "Stand when mana has recovered to the configured percentage.", "mana-above", "stand", 90, 35, 10, onlyOutOfCombat: true);
        Add(profile, AutomationArea.Health, "Auto Rest", "Rest when mana reaches the configured percentage.", "mana-below", "rest", 25, 30, 20, onlyOutOfCombat: true);
        Add(profile, AutomationArea.Health, "Auto Meditate", "Meditate when mana is low and the character is safe.", "mana-below", "meditate", 15, 25, 20, onlyOutOfCombat: true);
        Add(profile, AutomationArea.Health, "Auto Cure Silence", "React when the Host reports that spellcasting is silenced.", "text-contains", "cast remove silence", 0, 3, 15, "silenced");
        Add(profile, AutomationArea.Health, "Emergency Recall", "Use the configured emergency travel command at critical health.", "health-below", "recall", 15, -2, 20);
        Add(profile, AutomationArea.Health, "Auto Wake", "Wake when the Host reports that the character is asleep.", "text-contains", "wake", 0, 7, 5, "you are asleep");
        Add(profile, AutomationArea.Spells, "Maintain Bless", "Maintain the configured blessing while out of combat.", "out-of-combat", "cast bless", 0, 50, 120, onlyOutOfCombat: true);
        Add(profile, AutomationArea.Spells, "Maintain Armor", "Maintain the configured defensive spell while out of combat.", "out-of-combat", "cast armor", 0, 55, 120, onlyOutOfCombat: true);
        Add(profile, AutomationArea.Spells, "Auto Room Light", "Create light when darkness is detected.", "text-contains", "cast light", 0, 30, 30, "too dark");
        Add(profile, AutomationArea.Spells, "Retry Failed Cast", "Retry the configured spell command after a failed cast message.", "text-contains", "cast", 0, 45, 5, "failed to cast");
        Add(profile, AutomationArea.Spells, "Recast After Dispel", "Recast a maintained effect after the Host reports that it faded or was dispelled.", "text-contains", "cast bless", 0, 46, 15, "spell fades");
        Add(profile, AutomationArea.Combat, "Auto Attack", "Attack the current Host-selected target.", "in-combat", "attack", 0, 20, 3, onlyInCombat: true);
        Add(profile, AutomationArea.Combat, "Auto Bash", "Attempt the configured bash command during combat.", "in-combat", "bash", 0, 24, 8, onlyInCombat: true);
        Add(profile, AutomationArea.Combat, "Auto Retarget", "Acquire another target after the current opponent is defeated.", "combat-ended", "attack", 0, 22, 3);
        Add(profile, AutomationArea.Combat, "Auto Rescue Party", "Attempt the configured rescue command when a party member needs help.", "text-contains", "rescue", 0, 12, 5, "needs your help", onlyInCombat: true);
        Add(profile, AutomationArea.Combat, "Auto Flee", "Flee at the configured safety threshold.", "health-below", "flee", 25, 0, 2, onlyInCombat: true);
        Add(profile, AutomationArea.Combat, "Defensive Action", "Use a defensive command at the configured low-health threshold.", "health-below", "defend", 40, 3, 8, onlyInCombat: true);
        Add(profile, AutomationArea.Combat, "Low Mana Escape", "Use the configured escape command when mana is critically low in combat.", "mana-below", "flee", 10, 4, 8, onlyInCombat: true);
        Add(profile, AutomationArea.PvP, "PvP Alert", "Play the configured PvP reaction command when another player attacks.", "text-contains", "flee", 0, 1, 2, "attacks you");
        Add(profile, AutomationArea.PvP, "PvP Return Attack", "Return an attack using the configured command.", "text-contains", "attack", 0, 2, 2, "attacks you");
        Add(profile, AutomationArea.PvP, "Chase PvP Opponent", "Use the configured chase command when an attacking player flees.", "text-contains", "chase", 0, 3, 3, "flees from you");
        Add(profile, AutomationArea.Party, "Assist Leader", "Assist the current party leader during combat.", "in-combat", "assist", 0, 25, 5, onlyInCombat: true);
        Add(profile, AutomationArea.Party, "Follow Leader", "Follow when the leader moves.", "text-contains", "follow", 0, 40, 2, "leaves");
        Add(profile, AutomationArea.Party, "Accept Invite", "Accept a trusted party invitation.", "text-contains", "join", 0, 10, 5, "invites you");
        Add(profile, AutomationArea.Party, "Heal Wounded Party", "Use the configured party-heal command when a member is reported wounded.", "text-contains", "cast heal", 0, 8, 8, "badly wounded");
        Add(profile, AutomationArea.Party, "Regroup When Separated", "Run the configured regroup command when the leader is lost.", "text-contains", "where", 0, 15, 10, "cannot follow");
        Add(profile, AutomationArea.Party, "Wait For Leader", "Wait safely when the Host reports that the leader is missing.", "text-contains", "wait", 0, 16, 8, "leader is not here");
        Add(profile, AutomationArea.Party, "Resume With Leader", "Resume following when the leader returns.", "text-contains", "follow", 0, 17, 8, "leader arrives");
        Add(profile, AutomationArea.Roaming, "Auto Search", "Search after entering a room.", "room-entered", "search", 0, 70, 5);
        Add(profile, AutomationArea.Roaming, "Auto Sneak", "Attempt to maintain stealth while moving.", "moving", "sneak", 0, 40, 10);
        Add(profile, AutomationArea.Roaming, "Auto Track", "Use the configured tracking command while roaming.", "room-entered", "track", 0, 75, 10);
        Add(profile, AutomationArea.Roaming, "Auto Open Doors", "Open a door when the Host reports it closed.", "text-contains", "open door", 0, 15, 2, "closed");
        Add(profile, AutomationArea.Roaming, "Auto Pick Locks", "Attempt the configured pick-lock command.", "text-contains", "pick lock", 0, 16, 4, "locked");
        Add(profile, AutomationArea.Roaming, "Auto Bash Doors", "Attempt the configured bash command when a door blocks travel.", "text-contains", "bash door", 0, 17, 4, "won't budge");
        Add(profile, AutomationArea.Roaming, "Auto Detect Traps", "Check for traps when the Host reports a suspicious passage or container.", "text-contains", "detect traps", 0, 18, 6, "trap");
        Add(profile, AutomationArea.Roaming, "Auto Disarm Traps", "Attempt the configured disarm command after a trap is found.", "text-contains", "disarm trap", 0, 19, 6, "trap detected");
        Add(profile, AutomationArea.LootAndCash, "Auto Get Cash", "Collect visible currency.", "text-contains", "get coins", 0, 60, 3, "coins");
        Add(profile, AutomationArea.LootAndCash, "Auto Get Items", "Collect items matching Client loot rules.", "text-contains", "get all", 0, 65, 3, "you see");
        Add(profile, AutomationArea.LootAndCash, "Auto Deposit", "Deposit cash when the configured Client threshold is reached.", "text-contains", "deposit all", 0, 80, 30, "banker");
        Add(profile, AutomationArea.LootAndCash, "Auto Loot Corpse", "Loot a defeated enemy using the configured corpse command.", "text-contains", "get all corpse", 0, 55, 3, "corpse");
        Add(profile, AutomationArea.LootAndCash, "Convert Currency", "Convert carried denominations when a banker is recognized.", "text-contains", "exchange all", 0, 82, 30, "banker");
        Add(profile, AutomationArea.LootAndCash, "Auto Sell At Shop", "Sell configured loot when a shopkeeper is recognized.", "text-contains", "sell all", 0, 84, 20, "shopkeeper");
        Add(profile, AutomationArea.LootAndCash, "Drop When Overweight", "Use the configured drop command after an overweight rejection.", "text-contains", "drop heavy", 0, 5, 10, "too heavy");
        Add(profile, AutomationArea.Utility, "Auto Hide", "Hide when entering a safe room.", "room-entered", "hide", 0, 45, 10, onlyOutOfCombat: true);
        Add(profile, AutomationArea.Utility, "Auto Stand", "Stand after the Host reports that the character is resting.", "text-contains", "stand", 0, 5, 4, "you are resting");
        Add(profile, AutomationArea.Utility, "Auto Recover", "Use the configured recovery command after incapacitation.", "text-contains", "recover", 0, 2, 10, "incapacitated");
        Add(profile, AutomationArea.Utility, "Auto Look", "Refresh the room display after movement.", "room-entered", "look", 0, 90, 3);
        Add(profile, AutomationArea.Events, "Scheduled Event", "User-defined timer or event command.", "always", string.Empty, 0, 100, 60);
        return profile;
    }

    private static void MigrateLegacyProfile(ClientAutomationProfile profile)
    {
        if (profile.SchemaVersion >= CurrentProfileSchemaVersion) return;
        var options = profile.Options;
        var capabilities = profile.Capabilities;
        capabilities.CombatEnabled |= options.Combat.AutoAttack;
        capabilities.HealthRecoveryEnabled |= options.Health.AutoHeal || options.Health.AutoRest || options.Health.AutoMeditate ||
            options.Health.AutoUseHealingItem || options.Health.AutoRecallAtCriticalHealth;
        capabilities.SpellMaintenanceEnabled |= options.Spells.MaintainBless || options.Spells.MaintainArmor || options.Spells.MaintainRoomLight;
        capabilities.PartyAutomationEnabled |= options.Party.AutoAssistLeader || options.Party.AutoFollowLeader ||
            options.Party.AutoAcceptTrustedInvites || options.Party.RegroupWhenSeparated;
        capabilities.LootAutomationEnabled |= options.LootAndCash.GetItems || options.LootAndCash.GetCash ||
            options.LootAndCash.LootCorpses || options.LootAndCash.AutoSellAtShop || options.LootAndCash.DepositAtBank;
        capabilities.RoamingAutomationEnabled |= options.Roaming.AutoSneak || options.Roaming.AutoSearch || options.Roaming.AutoTrack ||
            options.Roaming.DetectTraps || options.Roaming.DisarmTraps || options.Utility.AutoHide || options.Utility.AutoLook;
        capabilities.PvpAutomationEnabled |= options.PvP.Enabled;
        capabilities.CommunicationsAutomationEnabled |= capabilities.AwayFromKeyboardEnabled;
        profile.SchemaVersion = CurrentProfileSchemaVersion;
    }

    private static void SynchronizeRulesFromOptions(ClientAutomationProfile profile)
    {
        EnsureProfileDefaults(profile);
        var o = profile.Options;
        Configure(profile, "Auto Minor Heal", o.Health.AutoHeal, o.Spells.MinorHealCommand, o.Health.MinorHealAtPercent);
        Configure(profile, "Auto Major Heal", o.Health.AutoHeal, o.Spells.MajorHealCommand, o.Health.MajorHealAtPercent);
        Configure(profile, "Auto Cure Poison", o.Health.CurePoison, "cast cure poison");
        Configure(profile, "Auto Cure Disease", o.Health.CureDisease, "cast cure disease");
        Configure(profile, "Auto Cure Blindness", o.Health.CureBlindness, o.Health.CureBlindnessCommand);
        Configure(profile, "Emergency Healing Item", o.Health.AutoUseHealingItem, o.Health.HealingItemCommand, o.Health.HealingItemAtPercent);
        Configure(profile, "Stop Resting When Recovered", o.Health.StopRestingWhenRecovered, "stand", o.Health.StopRestingManaPercent);
        Configure(profile, "Auto Rest", o.Health.AutoRest, "rest", o.Health.RestManaAtPercent);
        Configure(profile, "Auto Meditate", o.Health.AutoMeditate, "meditate", o.Health.RestManaAtPercent);
        Configure(profile, "Auto Cure Silence", o.Health.CureSilence, o.Health.CureSilenceCommand);
        Configure(profile, "Emergency Recall", o.Health.AutoRecallAtCriticalHealth, o.Health.RecallCommand, o.Health.RecallAtPercent);
        Configure(profile, "Auto Wake", o.Health.AutoWake, o.Health.WakeCommand);
        Configure(profile, "Maintain Bless", o.Spells.MaintainBless, o.Spells.BlessCommand, cooldownSeconds: o.Spells.MaintainedSpellCooldownSeconds, onlyOutOfCombat: o.Spells.CastMaintainedSpellsOnlyOutOfCombat);
        Configure(profile, "Maintain Armor", o.Spells.MaintainArmor, o.Spells.ArmorCommand, cooldownSeconds: o.Spells.MaintainedSpellCooldownSeconds, onlyOutOfCombat: o.Spells.CastMaintainedSpellsOnlyOutOfCombat);
        Configure(profile, "Auto Room Light", o.Spells.MaintainRoomLight, o.Spells.LightCommand);
        Configure(profile, "Retry Failed Cast", o.Spells.RetryFailedCast, o.Spells.RetryFailedCastCommand);
        Configure(profile, "Recast After Dispel", o.Spells.RecastAfterDispel, o.Spells.RecastAfterDispelCommand);
        Configure(profile, "Auto Attack", o.Combat.AutoAttack, o.Combat.PrimaryAttackCommand);
        Configure(profile, "Auto Bash", o.Combat.UseSecondaryAttack, o.Combat.SecondaryAttackCommand);
        Configure(profile, "Auto Retarget", o.Combat.AutoRetarget, o.Combat.RetargetCommand);
        Configure(profile, "Auto Rescue Party", o.Combat.AutoRescueParty, o.Combat.RescueCommand);
        Configure(profile, "Auto Flee", o.Combat.AutoFlee, "flee", o.Combat.FleeAtPercent);
        Configure(profile, "Defensive Action", o.Combat.UseDefensiveActionAtLowHealth, o.Combat.DefensiveActionCommand, o.Combat.DefensiveActionAtPercent);
        Configure(profile, "Low Mana Escape", o.Combat.EscapeAtLowMana, o.Combat.LowManaEscapeCommand, o.Combat.LowManaEscapeAtPercent);
        Configure(profile, "PvP Alert", o.PvP.Enabled && o.PvP.AlertWhenAttacked, o.PvP.ReactionCommand);
        Configure(profile, "PvP Return Attack", o.PvP.Enabled && o.PvP.AutoReturnAttack, o.PvP.ReturnAttackCommand);
        Configure(profile, "Chase PvP Opponent", o.PvP.Enabled && o.PvP.ChaseFleeingAttacker, o.PvP.ChaseCommand);
        Configure(profile, "Assist Leader", o.Party.AutoAssistLeader, JoinCommand(o.Party.AssistCommand, o.Party.LeaderName));
        Configure(profile, "Follow Leader", o.Party.AutoFollowLeader, JoinCommand(o.Party.FollowCommand, o.Party.LeaderName));
        Configure(profile, "Accept Invite", o.Party.AutoAcceptTrustedInvites, o.Party.AcceptInviteCommand);
        Configure(profile, "Heal Wounded Party", o.Party.AutoHealParty, o.Party.HealPartyCommand);
        Configure(profile, "Regroup When Separated", o.Party.RegroupWhenSeparated, o.Party.RegroupCommand);
        Configure(profile, "Wait For Leader", o.Party.WaitWhenLeaderMissing, o.Party.WaitCommand);
        Configure(profile, "Resume With Leader", o.Party.ResumeWhenLeaderReturns, JoinCommand(o.Party.ResumeCommand, o.Party.LeaderName));
        Configure(profile, "Auto Get Cash", o.LootAndCash.GetCash, o.LootAndCash.GetCashCommand);
        Configure(profile, "Auto Get Items", o.LootAndCash.GetItems, o.LootAndCash.GetItemsCommand);
        Configure(profile, "Auto Loot Corpse", o.LootAndCash.LootCorpses, o.LootAndCash.LootCorpseCommand);
        Configure(profile, "Auto Deposit", o.LootAndCash.DepositAtBank, o.LootAndCash.DepositCommand);
        Configure(profile, "Convert Currency", o.LootAndCash.ConvertCurrencyAtBank, o.LootAndCash.ConvertCurrencyCommand);
        Configure(profile, "Auto Sell At Shop", o.LootAndCash.AutoSellAtShop, o.LootAndCash.SellCommand);
        Configure(profile, "Drop When Overweight", o.LootAndCash.DropWhenOverweight, o.LootAndCash.DropOverweightCommand);
        Configure(profile, "Auto Search", o.Roaming.AutoSearch, o.Roaming.SearchCommand);
        Configure(profile, "Auto Sneak", o.Roaming.AutoSneak, o.Roaming.SneakCommand);
        Configure(profile, "Auto Track", o.Roaming.AutoTrack, o.Roaming.TrackCommand);
        Configure(profile, "Auto Open Doors", o.Roaming.OpenClosedDoors, o.Roaming.OpenDoorCommand);
        Configure(profile, "Auto Pick Locks", o.Roaming.PickLockedDoors, o.Roaming.PickLockCommand);
        Configure(profile, "Auto Bash Doors", o.Roaming.BashBlockedDoors, o.Roaming.BashDoorCommand);
        Configure(profile, "Auto Detect Traps", o.Roaming.DetectTraps, o.Roaming.DetectTrapCommand);
        Configure(profile, "Auto Disarm Traps", o.Roaming.DisarmTraps, o.Roaming.DisarmTrapCommand);
        Configure(profile, "Auto Hide", o.Utility.AutoHide, o.Utility.HideCommand);
        Configure(profile, "Auto Stand", o.Utility.AutoStand, o.Utility.StandCommand);
        Configure(profile, "Auto Recover", o.Utility.AutoRecover, o.Utility.RecoverCommand);
        Configure(profile, "Auto Look", o.Utility.AutoLook, o.Utility.LookCommand);
        SynchronizePriorityRules(profile, AutomationArea.Spells, "Priority Spell", o.Spells.PriorityActions);
        SynchronizePriorityRules(profile, AutomationArea.Combat, "Priority Combat", o.Combat.PriorityActions);
    }

    private static void SynchronizePriorityRules(ClientAutomationProfile profile, AutomationArea area, string prefix, IEnumerable<ClientPriorityAction> actions)
    {
        var activeIds = new HashSet<string>(actions.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var obsolete in profile.Rules.Where(r => r.Area == area && r.Name.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase) && !activeIds.Contains(r.Id)).ToArray())
            profile.Rules.Remove(obsolete);
        foreach (var action in actions)
        {
            var rule = profile.Rules.FirstOrDefault(r => string.Equals(r.Id, action.Id, StringComparison.OrdinalIgnoreCase));
            if (rule is null)
            {
                rule = new ClientAutomationRule { Id = action.Id, Area = area };
                profile.Rules.Add(rule);
            }
            rule.Name = $"{prefix}: {action.Name}";
            rule.Description = "User-configured priority action";
            rule.Enabled = action.Enabled;
            rule.Command = action.Command.Trim();
            rule.Priority = action.Priority;
            rule.Trigger = action.OnlyInCombat ? "in-combat" : action.OnlyOutOfCombat ? "out-of-combat" : "always";
            rule.OnlyInCombat = action.OnlyInCombat;
            rule.OnlyOutOfCombat = action.OnlyOutOfCombat;
            rule.MinimumHealthPercent = action.MinimumHealthPercent;
            rule.MinimumManaPercent = action.MinimumManaPercent;
            rule.CooldownSeconds = 3;
        }
    }

    private static void Configure(ClientAutomationProfile profile, string name, bool enabled, string command, int? threshold = null, int? cooldownSeconds = null, bool? onlyOutOfCombat = null)
    {
        var rule = profile.Rules.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        if (rule is null) return;
        rule.Enabled = enabled;
        rule.Command = command.Trim();
        if (threshold.HasValue) rule.Threshold = threshold.Value;
        if (cooldownSeconds.HasValue) rule.CooldownSeconds = Math.Max(0, cooldownSeconds.Value);
        if (onlyOutOfCombat.HasValue) rule.OnlyOutOfCombat = onlyOutOfCombat.Value;
    }

    private static string JoinCommand(string command, string argument)
        => string.IsNullOrWhiteSpace(argument) ? command.Trim() : $"{command.Trim()} {argument.Trim()}";

    private static void EnsureProfileDefaults(ClientAutomationProfile profile)
    {
        profile.Options ??= new ClientAutomationOptions();
        profile.Capabilities ??= new ClientAutomationCapabilities();
        profile.Capabilities.Aliases ??= [];
        profile.Capabilities.Macros ??= [];
        profile.Capabilities.TextTriggers ??= [];
        foreach (var alias in profile.Capabilities.Aliases)
        {
            alias.Id = string.IsNullOrWhiteSpace(alias.Id) ? Guid.NewGuid().ToString("N") : alias.Id;
            alias.Name = alias.Name?.Trim() ?? string.Empty;
            alias.Command ??= string.Empty;
        }
        foreach (var macro in profile.Capabilities.Macros)
        {
            macro.Id = string.IsNullOrWhiteSpace(macro.Id) ? Guid.NewGuid().ToString("N") : macro.Id;
            macro.Name = macro.Name?.Trim() ?? string.Empty;
            macro.Commands ??= [];
        }
        foreach (var trigger in profile.Capabilities.TextTriggers)
        {
            trigger.Id = string.IsNullOrWhiteSpace(trigger.Id) ? Guid.NewGuid().ToString("N") : trigger.Id;
            trigger.Name ??= string.Empty;
            trigger.MatchText ??= string.Empty;
            trigger.Command ??= string.Empty;
            trigger.CooldownSeconds = Math.Clamp(trigger.CooldownSeconds, 0, 86400);
            trigger.Priority = Math.Clamp(trigger.Priority, -100000, 100000);
            if (trigger.OnlyOutOfCombat && trigger.OnlyInCombat) trigger.OnlyInCombat = false;
        }
        profile.Options.Health ??= new HealthAutomationOptions();
        profile.Options.Spells ??= new SpellAutomationOptions();
        profile.Options.Combat ??= new CombatAutomationOptions();
        profile.Options.PvP ??= new PvpAutomationOptions();
        profile.Options.Party ??= new PartyAutomationOptions();
        profile.Options.LootAndCash ??= new LootCashAutomationOptions();
        profile.Options.Roaming ??= new RoamingAutomationOptions();
        profile.Options.Utility ??= new UtilityAutomationOptions();
        profile.Options.Events ??= new EventAutomationOptions();
        profile.Options.Presentation ??= new ClientPresentationOptions();
        profile.Options.Sounds ??= new ClientSoundOptions();
        profile.Options.Health.MinorHealAtPercent = Math.Clamp(profile.Options.Health.MinorHealAtPercent, 0, 100);
        profile.Options.Health.MajorHealAtPercent = Math.Clamp(profile.Options.Health.MajorHealAtPercent, 0, 100);
        profile.Options.Health.FleeAtPercent = Math.Clamp(profile.Options.Health.FleeAtPercent, 0, 100);
        profile.Options.Health.RestManaAtPercent = Math.Clamp(profile.Options.Health.RestManaAtPercent, 0, 100);
        profile.Options.Health.HealingItemAtPercent = Math.Clamp(profile.Options.Health.HealingItemAtPercent, 0, 100);
        profile.Options.Health.StopRestingManaPercent = Math.Clamp(profile.Options.Health.StopRestingManaPercent, 0, 100);
        profile.Options.Health.RecallAtPercent = Math.Clamp(profile.Options.Health.RecallAtPercent, 0, 100);
        profile.Options.Combat.FleeAtPercent = Math.Clamp(profile.Options.Combat.FleeAtPercent, 0, 100);
        profile.Options.Party.HealPartyBelowPercent = Math.Clamp(profile.Options.Party.HealPartyBelowPercent, 0, 100);
        profile.Options.Party.PartyStateMaximumAgeSeconds = Math.Clamp(profile.Options.Party.PartyStateMaximumAgeSeconds, 5, 300);
        profile.Options.Party.CommandCooldownSeconds = Math.Clamp(profile.Options.Party.CommandCooldownSeconds, 1, 60);
        profile.Options.PvP.FleeAtPercent = Math.Clamp(profile.Options.PvP.FleeAtPercent, 0, 100);
        profile.Options.PvP.ReturnAttackAtOrAbovePercent = Math.Clamp(profile.Options.PvP.ReturnAttackAtOrAbovePercent, 0, 100);
        profile.Options.PvP.CommandCooldownSeconds = Math.Clamp(profile.Options.PvP.CommandCooldownSeconds, 1, 60);
        profile.Options.PvP.AttackerStateMaximumAgeSeconds = Math.Clamp(profile.Options.PvP.AttackerStateMaximumAgeSeconds, 2, 60);
        profile.Options.Roaming.RoomActionDelayMilliseconds = Math.Clamp(profile.Options.Roaming.RoomActionDelayMilliseconds, 500, 10000);
        profile.Options.LootAndCash.DepositWhenCarrying = Math.Max(0, profile.Options.LootAndCash.DepositWhenCarrying);
        profile.Options.LootAndCash.KeepOnCharacter = Math.Max(0, profile.Options.LootAndCash.KeepOnCharacter);
        profile.Options.LootAndCash.MaximumItemWeight = Math.Max(0, profile.Options.LootAndCash.MaximumItemWeight);
        profile.Options.LootAndCash.StopLootingAtWeightPercent = Math.Clamp(profile.Options.LootAndCash.StopLootingAtWeightPercent, 1, 999);
        profile.Options.Toolbar ??= [];
        if (profile.Options.Toolbar.Count == 0)
        {
            var labels = new[] { "Health", "Spells", "Combat", "Party", "Roaming", "Loot & Cash", "Utility", "Events" };
            for (var index = 0; index < labels.Length; index++)
                profile.Options.Toolbar.Add(new ClientToolbarOption { Label = labels[index], AutomationArea = labels[index].Replace(" & ", "And"), Order = index });
        }
        profile.Options.Presentation.OutputScalePercent = Math.Clamp(profile.Options.Presentation.OutputScalePercent, 75, 200);
        foreach (var item in profile.Options.Toolbar)
        {
            item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
            item.Label = string.IsNullOrWhiteSpace(item.Label) ? "Tool" : item.Label.Trim();
            item.AutomationArea = item.AutomationArea?.Trim() ?? string.Empty;
            item.Order = Math.Clamp(item.Order, -1000, 1000);
        }
        profile.Options.KeyBindings ??= [];
        if (profile.Options.KeyBindings.Count == 0)
        {
            profile.Options.KeyBindings.Add(new ClientCommandKeyBindingOption { Label = "Look", Gesture = "Ctrl+1", Command = "look", Order = 0 });
            profile.Options.KeyBindings.Add(new ClientCommandKeyBindingOption { Label = "Inventory", Gesture = "Ctrl+2", Command = "inventory", Order = 10 });
            profile.Options.KeyBindings.Add(new ClientCommandKeyBindingOption { Label = "Equipment", Gesture = "Ctrl+3", Command = "equipment", Order = 20 });
            profile.Options.KeyBindings.Add(new ClientCommandKeyBindingOption { Label = "Party", Gesture = "Ctrl+4", Command = "party", Order = 30 });
        }
        foreach (var binding in profile.Options.KeyBindings)
        {
            binding.Id = string.IsNullOrWhiteSpace(binding.Id) ? Guid.NewGuid().ToString("N") : binding.Id;
            binding.Label = string.IsNullOrWhiteSpace(binding.Label) ? "Command hotkey" : binding.Label.Trim();
            binding.Gesture = binding.Gesture?.Trim() ?? string.Empty;
            binding.Command = binding.Command?.Trim() ?? string.Empty;
            binding.Order = Math.Clamp(binding.Order, -1000, 1000);
        }
        profile.Options.ContextCommands ??= [];
        if (profile.Options.ContextCommands.Count == 0)
        {
            var commands = new (string Label, string Command)[]
            {
                ("Look", "look"), ("Inventory", "inventory"), ("Equipment", "equipment"),
                ("Stats", "stats"), ("Party", "party")
            };
            for (var index = 0; index < commands.Length; index++)
                profile.Options.ContextCommands.Add(new ClientContextCommandOption { Label = commands[index].Label, Command = commands[index].Command, Order = index * 10 });
        }
        foreach (var command in profile.Options.ContextCommands)
        {
            command.Id = string.IsNullOrWhiteSpace(command.Id) ? Guid.NewGuid().ToString("N") : command.Id;
            command.Label = string.IsNullOrWhiteSpace(command.Label) ? "Command" : command.Label.Trim();
            command.Command = command.Command?.Trim() ?? string.Empty;
            command.Order = Math.Clamp(command.Order, -1000, 1000);
        }
        profile.Options.Events.ScheduledEvents ??= [];
        foreach (var scheduledEvent in profile.Options.Events.ScheduledEvents)
        {
            scheduledEvent.Id = string.IsNullOrWhiteSpace(scheduledEvent.Id) ? Guid.NewGuid().ToString("N") : scheduledEvent.Id;
            scheduledEvent.Name = string.IsNullOrWhiteSpace(scheduledEvent.Name) ? "Event" : scheduledEvent.Name.Trim();
            scheduledEvent.Command ??= string.Empty;
            scheduledEvent.IntervalMinutes = Math.Max(1, scheduledEvent.IntervalMinutes);
            scheduledEvent.IntervalSeconds = Math.Clamp(scheduledEvent.IntervalSeconds, 0, 86400);
            scheduledEvent.Priority = Math.Clamp(scheduledEvent.Priority, -100000, 100000);
            if (scheduledEvent.OnlyOutOfCombat && scheduledEvent.OnlyInCombat) scheduledEvent.OnlyInCombat = false;
        }
        profile.Options.Spells.PriorityActions ??= [];
        profile.Options.Spells.DebuffPriorityActions ??= [];
        profile.Options.Spells.NukePriorityActions ??= [];
        profile.Options.Spells.ReserveManaPercent = Math.Clamp(profile.Options.Spells.ReserveManaPercent, 0, 100);
        profile.Options.Spells.NukeCooldownSeconds = Math.Clamp(profile.Options.Spells.NukeCooldownSeconds, 1, 300);
        profile.Options.Spells.AreaNukeMinimumTargets = Math.Clamp(profile.Options.Spells.AreaNukeMinimumTargets, 2, 99);
        foreach (var action in profile.Options.Spells.DebuffPriorityActions.Concat(profile.Options.Spells.NukePriorityActions))
        {
            action.MinimumHealthPercent = Math.Clamp(action.MinimumHealthPercent, 0, 100);
            action.MinimumManaPercent = Math.Clamp(action.MinimumManaPercent, 0, 100);
            action.MinimumTargetCount = Math.Clamp(action.MinimumTargetCount, 1, 99);
            action.MaximumTargetCount = Math.Clamp(action.MaximumTargetCount, action.MinimumTargetCount, 99);
        }
        profile.Options.Combat.PriorityActions ??= [];
        profile.Options.LootAndCash.CurrencyRules ??= [];
        EnsureRule(profile, AutomationArea.Health, "Auto Cure Silence", "text-contains", "cast remove silence", 0, 3, 15, "silenced");
        EnsureRule(profile, AutomationArea.Health, "Emergency Recall", "health-below", "recall", 15, -2, 20);
        EnsureRule(profile, AutomationArea.Health, "Auto Wake", "text-contains", "wake", 0, 7, 5, "you are asleep");
        EnsureRule(profile, AutomationArea.Spells, "Retry Failed Cast", "text-contains", "cast", 0, 45, 5, "failed to cast");
        EnsureRule(profile, AutomationArea.Spells, "Recast After Dispel", "text-contains", "cast bless", 0, 46, 15, "spell fades");
        EnsureRule(profile, AutomationArea.Combat, "Defensive Action", "health-below", "defend", 40, 3, 8, onlyInCombat: true);
        EnsureRule(profile, AutomationArea.Combat, "Low Mana Escape", "mana-below", "flee", 10, 4, 8, onlyInCombat: true);
        EnsureRule(profile, AutomationArea.PvP, "Chase PvP Opponent", "text-contains", "chase", 0, 3, 3, "flees from you");
        EnsureRule(profile, AutomationArea.Party, "Wait For Leader", "text-contains", "wait", 0, 16, 8, "leader is not here");
        EnsureRule(profile, AutomationArea.Party, "Resume With Leader", "text-contains", "follow", 0, 17, 8, "leader arrives");
        EnsureRule(profile, AutomationArea.LootAndCash, "Auto Sell At Shop", "text-contains", "sell all", 0, 84, 20, "shopkeeper");
        EnsureRule(profile, AutomationArea.LootAndCash, "Drop When Overweight", "text-contains", "drop heavy", 0, 5, 10, "too heavy");
        if (profile.Options.Spells.PriorityActions.Count == 0)
        {
            profile.Options.Spells.PriorityActions.Add(new ClientPriorityAction { Name = "Emergency Heal", Command = profile.Options.Spells.MajorHealCommand, Priority = 1, MinimumManaPercent = 5 });
            profile.Options.Spells.PriorityActions.Add(new ClientPriorityAction { Name = "Minor Heal", Command = profile.Options.Spells.MinorHealCommand, Priority = 10, MinimumManaPercent = 5 });
            profile.Options.Spells.PriorityActions.Add(new ClientPriorityAction { Name = "Bless", Command = profile.Options.Spells.BlessCommand, Priority = 50, OnlyOutOfCombat = true });
        }
        if (profile.Options.Combat.PriorityActions.Count == 0)
        {
            profile.Options.Combat.PriorityActions.Add(new ClientPriorityAction { Name = "Primary Attack", Command = profile.Options.Combat.PrimaryAttackCommand, Priority = 20, OnlyInCombat = true });
            profile.Options.Combat.PriorityActions.Add(new ClientPriorityAction { Name = "Secondary Attack", Command = profile.Options.Combat.SecondaryAttackCommand, Priority = 30, OnlyInCombat = true });
        }
        if (profile.Options.LootAndCash.CurrencyRules.Count == 0)
        {
            profile.Options.LootAndCash.CurrencyRules.Add(new ClientCurrencyRule { Name = "Copper", RecognitionWords = "copper,coppers", SortOrder = 10 });
            profile.Options.LootAndCash.CurrencyRules.Add(new ClientCurrencyRule { Name = "Silver", RecognitionWords = "silver,silvers", SortOrder = 20 });
            profile.Options.LootAndCash.CurrencyRules.Add(new ClientCurrencyRule { Name = "Gold", RecognitionWords = "gold,golds", SortOrder = 30 });
            profile.Options.LootAndCash.CurrencyRules.Add(new ClientCurrencyRule { Name = "Platinum", RecognitionWords = "platinum,platinums", SortOrder = 40 });
        }
    }

    private static void EnsureRule(ClientAutomationProfile profile, AutomationArea area, string name, string trigger, string command, int threshold, int priority, int cooldownSeconds, string? matchText = null, bool onlyOutOfCombat = false, bool onlyInCombat = false)
    {
        if (profile.Rules.Any(rule => string.Equals(rule.Name, name, StringComparison.OrdinalIgnoreCase))) return;
        Add(profile, area, name, "Migrated MegaMUD-compatible automation option.", trigger, command, threshold, priority, cooldownSeconds, matchText, onlyOutOfCombat, onlyInCombat);
    }

    private static void Add(ClientAutomationProfile p, AutomationArea area, string name, string description, string trigger, string command, int threshold, int priority, int cooldownSeconds, string? matchText = null, bool onlyOutOfCombat = false, bool onlyInCombat = false)
        => p.Rules.Add(new ClientAutomationRule { Area = area, Name = name, Description = matchText ?? description, Trigger = trigger, Command = command, Threshold = threshold, Priority = priority, CooldownSeconds = cooldownSeconds, OnlyOutOfCombat = onlyOutOfCombat, OnlyInCombat = onlyInCombat });
}

internal sealed class ClientAutomationCommandDeduplicator
{
    private string _lastCommand = string.Empty;
    private DateTimeOffset _lastSentAt;

    public bool ShouldSend(string command, ClientCommandOrigin origin, DateTimeOffset now)
    {
        if (origin is not (ClientCommandOrigin.ClientAutomation or ClientCommandOrigin.RouteAutomation)) return true;
        var normalized = string.Join(' ', (command ?? string.Empty).Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToLowerInvariant();
        if (normalized.Length == 0) return true;
        if (normalized.Equals(_lastCommand, StringComparison.Ordinal) && now - _lastSentAt < TimeSpan.FromMilliseconds(500)) return false;
        _lastCommand = normalized;
        _lastSentAt = now;
        return true;
    }
}

internal static class ClientAutomationCapabilityPolicy
{
    public static bool Allows(ClientAutomationCapabilities capabilities, AutomationArea area) => area switch
    {
        AutomationArea.Health => capabilities.HealthRecoveryEnabled,
        AutomationArea.Spells => capabilities.SpellMaintenanceEnabled,
        AutomationArea.Combat => capabilities.CombatEnabled,
        AutomationArea.PvP => capabilities.PvpAutomationEnabled,
        AutomationArea.Party => capabilities.PartyAutomationEnabled,
        AutomationArea.Roaming or AutomationArea.Utility => capabilities.RoamingAutomationEnabled,
        AutomationArea.LootAndCash => capabilities.LootAutomationEnabled,
        AutomationArea.Events => capabilities.TimedEventsEnabled,
        _ => false
    };
}
