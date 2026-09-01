using System.Collections.Concurrent;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePvpSafeRoomEvidence(string RoomId, string EvidenceClassification, IReadOnlyList<string> SourceRecordIds);

public sealed record RuntimePvpZoneCatalog(
    int SchemaVersion,
    IReadOnlyList<RuntimePvpZoneRule> Zones,
    IReadOnlyList<RuntimePvpSafeRoomEvidence>? ProvenSafeRooms = null)
{
    public static RuntimePvpZoneCatalog Load(string projectRoot, string worldId)
    {
        var dataRoot = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
        var path = Path.Combine(dataRoot, "pvp_zones.json");
        if (!File.Exists(path)) return new(1, []);
        var catalog = JsonSerializer.Deserialize<RuntimePvpZoneCatalog>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("PVP_ZONE_CATALOG_INVALID");
        if (catalog.SchemaVersion != 1 || catalog.Zones.Any(zone => string.IsNullOrWhiteSpace(zone.RoomId)) ||
            (catalog.ProvenSafeRooms ?? []).Any(room => string.IsNullOrWhiteSpace(room.RoomId)))
            throw new InvalidDataException("PVP_ZONE_CATALOG_INVALID");
        return catalog;
    }

    public bool IsProvenSafe(string roomId) => (ProvenSafeRooms ?? []).Any(room =>
        room.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase) &&
        room.EvidenceClassification.Equals("proven", StringComparison.OrdinalIgnoreCase) &&
        room.SourceRecordIds.Count > 0 && room.SourceRecordIds.All(source => !string.IsNullOrWhiteSpace(source)));
}

public sealed record RuntimePvpWorldConfiguration(
    int SchemaVersion, string WorldId, bool Enabled, string UpdatedByCharacterId,
    DateTimeOffset UpdatedAtUtc, long Revision, int MaximumLevelDifference = 0);

public sealed class RuntimePvpWorldConfigurationStore
{
    private readonly string _path;
    private readonly string _worldId;
    private readonly object _gate = new();

    public RuntimePvpWorldConfigurationStore(string projectRoot, string worldId)
    {
        _worldId = worldId;
        _path = Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "pvp", RuntimeIdentity.Normalize(worldId, "world") + ".json");
    }

    public RuntimePvpWorldConfiguration Capture()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
                return new(1, _worldId, true, string.Empty, DateTimeOffset.MinValue, 0,
                    RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference);
            var value = JsonSerializer.Deserialize<RuntimePvpWorldConfiguration>(File.ReadAllText(_path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (value is null || value.SchemaVersion != 1 || !value.WorldId.Equals(_worldId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PVP_WORLD_CONFIGURATION_INVALID");
            if (value.MaximumLevelDifference < RuntimeClassicConfigurationAuthority.MinimumPvpLevelDifference ||
                value.MaximumLevelDifference > RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifferenceAllowed)
                throw new InvalidDataException("PVP_WORLD_CONFIGURATION_RANGE_INVALID");

            // Migrate the pre-0358 Reforged-only Enabled flag into the native LVLDIFF sentinel.
            var maximum = !value.Enabled && value.MaximumLevelDifference >= 0
                ? RuntimeClassicConfigurationAuthority.PvpDisabledLevelDifference
                : value.MaximumLevelDifference;
            return value with { Enabled = RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(maximum), MaximumLevelDifference = maximum };
        }
    }

    public RuntimePvpWorldConfiguration SetEnabled(bool enabled, string updatedByCharacterId)
    {
        lock (_gate)
        {
            var prior = Capture();
            var maximum = enabled
                ? (prior.MaximumLevelDifference < 0 ? RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference : prior.MaximumLevelDifference)
                : RuntimeClassicConfigurationAuthority.PvpDisabledLevelDifference;
            var updated = new RuntimePvpWorldConfiguration(1, _worldId, enabled, updatedByCharacterId,
                DateTimeOffset.UtcNow, checked(prior.Revision + 1), maximum);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, true);
            return updated;
        }
    }

    public RuntimePvpWorldConfiguration SetMaximumLevelDifference(int maximumLevelDifference, string updatedByCharacterId)
    {
        if (maximumLevelDifference < RuntimeClassicConfigurationAuthority.MinimumPvpLevelDifference ||
            maximumLevelDifference > RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifferenceAllowed)
            throw new ArgumentOutOfRangeException(nameof(maximumLevelDifference));
        lock (_gate)
        {
            var prior = Capture();
            var updated = new RuntimePvpWorldConfiguration(1, _worldId,
                RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(maximumLevelDifference), updatedByCharacterId,
                DateTimeOffset.UtcNow, checked(prior.Revision + 1), maximumLevelDifference);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, true);
            return updated;
        }
    }
}

