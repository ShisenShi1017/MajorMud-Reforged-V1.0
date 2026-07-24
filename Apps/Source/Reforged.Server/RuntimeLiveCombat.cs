using System.Collections.Concurrent;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeLiveCombatEncounter(
    string SessionId,
    string WorldId,
    string RoomId,
    string NpcInstanceId,
    string SpawnId,
    string NpcId,
    string NpcName,
    int NpcLevel,
    int NpcMaximumHitPoints,
    IReadOnlyList<RuntimeNpcAttackDefinition> NpcAttacks,
    int NpcMaximumEnergy,
    int NpcCurrentEnergy,
    int NpcCurrentHitPoints,
    int PlayerCurrentEnergy,
    int RoundNumber,
    ClassicAttackType PlayerAttackType,
    DateTimeOffset StartedAtUtc);

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string, RuntimeLiveCombatEncounter> _liveCombats = new(StringComparer.OrdinalIgnoreCase);

    public bool CanMove(RuntimeSession session, out string message)
    {
        if (session.Actor is RuntimeCharacterActor actor)
        {
            var character = _characters.LoadCharacter(actor.Id);
            if (character is not null && RuntimeClassicStatusProjection.IsMovementBlocked(character))
            {
                message = "You cannot move while paralyzed.";
                return false;
            }
        }
        if (_liveCombats.TryRemove(session.Id, out var encounter))
        {
            _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} runs from {encounter.NpcName}.");
            message = $"You run from {encounter.NpcName}.";
            return true;
        }
        message = string.Empty;
        return true;
    }

    public string ExecuteLiveCombatCommand(RuntimeSession session, RuntimeCommand command)
    {
        return command.Name switch
        {
            "attack" or "kill" => BeginLiveCombat(session, command.ArgumentText, ClassicAttackType.NormalWeapon),
            "bash" => BeginLiveCombat(session, command.ArgumentText, ClassicAttackType.Bash),
            "smash" => BeginLiveCombat(session, command.ArgumentText, ClassicAttackType.Smash),
            "backstab" => BeginLiveCombat(session, command.ArgumentText, ClassicAttackType.Backstab),
            "flee" => FleeLiveCombat(session),
            "combat" => RenderLiveCombatStatus(session),
            "round" => ExecuteLiveCombatRound(session),
            "assist" => AssistLiveCombat(session, command.ArgumentText),
            _ => _policyGateway.Execute(session, command)
        };
    }

    public string RenderLiveCombatStatus(RuntimeSession session)
    {
        if (!_liveCombats.TryGetValue(session.Id, out var encounter)) return "You are not in combat.";
        return string.Join(Environment.NewLine, new[]
        {
            $"Combat target: {encounter.NpcName}",
            $"NPC ID: {encounter.NpcId}",
            $"Spawn ID: {encounter.SpawnId}",
            $"Generated level: {encounter.NpcLevel}",
            $"NPC HP: {encounter.NpcCurrentHitPoints}/{encounter.NpcMaximumHitPoints}",
            $"NPC Energy: {encounter.NpcCurrentEnergy}/{encounter.NpcMaximumEnergy}",
            $"Round: {encounter.RoundNumber}",
            $"Player carried attack energy: {encounter.PlayerCurrentEnergy}",
            $"Player attack: {encounter.PlayerAttackType}",
            $"Room: {encounter.RoomId}",
            $"Generated attack rows: {encounter.NpcAttacks.Count}",
            RenderPlayerCombatReadiness(session, encounter)
        });
    }

    private string RenderPlayerCombatReadiness(RuntimeSession session, RuntimeLiveCombatEncounter encounter)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "Player combat profile is unavailable for a non-character session.";
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return "Player combat profile is unavailable because the character save could not be loaded.";
        var profile = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot), _characters.ProjectRoot).Resolve(character);
        if (!profile.Ready) return "Player combat profile blocked: " + string.Join(", ", profile.Blockers);
        var npcDefense = RuntimeNpcDefensiveProfileResolver.Resolve(ResolveEncounterNpc(session, encounter));
        return $"Player combat profile ready: AV {profile.AttackValue}, DV {profile.DefenseValue}, AC {profile.ArmorClass}, DR {profile.DamageResistance}, Dodge {profile.DodgeBonus}, Damage {profile.MinimumDamage}-{profile.MaximumDamage} (STR {profile.StrengthMaximumDamageBonus:+#;-#;0}), Speed {profile.WeaponSpeed}, Energy {profile.AttackEnergyCost}, Accuracy class={profile.ClassCombatAccuracy} ability={profile.AbilityAccuracyModifier} weapon={profile.WeaponAccuracyModifier}. NPC defense ready: AC {npcDefense.ArmorClass}, DR {npcDefense.DamageResistance}, Backstab DV {npcDefense.BackstabDefense}; no separate NPC dodge input is defined by the authoritative schema.";
    }


    private static RuntimeNpcDefinition ResolveEncounterNpc(RuntimeSession session, RuntimeLiveCombatEncounter encounter)
    {
        var npc = session.World.Npcs.Values.FirstOrDefault(x => x.Id.Equals(encounter.NpcId, StringComparison.OrdinalIgnoreCase));
        if (npc is not null) return npc;
        throw new InvalidOperationException($"LIVE_COMBAT_NPC_PROFILE_NOT_FOUND:{encounter.NpcId}");
    }

    private string BeginLiveCombat(RuntimeSession session, string query, ClassicAttackType attackType)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Attack what?";
        if (_liveCombats.TryGetValue(session.Id, out var current))
            return $"You are already fighting {current.NpcName}.";

        var target = ResolveCombatTarget(session, query.Trim());
        var protection = RuntimeNpcProtectionAdmission.Admit(
            target.Instance.InstanceId,
            session.CurrentRoom.Id,
            LivingNpcsInRoom(session));
        if (!protection.Allowed)
            return $"{protection.BlockingNpcName ?? "Another creature"} moves between you and {target.Definition.Name}, blocking your attack.";
        string? preparationMessage = null;
        if (attackType == ClassicAttackType.Backstab)
        {
            if (session.Actor is not RuntimeCharacterActor backstabActor) return "A persistent character is required to Backstab.";
            var backstabCharacter = _characters.LoadCharacter(backstabActor.Id) ?? throw new InvalidOperationException("Character save was not found.");
            var backstabProfile = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot), _characters.ProjectRoot).Resolve(backstabCharacter);
            if (!backstabProfile.Ready) return "Backstab blocked: " + string.Join(", ", backstabProfile.Blockers);
            var prepared = ClassicAttackPreparationPolicy.PrepareBackstab(
                IsCharacterHidden(backstabCharacter),
                alreadyEngaged: false,
                backstabProfile.Barehanded,
                backstabProfile.WeaponSupportsBackstab,
                backstabProfile.WeaponIsTwoHanded);
            attackType = prepared.AttackType;
            preparationMessage = prepared.Message;
        }
        if (attackType == ClassicAttackType.Smash)
        {
            if (session.Actor is not RuntimeCharacterActor smashActor) return "A persistent character is required to Smash.";
            var smashCharacter = _characters.LoadCharacter(smashActor.Id) ?? throw new InvalidOperationException("Character save was not found.");
            var smashValues = RuntimeCharacterAbilityValues.Restore(smashCharacter.Conditions);
            if (RuntimeCharacterAbilityValues.Read(smashValues, RuntimeCharacterQuestAbilityCatalog.SmashAbilityId) <= 0)
                return "You have not learned Smash.";
        }

        if (session.Actor is RuntimeCharacterActor combatActor)
        {
            var combatCharacter = _characters.LoadCharacter(combatActor.Id) ?? throw new InvalidOperationException("Character save was not found.");
            var revealed = BreakPhysicalAttackConcealment(combatCharacter);
            if (!ReferenceEquals(revealed, combatCharacter))
                _characters.SaveCharacter(revealed);
        }

        var encounter = new RuntimeLiveCombatEncounter(
            session.Id,
            session.World.Definition.Id,
            session.CurrentRoom.Id,
            target.Instance.InstanceId,
            target.Instance.SpawnId,
            target.Definition.Id,
            target.Definition.Name,
            target.Definition.Level,
            target.Definition.HitPoints,
            target.Definition.Attacks,
            Math.Max(0, target.Definition.Energy),
            ResolveNpcEncounterStartingEnergy(target.Definition.Energy),
            target.Instance.CurrentHitPoints,
            0,
            0,
            attackType,
            DateTimeOffset.UtcNow);
        if (!_liveCombats.TryAdd(session.Id, encounter)) throw new InvalidOperationException("LIVE_COMBAT_DUPLICATE_SESSION");
        RegisterCombatEngagement(session, encounter, initialThreat: 1);
        AuthorizeOwnedCompanionAssistance(session, encounter);
        AuthorizeExplicitNpcAssistance(session, encounter);
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} attacks {target.Definition.Name}.");
        var engaged = $"*Combat Engaged*\nYou attack {target.Definition.Name}.";
        return string.IsNullOrWhiteSpace(preparationMessage) ? engaged : preparationMessage + Environment.NewLine + engaged;
    }



    internal static int ResolveNpcEncounterStartingEnergy(int decodedMaximumEnergy)
    {
        if (decodedMaximumEnergy < 0)
            throw new InvalidDataException("NPC_COMBAT_NEGATIVE_MAXIMUM_ENERGY");
        return decodedMaximumEnergy;
    }

    private static readonly string[] CombatConcealmentConditionKeys = ["combat.hidden", "state.hidden", "hidden", "combat.sneaking", "state.sneaking", "sneaking"];

    private static bool IsCharacterHidden(RuntimeCharacterRecord character)
    {
        foreach (var key in CombatConcealmentConditionKeys.Take(3))
        {
            if (!character.Conditions.TryGetValue(key, out var raw)) continue;
            if (bool.TryParse(raw, out var flag)) return flag;
            if (int.TryParse(raw, out var value)) return value > 0;
        }
        return false;
    }

    internal static RuntimeCharacterRecord BreakPhysicalAttackConcealment(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var key in CombatConcealmentConditionKeys)
        {
            if (!conditions.TryGetValue(key, out var raw)) continue;
            var active = (bool.TryParse(raw, out var flag) && flag) || (int.TryParse(raw, out var value) && value > 0);
            if (!active) continue;
            conditions[key] = "0";
            changed = true;
        }
        return changed ? character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow } : character;
    }

    private string FleeLiveCombat(RuntimeSession session)
    {
        if (!_liveCombats.TryRemove(session.Id, out var encounter)) return "You are not in combat.";
        ReleaseCombatEngagement(session, encounter, "player-flee");
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} flees from {encounter.NpcName}.");
        return $"You flee from {encounter.NpcName}.";
    }

    private CombatTarget ResolveCombatTarget(RuntimeSession session, string query)
    {
        var matches = new List<CombatTarget>();
        foreach (var instance in LivingNpcsInRoom(session))
        {
            var npc = ResolveNpcDefinition(session.World, instance.NpcId);
            if (npc is not null && Matches(query, instance, npc))
                matches.Add(new CombatTarget(instance, npc));
        }
        matches = matches.GroupBy(x => x.Instance.InstanceId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        if (matches.Count == 0) throw new InvalidOperationException($"No generated NPC here uniquely matches '{query}'.");
        if (matches.Count > 1) throw new InvalidOperationException($"More than one live NPC here matches '{query}'; use an NPC instance ID.");
        return matches[0];
    }

    private static RuntimeNpcDefinition? ResolveNpcDefinition(RuntimeWorld world, string npcId) =>
        world.Npcs.Values.FirstOrDefault(npc => npc.Id.Equals(npcId, StringComparison.OrdinalIgnoreCase));

    private static bool Matches(string query, RuntimeLivingNpcState instance, RuntimeNpcDefinition npc) =>
        instance.InstanceId.Equals(query, StringComparison.OrdinalIgnoreCase)
        || instance.SpawnId.Equals(query, StringComparison.OrdinalIgnoreCase)
        || npc.Id.Equals(query, StringComparison.OrdinalIgnoreCase)
        || npc.Name.Equals(query, StringComparison.OrdinalIgnoreCase)
        || npc.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

    private sealed record CombatTarget(RuntimeLivingNpcState Instance, RuntimeNpcDefinition Definition);
}

