import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
server = root / 'Apps/Source/Reforged.Server'
errors = []
checks = []

def require(condition, code):
    if condition:
        checks.append(code)
    else:
        errors.append(code)

selector = (server / 'RuntimeNpcModels.cs').read_text(encoding='utf-8')
cadence = (server / 'RuntimeClassicNpcCombatCadence.cs').read_text(encoding='utf-8')
rounds = (server / 'RuntimeLiveCombatRounds.cs').read_text(encoding='utf-8')
authority = (server / 'RuntimeClassicFormulaAuthority.cs').read_text(encoding='utf-8')
parity_audit = (server / 'RuntimeMajorMudParityAudit.cs').read_text(encoding='utf-8')
powers = (server / 'RuntimeLivePowers.cs').read_text(encoding='utf-8')

require('combat.npc_attack_selection_threshold' in selector, 'threshold-formula-id')
require('random.Next(1, 101)' in selector, 'selection-inclusive-d100')
require('FirstOrDefault(x => roll <= x.SelectionPercent)' in selector, 'ordered-threshold-selection')
require('.Where(x => x.EnergyCost <= currentEnergy)' not in selector and 'x.SelectionPercent > 0 && x.EnergyCost <= currentEnergy' not in selector, 'selection-does-not-prefilter-energy')
require('MaximumDamage >= x.MinimumDamage' not in selector, 'type2-spell-row-not-damage-filtered')
require('EffectiveSpellLegacyId => Type == 2 ? Accuracy : HitSpellLegacyId' in selector, 'type2-direct-spell-precedence')
require('public const int AttemptsPerRound = 6;' in cadence, 'six-attempt-cadence')
require('public const int StockHpRegenRounds = 18;' in cadence, 'stock-mob-hp-regen-18-rounds')
require('checked(carriedEnergy + energyPerRound)' in cadence, 'round-energy-carry')
require('return roll <= attack.MinimumDamage;' in cadence, 'type2-attmin-cast-success')
require('MidpointRounding.ToEven' in cadence, 'vb-half-energy-rounding')
require('npcEnergy = RuntimeClassicNpcCombatCadence.AddRoundEnergy' in rounds and 'NpcEnergyPerRound' in rounds, 'live-round-adds-npc-energy')
require('DeductFailedSpellCost(energyBeforeAttack, attack)' in rounds, 'live-direct-spell-half-cost-on-resist')
require('attack.Type == 2 ? attack.MaximumDamage : spell.SpellLevel' in rounds, 'direct-spell-cast-level-used')
require('attack.Type == 2 && !IsNpcRoomAreaSpell(hitSpell)' in rounds, 'physical-hitspell-no-half-refund')
require('ResolveNpcEncounterStartingEnergy(333) == 0' in rounds, 'encounter-energy-start-zero-test')
require('10059' in powers, 'npc-mana-energy-not-one-round-capped')
require('RuntimeFormulaEvidenceGrade.StronglyCorroborated' in authority and 'external:syntax53/MMUD-Explorer/clsMonsterAttackSim.cls' in authority, 'npc-native-provenance-retained')
regen_pos = authority.find('RuntimeClassicNpcCombatCadence.StockHpRegenIntervalFormulaId')
require(regen_pos >= 0 and 'RuntimeFormulaEvidenceGrade.StronglyCorroborated' in authority[regen_pos:regen_pos+450] and 'false' in authority[regen_pos:regen_pos+450], 'hp-regen-evidence-not-overactivated')
for formula in ['NpcTargetThreatFormulaId', 'PlayerAssistFormulaId', 'CompanionAssistFormulaId', 'GuardAssistFormulaId']:
    pos = authority.find(formula)
    require(pos >= 0 and 'RuntimeFormulaEvidenceGrade.StronglyCorroborated' in authority[pos:pos+350], f'runtime-ai-not-overpromoted:{formula}')
require(('majormud_parity_assertions_0283.json' in parity_audit or 'majormud_parity_assertions_0284.json' in parity_audit or 'majormud_parity_assertions_0285.json' in parity_audit or 'majormud_parity_assertions_0286.json' in parity_audit or 'majormud_parity_assertions_0287.json' in parity_audit or 'majormud_parity_assertions_0288.json' in parity_audit), 'parity-ledger-current-connected')
require('existing.Id.Equals(assertionId' in parity_audit, 'curated-native-override-dedup')

