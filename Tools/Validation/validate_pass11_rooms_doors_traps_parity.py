import json
import pathlib
import sys
from collections import Counter

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
server = root / 'Apps/Source/Reforged.Server'
checks = []
errors = []

def require(condition, code):
    if condition:
        checks.append(code)
    else:
        errors.append(code)

# Pass 11 evidence/ledger/runtime-audit wiring.
evidence_path = root / 'Librarium/Parity/majormud_rooms_doors_traps_evidence_0281.json'
ledger_path = root / 'Librarium/Parity/majormud_parity_assertions_0281.json'
parity_path = server / 'RuntimeMajorMudParityAudit.cs'
pass11_audit_path = server / 'RuntimeClassicRoomsDoorsTrapsParityAudit.cs'
require(evidence_path.exists(), 'pass11-evidence-exists')
require(ledger_path.exists(), 'ledger-0281-exists')
require(pass11_audit_path.exists(), 'pass11-csharp-audit-exists')
parity_text = parity_path.read_text(encoding='utf-8')
require(('majormud_parity_assertions_0283.json' in parity_text or 'majormud_parity_assertions_0284.json' in parity_text or 'majormud_parity_assertions_0285.json' in parity_text or 'majormud_parity_assertions_0286.json' in parity_text or 'majormud_parity_assertions_0287.json' in parity_text or 'majormud_parity_assertions_0288.json' in parity_text), 'parity-ledger-current-connected')
require('RuntimeClassicRoomsDoorsTrapsParityAudit.Run(root)' in parity_text, 'pass11-audit-connected-to-unified-parity-run')

if evidence_path.exists():
    evidence = json.loads(evidence_path.read_text(encoding='utf-8'))
    closure = evidence['room_latch_closure']['aggregate_action_gate_closure']
    require(closure['action_gate_count'] == 206, 'action-gate-count-206')
    require(closure['bound_action_gate_count'] == 206, 'action-gates-bound-206')
    require(closure['native_passive_placeholder_count'] == 0, 'zero-native-passive-placeholders')
    require(closure['ordinary_action_semantic_gap_count'] == 0, 'zero-ordinary-action-semantic-gaps')
    require(len(evidence['room_latch_closure']['previous_unresolved_edge_state_ids']) == 7, 'seven-previous-latches-enumerated')
    require(evidence['semantic_exit_corpus']['total_exit_records'] == 61976, 'semantic-exit-total-61976')
    require(evidence['semantic_exit_corpus']['strongly_correlated_active'] == 61975, 'semantic-exits-classified-61975')
    require(evidence['semantic_exit_corpus']['unknown_preserved_safe_runtime'] == 1, 'semantic-exit-one-unknown')
    require(evidence['semantic_exit_corpus']['remaining_unknown']['exit_id'] == 'exit.map12.2248.d', 'remaining-exit-map12-2248-down')
    require(evidence['classic_exit_traps']['total'] == 295, 'exit-trap-total-295')
    require(evidence['classic_exit_traps']['damage_traps'] == 273, 'damage-trap-total-273')
    require(evidence['classic_exit_traps']['spell_traps'] == 22, 'spell-trap-total-22')

# Exact seven-latch command/native-action evidence.
boulder_path = root / 'Librarium/Objects/room_interactions/decoded_map17_boulder_action_records.json'
pedestal_path = root / 'Librarium/Objects/room_interactions/decoded_map17_pedestal_action_records.json'
room_actions_path = root / 'Librarium/Objects/room_interactions/strongly_correlated_room_action_records.json'
for p, code in [(boulder_path,'boulder-evidence-exists'),(pedestal_path,'pedestal-evidence-exists'),(room_actions_path,'sphinx-evidence-exists')]:
    require(p.exists(), code)

