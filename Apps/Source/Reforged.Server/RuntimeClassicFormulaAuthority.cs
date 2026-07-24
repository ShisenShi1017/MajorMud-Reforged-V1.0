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
/// Pass 09 formula authority. This is deliberately separate from the MegaMUD client.
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
            "Librarium/FormulaGapResolution/formula_gap_resolution_index_0077.json",
            "Data/NativeDecodeReports/CodeUpdate0142CharacterStatistics/character_statistics_contract_0142.json",
            "Data/NativeDecodeReports/CodeUpdate0155CharacterStatistics/character_statistics_contract_0155.json",
            "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_CRITICAL.json",
            "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_HIT_AC.json",
            "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-ability-wcc.json"
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
                    new RuntimeFormulaEvidenceSource("native_decode", "Data/NativeDecodeReports/CodeUpdate0142CharacterStatistics/character_statistics_contract_0142.json"),
                    new RuntimeFormulaEvidenceSource("runtime_native_trace", "Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs", "wccmmud.dll calculate_secondary_stats @0x0041a424"),
                    new RuntimeFormulaEvidenceSource("imported_wcc", "ImportedDatabases/WCCDefaultInstall")
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
                    new RuntimeFormulaEvidenceSource("native_decode", "Data/NativeDecodeReports/CodeUpdate0155CharacterStatistics/character_statistics_contract_0155.json"),
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/SourceImports/MMExplorer"),
                    new RuntimeFormulaEvidenceSource("imported_ability_catalog", "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-ability-wcc.json")
                },
                "Only explicit equipment and ability modifiers are executable; unknown base-stat ordering remains quarantined."),
            new(
                ClassicCombatResolver.ClassCombatAccuracyFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimePlayerCombatProfileResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_class_catalog", "Data/Classic/character_foundation.json", "classes[].combat_level and public combat_bonus projection"),
                    new RuntimeFormulaEvidenceSource("imported_wcc", "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-class-wcc.json", "combat_bonus per class"),
                    new RuntimeFormulaEvidenceSource("bundle_a_handoff", "Apps/Source/Reforged.Server/RuntimeCharacterBundleCatalog.cs", "class_combat_level_accuracy")
                },
                "Decoded class combat rating now contributes its per-level innate physical accuracy before ability and weapon accuracy modifiers in the single live Classic combat path."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.StrengthMaximumDamageFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "MMUDExplorer/MMUD-Explorer-master/frmMain.frm", "line 27694: Fix((Strength - 50) / 10), explicitly matching MajorMUD"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "retained MMUD Explorer source line and provenance"),
                    new RuntimeFormulaEvidenceSource("item_schema", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/ITEM_FIELD_SCHEMA.json", "item authoring warning confirms Strength maximum-damage bonus is a distinct component that must be included")
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
                    new RuntimeFormulaEvidenceSource("mmexplorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas", "lines 4663-4678: weapon energy formula"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm", "lines 1995-2006: independent matching implementation"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "retained source formula and provenance")
                },
                "Player attack energy is Fix((weaponSpeed * 1000) / Fix((((level * (combatCode + 2)) + 45) * (agility + 150)) / 6)). The value is projected into the authoritative player combat profile for timing consumption."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.PlayerAttackSchedulingFormulaId,
                "combat_timing",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmexplorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas", "lines 1587-1593: swings = 1000 / energy, capped by MAX_SWINGS=5"),
                    new RuntimeFormulaEvidenceSource("mmexplorer", "MMUDExplorer/MMUD-Explorer-master/frmSwingCalc.frm", "lines 2006-2041: 1000/energy swing calculation and cap"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm", "independent matching 1000/energy swing model"),
                    new RuntimeFormulaEvidenceSource("librarium", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_ATTACK_ENERGY.json", "high-confidence verified attack-energy/action loop")
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
                    new RuntimeFormulaEvidenceSource("mmexplorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas", "lines 1564-1595: Backstab/Smash force 1000 energy and one swing; Bash doubles calculated energy; all attacks retain the five-swing cap"),
                    new RuntimeFormulaEvidenceSource("dll_native_trace", "Evidence/Combat/DLLCombatResolutionOrder_0130.md", "Bash -15 accuracy, +10% pre-roll, x3 after DR; Smash -25 accuracy, +20% pre-roll, x5 after DR; criticals disabled and Dodge remains last"),
                    new RuntimeFormulaEvidenceSource("native_help", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMHLP.MSG", "Bash is a heavy inaccurate attack available to any class; Smash is ability-gated"),
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
                    new RuntimeFormulaEvidenceSource("mmexplorer", "MMUDExplorer/MMUD-Explorer-master/frmMain.frm", "lines 27917-27951: level, Intellect, Agility, and Charm components; natural cap 75 and minimum 1 before later modifiers"),
                    new RuntimeFormulaEvidenceSource("mudinfo", "MajorMUD Critical Hit Chance Formula", "Fix(Level/10) + Fix((Intellect-50)/10) + Fix((Agility-50)/20) + Fix((Charm-50)/30), capped 1..75"),
                    new RuntimeFormulaEvidenceSource("native_help", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMHLP.MSG", "Intellect is explicitly a component of Critical Hit Chance"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "retained MMUD Explorer calculation sequence and cap provenance")
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
                    new RuntimeFormulaEvidenceSource("mmexplorer", "Librarium/FormulaGapResolution/formula_gap_resolution_index_0077.json", "retained Classic percentage-roll behavior"),
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
                    new RuntimeFormulaEvidenceSource("formula_roll_convention", "Librarium/FormulaGapResolution/formula_gap_resolution_index_0077.json", "Classic percentage checks use inclusive 1 through 100 boundaries"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/ClassicCombatResolver.cs", "inclusive 1..100 dodge roll")
                },
                "Final Dodge chance uses an inclusive 1 through 100 roll and succeeds when the roll is less than or equal to the clamped Dodge percentage."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.BackstabDamageFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer_formula", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas", "CalcBSDamage lines 4196-4198"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer_consumers", "MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm", "minimum and maximum endpoint calls lines 1123-1124"),
                    new RuntimeFormulaEvidenceSource("librarium_evidence", "Evidence/FormulaBehavior0072/candidate_references_0072.json", "retained formula and Classic level-scaling branch")
                },
                "Backstab minimum and maximum damage are calculated independently from level, modified Stealth, the corresponding normal damage endpoint, and the decoded Backstab endpoint bonus, then scaled by (level + 100) percent for Classic."),
            new(
                RuntimeClassicPhysicalFormulaCalculator.CriticalDamageRangeFormulaId,
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(RuntimeClassicPhysicalFormulaCalculator),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Evidence/native_message_catalog_correlation_0022.json", "critical hits do up to four times normal damage"),
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
                    new RuntimeFormulaEvidenceSource("uploaded_mmexplorer_evidence", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_CRITICAL.json", "retained MMUD Explorer critical calculation provenance"),
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMUD.NOT", "critical formulas reworked; normal critical percentage remains distinct from Quick-and-Deadly"),
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
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMUD.NOT", "Version 1.11j: critical hit is determined only after a hit has been determined"),
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
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMUD.NOT", "most Mystic forms no longer give critical bonuses; critical changes are authored through stats and abilities")
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
                    new RuntimeFormulaEvidenceSource("mmexplorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas", "lines 4564-4566: (200 - EU) + Fix((AGL - 50) / 10), cap 20, halve at encumbrance >= 33"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm", "lines 2064-2067: independent matching implementation"),
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMUD.NOT", "Quick and Deadly critical bonus capped and tied to attack speed"),
                    new RuntimeFormulaEvidenceSource("librarium_import", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "retained source formula and provenance")
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
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("generated_item_catalog", "Data/Classic/items.json", "weapon minimum/maximum damage and damage resistance fields"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_CRITICAL.json", "physical range, critical range, and post-roll DR"),
                    new RuntimeFormulaEvidenceSource("runtime_authority", "Apps/Source/Reforged.Server/ClassicCombatResolver.cs", "one roll, DR subtraction, attack-type scaling, and non-negative final damage")
                },
                "Weapon damage range, decoded attack-type scaling, one inclusive range roll, post-roll damage resistance, and non-negative final damage execute through ClassicCombatResolver."),
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
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_HIT_AC.json"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer"),
                    new RuntimeFormulaEvidenceSource("nightmare_redux", "ImportedDatabases/OmniVerseReference"),
                    new RuntimeFormulaEvidenceSource("gap_resolution", "Librarium/FormulaGapResolution/formula_gap_resolution_index_0077.json")
                },
                "The decoded and independently corroborated hit/AC equation, 10..99 clamp, inclusive d100 roll, and live resolver are promoted as one authoritative path."),
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
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_CRITICAL.json"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer"),
                    new RuntimeFormulaEvidenceSource("gap_resolution", "Librarium/FormulaGapResolution/formula_gap_resolution_index_0077.json")
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer"),
                    new RuntimeFormulaEvidenceSource("imported_ability_catalog", "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-ability-wcc.json")
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer", "CalcResistedDamage consumers"),
                    new RuntimeFormulaEvidenceSource("imported_ability_catalog", "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-ability-wcc.json", "AntiMagic ability 51")
                },
                "Equation is strongly corroborated; runtime activation waits for decoded rounding, caps, and anti-magic branch ordering."),
            new(
                "combat.dodge.native",
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mudinfo", "MUDInfo topic 2624"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer/clsMonsterAttackSim.cls"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_HIT_AC.json")
                },
                "Decoded dodge scaling, 95 cap, backstab reduction, and inclusive roll boundary execute through the authoritative ClassicCombatResolver."),
            new(
                "combat.dodge_base",
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mudinfo", "MUDInfo topic 2624"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer/clsMonsterAttackSim.cls")
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
                    new RuntimeFormulaEvidenceSource("decoded_ability_catalog", "GeneratedData/Classic/abilities.json", "Ability 34 Dodge"),
                    new RuntimeFormulaEvidenceSource("character_runtime", "RuntimeCharacterQuestAbilities.cs")
                },
                "Decoded Ability 34 is promoted into the defender DodgeBonus before the authoritative avoidance check."),
            new(
                "combat.dodge_final",
                "combat",
                RuntimeFormulaEvidenceGrade.StronglyCorroborated,
                true,
                nameof(ClassicCombatResolver),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer/clsMonsterAttackSim.cls"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/COMBAT_HIT_AC.json")
                },
                "The final avoidance stage applies the 95 percent cap and the distinct Backstab reduction before the inclusive avoidance roll."),
            new(
                "combat.npc_attack_count",
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "combat.attack_count"),
                    new RuntimeFormulaEvidenceSource("runtime_npc_model", "RuntimeNpcModels.cs", "RuntimeNpcDefinition.AttackCount"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcAttacksPerRound")
                },
                "The decoded NPC attack_count controls the exact number of independently selected physical attacks executed during each live NPC combat round. A zero value performs no NPC attacks, invalid negative values are rejected, positive counts require decoded attack rows, and execution stops immediately when the player reaches zero hit points."),
            new(
                "combat.npc_attack_energy",
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "combat.energy and attacks.energy_cost"),
                    new RuntimeFormulaEvidenceSource("runtime_npc_model", "RuntimeNpcModels.cs", "RuntimeNpcDefinition.Energy and RuntimeNpcAttackDefinition.EnergyCost"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "SelectNpcAttack and ResolveNpcEnergyAfterAttack")
                },
                "The decoded NPC energy pool gates attack-row eligibility. Each selected attack deducts its decoded energy_cost, unaffordable rows are excluded, and combat performs no NPC attack when no decoded row is affordable. No energy regeneration is introduced because its timing remains unresolved."),
            new(
                ClassicCombatResolver.NpcResistedSpellEnergyRefundFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_spell_catalog", "Data/Classic/spells.json", "formula_refs contains formula.npc.spell_half_energy_refund"),
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/CombatSpellReconstruction/canonical_combat_spell_catalog_0184.json", "NPC hit-spell energy metadata"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcEnergyAfterResistedHitSpell")
                },
                "When a triggered NPC hit spell is fully resisted, half of the selected attack row's decoded energy_cost is refunded after the original cost is paid. Integer division truncates odd costs and the result is capped at the NPC encounter maximum energy."),
            new(
                ClassicCombatResolver.NpcEncounterEnergyResetFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "combat.energy"),
                    new RuntimeFormulaEvidenceSource("encounter_constructor", "RuntimeLiveCombat.cs", "ResolveNpcEncounterStartingEnergy"),
                    new RuntimeFormulaEvidenceSource("combat_lifecycle", "RuntimeLiveCombat.cs", "new encounter starts from decoded maximum; ended encounters are discarded")
                },
                "Each newly admitted combat encounter starts the NPC energy pool at the decoded combat.energy maximum. Flee, defeat, or invalidation discards the encounter state, so a later encounter resets to the decoded maximum without inventing in-combat regeneration."),
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
                "Decoded monster currency fields are denomination amounts rather than drop percentages. The live death consumer preserves copper, silver, gold, and platinum independently, creates no fabricated fallback money for zero-value NPCs, and leaves decoded runic handling quarantined until its wallet/value contract is resolved."),
            new(
                ClassicCombatResolver.NpcAttackSelectionWeightFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeNpcAttackSelector),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("decoded_npc_catalog", "Data/Classic/npcs.json", "attacks.selection_percent values frequently total above 100 within one NPC record"),
                    new RuntimeFormulaEvidenceSource("runtime_npc_model", "RuntimeNpcModels.cs", "RuntimeNpcAttackDefinition.SelectionPercent"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "SelectNpcAttack delegates to RuntimeNpcAttackSelector")
                },
                "Decoded NPC selection_percent values are positive relative weights, not independent absolute percentages. Invalid or unaffordable rows are removed first; the remaining rows are ordered by slot and selected by one inclusive 1..sum(weights) draw. Energy filtering therefore re-normalizes the eligible rows without changing authored weights."),
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
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/CombatSpellReconstruction/canonical_combat_spell_catalog_0184.json", "raw spell type, target, attack, effect, duration, resistance, and difficulty fields"),
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
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/CombatSpellReconstruction/canonical_combat_spell_catalog_0184.json", "spell_type_raw, target_type_raw, attack_type_raw, resist_type_raw"),
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
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/CombatSpellReconstruction/canonical_combat_spell_catalog_0184.json", "attack_type_raw correlated with spell names"),
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
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/CombatSpellReconstruction/canonical_combat_spell_catalog_0184.json", "minimum_effect and maximum_effect"),
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas", "CalculateResistDamage documents 0-never, 1-antimagic only, 2-everyone"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls", "IsSpellResisted branch for TypeOfResists"),
                    new RuntimeFormulaEvidenceSource("knowledge_base", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "retained TypeOfResists implementation excerpts"),
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
                    new RuntimeFormulaEvidenceSource("classic_research", "Evidence/Omniverse/ClassicResearch/Combat/2026-07-07-CombatSpellBehavior/screenshots/04-spell-resistance-formula.jpg", "MR=(WIL+INT*3)/4+MagicRes; resist chance MR/2 capped at 97 percent"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer", "IsSpellResisted and CalcResistedDamage consumers corroborate resistance gating"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveNpcHitSpellResistance")
                },
                "For eligible NPC hit spells, target magic resistance is integer-truncated as (Willpower + Intellect*3)/4 plus the resolved MagicRes value. Full-resist chance is integer-truncated MR/2, clamped from 0 through 97 percent, and succeeds on an inclusive 1-100 roll. Resistance type 0 bypasses the check; type 1 requires Ability 51 Anti-Magic; type 2 checks everyone. Partial damage reduction and resisted energy cost remain separately unresolved."),
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
                    new RuntimeFormulaEvidenceSource("retained_spell_clusters", "Librarium/CombatSpellReconstruction/canonical_combat_spell_catalog_0184.json", "type 0 immediate attacks, type 1 poison delivery, type 3 status/scripted records"),
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
                    new RuntimeFormulaEvidenceSource("canonical_spell_catalog", "Librarium/CombatSpellReconstruction/canonical_combat_spell_catalog_0184.json", "spell type, target scope, attack element, and effect endpoints"),
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
                    new RuntimeFormulaEvidenceSource("monster_resistance_inventory", "Simulator/DataSimVer/0008/MonsterAbilityInventory.csv", "positive, negative, 100, and greater-than-100 elemental resistance values across Classic monsters"),
                    new RuntimeFormulaEvidenceSource("retained_simulators", "Librarium/MajorMUDKnowledgeBase/MechanicsFull/ITEM_RESISTANCES.md", "resistance and vulnerability damage projections"),
                    new RuntimeFormulaEvidenceSource("live_combat_round", "RuntimeLiveCombatRounds.cs", "ResolveElementalSpellDamage")
                },
                "Cold, fire, stone, and lightning resistance values modify immediate elemental spell damage as a percentage. Positive values reduce damage, negative values increase damage, and values at or above 100 suppress damage. Integer division truncates toward zero. Acid and untyped damage remain unchanged because no corresponding decoded player resistance field is established."),
            new(
                ClassicCombatResolver.NpcHitSpellPoisonImmunityFormulaId,
                "combat_npc",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("ability_catalog", "Data/Classic/abilities.json", "Ability 21 ImmuPoison"),
                    new RuntimeFormulaEvidenceSource("spell_ability_evidence", "Evidence/DataSimVer0015/SpellStatusEvidenceExtract.csv", "ImmuPoison applied by Classic spells and effects"),
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
                    new RuntimeFormulaEvidenceSource("poison_evidence", "Evidence/DataSimVer0015/SpellStatusEvidenceExtract.csv", "Poison, cure poison, duration, and immunity relationships"),
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
                "NPC-delivered poison and status effects use decoded duration in six-second Classic combat rounds. Expired effects are removed on restore, power commands, and automatic ticks; character projections are synchronized and persisted. Target death removes all remaining active effects."),
            new(
                ClassicCombatResolver.NpcHitSpellCurePoisonFormulaId,
                "combat_status",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("spell_catalog", "Data/Classic/spells.json", "spell.31 Antidote and spell.222 Cure Poison"),
                    new RuntimeFormulaEvidenceSource("native_spell_text", "Data/NativeDecodeReports/CodeUpdate0139RoomTextAudit/WCCSPELS_block_samples_512.json", "Cure Poison removes all poison from the target's system"),
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
                    new RuntimeFormulaEvidenceSource("server_tick", "Apps/Source/Reforged.Server/ServerHostConfigurationAudit.cs", "Classic combat tick defaults to 6000 milliseconds"),
                    new RuntimeFormulaEvidenceSource("decoded_spell_effect", "Data/Classic/spells.json", "Poison and periodic damage spells provide authored effect ranges and durations"),
                    new RuntimeFormulaEvidenceSource("runtime_consumer", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "Inclusive periodic magnitude roll once per combat round")
                },
                "Periodic poison and decoded duration damage execute once per six-second Classic combat round using an inclusive authored effect-range roll. Missed ticks are caught up deterministically and stop at expiration or target death."),
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
                    new RuntimeFormulaEvidenceSource("runtime_consumer", "Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs", "Decoded poison cures, explicit spell removals, and general magical dispel")
                },
                "Cure Poison removes all active poison effects. RemovesSpell removes the authored linked spell. General DispellMagic removes magical effects while preserving effects marked NonMagical."),
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas:2111-2132", "Stock: Fix((Stealth + Agility) / 2) + Fix(PlusBSAccu / 2) + class/race stealth adjustment + normal accuracy bonuses"),
                    new RuntimeFormulaEvidenceSource("librarium", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "Retained CalculateBackstabAccuracy source and call sites")
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas:1980-2140", "Stock NPC Backstab defense is AC/4 + Backstab Defense, or full AC + Backstab Defense for See Hidden targets; Dodge is checked last at one-fifth strength"),
                    new RuntimeFormulaEvidenceSource("native_help", "Librarium/Evidence/native_message_catalog_correlation_0022.json", "High Perception or AC makes a target difficult to Backstab; generated NPC Backstab Defense is the dedicated DV input"),
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
                    new RuntimeFormulaEvidenceSource("status_modifier_contract", "Librarium/CombatSpellReconstruction/combat_status_modifiers_0171.json", "physical attacks emit the hidden-break signal"),
                    new RuntimeFormulaEvidenceSource("status_modifier_evidence", "Evidence/CombatSpellReconstruction/combat_status_modifiers_evidence_0171.json", "hidden break remains owned by the character visibility subsystem"),
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas", "CalculateStealth: level progression +20 + Fix(Agility/4) + Fix(Intellect/8) + Fix(Charm/6), class/race adjustments, then Ability 27"),
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
                    new RuntimeFormulaEvidenceSource("wcc_release_notes", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMUD.NOT", "Version 1.1 12/24/95: every Hide/Sneak attempt is a new roll and repeated macros converge on the actual percentage chance"),
                    new RuntimeFormulaEvidenceSource("user_classic_evidence", "Bundle B stealth handoff", "Modified Stealth is opposed by modified Perception; failure leaves the actor visible and immediately exposes them to hostile monsters"),
                    new RuntimeFormulaEvidenceSource("native_help", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMHLP.MSG", "Stealth checks, high Perception opposition, and harder checks in populated rooms"),
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
                    new RuntimeFormulaEvidenceSource("native_help", "ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMHLP.MSG", "Neither sneak nor hide may be used while in combat"),
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
                    new RuntimeFormulaEvidenceSource("npc_relationships", "Librarium/Validation0003/monster_ability_relationships_verified_0003.json", "Decoded NPC-to-SeeHidden relationships identify which monsters own the capability"),
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
                    new RuntimeFormulaEvidenceSource("movement_blocker_contract", "Apps/Source/Reforged.Server/RuntimeCombatMovement.cs", "Stunned, Sleeping, and Paralyzed are explicit incapacitating movement blockers"),
                    new RuntimeFormulaEvidenceSource("decoded_status_ability", "Data/Classic/abilities.json", "Ability 71 Confusion at +100 is documented as causing 100 percent fumble for stun spells"),
                    new RuntimeFormulaEvidenceSource("decoded_hold_status", "Data/Classic/spells.json", "Ability 74 HoldPerson relationships identify immobilizing combat statuses"),
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer_exhaustion", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "No executable shield-block percentage or ordering calculation was retained"),
                    new RuntimeFormulaEvidenceSource("runtime_reconstruction", "Librarium/CombatSpellReconstruction/physical_weapon_attack_execution_0160.json", "Parry/block were explicitly listed as unresolved speculative outcomes, not decoded formulas"),
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
                    new RuntimeFormulaEvidenceSource("mmud_explorer_exhaustion", "Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json", "No executable parry percentage or branch-order calculation was retained"),
                    new RuntimeFormulaEvidenceSource("runtime_reconstruction", "Librarium/CombatSpellReconstruction/physical_weapon_attack_execution_0160.json", "Parry/block were explicitly listed as unresolved speculative outcomes, not decoded formulas"),
                    new RuntimeFormulaEvidenceSource("dll_formula_catalog", "Data/Classic/formulas.json", "No separate parry formula or branch is present in the promoted Classic formula catalog")
                },
                "After exhausting decoded WCC data, imported databases, MMUD Explorer evidence, item/ability catalogs, DLL-derived formulas, and supporting documents, no separate Classic parry roll exists in the available decode. Physical avoidance remains hit/AC plus the verified Dodge branch unless later native evidence directly proves another check."),
            new(
                RuntimeLiveGameplayFacade.NpcTargetThreatFormulaId,
                "combat_npc_ai",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcThreatGeneration.cs", "explicit combat admission and direct damage produce deterministic threat ledger entries"),
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcThreat.cs", "highest explicit same-room detectable hostile threat is selected with stable tie ordering"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs", "live combat admission and damage feed the uploaded threat authority; NPC retaliation follows current_target_id")
                },
                "NPC target selection uses the uploaded explicit threat ledger. Combat admission establishes initial threat, direct damage adds exact threat, same-room detectable living hostile candidates are ranked by descending explicit threat, and only the selected target receives NPC retaliation."),
            new(
                RuntimeLiveGameplayFacade.PlayerAssistFormulaId,
                "combat_party",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLiveGameplayFacade),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("command_catalog", "Apps/Source/Reforged.Server/RuntimeCommandSystem.cs", "assist is a supported combat command"),
                    new RuntimeFormulaEvidenceSource("session_authority", "Apps/Source/Reforged.Server/RuntimeSessionManager.cs", "unique same-room actor resolution"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs", "assist joins the ally's exact living NPC encounter and registers explicit threat")
                },
                "Assist joins a uniquely identified same-room ally's active encounter against the same living NPC instance. The assisting player receives an independent action-energy state and contributes explicit threat through normal attacks."),
            new(
                RuntimeLiveGameplayFacade.CompanionAssistFormulaId,
                "combat_companion",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLivingWorldService),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcCompanionOwnership.cs", "persistent explicit companion owner and relationship metadata"),
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcCompanionCombatAssist.cs", "same-room owner-authorized hostile target admission"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs", "owned same-room companions receive the owner's current hostile NPC target")
                },
                "A living NPC explicitly owned by the attacking character is authorized to assist only in the same room against the owner's live hostile target. Ownership and target relationships persist through the existing living-world metadata authority."),
            new(
                RuntimeLiveGameplayFacade.GuardAssistFormulaId,
                "combat_npc_ai",
                RuntimeFormulaEvidenceGrade.Verified,
                true,
                nameof(RuntimeLivingWorldService),
                new[]
                {
                    new RuntimeFormulaEvidenceSource("uploaded_runtime", "Apps/Source/Reforged.Server/RuntimeNpcAssist.cs", "explicit same-room caller and hostile target admission"),
                    new RuntimeFormulaEvidenceSource("uploaded_metadata", "Runtime living NPC metadata", "assist_target_instance_id, guard_target_instance_id, protected_npc_instance_id, or shared assist_group_id"),
                    new RuntimeFormulaEvidenceSource("live_consumer", "Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs", "explicitly linked same-room NPCs acquire the attacked character through the uploaded assist authority")
                },
                "Guard and allied-NPC assistance executes only from explicit decoded/runtime relationship metadata. Same-room linked NPCs adopt the already observed hostile character target; no name-based, faction-inferred, or radius-based guard behavior is fabricated."),
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
                    new RuntimeFormulaEvidenceSource("imported_ability_catalog", "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-ability-wcc.json", "Ability 22 Accuracy"),
                    new RuntimeFormulaEvidenceSource("mmud_explorer", "Evidence/MMUDExplorer")
                },
                "Highest non-item Ability 22 selection is corroborated; item stacking and category ordering require exact decoded implementation."),
        };

        return new RuntimeClassicFormulaAuthority(records);
    }
}