public sealed record RuntimeLivePvpEncounter(
    string EncounterId, string WorldId, string RoomId,
    string AttackerSessionId, string DefenderSessionId,
    ClassicAttackType AttackerAttackType, ClassicAttackType DefenderAttackType,
    bool AttackerAutocombatActive, bool DefenderAutocombatActive,
    int RoundNumber, DateTimeOffset StartedAtUtc);

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string, RuntimeLivePvpEncounter> _livePvpEncounters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _livePvpEncounterBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuntimePvpPolicyService> _livePvpPolicies = new(StringComparer.OrdinalIgnoreCase);

    private bool TryResolveExactPvpTarget(RuntimeSession source, string query, out RuntimeSession? target)
    {
        target = _sessions.InRoom(source.World.Definition.Id, source.CurrentRoom.Id)
            .Where(candidate => !candidate.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase))
            .SingleOrDefault(candidate => candidate.Actor.Id.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                                          candidate.Actor.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase));
        return target?.Actor is RuntimeCharacterActor;
    }

    private string BeginLivePvpCombat(RuntimeSession attackerSession, RuntimeSession defenderSession, ClassicAttackType attackType)
    {
        if (attackerSession.Actor is not RuntimeCharacterActor attackerActor || defenderSession.Actor is not RuntimeCharacterActor defenderActor)
            return "PvP requires two persistent characters.";
        if (_livePvpEncounterBySession.TryGetValue(attackerSession.Id, out var existingEncounterId) &&
            _livePvpEncounters.TryGetValue(existingEncounterId, out var existingEncounter))
        {
            var existingOpponentId = existingEncounter.AttackerSessionId.Equals(attackerSession.Id, StringComparison.OrdinalIgnoreCase)
                ? existingEncounter.DefenderSessionId : existingEncounter.AttackerSessionId;
            if (!existingOpponentId.Equals(defenderSession.Id, StringComparison.OrdinalIgnoreCase) ||
                IsLivePvpAutocombatActive(existingEncounter, attackerSession.Id))
                return "You are already in PvP combat.";

            var resumed = SetLivePvpAutocombat(existingEncounter, attackerSession.Id, true, attackType);
            _livePvpEncounters[resumed.EncounterId] = resumed;
            CancelFocusedRegeneration(attackerActor.Id);
            defenderSession.EnqueueMessage($"{attackerActor.DisplayName} attacks you.");
            _sessions.BroadcastRoom(attackerSession, $"{attackerActor.DisplayName} attacks {defenderActor.DisplayName}.");
            return $"You attack {defenderActor.DisplayName}.\n*Combat Engaged*";
        }
        if (_livePvpEncounterBySession.ContainsKey(defenderSession.Id)) return $"{defenderActor.DisplayName} is already in PvP combat.";
        var pvpConfiguration = new RuntimePvpWorldConfigurationStore(_characters.ProjectRoot, attackerSession.World.Definition.Id).Capture();
        if (!RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(pvpConfiguration.MaximumLevelDifference))
            return "PvP is disabled by the SYSOP.";
        var attacker = _characters.LoadCharacter(attackerActor.Id) ?? throw new InvalidOperationException("PvP attacker save was not found.");
        var defender = _characters.LoadCharacter(defenderActor.Id) ?? throw new InvalidOperationException("PvP defender save was not found.");
        var defenderAlignment = defender.SemanticState?.Alignment;
        var defenderReputation = defenderAlignment?.Reputation ?? "Neutral";
        var defenderIsFiend = defenderReputation.Equals("Fiend", StringComparison.OrdinalIgnoreCase);
        var practiceArena = IsClassicPracticeArena(attackerSession);
        if (!practiceArena && !RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(
                attacker.Level, defender.Level, pvpConfiguration.MaximumLevelDifference, defenderIsFiend))
            return $"PvP attack denied: both players must be level {RuntimeClassicDeathLawPvpAuthority.MinimumPvpLevel}+ and within {pvpConfiguration.MaximumLevelDifference} level(s).";

        var characterCatalog = _characters.GetCatalog(attacker.WorldId);
        var attackerAlignment = attacker.SemanticState?.Alignment ?? RuntimeCharacterAlignmentResolver.CreateInitial(characterCatalog.AlignmentRuleContract);
        RuntimeCharacterAlignmentState? projectedAlignment = null;
        if (!practiceArena)
        {
            var defenderIsLawful = RuntimeClassicWarningAuthority.IsLawful(defender);
            var evilPointAward = RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(defenderAlignment?.EvilPoints ?? 0, defenderIsLawful);
            projectedAlignment = RuntimeCharacterAlignmentResolver.ApplyEvilPointDelta(attackerAlignment, evilPointAward, characterCatalog.AlignmentRuleContract);
            if (RuntimeClassicWarningAuthority.TryResolvePositiveEvilPointActionBlock(
                    attacker, attackerAlignment.EvilPoints, projectedAlignment.EvilPoints, out var evilActionBlock))
                return evilActionBlock;
        }

        var policy = _livePvpPolicies.GetOrAdd(attacker.WorldId, worldId =>
        {
            var catalog = RuntimePvpZoneCatalog.Load(_characters.ProjectRoot, worldId);
            var zones = attackerSession.World.AllRooms().Select(room => new RuntimePvpZoneRule(room.Id,
                catalog.IsProvenSafe(room.Id) ? RuntimePvpZonePolicy.Forbidden : RuntimePvpZonePolicy.Open,
                MaximumLevelDifference: 0,
                DenySameParty: false,
                DenySameGuild: false));
            return new RuntimePvpPolicyService(zones);
        });
        var attackerParty = _partyGameplay.FindByMember(attacker.CharacterId)?.PartyId;
        var defenderParty = _partyGameplay.FindByMember(defender.CharacterId)?.PartyId;
        policy.UpsertActor(ProjectPvpActor(attacker, attackerSession.CurrentRoom.Id, attackerParty));
        policy.UpsertActor(ProjectPvpActor(defender, defenderSession.CurrentRoom.Id, defenderParty));
        var admission = policy.RegisterAttack(attacker.CharacterId, defender.CharacterId, DateTimeOffset.UtcNow);
        if (!admission.Allowed) return $"PvP attack denied: {admission.Reason}.";

        if (projectedAlignment is not null)
        {
            var semantic = (attacker.SemanticState ?? RuntimeCharacterSemanticState.Empty) with { Alignment = projectedAlignment };
            attacker = attacker with { SemanticState = semantic, UpdatedAtUtc = DateTimeOffset.UtcNow };
            _characters.SaveCharacter(attacker, "runtime.pvp.attack-alignment");
        }

        var encounter = new RuntimeLivePvpEncounter($"pvp.{Guid.NewGuid():N}", attacker.WorldId, attackerSession.CurrentRoom.Id,
            attackerSession.Id, defenderSession.Id, attackType, ClassicAttackType.NormalWeapon,
            true, true, 0, DateTimeOffset.UtcNow);
        if (!_livePvpEncounters.TryAdd(encounter.EncounterId, encounter) ||
            !_livePvpEncounterBySession.TryAdd(attackerSession.Id, encounter.EncounterId) ||
            !_livePvpEncounterBySession.TryAdd(defenderSession.Id, encounter.EncounterId))
        {
            EndLivePvpEncounter(encounter);
            throw new InvalidOperationException("PVP_ENCOUNTER_ATOMIC_CREATE_FAILED");
        }
        CancelFocusedRegeneration(attackerActor.Id);
        CancelFocusedRegeneration(defenderActor.Id);
        defenderSession.EnqueueMessage($"{attackerActor.DisplayName} attacks you.\n*Combat Engaged*");
        _sessions.BroadcastRoom(attackerSession, $"{attackerActor.DisplayName} attacks {defenderActor.DisplayName}.");
        return $"You attack {defenderActor.DisplayName}.\n*Combat Engaged*";
    }

    private static RuntimePvpActor ProjectPvpActor(RuntimeCharacterRecord character, string roomId, string? partyId)
    {
        var reputation = character.SemanticState?.Alignment?.Reputation ?? "Neutral";
        var criminal =
            reputation.Equals("Outlaw", StringComparison.OrdinalIgnoreCase) ||
            reputation.Equals("Criminal", StringComparison.OrdinalIgnoreCase) ||
            reputation.Equals("Villain", StringComparison.OrdinalIgnoreCase) ||
            reputation.Equals("Fiend", StringComparison.OrdinalIgnoreCase);
        return new(character.CharacterId, Math.Max(1, character.Level), roomId, partyId, null, true, criminal);
    }

    private string FleeLivePvpCombat(RuntimeSession session)
    {
        if (!_livePvpEncounterBySession.TryGetValue(session.Id, out var encounterId) || !_livePvpEncounters.TryGetValue(encounterId, out var encounter))
            return "You are not in PvP combat.";
        var opponentId = encounter.AttackerSessionId.Equals(session.Id, StringComparison.OrdinalIgnoreCase)
            ? encounter.DefenderSessionId : encounter.AttackerSessionId;
        var opponent = _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Id.Equals(opponentId, StringComparison.OrdinalIgnoreCase));
        EndLivePvpEncounter(encounter);
        if (opponent is not null) opponent.EnqueueMessage($"{session.Actor.DisplayName} flees from you.\n*Combat Off*");
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} flees from {opponent?.Actor.DisplayName ?? "PvP combat"}.");
        return $"You flee from {opponent?.Actor.DisplayName ?? "PvP combat"}.\n*Combat Off*";
    }

    internal static bool IsLivePvpAutocombatActive(RuntimeLivePvpEncounter encounter, string sessionId)
    {
        if (encounter.AttackerSessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
            return encounter.AttackerAutocombatActive;
        if (encounter.DefenderSessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
            return encounter.DefenderAutocombatActive;
        return false;
    }

    internal static RuntimeLivePvpEncounter SetLivePvpAutocombat(
        RuntimeLivePvpEncounter encounter, string sessionId, bool active, ClassicAttackType? attackType = null)
    {
        if (encounter.AttackerSessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
            return encounter with
            {
                AttackerAutocombatActive = active,
                AttackerAttackType = attackType ?? encounter.AttackerAttackType
            };
        if (encounter.DefenderSessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
            return encounter with
            {
                DefenderAutocombatActive = active,
                DefenderAttackType = attackType ?? encounter.DefenderAttackType
            };
        return encounter;
    }

    private void StopLivePvpAutocombatForSession(RuntimeLivePvpEncounter encounter, string sessionId)
    {
        var updated = SetLivePvpAutocombat(encounter, sessionId, false);
        if (!updated.AttackerAutocombatActive && !updated.DefenderAutocombatActive)
        {
            EndLivePvpEncounter(updated);
            return;
        }
        _livePvpEncounters[updated.EncounterId] = updated;
    }

    private void EndLivePvpEncounter(RuntimeLivePvpEncounter encounter)
    {
        _livePvpEncounters.TryRemove(encounter.EncounterId, out _);
        _livePvpEncounterBySession.TryRemove(encounter.AttackerSessionId, out _);
        _livePvpEncounterBySession.TryRemove(encounter.DefenderSessionId, out _);
    }

    private void PruneLivePvpConnections()
    {
        var active = _sessions.ActiveSessions.Select(session => session.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var encounter in _livePvpEncounters.Values.Where(encounter => !active.Contains(encounter.AttackerSessionId) || !active.Contains(encounter.DefenderSessionId)).ToArray())
        {
            var survivorId = active.Contains(encounter.AttackerSessionId) ? encounter.AttackerSessionId : encounter.DefenderSessionId;
            EndLivePvpEncounter(encounter);
            _sessions.ActiveSessions.FirstOrDefault(session => session.Id.Equals(survivorId, StringComparison.OrdinalIgnoreCase))
                ?.EnqueueMessage("Your PvP opponent disconnected.\n*Combat Off*");
        }
    }

    private string RenderLivePvpStatus(RuntimeSession session)
    {
        if (!_livePvpEncounterBySession.TryGetValue(session.Id, out var id) || !_livePvpEncounters.TryGetValue(id, out var encounter))
            return "You are not in PvP combat.";
        var opponentId = encounter.AttackerSessionId.Equals(session.Id, StringComparison.OrdinalIgnoreCase) ? encounter.DefenderSessionId : encounter.AttackerSessionId;
        var opponent = _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Id.Equals(opponentId, StringComparison.OrdinalIgnoreCase));
        return $"PvP target: {opponent?.Actor.DisplayName ?? opponentId}\nRound: {encounter.RoundNumber}\nRoom: {encounter.RoomId}";
    }

    private string ExecuteLivePvpRound(RuntimeLivePvpEncounter encounter)
    {
        if (!new RuntimePvpWorldConfigurationStore(_characters.ProjectRoot, encounter.WorldId).Capture().Enabled)
        {
            EndLivePvpEncounter(encounter);
            return "PvP combat ended because the SYSOP disabled PvP.\n*Combat Off*";
        }
        if (!encounter.AttackerAutocombatActive && !encounter.DefenderAutocombatActive)
        {
            EndLivePvpEncounter(encounter);
            return string.Empty;
        }

        var attackerSession = _sessions.ActiveSessions.FirstOrDefault(session => session.Id.Equals(encounter.AttackerSessionId, StringComparison.OrdinalIgnoreCase));
        var defenderSession = _sessions.ActiveSessions.FirstOrDefault(session => session.Id.Equals(encounter.DefenderSessionId, StringComparison.OrdinalIgnoreCase));
        if (attackerSession?.Actor is not RuntimeCharacterActor attackerActor || defenderSession?.Actor is not RuntimeCharacterActor defenderActor ||
            !attackerSession.CurrentRoom.Id.Equals(encounter.RoomId, StringComparison.OrdinalIgnoreCase) ||
            !defenderSession.CurrentRoom.Id.Equals(encounter.RoomId, StringComparison.OrdinalIgnoreCase))
        { EndLivePvpEncounter(encounter); return "PvP combat ended because a participant is no longer present."; }
        var attacker = _characters.LoadCharacter(attackerActor.Id) ?? throw new InvalidOperationException("PvP attacker save was not found.");
        var defender = _characters.LoadCharacter(defenderActor.Id) ?? throw new InvalidOperationException("PvP defender save was not found.");
        var attackerProfile = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot), _characters.ProjectRoot).Resolve(attacker);
        var defenderProfile = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot), _characters.ProjectRoot).Resolve(defender);
        if (!attackerProfile.Ready || !defenderProfile.Ready) throw new InvalidOperationException("PVP_PLAYER_COMBAT_PROFILE_BLOCKED");
        var resolver = new ClassicCombatResolver(authority: RuntimeClassicFormulaAuthority.Load(_characters.ProjectRoot));
        var attackerHp = attacker.HitPoints;
        var defenderHp = defender.HitPoints;
        var attackerOutput = new List<string>();

        if (encounter.AttackerAutocombatActive)
        {
            var attackerResult = ResolvePvpStrike(resolver, attackerProfile, defenderProfile, encounter.AttackerAttackType);
            defenderHp = Math.Max(0, defenderHp - attackerResult.FinalDamage);
            attackerOutput.Add(RenderAttack(attacker.DisplayName, defender.DisplayName, attackerResult));
            defenderSession.EnqueueMessage(attackerResult.FinalDamage > 0
                ? $"{attacker.DisplayName} hits you for {attackerResult.FinalDamage} damage."
                : $"{attacker.DisplayName} misses you.");
        }

        if (defenderHp > 0 && encounter.DefenderAutocombatActive)
        {
            var defenderResult = ResolvePvpStrike(resolver, defenderProfile, attackerProfile, encounter.DefenderAttackType);
            attackerHp = Math.Max(0, attackerHp - defenderResult.FinalDamage);
            defenderSession.EnqueueMessage(RenderAttack(defender.DisplayName, attacker.DisplayName, defenderResult));
            attackerOutput.Add(defenderResult.FinalDamage > 0
                ? $"{defender.DisplayName} hits you for {defenderResult.FinalDamage} damage."
                : $"{defender.DisplayName} misses you.");
        }

        _characters.SaveCharacter(attacker with { HitPoints = attackerHp, UpdatedAtUtc = DateTimeOffset.UtcNow }, "runtime.pvp.round");
        _characters.SaveCharacter(defender with { HitPoints = defenderHp, UpdatedAtUtc = DateTimeOffset.UtcNow }, "runtime.pvp.round");
        if (defenderHp == 0 || attackerHp == 0)
        {
            var victimSession = defenderHp == 0 ? defenderSession : attackerSession;
            var victim = defenderHp == 0 ? defender with { HitPoints = 0 } : attacker with { HitPoints = 0 };
            var killerSession = defenderHp == 0 ? attackerSession : defenderSession;
            var death = CompletePlayerDefeat(victimSession, victim);
            EndLivePvpEncounter(encounter);
            victimSession.EnqueueMessage($"{killerSession.Actor.DisplayName} defeated you. {death}\n*Combat Off*");
            attackerOutput.Add($"{killerSession.Actor.DisplayName} defeats {victimSession.Actor.DisplayName}.\n*Combat Off*");
            return string.Join(Environment.NewLine, attackerOutput);
        }
        _livePvpEncounters[encounter.EncounterId] = encounter with { RoundNumber = encounter.RoundNumber + 1 };
        return string.Join(Environment.NewLine, attackerOutput);
    }

    public void CaptureClassicPracticeArenaEntry(RuntimeSession session, RuntimeRoom previousRoom)
    {
        if (session.Actor is not RuntimeCharacterActor actor ||
            !IsClassicPracticeArena(session) ||
            RuntimeClassicDeathLawPvpAuthority.IsPracticeArena(previousRoom,
                RuntimeClassicDeathLawPvpAuthority.IsArenaEnabled(_characters.ProjectRoot, session.World.Definition.Id))) return;
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return;
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeClassicDeathLawPvpAuthority.PracticeArenaEntryHpCondition] = Math.Max(1, character.HitPoints).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "classic.practice-arena.entry-hp");
    }

    private bool IsClassicPracticeArena(RuntimeSession session) =>
        RuntimeClassicDeathLawPvpAuthority.IsPracticeArena(session.CurrentRoom,
            RuntimeClassicDeathLawPvpAuthority.IsArenaEnabled(_characters.ProjectRoot, session.World.Definition.Id));

    private string CompletePracticeArenaPlayerDefeat(RuntimeSession victimSession, RuntimeCharacterRecord victim)
    {
        var restoreHp = RuntimeClassicDeathLawPvpAuthority.RequirePracticeArenaEntryHitPoints(victim);
        var evilPoints = RuntimeClassicNativeCharacterDeathAuthority.ReadEvilPoints(victim);
        var destination = RuntimeClassicDeathLawPvpAuthority.ResolveDeathDestination(victimSession.World, victimSession.CurrentRoom, evilPoints);
        var restored = victim with
        {
            HitPoints = restoreHp,
            Mana = victim.MaximumMana,
            RoomId = destination.Id,
            RoomNumber = destination.RoomNumber,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _characters.SaveCharacter(restored, "classic.practice-arena.defeat");
        victimSession.MoveToWorld(victimSession.World, destination, "classic-practice-arena-revival");
        return $"Practice-arena defeat costs no life or evil points. You revive in {destination.Name} with {restoreHp} HP and full mana.";
    }

    private static ClassicAttackResult ResolvePvpStrike(ClassicCombatResolver resolver, RuntimePlayerCombatProfile attacker,
        RuntimePlayerCombatProfile defender, ClassicAttackType attackType)
    {
        var attack = new ClassicCombatantStats(attacker.AttackValue + attacker.ClassCombatAccuracy + attacker.AbilityAccuracyModifier,
            attacker.DefenseValue, attacker.ArmorClass, attacker.DamageResistance, attacker.DodgeBonus, 0, attacker.CriticalChance);
        var defense = new ClassicCombatantStats(defender.AttackValue, defender.DefenseValue, defender.ArmorClass,
            defender.DamageResistance, defender.DodgeBonus, 0, defender.CriticalChance);
        return resolver.Resolve(new ClassicAttackRequest(attackType, attack, defense, attacker.MinimumDamage, attacker.MaximumDamage));
    }
}