if boulder_path.exists():
    doc = json.loads(boulder_path.read_text(encoding='utf-8'))
    records = doc.get('records', [])
    primary = {
        'edge-state.map17.2946.s': [2946,3306,0,1],
        'edge-state.map17.2949.e': [2949,3306,0,2],
        'edge-state.map17.2956.n': [2956,3306,0,0],
        'edge-state.map17.2958.n': [2958,3306,0,0],
        'edge-state.map17.2970.s': [2970,3306,0,1],
    }
    for edge, remote in primary.items():
        candidates = [r for r in records if r.get('bound_edge_state_id') == edge and r.get('command_semantics',{}).get('success_remote_action') == remote]
        require(bool(candidates), f'boulder-remoteaction:{edge}')
        if candidates:
            r=candidates[0]
            semantics=r.get('command_semantics',{})
            require(r.get('commands') == ['move boulder','push boulder'], f'boulder-command-aliases:{edge}')
            require(semantics.get('required_skill') == 'strength' and semantics.get('skill_modifier') == 60, f'boulder-strength60:{edge}')
            require(semantics.get('delay_seconds') == 5 and semantics.get('failure_textblock_id') == 4235, f'boulder-delay-failure:{edge}')
            require(semantics.get('skill_test_model') == 'roll_1_to_100_lte_skill_plus_modifier', f'boulder-roll-model:{edge}')

if pedestal_path.exists():
    records=json.loads(pedestal_path.read_text(encoding='utf-8')).get('records',[])
    expected=[
        ('diamond','item.1921',[3042,0,1,0]),
        ('pearl','item.1923',[3042,0,2,0]),
        ('moonstone','item.1922',[3042,0,3,0]),
        ('marble','item.1924',[3042,0,4,0]),
    ]
    for item,item_id,remote in expected:
        candidates=[r for r in records if r.get('bound_edge_state_id')=='edge-state.map17.3042.n' and r.get('command_semantics',{}).get('required_inventory_item_id')==item_id]
        require(bool(candidates), f'pedestal-item:{item}')
        if candidates:
            sem=candidates[0]['command_semantics']
            require(sem.get('success_remote_action')==remote, f'pedestal-remoteaction:{item}')
            require(sem.get('consume_inventory_item') is True, f'pedestal-consumes:{item}')

if room_actions_path.exists():
    records=json.loads(room_actions_path.read_text(encoding='utf-8')).get('records',[])
    candidates=[r for r in records if r.get('bound_edge_state_id')=='edge-state.map12.2051.u']
    require(bool(candidates), 'sphinx-latch-record')
    if candidates:
        r=candidates[0]
        require(r.get('commands') == ['ask sphinx stars','ask sphinx star','ask sphinx the stars'], 'sphinx-stars-command-aliases')
        require(r.get('command_semantics',{}).get('native_remote_action') == [2051,0,0,8], 'sphinx-remoteaction-2051')

project_status=(root/'Docs/PROJECT_STATUS.md').read_text(encoding='utf-8')
require('all 206/206 action gates with zero native/passive placeholders and zero ordinary action-semantic gaps' in project_status, 'project-status-206-closure')
remote_registry=(server/'WorldgroupRemoteActionDispatchRegistry.cs').read_text(encoding='utf-8')
require('public const uint Entry = 0x0046C573;' in remote_registry, 'native-remoteaction-entry')
require('ModeClearedMasks' in remote_registry and '0x2000' in remote_registry, 'native-remoteaction-mode-mask-table')
living_completion=(server/'RuntimeLivingWorldCompletion.cs').read_text(encoding='utf-8')
require('WorldgroupRemoteActionDispatchRegistry.ModeClearedMasks' in living_completion, 'live-remoteaction-runtime-uses-native-mask-table')

# Re-scan the semantic corpus rather than trusting the evidence summary.
manifest_path=root/'Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0183/exits_decoded_semantic.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
status=Counter(); gates=Counter(); unknown=[]
for rel in manifest.get('record_parts',[]):
    part=json.loads((manifest_path.parent/rel).read_text(encoding='utf-8'))
    records=part if isinstance(part,list) else part.get('records',part.get('exits',[]))
    for r in records:
        status[r.get('semantic_status')]+=1
        gates[r.get('gate_kind')]+=1
        if r.get('semantic_status') != 'strongly_correlated_active':
            unknown.append(r)
require(manifest.get('record_count') == 61976 and sum(status.values()) == 61976, 'semantic-manifest-record-count')
require(status.get('strongly_correlated_active') == 61975, 'semantic-rescan-classified-61975')
require(status.get('unknown_preserved_safe_runtime') == 1 and len(unknown) == 1, 'semantic-rescan-one-unknown')
require(len(unknown)==1 and unknown[0].get('id')=='exit.map12.2248.d', 'semantic-rescan-unknown-id')
require(gates.get('damage_trap')==273 and gates.get('spell_trap')==22, 'semantic-rescan-trap-counts')

