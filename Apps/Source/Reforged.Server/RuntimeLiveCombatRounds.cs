using System.Text.Json;

namespace Reforged.Server;


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
            return $"{encounter.NpcName} is no longer here.";
        }
        encounter = encounter with { NpcCurrentHitPoints = liveNpc.CurrentHitPoints, RoomId = liveNpc.RoomId };

        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        var player = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot), _characters.ProjectRoot).Resolve(before);
        if (!player.Ready) return "Combat round blocked: " + string.Join(", ", player.Blockers);

        var npc = ResolveEncounterNpc(session, encounter);
        var npcDefense = RuntimeNpcDefensiveProfileResolver.Resolve(npc);
        var resolver = new ClassicCombatResolver(authority: RuntimeClassicFormulaAuthority.Load(_characters.ProjectRoot));

        var resolvedPlayerAttackValue = encounter.PlayerAttackType == ClassicAttackType.Backstab
            ? player.BackstabAttackValue
            : checked(player.AttackValue + player.ClassCombatAccuracy + player.AbilityAccuracyModifier + player.WeaponAccuracyModifier);
        var playerStats = new ClassicCombatantStats(
            resolvedPlayerAttackValue,
            player.DefenseValue,
            player.ArmorClass,
            player.DamageResistance,
            player.DodgeBonus,
            0,
            player.CriticalChance);
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
        var playerAttackSuppressed = RuntimePlayerCombatProfileResolver.IsPhysicalAttackSuppressed(before);
        var npcHp = encounter.NpcCurrentHitPoints;
        var playerEnergy = encounter.PlayerCurrentEnergy;
        var messages = new List<string>();
        if (playerAttackSuppressed)
        {
            messages.Add($"{actor.DisplayName} cannot act this round.");
        }
        else
        {
            var schedule = RuntimeClassicPhysicalFormulaCalculator.ResolvePhysicalAttackSchedule(
                encounter.PlayerAttackType, playerEnergy, player.AttackEnergyCost);
            playerEnergy = schedule.RemainingEnergy;

            if (schedule.Swings == 0)
                messages.Add($"{actor.DisplayName} is not ready to execute {encounter.PlayerAttackType} this round.");

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
                messages.Add(RenderAttack(actor.DisplayName, encounter.NpcName, playerResult));
            }
        }

        var playerHp = before.HitPoints;
        var npcEnergy = encounter.NpcCurrentEnergy;
        if (npcHp > 0 && IsSelectedNpcTarget(session, encounter))
        {
            var attacksThisRound = ResolveNpcAttacksPerRound(npc);
            for (var attackIndex = 0; attackIndex < attacksThisRound && playerHp > 0; attackIndex++)
            {
                var attack = SelectNpcAttack(encounter.NpcAttacks, npcEnergy);
                if (attack is null) break;
                npcEnergy = ResolveNpcEnergyAfterAttack(npcEnergy, attack);
                var attackType = ResolveAttackType(attack.Type);
                var npcAttacker = new ClassicCombatantStats(attack.Accuracy, 0, 0, 0, 0);
                var npcResult = resolver.Resolve(new ClassicAttackRequest(
                    attackType,
                    npcAttacker,
                    playerStats,
                    attack.MinimumDamage,
                    attack.MaximumDamage));
                playerHp = Math.Max(0, playerHp - npcResult.FinalDamage);
                messages.Add(RenderNpcAttack(encounter.NpcName, actor.DisplayName, attack, npcResult));
                var hitSpell = ResolveNpcHitSpell(attack, npcResult, _characters.ProjectRoot);
                if (hitSpell is { Triggered: true })
                {
                    var resistance = ResolveNpcHitSpellResistance(
                        hitSpell.ResistTypeRaw,
                        ReadBaseStat(before, "wil", "willpower", "will"),
                        ReadBaseStat(before, "int", "intellect", "intelligence"),
                        ReadMagicResistance(before),
                        ReadAbilityValue(before, 51),
                        Random.Shared.Next(1, 101));
                    if (resistance.Resisted)
                    {
                        npcEnergy = ResolveNpcEnergyAfterResistedHitSpell(npcEnergy, attack, encounter.NpcMaximumEnergy);
                        messages.Add($"{actor.DisplayName} resists {encounter.NpcName}'s {hitSpell.SpellName}.");
                    }
                    else
                    {
                        var effect = ResolveNpcHitSpellEffect(
                            hitSpell,
                            ReadElementalResistance(before, ResolveNpcHitSpellAttackElement(hitSpell.AttackTypeRaw)),
                            ReadAbilityValue(before, 21) > 0,
                            Random.Shared);
                        if (effect.Executable && effect.DirectDamage > 0)
                        {
                            playerHp = Math.Max(0, playerHp - effect.DirectDamage);
                            messages.Add($"{encounter.NpcName}'s {hitSpell.SpellName} hits {actor.DisplayName} for {effect.DirectDamage} damage.");
                        }
                        else
                        {
                            var delivery = ApplyNpcHitSpellStatusDelivery(
                                session,
                                before,
                                hitSpell,
                                $"npc.{npc.LegacyId}",
                                ReadAbilityValue(before, 21) > 0,
                                DateTimeOffset.UtcNow);
                            if (delivery.Applied)
                            {
                                before = _characters.LoadCharacter(actor.Id) ?? before;
                                messages.Add($"{encounter.NpcName}'s {hitSpell.SpellName} affects {actor.DisplayName} for {delivery.DurationSeconds} seconds.");
                            }
                            else if (delivery.PoisonImmune)
                                messages.Add($"{actor.DisplayName} is immune to {encounter.NpcName}'s {hitSpell.SpellName}.");
                            else
                                messages.Add($"{encounter.NpcName}'s hit invokes {hitSpell.SpellName} [{hitSpell.CanonicalSpellId}].");
                        }
                    }
                }
            }
        }
        else if (npcHp > 0)
        {
            messages.Add($"{encounter.NpcName} is focused on another combatant.");
        }

        try
        {
            if (npcHp == 0)
            {
                _liveCombats.TryRemove(session.Id, out _);
                messages.Add($"{encounter.NpcName} is defeated.");
                messages.Add(CompleteNpcDefeat(session, before, npc, playerHp));
            }
            else if (playerHp == 0)
            {
                _liveCombats.TryRemove(session.Id, out _);
                ReleaseCombatEngagement(session, encounter, "player-defeat");
                messages.Add($"{actor.DisplayName} is defeated.");
                messages.Add(CompletePlayerDefeat(session, before));
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
            _characters.SaveCharacter(before);
            throw;
        }

        foreach (var message in messages) _sessions.BroadcastRoom(session, message, includeSource: false);
        messages.Add($"HP: {playerHp}/{before.MaximumHitPoints}; {encounter.NpcName}: {npcHp}/{encounter.NpcMaximumHitPoints}");
        return string.Join(Environment.NewLine, messages);
    }

    internal static int ResolveNpcAttacksPerRound(RuntimeNpcDefinition npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (npc.AttackCount < 0)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_COUNT_NEGATIVE:{npc.Id}:{npc.AttackCount}");
        if (npc.AttackCount == 0)
            return 0;
        if (npc.Attacks.Count == 0)
            throw new InvalidDataException($"NPC_COMBAT_ATTACK_COUNT_WITHOUT_ROWS:{npc.Id}:{npc.AttackCount}");
        return npc.AttackCount;
    }

    internal static RuntimeNpcAttackDefinition? SelectNpcAttack(IReadOnlyList<RuntimeNpcAttackDefinition> attacks, int currentEnergy)
        => RuntimeNpcAttackSelector.Select(attacks, currentEnergy, Random.Shared).Attack;

    internal static int ResolveNpcEnergyAfterAttack(int currentEnergy, RuntimeNpcAttackDefinition attack)
    {
        ArgumentNullException.ThrowIfNull(attack);
        if (currentEnergy < 0) throw new ArgumentOutOfRangeException(nameof(currentEnergy));
        if (attack.EnergyCost < 0) throw new InvalidDataException("NPC_COMBAT_NEGATIVE_ATTACK_ENERGY_COST");
        if (attack.EnergyCost > currentEnergy) throw new InvalidOperationException("NPC_COMBAT_INSUFFICIENT_ENERGY");
        return checked(currentEnergy - attack.EnergyCost);
    }

    internal static int ResolveNpcEnergyAfterResistedHitSpell(
        int currentEnergyAfterCost, RuntimeNpcAttackDefinition attack, int maximumEnergy)
    {
        ArgumentNullException.ThrowIfNull(attack);
        if (currentEnergyAfterCost < 0) throw new ArgumentOutOfRangeException(nameof(currentEnergyAfterCost));
        if (maximumEnergy < 0) throw new ArgumentOutOfRangeException(nameof(maximumEnergy));
        if (currentEnergyAfterCost > maximumEnergy) throw new InvalidDataException("NPC_COMBAT_ENERGY_EXCEEDS_MAXIMUM");
        if (attack.EnergyCost < 0) throw new InvalidDataException("NPC_COMBAT_NEGATIVE_ATTACK_ENERGY_COST");

        // Retained Classic spell records bind formula.npc.spell_half_energy_refund.
        // Integer division preserves the runtime's truncation for odd authored costs.
        var refund = attack.EnergyCost / 2;
        return Math.Min(maximumEnergy, checked(currentEnergyAfterCost + refund));
    }


    internal static RuntimeNpcHitSpellSelection? ResolveNpcHitSpell(
        RuntimeNpcAttackDefinition attack, ClassicAttackResult result, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(result);
        if (attack.HitSpellLegacyId <= 0) return null;
        var triggered = result.Outcome is ClassicHitOutcome.Hit or ClassicHitOutcome.Critical;
        if (!triggered) return new RuntimeNpcHitSpellSelection(
            attack.HitSpellLegacyId, string.Empty, string.Empty, false, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            ClassifyNpcHitSpell(0, 0, 0, 0), ResolveNpcHitSpellEffectRange(0, 0));

        var catalogPath = Path.Combine(projectRoot, "Librarium", "CombatSpellReconstruction", "canonical_combat_spell_catalog_0184.json");
        var catalog = RuntimeCanonicalCombatSpellCatalogLoader.Load(catalogPath);
        var canonicalId = $"spell.{attack.HitSpellLegacyId}";
        var spell = RuntimeCanonicalCombatSpellCatalogLoader.RequireSpell(catalog, canonicalId);
        if (!spell.LegacyId.Equals(attack.HitSpellLegacyId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"NPC_HIT_SPELL_LEGACY_ID_MISMATCH:{attack.HitSpellLegacyId}:{spell.LegacyId}");
        return new RuntimeNpcHitSpellSelection(
            attack.HitSpellLegacyId, spell.Id, spell.Name, true,
            spell.SpellTypeRaw, spell.TargetTypeRaw, spell.AttackTypeRaw,
            spell.MinimumEffect, spell.MaximumEffect, spell.DurationRaw,
            spell.ResistTypeRaw, spell.ResistAbilityRaw, spell.DifficultyRaw,
            ClassifyNpcHitSpell(spell.SpellTypeRaw, spell.TargetTypeRaw, spell.AttackTypeRaw, spell.ResistTypeRaw),
            ResolveNpcHitSpellEffectRange(spell.MinimumEffect, spell.MaximumEffect));
    }

    internal static RuntimeNpcHitSpellResistanceResolution ResolveNpcHitSpellResistance(
        int resistanceTypeRaw, int willpower, int intellect, int magicResistanceBonus, int antiMagicAbility, int roll)
    {
        if (resistanceTypeRaw < 0) throw new InvalidDataException("NPC_HIT_SPELL_NEGATIVE_RESISTANCE_TYPE");
        if (willpower < 0 || intellect < 0 || magicResistanceBonus < 0 || antiMagicAbility < 0)
            throw new InvalidDataException("NPC_HIT_SPELL_NEGATIVE_RESISTANCE_INPUT");
        if (roll is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(roll));

        var antiMagicRequired = resistanceTypeRaw == 1;
        var checkEligible = resistanceTypeRaw switch
        {
            0 => false,
            1 => antiMagicAbility > 0,
            2 => true,
            _ => false
        };
        var score = checked((willpower + checked(intellect * 3)) / 4 + magicResistanceBonus);
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
    {
        var values = character.SemanticState?.Resistances;
        var baseValue = 0;
        if (values is not null)
            foreach (var key in new[] { "magic", "magicres", "magic_resistance" })
                if (values.TryGetValue(key, out var value)) { baseValue = value; break; }
        return Math.Max(0, checked(baseValue + RuntimeClassicStatusProjection.Numeric(character, "magic_resistance")));
    }

    private static int ReadAbilityValue(RuntimeCharacterRecord character, int abilityId)
    {
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

    internal static RuntimeNpcHitSpellEffectResolution ResolveNpcHitSpellEffect(
        RuntimeNpcHitSpellSelection spell, int elementalResistancePercent, bool poisonImmune, Random random)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(random);
        var type = ResolveNpcHitSpellTypeMeaning(spell.SpellTypeRaw);
        var scope = ResolveNpcHitSpellTargetScope(spell.TargetTypeRaw);
        var element = ResolveNpcHitSpellAttackElement(spell.AttackTypeRaw);
        var positiveRange = spell.EffectRange.Sign == RuntimeSpellEffectSign.Positive && spell.EffectRange.RollMaximum > 0;
        var hostileScope = spell.TargetTypeRaw is 0 or 8 or 12;
        var executable = spell.SpellTypeRaw == 0 && hostileScope && positiveRange;
        if (!executable)
        {
            var reason = spell.SpellTypeRaw != 0 ? "non-immediate-spell-type"
                : !hostileScope ? "non-hostile-or-scripted-target-scope"
                : "non-positive-effect-range";
            return new RuntimeNpcHitSpellEffectResolution(false, type, scope, element,
                spell.EffectRange.RollMinimum, spell.EffectRange.RollMaximum, 0, 0, elementalResistancePercent, poisonImmune, 0, reason);
        }
        var roll = random.Next(spell.EffectRange.RollMinimum, checked(spell.EffectRange.RollMaximum + 1));
        var finalDamage = ResolveElementalSpellDamage(roll, element, elementalResistancePercent, poisonImmune);
        return new RuntimeNpcHitSpellEffectResolution(true, type, scope, element,
            spell.EffectRange.RollMinimum, spell.EffectRange.RollMaximum, roll, roll, elementalResistancePercent, poisonImmune, finalDamage,
            finalDamage == 0 && (poisonImmune || elementalResistancePercent >= 100) ? "elemental-immunity" : string.Empty);
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
        var executable = spell.SpellTypeRaw == 0 && spell.TargetTypeRaw is 0 or 8 or 12
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
        if (elementalResistancePercent >= 100) return 0;
        var multiplierPercent = checked(100 - elementalResistancePercent);
        return Math.Max(0, checked(rawDamage * multiplierPercent) / 100);
    }

    internal static RuntimeNpcHitSpellStatusDeliveryResolution ResolveNpcHitSpellStatusDelivery(
        RuntimeNpcHitSpellSelection spell, bool poisonImmune, bool duplicateActiveEffect)
    {
        ArgumentNullException.ThrowIfNull(spell);
        var kind = spell.SpellTypeRaw == 1 ? "poison" : spell.SpellTypeRaw == 3 ? "status-or-scripted" : "unsupported";
        var hostileScope = spell.TargetTypeRaw is 0 or 8 or 12;
        var eligibleType = spell.SpellTypeRaw is 1 or 3;
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
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(statusKey) ? baseValue : checked(baseValue + RuntimeClassicStatusProjection.Numeric(character, statusKey));
    }

    internal static string ResolveNpcHitSpellTargetScope(int targetTypeRaw) => targetTypeRaw switch
    {
        0 => "contact-attached-target",
        1 => "scripted-world-transition",
        8 => "single-hostile-target",
        12 => "area-hostile-target",
        _ => $"target-type-{targetTypeRaw}"
    };

    internal static string ResolveNpcHitSpellAttackElement(int attackTypeRaw) => attackTypeRaw switch
    {
        0 => "cold",
        1 => "fire",
        2 => "stone",
        3 => "lightning",
        4 => "untyped",
        5 => "acid",
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

    private static string RenderAttack(string attacker, string defender, ClassicAttackResult result) => result.Outcome switch
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
            RenderAttack(attacker, defender, result));
    }

    private static string RenderNpcAttack(string attacker, string defender, RuntimeNpcAttackDefinition attack, ClassicAttackResult result)
    {
        var selection = ResolveNpcCombatMessage(attack, result, attacker, defender);
        // Bundle F owns decoded message-body authority. Bundle B consumes the correct decoded
        // relationship now and uses the established safe text until that body is promoted.
        return selection.SafeFallback;
    }

    private RuntimeLivingNpcState? ResolveEncounterLivingNpc(RuntimeSession session, RuntimeLiveCombatEncounter encounter) =>
        Living(session).Capture().Npcs.FirstOrDefault(npc =>
            npc.InstanceId.Equals(encounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase));
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
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "npcs.json")));
            var npcs = document.RootElement.GetProperty("records").EnumerateArray().Select(RuntimeNpcDefinition.FromJson).ToArray();
            Require(npcs.Any(x => x.Attacks.Any(a => a.Accuracy > 0 && a.MaximumDamage >= a.MinimumDamage && a.SelectionPercent > 0)), "generated-npc-executable-attacks"); report.Checks.Add("generated-npc-executable-attacks");
            var multiAttackNpc = npcs.FirstOrDefault(x => x.AttackCount > 1 && x.Attacks.Any(a => a.Accuracy > 0 && a.MaximumDamage >= a.MinimumDamage && a.SelectionPercent > 0));
            Require(multiAttackNpc is not null, "generated-npc-multi-attack-count"); report.Checks.Add("generated-npc-multi-attack-count");
            Require(RuntimeLiveGameplayFacade.ResolveNpcAttacksPerRound(multiAttackNpc!) == multiAttackNpc!.AttackCount, "npc-attack-count-live-consumer"); report.Checks.Add("npc-attack-count-live-consumer");
            var zeroAttackNpc = npcs.FirstOrDefault(x => x.AttackCount == 0);
            Require(zeroAttackNpc is not null, "generated-npc-zero-attack-count"); report.Checks.Add("generated-npc-zero-attack-count");
            Require(RuntimeLiveGameplayFacade.ResolveNpcAttacksPerRound(zeroAttackNpc!) == 0, "zero-attack-npc-does-not-attack"); report.Checks.Add("zero-attack-npc-does-not-attack");
            var energyAttack = npcs.SelectMany(x => x.Attacks).FirstOrDefault(x => x.EnergyCost > 0 && x.Accuracy > 0 && x.SelectionPercent > 0);
            Require(energyAttack is not null, "decoded-npc-attack-energy-cost"); report.Checks.Add("decoded-npc-attack-energy-cost");
            Require(RuntimeLiveGameplayFacade.SelectNpcAttack(new[] { energyAttack! }, energyAttack!.EnergyCost - 1) is null, "npc-attack-blocked-without-energy"); report.Checks.Add("npc-attack-blocked-without-energy");
            Require(RuntimeLiveGameplayFacade.SelectNpcAttack(new[] { energyAttack! }, energyAttack.EnergyCost) == energyAttack, "npc-affordable-attack-selected"); report.Checks.Add("npc-affordable-attack-selected");
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterAttack(energyAttack.EnergyCost + 3, energyAttack) == 3, "npc-attack-energy-deducted"); report.Checks.Add("npc-attack-energy-deducted");
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterResistedHitSpell(3, energyAttack, energyAttack.EnergyCost + 3) == 3 + (energyAttack.EnergyCost / 2), "npc-resisted-spell-half-energy-refund"); report.Checks.Add("npc-resisted-spell-half-energy-refund");
            var oddCostAttack = energyAttack with { EnergyCost = 125 };
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterResistedHitSpell(10, oddCostAttack, 200) == 72, "npc-resisted-spell-refund-truncates"); report.Checks.Add("npc-resisted-spell-refund-truncates");
            Require(RuntimeLiveGameplayFacade.ResolveNpcEnergyAfterResistedHitSpell(190, oddCostAttack, 200) == 200, "npc-resisted-spell-refund-capped"); report.Checks.Add("npc-resisted-spell-refund-capped");
            Require(RuntimeLiveGameplayFacade.ResolveNpcEncounterStartingEnergy(333) == 333, "npc-encounter-energy-reset-to-decoded-maximum"); report.Checks.Add("npc-encounter-energy-reset-to-decoded-maximum");
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
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellAttackElement(6) == "poison", "npc-hit-spell-poison-classification"); report.Checks.Add("npc-hit-spell-poison-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(0) == "contact-attached-target", "npc-hit-spell-contact-target-classification"); report.Checks.Add("npc-hit-spell-contact-target-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(1) == "scripted-world-transition", "npc-hit-spell-scripted-target-classification"); report.Checks.Add("npc-hit-spell-scripted-target-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(8) == "single-hostile-target", "npc-hit-spell-single-hostile-target-classification"); report.Checks.Add("npc-hit-spell-single-hostile-target-classification");
            Require(RuntimeLiveGameplayFacade.ResolveNpcHitSpellTargetScope(12) == "area-hostile-target", "npc-hit-spell-area-hostile-target-classification"); report.Checks.Add("npc-hit-spell-area-hostile-target-classification");
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
                if (resolved is { Triggered: true, SpellTypeRaw: 0 } && resolved.TargetTypeRaw is 0 or 8 or 12
                    && resolved.EffectRange.Sign == RuntimeSpellEffectSign.Positive && resolved.EffectRange.RollMaximum > 0)
                {
                    immediateDamageSpell = resolved;
                    break;
                }
            }
            Require(immediateDamageSpell is not null, "generated-npc-immediate-damage-hit-spell"); report.Checks.Add("generated-npc-immediate-damage-hit-spell");
            var immediateEffect = RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectForAudit(immediateDamageSpell!, immediateDamageSpell!.EffectRange.RollMinimum);
            Require(immediateEffect.Executable && immediateEffect.DirectDamage == immediateDamageSpell.EffectRange.RollMinimum, "npc-hit-spell-immediate-damage-live-consumer"); report.Checks.Add("npc-hit-spell-immediate-damage-live-consumer");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "fire", 25, false) == 75, "npc-hit-spell-elemental-resistance-reduction"); report.Checks.Add("npc-hit-spell-elemental-resistance-reduction");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "cold", -50, false) == 150, "npc-hit-spell-elemental-vulnerability-increase"); report.Checks.Add("npc-hit-spell-elemental-vulnerability-increase");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "stone", 100, false) == 0, "npc-hit-spell-elemental-immunity-threshold"); report.Checks.Add("npc-hit-spell-elemental-immunity-threshold");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "lightning", 200, false) == 0, "npc-hit-spell-overcap-resistance-suppression"); report.Checks.Add("npc-hit-spell-overcap-resistance-suppression");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "poison", 0, true) == 0, "npc-hit-spell-poison-immunity"); report.Checks.Add("npc-hit-spell-poison-immunity");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "acid", 75, false) == 100, "npc-hit-spell-acid-unmapped-resistance-fallback"); report.Checks.Add("npc-hit-spell-acid-unmapped-resistance-fallback");
            Require(RuntimeLiveGameplayFacade.ResolveElementalSpellDamage(100, "untyped", 75, false) == 100, "npc-hit-spell-untyped-resistance-bypass"); report.Checks.Add("npc-hit-spell-untyped-resistance-bypass");
            var quarantinedSpell = immediateDamageSpell with { SpellTypeRaw = 3, Classification = RuntimeLiveGameplayFacade.ClassifyNpcHitSpell(3, immediateDamageSpell.TargetTypeRaw, immediateDamageSpell.AttackTypeRaw, immediateDamageSpell.ResistTypeRaw) };
            Require(!RuntimeLiveGameplayFacade.ResolveNpcHitSpellEffectForAudit(quarantinedSpell, quarantinedSpell.EffectRange.RollMinimum).Executable, "npc-hit-spell-status-effect-quarantined"); report.Checks.Add("npc-hit-spell-status-effect-quarantined");
            RuntimeNpcHitSpellSelection? poisonStatusSpell = null;
            RuntimeNpcHitSpellSelection? generalStatusSpell = null;
            foreach (var candidate in npcs.SelectMany(x => x.Attacks).Where(x => x.HitSpellLegacyId > 0))
            {
                var resolved = RuntimeLiveGameplayFacade.ResolveNpcHitSpell(candidate, new ClassicAttackResult(ClassicHitOutcome.Hit, 0, 0, 1, 1, 1, false), projectRoot);
                if (resolved is not { Triggered: true } || resolved.DurationRaw <= 0 || resolved.TargetTypeRaw is not (0 or 8 or 12)) continue;
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
            var mrNormal = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 40, 60, 7, 0, 24);
            Require(mrNormal.MagicResistanceScore == 62 && mrNormal.ResistanceChancePercent == 31 && mrNormal.Resisted, "npc-hit-spell-mr-equation"); report.Checks.Add("npc-hit-spell-mr-equation");
            var mrMiss = RuntimeLiveGameplayFacade.ResolveNpcHitSpellResistance(2, 40, 60, 7, 0, 32);
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
}
