using System.Text.Json;

namespace RealmGate.Server;


public sealed record RuntimeNpcCombatMessageSelection(
    ClassicHitOutcome Outcome,
    int LegacyMessageId,
    string SafeFallback);

public sealed record RuntimeDecodedSpellClassification(
    string SpellTypeBucket,
    string TargetTypeBucket,
    string AttackTypeBucket,
    string ResistanceTypeBucket);

public enum RuntimeSpellEffectSign { Zero, Positive, Negative, Mixed }
public sealed record RuntimeNpcHitSpellEffectResolution(
    bool Executable,
    string SpellTypeMeaning,
    string TargetScope,
    string AttackElement,
    int RollMinimum,
    int RollMaximum,
    int Roll,
    int RawDirectDamage,
    int ElementalResistancePercent,
    bool PoisonImmune,
    int DirectDamage,
    string QuarantineReason);
public sealed record RuntimeElementalHitPointResolution(
    int HitPointsBefore,
    int MaximumHitPoints,
    int SignedDamage,
    int HitPointsAfter,
    int DamageApplied,
    int HealingApplied);
// One entry per populated decoded effect, in the original relative order.
// PopulatedOrdinal is not an absolute native slot index: empty positions in
// the generated projection are not represented here.
public sealed record RuntimeClassicNpcEffectSlot(
    int PopulatedOrdinal,
    int AbilityId,
    int RawValue,
    int EffectiveValue);

// This is a native dispatch *plan*, not a request to execute arbitrary
// special commands or create unowned monster instances. Each populated
// Summon(12) or TextBlock(148) slot is independently ordered.
public sealed record RuntimeClassicNativeSpecialSpellSlot(
    int PopulatedOrdinal,
    int AbilityId,
    int RawValue,
    int EffectiveId,
    bool NativeTargetAdmitted,
    bool ResolvedId);

public sealed record RuntimeNpcHitSpellStatusDeliveryResolution(
    bool Eligible,
    bool Applied,
    bool PoisonImmune,
    string EffectKind,
    string SpellId,
    int DurationSeconds,
    int MinimumEffect,
    int MaximumEffect,
    string Reason);

public sealed record RuntimeNpcHitSpellResistanceResolution(
    bool CheckEligible,
    bool AntiMagicRequired,
    int Willpower,
    int Intellect,
    int MagicResistanceBonus,
    int MagicResistanceScore,
    int ResistanceChancePercent,
    int Roll,
    bool Resisted);


// Native _get_room_targets operates on same-room live player sessions
// before _monster_cast_area generates its one shared effect magnitude.
public sealed record RuntimeClassicNpcAreaTargetSnapshot(
    string SessionId, bool SpellImmune, int Willpower, int Intellect,
    int MagicResistanceBonus, int AntiMagicAbility);
public sealed record RuntimeClassicNpcAreaTargetAdmission(
    string SessionId, bool Admitted, bool SpellImmune, int ResistanceRoll);
public sealed record RuntimeClassicNpcAreaRollPlan(
    IReadOnlyList<RuntimeClassicNpcAreaTargetAdmission> Targets,
    int EligibleTargetCount, int? SharedBaseMagnitude);

public sealed record RuntimeDecodedSpellEffectRange(
    int AuthoredFirstEndpoint,
    int AuthoredSecondEndpoint,
    int RollMinimum,
    int RollMaximum,
    RuntimeSpellEffectSign Sign,
    bool AuthoredDescending);

public sealed record RuntimeNpcHitSpellSelection(
    int LegacySpellId,
    string CanonicalSpellId,
    string SpellName,
    bool Triggered,
    int SpellLevel,
    int SpellTypeRaw,
    int TargetTypeRaw,
    int AttackTypeRaw,
    int MinimumEffect,
    int MaximumEffect,
    int DurationRaw,
    int ResistTypeRaw,
    int ResistAbilityRaw,
    int DifficultyRaw,
    RuntimeDecodedSpellClassification Classification,
    RuntimeDecodedSpellEffectRange EffectRange);