public sealed class RuntimeLiveCombatSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveCombatSmokeReport Run(string projectRoot, string worldId)
    {
        var report = new RuntimeLiveCombatSmokeReport();
        try
        {
            var resolver = ServerDataRootResolver.ResolveDefault(projectRoot);
            var npcPath = Path.Combine(resolver.DataRoot, "npcs.json");
            using var document = JsonDocument.Parse(File.ReadAllText(npcPath));
            var records = document.RootElement.GetProperty("records");
            Require(records.GetArrayLength() > 0, "generated-npc-catalog"); report.Checks.Add("generated-npc-catalog");
            Require(records.EnumerateArray().Any(x => x.TryGetProperty("combat", out var c) && c.TryGetProperty("hp", out _)), "generated-combat-profile"); report.Checks.Add("generated-combat-profile");
            Require(RuntimeCommand.Parse("attack rat").Name == "attack", "attack-route"); report.Checks.Add("attack-route");
            Require(RuntimeCommand.Parse("a kobold thief").Name == "attack" && RuntimeCommand.Parse("a kobold thief").ArgumentText == "kobold thief", "attack-a-full-target-route"); report.Checks.Add("attack-a-full-target-route");
            Require(RuntimeCommand.Parse("bash kobold thief").Name == "bash" && RuntimeCommand.Parse("bash kobold thief").ArgumentText == "kobold thief", "bash-combat-route"); report.Checks.Add("bash-combat-route");
            Require(RuntimeCommand.Parse("smash kobold thief").Name == "smash", "smash-combat-route"); report.Checks.Add("smash-combat-route");
            Require(RuntimeCommand.Parse("backstab kobold thief").Name == "backstab", "backstab-combat-route"); report.Checks.Add("backstab-combat-route");
            var qualifiedBackstab = ClassicAttackPreparationPolicy.PrepareBackstab(true, false, false, true, false);
            Require(qualifiedBackstab.AttackType == ClassicAttackType.Backstab && qualifiedBackstab.BackstabQualified, "backstab-qualified-one-handed"); report.Checks.Add("backstab-qualified-one-handed");
            var twoHandedDowngrade = ClassicAttackPreparationPolicy.PrepareBackstab(true, false, false, true, true);
            Require(twoHandedDowngrade.AttackType == ClassicAttackType.NormalWeapon && !twoHandedDowngrade.BackstabQualified && twoHandedDowngrade.EngageTarget, "backstab-two-handed-downgrade"); report.Checks.Add("backstab-two-handed-downgrade");
            var visibleDowngrade = ClassicAttackPreparationPolicy.PrepareBackstab(false, false, true, false, false);
            Require(visibleDowngrade.AttackType == ClassicAttackType.NormalWeapon && visibleDowngrade.EngageTarget, "backstab-hidden-state-required"); report.Checks.Add("backstab-hidden-state-required");
            var concealed = new RuntimeCharacterRecord(
                "character.26", "account.26", "Concealed", "Classic", "race.1", "Race", "class.1", "Class", 1, 0,
                10, 10, 0, 0, "room.1", 1, new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(),
                new Dictionary<string, string?>(), new Dictionary<string, string> { ["combat.hidden"] = "1", ["combat.sneaking"] = "true" },
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var revealed = RuntimeLiveGameplayFacade.BreakPhysicalAttackConcealment(concealed);
            Require(revealed.Conditions["combat.hidden"] == "0" && revealed.Conditions["combat.sneaking"] == "0", "physical-attack-breaks-concealment"); report.Checks.Add("physical-attack-breaks-concealment");
            Require(RuntimeCommand.Parse("combat").Name == "combat", "combat-status-route"); report.Checks.Add("combat-status-route");
            Require(RuntimeCommand.Parse("flee").Name == "flee", "flee-route"); report.Checks.Add("flee-route");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.ExecuteLiveCombatCommand)) is not null, "live-combat-admission"); report.Checks.Add("live-combat-admission");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.CanMove)) is not null, "combat-movement-guard"); report.Checks.Add("combat-movement-guard");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