# Confirm the one unknown has direct command evidence, but no invented effect promotion.
interaction_path=root/'Librarium/Objects/triggers/mmexplorer_room_interaction_records_0140.json'
interactions=json.loads(interaction_path.read_text(encoding='utf-8'))
records=interactions if isinstance(interactions,list) else interactions.get('records',interactions.get('interactions',[]))
rug=[r for r in records if r.get('relationships',{}).get('from_room')=='room.map12.2248' and r.get('direction')=='D']
require(bool(rug), 'map12-2248-down-interaction-record')
if rug:
    require(rug[0].get('accepted_commands')==['move rug','pull rug'], 'map12-2248-rug-command-aliases')
    sem=rug[0].get('command_semantics',{})
    require(sem.get('state_effect') in {'native_action_result_not_yet_decoded','temporarily_reveals_bound_exit_via_shared_classic_door_timer'}, 'map12-2248-effect-remains-evidence-backed')

# Door exact core audit source.
door_audit=(server/'RuntimeBundleDDoorAudit.cs').read_text(encoding='utf-8')
for token,code in [
    ('every_decoded_door_exit_has_one_live_runtime_definition','door-one-live-definition'),
    ('decoded_classic_doors_begin_closed','door-classic-closed'),
    ('key_qualified_classic_doors_begin_locked','door-key-qualified-locked'),
    ('Open, close, lock, and unlock commands mutate persisted world-object state through RuntimeLivingWorldService.','door-live-state-mutation'),
    ('Picklock and strength success calculations remain evidence-only','door-pick-strength-not-overpromoted')]:
    require(token in door_audit, code)

# Exit trap exact core audit/runtime.
trap_audit=(server/'RuntimeBundleDTrapAudit.cs').read_text(encoding='utf-8')
trap_runtime=(server/'RuntimeClassicExitTraps.cs').read_text(encoding='utf-8')
command_runtime=(server/'RuntimeCommandSystem.cs').read_text(encoding='utf-8')
for token,code in [
    ('definitions.Length == 295','trap-audit-295'),('damage.Length == 273','trap-audit-273'),('spell.Length == 22','trap-audit-22'),
    ('CompletePlayerDefeat(session, character)','trap-lethal-authoritative-death'),('RuntimeSpellExecutionEngine','trap-authoritative-spell-engine')]:
    require(token in (trap_audit+trap_runtime), code)
require('DetectionDifficulty' not in trap_runtime and 'DisarmDifficulty' not in trap_runtime, 'trap-no-invented-detect-disarm')
call='ApplyExitTrap(session, previousRoom, command.Name)'
idx=command_runtime.find(call)
require(idx>=0, 'trap-live-movement-callsite')
if idx>=0:
    # The immediate movement branch preserves departure -> trap -> arrival ordering.
    before=command_runtime[max(0,idx-1000):idx]
    after=command_runtime[idx:idx+1000]
    require('BroadcastDeparture' in before and 'BroadcastArrival' in after, 'trap-order-between-departure-arrival')

# Ledger states: close only what the evidence actually closes.
if ledger_path.exists():
    ledger=json.loads(ledger_path.read_text(encoding='utf-8'))
    require(ledger.get('update')=='0281', 'ledger-update-0281')
    by={a['id']:a for a in ledger.get('assertions',[])}
    expected={
        'native.rooms':'PASS',
        'rooms.exit-semantic-classification':'PASS',
        'rooms.hidden-2248-command-activation':'STRONG',
        'doors.live-state-owner':'PASS',
        'traps.classic-exit-catalog':'PASS',
        'traps.damage-exit-runtime':'PASS',
        'traps.spell-exit-runtime':'PASS',
        'traps.live-movement-order':'STRONG',
        'rooms.unknown-qualified-exits':'UNKNOWN',
        'native.doors-locks-access':'UNKNOWN',
        'native.traps':'UNKNOWN',
        'character.picklocks-stock-formula':'UNKNOWN',
    }
    for assertion_id,state in expected.items():
        require(by.get(assertion_id,{}).get('state')==state, f'ledger:{assertion_id}:{state}')

result={'passed':not errors,'check_count':len(checks)+len(errors),'checks':checks,'errors':errors}
out=root/'Reports/RuntimeVerification/rooms_doors_traps_pass11_static_validation_0281.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
sys.exit(0 if not errors else 1)