public sealed partial class RuntimeLiveGameplayFacade
{
    public string ExecuteLiveCombatRound(RuntimeSession session)
    {
        // Combat is also a live-status consumer. Expire persisted effects before resolving
        // profiles so stale poison/status projections cannot affect the round or falsely
        // block a valid reapplication as a duplicate.
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            ExpireLivePowers(session, DateTimeOffset.UtcNow);
        }
        if (!_liveCombats.TryGetValue(session.Id, out var encounter)) return "You are not in combat.";
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required for live combat.";
        var liveNpc = ResolveEncounterLivingNpc(session, encounter);
        if (liveNpc is null || !liveNpc.Alive || !liveNpc.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase))
        {
            _liveCombats.TryRemove(session.Id, out _);
            ReleaseCombatEngagement(session, encounter, "npc-no-longer-present");
            return $"{encounter.NpcName} is no longer here.\n*Combat Off*";
        }
        encounter = encounter with { NpcCurrentHitPoints = liveNpc.CurrentHitPoints, RoomId = liveNpc.RoomId };

        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        if (RuntimeClassicStatusProjection.MustFleeCombat(before))
            return FleeLiveCombat(session);
        if (IsLiveNpcFeared(session, encounter.NpcInstanceId))
        {
            _liveCombats.TryRemove(session.Id, out _);
            ReleaseCombatEngagement(session, encounter, "npc-fear-flee");
            _sessions.BroadcastRoom(session, $"{encounter.NpcName} flees from {actor.DisplayName}.");
            return $"{encounter.NpcName} flees in fear.\n*Combat Off*";
        }
        var roundStartCharacter = before;
        var player = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot, before.WorldId), _characters.ProjectRoot).Resolve(before);
        if (!player.Ready) return "Combat round blocked: " + string.Join(", ", player.Blockers);

        var npc = ResolveEncounterNpc(session, encounter);
        var npcDefense = RuntimeNpcDefensiveProfileResolver.Resolve(npc);
        var resolver = new ClassicCombatResolver(authority: RuntimeClassicFormulaAuthority.Load(_characters.ProjectRoot, before.WorldId));

        var isMartialAttack = encounter.PlayerAttackType is ClassicAttackType.Punch or ClassicAttackType.Kick or ClassicAttackType.JumpKick;
        var martialAccuracyBonus = RuntimeEquipmentCombatEffects.ResolveMartialAccuracy(before, encounter.PlayerAttackType);
        var resolvedPlayerAttackValue = encounter.PlayerAttackType == ClassicAttackType.Backstab
            ? player.BackstabAttackValue
            : checked(player.AttackValue + player.ClassCombatAccuracy + player.AbilityAccuracyModifier + martialAccuracyBonus);
        var playerStats = new ClassicCombatantStats(
            resolvedPlayerAttackValue,
            player.DefenseValue,
            player.ArmorClass,
            player.DamageResistance,
            player.DodgeBonus,
            0,
            isMartialAttack ? player.CriticalChanceWithoutQuickAndDeadly : player.CriticalChance);
        var npcStats = encounter.PlayerAttackType == ClassicAttackType.Backstab
            ? new ClassicCombatantStats(
                0,
                checked((npcDefense.ArmorClass / (npc.SeeHidden ? 1 : 4)) + npcDefense.BackstabDefense),
                0,
                npcDefense.DamageResistance,
                npcDefense.DodgeBonus)
            : npcDefense.ToCombatantStats();
        var playerMinimumDamage = player.MinimumDamage;
        var playerMaximumDamage = player.MaximumDamage;
        if (encounter.PlayerAttackType == ClassicAttackType.Backstab)
        {
            var stealth = RuntimePlayerCombatProfileResolver.ResolveModifiedStealth(before);
            playerMinimumDamage = RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(
                before.Level, stealth, playerMinimumDamage, player.BackstabMinimumBonus, RuntimePlayerCombatProfileResolver.HasClassStealth(before));
            playerMaximumDamage = RuntimeClassicPhysicalFormulaCalculator.CalculateBackstabDamage(
                before.Level, stealth, playerMaximumDamage, player.BackstabMaximumBonus, RuntimePlayerCombatProfileResolver.HasClassStealth(before));
            if (playerMaximumDamage < playerMinimumDamage) playerMaximumDamage = playerMinimumDamage;
        }
        else if (isMartialAttack)
        {
            var martialDamageBonus = RuntimeEquipmentCombatEffects.ResolveMartialDamage(before, encounter.PlayerAttackType);
            var strength = ReadBaseStat(before, "str", "strength");
            var unarmedMinimum = Math.Max(1, 1 + martialDamageBonus);
            var unarmedMaximum = checked(Math.Max(2, 2 + (strength / 25)) +
                player.StrengthMaximumDamageBonus + player.QuestMaximumDamageBonus +
                RuntimeClassicStatusProjection.Numeric(before, "maximum_damage") +
                RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(
                    before, "maximum_damage_bonus", player.QuestMaximumDamageBonus) + martialDamageBonus);
            playerMinimumDamage = unarmedMinimum;
            playerMaximumDamage = Math.Max(unarmedMinimum, unarmedMaximum);
        }
        var kaiPowerSuppressed = isMartialAttack && RuntimeClassicStatusProjection.IsKaiPowerBlocked(before);
        var playerAttackSuppressed = RuntimePlayerCombatProfileResolver.IsPhysicalAttackSuppressed(before);
        var confusionFumble = !playerAttackSuppressed &&
            RuntimeClassicStatusProjection.ConfusionFumbles(before, Random.Shared.Next(1, 101));
        var npcHp = encounter.NpcCurrentHitPoints;
        var playerEnergy = encounter.PlayerCurrentEnergy;
        var messages = new List<string>();
        var roomMessageOverrides = new Dictionary<string, string>(StringComparer.Ordinal);
        var roomMessagePriorities = new Dictionary<string, RuntimeClassicRoomMessagePriority>(StringComparer.Ordinal);
        if (!encounter.PlayerAutocombatActive)
        {
            // BREAK is character-only: suppress this character's automatic swings while
            // leaving the NPC's threat/attack side of the encounter active.
        }
        else if (kaiPowerSuppressed)
        {
            messages.Add(RuntimeClassicCombatAnsi.Warning($"{actor.DisplayName}'s Kai powers are bound this round."));
        }
        else if (playerAttackSuppressed)
        {
            messages.Add(RuntimeClassicCombatAnsi.Warning($"{actor.DisplayName} cannot act this round."));
        }
        else if (confusionFumble)
        {
            var messageId = RuntimeClassicStatusProjection.Numeric(before, "confusion_message_id");
            if (messageId > 0)
            {
                var native = NativePayloads(session.World.Definition.Id);
                var payload = native.RequireMessage(messageId);
                if (!string.IsNullOrEmpty(payload)) messages.Add(RuntimeClassicCombatAnsi.Warning(payload));
            }
            else
            {
                messages.Add(RuntimeClassicCombatAnsi.Warning($"{actor.DisplayName} fumbles."));
            }
        }
        else
        {
            var schedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(
                encounter.PlayerAttackType, playerEnergy, isMartialAttack ? 0 : player.AttackEnergyCost);
            playerEnergy = schedule.RemainingEnergy;

            if (schedule.Swings == 0)
                messages.Add(RuntimeClassicCombatAnsi.Warning($"{actor.DisplayName} is not ready to execute {encounter.PlayerAttackType} this round."));

            for (var swing = 0; swing < schedule.Swings && npcHp > 0; swing++)
            {
                var playerResult = resolver.Resolve(new ClassicAttackRequest(
                    encounter.PlayerAttackType,
                    playerStats,
                    npcStats,
                    playerMinimumDamage,
                    playerMaximumDamage));

                npcHp = Math.Max(0, npcHp - playerResult.FinalDamage);
                RegisterCombatDamageThreat(session, encounter, playerResult.FinalDamage, swing);
                if (playerResult.FinalDamage > 0 && !Living(session).ApplyNpcDamage(encounter.NpcInstanceId, playerResult.FinalDamage, DateTimeOffset.UtcNow))
                {
                    _liveCombats.TryRemove(session.Id, out _);
                    return $"{encounter.NpcName} is no longer a valid live combat target.";
                }
                var playerAttackMessage = RenderAttack(actor.DisplayName, encounter.NpcName, playerResult);
                messages.Add(playerAttackMessage);
                roomMessagePriorities[playerAttackMessage] = RuntimeClassicRoomMessagePriority.Low;
                if (!isMartialAttack && RuntimeWeaponOnHitSpellTrigger.IsEligibleHit(playerResult.Outcome))
                {
                    foreach (var effect in player.WeaponOnHitSpells)
                    {
                        var roll = Random.Shared.Next(1, 101);
                        if (!RuntimeWeaponOnHitSpellTrigger.Triggers(effect.ChancePercent, roll)) continue;
                        var proc = ExecuteWeaponOnHitSpell(session, effect.LegacySpellId);
                        npcHp = proc.NpcCurrentHitPoints;
                        if (!string.IsNullOrWhiteSpace(proc.Output)) messages.Add(proc.Output);
                        if (npcHp == 0) break;
                    }
                }
            }
        }

        var playerHp = before.HitPoints;
        var npcEnergy = _liveCombats.TryGetValue(session.Id, out var resourceUpdatedEncounter)
            ? resourceUpdatedEncounter.NpcCurrentEnergy
            : encounter.NpcCurrentEnergy;
        // Native _energy_update_monster refreshes active energy against the authored baseline:
        // below baseline add one quantum, above baseline clamp, equality unchanged.
        npcEnergy = RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(npcEnergy, encounter.NpcEnergyPerRound);
        var forcedMoveByNpcScript = false;
        var roundStartRoom = session.CurrentRoom;
        var alignmentProtection = RuntimeEquipmentCombatEffects.ResolveAlignmentProtection(before, npc.Alignment);
        var defendedPlayerStats = playerStats with { ArmorClass = checked(playerStats.ArmorClass + alignmentProtection) };
        if (npcHp > 0 && IsSelectedNpcTarget(session, encounter)
            && !IsNpcAttackSuppressedByItem(liveNpc, before))
        {
            var attacksThisRound = ResolveNpcAttacksPerRound(npc);
            for (var attackIndex = 0; attackIndex < attacksThisRound && playerHp > 0 && npcHp > 0; attackIndex++)
            {
                var attack = SelectNpcAttack(encounter.NpcAttacks, npcEnergy);
                if (attack is null) continue;
                if (!RuntimeClassicNpcCombatCadence.CanAfford(attack, npcEnergy))
                {
                    // Native selection does not re-normalize around unaffordable rows. A selected
                    // physical row can end the round; an unaffordable spell simply consumes this
                    // attempt slot and allows the next d100 selection attempt.
                    if (RuntimeClassicNpcCombatCadence.ShouldEndRoundAfterPhysicalSelection(attack, npcEnergy)) break;
                    continue;
                }

                var energyBeforeAttack = npcEnergy;
                var isDecodedSpellRow = attack.Type == 2 && attack.EffectiveSpellLegacyId > 0;
                var requiredStrikeCode = RuntimeClassicStatusProjection.Numeric(before, "required_to_hit");
                ClassicAttackResult npcResult;
                if (isDecodedSpellRow)
                {
                    var castSucceeded = RuntimeClassicNpcCombatCadence.SpellCastSucceeds(attack, Random.Shared.Next(0, 100));
                    npcEnergy = castSucceeded
                        ? RuntimeClassicNpcCombatCadence.DeductFullAttackCost(npcEnergy, attack)
                        : RuntimeClassicNpcCombatCadence.DeductFailedSpellCost(npcEnergy, attack);
                    npcResult = castSucceeded
                        ? new ClassicAttackResult(ClassicHitOutcome.Hit, attack.MinimumDamage, 0, 0, 0, 0, false)
                        : new ClassicAttackResult(ClassicHitOutcome.Miss, attack.MinimumDamage, 0, 0, 0, 0, false);
                }
                else
                {
                    npcEnergy = RuntimeClassicNpcCombatCadence.DeductFullAttackCost(npcEnergy, attack);
                    npcResult = !IsRequiredStrikeCodeSatisfied(requiredStrikeCode, incomingStrikeCode: 0)
                        ? new ClassicAttackResult(ClassicHitOutcome.Miss, 0, 0, 0, 0, 0, false)
                        : resolver.Resolve(new ClassicAttackRequest(
                            ResolveAttackType(attack.Type),
                            new ClassicCombatantStats(attack.Accuracy, 0, 0, 0, 0),
                            defendedPlayerStats,
                            attack.MinimumDamage,
                            attack.MaximumDamage));
                }
                playerHp = Math.Max(0, playerHp - npcResult.FinalDamage);
                if (!isDecodedSpellRow)
                {
                    var npcAttackMessage = RenderNpcAttack(session.World.Definition.Id, encounter.NpcName, actor.DisplayName, attack, npcResult);
                    if (!string.IsNullOrEmpty(npcAttackMessage))
                    {
                        messages.Add(npcAttackMessage);
                        roomMessagePriorities[npcAttackMessage] = RuntimeClassicRoomMessagePriority.Low;
                    }
                    var retaliationComponents = RuntimeEquipmentCombatEffects.ResolveEquipmentDamageShields(
                            _characters.ProjectRoot, before, npcResult.FinalDamage)
                        .Concat(ResolveLiveDamageShieldComponents(session, npcResult.FinalDamage))
                        .ToArray();
                    foreach (var retaliation in retaliationComponents)
                    {
                        var retaliationDamage = retaliation.Damage;
                        npcHp = Math.Max(0, npcHp - retaliationDamage);
                        if (!Living(session).ApplyNpcDamage(encounter.NpcInstanceId, retaliationDamage, DateTimeOffset.UtcNow))
                        {
                            _liveCombats.TryRemove(session.Id, out _);
                            return $"{encounter.NpcName} is no longer a valid live combat target.";
                        }
                        if (retaliation.MessageId > 0)
                        {
                            var native = NativePayloads(session.World.Definition.Id);
                            var sourceMessage = RuntimeEquipmentCombatEffects.FormatDamageShieldMessage(
                                native.RequireMessage(retaliation.MessageId), encounter.NpcName, retaliationDamage);
                            var observerTemplate = native.TryResolveTertiaryMessage(retaliation.MessageId, out var tertiary)
                                ? tertiary
                                : native.RequireMessage(retaliation.MessageId);
                            var observerMessage = RuntimeEquipmentCombatEffects.FormatDamageShieldMessage(
                                observerTemplate, encounter.NpcName, retaliationDamage);
                            var renderedSource = RuntimeClassicCombatAnsi.Effect(sourceMessage);
                            messages.Add(renderedSource);
                            roomMessageOverrides[renderedSource] = RuntimeClassicCombatAnsi.Effect(observerMessage);
                            roomMessagePriorities[renderedSource] = RuntimeClassicRoomMessagePriority.Low;
                        }
                        if (npcHp == 0) break;
                    }
                }
                var hitSpell = ResolveNpcHitSpell(attack, npcResult, _characters.ProjectRoot, session.World.Definition.Id);
                var nativeSpellDamage = 0;
                var nativeSpellHealing = false;
                if (hitSpell is { Triggered: true })
                {
                    var hitSpellScript = RuntimeNpcHitSpellScriptCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id, hitSpell.LegacySpellId);
                    // Classic _get_room_targets completes every recipient's immunity
                    // and resistance checks before _monster_cast_area draws its one
                    // shared magnitude. Custom worlds retain their original path.
                    var classicDirectHpAdmission = hitSpellScript is null && !IsNpcSelfSpell(hitSpell)
                        ? ResolveClassicNpcDirectHitPointAdmission(session, hitSpell)
                        : null;
                    var isNativeClassicNpcSpell = session.World.Definition.Type.Equals(
                        "Classic", StringComparison.OrdinalIgnoreCase);
                    var classicDirectRawSlotValue = hitSpellScript is null
                        && !IsNpcSelfSpell(hitSpell)
                        ? ResolveClassicNpcDirectRawSlotValue(session, hitSpell)
                        : null;
                    var classicDrainAbility8 = hitSpellScript is null
                        && isNativeClassicNpcSpell
                        && ClassicLiveStatusCatalog(session).TryGet(hitSpell.CanonicalSpellId, out var drainStatus)
                        && ClassicNpcHasDrainHandler(drainStatus);
                    var nativeAreaPlan = hitSpellScript is null
                        && session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase)
                        && IsNpcRoomAreaSpell(hitSpell)
                        ? BuildClassicNpcAreaSpellRollPlan(session, hitSpell)
                        : null;
                    var classicAreaDamageMinusMr17 = nativeAreaPlan is not null
                        && ClassicLiveStatusCatalog(session).TryGet(hitSpell.CanonicalSpellId, out var minusMrAreaStatus)
                        && minusMrAreaStatus.Abilities.Any(ability => ability.AbilityId == 17);
                    var classicAreaDamageAbility1 = nativeAreaPlan is not null
                        && ClassicLiveStatusCatalog(session).TryGet(hitSpell.CanonicalSpellId, out var directAreaStatus)
                        && directAreaStatus.Abilities.Any(ability => ability.AbilityId == 1);
                    // Native type-3 immediate ability1 is an HP handler,
                    // not a generic persistent status. Restrict to duration
                    // zero single-hostile spells; duration upkeep is separate.
                    var classicSingleDamageAbility1 = isNativeClassicNpcSpell
                        && hitSpell.TargetTypeRaw == 8 && hitSpell.DurationRaw == 0
                        && ClassicLiveStatusCatalog(session).TryGet(hitSpell.CanonicalSpellId,
                            out var directSingleStatus)
                        && directSingleStatus.Abilities.Any(ability => ability.AbilityId == 1);
                    var nativeAreaMagnitude = nativeAreaPlan?.SharedBaseMagnitude is int areaBase
                        ? ResolveClassicNpcAreaPerTargetBaseMagnitude(
                            areaBase, nativeAreaPlan.EligibleTargetCount, hitSpell.TargetTypeRaw)
                        : (int?)null;
                    var primaryAreaAdmission = nativeAreaPlan?.Targets.FirstOrDefault(
                        target => target.SessionId.Equals(session.Id, StringComparison.OrdinalIgnoreCase));
                    if (nativeAreaPlan is not null && primaryAreaAdmission is null)
                        throw new InvalidDataException("NPC_NATIVE_AREA_PRIMARY_SESSION_MISSING_FROM_ADMISSION");
                    if (hitSpellScript is not null)
                    {
                        var plan = RuntimeNpcHitSpellScriptCatalog.ValidateAndPlan(
                            _characters.ProjectRoot,
                            session.World,
                            hitSpellScript,
                            Random.Shared.Next(hitSpellScript.RollMinimum, checked(hitSpellScript.RollMaximum + 1)),
                            Random.Shared);
                        ExecuteNpcHitSpellScriptPlan(session, actor.DisplayName, plan, messages);
                        playerHp = Math.Max(0, playerHp - plan.TotalDamage);
                        forcedMoveByNpcScript = true;
                    }
                    else if (IsNpcSelfSpell(hitSpell))
                    {
                        var definition = ClassicLiveStatusCatalog(session).TryGet(hitSpell.CanonicalSpellId, out var decodedSelfStatus)
                            ? decodedSelfStatus
                            : throw new InvalidDataException($"NPC_SELF_SPELL_STATUS_UNRESOLVED:{hitSpell.CanonicalSpellId}");
                        // Native monster self-casts also roll the base effect magnitude
                        // before dispatching the ten ordered ability positions, including
                        // fixed ranges. A zero-valued ability inherits that base.
                        var classicSelfCast = session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase);
                        var resolvedSelfSlots = classicSelfCast
                            ? ResolveClassicNpcEffectSlots(definition.Abilities,
                                ResolveNpcStatusEffectMagnitude(hitSpell.MinimumEffect, hitSpell.MaximumEffect,
                                    null, Random.Shared, classicWorld: true))
                            : definition.Abilities.Select((ability, index) => new RuntimeClassicNpcEffectSlot(
                                index, ability.AbilityId, ability.Value, ability.Value)).ToArray();
                        // The native slot vector is ordered and may contain
                        // repeated control abilities (e.g. several RemovesSpell
                        // entries). Only actual stat modifiers can be placed
                        // in the timed-status dictionary. Retain Custom's
                        // existing projection until its separate audit.
                        var modifiers = classicSelfCast
                            ? ResolveClassicNpcTimedModifierProjection(resolvedSelfSlots)
                            : resolvedSelfSlots.Where(slot => slot.EffectiveValue != 0)
                                .ToDictionary(slot => slot.AbilityId, slot => slot.EffectiveValue);
                        if (definition.DurationRounds > 0 && modifiers.Count > 0)
                            Living(session).ApplyNpcTimedStatus(encounter.NpcInstanceId, definition.SpellId, definition.DurationRounds, modifiers, DateTimeOffset.UtcNow);
                        if (classicSelfCast && definition.DurationRounds == 0 && npcHp > 0)
                        {
                            // Immediate native HealMana(150) modifies the
                            // monster's current combat energy in slot order.
                            // Duration-based mana upkeep remains a separate
                            // lifecycle handler; do not manufacture ticks.
                            foreach (var manaSlot in resolvedSelfSlots.Where(slot =>
                                         slot.AbilityId == 150))
                                npcEnergy = (int)Math.Clamp(
                                    (long)npcEnergy + manaSlot.EffectiveValue, 0, int.MaxValue);
                        }
                        if (classicSelfCast && definition.DurationRounds == 0 && npcHp > 0)
                        {
                            // Native Heal(18) is not a timed combat-stat
                            // modifier. For an immediate positive self-cast,
                            // apply every ordered healing slot directly to
                            // the same authoritative living NPC HP state.
                            // Negative heals and duration upkeep are isolated
                            // until their distinct native branches are mapped.
                            foreach (var healSlot in resolvedSelfSlots.Where(slot =>
                                         slot.AbilityId == 18 && slot.EffectiveValue > 0))
                            {
                                if (Living(session).ApplyNpcHealing(
                                        encounter.NpcInstanceId, healSlot.EffectiveValue,
                                        DateTimeOffset.UtcNow))
                                    npcHp = (int)Math.Min((long)liveNpc.MaximumHitPoints,
                                        (long)npcHp + healSlot.EffectiveValue);
                            }
                        }
                        if (classicSelfCast)
                        {
                            // _monster_cast diverts target type 1 into the native
                            // area/self handler, whose ability-12 branch summons
                            // once per original slot (0x427641..0x4276C0).
                            // Keep the authoritative living-world room/AI owner;
                            // no special-command TextBlocks are executed here.
                            var selfSummonSlots = ResolveClassicNativeSpecialSpellSlots(
                                resolvedSelfSlots, hitSpell.TargetTypeRaw, nativeAreaDispatcher: true);
                            foreach (var slot in selfSummonSlots.Where(slot =>
                                         slot.AbilityId == 12 && slot.NativeTargetAdmitted && slot.ResolvedId))
                                _ = Living(session).TrySummonClassicSpellNpc(
                                    slot.EffectiveId, session.CurrentRoom.Id,
                                    $"classic-native-spell:{encounter.NpcInstanceId}:{hitSpell.LegacySpellId}");
                        }
                    }
                    // Native target type 1 is Self. Do not run player resistance, damage,
                    // status, or scripted-character execution for an NPC self-cast. The
                    // two generated spell payloads still fail closed at their separate
                    // NPC-effect boundaries (TextBlock 2510 and NPC Picklocks).
                    if (hitSpellScript is null && !IsNpcSelfSpell(hitSpell))
                    {
                        if (nativeAreaPlan is not null
                            ? primaryAreaAdmission!.SpellImmune
                            : RuntimeClassicStatusProjection.IsSpellImmune(before, hitSpell.SpellLevel))
                        {
                            var spellImmuneMessage = RuntimeClassicCombatAnsi.Effect($"{actor.DisplayName} is immune to {encounter.NpcName}'s {hitSpell.SpellName}.");
                            messages.Add(spellImmuneMessage);
                            roomMessagePriorities[spellImmuneMessage] = RuntimeClassicRoomMessagePriority.Medium;
                        }
                        else
                        {
                            var resisted = nativeAreaPlan is not null
                                ? !primaryAreaAdmission!.Admitted
                                : ResolveNpcHitSpellResistanceWithAdmission(
                                    hitSpell.ResistTypeRaw,
                                    ReadBaseStat(before, "wil", "willpower", "will"),
                                    ReadBaseStat(before, "int", "intellect", "intelligence"),
                                    RuntimeClassicCharacterFormulaCalculator.ResolveMagicResistanceModifier(before),
                                    ReadAbilityValue(before, 51),
                                    Random.Shared).Resisted;
                            if (resisted)
                            {
                                // Native _monster_cast has already passed its pre-cast roll and paid the full
                                // AttEnergy here. A single-target effect-admission/resistance rejection restores
                                // that full cost (net zero). Area casts keep the shared cast cost because other
                                // room targets may still receive effects.
                                if (attack.Type == 2 && !IsNpcRoomAreaSpell(hitSpell))
                                    npcEnergy = RuntimeClassicNpcCombatCadence.RefundRejectedSpellCost(energyBeforeAttack, attack);
                                var spellResistMessage = RuntimeClassicCombatAnsi.Effect($"{actor.DisplayName} resists {encounter.NpcName}'s {hitSpell.SpellName}.");
                                messages.Add(spellResistMessage);
                                roomMessagePriorities[spellResistMessage] = RuntimeClassicRoomMessagePriority.Medium;
                            }
                            else
                            {
                                var effect = ResolveNpcHitSpellEffect(
                                    hitSpell,
                                    ReadElementalResistance(before, ResolveNpcHitSpellAttackElement(
                                        hitSpell.AttackTypeRaw, isNativeClassicNpcSpell)),
                                    ReadAbilityValue(before, 21) > 0,
                                    Random.Shared, nativeAreaMagnitude, nativeAreaPlan is not null,
                                    classicDirectHpAdmission, classicDrainAbility8,
                                    classicAreaDamageMinusMr17, classicAreaDamageAbility1,
                                    classicSingleDamageAbility1,
                                    ReadMagicResistance(before),
                                    ReadAbilityValue(before, 51) > 0,
                                    isNativeClassicNpcSpell, classicDirectRawSlotValue);
                                var nativeSpecialSummoned = false;
                                if (isNativeClassicNpcSpell && hitSpell.TargetTypeRaw == 8
                                    && ClassicLiveStatusCatalog(session).TryGet(hitSpell.CanonicalSpellId,
                                        out var nativeTargetSummonDefinition)
                                    && nativeTargetSummonDefinition.Abilities.Any(ability => ability.AbilityId == 12))
                                {
                                    // _monster_cast's target-type-8 ability12 branch
                                    // is reached only after admission/resistance.
                                    // A single base magnitude roll belongs to the
                                    // whole ordered spell even with fixed summon IDs.
                                    var baseMagnitude = effect.Executable ? effect.Roll :
                                        ResolveNpcStatusEffectMagnitude(hitSpell.MinimumEffect,
                                            hitSpell.MaximumEffect, null, Random.Shared, classicWorld: true);
                                    var nativeSummonSlots = ResolveClassicNativeSpecialSpellSlots(
                                        ResolveClassicNpcEffectSlots(nativeTargetSummonDefinition.Abilities,
                                            baseMagnitude), hitSpell.TargetTypeRaw);
                                    foreach (var slot in nativeSummonSlots.Where(slot =>
                                                 slot.AbilityId == 12 && slot.NativeTargetAdmitted
                                                     && slot.ResolvedId))
                                        nativeSpecialSummoned |= Living(session).TrySummonClassicSpellNpc(
                                            slot.EffectiveId, session.CurrentRoom.Id,
                                            $"classic-native-spell:{encounter.NpcInstanceId}:{hitSpell.LegacySpellId}") is not null;
                                }
                                if (effect.Executable && effect.DirectDamage != 0)
                                {
                                    var hitPointResolution = ApplyElementalSpellHitPoints(playerHp, before.MaximumHitPoints, effect.DirectDamage);
                                    playerHp = hitPointResolution.HitPointsAfter;
                                    // WCCMMUD _monster_cast ability 8 at 0x428A9A..0x428ACE
                                    // applies the same raw magnitude to the monster's current HP
                                    // and clamps to its definition maximum (+0x78). Do not
                                    // substitute capped actual player damage for that magnitude.
                                    // The native area variant uses the same caster HP
                                    // recovery, once for each admitted player recipient.
                                    if (npcHp > 0
                                        && session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase)
                                        && classicDrainAbility8 && effect.RawDirectDamage > 0)
                                    {
                                        var healedNpcHp = ResolveClassicNpcDrainHitPointsAfter(
                                            npcHp, liveNpc.MaximumHitPoints, effect.RawDirectDamage);
                                        if (healedNpcHp > npcHp)
                                        {
                                            if (!Living(session).ApplyNpcHealing(
                                                encounter.NpcInstanceId, effect.RawDirectDamage, DateTimeOffset.UtcNow))
                                                throw new InvalidOperationException(
                                                    $"NPC_NATIVE_DRAIN_CASTER_MISSING:{encounter.NpcInstanceId}");
                                            npcHp = healedNpcHp;
                                        }
                                    }
                                    if (hitPointResolution.DamageApplied > 0)
                                    {
                                        nativeSpellDamage = hitPointResolution.DamageApplied;
                                        if (!isDecodedSpellRow)
                                        {
                                            var spellHitMessage = RuntimeClassicCombatAnsi.NpcAttack($"{encounter.NpcName}'s {hitSpell.SpellName} hits {actor.DisplayName} for {hitPointResolution.DamageApplied} damage.", ClassicHitOutcome.Hit);
                                            messages.Add(spellHitMessage);
                                            roomMessagePriorities[spellHitMessage] = RuntimeClassicRoomMessagePriority.Medium;
                                        }
                                    }
                                    else
                                    {
                                        nativeSpellHealing = true;
                                        var spellHealingMessage = RuntimeClassicCombatAnsi.Effect(hitPointResolution.HealingApplied > 0
                                            ? $"{encounter.NpcName}'s {hitSpell.SpellName} heals {actor.DisplayName} for {hitPointResolution.HealingApplied} hit points."
                                            : $"{encounter.NpcName}'s {hitSpell.SpellName} would heal {actor.DisplayName}, but {actor.DisplayName} is already at full health.");
                                        messages.Add(spellHealingMessage);
                                        roomMessagePriorities[spellHealingMessage] = RuntimeClassicRoomMessagePriority.Medium;
                                    }
                                    var durationDelivery = ApplyNpcHitSpellStatusDelivery(
                                        session,
                                        before,
                                        hitSpell,
                                        $"npc.{npc.LegacyId}",
                                        ReadAbilityValue(before, 21) > 0,
                                        DateTimeOffset.UtcNow,
                                        effect.Executable ? effect.Roll : nativeAreaMagnitude,
                                        nativeAreaPlan is not null);
                                    if (durationDelivery.Applied)
                                    {
                                        before = _characters.LoadCharacter(actor.Id) ?? before;
                                        var durationMessage = RuntimeClassicCombatAnsi.Effect($"{encounter.NpcName}'s {hitSpell.SpellName} affects {actor.DisplayName} for {durationDelivery.DurationSeconds} combat rounds.");
                                        messages.Add(durationMessage);
                                        roomMessagePriorities[durationMessage] = RuntimeClassicRoomMessagePriority.Medium;
                                    }
                                }
                                else
                                {
                                    var delivery = ApplyNpcHitSpellStatusDelivery(
                                        session,
                                        before,
                                        hitSpell,
                                        $"npc.{npc.LegacyId}",
                                        ReadAbilityValue(before, 21) > 0,
                                        DateTimeOffset.UtcNow,
                                        effect.Executable ? effect.Roll : nativeAreaMagnitude,
                                        nativeAreaPlan is not null);
                                    if (delivery.Applied)
                                    {
                                        before = _characters.LoadCharacter(actor.Id) ?? before;
                                        var statusMessage = RuntimeClassicCombatAnsi.Effect($"{encounter.NpcName}'s {hitSpell.SpellName} affects {actor.DisplayName} for {delivery.DurationSeconds} combat rounds.");
                                        messages.Add(statusMessage);
                                        roomMessagePriorities[statusMessage] = RuntimeClassicRoomMessagePriority.Medium;
                                    }
                                    else if (nativeSpecialSummoned)
                                    {
                                        // The native summon already created a living NPC,
                                        // so this is not a no-effect spell. Native cast
                                        // messages remain owned by the attack renderer.
                                    }
                                    else if (delivery.PoisonImmune || effect.QuarantineReason == "elemental-immunity")
                                    {
                                        var spellImmuneMessage = RuntimeClassicCombatAnsi.Effect($"{actor.DisplayName} is immune to {encounter.NpcName}'s {hitSpell.SpellName}.");
                                        messages.Add(spellImmuneMessage);
                                        roomMessagePriorities[spellImmuneMessage] = RuntimeClassicRoomMessagePriority.Medium;
                                    }
                                    else
                                    {
                                        var noEffectMessage = RuntimeClassicCombatAnsi.Warning($"{encounter.NpcName}'s {hitSpell.SpellName} has no applicable effect on {actor.DisplayName}.");
                                        messages.Add(noEffectMessage);
                                        roomMessagePriorities[noEffectMessage] = RuntimeClassicRoomMessagePriority.Medium;
                                    }
                                }
                            }
                        }

                    if (IsNpcRoomAreaSpell(hitSpell))
                        npcHp = ApplyNpcRoomAreaSpellToBystanders(
                            session,
                            hitSpell,
                            $"npc.{npc.LegacyId}",
                            encounter.NpcName,
                            messages,
                            roomMessagePriorities,
                            nativeAreaPlan,
                            nativeAreaMagnitude,
                            classicDirectHpAdmission,
                            classicDrainAbility8,
                            classicAreaDamageMinusMr17,
                            classicAreaDamageAbility1,
                            classicSingleDamageAbility1,
                            classicDirectRawSlotValue,
                            encounter.NpcInstanceId,
                            npcHp,
                            liveNpc.MaximumHitPoints);
                    }
                }
                if (isDecodedSpellRow && !nativeSpellHealing)
                {
                    var spellMessageResult = npcResult with
                    {
                        RolledDamage = nativeSpellDamage,
                        DamageAfterDr = nativeSpellDamage,
                        FinalDamage = nativeSpellDamage
                    };
                    var npcSpellMessage = RenderNpcAttack(session.World.Definition.Id, encounter.NpcName, actor.DisplayName, attack, spellMessageResult);
                    if (!string.IsNullOrEmpty(npcSpellMessage))
                    {
                        messages.Add(npcSpellMessage);
                        roomMessagePriorities[npcSpellMessage] = RuntimeClassicRoomMessagePriority.Medium;
                    }
                }

                if (RuntimeClassicNpcCombatCadence.ShouldEndRoundAfterPhysicalSelection(attack, npcEnergy)) break;
            }
        }
        else if (npcHp > 0)
        {
            messages.Add(RuntimeClassicCombatAnsi.Warning($"{encounter.NpcName} is focused on another combatant."));
        }

        try
        {
            if (npcHp == 0)
            {
                // Keep every encounter targeting this exact NPC live until the defeat handler
                // snapshots death-moment combat participation for native XP distribution.
                messages.Add(RuntimeClassicCombatAnsi.Victory($"{encounter.NpcName} is defeated."));
                messages.Add(CompleteNpcDefeat(session, encounter, before, npc, playerHp));
            }
            else if (playerHp == 0)
            {
                _liveCombats.TryRemove(session.Id, out _);
                ReleaseCombatEngagement(session, encounter, "player-defeat");
                messages.Add(RuntimeClassicCombatAnsi.Defeat($"{actor.DisplayName} is defeated."));
                messages.Add(CompletePlayerDefeat(session, before));
            }
            else if (forcedMoveByNpcScript)
            {
                _liveCombats.TryRemove(session.Id, out _);
                ReleaseCombatEngagement(session, encounter, "npc-hit-spell-forced-move");
                before = before with
                {
                    HitPoints = playerHp,
                    RoomId = session.CurrentRoom.Id,
                    RoomNumber = session.CurrentRoom.RoomNumber,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                _characters.SaveCharacter(before, "runtime.live_combat.npc_hit_spell_script:generated-chain");
                messages.Add(RoomRenderer.Render(session.World, session.CurrentRoom));
            }
            else
            {
                _characters.SaveCharacter(before with { HitPoints = playerHp, UpdatedAtUtc = DateTimeOffset.UtcNow });
                _liveCombats[session.Id] = encounter with
                {
                    NpcCurrentHitPoints = npcHp,
                    NpcCurrentEnergy = npcEnergy,
                    PlayerCurrentEnergy = playerEnergy,
                    RoundNumber = checked(encounter.RoundNumber + 1),
                    PlayerAttackType = ClassicAttackType.NormalWeapon
                };
            }
        }
        catch
        {
            if (forcedMoveByNpcScript)
                session.MoveToWorld(session.World, roundStartRoom, "npc-hit-spell-script-rollback");
            _characters.SaveCharacter(roundStartCharacter);
            throw;
        }

        if (!forcedMoveByNpcScript)
            foreach (var message in messages)
                _sessions.BroadcastRoom(
                    session,
                    roomMessageOverrides.GetValueOrDefault(message, message),
                    includeSource: false,
                    priority: roomMessagePriorities.GetValueOrDefault(message, RuntimeClassicRoomMessagePriority.High));
        messages.Add(RuntimeClassicCombatAnsi.RoundSummary(playerHp, before.MaximumHitPoints, encounter.NpcName, npcHp, encounter.NpcMaximumHitPoints));
        return string.Join(Environment.NewLine, messages);
    }

    internal static class RuntimeWeaponOnHitSpellTrigger
    {
        public static bool IsEligibleHit(ClassicHitOutcome outcome) => outcome is ClassicHitOutcome.Hit or ClassicHitOutcome.Critical;
        public static bool Triggers(int chancePercent, int roll) =>
            chancePercent is >= 1 and <= 100 && roll is >= 1 and <= 100 && roll <= chancePercent;
    }

    private void ExecuteNpcHitSpellScriptPlan(
        RuntimeSession session,
        string actorName,
        RuntimeNpcHitSpellScriptPlan plan,
        List<string> messages)
    {
        var native = NativePayloads(session.World.Definition.Id);
        foreach (var operation in plan.Branch.Operations)
        {
            if (operation.Kind == "message")
            {
                messages.Add(native.RequireMessage(operation.FirstArgument));
                if (native.TryResolveSecondaryMessage(operation.FirstArgument, out var secondary) && !string.IsNullOrWhiteSpace(secondary))
                    _sessions.BroadcastRoom(
                        session,
                        secondary.Replace("%s", actorName, StringComparison.Ordinal),
                        includeSource: false,
                        priority: RuntimeClassicRoomMessagePriority.Medium);
            }
            else if (operation.Kind == "teleport")
            {
                var room = session.World.FindRoom(operation.SecondArgument, operation.FirstArgument)
                    ?? throw new InvalidOperationException($"Validated NPC hit-spell room disappeared: {operation.SecondArgument}/{operation.FirstArgument}.");
                session.MoveToWorld(session.World, room, "npc-hit-spell-script-teleport");
            }
        }
    }

    internal static bool IsNpcAreaHostileSpell(RuntimeNpcHitSpellSelection spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.TargetTypeRaw == 12;
    }

    internal static bool IsRequiredStrikeCodeSatisfied(int requiredCode, int incomingStrikeCode) =>
        requiredCode <= 0 || incomingStrikeCode == requiredCode;

    internal static bool IsNpcSelfSpell(RuntimeNpcHitSpellSelection spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.TargetTypeRaw == 1;
    }

    internal static bool IsNpcFullAreaSpell(RuntimeNpcHitSpellSelection spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.TargetTypeRaw == 11;
    }

    internal static bool IsNpcRoomAreaSpell(RuntimeNpcHitSpellSelection spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return IsNpcFullAreaSpell(spell) || IsNpcAreaHostileSpell(spell);
    }

    private int? ResolveClassicNpcDirectRawSlotValue(
        RuntimeSession session, RuntimeNpcHitSpellSelection spell)
    {
        if (!session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase))
            return null;
        var catalog = ClassicLiveStatusCatalog(session);
        if (!catalog.TryGet(spell.CanonicalSpellId, out var definition))
            throw new InvalidDataException(
                $"NPC_CLASSIC_DIRECT_ABILITY_VECTOR_MISSING:{spell.CanonicalSpellId}");
        return definition.Abilities.FirstOrDefault(ability =>
            ability.AbilityId is 1 or 8 or 17)?.Value;
    }

    private bool? ResolveClassicNpcDirectHitPointAdmission(
        RuntimeSession session, RuntimeNpcHitSpellSelection spell)
    {
        if (!session.World.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase))
            return null;
        var catalog = ClassicLiveStatusCatalog(session);
        if (!catalog.TryGet(spell.CanonicalSpellId, out var definition))
            throw new InvalidDataException($"NPC_CLASSIC_DIRECT_ABILITY_VECTOR_MISSING:{spell.CanonicalSpellId}");
        return ClassicNpcHasDirectHitPointHandler(definition);
    }

    private RuntimeClassicNpcAreaRollPlan BuildClassicNpcAreaSpellRollPlan(
        RuntimeSession primarySession, RuntimeNpcHitSpellSelection spell)
    {
        // Reforged's session manager sorts by player display name. The native
        // Worldgroup session-index iteration is not yet fully mapped.
        // Preserve the current live order and retain this as an explicit
        // exact-RNG-sequence parity boundary.
        var snapshots = new List<RuntimeClassicNpcAreaTargetSnapshot>();
        foreach (var target in _sessions.InRoom(
                     primarySession.World.Definition.Id, primarySession.CurrentRoom.Id))
        {
            if (target.Actor is not RuntimeCharacterActor actor) continue;
            var character = _characters.LoadCharacter(actor.Id);
            if (character is null || character.HitPoints <= 0) continue;
            snapshots.Add(new RuntimeClassicNpcAreaTargetSnapshot(
                target.Id,
                RuntimeClassicStatusProjection.IsSpellImmune(character, spell.SpellLevel),
                ReadBaseStat(character, "wil", "willpower", "will"),
                ReadBaseStat(character, "int", "intellect", "intelligence"),
                RuntimeClassicCharacterFormulaCalculator.ResolveMagicResistanceModifier(character),
                ReadAbilityValue(character, 51)));
        }
        return ResolveClassicNpcAreaRollPlan(
            spell.ResistTypeRaw, spell.MinimumEffect, spell.MaximumEffect,
            snapshots, Random.Shared);
    }

    private int ApplyNpcRoomAreaSpellToBystanders(
        RuntimeSession primarySession,
        RuntimeNpcHitSpellSelection spell,
        string casterId,
        string casterName,
        List<string> messages,
        Dictionary<string, RuntimeClassicRoomMessagePriority> roomMessagePriorities,
        RuntimeClassicNpcAreaRollPlan? nativeAreaPlan,
        int? nativeAreaMagnitude,
        bool? classicDirectHpAdmission,
        bool classicDrainAbility8,
        bool classicAreaDamageMinusMr17,
        bool classicAreaDamageAbility1,
        bool classicSingleDamageAbility1,
        int? classicDirectRawSlotValue,
        string casterNpcInstanceId,
        int casterHitPoints,
        int casterMaximumHitPoints)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var targets = _sessions.InRoom(primarySession.World.Definition.Id, primarySession.CurrentRoom.Id)
            .Where(target => !target.Id.Equals(primarySession.Id, StringComparison.OrdinalIgnoreCase))
            .Where(target => target.Actor is RuntimeCharacterActor)
            .ToArray();

        foreach (var target in targets)
        {
            var actor = (RuntimeCharacterActor)target.Actor;
            var character = _characters.LoadCharacter(actor.Id);
            if (character is null || character.HitPoints <= 0) continue;

            var areaAdmission = nativeAreaPlan?.Targets.FirstOrDefault(
                entry => entry.SessionId.Equals(target.Id, StringComparison.OrdinalIgnoreCase));
            if (nativeAreaPlan is not null && areaAdmission is null) continue;
            if (nativeAreaPlan is not null
                ? areaAdmission!.SpellImmune
                : RuntimeClassicStatusProjection.IsSpellImmune(character, spell.SpellLevel))
            {
                var immuneMessage = RuntimeClassicCombatAnsi.Effect($"{actor.DisplayName} is immune to {casterName}'s {spell.SpellName}.");
                messages.Add(immuneMessage);
                roomMessagePriorities[immuneMessage] = RuntimeClassicRoomMessagePriority.Medium;
                continue;
            }

            var resisted = nativeAreaPlan is not null
                ? !areaAdmission!.Admitted
                : ResolveNpcHitSpellResistanceWithAdmission(
                    spell.ResistTypeRaw,
                    ReadBaseStat(character, "wil", "willpower", "will"),
                    ReadBaseStat(character, "int", "intellect", "intelligence"),
                    RuntimeClassicCharacterFormulaCalculator.ResolveMagicResistanceModifier(character),
                    ReadAbilityValue(character, 51),
                    Random.Shared).Resisted;
            if (resisted)
            {
                var resistMessage = RuntimeClassicCombatAnsi.Effect($"{actor.DisplayName} resists {casterName}'s {spell.SpellName}.");
                messages.Add(resistMessage);
                roomMessagePriorities[resistMessage] = RuntimeClassicRoomMessagePriority.Medium;
                continue;
            }

            var poisonImmune = ReadAbilityValue(character, 21) > 0;
            var effect = ResolveNpcHitSpellEffect(
                spell,
                ReadElementalResistance(character, ResolveNpcHitSpellAttackElement(
                    spell.AttackTypeRaw, nativeAreaPlan is not null)),
                poisonImmune,
                Random.Shared, nativeAreaMagnitude, nativeAreaPlan is not null,
                classicDirectHpAdmission, classicDrainAbility8,
                classicAreaDamageMinusMr17, classicAreaDamageAbility1,
                classicSingleDamageAbility1,
                ReadMagicResistance(character),
                ReadAbilityValue(character, 51) > 0,
                nativeAreaPlan is not null, classicDirectRawSlotValue);
            if (effect.Executable && effect.DirectDamage != 0)
            {
                var hitPointResolution = ApplyElementalSpellHitPoints(character.HitPoints, character.MaximumHitPoints, effect.DirectDamage);
                var nextHitPoints = hitPointResolution.HitPointsAfter;
                // Native area Drain (0x427312..0x42734C) heals the casting
                // monster once for each affected room player, independent of
                // the victim's remaining HP. The primary target was processed
                // before this loop and is not in the bystander enumeration.
                if (classicDrainAbility8 && effect.DirectDamage > 0
                    && casterHitPoints > 0 && casterHitPoints < casterMaximumHitPoints)
                {
                    var healed = ResolveClassicNpcDrainHitPointsAfter(
                        casterHitPoints, casterMaximumHitPoints, effect.DirectDamage);
                    if (!Living(primarySession).ApplyNpcHealing(
                        casterNpcInstanceId, effect.DirectDamage, nowUtc))
                        throw new InvalidOperationException($"NPC_NATIVE_AREA_DRAIN_CASTER_MISSING:{casterNpcInstanceId}");
                    casterHitPoints = healed;
                }
                if (hitPointResolution.DamageApplied > 0)
                {
                    var areaHitMessage = RuntimeClassicCombatAnsi.NpcAttack(
                        $"{casterName}'s {spell.SpellName} hits {actor.DisplayName} for {hitPointResolution.DamageApplied} damage.",
                        ClassicHitOutcome.Hit);
                    messages.Add(areaHitMessage);
                    roomMessagePriorities[areaHitMessage] = RuntimeClassicRoomMessagePriority.Medium;
                    if (nextHitPoints == 0)
                    {
                        if (_liveCombats.TryRemove(target.Id, out var targetEncounter))
                            ReleaseCombatEngagement(target, targetEncounter, "area-spell-player-defeat");
                        messages.Add(RuntimeClassicCombatAnsi.Defeat($"{actor.DisplayName} is defeated."));
                        messages.Add(CompletePlayerDefeat(target, character));
                    }
                    else
                    {
                        _characters.SaveCharacter(character with
                        {
                            HitPoints = nextHitPoints,
                            UpdatedAtUtc = nowUtc
                        }, $"runtime.live_combat.npc_area_spell:{spell.CanonicalSpellId}");
                    }
                }
                else
                {
                    var areaHealingMessage = RuntimeClassicCombatAnsi.Effect(hitPointResolution.HealingApplied > 0
                        ? $"{casterName}'s {spell.SpellName} heals {actor.DisplayName} for {hitPointResolution.HealingApplied} hit points."
                        : $"{casterName}'s {spell.SpellName} would heal {actor.DisplayName}, but {actor.DisplayName} is already at full health.");
                    messages.Add(areaHealingMessage);
                    roomMessagePriorities[areaHealingMessage] = RuntimeClassicRoomMessagePriority.Medium;
                    if (hitPointResolution.HealingApplied > 0)
                    {
                        character = character with { HitPoints = nextHitPoints, UpdatedAtUtc = nowUtc };
                        _characters.SaveCharacter(character, $"runtime.live_combat.npc_area_spell_resistance_heal:{spell.CanonicalSpellId}");
                    }
                }
                if (nextHitPoints > 0)
                {
                    var durationDelivery = ApplyNpcHitSpellStatusDelivery(target, character, spell, casterId, poisonImmune, nowUtc,
                        effect.Executable ? effect.Roll : nativeAreaMagnitude,
                        nativeAreaPlan is not null);
                    if (durationDelivery.Applied)
                    {
                        var areaDurationMessage = RuntimeClassicCombatAnsi.Effect($"{casterName}'s {spell.SpellName} affects {actor.DisplayName} for {durationDelivery.DurationSeconds} combat rounds.");
                        messages.Add(areaDurationMessage);
                        roomMessagePriorities[areaDurationMessage] = RuntimeClassicRoomMessagePriority.Medium;
                    }
                }
                continue;
            }

            var delivery = ApplyNpcHitSpellStatusDelivery(target, character, spell, casterId, poisonImmune, nowUtc,
                        effect.Executable ? effect.Roll : nativeAreaMagnitude,
                        nativeAreaPlan is not null);
            if (delivery.Applied)
            {
                var areaStatusMessage = RuntimeClassicCombatAnsi.Effect($"{casterName}'s {spell.SpellName} affects {actor.DisplayName} for {delivery.DurationSeconds} combat rounds.");
                messages.Add(areaStatusMessage);
                roomMessagePriorities[areaStatusMessage] = RuntimeClassicRoomMessagePriority.Medium;
            }
            else if (delivery.PoisonImmune || effect.QuarantineReason == "elemental-immunity")
            {
                var areaImmuneMessage = RuntimeClassicCombatAnsi.Effect($"{actor.DisplayName} is immune to {casterName}'s {spell.SpellName}.");
                messages.Add(areaImmuneMessage);
                roomMessagePriorities[areaImmuneMessage] = RuntimeClassicRoomMessagePriority.Medium;
            }
            else
            {
                var areaNoEffectMessage = RuntimeClassicCombatAnsi.Warning($"{casterName}'s {spell.SpellName} has no applicable effect on {actor.DisplayName}.");
                messages.Add(areaNoEffectMessage);
                roomMessagePriorities[areaNoEffectMessage] = RuntimeClassicRoomMessagePriority.Medium;
            }
        }
        return casterHitPoints;
    }

    internal static int ResolveNpcAttacksPerRound(RuntimeNpcDefinition npc)
        => RuntimeClassicNpcCombatCadence.ResolveAttemptSlots(npc);

    internal static RuntimeNpcAttackDefinition? SelectNpcAttack(IReadOnlyList<RuntimeNpcAttackDefinition> attacks, int currentEnergy)
        => RuntimeNpcAttackSelector.Select(attacks, currentEnergy, Random.Shared).Attack;

    internal static int ResolveNpcEnergyAfterAttack(int currentEnergy, RuntimeNpcAttackDefinition attack)
        => RuntimeClassicNpcCombatCadence.DeductFullAttackCost(currentEnergy, attack);

    internal static int ResolveNpcEnergyAfterResistedHitSpell(
        int currentEnergyBeforeAttack, RuntimeNpcAttackDefinition attack, int energyPerRound)
    {
        if (energyPerRound < 0) throw new ArgumentOutOfRangeException(nameof(energyPerRound));
        return RuntimeClassicNpcCombatCadence.RefundRejectedSpellCost(currentEnergyBeforeAttack, attack);
    }

    internal static RuntimeNpcHitSpellSelection? ResolveNpcHitSpell(
        RuntimeNpcAttackDefinition attack, ClassicAttackResult result, string projectRoot) =>
        ResolveNpcHitSpell(attack, result, projectRoot, "Classic");

    internal static RuntimeNpcHitSpellSelection? ResolveNpcHitSpell(
        RuntimeNpcAttackDefinition attack, ClassicAttackResult result, string projectRoot, string worldId)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(result);
        var spellLegacyId = attack.EffectiveSpellLegacyId;
        if (spellLegacyId <= 0) return null;
        var triggered = result.Outcome is ClassicHitOutcome.Hit or ClassicHitOutcome.Critical;
        if (!triggered) return new RuntimeNpcHitSpellSelection(
            spellLegacyId, string.Empty, string.Empty, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            ClassifyNpcHitSpell(0, 0, 0, 0), ResolveNpcHitSpellEffectRange(0, 0));

        var catalogPath = Path.Combine(ServerDataRootResolver.Resolve(projectRoot, worldId).DataRoot, "combat_spells.json");
        var catalog = RuntimeCanonicalCombatSpellCatalogLoader.Load(catalogPath);
        var canonicalId = $"spell.{spellLegacyId}";
        var spell = RuntimeCanonicalCombatSpellCatalogLoader.RequireSpell(catalog, canonicalId);
        if (!spell.LegacyId.Equals(spellLegacyId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"NPC_HIT_SPELL_LEGACY_ID_MISMATCH:{spellLegacyId}:{spell.LegacyId}");
        var effectiveSpellLevel = attack.Type == 2 ? attack.MaximumDamage : spell.SpellLevel;
        return new RuntimeNpcHitSpellSelection(
            spellLegacyId, spell.Id, spell.Name, true, effectiveSpellLevel,
            spell.SpellTypeRaw, spell.TargetTypeRaw, spell.AttackTypeRaw,
            spell.MinimumEffect, spell.MaximumEffect, spell.DurationRaw,
            spell.ResistTypeRaw, spell.ResistAbilityRaw, spell.DifficultyRaw,
            ClassifyNpcHitSpell(spell.SpellTypeRaw, spell.TargetTypeRaw, spell.AttackTypeRaw, spell.ResistTypeRaw),
            ResolveNpcHitSpellEffectRange(spell.MinimumEffect, spell.MaximumEffect));
    }

    // The native _get_room_targets (0x425F65..0x4260A5) admits room
    // sessions in the original enumeration order: immunity first, then
    // conditional MR rolls. _monster_cast_area (0x426472..0x426599) draws
    // a single base magnitude only when this prepass accepted >=1 target.
    // The caller must provide same-room, living player sessions in the native
    // enumeration order. The exact live session-to-native index mapping remains
    // unverified, so this result is not yet wired into live mutation.
    internal static RuntimeClassicNpcAreaRollPlan ResolveClassicNpcAreaRollPlan(
        int resistanceTypeRaw, int effectMinimum, int effectMaximum,
        IReadOnlyList<RuntimeClassicNpcAreaTargetSnapshot> targets, Random random)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(random);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<RuntimeClassicNpcAreaTargetAdmission>(targets.Count);
        var eligibleCount = 0;
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.SessionId) || !seen.Add(target.SessionId))
                throw new InvalidDataException("NPC_NATIVE_AREA_TARGET_ID_INVALID_OR_DUPLICATE");
            if (target.SpellImmune)
            {
                results.Add(new(target.SessionId, false, true, 0));
                continue;
            }
            var resistance = ResolveNpcHitSpellResistanceWithAdmission(
                resistanceTypeRaw, target.Willpower, target.Intellect,
                target.MagicResistanceBonus, target.AntiMagicAbility, random);
            var admitted = !resistance.Resisted;
            if (admitted) eligibleCount++;
            results.Add(new(target.SessionId, admitted, false, resistance.Roll));
        }
        var magnitude = eligibleCount > 0
            ? ResolveNpcStatusEffectMagnitude(effectMinimum, effectMaximum,
                null, random, classicWorld: true)
            : (int?)null;
        return new(results, eligibleCount, magnitude);
    }

    // WCCMMUD _monster_cast at 0x427ebb uses _genrdn(1,100) only when
    // ResistType +0xC6 admits a roll. WGSERVER _genrdn uses an exclusive
    // upper bound, so eligible native resistance rolls are 1..99.
    // The original _attack_monster_user calls _monster_cast for the type-2
    // cast row (0x42E6A8) and for a physical row's HitSpell (0x42EE69);
    // this native admission rule applies to both entry points.
    internal static RuntimeNpcHitSpellResistanceResolution ResolveNpcHitSpellResistanceWithAdmission(
        int resistanceTypeRaw, int willpower, int intellect, int magicResistanceBonus,
        int antiMagicAbility, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (resistanceTypeRaw < 0 || willpower < 0 || intellect < 0 || antiMagicAbility < 0)
            throw new InvalidDataException("NPC_HIT_SPELL_NEGATIVE_RESISTANCE_INPUT");
        var eligible = resistanceTypeRaw == 2 || resistanceTypeRaw == 1 && antiMagicAbility > 0;
        var roll = eligible ? random.Next(1, 100) : 0;
        return ResolveNpcHitSpellResistance(
            resistanceTypeRaw, willpower, intellect, magicResistanceBonus, antiMagicAbility, roll);
    }

    internal static RuntimeNpcHitSpellResistanceResolution ResolveNpcHitSpellResistance(
        int resistanceTypeRaw, int willpower, int intellect, int magicResistanceBonus, int antiMagicAbility, int roll)
    {
        if (resistanceTypeRaw < 0) throw new InvalidDataException("NPC_HIT_SPELL_NEGATIVE_RESISTANCE_TYPE");
        if (willpower < 0 || intellect < 0 || antiMagicAbility < 0)
            throw new InvalidDataException("NPC_HIT_SPELL_NEGATIVE_RESISTANCE_INPUT");
        if (roll is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(roll));

        var antiMagicRequired = resistanceTypeRaw == 1;
        var checkEligible = resistanceTypeRaw switch
        {
            0 => false,
            1 => antiMagicAbility > 0,
            2 => true,
            _ => false
        };
        if (checkEligible && roll == 0)
            throw new ArgumentOutOfRangeException(nameof(roll));
        var score = RuntimeClassicCharacterFormulaCalculator.CalculateMagicResistance(intellect, willpower, magicResistanceBonus);
        var chance = checkEligible ? Math.Clamp(score / 2, 0, 97) : 0;
        return new RuntimeNpcHitSpellResistanceResolution(
            checkEligible, antiMagicRequired, willpower, intellect, magicResistanceBonus,
            score, chance, roll, checkEligible && roll <= chance);
    }

    private static int ReadBaseStat(RuntimeCharacterRecord character, params string[] keys)
    {
        foreach (var key in keys)
            if (character.BaseStats.TryGetValue(key, out var value))
            {
                var canonical = key.ToLowerInvariant() switch
                {
                    "int" or "intelligence" => "intellect",
                    "wil" or "will" => "wisdom",
                    "agi" or "agl" => "agility",
                    "str" => "strength",
                    "cha" => "charm",
                    _ => key.ToLowerInvariant()
                };
                return Math.Max(0, checked(value + RuntimeClassicStatusProjection.Numeric(character, "stat_" + canonical)));
            }
        return 0;
    }

    private static int ReadMagicResistance(RuntimeCharacterRecord character)
        => RuntimeClassicCharacterFormulaCalculator.ResolveMagicResistance(character);

    private static int ReadDerivedStat(RuntimeCharacterRecord character, string key)
        => character.SemanticState?.DerivedStats.TryGetValue(key, out var value) == true ? value : 0;

    private static int ReadAbilityValue(RuntimeCharacterRecord character, int abilityId)
    {
        if (abilityId == 51 && character.SemanticState?.DerivedStats.TryGetValue("anti_magic_rank", out var equipmentRank) == true)
            return Math.Max(0, equipmentRank);
        foreach (var key in new[] { $"quest.ability.{abilityId}", $"ability.{abilityId}" })
            if (character.Conditions.TryGetValue(key, out var raw) && int.TryParse(raw, out var value))
                return Math.Max(0, value);
        return 0;
    }

    internal static RuntimeDecodedSpellEffectRange ResolveNpcHitSpellEffectRange(int authoredFirstEndpoint, int authoredSecondEndpoint)
    {
        var minimum = Math.Min(authoredFirstEndpoint, authoredSecondEndpoint);
        var maximum = Math.Max(authoredFirstEndpoint, authoredSecondEndpoint);
        var sign = minimum == 0 && maximum == 0
            ? RuntimeSpellEffectSign.Zero
            : minimum >= 0
                ? RuntimeSpellEffectSign.Positive
                : maximum <= 0
                    ? RuntimeSpellEffectSign.Negative
                    : RuntimeSpellEffectSign.Mixed;
        return new RuntimeDecodedSpellEffectRange(
            authoredFirstEndpoint,
            authoredSecondEndpoint,
            minimum,
            maximum,
            sign,
            authoredFirstEndpoint > authoredSecondEndpoint);
    }

    internal static RuntimeDecodedSpellClassification ClassifyNpcHitSpell(
        int spellTypeRaw, int targetTypeRaw, int attackTypeRaw, int resistanceTypeRaw)
    {
        if (spellTypeRaw < 0 || targetTypeRaw < 0 || attackTypeRaw < 0 || resistanceTypeRaw < 0)
            throw new InvalidDataException("NPC_HIT_SPELL_NEGATIVE_CLASSIFICATION");

        return new RuntimeDecodedSpellClassification(
            ResolveNpcHitSpellTypeMeaning(spellTypeRaw),
            ResolveNpcHitSpellTargetScope(targetTypeRaw),
            ResolveNpcHitSpellAttackElement(attackTypeRaw),
            ResolveNpcHitSpellResistanceScope(resistanceTypeRaw));
    }


    internal static string ResolveNpcHitSpellTypeMeaning(int spellTypeRaw) => spellTypeRaw switch
    {
        0 => "immediate-effect",
        1 => "poison-delivery",
        3 => "status-or-scripted-effect",
        _ => $"spell-type-{spellTypeRaw}"
    };

    // WCCMMUD _monster_cast dispatches ability IDs through the 0x4282CA
    // table: direct HP handlers 1 and 17 differ from poison 19 and confusion
    // 71. Ability 8 drains the victim and heals the casting monster; the
    // area variant repeats that mutation per admitted recipient.
    // Native _monster_cast_area ability-17 handler (0x426FCE-0x42707C)
    // applies elemental resistance before this MR adjustment. If ability 51
    // (Anti-Magic) is present, reduction = min(MR/2,75) percent. Otherwise
    // positive reduction = min((MR-50)/2,50), and a zero reduction falls
    // through the original (50-MR)% bonus/penalty branch. Keep this distinct
    // from the player-cast Damage(-MR) handler at 0x474E3D.
    internal static int ResolveClassicNpcAreaDamageMinusMr(
        int signedMagnitudeAfterElement, int targetMagicResistance, bool targetHasAntiMagic)
    {
        var mr = Math.Clamp(targetMagicResistance, 0, ushort.MaxValue);
        if (targetHasAntiMagic)
        {
            var percent = Math.Min(mr / 2, 75);
            return checked((int)((long)signedMagnitudeAfterElement
                - ((long)signedMagnitudeAfterElement * percent) / 100));
        }
        var reduction = Math.Min(Math.Max(0, (mr - 50) / 2), 50);
        if (reduction > 0)
            return checked((int)((long)signedMagnitudeAfterElement
                - ((long)signedMagnitudeAfterElement * reduction) / 100));
        return checked((int)((long)signedMagnitudeAfterElement
            + ((long)signedMagnitudeAfterElement * (50 - mr)) / 100));
    }

    internal static int ResolveClassicNpcDrainHitPointsAfter(
        int currentHitPoints, int maximumHitPoints, int rawDrainMagnitude)
    {
        if (maximumHitPoints <= 0 || currentHitPoints < 0 || currentHitPoints > maximumHitPoints
            || rawDrainMagnitude < 0)
            throw new InvalidDataException("NPC_NATIVE_DRAIN_HIT_POINT_INPUT_INVALID");
        return (int)Math.Min((long)maximumHitPoints,
            (long)currentHitPoints + rawDrainMagnitude);
    }

    internal static bool ClassicNpcHasDrainHandler(
        RuntimeClassicLiveStatusDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Abilities.Any(ability => ability.AbilityId == 8);
    }

    internal static bool ClassicNpcHasDirectHitPointHandler(
        RuntimeClassicLiveStatusDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Abilities.Any(ability => ability.AbilityId is 1 or 8 or 17);
    }

    // Original _monster_cast (0x42828B..0x4282A9) restores the single base
    // magnitude when a slot's signed Int16 value at +0xA8 is zero, otherwise
    // it uses that slot's own value. The ability ID comes from +0xD8.
    // Preserve repeated IDs and the relative order of populated slots.
    // The existing timed-status store is keyed by ability ID. This is
    // intentionally a *stat-only projection*, not the native ordered
    // dispatcher. Duplicate stat IDs retain the last populated value here;
    // exact native stacking remains a separate parity boundary. Repeated
    // scripted/removal slots are preserved in ResolveClassicNpcEffectSlots.
    // Summon(12) in _monster_cast (0x428BD9) admits target types 0, 2,
    // 6 and 8 (verified via 0x428BD9..0x428BF7). It does not admit type 1;
    // self-target summons instead use _monster_cast_area's separate
    // summon handler at 0x427641..0x4276C0 (targets 1, 2, 6).
    // TextBlock(148) dispatches the special-command interpreter
    // at 0x428445 for every authored slot. The native effect magnitude is
    // substituted only for slots whose raw value is zero; repeated IDs are
    // never deduplicated. This boundary deliberately does not perform the
    // unverified ownership, lifetime or transactional script mutations.
    internal static IReadOnlyList<RuntimeClassicNativeSpecialSpellSlot> ResolveClassicNativeSpecialSpellSlots(
        IReadOnlyList<RuntimeClassicNpcEffectSlot> orderedSlots, int nativeTargetTypeRaw,
        bool nativeAreaDispatcher = false)
    {
        ArgumentNullException.ThrowIfNull(orderedSlots);
        if (orderedSlots.Count > 10)
            throw new InvalidDataException("CLASSIC_NATIVE_SPECIAL_SLOTS_EXCEED_TEN");
        return orderedSlots
            .Where(slot => slot.AbilityId is 12 or 148)
            .Select(slot => new RuntimeClassicNativeSpecialSpellSlot(
                slot.PopulatedOrdinal, slot.AbilityId, slot.RawValue, slot.EffectiveValue,
                slot.AbilityId == 148 || (nativeAreaDispatcher
                    ? nativeTargetTypeRaw is 1 or 2 or 6
                    : nativeTargetTypeRaw is 0 or 2 or 6 or 8),
                slot.EffectiveValue > 0))
            .ToArray();
    }

    internal static Dictionary<int, int> ResolveClassicNpcTimedModifierProjection(
        IReadOnlyList<RuntimeClassicNpcEffectSlot> orderedSlots)
    {
        ArgumentNullException.ThrowIfNull(orderedSlots);
        return orderedSlots
            .Where(slot => slot.EffectiveValue != 0
                && RuntimeClassicLiveStatusDefinition.IsCombatModifierAbility(slot.AbilityId))
            .GroupBy(slot => slot.AbilityId)
            .ToDictionary(group => group.Key, group => group.Last().EffectiveValue);
    }

    internal static IReadOnlyList<RuntimeClassicNpcEffectSlot> ResolveClassicNpcEffectSlots(
        IReadOnlyList<RuntimeClassicLiveStatusAbility> abilities, int baseMagnitude)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        if (abilities.Count > 10)
            throw new InvalidDataException($"CLASSIC_NPC_NATIVE_ABILITY_SLOTS_EXCEEDED:{abilities.Count}");
        return abilities.Select((ability, index) => new RuntimeClassicNpcEffectSlot(
            index, ability.AbilityId, ability.Value,
            ability.Value == 0 ? baseMagnitude : ability.Value)).ToArray();
    }

    // WCCMMUD _monster_cast_area first obtains the count of room occupants
    // admitted by _get_room_targets (0x425F65..0x4260A5), including its native
    // spell-immunity and resist rolls. Only after that count is positive does
    // it draw one base magnitude at 0x42658E. The native definition-type
    // field at +0xCC is target_type (NOT spell_type at +0xC4). Its
    // jump table at 0x4265B3 divides the shared magnitude by target count for
    // all types except 11, 12 and 13, which retain the full raw magnitude.
    // The two-phase Classic live area path now calls this after its admission
    // prepass. Native session-index order and the remaining effect handlers
    // are still separate parity boundaries.
    internal static int ResolveClassicNpcAreaPerTargetBaseMagnitude(
        int sharedMagnitude, int admittedTargetCount, int nativeTargetTypeAtCc)
    {
        if (admittedTargetCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(admittedTargetCount));
        return nativeTargetTypeAtCc is 11 or 12 or 13
            ? sharedMagnitude
            : sharedMagnitude / admittedTargetCount;
    }

    internal static RuntimeNpcHitSpellEffectResolution ResolveNpcHitSpellEffect(
        RuntimeNpcHitSpellSelection spell, int elementalResistancePercent, bool poisonImmune, Random random,
        int? nativePreRolledMagnitude = null, bool nativeAreaMagnitudeScaled = false,
        bool? classicNativeHasDirectHitPointHandler = null,
        bool classicNativeDrainAbility8 = false,
        bool classicNativeAreaDamageMinusMr17 = false,
        bool classicNativeAreaDamageAbility1 = false,
        bool classicNativeSingleDamageAbility1 = false,
        int nativeTargetMagicResistance = 0,
        bool nativeTargetHasAntiMagic = false,
        bool classicNativeAttackElement = false,
        int? classicNativeDirectSlotRaw = null)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(random);
        var type = ResolveNpcHitSpellTypeMeaning(spell.SpellTypeRaw);
        var scope = ResolveNpcHitSpellTargetScope(spell.TargetTypeRaw);
        var element = ResolveNpcHitSpellAttackElement(spell.AttackTypeRaw, classicNativeAttackElement);
        var positiveRange = spell.EffectRange.Sign == RuntimeSpellEffectSign.Positive && spell.EffectRange.RollMaximum > 0;
        // In native Classic, the HP slot may carry a signed nonzero override
        // even when the shared base magnitude is zero or negative. Custom
        // worlds retain their previous strictly-positive direct-effect gate.
        var classicSignedRange = classicNativeDirectSlotRaw.HasValue
            && (classicNativeDirectSlotRaw.Value != 0
                || spell.EffectRange.RollMinimum != 0
                || spell.EffectRange.RollMaximum != 0);
        var hostileScope = spell.TargetTypeRaw is 0 or 8 or 11 or 12;
        // _monster_cast ability8's +0xCC gate (0x428A6D) admits single
        // target types 1/2/6/8, not contact target 0; the area handler
        // separately admits types 11/12. Avoid inventing a Classic
        // contact-attached Drain for a spell such as Draws the Breath (82).
        var nativeDrainTargetValid = !classicNativeDrainAbility8 || spell.TargetTypeRaw != 0;
        // Ability 8 is an independent native HP handler even for spell
        // type 3 (Shadow Breath / 1011). Do not generalize the exception
        // to unrelated status handlers or to the Custom-world resolver.
        var executable = (spell.SpellTypeRaw == 0 || classicNativeDrainAbility8
                || classicNativeAreaDamageMinusMr17 || classicNativeAreaDamageAbility1
                || classicNativeSingleDamageAbility1)
            && hostileScope && (positiveRange || classicSignedRange) && nativeDrainTargetValid
            && classicNativeHasDirectHitPointHandler != false;
        if (!executable)
        {
            var reason = spell.SpellTypeRaw != 0
                && !classicNativeDrainAbility8 && !classicNativeAreaDamageMinusMr17
                && !classicNativeAreaDamageAbility1
                && !classicNativeSingleDamageAbility1
                ? "non-immediate-spell-type"
                : !hostileScope ? "non-hostile-or-scripted-target-scope"
                : !nativeDrainTargetValid ? "native-drain-target-type-ineligible"
                : classicNativeHasDirectHitPointHandler == false ? "no-native-direct-hit-point-handler"
                : "non-positive-effect-range";
            return new RuntimeNpcHitSpellEffectResolution(false, type, scope, element,
                spell.EffectRange.RollMinimum, spell.EffectRange.RollMaximum, 0, 0, elementalResistancePercent, poisonImmune, 0, reason);
        }
        // WCCMMUD _monster_cast 0x428218..0x42822f consumes an effect
        // range draw before dispatching the ordered +0xD8 ability slots.
        // Do not skip the draw for equal authored range endpoints.
        // Classic area spells receive their magnitude from _monster_cast_area
        // after the native target-admission prepass. It may be smaller than the
        // authored range because the admitted-target divisor has already run.
        if (nativePreRolledMagnitude is int supplied && !nativeAreaMagnitudeScaled
            && (supplied < spell.EffectRange.RollMinimum || supplied > spell.EffectRange.RollMaximum))
            throw new InvalidDataException("NPC_NATIVE_EFFECT_MAGNITUDE_OUTSIDE_AUTHORED_RANGE");
        var roll = nativePreRolledMagnitude
            ?? random.Next(spell.EffectRange.RollMinimum, checked(spell.EffectRange.RollMaximum + 1));
        // In both native area HP handlers 1 and 17, +0xC4 spell type
        // >= 3 bypasses the element-resistance switch (0x426B9B and
        // 0x426E76). It must not turn type-3 Acid Rain or Demon Explosion
        // into elemental immunity or over-resistance healing.
        var signedEffectiveMagnitude = classicNativeDirectSlotRaw is int authoredValue
            && authoredValue != 0 ? authoredValue : roll;
        var skipElementalForNativeAreaStatus = spell.SpellTypeRaw >= 3
            && (classicNativeAreaDamageAbility1 || classicNativeAreaDamageMinusMr17
                || classicNativeSingleDamageAbility1 || classicNativeDrainAbility8);
        var elementalDamage = skipElementalForNativeAreaStatus
            ? signedEffectiveMagnitude
            : classicNativeDirectSlotRaw.HasValue
                ? ResolveSignedClassicSpellMagnitude(
                    signedEffectiveMagnitude, element, elementalResistancePercent, poisonImmune)
                : ResolveElementalSpellDamage(roll, element, elementalResistancePercent, poisonImmune);
        var finalDamage = classicNativeAreaDamageMinusMr17
            ? ResolveClassicNpcAreaDamageMinusMr(
                elementalDamage, nativeTargetMagicResistance, nativeTargetHasAntiMagic)
            : elementalDamage;
        return new RuntimeNpcHitSpellEffectResolution(true, type, scope, element,
            spell.EffectRange.RollMinimum, spell.EffectRange.RollMaximum,
            roll, signedEffectiveMagnitude, elementalResistancePercent, poisonImmune, finalDamage,
            finalDamage == 0 && (poisonImmune || elementalResistancePercent == 100) ? "elemental-immunity" : string.Empty);
    }

    internal static RuntimeNpcHitSpellEffectResolution ResolveNpcHitSpellEffectForAudit(
        RuntimeNpcHitSpellSelection spell, int roll, int elementalResistancePercent = 0, bool poisonImmune = false)
    {
        ArgumentNullException.ThrowIfNull(spell);
        if (roll < spell.EffectRange.RollMinimum || roll > spell.EffectRange.RollMaximum)
            throw new ArgumentOutOfRangeException(nameof(roll));
        var type = ResolveNpcHitSpellTypeMeaning(spell.SpellTypeRaw);
        var scope = ResolveNpcHitSpellTargetScope(spell.TargetTypeRaw);
        var element = ResolveNpcHitSpellAttackElement(spell.AttackTypeRaw);
        var executable = spell.SpellTypeRaw == 0 && spell.TargetTypeRaw is 0 or 8 or 11 or 12
            && spell.EffectRange.Sign == RuntimeSpellEffectSign.Positive && spell.EffectRange.RollMaximum > 0;
        var finalDamage = executable ? ResolveElementalSpellDamage(roll, element, elementalResistancePercent, poisonImmune) : 0;
        return new RuntimeNpcHitSpellEffectResolution(executable, type, scope, element,
            spell.EffectRange.RollMinimum, spell.EffectRange.RollMaximum, executable ? roll : 0, executable ? roll : 0,
            elementalResistancePercent, poisonImmune, finalDamage, executable ? string.Empty : "quarantined");
    }

    internal static int ResolveElementalSpellDamage(int rawDamage, string attackElement, int elementalResistancePercent, bool poisonImmune)
    {
        if (rawDamage < 0) throw new ArgumentOutOfRangeException(nameof(rawDamage));
        ArgumentException.ThrowIfNullOrWhiteSpace(attackElement);
        if (attackElement.Equals("poison", StringComparison.OrdinalIgnoreCase) && poisonImmune) return 0;
        if (attackElement is "untyped" or "acid") return rawDamage;

        // Classic MMUD Explorer evidence preserves the signed elemental-resistance formula:
        // Fix(((100 - nBonusResist) * nDamage) / 100). Exactly 100% yields zero damage;
        // values above 100% yield negative damage, which the live consumer applies as healing.
        var multiplierPercent = checked(100 - elementalResistancePercent);
        return checked(rawDamage * multiplierPercent) / 100;
    }

    internal static RuntimeElementalHitPointResolution ApplyElementalSpellHitPoints(
        int currentHitPoints, int maximumHitPoints, int signedDamage)
    {
        if (maximumHitPoints < 1) throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));
        if (currentHitPoints < 0 || currentHitPoints > maximumHitPoints)
            throw new ArgumentOutOfRangeException(nameof(currentHitPoints));

        if (signedDamage > 0)
        {
            var after = Math.Max(0, currentHitPoints - signedDamage);
            return new RuntimeElementalHitPointResolution(
                currentHitPoints, maximumHitPoints, signedDamage, after, currentHitPoints - after, 0);
        }
        if (signedDamage < 0)
        {
            var requestedHealing = -(long)signedDamage;
            var after = (int)Math.Min(maximumHitPoints, (long)currentHitPoints + requestedHealing);
            return new RuntimeElementalHitPointResolution(
                currentHitPoints, maximumHitPoints, signedDamage, after, 0, after - currentHitPoints);
        }
        return new RuntimeElementalHitPointResolution(
            currentHitPoints, maximumHitPoints, signedDamage, currentHitPoints, 0, 0);
    }

    internal static RuntimeNpcHitSpellStatusDeliveryResolution ResolveNpcHitSpellStatusDelivery(
        RuntimeNpcHitSpellSelection spell, bool poisonImmune, bool duplicateActiveEffect,
        bool hasDecodedDurationModifiers = false, bool classicNativePoisonAbility19 = false)
    {
        ArgumentNullException.ThrowIfNull(spell);
        // Native _monster_cast ability 19 is dispatched by its own effect ID.
        // Spell type 0 can therefore carry poison without an HP-damage slot
        // or other combat-stat modifiers (e.g. Bee Poison / spell.365).
        var immediatePoison = spell.SpellTypeRaw == 0 && classicNativePoisonAbility19;
        var kind = spell.SpellTypeRaw == 1 ? "poison" : spell.SpellTypeRaw == 3 ? "status-or-scripted"
            : spell.SpellTypeRaw == 0 && hasDecodedDurationModifiers ? "immediate-with-duration-modifiers"
            : immediatePoison ? "immediate-with-poison" : "unsupported";
        var hostileScope = spell.TargetTypeRaw is 0 or 8 or 11 or 12;
        var eligibleType = spell.SpellTypeRaw is 1 or 3
            || spell.SpellTypeRaw == 0 && (hasDecodedDurationModifiers || immediatePoison);
        if (!eligibleType) return new(false, false, false, kind, spell.CanonicalSpellId, spell.DurationRaw, spell.MinimumEffect, spell.MaximumEffect, "not-status-delivery-type");
        if (!hostileScope) return new(false, false, false, kind, spell.CanonicalSpellId, spell.DurationRaw, spell.MinimumEffect, spell.MaximumEffect, "non-hostile-or-scripted-target-scope");
        if (spell.DurationRaw <= 0) return new(false, false, false, kind, spell.CanonicalSpellId, spell.DurationRaw, spell.MinimumEffect, spell.MaximumEffect, "non-positive-duration");
        if (spell.SpellTypeRaw == 1 && poisonImmune) return new(true, false, true, kind, spell.CanonicalSpellId, spell.DurationRaw, spell.MinimumEffect, spell.MaximumEffect, "poison-immunity");
        if (duplicateActiveEffect) return new(true, true, false, kind, spell.CanonicalSpellId, spell.DurationRaw, spell.MinimumEffect, spell.MaximumEffect, "refresh-existing-effect");
        return new(true, true, false, kind, spell.CanonicalSpellId, spell.DurationRaw, spell.MinimumEffect, spell.MaximumEffect, "independent-new-effect");
    }

    private static int ReadElementalResistance(RuntimeCharacterRecord character, string attackElement)
    {
        if (attackElement is "untyped" or "acid" or "poison") return 0;
        var values = character.SemanticState?.Resistances;
        var baseValue = values is not null && values.TryGetValue(attackElement, out var value) ? value : 0;
        var statusKey = attackElement switch
        {
            "cold" => "resist_cold",
            "fire" => "resist_fire",
            "stone" => "resist_stone",
            "lightning" => "resist_lightning",
            "water" => "resist_water",
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(statusKey) ? baseValue : checked(baseValue + RuntimeClassicStatusProjection.Numeric(character, statusKey));
    }

    internal static string ResolveNpcHitSpellTargetScope(int targetTypeRaw) => targetTypeRaw switch
    {
        0 => "contact-attached-target",
        1 => "self-target",
        8 => "single-hostile-target",
        11 => "full-area-target",
        12 => "area-hostile-target",
        _ => $"target-type-{targetTypeRaw}"
    };

    // Both native _monster_cast and _monster_cast_area use ability 147
    // (Water Resistance) for attack element 5. Keep the Custom descriptor
    // "acid" unchanged: it was a preexisting Custom-world convention.
    internal static string ResolveNpcHitSpellAttackElement(
        int attackTypeRaw, bool classicNativeElement = false) => attackTypeRaw switch
    {
        0 => "cold",
        1 => "fire",
        2 => "stone",
        3 => "lightning",
        4 => "untyped",
        5 => classicNativeElement ? "water" : "acid",
        6 => "poison",
        _ => $"attack-type-{attackTypeRaw}"
    };

    internal static string ResolveNpcHitSpellResistanceScope(int resistanceTypeRaw) => resistanceTypeRaw switch
    {
        0 => "never-resisted",
        1 => "anti-magic-only",
        2 => "magic-resistance-eligible",
        _ => $"resistance-type-{resistanceTypeRaw}"
    };

    private static ClassicAttackType ResolveAttackType(int generatedType)
    {
        if (!Enum.IsDefined(typeof(ClassicAttackType), generatedType))
            throw new InvalidOperationException($"NPC_COMBAT_ATTACK_TYPE_UNSUPPORTED:{generatedType}");
        return (ClassicAttackType)generatedType;
    }

    private static string RenderAttack(string attacker, string defender, ClassicAttackResult result) =>
        RuntimeClassicCombatAnsi.PlayerAttack(RenderAttackPlain(attacker, defender, result), result.Outcome);

    private static string RenderAttackPlain(string attacker, string defender, ClassicAttackResult result) => result.Outcome switch
    {
        ClassicHitOutcome.Miss => $"{attacker} misses {defender}.",
        ClassicHitOutcome.Dodged => $"{defender} dodges {attacker}'s attack.",
        ClassicHitOutcome.Glance => $"{attacker}'s attack glances off {defender} for no damage.",
        ClassicHitOutcome.Critical => $"{attacker} critically hits {defender} for {result.FinalDamage} damage.",
        _ => $"{attacker} hits {defender} for {result.FinalDamage} damage."
    };

    internal static RuntimeNpcCombatMessageSelection ResolveNpcCombatMessage(
        RuntimeNpcAttackDefinition attack, ClassicAttackResult result, string attacker, string defender)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(result);
        var legacyMessageId = result.Outcome switch
        {
            ClassicHitOutcome.Miss => attack.MissMessageLegacyId,
            ClassicHitOutcome.Dodged => attack.DodgeMessageLegacyId,
            _ => attack.HitMessageLegacyId
        };
        return new RuntimeNpcCombatMessageSelection(result.Outcome, Math.Max(0, legacyMessageId),
            RenderAttackPlain(attacker, defender, result));
    }

    private string RenderNpcAttack(string worldId, string attacker, string defender, RuntimeNpcAttackDefinition attack, ClassicAttackResult result)
    {
        var attackArguments = NpcAttackArguments(worldId);
        var nativePayloads = NativePayloads(worldId);
        var npcSpellCatalog = NpcSpellCatalog(worldId);
        attack = attackArguments.Complete(attacker, attack);
        var selection = ResolveNpcCombatMessage(attack, result, attacker, defender);
        var rendered = TryRenderNativeNpcCombatMessage(nativePayloads, selection, attacker, defender, result.FinalDamage, out var native)
            ? native
            : TryRenderNativeNpcSpellMessage(nativePayloads, npcSpellCatalog, selection, attack, attacker, result.FinalDamage, out native)
                ? native
            : TryRenderNativeNpcCombatMessageFromAttack(nativePayloads, selection, attack, attacker, defender, out native)
                ? native
            : selection.SafeFallback;
        return rendered.Length == 0 ? string.Empty : RuntimeClassicCombatAnsi.NpcAttack(rendered, result.Outcome);
    }

    internal static bool TryRenderNativeNpcSpellMessage(
        RuntimeClassicNativeTextblockPayloadAuthority authority,
        RuntimeSpellCatalog spells,
        RuntimeNpcCombatMessageSelection selection,
        RuntimeNpcAttackDefinition attack,
        string attacker,
        int damage,
        out string rendered)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(attack);
        if (attack.Type != 2 || attack.Accuracy <= 0 ||
            !authority.TryResolveMessage(selection.LegacyMessageId, out var playerTemplate))
        {
            rendered = string.Empty;
            return false;
        }

        var spell = spells.Require($"spell.{attack.Accuracy}");
        var template = playerTemplate;
        if (playerTemplate.StartsWith("You cast ", StringComparison.OrdinalIgnoreCase) ||
            playerTemplate.StartsWith("You sing ", StringComparison.OrdinalIgnoreCase))
        {
            if (!authority.TryResolveSecondaryMessage(selection.LegacyMessageId, out template) || string.IsNullOrWhiteSpace(template))
            {
                rendered = string.Empty;
                return false;
            }
        }

        var strings = CountToken(template, "%s");
        var numbers = CountToken(template, "%d");
        if (strings == 1 && numbers == 0)
        {
            rendered = ReplaceFirst(template, "%s", attacker);
            return true;
        }
        if (strings == 2 && numbers is 0 or 1)
        {
            rendered = ReplaceFirst(ReplaceFirst(template, "%s", attacker), "%s", spell.Name);
            if (numbers == 1) rendered = ReplaceFirst(rendered, "%d", damage.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        rendered = string.Empty;
        return false;
    }

    internal static bool TryRenderNativeNpcCombatMessageFromAttack(
        RuntimeClassicNativeTextblockPayloadAuthority authority,
        RuntimeNpcCombatMessageSelection selection,
        RuntimeNpcAttackDefinition attack,
        string attacker,
        string defender,
        out string rendered)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(attack);
        if (!authority.TryResolveMessage(selection.LegacyMessageId, out var template) ||
            !authority.TryResolveMessage(attack.HitMessageLegacyId, out var hitTemplate))
        {
            rendered = string.Empty;
            return false;
        }

        var stringTokens = CountToken(template, "%s");
        if (stringTokens == 5 &&
            selection.LegacyMessageId == attack.MissMessageLegacyId &&
            attack.HasExactNativeMissArguments)
        {
            rendered = template;
            foreach (var argument in new[] { attacker, attack.MissVerb, defender, attack.WeaponName, defender })
                rendered = ReplaceFirst(rendered, "%s", argument);
            return !rendered.Contains("%s", StringComparison.Ordinal);
        }

        if (!TryExtractNativeAttackPhrase(hitTemplate, out var attackPhrase))
        {
            rendered = string.Empty;
            return false;
        }

        if (stringTokens == 2 && template.Contains("you, but your armor", StringComparison.OrdinalIgnoreCase) ||
            stringTokens == 2 && template.Contains("you, but your armour", StringComparison.OrdinalIgnoreCase))
        {
            rendered = ReplaceFirst(ReplaceFirst(template, "%s", attacker), "%s", attackPhrase);
            return true;
        }

        if (stringTokens == 3 && template.EndsWith(", but the attack is dodged!", StringComparison.OrdinalIgnoreCase))
        {
            rendered = ReplaceFirst(ReplaceFirst(ReplaceFirst(template, "%s", attacker), "%s", attackPhrase), "%s", defender);
            return true;
        }

        rendered = string.Empty;
        return false;
    }

    private static bool TryExtractNativeAttackPhrase(string hitTemplate, out string attackPhrase)
    {
        var attackerToken = hitTemplate.IndexOf("%s", StringComparison.Ordinal);
        if (attackerToken < 0 || CountToken(hitTemplate, "%s") != 1)
        {
            attackPhrase = string.Empty;
            return false;
        }

        var phraseStart = attackerToken + 2;
        while (phraseStart < hitTemplate.Length && char.IsWhiteSpace(hitTemplate[phraseStart])) phraseStart++;
        var target = hitTemplate.IndexOf(" you", phraseStart, StringComparison.OrdinalIgnoreCase);
        if (target <= phraseStart)
        {
            attackPhrase = string.Empty;
            return false;
        }

        attackPhrase = hitTemplate[phraseStart..target].Trim();
        return attackPhrase.Length > 0 && !attackPhrase.Contains('%');
    }

    internal static bool TryRenderNativeNpcCombatMessage(
        RuntimeClassicNativeTextblockPayloadAuthority authority,
        RuntimeNpcCombatMessageSelection selection,
        string attacker,
        string defender,
        int damage,
        out string rendered)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.LegacyMessageId <= 0)
        {
            rendered = string.Empty;
            return false;
        }
        if (authority.IsVerifiedNoOutputMessage(selection.LegacyMessageId))
        {
            rendered = string.Empty;
            return true;
        }
        if (!authority.TryResolveMessage(selection.LegacyMessageId, out var template) || string.IsNullOrWhiteSpace(template))
        {
            rendered = string.Empty;
            return false;
        }

        // Proven target-facing native forms have either one attacker token or one attacker
        // plus one numeric damage token. Other WCCMSG2 forms require color/pronoun/observer
        // arguments owned by the original call site; retain the safe fallback for those
        // rather than guessing argument order.
        if (CountToken(template, "%s") == 1 && CountToken(template, "%d") == 1 && CountToken(template, "%") == 2)
        {
            rendered = ReplaceFirst(ReplaceFirst(template, "%s", attacker), "%d", damage.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }
        if (CountToken(template, "%s") == 1 && CountToken(template, "%d") == 0 && CountToken(template, "%") == 1)
        {
            rendered = ReplaceFirst(template, "%s", attacker);
            return true;
        }

        if (CountToken(template, "%d") == 0 && TryResolveComplexStringArguments(template, attacker, defender, out rendered))
            return true;

        rendered = string.Empty;
        return false;
    }

    private static bool TryResolveComplexStringArguments(string template, string attacker, string defender, out string rendered)
    {
        var positions = new List<int>();
        for (var index = 0; (index = template.IndexOf("%s", index, StringComparison.Ordinal)) >= 0; index += 2) positions.Add(index);
        static bool IsAffix(string text, int position) =>
            (position > 0 && char.IsLetter(text[position - 1])) ||
            (position + 2 < text.Length && char.IsLetter(text[position + 2]));

        string[]? arguments = null;
        if (positions.Count == 2 && IsAffix(template, positions[1]))
            arguments = [attacker, string.Empty];
        else if (positions.Count == 3)
        {
            if (IsAffix(template, positions[1])) arguments = [attacker, string.Empty, defender];
            else
            {
                var tail = template[(positions[2] + 2)..];
                if (tail.TrimStart().StartsWith("dodges", StringComparison.OrdinalIgnoreCase))
                    arguments = [attacker, defender, defender];
            }
        }
        else if (positions.Count == 5 && IsAffix(template, positions[1]) && IsAffix(template, positions[3]))
            arguments = [attacker, string.Empty, defender, string.Empty, defender];

        if (arguments is null)
        {
            rendered = string.Empty;
            return false;
        }

        rendered = template;
        foreach (var argument in arguments) rendered = ReplaceFirst(rendered, "%s", argument);
        return !rendered.Contains("%s", StringComparison.Ordinal);
    }

    private static int CountToken(string text, string token)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0; index += token.Length) count++;
        return count;
    }

    private static string ReplaceFirst(string text, string token, string replacement)
    {
        var index = text.IndexOf(token, StringComparison.Ordinal);
        return index < 0 ? text : string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + token.Length));
    }

    private RuntimeLivingNpcState? ResolveEncounterLivingNpc(RuntimeSession session, RuntimeLiveCombatEncounter encounter) =>
        Living(session).Capture().Npcs.FirstOrDefault(npc =>
            npc.InstanceId.Equals(encounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase));
}

