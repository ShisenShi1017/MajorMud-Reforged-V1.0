using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeFormulaEvidenceGrade
{
    Verified,
    StronglyCorroborated,
    DecodedButNotMapped,
    RelationshipUnresolved,
    TrulyAbsentFromAvailableDecode,
    Conflicting
}

public sealed record RuntimeFormulaEvidenceSource(
    string SourceKind,
    string RelativePath,
    string? Detail = null);

public sealed record RuntimeFormulaAuthorityRecord(
    string FormulaId,
    string Subsystem,
    RuntimeFormulaEvidenceGrade Grade,
    bool Executable,
    string RuntimeOwner,
    IReadOnlyList<RuntimeFormulaEvidenceSource> Sources,
    string Notes);

/// <summary>
/// Pass 10 formula authority. This is deliberately separate from the MegaMUD client.
/// It reconciles server-native decode, imported databases, generated Librarium data,
/// and current runtime formula owners before a formula is allowed to execute.
/// </summary>
public sealed class RuntimeClassicFormulaAuthority
{
    private readonly Dictionary<string, RuntimeFormulaAuthorityRecord> _records;

    private RuntimeClassicFormulaAuthority(IEnumerable<RuntimeFormulaAuthorityRecord> records)
    {
        _records = records.ToDictionary(x => x.FormulaId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<RuntimeFormulaAuthorityRecord> Records => _records.Values;

    public RuntimeFormulaAuthorityRecord RequireExecutable(string formulaId)
    {
        if (!_records.TryGetValue(formulaId, out var record))
            throw new InvalidDataException($"Classic formula authority has no record for '{formulaId}'.");
        if (!record.Executable || record.Grade is RuntimeFormulaEvidenceGrade.DecodedButNotMapped or RuntimeFormulaEvidenceGrade.RelationshipUnresolved or RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode or RuntimeFormulaEvidenceGrade.Conflicting)
            throw new InvalidDataException($"Classic formula '{formulaId}' is quarantined ({record.Grade}).");
        return record;
    }

    public static RuntimeClassicFormulaAuthority Load(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var required = new[]
        {
            "Data/Classic/formulas.json",
            "Data/Classic/combat_spells.json",
            "Data/Classic/abilities.json"
        };
        foreach (var relative in required)
        {
            var full = Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) throw new FileNotFoundException($"Required Classic formula evidence is missing: {relative}", full);
            using var stream = File.OpenRead(full);
            _ = JsonDocument.Parse(stream);
        }

        var records = new List<RuntimeFormulaAuthorityRecord>
        {
            new(
                RuntimeCharacterDerivedStatisticCalculator.FormulaId,
                "character_statistics",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeCharacterDerivedStatisticCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_decode", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("runtime_native_trace", "Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs", "wccmmud.dll calculate_secondary_stats @0x0041a424"),
                    new RuntimeFormulaEvidenceSource("imported_wcc", "Librarium/Objects/evidence_records/catalog.json")
                },
                "Creation and promotion HP/mana math is already native-traced and executable."),
            new(
                RuntimeCharacterCombatStatisticProjector.FormulaId,
                "character_statistics",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeCharacterCombatStatisticProjector),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_decode", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Librarium/Objects/abilities/ability_records.json")
                },
                "Only explicit equipment and ability modifiers are executable; unknown base-stat ordering remains quarantined."),
            new(
                RuntimeClassicCharacterFormulaCalculator.MagicResistanceFormulaId,
                "character_statistics",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicCharacterFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "CalcMR line 4270: Fix((INT + (WIL * 3)) / 4) + modifiers"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/Objects/evidence_records/catalog.json", "retained CalcMR implementation at source line 4270"),
                    new RuntimeFormulaEvidenceSource("pass9_evidence", "Librarium/Objects/parity/parity_records.json", "MR weighting conflict resolved in favor of retained implementation evidence"),
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "WCCMMUD.DLL 0x0041ab17..0x0041ab3f computes (INT + WIL*3)/4 then adds the native modifier output")
                },
                "Character Magic Resistance is native-verified: WCCMMUD.DLL computes signed Fix((INT + WIL*3)/4) and then adds the modifier result. The executable runtime and regression vectors match that ordering."),
            new(
                RuntimeClassicCharacterFormulaCalculator.RawDodgeFormulaId,
                "character_statistics",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicCharacterFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_move_player_to_fighter directly calls _get_user_ability_value for ability 34, adds Level/5, (Charm-50)/5 and (Agility-50)/3, then applies the <33% encumbrance bonus to fighter +0x20"),
                    new RuntimeFormulaEvidenceSource("native_consumer", "Librarium/Objects/parity/parity_records.json", "_calculate_attack directly consumes fighter +0x20 as raw Dodge, applies the recovered accuracy divisor/95% cap and Backstab /5"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "CalcDodge lines 2482-2495 independently match the direct DLL arithmetic"),
                    new RuntimeFormulaEvidenceSource("pass9_evidence", "Librarium/Objects/parity/parity_records.json", "retained stock formula and regression vectors")
                },
                "Player raw Dodge is native-verified end-to-end: the original DLL constructs fighter Dodge from effective ability 34 plus Level/Charm/Agility, applies the below-33-percent encumbrance bonus, and passes that exact fighter field to the stock Dodge-vs-Accuracy resolver."),
            new(
                RuntimeCharacterRegenerationAuthority.FormulaId,
                "character_statistics",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeCharacterRegenerationAuthority),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_slow_update_character and _fast_update_character exact HP/mana arithmetic, modifiers, counters and owner ordering"),
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Objects/parity/parity_records.json", "REST/MEDITATE historical HELP boundary retained separately from executable MEDITATE timing"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeCharacterRegeneration.cs", "native combined percent arithmetic, passive/rest/meditate formulas, exact counters and response"),
                    new RuntimeFormulaEvidenceSource("runtime_clock", "Apps/Source/Reforged.Server/RuntimeClassicNativeSchedulerClock.cs", "one-shot one-second fast clock with 3/5/5/30 owner buckets and no elapsed catch-up")
                },
                "Character regeneration is native-verified. HP uses max(1,trunc(((Level+20)*Health)/750)); REST multiplies the base by three; HP/MP modifiers use trunc(((100+modifier)*base)/100). Mana types 1-4 and Kai/type 5 match the DLL, including MEDITATE bypassing MPRegen. Passive work runs in the 30-tick slow owner, REST fires on fast counter 21, and MEDITATE on fast counter 15."),
            new(
                ClassicCombatResolver.ClassCombatAccuracyFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimePlayerCombatProfileResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_move_player_to_fighter directly constructs fighter +0x00 from class +0x48, floor-sqrt(Level), Level/2, the folded equipped-Accuracy/encumbrance bucket, STR/3 and AGI/6"),
                    new RuntimeFormulaEvidenceSource("native_plus_accuracy", "Librarium/Objects/parity/parity_records.json", "_move_player_to_fighter adds user +0x70A after base fighter Accuracy construction; martial accuracy remains a later attack-type-specific path"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "https://github.com/syntax53/MMUD-Explorer/blob/master/modMMudFunc.bas", "read-only CalculateAccuracy cross-check for stock class/level/STR/AGI/encumbrance semantics"),
                    new RuntimeFormulaEvidenceSource("decoded_class_catalog", "Data/Classic/character_foundation.json", "public combat_bonus supplies the class combat value consumed by the native formula")
                },
                "Stock player base Accuracy is native-verified. Reforged constructs it once from the original fighter-build ordering and folds equipped direct Accuracy before direct ability/status plus-accuracy; normal live combat does not double-add the wielded weapon Accuracy."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.StrengthMaximumDamageFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json", "line 27694: Fix((Strength - 50) / 10), explicitly matching MajorMUD"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/Objects/evidence_records/catalog.json", "retained MMUD Explorer source line and provenance"),
                    new RuntimeFormulaEvidenceSource("item_schema", "Librarium/Objects/evidence_records/catalog.json", "item authoring warning confirms Strength maximum-damage bonus is a distinct component that must be included")
                },
                "Classic Strength contributes Fix((Strength - 50) / 10) to maximum physical damage. Negative values are retained in Classic; C# integer division matches VB6 Fix truncation for integer inputs."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.AttackEnergyFormulaId,
                "combat_timing",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json", "lines 4663-4678: weapon energy formula"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "Librarium/Objects/evidence_records/catalog.json", "lines 1995-2006: independent matching implementation"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/Objects/evidence_records/catalog.json", "retained source formula and provenance")
                },
                "Player attack energy starts with Fix((weaponSpeed * 1000) / Fix((((level * (combatCode + 2)) + 45) * (agility + 150)) / 6)), applies the recovered below-STR-requirement penalty, optional Speed percentage branch, and ((Encumbrance/2)+75)% modifier, then feeds the authoritative per-round swing scheduler."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.PlayerAttackSchedulingFormulaId,
                "combat_timing",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json", "lines 1587-1593: swings = 1000 / energy, capped by MAX_SWINGS=5"),
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json", "lines 2006-2041: 1000/energy swing calculation and cap"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "Librarium/Objects/evidence_records/catalog.json", "independent matching 1000/energy swing model"),
                    new RuntimeFormulaEvidenceSource("librarium", "Librarium/Objects/evidence_records/catalog.json", "high-confidence verified attack-energy/action loop")
                },
                "Normal weapon rounds receive 1000 action-energy units, execute whole affordable swings, carry unused energy forward, and cap execution at five swings per round."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.SpecialAttackTimingFormulaId,
                "combat_timing",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json", "lines 1564-1595: Backstab/Smash force 1000 energy and one swing; Bash doubles calculated energy; all attacks retain the five-swing cap"),
                    new RuntimeFormulaEvidenceSource("dll_native_trace", "Librarium/Objects/evidence_records/catalog.json", "Bash -15 accuracy, +10% pre-roll, x3 after DR; Smash -25 accuracy, +20% pre-roll, x5 after DR; criticals disabled and Dodge remains last"),
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Objects/native_help/native_help_records.json", "Bash is a heavy inaccurate attack available to any class; Smash is ability-gated"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatRounds.cs", "ResolvePhysicalAttackSchedule drives Bash, Smash, Backstab, and normal weapon rounds")
                },
                "Classic special timing is closed: Backstab and Smash each consume a full 1000-energy round and execute once; Bash doubles normal weapon energy and executes whole affordable swings up to the five-swing cap. Bash/Smash accuracy, pre-roll damage, post-DR multipliers, critical suppression, hit/DR/Dodge ordering, ability gate, and return-to-normal behavior execute through the single resolver and live encounter."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.NaturalCriticalChanceFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json", "lines 27917-27951: level, Intellect, Agility, and Charm components; natural cap 75 and minimum 1 before later modifiers"),
                    new RuntimeFormulaEvidenceSource("mudinfo", "MajorMUD Critical Hit Chance Formula", "Fix(Level/10) + Fix((Intellect-50)/10) + Fix((Agility-50)/20) + Fix((Charm-50)/30), capped 1..75"),
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Objects/native_help/native_help_records.json", "Intellect is explicitly a component of Critical Hit Chance"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/Objects/evidence_records/catalog.json", "retained MMUD Explorer calculation sequence and cap provenance")
                },
                "Classic natural critical chance is Fix(Level/10) + Fix((Intellect-50)/10) + Fix((Agility-50)/20) + Fix((Charm-50)/30), capped from 1 through 75 before Quick-and-Deadly, quest, and item bonuses."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.CriticalRollBoundaryFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/formulas/catalog.json", "retained Classic percentage-roll behavior"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/ClassicCombatResolver.cs", "inclusive 1..100 critical roll")
                },
                "Critical chance uses an inclusive 1 through 100 roll and succeeds when the roll is less than or equal to final critical chance."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.DodgeRollBoundaryFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("formula_roll_convention", "Librarium/Objects/formulas/catalog.json", "Classic percentage checks use inclusive 1 through 100 boundaries"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/ClassicCombatResolver.cs", "inclusive 1..100 dodge roll")
                },
                "Final Dodge chance uses an inclusive 1 through 100 roll and succeeds when the roll is less than or equal to the clamped Dodge percentage."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.BackstabDamageFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer_formula", "Librarium/Objects/evidence_records/catalog.json", "CalcBSDamage lines 4196-4198"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer_consumers", "Librarium/Objects/evidence_records/catalog.json", "minimum and maximum endpoint calls lines 1123-1124"),
                    new RuntimeFormulaEvidenceSource("librarium_evidence", "Librarium/Objects/evidence_records/catalog.json", "retained formula and Classic level-scaling branch"),
                    new RuntimeFormulaEvidenceSource("native_dll", "Librarium/Objects/parity/parity_records.json", "_move_player_to_fighter 0x42B064-0x42B119 directly proves both Backstab endpoint formulas, race-only 75% penalty, and Classic level scale")
                },
                "Native _move_player_to_fighter directly closes both Classic Backstab endpoints: 2*normal + 2*Level + floor(Stealth/10) + ability 117/118, race-only Stealth at 75%, then (Level+100)% scaling."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.CriticalDamageRangeFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Objects/native_message_payloads/native_message_payload_records.json", "critical hits do up to four times normal damage"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "stock CalculateAttack: critical range is 2x through 4x normal maximum, then DR is subtracted"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/ClassicCombatResolver.cs", "critical damage range is two through four times normal maximum")
                },
                "Critical damage rolls inclusively from twice through four times the normal maximum damage before damage resistance."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.FinalCriticalChanceFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("uploaded_mmexplorer_evidence", "Librarium/Objects/evidence_records/catalog.json", "retained MMUD Explorer critical calculation provenance"),
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "Librarium/Objects/evidence_records/catalog.json", "critical formulas reworked; normal critical percentage remains distinct from Quick-and-Deadly"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/RuntimeClassicPhysicalFormulaCalculator.cs", "over 40, excess critical points count at one-third; final cap 99")
                },
                "After all critical bonuses are combined, values through 40 are unchanged; excess critical points count at one-third using truncation, with a final cap of 99."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.CriticalResolutionOrderFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "Librarium/Objects/evidence_records/catalog.json", "Version 1.11j: critical hit is determined only after a hit has been determined"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/ClassicCombatResolver.cs", "hit -> critical range -> damage roll -> DR -> Dodge -> final damage")
                },
                "Classic first determines whether the attack hits, then rolls critical, rolls critical or normal damage, subtracts DR, checks Dodge last, and applies any post-DR special multiplier."),
            new(
                "combat.npc_critical_bonus",
                "combat",
                RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                false,
                "none",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_npc_catalog", "Data/Classic/npcs.json", "all 1100 NPC records exhausted: no critical field and no Ability 58 NPC relationship"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Data/Classic/abilities.json", "Ability 58 modifies a target critical chance only when explicitly authored")
                },
                "No decoded Classic NPC record authors a critical chance or Ability 58 bonus; NPC physical attacks therefore do not roll criticals."),
            new(
                "combat.class_specific_critical_progression",
                "combat",
                RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                false,
                "none",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_class_catalog", "Data/Classic/classes.json", "Ninja and Mystic records contain no separate hardcoded critical progression field"),
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "Librarium/Objects/evidence_records/catalog.json", "most Mystic forms no longer give critical bonuses; critical changes are authored through stats and abilities")
                },
                "Ninja and Mystic do not receive a separate hardcoded critical progression. Any form, item, quest, or condition critical adjustment must arrive through the shared Ability 58/critical modifier path."),
            new(
                "combat.critical_immunity",
                "combat",
                RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                false,
                "none",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_catalog_exhaustion", "Data/Classic/abilities.json; Data/Classic/npcs.json; Data/Classic/items.json", "no critical-immunity field or ability relationship")
                },
                "No separate Classic critical-immunity mechanic exists in the available decoded data; ordinary hit, DR, and Dodge defenses remain authoritative."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.QuickAndDeadlyFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/Objects/evidence_records/catalog.json", "lines 4564-4566: (200 - EU) + Fix((AGL - 50) / 10), cap 20, halve at encumbrance >= 33"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "Librarium/Objects/evidence_records/catalog.json", "lines 2064-2067: independent matching implementation"),
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "Librarium/Objects/evidence_records/catalog.json", "Quick and Deadly critical bonus capped and tied to attack speed"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/Objects/evidence_records/catalog.json", "retained source formula and provenance")
                },
                "Quick-and-Deadly critical bonus is (200 - attack energy) + Fix((Agility - 50) / 10), capped at 20 and halved when encumbrance is at least 33%. The result is added to the authoritative player critical chance."),
            new(
                "combat.physical_damage.class_contribution",
                "combat",
                RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                false,
                "quarantined",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_class_catalog", "Data/Classic/character_foundation.json", "combat_level and class combat fields"),
                    new RuntimeFormulaEvidenceSource("runtime_trace", "Apps/Source/Reforged.Server/RuntimePlayerCombatProfiles.cs", "class combat_level is consumed only by physical accuracy")
                },
                "No decoded formula currently proves a separate class-based physical-damage addend."),
            new(
                "combat.physical_damage.combat_level_contribution",
                "combat",
                RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                false,
                "quarantined",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_class_catalog", "Data/Classic/character_foundation.json", "combat_level"),
                    new RuntimeFormulaEvidenceSource("bundle_b_accuracy_binding", "Apps/Source/Reforged.Server/RuntimePlayerCombatProfiles.cs", "combat_level per-level accuracy progression")
                },
                "combat_level has a live verified accuracy relationship; no independent damage relationship is promoted without formula evidence."),
            new(
                ClassicCombatResolver.PhysicalDamageFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_item_catalog", "Data/Classic/items.json", "weapon minimum/maximum damage and damage resistance fields"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/Objects/evidence_records/catalog.json", "physical range, critical range, and post-roll DR"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/ClassicCombatResolver.cs", "one roll, DR subtraction, attack-type scaling, and non-negative final damage"),
                    new RuntimeFormulaEvidenceSource("native_dll", "Librarium/Objects/parity/parity_records.json", "_calculate_attack 0x42B800-0x42BB23 directly proves pre-DR special scaling, critical-before-roll, DR, Bash/Smash post-DR multiplier, Dodge, and final clamp order")
                },
                "Native _calculate_attack directly closes Classic physical resolution ordering: attack-type pre-DR scaling, hit, critical range expansion, damage roll, DR subtraction, Bash/Smash post-DR multiplier, Dodge resolution, then non-negative glance/hit clamp."),
            new(
                ClassicCombatResolver.QuestCombatBonusFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimePlayerCombatProfileResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Data/Classic/abilities.json", "abilities 2, 4, 22, 34, and 58"),
                    new RuntimeFormulaEvidenceSource("quest_projection", "Apps/Source/Reforged.Server/RuntimeCharacterAbilityAuthority.cs", "authoritative persistent quest-derived combat bonuses"),
                    new RuntimeFormulaEvidenceSource("live_profile", "Apps/Source/Reforged.Server/RuntimePlayerCombatProfiles.cs", "AC, maximum damage, accuracy, dodge, and critical consumers")
                },
                "Decoded persistent quest ability values now modify the single live physical combat profile without parallel stat tables."),
            new(
                "combat.hit_ac.native",
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("gap_resolution", "Librarium/Objects/formulas/catalog.json")
                },
                "The decoded stock hit/AC equation, 8..99 clamp, inclusive d100 roll (roll <= chance succeeds), and live resolver are promoted as one authoritative path."),
            new(
                ClassicCombatResolver.ItemCriticalBonusFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimePlayerCombatProfileResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_item_catalog", "Data/Classic/items.json", "equipped item abilities_named entries with ability_id 58"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Data/Classic/abilities.json", "Ability 58 modifies critical hit chance by its value"),
                    new RuntimeFormulaEvidenceSource("live_profile", "Apps/Source/Reforged.Server/RuntimePlayerCombatProfiles.cs", "equipped item critical bonuses feed the single ClassicCombatantStats.CriticalChance consumer")
                },
                "Equipped item Ability 58 values are summed into the authoritative player critical chance before ClassicCombatResolver performs the critical roll."),
            new(
                "combat.critical.native",
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("gap_resolution", "Librarium/Objects/formulas/catalog.json")
                },
                "Critical eligibility, chance roll, and damage-range multiplier are strongly corroborated and execute through the authoritative ClassicCombatResolver."),
            new(
                "character.stat_bonus.per_10",
                "character_statistics",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                false,
                "RuntimeCharacterDerivedStatisticCalculator",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("creator_or_community_reference", "MUDInfo topic 1647"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Librarium/Objects/abilities/ability_records.json")
                },
                "Per-10 relationships are accepted as strongly corroborated, but activation waits for exact baseline, truncation, and fixed-point rules from decoded data."),
            new(
                "combat.magic_resistance.native",
                "combat_spells",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                false,
                "RuntimeCombatSpellResolution",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mudinfo", "MUDInfo topic 2115"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "CalcResistedDamage consumers"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Librarium/Objects/abilities/ability_records.json", "AntiMagic ability 51")
                },
                "Equation is strongly corroborated; runtime activation waits for decoded rounding, caps, and anti-magic branch ordering."),
            new(
                "combat.dodge.native",
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mudinfo", "MUDInfo topic 2624"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/Objects/evidence_records/catalog.json")
                },
                "Recovered stock dodge scaling, 95 cap, Backstab /5 reduction, and inclusive roll boundary execute through the authoritative ClassicCombatResolver."),
            new(
                "combat.dodge_base",
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mudinfo", "MUDInfo topic 2624"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json")
                },
                "Base dodge scaling is calculated once from attacker attack value and the defender's resolved dodge value."),
            new(
                "combat.dodge_ability34",
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimePlayerCombatProfileResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Data/Classic/abilities.json", "Ability 34 Dodge"),
                    new RuntimeFormulaEvidenceSource("character_runtime", "RuntimeCharacterQuestAbilities.cs")
                },
                "Decoded Ability 34 is promoted into the defender DodgeBonus before the authoritative avoidance check."),
            new(
                "combat.dodge_final",
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/Objects/evidence_records/catalog.json")
                },
                "The final avoidance stage applies the 95 percent cap and the distinct Backstab reduction before the inclusive avoidance roll."),
            new(
                RuntimeClassicNpcCombatCadence.AttackRowCountFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeNpcDefinition),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "combat.attack_count"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "external:syntax53/MMUD-Explorer/modMMudDatabase.bas", "attack_count limits authored attack rows; it is not the number of per-round attempts"),
                    new RuntimeFormulaEvidenceSource("runtime_npc_model", "RuntimeNpcModels.cs", "ReadAttacks takes the declared row count")
                },
                "Decoded attack_count is the number of authored executable attack rows retained from the monster record. It limits the fixed-width row array and does not control how many selection attempts occur in a combat round."),
            new(
                RuntimeClassicNpcCombatCadence.AttemptsPerRoundFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicNpcCombatCadence),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_attack_monster_user increments an attempt counter and loops while counter <= 5, yielding attempts 1..6"),
                    new RuntimeFormulaEvidenceSource("runtime", "RuntimeClassicNpcCombatCadence.cs", "AttemptsPerRound = 6")
                },
                "Original WCCMMUD.DLL proves six NPC attack-selection attempts: the native counter is incremented before each selection and loops while its completed value is <= 5."),
            new(
                RuntimeClassicNpcCombatCadence.EnergyCarryFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicNpcCombatCadence),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_generate_monster seeds current +0x16 and authored +0x114 from Energy; _energy_update_monster performs add-below/clamp-above/equality-hold"),
                    new RuntimeFormulaEvidenceSource("runtime", "RuntimeClassicNpcCombatCadence.cs", "RefreshRoundEnergy")
                },
                "Native active-monster energy starts at the authored Energy value. On each native energy update: if current is below authored, add one authored-energy quantum; if current is above authored, clamp to authored; if equal, leave it unchanged. Idle energy therefore does not accumulate without bound."),
            new(
                RuntimeNpcAttackSelector.FormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeNpcAttackSelector),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_attack_monster_user calls range(0,100), scans five Att% bytes in slot order, and selects on roll < threshold"),
                    new RuntimeFormulaEvidenceSource("runtime", "RuntimeNpcModels.cs", "RuntimeNpcAttackSelector")
                },
                "Each native attempt draws one [0,100) value and scans the five authored Att% bytes in slot order. The first slot satisfying roll < threshold is selected. Energy affordability is evaluated after selection, so rows are not re-normalized; thresholds above 100 are always true when reached."),
            new(
                RuntimeClassicNpcCombatCadence.SpellCastSuccessFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicNpcCombatCadence),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_monster_cast resolves AttAcc spell id at +0x12e, AttMin success at +0x13e, AttMax cast level at +0x148, then draws [0,100) and tests roll < AttMin"),
                    new RuntimeFormulaEvidenceSource("runtime", "RuntimeClassicNpcCombatCadence.cs", "SpellCastSucceeds")
                },
                "For native Type-2 rows, AttAcc is spell id, AttMin is cast-success percentage, and AttMax is cast level. _monster_cast performs a separate [0,100) roll and succeeds on roll < AttMin."),
            new(
                RuntimeClassicNpcCombatCadence.SpellFailureEnergyFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeClassicNpcCombatCadence),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_monster_cast reads per-row AttEnergy and its failed-cast path deducts max(1, floor(AttEnergy/2)) for positive costs"),
                    new RuntimeFormulaEvidenceSource("runtime", "RuntimeClassicNpcCombatCadence.cs", "DeductFailedSpellCost")
                },
                "The native pre-cast failure path deducts max(1, floor(AttEnergy / 2)) for a non-zero positive attack cost, correcting the retained simulator's banker-rounding approximation. Exact mapping of every later spell-resolution/resistance refund branch is still being held STRONG."),
            new(
                RuntimeClassicNpcCombatCadence.PhysicalEarlyEndFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicNpcCombatCadence),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_attack_monster_user checks affordability before physical execution, deducts AttEnergy, then sets the attempt-loop early-end flag when remaining energy is below the same cost"),
                    new RuntimeFormulaEvidenceSource("runtime", "RuntimeClassicNpcCombatCadence.cs", "ShouldEndRoundAfterPhysicalSelection")
                },
                "Native Type-1 selection terminates when the row cannot be afforded; after an affordable physical attack it deducts the full cost and ends further attempts when remaining energy is below that same row cost. Type-2 rows use _monster_cast instead."),
            new(
                ClassicCombatResolver.NpcEncounterEnergyResetFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_generate_monster copies authored Energy to both active current and authored-baseline fields"),
                    new RuntimeFormulaEvidenceSource("live_combat", "RuntimeLiveCombat.cs", "ResolveNpcEncounterStartingEnergy")
                },
                "A native active monster starts with current energy equal to authored Energy, so a newly admitted Reforged encounter now starts at the decoded Energy value rather than zero."),
            new(
                RuntimeClassicNpcCombatCadence.StockHpRegenIntervalFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLivingWorldService),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_wccmmud", "Librarium/Objects/parity/parity_records.json", "_slow_update_monster adds authored +0x130 HP regeneration once per slow generation and caps at maximum"),
                    new RuntimeFormulaEvidenceSource("native_scheduler", "Librarium/Objects/parity/parity_records.json", "slow generation is rooted at 30 logical fast ticks; per-monster generation markers prevent duplicate updates"),
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Librarium/Objects/npcs/npc_records.json", "public_reference_0139.fields.hp_regen retains the authored per-monster amount"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLivingWorld.cs", "ApplyNativeSlowNpcRegeneration")
                },
                "The retained 18-combat-round simulator approximation is rejected for Classic. WCCMMUD.DLL applies authored NPC HP regeneration once per 30-fast-tick slow generation. Reforged now activates the amount on the recovered slow boundary, while exact native per-monster host-cycle staggering remains under the broader scheduler assertion."),
            new(
                ClassicCombatResolver.NpcDecodedCurrencyAmountsFormulaId,
                "combat_reward",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "rewards.copper, rewards.silver, rewards.gold, rewards.platinum, rewards.runic"),
                    new RuntimeFormulaEvidenceSource("runtime_npc_model", "RuntimeNpcModels.cs", "decoded denomination amounts retained independently"),
                    new RuntimeFormulaEvidenceSource("live_combat_completion", "RuntimeCombatCompletion.cs", "exact standard denominations placed on room ground; zero values remain zero")
                },
                "Decoded monster currency fields are denomination amounts rather than drop percentages. The live death consumer preserves copper, silver, gold, platinum, and runic independently, creates no fabricated fallback money for zero-value NPCs, and routes all five physical denominations through the shared room, inventory, banking, weight, pickup, drop, spending, persistence, and cleanup authorities."),
            new(
                "combat.npc_hit_spell_relationship",
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "attacks.hit_spell_legacy_id"),
                    new RuntimeFormulaEvidenceSource("generated_spell_catalog", "Data/Classic/spells.json", "records.legacy_id"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpell")
                },
                "A positive decoded NPC hit_spell_legacy_id is resolved to exactly one canonical Classic spell record after a successful hit or critical outcome. Miss and dodge outcomes do not trigger the relationship. Spell damage, resistance, status, and persistence execution remain quarantined until their individual mechanics are verified."),
            new(
                ClassicCombatResolver.NpcHitSpellMetadataFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "attacks.hit_spell_legacy_id"),
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/Objects/combat_spells/combat_spell_records.json", "raw spell type, target, attack, effect, duration, resistance, and difficulty fields"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpell")
                },
                "Successful NPC hit-spell relationships now bind to the canonical combat-spell catalog and carry the decoded spell classification, effect range, duration, resistance, and difficulty metadata into the live outcome path. Effect execution remains quarantined until the branch semantics are individually verified."),
            new(
                ClassicCombatResolver.NpcHitSpellClassificationFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "attacks.hit_spell_legacy_id"),
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/Objects/combat_spells/combat_spell_records.json", "spell_type_raw, target_type_raw, attack_type_raw, resist_type_raw"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ClassifyNpcHitSpell")
                },
                "Every resolved NPC hit spell is assigned deterministic typed buckets for its decoded spell type, target type, attack type, and resistance type. The buckets preserve the original numeric values and intentionally do not invent semantic names before the branch meanings are independently verified."),
            new(
                ClassicCombatResolver.NpcHitSpellAttackElementFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/Objects/combat_spells/combat_spell_records.json", "attack_type_raw correlated with spell names"),
                    new RuntimeFormulaEvidenceSource("generated_spell_catalog", "Data/Classic/spells.json", "classification.attack_type and spell name clusters"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellAttackElement")
                },
                "Attack type codes are promoted to strongly corroborated elemental branches: 0 cold, 1 fire, 2 stone, 3 lightning, 4 untyped/default, 5 acid, and 6 poison. The mapping is supported by independent, highly consistent spell-name clusters while damage and resistance execution remain quarantined."),
            new(
                ClassicCombatResolver.NpcHitSpellEffectRangeFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/Objects/combat_spells/combat_spell_records.json", "minimum_effect and maximum_effect"),
                    new RuntimeFormulaEvidenceSource("canonical_spell_loader", "RuntimeCanonicalCombatSpellCatalog.cs", "EffectRollMinimum and EffectRollMaximum"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellEffectRange")
                },
                "Classic spell effect endpoints are preserved exactly as authored, including descending and negative pairs. The live hit-spell metadata also exposes normalized inclusive roll bounds and a signed-effect classification without assigning unverified damage, healing, drain, or status semantics."),
            new(
                ClassicCombatResolver.NpcHitSpellTargetScopeFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_spell_catalog", "Data/Classic/spells.json", "classification.target_type correlated with NPC hit-spell relationships and spell names"),
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "attacks.hit_spell_legacy_id"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellTargetScope")
                },
                "For NPC hit-spell relationships, target type 0 is the contact-attached branch, 1 is a scripted world-transition branch, 8 is a single hostile target, and 12 is an area hostile target. The mapping is strongly corroborated by all 485 relationships and representative spell-name clusters; effect execution remains separately gated."),
            new(
                ClassicCombatResolver.NpcHitSpellResistanceScopeFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "CalculateResistDamage documents 0-never, 1-antimagic only, 2-everyone"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "IsSpellResisted branch for TypeOfResists"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/Objects/evidence_records/catalog.json", "retained TypeOfResists implementation excerpts"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellResistanceScope")
                },
                "Decoded resistance type semantics are verified from retained MMUD Explorer implementation evidence: 0 never permits a resistance check, 1 permits resistance only for anti-magic targets, and 2 permits the normal magic-resistance branch for everyone. Exact roll, rounding, total-resist, and damage-reduction ordering remain separately unresolved."),
            new(
                ClassicCombatResolver.NpcHitSpellResistanceRollFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "CalcMR: MR=(INT+WIL*3)/4+modifiers; resist chance MR/2 capped at 97 percent"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "IsSpellResisted and CalcResistedDamage consumers corroborate resistance gating"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellResistance")
                },
                "For eligible NPC hit spells, target magic resistance uses the Pass 9 character authority: integer-truncated (Intellect + Willpower*3)/4 plus resolved MagicRes modifiers. Full-resist chance is integer-truncated MR/2, clamped from 0 through 97 percent, and succeeds on an inclusive 1-100 roll. Resistance type 0 bypasses the check; type 1 requires Ability 51 Anti-Magic; type 2 checks everyone. Partial damage reduction and resisted energy cost remain separately unresolved."),
            new(
                ClassicCombatResolver.NpcHitSpellTypeSemanticsFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_spell_catalog", "Data/Classic/spells.json", "spell_type correlated with effect and attack branches"),
                    new RuntimeFormulaEvidenceSource("existing_live_spell_policy", "RuntimeLivePowers.cs", "spell type 1 healing/non-damage branch and attack-type damage branch"),
                    new RuntimeFormulaEvidenceSource("retained_spell_clusters", "Librarium/Objects/combat_spells/combat_spell_records.json", "type 0 immediate attacks, type 1 poison delivery, type 3 status/scripted records"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellTypeMeaning")
                },
                "NPC hit-spell type 0 is the immediate-effect branch, type 1 is poison delivery, and type 3 is status or scripted behavior. The mapping is strongly corroborated by retained consumers and complete spell clusters; only the immediate positive hostile branch is executable here."),
            new(
                ClassicCombatResolver.NpcHitSpellImmediateDamageFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/Objects/combat_spells/combat_spell_records.json", "spell type, target scope, attack element, and effect endpoints"),
                    new RuntimeFormulaEvidenceSource("existing_live_spell_policy", "RuntimeLivePowers.cs", "nonzero attack types execute damage operations"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellEffect")
                },
                "After the verified full-resistance gate, spell type 0 with a positive normalized effect range and contact, single-hostile, or area-hostile scope rolls inclusive direct damage from the authored endpoints. Poison, status, scripted, zero, negative, and mixed ranges remain quarantined."),
            new(
                ClassicCombatResolver.NpcHitSpellElementalResistanceFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("ability_catalog", "Data/Classic/abilities.json", "Abilities 3, 5, 65, and 66 modify cold, fire, stone, and lightning resistance by their stored values"),
                    new RuntimeFormulaEvidenceSource("monster_resistance_inventory", "Tools/ReforgedValidator/Baselines/DataSimVer/0008/MonsterAbilityInventory.csv", "positive, negative, 100, and greater-than-100 elemental resistance values across Classic monsters"),
                    new RuntimeFormulaEvidenceSource("retained_simulators", "Librarium/Objects/evidence_records/catalog.json", "resistance and vulnerability damage projections"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "CalculateResistDamage applies Fix(((100 - nBonusResist) * nDamage) / 100)"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveElementalSpellDamage and ApplyElementalSpellHitPoints")
                },
                "Cold, fire, stone, and lightning resistance values modify immediate elemental spell damage with the retained signed Classic percentage formula. Positive values reduce damage, negative values increase damage, exactly 100 yields zero damage, and values above 100 produce negative damage that restores hit points up to the target's maximum. Integer division truncates toward zero. Acid and untyped damage remain unchanged because no corresponding decoded player resistance field is established. Water resistance remains projected and stackable but has no decoded NPC hit-spell attack-type consumer in the canonical Classic spell catalog."),
            new(
                ClassicCombatResolver.NpcHitSpellPoisonImmunityFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("ability_catalog", "Data/Classic/abilities.json", "Ability 21 ImmuPoison"),
                    new RuntimeFormulaEvidenceSource("spell_ability_evidence", "Librarium/Objects/evidence_records/catalog.json", "ImmuPoison applied by Classic spells and effects"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveElementalSpellDamage")
                },
                "Ability 21 Poison Immunity suppresses both immediate poison-element damage and persistent poison delivery before an active effect can be created."),
            new(
                ClassicCombatResolver.NpcHitSpellStatusDeliveryFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("spell_catalog", "Data/Classic/spells.json", "spell type, hostile target scope, authored duration, and effect endpoints"),
                    new RuntimeFormulaEvidenceSource("poison_evidence", "Librarium/Objects/evidence_records/catalog.json", "Poison, cure poison, duration, and immunity relationships"),
                    new RuntimeFormulaEvidenceSource("spell_persistence", "RuntimeSpells.cs", "RuntimeActiveSpellEffect persistence and expiration"),
                    new RuntimeFormulaEvidenceSource("character_projection", "RuntimeCharacterStatusEffects.cs", "active spell effects projected into character status and timers"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellStatusDelivery and ApplyNpcHitSpellStatusDelivery")
                },
                "After the full-resistance gate, hostile spell type 1 poison-delivery and type 3 status effects with positive decoded duration are persisted as active spell effects. Ability 21 blocks poison delivery. Reapplying the same spell refreshes it; different spells remain independent. Periodic damage and decoded ability modifiers are consumed by the shared status lifecycle."),
            new(
                ClassicCombatResolver.NpcHitSpellExpirationFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("spell_persistence", "RuntimeSpells.cs", "RuntimeSpellEngine.Expire removes effects whose ExpiresAtUtc is at or before the authoritative UTC time"),
                    new RuntimeFormulaEvidenceSource("character_projection", "RuntimeCharacterStatusEffects.cs", "Synchronize excludes expired effects and rebuilds status/timer projections"),
                    new RuntimeFormulaEvidenceSource("live_power_restore", "RuntimeLivePowers.cs", "EnsureLivePowers expires stale persisted effects on world restore/reconnect"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ExecuteLiveCombatRound expires effects before combat profile resolution")
                },
                "NPC-delivered poison and status effects use decoded duration in three-second spell/system rounds, separate from the five-second physical combat round. Expired effects are removed on restore, power commands, and automatic ticks; character projections are synchronized and persisted. Target death removes all remaining active effects."),
            new(
                ClassicCombatResolver.NpcHitSpellCurePoisonFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("spell_catalog", "Data/Classic/spells.json", "spell.31 Antidote and spell.222 Cure Poison"),
                    new RuntimeFormulaEvidenceSource("native_spell_text", "Librarium/Objects/evidence_records/catalog.json", "Cure Poison removes all poison from the target's system"),
                    new RuntimeFormulaEvidenceSource("decoded_ability", "Data/Classic/spells.json", "Ability 20 Cure Poison and RemovesSpell relationships on Antidote"),
                    new RuntimeFormulaEvidenceSource("live_power_consumer", "RuntimeLivePowers.cs", "IsCurePoisonSpell and RemovePoisonEffects")
                },
                "Casting Antidote or Cure Poison removes every active effect classified as poison from the selected character target, removes the transient cure cast effect, preserves unrelated statuses, synchronizes character status/timer projections, and persists the cleaned power state."),
            new(
                ClassicCombatResolver.StatusLifecycleFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_spells", "Data/Classic/spells.json", "Authored duration, spell type, target type, and abilities_named status relationships"),
                    new RuntimeFormulaEvidenceSource("runtime_lifecycle", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "Six-second combat rounds, same-spell refresh, independent different-spell effects, persistence and expiration")
                },
                "Decoded poison and status effects use authored combat-round duration. Reapplying the same spell refreshes that spell; different spells remain independent. Effects persist across logout, expire by authoritative UTC time, and synchronize character status projections."),
            new(
                ClassicCombatResolver.StatusPeriodicFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("server_tick", "Apps/Source/Reforged.Server/ServerHostConfigurationAudit.cs", "Classic spell/system periodic status tick is three seconds; physical combat is scheduled separately at five seconds"),
                    new RuntimeFormulaEvidenceSource("decoded_spell_effect", "Data/Classic/spells.json", "Poison and periodic damage spells provide authored effect ranges and durations"),
                    new RuntimeFormulaEvidenceSource("runtime_consumer", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "Inclusive periodic magnitude roll once per combat round")
                },
                "Periodic poison and decoded duration damage execute once per three-second spell/system round using an inclusive authored effect-range roll. Missed ticks are caught up deterministically and stop at expiration or target death."),
            new(
                ClassicCombatResolver.StatusModifierFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicStatusProjection),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_abilities", "Data/Classic/spells.json", "abilities_named maps AC, damage, resistance, stats, accuracy, Dodge, stealth, critical, paralysis, silence, confusion and other status effects"),
                    new RuntimeFormulaEvidenceSource("runtime_projection", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "Active decoded abilities project into combat.status conditions consumed by combat and command admission")
                },
                "Active decoded spell abilities are projected into live character combat modifiers and action restrictions. Paralyze blocks movement, casting, and attacks; silence blocks casting; confusion blocks attacks and casting; numeric modifiers feed the authoritative combat profile."),
            new(
                ClassicCombatResolver.StatusCureDispelFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_cures", "Data/Classic/spells.json", "Ability 20 Cure Poison, Ability 73 DispellMagic, and Ability 122 RemovesSpell"),
                    new RuntimeFormulaEvidenceSource("runtime_consumer", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "Decoded poison cures, ability-scoped dispels, explicit spell removals, and value-zero general magical dispel")
                },
                "Cure Poison removes all active poison effects. Nonzero DispellMagic values remove active spells containing that exact ability ID. RemovesSpell removes the authored linked spell. Value-zero DispellMagic removes magical effects while preserving effects marked NonMagical."),
            new(
                ClassicCombatResolver.StatusDeathCleanupFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("runtime_death", "Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs", "Player defeat removes all active status effects before death persistence"),
                    new RuntimeFormulaEvidenceSource("runtime_periodic", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "Periodic death removes target effects and clears projections")
                },
                "All active poison and status effects are removed when the character dies. Character status and timer projections are cleared before the death transition is persisted."),
            new(
                ClassicCombatResolver.BackstabAccuracyFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "Stock: Fix((Stealth + Agility) / 2) + Fix(PlusBSAccu / 2) + class/race stealth adjustment + normal accuracy bonuses"),
                    new RuntimeFormulaEvidenceSource("librarium", "Librarium/Objects/evidence_records/catalog.json", "Retained CalculateBackstabAccuracy source and call sites")
                },
                "Stock Classic Backstab uses Fix((Stealth + Agility) / 2), half of decoded Ability 116 BSAccu, +5 for class stealth or -15 for race-only stealth, followed by normal accuracy bonuses. The resulting rating is consumed by the authoritative Backstab hit branch."),
            new(
                ClassicCombatResolver.BackstabDefenseFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeNpcDefensiveProfileResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_npc_catalog", "Data/Classic/npcs.json", "public_reference_0139.fields.backstab_defense"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "Stock NPC Backstab defense is AC/4 + Backstab Defense, or full AC + Backstab Defense for See Hidden targets; Dodge is checked last at one-fifth strength"),
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Objects/native_message_payloads/native_message_payload_records.json", "High Perception or AC makes a target difficult to Backstab; generated NPC Backstab Defense is the dedicated DV input"),
                    new RuntimeFormulaEvidenceSource("runtime_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatRounds.cs", "Backstab defender uses decoded AC quarter/full See Hidden branch plus BackstabDefense as DV")
                },
                "NPC Backstab Defense is loaded from generated Classic data. Stock Classic adds one quarter of NPC AC, or full AC when the NPC has See Hidden, then applies Backstab Defense as the defender value before the final one-fifth Dodge check."),
            new(
                ClassicCombatResolver.BackstabPreparationFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("classic_bug_fix_catalog", "Data/Classic/classic_bug_fixes.json", "classic_fix.two_handed_backstab_downgrade"),
                    new RuntimeFormulaEvidenceSource("runtime_contract", "Apps/Source/Reforged.Server/ClassicBugFixPolicies.cs", "PrepareBackstab"),
                    new RuntimeFormulaEvidenceSource("item_combat_catalog", "Data/Classic/items.json", "decoded Ability 116 BSAccu marks Backstab-capable weapons; weapon_type and authored identity determine two-handed state"),
                    new RuntimeFormulaEvidenceSource("live_combat_entry", "RuntimeLiveCombat.cs", "BeginLiveCombat backstab preparation")
                },
                "A Backstab attempt qualifies only from hidden state and with bare hands or a supported one-handed weapon. Invalid or non-hidden attempts downgrade to a normal engaged attack; a two-handed or unsupported weapon cannot retain the Backstab surprise branch."),
            new(
                ClassicCombatResolver.PhysicalAttackConcealmentBreakFormulaId,
                "combat_state",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("status_modifier_contract", "Librarium/Objects/combat_spells/combat_spell_records.json", "physical attacks emit the hidden-break signal"),
                    new RuntimeFormulaEvidenceSource("status_modifier_evidence", "Librarium/Objects/evidence_records/catalog.json", "hidden break remains owned by the character visibility subsystem"),
                    new RuntimeFormulaEvidenceSource("live_combat_entry", "RuntimeLiveCombat.cs", "BreakPhysicalAttackConcealment persists concealment removal before engagement")
                },
                "Beginning any physical attack clears persisted hidden and sneaking condition flags before the encounter is admitted. Backstab qualification is evaluated first, so a qualified opening attack is retained for that round but concealment cannot be reused on a later attack."),
            new(
                ClassicCombatResolver.PhysicalSpecialCommandRoutingFormulaId,
                "combat_command",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeCommandDispatcher),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("command_registration", "RuntimeCommandSystem.cs", "smash, backstab, and bs are registered canonical commands"),
                    new RuntimeFormulaEvidenceSource("dispatcher_route", "RuntimeCommandSystem.cs", "RoutesToPolicy forwards smash and backstab to ExecutePolicyCommand"),
                    new RuntimeFormulaEvidenceSource("policy_consumer", "RuntimeLiveGameplayPart2.cs", "ExecutePolicyCommand forwards smash and backstab to live combat"),
                    new RuntimeFormulaEvidenceSource("combat_entry", "RuntimeLiveCombat.cs", "ExecuteLiveCombatCommand selects Smash or Backstab attack types")
                },
                "Smash and Backstab are registered, exposed in help, routed through the main dispatcher, forwarded through the policy facade, and consumed by live combat. A single shared policy-command set prevents registration and dispatcher routing from diverging again."),
            new(
                ClassicCombatResolver.PerfectStealthCommandFormulaId,
                "combat_stealth",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_ability", "Data/Classic/abilities.json", "ability.186 Perfect Stealth is marked implemented"),
                    new RuntimeFormulaEvidenceSource("ability_authority", "RuntimeCharacterAbilityAuthority.cs", "PerfectStealthAbilityId is an executable quest capability"),
                    new RuntimeFormulaEvidenceSource("combat_consumer", "RuntimeLiveCombat.cs", "hidden state qualifies Backstab and physical attacks consume concealment")
                },
                "Hide and Sneak commands are executable without a random roll only for characters owning Ability 186 Perfect Stealth. They persist the authoritative hidden or sneaking conditions consumed by Backstab and cleared by physical attacks. Characters without Perfect Stealth fail closed until the ordinary stealth formula is decoded."),
            new(
                RuntimeClassicStealthResolver.ModifiedStealthFormulaId,
                "combat_stealth",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicStealthResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json", "CalculateStealth: level progression +20 + Fix(Agility/4) + Fix(Intellect/8) + Fix(Charm/6), class/race adjustments, then Ability 27"),
                    new RuntimeFormulaEvidenceSource("decoded_abilities", "Data/Classic/abilities.json", "Abilities 27, 102, and 103 provide Stealth modifier, RaceStealth, and ClassStealth inputs"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "RuntimeClassicStealthResolution.cs", "CalculateModifiedStealth and TryReadModifiedStealth")
                },
                "Modified Stealth is calculated from decoded class/race stealth ownership, level, Agility, Intellect, Charm, and Ability 27. The verified MMUD Explorer formula is consumed directly by ordinary Hide/Sneak and Backstab accuracy."),
            new(
                ClassicCombatResolver.StealthVsPerceptionFormulaId,
                "combat_stealth",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeClassicStealthResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("user_classic_evidence", "Bundle B Revision 30 handoff", "Hide/Sneak compares modified Stealth Rating against modified opponent Perception; failure leaves the actor fully visible and immediately aggros nearby aggressive monsters"),
                    new RuntimeFormulaEvidenceSource("decoded_stealth_ability", "Data/Classic/abilities.json", "Ability 27 provides the decoded Stealth capability input"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "RuntimeLiveGameplayPart2.cs", "ExecuteStealthCommand applies success, full-visibility failure, immediate hostile engagement, and Backstab-ready hidden state")
                },
                "Ordinary Hide and Sneak use one authoritative modified-Stealth versus modified-Perception comparison. Success persists concealment for monster-ignore/Backstab consumers. Failure clears all concealment and immediately engages a nearby hostile NPC. The modified Stealth builder is verified; live resolution now performs the retained fresh percentage roll against each hostile observer. Optional decoded clearance remains an additive adjustment when present."),
            new(
                RuntimeClassicStealthResolver.PercentileRollFormulaId,
                "combat_stealth",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeClassicStealthResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "Librarium/Objects/evidence_records/catalog.json", "Version 1.1 12/24/95: every Hide/Sneak attempt is a new roll and repeated macros converge on the actual percentage chance"),
                    new RuntimeFormulaEvidenceSource("user_classic_evidence", "Bundle B stealth handoff", "Modified Stealth is opposed by modified Perception; failure leaves the actor visible and immediately exposes them to hostile monsters"),
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Objects/native_help/native_help_records.json", "Stealth checks, high Perception opposition, and harder checks in populated rooms"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "RuntimeLiveGameplayPart2.cs", "independent inclusive 1..100 checks against each hostile observer")
                },
                "Every ordinary Hide/Sneak command performs a fresh inclusive percentile roll. Success chance is clamped modified Stealth minus modified Perception (and any decoded clearance adjustment). Each hostile observer checks independently, so additional observers reduce the chance of remaining unseen without a fabricated population multiplier."),
            new(
                RuntimeClassicStealthResolver.CommandAdmissionFormulaId,
                "combat_stealth",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Objects/native_help/native_help_records.json", "Neither sneak nor hide may be used while in combat"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "RuntimeLiveGameplayPart2.cs", "ExecuteStealthCommand rejects Hide/Sneak while a live combat encounter exists")
                },
                "Classic Hide and Sneak are unavailable during combat. The command is rejected before Perfect Stealth, See Hidden, or ordinary percentile resolution runs."),
            new(
                RuntimeClassicStealthResolver.SeeHiddenFormulaId,
                "combat_stealth",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicStealthResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_ability", "Data/Classic/abilities.json", "Ability 57 allows monsters to see hidden players, attack while they hide or sneak, and continue following after escape and re-sneak"),
                    new RuntimeFormulaEvidenceSource("npc_relationships", "Librarium/Objects/evidence_records/catalog.json", "Decoded NPC-to-SeeHidden relationships identify which monsters own the capability"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "RuntimeLiveGameplayPart2.cs", "Detected Hide/Sneak clears concealment, immediately engages, and persists the detecting NPC's target relationship")
                },
                "Ability 57 See Hidden bypasses Hide/Sneak concealment for the owning monster. Detection leaves the player visible, initiates hostile combat, and persists current_target_id/see_hidden_target_id so escaping and re-sneaking does not erase that monster's awareness."),
            new(
                ClassicCombatResolver.IncapacitatedDodgeSuppressionFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimePlayerCombatProfileResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("movement_blocker_contract", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "HoldPerson projects as immobilized movement-only state, distinct from paralysis"),
                    new RuntimeFormulaEvidenceSource("decoded_status_ability", "Data/Classic/abilities.json", "Ability 71 Confusion at +100 is documented as causing 100 percent fumble for stun spells"),
                    new RuntimeFormulaEvidenceSource("decoded_hold_status", "Data/Classic/abilities.json", "Ability 74 HoldPerson says it holds the target in the room and is used by Entangle and Hold Person"),
                    new RuntimeFormulaEvidenceSource("live_profile_consumer", "RuntimePlayerCombatProfiles.cs", "IsAvoidanceSuppressed zeros the final DodgeBonus")
                },
                "Characters explicitly marked stunned, paralyzed, immobilized, or sleeping cannot perform the active Dodge avoidance check. Armor Class and damage resistance remain intact; only the final dodge input is suppressed."),
            new(
                ClassicCombatResolver.IncapacitatedAttackSuppressionFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("status_contracts", "Apps/Source/Reforged.Server/RuntimePlayerCombatProfiles.cs", "stunned/paralyzed/sleeping action-loss states"),
                    new RuntimeFormulaEvidenceSource("live_combat", "Apps/Source/Reforged.Server/RuntimeLiveCombatRounds.cs", "player physical attack admission before NPC retaliation"),
                    new RuntimeFormulaEvidenceSource("bundle_b_audit", "Docs/INCAPACITATED_ATTACK_SUPPRESSION_LEDGER.json")
                },
                "Stunned, paralyzed, and sleeping characters lose their physical attack for the round. Rooted or immobilized characters retain attacks; only Dodge is suppressed for those movement-only states."),
            new(
                "combat.shield_block_roll",
                "combat",
                RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                false,
                "absent_no_runtime_consumer",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_item_catalog", "Data/Classic/items.json", "Shield items expose authored AC/equipment data but no block chance, block strength, or block-roll field"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Librarium/Objects/abilities/ability_records.json", "No Classic ability defines shield block; ability.35 is JumpKick"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer_exhaustion", "Librarium/Objects/evidence_records/catalog.json", "No executable shield-block percentage or ordering calculation was retained"),
                    new RuntimeFormulaEvidenceSource("runtime_reconstruction", "Librarium/Objects/combat_spells/combat_spell_records.json", "Parry/block were explicitly listed as unresolved speculative outcomes, not decoded formulas"),
                    new RuntimeFormulaEvidenceSource("dll_formula_catalog", "Data/Classic/formulas.json", "No separate shield-block formula or branch is present in the promoted Classic formula catalog")
                },
                "After exhausting decoded WCC data, imported databases, MMUD Explorer evidence, item/ability catalogs, DLL-derived formulas, and supporting documents, no separate Classic shield-block roll exists in the available decode. Shields contribute through their authored Armor Class and other explicit item modifiers only."),
            new(
                "combat.parry_roll",
                "combat",
                RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode,
                false,
                "absent_no_runtime_consumer",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_item_catalog", "Data/Classic/items.json", "Weapons expose authored accuracy, damage, speed, type, and explicit abilities but no parry chance or parry-capable flag"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Librarium/Objects/abilities/ability_records.json", "No Classic ability defines a general parry mechanic"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer_exhaustion", "Librarium/Objects/evidence_records/catalog.json", "No executable parry percentage or branch-order calculation was retained"),
                    new RuntimeFormulaEvidenceSource("runtime_reconstruction", "Librarium/Objects/combat_spells/combat_spell_records.json", "Parry/block were explicitly listed as unresolved speculative outcomes, not decoded formulas"),
                    new RuntimeFormulaEvidenceSource("dll_formula_catalog", "Data/Classic/formulas.json", "No separate parry formula or branch is present in the promoted Classic formula catalog")
                },
                "After exhausting decoded WCC data, imported databases, MMUD Explorer evidence, item/ability catalogs, DLL-derived formulas, and supporting documents, no separate Classic parry roll exists in the available decode. Physical avoidance remains hit/AC plus the verified Dodge branch unless later native evidence directly proves another check."),
            new(
                RuntimeLiveGameplayFacade.NpcTargetThreatFormulaId,
                "combat_npc_ai",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcThreatGeneration.cs", "explicit combat admission and direct damage produce deterministic threat ledger entries"),
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcThreat.cs", "highest explicit same-room detectable hostile threat is selected with stable tie ordering"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs", "live combat admission and damage feed the uploaded threat authority; NPC retaliation follows current_target_id")
                },
                "Reforged NPC target selection currently uses the explicit threat ledger: combat admission establishes initial threat, direct damage adds threat, and same-room detectable hostile candidates are ranked deterministically. This runtime path is executable but remains STRONG because the original MajorMUD hate/threat selection and tie-order have not been proven."),
            new(
                RuntimeLiveGameplayFacade.PlayerAssistFormulaId,
                "combat_party",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("command_catalog", "Apps/Source/Reforged.Server/RuntimeCommandSystem.cs", "assist is a supported combat command"),
                    new RuntimeFormulaEvidenceSource("session_authority", "Apps/Source/Reforged.Server/RuntimeSessionManager.cs", "unique same-room actor resolution"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs", "assist joins the ally's exact living NPC encounter and registers explicit threat")
                },
                "Reforged ASSIST joins a uniquely identified same-room ally's active encounter against the same living NPC instance and contributes through the current threat runtime. The command path remains STRONG until original MajorMUD party/assist admission and threat effects are proven."),
            new(
                RuntimeLiveGameplayFacade.CompanionAssistFormulaId,
                "combat_companion",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeLivingWorldService),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcCompanionOwnership.cs", "persistent explicit companion owner and relationship metadata"),
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcCompanionCombatAssist.cs", "same-room owner-authorized hostile target admission"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs", "owned same-room companions receive the owner's current hostile NPC target")
                },
                "Reforged companion assistance uses explicit owner metadata and same-room hostile target admission. This is an evidence-backed deterministic runtime boundary, not a claim that the original MajorMUD companion assist timing/selection has been recovered."),
            new(
                RuntimeLiveGameplayFacade.GuardAssistFormulaId,
                "combat_npc_ai",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLivingWorldService),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_relationship", "Librarium/Objects/evidence_records/catalog.json", "Ability 146 is MonsGuards and its ordered values resolve to monster legacy ids"),
                    new RuntimeFormulaEvidenceSource("native_owner", "Librarium/Objects/parity/parity_records.json", "WCCMMUD _cast_monster_target proves MonsGuards is same-room spell-target substitution, not physical-attack admission or generic assist propagation"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeNpcProtectionAdmission.cs", "Classic spell targeting scans ordered ability-146 values and native room-slot order; generic protector/assist metadata is isolated to non-Classic worlds")
                },
                "Classic MonsGuards is native-verified as ordered same-room spell-target substitution. It does not create a generic combat assistant, threat propagation rule, or physical ATTACK body-block."),
            new(
                RuntimeLiveGameplayFacade.KillRewardAttributionFormulaId,
                "combat_rewards",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_rewards", "Data/Classic/npcs.json", "authored xp and item/currency reward relationships"),
                    new RuntimeFormulaEvidenceSource("live_completion", "Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs", "the character whose resolved attack transitions the living NPC to defeated receives XP and exact loot attribution"),
                    new RuntimeFormulaEvidenceSource("living_world", "Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs", "NPC death clears AI target and creates the single persistent corpse transition")
                },
                "Kill credit belongs to the character whose authoritative combat resolution first defeats the shared living NPC instance. XP, item-loot attribution, and kill provenance are persisted once on that character; other stale encounters fail closed when the living NPC is no longer alive."),
            new(
                "combat.accuracy_ability_22.selection",
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                false,
                "RuntimeCharacterCombatStatisticProjector",
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mudinfo", "MUDInfo topic 3122"),
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "Librarium/Objects/abilities/ability_records.json", "Ability 22 Accuracy"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Librarium/Objects/evidence_records/catalog.json")
                },
                "Highest non-item Ability 22 selection is corroborated; item stacking and category ordering require exact decoded implementation."),
        };

        return new RuntimeClassicFormulaAuthority(records);
    }
}