ledger_path = root / 'Librarium/Parity/majormud_parity_assertions_0280.json'
require(ledger_path.exists(), 'ledger-0280-exists')
if ledger_path.exists():
    ledger = json.loads(ledger_path.read_text(encoding='utf-8'))
    by_id = {a['id']: a for a in ledger['assertions']}
    require(ledger.get('update') == '0280', 'ledger-update-0280')
    expected = {
        'npc.attack-row-count': 'STRONG',
        'npc.six-attempt-cadence': 'STRONG',
        'npc.round-energy-carry': 'STRONG',
        'npc.attack-threshold-selection': 'STRONG',
        'npc.direct-spell-field-semantics': 'STRONG',
        'npc.failed-spell-half-energy': 'STRONG',
        'npc.physical-early-round-end': 'STRONG',
        'npc.hp-regen-stock-interval': 'STRONG',
        'character.lives-starting-maximum': 'PASS',
        'character.life-loss-on-death': 'PASS',
        'character.life-gain-on-level': 'STRONG',
        'character.permadeath-zero-lives': 'PASS',
        'native.builder-content-editing': 'STRONG',
        'administrative.sysop-worldgroup-key-boundary': 'STRONG',
    }
    for assertion_id, state in expected.items():
        require(by_id.get(assertion_id, {}).get('state') == state, f'ledger:{assertion_id}:{state}')
    require(by_id.get('character.death-xp-loss-exact', {}).get('state') == 'UNKNOWN', 'death-xp-exact-remains-unknown')
    require(by_id.get('npc.native-ai-semantics', {}).get('state') == 'UNKNOWN', 'broad-native-ai-remains-unknown')

# Validate the retained Librarium attack corpus directly so this pass does not depend on missing generated Data/Classic files.
npc_librarium = root / 'Librarium/Objects/npcs/npc_records_0122.json'
require(npc_librarium.exists(), 'npc-librarium-exists')
if npc_librarium.exists():
    npc_doc = json.loads(npc_librarium.read_text(encoding='utf-8'))
    records = npc_doc.get('records', [])
    spell_rows = []
    over_100_thresholds = []
    abomination = None
    for record in records:
        if str(record.get('name', '')).lower() == 'abomination' and str(record.get('legacy_id')) == '502':
            abomination = record
        for attack in record.get('attacks') or []:
            if int(attack.get('type', 0) or 0) == 2:
                spell_rows.append(attack)
            if int(attack.get('selection_percent', 0) or 0) > 100:
                over_100_thresholds.append(attack)
    row_count_shapes_ok = True
    selected_types_ok = True
    for record in records:
        declared = int((record.get('combat') or {}).get('attack_count', 0) or 0)
        executable = []
        for attack in sorted(record.get('attacks') or [], key=lambda value: int(value.get('slot', 0) or 0)):
            if int(attack.get('slot', 0) or 0) <= 0:
                continue
            fields = ['minimum_damage','maximum_damage','selection_percent','accuracy','hit_spell_legacy_id','energy_cost','hit_message_legacy_id','miss_message_legacy_id','dodge_message_legacy_id']
            if any(int(attack.get(field, 0) or 0) != 0 for field in fields):
                executable.append(attack)
        selected = executable[:declared]
        if len(selected) != declared:
            row_count_shapes_ok = False
        if any(int(attack.get('type', 0) or 0) not in (1, 2) for attack in selected):
            selected_types_ok = False
    require(row_count_shapes_ok, 'declared-attack-row-counts-supported')
    require(selected_types_ok, 'declared-live-attack-types-physical-or-spell')
    require(len(spell_rows) >= 500, 'retained-type2-spell-rows-present')
    require(any(int(row.get('minimum_damage', 0)) > int(row.get('maximum_damage', 0)) for row in spell_rows), 'type2-field-overload-visible-in-data')
    require(len(over_100_thresholds) > 0, 'thresholds-over-100-present')
    require(abomination is not None and [int(a['selection_percent']) for a in abomination.get('attacks', [])[:3]] == [25, 75, 100], 'abomination-authored-threshold-example')

old_formula_ids = [
    'combat.npc_attack_selection_weight',
    'combat.npc_resisted_spell_half_energy_refund',
    'combat.npc_encounter_energy_reset',
]
combined = selector + cadence + rounds + authority
for old in old_formula_ids:
    require(old not in combined, f'old-formula-removed:{old}')

print(json.dumps({'passed': not errors, 'checks': checks, 'errors': errors}, indent=2))
sys.exit(0 if not errors else 1)