public static class RuntimeEquipmentCombatEffects
{
    public sealed record DamageShieldComponent(int Damage, int MessageId);

    public static IReadOnlyList<DamageShieldComponent> ResolveEquipmentDamageShields(
        string projectRoot, RuntimeCharacterRecord character, int receivedPhysicalDamage)
    {
        if (receivedPhysicalDamage <= 0) return Array.Empty<DamageShieldComponent>();
        var catalog = RuntimeItemCatalog.Load(projectRoot, character.WorldId);
        return (character.ItemState?.Items ?? Array.Empty<RuntimeItemInstance>())
            .Where(item => string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase) && item.EquippedSlot is not null)
            .Select(item => catalog.Require(item.CanonicalItemId))
            .Where(item => item.EquipmentAbilityModifiers.GetValueOrDefault(72) > 0 && item.EquipmentAbilityModifiers.GetValueOrDefault(137) > 0)
            .Select(item => new DamageShieldComponent(
                item.EquipmentAbilityModifiers[72], item.EquipmentAbilityModifiers[137]))
            .ToArray();
    }

    public static string FormatDamageShieldMessage(string template, string targetName, int damage)
    {
        var rendered = template.Replace("%d", damage.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        var marker = rendered.IndexOf("%s", StringComparison.Ordinal);
        if (marker >= 0) rendered = rendered[..marker] + targetName + rendered[(marker + 2)..];
        marker = rendered.IndexOf("%s", StringComparison.Ordinal);
        if (marker >= 0) rendered = rendered[..marker] + damage.ToString(System.Globalization.CultureInfo.InvariantCulture) + rendered[(marker + 2)..];
        return rendered;
    }

    public static int ResolveMartialAccuracy(RuntimeCharacterRecord character, ClassicAttackType attackType)
    {
        var key = attackType switch
        {
            ClassicAttackType.Punch => "punch_accuracy",
            ClassicAttackType.Kick => "kick_accuracy",
            ClassicAttackType.JumpKick => "jumpkick_accuracy",
            _ => null
        };
        return key is null ? 0 : checked(ReadDerived(character, key + "_bonus") + RuntimeClassicStatusProjection.Numeric(character, key));
    }

    public static int ResolveMartialDamage(RuntimeCharacterRecord character, ClassicAttackType attackType)
    {
        var key = attackType switch
        {
            ClassicAttackType.Punch => "punch_damage",
            ClassicAttackType.Kick => "kick_damage",
            ClassicAttackType.JumpKick => "jumpkick_damage",
            _ => null
        };
        return key is null ? 0 : checked(ReadDerived(character, key + "_bonus") + RuntimeClassicStatusProjection.Numeric(character, key));
    }

    public static int ResolveDamageShieldRetaliation(RuntimeCharacterRecord character, int receivedPhysicalDamage)
    {
        if (receivedPhysicalDamage <= 0) return 0;
        var equipment = ReadDerived(character, "damage_shield_bonus");
        var activeSpell = RuntimeClassicStatusProjection.Numeric(character, "damage_shield");
        return Math.Max(0, checked(equipment + activeSpell));
    }

    public static int ResolveAlignmentProtection(RuntimeCharacterRecord character, string attackerAlignment)
    {
        if (IsEvil(attackerAlignment))
            return Math.Max(0, checked(ReadDerived(character, "protection_from_evil_bonus") +
                                       RuntimeClassicStatusProjection.Numeric(character, "protection_from_evil")));
        if (IsGood(attackerAlignment))
            return Math.Max(0, checked(ReadDerived(character, "protection_from_good_bonus") +
                                       RuntimeClassicStatusProjection.Numeric(character, "protection_from_good")));
        return 0;
    }

    private static int ReadDerived(RuntimeCharacterRecord character, string key)
        => character.SemanticState?.DerivedStats.TryGetValue(key, out var value) == true ? value : 0;

    private static bool IsGood(string value) =>
        value.Contains("lawful", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("saint", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("good", StringComparison.OrdinalIgnoreCase);

    private static bool IsEvil(string value) =>
        value.Contains("outlaw", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("criminal", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("villain", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("fiend", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("evil", StringComparison.OrdinalIgnoreCase);
}

public sealed class RuntimeLiveCombatRoundSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveCombatRoundSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeLiveCombatRoundSmokeReport();
        try
        {
            var itemCatalog = RuntimeItemCombatCatalog.Load(projectRoot);
            Require(itemCatalog.Profiles.Any(x => x.MaximumDamage > 0), "generated-player-weapon-profile"); report.Checks.Add("generated-player-weapon-profile");
            var spellCatalog = RuntimeSpellCatalog.Load(projectRoot, "Classic");
            var expirationSpell = spellCatalog.Spells.First();
            var expirationNow = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
            var expirationTarget = new RuntimeSpellTarget("character", "audit.character");
            var expiredEffect = new RuntimeActiveSpellEffect("audit.expired", expirationSpell.Id, "npc.audit", expirationTarget, 1, 1, 10, expirationNow.AddSeconds(-20), expirationNow.AddSeconds(-10));
            var liveEffect = new RuntimeActiveSpellEffect("audit.live", expirationSpell.Id, "npc.audit", expirationTarget, 1, 1, 10, expirationNow.AddSeconds(-5), expirationNow.AddSeconds(5));
            var expirationEngine = new RuntimeSpellEngine(spellCatalog, initial: new RuntimeSpellState(Array.Empty<RuntimeKnownSpell>(), new[] { expiredEffect, liveEffect }, new Dictionary<string, RuntimeSpellResources>()));
            Require(expirationEngine.Expire(expirationNow) == 1, "npc-status-expiration-removes-only-expired"); report.Checks.Add("npc-status-expiration-removes-only-expired");
            Require(expirationEngine.EffectsFor("audit.character").Single().EffectId == "audit.live", "npc-status-expiration-preserves-live"); report.Checks.Add("npc-status-expiration-preserves-live");
            var root = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
            var classicDefinition = ServerRuntimeOptions.Load(projectRoot).Worlds.Single(world => world.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase));
            var classicWorld = new RuntimeWorldLoader().Load(projectRoot, classicDefinition);
            var sphinxScript = RuntimeNpcHitSpellScriptCatalog.Load(projectRoot, 686)
                ?? throw new InvalidDataException("Generated sphinx hit-spell script 686 was not found.");
            Require(sphinxScript is { RootTextblockId: 2510, RollMinimum: 1, RollMaximum: 100 } &&
                sphinxScript.Branches.Select(branch => branch.Threshold).SequenceEqual(new[] { 25, 50, 75, 100 }),
                "sphinx-hit-spell-generated-chain"); report.Checks.Add("sphinx-hit-spell-generated-chain");
            Require(RuntimeNpcHitSpellScriptCatalog.SelectBranch(sphinxScript, 1).TextblockId == 2512 &&
                RuntimeNpcHitSpellScriptCatalog.SelectBranch(sphinxScript, 25).TextblockId == 2512 &&
                RuntimeNpcHitSpellScriptCatalog.SelectBranch(sphinxScript, 26).TextblockId == 2513 &&
                RuntimeNpcHitSpellScriptCatalog.SelectBranch(sphinxScript, 51).TextblockId == 2514 &&
                RuntimeNpcHitSpellScriptCatalog.SelectBranch(sphinxScript, 76).TextblockId == 2515 &&
                RuntimeNpcHitSpellScriptCatalog.SelectBranch(sphinxScript, 100).TextblockId == 2515,
                "sphinx-hit-spell-threshold-boundaries"); report.Checks.Add("sphinx-hit-spell-threshold-boundaries");
            var minimumDamagePlan = RuntimeNpcHitSpellScriptCatalog.ValidateAndPlan(projectRoot, classicWorld, sphinxScript, 1, new Random(1));
            Require(minimumDamagePlan.Branch.Operations.Select(operation => operation.Kind).SequenceEqual(new[] { "message", "teleport", "message", "message", "teleport", "message", "cast", "cast" }) &&
                minimumDamagePlan.TotalDamage is >= 10 and <= 30,
                "sphinx-hit-spell-runtime-plan"); report.Checks.Add("sphinx-hit-spell-runtime-plan");
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "npcs.json")));
            var npcs = document.RootElement.GetProperty("records").EnumerateArray().Select(RuntimeNpcDefinition.FromJson).ToArray();
            Require(npcs.Any(x => x.Attacks.Any(a => a.Accuracy > 0 && a.MaximumDamage >= a.MinimumDamage && a.SelectionPercent > 0)), "generated-npc-executable-attacks"); report.Checks.Add("generated-npc-executable-attacks");
            var multiAttackNpc = npcs.FirstOrDefault(x => x.AttackCount > 1 && x.Attacks.Any(a => a.Accuracy > 0 && a.MaximumDamage >= a.MinimumDamage && a.SelectionPercent > 0));
            Require(multiAttackNpc is not null, "generated-npc-multi-attack-count"); report.Checks.Add("generated-npc-multi-attack-count");
            Require(RuntimeLiveGameplayFacade.ResolveNpcAttacksPerRound(multiAttackNpc!) == RuntimeClassicNpcCombatCadence.AttemptsPerRound, "npc-six-attempt-live-consumer"); report.Checks.Add("npc-six-attempt-live-consumer");
            var zeroAttackNpc = npcs.FirstOrDefault(x => x.AttackCount == 0);
            Require(zeroAttackNpc is not null, "generated-npc-zero-attack-count"); report.Checks.Add("generated-npc-zero-attack-count");
            Require(RuntimeLiveGameplayFacade.ResolveNpcAttacksPerRound(zeroAttackNpc!) == 0, "zero-attack-npc-does-not-attack"); report.Checks.Add("zero-attack-npc-does-not-attack");
            var energyAttack = npcs.SelectMany(x => x.Attacks).FirstOrDefault(x => x.EnergyCost > 0 && x.Accuracy > 0 && x.SelectionPercent > 0);
            Require(energyAttack is not null, "decoded-npc-attack-energy-cost"); report.Checks.Add("decoded-npc-attack-energy-cost");
            var deterministicEnergyAttack = energyAttack! with { SelectionPercent = 100 };
            Require(RuntimeLiveGameplayFacade.SelectNpcAttack(new[] { deterministicEnergyAttack }, deterministicEnergyAttack.EnergyCost - 1) == deterministicEnergyAttack, "npc-unaffordable-row-still-selected"); report.Checks.Add("npc-unaffordable-row-still-selected");
            Require(RuntimeLiveGameplayFacade.SelectNpcAttack(new[] { deterministicEnergyAttack }, deterministicEnergyAttack.EnergyCost) == deterministicEnergyAttack, "npc-affordable-attack-selected"); report.Checks.Add("npc-affordable-attack-selected");
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterAttack(energyAttack.EnergyCost + 3, energyAttack) == 3, "npc-attack-energy-deducted"); report.Checks.Add("npc-attack-energy-deducted");
            var directSpellEnergyAttack = energyAttack with { Type = 2, EnergyCost = 100 };
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterResistedHitSpell(103, directSpellEnergyAttack, 1000) == 103, "npc-resisted-spell-full-refund"); report.Checks.Add("npc-resisted-spell-full-refund");
            var oddCostAttack = energyAttack with { Type = 2, EnergyCost = 125 };
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterResistedHitSpell(200, oddCostAttack, 1000) == 200, "npc-resisted-spell-odd-cost-full-refund"); report.Checks.Add("npc-resisted-spell-odd-cost-full-refund");
            var odd127CostAttack = energyAttack with { Type = 2, EnergyCost = 127 };
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterResistedHitSpell(200, odd127CostAttack, 1000) == 200, "npc-resisted-spell-127-cost-full-refund"); report.Checks.Add("npc-resisted-spell-127-cost-full-refund");
            Require(RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(250, 333) == 583, "npc-energy-below-baseline-adds-quantum"); report.Checks.Add("npc-energy-below-baseline-adds-quantum");
            Require(RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(333, 333) == 333, "npc-energy-idle-does-not-accumulate"); report.Checks.Add("npc-energy-idle-does-not-accumulate");
            Require(RuntimeClassicNpcCombatCadence.RefreshRoundEnergy(500, 333) == 333, "npc-energy-above-baseline-clamps"); report.Checks.Add("npc-energy-above-baseline-clamps");
            Require(RuntimeLiveGameplayFacade.ResolveNpcEncounterStartingEnergy(333) == 333, "npc-encounter-energy-starts-at-authored-value"); report.Checks.Add("npc-encounter-energy-starts-at-authored-value");
            var invalidAttackCountRejected = false;
            try { _ = RuntimeLiveGameplayFacade.ResolveNpcAttacksPerRound(new RuntimeNpcDefinition { Id = "npc.invalid", AttackCount = -1 }); }
            catch (InvalidDataException) { invalidAttackCountRejected = true; }
            Require(invalidAttackCountRejected, "negative-npc-attack-count-rejected"); report.Checks.Add("negative-npc-attack-count-rejected");
            var missingRowsRejected = false;
            try { _ = RuntimeLiveGameplayFacade.ResolveNpcAttacksPerRound(new RuntimeNpcDefinition { Id = "npc.invalid.rows", AttackCount = 1 }); }
            catch (InvalidDataException) { missingRowsRejected = true; }
            Require(missingRowsRejected, "positive-attack-count-requires-attack-rows"); report.Checks.Add("positive-attack-count-requires-attack-rows");
            var messageAttack = npcs.SelectMany(x => x.Attacks).FirstOrDefault(x => x.HitMessageLegacyId > 0 && x.MissMessageLegacyId > 0 && x.DodgeMessageLegacyId > 0);
            Require(messageAttack is not null, "generated-npc-outcome-message-relationships"); report.Checks.Add("generated-npc-outcome-message-relationships");
            var hitMessage = RuntimeLiveGameplayFacade.ResolveNpcCombatMessage(messageAttack!, new ClassicAttackResult(ClassicHitOutcome.Hit, 0, 0, 1, 1, 1, false), "npc", "player");
            var missMessage = RuntimeLiveGameplayFacade.ResolveNpcCombatMessage(messageAttack!, new ClassicAttackResult(ClassicHitOutcome.Miss, 0, 0, 0, 0, 0, false), "npc", "player");
            var dodgeMessage = RuntimeLiveGameplayFacade.ResolveNpcCombatMessage(messageAttack!, new ClassicAttackResult(ClassicHitOutcome.Dodged, 0, 0, 0, 0, 0, false), "npc", "player");
            Require(hitMessage.LegacyMessageId == messageAttack!.HitMessageLegacyId, "npc-hit-message-live-consumer"); report.Checks.Add("npc-hit-message-live-consumer");
            Require(missMessage.LegacyMessageId == messageAttack!.MissMessageLegacyId, "npc-miss-message-live-consumer"); report.Checks.Add("npc-miss-message-live-consumer");
            Require(dodgeMessage.LegacyMessageId == messageAttack!.DodgeMessageLegacyId, "npc-dodge-message-live-consumer"); report.Checks.Add("npc-dodge-message-live-consumer");
            var nativeMessages = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
            var decodedHit = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Hit, 1804, "fallback");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcCombatMessage(nativeMessages, decodedHit, "abomination", "player", 17, out var decodedHitText)
                && decodedHitText == "The abomination savages you for 17 damage!", "npc-native-hit-message-rendering"); report.Checks.Add("npc-native-hit-message-rendering");
            var argumentBlocked = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Miss, 8293, "fallback");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcCombatMessage(nativeMessages, argumentBlocked, "abomination", "Shisenshi", 0, out var decodedDodgeText)
                && decodedDodgeText == "The abomination snaps at Shisenshi, but Shisenshi dodges out of the way!", "npc-native-complex-message-rendering"); report.Checks.Add("npc-native-complex-message-rendering");
            var contextualMiss = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Miss, 8297, "fallback");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcCombatMessageFromAttack(nativeMessages, contextualMiss, messageAttack!, "abomination", "Shisenshi", out var contextualMissText)
                && contextualMissText == "The abomination savages you, but your armour deflects the blow!", "npc-native-paired-hit-argument-rendering"); report.Checks.Add("npc-native-paired-hit-argument-rendering");
            var verifiedNoOutput = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Hit, 66, "fallback-must-not-render");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcCombatMessage(nativeMessages, verifiedNoOutput, "stone sphinx", "Shisenshi", 0, out var noOutputText)
                && noOutputText.Length == 0, "npc-native-verified-no-output-rendering"); report.Checks.Add("npc-native-verified-no-output-rendering");
            var commanderAttack = npcs.Single(x => x.LegacyId == 246).Attacks.First(x => x.MissMessageLegacyId == 806);
            var bodyPartMiss = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Miss, 806, "fallback");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcCombatMessageFromAttack(nativeMessages, bodyPartMiss, commanderAttack, "Commander Markus", "Shisenshi", out var bodyPartMissText)
                && bodyPartMissText == "Commander Markus swings at Shisenshi with his greatsword, but Shisenshi dodges!", "npc-native-decoded-weapon-dodge-rendering"); report.Checks.Add("npc-native-decoded-weapon-dodge-rendering");
            var bodyPartMessageIds = new HashSet<int> { 806, 2490, 2939, 8310, 8372, 8480 };
            var bodyPartRows = npcs.SelectMany(npc => npc.Attacks).Where(attack => bodyPartMessageIds.Contains(attack.MissMessageLegacyId)).ToArray();
            Require(bodyPartRows.Length == 822, "npc-native-body-part-row-count"); report.Checks.Add("npc-native-body-part-row-count-822");
            Require(bodyPartRows.Count(attack => !string.IsNullOrWhiteSpace(attack.MissVerb) && !string.IsNullOrWhiteSpace(attack.WeaponName)) == 778,
                "npc-native-body-part-decoded-argument-coverage"); report.Checks.Add("npc-native-body-part-decoded-argument-coverage-778-of-822");
            var fullyBoundBodyPartIds = bodyPartRows.GroupBy(attack => attack.MissMessageLegacyId)
                .Where(group => group.All(attack => !string.IsNullOrWhiteSpace(attack.MissVerb) && !string.IsNullOrWhiteSpace(attack.WeaponName)))
                .Select(group => group.Key).OrderBy(id => id).ToArray();
            Require(fullyBoundBodyPartIds.SequenceEqual(new[] { 806, 2490, 8372 }), "npc-native-body-part-fully-bound-message-ids"); report.Checks.Add("npc-native-body-part-fully-bound-message-ids-3");
            var recoveredArgumentCatalog = RuntimeNpcAttackArgumentRecoveryCatalog.Load(projectRoot);
            var recoveredBodyPartRows = npcs.SelectMany(npc => npc.Attacks
                .Where(attack => bodyPartMessageIds.Contains(attack.MissMessageLegacyId))
                .Select(attack => recoveredArgumentCatalog.Complete(npc.Name, attack))).ToArray();
            Require(recoveredBodyPartRows.Count(attack => !string.IsNullOrWhiteSpace(attack.MissVerb) && !string.IsNullOrWhiteSpace(attack.WeaponName)) == 786,
                "npc-native-duplicate-definition-argument-recovery"); report.Checks.Add("npc-native-duplicate-definition-argument-recovery-786-of-822");
            var reachablePhysicalBodyPartRows = npcs.SelectMany(npc => npc.Attacks
                .Where(attack => attack.Type != 2 && bodyPartMessageIds.Contains(attack.MissMessageLegacyId))
                .Select(attack => recoveredArgumentCatalog.Complete(npc.Name, attack))).ToArray();
            Require(reachablePhysicalBodyPartRows.Length == 785 &&
                reachablePhysicalBodyPartRows.Count(attack => !string.IsNullOrWhiteSpace(attack.MissVerb) && !string.IsNullOrWhiteSpace(attack.WeaponName)) == 780,
                "npc-native-reachable-physical-body-part-coverage"); report.Checks.Add("npc-native-reachable-physical-body-part-coverage-780-of-785");
            Require(reachablePhysicalBodyPartRows.Count(attack => attack.HasExactNativeMissArguments) == 785,
                "npc-native-reachable-physical-body-part-exact-coverage"); report.Checks.Add("npc-native-reachable-physical-body-part-exact-coverage-785-of-785");
            var fullyBoundReachableIds = reachablePhysicalBodyPartRows.GroupBy(attack => attack.MissMessageLegacyId)
                .Where(group => group.All(attack => !string.IsNullOrWhiteSpace(attack.MissVerb) && !string.IsNullOrWhiteSpace(attack.WeaponName)))
                .Select(group => group.Key).OrderBy(id => id).ToArray();
            Require(fullyBoundReachableIds.SequenceEqual(new[] { 806, 2490, 2939, 8310, 8372 }),
                "npc-native-reachable-body-part-fully-bound-message-ids"); report.Checks.Add("npc-native-reachable-body-part-fully-bound-message-ids-5");
            var nahr = npcs.Single(x => x.LegacyId == 1006);
            Require(nahr.Attacks.Count == 5 && nahr.Attacks.All(attack =>
                attack.MissMessageLegacyId == 8480 && attack.NativeEmptyMissArgumentsVerified &&
                attack.MissVerb.Length == 0 && attack.WeaponName.Length == 0),
                "npc-native-nahr-8480-empty-arguments"); report.Checks.Add("npc-native-nahr-8480-empty-arguments-5-of-5");
            var nahrMiss = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Miss, 8480, "fallback-must-not-render");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcCombatMessageFromAttack(
                    nativeMessages, nahrMiss, nahr.Attacks[0], "Nahr", "Shisenshi", out var nahrMissText) &&
                nahrMissText == "Nahr  Shisenshi with his , but Shisenshi dodges!",
                "npc-native-nahr-8480-empty-argument-rendering"); report.Checks.Add("npc-native-nahr-8480-empty-argument-rendering");
            Require(npcs.SelectMany(npc => npc.Attacks).Count(attack => attack.Type == 2 && bodyPartMessageIds.Contains(attack.MissMessageLegacyId)) == 37,
                "npc-native-type2-physical-miss-template-nonreachable"); report.Checks.Add("npc-native-type2-physical-miss-template-nonreachable-37");
            var generatedSpellCatalog = RuntimeSpellCatalog.Load(projectRoot, "Classic");
            var darkClericSpellAttack = npcs.Single(x => x.LegacyId == 33).Attacks.Single(x => x.HitMessageLegacyId == 18);
            var npcSpellMessage = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Hit, 18, "fallback");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcSpellMessage(nativeMessages, generatedSpellCatalog, npcSpellMessage, darkClericSpellAttack, "dark cleric", 7, out var npcSpellText)
                && npcSpellText == "dark cleric casts spiritual hammer on you for 7 damage!", "npc-native-type2-spell-message-rendering"); report.Checks.Add("npc-native-type2-spell-message-rendering");
            var decodedType2Spell = RuntimeLiveGameplayFacade.ResolveNpcHitSpell(
                darkClericSpellAttack, new ClassicAttackResult(ClassicHitOutcome.Hit, 100, 0, 0, 0, 0, false), projectRoot);
            Require(darkClericSpellAttack.HitSpellLegacyId == 0 && darkClericSpellAttack.EffectiveSpellLegacyId == 16 &&
                decodedType2Spell is { Triggered: true, LegacySpellId: 16, SpellName: "spiritual hammer" },
                "npc-decoded-type2-spell-id-semantics"); report.Checks.Add("npc-decoded-type2-spell-id-semantics");
            var paleMushroomSongAttack = npcs.Single(x => x.LegacyId == 411).Attacks.Single();
            var npcSongMessage = new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Miss, 8502, "fallback");
            Require(RuntimeLiveGameplayFacade.TryRenderNativeNpcSpellMessage(nativeMessages, generatedSpellCatalog, npcSongMessage, paleMushroomSongAttack, "pale mushroom", 0, out var npcSongText)
                && npcSongText == "pale mushroom sings the cloud of spores to you!", "npc-native-type2-song-message-rendering"); report.Checks.Add("npc-native-type2-song-message-rendering");
            var combatMessageIds = npcs.SelectMany(npc => npc.Attacks)
                .SelectMany(attack => new[] { attack.HitMessageLegacyId, attack.MissMessageLegacyId, attack.DodgeMessageLegacyId })
                .Where(id => id > 0).Distinct().ToArray();
            Require(combatMessageIds.Length == 735, $"live-npc-combat-message-id-count:{combatMessageIds.Length}"); report.Checks.Add("live-npc-combat-message-id-count-735");
            var unresolvedCombatMessageIds = combatMessageIds.Where(id => !nativeMessages.TryResolveMessage(id, out _)).ToArray();
            Require(unresolvedCombatMessageIds.Length == 0, "npc-combat-message-ids-unresolved:" + string.Join(',', unresolvedCombatMessageIds.Take(12))); report.Checks.Add("all-npc-combat-message-ids-resolve");
            var generatedCombatMessageIds = document.RootElement.GetProperty("records").EnumerateArray()
                .Where(npc => npc.TryGetProperty("attacks", out _))
                .SelectMany(npc => npc.GetProperty("attacks").EnumerateArray())
                .SelectMany(attack => new[]
                {
                    JsonValueReader.ReadInt(attack, "hit_message_legacy_id", 0),
                    JsonValueReader.ReadInt(attack, "miss_message_legacy_id", 0),
                    JsonValueReader.ReadInt(attack, "dodge_message_legacy_id", 0)
                }).Where(id => id > 0).Distinct().ToArray();
            Require(generatedCombatMessageIds.Length == 736 && generatedCombatMessageIds.All(id => nativeMessages.TryResolveMessage(id, out _)), "all-generated-npc-combat-message-ids-resolve"); report.Checks.Add("all-generated-npc-combat-message-ids-resolve-736");
            var directlyRenderable = generatedCombatMessageIds.Count(id => RuntimeLiveGameplayFacade.TryRenderNativeNpcCombatMessage(
                nativeMessages, new RuntimeNpcCombatMessageSelection(ClassicHitOutcome.Hit, id, "fallback"), "npc", "player", 1, out _));
            Require(directlyRenderable == 719, $"npc-native-safe-template-coverage:{directlyRenderable}-of-736"); report.Checks.Add($"npc-native-safe-template-coverage-{directlyRenderable}-of-736");
            var hitSpellAttack = npcs.SelectMany(x => x.Attacks).FirstOrDefault(x => x.HitSpellLegacyId > 0);
            Require(hitSpellAttack is not null, "generated-npc-hit-spell-relationship"); report.Checks.Add("generated-npc-hit-spell-relationship");
            var hitSpell = RuntimeLiveGameplayFacade.ResolveNpcHitSpell(hitSpellAttack!, new ClassicAttackResult(ClassicHitOutcome.Hit, 0, 0, 1, 1, 1, false), projectRoot);
            Require(hitSpell is { Triggered: true } && hitSpell.LegacySpellId == hitSpellAttack!.HitSpellLegacyId && hitSpell.CanonicalSpellId.StartsWith("spell.", StringComparison.OrdinalIgnoreCase), "npc-hit-spell-canonical-resolution"); report.Checks.Add("npc-hit-spell-canonical-resolution");
            Require(hitSpell!.EffectRange.RollMaximum >= hitSpell.EffectRange.RollMinimum, "npc-hit-spell-effect-range-bound"); report.Checks.Add("npc-hit-spell-effect-range-bound");
            Require(hitSpell.TargetTypeRaw >= 0 && hitSpell.AttackTypeRaw >= 0 && hitSpell.ResistTypeRaw >= 0 && hitSpell.DifficultyRaw >= 0, "npc-hit-spell-metadata-bound"); report.Checks.Add("npc-hit-spell-metadata-bound");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(0) == "cold", "npc-hit-spell-cold-classification"); report.Checks.Add("npc-hit-spell-cold-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(1) == "fire", "npc-hit-spell-fire-classification"); report.Checks.Add("npc-hit-spell-fire-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(2) == "stone", "npc-hit-spell-stone-classification"); report.Checks.Add("npc-hit-spell-stone-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(3) == "lightning", "npc-hit-spell-lightning-classification"); report.Checks.Add("npc-hit-spell-lightning-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(4) == "untyped", "npc-hit-spell-untyped-classification"); report.Checks.Add("npc-hit-spell-untyped-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(5) == "acid", "npc-hit-spell-acid-classification"); report.Checks.Add("npc-hit-spell-acid-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(5, classicNativeElement: true) == "water",
                "npc-native-attack5-uses-water-resistance-ability147");
            report.Checks.Add("npc-native-attack5-uses-water-resistance-ability147");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(6) == "poison", "npc-hit-spell-poison-classification"); report.Checks.Add("npc-hit-spell-poison-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(0) == "contact-attached-target", "npc-hit-spell-contact-target-classification"); report.Checks.Add("npc-hit-spell-contact-target-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(1) == "self-target", "npc-hit-spell-self-target-classification"); report.Checks.Add("npc-hit-spell-self-target-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(8) == "single-hostile-target", "npc-hit-spell-single-hostile-target-classification"); report.Checks.Add("npc-hit-spell-single-hostile-target-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(11) == "full-area-target", "npc-hit-spell-full-area-target-classification"); report.Checks.Add("npc-hit-spell-full-area-target-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(12) == "area-hostile-target", "npc-hit-spell-area-hostile-target-classification"); report.Checks.Add("npc-hit-spell-area-hostile-target-classification");
            Require(RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.IsEligibleHit(ClassicHitOutcome.Hit), "weapon-on-hit-normal-hit-admission"); report.Checks.Add("weapon-on-hit-normal-hit-admission");
            Require(RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.IsEligibleHit(ClassicHitOutcome.Critical), "weapon-on-hit-critical-admission"); report.Checks.Add("weapon-on-hit-critical-admission");
            Require(!RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.IsEligibleHit(ClassicHitOutcome.Miss), "weapon-on-hit-miss-rejection"); report.Checks.Add("weapon-on-hit-miss-rejection");
            Require(!RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.IsEligibleHit(ClassicHitOutcome.Dodged), "weapon-on-hit-dodge-rejection"); report.Checks.Add("weapon-on-hit-dodge-rejection");
            Require(!RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.IsEligibleHit(ClassicHitOutcome.Glance), "weapon-on-hit-glance-rejection"); report.Checks.Add("weapon-on-hit-glance-rejection");
            Require(RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.Triggers(25, 25) && !RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.Triggers(25, 26), "weapon-on-hit-percentile-boundary"); report.Checks.Add("weapon-on-hit-percentile-boundary");
            Require(!RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.Triggers(0, 1) && RuntimeLiveGameplayFacade.RuntimeWeaponOnHitSpellTrigger.Triggers(100, 100), "weapon-on-hit-percentile-range"); report.Checks.Add("weapon-on-hit-percentile-range");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistanceScope(0) == "never-resisted", "npc-hit-spell-never-resisted-classification"); report.Checks.Add("npc-hit-spell-never-resisted-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistanceScope(1) == "anti-magic-only", "npc-hit-spell-antimagic-only-classification"); report.Checks.Add("npc-hit-spell-antimagic-only-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistanceScope(2) == "magic-resistance-eligible", "npc-hit-spell-magic-resistance-eligible-classification"); report.Checks.Add("npc-hit-spell-magic-resistance-eligible-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTypeMeaning(0) == "immediate-effect", "npc-hit-spell-immediate-type-classification"); report.Checks.Add("npc-hit-spell-immediate-type-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTypeMeaning(1) == "poison-delivery", "npc-hit-spell-poison-type-classification"); report.Checks.Add("npc-hit-spell-poison-type-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTypeMeaning(3) == "status-or-scripted-effect", "npc-hit-spell-status-scripted-type-classification"); report.Checks.Add("npc-hit-spell-status-scripted-type-classification");
            RuntimeNpcHitSpellSelection? immediateDamageSpell = null;
            foreach (var candidate in npcs.SelectMany(x => x.Attacks).Where(x => x.HitSpellLegacyId > 0))
            {
                var resolved = RuntimeLiveGameplayFacade.ResolveNpcHitSpell(candidate, new ClassicAttackResult(ClassicHitOutcome.Hit, 0, 0, 1, 1, 1, false), projectRoot);
                if (resolved is { Triggered: true, SpellTypeRaw: 0 } && resolved.TargetTypeRaw is 0 or 8 or 11 or 12
                    && resolved.EffectRange.Sign == RuntimeSpellEffectSign.Positive && resolved.EffectRange.RollMaximum > 0)
                {
                    immediateDamageSpell = resolved;
                    break;
                }
            }
            Require(immediateDamageSpell is not null, "generated-npc-immediate-damage-hit-spell"); report.Checks.Add("generated-npc-immediate-damage-hit-spell");
            var areaHostileSpell = immediateDamageSpell is { } areaCandidate
                ? areaCandidate with { TargetTypeRaw = 12, Classification = RuntimeLiveGameplayFacade.ClassifyNpcHitSpell(areaCandidate.SpellTypeRaw, 12, areaCandidate.AttackTypeRaw, areaCandidate.ResistTypeRaw) }
                : throw new InvalidOperationException("npc-area-hostile-spell-candidate-missing");
            Require(RuntimeLiveGameplayFacade.IsNpcAreaHostileSpell(areaHostileSpell), "npc-area-hostile-live-routing"); report.Checks.Add("npc-area-hostile-live-routing");
            Require(!RuntimeLiveGameplayFacade.IsNpcAreaHostileSpell(areaHostileSpell with { TargetTypeRaw = 8 }), "npc-single-target-excluded-from-area-routing"); report.Checks.Add("npc-single-target-excluded-from-area-routing");
            var plagueAttack = npcs.Single(x => x.LegacyId == 361).Attacks.Single(x => x.Type == 2 && x.EffectiveSpellLegacyId == 446);
            var plagueSpell = RuntimeLiveGameplayFacade.ResolveNpcHitSpell(
                plagueAttack, new ClassicAttackResult(ClassicHitOutcome.Hit, 100, 0, 0, 0, 0, false), projectRoot);
            Require(plagueSpell is { Triggered: true, LegacySpellId: 446, TargetTypeRaw: 11, SpellTypeRaw: 0 } &&
                RuntimeLiveGameplayFacade.IsNpcFullAreaSpell(plagueSpell) && RuntimeLiveGameplayFacade.IsNpcRoomAreaSpell(plagueSpell),
                "npc-plague-full-area-live-routing"); report.Checks.Add("npc-plague-full-area-live-routing");
            var plagueEffect = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectForAudit(plagueSpell!, plagueSpell!.EffectRange.RollMinimum);
            Require(plagueEffect.Executable && plagueEffect.DirectDamage == 4 && plagueSpell.EffectRange.RollMaximum == 10,
                "npc-plague-decoded-damage-range"); report.Checks.Add("npc-plague-decoded-damage-range-4-to-10");
            var liveStatusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, "Classic");
            Require(liveStatusCatalog.TryGet("spell.446", out var plagueStatus) && plagueStatus.DurationRounds == 30 &&
                plagueStatus.HasCombatModifiers &&
                plagueStatus.Abilities.Any(ability => ability.AbilityId == 34 && ability.Value == -10) &&
                plagueStatus.Abilities.Any(ability => ability.AbilityId == 2 && ability.Value == -5) &&
                plagueStatus.Abilities.Any(ability => ability.AbilityId == 4 && ability.Value == -4),
                "npc-plague-decoded-duration-modifiers"); report.Checks.Add("npc-plague-decoded-duration-modifiers");
            Require(liveStatusCatalog.TryGet("spell.365", out var beePoisonStatus)
                    && !RuntimeLiveGameplayFacade.ClassicNpcHasDirectHitPointHandler(beePoisonStatus)
                    && liveStatusCatalog.TryGet("spell.777", out var voodooStunStatus)
                    && !RuntimeLiveGameplayFacade.ClassicNpcHasDirectHitPointHandler(voodooStunStatus)
                    && RuntimeLiveGameplayFacade.ClassicNpcHasDirectHitPointHandler(plagueStatus)
                    && liveStatusCatalog.TryGet("spell.950", out var drainStatus)
                    && RuntimeLiveGameplayFacade.ClassicNpcHasDirectHitPointHandler(drainStatus),
                "npc-classic-status-only-does-not-fabricate-direct-hp-damage");
            report.Checks.Add("npc-classic-status-only-does-not-fabricate-direct-hp-damage");
            Require(RuntimeLiveGameplayFacade.ResolveClassicNpcDrainHitPointsAfter(20, 100, 30) == 50
                && RuntimeLiveGameplayFacade.ResolveClassicNpcDrainHitPointsAfter(90, 100, 30) == 100
                && RuntimeLiveGameplayFacade.ResolveClassicNpcDrainHitPointsAfter(100, 100, 30) == 100
                && liveStatusCatalog.TryGet("spell.950", out var nativeDrainVector)
                && nativeDrainVector.Abilities.Count(ability => ability.AbilityId == 8) == 1,
                "npc-native-ability8-drain-recovers-caster-hp-capped-at-maximum");
            report.Checks.Add("npc-native-ability8-drain-recovers-caster-hp-capped-at-maximum");
            // WCCMMUD _monster_cast_area dispatches ability 8 by its effect
            // ID even if the spell's +0xC4 type is 3 (Shadow Breath / 1011).
            // Four canonical area spells have Drain: 389, 520, 899, 1011.
            Require(liveStatusCatalog.TryGet("spell.389", out var drainsAreaStatus)
                    && RuntimeLiveGameplayFacade.ClassicNpcHasDrainHandler(drainsAreaStatus)
                    && liveStatusCatalog.TryGet("spell.520", out var sucksLifeAreaStatus)
                    && RuntimeLiveGameplayFacade.ClassicNpcHasDrainHandler(sucksLifeAreaStatus)
                    && liveStatusCatalog.TryGet("spell.899", out var necromanticStormAreaStatus)
                    && RuntimeLiveGameplayFacade.ClassicNpcHasDrainHandler(necromanticStormAreaStatus)
                    && liveStatusCatalog.TryGet("spell.1011", out var shadowBreathAreaStatus)
                    && RuntimeLiveGameplayFacade.ClassicNpcHasDrainHandler(shadowBreathAreaStatus)
                    && shadowBreathAreaStatus.SpellTypeRaw == 3,
                "npc-four-area-drain-owners-including-type3-shadow-breath");
            report.Checks.Add("npc-four-area-drain-owners-including-type3-shadow-breath");
            var shadowBreathFixture = plagueSpell! with
            {
                SpellTypeRaw = 3,
                TargetTypeRaw = 12,
                AttackTypeRaw = 4,
                MinimumEffect = 50,
                MaximumEffect = 150,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(50, 150)
            };
            var nativeShadowBreathEffect = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                shadowBreathFixture, 0, false, new CountingRangeRandom(),
                classicNativeHasDirectHitPointHandler: true, classicNativeDrainAbility8: true);
            var legacyShadowBreathEffect = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                shadowBreathFixture, 0, false, new CountingRangeRandom(),
                classicNativeHasDirectHitPointHandler: true);
            Require(nativeShadowBreathEffect.Executable
                && nativeShadowBreathEffect.DirectDamage == 50
                && !legacyShadowBreathEffect.Executable
                && RuntimeLiveGameplayFacade.ResolveClassicNpcDrainHitPointsAfter(20, 100, 50) == 70
                && RuntimeLiveGameplayFacade.ResolveClassicNpcDrainHitPointsAfter(70, 100, 50) == 100,
                "npc-native-type3-area-drain-damages-and-heals-once-per-recipient");
            report.Checks.Add("npc-native-type3-area-drain-damages-and-heals-once-per-recipient");
            Require(liveStatusCatalog.TryGet("spell.82", out var breathDrainStatus)
                    && breathDrainStatus.SpellTypeRaw == 3
                    && RuntimeLiveGameplayFacade.ClassicNpcHasDrainHandler(breathDrainStatus),
                "npc-native-contact-type0-drain-catalog-owner");
            report.Checks.Add("npc-native-contact-type0-drain-catalog-owner");
            var invalidContactDrain = shadowBreathFixture with
            {
                TargetTypeRaw = 0,
                MinimumEffect = 4, MaximumEffect = 12,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(4, 12)
            };
            var invalidContactDrainRng = new CountingRangeRandom();
            var contactDrainResult = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                invalidContactDrain, 0, false, invalidContactDrainRng,
                classicNativeHasDirectHitPointHandler: true, classicNativeDrainAbility8: true);
            Require(!contactDrainResult.Executable
                && contactDrainResult.QuarantineReason == "native-drain-target-type-ineligible"
                && invalidContactDrainRng.Calls == 0,
                "npc-native-contact-type0-drain-fails-closed-without-roll");
            report.Checks.Add("npc-native-contact-type0-drain-fails-closed-without-roll");
            var statusOnlyRng = new CountingRangeRandom();
            var statusOnlyDamage = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                plagueSpell!, 0, false, statusOnlyRng,
                classicNativeHasDirectHitPointHandler: false);
            Require(!statusOnlyDamage.Executable && statusOnlyDamage.DirectDamage == 0
                && statusOnlyDamage.QuarantineReason == "no-native-direct-hit-point-handler"
                && statusOnlyRng.Calls == 0,
                "npc-classic-status-only-no-immediate-damage-or-rng");
            report.Checks.Add("npc-classic-status-only-no-immediate-damage-or-rng");
            var nativeBeePoisonFixture = plagueSpell! with
            {
                CanonicalSpellId = "spell.365", SpellTypeRaw = 0,
                TargetTypeRaw = 8, DurationRaw = 100
            };
            var nativeBeePoisonDelivery = RuntimeLiveGameplayFacade.ResolveNpcHitSpellStatusDelivery(
                nativeBeePoisonFixture, false, false, classicNativePoisonAbility19: true);
            var customBeePoisonDelivery = RuntimeLiveGameplayFacade.ResolveNpcHitSpellStatusDelivery(
                nativeBeePoisonFixture, false, false);
            Require(nativeBeePoisonDelivery.Eligible && nativeBeePoisonDelivery.Applied
                && nativeBeePoisonDelivery.EffectKind == "immediate-with-poison"
                && !customBeePoisonDelivery.Eligible,
                "npc-classic-type0-poison19-delivers-status-without-direct-hp-damage");
            report.Checks.Add("npc-classic-type0-poison19-delivers-status-without-direct-hp-damage");
            var plagueDelivery = RuntimeLiveGameplayFacade.ResolveNpcHitSpellStatusDelivery(plagueSpell, false, false, plagueStatus.HasCombatModifiers);
            Require(plagueDelivery.Eligible && plagueDelivery.Applied && plagueDelivery.DurationSeconds == 30 &&
                plagueDelivery.EffectKind == "immediate-with-duration-modifiers",
                "npc-plague-damage-and-status-routing"); report.Checks.Add("npc-plague-damage-and-status-routing");
            var plagueNow = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
            var plagueCharacter = new RuntimeCharacterRecord(
                "character.plague.audit", "account.audit", "Audit", "Classic", "race.audit", "Audit", "class.audit", "Audit", 1, 0,
                100, 100, 0, 0, "room.map1.2140", 2140,
                new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(),
                new Dictionary<string, string?>(), new Dictionary<string, string>(), plagueNow, plagueNow);
            var plagueActiveEffect = new RuntimeActiveSpellEffect(
                "effect.plague.audit", "spell.446", "npc.361", new RuntimeSpellTarget("character", plagueCharacter.CharacterId),
                4, 10, 30, plagueNow, plagueNow.AddSeconds(30 * RuntimeClassicLiveStatusCatalog.SpellRoundSeconds));
            var plagueProjected = RuntimeClassicStatusProjection.Apply(
                plagueCharacter, new[] { plagueActiveEffect }, liveStatusCatalog, plagueNow, "audit.plague");
            Require(RuntimeClassicStatusProjection.Numeric(plagueProjected, "dodge") == -10 &&
                RuntimeClassicStatusProjection.Numeric(plagueProjected, "armor_class") == -5 &&
                RuntimeClassicStatusProjection.Numeric(plagueProjected, "maximum_damage") == -4,
                "npc-plague-live-combat-modifier-projection"); report.Checks.Add("npc-plague-live-combat-modifier-projection");
            var selfTargetSpells = npcs.SelectMany(npc => npc.Attacks.Select(attack => (npc.LegacyId, Attack: attack)))
                .Select(row => (row.LegacyId, Spell: RuntimeLiveGameplayFacade.ResolveNpcHitSpell(
                    row.Attack, new ClassicAttackResult(ClassicHitOutcome.Hit, 100, 0, 0, 0, 0, false), projectRoot)))
                .Where(row => row.Spell is { Triggered: true, TargetTypeRaw: 1 })
                .ToArray();
            Require(selfTargetSpells.Length == 6 && selfTargetSpells.All(row => RuntimeLiveGameplayFacade.IsNpcSelfSpell(row.Spell!)),
                "npc-self-target-row-inventory"); report.Checks.Add("npc-self-target-six-row-inventory");
            Require(selfTargetSpells.Count(row => row.LegacyId is >= 548 and <= 552 && row.Spell!.LegacySpellId == 686) == 5,
                "npc-sphinx-self-spell-inventory"); report.Checks.Add("npc-sphinx-five-self-spell-686-rows");
            Require(selfTargetSpells.Count(row => row.LegacyId == 1000 && row.Spell!.LegacySpellId == 1220) == 1,
                "npc-blacknight-self-spell-inventory"); report.Checks.Add("npc-blacknight-one-self-spell-1220-row");
            Require(liveStatusCatalog.TryGet("spell.1220", out var blacknightStatus) && blacknightStatus.DurationRounds == 15 &&
                blacknightStatus.Abilities.Count == 1 && blacknightStatus.Abilities[0] is { AbilityId: 37, Value: 200 },
                "npc-blacknight-decoded-self-status"); report.Checks.Add("npc-blacknight-decoded-self-status");
            var nativeAreaCandidates = new[]
            {
                new RuntimeClassicNpcAreaTargetSnapshot("area.member1", false, 0, 0, 0, 0),
                new RuntimeClassicNpcAreaTargetSnapshot("area.immune", true, 0, 0, 0, 0),
                new RuntimeClassicNpcAreaTargetSnapshot("area.member2", false, 0, 0, 0, 0)
            };
            var noResistAreaRng = new CountingRangeRandom();
            var noResistArea = RuntimeLiveGameplayFacade.ResolveClassicNpcAreaRollPlan(
                0, 4, 10, nativeAreaCandidates, noResistAreaRng);
            Require(noResistArea.EligibleTargetCount == 2
                && noResistArea.SharedBaseMagnitude == 4
                && noResistAreaRng.Calls == 1
                && noResistArea.Targets.Select(x => x.Admitted).SequenceEqual(new[] { true, false, true })
                && noResistArea.Targets.All(x => x.ResistanceRoll == 0),
                "npc-native-area-immunity-and-single-base-draw-after-admission");
            report.Checks.Add("npc-native-area-immunity-and-single-base-draw-after-admission");
            var fullyResistedAreaRng = new CountingRangeRandom();
            var fullyResistedArea = RuntimeLiveGameplayFacade.ResolveClassicNpcAreaRollPlan(
                2, 4, 10,
                new[] { new RuntimeClassicNpcAreaTargetSnapshot("area.resisted", false, 100, 100, 100, 0) },
                fullyResistedAreaRng);
            Require(fullyResistedArea.EligibleTargetCount == 0
                && fullyResistedArea.SharedBaseMagnitude is null
                && fullyResistedAreaRng.Calls == 1
                && fullyResistedArea.Targets[0].ResistanceRoll == 1,
                "npc-native-area-no-magnitude-draw-when-all-targets-resist");
            report.Checks.Add("npc-native-area-no-magnitude-draw-when-all-targets-resist");
            var unresistedAreaRng = new CountingRangeRandom();
            var unresistedArea = RuntimeLiveGameplayFacade.ResolveClassicNpcAreaRollPlan(
                2, 4, 10, nativeAreaCandidates, unresistedAreaRng);
            Require(unresistedArea.EligibleTargetCount == 2
                && unresistedArea.SharedBaseMagnitude == 4
                && unresistedAreaRng.Calls == 3,
                "npc-native-area-target-resistance-rolls-before-one-magnitude-roll");
            report.Checks.Add("npc-native-area-target-resistance-rolls-before-one-magnitude-roll");
            Require(RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(10, 3, 0) == 3
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(10, 3, 3) == 3
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(10, 3, 9) == 3
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(-10, 3, 0) == -3
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(10, 3, 11) == 10
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(10, 3, 12) == 10
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(10, 3, 13) == 10,
                "npc-native-area-accepted-target-divisor-and-type11-13-exceptions");
            report.Checks.Add("npc-native-area-accepted-target-divisor-and-type11-13-exceptions");
            // SpellType=0 is not the native divisor selector. Original +0xCC
            // is TargetType: plague 446 is full-area 11; poison gas 1304 is
            // hostile-area 12. Neither divides the cast's base magnitude.
            Require(plagueSpell.SpellTypeRaw == 0 && plagueSpell.TargetTypeRaw == 11
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(
                    10, 3, plagueSpell.TargetTypeRaw) == 10
                && nativeSpells.Require("spell.1304").TargetType == "12"
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(10, 3, 12) == 10,
                "npc-native-area-target-type-not-spell-type-divisor-provenance");
            report.Checks.Add("npc-native-area-target-type-not-spell-type-divisor-provenance");
            // Native _monster_cast_area ability17 at 0x426FCE adjusts the
            // elemental result with a different MR formula from the user-cast
            // handler. In particular, no Anti-Magic below 50 MR amplifies
            // damage, whereas Anti-Magic uses capped MR/2 reduction.
            Require(RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 0, false) == 150
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 50, false) == 100
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 51, false) == 99
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 100, false) == 75
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 200, false) == 50
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 0, true) == 100
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 100, true) == 50
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(100, 200, true) == 25
                && RuntimeLiveGameplayFacade.ResolveClassicNpcAreaDamageMinusMr(-100, 0, false) == -150,
                "npc-native-area-damage-minus-mr-antimagic-caps-and-signed-truncation");
            report.Checks.Add("npc-native-area-damage-minus-mr-antimagic-caps-and-signed-truncation");
            var nativeAreaMinusMrFixture = plagueSpell! with
            {
                SpellTypeRaw = 0, TargetTypeRaw = 12, AttackTypeRaw = 4,
                MinimumEffect = 10, MaximumEffect = 50,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(10, 50)
            };
            var noExtraMinusMrRng = new CountingRangeRandom();
            var nativeAreaMinusMr = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                nativeAreaMinusMrFixture, 0, false, noExtraMinusMrRng,
                nativePreRolledMagnitude: 10, nativeAreaMagnitudeScaled: true,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAreaDamageMinusMr17: true,
                nativeTargetMagicResistance: 100);
            var nativeAreaAntiMagic = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                nativeAreaMinusMrFixture, 0, false, noExtraMinusMrRng,
                nativePreRolledMagnitude: 10, nativeAreaMagnitudeScaled: true,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAreaDamageMinusMr17: true,
                nativeTargetMagicResistance: 100, nativeTargetHasAntiMagic: true);
            Require(nativeAreaMinusMr.Executable && nativeAreaMinusMr.DirectDamage == 8
                && nativeAreaAntiMagic.Executable && nativeAreaAntiMagic.DirectDamage == 5
                && noExtraMinusMrRng.Calls == 0,
                "npc-native-area-ability17-damage-uses-one-shared-roll-and-target-mr");
            report.Checks.Add("npc-native-area-ability17-damage-uses-one-shared-roll-and-target-mr");
            Require(liveStatusCatalog.TryGet("spell.805", out var acidTempestStatus)
                    && acidTempestStatus.SpellTypeRaw == 0
                    && acidTempestStatus.AttackTypeRaw == 5
                    && acidTempestStatus.Abilities.Any(ability => ability.AbilityId == 17),
                "npc-native-area-acid-tempest-uses-attack-element5-water");
            report.Checks.Add("npc-native-area-acid-tempest-uses-attack-element5-water");
            var attackFiveFixture = plagueSpell! with
            {
                SpellTypeRaw = 0, TargetTypeRaw = 12, AttackTypeRaw = 5,
                MinimumEffect = 100, MaximumEffect = 100,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(100, 100)
            };
            var attackFiveRng = new CountingRangeRandom();
            var nativeWaterResistance = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                attackFiveFixture, 50, false, attackFiveRng,
                nativePreRolledMagnitude: 100, nativeAreaMagnitudeScaled: true,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAttackElement: true);
            var customAcidResistance = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                attackFiveFixture, 50, false, attackFiveRng,
                nativePreRolledMagnitude: 100, nativeAreaMagnitudeScaled: true);
            Require(nativeWaterResistance.Element == "water"
                && nativeWaterResistance.DirectDamage == 50
                && customAcidResistance.Element == "acid"
                && customAcidResistance.DirectDamage == 100
                && attackFiveRng.Calls == 0,
                "npc-native-water-defense-scales-acid-attack-without-changing-custom");
            report.Checks.Add("npc-native-water-defense-scales-acid-attack-without-changing-custom");
            Require(liveStatusCatalog.TryGet("spell.139", out var acidRainStatus)
                    && acidRainStatus.SpellTypeRaw == 3
                    && acidRainStatus.Abilities.Any(ability => ability.AbilityId == 1)
                    && liveStatusCatalog.TryGet("spell.751", out var demonExplosionStatus)
                    && demonExplosionStatus.SpellTypeRaw == 3
                    && demonExplosionStatus.Abilities.Any(ability => ability.AbilityId == 17),
                "npc-native-type3-area-ability1-and-17-catalog-owners");
            report.Checks.Add("npc-native-type3-area-ability1-and-17-catalog-owners");
            var type3AreaDamageFixture = plagueSpell! with
            {
                SpellTypeRaw = 3, TargetTypeRaw = 12, AttackTypeRaw = 5,
                MinimumEffect = 1, MaximumEffect = 1,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(1, 1)
            };
            var type3AreaRng = new CountingRangeRandom();
            var nativeAcidRainDamage = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                type3AreaDamageFixture, 100, false, type3AreaRng,
                nativePreRolledMagnitude: 1, nativeAreaMagnitudeScaled: true,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAreaDamageAbility1: true);
            var customAcidRainDamage = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                type3AreaDamageFixture, 100, false, type3AreaRng);
            Require(nativeAcidRainDamage.Executable && nativeAcidRainDamage.DirectDamage == 1
                && !customAcidRainDamage.Executable && type3AreaRng.Calls == 0,
                "npc-type3-area-ability1-not-falsely-immunized-by-elemental-resistance");
            report.Checks.Add("npc-type3-area-ability1-not-falsely-immunized-by-elemental-resistance");
            var type3AreaMinusMrFixture = type3AreaDamageFixture with
            {
                AttackTypeRaw = 1, MinimumEffect = 100, MaximumEffect = 100,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(100, 100)
            };
            var nativeDemonExplosionDamage = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                type3AreaMinusMrFixture, 100, false, type3AreaRng,
                nativePreRolledMagnitude: 100, nativeAreaMagnitudeScaled: true,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAreaDamageMinusMr17: true,
                nativeTargetMagicResistance: 100);
            Require(nativeDemonExplosionDamage.Executable
                && nativeDemonExplosionDamage.DirectDamage == 75 && type3AreaRng.Calls == 0,
                "npc-type3-area-ability17-mr-adjusts-untyped-native-magnitude");
            report.Checks.Add("npc-type3-area-ability17-mr-adjusts-untyped-native-magnitude");
            // Once the room admission prepass rolls a single base magnitude,
            // each accepted Classic recipient consumes the pre-scaled value
            // without a second damage or status RNG draw.
            var areaScaledFixture = areaHostileSpell with
            {
                SpellTypeRaw = 0, TargetTypeRaw = 8,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(4, 10)
            };
            var areaPerTargetMagnitude = RuntimeLiveGameplayFacade.ResolveClassicNpcAreaPerTargetBaseMagnitude(
                4, 2, areaScaledFixture.TargetTypeRaw);
            var noExtraAreaRng = new CountingRangeRandom();
            var areaFirst = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                areaScaledFixture, 0, false, noExtraAreaRng,
                areaPerTargetMagnitude, nativeAreaMagnitudeScaled: true);
            var areaSecond = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                areaScaledFixture, 0, false, noExtraAreaRng,
                areaPerTargetMagnitude, nativeAreaMagnitudeScaled: true);
            var areaStatusRoll = RuntimeLiveGameplayFacade.ResolveNpcStatusEffectMagnitude(
                4, 10, areaPerTargetMagnitude, noExtraAreaRng,
                classicWorld: true, nativeAreaMagnitudeScaled: true);
            Require(areaFirst.Executable && areaSecond.Executable
                && areaPerTargetMagnitude == 2
                && areaFirst.DirectDamage == 2 && areaSecond.DirectDamage == 2
                && areaStatusRoll == 2 && noExtraAreaRng.Calls == 0,
                "npc-native-area-scaled-damage-and-status-reuse-no-per-recipient-draw");
            report.Checks.Add("npc-native-area-scaled-damage-and-status-reuse-no-per-recipient-draw");
            Require(RuntimeLivingWorldService.ClassicNativeSpellSummonAdmitted(0, 0, 0)
                && RuntimeLivingWorldService.ClassicNativeSpellSummonAdmitted(14, 999, 0)
                && !RuntimeLivingWorldService.ClassicNativeSpellSummonAdmitted(15, 0, 0)
                && RuntimeLivingWorldService.ClassicNativeSpellSummonAdmitted(0, 1, 2)
                && !RuntimeLivingWorldService.ClassicNativeSpellSummonAdmitted(0, 2, 2)
                && !RuntimeLivingWorldService.ClassicNativeSpellSummonAdmitted(15, 1, 2),
                "npc-native-spell-summon-15-room-slots-and-definition-game-limit");
            report.Checks.Add("npc-native-spell-summon-15-room-slots-and-definition-game-limit");
            var nativeSlotFixture = new[]
            {
                new RuntimeClassicLiveStatusAbility(122, "RemovesSpell", 0),
                new RuntimeClassicLiveStatusAbility(7, "DamageResistance", -5),
                new RuntimeClassicLiveStatusAbility(122, "RemovesSpell", 31),
                new RuntimeClassicLiveStatusAbility(122, "RemovesSpell", 0)
            };
            var resolvedNativeSlots = RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(nativeSlotFixture, 19);
            Require(resolvedNativeSlots.Count == 4
                && resolvedNativeSlots.Select(slot => slot.AbilityId).SequenceEqual(new[] { 122, 7, 122, 122 })
                && resolvedNativeSlots.Select(slot => slot.PopulatedOrdinal).SequenceEqual(new[] { 0, 1, 2, 3 })
                && resolvedNativeSlots.Select(slot => slot.RawValue).SequenceEqual(new[] { 0, -5, 31, 0 })
                && resolvedNativeSlots.Select(slot => slot.EffectiveValue).SequenceEqual(new[] { 19, -5, 31, 19 }),
                "npc-native-ordered-repeated-slots-and-zero-inherits-base-magnitude");
            report.Checks.Add("npc-native-ordered-repeated-slots-and-zero-inherits-base-magnitude");
            Require(liveStatusCatalog.TryGet("spell.1062", out var barbskinSelf)
                    && barbskinSelf.Abilities.Count(ability => ability.AbilityId == 122) == 4,
                "npc-self-barbskin-four-repeated-removespell-slots");
            report.Checks.Add("npc-self-barbskin-four-repeated-removespell-slots");
            var barbskinOrderedSlots = RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                barbskinSelf.Abilities, 19);
            var barbskinModifiers = RuntimeLiveGameplayFacade.ResolveClassicNpcTimedModifierProjection(
                barbskinOrderedSlots);
            Require(barbskinOrderedSlots.Count == barbskinSelf.Abilities.Count
                && barbskinModifiers.Count == 3
                && barbskinModifiers[7] == 50
                && barbskinModifiers[2] == 10
                && barbskinModifiers[72] == 19
                && !barbskinModifiers.ContainsKey(122)
                && !barbskinModifiers.ContainsKey(115)
                && !barbskinModifiers.ContainsKey(137),
                "npc-classic-self-timed-buff-excludes-ordered-removal-and-message-commands");
            report.Checks.Add("npc-classic-self-timed-buff-excludes-ordered-removal-and-message-commands");
            Require(liveStatusCatalog.TryGet("spell.1220", out var nativePicklockSelf)
                    && nativePicklockSelf.Abilities.Count == 1
                    && nativePicklockSelf.Abilities[0].AbilityId == 37
                    && RuntimeLiveGameplayFacade.ResolveClassicNpcTimedModifierProjection(
                        RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                            nativePicklockSelf.Abilities, 200)).Count == 0,
                "npc-classic-scripted-self-skill-not-fabricated-as-combat-stat");
            report.Checks.Add("npc-classic-scripted-self-skill-not-fabricated-as-combat-stat");
            Require(liveStatusCatalog.TryGet("spell.1347", out var allBallSummon)
                    && liveStatusCatalog.TryGet("spell.1386", out var duergarSummon)
                    && liveStatusCatalog.TryGet("spell.465", out var chestItemsTextblocks)
                    && liveStatusCatalog.TryGet("spell.997", out var blackwoodOpenTextblock),
                "npc-native-special-slot-catalog-vectors-exist");
            report.Checks.Add("npc-native-special-slot-catalog-vectors-exist");
            var fourSummons = RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(allBallSummon.Abilities, 0), 2);
            var threeZeroSummons = RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(duergarSummon.Abilities, 1112), 8);
            var deniedAreaSummons = RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(allBallSummon.Abilities, 0), 1);
            Require(fourSummons.Count == 4
                && fourSummons.Select(slot => slot.EffectiveId)
                    .SequenceEqual(new[] { 1080, 1081, 1082, 1083 })
                && fourSummons.All(slot => slot.AbilityId == 12
                    && slot.NativeTargetAdmitted && slot.ResolvedId)
                && threeZeroSummons.Count == 3
                && threeZeroSummons.All(slot => slot.RawValue == 0
                    && slot.EffectiveId == 1112 && slot.ResolvedId)
                && deniedAreaSummons.All(slot => !slot.NativeTargetAdmitted)
                && new[] { 0, 2, 6, 8 }.All(nativeType =>
                    RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                        RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                            duergarSummon.Abilities, 1112), nativeType)
                        .All(slot => slot.NativeTargetAdmitted))
                && new[] { 1, 11, 12, 13 }.All(nativeType =>
                    RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                        RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                            duergarSummon.Abilities, 1112), nativeType)
                        .All(slot => !slot.NativeTargetAdmitted)),
                "npc-native-summon-repeated-slots-zero-fallback-and-cc-target-gate");
            report.Checks.Add("npc-native-summon-repeated-slots-zero-fallback-and-cc-target-gate");
            Require(liveStatusCatalog.TryGet("spell.458", out var nativeInstantHeal)
                    && nativeInstantHeal.DurationRounds == 0
                    && nativeInstantHeal.Abilities.Single().AbilityId == 18
                    && nativeInstantHeal.Abilities.Single().Value == 0
                    && liveStatusCatalog.TryGet("spell.459", out var nativeInstantMana)
                    && nativeInstantMana.DurationRounds == 0
                    && nativeInstantMana.Abilities.Single().AbilityId == 150
                    && nativeInstantMana.Abilities.Single().Value == 0
                    && liveStatusCatalog.TryGet("spell.1164", out var nativeInstantHpMana)
                    && nativeInstantHpMana.DurationRounds == 0
                    && nativeInstantHpMana.Abilities.Select(ability => ability.AbilityId)
                        .SequenceEqual(new[] { 18, 150 }),
                "npc-native-instant-self-hp-mana-ordered-handler-vectors");
            report.Checks.Add("npc-native-instant-self-hp-mana-ordered-handler-vectors");
            var instantSelfMagnitude = RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                nativeInstantHpMana.Abilities, 150);
            Require(instantSelfMagnitude.Count == 2
                && instantSelfMagnitude[0].EffectiveValue == 150
                && instantSelfMagnitude[1].EffectiveValue == 150
                && instantSelfMagnitude[0].AbilityId == 18
                && instantSelfMagnitude[1].AbilityId == 150,
                "npc-native-instant-self-hp-then-mana-one-base-roll");
            report.Checks.Add("npc-native-instant-self-hp-then-mana-one-base-roll");
            var nativeSelfSummons = RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                    duergarSummon.Abilities, 1112), 1, nativeAreaDispatcher: true);
            Require(nativeSelfSummons.Count == 3
                && nativeSelfSummons.All(slot =>
                    slot.AbilityId == 12 && slot.NativeTargetAdmitted && slot.EffectiveId == 1112)
                && new[] { 1, 2, 6 }.All(target =>
                    RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                        RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                            allBallSummon.Abilities, 0), target, nativeAreaDispatcher: true)
                        .Where(slot => slot.AbilityId == 12)
                        .All(slot => slot.NativeTargetAdmitted))
                && new[] { 0, 8, 11, 12 }.All(target =>
                    RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                        RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                            allBallSummon.Abilities, 0), target, nativeAreaDispatcher: true)
                        .Where(slot => slot.AbilityId == 12)
                        .All(slot => !slot.NativeTargetAdmitted)),
                "npc-native-area-self-summon-target-gate-from-original-dll");
            report.Checks.Add("npc-native-area-self-summon-target-gate-from-original-dll");
            var repeatedChestCommands = RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(chestItemsTextblocks.Abilities, 0), 1);
            var fallbackBlackwoodCommand = RuntimeLiveGameplayFacade.ResolveClassicNativeSpecialSpellSlots(
                RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(blackwoodOpenTextblock.Abilities, 9384), 1);
            Require(repeatedChestCommands.Count == 8
                && repeatedChestCommands.Select(slot => slot.PopulatedOrdinal)
                    .SequenceEqual(Enumerable.Range(0, 8))
                && repeatedChestCommands.All(slot => slot.AbilityId == 148
                    && slot.EffectiveId == 880 && slot.ResolvedId)
                && fallbackBlackwoodCommand.Count == 1
                && fallbackBlackwoodCommand[0].RawValue == 0
                && fallbackBlackwoodCommand[0].EffectiveId == 9384
                && fallbackBlackwoodCommand[0].NativeTargetAdmitted,
                "npc-native-textblock-repeated-commands-and-zero-fallback-in-order");
            report.Checks.Add("npc-native-textblock-repeated-commands-and-zero-fallback-in-order");
            var fixedSelfSlots = RuntimeLiveGameplayFacade.ResolveClassicNpcEffectSlots(
                blacknightStatus.Abilities, 13);
            Require(fixedSelfSlots.Count == 1
                && fixedSelfSlots[0].AbilityId == 37 && fixedSelfSlots[0].RawValue == 200
                && fixedSelfSlots[0].EffectiveValue == 200,
                "npc-blacknight-fixed-slot-overrides-base-magnitude");
            report.Checks.Add("npc-blacknight-fixed-slot-overrides-base-magnitude");
            var npcStatusRoot = Path.Combine(Path.GetTempPath(), "reforged-npc-status-" + Guid.NewGuid().ToString("N"));
            try
            {
                var livingStatusAudit = new RuntimeLivingWorldService(projectRoot, classicWorld, npcStatusRoot);
                var hoodedMan = livingStatusAudit.Capture().Npcs.Single(living => living.NpcId.Equals("npc.1000", StringComparison.OrdinalIgnoreCase));
                var statusNow = new DateTimeOffset(2026, 8, 9, 13, 0, 0, TimeSpan.Zero);
                // Exercise the actual living-world NPC poison path, not just
                // the scalar helper. The same slow instant must not damage twice.
                if (hoodedMan.CurrentHitPoints > 1)
                {
                    var poisonedMetadata = RuntimeClassicNativePoisonAuthority.SetMonster(
                        hoodedMan.Metadata, 1);
                    Require(livingStatusAudit.ReplaceNpcMetadata(
                            hoodedMan.InstanceId, poisonedMetadata),
                        "native-monster-poison-metadata-persisted");
                    report.Checks.Add("native-monster-poison-metadata-persisted");
                    var poisonedCount = livingStatusAudit.TickNativeClassicMonsterPoison(statusNow);
                    var afterPoison = livingStatusAudit.Capture().Npcs.Single(npc =>
                        npc.InstanceId.Equals(hoodedMan.InstanceId, StringComparison.OrdinalIgnoreCase));
                    Require(poisonedCount == 1
                        && afterPoison.CurrentHitPoints == hoodedMan.CurrentHitPoints - 1
                        && livingStatusAudit.TickNativeClassicMonsterPoison(statusNow) == 0
                        && livingStatusAudit.Capture().Npcs.Single(npc =>
                            npc.InstanceId.Equals(hoodedMan.InstanceId, StringComparison.OrdinalIgnoreCase))
                            .CurrentHitPoints == afterPoison.CurrentHitPoints,
                        "native-monster-poison-30-second-slow-tick-once");
                    report.Checks.Add("native-monster-poison-30-second-slow-tick-once");
                    Require(livingStatusAudit.ApplyNpcHealing(hoodedMan.InstanceId, 1, statusNow),
                        "native-monster-poison-audit-restore-health");
                    report.Checks.Add("native-monster-poison-audit-restore-health");
                }
                var appliedStatus = livingStatusAudit.ApplyNpcTimedStatus(
                    hoodedMan.InstanceId, blacknightStatus.SpellId, blacknightStatus.DurationRounds,
                    blacknightStatus.Abilities.ToDictionary(ability => ability.AbilityId, ability => ability.Value), statusNow);
                var blacknightDurationSeconds = checked(blacknightStatus.DurationRounds * RuntimeClassicLiveStatusCatalog.SpellRoundSeconds);
                Require(appliedStatus.Applied && appliedStatus.ExpiresAtUtc == statusNow.AddSeconds(blacknightDurationSeconds) &&
                    livingStatusAudit.ResolveNpcTimedAbilityModifier(hoodedMan.InstanceId, 37, statusNow.AddSeconds(blacknightDurationSeconds - 1)) == 200,
                    "npc-blacknight-self-status-persisted"); report.Checks.Add("npc-blacknight-self-status-persisted");
                Require(livingStatusAudit.Tick(statusNow.AddSeconds(blacknightDurationSeconds)).Contains($"npc-status-expired:{hoodedMan.InstanceId}:spell.1220") &&
                    livingStatusAudit.ResolveNpcTimedAbilityModifier(hoodedMan.InstanceId, 37, statusNow.AddSeconds(blacknightDurationSeconds)) == 0,
                    "npc-blacknight-self-status-expiration"); report.Checks.Add("npc-blacknight-self-status-expiration");
                _ = livingStatusAudit.ApplyNpcTimedStatus(
                    hoodedMan.InstanceId, blacknightStatus.SpellId, blacknightStatus.DurationRounds,
                    blacknightStatus.Abilities.ToDictionary(ability => ability.AbilityId, ability => ability.Value), statusNow.AddMinutes(2));
                Require(livingStatusAudit.ApplyNpcDamage(hoodedMan.InstanceId, hoodedMan.MaximumHitPoints, statusNow.AddMinutes(2)) &&
                    livingStatusAudit.ResolveNpcTimedAbilityModifier(hoodedMan.InstanceId, 37, statusNow.AddMinutes(2)) == 0
                    && !livingStatusAudit.Capture().Npcs.Single(npc =>
                        npc.InstanceId.Equals(hoodedMan.InstanceId, StringComparison.OrdinalIgnoreCase))
                        .Metadata.ContainsKey(RuntimeClassicNativePoisonAuthority.MonsterSeverityKey),
                    "npc-blacknight-self-status-cleared-on-death"); report.Checks.Add("npc-blacknight-self-status-cleared-on-death");
            }
            finally
            {
                if (Directory.Exists(npcStatusRoot)) Directory.Delete(npcStatusRoot, true);
            }
            Require(selfTargetSpells.All(row => !RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectForAudit(
                    row.Spell!, row.Spell!.EffectRange.RollMinimum).Executable),
                "npc-self-spells-player-direct-effect-quarantined"); report.Checks.Add("npc-self-spells-excluded-from-player-direct-effects");
            var directFixedRng = new CountingRangeRandom();
            var fixedDirect = immediateDamageSpell! with
            {
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(7, 7)
            };
            var directFixedResult = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                fixedDirect, 0, false, directFixedRng);
            Require(directFixedResult.Executable && directFixedResult.Roll == 7
                && directFixedRng.Calls == 1
                && directFixedRng.LastMinimum == 7 && directFixedRng.LastExclusiveMaximum == 8,
                "npc-native-fixed-damage-range-consumes-one-draw");
            report.Checks.Add("npc-native-fixed-damage-range-consumes-one-draw");
            // Native per-slot signed Int16 values override the one base
            // magnitude without rerolling. A zero slot inherits the roll.
            // The same native negative value heals through the existing
            // signed hit-point consumer, even when the authored range is
            // entirely negative or crosses zero.
            var mixedRawFixture = immediateDamageSpell! with
            {
                SpellTypeRaw = 0, TargetTypeRaw = 8, AttackTypeRaw = 4,
                MinimumEffect = -10, MaximumEffect = 20,
                EffectRange = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectRange(-10, 20)
            };
            var mixedPositiveRng = new CountingRangeRandom();
            var mixedPositive = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                mixedRawFixture, 0, false, mixedPositiveRng,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAttackElement: true,
                classicNativeDirectSlotRaw: 5);
            var mixedNegativeRng = new CountingRangeRandom();
            var mixedNegative = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                mixedRawFixture, 0, false, mixedNegativeRng,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAttackElement: true,
                classicNativeDirectSlotRaw: -7);
            var nativeZeroRng = new CountingRangeRandom();
            var mixedFallback = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                mixedRawFixture, 0, false, nativeZeroRng,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAttackElement: true,
                classicNativeDirectSlotRaw: 0);
            var customMixedRng = new CountingRangeRandom();
            var customMixed = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                mixedRawFixture, 0, false, customMixedRng);
            Require(mixedPositive.Executable && mixedPositive.Roll == -10
                && mixedPositive.RawDirectDamage == 5 && mixedPositive.DirectDamage == 5
                && mixedNegative.Executable && mixedNegative.Roll == -10
                && mixedNegative.RawDirectDamage == -7 && mixedNegative.DirectDamage == -7
                && mixedFallback.Executable && mixedFallback.DirectDamage == -10
                && !customMixed.Executable
                && mixedPositiveRng.Calls == 1 && mixedNegativeRng.Calls == 1
                && nativeZeroRng.Calls == 1 && customMixedRng.Calls == 0,
                "npc-native-signed-slot-overrides-base-roll-and-mixed-custom-isolation");
            report.Checks.Add("npc-native-signed-slot-overrides-base-roll-and-mixed-custom-isolation");
            var nativeWaterNegativeRng = new CountingRangeRandom();
            var negativeWaterFixture = mixedRawFixture with { AttackTypeRaw = 5 };
            var nativeWaterNegative = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                negativeWaterFixture, 50, false, nativeWaterNegativeRng,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAttackElement: true,
                classicNativeDirectSlotRaw: -20);
            var nativeWaterOverresist = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffect(
                negativeWaterFixture, 150, false, nativeWaterNegativeRng,
                classicNativeHasDirectHitPointHandler: true,
                classicNativeAttackElement: true,
                classicNativeDirectSlotRaw: -20);
            Require(nativeWaterNegative.Element == "water" && nativeWaterNegative.DirectDamage == -10
                && nativeWaterOverresist.DirectDamage == 10
                && nativeWaterNegativeRng.Calls == 2,
                "npc-native-negative-water-damage-and-overresistance-inversion");
            report.Checks.Add("npc-native-negative-water-damage-and-overresistance-inversion");
            var immediateEffect = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectForAudit(immediateDamageSpell!, immediateDamageSpell!.EffectRange.RollMinimum);
            Require(immediateEffect.Executable && immediateEffect.DirectDamage == immediateDamageSpell.EffectRange.RollMinimum, "npc-hit-spell-immediate-damage-live-consumer"); report.Checks.Add("npc-hit-spell-immediate-damage-live-consumer");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "fire", 25, false) == 75, "npc-hit-spell-elemental-resistance-reduction"); report.Checks.Add("npc-hit-spell-elemental-resistance-reduction");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "cold", -50, false) == 150, "npc-hit-spell-elemental-vulnerability-increase"); report.Checks.Add("npc-hit-spell-elemental-vulnerability-increase");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "stone", 100, false) == 0, "npc-hit-spell-elemental-immunity-threshold"); report.Checks.Add("npc-hit-spell-elemental-immunity-threshold");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "fire", 125, false) == -25, "npc-hit-spell-overcap-resistance-partial-healing"); report.Checks.Add("npc-hit-spell-overcap-resistance-partial-healing");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "lightning", 200, false) == -100, "npc-hit-spell-overcap-resistance-full-healing"); report.Checks.Add("npc-hit-spell-overcap-resistance-full-healing");
            var elementalHealing = RuntimeLiveGameplayFacade.ApplyElementalSpellHitPoints(40, 100, -100);
            Require(elementalHealing.HitPointsAfter == 100 && elementalHealing.DamageApplied == 0 && elementalHealing.HealingApplied == 60, "npc-hit-spell-overcap-healing-capped-at-maximum"); report.Checks.Add("npc-hit-spell-overcap-healing-capped-at-maximum");
            var elementalDamage = RuntimeLiveGameplayFacade.ApplyElementalSpellHitPoints(40, 100, 25);
            Require(elementalDamage.HitPointsAfter == 15 && elementalDamage.DamageApplied == 25 && elementalDamage.HealingApplied == 0, "npc-hit-spell-signed-damage-consumer"); report.Checks.Add("npc-hit-spell-signed-damage-consumer");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "poison", 0, true) == 0, "npc-hit-spell-poison-immunity"); report.Checks.Add("npc-hit-spell-poison-immunity");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "acid", 75, false) == 100, "npc-hit-spell-acid-unmapped-resistance-fallback"); report.Checks.Add("npc-hit-spell-acid-unmapped-resistance-fallback");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "untyped", 75, false) == 100, "npc-hit-spell-untyped-resistance-bypass"); report.Checks.Add("npc-hit-spell-untyped-resistance-bypass");
            var lifecycleSpell = immediateDamageSpell with { SpellTypeRaw = 3, Classification = RuntimeLiveGameplayFacade.ClassifyNpcHitSpell(3, immediateDamageSpell.TargetTypeRaw, immediateDamageSpell.AttackTypeRaw, immediateDamageSpell.ResistTypeRaw) };
            Require(!RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectForAudit(lifecycleSpell, lifecycleSpell.EffectRange.RollMinimum).Executable, "npc-hit-spell-status-effect-not-direct-damage"); report.Checks.Add("npc-hit-spell-status-effect-not-direct-damage");
            RuntimeNpcHitSpellSelection? poisonStatusSpell = null;
            RuntimeNpcHitSpellSelection? generalStatusSpell = null;
            foreach (var candidate in npcs.SelectMany(x => x.Attacks).Where(x => x.HitSpellLegacyId > 0))
            {
                var resolved = RuntimeLiveGameplayFacade.ResolveNpcHitSpell(candidate, new ClassicAttackResult(ClassicHitOutcome.Hit, 0, 0, 1, 1, 1, false), projectRoot);
                if (resolved is not { Triggered: true } || resolved.DurationRaw <= 0 || resolved.TargetTypeRaw is not (0 or 8 or 11 or 12)) continue;
                if (resolved.SpellTypeRaw == 1 && poisonStatusSpell is null) poisonStatusSpell = resolved;
                if (resolved.SpellTypeRaw == 3 && generalStatusSpell is null) generalStatusSpell = resolved;
                if (poisonStatusSpell is not null && generalStatusSpell is not null) break;
            }
            Require(poisonStatusSpell is not null, "generated-npc-poison-status-delivery"); report.Checks.Add("generated-npc-poison-status-delivery");
            var poisonDelivery = RuntimeLiveGameplayFacade.ResolveNpcHitSpellStatusDelivery(poisonStatusSpell!, false, false);
            Require(poisonDelivery.Eligible && poisonDelivery.Applied && poisonDelivery.EffectKind == "poison", "npc-hit-spell-poison-status-live-consumer"); report.Checks.Add("npc-hit-spell-poison-status-live-consumer");
            var poisonImmuneDelivery = RuntimeLiveGameplayFacade.ResolveNpcHitSpellStatusDelivery(poisonStatusSpell!, true, false);
            Require(poisonImmuneDelivery.Eligible && !poisonImmuneDelivery.Applied && poisonImmuneDelivery.PoisonImmune, "npc-hit-spell-poison-status-immunity"); report.Checks.Add("npc-hit-spell-poison-status-immunity");
            var duplicatePoison = RuntimeLiveGameplayFacade.ResolveNpcHitSpellStatusDelivery(poisonStatusSpell!, false, true);
            Require(duplicatePoison.Eligible && duplicatePoison.Applied && duplicatePoison.Reason == "refresh-existing-effect", "npc-hit-spell-status-refresh-same-spell"); report.Checks.Add("npc-hit-spell-status-refresh-same-spell");
            Require(generalStatusSpell is not null, "generated-npc-general-status-delivery"); report.Checks.Add("generated-npc-general-status-delivery");
            var generalDelivery = RuntimeLiveGameplayFacade.ResolveNpcHitSpellStatusDelivery(generalStatusSpell!, false, false);
            Require(generalDelivery.Eligible && generalDelivery.Applied && generalDelivery.EffectKind == "status-or-scripted", "npc-hit-spell-general-status-live-consumer"); report.Checks.Add("npc-hit-spell-general-status-live-consumer");
            var skippedRng = new Random(1042);
            var untouchedRng = new Random(1042);
            var noRoll = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistanceWithAdmission(
                0, 40, 60, 7, 0, skippedRng);
            Require(!noRoll.CheckEligible && noRoll.Roll == 0
                && skippedRng.Next(1, 101) == untouchedRng.Next(1, 101),
                "npc-unresistable-spell-does-not-consume-resistance-rng");
            report.Checks.Add("npc-unresistable-spell-does-not-consume-resistance-rng");
            var absentAntiMagicRng = new Random(1042);
            var untouchedAntiMagicRng = new Random(1042);
            var noAntiMagicRoll = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistanceWithAdmission(
                1, 40, 60, 7, 0, absentAntiMagicRng);
            Require(!noAntiMagicRoll.CheckEligible && noAntiMagicRoll.Roll == 0
                && absentAntiMagicRng.Next(1, 101) == untouchedAntiMagicRng.Next(1, 101),
                "npc-antimagic-required-spell-does-not-consume-ineligible-rng");
            report.Checks.Add("npc-antimagic-required-spell-does-not-consume-ineligible-rng");
            var normalRng = new Random(1042);
            var expectedRng = new Random(1042);
            var expectedRoll = expectedRng.Next(1, 100);
            var admittedRoll = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistanceWithAdmission(
                2, 40, 60, 7, 0, normalRng);
            Require(admittedRoll.CheckEligible && admittedRoll.Roll is >= 1 and <= 99
                && admittedRoll.Roll == expectedRoll
                && normalRng.Next(1, 100) == expectedRng.Next(1, 100),
                "npc-eligible-spell-consumes-exactly-one-resistance-rng");
            report.Checks.Add("npc-eligible-spell-consumes-exactly-one-resistance-rng");
            var maximumNativeRoll = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 40, 60, 7, 0, 99);
            Require(maximumNativeRoll.CheckEligible && maximumNativeRoll.Roll == 99
                && !maximumNativeRoll.Resisted, "npc-native-resistance-99-valid-not-forced-resist");
            report.Checks.Add("npc-native-resistance-99-valid-not-forced-resist");
            var highMrRoll97 = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 100, 100, 100, 0, 97);
            var highMrRoll98 = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 100, 100, 100, 0, 98);
            var highMrRoll99 = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 100, 100, 100, 0, 99);
            Require(highMrRoll97.ResistanceChancePercent == 97 && highMrRoll97.Resisted
                && !highMrRoll98.Resisted && !highMrRoll99.Resisted,
                "npc-native-97-cap-inclusive-resist-threshold");
            report.Checks.Add("npc-native-97-cap-inclusive-resist-threshold");
            var mrNormal = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 40, 60, 7, 0, 24);
            Require(mrNormal.MagicResistanceScore == 52 && mrNormal.ResistanceChancePercent == 26 && mrNormal.Resisted, "npc-hit-spell-mr-equation"); report.Checks.Add("npc-hit-spell-mr-equation");
            var mrMiss = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 40, 60, 7, 0, 27);
            Require(!mrMiss.Resisted, "npc-hit-spell-mr-inclusive-boundary"); report.Checks.Add("npc-hit-spell-mr-inclusive-boundary");
            var mrNever = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(0, 100, 100, 100, 10, 1);
            Require(!mrNever.CheckEligible && !mrNever.Resisted, "npc-hit-spell-never-resisted-gate"); report.Checks.Add("npc-hit-spell-never-resisted-gate");
            var mrAntiMagicBlocked = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(1, 100, 100, 100, 0, 1);
            Require(!mrAntiMagicBlocked.CheckEligible, "npc-hit-spell-antimagic-ability-gate"); report.Checks.Add("npc-hit-spell-antimagic-ability-gate");
            var mrAntiMagic = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(1, 100, 100, 100, 1, 97);
            Require(mrAntiMagic.CheckEligible && mrAntiMagic.ResistanceChancePercent == 97 && mrAntiMagic.Resisted, "npc-hit-spell-antimagic-live-check"); report.Checks.Add("npc-hit-spell-antimagic-live-check");
            var missedHitSpell = RuntimeLiveGameplayFacade.ResolveNpcHitSpell(hitSpellAttack!, new ClassicAttackResult(ClassicHitOutcome.Miss, 0, 0, 0, 0, 0, false), projectRoot);
            Require(missedHitSpell is { Triggered: false }, "npc-hit-spell-only-on-hit"); report.Checks.Add("npc-hit-spell-only-on-hit");
            Require(RuntimeCommand.Parse("round").Name == "round", "round-command-route"); report.Checks.Add("round-command-route");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.ExecuteLiveCombatRound)) is not null, "live-round-execution"); report.Checks.Add("live-round-execution");
            var deterministic = new ClassicCombatResolver(new FixedClassicRandom());
            var result = deterministic.Resolve(new ClassicAttackRequest(ClassicAttackType.NormalWeapon, new(100,0,0,0,0), new(0,0,0,0,0), 3,3));
            Require(result.FinalDamage == 3, "classic-resolver-connected"); report.Checks.Add("classic-resolver-connected");
            Require(ClassicCombatResolver.CalculateBaseDodgeChance(8, 100) == 0, "dodge-minimum-attacker-threshold"); report.Checks.Add("dodge-minimum-attacker-threshold");
            Require(ClassicCombatResolver.CalculateDodgeChance(ClassicAttackType.NormalWeapon, 16, 1000) == 95, "dodge-final-cap"); report.Checks.Add("dodge-final-cap");
            Require(ClassicCombatResolver.CalculateDodgeChance(ClassicAttackType.Backstab, 16, 1000) == 19, "backstab-dodge-reduction"); report.Checks.Add("backstab-dodge-reduction");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
    private sealed class FixedClassicRandom : IClassicRandom { public int NextInclusive(int minimum, int maximum) => minimum; }
    private sealed class CountingRangeRandom : Random
    {
        public int Calls { get; private set; }
        public int LastMinimum { get; private set; }
        public int LastExclusiveMaximum { get; private set; }
        public override int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue) throw new ArgumentOutOfRangeException(nameof(maxValue));
            Calls++;
            LastMinimum = minValue;
            LastExclusiveMaximum = maxValue;
            return minValue;
        }
    }
}
